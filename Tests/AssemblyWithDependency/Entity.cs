using System;
using Newtonsoft.Json;

namespace AssemblyWithDependency
{
    public class Entity
    {
        public String Type { get; set; }
        public String Value { get; set; }        

        public Entity(String type, String value)
        {
            Type = type;
            Value = value;
        }

        public String Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
