using System;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;

namespace ICSharpCode.NRefactory.CSharp.TypeSystem.ConstantValues;

[Serializable]
public sealed class ConstantCast : ConstantExpression, ISupportsInterning
{
    private readonly ITypeReference _targetType;
    private readonly ConstantExpression _expression;
    private readonly bool _allowNullableConstants;

    public ConstantCast(ITypeReference targetType, ConstantExpression expression, bool allowNullableConstants)
    {
        _targetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
        _expression = expression ?? throw new ArgumentNullException(nameof(expression));
        _allowNullableConstants = allowNullableConstants;
    }

    public override ResolveResult Resolve(CSharpResolver resolver)
    {
        var type = _targetType.Resolve(resolver.CurrentTypeResolveContext);
        var resolveResult = _expression.Resolve(resolver);

        if (_allowNullableConstants && NullableType.IsNullable(type))
        {
            resolveResult = resolver.ResolveCast(NullableType.GetUnderlyingType(type), resolveResult);

            if (resolveResult.IsCompileTimeConstant)
                return new ConstantResolveResult(type, resolveResult.ConstantValue);
        }

        return resolver.ResolveCast(type, resolveResult);
    }

    int ISupportsInterning.GetHashCodeForInterning() => unchecked(_targetType.GetHashCode() + _expression.GetHashCode() * 1018829);

    bool ISupportsInterning.EqualsForInterning(ISupportsInterning other) =>
        other is ConstantCast cast &&
        _targetType == cast._targetType &&
        _expression == cast._expression &&
        _allowNullableConstants == cast._allowNullableConstants;
}