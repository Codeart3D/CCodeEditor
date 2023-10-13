using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCodeEditorLib.Source
{
    public class UndoRedoAction
    {
        public string Text { get; set; }
        public List<bool> NewTags { get; set; }
        public List<int> IndexTags { get; set; }

        public UndoRedoAction(string text)
        {
            Text = text;
        }

        public UndoRedoAction(string text, List<bool> nt, List<int> it)
        {
            Text = text;
            NewTags = TextUtils.Clone(nt);
            IndexTags = TextUtils.Clone(it);
        }
    }
}
