using System;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Semantics;

namespace ICSharpCode.NRefactory.CSharp.TypeSystem.ConstantValues;

[Serializable]
public sealed class ConstantUnaryOperator : ConstantExpression
{
    private readonly UnaryOperatorType _operatorType;
    private readonly ConstantExpression _expression;

    public ConstantUnaryOperator(UnaryOperatorType operatorType, ConstantExpression expression)
    {
        _operatorType = operatorType;
        _expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }

    public override ResolveResult Resolve(CSharpResolver resolver) =>
        resolver.ResolveUnaryOperator(_operatorType, _expression.Resolve(resolver));
}