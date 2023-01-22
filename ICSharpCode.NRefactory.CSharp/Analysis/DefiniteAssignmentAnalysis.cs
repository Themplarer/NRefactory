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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using ICSharpCode.NRefactory.Utils;

namespace ICSharpCode.NRefactory.CSharp.Analysis;

/// <summary>
/// Implements the C# definite assignment analysis (C# 4.0 Spec: §5.3 Definite assignment)
/// </summary>
public class DefiniteAssignmentAnalysis
{
    private readonly DefiniteAssignmentVisitor _visitor = new();
    private readonly List<DefiniteAssignmentNode> _allNodes = new();
    private readonly Dictionary<Statement, DefiniteAssignmentNode> _beginNodeDict = new();
    private readonly Dictionary<Statement, DefiniteAssignmentNode> _endNodeDict = new();
    private readonly Dictionary<Statement, DefiniteAssignmentNode> _conditionNodeDict = new();
    private readonly Dictionary<ControlFlowEdge, DefiniteAssignmentStatus> _edgeStatus = new();
    private readonly List<IdentifierExpression> _unassignedVariableUses = new();
    private readonly Queue<DefiniteAssignmentNode> _nodesWithModifiedInput = new();

    private readonly CSharpAstResolver _resolver;
    private string _variableName;
    private int _analyzedRangeStart, _analyzedRangeEnd;
    private CancellationToken _analysisCancellationToken;

    public DefiniteAssignmentAnalysis(Statement rootStatement, CancellationToken cancellationToken)
        : this(rootStatement,
            new CSharpAstResolver(new CSharpResolver(MinimalCorlib.Instance.CreateCompilation()), rootStatement),
            cancellationToken)
    {
    }

    public DefiniteAssignmentAnalysis(Statement rootStatement, CSharpAstResolver resolver, CancellationToken cancellationToken)
    {
        if (rootStatement == null)
            throw new ArgumentNullException(nameof(rootStatement));

        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _visitor.Analysis = this;
        var cfgBuilder = new DerivedControlFlowGraphBuilder();

        if (resolver.TypeResolveContext.Compilation.MainAssembly.UnresolvedAssembly is MinimalCorlib)
            cfgBuilder.EvaluateOnlyPrimitiveConstants = true;

        _allNodes.AddRange(cfgBuilder.BuildControlFlowGraph(rootStatement, resolver, cancellationToken).Cast<DefiniteAssignmentNode>());

        for (var i = 0; i < _allNodes.Count; i++)
        {
            var node = _allNodes[i];
            node.Index = i; // assign numbers to the nodes

            if (node.Type is ControlFlowNodeType.StartNode or ControlFlowNodeType.BetweenStatements)
                // Anonymous methods have separate control flow graphs, but we also need to analyze those.
                // Iterate backwards so that anonymous methods are inserted in the correct order
                for (var child = node.NextStatement.LastChild; child != null; child = child.PrevSibling)
                    InsertAnonymousMethods(i + 1, child, cfgBuilder, cancellationToken);

            // Now register the node in the dictionaries:
            if (node.Type is ControlFlowNodeType.StartNode or ControlFlowNodeType.BetweenStatements)
                _beginNodeDict.Add(node.NextStatement, node);

            if (node.Type is ControlFlowNodeType.BetweenStatements or ControlFlowNodeType.EndNode)
                _endNodeDict.Add(node.PreviousStatement, node);

            if (node.Type is ControlFlowNodeType.LoopCondition)
                _conditionNodeDict.Add(node.NextStatement, node);
        }

        // Verify that we created nodes for all statements:
        Debug.Assert(!rootStatement.DescendantsAndSelf.OfType<Statement>().Except(_allNodes.Select(n => n.NextStatement)).Any());
        // Verify that we put all nodes into the dictionaries:
        Debug.Assert(rootStatement.DescendantsAndSelf.OfType<Statement>().All(stmt => _beginNodeDict.ContainsKey(stmt)));
        Debug.Assert(rootStatement.DescendantsAndSelf.OfType<Statement>().All(stmt => _endNodeDict.ContainsKey(stmt)));

        _analyzedRangeStart = 0;
        _analyzedRangeEnd = _allNodes.Count - 1;
    }

