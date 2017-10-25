namespace ES.Shed

open System
open System.Threading
open System.Text
open System.IO
open System.Reflection
open System.Collections.Generic
open System.Security.Cryptography
open Microsoft.Diagnostics.Runtime
open System.Runtime.Serialization.Json

type AnalysisReport(messageBus: MessageBus) =
    let _messages = new List<IMessage>()
    let (trace, info, _, error) = createLoggers(messageBus)

    let md5(buffer: Byte array) =
        use md5 = MD5.Create()
        let hash = md5.ComputeHash(buffer)
        let sBuilder = new StringBuilder()
        hash |> Array.iter(fun b -> sBuilder.Append(b.ToString("x2")) |> ignore)
        sBuilder.ToString()

    let getUniqueFilename(file: String) =
        let counter = ref 0
        let name = Path.GetFileName(file)
        let modDir = Path.GetDirectoryName(file)
        let mutable filename = file
        while File.Exists(filename) do
            filename <- Path.Combine(modDir, (!counter).ToString() + "_" + name)
            incr counter
        filename

    let saveFileBuffer(outputDir: String, inName: String, content: Byte array, isDll: Boolean, isExe: Boolean) =
        let mutable name = inName
        if String.IsNullOrWhiteSpace(name) || not <| Uri.IsWellFormedUriString(name, UriKind.RelativeOrAbsolute) then
            name <- md5(Encoding.Default.GetBytes(name))

            if isDll then name <- name + ".dll"
            elif isExe then name <- name + ".exe"

        let modDir = Path.Combine(outputDir, "Modules")        
        Directory.CreateDirectory(modDir) |> ignore        
        let filename = getUniqueFilename(Path.Combine(modDir, name))
        File.WriteAllBytes(filename, content)
        info("Saved dynamic module: " + name)
            
    let saveModule(outputDir: String, modEvent: ExtractedManagedModuleEvent) =
        let name = Path.GetFileName(modEvent.Module.Name.Split(',').[0])
        saveFileBuffer(outputDir, name, modEvent.Bytes, modEvent.IsDll, modEvent.isExecutable)

    let saveProcessModule(outputDir: String, modEvent: ExtractedProcessModule) =
        let processModule = modEvent.Module
        let modDir = Path.Combine(outputDir, "Modules")
        Directory.CreateDirectory(modDir) |> ignore

        if String.IsNullOrWhiteSpace(processModule.FileName) |> not then
            let filename = Path.Combine(modDir, Path.GetFileName(processModule.FileName))
            if File.Exists(filename) |> not then
                File.Copy(processModule.FileName, filename)
                info("Saved module: " + filename)
            else
                // check if it the same file
                let md5Exisintg = md5(File.ReadAllBytes(filename))
                let md5Module = md5(File.ReadAllBytes(processModule.FileName))
                if md5Exisintg.Equals(md5Module, StringComparison.OrdinalIgnoreCase) |> not then
                    // files are different, save it with a new name
                    let filename = getUniqueFilename(filename)
                    File.Copy(processModule.FileName, filename)
                    info("Saved module: " + filename)

    let serializeHeap(outputDir: String, root: HeapObject) =
        // create writer
        let jsonFile = Path.Combine(outputDir, "heap.json")
        use jsonFileHandle = File.OpenWrite(jsonFile)
        let currentCulture = Thread.CurrentThread.CurrentCulture
        let jsonWriter = JsonReaderWriterFactory.CreateJsonWriter(jsonFileHandle, Encoding.UTF8, true, true, "  ")

        // serialize
        let ser = new DataContractJsonSerializer(typeof<HeapObject>)
        ser.WriteObject(jsonWriter, root)
        jsonWriter.Flush()

        Thread.CurrentThread.CurrentCulture <- currentCulture
        info("Heap json content saved to file: heap.json")

    let generateReport(command: GenerateReportCommand) =
        let outputDir = Path.Combine(Directory.GetCurrentDirectory(), "Result", command.ProcessId.ToString())
        Directory.CreateDirectory(outputDir) |> ignore

        for message in _messages do
            match message with
            | :? ExtractedManagedModuleEvent as modEvent -> saveModule(outputDir, modEvent)
            | :? HeapWalked as heapEvent -> serializeHeap(outputDir, heapEvent.Root)
            | :? ExtractedProcessModule as procModuleEvent -> saveProcessModule(outputDir, procModuleEvent)
            | _ -> ()

        messageBus.Dispatch(new LogMessageEvent("Result saved to " + outputDir, LogLevel.Info))

    member this.CanHandle(message: IMessage) =
        message :? ExtractedManagedModuleEvent ||
        message :? ExtractedProcessModule ||
        message :? GenerateReportCommand ||
        message :? HeapWalked

    member this.Handle(msg: IMessage) =
        match msg with
        | :? GenerateReportCommand as command -> generateReport(command)
        | _ -> _messages.Add(msg)

    interface IMessageHandler with
        member this.CanHandle(command: IMessage) =
            this.CanHandle(command)

        member this.Handle(command: IMessage) =
            this.Handle(command)