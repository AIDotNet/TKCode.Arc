using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ToolCall"/> (Arc Tool call, design reference): the row's radius, the standalone frame, the
/// inline buttons, the type metrics of the title, the reason and the meta, and the approval outline's strength. Colors,
/// fonts and the focus ring come from the ArcDesign tokens in the theme; the head's column layout is measured in
/// <c>ToolCallHeadPanel</c>.
/// </summary>
[ControlDesignToken]
internal sealed class ToolCallToken : AbstractControlDesignToken
{
    /// <summary>A row's corner radius (its hover fill, focus ring and approval outline).</summary>
    public CornerRadius RowCornerRadius { get; set; }

    /// <summary>The frame of a row on its own: radius 16 (the group card's), 4px around the row.</summary>
    public CornerRadius FrameCornerRadius { get; set; }

    public Thickness FramePadding { get; set; }

    /// <summary>Retry, Deny and Allow: 28px high, 12px in from the sides, 6px apart, 12px medium text.</summary>
    public double ActionHeight { get; set; }

    public Thickness ActionInlinePadding { get; set; }

    public double ActionGap { get; set; }

    public double ActionIconSize { get; set; }

    /// <summary>The title: 14/20, letter spacing −0.14.</summary>
    public double TitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    /// <summary>The reason line (12/18) and the meta (12/16.8, its parts 6px apart).</summary>
    public double NoteLineHeight { get; set; }

    public double MetaLineHeight { get; set; }

    public double MetaGap { get; set; }

    /// <summary>The approval row: Accent at 3% under it and an inset 1px Accent outline at 50%.</summary>
    public double ApprovalTintOpacity { get; set; }

    public double ApprovalOutlineOpacity { get; set; }

    /// <summary>The head's hover fill: SurfaceMuted at 75%.</summary>
    public double HoverOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        RowCornerRadius = new CornerRadius(12);
        FrameCornerRadius = new CornerRadius(16);
        FramePadding = new Thickness(4);
        ActionHeight = 28;
        ActionInlinePadding = new Thickness(12, 0);
        ActionGap = 6;
        ActionIconSize = 14;
        TitleLineHeight = 20;
        TitleLetterSpacing = -0.14;
        NoteLineHeight = 18;
        MetaLineHeight = 16.8;
        MetaGap = 6;
        ApprovalTintOpacity = 0.03;
        ApprovalOutlineOpacity = 0.5;
        HoverOpacity = 0.75;
    }
}
