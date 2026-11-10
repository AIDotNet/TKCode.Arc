using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="AiSetupWizard"/> (an original TKCode design). Colors, radii, type sizes and fonts come from
/// ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class AiSetupWizardToken : AbstractControlDesignToken
{
    /// <summary>The widest the wizard grows (860).</summary>
    public double WizardMaxWidth { get; set; }

    /// <summary>The card's padding (24) and its compact form (16).</summary>
    public Thickness CardPadding { get; set; }

    public Thickness CompactCardPadding { get; set; }

    /// <summary>At or below this width the stepper moves above the page and turns horizontal (640).</summary>
    public double CompactWidth { get; set; }

    /// <summary>The stepper column's width (220) and the gap between it and the page (32).</summary>
    public double StepperWidth { get; set; }

    public double ColumnGap { get; set; }

    /// <summary>The page keeps at least this height (360), so the footer does not jump between steps.</summary>
    public double PageMinHeight { get; set; }

    /// <summary>Space between the title, the body and the footer (20).</summary>
    public double SectionGap { get; set; }

    /// <summary>A test check's glyph (18) and the gap after it (10).</summary>
    public double CheckGlyphSize { get; set; }

    public double CheckGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        WizardMaxWidth = 860;
        CardPadding = new Thickness(24);
        CompactCardPadding = new Thickness(16);
        CompactWidth = 640;
        StepperWidth = 220;
        ColumnGap = 32;
        PageMinHeight = 360;
        SectionGap = 20;
        CheckGlyphSize = 18;
        CheckGap = 10;
    }
}