    private void InsertAnonymousMethods(int insertPos, AstNode node, ControlFlowGraphBuilder cfgBuilder, CancellationToken cancellationToken)
    {
        // Ignore any statements, as those have their own ControlFlowNode and get handled separately
        if (node is Statement)
            return;

        if (node is AnonymousMethodExpression ame)
        {
            _allNodes.InsertRange(insertPos, cfgBuilder.BuildControlFlowGraph(ame.Body, _resolver, cancellationToken).Cast<DefiniteAssignmentNode>());
            return;
        }

        if (node is LambdaExpression { Body: Statement body })
        {
            _allNodes.InsertRange(insertPos, cfgBuilder.BuildControlFlowGraph(body, _resolver, cancellationToken).Cast<DefiniteAssignmentNode>());
            return;
        }

        // Descend into child expressions
        // Iterate backwards so that anonymous methods are inserted in the correct order
        for (var child = node.LastChild; child != null; child = child.PrevSibling)
            InsertAnonymousMethods(insertPos, child, cfgBuilder, cancellationToken);
    }

    /// <summary>
    /// Gets the unassigned usages of the previously analyzed variable.
    /// </summary>
    public IList<IdentifierExpression> UnassignedVariableUses => _unassignedVariableUses.AsReadOnly();

    /// <summary>
    /// Sets the range of statements to be analyzed.
    /// This method can be used to restrict the analysis to only a part of the method.
    /// Only the control flow paths that are fully contained within the selected part will be analyzed.
    /// </summary>
    /// <remarks>By default, both 'start' and 'end' are inclusive.</remarks>
    public void SetAnalyzedRange(Statement start, Statement end, bool startInclusive = true, bool endInclusive = true)
    {
        var dictForStart = startInclusive ? _beginNodeDict : _endNodeDict;
        var dictForEnd = endInclusive ? _endNodeDict : _beginNodeDict;
        Debug.Assert(dictForStart.ContainsKey(start) && dictForEnd.ContainsKey(end));
        var startIndex = dictForStart[start].Index;
        var endIndex = dictForEnd[end].Index;

        if (startIndex > endIndex)
            throw new ArgumentException("The start statement must be lexically preceding the end statement");

        _analyzedRangeStart = startIndex;
        _analyzedRangeEnd = endIndex;
    }

    public void Analyze(string variable, CancellationToken cancellationToken,
        DefiniteAssignmentStatus initialStatus = DefiniteAssignmentStatus.PotentiallyAssigned)
    {
        _analysisCancellationToken = cancellationToken;
        _variableName = variable;

        try
        {
            // Reset the status:
            _unassignedVariableUses.Clear();

            foreach (var node in _allNodes)
            {
                node.NodeStatus = DefiniteAssignmentStatus.CodeUnreachable;

                foreach (var edge in node.Outgoing)
                    _edgeStatus[edge] = DefiniteAssignmentStatus.CodeUnreachable;
            }

            ChangeNodeStatus(_allNodes[_analyzedRangeStart], initialStatus);
            // Iterate as long as the input status of some nodes is changing:
            var count = 0;
            const int hackPollCount = 0x00200000;

            while (_nodesWithModifiedInput.Count > 0)
            {
                if (count++ >= hackPollCount)
                {
                    _analysisCancellationToken.ThrowIfCancellationRequested();
                    count = 0;
                }

                var node = _nodesWithModifiedInput.Dequeue();
                var inputStatus = node.Incoming.Aggregate(DefiniteAssignmentStatus.CodeUnreachable,
                    (current, edge) => MergeStatus(current, _edgeStatus[edge]));

                ChangeNodeStatus(node, inputStatus);
            }
        }
        finally
        {
            _analysisCancellationToken = CancellationToken.None;
            _variableName = null;
        }
    }

    public DefiniteAssignmentStatus GetStatusBefore(Statement statement) => _beginNodeDict[statement].NodeStatus;

    public DefiniteAssignmentStatus GetStatusAfter(Statement statement) => _endNodeDict[statement].NodeStatus;

    public DefiniteAssignmentStatus GetStatusBeforeLoopCondition(Statement statement) => _conditionNodeDict[statement].NodeStatus;

