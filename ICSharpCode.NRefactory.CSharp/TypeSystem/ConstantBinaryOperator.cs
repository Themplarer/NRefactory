using System;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Semantics;

namespace ICSharpCode.NRefactory.CSharp.TypeSystem.ConstantValues;

[Serializable]
public sealed class ConstantBinaryOperator : ConstantExpression
{
    private readonly ConstantExpression _left;
    private readonly BinaryOperatorType _operatorType;
    private readonly ConstantExpression _right;

    public ConstantBinaryOperator(ConstantExpression left, BinaryOperatorType operatorType, ConstantExpression right)
    {
        _left = left ?? throw new ArgumentNullException(nameof(left));
        _operatorType = operatorType;
        _right = right ?? throw new ArgumentNullException(nameof(right));
    }

    public override ResolveResult Resolve(CSharpResolver resolver)
    {
        var lhs = _left.Resolve(resolver);
        var rhs = _right.Resolve(resolver);
        return resolver.ResolveBinaryOperator(_operatorType, lhs, rhs);
    }
}