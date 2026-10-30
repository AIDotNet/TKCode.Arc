using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ReasoningBlock"/>: the card's radius, the head's height and padding, the mark, the type metrics
/// of the label, the streaming preview and the steps, the rail with its dots, the footer and the Stop / Retry buttons.
/// Colors, fonts and the focus ring come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ReasoningBlockToken : AbstractControlDesignToken
{
    /// <summary>The card's corner radius.</summary>
    public CornerRadius CardCornerRadius { get; set; }

    /// <summary>The head button's height, and the padding the card's rows share (the sides; plain drops it).</summary>
    public double HeadHeight { get; set; }

    public Thickness CardPadding { get; set; }

    /// <summary>The space between the mark, the label, the time and the buttons.</summary>
    public double HeadGap { get; set; }

    public double MarkSize { get; set; }

    /// <summary>The label: 14/20, letter spacing −0.14.</summary>
    public double LabelLineHeight { get; set; }

    public double LabelLetterSpacing { get; set; }

    /// <summary>The streaming preview under the head: 13/20, in line with the label.</summary>
    public double PreviewFontSize { get; set; }

    public double PreviewLineHeight { get; set; }

    public Thickness PreviewMargin { get; set; }

    /// <summary>How many lines of the latest reasoning the preview shows.</summary>
    public int PreviewMaxLines { get; set; }

    /// <summary>The preview's fixed height (its lines times their height), so streaming never changes the block's height.</summary>
    public double PreviewHeight { get; set; }

    /// <summary>The open part: the steps, the reason of a failure and the footer.</summary>
    public Thickness BodyPadding { get; set; }

    /// <summary>A step: its type, the space under it and between its heading and text.</summary>
    public double StepFontSize { get; set; }

    public double StepHeadingLineHeight { get; set; }

    public double StepTextLineHeight { get; set; }

    public double StepTextGap { get; set; }

    public double StepGap { get; set; }

    /// <summary>The rail down the steps: its column, the hairline's width, the dot's size and its top, and where the line ends on the last step.</summary>
    public double RailWidth { get; set; }

    public double RailLineWidth { get; set; }

    public double DotSize { get; set; }

    public double DotTop { get; set; }

    public double DotStrokeWidth { get; set; }

    /// <summary>Where the dot's center is (its top plus half its size): the first step's line starts there, the last one's ends there.</summary>
    public double RailCenter { get; set; }

    public Thickness RailFirstMargin { get; set; }

    public Thickness DotMargin { get; set; }

    /// <summary>The footer: its height and padding, the status text and the 28px copy button.</summary>
    public double FooterHeight { get; set; }

    public Thickness FooterMargin { get; set; }

    public double StatusFontSize { get; set; }

    public double StatusGlyphSize { get; set; }

    public double StatusGap { get; set; }

    /// <summary>Stop and Retry: 28px high, 10px in from the sides, 12px medium text.</summary>
    public double ActionHeight { get; set; }

    public Thickness ActionInlinePadding { get; set; }

    /// <summary>The head's hover fill: SurfaceMuted at 60%.</summary>
    public double HoverOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardCornerRadius = new CornerRadius(16);
        HeadHeight = 44;
        CardPadding = new Thickness(14, 0, 8, 0);
        HeadGap = 8;
        MarkSize = 18;
        LabelLineHeight = 20;
        LabelLetterSpacing = -0.14;
        PreviewFontSize = 13;
        PreviewLineHeight = 20;
        PreviewMargin = new Thickness(26, 0, 0, 12);
        PreviewMaxLines = 2;
        PreviewHeight = PreviewMaxLines * PreviewLineHeight;
        BodyPadding = new Thickness(0, 4, 0, 6);
        StepFontSize = 13;
        StepHeadingLineHeight = 20;
        StepTextLineHeight = 20;
        StepTextGap = 2;
        StepGap = 16;
        RailWidth = 16;
        RailLineWidth = 1;
        DotSize = 7;
        DotTop = 7;
        DotStrokeWidth = 1.5;
        RailCenter = DotTop + DotSize / 2;
        RailFirstMargin = new Thickness(0, RailCenter, 0, 0);
        DotMargin = new Thickness(0, DotTop, 0, 0);
        FooterHeight = 32;
        FooterMargin = new Thickness(0, 4, 0, 6);
        StatusFontSize = 12;
        StatusGlyphSize = 14;
        StatusGap = 6;
        ActionHeight = 28;
        ActionInlinePadding = new Thickness(10, 0);
        HoverOpacity = 0.6;
    }
}
