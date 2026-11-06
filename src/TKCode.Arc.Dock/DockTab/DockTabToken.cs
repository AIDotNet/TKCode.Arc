using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of a tab in a group's strip (self-designed, after <c>TabItem</c>): 28px high, 10px inline padding, 13px text,
/// a 14px icon, a 16px close button and a 6px modified dot; tabs sit 2px apart. Colors and radius come from the ArcDesign
/// tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class DockTabToken : AbstractControlDesignToken
{
    /// <summary>Height of a tab: 28.</summary>
    public double Height { get; set; }

    /// <summary>Padding at the start and end of a tab: 10.</summary>
    public Thickness InlinePadding { get; set; }

    /// <summary>Title size: 13 (TypeXS + 1).</summary>
    public double FontSize { get; set; }

    /// <summary>Icon size: 14.</summary>
    public double IconSize { get; set; }

    /// <summary>Gap between the icon, the title and the close button: 6.</summary>
    public double ItemGap { get; set; }

    /// <summary>The close button of a document tab: 16.</summary>
    public double CloseSize { get; set; }

    /// <summary>The close button's glyph: 12.</summary>
    public double CloseGlyphSize { get; set; }

    /// <summary>The modified dot of a document tab: 6.</summary>
    public double ModifiedDotSize { get; set; }

    /// <summary>Gap between neighbouring tabs: 2.</summary>
    public double TabGap { get; set; }

    /// <summary>The keyboard focus ring sits on the tab's edge (the strip clips anything outside): 0.</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        Height = 28;
        InlinePadding = new Thickness(10, 0);
        FontSize = 13;
        IconSize = 14;
        ItemGap = 6;
        CloseSize = 16;
        CloseGlyphSize = 12;
        ModifiedDotSize = 6;
        TabGap = 2;
        FocusRingMargin = new Thickness(0);
    }
}
