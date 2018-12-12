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

    (*
    member this.Extract() =
        let command = new ExtractCommand()
        shed.Run(command)
        *)
    (*
    member this.Run(milliseconds: Int32) =
        shed.Go(milliseconds)
    *)

    member this.GenerateReport() =
        let command = new GenerateReportCommand()
        shed.Run(command)