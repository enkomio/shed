open System
open System.IO
open System.Reflection
open System.Diagnostics
open ES.Shed
open Microsoft.Diagnostics.Runtime
open Microsoft.Diagnostics.Runtime.ICorDebug

let currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)

let inspectProcess(commandLine: String) =
    let args = commandLine.Split(' ')
    Shed.Program.main(args) |> ignore

let dumpHeapTestCase<'T>() =
    let exe = typeof<'T>.Assembly.Location
    let commandLine = String.Format("--exe {0} --ump-heap --timeout 1000", exe)
    inspectProcess(commandLine)

[<EntryPoint>]
let main argv = 
    try
        dumpHeapTestCase<SmtpClientCredentials.Program>()
        0
    with e ->
        Console.WriteLine(e)
        -1
