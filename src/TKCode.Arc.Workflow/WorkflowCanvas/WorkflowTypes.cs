namespace TKCode.Arc;

/// <summary>Where one step of a workflow run stands (<see cref="WorkflowNode.State"/>).</summary>
public enum WorkflowNodeState
{
    /// <summary>No run: the card shows its title and description without a state mark (editing a workflow).</summary>
    Idle,

    /// <summary>Waiting for the run to reach the step: an empty ring.</summary>
    Pending,

    /// <summary>The step is running: a spinning ring, and the card takes the accent.</summary>
    Running,

    /// <summary>Finished: the time it took and a check; the description gives way to the result.</summary>
    Done,

    /// <summary>Failed: an alert mark and the error under the title.</summary>
    Failed,

    /// <summary>Not run on this path (a branch went the other way): the card fades and its edges turn dashed.</summary>
    Skipped
}

/// <summary>How an edge shows the run (<see cref="WorkflowEdge.State"/>).</summary>
public enum WorkflowEdgeState
{
    /// <summary>
    /// Follows its two steps: once the source is done and the target runs, is done or failed, the data draws along the
    /// edge; when either step is skipped, the edge turns dashed; otherwise it is a quiet wire.
    /// </summary>
    Auto,

    /// <summary>A quiet wire.</summary>
    Idle,

    /// <summary>Data went along the edge: it draws in the accent.</summary>
    Active,

    /// <summary>The run did not take this edge: a dashed, fainter wire.</summary>
    Skipped
}

/// <summary>How a step's card is laid out (<see cref="WorkflowNode.Appearance"/>).</summary>
public enum WorkflowNodeAppearance
{
    /// <summary>One row, 300 × 60: icon, title over description, state on the right (Arc's main steps).</summary>
    Standard,

    /// <summary>Two rows, 200 × 84: icon and state on top, title and description below (Arc's action steps).</summary>
    Compact
}

/// <summary>How a <see cref="WorkflowCanvas"/> fits the workflow into its bounds.</summary>
public enum WorkflowAutoFit
{
    /// <summary>The view stays where <see cref="WorkflowCanvas.Zoom"/> and <see cref="WorkflowCanvas.Offset"/> put it.</summary>
    None,

    /// <summary>Actual size, centered, shrinking the whole workflow when it does not fit (like a screenshot; Arc's hero).</summary>
    ShrinkToFit,

    /// <summary>As large as fits, within <see cref="WorkflowCanvas.MaxZoom"/>.</summary>
    Fit
}

/// <summary>Which way a workflow runs on the canvas.</summary>
public enum WorkflowLayoutDirection
{
    /// <summary>Steps run down the canvas; edges leave a card's bottom and enter the next card's top (Arc).</summary>
    TopToBottom,

    /// <summary>Steps run across the canvas; edges leave a card's right side and enter the next card's left side.</summary>
    LeftToRight
}

/// <summary>How a <see cref="WorkflowCanvas"/> draws its edges.</summary>
public enum WorkflowEdgeStyle
{
    /// <summary>Curves that leave and enter straight, with handles of half the gap (Arc's workflow graph).</summary>
    Bezier,

    /// <summary>Right angles: out, across halfway, and in, with rounded corners.</summary>
    Step,

    /// <summary>Straight lines from card to card.</summary>
    Straight
}
