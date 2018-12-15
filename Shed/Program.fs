namespace Shed

open System
open System.Reflection
open System.IO
open System.Diagnostics
open Argu
open ES.Shed
open System.Security.Principal
open ES.Shed.ManagedInjector

module Program =    

    type CLIArguments =
        | Dump_Heap
        | Dump_Modules    
        | Method of method:String
        //| Extract_Expression
        | Pid of pid:Int32
        | Exe of file:String
        | Timeout of timeout:Int32
        | Version
        | Verbose
    with
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Dump_Modules _ -> "dump all the .NET modules from the running program."
                | Dump_Heap -> "dump all objects found in the heap." 
                | Method _ -> "inject the given assembly into the specified process and invoke the method."
                //| Extract_Expression -> "evaluate an expression from the specified process/pid."
                | Pid _ -> "the id of the process to inspect." 
                | Exe _ -> "a filename to execute and inspect."
                | Timeout _ -> "wait the given amount of milliseconds before to inspect or debug the process."
                | Verbose _ -> "print verbose messages."
                | Version _ -> "print the Shed version."

    let private printBanner() =
        Console.ForegroundColor <- ConsoleColor.Cyan        
        let banner = "-=[ Shed .NET program inspector ]=-"
        let year = if DateTime.Now.Year = 2017 then "2017" else String.Format("2017-{0}", DateTime.Now.Year)
        let copy = String.Format("Copyright (c) {0} Antonio Parata - @s4tan{1}", year, Environment.NewLine)
        Console.WriteLine(banner.PadLeft(abs(banner.Length - copy.Length) / 2 + banner.Length))
        Console.WriteLine(copy)
        Console.ResetColor()

    let private printUsage(body: String) =
        Console.WriteLine(body)

    let private printError(errorMsg: String) =
        Console.ForegroundColor <- ConsoleColor.Red
        Console.WriteLine(errorMsg)
        Console.ResetColor()

    let private printVersion() =
        let version = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion
        Console.WriteLine("Shed version: {0}", version)

    let private isAdmin() =
        (new WindowsPrincipal(WindowsIdentity.GetCurrent())).IsInRole(WindowsBuiltInRole.Administrator)
        
    let runFramework(shed: ShedFramework, results: ParseResults<CLIArguments>) =
        let shedApplication = new ShedApplication(shed)
        
        let timeout = results.GetResult(<@ Timeout @>, 1000)
        let dumpModules = results.Contains(<@ Dump_Modules @>)
        let dumpHeap = results.Contains(<@ Dump_Heap @>)
        //let extractExpression = results.Contains(<@ Extract_Expression @>)    
        let dumpAll = not dumpModules && not dumpHeap // && not extractExpression

        (*
        // as first operation run the extraction with the debugger.
        // It is very important that the extraction is the first command, 
        // otherwise the clrmd framework will hang the debugger
        if extractExpression then
            shedApplication.Extract()
        else
            // run the program to run for timeout milliseconds
            // this will aloows the debugger to catch usefull information
            shedApplication.Run(timeout)
        *)
        // run the remaining option in no particular order
        [            
            (dumpHeap || dumpAll, shedApplication.DumpHeap)
            (dumpModules || dumpAll, shedApplication.DumpModules)                        
        ]
        |> List.filter fst
        |> List.map snd
        |> List.iter(fun callback -> callback())

        // finally get the report
        shedApplication.GenerateReport()

    let createShedFramework(results: ParseResults<CLIArguments>) =
        let verbose = results.Contains(<@ Verbose @>)
        let messageBus = new MessageBus()
        use shed = new ShedFramework(messageBus)

        if verbose then
            messageBus.Dispatch(new SetLoggingLevelCommand(LogLevel.Trace))

        if not(isAdmin()) then
            messageBus.Dispatch(new LogMessageEvent("You are not running Shed as Administrator, this may imply some limitation.", LogLevel.Warning))
            
        shed

    let runFrameworkWithPid(pid: Int32, results: ParseResults<CLIArguments>) = 
        use shed = createShedFramework(results)
        if shed.Attach(pid) 
        then runFramework(shed, results)            
        else printError("Unable to attach to the process id: " + pid.ToString())

    let runFrameworkWithExe(program: String, results: ParseResults<CLIArguments>) =
        use shed = createShedFramework(results)
        shed.CreateProcess(program) 
        runFramework(shed, results)

    let injectAssembly(pid: Int32, file: String, method: String) =
        let filename = Path.GetFullPath(file)
        if not(File.Exists(filename)) then
            Console.Error.WriteLine("The file '{0}', doesn't exists.", filename)
            1
        else
            let fileContent = File.ReadAllBytes(filename)
            let injector = new Injector(pid, fileContent, method)
            match injector.Inject() with
            | InjectionResult.Success -> 
                Console.WriteLine("DLL was correctly injected");
                0
            | error -> 
                Console.Error.WriteLine("Unable to inject the given file. Reason: {0}", error.ToString())
                1
                
    [<EntryPoint>]
    let main argv =
        printBanner()

        let parser = ArgumentParser.Create<CLIArguments>(programName = "shed.exe")
        try            
            let results = parser.Parse(argv)
                    
            if results.IsUsageRequested then
                printUsage(parser.PrintUsage())
                0
            elif results.Contains(<@ Version @>) then
                printVersion()
                0
            else            
                match results.TryGetResult(<@ Pid @>), results.TryGetResult(<@ Exe @>) with
                | (None, None) ->          
                    // wrong options
                    printUsage(parser.PrintUsage())   
                    1
                | (Some pid, Some exe) ->
                    // maybe inject assembly
                    let methodName = results.GetResult(<@ Method @>, String.Empty)
                    injectAssembly(pid, exe, methodName)
                | Some pid, None ->
                    // attach to pid
                    runFrameworkWithPid(pid, results)
                    0
                | None, Some file ->
                    // run executable
                    let fullPath = Path.GetFullPath(file)
                    if File.Exists(fullPath) then
                        runFrameworkWithExe(fullPath, results)
                        0                         
                    else
                        printError(String.Format("File '{0}' not found", fullPath))
                        1                 
        with 
            | :? ArguParseException ->
                printUsage(parser.PrintUsage())   
                1
            | e ->
                printError(e.ToString())
                1
        
