namespace ES.Shed

open System
open System.Collections.Generic
open System.Text
open System.Diagnostics
open System.Reflection
open System.IO
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Microsoft.Diagnostics.Runtime
open Microsoft.Diagnostics.Runtime.Interop
open Microsoft.Diagnostics.Runtime.Utilities
open Microsoft.FSharp.NativeInterop
open ES.Shed.Native

type internal Section = {
    Name: String
    VirtualAddress: Int32
    VirtualSize: Int32
    SizeOfRawData: Int32
    PointerToRawData: Int32
    mutable Content: Byte array
}

type ModuleDumper(settings: HandlerSettings) =
    let IMAGE_FILE_DLL = uint16 0x2000
    let IMAGE_FILE_EXECUTABLE_IMAGE = uint16 0x0002
    let PAGE_READWRITE = uint32 0x04
    let PROCESS_ALL_ACCESS = uint32 0x1FFFFF
    let (trace, info, _, error) = createLoggers(settings.MessageBus)
    let messageBus = settings.MessageBus

    let M = byte <| Char.ConvertToUtf32("M", 0)
    let Z = byte <| Char.ConvertToUtf32("Z", 0)
    let P = byte <| Char.ConvertToUtf32("P", 0)
    let E = byte <| Char.ConvertToUtf32("E", 0)

    let checkAssemblyDumpValidity(buffer: Byte array) =
        try
            Some <| Assembly.Load(buffer)
        with :? BadImageFormatException -> None
        
    let readAsciiString(buffer: Byte array, offset: Int32) =
        let sb = new StringBuilder()
        let index = ref 0
        while offset >= 0 && offset + !index < buffer.Length && buffer.[offset + !index] <> 0uy do
            let c = buffer.[offset + !index]
            sb.Append(Convert.ToChar(c)) |> ignore
            incr index
        sb.ToString()

    let readMemory(runtime: ClrRuntime, address: UInt64, size: Int32) =        
        let buffer = Array.zeroCreate<Byte> size 
        let mutable result = true
        let outSize = ref 0        
        try
            result <- runtime.DataTarget.ReadProcessMemory(address, buffer, size, outSize)
        with _ -> ()
        
        (result, buffer, !outSize)

    let getThunkData(offset: Int32, index: Int32, section: Section, carvedPe: Byte array) =
        let sizeOfThunkData = Marshal.SizeOf(typeof<IMAGE_THUNK_DATA32>)
        let thunkDataOffset = section.PointerToRawData + offset - section.VirtualAddress + (sizeOfThunkData * index)
            
        let thunkDataBuffer = Array.zeroCreate<Byte>(sizeOfThunkData)
        Array.Copy(carvedPe, thunkDataOffset, thunkDataBuffer, 0, sizeOfThunkData)

        let handle = GCHandle.Alloc(thunkDataBuffer, GCHandleType.Pinned)
        Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof<IMAGE_THUNK_DATA32>) :?> IMAGE_THUNK_DATA32

    let dumpFilename(clrModule: ClrModule) =
        let buffer = File.ReadAllBytes(clrModule.FileName)
        let extension = Path.GetExtension(clrModule.FileName)
        let isDll = extension.Equals(".dll", StringComparison.OrdinalIgnoreCase)
        let isExec = extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)

        messageBus.Dispatch(new ExtractedManagedModuleEvent(clrModule, buffer, isDll, isExec))
        info("File Module: " + clrModule.Name)

    let rebuildPe(sections: List<Section>, pe: PEFile, runtime: ClrRuntime, baseAddress: Int32) =
        let mutable carvedPe: Byte array option = None
        let bytesRead = ref 0

        let totalSize =
            sections 
            |> Seq.sortByDescending(fun s -> s.PointerToRawData)
            |> Seq.head
            |> fun s -> s.PointerToRawData + s.SizeOfRawData
        
        let peHeader = Array.zeroCreate<Byte> pe.Header.PEHeaderSize                
        if runtime.ReadMemory(uint64 baseAddress, peHeader, peHeader.Length, bytesRead) then
            carvedPe <- Some <| Array.zeroCreate<Byte>(totalSize)

            Array.Copy(peHeader, carvedPe.Value, peHeader.Length)                    
            for section in sections do
                Array.Copy(section.Content, 0, carvedPe.Value, section.PointerToRawData, section.Content.Length)
        
        // fix entry point
        sections
        |> Seq.toList
        |> List.filter(fun section -> 
            pe.Header.ImportDirectory.VirtualAddress >= section.VirtualAddress &&
            pe.Header.ImportDirectory.VirtualAddress < section.VirtualAddress + section.VirtualSize
        )
        |> List.iter(fun section ->
            let importDataDirectoryOffset = section.PointerToRawData + pe.Header.ImportDirectory.VirtualAddress - section.VirtualAddress

            // read import directory section
            let importDataDirectory = Array.zeroCreate<Byte>(pe.Header.ImportDirectory.Size)
            Array.Copy(carvedPe.Value, importDataDirectoryOffset, importDataDirectory, 0, Marshal.SizeOf(typeof<IMAGE_IMPORT_DESCRIPTOR>))
            
            // parse import directory
            let handle = GCHandle.Alloc(importDataDirectory, GCHandleType.Pinned)
            let imageImportDescriptor = Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof<IMAGE_IMPORT_DESCRIPTOR>) :?> IMAGE_IMPORT_DESCRIPTOR
                                       
            // get dll name   
            let dllNameOffset = section.PointerToRawData + int32 imageImportDescriptor.Name - section.VirtualAddress
            let dllName = readAsciiString(carvedPe.Value, dllNameOffset)

            if dllName.Equals("mscoree.dll", StringComparison.Ordinal) then                        
                // dump function names
                let mutable functionDumpCompleted = false
                let index = ref 0
                while not functionDumpCompleted do
                    let thunkData = getThunkData(int32 imageImportDescriptor.OriginalFirstThunk, !index, section, carvedPe.Value)
                    let functionNameOffset = section.PointerToRawData + int32 thunkData.AddressOfData - section.VirtualAddress + 2
                    let functionName = readAsciiString(carvedPe.Value, functionNameOffset)
                            
                    if functionName.Equals("_CorExeMain", StringComparison.Ordinal) || functionName.Equals("_CorDllMain", StringComparison.Ordinal) then
                        use binaryWriter = new BinaryWriter(new MemoryStream(carvedPe.Value))

                        // get the function address                       
                        let funcVirtualAddress = BitConverter.GetBytes(int32 imageImportDescriptor.FirstThunk + int32 pe.Header.ImageBase)
                                                                
                        // Search entrypoint: jmp funcVirtualAddress --> 0xFF 0x37 <addr bytes>
                        // See https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/hosting/corexemain-function
                        let pattern = Array.concat [[|0xFFuy; 0X25uy|]; funcVirtualAddress]

                        // search EntryPoint pattern and fix AddressOfEntryPoint
                        let mutable peFixed = false
                        for section in sections do                                    
                            for i=0 to section.Content.Length-1-pattern.Length do
                                let sectionPattern = section.Content.[i..pattern.Length-1+i]                                        
                                if not peFixed && Seq.compareWith Operators.compare sectionPattern pattern = 0 then
                                    let entryPointVA = section.VirtualAddress + i

                                    // parse PE Header to fix value
                                    let peHeaderStart = BitConverter.ToInt32(carvedPe.Value, 0x3c)
                                    let addressOfEntryPointOffset = peHeaderStart + 0x28
                                    binaryWriter.BaseStream.Position <- int64 addressOfEntryPointOffset
                                    binaryWriter.Write(entryPointVA)
                                    peFixed <- true
                                
                        binaryWriter.Close()

                    incr index                
                    functionDumpCompleted <- String.IsNullOrWhiteSpace(functionName)
        )

        carvedPe

    let extractSectionsFromMemory(pe: PEFile, runtime: ClrRuntime, baseAddress: Int32) =
        let sections = new List<Section>()
        let sectionsField = 
            pe.Header.GetType()
                .GetField("_sections", BindingFlags.NonPublic ||| BindingFlags.Instance)
                .GetValue(pe.Header) :?> Pointer
                
        let sectionHeaderType = 
            pe.GetType().Assembly.GetTypes() 
                |> Array.filter(fun t -> t.Name.EndsWith("IMAGE_SECTION_HEADER"))
                |> Array.head

        let sectionHeaderSize = Marshal.SizeOf(sectionHeaderType)

        // properties useful for dump
        let nameBytesOffset = Marshal.OffsetOf(sectionHeaderType, "NameBytes")
        let virtualAddressOffset = Marshal.OffsetOf(sectionHeaderType, "VirtualAddress")
        let virtualSizeOffset = Marshal.OffsetOf(sectionHeaderType, "VirtualSize")
        let sizeOfRawDataOffset = Marshal.OffsetOf(sectionHeaderType, "SizeOfRawData")
        let rawAddressOffset = Marshal.OffsetOf(sectionHeaderType, "PointerToRawData")
                
        // extract sections content
        let mutable sectionPointer = new IntPtr(Pointer.Unbox(sectionsField))                    
        for i=0 to int pe.Header.NumberOfSections-1 do
            // read section fieds
            let section = {
                Name = Marshal.PtrToStringAnsi(sectionPointer + nameBytesOffset)
                VirtualAddress = Marshal.ReadInt32(sectionPointer + virtualAddressOffset)
                VirtualSize = Marshal.ReadInt32(sectionPointer + virtualSizeOffset)
                SizeOfRawData = Marshal.ReadInt32(sectionPointer + sizeOfRawDataOffset)
                PointerToRawData = Marshal.ReadInt32(sectionPointer + rawAddressOffset)
                Content = Array.empty<Byte>
            }
            sections.Add(section)

            // read section content
            let sectionBuffer = Array.zeroCreate<Byte> section.SizeOfRawData
            let bytesRead = ref 0
            if runtime.ReadMemory(uint64 (section.VirtualAddress + baseAddress), sectionBuffer, section.SizeOfRawData, bytesRead) then    
                section.Content <- sectionBuffer

            // go next section
            sectionPointer <- sectionPointer + new IntPtr(sectionHeaderSize)
        sections

    let carveFileFromMemory(pe: PEFile, baseAddress: Int32, runtime: ClrRuntime) =
        let mutable carvedPe: Byte array option = None
                
        try
            let sections = extractSectionsFromMemory(pe, runtime, baseAddress)
        
            // build PE
            if sections |> Seq.isEmpty |> not then
                carvedPe <- rebuildPe(sections, pe, runtime, baseAddress)
        with _ -> ()

        carvedPe

    let extractModule(clrModule: ClrModule, runtime: ClrRuntime, pid: Int32) =
        let moduleName = 
            if clrModule.Name <> null 
            then clrModule.Name 
            else String.Format("Unamed_{0}", Guid.NewGuid().ToString("N"))

        let isFromGAC = moduleName.Contains("GAC_")      
        if not isFromGAC then
            let mutable errorMessage: String option = None  
            
            // module loaded via reflection
            let virtualQueryData = ref(new VirtualQueryData())
            if runtime.DataTarget.DataReader.VirtualQuery(clrModule.ImageBase, virtualQueryData) then
                let (result, assemblyBytes, outSize) = readMemory(runtime, clrModule.ImageBase, int32 ((!virtualQueryData).Size))
                if result then
                    // try to fix dynamic assembly
                    if clrModule.IsDynamic then
                        assemblyBytes.[0] <- M
                        assemblyBytes.[1] <- Z                    

                    match checkAssemblyDumpValidity(assemblyBytes) with
                    | Some assembly ->
                        let isDll = assembly.EntryPoint = null
                        let isExec = not isDll

                        // dispatch messages
                        messageBus.Dispatch(new ExtractedManagedModuleEvent(clrModule, assemblyBytes, isDll, isExec, Assembly = Some assembly))                            
                    | None ->
                        // file is mapped, try to dump it from memory
                        use streamPe = new MemoryStream(assemblyBytes)
                        use pe = PEFile.TryLoad(streamPe, true)     

                        let baseAddress = int32 <| if clrModule.ImageBase > 0uL then clrModule.ImageBase else pe.Header.ImageBase
                        match carveFileFromMemory(pe, baseAddress, runtime) with
                        | Some peBuffer ->
                            use streamPe = new MemoryStream(peBuffer)
                            use pe = PEFile.TryLoad(streamPe, false)
                            if pe <> null then
                                let isDll = pe.Header.Characteristics &&& IMAGE_FILE_DLL > uint16 0
                                let isExec = pe.Header.Characteristics &&& IMAGE_FILE_EXECUTABLE_IMAGE > uint16 0
                                messageBus.Dispatch(new ExtractedManagedModuleEvent(clrModule, peBuffer, isDll, isExec)) 
                                info("Carved Module from memory: " + moduleName)                               
                            else
                                errorMessage <- Some("Unable carve file from memory. Error during loading of extracted assembly.") 
                        | None -> 
                            errorMessage <- Some("Unable to dump dynamic module: " + moduleName + ". Error during loading of extracted assembly.")
                else
                    errorMessage <- Some("Unable to dump dynamic module: " + moduleName + ". Error reading memory") 
            else
                errorMessage <- Some("Unable to dump dynamic module: " + moduleName + ". Error accessing memory")

            // check error
            match errorMessage with 
            | Some msg ->
                if File.Exists(clrModule.FileName) then
                    dumpFilename(clrModule)
                else
                    error(msg)
            | _ -> ()

    let inspectBuffer(buffer: Byte array, runtime: ClrRuntime, baseAddress: Int32) =
        match checkAssemblyDumpValidity(buffer) with
        | Some assembly ->
            let isDll = assembly.EntryPoint = null
            let isExec = not isDll
            
            messageBus.Dispatch(new ExtractedManagedModuleViaMemoryScanEvent(buffer, isDll, isExec, Assembly = Some assembly))
            info(String.Format("Carved module '{0}' via memory scan", assembly.ManifestModule.ScopeName))
        | None ->
            // file is mapped, try to dump it from memory          
            use peMemStream = new MemoryStream(buffer)
            use pe = PEFile.TryLoad(peMemStream, false)      
            match carveFileFromMemory(pe, baseAddress, runtime) with
            | Some carvedBuffer ->
                use streamPe = new MemoryStream(carvedBuffer)
                use pe = PEFile.TryLoad(streamPe, false)
                if pe <> null then
                    let isDll = pe.Header.Characteristics &&& IMAGE_FILE_DLL > uint16 0
                    let isExec = pe.Header.Characteristics &&& IMAGE_FILE_EXECUTABLE_IMAGE > uint16 0
                    messageBus.Dispatch(new ExtractedManagedModuleViaMemoryScanEvent(carvedBuffer, isDll, isExec)) 
                    info("Carved Module via memory scan")
            | None -> ()

    let isPE(buffer: Byte array, offset: Int32) =
        if offset + 0x3C + 4 >= buffer.Length then false
        elif buffer.[offset] = M && buffer.[offset+1] = Z then
            let peOffset = BitConverter.ToInt32(buffer, offset + 0x3c)
            if peOffset >= 0 && peOffset + offset + 1 < buffer.Length-1 then
                buffer.[offset+peOffset] = P && buffer.[offset+peOffset+1] = E
            else
                false
        else false

    let scanMemoryForModules(runtime: ClrRuntime) =
        let mutable systemInfo = new SYSTEM_INFO()
        GetSystemInfo(&systemInfo)
        let mutable currentBaseAddress = systemInfo.lpMinimumApplicationAddress.ToInt64()
        let mutable size = int32 systemInfo.dwPageSize
        let maxMemSize = 100000000

        while currentBaseAddress < systemInfo.lpMaximumApplicationAddress.ToInt64() do
            let virtualQuery = ref(new VirtualQueryData())
            size <- int32 systemInfo.dwPageSize
            if runtime.DataTarget.DataReader.VirtualQuery(uint64 currentBaseAddress, virtualQuery) then
                size <- int32 <| (!virtualQuery).Size
                // max 100 MB
                if size < maxMemSize then
                    let (result, assemblyBytes, outSize) = readMemory(runtime, uint64 currentBaseAddress, size)
                    if result then
                        assemblyBytes
                        |> Array.iteri(fun i _ -> 
                            if isPE(assemblyBytes, i) then
                                let peBuffer = Array.sub assemblyBytes i (assemblyBytes.Length-i) 
                                let baseAddress = int32 currentBaseAddress + i
                                inspectBuffer(peBuffer, runtime, baseAddress)                    
                        )
                else
                    size <- size + maxMemSize
            currentBaseAddress <- currentBaseAddress + int64 size 

                                                
    let dumpModules(runtime: ClrRuntime, pid: Int32) =        
        // inspect Process modules
        let proc = Process.GetProcessById(pid)
        for procModule in proc.Modules do
            messageBus.Dispatch(new ExtractedProcessModule(procModule))        

        // inspect runtime modules
        runtime.Modules
        |> Seq.iter(fun clrModule ->
            extractModule(clrModule, runtime, pid)
        )            

        // memory scan
        scanMemoryForModules(runtime)

    member this.CanHandle(command: IMessage) =
        command :? DumpModulesCommand

    member this.Handle(command: IMessage) =
        let dumpCommand = command :?> DumpModulesCommand
        dumpModules(dumpCommand.Runtime.Value, dumpCommand.ProcessId.Value)

    interface IMessageHandler with
        member this.CanHandle(command: IMessage) =
            this.CanHandle(command)

        member this.Handle(command: IMessage) =
            this.Handle(command)