using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace CCodeEditorLib.Source
{
    public class UndoRedoCode
    {
        public string Code { get; set; }
        public TextPointer CaretPosition { get; set; }

        public UndoRedoCode()
        {
        }

        public UndoRedoCode(string c, TextPointer p)
        {
            Code = c;
            CaretPosition = p;
        }
    }
}
