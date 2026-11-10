using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="StudioPerspectives"/>. Radii, fonts and colors come from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class StudioPerspectivesToken : AbstractControlDesignToken
{
    /// <summary>The section's padding (24 80).</summary>
    public Thickness SectionPadding { get; set; }

    /// <summary>The section's padding below the compact width (16 48).</summary>
    public Thickness CompactSectionPadding { get; set; }

    /// <summary>The stage is at most this wide (880).</summary>
    public double ContentMaxWidth { get; set; }

    /// <summary>The quotation mark's size (36).</summary>
    public double MarkSize { get; set; }

    /// <summary>The space above the quotation mark (0 28 0 0).</summary>
    public Thickness MarkMargin { get; set; }

    /// <summary>The space above the quote (0 16 0 0).</summary>
    public Thickness QuoteMargin { get; set; }

    /// <summary>The quote's line height at 36 (46).</summary>
    public double QuoteLineHeight { get; set; }

    /// <summary>The quote's line height at 22, below the compact width (31).</summary>
    public double CompactQuoteLineHeight { get; set; }

    /// <summary>The quote's letter spacing (-0.6).</summary>
    public double QuoteLetterSpacing { get; set; }

    /// <summary>The space above the person row (0 36 0 0).</summary>
    public Thickness PersonMargin { get; set; }

    /// <summary>The portrait's size (48).</summary>
    public double PortraitSize { get; set; }

    /// <summary>The space between the portrait and the name (14).</summary>
    public double PersonGap { get; set; }

    /// <summary>An icon button's width (40).</summary>
    public double ButtonWidth { get; set; }

    /// <summary>The space above the progress row (0 32 0 0).</summary>
    public Thickness ProgressMargin { get; set; }

    /// <summary>A progress bar's thickness (3).</summary>
    public double BarHeight { get; set; }

    /// <summary>The space between progress bars (6).</summary>
    public double BarGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        SectionPadding = new Thickness(24, 80);
        CompactSectionPadding = new Thickness(16, 48);
        ContentMaxWidth = 880;
        MarkSize = 36;
        MarkMargin = new Thickness(0, 28, 0, 0);
        QuoteMargin = new Thickness(0, 16, 0, 0);
        QuoteLineHeight = 46;
        CompactQuoteLineHeight = 31;
        QuoteLetterSpacing = -0.6;
        PersonMargin = new Thickness(0, 36, 0, 0);
        PortraitSize = 48;
        PersonGap = 14;
        ButtonWidth = 40;
        ProgressMargin = new Thickness(0, 32, 0, 0);
        BarHeight = 3;
        BarGap = 6;
    }
}
