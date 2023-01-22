// Copyright (c) 2010-2013 AlphaSierraPapa for the SharpDevelop Team
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Text;

namespace ICSharpCode.NRefactory.CSharp;

/// <summary>
/// Writes C# code into a TextWriter.
/// </summary>
public class TextWriterTokenWriter : TokenWriter, ILocatable
{
    private readonly TextWriter _textWriter;
    private readonly int _maxStringLength;
    private bool _needsIndent = true, _isAtStartOfLine = true;
    private int _line, _column;

    public TextWriterTokenWriter(TextWriter textWriter, int maxStringLength = -1)
    {
        _textWriter = textWriter ?? throw new ArgumentNullException(nameof(textWriter));
        IndentationString = "\t";
        _line = 1;
        _column = 1;
        _maxStringLength = maxStringLength;
    }

    public int Indentation { get; set; }

    public TextLocation Location => new(_line, _column + (_needsIndent ? Indentation * IndentationString.Length : 0));

    public string IndentationString { get; set; }

    public override void WriteIdentifier(Identifier identifier, object data)
    {
        WriteIndentation();

        if (!BoxedTextColor.Keyword.Equals(data) && (identifier.IsVerbatim || CSharpOutputVisitor.IsKeyword(identifier.Name, identifier)))
        {
            _textWriter.Write('@');
            _column++;
        }

        var name = EscapeIdentifier(identifier.Name);
        _textWriter.Write(name);
        _column += name.Length;
        _isAtStartOfLine = false;
    }

    public override void WriteKeyword(Role role, string keyword)
    {
        WriteIndentation();
        _column += keyword.Length;
        _textWriter.Write(keyword);
        _isAtStartOfLine = false;
    }

    public override void WriteToken(Role role, string token, object data)
    {
        WriteIndentation();
        _column += token.Length;
        _textWriter.Write(token);
        _isAtStartOfLine = false;
    }

    public override void Space()
    {
        WriteIndentation();
        _column++;
        _textWriter.Write(' ');
    }

    protected void WriteIndentation()
    {
        if (!_needsIndent) return;

        _needsIndent = false;

        for (var i = 0; i < Indentation; i++) _textWriter.Write(IndentationString);

        _column += Indentation * IndentationString.Length;
    }

    public override void NewLine()
    {
        _textWriter.WriteLine();
        _column = 1;
        _line++;
        _needsIndent = true;
        _isAtStartOfLine = true;
    }

    public override void Indent() => Indentation++;

    public override void Unindent() => Indentation--;

    public override void WriteComment(CommentType commentType, string content, CommentReference[] refs)
    {
        WriteIndentation();

        switch (commentType)
        {
            case CommentType.SingleLine:
                _textWriter.Write("//");
                _textWriter.WriteLine(content);
                _column = 1;
                _line++;
                _needsIndent = true;
                _isAtStartOfLine = true;
                break;
            case CommentType.MultiLine:
                _textWriter.Write("/*");
                _textWriter.Write(content);
                _textWriter.Write("*/");
                _column += 2;
                UpdateEndLocation(content, ref _line, ref _column);
                _column += 2;
                _isAtStartOfLine = false;
                break;
            case CommentType.Documentation:
                _textWriter.Write("///");
                _textWriter.WriteLine(content);
                _column = 1;
                _line++;
                _needsIndent = true;
                _isAtStartOfLine = true;
                break;
            case CommentType.MultiLineDocumentation:
                _textWriter.Write("/**");
                _textWriter.Write(content);
                _textWriter.Write("*/");
                _column += 3;
                UpdateEndLocation(content, ref _line, ref _column);
                _column += 2;
                _isAtStartOfLine = false;
                break;
            default:
                _textWriter.Write(content);
                _column += content.Length;
                break;
        }
    }

    private static void UpdateEndLocation(string content, ref int line, ref int column)
    {
        if (string.IsNullOrEmpty(content))
            return;

        for (var i = 0; i < content.Length; i++)
        {
            switch (content[i])
            {
                case '\r':
                    if (i + 1 < content.Length && content[i + 1] == '\n')
                        i++;
                    goto case '\n';
                case '\n':
                    line++;
                    column = 0;
                    break;
            }

            column++;
        }
    }

