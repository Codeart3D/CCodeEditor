using CCodeEditorLib.Source;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
        private bool UndoAction = false;
        private bool Formated = false;
        private bool Checking = false;
        private int TagCounter = 0;
        private string FilterWord = "";
        private Keyword CurrentKeyword;
        private TextPointer StartWord;
        private TextPointer EndWord;
        private List<string> TagNames;
        private List<Keyword> AttribList;
        private List<LType> Types = new List<LType>();
        private List<Keyword> Keywords = new List<Keyword>();
        private string CurrentTag = null;
        private string[] Lines;
        private string CodeText;
        private char[] Delimiters;
        private char[] CodeDelimiters = new char[] { ' ', '\0', '(', ')', '.', '=', '+', '-', '*', '/', '>', '<', '&', '|', '{', '}', '"' };
        private char[] XmlDelimiters = new char[] { ' ', '\0', '=' };

        private Stack<string> UndoStack = new Stack<string>();
        private Stack<string> RedoStack = new Stack<string>();

        private DispatcherTimer Timer;
        private DispatcherTimer XmlTimer = null;

        public bool IsXML { get; set; }
        public Visibility DisplayErrorSection { get { return tbkError.Visibility; } set { tbkError.Visibility = value; } }
        public string Error { get { return tbkError.Text; } set { tbkError.Text = value; } }
        public bool CheckXmlError { get; set; }
        public bool IsEnableXmlFormatter { get; set; }
        public bool UndoRedoShortcutKey { get; set; } = true;

        // Event
        public delegate void XMLChangedHandler(object sender, string Xml);
        public event XMLChangedHandler XmlChanged;

        public string Text
        {
            set
            {
                if (IsLoaded)
                {
                    Editing = true;

                    CodeText = Source.XMLParser.FormatXml(value);
                    Lines = CodeText.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                    TextChecking();
                    SetLineNumber();

                    Editing = false;
                }
            }
        }

        public void Clear()
        {
            Editing = true;
            CodeText = "";

            tbxCode.Document.Blocks.Clear();
            SetLineNumber();

            Editing = false;
        }

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
                    XmlTimer.Interval = new TimeSpan(0, 0, 10);
                    XmlTimer.Tick += XmlTimer_Tick;
                }

                Delimiters = XmlDelimiters;

                Keywords.Add(new Keyword(Brushes.PaleGoldenrod, "<", KeywordType.XMLStart, null, false));
                Keywords.Add(new Keyword(Brushes.PaleGoldenrod, "/>", KeywordType.XMLEnd, null, false));
                Keywords.Add(new Keyword(Brushes.PaleGoldenrod, ">", KeywordType.XMLEnd, null, false));
                Keywords.Add(new Keyword(Brushes.PaleGoldenrod, "=", KeywordType.XMLEqual, "=\"\"", false, 1));


                if (Debugger.IsAttached)
                {
                    List<string> basep = new List<string>();
                    basep.Add("X");
                    basep.Add("Y");
                    basep.Add("Width");
                    basep.Add("Height");

                    List<string> pi = new List<string>();
                    pi.Add("Color");
                    pi.Add("Texture");

                    List<string> ps = new List<string>();
                    ps.Add("Play");
                    ps.Add("Volume");

                    List<string> pp = new List<string>();
                    pp.Add("Background");
                    pp.Add("Foreground");

                    var basepa = GetXmlAttrib(basep);
                    var pia = GetXmlAttrib(pi);
                    var psa = GetXmlAttrib(ps);
                    var ppa = GetXmlAttrib(pp);

                    var att = new List<KeywordClass>();
                    att.Add(new KeywordClass("Image", pia, basepa));
                    att.Add(new KeywordClass("Sound", psa, basepa));
                    att.Add(new KeywordClass("Progressbar", ppa, basepa));
                    SetXmlClasses(att);
                }
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
            Timer.Interval = new TimeSpan(0, 0, 0, 0, 400);
            Timer.Tick += Timer_Tick;

            lstKeyword.ItemsSource = Keywords.Where(p => p.Visible);

            tbxCode.Focus();
        }

        public void SetXmlRoot(KeywordClass root)
        {
            Keywords.Add(new Keyword(Brushes.PaleGoldenrod, $"<{root.Name}>", KeywordType.XMLRootTag) { KeyName = root.Name, Suggestions = root.Properties, BaseSuggestions = root.BaseProperties });
            Keywords.Add(new Keyword(Brushes.PaleGoldenrod, $"</{root.Name}>", KeywordType.XMLEndTag));
            Keywords.Add(new Keyword(Brushes.PaleGoldenrod, $"<{root.Name}", KeywordType.XMLRootTag, null, false));
        }

        public void SetXmlClasses(List<KeywordClass> classes)
        {
            foreach (var item in classes)
            {
                Keywords.Add(new Keyword(Brushes.PaleGoldenrod, $"<{item.Name}/>", KeywordType.XMLTag, null, true, 2) { KeyName = item.Name, Suggestions = item.Properties, BaseSuggestions = item.BaseProperties });
                Keywords.Add(new Keyword(Brushes.PaleGoldenrod, $"<{item.Name}", KeywordType.XMLTag, null, false));
            }
        }

        public void SetXmlClassWithChild(KeywordClass cclass)
        {
            Keywords.Add(new Keyword(Brushes.PaleGoldenrod, $"<{cclass.Name}>", KeywordType.XMLTag, null, true, 1)
            { KeyName = cclass.Name, Suggestions = cclass.Properties, BaseSuggestions = cclass.BaseProperties, InsertAfter = $"</{cclass.Name}>" });
            Keywords.Add(new Keyword(Brushes.PaleGoldenrod, $"</{cclass.Name}>", KeywordType.XMLEndTag, null, true));
            Keywords.Add(new Keyword(Brushes.PaleGoldenrod, $"<{cclass.Name}", KeywordType.XMLTag, null, false));
        }

        public List<Keyword> GetXmlAttrib(List<string> attribs)
        {
            List<Keyword> atts = new List<Keyword>();

            foreach (var item in attribs)
                atts.Add(new Keyword(Brushes.LightGreen, item, KeywordType.XMLAttrib, $"{item}=\"\"", true, 1));

            return atts;
        }

        private void XmlTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                Timer.Stop();
                XmlTimer.Stop();
                XmlFormat();
            }
            catch { }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            Editing = true;
            Timer.Stop();

            TextChecking();
            SetLineNumber();

            // this methode must call after TextChecking because find childs indexes
            if (IsXML && CheckXmlError)
                XmlErrorChecking();

            Editing = false;
        }

        private void XmlErrorChecking()
        {
            try
            {
                //Source.XMLParser.LoadFromXMLString(xml);
                XmlChanged?.Invoke(this, CodeText);
                //tbkError.Text = null;
            }
            catch (Exception ex)
            {
                tbkError.Text = ex.Message;
            }
        }

        private void TextChecking()
        {
            if (!IsXML)
                CheckCodeKeyword();
            else
                CheckXMLKeyword();
        }

        private void tbxCode_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!Editing)
            {
                CodeText = new TextRange(tbxCode.Document.ContentStart, tbxCode.Document.ContentEnd).Text;

                // StartChecking
                UndoAction = false;

                Lines = CodeText.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                if (!string.IsNullOrEmpty(CodeText))
                    UndoStack.Push(CodeText);

                Timer.Stop();
                Timer.Start();

                if (IsXML && IsEnableXmlFormatter)
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

        private void SetLineNumber()
        {
            string lineno = "1\n";

            for (int i = 2; i <= tbxCode.Document.Blocks.Count; i++)
                lineno += i + "\n";

            tbkLineNo.Text = lineno;
        }

        private bool Equal(double v1, double v2)
        {
            return Math.Abs(v2 - v1) < 1.0;
        }

        private void CollectXMLKeyword(int index, Paragraph paragraph)
        {
            int k = 0;
            bool collect_string = false;
            char[] word = new char[1024];

            string tag = TextUtils.FindCurrentXmlTag(Lines[index]);

            if (tag != null)
                CurrentKeyword = Keywords.Where(p => p.KeyName == tag).FirstOrDefault();

            for (int j = 0; j < Lines[index].Length; j++)
            {
                bool sign = false;
                char c = Lines[index][j];
                int nj = j + 1;

                foreach (var delimiter in Delimiters)
                {
                    if (c == delimiter)
                    {
                        sign = true;
                        break;
                    }
                }

                if (!sign)
                {
                    word[k] = c;
                    k++;

                    if (nj == Lines[index].Length)
                    {
                        if (collect_string)
                            paragraph.Inlines.Add(new Run(new string(word, 0, k)) { Foreground = Brushes.LightSalmon });
                        else
                            CheckKeywordInLine(sign, c, new string(word, 0, k), paragraph);
                    }
                    else if (k > 1 && c == '>')
                    {
                        CheckKeywordInLine(sign, c, new string(word, 0, k), paragraph);
                        k = 0;
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        // check string
                        if (!collect_string)
                        {
                            collect_string = true;

                            CheckKeywordInLine(false, c, new string(word, 0, k), paragraph);
                            word[0] = '"';
                            k = 1;

                            if (nj == Lines[index].Length)
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

                        if (nj == Lines[index].Length)
                            paragraph.Inlines.Add(new Run(new string(word, 0, k)) { Foreground = Brushes.LightSalmon });
                    }
                    else
                    {
                        CheckKeywordInLine(sign, c, new string(word, 0, k), paragraph);
                        k = 0;
                    }
                }
            }
        }

        private void CheckXMLKeyword()
        {
            if (Checking)
                return;

            Checking = true;
            int lcount;

            if (string.IsNullOrEmpty(Lines.Last()))
                lcount = Lines.Length - 1;
            else
                lcount = Lines.Length;

            // Save caret position
            Rect rect = tbxCode.CaretPosition.GetCharacterRect(LogicalDirection.Forward);
            Point point = new Point(rect.X, rect.Y);

            // Start checking
            tbxCode.Document.Blocks.Clear();

            for (int i = 0; i < lcount; i++)
            {
                Paragraph paragraph = new Paragraph();

                CollectXMLKeyword(i, paragraph);

                tbxCode.Document.Blocks.Add(paragraph);
            }

            // return caret to pre position
            var tp = tbxCode.GetPositionFromPoint(point, true);

            if (tp != null)
                tbxCode.CaretPosition = tp;

            Checking = false;
        }

        private void CheckCodeKeyword()
        {
            if (Checking)
                return;

            Checking = true;

            int lcount;
            bool collect_comment_star = false;
            Rect rect = tbxCode.CaretPosition.GetCharacterRect(LogicalDirection.Forward);
            Point point = new Point(rect.X, rect.Y);

            tbxCode.Document.Blocks.Clear();

            if (string.IsNullOrEmpty(Lines.Last()))
                lcount = Lines.Length - 1;
            else
                lcount = Lines.Length;

            for (int i = 0; i < lcount; i++)
            {
                int k = 0;
                bool collect_string = false;
                bool collect_comment_slash = false;
                char[] word = new char[1024];
                Lines[i] = Lines[i].Replace("\t", String.Empty);
                Paragraph paragraph = new Paragraph();

                for (int j = 0; j < Lines[i].Length; j++)
                {
                    bool sign = false;
                    char c = Lines[i][j];
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

                        if (nj == Lines[i].Length)
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
                            if (nj < Lines[i].Length)
                            {
                                // check with next character
                                char nc = Lines[i][nj];

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

                                if (nj == Lines[i].Length)
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

                            if (nj == Lines[i].Length)
                                paragraph.Inlines.Add(new Run(new string(word, 0, k)) { Foreground = Brushes.LightSalmon });
                        }
                        else if (c == '/')
                        {
                            // check comment
                            if (nj < Lines[i].Length)
                            {
                                // check with next character
                                char nc = Lines[i][nj];

                                if (nc == '/')
                                {
                                    collect_comment_slash = true;

                                    CheckKeywordInLine(false, c, new string(word, 0, k), paragraph);
                                    word[0] = '/';
                                    k = 1;

                                    if (nj == Lines[i].Length)
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

                                    if (nj == Lines[i].Length)
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

            var tp = tbxCode.GetPositionFromPoint(point, true);

            if (tp != null)
                tbxCode.CaretPosition = tp;

            Checking = false;
        }

        public string Compile()
        {
            string code = "";
            bool collect_comment_star = false;

            for (int i = 0; i < Lines.Length - 1; i++)
            {
                int k = 0;
                bool collect_string = false;
                bool collect_comment_slash = false;
                char[] word = new char[1024];
                Lines[i] = Lines[i].Replace("\t", String.Empty);
                var chars = Lines[i].ToCharArray();

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
                {
                    run.Foreground = key.Color;

                    if (IsXML)
                    {
                        if (key.Type == KeywordType.XMLTag)
                        {
                            if (!Formated)
                            {
                                if (TagNames != null && TagNames.Count > 0 && TagCounter < TagNames.Count)
                                {
                                    if (TagNames[TagCounter] != null)
                                    {
                                        int s = 0;

                                        if (part[part.Length - 1] == '>')
                                        {
                                            if (part[part.Length - 2] == '/')
                                                s = 2;
                                            else
                                                s = 1;

                                            run = new Run(part.Substring(0, part.Length - s) + " ");
                                            run.Foreground = key.Color;
                                        }

                                        // first add keyword
                                        paragraph.Inlines.Add(run);
                                        // then add name
                                        run = new Run("Name");
                                        run.Foreground = Brushes.LightGreen;
                                        paragraph.Inlines.Add(run);
                                        run = new Run("=\"" + TagNames[TagCounter] + "\"");

                                        if (s == 1)
                                        {
                                            paragraph.Inlines.Add(run);
                                            run = new Run(">");
                                            run.Foreground = key.Color;
                                        }
                                        else if (s == 2)
                                        {
                                            paragraph.Inlines.Add(run);
                                            run = new Run("/>");
                                            run.Foreground = key.Color;
                                        }
                                    }
                                }

                                TagCounter++;
                            }
                        }
                    }
                }
                else
                {
                    if (CurrentKeyword != null)
                    {
                        key = CurrentKeyword.BaseSuggestions?.Where(p => p.Key == part).FirstOrDefault();

                        if (key != null)
                            run.Foreground = key.Color;
                        else
                        {
                            key = CurrentKeyword.Suggestions?.Where(p => p.Key == part).FirstOrDefault();

                            if (key != null)
                                run.Foreground = key.Color;
                            else
                            {
                                run.Foreground = Brushes.LightGray;
                            }
                        }
                    }
                    else
                        run.Foreground = Brushes.LightGray;
                }

                paragraph.Inlines.Add(run);
            }

            if (sign)
                paragraph.Inlines.Add(new Run(c.ToString()) { Foreground = Brushes.LightGray });
        }

        public void SetTagNames(List<string> names)
        {
            Editing = true;
            TagNames = names;
            TagCounter = 0;

            CheckXMLKeyword();

            TagNames = null;
            Editing = false;
        }

        private void tbxCode_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                if (e.Key == Key.Z)
                {
                    if (UndoRedoShortcutKey)
                        Redo();
                }
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.Space)
                {
                    DisplaySuggestionPopup();
                    return;
                }
                if (e.Key == Key.K)
                {
                    if (IsXML)
                        XmlFormat();
                    else
                        Format();
                }
                else if (e.Key == Key.Enter)
                    TextUtils.InsertEmptyLine(tbxCode);
                else if (e.Key == Key.D)
                    TextUtils.CopyCurrentLine(tbxCode);
                else if (e.Key == Key.Z)
                {
                    if (UndoRedoShortcutKey)
                        Undo();
                }
                else if (e.Key == Key.Y)
                {
                    if (UndoRedoShortcutKey)
                        Redo();
                }

                popSuggestion.IsOpen = false;
                lstKeyword.Items.Filter = null;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                if (e.Key == Key.Delete)
                    TextUtils.DeleteCurrentLine(tbxCode);
                else
                    CaptureInput(e.Key);
            }
            else
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
                    e.Handled = SelectSuggestion();
                else if (e.Key == Key.Right || e.Key == Key.Left || e.Key == Key.Escape)
                    popSuggestion.IsOpen = false;
                else
                    CaptureInput(e.Key);
            }
        }

        private void CaptureInput(Key key)
        {
            string inputchar = null;

            if (!IsXML)
            {
                if (key >= Key.A && key <= Key.Z)
                {
                    inputchar = key.ToString().ToLower();
                    DisplaySuggestionPopup();
                }
            }
            else
            {
                if (key >= Key.A && key <= Key.Z)
                {
                    inputchar = key.ToString().ToLower();
                    DisplaySuggestionPopup();
                }
                else if (key == Key.OemComma && Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    inputchar = "<";
                    DisplaySuggestionPopup();
                }
                else if (key == Key.Oem2)
                {
                    inputchar = "/";
                    DisplaySuggestionPopup();
                }
            }

            FilterWord = tbxCode.CaretPosition.GetTextInRun(LogicalDirection.Backward).Trim().ToLower() + inputchar;

            if (key == Key.Back)
            {
                if (FilterWord.Length > 0)
                    FilterWord = FilterWord.Remove(FilterWord.Length - 1);

                if (string.IsNullOrEmpty(FilterWord))
                    popSuggestion.IsOpen = false;
            }

            //if (!IsXML)
            //else
            lstKeyword.Items.Filter = r => { return (r as Keyword).Key.ToLower().StartsWith(FilterWord); };

            if (lstKeyword.Items.Count == 0)
                lstKeyword.Items.Filter = r => { return (r as Keyword).Key.ToLower().Contains(FilterWord); };

            if (lstKeyword.Items.Count > 0)
                lstKeyword.SelectedIndex = 0;
            else
                // filtered list is empty
                popSuggestion.IsOpen = false;
        }

        private bool SelectSuggestion()
        {
            if (popSuggestion.IsOpen)
            {
                Keyword keyword = lstKeyword.SelectedItem as Keyword;

                if (keyword != null)
                {
                    FindBeginOfWord();
                    FindEndOfWord();
                    TextRange textRange = new TextRange(StartWord, EndWord);
                    textRange.Text = "";

                    tbxCode.CaretPosition = tbxCode.CaretPosition.GetPositionAtOffset(0, LogicalDirection.Forward);

                    string sugg = "";
                    string prec = TextUtils.GetPreCharacter(tbxCode)?.Trim();

                    if (!string.IsNullOrEmpty(prec))
                        sugg = " ";

                    sugg += keyword.ReplaceKey == null ? keyword.Key : keyword.ReplaceKey;

                    if (keyword.InsertAfter != null)
                        sugg += keyword.InsertAfter;

                    tbxCode.CaretPosition.InsertTextInRun(sugg);

                    for (int i = 0; i < keyword.ReturnBackward; i++)
                        tbxCode.CaretPosition = tbxCode.CaretPosition.GetNextInsertionPosition(LogicalDirection.Backward);
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

        public void Undo()
        {
            try
            {
                if (UndoStack.Count > 0)
                {
                    Editing = true;
                    CodeText = UndoStack.Pop();
                    RedoStack.Push(CodeText);

                    // pop again when undo action is false
                    if (!UndoAction && UndoStack.Count > 0)
                    {
                        CodeText = UndoStack.Pop();
                        RedoStack.Push(CodeText);
                    }

                    Lines = CodeText.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                    TextChecking();
                    SetLineNumber();
                    Editing = false;
                    XmlChanged?.Invoke(this, CodeText);
                }
                else
                    tbxCode.Document.Blocks.Clear();

                UndoAction = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        public void Redo()
        {
            if (RedoStack.Count > 0)
            {
                Editing = true;
                UndoAction = false;

                CodeText = RedoStack.Pop();
                UndoStack.Push(CodeText);

                Lines = CodeText.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                TextChecking();
                SetLineNumber();
                Editing = false;
                XmlChanged?.Invoke(this, CodeText);
            }
        }

        private void FindCurrentTag()
        {
            string tag = TextUtils.FindCurrentXmlTag(tbxCode);

            if (tag != null)
            {
                if (tag != CurrentTag)
                {
                    CurrentTag = tag;
                    Keyword key = Keywords.Where(p => p.KeyName == tag).FirstOrDefault();

                    if (key != null)
                    {
                        lstKeyword.Items.Filter = null;
                        AttribList = new List<Keyword>();
                        AttribList.AddRange(key.Suggestions);

                        if (key.BaseSuggestions != null)
                            AttribList.AddRange(key.BaseSuggestions);

                        lstKeyword.ItemsSource = null;
                        lstKeyword.ItemsSource = AttribList;
                    }
                }
            }
            else
            {
                CurrentTag = null;
                lstKeyword.ItemsSource = Keywords.Where(p => p.Visible);
            }
        }

        private void TbxCode_SelectionChanged(object sender, RoutedEventArgs e)
        {
            FindCurrentTag();
        }

        private void LstKeyword_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstKeyword.SelectedItem != null)
                lstKeyword.ScrollIntoView(lstKeyword.SelectedItem);
        }

        private void XmlFormat()
        {
            Editing = true;
            Formated = true;

            TextRange range = new TextRange(tbxCode.Document.ContentStart, tbxCode.Document.ContentEnd);
            CodeText = Source.XMLParser.FormatXml(range.Text);
            Lines = CodeText.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            CheckXMLKeyword();
            SetLineNumber();

            Formated = false;
            Editing = false;
        }

        private void Format()
        {
            Editing = true;
            int startcolumn = 0;
            TextRange range = new TextRange(tbxCode.Document.ContentStart, tbxCode.Document.ContentEnd);
            string code = range.Text;
            string preword = null;
            char[] word = new char[512];
            char[] ncode = new char[code.Length + 10000];
            int j = 0;
            int k = 0;
            int last = code.Length - 1;
            char precs = '\0'; // pre character without space

            int parantes_no = 0;
            bool keyword_seen = false;
            bool collect_string = false;
            bool collect_comment = false;
            bool collect_comment_star = false;

            for (int i = 0; i < code.Length; i++)
            {
                int pre = i - 1;
                int nex = i + 1;

                char curc = code[i];
                char prec = '\0'; // prev character with space 
                char nexc = '\0'; // next character with space

                if (pre > -1)
                    prec = code[pre];

                if (nex < code.Length)
                    nexc = code[nex];


                if (collect_string)
                {
                    if (curc == '\"')
                        collect_string = false;

                    ncode[j++] = curc;

                    continue;
                }
                else if (collect_comment)
                {
                    if (curc == '\r' || curc == '\n' || curc == '\0')
                        collect_comment = false;

                    ncode[j++] = curc;

                    continue;
                }
                else if (collect_comment_star)
                {
                    if (curc == '*' && nexc == '/')
                        collect_comment_star = false;

                    ncode[j++] = curc;

                    continue;
                }


                // remove more than one space
                char nextcs = SkipForward(ref code, ' ', ref i, last); // next character without space

                if (curc == ';')
                {
                    SetPreWord(ref preword, ref word, ref k);
                    // go to new line after ; character
                    ncode[j++] = curc;

                    // go to next line, commented for 'for' keyword
                    //if (nextcs != '\r')
                    //    ncode[j++] = '\n';

                    precs = ncode[j - 1];
                }
                else if (curc == '{')
                {
                    parantes_no = 0;
                    keyword_seen = false;
                    SetPreWord(ref preword, ref word, ref k);
                    // go to new line after and before { character
                    // set start column gap
                    InsertGap(ncode, ref j, startcolumn);
                    startcolumn++;

                    if (precs != '\n')
                        ncode[j++] = '\n';

                    ncode[j++] = '{';

                    if (nextcs != '\r')
                        ncode[j++] = '\n';

                    precs = ncode[j - 1];
                }
                else if (curc == '}')
                {
                    SetPreWord(ref preword, ref word, ref k);
                    // go to new line after and before } character
                    // set start column gap
                    startcolumn--;
                    InsertGap(ncode, ref j, startcolumn);

                    if (precs != '\n')
                        ncode[j++] = '\n';

                    ncode[j++] = '}';

                    if (nextcs != '\r')
                        ncode[j++] = '\n';

                    precs = ncode[j - 1];
                }
                else if (curc == '\r')
                {
                    SetPreWord(ref preword, ref word, ref k);
                    precs = '\n';
                    ncode[j++] = '\n';
                    // insert gap in begin of line
                }
                else if (curc == ' ')
                {
                    SetPreWord(ref preword, ref word, ref k);

                    // remove space before ; character
                    if (nextcs != ';')
                        ncode[j++] = curc;
                }
                else if (curc == '\"')
                {
                    collect_string = true;
                    ncode[j++] = '\"';
                }
                else if (curc == '/' && nexc == '/')
                {
                    collect_comment = true;
                    ncode[j++] = '/';
                    ncode[j++] = '/';
                    i++;
                }
                else if (curc == '/' && nexc == '*')
                {
                    collect_comment = true;
                    ncode[j++] = '/';
                    ncode[j++] = '*';
                    i++;
                }
                else
                {
                    if (curc != '\n')
                    {
                        // collect word
                        if (CheckLetterNumber(curc))
                            word[k++] = curc;
                        else
                            SetPreWord(ref preword, ref word, ref k);

                        // check main keywords
                        if (curc == '(')
                        {
                            if (CheckMainKeyword(ref preword))
                            {
                                if (prec != ' ')
                                    ncode[j++] = ' ';
                            }

                            parantes_no++;
                            keyword_seen = true;
                        }
                        else if (curc == ')')
                        {
                            parantes_no--;

                            if (parantes_no == 0 && !CheckEndLine(nextcs))
                            {
                                keyword_seen = false;

                                if (nexc != ' ')
                                {
                                    ncode[j++] = ')';
                                    ncode[j++] = '\n';
                                    InsertGap(ncode, ref j, startcolumn + 1);
                                    precs = '\n';
                                    continue;
                                }
                            }
                        }

                        // insert space before a sign
                        if ((CheckLetterNumber(prec) && CheckStandardSign(curc)) && !(curc == '+' && nexc == '+'))
                            ncode[j++] = ' ';

                        precs = curc;
                        ncode[j++] = curc;

                        // insert space after a sign
                        if (CheckStandardSignWithExtra(curc) && CheckLetterNumber(nexc))
                            ncode[j++] = ' ';
                    }
                    else if (precs == '\n' && nextcs != '}' && nextcs != '{')
                    {
                        SetPreWord(ref preword, ref word, ref k);

                        // insert gap for 'if' without { } sign
                        if (keyword_seen && parantes_no == 0)
                        {
                            keyword_seen = false;
                            InsertGap(ncode, ref j, startcolumn + 1);
                        }
                        else
                            InsertGap(ncode, ref j, startcolumn);
                    }
                }
            }

            CodeText = new string(ncode, 0, j);
            range.Text = CodeText;
            Lines = CodeText.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            //TextChecking();
            Editing = false;
        }

        private bool CheckEndLine(char next)
        {
            if (next == '\r' || next == '\n' || next == '\0')
                return true;

            return false;
        }

        private void SetPreWord(ref string preword, ref char[] word, ref int k)
        {
            preword = new string(word, 0, k);
            k = 0;
        }

        private bool CheckMainKeyword(ref string w)
        {
            if (w == "if" || w == "else" || w == "for" || w == "foreach" || w == "while")
                return true;

            return false;
        }

        private bool CheckStandardSign(char sign)
        {
            if (sign == '=' || sign == '+' || sign == '-' || sign == '*' || sign == '/' ||
                sign == '%' || sign == '^' || sign == '<' || sign == '>')
                return true;

            return false;
        }

        private bool CheckStandardSignWithExtra(char sign)
        {
            if (sign == '=' || sign == '+' || sign == '-' || sign == '*' || sign == '/' ||
                sign == '%' || sign == '^' || sign == '<' || sign == '>' || sign == ',')
                return true;

            return false;
        }

        private bool CheckLetterNumber(char c)
        {
            if (Char.IsLetter(c) || Char.IsNumber(c))
                return true;

            return false;
        }

        private char SkipForward(ref string t, char c, ref int i, int last)
        {
            char e = '\0';
            int k = i;

            while (k < last)
            {
                if (t[++k] != c)
                {
                    e = t[k];
                    k--;
                    break;
                }
            }

            if (k - i > 1)
                i = --k;


            return e;
        }

        private void InsertGap(char[] t, ref int j, int startcolumn)
        {
            for (int k = 0; k < startcolumn * 4; k++)
                t[j++] = ' ';
        }
    }
}