    /// <summary>
    /// Exports the CFG. This method is intended to help debugging issues related to definite assignment.
    /// </summary>
    public GraphVizGraph ExportGraph()
    {
        var graphVizGraph = new GraphVizGraph
        {
            Title = "DefiniteAssignment - " + _variableName
        };

        for (var i = 0; i < _allNodes.Count; i++)
        {
            var name = $"#{i} = {_allNodes[i].NodeStatus}{Environment.NewLine}";

            switch (_allNodes[i].Type)
            {
                case ControlFlowNodeType.StartNode:
                case ControlFlowNodeType.BetweenStatements:
                    name += _allNodes[i].NextStatement.ToString();
                    break;
                case ControlFlowNodeType.EndNode:
                    name += "End of " + _allNodes[i].PreviousStatement.ToString();
                    break;
                case ControlFlowNodeType.LoopCondition:
                    name += "Condition in " + _allNodes[i].NextStatement.ToString();
                    break;
                default:
                    name += _allNodes[i].Type.ToString();
                    break;
            }

            graphVizGraph.AddNode(new GraphVizNode(i) { label = name });

            foreach (ControlFlowEdge edge in _allNodes[i].Outgoing)
            {
                GraphVizEdge ge = new GraphVizEdge(i, ((DefiniteAssignmentNode)edge.To).Index);
                if (_edgeStatus.Count > 0)
                    ge.label = _edgeStatus[edge].ToString();
                if (edge.IsLeavingTryFinally)
                    ge.style = "dashed";

                switch (edge.Type)
                {
                    case ControlFlowEdgeType.ConditionTrue:
                        ge.color = "green";
                        break;
                    case ControlFlowEdgeType.ConditionFalse:
                        ge.color = "red";
                        break;
                    case ControlFlowEdgeType.Jump:
                        ge.color = "blue";
                        break;
                }

                graphVizGraph.AddEdge(ge);
            }
        }

        return graphVizGraph;
    }

    private static DefiniteAssignmentStatus MergeStatus(DefiniteAssignmentStatus a, DefiniteAssignmentStatus b) =>
        // The result will be DefinitelyAssigned if at least one incoming edge is DefinitelyAssigned and all others are unreachable.
        // The result will be DefinitelyUnassigned if at least one incoming edge is DefinitelyUnassigned and all others are unreachable.
        // The result will be Unreachable if all incoming edges are unreachable.
        // Otherwise, the result will be PotentiallyAssigned.
        (a, b) switch
        {
            (_, _) when a == b => a,
            (DefiniteAssignmentStatus.CodeUnreachable, _) => b,
            (_, DefiniteAssignmentStatus.CodeUnreachable) => a,
            _ => DefiniteAssignmentStatus.PotentiallyAssigned
        };

    private void ChangeNodeStatus(DefiniteAssignmentNode node, DefiniteAssignmentStatus inputStatus)
    {
        if (node.NodeStatus == inputStatus)
            return;

        node.NodeStatus = inputStatus;
        DefiniteAssignmentStatus outputStatus;

        switch (node.Type)
        {
            case ControlFlowNodeType.StartNode or ControlFlowNodeType.BetweenStatements:
                if (node.NextStatement is IfElseStatement)
                    // Handle if-else as a condition node
                    goto case ControlFlowNodeType.LoopCondition;

                outputStatus = GetStatus(node, inputStatus);
                break;

            case ControlFlowNodeType.EndNode:
                outputStatus = inputStatus;
                HandleTryFinally(node, outputStatus);
                break;
            case ControlFlowNodeType.LoopCondition:
                if (node.NextStatement is ForeachStatement foreachStmt)
                {
                    outputStatus = CleanSpecialValues(foreachStmt.InExpression.AcceptVisitor(_visitor, inputStatus));

                    if (foreachStmt.VariableName == _variableName)
                        outputStatus = DefiniteAssignmentStatus.DefinitelyAssigned;

                    break;
                }

                Debug.Assert(node.NextStatement is IfElseStatement or WhileStatement or ForStatement or DoWhileStatement);
                outputStatus = node.NextStatement.GetChildByRole(Roles.Condition) is var condition && condition.IsNull
                    ? inputStatus
                    : condition.AcceptVisitor(_visitor, inputStatus);

                foreach (var edge in node.Outgoing) ChangeEdgeStatusWithSelection(edge, outputStatus);

                return;
            default:
                throw new InvalidOperationException();
        }

        foreach (var edge in node.Outgoing) ChangeEdgeStatus(edge, outputStatus);
    }

