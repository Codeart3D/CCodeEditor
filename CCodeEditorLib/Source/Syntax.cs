using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CCodeEditorLib.Source
{
    public class Syntax
    {
        public string Format { get; set; }
        public string StartFormat { get; set; }
        List<LType> Parameters { get; set; }

        public static List<LType> Types { get; set; }
        public static Dictionary<string, TypeValue> TypeTables { get; set; }

        public Syntax(string format)
        {
            Format = format;
            StartFormat = format.Split('(')[0] + "(";
            Parameters = new List<LType>();
            string[] paramstr = TextUtils.GetStringBetweenTwoChar(format, '(', ')').Split(',');

            foreach (var item in paramstr)
                Parameters.Add(Types.Where(p => p.Keyword == item).FirstOrDefault());
        }

        public bool Check(string phrase)
        {
            string[] str = phrase.Split(';');

            if (str[0].StartsWith(StartFormat))
            {
                string[] inputparam = TextUtils.GetStringBetweenTwoChar(phrase, '(', ')').Split(',');

                if (inputparam.Length != Parameters.Count)
                    return false;

                for (int i = 0; i < inputparam.Length; i++)
                {
                    string pi = inputparam[i];

                    if (Regex.IsMatch(pi, @"^\d+$"))
                    {
                        if (!Parameters[i].AcceptValues.Any(p => p == "%num%"))
                            return false;
                    }
                    else
                    {
                        TypeValue type = TypeTables[pi];

                        if (type == null || type.Type.Keyword != Parameters[i].Keyword)
                            return false;
                    }
                }

                return true;
            }

            return false;
        }
    }
}
