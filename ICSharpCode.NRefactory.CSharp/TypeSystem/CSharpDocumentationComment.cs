﻿// Copyright (c) 2010-2013 AlphaSierraPapa for the SharpDevelop Team
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

using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Documentation;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;

namespace ICSharpCode.NRefactory.CSharp.TypeSystem;

/// <summary>
/// DocumentationComment with C# cref lookup.
/// </summary>
sealed class CSharpDocumentationComment : DocumentationComment
{
    public CSharpDocumentationComment(string xmlDoc, ITypeResolveContext context) : base(xmlDoc, context)
    {
    }

    public override IEntity ResolveCref(string cref)
    {
        if (cref.Length > 2 && cref[1] == ':')
            // resolve ID string
            return base.ResolveCref(cref);

        var documentationReference = new DocumentationReference(); //new CSharpParser().ParseDocumentationReference(cref);
        var resolver = context is CSharpTypeResolveContext csharpContext
            ? new CSharpResolver(csharpContext)
            : new CSharpResolver(context.Compilation);
        var astResolver = new CSharpAstResolver(resolver, documentationReference);

        return astResolver.Resolve(documentationReference) switch
        {
            MemberResolveResult mrr => mrr.Member,
            TypeResolveResult trr => trr.Type.GetDefinition(),
            _ => null
        };
    }
}