using System;
using System.Collections.Generic;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;

namespace ICSharpCode.NRefactory.CSharp.TypeSystem.ConstantValues;

[Serializable]
public sealed class ConstantIdentifierReference : ConstantExpression
{
    private readonly string _identifier;
    private readonly IList<ITypeReference> _typeArguments;

    public ConstantIdentifierReference(string identifier, IList<ITypeReference> typeArguments = null)
    {
        _identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
        _typeArguments = typeArguments ?? EmptyList<ITypeReference>.Instance;
    }

    public override ResolveResult Resolve(CSharpResolver resolver) =>
        resolver.ResolveSimpleName(_identifier, _typeArguments.Resolve(resolver.CurrentTypeResolveContext));
}