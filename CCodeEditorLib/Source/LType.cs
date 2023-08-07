using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CCodeEditorLib.Source
{
    public class LType
    {
        public string Name { get; set; }
        public string Keyword { get; set; }
        public KeywordType Type { get; set; }
        public string Value { get; set; }
        public List<string> AcceptValues { get; set; }


        public static Dictionary<string, TypeValue> TypeTables { get; set; }

        public LType(string name, KeywordType type)
        {
            Name = name;
            Keyword = $"%{name}%";
            Type = type;
            AcceptValues = new List<string>();
        }

        public bool Check(string phrase)
        {
            string[] str = phrase.Split('=');

            // check type
            if (str[0].StartsWith(Name + " "))
            {
                string[] fpart = str[0].Split(' ');

                // check name
                if (Regex.IsMatch(fpart[1], "^[a-zA-Z_$][a-zA-Z_$0-9]*$"))
                {
                    string spart = str[1].Trim().TrimEnd(';');

                    // check value
                    if (Regex.IsMatch(spart, @"^\d+$"))
                    {
                        if (AcceptValues.Any(p => p == "%num%"))
                        {
                            TypeTables.Add(fpart[1], new TypeValue(fpart[1], spart, this));
                            return true;
                        }
                    }
                    else if (Regex.IsMatch(spart, "\"\\[(.*?)\\]\""))
                    {
                        if (AcceptValues.Any(p => p == "%str%"))
                        {
                            TypeTables.Add(fpart[1], new TypeValue(fpart[1], spart.Trim('\"'), this));
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