    private void ChangeEdgeStatusWithSelection(ControlFlowEdge edge, DefiniteAssignmentStatus outputStatus)
    {
        if (edge.Type == ControlFlowEdgeType.ConditionTrue && outputStatus == DefiniteAssignmentStatus.AssignedAfterTrueExpression ||
            edge.Type == ControlFlowEdgeType.ConditionFalse && outputStatus == DefiniteAssignmentStatus.AssignedAfterFalseExpression)
            ChangeEdgeStatus(edge, DefiniteAssignmentStatus.DefinitelyAssigned);
        else
            ChangeEdgeStatus(edge, CleanSpecialValues(outputStatus));
    }

    private void HandleTryFinally(ControlFlowNode node, DefiniteAssignmentStatus outputStatus)
    {
        if (node.PreviousStatement.Role != TryCatchStatement.FinallyBlockRole ||
            outputStatus is not (DefiniteAssignmentStatus.DefinitelyAssigned or DefiniteAssignmentStatus.PotentiallyAssigned))
            return;

        var tryFinally = (TryCatchStatement)node.PreviousStatement.Parent;

        // Changing the status on a finally block potentially changes the status of all edges leaving that finally block:
        foreach (var edge in _allNodes.SelectMany(n => n.Outgoing))
            if (edge.IsLeavingTryFinally &&
                edge.TryFinallyStatements.Contains(tryFinally) &&
                _edgeStatus[edge] is DefiniteAssignmentStatus.PotentiallyAssigned)
                ChangeEdgeStatus(edge, outputStatus);
    }

    private DefiniteAssignmentStatus GetStatus(ControlFlowNode node, DefiniteAssignmentStatus inputStatus) =>
        inputStatus == DefiniteAssignmentStatus.DefinitelyAssigned
            // There isn't any way to un-assign variables, so we don't have to check the expression
            // if the status already is definitely assigned.
            ? DefiniteAssignmentStatus.DefinitelyAssigned
            : CleanSpecialValues(node.NextStatement.AcceptVisitor(_visitor, inputStatus));

    private void ChangeEdgeStatus(ControlFlowEdge edge, DefiniteAssignmentStatus newStatus)
    {
        var oldStatus = _edgeStatus[edge];

        if (oldStatus == newStatus)
            return;

        // Ensure that status can cannot change back to CodeUnreachable after it once was reachable.
        // Also, don't ever use AssignedAfter... for statements.
        if (newStatus is DefiniteAssignmentStatus.CodeUnreachable or DefiniteAssignmentStatus.AssignedAfterFalseExpression
            or DefiniteAssignmentStatus.AssignedAfterTrueExpression)
            throw new InvalidOperationException();

        // Note that the status can change from DefinitelyAssigned
        // back to PotentiallyAssigned as unreachable input edges are
        // discovered to be reachable.

        _edgeStatus[edge] = newStatus;
        var targetNode = (DefiniteAssignmentNode)edge.To;

        if (_analyzedRangeStart <= targetNode.Index && targetNode.Index <= _analyzedRangeEnd)
            // TODO: potential optimization: visit previously unreachable nodes with higher priority
            // (e.g. use Deque and enqueue previously unreachable nodes at the front, but
            // other nodes at the end)
            _nodesWithModifiedInput.Enqueue(targetNode);
    }

    /// <summary>
    /// Evaluates an expression.
    /// </summary>
    /// <returns>The constant value of the expression; or null if the expression is not a constant.</returns>
    private ResolveResult EvaluateConstant(Expression expression) => _resolver.Resolve(expression, _analysisCancellationToken);

    /// <summary>
    /// Evaluates an expression.
    /// </summary>
    /// <returns>The value of the constant boolean expression; or null if the value is not a constant boolean expression.</returns>
    private bool? EvaluateCondition(Expression expression) =>
        EvaluateConstant(expression) is { IsCompileTimeConstant: true } resolveResult
            ? resolveResult.ConstantValue as bool?
            : null;

    private static DefiniteAssignmentStatus CleanSpecialValues(DefiniteAssignmentStatus status) =>
        status switch
        {
            DefiniteAssignmentStatus.AssignedAfterTrueExpression or DefiniteAssignmentStatus.AssignedAfterFalseExpression =>
                DefiniteAssignmentStatus.PotentiallyAssigned,
            _ => status
        };

    private sealed class DefiniteAssignmentNode : ControlFlowNode
    {
        public int Index;
        public DefiniteAssignmentStatus NodeStatus;

