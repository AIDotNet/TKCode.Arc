namespace TKCode.Arc.Editor;

/// <summary>
/// How a tracked range (a decoration) behaves when text is typed exactly at one of its edges
/// (Monaco's <c>TrackedRangeStickiness</c>).
/// </summary>
public enum TrackedRangeStickiness
{
    /// <summary>The range grows when text is typed at either edge.</summary>
    AlwaysGrowsWhenTypingAtEdges = 0,

    /// <summary>The range does not grow when text is typed at either edge.</summary>
    NeverGrowsWhenTypingAtEdges = 1,

    /// <summary>The range grows only when text is typed at its start.</summary>
    GrowsOnlyWhenTypingBefore = 2,

    /// <summary>The range grows only when text is typed at its end.</summary>
    GrowsOnlyWhenTypingAfter = 3
}
