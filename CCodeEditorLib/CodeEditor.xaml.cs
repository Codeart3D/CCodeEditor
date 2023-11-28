using CCodeEditorLib.Source;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
        private bool InitOnce = false;
        private bool Editing = false;
        private bool UndoAction = false;
        private bool Formated = false;
        private bool Checking = false;
        private int TagCounter = 0;
        private string FilterWord = "";
        private Typeface Typeface;
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
        private TextBlock[] textBlocks = new TextBlock[100];
        private char[] Delimiters;
        private char[] FindDelimiters = new char[] { ' ', '<', '>', '{', '}', '[', ']', '(', ')', ',', '.' };
        private char[] CodeDelimiters = new char[] { ' ', '\0', '(', ')', '.', '=', '+', '-', '*', '/', '>', '<', '&', '|', '{', '}', '"', ',', ';' };
        private char[] XmlDelimiters = new char[] { ' ', '\0', '=' };

        private Stack<string> UndoStack = new Stack<string>();
        private Stack<string> RedoStack = new Stack<string>();

        private DispatcherTimer Timer;
        private DispatcherTimer XmlTimer = null;

        private SolidColorBrush FindMarkBrush = new SolidColorBrush(Color.FromRgb(40, 100, 40));
        private SolidColorBrush MainKeywordBrush = new SolidColorBrush(Color.FromRgb(65, 170, 220));
        private SolidColorBrush LineNumberColor = new SolidColorBrush(Color.FromRgb(80, 170, 160));
        private SolidColorBrush EnumColor = new SolidColorBrush(Color.FromRgb(190, 230, 150));
        private SolidColorBrush VariableColor = new SolidColorBrush(Color.FromRgb(130, 130, 130));
        // 230, 200, 150 cream gold

        public Visibility DisplayErrorSection { get { return tbkError.Visibility; } set { tbkError.Visibility = value; } }
        public string Error { get { return tbkError.Text; } set { tbkError.Text = value; } }
        public bool CheckXmlError { get; set; }
        public bool IsEnableXmlFormatter { get; set; }
        public bool UndoRedoShortcutKey { get; set; } = true;

        // Event
        public delegate void XMLChangedHandler(object sender, string Xml);
        public event XMLChangedHandler XmlChanged;

        public delegate void CodeEditorTextReplace(CodeEditor editor);
        public static event CodeEditorTextReplace RequestToReplaceText;

        public event TextChangedEventHandler TextChanged;

        public enum EditorCodeType
        {
            CSharp,
            Shader,
            XML
        }

        private EditorCodeType InputCodeType;

        public string Text
        {
            get
            {
                return new TextRange(tbxCode.Document.ContentStart, tbxCode.Document.ContentEnd).Text;
            }

            set
            {
                //if (IsLoaded)
                {
                    Editing = true;

                    if (CodeType == EditorCodeType.XML)
                        CodeText = Source.XMLParser.FormatXml(value);
                    else
                        CodeText = value;

                    Lines = CodeText.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                    TextChecking();
                    SetLineNumber();
                    CheckScrollBarVisibility();

                    Editing = false;
                }
            }
        }

        public string Caption { get { return tbkCaption.Text; } set { tbkCaption.Text = value; } }
        public bool IsReadOnly { get { return tbxCode.IsReadOnly; } set { tbxCode.IsReadOnly = value; tbxCode.Opacity = value ? 0.6 : 1.0; } }

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

            Typeface = new Typeface(tbxCode.FontFamily, tbxCode.FontStyle, tbxCode.FontWeight, tbxCode.FontStretch);

            for (int i = 0; i < 100; i++)
            {
                textBlocks[i] = new TextBlock() { Margin = new Thickness(0, 0, 0, 0), TextAlignment = TextAlignment.Right, FontSize = 13, Foreground = LineNumberColor };
                griLine.Children.Add(textBlocks[i]);
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!InitOnce)
            {
                InitOnce = true;

                if (InputCodeType == EditorCodeType.XML)
                {
                    if (IsEnableXmlFormatter)
                    {
                        XmlTimer = new DispatcherTimer();
                        XmlTimer.Interval = new TimeSpan(0, 0, 10);
                        XmlTimer.Tick += XmlTimer_Tick;
                    }
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
            }

            tbxCode.Focus();
        }

        public EditorCodeType CodeType
        {
            get { return InputCodeType; }

            set
            {
                InputCodeType = value;
                Keywords.Clear();

                if (value == EditorCodeType.CSharp)
                {
                    Delimiters = CodeDelimiters;

                    Keywords.Add(new Keyword(MainKeywordBrush, "if"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "else"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "foreach"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "for"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "while"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "var"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "in"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "return"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "continue"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "break"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "null"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "true"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "false"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "public"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "object"));
                }
                else if (value == EditorCodeType.Shader)
                {
                    Delimiters = CodeDelimiters;

                    Keywords.Add(new Keyword(MainKeywordBrush, "void"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "int"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "float"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "double"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "bool"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "char"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "if"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "else"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "for"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "return"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "continue"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "break"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "null"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "true"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "false"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "struct"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "class"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "in"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "out"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "inout"));

                    Keywords.Add(new Keyword(MainKeywordBrush, "uniform"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "varying"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "attribute"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "lowp"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "mediump"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "highp"));

                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "vec2"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "vec3"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "vec4"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "mat2"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "mat3"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "mat4"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "sampler2D"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "texture2D"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "gl_Position"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "gl_FragColor"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "gl_FragCoord"));

                    Keywords.Add(new Keyword(Brushes.HotPink, "#define"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "PIXEL_SHADER"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "VERTEX_SHADER"));
                }
                else if (value == EditorCodeType.XML)
                {
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
            }
        }

        public void AddCSharpClassKeyword(string ClassKey)
        {
            Keywords.Add(new Keyword(Brushes.LightSeaGreen, ClassKey, KeywordType.Class));
        }

        public void AddCSharpEnumKeyword(string EnumKey)
        {
            Keywords.Add(new Keyword(EnumColor, EnumKey, KeywordType.Enum));
        }

        public void ClearVariableKeywords()
        {
            Keywords.RemoveAll(p => p.Type == KeywordType.Variable);
        }

        public void AddCSharpVariableKeyword(string VariableKey)
        {
            Keywords.Add(new Keyword(VariableColor, VariableKey, KeywordType.Variable));
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
                CheckScrollBarVisibility();
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
            if (InputCodeType == EditorCodeType.XML && CheckXmlError)
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
            if (InputCodeType != EditorCodeType.XML)
                CheckCodeKeyword();
            else
                CheckXMLKeyword();
        }

        private void SetLineNumber()
        {
            int i = 0;
            int num = 0;
            double height = tbxCode.ActualHeight + 20;

            foreach (var item in tbxCode.Document.Blocks)
            {
                num++;
                Rect rect = item.ContentStart.GetCharacterRect(LogicalDirection.Forward);

                if (rect.Top > -20 && rect.Top < height)
                {
                    textBlocks[i].Text = num.ToString();
                    textBlocks[i].Visibility = Visibility.Visible;
                    textBlocks[i++].Margin = new Thickness(0, rect.Top, 0, 0);
                }
                else if (rect.Top > height)
                    break;
            }

            for (; i < 100; i++)
                textBlocks[i].Visibility = Visibility.Collapsed;
        }

        private void tbxCode_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!Editing)
            {
                CodeText = new TextRange(tbxCode.Document.ContentStart, tbxCode.Document.ContentEnd).Text;
                CheckScrollBarVisibility();

                // StartChecking
                UndoAction = false;

                Lines = CodeText.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                if (!string.IsNullOrEmpty(CodeText))
                    UndoStack.Push(CodeText);

                Timer.Stop();
                Timer.Start();

                if (InputCodeType == EditorCodeType.XML && IsEnableXmlFormatter)
                {
                    XmlTimer.Stop();
                    XmlTimer.Start();
                }

                TextChanged?.Invoke(sender, e);
            }
        }

        private void CheckScrollBarVisibility()
        {
            if (IsLoaded)
            {
                if (CodeText != null)
                {
                    FormattedText ft = new FormattedText(CodeText, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface, tbxCode.FontSize, Brushes.Black);
                    tbxCode.Document.PageWidth = ft.Width + 12;
                    tbxCode.HorizontalScrollBarVisibility = (tbxCode.ActualWidth < tbxCode.Document.PageWidth) ? ScrollBarVisibility.Visible : ScrollBarVisibility.Hidden;
                }
                else
                {
                    tbxCode.Document.PageWidth = this.ActualWidth - 20;
                    tbxCode.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
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
                if (InputCodeType == EditorCodeType.XML)
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

                    if (InputCodeType == EditorCodeType.XML)
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
                    Format();
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
                else if (e.Key == Key.F)
                    DisplayFindPane();

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
                else if (e.Key == Key.Right || e.Key == Key.Left)
                    popSuggestion.IsOpen = false;
                else if (e.Key == Key.F3)
                    SearchText();
                else if (e.Key == Key.Escape)
                {
                    if (popSuggestion.IsOpen)
                    {
                        popSuggestion.IsOpen = false;
                        e.Handled = true;
                    }
                    else if (borFind.Visibility == Visibility.Visible)
                    {
                        ExitSearch();
                        e.Handled = true;
                    }
                }
                else
                    CaptureInput(e.Key);
            }
        }

        private void CaptureInput(Key key)
        {
            string inputchar = null;

            if (InputCodeType != EditorCodeType.XML)
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

        public void Format()
        {
            if (InputCodeType == EditorCodeType.XML)
                XmlFormat();
            else
                FormatCode();

            CheckScrollBarVisibility();
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

        private void FormatCode()
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

                // skip space at begin of line for prevent extra gap
                if (curc == ' ' && precs == '\n')
                    continue;

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
                    if (curc == '\r')
                    {
                        ncode[j++] = '\n';
                        collect_comment = false;
                    }
                    else if (curc == '\n' || curc == '\0')
                    {
                        ncode[j++] = curc;
                        collect_comment = false;
                    }
                    else
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

                    if (precs != '\n' && precs != '\0')
                        ncode[j++] = '\n';

                    ncode[j++] = '}';

                    // check ';' for do while and struct for ';' display after '}'
                    if (nextcs != '\r' && nexc != ';')
                        ncode[j++] = '\n';

                    precs = ncode[j - 1];
                }
                else if (curc == '\r')
                {
                    SetPreWord(ref preword, ref word, ref k);
                    precs = '\n';
                    ncode[j++] = '\n';
                    // insert gap in begin of line
                    // for 'else' word
                    if (preword == "else")
                        keyword_seen = true;
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

                                    // end of functions
                                    // call class member with '.'
                                    if (nexc != ';' && nexc != '.')
                                    {
                                        ncode[j++] = '\n';
                                        InsertGap(ncode, ref j, startcolumn + 1);
                                        precs = '\n';
                                    }

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
            Lines = CodeText.Split('\n');
            TextChecking();
            Editing = false;

            //string error = CSharpParser.Compile(CodeText);

            //if (error != null)
            //    MessageBox.Show(error);
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
            int count = startcolumn * 4;

            for (int k = 0; k < count; k++)
                t[j++] = ' ';
        }

        private void TbxCode_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            CheckScrollBarVisibility();
        }

        private void TbxCode_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            SetLineNumber();
        }

        public void Save(string fullpath)
        {
            try
            {
                File.WriteAllText(fullpath, Text);
            }
            catch { }
        }

        public void Open(string fullpath)
        {
            try
            {
                if (File.Exists(fullpath))
                {
                    if (System.IO.Path.GetExtension(fullpath).ToLower() == "xml")
                        InputCodeType = EditorCodeType.XML;
                    else
                        InputCodeType = EditorCodeType.CSharp;

                    Text = File.ReadAllText(fullpath);
                }
            }
            catch { }
        }

        private void BtnClearError_Click(object sender, RoutedEventArgs e)
        {
            tbkError.Text = null;
            rowError.Height = new GridLength(0, GridUnitType.Auto);
        }

        private void SearchText(string text = null, bool casesensitive = false, bool single = false)
        {
            Editing = true;

            if (text == null)
                text = GetCurrentString();

            if (!casesensitive)
                text = text.ToLower();

            ClearSearchMark();

            for (int j = 0; j < tbxCode.Document.Blocks.Count; j++)
            {
                Paragraph item = tbxCode.Document.Blocks.ElementAt(j) as Paragraph;

                for (int i = 0; i < item.Inlines.Count; i++)
                {
                    int k = 0;
                    Run run = item.Inlines.ElementAt(i) as Run;

                    if (casesensitive)
                    {
                        while ((k = run.Text.IndexOf(text, k)) != -1)
                        {
                            if (single)
                            {
                                if (k > 0)
                                {
                                    if (Char.IsLetterOrDigit(run.Text[k - 1]))
                                    {
                                        k += text.Length;
                                        continue;
                                    }
                                }

                                if (k + text.Length < run.Text.Length - 1)
                                {
                                    if (Char.IsLetterOrDigit(run.Text[k + text.Length]))
                                    {
                                        k += text.Length;
                                        continue;
                                    }
                                }
                            }

                            TextPointer start = run.ContentStart.GetPositionAtOffset(k);
                            TextPointer end = start.GetPositionAtOffset(text.Length);
                            new TextRange(start, end).ApplyPropertyValue(TextElement.BackgroundProperty, FindMarkBrush);
                            k += text.Length;

                            if (k >= run.Text.Length)
                                break;
                        }
                    }
                    else
                    {
                        while ((k = run.Text.ToLower().IndexOf(text, k)) != -1)
                        {
                            if (single)
                            {
                                if (k > 0)
                                {
                                    if (Char.IsLetterOrDigit(run.Text[k - 1]))
                                    {
                                        k += text.Length;
                                        continue;
                                    }
                                }

                                if (k + text.Length < run.Text.Length - 1)
                                {
                                    if (Char.IsLetterOrDigit(run.Text[k + text.Length]))
                                    {
                                        k += text.Length;
                                        continue;
                                    }
                                }
                            }

                            TextPointer start = run.ContentStart.GetPositionAtOffset(k);
                            TextPointer end = start.GetPositionAtOffset(text.Length);
                            new TextRange(start, end).ApplyPropertyValue(TextElement.BackgroundProperty, FindMarkBrush);
                            k += text.Length;

                            if (k >= run.Text.Length)
                                break;
                        }
                    }
                }
            }

            Editing = false;
        }

        public int TextReplace()
        {
            return ReplaceText(tbxFind.Text, tbxReplace.Text);
        }

        private int ReplaceText(string text, string replace)
        {
            int matches = 0;
            // call this function for recrate runs
            ClearSearchMark();

            for (int j = 0; j < tbxCode.Document.Blocks.Count; j++)
            {
                Paragraph item = tbxCode.Document.Blocks.ElementAt(j) as Paragraph;

                for (int i = 0; i < item.Inlines.Count; i++)
                {
                    int k = 0;
                    Run run = item.Inlines.ElementAt(i) as Run;

                    while ((k = run.Text.IndexOf(text, k)) != -1)
                    {
                        TextPointer start = run.ContentStart.GetPositionAtOffset(k);
                        TextPointer end = start.GetPositionAtOffset(text.Length);
                        new TextRange(start, end).Text = replace;
                        k += text.Length;
                        matches++;

                        if (k >= run.Text.Length)
                            break;
                    }
                }
            }

            return matches;
        }

        private string GetCurrentString()
        {
            TextPointer start = tbxCode.CaretPosition;
            string text = start.GetTextInRun(LogicalDirection.Backward)?.Split(FindDelimiters).LastOrDefault();
            text += start.GetTextInRun(LogicalDirection.Forward)?.Split(FindDelimiters).FirstOrDefault();

            return text;
        }

        private void ClearSearchMark()
        {
            new TextRange(tbxCode.Document.ContentStart, tbxCode.Document.ContentEnd).ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Transparent);
        }

        public void DisplayFindPane()
        {
            borFind.Visibility = Visibility.Visible;

            if (string.IsNullOrEmpty(tbxFind.Text))
                tbxFind.Text = GetCurrentString();
        }

        private void btnFind_Click(object sender, RoutedEventArgs e)
        {
            FindText();
        }

        private void FindText()
        {
            if (!string.IsNullOrWhiteSpace(tbxFind.Text))
                SearchText(tbxFind.Text, btnCaseSensitive.IsChecked == true, btnSingleWord.IsChecked == true);
        }

        private void btnReplace_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(tbxFind.Text))
                RequestToReplaceText?.Invoke(this);
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            ExitSearch();
        }

        private void ExitSearch()
        {
            ClearSearchMark();
            borFind.Visibility = Visibility.Collapsed;
        }

        private void tbxFind_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                FindText();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                ExitSearch();
                e.Handled = true;
            }
        }
    }
}
