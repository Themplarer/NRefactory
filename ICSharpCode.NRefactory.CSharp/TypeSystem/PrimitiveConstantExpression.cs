using System;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;

namespace ICSharpCode.NRefactory.CSharp.TypeSystem.ConstantValues;

/// <summary>
/// C#'s equivalent to the SimpleConstantValue.
/// </summary>
[Serializable]
public sealed class PrimitiveConstantExpression : ConstantExpression, ISupportsInterning
{
    public PrimitiveConstantExpression(ITypeReference type, object value)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Value = value;
    }

    public ITypeReference Type { get; }

    public object Value { get; }

    public override ResolveResult Resolve(CSharpResolver resolver) =>
        new ConstantResolveResult(Type.Resolve(resolver.CurrentTypeResolveContext), Value);

    int ISupportsInterning.GetHashCodeForInterning() => Type.GetHashCode() ^ (Value?.GetHashCode() ?? 0);

    bool ISupportsInterning.EqualsForInterning(ISupportsInterning other) =>
        other is PrimitiveConstantExpression scv && Type == scv.Type && Value == scv.Value;
}