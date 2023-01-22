using System;
using dnSpy.Contracts.Decompiler;

namespace ICSharpCode.NRefactory.CSharp;

public abstract class DecoratingTokenWriter : TokenWriter
{
    private readonly TokenWriter _decoratedWriter;

    protected DecoratingTokenWriter(TokenWriter decoratedWriter) =>
        _decoratedWriter = decoratedWriter ?? throw new ArgumentNullException(nameof(decoratedWriter));

    public override void StartNode(AstNode node) => _decoratedWriter.StartNode(node);

    public override void EndNode(AstNode node) => _decoratedWriter.EndNode(node);

    public override void WriteSpecialsUpToNode(AstNode node) => _decoratedWriter.WriteSpecialsUpToNode(node);

    public override void WriteIdentifier(Identifier identifier, object data) => _decoratedWriter.WriteIdentifier(identifier, data);

    public override void WriteKeyword(Role role, string keyword) => _decoratedWriter.WriteKeyword(role, keyword);

    public override void WriteToken(Role role, string token, object data) => _decoratedWriter.WriteToken(role, token, data);

    public override void WritePrimitiveValue(object value, object data = null, string literalValue = null) =>
        _decoratedWriter.WritePrimitiveValue(value, data, literalValue);

    public override void WritePrimitiveType(string type) => _decoratedWriter.WritePrimitiveType(type);

    public override void Space() => _decoratedWriter.Space();

    public override void Indent() => _decoratedWriter.Indent();

    public override void Unindent() => _decoratedWriter.Unindent();

    public override void NewLine() => _decoratedWriter.NewLine();

    public override void WriteComment(CommentType commentType, string content, CommentReference[] refs) =>
        _decoratedWriter.WriteComment(commentType, content, refs);

    public override void WritePreProcessorDirective(PreProcessorDirectiveType type, string argument) =>
        _decoratedWriter.WritePreProcessorDirective(type, argument);

    public override void DebugStart(AstNode node, int? start) => _decoratedWriter.DebugStart(node, start);

    public override void DebugHidden(AstNode hiddenNode) => _decoratedWriter.DebugHidden(hiddenNode);

    public override void DebugExpression(AstNode node) => _decoratedWriter.DebugExpression(node);

    public override void DebugEnd(AstNode node, int? end) => _decoratedWriter.DebugEnd(node, end);

    public override int? GetLocation() => _decoratedWriter.GetLocation();

    public override void AddHighlightedKeywordReference(object reference, int start, int end) =>
        _decoratedWriter.AddHighlightedKeywordReference(reference, start, end);

    public override void AddBracePair(int leftStart, int leftEnd, int rightStart, int rightEnd, CodeBracesRangeFlags flags) =>
        _decoratedWriter.AddBracePair(leftStart, leftEnd, rightStart, rightEnd, flags);

    public override void AddLineSeparator(int position) => _decoratedWriter.AddLineSeparator(position);
}