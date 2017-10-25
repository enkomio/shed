using System;
using System.Diagnostics;

namespace HelloWorld
{
    class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {                
                Console.WriteLine("Hello world from program: " + Process.GetCurrentProcess().Id.ToString());
                System.Threading.Thread.Sleep(10000);
            }
        }
    }
}
