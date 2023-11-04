using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace CCodeEditorLib.Source
{
    public static class TextUtils
    {
        public static void FindAndReplace(this RichTextBox richTextBox, string text, Brush highlight, string replace = null)
        {
            int idx1 = 0;
            int idx2 = 0;
            bool find = false;
            TextPointer last = richTextBox.Document.ContentStart;
            TextPointer first = richTextBox.Document.ContentStart;
            TextPointer position = richTextBox.Document.ContentStart;

            while (position != null)
            {
                if (position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    string str = position.GetTextInRun(LogicalDirection.Forward);

                    if (!find)
                    {
                        idx1 = str.IndexOf(text[0]);

                        if (idx1 > -1)
                        {
                            int res = CheckText(str, text, ref idx1, ref idx2);

                            if (res == 0)
                            {
                                find = true;
                                first = position.GetPositionAtOffset(idx1);
                            }
                            else if (res == 1)
                            {
                                // found
                                idx2 = 0;
                                find = false;

                                first = position.GetPositionAtOffset(idx1);
                                last = position.GetPositionAtOffset(idx2);

                                if (string.IsNullOrEmpty(replace))
                                    HighlightText(first, last, highlight);
                                else
                                    Replace(first, last, replace);
                            }
                        }
                    }
                    else
                    {
                        idx1 = 0;
                        int res = CheckText(str, text, ref idx1, ref idx2);

                        if (res == 1)
                        {
                            // found
                            idx2 = 0;
                            find = false;

                            last = position.GetPositionAtOffset(idx1);

                            if (string.IsNullOrEmpty(replace))
                                HighlightText(first, last, highlight);
                            else
                                Replace(first, last, replace);
                        }
                        else if (res == -1)
                        {
                            // Not found
                            idx2 = 0;
                            find = false;
                        }
                    }
                }

                TextPointer nextContextPosition = position.GetNextContextPosition(LogicalDirection.Forward);

                if (nextContextPosition == null)
                    break;

                position = nextContextPosition;
            }
        }

        private static void HighlightText(TextPointer start, TextPointer end, Brush color)
        {
            TextRange range = new TextRange(start, end);
            range.ApplyPropertyValue(TextElement.ForegroundProperty, color);
        }

        private static void Replace(TextPointer first, TextPointer last, string replace)
        {
            TextRange range = new TextRange(first, last);
            range.Text = replace;
        }

        public static string GetLineText(RichTextBox rtb)
        {
            TextPointer caretPos = rtb.CaretPosition;
            TextPointer start = caretPos.GetLineStartPosition(0);
            TextPointer end = caretPos.GetLineStartPosition(1) != null ? caretPos.GetLineStartPosition(1) : caretPos.DocumentEnd;

            return new TextRange(start, end).Text;
        }

        public static void CopyCurrentLine(RichTextBox rtb)
        {
            TextPointer caretPos = rtb.CaretPosition;
            TextPointer start = caretPos.GetLineStartPosition(0);
            TextPointer end = caretPos.GetLineStartPosition(1) != null ? caretPos.GetLineStartPosition(1) : caretPos.DocumentEnd;
            TextRange range = new TextRange(start, end);
            range.Text += range.Text;
            rtb.CaretPosition = range.End.GetNextInsertionPosition(LogicalDirection.Backward);
        }

        public static void DeleteCurrentLine(RichTextBox rtb)
        {
            TextPointer caretPos = rtb.CaretPosition;
            TextPointer start = caretPos.GetLineStartPosition(0);
            TextPointer end = caretPos.GetLineStartPosition(1) != null ? caretPos.GetLineStartPosition(1) : caretPos.DocumentEnd;
            TextRange range = new TextRange(start, end);
            range.Text = "";
            TextPointer tp = start.GetPositionAtOffset(-2, LogicalDirection.Forward);

            if (tp != null)
                rtb.CaretPosition = tp;
        }

        public static void InsertEmptyLine(RichTextBox rtb)
        {
            TextPointer caretPos = rtb.CaretPosition;
            TextPointer start = caretPos.GetLineStartPosition(0);
            start.InsertLineBreak();
            rtb.CaretPosition = start.GetPositionAtOffset(-2, LogicalDirection.Forward);
        }

        private static int CheckText(string s1, string s2, ref int idx1, ref int idx2)
        {
            for (; idx1 < s1.Length; idx1++)
            {
                if (s1[idx1] != s2[idx2])
                    return -1;

                idx2++;

                if (idx2 >= s2.Length)
                {
                    idx1++;
                    return 1;
                }
            }

            idx1--;

            return 0;
        }

        public static List<string> SplitAndKeepDelimiters(this string s, params char[] delimiters)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(s))
            {
                int iFirst = 0;
                do
                {
                    int iLast = s.IndexOfAny(delimiters, iFirst);
                    if (iLast >= 0)
                    {
                        if (iLast > iFirst)
                            parts.Add(s.Substring(iFirst, iLast - iFirst)); //part before the delimiter
                        parts.Add(new string(s[iLast], 1));//the delimiter
                        iFirst = iLast + 1;
                        continue;
                    }

                    //No delimiters were found, but at least one character remains. Add the rest and stop.
                    parts.Add(s.Substring(iFirst, s.Length - iFirst));
                    break;

                } while (iFirst < s.Length);
            }

            return parts;
        }

        public static string GetStringBetweenTwoChar(string s, char first, char last)
        {
            s = s.Substring(s.IndexOf(first) + 1);
            s = s.Substring(0, s.IndexOf(last));

            return s;
        }

        public static string GetStringBetweenParanteses(string s, out int lastindex)
        {
            int i = 0;
            int p = 0;
            int c = 0;
            char[] result = new char[500];

            for (; i < s.Length; i++)
            {
                if (s[i] == '(')
                    p++;
                else if (s[i] == ')')
                {
                    p--;

                    if (p != 0)
                        result[c++] = s[i];
                    else
                        break;
                }
                else if (p > 0)
                    result[c++] = s[i];
            }

            lastindex = ++i;
            return new string(result, 0, c);
        }

        public static string GetStringBetweenAqulad(string s)
        {
            int p = 0;
            int c = 0;
            char[] result = new char[10000];

            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '{')
                    p++;
                else if (s[i] == '}')
                {
                    p--;

                    if (p != 0)
                        result[c++] = s[i];
                    else
                        break;
                }
                else if (p > 0)
                    result[c++] = s[i];
            }

            return new string(result, 0, c);
        }

        public static string GetPreCharacter(RichTextBox rtx)
        {
            TextPointer caretPos = rtx.CaretPosition;
            TextPointer pre = caretPos.GetPositionAtOffset(-1, LogicalDirection.Forward);

            return new TextRange(caretPos, pre).Text;
        }

        public static string FindCurrentXmlTag(RichTextBox rtx)
        {
            TextPointer caretPos = rtx.CaretPosition;
            TextPointer start = caretPos.GetLineStartPosition(0);

            string line = new TextRange(start, caretPos).Text;

            int lindex = -1;

            // find start tag index
            for (int i = line.Length - 1; i > -1; i--)
            {
                if (line[i] == '<')
                {
                    lindex = i;
                    break;
                }
            }

            if (lindex != -1)
            {
                int j = -1;
                char[] word = new char[50];

                // find first word after < sign
                for (int i = lindex + 1; i < line.Length; i++)
                {
                    if (line[i] != ' ')
                    {
                        if (j < 50)
                            word[++j] = line[i];
                        else
                            break;
                    }
                    else if (j != -1)
                        break;
                }

                if (j != -1)
                    return new string(word, 0, j + 1);
            }

            return null;
            //TextPointer s = start.GetPositionAtOffset(index);
            //TextPointer e = start.GetPositionAtOffset(index + 13);

            //TextPointer start = rtx.CaretPosition;
            //string text1 = start.GetTextInRun(LogicalDirection.Backward);
            //TextPointer end = start.GetNextContextPosition(LogicalDirection.Backward);
            //string text2 = end.GetTextInRun(LogicalDirection.Backward);

            //TextRange range = new TextRange(s, e);
            //range.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Red);
            //rtx.Selection.Select(start, start);
        }

        public static string FindCurrentXmlTag(string line)
        {
            int lindex = -1;

            // find start tag index
            for (int i = line.Length - 1; i > -1; i--)
            {
                if (line[i] == '<')
                {
                    lindex = i;
                    break;
                }
            }

            if (lindex != -1)
            {
                int j = -1;
                char[] word = new char[50];

                // find first word after < sign
                for (int i = lindex + 1; i < line.Length; i++)
                {
                    if (line[i] != ' ')
                    {
                        if (j < 50)
                            word[++j] = line[i];
                        else
                            break;
                    }
                    else if (j != -1)
                        break;
                }

                if (j != -1)
                    return new string(word, 0, j + 1);
            }

            return null;
        }

        public static T Clone<T>(this T obj)
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);
                ms.Position = 0;

                return (T)formatter.Deserialize(ms);
            }
        }
    }
}
