using System;
using System.Collections.Generic;
using System.Linq;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;

namespace ICSharpCode.NRefactory.CSharp.TypeSystem.ConstantValues;

/// <summary>
/// Represents an array creation (as used within an attribute argument)
/// </summary>
[Serializable]
public sealed class ConstantArrayCreation : ConstantExpression
{
    // type may be null when the element is being inferred
    private readonly ITypeReference _elementType;
    private readonly IList<ConstantExpression> _arrayElements;

    public ConstantArrayCreation(ITypeReference type, IList<ConstantExpression> arrayElements)
    {
        _elementType = type;
        _arrayElements = arrayElements ?? throw new ArgumentNullException(nameof(arrayElements));
    }

    public override ResolveResult Resolve(CSharpResolver resolver)
    {
        var elements = _arrayElements
            .Select(e => e.Resolve(resolver))
            .ToArray();
        int[] sizeArguments = { elements.Length };
        return resolver.ResolveArrayCreation(_elementType?.Resolve(resolver.CurrentTypeResolveContext), sizeArguments, elements);
    }
}