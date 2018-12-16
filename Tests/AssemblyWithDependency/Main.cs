using System.IO;

namespace AssemblyWithDependency
{
    public class Main
    {
        public void Run()
        {
            var entity = new Entity("TYPE", "SOME VALUE");
            File.AppendAllText("log.txt", "\r\nCur dir: " + Directory.GetCurrentDirectory());
            File.AppendAllText("log.txt", "\r\nSerialized value: " + entity.Serialize());
        }
    }
}
