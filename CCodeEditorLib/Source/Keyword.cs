using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace CCodeEditorLib.Source
{
    public enum KeywordType
    {
        Main,
        Enum,
        Struct,
        Class,
        Method,
        XMLTag
    }

    public class Keyword
    {
        public bool Visible { get; set; }
        public string Key { get; set; }
        public Brush Color { get; set; }
        public KeywordType Type { get; set; }
        public List<string> Suggestions { get; set; }

        public Keyword(Brush color, string key, KeywordType type = KeywordType.Main, bool visible = true)
        {
            Key = key;
            Color = color;
            Type = type;
            Visible = visible;
        }
    }
}
