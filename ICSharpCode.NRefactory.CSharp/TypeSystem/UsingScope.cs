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

namespace ICSharpCode.NRefactory.CSharp.TypeSystem;

/// <summary>
/// Represents a scope that contains "using" statements.
/// This is either the file itself, or a namespace declaration.
/// </summary>
[Serializable]
public class UsingScope : AbstractFreezable
{
    private DomRegion _region;
    private IList<TypeOrNamespaceReference> _usings;
    private IList<KeyValuePair<string, TypeOrNamespaceReference>> _usingAliases;
    private IList<string> _externAliases;

    /// <summary>
    /// Creates a new root using scope.
    /// </summary>
    public UsingScope()
    {
    }

    /// <summary>
    /// Creates a new nested using scope.
    /// </summary>
    /// <param name="parent">The parent using scope.</param>
    /// <param name="shortName">The short namespace name.</param>
    public UsingScope(UsingScope parent, string shortName)
    {
        Parent = parent ?? throw new ArgumentNullException(nameof(parent));
        ShortNamespaceName = shortName ?? throw new ArgumentNullException(nameof(shortName));
    }

    public UsingScope Parent { get; }

    public DomRegion Region
    {
        get => _region;
        set
        {
            FreezableHelper.ThrowIfFrozen(this);
            _region = value;
        }
    }

    public string ShortNamespaceName { get; } = "";

    public string NamespaceName =>
        Parent != null
            ? NamespaceDeclaration.BuildQualifiedName(Parent.NamespaceName, ShortNamespaceName)
            : ShortNamespaceName;

    //			set {
    //				if (value == null)
    //					throw new ArgumentNullException("NamespaceName");
    //				FreezableHelper.ThrowIfFrozen(this);
    //				namespaceName = value;
    //			}

    public IList<TypeOrNamespaceReference> Usings => _usings ??= new List<TypeOrNamespaceReference>();

    public IList<KeyValuePair<string, TypeOrNamespaceReference>> UsingAliases =>
        _usingAliases ??= new List<KeyValuePair<string, TypeOrNamespaceReference>>();

    public IList<string> ExternAliases => _externAliases ??= new List<string>();

    //		public IList<UsingScope> ChildScopes {
    //			get {
    //				if (childScopes == null)
    //					childScopes = new List<UsingScope>();
    //				return childScopes;
    //			}
    //		}

    protected override void FreezeInternal()
    {
        _usings = FreezableHelper.FreezeList(_usings);
        _usingAliases = FreezableHelper.FreezeList(_usingAliases);
        _externAliases = FreezableHelper.FreezeList(_externAliases);

        // In current model (no child scopes), it makes sense to freeze the parent as well
        // to ensure the whole lookup chain is immutable.
        Parent?.Freeze();
        base.FreezeInternal();
    }

    /// <summary>
    /// Gets whether this using scope has an alias (either using or extern)
    /// with the specified name.
    /// </summary>
    public bool HasAlias(string identifier) =>
        _usingAliases != null && _usingAliases.Any(pair => pair.Key == identifier) ||
        _externAliases != null && _externAliases.Contains(identifier);

    /// <summary>
    /// Resolves the namespace represented by this using scope.
    /// </summary>
    public ResolvedUsingScope Resolve(ICompilation compilation)
    {
        if (compilation.CacheManager is var cache && cache.GetShared(this) is not ResolvedUsingScope resolved)
        {
            var csContext = new CSharpTypeResolveContext(compilation.MainAssembly, Parent?.Resolve(compilation));
            resolved = (ResolvedUsingScope)cache.GetOrAddShared(this, new ResolvedUsingScope(csContext, this));
        }

        return resolved;
    }
}