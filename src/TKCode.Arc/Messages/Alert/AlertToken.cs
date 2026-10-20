using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class AlertToken : AbstractControlDesignToken
{
    /// <summary>Inner padding of the surface (space-4).</summary>
    public Thickness SurfacePadding { get; set; }

    /// <summary>Status glyph size (18px, Lucide stroke 2).</summary>
    public double GlyphSize { get; set; }

    /// <summary>Nudges the 18px glyph onto the title's first line, with the column gap after it.</summary>
    public Thickness GlyphMargin { get; set; }

    /// <summary>Space between title and description (space-1).</summary>
    public double DescriptionGap { get; set; }

    /// <summary>Title line height (text-sm × leading-body).</summary>
    public double TitleLineHeight { get; set; }

    /// <summary>Description line height (text-xs × leading-body).</summary>
    public double DescriptionLineHeight { get; set; }

    /// <summary>Round dismiss button (28px).</summary>
    public double DismissDiameter { get; set; }

    /// <summary>Column gap, then pulls the dismiss button into the corner padding (-5px -6px -5px 0).</summary>
    public Thickness DismissMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        SurfacePadding = new Thickness(16);
        GlyphSize = 18;
        GlyphMargin = new Thickness(0, 1, 12, 0);
        DescriptionGap = 4;
        TitleLineHeight = 14 * 1.4;
        DescriptionLineHeight = 12 * 1.4;
        DismissDiameter = 28;
        DismissMargin = new Thickness(12, -5, -6, -5);
    }
}
