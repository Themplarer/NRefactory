using System;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Semantics;

namespace ICSharpCode.NRefactory.CSharp.TypeSystem.ConstantValues;

[Serializable]
public sealed class ConstantCheckedExpression : ConstantExpression
{
    private readonly bool _checkForOverflow;
    private readonly ConstantExpression _expression;

    public ConstantCheckedExpression(bool checkForOverflow, ConstantExpression expression)
    {
        _checkForOverflow = checkForOverflow;
        _expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }

    public override ResolveResult Resolve(CSharpResolver resolver) =>
        _expression.Resolve(resolver.WithCheckForOverflow(_checkForOverflow));
}