    public override void WritePreProcessorDirective(PreProcessorDirectiveType type, string argument)
    {
        // pre-processor directive must start on its own line
        if (!_isAtStartOfLine)
            NewLine();

        WriteIndentation();
        _textWriter.Write('#');
        var directive = type.ToString().ToLowerInvariant();
        _textWriter.Write(directive);
        _column += 1 + directive.Length;

        if (!string.IsNullOrEmpty(argument))
        {
            _textWriter.Write(' ');
            _textWriter.Write(argument);
            _column += 1 + argument.Length;
        }

        NewLine();
    }

    public static string PrintPrimitiveValue(object value)
    {
        var writer = new StringWriter();
        var tokenWriter = new TextWriterTokenWriter(writer);
        tokenWriter.WritePrimitiveValue(value, CSharpMetadataTextColorProvider.Instance.GetColor(value));
        return writer.ToString();
    }

    public override void WritePrimitiveValue(object value, object data = null, string literalValue = null)
    {
        var numberFormatter = NumberFormatter.GetCSharpInstance(hex: false, upper: true);
        WritePrimitiveValue(value, data, literalValue, _maxStringLength, ref _column, numberFormatter, (a, _, _) => _textWriter.Write(a), WriteToken);
    }

    public static void WritePrimitiveValue(object value, object data, string literalValue, int maxStringLength, ref int column,
        NumberFormatter numberFormatter, Action<string, object, object> writer, Action<Role, string, object> writeToken)
    {
        if (literalValue != null)
        {
            Debug.Assert(data != null);
            writer(literalValue, null, data);
            column += literalValue.Length;
            return;
        }

        switch (value)
        {
            case null:
                // usually NullReferenceExpression should be used for this, but we'll handle it anyways
                writer("null", null, BoxedTextColor.Keyword);
                column += 4;
                return;
            case bool b:
            {
                if (b)
                {
                    writer("true", null, BoxedTextColor.Keyword);
                    column += 4;
                }
                else
                {
                    writer("false", null, BoxedTextColor.Keyword);
                    column += 5;
                }

                return;
            }
            default:
                Write(value, maxStringLength, ref column, numberFormatter, writer, writeToken);
                break;
        }
    }

    private static void Write(object value, int maxStringLength, ref int column, NumberFormatter numberFormatter,
        Action<string, object, object> writer, Action<Role, string, object> writeToken)
    {
        if (value is string @string)
        {
            HandleString(maxStringLength, ref column, writer, @string);
            return;
        }

        if (value is char @char)
        {
            HandleChar(ref column, writer, @char);
            return;
        }

        if (value is decimal @decimal)
        {
            HandleDecimal(ref column, writer, @decimal);
            return;
        }

        if (value is float @float)
        {
            HandleFloat(ref column, writer, writeToken, @float);
            return;
        }

        if (value is double @double)
        {
            HandleDouble(ref column, writer, writeToken, @double);
            return;
        }

        if (value is IFormattable formattable)
        {
            HandleFormattable(ref column, numberFormatter, writer, formattable);
            return;
        }

        @string = value.ToString()!;
        writer(@string, null, CSharpMetadataTextColorProvider.Instance.GetColor(value));
        column += @string.Length;
    }

    private static void HandleString(int maxStringLength, ref int column, Action<string, object, object> writer, string s)
    {
        var convertedString = "\"" + ConvertStringMaxLength(s, maxStringLength) + "\"";
        column += convertedString.Length;
        writer(convertedString, null, BoxedTextColor.String);
    }

    private static void HandleChar(ref int column, Action<string, object, object> writer, char c)
    {
        var convertedChar = "'" + ConvertCharLiteral(c) + "'";
        column += convertedChar.Length;
        writer(convertedChar, null, BoxedTextColor.Char);
    }

    private static void HandleDecimal(ref int column, Action<string, object, object> writer, decimal d)
    {
        var convertedDecimal = d.ToString(NumberFormatInfo.InvariantInfo) + "m";
        column += convertedDecimal.Length;
        writer(convertedDecimal, null, BoxedTextColor.Number);
    }

