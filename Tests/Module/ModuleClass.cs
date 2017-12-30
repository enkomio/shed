using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Module
{
    /**
     * Compile with: csc /target:module /platform:x86 ModuleClass.cs
     * */
    class ModuleClass
    {
        public ModuleClass()
        {
            Console.WriteLine("ModuleClass created");
        }

        private void PrintModule(System.Reflection.Module module)
        {
            Console.WriteLine();
            Console.WriteLine("Name: " + module.FullyQualifiedName);
            Console.WriteLine("Types:");
            foreach(var type in module.GetTypes())
            {
                Console.Write(" " + type.FullName);
            }
            Console.WriteLine();
        }

        public void Inspect()
        {
            Console.WriteLine("CodeBase: " + Assembly.GetExecutingAssembly().GetName().CodeBase);

            Console.WriteLine("GetModules");
            foreach (var module in Assembly.GetExecutingAssembly().GetModules())
            {
                PrintModule(module);
            }
            Console.WriteLine("---------------------------");
        }
    }
}
