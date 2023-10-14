using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCodeEditorLib.Source
{
    public class TagIndex
    {
        public bool IsNew;
        public int Index;
        
        public TagIndex(int idx)
        {
            Index = idx;
        }

        public TagIndex(int idx, bool nw)
        {
            IsNew = nw;
            Index = idx;
        }
    }
}
