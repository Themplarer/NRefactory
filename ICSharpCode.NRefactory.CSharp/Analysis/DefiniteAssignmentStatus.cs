namespace ICSharpCode.NRefactory.CSharp.Analysis;

/// <summary>
/// Represents the definite assignment status of a variable at a specific location.
/// </summary>
public enum DefiniteAssignmentStatus
{
    /// <summary>
    /// The variable might be assigned or unassigned.
    /// </summary>
    PotentiallyAssigned,

    /// <summary>
    /// The variable is definitely assigned.
    /// </summary>
    DefinitelyAssigned,

    /// <summary>
    /// The variable is definitely assigned iff the expression results in the value 'true'.
    /// </summary>
    AssignedAfterTrueExpression,

    /// <summary>
    /// The variable is definitely assigned iff the expression results in the value 'false'.
    /// </summary>
    AssignedAfterFalseExpression,

    /// <summary>
    /// The code is unreachable.
    /// </summary>
    CodeUnreachable
}