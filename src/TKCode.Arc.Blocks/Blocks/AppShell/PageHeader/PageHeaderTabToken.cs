using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

// Geometry from Arc's free page-header block (page-header.module.css; MIT, Copyright (c) 2026 Elia Kuratli).
[ControlDesignToken]
internal sealed class PageHeaderTabToken : AbstractControlDesignToken
{
    /// <summary>Arc: .tab height 44px.</summary>
    public double TabHeight { get; set; }

    /// <summary>Arc: .tab padding 0 10px.</summary>
    public Thickness TabPadding { get; set; }

    /// <summary>Gap between the label and the count (Arc: 6px).</summary>
    public double CountGap { get; set; }

    /// <summary>Hover block: inset 8px 0, radius 8px.</summary>
    public Thickness HoverInset { get; set; }

    public CornerRadius HoverRadius { get; set; }

    /// <summary>Hover block fill: color-mix(foreground 4.5%, transparent).</summary>
    public Color HoverFill { get; set; }

    /// <summary>Focus ring 2px outside the hover block.</summary>
    public Thickness FocusRingMargin { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var neutrals = ArcPalette.Neutrals(isDarkMode);
        TabHeight = 44;
        TabPadding = new Thickness(10, 0);
        CountGap = 6;
        HoverInset = new Thickness(0, 8);
        HoverRadius = new CornerRadius(8);
        HoverFill = Oklch.WithAlpha(neutrals.Foreground, 0.045);
        FocusRingMargin = new Thickness(-2, 6);
        FocusRingRadius = new CornerRadius(10);
        DisabledOpacity = 0.5;
    }
}
