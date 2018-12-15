using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ES.Shed.ManagedInjector
{
    public class Injector
    {
        private readonly Int32 _pid;
        private readonly Byte[] _assemblyContent;
        private readonly String _methodName = null;
        private Process _process = null;
        private IntPtr _processHandle = IntPtr.Zero;        

        public Injector(Int32 pid, Byte[] assemblyContent, String methodName)
        {
            _pid = pid;
            _assemblyContent = assemblyContent;
            _methodName = methodName;
        }        

        public InjectionResult Inject()
        {
            var result = InjectionResult.UnknownError;
            
            try
            {
                _process = Process.GetProcessById(_pid);
            }
            catch
            {
                result = InjectionResult.PidNotValid;
            }


            if (_process != null)
            {
                try
                {
                    UInt32 threadId = 0;
                    foreach (var windowHandle in GetProcessWindows(_pid))
                    {
                        _processHandle = windowHandle;
                        threadId = Methods.GetWindowThreadProcessId(windowHandle, IntPtr.Zero);
                        if (threadId > 0)
                        {
                            Remote.hookHandle = InjectIntoThread(threadId);
                            if (Remote.hookHandle != IntPtr.Zero)
                            {
                                ActivateHook();                                
                                if (VerifyInjection())
                                {
                                    result = ActivateAssembly();
                                    break;
                                }
                                else
                                {
                                    result = InjectionResult.InjectionFailed;
                                }
                            }
                            else
                            {
                                result = InjectionResult.InjectionFailed;
                            }
                        }
                        else
                        {
                            result = InjectionResult.WindowThreadNotFound;
                        }
                    }
                }
                catch { }
            }

            return result;
        }

        private InjectionResult ActivateAssembly()
        {
            var client = new Client(_assemblyContent, _methodName);
            client.ActivateAssembly();
            return client.GetLastError();
        }

        private IntPtr[] GetProcessWindows(Int32 pid)
        {
            // Yes, I copied this piece of code from StackOverFlow
            // src: https://stackoverflow.com/a/25152035/1422545
            var apRet = new List<IntPtr>();
            var pLast = IntPtr.Zero;
            var currentPid = 0;

            do
            {
                pLast = Methods.FindWindowEx(IntPtr.Zero, pLast, null, null);                
                Methods.GetWindowThreadProcessId(pLast, out currentPid);

                if (currentPid == pid)
                    apRet.Add(pLast);

            } while (pLast != IntPtr.Zero);

            return apRet.ToArray();
        }   

        private Boolean VerifyInjection()
        {
            _process.Refresh();
            var moduleName = typeof(Injector).Module.Name;
            var moduleFound = false;
            foreach (ProcessModule procModule in _process.Modules)
            {
                var fileName = Path.GetFileName(procModule.FileName);
                if (fileName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
                {
                    moduleFound = true;
                    break;
                }
            }
            return moduleFound;
        }

        private IntPtr InjectIntoThread(UInt32 threadId)
        {
            var thisModule = typeof(Injector).Module;
            var moduleHandle = Methods.LoadLibrary(thisModule.Name);

            // get addr exported function
            var hookProc = Methods.GetProcAddress(moduleHandle, "HookProc");
            return Methods.SetWindowsHookEx(Constants.WH_CALLWNDPROC, hookProc, moduleHandle, threadId);
        }

        private void ActivateHook()
        {
            Methods.SendMessage(_processHandle, Constants.InjectorMessage, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
