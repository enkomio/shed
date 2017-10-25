namespace Shed

open System
open System.Threading
open System.IO
open System.Diagnostics
open Argu
open ES.Shed

module Program =
    type CLIArguments =
        | Dump_Heap
        | Dump_Modules        
        | Pid of pid:Int32
        | Exe of file:String
        | Timeout of timeout:Int32
        | Verbose
    with
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Dump_Modules _ -> "dump all the .NET modules from the running program."
                | Dump_Heap -> "dump all objects found in the heap." 
                | Pid _ -> "the id of the process to inspect." 
                | Exe _ -> "a filename to execute and inspect."
                | Timeout _ -> "wait the given amount of milliseconds before to inspect the process. This is only valid if an exe is specified."
                | Verbose _ -> "print verbose messages."

    let printBanner() =
        Console.ForegroundColor <- ConsoleColor.Cyan        
        let banner = "-=[ Shed .NET program inspector ]=-"
        let year = if DateTime.Now.Year = 2017 then "2017" else String.Format("2017-{0}", DateTime.Now.Year)
        let copy = String.Format("Copyright (c) {0} Antonio Parata - @s4tan{1}", year, Environment.NewLine)
        Console.WriteLine(banner.PadLeft(abs(banner.Length - copy.Length) / 2 + banner.Length))
        Console.WriteLine(copy)
        Console.ResetColor()

    let printUsage(body: String) =
        Console.WriteLine(body)

    let printError(errorMsg: String) =
        Console.ForegroundColor <- ConsoleColor.Red
        Console.WriteLine(errorMsg)
        Console.ResetColor()
        
    let runFramework(pid: Int32, results: ParseResults<CLIArguments>) =
        let dumpModules = results.Contains(<@ Dump_Modules @>)
        let dumpHeap = results.Contains(<@ Dump_Heap @>)
        let verbose = results.Contains(<@ Verbose @>)
        let timeout = results.TryGetResult(<@ Timeout @>)
        let dumpAll = not(dumpModules || dumpHeap)
        
        let messageBus = new MessageBus()
        use shed = new ShedFramework(messageBus)

        if verbose then
            messageBus.Dispatch(new SetLoggingLevelCommand(LogLevel.Trace))

        if timeout.IsSome then 
            Thread.Sleep(timeout.Value)

        if shed.Attach(pid) then
            let shedApplication = new ShedApplication(shed)
            [
                (dumpModules || dumpAll, shedApplication.DumpHeap)
                (dumpHeap || dumpAll, shedApplication.DumpModules)
            ] 
            |> List.filter fst
            |> List.map snd
            |> List.iter(fun callback -> callback())

            // finally get the report
            shedApplication.GenerateReport()
            
        else
            printError("Unable to attach to the process id: " + pid.ToString())
                
    [<EntryPoint>]
    let main argv = 
        printBanner()

        let parser = ArgumentParser.Create<CLIArguments>(programName = "shed.exe")
        try            
            let results = parser.Parse(argv)
                    
            if results.IsUsageRequested then
                printUsage(parser.PrintUsage())
                0
            else            
                match results.TryGetResult(<@ Pid @>), results.TryGetResult(<@ Exe @>) with
                | (None, None)
                | (Some _, Some _) ->
                    printUsage(parser.PrintUsage())   
                    1
                | Some pid, None ->
                    runFramework(pid, results)
                    0
                | None, Some file ->
                    let fullPath = Path.GetFullPath(file)
                    let proc = Process.Start(fullPath)
                    runFramework(proc.Id, results)   
                    proc.Kill()                     
                    0                    
        with 
            | :? ArguParseException ->
                printUsage(parser.PrintUsage())   
                1
            | e ->
                printError(e.Message)
                1
        
