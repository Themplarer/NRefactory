using System;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Semantics;

namespace ICSharpCode.NRefactory.CSharp.TypeSystem.ConstantValues;

[Serializable]
public sealed class ConstantConditionalOperator : ConstantExpression
{
    private readonly ConstantExpression _condition, _trueExpr, _falseExpr;

    public ConstantConditionalOperator(ConstantExpression condition, ConstantExpression trueExpr, ConstantExpression falseExpr)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _trueExpr = trueExpr ?? throw new ArgumentNullException(nameof(trueExpr));
        _falseExpr = falseExpr ?? throw new ArgumentNullException(nameof(falseExpr));
    }

    public override ResolveResult Resolve(CSharpResolver resolver) =>
        resolver.ResolveConditional(
            _condition.Resolve(resolver),
            _trueExpr.Resolve(resolver),
            _falseExpr.Resolve(resolver)
        );
}