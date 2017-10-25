namespace ES.Shed

open System
open System.Collections.Generic

exception UnrecognizedMessageTypeException of Type

type MessageBus() =
    let _handlers = new List<IMessageHandler>()

    let dispatchEvent(event: BaseEvent) =
        _handlers
        |> Seq.filter(fun handler -> handler.CanHandle(event))
        |> Seq.iter(fun handler -> handler.Handle(event))

    let dispatchCommand(command: BaseCommand) =
        match _handlers |> Seq.tryFind(fun handler -> handler.CanHandle(command)) with
        | Some handler -> handler.Handle(command)
        | None -> ()

    member this.RegistHandler(handler: IMessageHandler) =
        _handlers.Add(handler)

    member this.Dispatch(message: IMessage) =
        match message with
        | :? BaseEvent as e -> dispatchEvent(e)
        | :? BaseCommand as c -> dispatchCommand(c)
        | _ -> raise(UnrecognizedMessageTypeException(message.GetType()))

        
        
        

