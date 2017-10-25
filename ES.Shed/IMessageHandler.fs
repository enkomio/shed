namespace ES.Shed

open System

type IMessageHandler =
    interface
        abstract CanHandle: IMessage -> Boolean
        abstract Handle: IMessage -> unit
    end

