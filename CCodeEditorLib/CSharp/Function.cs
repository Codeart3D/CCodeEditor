using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCodeEditorLib.CSharp
{
    public class Function : Command
    {
        public string Parameters { get; set; }

        public Function(string name, string param)
        {
            Type = CommandType.Function;
            Name = name;
            Parameters = param;
        }
    }
}
