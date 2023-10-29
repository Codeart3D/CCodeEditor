using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCodeEditorLib.CSharp
{
    public class Variable : Command
    {
        public string Value { get; set; }

        public Variable(string name, string value)
        {
            Type = CommandType.Variable;
            Name = name;
            Value = value;
        }
    }
}
