namespace ES.Shed.AssemblyInspector

open System
open System.Collections.Generic
open System.Reflection.Emit
open dnlib.DotNet
open dnlib.DotNet.Emit
open Harmony
open System.Reflection

type AssemblyInspector(config: AssemblyInspectorConfig) =
    let _information = new List<ExtractedInformation>()

    let resolveMethodType(method: MethodDef) =
        AppDomain.CurrentDomain.GetAssemblies()
        |> Array.filter(fun assemblyName -> assemblyName.FullName.Equals(method.DeclaringType.DefinitionAssembly.FullName, StringComparison.OrdinalIgnoreCase))
        |> Array.map(fun assembly -> assembly.GetModules())
        |> Array.concat
        |> Array.map(fun moduleDef -> moduleDef.GetTypes() |> Seq.toArray)        
        |> Array.concat
        |> Array.find(fun modType -> modType.FullName.Equals(method.DeclaringType.FullName, StringComparison.OrdinalIgnoreCase))

    let resolveMethod(method: MethodDef) =
        let methodType = resolveMethodType(method)
        methodType.GetMethod(
            method.Name.ToString(), 
            BindingFlags.Public ||| 
            BindingFlags.NonPublic ||| 
            BindingFlags.Static ||| 
            BindingFlags.Instance
        )

    let createArgumentArray(parameters: ParameterInfo array) =
        parameters
        |> Array.map(fun parameter -> 
            if parameter.ParameterType = typeof<String>
            then String.Empty :> Object
            elif parameter.ParameterType.IsArray 
            then Array.CreateInstance(parameter.ParameterType, 0) :> Object
            else Activator.CreateInstance(parameter.ParameterType)
        )

    let collectInformation(info: ExtractedInformation) =
        lock _information (fun _ ->
            if not(_information.Contains(info)) then
                _information.Add(info)
        )

    let hookMethodAndInvokeIt(method: MethodDef) =
        let methodInfo = resolveMethod(method)

        let this =
            if not methodInfo.IsStatic 
            then 
                let constructor = methodInfo.DeclaringType.GetConstructors() |> Array.head
                Activator.CreateInstance(methodInfo.DeclaringType, createArgumentArray(constructor.GetParameters()))
            else null

        // create path
        let patch = new KnownPatch(collectInformation)
        patch.Apply()

        // invoke method
        let parameters = createArgumentArray(methodInfo.GetParameters())
        methodInfo.Invoke(this, parameters) |> ignore

    let isMethodInvokingMonitorFunction(method: MethodDef) =
        method.Body.Instructions
        |> Seq.toArray
        |> Array.exists(fun instruction ->
            if instruction.OpCode = OpCodes.Call || instruction.OpCode = OpCodes.Callvirt then
                let methodName =
                    match instruction.Operand with
                    | :? MethodSpec as methodSpec -> methodSpec.FullName
                    | :? MemberRef as memberRef -> memberRef.FullName
                    | :? MethodDef as methodDef -> methodDef.FullName
                    | _ -> failwith("Unknown operand type:" + instruction.Operand.ToString())

                methodName.Contains(config.MethodName)
            else 
                false
        )

    member this.Run() =
        AppDomain.CurrentDomain.GetAssemblies()
        |> Array.map(fun assembly -> assembly.GetModules())
        |> Array.concat
        |> Array.filter(fun assemblyModule -> assemblyModule.Name.StartsWith(config.AssemblyName, StringComparison.OrdinalIgnoreCase))
        |> Array.map(fun assemblyModule -> ModuleDefMD.Load(assemblyModule))
        |> Array.map(fun moduleDef -> moduleDef.GetTypes() |> Seq.toArray)        
        |> Array.concat
        |> Array.map(fun typeDef -> typeDef.Methods |> Seq.toArray)
        |> Array.concat
        |> Array.filter(isMethodInvokingMonitorFunction)
        |> Array.iter(hookMethodAndInvokeIt)

        // return result
        _information |> Seq.toArray