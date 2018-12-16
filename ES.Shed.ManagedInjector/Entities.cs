using System;

namespace ES.Shed.ManagedInjector
{
    internal class PipeMessage
    {
        private readonly String _type;
        private readonly String _data;
        
        public PipeMessage(String type, String data)
        {
            _type = type;
            _data = data;
        }

        public PipeMessage(String type) : this(type, String.Empty)
        { }

        public Boolean IsSuccess()
        {
            return _type.Equals(Constants.Ok, StringComparison.OrdinalIgnoreCase);
        }

        public String GetData()
        {
            return _data;
        }

        public new String GetType()
        {
            return _type;
        }

        public String Serialize()
        {
            return String.Format("{0}|{1}", _type, _data);
        }

        public static PipeMessage Create(String serializedMsg)
        {
            var indexOfPipe = serializedMsg.IndexOf('|');
            var type = serializedMsg.Substring(0, indexOfPipe);
            var data = serializedMsg.Substring(indexOfPipe + 1);
            return new PipeMessage(type, data);
        }
    }

    internal static class Constants
    {
        public const Int32 WH_CALLWNDPROC = 4;
        public static readonly Int32 InjectorMessage = Methods.RegisterWindowMessage("InjectorMessage");

        public static readonly UInt32 NamedPipeCode = 0xAC1DC0DE;

        // commands
        public static readonly String Ok = "OK";
        public static readonly String Error = "ERROR";
        public static readonly String Ping = "PING";
        public static readonly String Token = "TOKEN"; 
        public static readonly String Assembly = "ASSEMBLY";
        public static readonly String Dependency = "DEPENDENCY";
        public static readonly String Run = "RUN";
    }

    public enum InjectionResult : Int32
    {
        Success = 0,
        InjectionFailed = 1,
        WindowThreadNotFound = 2,
        InvalidAssemblyBuffer = 3,
        MethodNotFound = 4,
        PidNotValid = 5,
        UnknownError = 6,
        UnableToConnectToNamedPipe = 7,
        ErrorDuringInvocation = 8,
        InvalidAssemblyDependencyBuffer = 9,
    }
}