        public DefiniteAssignmentNode(Statement previousStatement, Statement nextStatement, ControlFlowNodeType type)
            : base(previousStatement, nextStatement, type)
        {
        }
    }

    private sealed class DerivedControlFlowGraphBuilder : ControlFlowGraphBuilder
    {
        protected override ControlFlowNode CreateNode(Statement previousStatement, Statement nextStatement, ControlFlowNodeType type) =>
            new DefiniteAssignmentNode(previousStatement, nextStatement, type);
    }

    private sealed class DefiniteAssignmentVisitor : DepthFirstAstVisitor<DefiniteAssignmentStatus, DefiniteAssignmentStatus>
    {
        internal DefiniteAssignmentAnalysis Analysis;

        // The general approach for unknown nodes is to pass the status through all child nodes in order
        protected override DefiniteAssignmentStatus VisitChildren(AstNode node, DefiniteAssignmentStatus data)
        {
            // the special values are valid as output only, not as input
            Debug.Assert(data == CleanSpecialValues(data));
            var status = data;

            for (var child = node.FirstChild; child != null; child = child.NextSibling)
            {
                Analysis._analysisCancellationToken.ThrowIfCancellationRequested();
                Debug.Assert(child is not Statement); // statements are visited with the CFG, not with the visitor pattern
                status = child.AcceptVisitor(this, status);
                status = CleanSpecialValues(status);
            }

            return status;
        }

        #region Statements

        // For statements, the visitor only describes the effect of the statement itself;
        // we do not consider the effect of any nested statements.
        // This is done because the nested statements will be reached using the control flow graph.

        // In fact, these methods are present so that the default logic in VisitChildren does not try to visit the nested statements.

        public override DefiniteAssignmentStatus VisitBlockStatement(BlockStatement blockStatement, DefiniteAssignmentStatus data) => data;

        public override DefiniteAssignmentStatus VisitCheckedStatement(CheckedStatement checkedStatement, DefiniteAssignmentStatus data) => data;

        public override DefiniteAssignmentStatus VisitUncheckedStatement(UncheckedStatement uncheckedStatement, DefiniteAssignmentStatus data) => data;

        // ExpressionStatement handled by default logic
        // VariableDeclarationStatement handled by default logic

        public override DefiniteAssignmentStatus VisitVariableInitializer(VariableInitializer variableInitializer, DefiniteAssignmentStatus data)
        {
            if (variableInitializer.Initializer.IsNull)
                return data;

            return variableInitializer.Initializer.AcceptVisitor(this, data) is var status &&
                   variableInitializer.Name == Analysis._variableName
                ? DefiniteAssignmentStatus.DefinitelyAssigned
                : status;
        }

        // IfStatement not handled by visitor, but special-cased in the code consuming the control flow graph

        public override DefiniteAssignmentStatus VisitSwitchStatement(SwitchStatement switchStatement, DefiniteAssignmentStatus data) =>
            switchStatement.Expression.AcceptVisitor(this, data);

        // condition is handled by special condition CFG node
        public override DefiniteAssignmentStatus VisitWhileStatement(WhileStatement whileStatement, DefiniteAssignmentStatus data) => data;

        // condition is handled by special condition CFG node
        public override DefiniteAssignmentStatus VisitDoWhileStatement(DoWhileStatement doWhileStatement, DefiniteAssignmentStatus data) => data;

        // condition is handled by special condition CFG node; initializer and iterator statements are handled by CFG
        public override DefiniteAssignmentStatus VisitForStatement(ForStatement forStatement, DefiniteAssignmentStatus data) => data;

        // Break/Continue/Goto: handled by default logic

        // ThrowStatement: handled by default logic (just visit the expression)
        // ReturnStatement: handled by default logic (just visit the expression)

        // no special logic when entering the try-catch-finally statement
        // TODO: where to put the special logic when exiting the try-finally statement?
        public override DefiniteAssignmentStatus VisitTryCatchStatement(TryCatchStatement tryCatchStatement, DefiniteAssignmentStatus data) => data;

        // assignment of the foreach loop variable is done when handling the condition node
        public override DefiniteAssignmentStatus VisitForeachStatement(ForeachStatement foreachStatement, DefiniteAssignmentStatus data) => data;

        public override DefiniteAssignmentStatus VisitUsingStatement(UsingStatement usingStatement, DefiniteAssignmentStatus data) =>
            usingStatement.ResourceAcquisition is Expression
                ? usingStatement.ResourceAcquisition.AcceptVisitor(this, data)
                : data; // don't handle resource acquisition statements, as those are connected in the control flow graph

