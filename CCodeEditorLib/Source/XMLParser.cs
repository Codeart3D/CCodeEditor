using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace CCodeEditorLib.Source
{
    public class XMLParser
    {
        public static List<Control> LoadFromXMLString(string xml)
        {
            var stringReader = new System.IO.StringReader(xml);
            XmlDocument xmlDoc = new XmlDocument();
            List<Control> controls = new List<Control>();
            xmlDoc.Load(stringReader);

            List<string> classes = new List<string>();
            classes.Add("Rectangle");
            //string name = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + ".Source";

            foreach (var cls in classes)
            {
                foreach (XmlNode reportNode in xmlDoc.SelectNodes("//" + cls))
                {
                    object obj = Activator.CreateInstance(Type.GetType($"Xml.Source.{cls}"));

                    var properties = obj.GetType().GetProperties();

                    foreach (var item in properties)
                    {
                        XmlAttribute attribute = reportNode.Attributes[item.Name];

                        if (attribute != null)
                        {
                            if (item.PropertyType != typeof(Color))
                                item.SetValue(obj, Convert.ChangeType(attribute.Value, item.PropertyType));
                            else
                                item.SetValue(obj, (Color)ColorConverter.ConvertFromString(attribute.Value));
                        }
                    }

                    controls.Add(obj as Control);
                }
            }

            return controls;
        }

        public static string FormatXml(string xml)
        {
            try
            {
                XDocument doc = XDocument.Parse(xml);
                return doc.ToString();
            }
            catch (Exception)
            {
                return xml;
            }
        }
    }
}
