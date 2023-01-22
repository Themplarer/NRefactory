using System.Diagnostics;
using System.Linq;

namespace ICSharpCode.NRefactory.CSharp;

public class GenericGrammarAmbiguityVisitor : DepthFirstAstVisitor<bool>
{
    private int _genericNestingLevel;
    private bool _ambiguityFound;

    /// <summary>
    /// Resolves ambiguities in the specified syntax tree.
    /// This method must be called after the InsertParenthesesVisitor, because the ambiguity depends on whether the
    /// final `>` in the possible-type-argument is followed by an opening parenthesis.
    /// </summary>
    public static void ResolveAmbiguities(AstNode rootNode)
    {
        foreach (var node in rootNode.Descendants.OfType<BinaryOperatorExpression>())
            if (CausesAmbiguityWithGenerics(node))
                node.ReplaceWith(n => new ParenthesizedExpression(n));
    }

    public static bool CausesAmbiguityWithGenerics(BinaryOperatorExpression binaryOperatorExpression)
    {
        if (binaryOperatorExpression.Operator != BinaryOperatorType.LessThan)
            return false;

        var v = new GenericGrammarAmbiguityVisitor
        {
            _genericNestingLevel = 1
        };

        for (AstNode node = binaryOperatorExpression.Right; node != null; node = node.GetNextNode())
            if (node.AcceptVisitor(v))
                return v._ambiguityFound;

        return false;
    }

    protected override bool VisitChildren(AstNode node)
    {
        // unhandled node: probably not syntactically valid in a typename

        // These are preconditions for all recursive Visit() calls.
        Debug.Assert(_genericNestingLevel > 0);
        Debug.Assert(!_ambiguityFound);

        // The return value merely indicates whether to stop visiting.
        return true; // stop visiting, no ambiguity found
    }

    public override bool VisitBinaryOperatorExpression(BinaryOperatorExpression binaryOperatorExpression)
    {
        if (binaryOperatorExpression.Left.AcceptVisitor(this))
            return true;

        Debug.Assert(_genericNestingLevel > 0);

        switch (binaryOperatorExpression.Operator)
        {
            case BinaryOperatorType.LessThan:
                _genericNestingLevel += 1;
                break;
            case BinaryOperatorType.GreaterThan:
                _genericNestingLevel--;
                break;
            case BinaryOperatorType.ShiftRight when _genericNestingLevel >= 2:
                _genericNestingLevel -= 2;
                break;
            default:
                return true; // stop visiting, no ambiguity found
        }

        if (_genericNestingLevel == 0)
        {
            // Of the all tokens that might follow `>` and trigger the ambiguity to be resolved in favor of generics,
            // `(` is the only one that might start an expression.
            _ambiguityFound = binaryOperatorExpression.Right is ParenthesizedExpression;
            return true; // stop visiting
        }

        return binaryOperatorExpression.Right.AcceptVisitor(this);
    }

    // identifier could also be valid in a type argument
    // keep visiting
    public override bool VisitIdentifierExpression(IdentifierExpression identifierExpression) => false;

    // keep visiting
    public override bool VisitTypeReferenceExpression(TypeReferenceExpression typeReferenceExpression) => false;

    // MRE could also be valid in a type argument
    public override bool VisitMemberReferenceExpression(MemberReferenceExpression memberReferenceExpression) =>
        memberReferenceExpression.Target.AcceptVisitor(this);
}