using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of a group card (self-designed): a 36px tab strip (<c>ControlSizeSM</c>) with 4px insets, and 24px action
/// buttons (overflow, more) with 14px glyphs. Colors, border and radius come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class DockGroupViewToken : AbstractControlDesignToken
{
    /// <summary>Height of the tab strip: 36.</summary>
    public double StripHeight { get; set; }

    /// <summary>Insets of the tab strip inside the card: 4 on the left and right.</summary>
    public Thickness StripPadding { get; set; }

    /// <summary>A square action button at the end of the strip: 24.</summary>
    public double ActionSize { get; set; }

    /// <summary>The action buttons' glyphs: 14.</summary>
    public double ActionGlyphSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        StripHeight = 36;
        StripPadding = new Thickness(4, 0);
        ActionSize = 24;
        ActionGlyphSize = 14;
    }
}
