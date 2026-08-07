using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

public class FastSyntaxHighlighter
{
    private readonly RichTextBox _rtb;
    private readonly Dictionary<string, SolidColorBrush> _keywords;
    private readonly SolidColorBrush _commentBrush;
    private readonly SolidColorBrush _stringBrush;
    private readonly SolidColorBrush _defaultBrush;

    // C# keywords
    private static readonly HashSet<string> CSharpKeywords = new HashSet<string>
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this",
        "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
        "using", "virtual", "void", "volatile", "while"
    };

    // Contextual keywords (not always keywords, but commonly colored)
    private static readonly HashSet<string> ContextualKeywords = new HashSet<string>
    {
        "add", "alias", "ascending", "async", "await", "by", "descending", "dynamic", "equals",
        "from", "get", "global", "group", "into", "join", "let", "nameof", "on", "orderby",
        "partial", "remove", "select", "set", "value", "var", "when", "where", "yield"
    };

    public FastSyntaxHighlighter(RichTextBox richTextBox)
    {
        _rtb = richTextBox;

        // Initialize brushes
        _keywords = new Dictionary<string, SolidColorBrush>();
        _commentBrush = new SolidColorBrush(Colors.Green);
        _stringBrush = new SolidColorBrush(Colors.Brown);
        _defaultBrush = new SolidColorBrush(Colors.Black);

        // Set keyword colors
        SetKeywordColor(Colors.Blue);

        // Set contextual keywords to blue with slight variation
        foreach (var keyword in ContextualKeywords)
        {
            _keywords[keyword] = new SolidColorBrush(Color.FromRgb(0, 100, 200));
        }
    }

    public void SetKeywordColor(Color color)
    {
        var brush = new SolidColorBrush(color);
        foreach (var keyword in CSharpKeywords)
        {
            _keywords[keyword] = brush;
        }
    }

    public void SetCommentColor(Color color)
    {
        _commentBrush.Color = color;
    }

    public void SetStringColor(Color color)
    {
        _stringBrush.Color = color;
    }

    public void Highlight()
    {
        var document = _rtb.Document;
        if (document == null) return;

        try
        {
            // Disable UI updates for performance
            _rtb.IsEnabled = false;

            // Get all text
            TextRange fullRange = new TextRange(document.ContentStart, document.ContentEnd);
            string text = fullRange.Text;

            if (string.IsNullOrEmpty(text))
            {
                _rtb.IsEnabled = true;
                return;
            }

            // Apply syntax highlighting
            ApplySyntaxHighlighting(document, text);
        }
        finally
        {
            _rtb.IsEnabled = true;
        }
    }

    private void ApplySyntaxHighlighting(FlowDocument document, string text)
    {
        // Parse and highlight
        int position = 0;
        int length = text.Length;
        bool inString = false;
        bool inVerbatimString = false;
        bool inCharLiteral = false;
        bool inSingleLineComment = false;
        bool inMultiLineComment = false;
        char stringChar = '"';
        char charLiteral = '\'';

        while (position < length)
        {
            char currentChar = text[position];

            // Check for multi-line comment end
            if (inMultiLineComment && currentChar == '*' && position + 1 < length && text[position + 1] == '/')
            {
                ApplyStyle(document, position, position + 2, _commentBrush);
                position += 2;
                inMultiLineComment = false;
                continue;
            }

            // Check for single-line comment end
            if (inSingleLineComment && (currentChar == '\n' || currentChar == '\r'))
            {
                inSingleLineComment = false;
                position++;
                continue;
            }

            // Handle strings and characters
            if (!inSingleLineComment && !inMultiLineComment)
            {
                // Verbatim string start: @"
                if (!inString && !inCharLiteral && currentChar == '@' && position + 1 < length && text[position + 1] == '"')
                {
                    inVerbatimString = true;
                    inString = true;
                    stringChar = '"';
                    position++;
                    continue;
                }

                // Regular string start
                if (!inString && !inCharLiteral && currentChar == '"')
                {
                    inString = true;
                    stringChar = '"';
                    ApplyStyle(document, position, position + 1, _stringBrush);
                    position++;
                    continue;
                }

                // Character literal start
                if (!inString && !inCharLiteral && currentChar == '\'')
                {
                    inCharLiteral = true;
                    charLiteral = '\'';
                    ApplyStyle(document, position, position + 1, _stringBrush);
                    position++;
                    continue;
                }
            }

            // Handle inside strings
            if (inString)
            {
                if (inVerbatimString)
                {
                    // Verbatim string: escape double quotes by doubling them
                    if (currentChar == '"' && position + 1 < length && text[position + 1] == '"')
                    {
                        ApplyStyle(document, position, position + 2, _stringBrush);
                        position += 2;
                        continue;
                    }

                    // End of verbatim string
                    if (currentChar == '"')
                    {
                        ApplyStyle(document, position, position + 1, _stringBrush);
                        inString = false;
                        inVerbatimString = false;
                        position++;
                        continue;
                    }
                }
                else
                {
                    // Regular string: escape sequences
                    if (currentChar == '\\' && position + 1 < length)
                    {
                        ApplyStyle(document, position, position + 2, _stringBrush);
                        position += 2;
                        continue;
                    }

                    // End of string
                    if (currentChar == '"')
                    {
                        ApplyStyle(document, position, position + 1, _stringBrush);
                        inString = false;
                        position++;
                        continue;
                    }
                }

                // Content of string
                if (inString)
                {
                    ApplyStyle(document, position, position + 1, _stringBrush);
                    position++;
                    continue;
                }
            }

            // Handle character literals
            if (inCharLiteral)
            {
                if (currentChar == '\\' && position + 1 < length)
                {
                    ApplyStyle(document, position, position + 2, _stringBrush);
                    position += 2;
                    continue;
                }

                if (currentChar == '\'')
                {
                    ApplyStyle(document, position, position + 1, _stringBrush);
                    inCharLiteral = false;
                    position++;
                    continue;
                }

                ApplyStyle(document, position, position + 1, _stringBrush);
                position++;
                continue;
            }

            // Handle comments
            if (!inString && !inCharLiteral)
            {
                // Single-line comment: //
                if (!inSingleLineComment && !inMultiLineComment && currentChar == '/' && position + 1 < length && text[position + 1] == '/')
                {
                    inSingleLineComment = true;
                    ApplyStyle(document, position, position + 2, _commentBrush);
                    position += 2;
                    continue;
                }

                // Multi-line comment start: /*
                if (!inSingleLineComment && !inMultiLineComment && currentChar == '/' && position + 1 < length && text[position + 1] == '*')
                {
                    inMultiLineComment = true;
                    ApplyStyle(document, position, position + 2, _commentBrush);
                    position += 2;
                    continue;
                }
            }

            // Handle keywords and identifiers
            if (!inString && !inCharLiteral && !inSingleLineComment && !inMultiLineComment && char.IsLetter(currentChar))
            {
                // Extract the word
                int start = position;
                while (position < length && (char.IsLetterOrDigit(text[position]) || text[position] == '_'))
                {
                    position++;
                }

                string word = text.Substring(start, position - start);

                // Check if it's a keyword
                if (_keywords.ContainsKey(word))
                {
                    ApplyStyle(document, start, position, _keywords[word]);
                }
                else
                {
                    // Default color for identifiers
                    ApplyStyle(document, start, position, _defaultBrush);
                }

                continue;
            }

            // Default: apply default color
            if (!inSingleLineComment && !inMultiLineComment)
            {
                ApplyStyle(document, position, position + 1, _defaultBrush);
            }
            else
            {
                // Comment content
                ApplyStyle(document, position, position + 1, _commentBrush);
            }

            position++;
        }
    }

    private void ApplyStyle(FlowDocument document, int start, int end, SolidColorBrush brush)
    {
        if (start >= end) return;

        try
        {
            TextPointer startPointer = document.ContentStart.GetPositionAtOffset(start);
            TextPointer endPointer = document.ContentStart.GetPositionAtOffset(end);

            if (startPointer != null && endPointer != null)
            {
                TextRange range = new TextRange(startPointer, endPointer);
                range.ApplyPropertyValue(TextElement.ForegroundProperty, brush);
            }
        }
        catch
        {
            // Ignore any position errors
        }
    }

    // Fast version without comments - just keywords
    public void HighlightKeywordsOnly(string[] keywords, Color[] colors)
    {
        if (keywords.Length != colors.Length)
            throw new ArgumentException("Keywords and colors arrays must have same length");

        var colorMap = new Dictionary<string, SolidColorBrush>();
        for (int i = 0; i < keywords.Length; i++)
        {
            colorMap[keywords[i]] = new SolidColorBrush(colors[i]);
        }

        // Reset to default
        TextRange fullRange = new TextRange(_rtb.Document.ContentStart, _rtb.Document.ContentEnd);
        fullRange.ApplyPropertyValue(TextElement.ForegroundProperty, _defaultBrush);

        // Color keywords
        string text = fullRange.Text;
        foreach (var kvp in colorMap)
        {
            int index = 0;
            while ((index = text.IndexOf(kvp.Key, index, StringComparison.Ordinal)) != -1)
            {
                // Check if it's a whole word
                bool isWholeWord = true;
                if (index > 0 && char.IsLetterOrDigit(text[index - 1])) isWholeWord = false;
                if (index + kvp.Key.Length < text.Length && char.IsLetterOrDigit(text[index + kvp.Key.Length])) isWholeWord = false;

                if (isWholeWord)
                {
                    var start = _rtb.Document.ContentStart.GetPositionAtOffset(index);
                    var end = start.GetPositionAtOffset(kvp.Key.Length);
                    if (start != null && end != null)
                    {
                        new TextRange(start, end).ApplyPropertyValue(TextElement.ForegroundProperty, kvp.Value);
                    }
                }
                index += kvp.Key.Length;
            }
        }
    }
}