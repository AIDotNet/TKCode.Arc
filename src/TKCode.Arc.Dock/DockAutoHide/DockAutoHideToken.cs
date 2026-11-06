using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of the auto-hide strips and the flyout (self-designed): strip buttons 26px thick on the outer side of the 32px
/// strip (the rest is the gap to the cards), 13px titles turned 90° on the left and right strips, a 2px accent bar on the
/// inner side of the open pane's button, and a flyout card with a 36px header and a 6px resize grip on its inner edge.
/// Colors, radius and shadow come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class DockAutoHideToken : AbstractControlDesignToken
{
    /// <summary>Thickness of a strip button across the strip: 26.</summary>
    public double ButtonThickness { get; set; }

    /// <summary>Padding at the start and end of a strip button, along the strip: 10.</summary>
    public Thickness ButtonPadding { get; set; }

    /// <summary>Gap between strip buttons: 4.</summary>
    public double ButtonGap { get; set; }

    /// <summary>Title size of a strip button: 13.</summary>
    public double FontSize { get; set; }

    /// <summary>Icon size of a strip button and of the flyout header: 14.</summary>
    public double IconSize { get; set; }

    /// <summary>The accent bar on the inner side of the open pane's button: 2.</summary>
    public double IndicatorThickness { get; set; }

    /// <summary>Height of the flyout card's header: 36.</summary>
    public double HeaderHeight { get; set; }

    /// <summary>Insets of the flyout header: 12 before the icon, 4 after the pin button.</summary>
    public Thickness HeaderPadding { get; set; }

    /// <summary>Width of the resize grip on the flyout's inner edge: 6.</summary>
    public double GripThickness { get; set; }

    /// <summary>The keyboard focus ring sits on the strip button's edge: 0.</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ButtonThickness = 26;
        ButtonPadding = new Thickness(10, 0);
        ButtonGap = 4;
        FontSize = 13;
        IconSize = 14;
        IndicatorThickness = 2;
        HeaderHeight = 36;
        HeaderPadding = new Thickness(12, 0, 4, 0);
        GripThickness = 6;
        FocusRingMargin = new Thickness(0);
    }
}
