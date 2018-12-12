using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace HelloWorld
{
    public class Printer
    {
        private String _prefix = "Hello world from program: ";

        public String GetMessage()
        {
            var id = Process.GetCurrentProcess().Id;
            return _prefix + id.ToString();
        }
    }

    public class Program
    {
        public static String CreateMessage()
        {
            var printer = new Printer();
            return printer.GetMessage();
        }
        
        public static void Main(string[] args)
        {   
            while (true)
            {
                var msg = CreateMessage();
                Console.WriteLine(msg);
                System.Threading.Thread.Sleep(3000);
            }
        }
    }
}
