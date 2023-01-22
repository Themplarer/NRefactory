using System;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;

namespace ICSharpCode.NRefactory.CSharp.TypeSystem.ConstantValues;

/// <summary>
/// Used for sizeof() expressions in constants.
/// </summary>
[Serializable]
public sealed class SizeOfConstantValue : ConstantExpression
{
    private readonly ITypeReference _type;

    public SizeOfConstantValue(ITypeReference type) => _type = type ?? throw new ArgumentNullException(nameof(type));

    public override ResolveResult Resolve(CSharpResolver resolver) => resolver.ResolveSizeOf(_type.Resolve(resolver.CurrentTypeResolveContext));
}