using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
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

        public static void GoAtTheBeginOfLine(RichTextBox rtb)
        {
            TextPointer caretPos = rtb.CaretPosition;
            TextPointer start = caretPos.GetLineStartPosition(0);
            TextPointer end = caretPos.GetLineStartPosition(1) != null ? caretPos.GetLineStartPosition(1) : caretPos.DocumentEnd;
            string line = new TextRange(start, end).Text;
            int tlen = line.Length;
            int tslen = line.TrimStart().Length;
            int forward = tlen - tslen;

            // Empty line go at the zero position
            if (tslen == 0)
                rtb.CaretPosition = caretPos.GetLineStartPosition(0);
            else if (forward != tlen)
            {
                for (int i = 0; i < forward; i++)
                    start = start.GetNextInsertionPosition(LogicalDirection.Forward);

                Rect rec1 = start.GetCharacterRect(LogicalDirection.Backward);
                Rect rec2 = rtb.CaretPosition.GetCharacterRect(LogicalDirection.Backward);

                // caret position at the start of line and pre position
                if (rec1.X == rec2.X)
                    rtb.CaretPosition = caretPos.GetLineStartPosition(0);
                else
                    rtb.CaretPosition = start;
            }
        }

        public static string GetLineText(RichTextBox rtb)
        {
            TextPointer caretPos = rtb.CaretPosition;
            TextPointer start = caretPos.GetLineStartPosition(0);
            TextPointer end = GetEndOfCurrentLine(caretPos);

            if (end == null)
                return "";

            return new TextRange(start, end).Text;
        }

        public static TextPointer GetEndOfCurrentLine(TextPointer caretPos)
        {
            TextPointer next = caretPos.GetLineStartPosition(1);

            return (next != null ? next : caretPos.DocumentEnd).GetNextInsertionPosition(LogicalDirection.Backward);
        }

        public static TextPointer GetFirstOfCurrentLine(TextPointer caretPos)
        {
            return caretPos.GetLineStartPosition(0);
        }

        public static TextPointer GetFirstOfCurrentLineWithoutSpace(TextPointer caretPos)
        {
            TextPointer pos = null;
            TextPointer nex = caretPos.GetLineStartPosition(0);

            do
            {
                pos = nex;
                nex = pos.GetNextInsertionPosition(LogicalDirection.Forward);
            }
            while (nex != null && string.IsNullOrWhiteSpace(new TextRange(pos, nex).Text) && new TextRange(pos, nex).Text != "\r\n");

            return pos;
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
            Rect rect = rtb.CaretPosition.GetCharacterRect(LogicalDirection.Forward);
            Point point = new Point(rect.X, rect.Y);

            TextPointer caretPos = rtb.CaretPosition;
            TextPointer start = caretPos.GetLineStartPosition(0);
            TextPointer end = caretPos.GetLineStartPosition(1) != null ? caretPos.GetLineStartPosition(1) : caretPos.DocumentEnd;
            TextRange range = new TextRange(start, end);
            range.Text = "";

            var tp = rtb.GetPositionFromPoint(point, true);

            if (tp != null)
                rtb.CaretPosition = tp;
        }

        public static void SelectFromCaretToStartOfLine(RichTextBox rtb)
        {
            TextPointer caretPos = rtb.CaretPosition;
            TextPointer start = caretPos.GetLineStartPosition(0);
            TextPointer end = caretPos.GetLineStartPosition(1) != null ? caretPos.GetLineStartPosition(1) : caretPos.DocumentEnd;
            string line = new TextRange(start, end).Text;
            int tlen = line.Length;
            int tslen = line.TrimStart().Length;
            int forward = tlen - tslen;

            // Empty line go at the zero position
            if (tslen == 0)
                rtb.Selection.Select(rtb.CaretPosition, caretPos.GetLineStartPosition(0));
            else if (forward != tlen)
            {
                for (int i = 0; i < forward; i++)
                    start = start.GetNextInsertionPosition(LogicalDirection.Forward);

                Rect rec1 = start.GetCharacterRect(LogicalDirection.Backward);
                Rect rec2 = rtb.CaretPosition.GetCharacterRect(LogicalDirection.Backward);

                // caret position at the start of line and pre position
                if (rec1.X == rec2.X)
                    rtb.Selection.Select(rtb.CaretPosition, caretPos.GetLineStartPosition(0));
                else
                    rtb.Selection.Select(rtb.CaretPosition, start);
            }

            //TextPointer fp = GetFirstOfCurrentLineWithoutSpace(rtb.CaretPosition);

            //if (rtb.CaretPosition.GetOffsetToPosition(fp) != rtb.CaretPosition.GetOffsetToPosition(rtb.CaretPosition))
            //    rtb.Selection.Select(rtb.CaretPosition, fp);
            //else
            //    rtb.Selection.Select(rtb.CaretPosition, rtb.CaretPosition.GetLineStartPosition(0));
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

        public static string GetStringBetweenParanteses(string str, out int lastindex)
        {
            int s = str.IndexOf('(') + 1;
            lastindex = str.LastIndexOf(')');

            if (s == -1 || lastindex == -1)
                return null;

            return str.Substring(s, lastindex - s);
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

        public static string FindCurrentWord(RichTextBox rtb)
        {
            TextPointer caretPos = rtb.CaretPosition;
            TextPointer start = caretPos.GetLineStartPosition(0);

            int lastindex = 0;
            string line = new TextRange(start, caretPos).Text;

            if (string.IsNullOrWhiteSpace(line))
                return "";

            for (int i = line.Length - 1; i > -1; i--)
            {
                if (line[i] == ' ')
                {
                    lastindex = i + 1;
                    break;
                }
            }

            return line.Substring(lastindex);
        }

        public static string GetCurrentLine(RichTextBox rtb)
        {
            TextPointer caretPos = rtb.CaretPosition;
            TextPointer start = caretPos.GetLineStartPosition(0);

            return new TextRange(start, caretPos).Text;
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

        public static void ReplaceInCurrentLine(RichTextBox rtb, string txt, string replace)
        {
            TextPointer caretPos = rtb.CaretPosition;
            TextPointer start = caretPos.GetLineStartPosition(0);
            TextPointer end = GetEndOfCurrentLine(caretPos);

            if (end == null)
                return;

            TextRange range = new TextRange(start, end);

            string str = range.Text;
            int position = str.IndexOf(txt);

            if (position > -1)
                range.Text = str.Substring(0, position) + replace + str.Substring(position + txt.Length);
        }

        public static void ChangeFirstLine(RichTextBox rtb, string txt)
        {
            TextPointer start = rtb.Document.ContentStart.GetLineStartPosition(0);
            TextPointer end = GetEndOfCurrentLine(start);

            if (end == null)
                return;

            TextRange range = new TextRange(start, end);

            if (range != null)
                range.Text = txt;
        }

        public static bool IsCharacter(string t)
        {
            if (t == " " || t == ";" || t == "\r\n" || t == "(" || t == ")" || t == "<" || t == ">"
                 || t == "," || t == "." || t == "!" || t == ">" || t == "#" || t == "*" || t == "&"
                 || t == "*" || t == "^" || t == "@" || t == "/" || t == "\\" || t == "\"" || t == "\'"
                 || t == ":" || t == "?" || t == "$" || t == "-" || t == "+" || t == "=" || t == "{"
                 || t == "}" || t == "[" || t == "]" || t == "|")
                return true;

            return false;
        }

        public static bool IsNumber(string t)
        {
            if (t == "0" || t == "1" || t == "2" || t == "3" || t == "4" || t == "5" || t == "6" || t == "7" || t == "8" || t == "9")
                return true;

            return false;
        }

        public static string GetCurrentWord(RichTextBox rtb)
        {
            string txt;
            string pre = null;
            TextPointer end = null;
            TextPointer start = null;
            TextPointer nex = rtb.CaretPosition;

            do
            {
                start = nex;
                nex = start.GetNextInsertionPosition(LogicalDirection.Backward);

                if (nex == null)
                    break;

                txt = new TextRange(start, nex).Text;

                if (txt == ".")
                {
                    if (pre == null)
                    {
                        start = nex;
                        nex = start.GetNextInsertionPosition(LogicalDirection.Backward);

                        if (nex == null)
                            break;

                        txt = new TextRange(start, nex).Text;

                        if (IsNumber(txt))
                        {
                            pre = txt;
                            txt = "";
                            continue;
                        }
                        else
                            break;
                    }
                    else if (IsNumber(pre))
                    {
                        pre = ".";
                        txt = "";
                        continue;
                    }
                }

                pre = txt;
            }
            while (!IsCharacter(txt));

            pre = null;
            nex = rtb.CaretPosition;

            do
            {
                end = nex;
                nex = end.GetNextInsertionPosition(LogicalDirection.Forward);

                if (nex == null)
                    break;

                txt = new TextRange(nex, end).Text;

                if (txt == ".")
                {
                    if (pre == null)
                    {
                        end = nex;
                        nex = end.GetNextInsertionPosition(LogicalDirection.Forward);

                        if (nex == null)
                            break;

                        txt = new TextRange(nex, end).Text;

                        if (IsNumber(txt))
                        {
                            pre = txt;
                            txt = "";
                            continue;
                        }
                        else
                            break;
                    }
                    else if (IsNumber(pre))
                    {
                        pre = ".";
                        txt = "";
                        continue;
                    }
                }

                pre = txt;
            }
            while (!IsCharacter(txt));

            return new TextRange(start, end).Text;
        }

        public static void SelectCurrentWord(RichTextBox rtb)
        {
            bool isletter = false;
            string txt = null;
            string stxt = null; // start text
            string pre = null;
            TextPointer end = null;
            TextPointer start = null;
            TextPointer nex = rtb.CaretPosition;

            do
            {
                start = nex;
                nex = start.GetNextInsertionPosition(LogicalDirection.Backward);

                if (nex == null)
                    break;

                txt = new TextRange(start, nex).Text;

                if (txt == ".")
                {
                    if (pre == null)
                    {
                        start = nex;
                        nex = start.GetNextInsertionPosition(LogicalDirection.Backward);

                        if (nex == null)
                            break;

                        txt = new TextRange(start, nex).Text;

                        if (IsNumber(txt))
                        {
                            pre = txt;
                            txt = "";
                            continue;
                        }
                        else
                            break;
                    }
                    else if (IsNumber(pre))
                    {
                        pre = ".";
                        txt = "";
                        continue;
                    }
                }

                isletter = !IsCharacter(txt);

                if (isletter)
                    pre = txt;
            }
            while (isletter);

            stxt = pre;
            pre = null;
            nex = rtb.CaretPosition;

            do
            {
                end = nex;
                nex = end.GetNextInsertionPosition(LogicalDirection.Forward);

                if (nex == null)
                    break;

                txt = new TextRange(nex, end).Text;

                if (txt == ".")
                {
                    if (pre == null)
                    {
                        end = nex;
                        nex = end.GetNextInsertionPosition(LogicalDirection.Forward);

                        if (nex == null)
                            break;

                        txt = new TextRange(nex, end).Text;

                        if (IsNumber(txt))
                        {
                            pre = txt;
                            txt = "";
                            continue;
                        }
                        else
                            break;
                    }
                    else if (IsNumber(pre) && IsNumber(stxt))
                    {
                        pre = ".";
                        txt = "";
                        continue;
                    }
                }

                pre = txt;
            }
            while (!IsCharacter(txt));

            rtb.Selection.Select(start, end);
        }

        public static void CorrectSelection(RichTextBox rtb)
        {
            string txt;
            string pre = null;
            TextPointer end = null;
            TextPointer nex = rtb.Selection.Start;

            if (rtb.Selection.Start.CompareTo(rtb.CaretPosition) < 0)
            {
                do
                {
                    end = nex;
                    nex = end.GetNextInsertionPosition(LogicalDirection.Forward);

                    if (nex == null)
                        break;

                    txt = new TextRange(end, nex).Text;

                    if (txt == ".")
                    {
                        if (pre == null)
                        {
                            end = nex;
                            nex = end.GetNextInsertionPosition(LogicalDirection.Forward);

                            if (nex == null)
                                break;

                            txt = new TextRange(end, nex).Text;

                            if (IsNumber(txt))
                            {
                                pre = txt;
                                txt = "";
                                continue;
                            }
                            else
                                break;
                        }
                        else if (IsNumber(pre))
                        {
                            pre = ".";
                            txt = "";
                            continue;
                        }
                    }

                    pre = txt;
                }
                while (!IsCharacter(txt));

                rtb.Selection.Select(rtb.Selection.Start, end);
            }
            else
            {
                pre = null;
                nex = rtb.Selection.End;

                do
                {
                    end = nex;
                    nex = end.GetNextInsertionPosition(LogicalDirection.Backward);

                    if (nex == null)
                        break;

                    txt = new TextRange(nex, end).Text;

                    if (txt == ".")
                    {
                        if (pre == null)
                        {
                            end = nex;
                            nex = end.GetNextInsertionPosition(LogicalDirection.Backward);

                            if (nex == null)
                                break;

                            txt = new TextRange(nex, end).Text;

                            if (IsNumber(txt))
                            {
                                pre = txt;
                                txt = "";
                                continue;
                            }
                            else
                                break;
                        }
                        else if (IsNumber(pre))
                        {
                            pre = ".";
                            txt = "";
                            continue;
                        }
                    }

                    pre = txt;
                }
                while (!IsCharacter(txt));

                rtb.Selection.Select(rtb.Selection.End, end);
            }
        }

        public static void RenameVariableInString(ref string code, string prename, string newname)
        {
            code = Regex.Replace(code, $@"(?<!\w){prename}(?!\w)", newname);
        }

        public static List<Keyword> FilterWithPriority(List<Keyword> source, string filterWord)
        {
            if (string.IsNullOrEmpty(filterWord) || source == null || source.Count == 0)
                return source ?? new List<Keyword>();

            var results = new List<Keyword>();

            // Level 1: StartsWith with exact case match
            var exactStartsWith = source
                .Where(s => s.Key.StartsWith(filterWord))
                .OrderBy(s => s.Key) // Alphabetical for closest matching
                .ToList();

            // Level 2: StartsWith with case-insensitive (but not exact case)
            var insensitiveStartsWith = source
                .Where(s => s.Key.StartsWith(filterWord, StringComparison.OrdinalIgnoreCase) &&
                           !s.Key.StartsWith(filterWord))
                .OrderBy(s => s.Key.ToLower())
                .Select(s => new Keyword(s.Color, s.Key, s.Type, s.ReplaceKey, s.Visible, s.ReturnBackward)) // Create new Keyword with lowercase
                .ToList();

            // Level 3: Contains with case-insensitive (but not StartsWith)
            var containsMatches = source
                .Where(s => s.Key.IndexOf(filterWord, StringComparison.OrdinalIgnoreCase) >= 0 &&
                           !s.Key.StartsWith(filterWord, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Key.ToLower())
                .Select(s => new Keyword(s.Color, s.Key, s.Type, s.ReplaceKey, s.Visible, s.ReturnBackward)) // Create new Keyword with lowercase
                .ToList();

            results.AddRange(exactStartsWith);
            results.AddRange(insensitiveStartsWith);
            results.AddRange(containsMatches);

            return results;
        }

        public static void InsertWordAtLineWithHeight(RichTextBox richTextBox, string word, int lineNumber)
        {
            FlowDocument document = richTextBox.Document;
            TextPointer startPointer = document.ContentStart;

            // Get line height (approximate)
            double lineHeight = richTextBox.FontSize * 1.2; // Adjust multiplier as needed

            // Navigate to the approximate position
            TextPointer targetPointer = startPointer;

            for (int i = 0; i < lineNumber; i++)
            {
                // Move down one line
                targetPointer = targetPointer.GetLineStartPosition(1);
                if (targetPointer == null) break;
            }

            if (targetPointer != null)
                targetPointer.InsertTextInRun(word + " ");
        }

        public static void RemoveCharAt(RichTextBox richTextBox, int lineNumber, int columnNumber)
        {
            if (richTextBox?.Document == null)
                return;

            if (lineNumber < 0 || columnNumber < 0)
                return;

            FlowDocument document = richTextBox.Document;
            TextPointer currentPointer = document.ContentStart;
            int currentLine = 0;

            // Navigate to the specified line
            while (currentPointer != null && currentLine < lineNumber)
            {
                currentPointer = currentPointer.GetLineStartPosition(0);

                if (currentPointer == null)
                    break;

                currentLine++;
            }

            if (currentPointer == null || currentLine != lineNumber)
                return;

            // Now navigate to the specified column within this line
            TextPointer targetPointer = currentPointer;
            int currentColumn = 0;

            while (targetPointer != null && currentColumn < columnNumber)
            {
                // Get the next character position
                TextPointer nextPointer = targetPointer.GetPositionAtOffset(1);

                if (nextPointer == null)
                    break;

                // Check if we've reached the end of the line
                if (targetPointer.GetAdjacentElement(LogicalDirection.Forward) is LineBreak)
                    break;

                targetPointer = nextPointer;
                currentColumn++;
            }

            if (targetPointer == null || currentColumn != columnNumber)
                return;

            // Check if there's a character at this position
            string text = targetPointer.GetTextInRun(LogicalDirection.Forward);

            if (string.IsNullOrEmpty(text) || text.Length == 0)
                return;

            // Remove the character
            TextPointer nextChar = targetPointer.GetPositionAtOffset(1);

            if (nextChar != null)
            {
                TextRange rangeToRemove = new TextRange(targetPointer, nextChar);
                rangeToRemove.Text = "";
            }
        }

        public static bool RemoveCharAtPoint(RichTextBox richTextBox, Point point)
        {
            if (richTextBox?.Document == null)
                return false;

            try
            {
                // Save current caret position
                TextPointer savedCaret = richTextBox.CaretPosition;

                // Get the TextPointer at the given point
                TextPointer pointer = richTextBox.GetPositionFromPoint(point, true);

                if (pointer == null)
                    return false;

                // Get the character at this position
                TextPointer charToRemove = pointer;

                // If we're at the end of a run, move back one character
                string text = pointer.GetTextInRun(LogicalDirection.Forward);

                if (string.IsNullOrEmpty(text))
                {
                    // Try to move backward to get a character
                    TextPointer prevPointer = pointer.GetPositionAtOffset(-1);

                    if (prevPointer != null)
                    {
                        string prevText = prevPointer.GetTextInRun(LogicalDirection.Forward);

                        if (!string.IsNullOrEmpty(prevText))
                        {
                            charToRemove = prevPointer;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }

                // Remove the character
                TextPointer nextChar = charToRemove.GetPositionAtOffset(1);

                if (nextChar != null)
                {
                    TextRange rangeToRemove = new TextRange(charToRemove, nextChar);
                    rangeToRemove.Text = "";

                    // Restore caret position
                    richTextBox.CaretPosition = savedCaret;
                    return true;
                }

                return false;
            }
            catch 
            {
                return false;
            }
        }

        public static bool InsertStringAtPoint(RichTextBox richTextBox, Point point, string textToInsert)
        {
            if (richTextBox?.Document == null || string.IsNullOrEmpty(textToInsert))
                return false;

            try
            {
                // Save current caret position
                TextPointer savedCaret = richTextBox.CaretPosition;

                // Get the TextPointer at the given point
                TextPointer pointer = richTextBox.GetPositionFromPoint(point, true);

                if (pointer == null)
                    return false;

                // Insert the text at the pointer position
                pointer.InsertTextInRun(textToInsert);

                // Restore caret position
                richTextBox.CaretPosition = savedCaret;

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
