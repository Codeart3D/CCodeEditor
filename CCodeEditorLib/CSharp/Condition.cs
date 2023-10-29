using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCodeEditorLib.CSharp
{
    public enum ConditionType
    {
        If,
        ElseIf,
        Else
    }

    public class Condition : Command
    {
        public ConditionType CType { get; set; }
        public string Expertion { get; set; }

        public Condition(ConditionType type, string expertion)
        {
            CType = type;
            Type = CommandType.Condition;
            Expertion = expertion;
        }
    }
}
