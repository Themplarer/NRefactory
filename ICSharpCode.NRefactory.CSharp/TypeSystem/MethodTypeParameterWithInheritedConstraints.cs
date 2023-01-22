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
using System.Linq;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using ICSharpCode.NRefactory.Utils;

namespace ICSharpCode.NRefactory.CSharp.TypeSystem;

[Serializable]
public sealed class MethodTypeParameterWithInheritedConstraints : DefaultUnresolvedTypeParameter
{
    public MethodTypeParameterWithInheritedConstraints(int index, string name)
        : base(SymbolKind.Method, index, name)
    {
    }

    public override ITypeParameter CreateResolvedTypeParameter(ITypeResolveContext context) =>
        context.CurrentMember is IMethod
            ? new ResolvedMethodTypeParameterWithInheritedConstraints(this, context)
            : base.CreateResolvedTypeParameter(context);

    private static ITypeParameter ResolveBaseTypeParameter(IMember parentMethod, int index) =>
        GetMethod(parentMethod) is { } baseMethod && index < baseMethod.TypeParameters.Count
            ? baseMethod.TypeParameters[index]
            : null;

    private static IMethod GetMethod(IMember parentMethod)
    {
        if (parentMethod.IsOverride)
            return GetBaseMethod(parentMethod);

        if (parentMethod.IsExplicitInterfaceImplementation && parentMethod.ImplementedInterfaceMembers.Count == 1)
            return parentMethod.ImplementedInterfaceMembers[0] as IMethod;

        return null;
    }

    private static IMethod GetBaseMethod(IMember parentMethod) =>
        InheritanceHelper.GetBaseMembers(parentMethod, false).OfType<IMethod>().FirstOrDefault(m => !m.IsOverride);

    private sealed class ResolvedMethodTypeParameterWithInheritedConstraints : AbstractTypeParameter
    {
        private volatile ITypeParameter _baseTypeParameter;

        public ResolvedMethodTypeParameterWithInheritedConstraints(MethodTypeParameterWithInheritedConstraints unresolved, ITypeResolveContext context)
            : base(context.CurrentMember, unresolved.Index, unresolved.Name, unresolved.Variance,
                unresolved.Attributes.CreateResolvedAttributes(context), unresolved.Region)
        {
        }

        ITypeParameter GetBaseTypeParameter()
        {
            var baseTp = _baseTypeParameter;

            if (baseTp == null)
                // ResolveBaseTypeParameter() is idempotent, so this is thread-safe.
                _baseTypeParameter = baseTp = ResolveBaseTypeParameter((IMethod)Owner, Index);

            return baseTp;
        }

        public override bool HasValueTypeConstraint => GetBaseTypeParameter()?.HasValueTypeConstraint ?? false;

        public override bool HasReferenceTypeConstraint => GetBaseTypeParameter()?.HasReferenceTypeConstraint ?? false;

        public override bool HasDefaultConstructorConstraint => GetBaseTypeParameter()?.HasDefaultConstructorConstraint ?? false;

        public override IEnumerable<IType> DirectBaseTypes
        {
            get
            {
                if (GetBaseTypeParameter() is { } baseTp)
                {
                    // Substitute occurrences of the base method's type parameters in the constraints
                    // with the type parameters from the
                    var owner = (IMethod)Owner;
                    var substitution = new TypeParameterSubstitution(null, new ProjectedList<ITypeParameter, IType>(owner.TypeParameters, t => t));
                    return baseTp.DirectBaseTypes.Select(t => t.AcceptVisitor(substitution));
                }

                return EmptyList<IType>.Instance;
            }
        }
    }
}