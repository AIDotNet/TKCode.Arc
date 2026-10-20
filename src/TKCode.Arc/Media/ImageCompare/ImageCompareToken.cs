using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ImageCompareToken : AbstractControlDesignToken
{
    /// <summary>Caption chip inset from the frame corner (<c>--space-3</c>).</summary>
    public Thickness ChipMargin { get; set; }

    /// <summary>Caption chip padding: 3px 10px.</summary>
    public Thickness ChipPadding { get; set; }

    /// <summary>12px × <c>--leading-body</c> 1.4.</summary>
    public double ChipLineHeight { get; set; }

    /// <summary>Frosted chip fill: <c>color-mix(in oklab, var(--background) 74%, transparent)</c>.</summary>
    public Color ChipBackground { get; set; }

    /// <summary>Inner hairline: <c>inset 0 0 0 1px color-mix(in oklab, var(--foreground) 8%, transparent)</c>.</summary>
    public Color HairlineColor { get; set; }

    /// <summary>The line sits on photography, not on the theme: raised surface in light mode, foreground in dark.</summary>
    public Color DividerColor { get; set; }

    /// <summary>Half-pixel edge around the line: <c>0 0 0 .5px oklch(0% 0 0 / .14)</c>.</summary>
    public Color DividerEdgeColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        ChipMargin = new Thickness(12);
        ChipPadding = new Thickness(10, 3);
        ChipLineHeight = 12 * 1.4;
        ChipBackground = Oklch.WithAlpha(n.Background, 0.74);
        HairlineColor = Oklch.WithAlpha(n.Foreground, 0.08);
        DividerColor = isDarkMode ? n.Foreground : n.SurfaceRaised;
        DividerEdgeColor = Color.FromArgb(36, 0, 0, 0);
    }
}
