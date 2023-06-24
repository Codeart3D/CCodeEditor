using CCodeEditor.Source;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CCodeEditor.Control
{
    /// <summary>
    /// Interaction logic for CodeEditor.xaml
    /// </summary>
    public partial class CodeEditor : UserControl
    {
        private bool Editing = false;
        private List<Keyword> Keywords = new List<Keyword>();
        private char[] Delimiters = new char[] { ' ', '\0', '(', ')', '.', '=', '+', '-', '*', '/', '>', '<', '&', '|', '{', '}', '"' };

        private DispatcherTimer timer;

        public CodeEditor()
        {
            InitializeComponent();

            Keywords.Add(new Keyword(Brushes.DeepSkyBlue, "if"));
            Keywords.Add(new Keyword(Brushes.DeepSkyBlue, "foreach"));
            Keywords.Add(new Keyword(Brushes.DeepSkyBlue, "for"));
            Keywords.Add(new Keyword(Brushes.DeepSkyBlue, "var"));
            Keywords.Add(new Keyword(Brushes.DeepSkyBlue, "return"));
            Keywords.Add(new Keyword(Brushes.DeepSkyBlue, "continue"));
            Keywords.Add(new Keyword(Brushes.DeepSkyBlue, "break"));
            Keywords.Add(new Keyword(Brushes.LightSeaGreen, "String"));

            timer = new DispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            timer.Tick += Timer_Tick;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            tbxCode.Focus();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            Editing = true;
            timer.Stop();

            TextRange textRange = new TextRange(tbxCode.Document.ContentStart, tbxCode.Document.ContentEnd);
            string[] lines = textRange.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            SetLineNumber(lines.Length);
            CheckKeyword(lines);

            Editing = false;
        }

        private void tbxCode_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!Editing)
            {
                timer.Stop();
                timer.Start();
            }
        }

        private void DisplaySuggestionPopup()
        {
            if (!popSuggestion.IsOpen)
            {
                TextPointer cur = tbxCode.CaretPosition;
                Rect rect = cur.GetCharacterRect(LogicalDirection.Backward);
                Point point = tbxCode.PointToScreen(rect.BottomRight);
                popSuggestion.HorizontalOffset = point.X;
                popSuggestion.VerticalOffset = point.Y;
                popSuggestion.IsOpen = true;
            }
        }

        private void SetLineNumber(int length)
        {
            string lineno = "1\n";

            for (int i = 2; i < length; i++)
                lineno += i + "\n";

            tbkLineNo.Text = lineno;
        }

        private string CheckCommentDoubleSlash(string input, out string comment)
        {
            int index = input.IndexOf("//");

            if (index > -1)
            {
                comment = input.Substring(index);
                return input.Substring(0, index);
            }
            else
                comment = null;

            return input;
        }

        private void CheckKeyword(string[] lines)
        {
            int forward = 0;
            string fstr = null;
            bool collect_comment_star = false;
            Rect rect = tbxCode.CaretPosition.GetCharacterRect(LogicalDirection.Forward);
            Point point = new Point(rect.X, rect.Y);

            tbxCode.Document.Blocks.Clear();

            for (int i = 0; i < lines.Length - 1; i++)
            {
                int k = 0;
                bool collect_string = false;
                bool collect_comment_slash = false;
                char[] word = new char[1024];
                lines[i] = lines[i].Replace("\t", String.Empty);
                var chars = lines[i].ToCharArray();
                Paragraph paragraph = new Paragraph();
                
                if (fstr != null)
                    paragraph.Inlines.Add(new Run(fstr));

                for (int j = 0; j < chars.Length; j++)
                {
                    bool sign = false;
                    char c = chars[j];
                    int nj = j + 1;


                    foreach (var delimiter in Delimiters)
                    {
                        if (c == delimiter)
                        {
                            sign = true;
                            break;
                        }
                    }

                    if (!sign || collect_comment_slash)
                    {
                        word[k] = c;
                        k++;

                        if (nj == chars.Length)
                        {
                            if (collect_string)
                                paragraph.Inlines.Add(new Run(new string(word, 0, k)) { Foreground = Brushes.LightSalmon });
                            else if (collect_comment_slash)
                                paragraph.Inlines.Add(new Run(new string(word, 0, k)) { Foreground = Brushes.LightGreen });
                            else if (collect_comment_star)
                                paragraph.Inlines.Add(new Run(new string(word, 0, k)) { Foreground = Brushes.LightGreen });
                            else
                                CheckKeywordInLine(sign, c, new string(word, 0, k), paragraph);
                        }
                    }
                    else
                    {
                        if (collect_comment_star)
                        {
                            word[k] = c;
                            k++;

                            // check end of comment
                            if (nj < chars.Length)
                            {
                                // check with next character
                                char nc = chars[nj];

                                if (c == '*' && nc == '/')
                                {
                                    word[k] = '/';
                                    k++;
                                    j++;
                                    collect_comment_star = false;
                                    paragraph.Inlines.Add(new Run(new string(word, 0, k)) { Foreground = Brushes.LightGreen });
                                    k = 0;
                                }
                            }
                            else
                            {
                                paragraph.Inlines.Add(new Run(new string(word, 0, k)) { Foreground = Brushes.LightGreen });
                                k = 0;
                            }
                        }
                        else if (c == '"')
                        {
                            // check string
                            if (!collect_string)
                            {
                                collect_string = true;

                                CheckKeywordInLine(false, c, new string(word, 0, k), paragraph);
                                word[0] = '"';
                                k = 1;

                                if (nj == chars.Length)
                                    paragraph.Inlines.Add(new Run(new string(word, 0, k)) { Foreground = Brushes.LightSalmon });
                            }
                            else
                            {
                                collect_string = false;
                                word[k] = c;
                                k++;
                                paragraph.Inlines.Add(new Run(new string(word, 0, k)) { Foreground = Brushes.LightSalmon });
                                k = 0;
                            }
                        }
                        else if (collect_string)
                        {
                            word[k] = c;
                            k++;

                            if (nj == chars.Length)
                                paragraph.Inlines.Add(new Run(new string(word, 0, k)) { Foreground = Brushes.LightSalmon });
                        }
                        else if (c == '/')
                        {
                            // check comment
                            if (nj < chars.Length)
                            {
                                // check with next character
                                char nc = chars[nj];

                                if (nc == '/')
                                {
                                    collect_comment_slash = true;

                                    CheckKeywordInLine(false, c, new string(word, 0, k), paragraph);
                                    word[0] = '/';
                                    k = 1;

                                    if (nj == chars.Length)
                                        paragraph.Inlines.Add(new Run(new string(word, 0, k)) { Foreground = Brushes.LightGreen });
                                }
                                else if (nc == '*')
                                {
                                    collect_comment_star = true;

                                    CheckKeywordInLine(false, c, new string(word, 0, k), paragraph);
                                    word[0] = '/';
                                    word[1] = '*';
                                    k = 2;
                                    j++;
                                    nj++;

                                    if (nj == chars.Length)
                                        paragraph.Inlines.Add(new Run(new string(word, 0, k)) { Foreground = Brushes.LightGreen });
                                }
                                else
                                {
                                    CheckKeywordInLine(sign, c, new string(word, 0, k), paragraph);
                                    k = 0;
                                }
                            }
                            else
                            {
                                if (collect_comment_slash || collect_comment_star)
                                {
                                    word[k] = c;
                                    k++;
                                    paragraph.Inlines.Add(new Run(new string(word, 0, k)) { Foreground = Brushes.LightGreen });
                                }
                                else
                                    CheckKeywordInLine(sign, c, new string(word, 0, k), paragraph);
                            }
                        }
                        else
                        {
                            //if (c == '{')
                            //{
                            //    fstr = null;
                            //    forward++;

                            //    for (int f = 0; f < forward; f++)
                            //        fstr += "\t";
                            //}
                            //else if (c == '}')
                            //{
                            //    fstr = null;
                            //    forward--;

                            //    for (int f = 0; f < forward; f++)
                            //        fstr += "\t";
                            //}

                            CheckKeywordInLine(sign, c, new string(word, 0, k), paragraph);
                            k = 0;
                        }
                    }
                }

                tbxCode.Document.Blocks.Add(paragraph);
            }

            tbxCode.CaretPosition = tbxCode.GetPositionFromPoint(point, true);
        }

        private void CheckKeywordInLine(bool sign, char c, string part, Paragraph paragraph)
        {
            if (!string.IsNullOrEmpty(part))
            {
                Run run = new Run(part);
                Keyword key = Keywords.Where(p => p.Key == part).FirstOrDefault();

                if (key != null)
                    run.Foreground = key.Color;
                else
                    run.Foreground = Brushes.LightGray;

                paragraph.Inlines.Add(run);
            }

            if (sign)
                paragraph.Inlines.Add(new Run(c.ToString()) { Foreground = Brushes.LightGray });
        }
    }
}
