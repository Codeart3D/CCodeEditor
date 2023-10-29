using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCodeEditorLib.CSharp
{
    public enum ExpertionType
    {
        PlusPlus,
        SetValue,
        Return,
    }

    public class Expertion : Command
    {
        public ExpertionType EType { get; set; }
        public string Expertions { get; set; }
        public string Value { get; set; }

        public Expertion(ExpertionType type, string expertions, string value = null)
        {
            EType = type;
            Type = CommandType.Condition;
            Expertions = expertions;
            Value = value;
        }
    }
}
