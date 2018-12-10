namespace ES.Shed.AssemblyInspector

open System

type AssemblyInspectorConfig() =
    member val MethodName = String.Empty with get, set
    member val AssemblyName = String.Empty with get, set

