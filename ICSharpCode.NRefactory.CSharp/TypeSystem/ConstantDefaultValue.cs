using System;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;

namespace ICSharpCode.NRefactory.CSharp.TypeSystem.ConstantValues;

[Serializable]
public sealed class ConstantDefaultValue : ConstantExpression, ISupportsInterning
{
    private readonly ITypeReference _type;

    public ConstantDefaultValue(ITypeReference type) => _type = type ?? throw new ArgumentNullException(nameof(type));

    public override ResolveResult Resolve(CSharpResolver resolver) =>
        resolver.ResolveDefaultValue(_type.Resolve(resolver.CurrentTypeResolveContext));

    int ISupportsInterning.GetHashCodeForInterning() => _type.GetHashCode();

    bool ISupportsInterning.EqualsForInterning(ISupportsInterning other) => other is ConstantDefaultValue o && _type == o._type;
}