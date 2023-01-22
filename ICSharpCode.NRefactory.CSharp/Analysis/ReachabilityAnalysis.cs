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
using System.Threading;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.CSharp.TypeSystem;
using ICSharpCode.NRefactory.Semantics;

namespace ICSharpCode.NRefactory.CSharp.Analysis;

/// <summary>
/// Statement reachability analysis.
/// </summary>
public sealed class ReachabilityAnalysis
{
    private readonly HashSet<Statement> _reachableStatements = new();
    private readonly HashSet<Statement> _reachableEndPoints = new();
    private HashSet<ControlFlowNode> _visitedNodes = new();
    private Stack<ControlFlowNode> _stack = new();
    private RecursiveDetectorVisitor _recursiveDetectorVisitor;

    public static ReachabilityAnalysis Create(Statement statement, CSharpAstResolver resolver = null,
        RecursiveDetectorVisitor recursiveDetectorVisitor = null, CancellationToken cancellationToken = default)
    {
        var cfgBuilder = new ControlFlowGraphBuilder();
        var cfg = cfgBuilder.BuildControlFlowGraph(statement, resolver, cancellationToken);
        return Create(cfg, recursiveDetectorVisitor, cancellationToken);
    }

    internal static ReachabilityAnalysis Create(Statement statement, Func<AstNode, CancellationToken, ResolveResult> resolver,
        CSharpTypeResolveContext typeResolveContext, CancellationToken cancellationToken)
    {
        var cfgBuilder = new ControlFlowGraphBuilder();
        var cfg = cfgBuilder.BuildControlFlowGraph(statement, resolver, typeResolveContext, cancellationToken);
        return Create(cfg, cancellationToken: cancellationToken);
    }

    public static ReachabilityAnalysis Create(IList<ControlFlowNode> controlFlowGraph, RecursiveDetectorVisitor recursiveDetectorVisitor = null,
        CancellationToken cancellationToken = default)
    {
        if (controlFlowGraph == null)
            throw new ArgumentNullException(nameof(controlFlowGraph));

        var reachabilityAnalysis = new ReachabilityAnalysis
        {
            _recursiveDetectorVisitor = recursiveDetectorVisitor
        };

        // Analysing a null node can result in an empty control flow graph
        if (controlFlowGraph.Count > 0)
        {
            reachabilityAnalysis._stack.Push(controlFlowGraph[0]);

            while (reachabilityAnalysis._stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                reachabilityAnalysis.MarkReachable(reachabilityAnalysis._stack.Pop());
            }
        }

        reachabilityAnalysis._stack = null;
        reachabilityAnalysis._visitedNodes = null;
        return reachabilityAnalysis;
    }

    private void MarkReachable(ControlFlowNode node)
    {
        if (node.PreviousStatement != null)
        {
            if (node.PreviousStatement is LabelStatement)
                _reachableStatements.Add(node.PreviousStatement);

            _reachableEndPoints.Add(node.PreviousStatement);
        }

        if (node.NextStatement != null)
        {
            _reachableStatements.Add(node.NextStatement);

            if (IsRecursive(node.NextStatement))
                return;
        }

        foreach (var edge in node.Outgoing)
            if (_visitedNodes.Add(edge.To))
                _stack.Push(edge.To);
    }

    private bool IsRecursive(Statement statement) => _recursiveDetectorVisitor != null && statement.AcceptVisitor(_recursiveDetectorVisitor);

    public IEnumerable<Statement> ReachableStatements => _reachableStatements;

    public bool IsReachable(Statement statement) => _reachableStatements.Contains(statement);

    public bool IsEndpointReachable(Statement statement) => _reachableEndPoints.Contains(statement);

    public class RecursiveDetectorVisitor : DepthFirstAstVisitor<bool>
    {
        public override bool VisitConditionalExpression(ConditionalExpression conditionalExpression) =>
            conditionalExpression.Condition.AcceptVisitor(this) ||
            conditionalExpression.TrueExpression.AcceptVisitor(this) &&
            conditionalExpression.FalseExpression.AcceptVisitor(this);

        public override bool VisitBinaryOperatorExpression(BinaryOperatorExpression binaryOperatorExpression) =>
            binaryOperatorExpression.Operator == BinaryOperatorType.NullCoalescing
                ? binaryOperatorExpression.Left.AcceptVisitor(this)
                : base.VisitBinaryOperatorExpression(binaryOperatorExpression);

        public override bool VisitIfElseStatement(IfElseStatement ifElseStatement) =>
            ifElseStatement.Condition.AcceptVisitor(this) ||
            // No need to worry about null ast nodes, since AcceptVisitor will just return false in those cases
            ifElseStatement.TrueStatement.AcceptVisitor(this) &&
            ifElseStatement.FalseStatement.AcceptVisitor(this);

        public override bool VisitForeachStatement(ForeachStatement foreachStatement) =>
            // Even if the body is always recursive, the function may stop if the collection is empty.
            foreachStatement.InExpression.AcceptVisitor(this);

        public override bool VisitForStatement(ForStatement forStatement) =>
            forStatement.Initializers.Any(initializer => initializer.AcceptVisitor(this)) ||
            forStatement.Condition.AcceptVisitor(this);

        public override bool VisitSwitchStatement(SwitchStatement switchStatement)
        {
            if (switchStatement.Expression.AcceptVisitor(this))
                return true;

            var foundDefault = false;

            foreach (var section in switchStatement.SwitchSections)
            {
                foundDefault = foundDefault || section.CaseLabels.Any(label => label.Expression.IsNull);

                if (!section.AcceptVisitor(this))
                    return false;
            }

            return foundDefault;
        }

        public override bool VisitBlockStatement(BlockStatement blockStatement) =>
            // If the block has a recursive statement, then that statement will be visited
            // individually by the CFG construction algorithm later.
            false;

        protected override bool VisitChildren(AstNode node) => VisitNodeList(node.Children);

        private bool VisitNodeList(IEnumerable<AstNode> nodes) => nodes.Any(node => node.AcceptVisitor(this));

        public override bool VisitQueryExpression(QueryExpression queryExpression) =>
            // We only care about the first from clause because:
            // in "from x in Method() select x", Method() might be recursive
            // but in "from x in Bar() from y in Method() select x + y", even if Method() is recursive
            // Bar might still be empty.
            queryExpression.Clauses.OfType<QueryFromClause>().FirstOrDefault() is var queryFromClause &&
            (queryFromClause == null || queryFromClause.AcceptVisitor(this));
    }
}