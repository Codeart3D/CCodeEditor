using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCodeEditorLib.Source
{
    public class TypeValue
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public LType Type { get; set; }

        public TypeValue(string key, string value, LType type)
        {
            Key = key;
            Value = value;
            Type = type;
        }
    }
}
