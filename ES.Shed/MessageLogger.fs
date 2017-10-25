namespace ES.Shed

open System
open System.Collections.Generic
open Microsoft.Diagnostics.Runtime

exception MessageLoggerException of IMessage

[<AutoOpen>]
module LogginHelpers =
    let createLoggers(messageBus: MessageBus) =
        let trace(txt: String) =
            messageBus.Dispatch(new LogMessageEvent(txt, LogLevel.Trace))

        let info(txt: String) =
            messageBus.Dispatch(new LogMessageEvent(txt, LogLevel.Info))

        let warning(txt: String) =
            messageBus.Dispatch(new LogMessageEvent(txt, LogLevel.Warning))

        let error(txt: String) =
            messageBus.Dispatch(new LogMessageEvent(txt, LogLevel.Error))

        (trace, info, warning, error)

type MessageLogger(messageBus: MessageBus) =
    let mutable _logLevel = LogLevel.Info

    let trace(msg: String) =
        Console.ForegroundColor <- ConsoleColor.DarkCyan
        Console.WriteLine("[~] {0}", msg)
        Console.ResetColor()        

    let info(msg: String) =        
        Console.WriteLine("[+] {0}", msg)

    let warning(msg: String) =      
        Console.ForegroundColor <- ConsoleColor.DarkYellow
        Console.WriteLine("[-] {0}", msg)
        Console.ResetColor()

    let error(msg: String) =        
        Console.ForegroundColor <- ConsoleColor.DarkRed
        Console.WriteLine("[!] {0}", msg)
        Console.ResetColor()

    let _handlers = 
        [
            (LogLevel.Trace, trace)
            (LogLevel.Info, info)
            (LogLevel.Warning, warning)
            (LogLevel.Error, error)
        ] |> Map.ofList    
            
    let handleSetLoggingLevelCommand(command: SetLoggingLevelCommand) =
        _logLevel <- command.Level

    let handleLogMessageEvent(event: LogMessageEvent) =        
        if event.Level >= _logLevel then
            _handlers.[event.Level](event.Message)

    member this.CanHandle(message: IMessage) =
        message :? LogMessageEvent ||
        message :? SetLoggingLevelCommand

    member this.Handle(msg: IMessage) =        
        match msg with
        | :? LogMessageEvent as moduleEvent -> handleLogMessageEvent(moduleEvent)
        | :? SetLoggingLevelCommand as setLoggingLevelCommand -> handleSetLoggingLevelCommand(setLoggingLevelCommand)
        | _ -> raise(MessageLoggerException(msg))        

    interface IMessageHandler with
        member this.CanHandle(command: IMessage) =
            this.CanHandle(command)

        member this.Handle(command: IMessage) =
            this.Handle(command)