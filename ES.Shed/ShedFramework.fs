namespace ES.Shed

open System
open System.Diagnostics
open System.Reflection
open Microsoft.Diagnostics.Runtime
open System.Threading

type ShedFramework(timeout: Int32, messageBus: MessageBus) = 
    let mutable _dataTarget: DataTarget option = None
    let mutable _runtime: ClrRuntime option = None
    let mutable _executable: String option = None
    let mutable _pid = 0

    let (_, info, _, error) = createLoggers(messageBus)
    
    do        
        // resolve via reflection all the handlers
        Assembly.GetExecutingAssembly().GetTypes()
        |> Array.filter(fun t -> t.IsClass)
        |> Array.filter(fun t -> typeof<IMessageHandler>.IsAssignableFrom(t))
        |> Array.map(fun shedModuleType -> Activator.CreateInstance(shedModuleType, new HandlerSettings(messageBus)) :?> IMessageHandler)
        |> Array.iter(messageBus.RegistHandler)
        
    let createDataTarget() =        
        try 
            Thread.Sleep(timeout)
            _dataTarget <- Some <| DataTarget.AttachToProcess(_pid, uint32 5000, AttachFlag.NonInvasive)
            
            if int _dataTarget.Value.PointerSize <> IntPtr.Size then
                error("Unable to attach to a process wich different architecture")
            elif _dataTarget.Value.ClrVersions.Count = 0 then
                error("Unable to instantiate a CLR runtime for the given process")
            else
                _runtime <- Some <| _dataTarget.Value.ClrVersions.[0].CreateRuntime()
                info("Created runtime: " + _runtime.Value.ClrInfo.Version.ToString())
        with e ->
            _dataTarget <- None   
            error(e.Message)

    let createDataTargetIfNecessary(message: IMessage) =
        match message with
        | :? DumpModulesCommand
        | :? DumpHeapCommand ->
            if _dataTarget.IsNone then 
                createDataTarget()
        | _ -> ()

    let enrichMessage(message: IMessage) =
        match message with
        | :? ExtractCommand as extractCommand ->
            extractCommand.ProcessId <- Some _pid
            extractCommand.Executable <- _executable

        | :? DumpModulesCommand as dumpModuleCommand -> 
            dumpModuleCommand.Runtime <- _runtime
            dumpModuleCommand.DataTarget <- _dataTarget
            dumpModuleCommand.ProcessId <- Some _pid

        | :? DumpHeapCommand as dumpHeapCommand ->
            dumpHeapCommand.Runtime <- _runtime
            dumpHeapCommand.ProcessId <- Some _pid

        | :? GenerateReportCommand as genReportCommand ->
            genReportCommand.ProcessId <- _pid

        | _ -> ()
        
    member this.Attach(pid: Int32) =
        _pid <- pid
        createDataTarget()
        _dataTarget.IsSome

    member this.CreateProcess(program: String) =
        _executable <- Some program
        let proc = Process.Start(program)
        _pid <- proc.Id
        info("Started program: " + program)
        _pid
        
    member this.Run(command: IMessage) =
        try
            createDataTargetIfNecessary(command)
            enrichMessage(command)              
            messageBus.Dispatch(command)   
        with e -> error("Exception: " + e.ToString())
        
    member this.Dispose() =
        messageBus.Dispatch(new Dispose())

        match _dataTarget with
        | Some dt -> 
            dt.Dispose()
            _dataTarget <- None
            _runtime <- None            
            info("Detached")
        | _ -> ()

        match _executable with
        | Some _ -> info("Process terminated")
        | None -> ()

    interface IDisposable with
        member this.Dispose() =
            this.Dispose()
