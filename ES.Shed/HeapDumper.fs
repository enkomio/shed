namespace ES.Shed

open System
open System.Text
open System.Text.RegularExpressions
open System.IO
open System.Reflection
open System.Collections.Generic
open Microsoft.Diagnostics.Runtime
open System.Runtime.Remoting
open System.Runtime.InteropServices
open System.Security

type HeapDumper(settings: HandlerSettings) =
    let _objectsAlreadyAnalyzed = new HashSet<UInt64>()
    let _basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    let _loggedStrings = new HashSet<String>()
    let (trace, info, _, error) = createLoggers(settings.MessageBus)

    let isInterestingString(o: HeapObject) =
        if o.Value <> null && o.Value.ToString().Length >= 10 then
            _loggedStrings.Add(o.Value.ToString())
        else
            false

    let traceString(o: HeapObject) =
        if o.Type.Equals(typeof<String>.FullName, StringComparison.OrdinalIgnoreCase) then
            let callback = if isInterestingString(o) then info else trace
            try
                let base64Decoded = Encoding.Default.GetString(Convert.FromBase64String(o.Value.ToString()))
                let checks = [Char.IsLetterOrDigit; Char.IsPunctuation; Char.IsSeparator; Char.IsWhiteSpace]
                if 
                    not(String.IsNullOrWhiteSpace(base64Decoded)) && 
                    base64Decoded.Length >= 10 &&
                    base64Decoded.ToCharArray() |> Array.forall(fun c -> checks |> List.exists(fun f -> f(c))) 
                    then
                    info(String.Format("{0}. Base64 decoded is: {1}", o, base64Decoded))
                else
                    callback(o.ToString())
            with _ -> callback(o.ToString())

    let loadFileContent(filename: String) =
        let file = Path.Combine(_basePath, filename)
        if File.Exists(file) then
            File.ReadAllLines(file)
            |> Array.map(fun l -> l.Trim().ToLower())
            |> Array.filter(fun l -> l.StartsWith("#") |> not)
        else
            Array.empty<String>

    let _blackListClasses = loadFileContent("blacklist.txt")
    let _whiteListFileClasses = loadFileContent("whitelist.txt")

    let exceptionError(e: Exception) =
        error("Error during dumping heap object. Message: " + e.Message)

    let isTypeAllowed(clrType: ClrType) = 
        let name = clrType.Name.ToLower()
        if _whiteListFileClasses |> Array.exists(fun prefix -> name.StartsWith(prefix)) |> not then
            // check if it is blacklisted
            _blackListClasses |> Array.exists(fun prefix -> name.StartsWith(prefix)) |> not
        else
            // it is whitelisted
            true            

    let isValid(clrType: ClrType) =
        clrType <> null && 
        not clrType.IsFree &&         
        isTypeAllowed(clrType)
        
    let isPrimitive(clrType: ClrType)   =
        clrType.IsPrimitive || clrType.IsString
                
    let rec dumpObjectValue(clrType: ClrType, objAddr: UInt64, node: HeapObject, heap: ClrHeap) =
        if clrType.IsArray then
            let arrayLength = clrType.GetArrayLength(objAddr)
            let byteArray = new List<Byte>()

            for i=0 to arrayLength-1 do
                let arrayElemValue = clrType.GetArrayElementValue(objAddr, i)

                if isValid(clrType.ComponentType) then
                    if isPrimitive(clrType.ComponentType) then
                        let arrayElemAddr = clrType.GetArrayElementAddress(objAddr, i)
                        let arrayElemNode = createHeapObject(clrType.ComponentType.Name, arrayElemAddr, arrayElemValue, None)                    
                        node.Properties.Add(arrayElemNode)
                        traceString(arrayElemNode)

                        if clrType.ComponentType.Name.Equals(typeof<Byte>.ToString()) then
                            byteArray.Add(arrayElemNode.Value :?> Byte)

                    elif arrayElemValue <> null then
                        analyzeObjectAddress(arrayElemValue :?> UInt64, heap, node, None)


        elif clrType.Name.StartsWith("System.Security.SecureString") then
            // dump all fields
            dumpAllFields(clrType, objAddr, node, heap)

            // now extract the passwords and add it
            let mBuffer = clrType.GetFieldByName("m_buffer").GetValue(objAddr)
            let mLength = clrType.GetFieldByName("m_length").GetValue(objAddr) :?> Int32
            let bstrClrType = heap.GetObjectType(mBuffer :?> UInt64)

            // compute correct length
            let mutable correctLength = 2 * mLength
            if correctLength % 8 <> 0
            then correctLength <- 2 * (mLength + 8 - (mLength % 8))

            //let bstrValue = bstrClrType.GetValue(mBuffer :?> UInt64)
            let handle = bstrClrType.GetFieldByName("handle").GetValue(mBuffer :?> UInt64) :?> Int64

            // read the remote password value
            let passwordArray = Array.zeroCreate<Byte>(correctLength)
            let tmp = ref 0
            heap.Runtime.ReadMemory(uint64 handle, passwordArray, passwordArray.Length, tmp) |> ignore
            let password = Encoding.Unicode.GetString(passwordArray).Substring(0, mLength)

            let passwordObject = createHeapObject(typeof<String>.Name, uint64 handle, password, Some "Password")
            node.Properties.Add(passwordObject)
                
        elif clrType.Name.StartsWith("System.Collections.Generic.Dictionary") then            
            let entriesField = clrType.GetFieldByName("entries")
            if entriesField <> null then
                // extract values
                let entriesFieldAddr = entriesField.GetValue(objAddr):?> UInt64
                if entriesFieldAddr > 0uL then
                    let entriesArray = heap.GetObjectType(entriesFieldAddr)
                    let arrayComponent = entriesArray.ComponentType
                    let keyField = arrayComponent.GetFieldByName("key")
                    let valueField = arrayComponent.GetFieldByName("value")

                    for i=0 to entriesArray.GetArrayLength(entriesFieldAddr) do
                        let arrayElementAddr = entriesArray.GetArrayElementAddress(entriesFieldAddr, i)  
                    
                        let kvNode = createHeapObject(typedefof<KeyValuePair<Object, Object>>.Name, 0uL, null, None)
                                        
                        if isValid(keyField.Type) && isValid(valueField.Type) then
                            // analyze key
                            let keyFieldAddr = keyField.GetAddress(entriesFieldAddr)
                            let keyFieldVal = keyField.GetValue(entriesFieldAddr, true)

                            if isPrimitive(keyField.Type) then
                                let keyFieldNode = createHeapObject(keyField.Type.Name, keyFieldAddr, keyFieldVal, Some keyField.Name)
                                traceString(keyFieldNode)
                                kvNode.Properties.Add(keyFieldNode)
                            else                            
                                let keyAddr = keyField.GetValue(arrayElementAddr, true)
                                if keyAddr <> null then
                                    analyzeObjectAddress(keyAddr :?> uint64, heap, kvNode, Some keyField.Name)

                            // analyze value
                            let valueFieldAddr = valueField.GetAddress(entriesFieldAddr)
                            let valueFieldVal = valueField.GetValue(entriesFieldAddr, true)

                            if isPrimitive(valueField.Type) then
                                let valueFieldNode = createHeapObject(valueField.Type.Name, valueFieldAddr, valueFieldVal, Some valueField.Name)
                                traceString(valueFieldNode)
                                kvNode.Properties.Add(valueFieldNode)
                            else
                                let valueAddr = valueField.GetValue(arrayElementAddr, true)
                                if valueAddr <> null then
                                    analyzeObjectAddress(valueAddr :?> uint64, heap, kvNode, Some valueField.Name)

                            if kvNode.Properties |> Seq.isEmpty |> not then
                                node.Properties.Add(kvNode)

        else
            dumpAllFields(clrType, objAddr, node, heap)
            
    and dumpAllFields(clrType: ClrType, objAddr: UInt64, node: HeapObject, heap: ClrHeap) =
        // dump all fields of this object (it is not primitive)
        for clrField in clrType.Fields do
            let field = clrType.GetFieldByName(clrField.Name)
            let fieldAddr = field.GetAddress(objAddr)
            let fieldValue = field.GetValue(objAddr)
            let name = Some clrField.Name

            if isValid(field.Type) then                
                if isPrimitive(field.Type) then
                    let fieldNode = createHeapObject(field.Type.Name, fieldAddr, fieldValue, name)
                    traceString(fieldNode)
                    node.Properties.Add(fieldNode)
                elif fieldValue <> null then
                    analyzeObjectAddress(fieldValue :?> UInt64, heap, node, name)

    and analyzeObjectAddress(objAddr: UInt64, heap: ClrHeap, parent: HeapObject, name: String option) =         
        try
            if objAddr > 0uL then 
                if _objectsAlreadyAnalyzed.Add(objAddr) then
                    let clrType = heap.GetObjectType(objAddr)                         
                    if isValid(clrType) then
                        let objValue = clrType.GetValue(objAddr)
                        let node = createHeapObject(clrType.Name, objAddr, objValue, name)
                        parent.Properties.Add(node)

                        if isPrimitive(clrType) then 
                            traceString(node)                        
                        else                        
                            dumpObjectValue(clrType, objAddr, node, heap)                        
                else
                    let refNode = createHeapObject(String.Empty, 0uL, null, Some String.Empty)
                    refNode.Reference <- objAddr
                    parent.Properties.Add(refNode)
        with e -> 
            if not <| e.Message.Contains("Unexpected element type.") then
                exceptionError(e)

    let handleDumpHeapCommand(command: DumpHeapCommand) =
        let heap = command.Runtime.Value.Heap
        if heap.CanWalkHeap then
            _objectsAlreadyAnalyzed.Clear()

            // analyze all objects in the heap
            let root = createHeapObject(String.Empty, 0uL, null, None)
            for objAddr in heap.EnumerateObjectAddresses() do
                analyzeObjectAddress(objAddr, heap, root, None)

            settings.MessageBus.Dispatch(new HeapWalked(root))
            info("Heap dump completed")
        else
            error("Heap is not walkable")

    member this.CanHandle(message: IMessage) =
        message :? DumpHeapCommand

    member this.Handle(msg: IMessage) =        
        handleDumpHeapCommand(msg :?> DumpHeapCommand)

    interface IMessageHandler with
        member this.CanHandle(command: IMessage) =
            this.CanHandle(command)

        member this.Handle(command: IMessage) =
            this.Handle(command)