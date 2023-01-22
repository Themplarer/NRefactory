// Copyright (c) 2010-2013 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;

namespace ICSharpCode.NRefactory.CSharp.TypeSystem;

/// <summary>
/// Reference to a qualified type or namespace name.
/// </summary>
[Serializable]
public sealed class MemberTypeOrNamespaceReference : TypeOrNamespaceReference, ISupportsInterning
{
    private readonly TypeOrNamespaceReference _target;
    private readonly string _identifier;
    private readonly IList<ITypeReference> _typeArguments;
    private readonly NameLookupMode _lookupMode;

    public MemberTypeOrNamespaceReference(TypeOrNamespaceReference target, string identifier, IList<ITypeReference> typeArguments,
        NameLookupMode lookupMode = NameLookupMode.Type)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
        _typeArguments = typeArguments ?? EmptyList<ITypeReference>.Instance;
        _lookupMode = lookupMode;
    }

    public string Identifier => _identifier;

    public TypeOrNamespaceReference Target => _target;

    public IList<ITypeReference> TypeArguments => _typeArguments;

    public NameLookupMode LookupMode => _lookupMode;

    /// <summary>
    /// Adds a suffix to the identifier.
    /// Does not modify the existing type reference, but returns a new one.
    /// </summary>
    public MemberTypeOrNamespaceReference AddSuffix(string suffix) => new(_target, _identifier + suffix, _typeArguments, _lookupMode);

    public override ResolveResult Resolve(CSharpResolver resolver)
    {
        var targetRr = _target.Resolve(resolver);

        if (targetRr.IsError)
            return targetRr;

        var typeArgs = _typeArguments.Resolve(resolver.CurrentTypeResolveContext);
        return resolver.ResolveMemberAccess(targetRr, _identifier, typeArgs, _lookupMode);
    }

    public override IType ResolveType(CSharpResolver resolver) =>
        Resolve(resolver) is TypeResolveResult trr
            ? trr.Type
            : new UnknownType(null, _identifier, _typeArguments.Count);

    public override string ToString() =>
        _typeArguments.Count == 0
            ? _target + "." + _identifier
            : _target + "." + _identifier + "<" + string.Join(",", _typeArguments) + ">";

    int ISupportsInterning.GetHashCodeForInterning()
    {
        var hashCode = 0;

        unchecked
        {
            hashCode += 1000000007 * _target.GetHashCode();
            hashCode += 1000000033 * _identifier.GetHashCode();
            hashCode += 1000000087 * _typeArguments.GetHashCode();
            hashCode += 1000000021 * (int)_lookupMode;
        }

        return hashCode;
    }

    bool ISupportsInterning.EqualsForInterning(ISupportsInterning other) =>
        other is MemberTypeOrNamespaceReference o &&
        _target == o._target &&
        _identifier == o._identifier &&
        _typeArguments == o._typeArguments &&
        _lookupMode == o._lookupMode;
}