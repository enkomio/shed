namespace ES.Shed

open System
open System.Collections.Generic
open System.Threading
open Microsoft.Samples.Debugging.MdbgEngine
open Microsoft.Samples.Debugging.CorDebug
open System.Runtime.InteropServices
open Microsoft.Samples.Debugging.CorDebug.NativeApi

type DebuggerErrors =
    | UnknownError
    | InternalException of String

type BreakpointExpression = {
    Assembly: String
    Type: String
    Method: String
}

type EvaluationExpression = { 
    Assembly: String
    Type: String
    Method: String
    Property: String
}

type ExtractionExpression = {
    Breakpoint: BreakpointExpression
    Evaluation: EvaluationExpression
}

type Debugger() =
    let _engine = new MDbgEngine()
    let _loadedAssemblies = new List<Byte array>()
    let mutable _managedProcess : MDbgProcess option = None      
    
    let isAlreadyDebugged(pid: Int32) =        
        _engine.Processes
        |> Seq.cast<MDbgProcess>
        |> Seq.exists(fun p -> pid = p.CorProcess.Id)
        
    let setBreakpoint(proc: MDbgProcess, expression: BreakpointExpression, waitForHit: Boolean) =
        let breakpointLocation = new BreakpointFunctionLocation(expression.Assembly, expression.Type, expression.Method, 0)        
        proc.Breakpoints.CreateBreakpoint(breakpointLocation) |> ignore
        
        if waitForHit then
            proc.Go().WaitOne() |> ignore
            Thread.Sleep(500)
            proc.Go().WaitOne() |> ignore

    let setOnAssemblyLoadedHandler(proc: MDbgProcess) =
        let breakpointExpr = {
            Assembly = "mscorlib";
            Type = "System.Reflection.Assembly";
            Method = "Load"
        }
        setBreakpoint(proc, breakpointExpr, false)
        
        proc.CorProcess.OnBreakpoint.Add(fun (e: CorBreakpointEventArgs) -> 
            let currentFunc = proc.Threads.Active.CurrentFrame.Function.FullName
            if currentFunc.Equals("System.Reflection.Assembly.Load", StringComparison.OrdinalIgnoreCase) then                
                // all the methods that we are interested in, have as first
                // argument a byte array with the assembly to load
                let corValue = e.Thread.ActiveFrame.GetArgument(0)
                if corValue.Type = CorElementType.ELEMENT_TYPE_SZARRAY then
                    let corRefValue = corValue.CastToReferenceValue()            
                    let corArrayValue = corRefValue.Dereference().CastToArrayValue()
                    if corArrayValue.Count > 0 then                        
                        let buffer =
                            Seq.init 
                                corArrayValue.Count
                                (fun i -> corArrayValue.GetElementAtPosition(i))
                            |> Seq.map(fun cv ->cv.CastToGenericValue().GetValue())
                            |> Seq.cast<Byte>
                            |> Seq.toArray
                        _loadedAssemblies.Add(buffer)
        )

    let runProgram(program: String) =        
        let proc = _engine.CreateProcess(program, String.Empty, DebugModeFlag.Debug, null)  
        setOnAssemblyLoadedHandler(proc)
        // wait for the entry breakpoint to hit        
        proc.Go().WaitOne() |> ignore
        proc

    let attachToPid(pid: Int32) =
        let clrVersion = MdbgVersionPolicy.GetDefaultAttachVersion(pid)
        let proc = _engine.Attach(pid, null, clrVersion)
        proc.Go().WaitOne() |> ignore
        Thread.Sleep(500)
        proc        

    let createEvalExpression(proc: MDbgProcess, expression: EvaluationExpression) =        
        let appDomain = proc.Threads.Active.CorThread.AppDomain
        let scope = String.Format("{0}!{1}.{2}", expression.Assembly, expression.Type, expression.Method)
        let func = proc.ResolveFunctionNameFromScope(scope, appDomain);        
        let eval = proc.Threads.Active.CorThread.CreateEval()
        (eval, func)

    let executeEvaluationExpression(eval: CorEval, funcs: List<MDbgFunction>, mdbgProcess: MDbgProcess, expression: EvaluationExpression) =
        let mutable result: String option = None
        
        // set arguments
        let args = Array.zeroCreate<CorValue>(1)
        let var = mdbgProcess.ResolveVariable(expression.Property, mdbgProcess.Threads.Active.CurrentFrame)
        let corValue = var.CorValue
        let hv = corValue.CastToHeapValue()
        if hv <> null then
            args.[0] <- hv :> Object :?> CorValue
        else
            args.[0] <- corValue

        let func = funcs.[0]
        eval.CallFunction(func.CorFunction, args)
        mdbgProcess.Go().WaitOne() |> ignore

        if mdbgProcess.StopReason :? EvalCompleteStopReason then
            let completedEval = (mdbgProcess.StopReason :?> EvalCompleteStopReason).Eval
            let cv = completedEval.Result
            if cv <> null then
                let mv = new MDbgValue(mdbgProcess, cv)
                result <- Some <| mv.GetStringValue(1)
        result

    let extract(proc: MDbgProcess, expression: ExtractionExpression) =
        setBreakpoint(proc, expression.Breakpoint, true)
                
        // now the break should have hit, let evaluate our expression due to the breakpoint
        let (eval, funcs) = createEvalExpression(proc, expression.Evaluation)
                               
        // exec eval
        executeEvaluationExpression(eval, funcs, proc, expression.Evaluation)

    member this.GetLoadedAssemblies() =
        _loadedAssemblies 
        |> Seq.readonly
        |> Seq.toArray

    member this.Run(milliseconds: Int32) =
        _managedProcess.Value.Go().WaitOne(milliseconds) |> ignore

    member this.Extract(expression: ExtractionExpression) =
        let mutable result: Result<String, DebuggerErrors> = Error UnknownError
        try            
            match extract(_managedProcess.Value, expression) with
            | Some textResult -> result <- Ok textResult
            | None -> ()
        with | :? COMException as e ->
            result <- Error(InternalException(e.ToString()))
        
        result

    member this.Kill() =
        _managedProcess.Value.Breakpoints        
        |> Seq.cast<MDbgBreakpoint>
        |> Seq.toList
        |> List.iter(fun breakpoint -> breakpoint.Delete())
        _managedProcess.Value.Kill().WaitOne() |> ignore

    member this.Attach(pid: Int32) =
        if not(isAlreadyDebugged(pid)) then
            _managedProcess <- Some <| attachToPid(pid)
            setOnAssemblyLoadedHandler(_managedProcess.Value)
            true
        else
            false

    member this.Start(program: String) =
        _managedProcess <- Some <| runProgram(program)        
        _managedProcess.Value.CorProcess.Id