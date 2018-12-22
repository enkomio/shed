using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace InjectedAssembly
{
    public class EntryPoint
    {
        public static void Inject()
        {
            // get static field form
            var mainForm = Assembly.GetEntryAssembly().GetType("WindowsFormHelloWorld.Program");            
            var formField = mainForm.GetField("form", BindingFlags.Static | BindingFlags.NonPublic);
            dynamic form = formField.GetValue(null);

            // set new value
            var label = form.Controls[0];
            label.Text = "Hooked value!";
            form.Refresh();
        }
    }
}
