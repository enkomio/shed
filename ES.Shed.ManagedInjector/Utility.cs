using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace ES.Shed.ManagedInjector
{
    internal static class Utility
    {
        private static String _msPublicKey = null;

        private static String TryGetPublicKey(Assembly assembly)
        {
            String publicKey = null;
            var evidence = assembly.Evidence.GetHostEnumerator();
            while (evidence.MoveNext())
            {
                var publisher = evidence.Current as Publisher;
                if (publisher != null)
                {
                    var cert = publisher.Certificate;
                    publicKey = BitConverter.ToString(cert.GetPublicKey()).Replace("-", String.Empty);
                }
            }
            return publicKey;
        }

        private static String GetMsPublicKey()
        {
            if (_msPublicKey == null)
            {
                _msPublicKey = TryGetPublicKey(String.Empty.GetType().Assembly);
            }

            return _msPublicKey;
        }

        public static Boolean IsBclAssembly(Assembly assembly)
        {
            var result = false;
            var assemblyPubKey = TryGetPublicKey(assembly);
            if (assemblyPubKey != null)
            {
                result = assemblyPubKey.Equals(GetMsPublicKey(), StringComparison.OrdinalIgnoreCase);
            }

            return result;
        }
    }
}
