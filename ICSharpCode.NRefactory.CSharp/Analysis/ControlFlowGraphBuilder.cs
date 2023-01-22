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
using ICSharpCode.NRefactory.CSharp.TypeSystem;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using ICSharpCode.NRefactory.Utils;

namespace ICSharpCode.NRefactory.CSharp.Analysis;

/// <summary>
/// Constructs the control flow graph for C# statements.
/// </summary>
public class ControlFlowGraphBuilder
{
    // Written according to the reachability rules in the C# spec (§8.1 End points and reachability)

    private Statement _rootStatement;
    private CSharpTypeResolveContext _typeResolveContext;
    private Func<AstNode, CancellationToken, ResolveResult> _resolver;
    private List<ControlFlowNode> _nodes;
    private Dictionary<string, ControlFlowNode> _labels;
    private List<ControlFlowNode> _gotoStatements;
    private CancellationToken _cancellationToken;

    protected virtual ControlFlowNode CreateNode(Statement previousStatement, Statement nextStatement, ControlFlowNodeType type)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        return new ControlFlowNode(previousStatement, nextStatement, type);
    }

    protected virtual ControlFlowEdge CreateEdge(ControlFlowNode from, ControlFlowNode to, ControlFlowEdgeType type)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        return new ControlFlowEdge(from, to, type);
    }

    public IList<ControlFlowNode> BuildControlFlowGraph(Statement statement, CancellationToken cancellationToken = default)
    {
        if (statement == null)
            throw new ArgumentNullException(nameof(statement));

        var resolver = new CSharpResolver(MinimalCorlib.Instance.CreateCompilation());
        return BuildControlFlowGraph(statement, new CSharpAstResolver(resolver, statement), cancellationToken);
    }

    public IList<ControlFlowNode> BuildControlFlowGraph(Statement statement, CSharpAstResolver resolver,
        CancellationToken cancellationToken = default)
    {
        if (statement == null)
            throw new ArgumentNullException(nameof(statement));

        if (resolver == null)
            throw new ArgumentNullException(nameof(resolver));

        return BuildControlFlowGraph(statement, resolver.Resolve, resolver.TypeResolveContext, cancellationToken);
    }

    internal IList<ControlFlowNode> BuildControlFlowGraph(Statement statement, Func<AstNode, CancellationToken, ResolveResult> resolver,
        CSharpTypeResolveContext typeResolveContext, CancellationToken cancellationToken)
    {
        var nodeCreationVisitor = new NodeCreationVisitor
        {
            Builder = this
        };

        try
        {
            _nodes = new List<ControlFlowNode>();
            _labels = new Dictionary<string, ControlFlowNode>();
            _gotoStatements = new List<ControlFlowNode>();
            _rootStatement = statement;
            _resolver = resolver;
            _typeResolveContext = typeResolveContext;
            _cancellationToken = cancellationToken;

            var entryPoint = CreateStartNode(statement);
            statement.AcceptVisitor(nodeCreationVisitor, entryPoint);

            // Resolve goto statements:
            foreach (var gotoStmt in _gotoStatements)
                if (((GotoStatement)gotoStmt.NextStatement).Label is var label &&
                    _labels.TryGetValue(label, out var labelNode))
                    nodeCreationVisitor.Connect(gotoStmt, labelNode, ControlFlowEdgeType.Jump);

            AnnotateLeaveEdgesWithTryFinallyBlocks();

            return _nodes;
        }
        finally
        {
            _nodes = null;
            _labels = null;
            _gotoStatements = null;
            _rootStatement = null;
            _resolver = null;
            _typeResolveContext = null;
            _cancellationToken = CancellationToken.None;
        }
    }

    private void AnnotateLeaveEdgesWithTryFinallyBlocks()
    {
        foreach (var edge in _nodes.SelectMany(n => n.Outgoing))
        {
            // Only jumps are potential candidates for leaving try-finally blocks.
            // Note that the regular edges leaving try or catch blocks are already annotated by the visitor.
            if (edge.Type != ControlFlowEdgeType.Jump)
                continue;

            var gotoStatement = edge.From.NextStatement;
            Debug.Assert(gotoStatement is GotoStatement or GotoDefaultStatement or GotoCaseStatement or BreakStatement or ContinueStatement);

            var targetStatement = edge.To.PreviousStatement ?? edge.To.NextStatement;

            if (gotoStatement.Parent == targetStatement.Parent)
                continue;

            var targetParentTryCatch = targetStatement.Ancestors.OfType<TryCatchStatement>().ToHashSet();

            for (var node = gotoStatement.Parent; node != null; node = node.Parent)
                if (node is TryCatchStatement leftTryCatch)
                {
                    if (targetParentTryCatch.Contains(leftTryCatch))
                        break;

                    if (!leftTryCatch.FinallyBlock.IsNull)
                        edge.AddJumpOutOfTryFinally(leftTryCatch);
                }
        }
    }

    #region Create*Node

    private ControlFlowNode CreateStartNode(Statement statement)
    {
        if (statement.IsNull)
            return null;

        var node = CreateNode(null, statement, ControlFlowNodeType.StartNode);
        _nodes.Add(node);
        return node;
    }

    private ControlFlowNode CreateSpecialNode(Statement statement, ControlFlowNodeType type, bool addToNodeList = true)
    {
        var node = CreateNode(null, statement, type);

        if (addToNodeList)
            _nodes.Add(node);

        return node;
    }

    private ControlFlowNode CreateEndNode(Statement statement, bool addToNodeList = true)
    {
        var nextStatement = statement == _rootStatement ? null : FindNextStatementInSameRole(statement);
        var type = nextStatement != null ? ControlFlowNodeType.BetweenStatements : ControlFlowNodeType.EndNode;
        var node = CreateNode(statement, nextStatement, type);

        if (addToNodeList)
            _nodes.Add(node);

        return node;
    }

    private static Statement FindNextStatementInSameRole(AstNode statement)
    {
        var next = statement;

        do next = next.NextSibling;
        while (next != null && next.Role != statement.Role);

        return next as Statement;
    }

    #endregion

    #region Constant evaluation

    /// <summary>
    /// Gets/Sets whether to handle only primitive expressions as constants (no complex expressions like "a + b").
    /// </summary>
    public bool EvaluateOnlyPrimitiveConstants { get; set; }

    /// <summary>
    /// Evaluates an expression.
    /// </summary>
    /// <returns>The constant value of the expression; or null if the expression is not a constant.</returns>
    private ResolveResult EvaluateConstant(Expression expr) =>
        expr.IsNull || EvaluateOnlyPrimitiveConstants && expr is not (PrimitiveExpression or NullReferenceExpression)
            ? null
            : _resolver(expr, _cancellationToken);

    /// <summary>
    /// Evaluates an expression.
    /// </summary>
    /// <returns>The value of the constant boolean expression; or null if the value is not a constant boolean expression.</returns>
    private bool? EvaluateCondition(Expression expr) =>
        EvaluateConstant(expr) is { IsCompileTimeConstant: true } resolveResult
            ? resolveResult.ConstantValue as bool?
            : null;

    private bool AreEqualConstants(ResolveResult c1, ResolveResult c2)
    {
        if (c1 == null || c2 == null || !c1.IsCompileTimeConstant || !c2.IsCompileTimeConstant)
            return false;

        var resolver = new CSharpResolver(_typeResolveContext);
        var binaryOperator = resolver.ResolveBinaryOperator(BinaryOperatorType.Equality, c1, c2);
        return binaryOperator.IsCompileTimeConstant && binaryOperator.ConstantValue is true;
    }

    #endregion

    private sealed class NodeCreationVisitor : DepthFirstAstVisitor<ControlFlowNode, ControlFlowNode>
    {
        // 'data' parameter: input control flow node (start of statement being visited)
        // Return value: result control flow node (end of statement being visited)

        internal ControlFlowGraphBuilder Builder;
        private readonly Stack<ControlFlowNode> _breakTargets = new();
        private readonly Stack<ControlFlowNode> _continueTargets = new();
        private readonly List<ControlFlowNode> _gotoCaseOrDefault = new();

        internal ControlFlowEdge Connect(ControlFlowNode from, ControlFlowNode to, ControlFlowEdgeType type = ControlFlowEdgeType.Normal)
        {
            if (from == null || to == null)
                return null;

            var edge = Builder.CreateEdge(from, to, type);
            from.Outgoing.Add(edge);
            to.Incoming.Add(edge);
            return edge;
        }

        /// <summary>
        /// Creates an end node for <c>stmt</c> and connects <c>from</c> with the new node.
        /// </summary>
        private ControlFlowNode CreateConnectedEndNode(Statement stmt, ControlFlowNode from)
        {
            var newNode = Builder.CreateEndNode(stmt);
            Connect(from, newNode);
            return newNode;
        }

        protected override ControlFlowNode VisitChildren(AstNode node, ControlFlowNode data)
        {
            // We have overrides for all possible statements and should visit statements only.
            throw new NotSupportedException();
        }

        public override ControlFlowNode VisitBlockStatement(BlockStatement blockStatement, ControlFlowNode data)
        {
            // C# 4.0 spec: §8.2 Blocks
            var childNode = HandleStatementList(blockStatement.Statements, data);
            return CreateConnectedEndNode(blockStatement, childNode);
        }

        private ControlFlowNode HandleStatementList(AstNodeCollection<Statement> statements, ControlFlowNode source)
        {
            ControlFlowNode childNode = null;

            foreach (var statement in statements)
            {
                if (childNode == null)
                {
                    childNode = Builder.CreateStartNode(statement);

                    if (source != null)
                        Connect(source, childNode);
                }

                Debug.Assert(childNode.NextStatement == statement);
                childNode = statement.AcceptVisitor(this, childNode);
                Debug.Assert(childNode.PreviousStatement == statement);
            }

            return childNode ?? source;
        }

        public override ControlFlowNode VisitEmptyStatement(EmptyStatement emptyStatement, ControlFlowNode data) =>
            CreateConnectedEndNode(emptyStatement, data);

        public override ControlFlowNode VisitLabelStatement(LabelStatement labelStatement, ControlFlowNode data)
        {
            var end = CreateConnectedEndNode(labelStatement, data);
            Builder._labels[labelStatement.Label] = end;
            return end;
        }

        public override ControlFlowNode VisitVariableDeclarationStatement(VariableDeclarationStatement variableDeclarationStatement,
            ControlFlowNode data) =>
            CreateConnectedEndNode(variableDeclarationStatement, data);

        public override ControlFlowNode VisitExpressionStatement(ExpressionStatement expressionStatement, ControlFlowNode data) =>
            CreateConnectedEndNode(expressionStatement, data);

        public override ControlFlowNode VisitIfElseStatement(IfElseStatement ifElseStatement, ControlFlowNode data)
        {
            var cond = Builder.EvaluateCondition(ifElseStatement.Condition);
            var trueBegin = Builder.CreateStartNode(ifElseStatement.TrueStatement);

            if (cond != false)
                Connect(data, trueBegin, ControlFlowEdgeType.ConditionTrue);

            var trueEnd = ifElseStatement.TrueStatement.AcceptVisitor(this, trueBegin);
            var falseBegin = Builder.CreateStartNode(ifElseStatement.FalseStatement);

            if (cond != true)
                Connect(data, falseBegin, ControlFlowEdgeType.ConditionFalse);

            var falseEnd = ifElseStatement.FalseStatement.AcceptVisitor(this, falseBegin);
            // (if no else statement exists, both falseBegin and falseEnd will be null)
            var end = Builder.CreateEndNode(ifElseStatement);
            Connect(trueEnd, end);

            if (falseEnd != null)
                Connect(falseEnd, end);
            else if (cond != true) Connect(data, end, ControlFlowEdgeType.ConditionFalse);

            return end;
        }

        public override ControlFlowNode VisitSwitchStatement(SwitchStatement switchStatement, ControlFlowNode data)
        {
            // First, figure out which switch section will get called (if the expression is constant):
            var constant = Builder.EvaluateConstant(switchStatement.Expression);
            var (defaultSection, sectionMatchedByConstant) = FindSections(switchStatement, constant);
            var gotoCaseOrDefaultInOuterScope = _gotoCaseOrDefault.Count;
            var sectionStartNodes = new List<ControlFlowNode>();
            var end = Builder.CreateEndNode(switchStatement, addToNodeList: false);

            _breakTargets.Push(end);

            foreach (var section in switchStatement.SwitchSections)
            {
                var sectionStartNodeId = Builder._nodes.Count;

                if (constant is not { IsCompileTimeConstant: true } || section == sectionMatchedByConstant)
                    HandleStatementList(section!.Statements, data);
                else
                    // This section is unreachable: pass null to HandleStatementList.
                    HandleStatementList(section.Statements, null);

                // Don't bother connecting the ends of the sections: the 'break' statement takes care of that.
                // Store the section start node for 'goto case' statements.
                sectionStartNodes.Add(sectionStartNodeId < Builder._nodes.Count ? Builder._nodes[sectionStartNodeId] : null);
            }

            _breakTargets.Pop();

            if (defaultSection == null && sectionMatchedByConstant == null) Connect(data, end);

            if (_gotoCaseOrDefault.Count > gotoCaseOrDefaultInOuterScope)
                ResolveGotoCases(switchStatement, gotoCaseOrDefaultInOuterScope, sectionStartNodes, end);

            Builder._nodes.Add(end);
            return end;
        }

        private (SwitchSection DefaultSection, SwitchSection SectionMatchedByConstant) FindSections(SwitchStatement switchStatement,
            ResolveResult constant)
        {
            SwitchSection defaultSection = null;
            SwitchSection sectionMatchedByConstant = null;
            var isCompileTimeConstant = constant is { IsCompileTimeConstant: true };

            foreach (var section in switchStatement.SwitchSections)
            foreach (var label in section.CaseLabels)
                if (label.Expression.IsNull)
                    defaultSection = section;
                else if (isCompileTimeConstant &&
                         Builder.EvaluateConstant(label.Expression) is var labelConstant &&
                         Builder.AreEqualConstants(constant, labelConstant))
                    sectionMatchedByConstant = section;

            if (isCompileTimeConstant && sectionMatchedByConstant == null)
                sectionMatchedByConstant = defaultSection;

            return (defaultSection, sectionMatchedByConstant);
        }

        private void ResolveGotoCases(SwitchStatement switchStatement, int gotoCaseOrDefaultInOuterScope, List<ControlFlowNode> sectionStartNodes,
            ControlFlowNode end)
        {
            for (var i = gotoCaseOrDefaultInOuterScope; i < _gotoCaseOrDefault.Count; i++)
            {
                var gotoCaseNode = _gotoCaseOrDefault[i];
                var gotoCaseStatement = gotoCaseNode.NextStatement as GotoCaseStatement;
                var gotoCaseConstant = gotoCaseStatement != null
                    ? Builder.EvaluateConstant(gotoCaseStatement.LabelExpression)
                    : null;

                var targetSectionIndex = -1;
                var currentSectionIndex = 0;

                foreach (var section in switchStatement.SwitchSections)
                {
                    targetSectionIndex = section.CaseLabels
                        .Aggregate(targetSectionIndex,
                            (current, label) => FindTargetSectionIndex(gotoCaseStatement, label, gotoCaseConstant, current, currentSectionIndex));
                    currentSectionIndex++;
                }

                var to = targetSectionIndex >= 0 && sectionStartNodes[targetSectionIndex] is { } node
                    ? node
                    : end;

                Connect(gotoCaseNode, to, ControlFlowEdgeType.Jump);
            }

            _gotoCaseOrDefault.RemoveRange(gotoCaseOrDefaultInOuterScope, _gotoCaseOrDefault.Count - gotoCaseOrDefaultInOuterScope);
        }

        private int FindTargetSectionIndex(GotoCaseStatement gotoCaseStatement, CaseLabel label, ResolveResult gotoCaseConstant, int targetSectionIndex,
            int currentSectionIndex) =>
            gotoCaseStatement != null
                ? GotoCase(label, gotoCaseConstant, targetSectionIndex, currentSectionIndex)
                : GotoDefault(label, targetSectionIndex, currentSectionIndex);

        private int GotoCase(CaseLabel label, ResolveResult gotoCaseConstant, int targetSectionIndex, int currentSectionIndex) =>
            !label.Expression.IsNull &&
            Builder.EvaluateConstant(label.Expression) is var labelConstant &&
            Builder.AreEqualConstants(gotoCaseConstant, labelConstant)
                ? currentSectionIndex
                : targetSectionIndex;

        private static int GotoDefault(CaseLabel label, int targetSectionIndex, int currentSectionIndex) =>
            label.Expression.IsNull
                ? currentSectionIndex
                : targetSectionIndex;

        public override ControlFlowNode VisitGotoCaseStatement(GotoCaseStatement gotoCaseStatement, ControlFlowNode data)
        {
            _gotoCaseOrDefault.Add(data);
            return Builder.CreateEndNode(gotoCaseStatement);
        }

        public override ControlFlowNode VisitGotoDefaultStatement(GotoDefaultStatement gotoDefaultStatement, ControlFlowNode data)
        {
            _gotoCaseOrDefault.Add(data);
            return Builder.CreateEndNode(gotoDefaultStatement);
        }

        public override ControlFlowNode VisitWhileStatement(WhileStatement whileStatement, ControlFlowNode data)
        {
            // <data> <condition> while (cond) { <bodyStart> embeddedStmt; <bodyEnd> } <end>
            var end = Builder.CreateEndNode(whileStatement, addToNodeList: false);
            var conditionNode = Builder.CreateSpecialNode(whileStatement, ControlFlowNodeType.LoopCondition);
            _breakTargets.Push(end);
            _continueTargets.Push(conditionNode);

            Connect(data, conditionNode);

            var cond = Builder.EvaluateCondition(whileStatement.Condition);
            var bodyStart = Builder.CreateStartNode(whileStatement.EmbeddedStatement);

            if (cond != false)
                Connect(conditionNode, bodyStart, ControlFlowEdgeType.ConditionTrue);

            var bodyEnd = whileStatement.EmbeddedStatement.AcceptVisitor(this, bodyStart);
            Connect(bodyEnd, conditionNode);

            if (cond != true)
                Connect(conditionNode, end, ControlFlowEdgeType.ConditionFalse);

            _breakTargets.Pop();
            _continueTargets.Pop();
            Builder._nodes.Add(end);
            return end;
        }

        public override ControlFlowNode VisitDoWhileStatement(DoWhileStatement doWhileStatement, ControlFlowNode data)
        {
            // <data> do { <bodyStart> embeddedStmt; <bodyEnd>} <condition> while(cond); <end>
            var end = Builder.CreateEndNode(doWhileStatement, addToNodeList: false);
            var conditionNode = Builder.CreateSpecialNode(doWhileStatement, ControlFlowNodeType.LoopCondition, addToNodeList: false);
            _breakTargets.Push(end);
            _continueTargets.Push(conditionNode);

            var bodyStart = Builder.CreateStartNode(doWhileStatement.EmbeddedStatement);
            Connect(data, bodyStart);
            var bodyEnd = doWhileStatement.EmbeddedStatement.AcceptVisitor(this, bodyStart);
            Connect(bodyEnd, conditionNode);

            var cond = Builder.EvaluateCondition(doWhileStatement.Condition);

            if (cond != false)
                Connect(conditionNode, bodyStart, ControlFlowEdgeType.ConditionTrue);

            if (cond != true)
                Connect(conditionNode, end, ControlFlowEdgeType.ConditionFalse);

            _breakTargets.Pop();
            _continueTargets.Pop();
            Builder._nodes.Add(conditionNode);
            Builder._nodes.Add(end);
            return end;
        }

        public override ControlFlowNode VisitForStatement(ForStatement forStatement, ControlFlowNode data)
        {
            data = HandleStatementList(forStatement.Initializers, data);
            // for (initializers <data>; <condition>cond; <iteratorStart>iterators<iteratorEnd>) { <bodyStart> embeddedStmt; <bodyEnd> } <end>
            var end = Builder.CreateEndNode(forStatement, addToNodeList: false);
            var conditionNode = Builder.CreateSpecialNode(forStatement, ControlFlowNodeType.LoopCondition);
            Connect(data, conditionNode);

            var iteratorStartNodeId = Builder._nodes.Count;
            var iteratorEnd = HandleStatementList(forStatement.Iterators, null);
            var iteratorStart = GetIteratorStart(iteratorEnd, iteratorStartNodeId, conditionNode);

            _breakTargets.Push(end);
            _continueTargets.Push(iteratorStart);

            var bodyStart = Builder.CreateStartNode(forStatement.EmbeddedStatement);
            var bodyEnd = forStatement.EmbeddedStatement.AcceptVisitor(this, bodyStart);
            Connect(bodyEnd, iteratorStart);

            _breakTargets.Pop();
            _continueTargets.Pop();

            var cond = forStatement.Condition.IsNull ? true : Builder.EvaluateCondition(forStatement.Condition);

            if (cond != false)
                Connect(conditionNode, bodyStart, ControlFlowEdgeType.ConditionTrue);

            if (cond != true)
                Connect(conditionNode, end, ControlFlowEdgeType.ConditionFalse);

            Builder._nodes.Add(end);
            return end;
        }

        private ControlFlowNode GetIteratorStart(ControlFlowNode iteratorEnd, int iteratorStartNodeId, ControlFlowNode conditionNode)
        {
            if (iteratorEnd == null) return conditionNode;

            Connect(iteratorEnd, conditionNode);
            return Builder._nodes[iteratorStartNodeId];
        }

        private ControlFlowNode HandleEmbeddedStatement(Statement embeddedStatement, ControlFlowNode source)
        {
            if (embeddedStatement == null || embeddedStatement.IsNull)
                return source;

            var bodyStart = Builder.CreateStartNode(embeddedStatement);

            if (source != null)
                Connect(source, bodyStart);

            return embeddedStatement.AcceptVisitor(this, bodyStart);
        }

        public override ControlFlowNode VisitForeachStatement(ForeachStatement foreachStatement, ControlFlowNode data)
        {
            // <data> foreach (<condition>...) { <bodyStart>embeddedStmt<bodyEnd> } <end>
            var end = Builder.CreateEndNode(foreachStatement, addToNodeList: false);
            var conditionNode = Builder.CreateSpecialNode(foreachStatement, ControlFlowNodeType.LoopCondition);
            Connect(data, conditionNode);

            _breakTargets.Push(end);
            _continueTargets.Push(conditionNode);

            var bodyEnd = HandleEmbeddedStatement(foreachStatement.EmbeddedStatement, conditionNode);
            Connect(bodyEnd, conditionNode);

            _breakTargets.Pop();
            _continueTargets.Pop();

            Connect(conditionNode, end);
            Builder._nodes.Add(end);
            return end;
        }

        public override ControlFlowNode VisitBreakStatement(BreakStatement breakStatement, ControlFlowNode data)
        {
            if (_breakTargets.Count > 0)
                Connect(data, _breakTargets.Peek(), ControlFlowEdgeType.Jump);

            return Builder.CreateEndNode(breakStatement);
        }

        public override ControlFlowNode VisitContinueStatement(ContinueStatement continueStatement, ControlFlowNode data)
        {
            if (_continueTargets.Count > 0)
                Connect(data, _continueTargets.Peek(), ControlFlowEdgeType.Jump);

            return Builder.CreateEndNode(continueStatement);
        }

        public override ControlFlowNode VisitGotoStatement(GotoStatement gotoStatement, ControlFlowNode data)
        {
            Builder._gotoStatements.Add(data);
            return Builder.CreateEndNode(gotoStatement);
        }

        public override ControlFlowNode VisitReturnStatement(ReturnStatement returnStatement, ControlFlowNode data) =>
            Builder.CreateEndNode(returnStatement); // end not connected with data

        public override ControlFlowNode VisitThrowStatement(ThrowStatement throwStatement, ControlFlowNode data) =>
            Builder.CreateEndNode(throwStatement); // end not connected with data

        public override ControlFlowNode VisitTryCatchStatement(TryCatchStatement tryCatchStatement, ControlFlowNode data)
        {
            var end = Builder.CreateEndNode(tryCatchStatement, addToNodeList: false);
            var edge = Connect(HandleEmbeddedStatement(tryCatchStatement.TryBlock, data), end);

            if (!tryCatchStatement.FinallyBlock.IsNull)
                edge.AddJumpOutOfTryFinally(tryCatchStatement);

            foreach (var catchClause in tryCatchStatement.CatchClauses)
            {
                edge = Connect(HandleEmbeddedStatement(catchClause.Body, data), end);

                if (!tryCatchStatement.FinallyBlock.IsNull)
                    edge.AddJumpOutOfTryFinally(tryCatchStatement);
            }

            if (!tryCatchStatement.FinallyBlock.IsNull)
            {
                // Don't connect the end of the try-finally block to anything.
                // Consumers of the CFG will have to special-case try-finally.
                HandleEmbeddedStatement(tryCatchStatement.FinallyBlock, data);
            }

            Builder._nodes.Add(end);
            return end;
        }

        public override ControlFlowNode VisitCheckedStatement(CheckedStatement checkedStatement, ControlFlowNode data)
        {
            var bodyEnd = HandleEmbeddedStatement(checkedStatement.Body, data);
            return CreateConnectedEndNode(checkedStatement, bodyEnd);
        }

        public override ControlFlowNode VisitUncheckedStatement(UncheckedStatement uncheckedStatement, ControlFlowNode data)
        {
            var bodyEnd = HandleEmbeddedStatement(uncheckedStatement.Body, data);
            return CreateConnectedEndNode(uncheckedStatement, bodyEnd);
        }

        public override ControlFlowNode VisitLockStatement(LockStatement lockStatement, ControlFlowNode data)
        {
            var bodyEnd = HandleEmbeddedStatement(lockStatement.EmbeddedStatement, data);
            return CreateConnectedEndNode(lockStatement, bodyEnd);
        }

        public override ControlFlowNode VisitUsingStatement(UsingStatement usingStatement, ControlFlowNode data)
        {
            data = HandleEmbeddedStatement(usingStatement.ResourceAcquisition as Statement, data);
            var bodyEnd = HandleEmbeddedStatement(usingStatement.EmbeddedStatement, data);
            return CreateConnectedEndNode(usingStatement, bodyEnd);
        }

        public override ControlFlowNode VisitYieldReturnStatement(YieldReturnStatement yieldStatement, ControlFlowNode data) =>
            CreateConnectedEndNode(yieldStatement, data);

        public override ControlFlowNode VisitYieldBreakStatement(YieldBreakStatement yieldBreakStatement, ControlFlowNode data) =>
            Builder.CreateEndNode(yieldBreakStatement); // end not connected with data

        public override ControlFlowNode VisitUnsafeStatement(UnsafeStatement unsafeStatement, ControlFlowNode data)
        {
            var bodyEnd = HandleEmbeddedStatement(unsafeStatement.Body, data);
            return CreateConnectedEndNode(unsafeStatement, bodyEnd);
        }

        public override ControlFlowNode VisitFixedStatement(FixedStatement fixedStatement, ControlFlowNode data)
        {
            var bodyEnd = HandleEmbeddedStatement(fixedStatement.EmbeddedStatement, data);
            return CreateConnectedEndNode(fixedStatement, bodyEnd);
        }
    }

    /// <summary>
    /// Debugging helper that exports a control flow graph.
    /// </summary>
    public static GraphVizGraph ExportGraph(IList<ControlFlowNode> nodes)
    {
        var graphVizGraph = new GraphVizGraph();
        var graphVizNodes = new GraphVizNode[nodes.Count];
        var dict = new Dictionary<ControlFlowNode, int>();

        for (var i = 0; i < graphVizNodes.Length; i++)
        {
            dict.Add(nodes[i], i);
            var graphVizNode = new GraphVizNode(i)
            {
                label = CreateName(nodes, i)
            };
            graphVizGraph.AddNode(graphVizNodes[i] = graphVizNode);
        }

        for (var i = 0; i < graphVizNodes.Length; i++)
            foreach (var edge in nodes[i].Outgoing)
            {
                var graphVizEdge = new GraphVizEdge(i, dict[edge.To]);

                if (edge.IsLeavingTryFinally)
                    graphVizEdge.style = "dashed";

                graphVizEdge.color = edge.Type switch
                {
                    ControlFlowEdgeType.ConditionTrue => "green",
                    ControlFlowEdgeType.ConditionFalse => "red",
                    ControlFlowEdgeType.Jump => "blue",
                    _ => graphVizEdge.color
                };

                graphVizGraph.AddEdge(graphVizEdge);
            }

        return graphVizGraph;
    }

    private static string CreateName(IList<ControlFlowNode> nodes, int index)
    {
        var name = $"#{index} = ";

        return nodes[index].Type switch
        {
            ControlFlowNodeType.StartNode => name + nodes[index].NextStatement.DebugToString(),
            ControlFlowNodeType.BetweenStatements => name + nodes[index].NextStatement.DebugToString(),
            ControlFlowNodeType.EndNode => name + "End of " + nodes[index].PreviousStatement.DebugToString(),
            ControlFlowNodeType.LoopCondition => name + "Condition in " + nodes[index].NextStatement.DebugToString(),
            _ => name + "?"
        };
    }
}