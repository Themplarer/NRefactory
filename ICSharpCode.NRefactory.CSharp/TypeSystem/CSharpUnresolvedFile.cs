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
using ICSharpCode.NRefactory.Documentation;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using System.Linq;

namespace ICSharpCode.NRefactory.CSharp.TypeSystem;

/// <summary>
/// Represents a file that was parsed and converted for the type system.
/// </summary>
[Serializable]
public class CSharpUnresolvedFile : AbstractFreezable, IUnresolvedFile, IUnresolvedDocumentationProvider
{
    // The 'FastSerializerVersion' attribute on CSharpUnresolvedFile must be incremented when fixing 
    // bugs in the TypeSystemConvertVisitor

    private string _fileName = string.Empty;
    private DateTime? _lastWriteTime;
    private IList<Error> _errors = new List<Error>();
    private Dictionary<IUnresolvedEntity, string> _documentation;

    protected override void FreezeInternal()
    {
        base.FreezeInternal();
        RootUsingScope.Freeze();
        TopLevelTypeDefinitions = FreezableHelper.FreezeListAndElements(TopLevelTypeDefinitions);
        AssemblyAttributes = FreezableHelper.FreezeListAndElements(AssemblyAttributes);
        ModuleAttributes = FreezableHelper.FreezeListAndElements(ModuleAttributes);
        UsingScopes = FreezableHelper.FreezeListAndElements(UsingScopes);
    }

    public string FileName
    {
        get => _fileName;
        set
        {
            FreezableHelper.ThrowIfFrozen(this);
            _fileName = value ?? string.Empty;
        }
    }

    public DateTime? LastWriteTime
    {
        get => _lastWriteTime;
        set
        {
            FreezableHelper.ThrowIfFrozen(this);
            _lastWriteTime = value;
        }
    }

    public UsingScope RootUsingScope { get; } = new();

    public IList<Error> Errors
    {
        get => _errors;
        internal set => _errors = (List<Error>)value;
    }

    public IList<UsingScope> UsingScopes { get; private set; } = new List<UsingScope>();

    public IList<IUnresolvedTypeDefinition> TopLevelTypeDefinitions { get; private set; } = new List<IUnresolvedTypeDefinition>();

    public IList<IUnresolvedAttribute> AssemblyAttributes { get; private set; } = new List<IUnresolvedAttribute>();

    public IList<IUnresolvedAttribute> ModuleAttributes { get; private set; } = new List<IUnresolvedAttribute>();

    public void AddDocumentation(IUnresolvedEntity entity, string xmlDocumentation)
    {
        FreezableHelper.ThrowIfFrozen(this);
        _documentation ??= new Dictionary<IUnresolvedEntity, string>();
        _documentation.Add(entity, xmlDocumentation);
    }

    public UsingScope GetUsingScope(TextLocation location)
    {
        foreach (var scope in UsingScopes)
            if (scope.Region.IsInside(location.Line, location.Column))
                return scope;

        return RootUsingScope;
    }

    public IUnresolvedTypeDefinition GetInnermostTypeDefinition(TextLocation location)
    {
        IUnresolvedTypeDefinition parent = null;
        var type = GetTopLevelTypeDefinition(location);

        while (type != null)
        {
            parent = type;
            type = FindEntity(parent.NestedTypes, location);
        }

        return parent;
    }

    public IUnresolvedTypeDefinition GetTopLevelTypeDefinition(TextLocation location) => FindEntity(TopLevelTypeDefinitions, location);

    public IUnresolvedMember GetMember(TextLocation location) =>
        GetInnermostTypeDefinition(location) is { } type
            ? FindEntity(type.Members, location)
            : null;

    public CSharpTypeResolveContext GetTypeResolveContext(ICompilation compilation, TextLocation loc)
    {
        var typeResolveContext = new CSharpTypeResolveContext(compilation.MainAssembly)
            .WithUsingScope(GetUsingScope(loc).Resolve(compilation));

        if (GetInnermostTypeDefinition(loc) is { } curDef)
        {
            var resolvedDef = curDef.Resolve(typeResolveContext).GetDefinition();

            if (resolvedDef == null)
                return typeResolveContext;

            typeResolveContext = typeResolveContext.WithCurrentTypeDefinition(resolvedDef);

            if (resolvedDef.Members.FirstOrDefault(m => m.Region.FileName == FileName && m.Region.Begin <= loc && loc < m.BodyRegion.End) is
                { } curMember)
                typeResolveContext = typeResolveContext.WithCurrentMember(curMember);
        }

        return typeResolveContext;
    }

    public Resolver.CSharpResolver GetResolver(ICompilation compilation, TextLocation loc) => new(GetTypeResolveContext(compilation, loc));

    public string GetDocumentation(IUnresolvedEntity entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        return _documentation is null || !_documentation.TryGetValue(entity, out var xmlDoc)
            ? null
            : xmlDoc;
    }

    public DocumentationComment GetDocumentation(IUnresolvedEntity entity, IEntity resolvedEntity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        if (resolvedEntity == null)
            throw new ArgumentNullException(nameof(resolvedEntity));

        var xmlDoc = GetDocumentation(entity);

        if (xmlDoc == null)
            return null;

        var unresolvedTypeDef = entity as IUnresolvedTypeDefinition ?? entity.DeclaringTypeDefinition;
        var resolvedTypeDef = resolvedEntity as ITypeDefinition ?? resolvedEntity.DeclaringTypeDefinition;

        if (unresolvedTypeDef != null && resolvedTypeDef != null)
        {
            // Strictly speaking, we would have to pass the parent context into CreateResolveContext,
            // then transform the result using WithTypeDefinition().
            // However, we can simplify this here because we know this is a C# type definition.
            var context = unresolvedTypeDef.CreateResolveContext(new SimpleTypeResolveContext(resolvedTypeDef));

            if (resolvedEntity is IMember member)
                context = context.WithCurrentMember(member);

            return new CSharpDocumentationComment(xmlDoc, context);
        }

        return new DocumentationComment(xmlDoc, new SimpleTypeResolveContext(resolvedEntity));
    }

    private static T FindEntity<T>(IEnumerable<T> list, TextLocation location) where T : class, IUnresolvedEntity =>
        // This could be improved using a binary search
        list.FirstOrDefault(entity => entity.Region.IsInside(location.Line, location.Column));
}