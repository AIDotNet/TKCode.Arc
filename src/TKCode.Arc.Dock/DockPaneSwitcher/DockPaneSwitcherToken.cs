using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of the pane switcher (Ctrl+Tab, self-designed): a 320px card with 6px insets, a 28px caption and 32px rows of a
/// 14px icon and 13px title, at most 360px of rows before they scroll. Colors, radius and shadow come from the ArcDesign
/// tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class DockPaneSwitcherToken : AbstractControlDesignToken
{
    /// <summary>Width of the switcher card: 320.</summary>
    public double Width { get; set; }

    /// <summary>Insets of the card: 6.</summary>
    public Thickness Padding { get; set; }

    /// <summary>Height of the caption row: 28.</summary>
    public double CaptionHeight { get; set; }

    /// <summary>Height of a row: 32.</summary>
    public double RowHeight { get; set; }

    /// <summary>Padding at the start and end of a row: 10.</summary>
    public Thickness RowPadding { get; set; }

    /// <summary>Row title size: 13.</summary>
    public double FontSize { get; set; }

    /// <summary>Row icon size: 14.</summary>
    public double IconSize { get; set; }

    /// <summary>Tallest the list of rows gets before it scrolls: 360.</summary>
    public double MaxListHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        Width = 320;
        Padding = new Thickness(6);
        CaptionHeight = 28;
        RowHeight = 32;
        RowPadding = new Thickness(10, 0);
        FontSize = 13;
        IconSize = 14;
        MaxListHeight = 360;
    }
}
