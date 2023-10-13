using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCodeEditorLib.Source
{
    internal class TagPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public object Tag { get; set; }

        public TagPoint(double x, double y, object tag)
        {
            X = x;
            Y = y;
            Tag = tag;
        }
    }
}
