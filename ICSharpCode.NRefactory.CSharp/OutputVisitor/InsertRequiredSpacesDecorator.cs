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

namespace ICSharpCode.NRefactory.CSharp;

class InsertRequiredSpacesDecorator : DecoratingTokenWriter
{
    /// <summary>
    /// Used to insert the minimal amount of spaces so that the lexer recognizes the tokens that were written.
    /// </summary>
    private LastWritten _lastWritten;

    public InsertRequiredSpacesDecorator(TokenWriter writer) : base(writer)
    {
    }

    public override void WriteIdentifier(Identifier identifier, object data)
    {
        if (identifier.IsVerbatim || CSharpOutputVisitor.IsKeyword(identifier.Name, identifier))
        {
            if (_lastWritten == LastWritten.KeywordOrIdentifier)
                // this space is not strictly required, so we call Space()
                Space();
        }
        else if (_lastWritten == LastWritten.KeywordOrIdentifier)
            // this space is strictly required, so we directly call the formatter
            base.Space();

        base.WriteIdentifier(identifier, data);
        _lastWritten = LastWritten.KeywordOrIdentifier;
    }

    public override void WriteKeyword(Role role, string keyword)
    {
        if (_lastWritten == LastWritten.KeywordOrIdentifier)
            Space();

        base.WriteKeyword(role, keyword);
        _lastWritten = LastWritten.KeywordOrIdentifier;
    }

    public override void WriteToken(Role role, string token, object data)
    {
        // Avoid that two +, - or ? tokens are combined into a ++, -- or ?? token.
        // Note that we don't need to handle tokens like = because there's no valid
        // C# program that contains the single token twice in a row.
        // (for +, - and &, this can happen with unary operators;
        // for ?, this can happen in "a is int? ? b : c" or "a as int? ?? 0";
        // and for /, this can happen with "1/ *ptr" or "1/ //comment".)
        if (_lastWritten == LastWritten.Plus && token[0] == '+' ||
            _lastWritten == LastWritten.Minus && token[0] == '-' ||
            _lastWritten == LastWritten.Ampersand && token[0] == '&' ||
            _lastWritten == LastWritten.QuestionMark && token[0] == '?' ||
            _lastWritten == LastWritten.Division && token[0] == '*')
            base.Space();

        base.WriteToken(role, token, data);

        _lastWritten = token switch
        {
            "+" => LastWritten.Plus,
            "-" => LastWritten.Minus,
            "&" => LastWritten.Ampersand,
            "?" => LastWritten.QuestionMark,
            "/" => LastWritten.Division,
            _ => LastWritten.Other
        };
    }

    public override void Space()
    {
        base.Space();
        _lastWritten = LastWritten.Whitespace;
    }

    public override void NewLine()
    {
        base.NewLine();
        _lastWritten = LastWritten.Whitespace;
    }

    public override void WriteComment(CommentType commentType, string content, CommentReference[] refs)
    {
        if (_lastWritten == LastWritten.Division)
            // When there's a comment starting after a division operator
            // "1.0 / /*comment*/a", then we need to insert a space in front of the comment.
            base.Space();

        base.WriteComment(commentType, content, refs);
        _lastWritten = LastWritten.Whitespace;
    }

    public override void WritePreProcessorDirective(PreProcessorDirectiveType type, string argument)
    {
        base.WritePreProcessorDirective(type, argument);
        _lastWritten = LastWritten.Whitespace;
    }

    public override void WritePrimitiveValue(object value, object data = null, string literalValue = null)
    {
        if (_lastWritten == LastWritten.KeywordOrIdentifier)
            Space();

        base.WritePrimitiveValue(value, data, literalValue);

        if (value is null or bool)
            return;

        if (value is string)
            _lastWritten = LastWritten.Other;
        else if (value is char)
            _lastWritten = LastWritten.Other;
        else if (value is decimal)
            _lastWritten = LastWritten.Other;
        else if (value is float f)
        {
            if (float.IsInfinity(f) || float.IsNaN(f))
                return;

            _lastWritten = LastWritten.Other;
        }
        else if (value is double d)
        {
            if (double.IsInfinity(d) || double.IsNaN(d))
                return;

            // needs space if identifier follows number;
            // this avoids mistaking the following identifier as type suffix
            _lastWritten = LastWritten.KeywordOrIdentifier;
        }
        else if (value is IFormattable)
            // needs space if identifier follows number;
            // this avoids mistaking the following identifier as type suffix
            _lastWritten = LastWritten.KeywordOrIdentifier;
        else
            _lastWritten = LastWritten.Other;
    }

    public override void WritePrimitiveType(string type)
    {
        if (_lastWritten == LastWritten.KeywordOrIdentifier)
            Space();

        base.WritePrimitiveType(type);
        _lastWritten = type == "new"
            ? LastWritten.Other
            : LastWritten.KeywordOrIdentifier;
    }

    private enum LastWritten
    {
        Whitespace,
        Other,
        KeywordOrIdentifier,
        Plus,
        Minus,
        Ampersand,
        QuestionMark,
        Division
    }
}