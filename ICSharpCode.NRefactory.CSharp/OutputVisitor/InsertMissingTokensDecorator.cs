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
using System.Linq;

namespace ICSharpCode.NRefactory.CSharp;

class InsertMissingTokensDecorator : DecoratingTokenWriter
{
    private readonly Stack<List<AstNode>> _nodes = new();
    private List<AstNode> _currentList;
    private readonly ILocatable _locationProvider;

    public InsertMissingTokensDecorator(TokenWriter writer, ILocatable locationProvider)
        : base(writer)
    {
        _locationProvider = locationProvider;
        _currentList = new List<AstNode>();
    }

    public override void StartNode(AstNode node)
    {
        if (node.NodeType != NodeType.Whitespace)
        {
            _currentList.Add(node);
            _nodes.Push(_currentList);
            _currentList = new List<AstNode>();
        }
        else if (node is Comment comment)
            comment.SetStartLocation(_locationProvider.Location);
        else if (node is ErrorExpression error)
            error.Location = _locationProvider.Location;

        base.StartNode(node);
    }

    public override void EndNode(AstNode node)
    {
        // ignore whitespace: these don't need to be processed.
        // StartNode/EndNode is only called for them to support folding of comments.
        if (node.NodeType != NodeType.Whitespace)
        {
            Debug.Assert(_currentList != null);

            foreach (var removable in node.Children.Where(n => n is CSharpTokenNode))
                removable.Remove();

            foreach (var child in _currentList)
            {
                Debug.Assert(child.Parent == null || node == child.Parent);
                child.Remove();
                node.AddChildWithExistingRole(child);
            }

            _currentList = _nodes.Pop();
        }
        else if (node is Comment comment)
            comment.SetEndLocation(_locationProvider.Location);

        base.EndNode(node);
    }

    public override void WriteToken(Role role, string token, object data)
    {
        switch (_nodes.Peek().LastOrDefault())
        {
            case EmptyStatement emptyStatement:
                emptyStatement.Location = _locationProvider.Location;
                break;
            case ErrorExpression errorExpression:
                errorExpression.Location = _locationProvider.Location;
                break;
            default:
                var t = new CSharpTokenNode(_locationProvider.Location, (TokenRole)role)
                {
                    Role = role
                };
                _currentList.Add(t);
                break;
        }

        base.WriteToken(role, token, data);
    }

    public override void WriteKeyword(Role role, string keyword)
    {
        var start = _locationProvider.Location;
        CSharpTokenNode t = null;

        if (role is TokenRole tokenRole)
            t = new CSharpTokenNode(start, tokenRole);
        else if (role == EntityDeclaration.ModifierRole)
            t = new CSharpModifierToken(start, CSharpModifierToken.GetModifierValue(keyword));
        else
            switch (keyword)
            {
                case "this" when _nodes.Peek().LastOrDefault() is ThisReferenceExpression node:
                    node.Location = start;
                    break;
                case "base" when _nodes.Peek().LastOrDefault() is BaseReferenceExpression node:
                    node.Location = start;
                    break;
            }

        if (t != null)
        {
            _currentList.Add(t);
            t.Role = role;
        }

        base.WriteKeyword(role, keyword);
    }

    public override void WriteIdentifier(Identifier identifier, object data)
    {
        if (!identifier.IsNull)
            identifier.SetStartLocation(_locationProvider.Location);

        _currentList.Add(identifier);
        base.WriteIdentifier(identifier, data);
    }

    public override void WritePrimitiveValue(object value, object data = null, string literalValue = null)
    {
        var node = _nodes.Peek().LastOrDefault() as Expression;
        var startLocation = _locationProvider.Location;
        base.WritePrimitiveValue(value, data, literalValue);

        if (node is PrimitiveExpression primitiveExpression)
            primitiveExpression.SetLocation(startLocation, _locationProvider.Location);

        if (node is NullReferenceExpression nullReferenceExpression)
            nullReferenceExpression.SetStartLocation(startLocation);
    }

    public override void WritePrimitiveType(string type)
    {
        if (_nodes.Peek().LastOrDefault() is PrimitiveType node)
            node.SetStartLocation(_locationProvider.Location);

        base.WritePrimitiveType(type);
    }
}