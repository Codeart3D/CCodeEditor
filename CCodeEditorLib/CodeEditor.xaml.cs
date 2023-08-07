using CCodeEditorLib.Source;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace CCodeEditorLib
{
    /// <summary>
    /// Interaction logic for CodeEditor.xaml
    /// </summary>
    public partial class CodeEditor : UserControl
    {
        private bool Editing = false;
        private string FilterWord = "";
        private TextPointer StartWord;
        private TextPointer EndWord;
        private List<LType> Types = new List<LType>();
        private List<Keyword> Keywords = new List<Keyword>();
        private char[] Delimiters;
        private char[] CodeDelimiters = new char[] { ' ', '\0', '(', ')', '.', '=', '+', '-', '*', '/', '>', '<', '&', '|', '{', '}', '"' };
        private char[] XmlDelimiters = new char[] { ' ', '\0' };

        private DispatcherTimer Timer;
        private DispatcherTimer XmlTimer = null;

        public bool IsXML { get; set; }
        public Visibility DisplayErrorSection { get { return tbkError.Visibility; } set { tbkError.Visibility = value; } }
        public string Error { get { return tbkError.Text; } set { tbkError.Text = value; } }
        public bool CheckXmlError { get; set; }
        public bool IsEnableXmlFormatter { get; set; }

        public CodeEditor()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsXML)
            {
                if (IsEnableXmlFormatter)
                {
                    XmlTimer = new DispatcherTimer();
                    XmlTimer.Interval = new TimeSpan(0, 0, 3);
                    XmlTimer.Tick += XmlTimer_Tick;
                }

                Delimiters = XmlDelimiters;

                Keywords.Add(new Keyword(Brushes.LightGreen, "<", KeywordType.XMLTag, false));
                Keywords.Add(new Keyword(Brushes.LightGreen, "/>", KeywordType.XMLTag, false));
                Keywords.Add(new Keyword(Brushes.LightGreen, ">", KeywordType.XMLTag, false));
                Keywords.Add(new Keyword(Brushes.LightGreen, "<Screen>", KeywordType.XMLTag));
                Keywords.Add(new Keyword(Brushes.LightGreen, "</Screen>", KeywordType.XMLTag));
                Keywords.Add(new Keyword(Brushes.LightGreen, "<Screen", KeywordType.XMLTag, false));
                Keywords.Add(new Keyword(Brushes.LightGreen, "<Rectangle/>", KeywordType.XMLTag));
                Keywords.Add(new Keyword(Brushes.LightGreen, "<Rectangle>", KeywordType.XMLTag));
                Keywords.Add(new Keyword(Brushes.LightGreen, "<Rectangle", KeywordType.XMLTag, false));
            }
            else
            {
                Delimiters = CodeDelimiters;

                Keywords.Add(new Keyword(Brushes.DeepSkyBlue, "if"));
                Keywords.Add(new Keyword(Brushes.DeepSkyBlue, "foreach"));
                Keywords.Add(new Keyword(Brushes.DeepSkyBlue, "for"));
                Keywords.Add(new Keyword(Brushes.DeepSkyBlue, "var"));
                Keywords.Add(new Keyword(Brushes.DeepSkyBlue, "return"));
                Keywords.Add(new Keyword(Brushes.DeepSkyBlue, "continue"));
                Keywords.Add(new Keyword(Brushes.DeepSkyBlue, "break"));
                Keywords.Add(new Keyword(Brushes.DeepSkyBlue, "null"));
                Keywords.Add(new Keyword(Brushes.LightSeaGreen, "String", KeywordType.Class));
                Keywords.Add(new Keyword(Brushes.PaleGoldenrod, "Control", KeywordType.Enum));
            }

            LType lt = new LType("var", KeywordType.Main);
            lt.AcceptValues.Add("%num%");
            lt.AcceptValues.Add("%str%");
            Types.Add(lt);

            Types.Add(new LType("string", KeywordType.Struct));
            Types.Add(new LType("vector", KeywordType.Struct));
            Types.Add(new LType("color", KeywordType.Struct));

            Dictionary<string, TypeValue> TypeTables = new Dictionary<string, TypeValue>();

            Syntax.Types = Types;
            Syntax.TypeTables = TypeTables;
            LType.TypeTables = TypeTables;

            lt.Check("var A = 0;");
            Syntax syntax = new Syntax("SetValue(%var%);");
            bool res = syntax.Check("SetValue(10);");

            Timer = new DispatcherTimer();
            Timer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            Timer.Tick += Timer_Tick;

            lstKeyword.ItemsSource = Keywords.Where(p => p.Visible);

            tbxCode.Focus();
        }

        private void XmlTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                Timer.Stop();
                XmlTimer.Stop();
                Editing = true;
                string xml = new TextRange(tbxCode.Document.ContentStart, tbxCode.Document.ContentEnd).Text;
                TextChecking(Source.XMLParser.FormatXml(xml));
                Editing = false;
            }
            catch { }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            Editing = true;
            Timer.Stop();

            TextRange textRange = new TextRange(tbxCode.Document.ContentStart, tbxCode.Document.ContentEnd);

            if (IsXML && CheckXmlError)
                XmlErrorChecking(textRange.Text);

            TextChecking(textRange.Text);

            Editing = false;
        }

        private void XmlErrorChecking(string xml)
        {
            try
            {
                var result = Source.XMLParser.LoadFromXMLString(xml);
                tbkError.Text = null;
            }
            catch (Exception ex)
            {
                tbkError.Text = ex.Message;
            }
        }

        private void TextChecking(string text)
        {
            string[] lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            if (string.IsNullOrEmpty(lines.Last()))
            {
                SetLineNumber(lines.Length);
                CheckKeyword(lines, lines.Length - 1);
            }
            else
            {
                SetLineNumber(lines.Length + 1);
                CheckKeyword(lines, lines.Length);
            }

            string code = Compile(lines);
        }

        private void tbxCode_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!Editing)
            {
                Timer.Stop();
                Timer.Start();

                if (IsEnableXmlFormatter)
                {
                    XmlTimer.Stop();
                    XmlTimer.Start();
                }
            }
        }

        private TextPointer FindBeginOfWord()
        {
            StartWord = tbxCode.CaretPosition;  // this is the variable we will advance to the left until a non-letter character is found
            //EndWord = tbxCode.CaretPosition;    // this is the variable we will advance to the right until a non-letter character is found

            String stringBeforeCaret = StartWord.GetTextInRun(LogicalDirection.Backward);   // extract the text in the current run from the caret to the left
            //String stringAfterCaret = start.GetTextInRun(LogicalDirection.Forward);     // extract the text in the current run from the caret to the left

            Int32 countToMoveLeft = 0;  // we record how many positions we move to the left until a non-letter character is found
            //Int32 countToMoveRight = 0; // we record how many positions we move to the right until a non-letter character is found

            for (Int32 i = stringBeforeCaret.Length - 1; i >= 0; --i)
            {
                // if the character at the location CaretPosition-LeftOffset is a letter, we move more to the left
                if (IsXML)
                {
                    if (Char.IsLetter(stringBeforeCaret[i]) || stringBeforeCaret[i] == '<' || stringBeforeCaret[i] == '/')
                        countToMoveLeft++;
                    else
                        break; // otherwise we have found the beginning of the word
                }
                else
                {
                    if (Char.IsLetter(stringBeforeCaret[i]))
                        countToMoveLeft++;
                    else break; // otherwise we have found the beginning of the word
                }
            }

            //for (Int32 i = 0; i < stringAfterCaret.Length; ++i)
            //{
            //    // if the character at the location CaretPosition+RightOffset is a letter, we move more to the right
            //    if (Char.IsLetter(stringAfterCaret[i]))
            //        ++countToMoveRight;
            //    else break; // otherwise we have found the end of the word
            //}

            StartWord = StartWord.GetPositionAtOffset(-countToMoveLeft);    // modify the start pointer by the offset we have calculated
            //end = end.GetPositionAtOffset(countToMoveRight);        // modify the end pointer by the offset we have calculated

            // extract the text between those two pointers
            //TextRange r = new TextRange(start, end);
            //FilterWord = r.Text.ToLower();

            return StartWord;
        }

        private TextPointer FindEndOfWord()
        {
            EndWord = tbxCode.CaretPosition;    // this is the variable we will advance to the right until a non-letter character is found

            String stringAfterCaret = EndWord.GetTextInRun(LogicalDirection.Forward);     // extract the text in the current run from the caret to the left
            Int32 countToMoveRight = 0; // we record how many positions we move to the right until a non-letter character is found

            for (Int32 i = 0; i < stringAfterCaret.Length; ++i)
            {
                // if the character at the location CaretPosition+RightOffset is a letter, we move more to the right
                if (Char.IsLetter(stringAfterCaret[i]))
                    ++countToMoveRight;
                else break; // otherwise we have found the end of the word
            }

            EndWord = EndWord.GetPositionAtOffset(countToMoveRight);        // modify the end pointer by the offset we have calculated

            return EndWord;
        }

        private void DisplaySuggestionPopup()
        {
            if (!popSuggestion.IsOpen)
            {
                FindBeginOfWord();
                Rect rect = StartWord.GetCharacterRect(LogicalDirection.Backward);
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

        private void CheckKeyword(string[] lines, int lcount)
        {
            bool collect_comment_star = false;
            Rect rect = tbxCode.CaretPosition.GetCharacterRect(LogicalDirection.Forward);
            Point point = new Point(rect.X, rect.Y);

            tbxCode.Document.Blocks.Clear();

            for (int i = 0; i < lcount; i++)
            {
                int k = 0;
                bool collect_string = false;
                bool collect_comment_slash = false;
                char[] word = new char[1024];
                lines[i] = lines[i].Replace("\t", String.Empty);
                var chars = lines[i].ToCharArray();
                Paragraph paragraph = new Paragraph();

                for (int j = 0; j < chars.Length; j++)
                {
                    bool sign = false;
                    char c = chars[j];
                    int nj = j + 1;

                    if (!collect_comment_slash)
                    {
                        foreach (var delimiter in Delimiters)
                        {
                            if (c == delimiter)
                            {
                                sign = true;
                                break;
                            }
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
                        else
                        {
                            if (IsXML)
                            {
                                if (k > 1 && c == '>')
                                {
                                    CheckKeywordInLine(sign, c, new string(word, 0, k), paragraph);
                                    k = 0;
                                }
                            }
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
                            CheckKeywordInLine(sign, c, new string(word, 0, k), paragraph);
                            k = 0;
                        }
                    }
                }

                tbxCode.Document.Blocks.Add(paragraph);
            }

            tbxCode.CaretPosition = tbxCode.GetPositionFromPoint(point, true);
        }

        public string Compile(string[] lines)
        {
            string code = "";
            bool collect_comment_star = false;

            for (int i = 0; i < lines.Length - 1; i++)
            {
                int k = 0;
                bool collect_string = false;
                bool collect_comment_slash = false;
                char[] word = new char[1024];
                lines[i] = lines[i].Replace("\t", String.Empty);
                var chars = lines[i].ToCharArray();

                for (int j = 0; j < chars.Length; j++)
                {
                    bool sign = false;
                    char c = chars[j];
                    int nj = j + 1;

                    if (!collect_comment_slash)
                    {
                        foreach (var delimiter in Delimiters)
                        {
                            if (c == delimiter)
                            {
                                sign = true;
                                break;
                            }
                        }
                    }

                    if (!sign)
                    {
                        if (!collect_comment_slash)
                        {
                            word[k] = c;
                            k++;

                            if (nj == chars.Length)
                            {
                                if (collect_string)
                                    code += new string(word, 0, k);
                                else
                                    code += new string(word, 0, k);
                            }
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
                                    //code += new string(word, 0, k);
                                    k = 0;
                                }
                            }
                            else
                            {
                                //code += new string(word, 0, k);
                                k = 0;
                            }
                        }
                        else if (c == '"')
                        {
                            // check string
                            if (!collect_string)
                            {
                                collect_string = true;

                                code += new string(word, 0, k);
                                word[0] = '"';
                                k = 1;

                                if (nj == chars.Length)
                                    code += new string(word, 0, k);
                            }
                            else
                            {
                                collect_string = false;
                                word[k] = c;
                                k++;
                                code += new string(word, 0, k);
                                k = 0;
                            }
                        }
                        else if (collect_string)
                        {
                            word[k] = c;
                            k++;

                            if (nj == chars.Length)
                                code += new string(word, 0, k);
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

                                    //code += new string(word, 0, k);
                                    //word[0] = '/';
                                    //k = 1;

                                    //if (nj == chars.Length)
                                    //    code += new string(word, 0, k);
                                }
                                else if (nc == '*')
                                {
                                    collect_comment_star = true;

                                    //code += new string(word, 0, k);
                                    //word[0] = '/';
                                    //word[1] = '*';
                                    //k = 2;
                                    //j++;
                                    //nj++;

                                    //if (nj == chars.Length)
                                    //    code += new string(word, 0, k);
                                }
                                else
                                {
                                    //code += new string(word, 0, k);
                                    k = 0;
                                }
                            }
                            else
                            {
                                if (collect_comment_slash || collect_comment_star)
                                {
                                    word[k] = c;
                                    k++;
                                    //code += new string(word, 0, k);
                                }
                                else
                                    code += new string(word, 0, k);
                            }
                        }
                        else
                        {
                            if (k > 0)
                                code += new string(word, 0, k) + " ";
                            else
                                code += c;

                            k = 0;
                        }
                    }
                }
            }

            return code;
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

        private void tbxCode_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Space)
            {
                if (e.Key == Key.Up)
                {
                    if (popSuggestion.IsOpen)
                    {
                        if (lstKeyword.SelectedIndex > 0)
                            lstKeyword.SelectedIndex--;

                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.Down)
                {
                    if (popSuggestion.IsOpen)
                    {
                        if (lstKeyword.SelectedIndex < lstKeyword.Items.Count - 1)
                            lstKeyword.SelectedIndex++;

                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.Enter)
                {
                    e.Handled = SelectSuggestion();
                }
                else if (e.Key == Key.Right || e.Key == Key.Left || e.Key == Key.Escape)
                {
                    popSuggestion.IsOpen = false;
                }
                else
                {
                    string inputchar = null;

                    if (!IsXML)
                    {
                        if (e.Key >= Key.A && e.Key <= Key.Z)
                        {
                            inputchar = e.Key.ToString().ToLower();
                            DisplaySuggestionPopup();
                        }
                    }
                    else
                    {
                        if (e.Key >= Key.A && e.Key <= Key.Z)
                        {
                            inputchar = e.Key.ToString().ToLower();
                            DisplaySuggestionPopup();
                        }
                        else if (e.Key == Key.OemComma && Keyboard.Modifiers == ModifierKeys.Shift)
                        {
                            inputchar = "<";
                            DisplaySuggestionPopup();
                        }
                        else if (e.Key == Key.Oem2)
                        {
                            inputchar = "/";
                            DisplaySuggestionPopup();
                        }
                    }

                    FilterWord = tbxCode.CaretPosition.GetTextInRun(LogicalDirection.Backward).Trim().ToLower() + inputchar;

                    if (e.Key == Key.Back)
                    {
                        if (FilterWord.Length > 0)
                            FilterWord = FilterWord.Remove(FilterWord.Length - 1);

                        if (string.IsNullOrEmpty(FilterWord))
                            popSuggestion.IsOpen = false;
                    }

                    if (!IsXML)
                        lstKeyword.Items.Filter = r => { return (r as Keyword).Key.ToLower().StartsWith(FilterWord); };
                    else
                        lstKeyword.Items.Filter = r => { return (r as Keyword).Key.ToLower().Contains(FilterWord); };

                    if (lstKeyword.Items.Count > 0)
                        lstKeyword.SelectedIndex = 0;
                    else
                        // filtered list is empty
                        popSuggestion.IsOpen = false;
                }
            }
            else
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                    DisplaySuggestionPopup();
                else
                {
                    popSuggestion.IsOpen = false;
                    lstKeyword.Items.Filter = null;
                }
            }
        }

        private bool SelectSuggestion()
        {
            if (popSuggestion.IsOpen)
            {
                if (lstKeyword.SelectedItem != null)
                {
                    FindBeginOfWord();
                    FindEndOfWord();
                    TextRange textRange = new TextRange(StartWord, EndWord);
                    textRange.Text = textRange.Text = "";

                    tbxCode.CaretPosition = tbxCode.CaretPosition.GetPositionAtOffset(0, LogicalDirection.Forward);
                    tbxCode.CaretPosition.InsertTextInRun((lstKeyword.SelectedItem as Keyword).Key);
                }

                popSuggestion.IsOpen = false;
                return true;
            }

            return false;
        }

        private void GriKeyItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SelectSuggestion();
        }
    }
}
