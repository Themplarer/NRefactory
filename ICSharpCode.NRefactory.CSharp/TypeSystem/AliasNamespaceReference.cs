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
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;

namespace ICSharpCode.NRefactory.CSharp.TypeSystem;

/// <summary>
/// Looks up an alias (identifier in front of :: operator).
/// </summary>
/// <remarks>
/// The member lookup performed by the :: operator is handled
/// by <see cref="MemberTypeOrNamespaceReference"/>.
/// </remarks>
[Serializable]
public sealed class AliasNamespaceReference : TypeOrNamespaceReference, ISupportsInterning
{
    public AliasNamespaceReference(string identifier) => Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));

    public string Identifier { get; }

    public override ResolveResult Resolve(CSharpResolver resolver) => resolver.ResolveAlias(Identifier);

    // alias cannot refer to types
    public override IType ResolveType(CSharpResolver resolver) => SpecialType.UnknownType;

    public override string ToString() => Identifier + "::";

    int ISupportsInterning.GetHashCodeForInterning() => Identifier.GetHashCode();

    bool ISupportsInterning.EqualsForInterning(ISupportsInterning other) =>
        other is AliasNamespaceReference aliasNamespaceReference && Identifier == aliasNamespaceReference.Identifier;
}