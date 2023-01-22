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
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.Utils;

namespace ICSharpCode.NRefactory.CSharp.TypeSystem;

[Serializable]
public sealed class CSharpAttribute : IUnresolvedAttribute
{
    private IList<IConstantValue> _positionalArguments;
    private IList<KeyValuePair<string, IConstantValue>> _namedCtorArguments;
    private IList<KeyValuePair<string, IConstantValue>> _namedArguments;

    public CSharpAttribute(ITypeReference attributeType, DomRegion region, IList<IConstantValue> positionalArguments,
        IList<KeyValuePair<string, IConstantValue>> namedCtorArguments, IList<KeyValuePair<string, IConstantValue>> namedArguments)
    {
        AttributeType = attributeType ?? throw new ArgumentNullException(nameof(attributeType));
        Region = region;
        _positionalArguments = positionalArguments ?? EmptyList<IConstantValue>.Instance;
        _namedCtorArguments = namedCtorArguments ?? EmptyList<KeyValuePair<string, IConstantValue>>.Instance;
        _namedArguments = namedArguments ?? EmptyList<KeyValuePair<string, IConstantValue>>.Instance;
    }

    public DomRegion Region { get; }

    public ITypeReference AttributeType { get; }

    public IAttribute CreateResolvedAttribute(ITypeResolveContext context) =>
        new CSharpResolvedAttribute((CSharpTypeResolveContext)context, this);

    private sealed class CSharpResolvedAttribute : IAttribute
    {
        private readonly CSharpTypeResolveContext _context;
        private readonly CSharpAttribute _unresolved;
        private readonly IType _attributeType;

        private ResolveResult _ctorInvocation;
        private IList<ResolveResult> _localPositionalArguments;
        private IList<KeyValuePair<IMember, ResolveResult>> _localNamedArguments;

        public CSharpResolvedAttribute(CSharpTypeResolveContext context, CSharpAttribute unresolved)
        {
            _context = context;
            _unresolved = unresolved;
            // Pretty much any access to the attribute checks the type first, so
            // we don't need to use lazy-loading for that.
            _attributeType = unresolved.AttributeType.Resolve(context);
        }

        DomRegion IAttribute.Region => _unresolved.Region;

        IType IAttribute.AttributeType => _attributeType;

        IMethod IAttribute.Constructor => GetCtorInvocation()?.Member as IMethod;

        IList<ResolveResult> IAttribute.PositionalArguments =>
            LazyInit.VolatileRead(ref _localPositionalArguments) ?? GetResolveResults();

        IList<KeyValuePair<IMember, ResolveResult>> IAttribute.NamedArguments =>
            LazyInit.VolatileRead(ref _localNamedArguments) ?? GetListFromNamedArgs();

        private InvocationResolveResult GetCtorInvocation() =>
            LazyInit.VolatileRead(ref _ctorInvocation) is { } resolveResult
                ? resolveResult as InvocationResolveResult
                : GetInvocationResolveResult();

        private InvocationResolveResult GetInvocationResolveResult()
        {
            var resolver = new CSharpResolver(_context);
            var totalArgumentCount = _unresolved._positionalArguments.Count + _unresolved._namedCtorArguments.Count;
            var arguments = new ResolveResult[totalArgumentCount];
            var argumentNames = new string[totalArgumentCount];
            var index = 0;

            for (; index < _unresolved._positionalArguments.Count; index++)
                arguments[index] = _unresolved._positionalArguments[index].Resolve(_context);

            foreach (var (key, value) in _unresolved._namedCtorArguments)
            {
                argumentNames[index] = key;
                arguments[index] = value.Resolve(_context);
                index++;
            }

            var resolveResult = resolver.ResolveObjectCreation(_attributeType, arguments, argumentNames);
            return LazyInit.GetOrSet(ref _ctorInvocation, resolveResult) as InvocationResolveResult;
        }

        private IList<ResolveResult> GetResolveResults()
        {
            var invocation = GetCtorInvocation();
            var results = invocation != null
                ? invocation.GetArgumentsForCall()
                : EmptyList<ResolveResult>.Instance;
            return LazyInit.GetOrSet(ref _localPositionalArguments, results);
        }

        private IList<KeyValuePair<IMember, ResolveResult>> GetListFromNamedArgs()
        {
            var keyValuePairs = _unresolved._namedArguments
                .Select(p => KeyValuePair.Create(GetMemberOrDefault(p.Key), p.Value.Resolve(_context)))
                .Where(p => p.Key is { })
                .ToArray();

            return LazyInit.GetOrSet(ref _localNamedArguments, keyValuePairs);
        }

        private IMember GetMemberOrDefault(string key) =>
            _attributeType
                .GetMembers(m => m.SymbolKind is SymbolKind.Field or SymbolKind.Property && m.Name == key)
                .FirstOrDefault();
    }
}