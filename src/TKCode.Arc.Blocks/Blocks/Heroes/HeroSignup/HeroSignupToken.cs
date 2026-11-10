using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="HeroSignup"/> and the dot color it derives from the neutrals. Radii, fonts and the accent glow
/// come from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class HeroSignupToken : AbstractControlDesignToken
{
    /// <summary>The section's padding (24 112 24 96).</summary>
    public Thickness SectionPadding { get; set; }

    /// <summary>The section's padding below the compact width (20 80 20 72).</summary>
    public Thickness CompactSectionPadding { get; set; }

    /// <summary>The section's padding below the narrow width (16 56 16 48).</summary>
    public Thickness NarrowSectionPadding { get; set; }

    /// <summary>The content column is at most this wide (820).</summary>
    public double ContentMaxWidth { get; set; }

    /// <summary>The copy under the headline is at most this wide (600).</summary>
    public double DescriptionMaxWidth { get; set; }

    /// <summary>The form and the confirmation are at most this wide (500).</summary>
    public double FormMaxWidth { get; set; }

    /// <summary>The eyebrow pill's padding (12 5).</summary>
    public Thickness EyebrowPadding { get; set; }

    /// <summary>The eyebrow's dot (6).</summary>
    public double EyebrowDot { get; set; }

    /// <summary>The space above the headline (0 22 0 0).</summary>
    public Thickness TitleMargin { get; set; }

    /// <summary>The headline's line height at 52 (58).</summary>
    public double TitleLineHeight { get; set; }

    /// <summary>The headline's line height at 36, below the compact width (42).</summary>
    public double CompactTitleLineHeight { get; set; }

    /// <summary>The headline's line height at 28, below the narrow width (34).</summary>
    public double NarrowTitleLineHeight { get; set; }

    /// <summary>The headline's letter spacing (-1.2).</summary>
    public double TitleLetterSpacing { get; set; }

    /// <summary>The space above the copy (0 18 0 0).</summary>
    public Thickness DescriptionMargin { get; set; }

    /// <summary>The copy's line height (28).</summary>
    public double DescriptionLineHeight { get; set; }

    /// <summary>The space above the form (0 36 0 0).</summary>
    public Thickness ActionMargin { get; set; }

    /// <summary>The space between the field and the button (10).</summary>
    public double FormGap { get; set; }

    /// <summary>The confirmation card's padding (16 14).</summary>
    public Thickness DonePadding { get; set; }

    /// <summary>The confirmation's check badge (36).</summary>
    public double DoneBadge { get; set; }

    /// <summary>The space above the proof row (0 30 0 0).</summary>
    public Thickness ProofMargin { get; set; }

    /// <summary>A face of the proof row, ring included (30).</summary>
    public double FaceDiameter { get; set; }

    /// <summary>How much each face overlaps the previous one (9).</summary>
    public double FaceOverlap { get; set; }

    /// <summary>The distance between the dots of the backdrop (22).</summary>
    public double DotSpacing { get; set; }

    /// <summary>The radius of a dot (1).</summary>
    public double DotRadius { get; set; }

    /// <summary>The dots' color at the center of the field: the strong border.</summary>
    public Color DotColor { get; set; }

    /// <summary>The accent glow's strength at its center (0.16; 0.2 dark).</summary>
    public double GlowOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        SectionPadding = new Thickness(24, 112, 24, 96);
        CompactSectionPadding = new Thickness(20, 80, 20, 72);
        NarrowSectionPadding = new Thickness(16, 56, 16, 48);
        ContentMaxWidth = 820;
        DescriptionMaxWidth = 600;
        FormMaxWidth = 500;
        EyebrowPadding = new Thickness(12, 5);
        EyebrowDot = 6;
        TitleMargin = new Thickness(0, 22, 0, 0);
        TitleLineHeight = 58;
        CompactTitleLineHeight = 42;
        NarrowTitleLineHeight = 34;
        TitleLetterSpacing = -1.2;
        DescriptionMargin = new Thickness(0, 18, 0, 0);
        DescriptionLineHeight = 28;
        ActionMargin = new Thickness(0, 36, 0, 0);
        FormGap = 10;
        DonePadding = new Thickness(16, 14);
        DoneBadge = 36;
        ProofMargin = new Thickness(0, 30, 0, 0);
        FaceDiameter = 30;
        FaceOverlap = 9;
        DotSpacing = 22;
        DotRadius = 1;
        DotColor = n.BorderStrong;
        GlowOpacity = isDarkMode ? 0.2 : 0.16;
    }
}
