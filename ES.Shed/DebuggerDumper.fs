namespace ES.Shed
(*
open System
open System.IO
open System.Reflection
open System.Text.RegularExpressions

type DebuggerDumper(settings: HandlerSettings) =
    let mutable _debugger: Debugger option = None    
    let (_, info, _, error) = createLoggers(settings.MessageBus)

    let parseEvaluationExpression(evaluationLine: String) =
        let evaluationRegex = 
            new Regex(@"^" +
                @"([^\!]+\!)?" + // optional module
                @"((?:[^.*+:<> ]+\.)*)" +  // optional class
                @"([^*+:<>\d ][^*+:<> ]*)" +  // method
                @" " + 
                @"([^*+:<>\d ]+)" + // property
                @"$"
            )

        let m = evaluationRegex.Match(evaluationLine)
        if m.Success then
            Some {
                Assembly = m.Groups.[1].Value.TrimEnd('!')
                Type = m.Groups.[2].Value.TrimEnd('.')
                Method = m.Groups.[3].Value
                Property = m.Groups.[4].Value
            }
        else
            None

    let parseBreakpointException(breakpointLine: String) =
        let breakpointRegex = 
            new Regex(@"^" +
                @"([^\!]+\!)?" + // optional module
                @"((?:[^.*+:<> ]+\.)*)" +  // optional class
                @"([^*+:<>\d ][^*+:<> ]*)" +  // method
                @"$"
            )

        let m = breakpointRegex.Match(breakpointLine)
        if m.Success then
            Some {
                Assembly = m.Groups.[1].Value.TrimEnd('!')
                Type = m.Groups.[2].Value.TrimEnd('.')
                Method = m.Groups.[3].Value
            }
        else
            None

    let parseExpression(lines: String array) =
        if lines.Length >= 2 then
            let breakpoint = parseBreakpointException(lines.[0])
            let evaluationLine = parseEvaluationExpression(lines.[1])
            match (breakpoint, evaluationLine) with
            | (Some breakpoint, Some evaluationLine) -> Some {Breakpoint=breakpoint; Evaluation=evaluationLine}
            | _ -> None
        else
            error("The expression file is not in the right format. Please consult the documentation.")
            None

    let readExpression() =
        let curDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)
        let expressionFile = Path.Combine(curDir, "expression.txt")
        if File.Exists(expressionFile) then
            info("Read expression from file: " + expressionFile)
            let lines = File.ReadAllLines(expressionFile)
            parseExpression(lines)
        else
            error(String.Format("Expression file '{0}' not found", expressionFile))
            None

    member this.CanHandle(command: IMessage) =
        command :? ExtractCommand

    member this.Handle(command: IMessage) =
        let extractCommand = command :?> ExtractCommand

        match readExpression() with
        | Some expression -> 
            // run the debugger            
            info("Run the debugger and wait for the conclusion")
            match extractCommand.Debugger.Value.Extract(expression) with
            | Error err -> error(string err)
            | Ok rawResult -> 
                let result = rawResult.Trim('"')
                info(String.Format("Extracted property '{0}': {1}", expression.Evaluation.Property, result))
                settings.MessageBus.Dispatch(new ExtractedExpression(expression.Evaluation.Property, result))
        | None -> ()

    interface IMessageHandler with
        member this.CanHandle(command: IMessage) =
            this.CanHandle(command)

        member this.Handle(command: IMessage) =
            this.Handle(command)
*)