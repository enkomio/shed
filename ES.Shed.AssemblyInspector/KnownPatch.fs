namespace ES.Shed.AssemblyInspector

open System
open Harmony

type KnownPatch(callback: ExtractedInformation -> unit) =
    let mutable _harmony = HarmonyInstance.Create("enkomio.Shed")    
    do KnownPatch.Callback <- callback

    static member val Callback = fun (_: ExtractedInformation) -> () with get, set

    static member ExtractNetworkCredentialPassword(__instance: System.Net.Mail.SmtpClient) =
        if __instance.Credentials <> null then
            let credentials = __instance.Credentials :?> System.Net.NetworkCredential
            KnownPatch.Callback(NetworkCredentialPassword credentials.Password)

    member this.Apply() =
        let original = typeof<System.Net.Mail.SmtpClient>.GetMethod("set_Credentials")
        let enterMethod = this.GetType().GetMethod("ExtractNetworkCredentialPassword")
        _harmony.Patch(original, prefix=null, postfix=new HarmonyMethod(enterMethod)) |> ignore

    member this.Dispose() =
        _harmony.UnpatchAll()

    interface IDisposable with
        member this.Dispose() =
            this.Dispose()