        public override DefiniteAssignmentStatus VisitLockStatement(LockStatement lockStatement, DefiniteAssignmentStatus data) =>
            lockStatement.Expression.AcceptVisitor(this, data);

        // Yield statements use the default logic

        public override DefiniteAssignmentStatus VisitUnsafeStatement(UnsafeStatement unsafeStatement, DefiniteAssignmentStatus data) => data;

        public override DefiniteAssignmentStatus VisitFixedStatement(FixedStatement fixedStatement, DefiniteAssignmentStatus data) =>
            fixedStatement.Variables.Aggregate(data, (current, variable) => variable.AcceptVisitor(this, current));

        #endregion

        #region Expressions

        public override DefiniteAssignmentStatus VisitDirectionExpression(DirectionExpression directionExpression, DefiniteAssignmentStatus data) =>
            directionExpression.FieldDirection == FieldDirection.Out
                ? HandleAssignment(directionExpression.Expression, null, data)
                // use default logic for 'ref'
                : VisitChildren(directionExpression, data);

        public override DefiniteAssignmentStatus VisitAssignmentExpression(AssignmentExpression assignmentExpression, DefiniteAssignmentStatus data) =>
            assignmentExpression.Operator == AssignmentOperatorType.Assign
                ? HandleAssignment(assignmentExpression.Left, assignmentExpression.Right, data)
                // use default logic for compound assignment operators
                : VisitChildren(assignmentExpression, data);

        private DefiniteAssignmentStatus HandleAssignment(Expression left, Expression right, DefiniteAssignmentStatus initialStatus)
        {
            if (left is IdentifierExpression ident && ident.Identifier == Analysis._variableName)
            {
                // right==null is special case when handling 'out' expressions
                right?.AcceptVisitor(this, initialStatus);
                return DefiniteAssignmentStatus.DefinitelyAssigned;
            }

            var status = left.AcceptVisitor(this, initialStatus);

            if (right != null)
                status = right.AcceptVisitor(this, CleanSpecialValues(status));

            return CleanSpecialValues(status);
        }

        public override DefiniteAssignmentStatus VisitParenthesizedExpression(ParenthesizedExpression parenthesizedExpression,
            DefiniteAssignmentStatus data) =>
            // Don't use the default logic here because we don't want to clean up the special values.
            parenthesizedExpression.Expression.AcceptVisitor(this, data);

        public override DefiniteAssignmentStatus VisitCheckedExpression(CheckedExpression checkedExpression, DefiniteAssignmentStatus data) =>
            checkedExpression.Expression.AcceptVisitor(this, data);

        public override DefiniteAssignmentStatus VisitUncheckedExpression(UncheckedExpression uncheckedExpression, DefiniteAssignmentStatus data) =>
            uncheckedExpression.Expression.AcceptVisitor(this, data);

        public override DefiniteAssignmentStatus VisitBinaryOperatorExpression(BinaryOperatorExpression binaryOperatorExpression,
            DefiniteAssignmentStatus data) =>
            binaryOperatorExpression.Operator switch
            {
                BinaryOperatorType.ConditionalAnd => HandleConditionalAnd(binaryOperatorExpression, data),
                BinaryOperatorType.ConditionalOr => HandleConditionalOr(binaryOperatorExpression, data),
                BinaryOperatorType.NullCoalescing => HandleNullCoalescing(binaryOperatorExpression, data),
                _ => VisitChildren(binaryOperatorExpression, data)
            };

        private DefiniteAssignmentStatus HandleConditionalAnd(BinaryOperatorExpression binaryOperatorExpression, DefiniteAssignmentStatus data) =>
            // Handle constant left side of && expressions (not in the C# spec, but done by the MS compiler)
            Analysis.EvaluateCondition(binaryOperatorExpression.Left) switch
            {
                true => binaryOperatorExpression.Right.AcceptVisitor(this, data),
                false => data,
                _ => GetAssignmentStatusForAnd(binaryOperatorExpression, data)
            };

        private DefiniteAssignmentStatus HandleConditionalOr(BinaryOperatorExpression binaryOperatorExpression, DefiniteAssignmentStatus data) =>
            // C# 4.0 spec: §5.3.3.25 Definite Assignment for || expressions
            Analysis.EvaluateCondition(binaryOperatorExpression.Left) switch
            {
                false => binaryOperatorExpression.Right.AcceptVisitor(this, data),
                true => data,
                _ => GetAssignmentStatusForOr(binaryOperatorExpression, data)
            };

