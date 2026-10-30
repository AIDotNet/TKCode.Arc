using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="DomainVerify"/> (design reference only, ADR 0027) and the accent-independent tints it derives
/// from the neutrals. Colors that follow the accent, the type ramp and the fonts come from ArcDesign tokens in the theme.
/// Paddings are Avalonia thicknesses (left, top, right, bottom).
/// </summary>
[ControlDesignToken]
internal sealed class DomainVerifyToken : AbstractControlDesignToken
{
    /// <summary>The widest the card grows (680).</summary>
    public double MaxWidth { get; set; }

    /// <summary>Below this width of the card the records stack and the column header leaves (544).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Padding of the header and the domain area (20, 16) and its compact form (16, 16).</summary>
    public Thickness SectionPadding { get; set; }

    public Thickness CompactSectionPadding { get; set; }

    /// <summary>Padding of a record row (20, 12) and its compact form (16, 12).</summary>
    public Thickness RowPadding { get; set; }

    public Thickness CompactRowPadding { get; set; }

    /// <summary>The tint of the column header and the footer: the muted surface mixed 60% into the surface.</summary>
    public Color StripBackground { get; set; }

    /// <summary>The footer's least height (56).</summary>
    public double FooterHeight { get; set; }

    /// <summary>The widest line of the description (520).</summary>
    public double DescriptionMaxWidth { get; set; }

    /// <summary>How far the placeholder rows are dimmed (0.5).</summary>
    public double PlaceholderOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        MaxWidth = 680;
        CompactWidth = 544;
        SectionPadding = new Thickness(20, 16);
        CompactSectionPadding = new Thickness(16, 16);
        RowPadding = new Thickness(20, 12);
        CompactRowPadding = new Thickness(16, 12);
        StripBackground = Oklch.MixOklab(n.Surface, n.SurfaceMuted, 0.6);
        FooterHeight = 56;
        DescriptionMaxWidth = 520;
        PlaceholderOpacity = 0.5;
    }
}
