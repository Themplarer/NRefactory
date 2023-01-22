using System;
using System.Collections.Generic;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;

namespace ICSharpCode.NRefactory.CSharp.TypeSystem.ConstantValues;

[Serializable]
public abstract class ConstantExpression : IConstantValue
{
    public abstract ResolveResult Resolve(CSharpResolver resolver);

    public ResolveResult Resolve(ITypeResolveContext context)
    {
        var csContext = (CSharpTypeResolveContext)context;

        if (context.CurrentAssembly != context.Compilation.MainAssembly)
        {
            // The constant needs to be resolved in a different compilation.
            var pc = context.CurrentAssembly as IProjectContent;

            if (pc != null && context.Compilation.SolutionSnapshot.GetCompilation(pc) is { } nestedCompilation)
            {
                var nestedContext = MapToNestedCompilation(csContext, nestedCompilation);
                var rr = Resolve(new CSharpResolver(nestedContext));
                return MapToNewContext(rr, context);
            }
        }

        return Resolve(new CSharpResolver(csContext));
    }

    private CSharpTypeResolveContext MapToNestedCompilation(CSharpTypeResolveContext context, ICompilation nestedCompilation)
    {
        var nestedContext = new CSharpTypeResolveContext(nestedCompilation.MainAssembly);

        if (context.CurrentUsingScope != null)
            nestedContext = nestedContext.WithUsingScope(context.CurrentUsingScope.UnresolvedUsingScope.Resolve(nestedCompilation));

        if (context.CurrentTypeDefinition != null)
            nestedContext = nestedContext.WithCurrentTypeDefinition(nestedCompilation.Import(context.CurrentTypeDefinition));

        return nestedContext;
    }

    private static ResolveResult MapToNewContext(ResolveResult rr, ITypeResolveContext newContext) =>
        rr switch
        {
            TypeOfResolveResult result => new TypeOfResolveResult(result.Type.ToTypeReference().Resolve(newContext),
                result.ReferencedType.ToTypeReference().Resolve(newContext)),
            ArrayCreateResolveResult arrayCreateResolveResult => new ArrayCreateResolveResult(
                arrayCreateResolveResult.Type.ToTypeReference().Resolve(newContext),
                MapToNewContext(arrayCreateResolveResult.SizeArguments, newContext),
                MapToNewContext(arrayCreateResolveResult.InitializerElements, newContext)),
            _ => rr.IsCompileTimeConstant
                ? new ConstantResolveResult(rr.Type.ToTypeReference().Resolve(newContext), rr.ConstantValue)
                : new ErrorResolveResult(rr.Type.ToTypeReference().Resolve(newContext))
        };

    private static ResolveResult[] MapToNewContext(IList<ResolveResult> input, ITypeResolveContext newContext)
    {
        if (input == null)
            return null;

        var output = new ResolveResult[input.Count];

        for (var i = 0; i < output.Length; i++)
            output[i] = MapToNewContext(input[i], newContext);

        return output;
    }
}