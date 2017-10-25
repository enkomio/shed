using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConfigurationSample
{
    enum ConfigKey {
        Secret1,
        Secret2,
        Secret3
    }

    class Configuration1
    {
        public static String EncryptionKey = Encoding.Default.GetString(Convert.FromBase64String("ZW5jcnlwdGlvbmtleQ==")); // encryptionkey

        public String SomeInstanceProperty;
        public Dictionary<ConfigKey, Object> Settings { get; private set; }

        public Configuration1()
        {
            this.SomeInstanceProperty = String.Empty;
            this.Settings = new Dictionary<ConfigKey, Object>();
        }

        private String Decrypt(Byte[] data)
        {
            var result = new StringBuilder();
            var i = 0;
            foreach(var b in data)
            {
                var k = (Int32)Configuration1.EncryptionKey[i++ % EncryptionKey.Length];
                var c = (Int32)b ^ k;
                result.Append((Char)c);
            }
            return result.ToString();
        }
            
        public void ReadConfig()
        {
            this.SomeInstanceProperty = Guid.NewGuid().ToString("N");
            var secret1 = Encoding.ASCII.GetString(new Byte[] { 115, 111, 109, 101, 32, 100, 105, 114, 116, 121, 32, 115, 101, 99, 114, 101, 116 });
            var secret2 = Decrypt(new Byte[] { 60, 1, 22, 82, 14, 25, 24, 5, 79, 0, 14, 19, 28, 23, 78, 5, 27, 23, 20, 84, 29, 7, 7, 24, 69, 26, 10, 0, 5, 27, 30, 5, 6, 8, 27, 7, 4, 11 });


            this.Settings.Add(ConfigKey.Secret1, "some dirty secret");
            this.Settings.Add(ConfigKey.Secret2, secret2);
            this.Settings.Add(ConfigKey.Secret3, Encoding.Default.GetString(Convert.FromBase64String("U3VwZXJTM2NyZXQ=")));
        }
    }
}
