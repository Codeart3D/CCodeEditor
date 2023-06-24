using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace CCodeEditor.Source
{
    public class Keyword
    {
        public string Key { get; set; }
        public string Attachment { get; set; }
        public string KeyAttachment { get; set; }
        public Brush Color { get; set; }
        public List<string> Suggestions { get; set; }

        public Keyword(Brush color, string key, string attachment = null)
        {
            Key = key;
            Color = color;
            Attachment = attachment;
            KeyAttachment = Key + Attachment;
        }
    }
}
