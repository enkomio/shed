using System;
using System.IO.Pipes;
using System.Reflection;

namespace ES.Shed.ManagedInjector
{
    internal class Client
    {
        private readonly NamedPipeClientStream _client = new NamedPipeClientStream(".", Constants.NamedPipeCode.ToString("X"), PipeDirection.InOut);
        private readonly PipeChanell _pipeChanell = null;

        private readonly Byte[] _assemblyContent;
        private readonly String _methodName = null;
        private InjectionResult _lastError = InjectionResult.Success;        
        
        public Client(Byte[] assemblyContent, String methodName)
        {
            _assemblyContent = assemblyContent;
            _methodName = methodName;
            _pipeChanell = new PipeChanell(_client);
        }

        public void ActivateAssembly()
        {
            try
            {
                _client.Connect(3000);
                if (_client.IsConnected)
                {
                    // send assembly and run it               
                    var result =
                        _pipeChanell.SendMessage(Constants.Ping) &&
                        SendToken() &&
                        SendAssembly() &&
                        _pipeChanell.SendMessage(Constants.Run);

                    _client.Dispose();

                    if (result)
                    {
                        SetLastError();
                    }
                }
                else
                {
                    _lastError = InjectionResult.UnableToConnectToNamedPipe;
                }
            }
            catch (TimeoutException)
            {
                _lastError = InjectionResult.UnableToConnectToNamedPipe;
            }
        }

        public InjectionResult GetLastError()
        {
            return _lastError;
        }

        private void SetLastError()
        {
            if (_lastError == InjectionResult.Success)
            {
                _lastError = _pipeChanell.GetLastError();
            }
        }

        private Int32 GetAssemblyEntryPointToken(Assembly assembly)
        {
            return assembly.EntryPoint.MetadataToken;
        }

        private Int32 GetSpecificMethodToken(Assembly assembly, String methodName)
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

        private Int32 GetMethodToken(Assembly assembly, String methodName)
        {
            var methodToken = 0;
            if (String.IsNullOrWhiteSpace(methodName))
            {
                methodToken = GetAssemblyEntryPointToken(assembly);
            }
            else
            {
                methodToken = GetSpecificMethodToken(assembly, methodName);
            }
            return methodToken;
        }

        private Boolean SendAssembly()
        {
            var stringBuffer = Convert.ToBase64String(_assemblyContent);
            return _pipeChanell.SendMessage(Constants.Assembly, stringBuffer);
        }

        private (String, Int32) GetModuleNameAndMethodToken()
        {
            try
            {
                var assembly = Assembly.Load(_assemblyContent);
                var methodToken = GetMethodToken(assembly, _methodName);
                return (assembly.ManifestModule.ScopeName, methodToken);
            }
            catch
            {
                return (null, 0);
            }
        }

        private Boolean SendToken()
        {
            var result = false;
            var (moduleName, methodToken) = GetModuleNameAndMethodToken();

            if (moduleName == null)
            {
                _lastError = InjectionResult.InvalidAssemblyBuffer;
            }
            else if (methodToken == 0)
            {
                _lastError = InjectionResult.MethodNotFound;
            }
            else
            {
                result = _pipeChanell.SendMessage(Constants.Token, methodToken.ToString());
            }

            return result;
        }
    }
}
