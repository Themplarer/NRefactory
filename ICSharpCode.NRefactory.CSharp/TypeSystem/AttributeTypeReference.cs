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
using ICSharpCode.NRefactory.TypeSystem;

namespace ICSharpCode.NRefactory.CSharp.TypeSystem;

/// <summary>
/// Type reference used within an attribute.
/// Looks up both 'withoutSuffix' and 'withSuffix' and returns the type that exists.
/// </summary>
[Serializable]
public sealed class AttributeTypeReference : ITypeReference, ISupportsInterning
{
    private readonly ITypeReference _withoutSuffix, _withSuffix;

    public AttributeTypeReference(ITypeReference withoutSuffix, ITypeReference withSuffix)
    {
        _withoutSuffix = withoutSuffix ?? throw new ArgumentNullException(nameof(withoutSuffix));
        _withSuffix = withSuffix ?? throw new ArgumentNullException(nameof(withSuffix));
    }

    public IType Resolve(ITypeResolveContext context)
    {
        var typeWithoutSuffix = _withoutSuffix.Resolve(context);
        var typeWithSuffix = _withSuffix.Resolve(context);
        return PreferAttributeTypeWithSuffix(typeWithoutSuffix, typeWithSuffix, context.Compilation)
            ? typeWithSuffix
            : typeWithoutSuffix;
    }

    internal static bool PreferAttributeTypeWithSuffix(IType typeWithoutSuffix, IType typeWithSuffix, ICompilation compilation)
    {
        if (typeWithSuffix.Kind == TypeKind.Unknown) return false;

        if (typeWithoutSuffix.Kind == TypeKind.Unknown) return true;

        if (compilation.FindType(KnownTypeCode.Attribute).GetDefinition() is { } attrTypeDef)
        {
            var typeWithoutSuffixIsAttribute = typeWithoutSuffix.GetDefinition()?.IsDerivedFrom(attrTypeDef) ?? false;
            var typeWithSuffixIsAttribute = typeWithSuffix.GetDefinition()?.IsDerivedFrom(attrTypeDef) ?? false;

            // If both types exist and are attributes, C# considers that to be an ambiguity, but we are less strict.

            if (typeWithSuffixIsAttribute && !typeWithoutSuffixIsAttribute)
                return true;
        }

        return false;
    }

    public override string ToString() => _withoutSuffix + "[Attribute]";

    int ISupportsInterning.GetHashCodeForInterning() =>
        unchecked(_withoutSuffix.GetHashCode() + 715613 * _withSuffix.GetHashCode());

    bool ISupportsInterning.EqualsForInterning(ISupportsInterning other) =>
        other is AttributeTypeReference attributeTypeReference &&
        _withoutSuffix == attributeTypeReference._withoutSuffix &&
        _withSuffix == attributeTypeReference._withSuffix;
}