        private DefiniteAssignmentStatus HandleNullCoalescing(BinaryOperatorExpression binaryOperatorExpression, DefiniteAssignmentStatus data)
        {
            // C# 4.0 spec: §5.3.3.27 Definite assignment for ?? expressions
            var leftConstant = Analysis.EvaluateConstant(binaryOperatorExpression.Left);

            if (leftConstant is { IsCompileTimeConstant: true, ConstantValue: null })
                return binaryOperatorExpression.Right.AcceptVisitor(this, data);

            var status = CleanSpecialValues(binaryOperatorExpression.Left.AcceptVisitor(this, data));
            binaryOperatorExpression.Right.AcceptVisitor(this, status);
            return status;
        }

        private DefiniteAssignmentStatus GetAssignmentStatusForAnd(BinaryOperatorExpression binaryOperatorExpression, DefiniteAssignmentStatus data)
        {
            // C# 4.0 spec: §5.3.3.24 Definite Assignment for && expressions
            var afterLeft = binaryOperatorExpression.Left.AcceptVisitor(this, data);
            var beforeRight = GetBeforeRightAssignmentStatusForAnd(afterLeft);
            var afterRight = binaryOperatorExpression.Right.AcceptVisitor(this, beforeRight);

            return (afterLeft, afterRight) switch
            {
                (DefiniteAssignmentStatus.DefinitelyAssigned, _) or
                    (DefiniteAssignmentStatus.AssignedAfterFalseExpression, DefiniteAssignmentStatus.DefinitelyAssigned) =>
                    DefiniteAssignmentStatus.DefinitelyAssigned,
                (_, DefiniteAssignmentStatus.DefinitelyAssigned) or (_, DefiniteAssignmentStatus.AssignedAfterTrueExpression) =>
                    DefiniteAssignmentStatus.AssignedAfterTrueExpression,
                (DefiniteAssignmentStatus.AssignedAfterFalseExpression, DefiniteAssignmentStatus.AssignedAfterFalseExpression) =>
                    DefiniteAssignmentStatus.AssignedAfterFalseExpression,
                (_, _) => DefiniteAssignmentStatus.PotentiallyAssigned
            };
        }

        private DefiniteAssignmentStatus GetAssignmentStatusForOr(BinaryOperatorExpression binaryOperatorExpression, DefiniteAssignmentStatus data)
        {
            var afterLeft = binaryOperatorExpression.Left.AcceptVisitor(this, data);
            var beforeRight = GetBeforeRightAssignmentStatusForOr(afterLeft);
            var afterRight = binaryOperatorExpression.Right.AcceptVisitor(this, beforeRight);

            return (afterLeft, afterRight) switch
            {
                (DefiniteAssignmentStatus.DefinitelyAssigned, _) or
                    (DefiniteAssignmentStatus.AssignedAfterTrueExpression, DefiniteAssignmentStatus.DefinitelyAssigned) =>
                    DefiniteAssignmentStatus.DefinitelyAssigned,
                (_, DefiniteAssignmentStatus.DefinitelyAssigned) or (_, DefiniteAssignmentStatus.AssignedAfterFalseExpression) =>
                    DefiniteAssignmentStatus.AssignedAfterFalseExpression,
                (DefiniteAssignmentStatus.AssignedAfterTrueExpression, DefiniteAssignmentStatus.AssignedAfterTrueExpression) =>
                    DefiniteAssignmentStatus.AssignedAfterTrueExpression,
                (_, _) => DefiniteAssignmentStatus.PotentiallyAssigned
            };
        }

        private static DefiniteAssignmentStatus GetBeforeRightAssignmentStatusForAnd(DefiniteAssignmentStatus afterLeft) =>
            afterLeft switch
            {
                DefiniteAssignmentStatus.AssignedAfterTrueExpression => DefiniteAssignmentStatus.DefinitelyAssigned,
                DefiniteAssignmentStatus.AssignedAfterFalseExpression => DefiniteAssignmentStatus.PotentiallyAssigned,
                _ => afterLeft
            };

