using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCodeEditorLib.CSharp
{
    class Class : Command
    {
        public List<Variable> Variables { get; } = new List<Variable>();
        public List<Function> Functions { get; } = new List<Function>();

        public Class(string name)
        {
            Type = CommandType.Class;
            Name = name;
        }
    }
}
