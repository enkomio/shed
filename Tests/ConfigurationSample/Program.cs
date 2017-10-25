using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConfigurationSample
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("My pid: {0}", System.Diagnostics.Process.GetCurrentProcess().Id);
            var config = new Configuration1();
            config.ReadConfig();
            Console.WriteLine("Configuration {0} readed", config.SomeInstanceProperty);
            System.Threading.Thread.Sleep(-1);
        }
    }
}