    private static void HandleFloat(ref int column, Action<string, object, object> writer, Action<Role, string, object> writeToken, float @float)
    {
        void WriteLiteralField(string s, ref int column)
        {
            writer(s, null, BoxedTextColor.LiteralField);
            column += s.Length;
        }

        if (float.IsInfinity(@float) || float.IsNaN(@float))
        {
            // Strictly speaking, these aren't PrimitiveExpressions;
            // but we still support writing these to make life easier for code generators.
            writer("float", null, BoxedTextColor.Keyword);
            column += 5;
            writeToken(Roles.Dot, ".", BoxedTextColor.Operator);

            if (float.IsPositiveInfinity(@float))
                WriteLiteralField("PositiveInfinity", ref column);
            else if (float.IsNegativeInfinity(@float))
                WriteLiteralField("NegativeInfinity", ref column);
            else
                WriteLiteralField("NaN", ref column);

            return;
        }

        var number = @float.ToString("R", NumberFormatInfo.InvariantInfo) + "f";

        // negative zero is a special case
        // (again, not a primitive expression, but it's better to handle
        // the special case here than to do it in all code generators)
        if (@float == 0 && float.IsNegativeInfinity(1 / @float) && number[0] != '-')
            number = "-" + number;

        column += number.Length;
        writer(number, @float, BoxedTextColor.Number);
    }

    private static void HandleDouble(ref int column, Action<string, object, object> writer, Action<Role, string, object> writeToken, double @double)
    {
        void WriteLiteralField(string s, ref int column)
        {
            writer(s, null, BoxedTextColor.LiteralField);
            column += s.Length;
        }

        if (double.IsInfinity(@double) || double.IsNaN(@double))
        {
            // Strictly speaking, these aren't PrimitiveExpressions;
            // but we still support writing these to make life easier for code generators.
            writer("double", null, BoxedTextColor.Keyword);
            column += 6;
            writeToken(Roles.Dot, ".", BoxedTextColor.Operator);

            if (double.IsPositiveInfinity(@double))
                WriteLiteralField("PositiveInfinity", ref column);
            else if (double.IsNegativeInfinity(@double))
                WriteLiteralField("NegativeInfinity", ref column);
            else
                WriteLiteralField("NaN", ref column);

            return;
        }

        var number = @double.ToString("R", NumberFormatInfo.InvariantInfo);

        // negative zero is a special case
        // (again, not a primitive expression, but it's better to handle
        // the special case here than to do it in all code generators)
        if (@double == 0 && double.IsNegativeInfinity(1 / @double) && number[0] != '-')
            number = "-" + number;

        if (number.IndexOf('.') < 0 && number.IndexOf('E') < 0)
            number += ".0";

        column += number.Length;
        writer(number, @double, BoxedTextColor.Number);
    }

    private static void HandleFormattable(ref int column, NumberFormatter numberFormatter, Action<string, object, object> writer,
        IFormattable formattable)
    {
        var valueStr = ToString(numberFormatter, formattable);
        writer(valueStr, formattable, BoxedTextColor.Number);
        column += valueStr.Length;
    }

    private static string ToString(NumberFormatter numberFormatter, IFormattable formattable) =>
        formattable switch
        {
            int v => numberFormatter.Format(v),
            uint v => numberFormatter.Format(v) + "U",
            long v => numberFormatter.Format(v) + "L",
            ulong v => numberFormatter.Format(v) + "UL",
            byte v => numberFormatter.Format(v),
            ushort v => numberFormatter.Format(v),
            short v => numberFormatter.Format(v),
            sbyte v => numberFormatter.Format(v),
            _ => formattable.ToString(null, NumberFormatInfo.InvariantInfo)
        };

    /// <summary>
    /// Gets the escape sequence for the specified character within a char literal.
    /// Does not include the single quotes surrounding the char literal.
    /// </summary>
    public static string ConvertCharLiteral(char ch) => ch == '\'' ? "\\'" : ConvertChar(ch);

    /// <summary>
    /// Gets the escape sequence for the specified character.
    /// </summary>
    /// <remarks>This method does not convert ' or ".</remarks>
    private static string ConvertChar(char ch) =>
        ch switch
        {
            '\\' => "\\\\",
            '\0' => "\\0",
            '\a' => "\\a",
            '\b' => "\\b",
            '\f' => "\\f",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            '\v' => "\\v",
            // ASCII characters we allow directly in the output even though we don't use
            // other Unicode characters of the same category.
            ' ' or '_' or '`' or '^' => ch.ToString(),
            '\ufffd' => "\\u" + ((int)ch).ToString("x4"),
            _ => FormatChar(ch)
        };

    private static string FormatChar(char ch) =>
        char.GetUnicodeCategory(ch) switch
        {
            UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark or UnicodeCategory.LineSeparator or
                UnicodeCategory.ParagraphSeparator or UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate or
                UnicodeCategory.PrivateUse or UnicodeCategory.ConnectorPunctuation or UnicodeCategory.ModifierSymbol or
                UnicodeCategory.OtherNotAssigned or UnicodeCategory.SpaceSeparator => "\\u" + ((int)ch).ToString("x4"),
            _ => ch.ToString()
        };

