using System;
using System.IO;

namespace ES.Shed.ManagedInjector
{
    internal class PipeChanell
    {
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private InjectionResult _lastError = InjectionResult.Success;

        public PipeChanell(Stream stream)
        {
            _reader = new StreamReader(stream);
            _writer = new StreamWriter(stream);
        }

        public Boolean SendMessage(String type, String data)
        {
            return SendMessage(new PipeMessage(type, data));
        }

        public Boolean SendMessage(String type)
        {
            return SendMessage(new PipeMessage(type, String.Empty));
        }        

        public InjectionResult GetLastError()
        {
            return _lastError;
        }

        public Boolean SendMessage(PipeMessage msg)
        {
            var response = SendData(msg);
            if (!response.IsSuccess())
            {
                _lastError = (InjectionResult)Int32.Parse(response.GetData());
            }
            return response.IsSuccess();
        }

        public PipeMessage GetMessage()
        {
            return PipeMessage.Create(ReadData());
        }
        
        public void SendAck(InjectionResult code)
        {
            var type = code == InjectionResult.Success ? Constants.Ok : Constants.Error;
            var ackMsg = new PipeMessage(type, ((Int32)code).ToString());
            _writer.WriteLine(ackMsg.Serialize());
            _writer.Flush();
        }

        private String ReadData()
        {
            return _reader.ReadLine();
        }

        private PipeMessage SendData(PipeMessage msg)
        {
            _writer.WriteLine(msg.Serialize());
            _writer.Flush();
            var ack = _reader.ReadLine();
            return PipeMessage.Create(ack);
        }
    }
}
