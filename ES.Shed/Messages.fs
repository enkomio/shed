namespace ES.Shed

open System
open Microsoft.Diagnostics
open System.Diagnostics
open Microsoft.Diagnostics.Runtime

type IMessage =
    interface
        abstract Id: Guid with get
    end

[<AbstractClass>]
type BaseMessage() =    
    member val Id = Guid.NewGuid() with get
    interface IMessage with
        member this.Id 
            with get() = this.Id

[<AbstractClass>]
type BaseCommand() =    
    inherit BaseMessage()

[<AbstractClass>]    
type BaseEvent() =
    inherit BaseMessage()

// list of application specific event and commands
type DumpModulesCommand() =    
    inherit BaseCommand()
    member val Runtime: ClrRuntime option = None with get, set
    member val DataTarget: DataTarget option = None with get, set
    member val ProcessId: Int32 option = None with get, set

type DumpHeapCommand() =    
    inherit BaseCommand()
    member val Runtime: ClrRuntime option = None with get, set
    member val ProcessId: Int32 option = None with get, set

type GenerateReportCommand() =    
    inherit BaseCommand()
    member val ProcessId = -1 with get, set
    
type ExtractedManagedModuleEvent(clrModule: ClrModule, bytes: Byte array, isDll: Boolean, isExec: Boolean) =
    inherit BaseEvent()
    member val Module = clrModule with get
    member val Bytes = bytes with get
    member val IsDll = isDll with get
    member val isExecutable = isExec with get

    override this.ToString() =
        if not(String.IsNullOrWhiteSpace(clrModule.Name)) then clrModule.Name
        else String.Format("Unnamed module, len: {0}", this.Bytes.Length)

type ExtractedProcessModule(procModule: ProcessModule) =
    inherit BaseEvent()
    member val Module = procModule with get, set

    override this.ToString() =
        string(procModule.ModuleName)

type HeapWalked(root: HeapObject) =
    inherit BaseEvent()
    member val Root = root with get

type LogLevel =
    | Trace = 0
    | Info = 1
    | Warning = 2
    | Error = 3
    
type SetLoggingLevelCommand(logLevel: LogLevel) =
    inherit BaseCommand()
    member val Level = logLevel with get

type LogMessageEvent(msg: String, logLevel: LogLevel) =
    inherit BaseEvent()
    member val Message = msg with get
    member val Level = logLevel with get