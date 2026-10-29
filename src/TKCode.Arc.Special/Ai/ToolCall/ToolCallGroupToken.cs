using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ToolCallGroup"/>: the card (1px Border, radius 16), the 48px header (a 40px button 4px in, 12px
/// in from its sides, parts 10px apart), the header type metrics and the list's padding under its BorderSubtle line.
/// </summary>
[ControlDesignToken]
internal sealed class ToolCallGroupToken : AbstractControlDesignToken
{
    public CornerRadius CardCornerRadius { get; set; }

    public Thickness HeadOuterPadding { get; set; }

    public double HeadButtonHeight { get; set; }

    public Thickness HeadInlinePadding { get; set; }

    /// <summary>The space between the stack, the label, the meta and the chevron.</summary>
    public double HeadGap { get; set; }

    /// <summary>The label (14/19.6, letter spacing −0.14) and the meta (12/16.8).</summary>
    public double LabelLineHeight { get; set; }

    public double LabelLetterSpacing { get; set; }

    public double MetaLineHeight { get; set; }

    /// <summary>The list: 4px around the rows, under a 1px BorderSubtle line.</summary>
    public Thickness ListPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardCornerRadius = new CornerRadius(16);
        HeadOuterPadding = new Thickness(4);
        HeadButtonHeight = 40;
        HeadInlinePadding = new Thickness(12, 0);
        HeadGap = 10;
        LabelLineHeight = 19.6;
        LabelLetterSpacing = -0.14;
        MetaLineHeight = 16.8;
        ListPadding = new Thickness(4);
    }
}
