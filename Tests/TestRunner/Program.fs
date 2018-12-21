open System
open System.IO
open System.Reflection
open System.Diagnostics.Contracts
open System.Diagnostics
open System.Threading
open ES.ManagedInjector
open SmtpClientCredentials

let currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)

let inspectProcess(commandLine: String) =
    let args = commandLine.Split(' ')
    Shed.Program.main(args) |> ignore

let dumpHeapTestCase<'T>() =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
    let exe = typeof<'T>.Assembly.Location
    let commandLine = String.Format("--exe {0} --output {1} --dump-heap --timeout 1000", exe, tempDir)
    inspectProcess(commandLine)
    Contract.Assert(Directory.Exists(tempDir), "Result directory not created")
    Directory.Delete(tempDir, true)

let dumpModulesTestCase<'T>() =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
    let exe = typeof<'T>.Assembly.Location
    let commandLine = String.Format("--exe {0} --output {1} --dump-modules --timeout 1000", exe, tempDir)
    inspectProcess(commandLine)
    Contract.Assert(Directory.Exists(tempDir), "Result directory not created")
    Directory.Delete(tempDir, true)

let injectExternalAssembly() =
    let assemblyFile = Path.GetFullPath(Path.Combine("..", "..", "..", "WindowsFormHelloWorld", "bin", "Debug", "WindowsFormHelloWorld.exe"))
    let assemblyExecute = Assembly.LoadFile(assemblyFile)
    let assemblyDir = Path.GetDirectoryName(assemblyExecute.Location)
    Directory.SetCurrentDirectory(assemblyDir)
    
    let procInfo = new ProcessStartInfo(assemblyExecute.Location, WorkingDirectory = assemblyDir)
    let proc = Process.Start(procInfo)
    Thread.Sleep(1000)

    let injector = new Injector(proc.Id, (new MailSender(String.Empty)).GetType().Assembly)
    let injectionResult = injector.Inject()
    proc.Kill()

    Contract.Assert((injectionResult = InjectionResult.Success))
    Console.WriteLine("Injection successful")
    
[<EntryPoint>]
let main argv = 
    try 
        injectExternalAssembly()
        dumpHeapTestCase<HelloWorld.Program>()
        dumpModulesTestCase<HelloWorld.Program>()
        0
    with e ->
        Console.WriteLine(e)
        -1
