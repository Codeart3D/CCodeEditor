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
        private bool Lock = false;
        private bool InitOnce = false;
        private bool Editing = false;
        private bool UndoAction = false;
        private bool Formated = false;
        private bool Checking = false;
        private bool SelectAllFlag = false;
        private bool StartFormatCode = false;
        private bool CaseSensitive = false;
        private bool SingleWord = false;
        private bool MultiLineStarting = false;
        private bool MultiLineSelector = false;
        private bool MultiLineDown = false; // down or up direction
        private bool MultiLineDec = false;
        private bool TextCheckingEnable = true;
        private bool SetLineNumberEnable = true;
        private bool FormatImmediately = false;
        private bool IsScrolling = false;
        private int CtrlHomeCounter = 0;
        private int TagCounter = 0;
        private int MultiLineCount = 0;
        private int MultiLinePreLen = 0;
        private int CaretPosLineFirstLen = 0;
        private int StartLineNumber = 0;
        private int LastLineNumber = 0;
        private int VisibleLineCount = 0;
        private double MultiLineHeight = 15.223333333333335;
        private string FilterWord = "";
        private string FindWord = null;
        private Key InputKey = Key.None;
        private Typeface Typeface;
        private Keyword CurrentKeyword;
        private Point MultiLineStart = new Point();
        private TextPointer StartWord;
        private TextPointer EndWord;
        private List<string> TagNames;
        private List<string> MultiLineFirstState = new List<string>();
        private List<Keyword> AttribList;
        private List<LType> Types = new List<LType>();
        private List<Keyword> Keywords = new List<Keyword>();
        private ScrollViewer VerticalScroll;
        private string CurrentTag = null;
        private string LinesBefore;
        private string LinesAfter;
        private string[] Lines;
        private string CodeText;
        private string CurrentSuggestion;
        private const int MAX_LINEBLOCK = 100;
        private TextBlock[] textBlocks = new TextBlock[MAX_LINEBLOCK];
        private char[] Delimiters;
        private char[] FindDelimiters = new char[] { ' ', '<', '>', '{', '}', '[', ']', '(', ')', ',', '.' };
        public static char[] CodeDelimiters = new char[] { ' ', '\0', '(', ')', '.', '=', '+', '-', '*', '/', '>', '<', '&', '|', '{', '}', '"', ',', ';', '#' };
        private char[] XmlDelimiters = new char[] { ' ', '\0', '=' };

        private Stack<UndoRedoCode> UndoStack = new Stack<UndoRedoCode>();
        private Stack<UndoRedoCode> RedoStack = new Stack<UndoRedoCode>();

        private DispatcherTimer Timer; // set line numbers and code color and ...
        private DispatcherTimer CodeTimer = null; // for format code
        private DispatcherTimer CaretPosTimer = null; // for format code

        private static SolidColorBrush XMLOpenCloseTagColor = Brushes.White;
        private static SolidColorBrush XMLTagColor = new SolidColorBrush(Color.FromRgb(75, 183, 134));
        private static SolidColorBrush FindMarkBrush = new SolidColorBrush(Color.FromRgb(40, 100, 40));
        private static SolidColorBrush MainKeywordBrush = new SolidColorBrush(Color.FromRgb(65, 170, 220));
        private static SolidColorBrush LineNumberColor = new SolidColorBrush(Color.FromRgb(140, 140, 140));
        private static SolidColorBrush SelectedLineNumberColor = Brushes.White;
        public static SolidColorBrush EnumColor = new SolidColorBrush(Color.FromRgb(190, 230, 150));
        private static SolidColorBrush VariableColor = new SolidColorBrush(Color.FromRgb(130, 130, 130));
        private static SolidColorBrush StringColor = new SolidColorBrush(Color.FromRgb(230, 160, 120));
        // 230, 200, 150 cream gold

        public List<Keyword> SubSuggestions;

        public Visibility DisplayErrorSection { get { return tbkError.Visibility; } set { tbkError.Visibility = value; } }
        public string Error { get { return tbkError.Text; } set { tbkError.Text = value; } }
        public bool CheckXmlError { get; set; }
        public bool IsEnableCodeFormatter { get; set; } = false;
        public bool UndoRedoShortcutKey { get; set; } = true;

        // Event
        public delegate void XMLChangedHandler(object sender, string Xml);
        public event XMLChangedHandler XmlChanged;

        public delegate void CodeEditorTextReplace(CodeEditor editor);
        public static event CodeEditorTextReplace RequestToReplaceText;

        public delegate void RestoreSuggestionList(List<string> part);
        public event RestoreSuggestionList UpdateSubSuggestionList;

        public event TextChangedEventHandler TextChanged;

        public enum EditorCodeType
        {
            CODA,
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

                    if (value == null)
                        CodeText = "";

                    Lines = CodeText.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                    TextChecking();
                    SetLineNumber();
                    CheckScrollBarVisibility();

                    if (!string.IsNullOrEmpty(CodeText))
                        UndoStack.Push(new UndoRedoCode(CodeText, tbxCode.CaretPosition));

                    Editing = false;
                }
            }
        }

        public string Caption { get { return tbkCaption.Text; } set { tbkCaption.Text = value; } }
        public bool IsReadOnly
        {
            get { return Lock; }

            set
            {
                Lock = value;
                tbxCode.IsReadOnly = value;
                tbxCode.Opacity = value ? 0.6 : 1.0;
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

        public void Reset()
        {
            Clear();

            UndoStack.Clear();
            RedoStack.Clear();
        }

        public CodeEditor()
        {
            InitializeComponent();
            UndoStack.Push(new UndoRedoCode("", tbxCode.CaretPosition));
            Typeface = new Typeface(tbxCode.FontFamily, tbxCode.FontStyle, tbxCode.FontWeight, tbxCode.FontStretch);

            for (int i = 0; i < 100; i++)
            {
                textBlocks[i] = new TextBlock() { Margin = new Thickness(0, 0, 0, 0), TextAlignment = TextAlignment.Right, FontSize = 13, Foreground = LineNumberColor };
                griLine.Children.Add(textBlocks[i]);
            }

            DataObject.AddPastingHandler(tbxCode, OnPaste);
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!InitOnce)
            {
                InitOnce = true;

                VerticalScroll = GetScrollViewer(tbxCode);

                if (IsEnableCodeFormatter)
                {
                    if (CodeType == EditorCodeType.XML)
                    {
                        CodeTimer = new DispatcherTimer();
                        CodeTimer.Interval = new TimeSpan(0, 0, 5);
                        CodeTimer.Tick += CodeTimer_Tick;
                    }

                    CaretPosTimer = new DispatcherTimer();
                    CaretPosTimer.Interval = new TimeSpan(0, 0, 0, 0, 50);
                    CaretPosTimer.Tick += CaretPosTimer_Tick;
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
                Timer.Interval = new TimeSpan(0, 0, 0, 0, 50);
                Timer.Tick += Timer_Tick;

                lstKeyword.ItemsSource = Keywords.Where(p => p.Visible);
            }

            tbxCode.Focus();
            CheckScrollBarVisibility();
        }

        private void CaretPosTimer_Tick(object sender, EventArgs e)
        {
            CaretPosTimer.Stop();

            int sub = TextUtils.GetLineText(tbxCode).Length - CaretPosLineFirstLen;

            if (sub > 0)
            {
                if (InputKey == Key.Oem1)
                    sub++;

                for (int i = 0; i < sub; i++)
                {
                    TextPointer tp = tbxCode.CaretPosition.GetNextInsertionPosition(LogicalDirection.Forward);

                    if (tp != null)
                        tbxCode.CaretPosition = tp;
                }
            }

            InputKey = Key.None;
            //else
            //{
            //    TextPointer tp = TextUtils.GetEndOfCurrentLine(tbxCode.CaretPosition);

            //    if (tp != null)
            //        tbxCode.CaretPosition = tp;

            //    //    sub = -sub;

            //    //    for (int i = 0; i < sub; i++)
            //    //        tbxCode.CaretPosition = tbxCode.CaretPosition.GetNextInsertionPosition(LogicalDirection.Forward);
            //}
        }

        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            // check is text
            if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true))
                return;

            //var text = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string;
            //StartFormatCode = true;
        }

        public EditorCodeType CodeType
        {
            get { return InputCodeType; }

            set
            {
                InputCodeType = value;
                Keywords.Clear();

                if (value == EditorCodeType.CODA)
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
                    Keywords.Add(new Keyword(MainKeywordBrush, "new"));
                }
                else if (value == EditorCodeType.Shader)
                {
                    Delimiters = CodeDelimiters;

                    Keywords.Add(new Keyword(MainKeywordBrush, "void"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "int"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "float"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "bool"));

                    Keywords.Add(new Keyword(MainKeywordBrush, "if"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "else"));

                    Keywords.Add(new Keyword(MainKeywordBrush, "for"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "while"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "do"));

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

                    Keywords.Add(new Keyword(MainKeywordBrush, "const"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "uniform"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "varying"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "attribute"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "lowp"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "mediump"));
                    Keywords.Add(new Keyword(MainKeywordBrush, "highp"));

                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "vec2"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "vec3"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "vec4"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "ivec2"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "ivec3"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "ivec4"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "bvec2"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "bvec3"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "bvec4"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "mat2"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "mat3"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "mat4"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "sampler2D"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "samplerCube"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "gl_Position"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "gl_PointSize"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "gl_FragColor"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "gl_FragCoord"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "gl_FrontFacing"));
                    Keywords.Add(new Keyword(Brushes.LightSeaGreen, "gl_PointCoord"));

                    Keywords.Add(new Keyword(Brushes.HotPink, "abs"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "sin"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "cos"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "tan"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "asin"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "acos"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "atan"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "pow"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "exp"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "log"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "sqrt"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "inversesqrt"));

                    Keywords.Add(new Keyword(Brushes.HotPink, "min"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "max"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "clamp"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "mix"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "step"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "smoothstep"));

                    Keywords.Add(new Keyword(Brushes.HotPink, "length"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "distance"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "dot"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "cross"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "normalize"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "faceforward"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "reflect"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "refract"));

                    Keywords.Add(new Keyword(Brushes.HotPink, "matrixCompMult"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "lessThan"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "lessThanEqual"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "greaterThan"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "greaterThanEqual"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "equal"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "notEqual"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "any"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "all"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "not"));

                    Keywords.Add(new Keyword(Brushes.HotPink, "texture2D"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "textureSize"));

                    Keywords.Add(new Keyword(Brushes.HotPink, "#define"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "PIXEL_SHADER"));
                    Keywords.Add(new Keyword(Brushes.HotPink, "VERTEX_SHADER"));
                }
                else if (value == EditorCodeType.XML)
                {
                    Delimiters = XmlDelimiters;

                    Keywords.Add(new Keyword(XMLOpenCloseTagColor, "<", KeywordType.XMLStart, null, false));
                    Keywords.Add(new Keyword(XMLOpenCloseTagColor, "/>", KeywordType.XMLEnd, null, false));
                    Keywords.Add(new Keyword(XMLOpenCloseTagColor, ">", KeywordType.XMLEnd, null, false));
                    Keywords.Add(new Keyword(Brushes.Gray, "=", KeywordType.XMLEqual, "=\"\"", false, 1));


                    if (Debugger.IsAttached)
                    {
                        SetXmlRoot(new KeywordClass("Screen", null, null));

                        List<string> basep = new List<string>();
                        basep.Add("X");
                        basep.Add("Y");
                        basep.Add("Width");
                        basep.Add("Height");
                        basep.Add("Name");

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

        public void ClearLocalVariableKeywords()
        {
            Keywords.RemoveAll(p => p.Type == KeywordType.LocalVariable);
        }

        public void AddCSharpLocalVariableKeyword(string VariableKey)
        {
            Keywords.Add(new Keyword(VariableColor, VariableKey, KeywordType.LocalVariable));
        }

        public void SetXmlRoot(KeywordClass root)
        {
            Keywords.Add(new Keyword(XMLTagColor, $"<{root.Name}>", KeywordType.XMLRootTag) { KeyName = root.Name, Suggestions = root.Properties, BaseSuggestions = root.BaseProperties });
            Keywords.Add(new Keyword(XMLTagColor, $"</{root.Name}>", KeywordType.XMLEndTag));
            Keywords.Add(new Keyword(XMLTagColor, $"<{root.Name}", KeywordType.XMLRootTag, null, false));
        }

        public void SetXmlClasses(List<KeywordClass> classes)
        {
            foreach (var item in classes)
            {
                Keywords.Add(new Keyword(XMLTagColor, $"<{item.Name}/>", KeywordType.XMLTag, null, true, 2) { KeyName = item.Name, Suggestions = item.Properties, BaseSuggestions = item.BaseProperties });
                Keywords.Add(new Keyword(XMLTagColor, $"<{item.Name}", KeywordType.XMLTag, null, false));
            }
        }

        public void SetXmlClassWithChild(KeywordClass cclass)
        {
            Keywords.Add(new Keyword(XMLTagColor, $"<{cclass.Name}>", KeywordType.XMLTag, null, true, 1)
            { KeyName = cclass.Name, Suggestions = cclass.Properties, BaseSuggestions = cclass.BaseProperties, InsertAfter = $"</{cclass.Name}>" });
            Keywords.Add(new Keyword(XMLTagColor, $"</{cclass.Name}>", KeywordType.XMLEndTag, null, true));
            Keywords.Add(new Keyword(XMLTagColor, $"<{cclass.Name}", KeywordType.XMLTag, null, false));
        }

        public List<Keyword> GetXmlAttrib(List<string> attribs)
        {
            List<Keyword> atts = new List<Keyword>();

            foreach (var item in attribs)
                atts.Add(new Keyword(XMLTagColor, item, KeywordType.XMLAttrib, $"{item}=\"\"", true, 1));

            return atts;
        }

        private void CodeTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                Timer.Stop();
                CodeTimer.Stop();

                if (InputCodeType == EditorCodeType.XML)
                    XmlFormat();
                else if (InputCodeType == EditorCodeType.CODA)
                    FormatCode();

                CheckScrollBarVisibility();
            }
            catch { }
        }

        private void CheckAndFormat()
        {
            Editing = true;

            if (TextCheckingEnable)
                TextChecking();

            if (SetLineNumberEnable)
                SetLineNumber();

            // this methode must call after TextChecking because find childs indexes
            if (InputCodeType == EditorCodeType.XML && CheckXmlError)
                XmlErrorChecking();

            if (IsEnableCodeFormatter)
            {
                if (CodeType == EditorCodeType.XML)
                {
                    CodeTimer.Stop();
                    CodeTimer.Start();
                }
                else if (StartFormatCode)
                {
                    StartFormatCode = false;
                    Format(); // use Format for scroll checking
                }
            }

            if (MultiLineDec)
                CheckScrollBarVisibility();

            TextCheckingEnable = true;
            SetLineNumberEnable = true;
            Editing = false;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            Timer.Stop();
            CheckAndFormat();
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

            FindAllWords();
        }

        ScrollViewer GetScrollViewer(DependencyObject depObj)
        {
            if (depObj is ScrollViewer) return (ScrollViewer)depObj;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }

            return null;
        }


        private void SetLineNumber()
        {
            //int i = 0;
            //int num = 0;
            //StartLineNumber = -1;
            //double height = tbxCode.ActualHeight + 20;

            //foreach (var item in tbxCode.Document.Blocks)
            //{
            //    num++;
            //    Rect rect = item.ContentStart.GetCharacterRect(LogicalDirection.Forward);

            //    if (rect.Top > -20 && rect.Top < height)
            //    {
            //        if (StartLineNumber == -1)
            //            StartLineNumber = num - 1;

            //        textBlocks[i].Text = num.ToString();
            //        textBlocks[i].Visibility = Visibility.Visible;
            //        textBlocks[i].Margin = new Thickness(0, rect.Top, 0, 0);

            //        if (rect.Top < borHighlight.Margin.Top || rect.Bottom > borHighlight.Margin.Top + borHighlight.ActualHeight)
            //            textBlocks[i].Foreground = LineNumberColor;
            //        else
            //            textBlocks[i].Foreground = SelectedLineNumberColor;

            //        i++;
            //    }
            //    else if (rect.Top > height)
            //        break;
            //}

            //for (; i < 100; i++)
            //    textBlocks[i].Visibility = Visibility.Collapsed;

            //if (StartLineNumber == -1)
            //    StartLineNumber = 0;

            //VisibleLineCount = num - StartLineNumber;


            if (VerticalScroll != null)
            {
                string lineno = "";
                double fl = VerticalScroll.VerticalOffset / MultiLineHeight;
                StartLineNumber = (int)fl + 1;
                LastLineNumber = StartLineNumber + (int)(tbxCode.ActualHeight / MultiLineHeight);

                for (int i = StartLineNumber; i < LastLineNumber; i++)
                    lineno += i + "\n";

                StartLineNumber--;
                LastLineNumber--;
                tbkLineNumber.Margin = new Thickness(10.0, ((int)fl - fl) * MultiLineHeight, 0.0, 0.0);
                tbkLineNumber.Text = lineno;
            }
        }

        private string GetBeforeVisibleText()
        {
            TextPointer firstLine = tbxCode.Document.ContentStart.GetNextInsertionPosition(LogicalDirection.Forward);
            TextPointer startPointer = firstLine?.GetLineStartPosition(0);
            TextPointer endPointer = startPointer?.GetLineStartPosition(StartLineNumber);

            if (startPointer != null && endPointer != null)
                return new TextRange(startPointer, endPointer).Text;

            return "";
        }

        private string GetAfterVisibleText()
        {
            TextPointer firstLine = tbxCode.Document.ContentStart.GetNextInsertionPosition(LogicalDirection.Forward);
            TextPointer startPointer = firstLine?.GetLineStartPosition(LastLineNumber);
            TextPointer endPointer = tbxCode.Document.ContentEnd;

            if (startPointer != null && endPointer != null)
                return new TextRange(startPointer, endPointer).Text;

            return "";
        }

        private string GetVisibleText()
        {
            TextPointer firstLine = tbxCode.Document.ContentStart.GetNextInsertionPosition(LogicalDirection.Forward);
            TextPointer startPointer = firstLine?.GetLineStartPosition(StartLineNumber);
            TextPointer endPointer = startPointer?.GetLineStartPosition(VisibleLineCount);

            if (startPointer != null && endPointer != null)
                return new TextRange(startPointer, endPointer).Text;

            return "";
        }

        void FindLineNumbersRange()
        {
            if (VerticalScroll != null)
            {
                StartLineNumber = (int)(VerticalScroll.VerticalOffset / MultiLineHeight); // start visible line number
                VisibleLineCount = (int)(tbxCode.ActualHeight / MultiLineHeight);
                LastLineNumber = StartLineNumber + VisibleLineCount;
            }
            else
            {
                StartLineNumber = 0;
                VisibleLineCount = Lines.Length;
                LastLineNumber = Lines.Length;
            }
        }

        private void UpdateLines()
        {
            FindLineNumbersRange();

            LinesBefore = GetBeforeVisibleText();
            LinesAfter = GetAfterVisibleText();
            Lines = GetVisibleText().Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            if (LinesBefore.EndsWith("\r\n"))
                LinesBefore = LinesBefore.Substring(0, LinesBefore.Length - 2);

            if (LinesAfter.EndsWith("\r\n"))
                LinesAfter = LinesAfter.Substring(0, LinesAfter.Length - 2);

            if (Lines.Length > 0)
            {
                int ll = Lines.Length - 1;

                if (Lines[ll].EndsWith("\r\n"))
                    Lines[ll] = Lines[ll].Substring(0, Lines[ll].Length - 2);
            }
        }

        private void tbxCode_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!Editing)
            {
                CodeText = new TextRange(tbxCode.Document.ContentStart, tbxCode.Document.ContentEnd).Text;

                if (MultiLineSelector)
                    InsertToMultipleLine();

                if (!MultiLineDec)
                    CheckScrollBarVisibility();

                // StartChecking
                UndoAction = false;

                //UpdateLines();
                Lines = CodeText.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                if (!string.IsNullOrEmpty(CodeText))
                    UndoStack.Push(new UndoRedoCode(CodeText, tbxCode.CaretPosition));

                Timer.Stop();

                if (FormatImmediately)
                {
                    FormatImmediately = false;
                    CheckAndFormat();
                }
                else
                    Timer.Start();

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
                    double w = this.ActualWidth - 20;

                    if (w > 0)
                        tbxCode.Document.PageWidth = w;
                    else
                        tbxCode.Document.PageWidth = 100;

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
            if (!popSuggestion.IsOpen && !Lock)
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
            //bool collect_string = false;
            char[] word = new char[1024];

            // collect color
            Run RunColor = null;
            int ccounter = 0;
            bool collect_color = false;
            char[] ColorValue = new char[8] { '0', '0', '0', '0', '0', '0', '0', '0' };

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
                    if (c == '#')
                    {
                        if (k > 0)
                        {
                            paragraph.Inlines.Add(new Run(new string(word, 0, k)));
                            k = 0;
                        }

                        collect_color = true;
                        RunColor = new Run("#");
                        paragraph.Inlines.Add(RunColor);
                        continue;
                    }
                    else if (collect_color)
                    {
                        if (RunColor != null && ccounter < 8)
                        {
                            ColorValue[ccounter++] = c;

                            try
                            {
                                string colorstr = new string(ColorValue);
                                Color color = (Color)ColorConverter.ConvertFromString("#" + colorstr);
                                uint ucolor = 0xFFFFFFu ^ ((uint)(color.R << 16 | color.G << 8 | color.B));
                                RunColor.Background = new SolidColorBrush(color);
                                RunColor.Foreground = new SolidColorBrush(Color.FromArgb(255, (byte)((ucolor >> 16) & 0xff), (byte)((ucolor >> 8) & 0xff), (byte)((ucolor) & 0xff)));
                            }
                            catch { }
                        }
                        else
                        {
                            ccounter = 0;
                            RunColor = null;
                            collect_color = false;
                            ColorValue[0] = '0';
                            ColorValue[1] = '0';
                            ColorValue[2] = '0';
                            ColorValue[3] = '0';
                            ColorValue[4] = '0';
                            ColorValue[5] = '0';
                            ColorValue[6] = '0';
                            ColorValue[7] = '0';
                        }
                    }

                    word[k] = c;
                    k++;

                    if (nj == Lines[index].Length)
                    {
                        //if (collect_string)
                        //    paragraph.Inlines.Add(new Run(new string(word, 0, k)) { Foreground = Brushes.LightSalmon });
                        //else
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
                    //if (c == '"')
                    //{
                    //    // check string
                    //    if (collect_string == false)
                    //    {
                    //        collect_string = true;

                    //        CheckKeywordInLine(false, c, new string(word, 0, k), paragraph);
                    //        word[0] = '"';
                    //        k = 1;

                    //        if (nj == Lines[index].Length)
                    //            paragraph.Inlines.Add(new Run(new string(word, 0, k)) { Foreground = Brushes.LightSalmon });
                    //    }
                    //    else
                    //    {
                    //        collect_string = false;
                    //        word[k] = c;
                    //        k++;
                    //        paragraph.Inlines.Add(new Run(new string(word, 0, k)) { Foreground = Brushes.LightSalmon });
                    //        k = 0;
                    //    }
                    //}
                    //else if (collect_string == true)
                    //{
                    //    word[k] = c;
                    //    k++;

                    //    if (nj == Lines[index].Length)
                    //        paragraph.Inlines.Add(new Run(new string(word, 0, k)) { Foreground = Brushes.LightSalmon });
                    //}
                    //else
                    //{
                    CheckKeywordInLine(sign, c, new string(word, 0, k), paragraph);
                    k = 0;
                    //}
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

            //TextPointer start = tbxCode.Document.ContentStart;
            //int caretOffset = start.GetOffsetToPosition(tbxCode.CaretPosition);

            // collect color
            Run RunColor = null;
            int ccounter = 0;
            bool collect_color = false;
            char[] word = new char[1024];
            char[] ColorValue = new char[8] { '0', '0', '0', '0', '0', '0', '0', '0' };

            tbxCode.Document.Blocks.Clear();

            //if (LinesBefore != null && LinesBefore.Length > 0)
            //{
            //    foreach (var lb in LinesBefore)
            //    {
            //        Paragraph parag = new Paragraph();
            //        parag.Inlines.Add(new Run(lb));
            //        tbxCode.Document.Blocks.Add(parag);
            //    }
            //}

            Paragraph paragraph;

            //if (StartLineNumber > 0)
            //{
            //    parag = new Paragraph();
            //    parag.Inlines.Add(new Run(LinesBefore));
            //    tbxCode.Document.Blocks.Add(parag);
            //}
            FindLineNumbersRange();

            if (string.IsNullOrEmpty(Lines.Last()))
                lcount = Lines.Length - 1;
            else
                lcount = Lines.Length;

            if (LastLineNumber > lcount)
                LastLineNumber = lcount;

            if (StartLineNumber > 0)
            {
                paragraph = new Paragraph();
                int ll = StartLineNumber - 1;

                for (int i = 0; i < ll; i++)
                    paragraph.Inlines.Add(new Run(Lines[i] + Environment.NewLine));

                paragraph.Inlines.Add(new Run(Lines[ll]));
                tbxCode.Document.Blocks.Add(paragraph);
            }

            int lln = LastLineNumber - 1;
            paragraph = new Paragraph();

            for (int i = StartLineNumber; i < LastLineNumber; i++)
            {
                int k = 0;
                bool collect_string = false;
                bool collect_comment_slash = false;
                Lines[i] = Lines[i].Replace("\t", String.Empty);

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

                        if (collect_color)
                        {
                            if (RunColor != null && ccounter < 8)
                            {
                                ColorValue[ccounter++] = c;

                                try
                                {
                                    string colorstr = new string(ColorValue);
                                    Color color = (Color)ColorConverter.ConvertFromString("#" + colorstr);
                                    uint ucolor = 0xFFFFFFu ^ ((uint)(color.R << 16 | color.G << 8 | color.B));
                                    RunColor.Background = new SolidColorBrush(color);
                                    RunColor.Foreground = new SolidColorBrush(Color.FromArgb(255, (byte)((ucolor >> 16) & 0xff), (byte)((ucolor >> 8) & 0xff), (byte)((ucolor) & 0xff)));
                                }
                                catch { }
                            }
                            else
                            {
                                ccounter = 0;
                                RunColor = null;
                                collect_color = false;
                                ColorValue[0] = '0';
                                ColorValue[1] = '0';
                                ColorValue[2] = '0';
                                ColorValue[3] = '0';
                                ColorValue[4] = '0';
                                ColorValue[5] = '0';
                                ColorValue[6] = '0';
                                ColorValue[7] = '0';
                            }
                        }

                        if (nj == Lines[i].Length)
                        {
                            if (collect_string)
                                paragraph.Inlines.Add(new Run(new string(word, 0, k)) { Foreground = StringColor });
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
                                    paragraph.Inlines.Add(new Run(new string(word, 0, k)) { Foreground = StringColor });
                            }
                            else
                            {
                                collect_string = false;
                                word[k] = c;
                                k++;
                                paragraph.Inlines.Add(new Run(new string(word, 0, k)) { Foreground = StringColor });
                                k = 0;
                            }
                        }
                        else if (collect_string)
                        {
                            word[k] = c;
                            k++;

                            if (nj == Lines[i].Length)
                                paragraph.Inlines.Add(new Run(new string(word, 0, k)) { Foreground = StringColor });
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
                        else if (c == '#')
                        {
                            collect_color = true;
                            RunColor = new Run("#");
                            paragraph.Inlines.Add(RunColor);
                        }
                        else
                        {
                            CheckKeywordInLine(sign, c, new string(word, 0, k), paragraph);
                            k = 0;
                        }
                    }
                }

                if (i < lln)
                    paragraph.Inlines.Add(new Run(Environment.NewLine));
            }

            tbxCode.Document.Blocks.Add(paragraph);

            //if (!string.IsNullOrEmpty(LinesAfter))
            //{
            //    parag = new Paragraph();
            //    parag.Inlines.Add(new Run(LinesAfter));
            //    tbxCode.Document.Blocks.Add(parag);
            //}

            if (LastLineNumber < lcount)
            {
                paragraph = new Paragraph();
                int ll = lcount - 1;

                for (int i = LastLineNumber; i < ll; i++)
                    paragraph.Inlines.Add(new Run(Lines[i] + Environment.NewLine));

                if (Lines[ll] != "")
                    paragraph.Inlines.Add(new Run(Lines[ll]));

                tbxCode.Document.Blocks.Add(paragraph);
            }

            //if (LinesAfter != null && LinesAfter.Length > 0)
            //{
            //    foreach (var lb in LinesAfter)
            //    {
            //        Paragraph parag = new Paragraph();
            //        parag.Inlines.Add(new Run(lb));
            //        tbxCode.Document.Blocks.Add(parag);
            //    }
            //}

            var tp = tbxCode.GetPositionFromPoint(point, true);

            if (tp != null)
            {
                double verticalOffset = VerticalScroll.VerticalOffset;
                tbxCode.CaretPosition = tp;

                if (IsScrolling)
                {
                    IsScrolling = false;
                    VerticalScroll.ScrollToVerticalOffset(verticalOffset);
                }
            }

            //if (IsLoaded)
            //{
            //    TextPointer newCaretPos = tbxCode.Document.ContentStart.GetPositionAtOffset(caretOffset, LogicalDirection.Forward);

            //    if (newCaretPos != null)
            //    {
            //        double verticalOffset = VerticalScroll.VerticalOffset;
            //        tbxCode.CaretPosition = newCaretPos;

            //        if (IsScrolling)
            //        {
            //            IsScrolling = false;
            //            VerticalScroll.ScrollToVerticalOffset(verticalOffset);
            //        }
            //    }
            //}

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

        private Run CheckOpenCloseTag(string part, Keyword key, Paragraph paragraph)
        {
            Run run = new Run(part);
            run.Foreground = key.Color;

            if (part[0] == '<')
            {
                if (part[1] == '/')
                {
                    run.Text = "</";
                    run.Foreground = XMLOpenCloseTagColor;
                    paragraph.Inlines.Add(run);
                    part = part.Remove(0, 2);
                }
                else
                {
                    run.Text = "<";
                    run.Foreground = XMLOpenCloseTagColor;
                    paragraph.Inlines.Add(run);
                    part = part.Remove(0, 1);
                }
            }

            if (part.EndsWith("/>"))
            {
                int fidx = part.LastIndexOf('/');
                part = part.Remove(fidx, part.Length - fidx);
                run = new Run(part);
                run.Foreground = key.Color;
                paragraph.Inlines.Add(run);

                run = new Run("/>");
                run.Foreground = XMLOpenCloseTagColor;
            }
            else if (part.EndsWith(">"))
            {
                int fidx = part.LastIndexOf('>');
                part = part.Remove(fidx, part.Length - fidx);
                run = new Run(part);
                run.Foreground = key.Color;
                paragraph.Inlines.Add(run);

                run = new Run(">");
                run.Foreground = XMLOpenCloseTagColor;
            }
            else
            {
                run = new Run(part);
                run.Foreground = key.Color;
            }

            return run;
        }

        private void CheckKeywordInLine(bool sign, char c, string part, Paragraph paragraph)
        {
            if (!string.IsNullOrEmpty(part))
            {
                Run run = new Run(part);
                Keyword key = Keywords.Where(p => p.Key == part).FirstOrDefault();

                // Find Highlight
                //if (FindWord != null && part == FindWord)
                //    run.Background = FindMarkBrush;

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
                                        run.Foreground = key.Color;
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
                                else
                                    run = CheckOpenCloseTag(part, key, paragraph);

                                TagCounter++;
                            }
                            else
                                run = CheckOpenCloseTag(part, key, paragraph);
                        }
                        else if (key.Type == KeywordType.XMLRootTag || key.Type == KeywordType.XMLEndTag)
                        {
                            run = CheckOpenCloseTag(part, key, paragraph);
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

        private void TbxCode_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            ExitMultiLineSelector();
        }

        private void tbxCode_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            InputKey = e.Key;
            CtrlHomeCounter--;

            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                if (e.Key == Key.Z)
                {
                    if (UndoRedoShortcutKey)
                        Redo();
                }
                else if (e.Key == Key.Oem2)
                    Uncomment();
            }
            else if (Keyboard.Modifiers == (ModifierKeys.Alt | ModifierKeys.Shift))
            {
                if (e.SystemKey == Key.Down)
                {
                    InsertInMultipleLine(true);
                    e.Handled = true;
                }
                else if (e.SystemKey == Key.Up)
                {
                    InsertInMultipleLine(false);
                    e.Handled = true;
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
                    Format();
                    UndoStack.Push(new UndoRedoCode(CodeText, tbxCode.CaretPosition));
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
                else if (e.Key == Key.F)
                    DisplayFindPane();
                else if (e.Key == Key.Oem2)
                    Comment();
                else if (e.Key == Key.A)
                {
                    SelectAllFlag = true;
                    ExitMultiLineSelector();
                }

                popSuggestion.IsOpen = false;
                lstKeyword.Items.Filter = null;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                if (e.Key == Key.Delete)
                    TextUtils.DeleteCurrentLine(tbxCode);
                else if (e.Key == Key.Home)
                {
                    if (CtrlHomeCounter <= 0)
                    {
                        CtrlHomeCounter = 2;
                        TextUtils.SelectFromCaretToStartOfLine(tbxCode);
                        e.Handled = true;
                        return;
                    }
                }
                else if (e.Key == Key.OemOpenBrackets)
                {
                    // Open and close '{' '}' start formatting
                    TextCheckingEnable = false;
                    SetLineNumberEnable = false;
                    popSuggestion.IsOpen = false;
                    //FormatImmediately = true;
                    //StartFormatCode = true;
                }
                else if (e.Key == Key.OemCloseBrackets)
                {
                    // Open and close '{' '}' start formatting
                    TextCheckingEnable = false;
                    SetLineNumberEnable = false;
                    popSuggestion.IsOpen = false;
                    //FormatImmediately = true;
                    StartFormatCode = true;
                }
                else if (e.Key == Key.D0 || e.Key == Key.D9)
                {
                    popSuggestion.IsOpen = false;
                }
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

                    ExitMultiLineSelector();
                }
                else if (e.Key == Key.Down)
                {
                    if (popSuggestion.IsOpen)
                    {
                        if (lstKeyword.SelectedIndex < lstKeyword.Items.Count - 1)
                            lstKeyword.SelectedIndex++;

                        e.Handled = true;
                    }

                    ExitMultiLineSelector();
                }
                else if (e.Key == Key.Enter)
                {
                    e.Handled = SelectSuggestion();
                    TextCheckingEnable = false;
                    //FormatImmediately = true;
                    StartFormatCode = true;
                    ExitMultiLineSelector();
                }
                else if (e.Key == Key.Oem1)
                {
                    // Check ; character
                    popSuggestion.IsOpen = false;
                    StartFormatCode = true;
                }
                else if (e.Key == Key.Right || e.Key == Key.Left)
                {
                    popSuggestion.IsOpen = false;
                    ExitMultiLineSelector();
                }
                else if (e.Key == Key.F3)
                    SearchText();
                else if (e.Key == Key.Escape)
                {
                    if (popSuggestion.IsOpen)
                    {
                        popSuggestion.IsOpen = false;
                        e.Handled = true;
                    }
                    else if (FindWord != null)
                    {
                        ExitSearch();
                        e.Handled = true;
                    }

                    ExitMultiLineSelector();
                }
                else if (e.Key == Key.Home)
                {
                    TextUtils.GoAtTheBeginOfLine(tbxCode);
                    e.Handled = true;
                    ExitMultiLineSelector();
                }
                else if (e.Key == Key.End)
                {
                    if (popSuggestion.IsOpen)
                    {
                        popSuggestion.IsOpen = false;
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
                else if (key == Key.OemPeriod)
                {
                    inputchar = ".";
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

            FindSuggestion(inputchar, key == Key.Back);
        }

        private string FilterFromFunction(string sugg, string sugg2)
        {
            if (!string.IsNullOrEmpty(sugg))
            {
                // check for functions
                string[] par = sugg.Split('(');

                if (par != null && par.Length > 1)
                {
                    sugg = par[par.Length - 1];

                    // check for pre coma
                    string[] coma = sugg.Split(',');

                    if (coma != null && coma.Length > 0)
                        sugg = coma[coma.Length - 1];
                }
                else
                    return sugg2.TrimStart('(');

                return sugg;
            }

            if (!string.IsNullOrEmpty(sugg2))
            {
                string fff = FilterFromFunction(sugg2, "");

                if (!string.IsNullOrEmpty(fff))
                    return fff;
                else
                    return sugg2;
            }

            return "";
        }

        private void FindSuggestion(string inputchar, bool backspace)
        {
            try
            {
                string curline = TextUtils.GetCurrentLine(tbxCode) + inputchar;

                if (string.IsNullOrEmpty(curline))
                    return;

                //string[] splt = curline.Split('(');
                //curline = splt[splt.Length - 1];
                //string[] sections = curline.TrimStart(')').TrimStart('.').Split('.');

                //bool first = false;
                //Stack<string> spart = new Stack<string>();
                List<string> newpart = new List<string>();
                List<string> part = curline.Split(CodeDelimiters).ToList();

                //for (int i = part.Count - 1; i >= 0; i--)
                //{
                //    if (string.IsNullOrEmpty(part[i]))
                //    {
                //        if (first)
                //            break;
                //        else
                //            continue;
                //    }

                //    first = true;
                //    spart.Push(part[i]);
                //}

                //int spc = spart.Count;

                //for (int i = 0; i < spc; i++)
                //    newpart.Add(spart.Pop());

                for (int i = 0; i < part.Count; i++)
                {
                    if (!string.IsNullOrEmpty(part[i]))
                        newpart.Add(part[i]);
                }

                // find suggestion after .
                if (newpart.Count > 0)
                {
                    if (UpdateSubSuggestionList != null)
                    {
                        //CurrentSuggestion = FilterFromFunction(sections[sections.Length - 1], sections[sections.Length - 2]);

                        // get list of new keywords
                        UpdateSubSuggestionList.Invoke(newpart);// CurrentSuggestion, null);

                        if (SubSuggestions != null && SubSuggestions.Count > 0)
                        {
                            // update new keyword list
                            lstKeyword.Items.Filter = null;
                            lstKeyword.ItemsSource = null;
                            lstKeyword.ItemsSource = SubSuggestions;
                        }
                        else
                            ResetFilterSuggestions();
                    }
                    else
                        ResetFilterSuggestions();

                    if (inputchar != null)
                    {
                        if (!CodeDelimiters.Any(p => p == inputchar[0]))
                            FilterWord = newpart[newpart.Count - 1].Trim().ToLower();
                        else
                            FilterWord = null;
                    }
                    else
                        FilterWord = newpart[newpart.Count - 1].Trim().ToLower();
                }
                else
                {
                    ResetFilterSuggestions();
                    FilterWord = curline.Trim().ToLower();
                }

                //FilterWord = tbxCode.CaretPosition.GetTextInRun(LogicalDirection.Backward).Trim().ToLower() + inputchar;
                //FilterWord = FilterWord.TrimStart('.');

                if (backspace)
                {
                    if (string.IsNullOrEmpty(FilterWord))
                        popSuggestion.IsOpen = false;
                    else
                        FilterWord = FilterWord.Remove(FilterWord.Length - 1);
                }

                //if (!IsXML)
                //else
                if (!string.IsNullOrEmpty(FilterWord) && lstKeyword.Items != null && lstKeyword.Items.Count > 0)
                {
                    lstKeyword.Items.Filter = r =>
                    {
                        if (!string.IsNullOrEmpty((r as Keyword).Key))
                            return (r as Keyword).Key.ToLower().StartsWith(FilterWord);

                        return false;
                    };

                    if (lstKeyword.Items.Count == 0)
                    {
                        lstKeyword.Items.Filter = r =>
                        {
                            if (!string.IsNullOrEmpty((r as Keyword).Key))
                                return (r as Keyword).Key.ToLower().Contains(FilterWord);

                            return false;
                        };
                    }
                }
                else
                    lstKeyword.Items.Filter = null;

                if (lstKeyword.Items.Count > 0)
                {
                    int idx = -1;
                    lstKeyword.SelectedIndex = -1;

                    foreach (Keyword item in lstKeyword.Items)
                    {
                        idx++;

                        if (item.Key == FilterWord)
                        {
                            lstKeyword.SelectedIndex = idx;
                            break;
                        }
                    }

                    if (lstKeyword.SelectedIndex == -1)
                    {
                        idx = -1;

                        foreach (Keyword item in lstKeyword.Items)
                        {
                            idx++;

                            if (item.Key.StartsWith(FilterWord))
                            {
                                lstKeyword.SelectedIndex = idx;
                                break;
                            }
                        }
                    }

                    if (lstKeyword.SelectedIndex == -1)
                        lstKeyword.SelectedIndex = 0;
                }
                else
                    // filtered list is empty
                    popSuggestion.IsOpen = false;
            }
            catch { }
        }

        private void ResetFilterSuggestions()
        {
            lstKeyword.Items.Filter = null;
            lstKeyword.ItemsSource = null;
            lstKeyword.ItemsSource = Keywords.Where(p => p.Visible);
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

                    if (!string.IsNullOrEmpty(prec) && prec != "." && prec != "(")
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
                    UndoRedoCode urc = UndoStack.Pop();
                    RedoStack.Push(urc);

                    // pop again when undo action is false
                    if (!UndoAction && UndoStack.Count > 0)
                    {
                        urc = UndoStack.Pop();
                        RedoStack.Push(urc);
                    }

                    CodeText = urc.Code;
                    Lines = CodeText.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                    TextChecking();
                    SetLineNumber();
                    Editing = false;

                    if (CodeType == EditorCodeType.XML)
                        XmlChanged?.Invoke(this, CodeText);
                    else
                        TextChanged?.Invoke(this, null);

                    //tbxCode.CaretPosition = urc.CaretPosition;
                }

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

                UndoRedoCode urc = RedoStack.Pop();
                UndoStack.Push(urc);
                CodeText = urc.Code;

                Lines = CodeText.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                TextChecking();
                SetLineNumber();
                Editing = false;

                if (CodeType == EditorCodeType.XML)
                    XmlChanged?.Invoke(this, CodeText);
                else
                    TextChanged?.Invoke(this, null);

                //tbxCode.CaretPosition = urc.CaretPosition;
            }
        }

        public void ClearUndoRedo()
        {
            UndoStack.Clear();
            RedoStack.Clear();

            UndoStack.Push(new UndoRedoCode("", tbxCode.CaretPosition));
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

                        if (key.Suggestions != null)
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

        private void UpdateLineHighlight()
        {
            Rect rect = tbxCode.CaretPosition.GetCharacterRect(LogicalDirection.Forward);

            if (rect.Bottom > -50.0f && rect.Top < tbxCode.ActualHeight + 50.0f)
            {
                borHighlight.Margin = new Thickness(0, rect.Top, 0, 0);

                for (int i = 0; i < MAX_LINEBLOCK; i++)
                {
                    Thickness thick = textBlocks[i].Margin;

                    if (thick.Top < borHighlight.Margin.Top - 5.0 || thick.Top > borHighlight.Margin.Top + 5.0)
                        textBlocks[i].Foreground = LineNumberColor;
                    else
                        textBlocks[i].Foreground = SelectedLineNumberColor;
                }
            }
        }

        private void TbxCode_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (CodeType == EditorCodeType.XML)
                FindCurrentTag();

            if (!string.IsNullOrEmpty(tbxCode.Selection.Text) && !Editing)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control && !SelectAllFlag)
                {
                    Editing = true;
                    TextUtils.CorrectSelection(tbxCode);
                    Editing = false;
                }

                SelectAllFlag = false;
            }

            UpdateLineHighlight();
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

            if (InitOnce)
                CaretPosLineFirstLen = TextUtils.GetLineText(tbxCode).Length;

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

                // skip space at begin of line for prevent extra gap
                if (curc == ' ' && precs == '\n')
                    continue;

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

                    if (precs != '\n' && precs != '\0')
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

                        // insert space after a sign when it is a operator not a value sign +- 0.2f
                        if (CheckStandardSignWithExtra(curc) && CheckLetterNumber(nexc) && !string.IsNullOrEmpty(preword))
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

            if (InitOnce)
            {
                if (CaretPosTimer != null)
                {
                    CaretPosTimer.Stop();
                    CaretPosTimer.Start();
                }
            }

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
            if (IsLoaded && !Editing)
            {
                //Editing = true;
                SetLineNumber();
                //UpdateLines();
                //TextChecking();
                UpdateLineHighlight();

                if (Lines != null)
                {
                    IsScrolling = true;
                    Timer.Stop();
                    Timer.Start();
                }
                //Editing = false;
            }
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
                        InputCodeType = EditorCodeType.CODA;

                    Text = File.ReadAllText(fullpath);
                    TextChanged?.Invoke(null, null);
                }
            }
            catch { }
        }

        private void BtnClearError_Click(object sender, RoutedEventArgs e)
        {
            tbkError.Text = null;
            rowError.Height = new GridLength(0, GridUnitType.Auto);
        }

        private void FindAllWords()
        {
            if (!string.IsNullOrEmpty(FindWord))
            {
                string pattern;
                TextPointer pointer = tbxCode.Document.ContentStart;

                RegexOptions op = RegexOptions.Singleline;

                if (!CaseSensitive)
                    op |= RegexOptions.IgnoreCase;

                if (SingleWord)
                    pattern = @"\b" + FindWord + @"\b";
                else
                    pattern = FindWord;

                while (pointer != null)
                {
                    if (pointer.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                    {
                        string textRun = pointer.GetTextInRun(LogicalDirection.Forward);
                        MatchCollection matches = Regex.Matches(textRun, pattern, op);

                        foreach (Match match in matches)
                        {
                            int startIndex = match.Index;
                            int length = match.Length;
                            TextPointer start = pointer.GetPositionAtOffset(startIndex);
                            TextPointer end = start.GetPositionAtOffset(length);
                            new TextRange(start, end).ApplyPropertyValue(TextElement.BackgroundProperty, FindMarkBrush);
                        }
                    }

                    pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
                }
            }
        }

        private void SearchText(string text = null, bool casesensitive = false, bool single = false)
        {
            Editing = true;

            if (text == null)
                text = GetCurrentString();

            //if (!casesensitive)
            //    text = text.ToLower();

            SingleWord = single;
            CaseSensitive = casesensitive;
            FindWord = text;

            TextChecking();
            Editing = false;

            //ClearSearchMark();
            //Editing = true; // must set true again because claar search change it

            //for (int j = 0; j < tbxCode.Document.Blocks.Count; j++)
            //{
            //    Paragraph item = tbxCode.Document.Blocks.ElementAt(j) as Paragraph;

            //    for (int i = 0; i < item.Inlines.Count; i++)
            //    {
            //        int k = 0;
            //        Run run = item.Inlines.ElementAt(i) as Run;

            //        if (casesensitive)
            //        {
            //            while ((k = run.Text.IndexOf(text, k)) != -1)
            //            {
            //                if (single)
            //                {
            //                    if (k > 0)
            //                    {
            //                        if (Char.IsLetterOrDigit(run.Text[k - 1]))
            //                        {
            //                            k += text.Length;
            //                            continue;
            //                        }
            //                    }

            //                    if (k + text.Length < run.Text.Length - 1)
            //                    {
            //                        if (Char.IsLetterOrDigit(run.Text[k + text.Length]))
            //                        {
            //                            k += text.Length;
            //                            continue;
            //                        }
            //                    }
            //                }

            //                TextPointer start = run.ContentStart.GetPositionAtOffset(k);
            //                TextPointer end = start.GetPositionAtOffset(text.Length);
            //                new TextRange(start, end).ApplyPropertyValue(TextElement.BackgroundProperty, FindMarkBrush);
            //                k += text.Length;

            //                if (k >= run.Text.Length)
            //                    break;
            //            }
            //        }
            //        else
            //        {
            //            while ((k = run.Text.ToLower().IndexOf(text, k)) != -1)
            //            {
            //                if (single)
            //                {
            //                    if (k > 0)
            //                    {
            //                        if (Char.IsLetterOrDigit(run.Text[k - 1]))
            //                        {
            //                            k += text.Length;
            //                            continue;
            //                        }
            //                    }

            //                    if (k + text.Length < run.Text.Length - 1)
            //                    {
            //                        if (Char.IsLetterOrDigit(run.Text[k + text.Length]))
            //                        {
            //                            k += text.Length;
            //                            continue;
            //                        }
            //                    }
            //                }

            //                TextPointer start = run.ContentStart.GetPositionAtOffset(k);
            //                TextPointer end = start.GetPositionAtOffset(text.Length);
            //                new TextRange(start, end).ApplyPropertyValue(TextElement.BackgroundProperty, FindMarkBrush);
            //                k += text.Length;

            //                if (k >= run.Text.Length)
            //                    break;
            //            }
            //        }
            //    }
            //}

            //Editing = false;
        }

        public int TextReplace()
        {
            return ReplaceText(tbxFind.Text, tbxReplace.Text);
        }

        public int ReplaceText(string text, string replace, bool first = false)
        {
            int matchecnt = 0;

            try
            {
                if (!string.IsNullOrEmpty(text))
                {
                    string pattern;
                    TextPointer pointer = tbxCode.Document.ContentStart;

                    RegexOptions op = RegexOptions.Singleline;

                    if (!CaseSensitive)
                        op |= RegexOptions.IgnoreCase;

                    if (SingleWord)
                        pattern = @"\b" + text + @"\b";
                    else
                        pattern = text;

                    while (pointer != null)
                    {
                        if (pointer.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                        {
                            string textRun = pointer.GetTextInRun(LogicalDirection.Forward);
                            MatchCollection matches = Regex.Matches(textRun, pattern, op);

                            foreach (Match match in matches)
                            {
                                matchecnt++;
                                int startIndex = match.Index;
                                int length = match.Length;
                                TextPointer start = pointer.GetPositionAtOffset(startIndex);
                                TextPointer end = start.GetPositionAtOffset(length);
                                new TextRange(start, end).Text = replace;

                                if (first)
                                    return matchecnt;
                            }
                        }

                        pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            return matchecnt;
        }

        private string GetCurrentString()
        {
            if (!string.IsNullOrEmpty(tbxCode.Selection.Text))
                return tbxCode.Selection.Text;

            //TextPointer start = tbxCode.CaretPosition;
            //string text = start.GetTextInRun(LogicalDirection.Backward)?.Split(FindDelimiters).LastOrDefault();
            //text += start.GetTextInRun(LogicalDirection.Forward)?.Split(FindDelimiters).FirstOrDefault();

            return TextUtils.GetCurrentWord(tbxCode);
        }

        private void ClearSearchMark()
        {
            Editing = true;
            new TextRange(tbxCode.Document.ContentStart, tbxCode.Document.ContentEnd).ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Transparent);
            Editing = false;
        }

        public void DisplayFindPane()
        {
            borFind.Visibility = Visibility.Visible;

            string fstr = GetCurrentString();
            tbxFind.Focus();

            if (!string.IsNullOrEmpty(fstr))
            {
                tbxFind.Text = fstr;
                FindText();
            }
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
            FindWord = null;
            //ClearSearchMark();
            Editing = true;
            TextChecking();
            Editing = false;
            borFind.Visibility = Visibility.Collapsed;
            tbxCode.Focus();
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

        public void Comment()
        {
            if (CodeType == EditorCodeType.XML)
                CommentXML();
            else
                CommentCode();
        }

        public void Uncomment()
        {
            if (CodeType == EditorCodeType.XML)
                UncommentXML();
            else
                UncommentCode();
        }

        private void CommentXML()
        {

        }

        private void UncommentXML()
        {

        }

        private void CommentCode()
        {
            TextRange trange = new TextRange(tbxCode.Selection.Start, tbxCode.Selection.End);

            if (string.IsNullOrEmpty(trange?.Text))
                TextUtils.GetFirstOfCurrentLineWithoutSpace(tbxCode.CaretPosition).InsertTextInRun("//");
            else
                trange.Text = "/*" + trange.Text + "*/";
        }

        private void UncommentCode()
        {
            TextRange trange = new TextRange(tbxCode.Selection.Start, tbxCode.Selection.End);

            if (string.IsNullOrEmpty(trange?.Text))
                TextUtils.ReplaceInCurrentLine(tbxCode, "//", "");
            else
                trange.Text = trange.Text.Replace("/*", "").Replace("*/", "").Replace("//", "");
        }

        private void tbxCode_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            TextUtils.SelectCurrentWord(tbxCode);
        }

        private void TbxCode_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
                TextUtils.SelectCurrentWord(tbxCode);
        }

        private void ExitMultiLineSelector()
        {
            if (MultiLineSelector)
            {
                MultiLinePreLen = 0;
                MultiLineDec = false;
                MultiLineStarting = false;
                MultiLineSelector = false;
                linMultiLine.Visibility = Visibility.Collapsed;
            }
        }

        private void InsertInMultipleLine(bool down)
        {
            if (!MultiLineSelector)
            {
                MultiLineDown = down;
                //MultiLineStart = tbxCode.Document.ContentStart.GetOffsetToPosition(tbxCode.CaretPosition);
                MultiLineSelector = true;
                linMultiLine.Visibility = Visibility.Visible;
                Rect rect = tbxCode.CaretPosition.GetCharacterRect(LogicalDirection.Forward);
                MultiLineStart.X = rect.X;
                MultiLineStart.Y = rect.Y;

                linMultiLine.X1 = rect.X;
                linMultiLine.X2 = rect.X;
                linMultiLine.Y1 = rect.Y;

                if (down)
                    linMultiLine.Y2 = rect.Y + MultiLineHeight;
                else
                    linMultiLine.Y2 = rect.Y - MultiLineHeight;
            }
            else
            {
                if (down)
                    linMultiLine.Y2 += MultiLineHeight;
                else
                    linMultiLine.Y2 -= MultiLineHeight;
            }

            double suby = linMultiLine.Y2 - linMultiLine.Y1;
            MultiLineStarting = false;
            MultiLineDown = suby >= 0.0;
            MultiLineCount = (int)Math.Round(Math.Abs(suby) / MultiLineHeight);
        }

        private void InsertToMultipleLine()
        {
            Editing = true;

            TextPointer StartPoint = tbxCode.GetPositionFromPoint(MultiLineStart, true);

            if (StartPoint != null)
            {
                // Check caret before the start positon then must exit
                if (tbxCode.CaretPosition.CompareTo(StartPoint) < 0)
                {
                    ExitMultiLineSelector();
                    return;
                }

                // Init
                if (!MultiLineStarting)
                {
                    if (MultiLineDown)
                        MultiLineCount--;

                    if (MultiLineCount <= 0)
                    {
                        ExitMultiLineSelector();
                        return;
                    }

                    MultiLineStarting = true; // lock init again
                    MultiLineFirstState.Clear();

                    for (int i = 0; i < MultiLineCount; i++)
                        MultiLineFirstState.Add(null);
                }

                string insertion = new TextRange(StartPoint, tbxCode.CaretPosition)?.Text;
                int pos = new TextRange(tbxCode.Document.ContentStart, StartPoint).Text.Length;

                int nex = 0;
                int cur = CodeText.Take(pos).Count(c => c == '\n'); // find line
                List<string> lines = CodeText.Split(new string[] { Environment.NewLine }, StringSplitOptions.None).ToList();
                lines.RemoveAt(lines.Count - 1);

                MultiLineDec = MultiLinePreLen > insertion.Length;
                MultiLinePreLen = insertion.Length;

                if (lines.Count > 0)
                {
                    for (int i = 0; i < cur; i++)
                        pos -= lines[i].Length + 2; // add 2 for new lines character removed in split
                }

                for (int k = 0; k < MultiLineCount; k++)
                {
                    if (MultiLineDown)
                        nex = cur + k + 1; // next line 
                    else
                        nex = cur - (k + 1); // pre line 

                    // Add new lines and free space
                    if (nex >= lines.Count)
                    {
                        int sub = (nex - lines.Count) + 1;

                        for (int i = 0; i < sub; i++)
                        {
                            string str = "";

                            for (int j = 0; j < pos; j++)
                                str += " ";

                            lines.Add(str);
                        }

                        MultiLineFirstState[k] = lines[nex];
                    }
                    else if (lines[nex].Length < pos)
                    {
                        int space = pos - lines[nex].Length;

                        for (int j = 0; j < space; j++)
                            lines[nex] += " ";

                        MultiLineFirstState[k] = lines[nex];
                    }
                    else if (MultiLineFirstState[k] == null)
                        MultiLineFirstState[k] = lines[nex];

                    // Clearing, restore line to first state
                    lines[nex] = MultiLineFirstState[k];

                    // Insertion
                    if (lines[nex].Length == 0 || lines[nex].Length == pos)
                        lines[nex] += insertion;
                    else
                        lines[nex] = lines[nex].Insert(pos, insertion);
                }

                CodeText = "";

                foreach (var item in lines)
                    CodeText += item + Environment.NewLine;
            }

            Editing = false;
        }

        public void UpdateFirstLine(string str)
        {
            TextUtils.ChangeFirstLine(tbxCode, str);
        }
    }
}
