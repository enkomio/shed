using System;
using System.Runtime.InteropServices;

namespace ES.Shed.ManagedInjector
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct CWPSTRUCT
    {
        public IntPtr lparam;
        public IntPtr wparam;
        public int message;
        public IntPtr hwnd;
    }
    
    internal static class Methods
    {   
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr SetWindowsHookEx(Int32 idHook, IntPtr callback, IntPtr hInstance, UInt32 threadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool UnhookWindowsHookEx(IntPtr hInstance);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr CallNextHookEx(IntPtr idHook, Int32 nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern Int32 SendMessage(IntPtr hWnd, Int32 wMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern Int32 RegisterWindowMessage(String lpString);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr parentWindow, IntPtr previousChildWindow, string windowClass, string windowTitle);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowThreadProcessId(IntPtr window, out int process);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr LoadLibrary(String lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, String procName);
    }
}
