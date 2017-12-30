namespace ES.Shed

open System

type HandlerSettings(messageBus: MessageBus) =
    member val MessageBus = messageBus with get