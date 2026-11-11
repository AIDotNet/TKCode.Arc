namespace TKCode.Arc;

/// <summary>Where a <see cref="WaterfallPanel"/> takes each child's size along the flow (its height in columns, its width in rows).</summary>
public enum WaterfallItemSizing
{
    /// <summary>From the child's own measurement, clamped to <c>MinItemSize</c>..<c>MaxItemSize</c> (default).</summary>
    Content,

    /// <summary>
    /// A size between <c>MinItemSize</c> and <c>MaxItemSize</c> picked from the child's index and <c>Seed</c>, whatever its
    /// content: children built from one template still get different heights. The same index and seed always give the same size.
    /// </summary>
    Varied
}
