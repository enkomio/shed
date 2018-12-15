using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ES.Shed.ManagedInjector
{
    public static class Remote
    {
        public static IntPtr hookHandle = IntPtr.Zero;

        private static void ProcessClientCommands()
        {
            var server = new Server();
            server.ProcessCommands();
        }

        [DllExport]
        public static IntPtr HookProc(Int32 code, IntPtr wParam, IntPtr lParam)
        {
            if (lParam != IntPtr.Zero)
            {
                var cwp = (CWPSTRUCT)Marshal.PtrToStructure(lParam, typeof(CWPSTRUCT));
                if (cwp.message == Constants.InjectorMessage)
                {
                    // run the code in a new task to avoid to block the SendMessage
                    Task.Factory.StartNew(() => {
                        ProcessClientCommands();
                        Methods.UnhookWindowsHookEx(hookHandle);
                    });                    
                }
            }

            return Methods.CallNextHookEx(hookHandle, code, wParam, lParam);
        }
    }
}
