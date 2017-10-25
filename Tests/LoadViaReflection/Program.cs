using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LoadViaReflection
{
    class Program
    {
        static void Main(string[] args)
        {
            var assemblyBytes = Db.GetAssemblyBytes();
            var assembly = Assembly.Load(assemblyBytes);
            assembly.EntryPoint.Invoke(null, new Object[] { new String[] { } });
        }
    }
}
