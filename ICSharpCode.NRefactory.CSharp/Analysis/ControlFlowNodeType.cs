namespace ICSharpCode.NRefactory.CSharp.Analysis;

public enum ControlFlowNodeType
{
    /// <summary>
    /// Unknown node type
    /// </summary>
    None,

    /// <summary>
    /// Node in front of a statement
    /// </summary>
    StartNode,

    /// <summary>
    /// Node between two statements
    /// </summary>
    BetweenStatements,

    /// <summary>
    /// Node at the end of a statement list
    /// </summary>
    EndNode,

    /// <summary>
    /// Node representing the position before evaluating the condition of a loop.
    /// </summary>
    LoopCondition
}