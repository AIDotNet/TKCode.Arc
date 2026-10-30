using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="SketchToImage"/>: the card's radius, padding and maximum widths per size, the canvas's radius,
/// the pencil and pen tip per size, the progress line, the caption's type and the percent's fixed width. Colors and fonts
/// come from the ArcDesign tokens in the theme; the scene's own colors belong to the picture.
/// </summary>
[ControlDesignToken]
internal sealed class SketchToImageToken : AbstractControlDesignToken
{
    /// <summary>The card's corner radius.</summary>
    public CornerRadius CardCornerRadius { get; set; }

    /// <summary>The space inside the card's edge.</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>The card's padding at the small size.</summary>
    public Thickness CardPaddingSM { get; set; }

    /// <summary>The widest the card grows at each size (Arc: 340, 520 and 720px).</summary>
    public double MaxWidthSM { get; set; }

    public double MaxWidthMD { get; set; }

    public double MaxWidthLG { get; set; }

    /// <summary>The canvas's corner radius.</summary>
    public CornerRadius CanvasCornerRadius { get; set; }

    /// <summary>The pencil's width on screen at each size, so lines stay crisp at every scale.</summary>
    public double PencilWidthSM { get; set; }

    public double PencilWidthMD { get; set; }

    public double PencilWidthLG { get; set; }

    /// <summary>The pen tip's diameter on screen.</summary>
    public double TipSize { get; set; }

    /// <summary>The progress line's height and the space around it.</summary>
    public double ProgressHeight { get; set; }

    public Thickness ProgressMargin { get; set; }

    /// <summary>The caption under the progress line: the gap between its columns and between the label and the prompt.</summary>
    public double CaptionGap { get; set; }

    public double CaptionLineGap { get; set; }

    /// <summary>The stage label: icon size, icon-to-word gap and line height.</summary>
    public double StageGlyphSize { get; set; }

    public double StageGap { get; set; }

    public double StageLineHeight { get; set; }

    /// <summary>The prompt: 12/16, muted, one line.</summary>
    public double PromptFontSize { get; set; }

    public double PromptLineHeight { get; set; }

    /// <summary>The percent's fixed width, so nothing shifts as it counts.</summary>
    public double PercentWidth { get; set; }

    /// <summary>The Replay button: 28px high, its icon 14px.</summary>
    public double ReplayHeight { get; set; }

    public Thickness ReplayPadding { get; set; }

    public Thickness ReplayIconPadding { get; set; }

    public double ReplayGlyphSize { get; set; }

    /// <summary>Below this card width the Replay button drops to its icon.</summary>
    public double CompactWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardCornerRadius = new CornerRadius(20);
        CardPadding = new Thickness(12, 12, 12, 14);
        CardPaddingSM = new Thickness(10, 10, 10, 12);
        MaxWidthSM = 340;
        MaxWidthMD = 520;
        MaxWidthLG = 720;
        CanvasCornerRadius = new CornerRadius(12);
        PencilWidthSM = 1.25;
        PencilWidthMD = 1.5;
        PencilWidthLG = 1.75;
        TipSize = 7;
        ProgressHeight = 2;
        ProgressMargin = new Thickness(0, 12, 0, 10);
        CaptionGap = 10;
        CaptionLineGap = 2;
        StageGlyphSize = 16;
        StageGap = 6;
        StageLineHeight = 20;
        PromptFontSize = 12;
        PromptLineHeight = 16;
        PercentWidth = 40;
        ReplayHeight = 28;
        ReplayPadding = new Thickness(10, 0);
        ReplayIconPadding = new Thickness(7, 0);
        ReplayGlyphSize = 14;
        CompactWidth = 400;
    }
}
