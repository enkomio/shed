namespace ES.Shed

open System
open System.Linq
open System.Reflection
open System.Collections.Generic
open Microsoft.Diagnostics.Runtime

type ShedFramework(messageBus: MessageBus) = 
    let mutable _dataTarget: DataTarget option = None
    let mutable _runtime: ClrRuntime option = None
    let mutable _pid = 0
    let (_, info, _, error) = createLoggers(messageBus)
    
    do        
        Assembly.GetExecutingAssembly().GetTypes()
        |> Array.filter(fun t -> t.IsClass)
        |> Array.filter(fun t -> typeof<IMessageHandler>.IsAssignableFrom(t))
        |> Array.map(fun shedModuleType -> Activator.CreateInstance(shedModuleType, new HandlerSettings(messageBus)) :?> IMessageHandler)
        |> Array.iter(messageBus.RegistHandler)

    let enrichMessage(message: IMessage) =
        match message with
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
        try 
            _pid <- pid
            _dataTarget <- Some <| DataTarget.AttachToProcess(_pid, uint32 5000, AttachFlag.NonInvasive)

            if int _dataTarget.Value.PointerSize <> IntPtr.Size || _dataTarget.Value.ClrVersions.Count = 0 then
                error("Unable to attach to a process wich different architecture")
                false
            else
                info("Attached to pid: " + _pid.ToString())
                _runtime <- Some <| _dataTarget.Value.ClrVersions.[0].CreateRuntime()
                info("Created runtime: " + _runtime.Value.ClrInfo.Version.ToString())
                true
        with _ ->
            _dataTarget <- None            
            false

    member this.Run(command: IMessage) =
        enrichMessage(command)        
        messageBus.Dispatch(command)   
        
       member this.Detach() =
        match _dataTarget with
        | Some dataTarget -> dataTarget.Dispose()
        | None -> ()
                 

    member this.Dispose() =
        match _dataTarget with
        | Some dt -> 
            dt.Dispose()
            _dataTarget <- None
            _runtime <- None
            info("Detached")
        | _ -> ()

    interface IDisposable with
        member this.Dispose() =
            this.Dispose()
