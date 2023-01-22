namespace ICSharpCode.NRefactory.CSharp.Analysis;

public enum ControlFlowEdgeType
{
    /// <summary>
    /// Regular control flow.
    /// </summary>
    Normal,

    /// <summary>
    /// Conditional control flow (edge taken if condition is true)
    /// </summary>
    ConditionTrue,

    /// <summary>
    /// Conditional control flow (edge taken if condition is false)
    /// </summary>
    ConditionFalse,

    /// <summary>
    /// A jump statement (goto, goto case, break or continue)
    /// </summary>
    Jump
}