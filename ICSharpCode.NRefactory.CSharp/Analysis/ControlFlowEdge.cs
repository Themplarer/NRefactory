using System;
using System.Collections.Generic;
using System.Linq;

namespace ICSharpCode.NRefactory.CSharp.Analysis;

public class ControlFlowEdge
{
    public readonly ControlFlowNode From;
    public readonly ControlFlowNode To;
    public readonly ControlFlowEdgeType Type;

    private List<TryCatchStatement> _jumpOutOfTryFinally;

    public ControlFlowEdge(ControlFlowNode from, ControlFlowNode to, ControlFlowEdgeType type)
    {
        From = from ?? throw new ArgumentNullException(nameof(from));
        To = to ?? throw new ArgumentNullException(nameof(to));
        Type = type;
    }

    internal void AddJumpOutOfTryFinally(TryCatchStatement tryFinally)
    {
        _jumpOutOfTryFinally ??= new List<TryCatchStatement>();
        _jumpOutOfTryFinally.Add(tryFinally);
    }

    /// <summary>
    /// Gets whether this control flow edge is leaving any try-finally statements.
    /// </summary>
    public bool IsLeavingTryFinally => _jumpOutOfTryFinally != null;

    /// <summary>
    /// Gets the try-finally statements that this control flow edge is leaving.
    /// </summary>
    public IEnumerable<TryCatchStatement> TryFinallyStatements => _jumpOutOfTryFinally ?? Enumerable.Empty<TryCatchStatement>();
}