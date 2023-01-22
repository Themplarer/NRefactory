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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.Utils;

namespace ICSharpCode.NRefactory.CSharp.TypeSystem;

/// <summary>
/// Resolved version of using scope.
/// </summary>
public class ResolvedUsingScope
{
    private readonly CSharpTypeResolveContext _parentContext;
    private readonly UsingScope _usingScope;
    private INamespace _namespace;
    private IList<INamespace> _usings;
    private IList<KeyValuePair<string, ResolveResult>> _usingAliases;

    internal readonly ConcurrentDictionary<string, ResolveResult> ResolveCache = new();
    internal List<List<IMethod>> AllExtensionMethods;

    public ResolvedUsingScope(CSharpTypeResolveContext context, UsingScope usingScope)
    {
        _parentContext = context ?? throw new ArgumentNullException(nameof(context));
        _usingScope = usingScope ?? throw new ArgumentNullException(nameof(usingScope));

        if (usingScope.Parent != null)
        {
            if (context.CurrentUsingScope == null)
                throw new InvalidOperationException();
        }
        else
        {
            if (context.CurrentUsingScope != null)
                throw new InvalidOperationException();
        }
    }

    public UsingScope UnresolvedUsingScope => _usingScope;

    public INamespace Namespace => LazyInit.VolatileRead(ref _namespace) ?? GetNamespace();

    public ResolvedUsingScope Parent => _parentContext.CurrentUsingScope;

    public IList<INamespace> Usings => LazyInit.VolatileRead(ref _usings) ?? GetNamespaces();

    public IList<KeyValuePair<string, ResolveResult>> UsingAliases => LazyInit.VolatileRead(ref _usingAliases) ?? GetStringAndResultsList();

    public IList<string> ExternAliases => _usingScope.ExternAliases;

    /// <summary>
    /// Gets whether this using scope has an alias (either using or extern)
    /// with the specified name.
    /// </summary>
    public bool HasAlias(string identifier) => _usingScope.HasAlias(identifier);

    private INamespace GetNamespace()
    {
        var @namespace = _parentContext.CurrentUsingScope != null
            ? _parentContext.CurrentUsingScope.Namespace.GetChildNamespace(_usingScope.ShortNamespaceName) ??
              new DummyNamespace(_parentContext.CurrentUsingScope.Namespace, _usingScope.ShortNamespaceName)
            : _parentContext.Compilation.RootNamespace;

        Debug.Assert(@namespace != null);
        return LazyInit.GetOrSet(ref _namespace, @namespace);
    }

    private IList<INamespace> GetNamespaces()
    {
        var set = new HashSet<INamespace>();
        var resolver = new CSharpResolver(_parentContext.WithUsingScope(this));

        foreach (var u in _usingScope.Usings)
            if (u.ResolveNamespace(resolver) is { } ns && !set.Contains(ns))
                set.Add(ns);

        return LazyInit.GetOrSet(ref _usings, new ReadOnlyCollection<INamespace>(set.ToArray()));
    }

    private IList<KeyValuePair<string, ResolveResult>> GetStringAndResultsList()
    {
        var resolver = new CSharpResolver(_parentContext.WithUsingScope(this));
        var result = _usingScope.UsingAliases
            .Select(p => KeyValuePair.Create(p.Key, ResolveResult(p.Key, p.Value.Resolve(resolver))))
            .ToArray();
        return LazyInit.GetOrSet(ref _usingAliases, result);
    }

    private static ResolveResult ResolveResult(string key, ResolveResult result) =>
        result switch
        {
            TypeResolveResult resolveResult => new AliasTypeResolveResult(key, resolveResult),
            NamespaceResolveResult namespaceResolveResult => new AliasNamespaceResolveResult(key, namespaceResolveResult),
            _ => result
        };

    private sealed class DummyNamespace : INamespace
    {
        private readonly INamespace _parentNamespace;

        public DummyNamespace(INamespace parentNamespace, string name)
        {
            _parentNamespace = parentNamespace;
            Name = name;
        }

        public string ExternAlias { get; set; }

        string INamespace.FullName => NamespaceDeclaration.BuildQualifiedName(_parentNamespace.FullName, Name);

        public string Name { get; }

        SymbolKind ISymbol.SymbolKind => SymbolKind.Namespace;

        INamespace INamespace.ParentNamespace => _parentNamespace;

        IEnumerable<INamespace> INamespace.ChildNamespaces => EmptyList<INamespace>.Instance;

        IEnumerable<ITypeDefinition> INamespace.Types => EmptyList<ITypeDefinition>.Instance;

        IEnumerable<IAssembly> INamespace.ContributingAssemblies => EmptyList<IAssembly>.Instance;

        ICompilation ICompilationProvider.Compilation => _parentNamespace.Compilation;

        INamespace INamespace.GetChildNamespace(string name) => null;

        ITypeDefinition INamespace.GetTypeDefinition(string name, int typeParameterCount) => null;

        public ISymbolReference ToReference() => new MergedNamespaceReference(ExternAlias, ((INamespace)this).FullName);
    }
}