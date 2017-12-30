namespace ES.Shed
#nowarn "9"

open System
open System.Runtime.InteropServices

module internal Native =
    [<Struct>]
    [<StructLayout(LayoutKind.Sequential)>]
    type SYSTEM_INFO =
        val mutable dwOemId: UInt32
        val mutable dwPageSize: UInt32
        val mutable lpMinimumApplicationAddress: IntPtr
        val mutable lpMaximumApplicationAddress: IntPtr
        val mutable dwActiveProcessorMask: IntPtr
        val mutable dwNumberOfProcessors: UInt32
        val mutable dwProcessorType: UInt32
        val mutable dwAllocationGranularity: UInt32
        val mutable dwProcessorLevel: UInt16
        val mutable dwProcessorRevision: UInt16

    [<DllImport("kernel32.dll")>]
    extern void GetSystemInfo(SYSTEM_INFO& lpSystemInfo)