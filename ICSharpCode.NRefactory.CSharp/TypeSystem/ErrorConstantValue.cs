using System;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;

namespace ICSharpCode.NRefactory.CSharp.TypeSystem.ConstantValues;

/// <summary>
/// Used for constants that could not be converted to IConstantValue.
/// </summary>
[Serializable]
public sealed class ErrorConstantValue : IConstantValue
{
    private readonly ITypeReference _type;

    public ErrorConstantValue(ITypeReference type) => _type = type ?? throw new ArgumentNullException(nameof(type));

    public ResolveResult Resolve(ITypeResolveContext context) => new ErrorResolveResult(_type.Resolve(context));
}