    /// <summary>
    /// Converts special characters to escape sequences within the given string.
    /// </summary>
    public static string ConvertString(string str) => ConvertString(str, 0, str.Length, -1);

    public static string ConvertStringMaxLength(string str, int maxChars) => ConvertString(str, 0, str.Length, maxChars);

    private static string ConvertString(string str, int start, int length, int maxChars)
    {
        var index = start;
        var truncated = false;

        if (maxChars > 0 && length > maxChars)
        {
            length = maxChars;
            truncated = true;
        }

        const string truncMsg = "[...string is too long...]";
        var end = start + length;

        for (;; index++)
        {
            if (index >= end)
            {
                if (start != 0 || end != str.Length)
                    str = str.Substring(start, length);

                if (truncated)
                    return str + truncMsg;

                return str;
            }

            if (TryGetResultByChar(str[index], str, start, index, end, truncated, truncMsg) is var result && !string.IsNullOrEmpty(result))
                return result;
        }
    }

    private static string TryGetResultByChar(char c, string str, int start, int index, int end, bool truncated, string truncMsg) =>
        c switch
        {
            '"' or '\\' or '\0' or '\a' or '\b' or '\f' or '\n' or '\r' or '\t' or '\v' or '\ufffd' => GetResult(str, start, index, end, truncated,
                truncMsg),
            ' ' or '_' or '`' or '^' => null,
            _ => TryGetResultByUnicodeCategory(char.GetUnicodeCategory(c), str, start, index, end, truncated, truncMsg)
        };

    private static string TryGetResultByUnicodeCategory(UnicodeCategory c, string str, int start, int index, int end, bool truncated,
        string truncMsg) =>
        c switch
        {
            UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark or UnicodeCategory.LineSeparator or
                UnicodeCategory.ParagraphSeparator or UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate or
                UnicodeCategory.PrivateUse or UnicodeCategory.ConnectorPunctuation or UnicodeCategory.ModifierSymbol or
                UnicodeCategory.OtherNotAssigned or UnicodeCategory.SpaceSeparator => GetResult(str, start, index, end, truncated, truncMsg),
            _ => null
        };

    private static string GetResult(string str, int start, int index, int end, bool truncated, string truncMsg)
    {
        var sb = new StringBuilder();

        if (index > start)
            sb.Append(str, start, index - start);

        for (; index < end; index++)
            sb.Append(str[index] is var ch && ch == '"' ? "\\\"" : ConvertChar(ch));

        if (truncated)
            sb.Append(truncMsg);

        return sb.ToString();
    }

    public static string EscapeIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return identifier;

        var sb = new StringBuilder();

        for (var i = 0; i < identifier.Length; i++)
            if (IsPrintableIdentifierChar(identifier, i))
            {
                if (char.IsSurrogatePair(identifier, i))
                {
                    sb.Append(identifier.AsSpan(i, 2));
                    i++;
                }
                else
                    sb.Append(identifier[i]);
            }
            else
            {
                if (char.IsSurrogatePair(identifier, i))
                {
                    sb.Append($"\\U{char.ConvertToUtf32(identifier, i):x8}");
                    i++;
                }
                else
                    sb.Append($"\\u{(int)identifier[i]:x4}");
            }

        return sb.ToString();
    }

    private static bool IsPrintableIdentifierChar(string identifier, int index) =>
        char.GetUnicodeCategory(identifier, index) switch
        {
            UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark or UnicodeCategory.LineSeparator or
                UnicodeCategory.ParagraphSeparator or UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate or
                UnicodeCategory.PrivateUse or UnicodeCategory.ConnectorPunctuation or UnicodeCategory.ModifierSymbol or
                UnicodeCategory.OtherNotAssigned or UnicodeCategory.SpaceSeparator => false,
            _ => true
        };

    public override void WritePrimitiveType(string type)
    {
        _textWriter.Write(type);
        _column += type.Length;

        if (type == "new")
        {
            _textWriter.Write("()");
            _column += 2;
        }
    }

    public override void StartNode(AstNode node) =>
        // Write out the indentation, so that overrides of this method
        // can rely use the current output length to identify the position of the node
        // in the output.
        WriteIndentation();

    public override void EndNode(AstNode node)
    {
    }
}