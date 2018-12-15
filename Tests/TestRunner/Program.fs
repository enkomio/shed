open System
open System.IO
open System.Reflection
open System.Diagnostics
open ES.Shed
open Microsoft.Diagnostics.Runtime
open Microsoft.Diagnostics.Runtime.ICorDebug
open System.Threading.Tasks
open System.Diagnostics.Contracts
open ES.Shed.ManagedInjector
open System.Threading

let currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)

let inspectProcess(commandLine: String) =
    let args = commandLine.Split(' ')
    Shed.Program.main(args) |> ignore

let dumpHeapTestCase<'T>() =
    let exe = typeof<'T>.Assembly.Location
    let commandLine = String.Format("--exe {0} --dump-heap --timeout 1000", exe)
    inspectProcess(commandLine)
    (*
let testAssemblyInspector() =
    Task.Factory.StartNew(fun () ->
        Program.Main(Array.zeroCreate<String> 0)
    , TaskCreationOptions.LongRunning) |> ignore
    
    let settings = 
        new AssemblyInspectorConfig(
            AssemblyName="SmtpClientCredentials",
            MethodName="System.Net.Mail.SmtpClient::set_Credentials"
        )

    let assemblyInspector = new AssemblyInspector(settings)
    let information = assemblyInspector.Run()
    Contract.Assert(information |> Array.contains(NetworkCredentialPassword "my_password"))
    *)
let testManagedInjection<'T>() =
    let proc = Process.Start(typeof<'T>.Assembly.Location)
    let buffer = File.ReadAllBytes(typeof<SmtpClientCredentials.Program>.Assembly.Location)
    Thread.Sleep(1000)
    let injector = new Injector(proc.Id, buffer, "SmtpClientCredentials.MailSender.Send")
    let injectionResult = injector.Inject()
    proc.Kill()
    Contract.Assert((injectionResult = InjectionResult.Success))
    Console.WriteLine("Injection successful")    

[<EntryPoint>]
let main argv = 
    try
        testManagedInjection<WindowsFormHelloWorld.MainForm>()        

        //testAssemblyInspector()
        //dumpHeapTestCase<HelloWorld.Program>()
        0
    with e ->
        Console.WriteLine(e)
        -1
