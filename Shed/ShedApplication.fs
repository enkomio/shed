namespace Shed

open System
open ES.Shed

type ShedApplication(shed: ShedFramework) =
    member this.DumpModules() =
        let command = new DumpModulesCommand()
        shed.Run(command)

    member this.DumpHeap() =
        let command = new DumpHeapCommand()
        shed.Run(command)

    member this.GenerateReport() =
        let command = new GenerateReportCommand()
        shed.Run(command)