open System
open System.IO
open System.Reflection
open System.Diagnostics.Contracts
open System.Diagnostics
open System.Threading
open InjectedAssembly

let currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)

let inspectProcess(commandLine: String) =
    let args = commandLine.Split(' ')
    Contract.Assert(Shed.Program.main(args) = 0)    

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

let injectExternalAssembly<'T>() =
    // this force compilation of the project
    let _ = new WindowsFormHelloWorld.MainForm()
    let assemblyFile = Path.GetFullPath(Path.Combine("..", "..", "..", "WindowsFormHelloWorld", "bin", "Debug", "WindowsFormHelloWorld.exe"))
    let assemblyExecute = Assembly.LoadFile(assemblyFile)
    let assemblyDir = Path.GetDirectoryName(assemblyExecute.Location)
    Directory.SetCurrentDirectory(assemblyDir)
    
    let procInfo = new ProcessStartInfo(assemblyExecute.Location, WorkingDirectory = assemblyDir)
    let proc = Process.Start(procInfo)
    Thread.Sleep(1000)

    //  run Shed
    let exe = typeof<'T>.Assembly.Location
    let commandLine = String.Format("--exe {0} --pid {1} --inject", exe, proc.Id)
    inspectProcess(commandLine)    
    proc.Kill()
    Console.WriteLine("Injection successful")

let injectExternalAssemblyInNativeApplication<'T>() =
    let executable = Path.Combine(Environment.ExpandEnvironmentVariables("%windir%\SysWoW64"), "notepad.exe")
    let proc = Process.Start(executable)
    Thread.Sleep(1000)

    //  run Shed
    let exe = typeof<'T>.Assembly.Location
    let commandLine = String.Format("--exe {0} --pid {1} --inject", exe, proc.Id)
    inspectProcess(commandLine)    
    proc.Kill()
    Console.WriteLine("Injection successful")
    
[<EntryPoint>]
let main argv = 
    try 
        injectExternalAssemblyInNativeApplication<SmtpClientCredentials.Program>()
        injectExternalAssembly<EntryPoint>()
        dumpHeapTestCase<HelloWorld.Program>()
        dumpModulesTestCase<HelloWorld.Program>()
        0
    with e ->
        Console.WriteLine(e)
        Contract.Assert(false, e.ToString())
        -1
