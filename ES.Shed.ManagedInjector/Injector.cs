using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ES.Shed.ManagedInjector
{
    public class Injector
    {
        // commands definition
        private static readonly Int32 ContentCommand = 1;
        private static readonly Int32 CompletedCommand = 2;
        private static readonly Int32 MethodTokenCommand = 3;

        private static IntPtr _hookHandle = IntPtr.Zero;
        private static readonly List<Byte> _receivedBuffer = new List<Byte>();
        private static Int32 _receivedMethodToken = 0;

        private readonly Int32 _pid;
        private readonly Byte[] _sentBuffer;
        private Process _process = null;
        private IntPtr _processHandle = IntPtr.Zero;
        private String _methodName = null;

        public Injector(Int32 pid, Byte[] buffer, String methodName)
        {
            _pid = pid;
            _sentBuffer = buffer;
            _methodName = methodName;
        }        

        public InjectionCodes Inject()
        {
            var result = InjectionCodes.UnknownError;
            var (moduleName, methodToken) = GetModuleNameAndMethodToken();

            if (moduleName == null)
            {
                result = InjectionCodes.InvalidAssemblyBuffer;
            }
            else if (methodToken == 0)
            {
                result = InjectionCodes.MethodNotFound;
            }
            else
            {
                try
                {
                    _process = Process.GetProcessById(_pid);
                }
                catch
                {
                    result = InjectionCodes.PidNotValid;
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
                                _hookHandle = InjectIntoThread(threadId);
                                if (_hookHandle != IntPtr.Zero)
                                {
                                    ActivateHook();
                                    if (VerifyInjection(typeof(Injector).Module.Name))
                                    {
                                        SendInformation(methodToken);
                                        result = InjectionCodes.Success;
                                        break;
                                    }
                                    else
                                    {
                                        result = InjectionCodes.InjectionFailed;
                                    }
                                }
                                else
                                {
                                    result = InjectionCodes.InjectionFailed;
                                }
                            }
                            else
                            {
                                result = InjectionCodes.WindowThreadNotFound;
                            }
                        }
                    }
                    catch { }
                }

            }
            return result;
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

        private (String, Int32) GetModuleNameAndMethodToken()
        {
            try
            {
                var assembly = Assembly.Load(_sentBuffer);
                var methodToken = GetMethodToken(assembly, _methodName);
                return (assembly.ManifestModule.ScopeName, methodToken);
            }
            catch
            {
                return (null, 0);
            }
        }

        private Int32 GetMethodToken(Assembly assembly, String methodName)
        {
            var methodToken = 0;
            foreach (var type in assembly.GetTypes())
            {
                var typeName = type.FullName;
                foreach (var method in type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                {
                    var fullname = String.Format("{0}.{1}", typeName, method.Name);
                    if (methodName.Equals(fullname, StringComparison.OrdinalIgnoreCase))
                    {
                        methodToken = method.MetadataToken;
                        break;
                    }
                }

                if (methodToken != 0) break;
            }
            return methodToken;
        }

        private Boolean VerifyInjection(String moduleName)
        {
            _process.Refresh();

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
            SendMessage(IntPtr.Zero, 0);
        }

        private void SendMessage(IntPtr data, Int32 command)
        {
            Methods.SendMessage(_processHandle, Constants.InjectorMessage, data, new IntPtr(command));
        }

        private void SendInformation(Int32 methodToken)
        {
            // send the method token
            SendMessage(new IntPtr(methodToken), MethodTokenCommand);

            // send the buffer
            foreach (var b in _sentBuffer)
            {
                SendMessage(new IntPtr(b), ContentCommand);
            }

            // tell that the process is completed
            SendMessage(IntPtr.Zero, CompletedCommand);
        }

        private static Object[] CreateArgumentArray(ParameterInfo[] parameters)
        {
            var parameterValues = new List<Object>();
            foreach (var parameter in parameters)
            {
                if (parameter.ParameterType == typeof(String))
                {
                    parameterValues.Add(String.Empty);
                }
                else if (parameter.ParameterType.IsArray)
                {
                    parameterValues.Add(Array.CreateInstance(parameter.ParameterType, 0));
                }
                else
                {
                    parameterValues.Add(Activator.CreateInstance(parameter.ParameterType));
                }
            }
            return parameterValues.ToArray();
        }


        private static void InvokeMethod(MethodBase method)
        {
            var arguments = CreateArgumentArray(method.GetParameters());
            Object thisObj = null;

            // check if I have to create an instance to invoke the method
            if (!method.IsStatic)
            {
                var constructor = method.DeclaringType.GetConstructors().FirstOrDefault();
                if (constructor != null)
                {
                    var constructorArguments = CreateArgumentArray(constructor.GetParameters());
                    thisObj = Activator.CreateInstance(method.DeclaringType, constructorArguments);
                }
            }

            // invoke the method in a new Task            
            Task.Factory.StartNew(() => method.Invoke(thisObj, arguments), TaskCreationOptions.LongRunning);
        }

        private static void ActivateDll()
        {
            try
            {
                var assembly = Assembly.Load(_receivedBuffer.ToArray());
                foreach (var module in assembly.Modules)
                {
                    try
                    {
                        var methodToInvoke = module.ResolveMethod(_receivedMethodToken);
                        InvokeMethod(methodToInvoke);
                        break;
                    }
                    catch { }
                }
            }
            catch { }
        }

        [DllExport]
        public static IntPtr HookProc(Int32 code, IntPtr wParam, IntPtr lParam)
        {
            if (lParam != IntPtr.Zero)
            {
                var cwp = (CWPSTRUCT)Marshal.PtrToStructure(lParam, typeof(CWPSTRUCT));
                if (cwp.message == Constants.InjectorMessage)
                {
                    if (cwp.lparam.ToInt32() == ContentCommand)
                    {
                        var value = (byte)cwp.wparam.ToInt32();
                        _receivedBuffer.Add(value);
                    }
                    else if (cwp.lparam.ToInt32() == CompletedCommand)
                    {
                        ActivateDll();
                        Methods.UnhookWindowsHookEx(_hookHandle);
                    }
                    else if (cwp.lparam.ToInt32() == MethodTokenCommand)
                    {
                        _receivedMethodToken = cwp.wparam.ToInt32();
                    }
                }
            }

            return Methods.CallNextHookEx(_hookHandle, code, wParam, lParam);
        }
    }
}
