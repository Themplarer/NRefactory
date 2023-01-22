using System;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;

namespace ICSharpCode.NRefactory.CSharp.TypeSystem.ConstantValues;

[Serializable]
public sealed class TypeOfConstantExpression : ConstantExpression
{
    public TypeOfConstantExpression(ITypeReference type) => Type = type;

    public ITypeReference Type { get; }

    public override ResolveResult Resolve(CSharpResolver resolver) =>
        resolver.ResolveTypeOf(Type.Resolve(resolver.CurrentTypeResolveContext));
}