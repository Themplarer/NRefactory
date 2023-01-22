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

using System.Collections.Generic;
using System.Diagnostics;

namespace ICSharpCode.NRefactory.CSharp;

internal class InsertSpecialsDecorator : DecoratingTokenWriter
{
    private readonly Stack<AstNode> _positionStack = new();
    private int _visitorWroteNewLine;

    public InsertSpecialsDecorator(TokenWriter writer) : base(writer)
    {
    }

    public override void StartNode(AstNode node)
    {
        if (_positionStack.Count > 0)
            WriteSpecialsUpToNode(node);

        _positionStack.Push(node.FirstChild);
        base.StartNode(node);
    }

    public override void EndNode(AstNode node)
    {
        base.EndNode(node);
        var pos = _positionStack.Pop();
        Debug.Assert(pos == null || pos.Parent == node);
        WriteSpecials(pos, null);
    }

    public override void WriteKeyword(Role role, string keyword)
    {
        if (role != null)
            WriteSpecialsUpToRole(role);

        base.WriteKeyword(role, keyword);
    }

    public override void WriteIdentifier(Identifier identifier, object data)
    {
        WriteSpecialsUpToRole(identifier.Role ?? Roles.Identifier);
        base.WriteIdentifier(identifier, data);
    }

    public override void WriteToken(Role role, string token, object data)
    {
        WriteSpecialsUpToRole(role);
        base.WriteToken(role, token, data);
    }

    public override void NewLine()
    {
        if (_visitorWroteNewLine >= 0)
            base.NewLine();

        _visitorWroteNewLine++;
    }

    #region WriteSpecials

    /// <summary>
    /// Writes all specials from start to end (exclusive). Does not touch the positionStack.
    /// </summary>
    private void WriteSpecials(AstNode start, AstNode end)
    {
        for (var pos = start; pos != end; pos = pos.NextSibling)
        {
            if (pos.Role == Roles.Comment)
            {
                var node = (Comment)pos;
                base.StartNode(node);
                base.WriteComment(node.CommentType, node.Content, node.References);
                base.EndNode(node);
            }

            // see CSharpOutputVisitor.VisitNewLine()
            //				if (pos.Role == Roles.NewLine) {
            //					if (visitorWroteNewLine <= 0)
            //						base.NewLine();
            //					visitorWroteNewLine--;
            //				}
            if (pos.Role == Roles.PreProcessorDirective)
            {
                var node = (PreProcessorDirective)pos;
                base.StartNode(node);
                base.WritePreProcessorDirective(node.Type, node.Argument);
                base.EndNode(node);
            }
        }
    }

    private void WriteSpecialsUpToRole(Role role, AstNode nextNode = null)
    {
        if (_positionStack.Count == 0)
            return;

        // Look for the role between the current position and the nextNode.
        for (var pos = _positionStack.Peek(); pos != null && pos != nextNode; pos = pos.NextSibling)
            if (pos.Role == role)
            {
                WriteSpecials(_positionStack.Pop(), pos);
                // Push the next sibling because the node matching the role is not a special,
                // and should be considered to be already handled.
                _positionStack.Push(pos.NextSibling);
                // This is necessary for OptionalComma() to work correctly.
                break;
            }
    }

    /// <summary>
    /// Writes all specials between the current position (in the positionStack) and the specified node.
    /// Advances the current position.
    /// </summary>
    public override void WriteSpecialsUpToNode(AstNode node)
    {
        if (_positionStack.Count == 0)
            return;

        for (var pos = _positionStack.Peek(); pos != null; pos = pos.NextSibling)
            if (pos == node)
            {
                WriteSpecials(_positionStack.Pop(), pos);
                // Push the next sibling because the node itself is not a special,
                // and should be considered to be already handled.
                _positionStack.Push(pos.NextSibling);
                // This is necessary for OptionalComma() to work correctly.
                break;
            }
    }

    #endregion
}