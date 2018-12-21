namespace ES.Shed

open System
open System.Text
open System.IO
open System.Collections.Generic
open System.Security.Cryptography
open Newtonsoft.Json

type AnalysisReport(settings: HandlerSettings) =
    let _messages = new List<IMessage>()
    let (_, info, _, _) = createLoggers(settings.MessageBus)
    let _logStringBuilder = new StringBuilder()
    
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

    let savePeFileBuffer(outputDir: String, inName: String, content: Byte array, isDll: Boolean, isExe: Boolean, isManaged: Boolean) =
        let subDir = if isManaged then ".NET" else "native"
        let mutable name = inName
        if String.IsNullOrWhiteSpace(name) || not <| Uri.IsWellFormedUriString(name, UriKind.RelativeOrAbsolute) then
            name <- md5(Encoding.Default.GetBytes(name))
            if isDll then name <- name + ".dll"
            elif isExe then name <- name + ".exe"

        let modDir = Path.Combine(outputDir, "Modules", subDir)        
        Directory.CreateDirectory(modDir) |> ignore        
        let filename = getUniqueFilename(Path.Combine(modDir, name))
        File.WriteAllBytes(filename, content)
        info("Saved dynamic module: " + name)
            
    let saveModule(outputDir: String, modEvent: ExtractedManagedModuleEvent) =
        let mutable name =
            match modEvent.Assembly with
            | Some assembly when assembly.ManifestModule <> null -> assembly.ManifestModule.ScopeName
            | _ -> 
                let tmpName = 
                    if String.IsNullOrWhiteSpace(modEvent.Module.Name) then Guid.NewGuid().ToString("N")
                    else
                        let chunks = modEvent.Module.Name.Split(',')
                        if chunks.Length > 0 then Path.GetFileName(chunks.[0])
                        else modEvent.Module.Name
                if String.IsNullOrWhiteSpace(Path.GetExtension(tmpName)) then
                    if modEvent.IsDll then tmpName + ".dll"
                    else tmpName + ".exe"
                else tmpName

        savePeFileBuffer(outputDir, name, modEvent.Bytes, modEvent.IsDll, modEvent.IsExecutable, true)

    let saveMemoryScanModule(outputDir: String, modEvent: ExtractedManagedModuleViaMemoryScanEvent) =
        let name = 
            match modEvent.Assembly with
            | Some assembly when assembly.ManifestModule <> null -> assembly.ManifestModule.ScopeName
            | _ -> Guid.NewGuid().ToString("N") + if modEvent.IsDll then ".dll" else ".exe"
        savePeFileBuffer(outputDir, name, modEvent.Bytes, modEvent.IsDll, modEvent.IsExecutable, true)

    let saveProcessModule(outputDir: String, modEvent: ExtractedProcessModule) =
        let processModule = modEvent.Module
        let modDir = Path.Combine(outputDir, "Modules", "Misc")
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

    let serializeObject(object, jsonFile: String) =
        let serializedValue = JsonConvert.SerializeObject(object, Formatting.Indented)
        File.WriteAllText(jsonFile, serializedValue)

    let serializeHeap(outputDir: String, root: HeapObject) =        
        let jsonFile = Path.Combine(outputDir, "heap.json")
        serializeObject(root, jsonFile)
        info("Heap json content saved to file: " + jsonFile)

    let saveExtractedProperty(outputDir: String, extractedExpression: ExtractedExpression) =
        let jsonFile = Path.Combine(outputDir, "extraction.json")
        serializeObject(extractedExpression, jsonFile)
        info("Extracted value saved to: " + jsonFile)

    let handleLogMessageEvent(event: LogMessageEvent) = 
        let msg = String.Format("[{0}] {1}", event.Level, event.Message)       
        _logStringBuilder.AppendLine(msg) |> ignore

    let generateReport(command: GenerateReportCommand) =
        let outputDir = 
            match command.OutputDirectory with
            | Some outputDir -> outputDir
            | _ -> Path.Combine(Directory.GetCurrentDirectory(), "Result", command.ProcessId.ToString())
        Directory.CreateDirectory(outputDir) |> ignore

        // handle all other messages
        for message in _messages do
            match message with
            | :? ExtractedManagedModuleEvent as modEvent -> saveModule(outputDir, modEvent)
            | :? HeapWalked as heapEvent -> serializeHeap(outputDir, heapEvent.Root)
            | :? ExtractedProcessModule as procModuleEvent -> saveProcessModule(outputDir, procModuleEvent)
            | :? ExtractedManagedModuleViaMemoryScanEvent as memScanModEvent -> saveMemoryScanModule(outputDir, memScanModEvent)
            | :? ExtractedExpression as extractedExpression -> saveExtractedProperty(outputDir, extractedExpression)
            | _ -> ()

        info("Result saved to: " + outputDir)

        // save log to file
        let logfile = Path.Combine(outputDir, "output.log")
        File.WriteAllText(logfile, _logStringBuilder.ToString())
        
    member this.CanHandle(message: IMessage) =
        message :? ExtractedManagedModuleEvent ||
        message :? ExtractedProcessModule ||
        message :? GenerateReportCommand ||
        message :? ExtractedManagedModuleViaMemoryScanEvent ||
        message :? LogMessageEvent ||
        message :? HeapWalked ||
        message :? ExtractedExpression

    member this.Handle(msg: IMessage) =
        match msg with
        | :? GenerateReportCommand as command -> generateReport(command)
        | :? LogMessageEvent as moduleEvent -> handleLogMessageEvent(moduleEvent)
        | _ -> _messages.Add(msg)

    interface IMessageHandler with
        member this.CanHandle(command: IMessage) =
            this.CanHandle(command)

        member this.Handle(command: IMessage) =
            this.Handle(command)