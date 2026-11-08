using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class PageViewerToken : AbstractControlDesignToken
{
    /// <summary>Height of the toolbar.</summary>
    public double ToolbarHeight { get; set; }

    /// <summary>Inner padding of the toolbar.</summary>
    public Thickness ToolbarPadding { get; set; }

    /// <summary>Gap between pages in a continuous layout; it does not scale with the zoom.</summary>
    public double PageSpacing { get; set; }

    /// <summary>Canvas around the pages.</summary>
    public double CanvasPadding { get; set; }

    /// <summary>Canvas around the pages when the viewer is narrower than 640.</summary>
    public double CanvasPaddingNarrow { get; set; }

    /// <summary>Width of the page number field.</summary>
    public double PageNumberBoxWidth { get; set; }

    /// <summary>Width of the zoom menu button.</summary>
    public double ZoomMenuWidth { get; set; }

    /// <summary>The placeholder ring on a page without a bitmap. Pages are always white paper, so it is the light neutral in both appearances.</summary>
    public Color PlaceholderInk { get; set; }

    /// <summary>Inset of the keyboard focus ring: the viewport clips, so the ring sits inside it.</summary>
    public Thickness FocusRingMargin { get; set; }

    /// <summary>Corner radius of the keyboard focus ring.</summary>
    public CornerRadius FocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ToolbarHeight = 48;
        ToolbarPadding = new Thickness(8, 0);
        PageSpacing = 12;
        CanvasPadding = 24;
        CanvasPaddingNarrow = 8;
        PageNumberBoxWidth = 56;
        ZoomMenuWidth = 72;
        PlaceholderInk = Oklch.WithAlpha(ArcPalette.Neutrals(false).Foreground, 0.22);
        FocusRingMargin = new Thickness(2);
        FocusRingRadius = new CornerRadius(0);
    }
}
