using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry and timing of <see cref="ModelSummary"/>, measured from the model picker's summary (design reference only,
/// ADR 0015). Colors, radii and fonts come from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ModelSummaryToken : AbstractControlDesignToken
{
    /// <summary>The widest the summary grows (the picker's stage is min(440, available)).</summary>
    public double SummaryMaxWidth { get; set; }

    /// <summary>Space between the description, the facts, the note and the change line (grid row gap 12).</summary>
    public double RowGap { get; set; }

    /// <summary>The description slot keeps at least this height (48), so a one- or two-line description never moves the facts.</summary>
    public double DescriptionMinHeight { get; set; }

    /// <summary>The description paragraph is at most this wide (380) and centered.</summary>
    public double DescriptionMaxWidth { get; set; }

    /// <summary>Line height of the description (14 / 21).</summary>
    public double DescriptionLineHeight { get; set; }

    /// <summary>Letter spacing of 14px text (−0.01em).</summary>
    public double BodyLetterSpacing { get; set; }

    /// <summary>Letter spacing of 12px text (−0.01em).</summary>
    public double SmallLetterSpacing { get; set; }

    /// <summary>The facts table sits a little lower than the row gap (margin-top 4).</summary>
    public Thickness FactsMargin { get; set; }

    /// <summary>Padding of a fact cell: 11 on top, 8 on the sides, 12 below.</summary>
    public Thickness FactPadding { get; set; }

    /// <summary>Space between a fact's label and its value (3).</summary>
    public double FactGap { get; set; }

    /// <summary>Line height of a fact label (12 / 15.6).</summary>
    public double FactLabelLineHeight { get; set; }

    /// <summary>Line height of a fact value (14 / 18.2).</summary>
    public double FactValueLineHeight { get; set; }

    /// <summary>At or below this width the four facts fold into two rows of two (400).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Line height of the note and of the change line (12 / 16.8).</summary>
    public double SmallLineHeight { get; set; }

    /// <summary>The change line's slot is always this high (20), shown or not, so nothing below it moves.</summary>
    public double NoticeHeight { get; set; }

    /// <summary>The green check before the change text (14px Lucide check at stroke 2) and its gap to the text (6).</summary>
    public double NoticeGlyphSize { get; set; }

    public double NoticeGlyphStroke { get; set; }

    public Thickness NoticeGlyphMargin { get; set; }

    /// <summary>How long the old description fades out when the model changes (120ms; the new one fades in over 160ms).</summary>
    public TimeSpan DescriptionExitDuration { get; set; }

    /// <summary>How long a changed fact's old value fades out (100ms; the new value fades in over 160ms).</summary>
    public TimeSpan FactExitDuration { get; set; }

    /// <summary>Fade-in of new descriptions, values and change lines (160ms).</summary>
    public TimeSpan SwapEnterDuration { get; set; }

    /// <summary>Fade-out of a change line, when it expires or a new one replaces it (120ms).</summary>
    public TimeSpan NoticeExitDuration { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        SummaryMaxWidth = 440;
        RowGap = 12;
        DescriptionMinHeight = 48;
        DescriptionMaxWidth = 380;
        DescriptionLineHeight = 21;
        BodyLetterSpacing = 14 * -0.01;
        SmallLetterSpacing = 12 * -0.01;
        FactsMargin = new Thickness(0, 4, 0, 0);
        FactPadding = new Thickness(8, 11, 8, 12);
        FactGap = 3;
        FactLabelLineHeight = 12 * 1.3;
        FactValueLineHeight = 14 * 1.3;
        CompactWidth = 400;
        SmallLineHeight = 12 * 1.4;
        NoticeHeight = 20;
        NoticeGlyphSize = 14;
        NoticeGlyphStroke = 2;
        NoticeGlyphMargin = new Thickness(0, 0, 6, 0);
        DescriptionExitDuration = TimeSpan.FromMilliseconds(120);
        FactExitDuration = TimeSpan.FromMilliseconds(100);
        SwapEnterDuration = TimeSpan.FromMilliseconds(160);
        NoticeExitDuration = TimeSpan.FromMilliseconds(120);
    }
}
