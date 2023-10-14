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
        XMLTag,
        XMLEndTag,
        XMLStart,
        XMLRootTag,
        XMLEnd,
        XMLEqual,
        XMLAttrib
    }

    public class Keyword
    {
        public bool Visible { get; set; }
        public string KeyName { get; set; }
        public string Key { get; set; }
        public string ReplaceKey { get; set; }
        public string InsertAfter { get; set; }
        public Brush Color { get; set; }
        public KeywordType Type { get; set; }
        public int ReturnBackward { get; set; }
        public List<Keyword> BaseSuggestions { get; set; }
        public List<Keyword> Suggestions { get; set; }
        public string Icon
        {
            get
            {
                switch (Type)
                {
                    case KeywordType.Main:
                    case KeywordType.Enum:
                    case KeywordType.Struct:
                    case KeywordType.Class:
                    case KeywordType.Method:
                    case KeywordType.XMLTag:
                        return "../Image/Items.png";
                    case KeywordType.XMLAttrib:
                        return "../Image/Attrib.png";
                }

                return "../Image/Items.png";
            }
        }

        public Keyword(Brush color, string key, KeywordType type = KeywordType.Main, string replace = null, bool visible = true, int returnback = 0)
        {
            Key = key;
            Color = color;
            Type = type;
            Visible = visible;
            ReplaceKey = replace;
            ReturnBackward = returnback;
        }
    }
}
