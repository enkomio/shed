using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;

namespace SmtpClientCredentials
{
    public class MailSender
    {
        private String _destination = null;
        private String _account = "my_username:my_password";

        public MailSender(String destination)
        {
            this._destination = destination;
        }

        public void Send()
        {
            var items = _account.Split(':');
            var username = items[0];
            var password = items[1];
            var credentials = new NetworkCredential(username, password);

            try
            {                
                Thread.Sleep(2000);
                var smtpClient = new SmtpClient("127.0.0.1", 25)
                {
                    Credentials = credentials
                };
                var message = new MailMessage("from@localhost.local", _destination)
                {
                    Subject = "subject",
                    Body = "body"
                };

                smtpClient.Send(message);
            }
            catch { }
        }
    }


    public static class Program
    {
        public static void Main(string[] args)
        {
            var i = 0;
            var mailSender = new MailSender("to@localhost.local");
            while(true)
            {
                Console.WriteLine("{0} - My pid: {1}", i++, System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
                mailSender.Send();
            }
        }
    }
}
