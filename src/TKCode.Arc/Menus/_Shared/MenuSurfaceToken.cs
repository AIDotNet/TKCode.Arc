using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class MenuSurfaceToken : AbstractControlDesignToken
{
    /// <summary>Panel inset around the rows (Arc: 5px).</summary>
    public Thickness PanelInset { get; set; }

    /// <summary>Dropdown family minimum width (12rem).</summary>
    public double DropdownMinWidth { get; set; }

    /// <summary>Dropdown family maximum height (24rem, about ten rows); longer menus scroll.</summary>
    public double DropdownMaxHeight { get; set; }

    /// <summary>Context menu minimum width (13rem) and maximum height (300px, scrolls beyond).</summary>
    public double ContextMinWidth { get; set; }
    public double ContextMaxHeight { get; set; }

    /// <summary>Row highlight radius: RadiusPanel (26) minus the 6px inset.</summary>
    public CornerRadius HighlightRadius { get; set; }

    /// <summary>Destructive highlight: 8% danger mixed into the surface.</summary>
    public Color DangerHighlight { get; set; }

    /// <summary>Account panel: 15.5rem wide, 4px inset, 10px radius, half-pixel border, 6px highlight radius.</summary>
    public double AccountWidth { get; set; }
    public Thickness AccountInset { get; set; }
    public CornerRadius AccountRadius { get; set; }
    public Thickness AccountBorderThickness { get; set; }
    public CornerRadius AccountHighlightRadius { get; set; }

    /// <summary>Account highlight: foreground at 6%.</summary>
    public Color AccountHighlight { get; set; }

    public double AccountFontSize { get; set; }

    /// <summary>Sheet: 8px top, 10px sides, 12px bottom; top corners RadiusPanel; no bottom border.</summary>
    public Thickness SheetInset { get; set; }
    public CornerRadius SheetRadius { get; set; }
    public Thickness SheetBorderThickness { get; set; }

    /// <summary>Sheet drag handle: 36x5 pill, 10px above the content.</summary>
    public double HandleWidth { get; set; }
    public double HandleHeight { get; set; }
    public Thickness HandleMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        PanelInset = new Thickness(5);
        DropdownMinWidth = 192;
        DropdownMaxHeight = 384;
        ContextMinWidth = 208;
        ContextMaxHeight = 300;
        HighlightRadius = new CornerRadius(20);
        DangerHighlight = Oklch.MixOklab(n.Surface, n.Danger, 0.08);
        AccountWidth = 248;
        AccountInset = new Thickness(4);
        AccountRadius = new CornerRadius(10);
        AccountBorderThickness = new Thickness(0.5);
        AccountHighlightRadius = new CornerRadius(6);
        AccountHighlight = Oklch.WithAlpha(n.Foreground, 0.06);
        AccountFontSize = 13;
        SheetInset = new Thickness(10, 8, 10, 12);
        SheetRadius = new CornerRadius(26, 26, 0, 0);
        SheetBorderThickness = new Thickness(0.5, 0.5, 0.5, 0);
        HandleWidth = 36;
        HandleHeight = 5;
        HandleMargin = new Thickness(0, 0, 0, 10);
    }
}
