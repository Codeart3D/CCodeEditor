using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCodeEditorLib.Source
{
    public class KeywordClass
    {
        public string Name { get; set; }
        public List<Keyword> Properties { get; set; }
        public List<Keyword> BaseProperties { get; set; }

        public KeywordClass(string name, List<Keyword> properties, List<Keyword> baseproperties)
        {
            Name = name;
            Properties = properties;
            BaseProperties = baseproperties;
        }
    }
}
