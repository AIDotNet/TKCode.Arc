using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="CodeMagicMove"/> (Arc Code magic move, design reference): a card with a header, the code
/// area and a footer with the caption and the step controls. Colors follow the design tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class CodeMagicMoveToken : AbstractControlDesignToken
{
    /// <summary>Size of the header, caption and button text.</summary>
    public double TextSize { get; set; }

    /// <summary>Size of the description and of the line counts.</summary>
    public double SmallTextSize { get; set; }

    /// <summary>Space inside the header.</summary>
    public Thickness HeaderPadding { get; set; }

    /// <summary>Space between the header's file name and its line counts and button.</summary>
    public double HeaderGap { get; set; }

    /// <summary>Space between the file icon and the file name.</summary>
    public double FileGap { get; set; }

    /// <summary>Size of the file, play, pause and chevron icons.</summary>
    public double IconSize { get; set; }

    /// <summary>Stroke of the icons.</summary>
    public double IconStroke { get; set; }

    /// <summary>Space between the added and removed counts.</summary>
    public double CountGap { get; set; }

    /// <summary>Space between the counts and the play button.</summary>
    public double MetaGap { get; set; }

    /// <summary>Width and height of the round icon buttons.</summary>
    public double ButtonSize { get; set; }

    /// <summary>Space around the code (wide layout).</summary>
    public Thickness CodePadding { get; set; }

    /// <summary>Space around the code (narrow layout).</summary>
    public Thickness CodePaddingCompact { get; set; }

    /// <summary>The largest code size; the code shrinks so the widest step fits.</summary>
    public double CodeFontSize { get; set; }

    /// <summary>The smallest code size.</summary>
    public double MinCodeFontSize { get; set; }

    /// <summary>Line height as a multiple of the code size.</summary>
    public double CodeLineSpacing { get; set; }

    /// <summary>Space between the line numbers and the code, in character widths.</summary>
    public double GutterGap { get; set; }

    /// <summary>Width of the accent hairline at the left of a changed line band.</summary>
    public double BandRuleWidth { get; set; }

    /// <summary>Opacity of the accent fill of a changed line band.</summary>
    public double BandFillOpacity { get; set; }

    /// <summary>Width of the typing caret.</summary>
    public double CaretWidth { get; set; }

    /// <summary>Height of the typing caret as a multiple of the code size.</summary>
    public double CaretHeight { get; set; }

    /// <summary>Space inside the footer (wide layout).</summary>
    public Thickness FooterPadding { get; set; }

    /// <summary>Space inside the footer (narrow layout).</summary>
    public Thickness FooterPaddingCompact { get; set; }

    /// <summary>Space between the caption and the step controls.</summary>
    public double FooterGap { get; set; }

    /// <summary>Below this width the caption takes its own row and the step controls spread across.</summary>
    public double CompactWidth { get; set; }

    /// <summary>Width of a step dot's touch target.</summary>
    public double DotWidth { get; set; }

    /// <summary>Height of a step dot's touch target.</summary>
    public double DotHeight { get; set; }

    /// <summary>Diameter of a step dot.</summary>
    public double PipSize { get; set; }

    /// <summary>Width of the current step's dot.</summary>
    public double ActivePipWidth { get; set; }

    /// <summary>How far the code can be pulled past the first or last step before it stops giving.</summary>
    public double RubberBandLimit { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TextSize = 13;
        SmallTextSize = 12;
        HeaderPadding = new Thickness(16, 8, 8, 8);
        HeaderGap = 12;
        FileGap = 8;
        IconSize = 16;
        IconStroke = 1.75;
        CountGap = 6;
        MetaGap = 8;
        ButtonSize = 36;
        CodePadding = new Thickness(20, 18);
        CodePaddingCompact = new Thickness(16, 18);
        CodeFontSize = 13;
        MinCodeFontSize = 10.5;
        CodeLineSpacing = 1.7;
        GutterGap = 2.4;
        BandRuleWidth = 2;
        BandFillOpacity = isDarkMode ? 0.11 : 0.07;
        CaretWidth = 2;
        CaretHeight = 1.3;
        FooterPadding = new Thickness(16, 8, 8, 8);
        FooterPaddingCompact = new Thickness(12, 12, 8, 4);
        FooterGap = 12;
        CompactWidth = 520;
        DotWidth = 20;
        DotHeight = 44;
        PipSize = 6;
        ActivePipWidth = 24;
        RubberBandLimit = 40;
    }
}
