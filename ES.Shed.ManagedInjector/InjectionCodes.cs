using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ES.Shed.ManagedInjector
{
    public enum InjectionCodes
    {
        Success,
        InjectionFailed,
        WindowThreadNotFound,
        InvalidAssemblyBuffer,
        MethodNotFound,
        PidNotValid,
        UnknownError
    }
}
