using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace CCodeEditorLib.Source
{
    internal static class TextUtils
    {
        internal static void FindAndReplace(this RichTextBox richTextBox, string text, Brush highlight, string replace = null)
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

        internal static List<string> SplitAndKeepDelimiters(this string s, params char[] delimiters)
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

        internal static string GetStringBetweenTwoChar(string s, char first, char last)
        {
            s = s.Substring(s.IndexOf(first) + 1);
            s = s.Substring(0, s.IndexOf(last));

            return s;
        }
    }
}
