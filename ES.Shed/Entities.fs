namespace ES.Shed

open System
open System.Collections.Generic
open System.Runtime.Serialization

[<AutoOpen>]
module Entities =
    
    [<DataContract>]
    type HeapObject() =
        [<DataMember>]
        member val Address = 0uL with get, set

        [<DataMember>]
        member val Reference = 0uL with get, set

        [<DataMember>]
        member val Type = String.Empty with get, set
        
        [<DataMember>]
        member val Name = None with get, set
        
        [<DataMember>]
        member val Value = null with get, set

        [<DataMember>]
        member val Properties = new List<HeapObject>() with get, set
    
        override this.ToString() =
            match this.Name with
            | Some name -> String.Format("[{0}] (named) 0x{1}: {2} => {3}", this.Type, this.Address.ToString("X"), name, this.Value)
            | None -> String.Format("[{0}] 0x{1}: {2}", this.Type, this.Address.ToString("X"), this.Value)

    let createHeapObject(t: String, objAddr: UInt64, objValue: Object, name: String option) =
        new HeapObject(Address = objAddr, Type = t, Value = objValue, Name = name)