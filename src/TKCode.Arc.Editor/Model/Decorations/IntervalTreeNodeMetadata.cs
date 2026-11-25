// Ported from vscode's src/vs/editor/common/model/intervalTree.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
// The metadata bit layout and the delta normalization bounds.

namespace TKCode.Arc.Editor.Model;

internal enum IntervalNodeColor
{
    Black = 0,
    Red = 1,
}

/// <summary>
/// Reads and writes the bit-packed metadata of an <see cref="IntervalNode"/>.
/// </summary>
internal static class IntervalTreeNodeMetadata
{
    private const int ColorMask = 0b0000_0001;
    private const int ColorMaskInverse = 0b1111_1110;
    private const int ColorOffset = 0;

    private const int IsVisitedMask = 0b0000_0010;
    private const int IsVisitedMaskInverse = 0b1111_1101;
    private const int IsVisitedOffset = 1;

    private const int IsForValidationMask = 0b0000_0100;
    private const int IsForValidationMaskInverse = 0b1111_1011;
    private const int IsForValidationOffset = 2;

    private const int StickinessMask = 0b0001_1000;
    private const int StickinessMaskInverse = 0b1110_0111;
    private const int StickinessOffset = 3;

    private const int CollapseOnReplaceEditMask = 0b0010_0000;
    private const int CollapseOnReplaceEditMaskInverse = 0b1101_1111;
    private const int CollapseOnReplaceEditOffset = 5;

    private const int IsMarginMask = 0b0100_0000;
    private const int IsMarginMaskInverse = 0b1011_1111;
    private const int IsMarginOffset = 6;

    private const int AffectsFontMask = 0b1000_0000;
    private const int AffectsFontMaskInverse = 0b0111_1111;
    private const int AffectsFontOffset = 7;

    /// <summary>
    /// Due to how deletion works (in order to avoid always walking the right subtree of the deleted node),
    /// the deltas for nodes can grow and shrink dramatically. It has been observed, in practice, that unless
    /// the deltas are corrected, integer overflow will occur. The bounds keep a delta within 30 bits plus sign,
    /// which still leaves room in an <see cref="int"/>.
    /// </summary>
    internal const int MinSafeDelta = -(1 << 30);

    /// <summary>The upper bound that triggers delta normalization; see <see cref="MinSafeDelta"/>.</summary>
    internal const int MaxSafeDelta = 1 << 30;

    internal static IntervalNodeColor GetNodeColor(IntervalNode node) =>
        (IntervalNodeColor)((node.Metadata & ColorMask) >> ColorOffset);

    internal static void SetNodeColor(IntervalNode node, IntervalNodeColor color)
    {
        node.Metadata = (node.Metadata & ColorMaskInverse) | ((int)color << ColorOffset);
    }

    internal static bool GetNodeIsVisited(IntervalNode node) =>
        ((node.Metadata & IsVisitedMask) >> IsVisitedOffset) == 1;

    internal static void SetNodeIsVisited(IntervalNode node, bool value)
    {
        node.Metadata = (node.Metadata & IsVisitedMaskInverse) | ((value ? 1 : 0) << IsVisitedOffset);
    }

    internal static bool GetNodeIsForValidation(IntervalNode node) =>
        ((node.Metadata & IsForValidationMask) >> IsForValidationOffset) == 1;

    internal static void SetNodeIsForValidation(IntervalNode node, bool value)
    {
        node.Metadata = (node.Metadata & IsForValidationMaskInverse) | ((value ? 1 : 0) << IsForValidationOffset);
    }

    internal static bool GetNodeIsInGlyphMargin(IntervalNode node) =>
        ((node.Metadata & IsMarginMask) >> IsMarginOffset) == 1;

    internal static void SetNodeIsInGlyphMargin(IntervalNode node, bool value)
    {
        node.Metadata = (node.Metadata & IsMarginMaskInverse) | ((value ? 1 : 0) << IsMarginOffset);
    }

    internal static bool GetNodeAffectsFont(IntervalNode node) =>
        ((node.Metadata & AffectsFontMask) >> AffectsFontOffset) == 1;

    internal static void SetNodeAffectsFont(IntervalNode node, bool value)
    {
        node.Metadata = (node.Metadata & AffectsFontMaskInverse) | ((value ? 1 : 0) << AffectsFontOffset);
    }

    internal static TrackedRangeStickiness GetNodeStickiness(IntervalNode node) =>
        (TrackedRangeStickiness)((node.Metadata & StickinessMask) >> StickinessOffset);

    private static void SetNodeStickinessRaw(IntervalNode node, int stickiness)
    {
        node.Metadata = (node.Metadata & StickinessMaskInverse) | (stickiness << StickinessOffset);
    }

    internal static void SetNodeStickiness(IntervalNode node, TrackedRangeStickiness stickiness)
    {
        SetNodeStickinessRaw(node, (int)stickiness);
    }

    internal static bool GetCollapseOnReplaceEdit(IntervalNode node) =>
        ((node.Metadata & CollapseOnReplaceEditMask) >> CollapseOnReplaceEditOffset) == 1;

    internal static void SetCollapseOnReplaceEdit(IntervalNode node, bool value)
    {
        node.Metadata = (node.Metadata & CollapseOnReplaceEditMaskInverse) | ((value ? 1 : 0) << CollapseOnReplaceEditOffset);
    }
}
