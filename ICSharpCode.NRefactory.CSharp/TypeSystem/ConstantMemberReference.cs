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

namespace ICSharpCode.NRefactory.CSharp.TypeSystem.ConstantValues;

[Serializable]
public sealed class ConstantMemberReference : ConstantExpression
{
    private readonly ITypeReference _targetType;
    private readonly ConstantExpression _targetExpression;
    private readonly string _memberName;
    private readonly IList<ITypeReference> _typeArguments;

    public ConstantMemberReference(ITypeReference targetType, string memberName, IList<ITypeReference> typeArguments = null)
    {
        _targetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
        _memberName = memberName ?? throw new ArgumentNullException(nameof(memberName));
        _typeArguments = typeArguments ?? EmptyList<ITypeReference>.Instance;
    }

    public ConstantMemberReference(ConstantExpression targetExpression, string memberName, IList<ITypeReference> typeArguments = null)
    {
        _targetExpression = targetExpression ?? throw new ArgumentNullException(nameof(targetExpression));
        _memberName = memberName ?? throw new ArgumentNullException(nameof(memberName));
        _typeArguments = typeArguments ?? EmptyList<ITypeReference>.Instance;
    }

    public override ResolveResult Resolve(CSharpResolver resolver)
    {
        var resolveResult = _targetType != null
            ? new TypeResolveResult(_targetType.Resolve(resolver.CurrentTypeResolveContext))
            : _targetExpression.Resolve(resolver);
        return resolver.ResolveMemberAccess(resolveResult, _memberName, _typeArguments.Resolve(resolver.CurrentTypeResolveContext));
    }
}