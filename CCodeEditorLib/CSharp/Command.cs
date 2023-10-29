using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCodeEditorLib.CSharp
{
    public enum CommandType
    {
        None,
        Variable,
        Function,
        Condition,
        Expersion,
        Class
    }

    public class Command
    {
        public CommandType Type { get; set; }
        public string Name { get; set; }
    }
}
