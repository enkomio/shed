using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace LoadViaReflection
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Wait a bit...");
            Thread.Sleep(2000);
            var assemblyBytes = Db.GetAssemblyBytes();
            var assembly = Assembly.Load(assemblyBytes);
            assembly.EntryPoint.Invoke(null, new Object[] { new String[] { } });
        }
    }
}