        private static DefiniteAssignmentStatus GetBeforeRightAssignmentStatusForOr(DefiniteAssignmentStatus afterLeft) =>
            afterLeft switch
            {
                DefiniteAssignmentStatus.AssignedAfterTrueExpression => DefiniteAssignmentStatus.PotentiallyAssigned,
                DefiniteAssignmentStatus.AssignedAfterFalseExpression => DefiniteAssignmentStatus.DefinitelyAssigned,
                _ => afterLeft
            };

        public override DefiniteAssignmentStatus VisitUnaryOperatorExpression(UnaryOperatorExpression unaryOperatorExpression,
            DefiniteAssignmentStatus data)
        {
            if (unaryOperatorExpression.Operator != UnaryOperatorType.Not)
                // use default logic for other operators
                return VisitChildren(unaryOperatorExpression, data);

            // C# 4.0 spec: §5.3.3.26 Definite assignment for ! expressions
            var status = unaryOperatorExpression.Expression.AcceptVisitor(this, data);
            return status switch
            {
                DefiniteAssignmentStatus.AssignedAfterFalseExpression => DefiniteAssignmentStatus.AssignedAfterTrueExpression,
                DefiniteAssignmentStatus.AssignedAfterTrueExpression => DefiniteAssignmentStatus.AssignedAfterFalseExpression,
                _ => status
            };
        }

        public override DefiniteAssignmentStatus
            VisitConditionalExpression(ConditionalExpression conditionalExpression, DefiniteAssignmentStatus data) =>
            // C# 4.0 spec: §5.3.3.28 Definite assignment for ?: expressions
            Analysis.EvaluateCondition(conditionalExpression.Condition) switch
            {
                true => conditionalExpression.TrueExpression.AcceptVisitor(this, data),
                false => conditionalExpression.FalseExpression.AcceptVisitor(this, data),
                _ => HandleNullConditionCase(conditionalExpression, data)
            };

        public override DefiniteAssignmentStatus VisitAnonymousMethodExpression(AnonymousMethodExpression anonymousMethodExpression,
            DefiniteAssignmentStatus data)
        {
            var body = anonymousMethodExpression.Body;
            Analysis.ChangeNodeStatus(Analysis._beginNodeDict[body], data);
            return data;
        }

        public override DefiniteAssignmentStatus VisitLambdaExpression(LambdaExpression lambdaExpression, DefiniteAssignmentStatus data)
        {
            if (lambdaExpression.Body is Statement body)
                Analysis.ChangeNodeStatus(Analysis._beginNodeDict[body], data);
            else
                lambdaExpression.Body.AcceptVisitor(this, data);

            return data;
        }

        public override DefiniteAssignmentStatus VisitIdentifierExpression(IdentifierExpression identifierExpression, DefiniteAssignmentStatus data)
        {
            if (data != DefiniteAssignmentStatus.DefinitelyAssigned &&
                identifierExpression.Identifier == Analysis._variableName &&
                identifierExpression.TypeArguments.Count == 0)
                Analysis._unassignedVariableUses.Add(identifierExpression);

            return data;
        }

        private DefiniteAssignmentStatus HandleNullConditionCase(ConditionalExpression conditionalExpression, DefiniteAssignmentStatus data)
        {
            var afterCondition = conditionalExpression.Condition.AcceptVisitor(this, data);
            var (beforeTrue, beforeFalse) = GetBeforeTrueAndFalse(afterCondition);
            var afterTrue = conditionalExpression.TrueExpression.AcceptVisitor(this, beforeTrue);
            var afterFalse = conditionalExpression.FalseExpression.AcceptVisitor(this, beforeFalse);
            return MergeStatus(CleanSpecialValues(afterTrue), CleanSpecialValues(afterFalse));
        }

        private static (DefiniteAssignmentStatus BeforeTrue, DefiniteAssignmentStatus BeforeFalse) GetBeforeTrueAndFalse(
            DefiniteAssignmentStatus afterCondition) =>
            afterCondition switch
            {
                DefiniteAssignmentStatus.AssignedAfterTrueExpression =>
                    (DefiniteAssignmentStatus.DefinitelyAssigned, DefiniteAssignmentStatus.PotentiallyAssigned),
                DefiniteAssignmentStatus.AssignedAfterFalseExpression =>
                    (DefiniteAssignmentStatus.PotentiallyAssigned, DefiniteAssignmentStatus.DefinitelyAssigned),
                _ => (afterCondition, afterCondition)
            };

        #endregion
    }
}