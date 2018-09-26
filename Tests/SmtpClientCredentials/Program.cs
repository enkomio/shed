using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace SmtpClientCredentials
{
    class Program
    {
        static unsafe void Main(string[] args)
        {
            try
            {
                var smtpClient = new SmtpClient("127.0.0.1", 25)
                {
                    Credentials = new NetworkCredential("my_username", "my_password")
                };
                var message = new MailMessage("from@localhost.local", "to@localhost.local")
                {
                    Subject = "subject",
                    Body = "body"
                };
                
                smtpClient.Send(message);
            }
            catch { }
            System.Threading.Thread.Sleep(10000);
        }
    }
}
