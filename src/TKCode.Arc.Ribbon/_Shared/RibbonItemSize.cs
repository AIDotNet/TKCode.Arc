namespace TKCode.Arc;

/// <summary>
/// How a ribbon command is laid out: <see cref="Large"/> stacks a large icon over the label and takes the full height of the
/// group, <see cref="Medium"/> puts a small icon beside the label, <see cref="Small"/> shows the icon alone. Medium and small
/// commands stack up to three in a column. The values are ordered: a group that runs out of room moves commands from a
/// larger size to a smaller one.
/// </summary>
public enum RibbonItemSize
{
    /// <summary>The icon alone (the label moves to the tooltip).</summary>
    Small,

    /// <summary>A small icon beside the label, one row high.</summary>
    Medium,

    /// <summary>A large icon over the label, the full height of the group.</summary>
    Large
}
