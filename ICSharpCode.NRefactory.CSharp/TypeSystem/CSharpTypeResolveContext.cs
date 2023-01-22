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

public sealed class CSharpTypeResolveContext : ITypeResolveContext
{
    private readonly string[] _methodTypeParameterNames;

    public CSharpTypeResolveContext(IAssembly assembly, ResolvedUsingScope usingScope = null, ITypeDefinition typeDefinition = null,
        IMember member = null)
    {
        CurrentAssembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        CurrentUsingScope = usingScope;
        CurrentTypeDefinition = typeDefinition;
        CurrentMember = member;
    }

    private CSharpTypeResolveContext(IAssembly assembly, ResolvedUsingScope usingScope, ITypeDefinition typeDefinition, IMember member,
        string[] methodTypeParameterNames)
    {
        CurrentAssembly = assembly;
        CurrentUsingScope = usingScope;
        CurrentTypeDefinition = typeDefinition;
        CurrentMember = member;
        _methodTypeParameterNames = methodTypeParameterNames;
    }

    public ResolvedUsingScope CurrentUsingScope { get; }

    public ICompilation Compilation => CurrentAssembly.Compilation;

    public IAssembly CurrentAssembly { get; }

    public ITypeDefinition CurrentTypeDefinition { get; }

    public IMember CurrentMember { get; }

    ITypeResolveContext ITypeResolveContext.WithCurrentTypeDefinition(ITypeDefinition typeDefinition) =>
        WithCurrentTypeDefinition(typeDefinition);

    ITypeResolveContext ITypeResolveContext.WithCurrentMember(IMember member) => WithCurrentMember(member);

    public CSharpTypeResolveContext WithCurrentTypeDefinition(ITypeDefinition typeDefinition) =>
        new(CurrentAssembly, CurrentUsingScope, typeDefinition, CurrentMember, _methodTypeParameterNames);

    public CSharpTypeResolveContext WithCurrentMember(IMember member) =>
        new(CurrentAssembly, CurrentUsingScope, CurrentTypeDefinition, member, _methodTypeParameterNames);

    public CSharpTypeResolveContext WithUsingScope(ResolvedUsingScope usingScope) =>
        new(CurrentAssembly, usingScope, CurrentTypeDefinition, CurrentMember, _methodTypeParameterNames);
}