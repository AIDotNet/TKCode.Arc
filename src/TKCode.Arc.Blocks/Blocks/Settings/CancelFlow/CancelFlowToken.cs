using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="CancelFlow"/> and the tint of its completed steps.</summary>
[ControlDesignToken]
internal sealed class CancelFlowToken : AbstractControlDesignToken
{
    /// <summary>Space inside the card.</summary>
    public Thickness FrameInset { get; set; }

    /// <summary>Space inside the card when it is narrower than <see cref="NarrowWidth"/>.</summary>
    public Thickness FrameInsetNarrow { get; set; }

    /// <summary>The card's default maximum width (the suggested width of the flow).</summary>
    public double PreferredWidth { get; set; }

    /// <summary>Below this width the actions stack and the card tightens its padding.</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Space under the step bar.</summary>
    public Thickness HeaderMargin { get; set; }

    /// <summary>Height of a step bar segment.</summary>
    public double SegmentHeight { get; set; }

    /// <summary>Gap between step bar segments.</summary>
    public double SegmentGap { get; set; }

    /// <summary>A completed segment: the foreground at 30%.</summary>
    public Color SegmentDone { get; set; }

    /// <summary>Side of the round back button (also the height of the step bar row).</summary>
    public double BackDiameter { get; set; }

    /// <summary>Space between the back button and the step bar.</summary>
    public Thickness BackMargin { get; set; }

    /// <summary>Space between the step bar and the step counter.</summary>
    public Thickness CounterMargin { get; set; }

    /// <summary>Line height of step titles.</summary>
    public double TitleLineHeight { get; set; }

    /// <summary>Line height of body text (1.4 × text-sm).</summary>
    public double BodyLineHeight { get; set; }

    /// <summary>Space between a step title and its description.</summary>
    public Thickness DescriptionMargin { get; set; }

    /// <summary>Space between a step's description and its content.</summary>
    public Thickness BodyMargin { get; set; }

    /// <summary>Space above the feedback field.</summary>
    public Thickness FeedbackMargin { get; set; }

    /// <summary>Space above a step's buttons.</summary>
    public Thickness ActionsMargin { get; set; }

    /// <summary>Space beside the secondary button (between it and the primary one).</summary>
    public Thickness SecondaryMargin { get; set; }

    /// <summary>Space above the secondary button when the buttons are stacked.</summary>
    public Thickness SecondaryMarginNarrow { get; set; }

    /// <summary>Side of the offer's icon tile.</summary>
    public double OfferTile { get; set; }

    /// <summary>Corner radius of the offer's icon tile.</summary>
    public CornerRadius OfferTileRadius { get; set; }

    /// <summary>Size of the offer's glyph.</summary>
    public double OfferGlyph { get; set; }

    /// <summary>Space between the offer tile and the offer title.</summary>
    public Thickness OfferTitleMargin { get; set; }

    /// <summary>Space inside the box that lists what ends.</summary>
    public Thickness LossesInset { get; set; }

    /// <summary>Corner radius of the box that lists what ends.</summary>
    public CornerRadius LossesRadius { get; set; }

    /// <summary>Gap between the items that end.</summary>
    public double LossesGap { get; set; }

    /// <summary>Gap between an item's mark and its text.</summary>
    public double LossMarkGap { get; set; }

    /// <summary>Space around the content of the last steps.</summary>
    public Thickness ResultInset { get; set; }

    /// <summary>Diameter of the success mark on the last steps.</summary>
    public double ResultMark { get; set; }

    /// <summary>Size of the drawn check inside the success mark.</summary>
    public double ResultGlyph { get; set; }

    /// <summary>Maximum width of the centered sentence on the last steps.</summary>
    public double ResultTextWidth { get; set; }

    /// <summary>Space between the success mark and the title on the last steps.</summary>
    public Thickness ResultTitleMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        FrameInset = new Thickness(28);
        FrameInsetNarrow = new Thickness(20, 22);
        PreferredWidth = 520;
        NarrowWidth = 440;
        HeaderMargin = new Thickness(0, 0, 0, 22);
        SegmentHeight = 4;
        SegmentGap = 6;
        SegmentDone = Oklch.WithAlpha(n.Foreground, 0.3);
        BackDiameter = 32;
        BackMargin = new Thickness(-6, 0, 12, 0);
        CounterMargin = new Thickness(14, 0, 0, 0);
        TitleLineHeight = 28;
        BodyLineHeight = 14 * 1.4;
        DescriptionMargin = new Thickness(0, 6, 0, 0);
        BodyMargin = new Thickness(0, 20, 0, 0);
        FeedbackMargin = new Thickness(0, 16, 0, 0);
        ActionsMargin = new Thickness(0, 28, 0, 0);
        SecondaryMargin = new Thickness(0, 0, 8, 0);
        SecondaryMarginNarrow = new Thickness(0, 8, 0, 0);
        OfferTile = 52;
        OfferTileRadius = new CornerRadius(16);
        OfferGlyph = 24;
        OfferTitleMargin = new Thickness(0, 20, 0, 0);
        LossesInset = new Thickness(16, 14);
        LossesRadius = new CornerRadius(16);
        LossesGap = 10;
        LossMarkGap = 10;
        ResultInset = new Thickness(0, 12, 0, 4);
        ResultMark = 56;
        ResultGlyph = 28;
        ResultTextWidth = 380;
        ResultTitleMargin = new Thickness(0, 20, 0, 0);
    }
}
