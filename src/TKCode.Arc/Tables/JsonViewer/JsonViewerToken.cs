using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>JsonViewer geometry and the accent-independent row fills.</summary>
[ControlDesignToken]
internal sealed class JsonViewerToken : AbstractControlDesignToken
{
    public double RowSlotHeight { get; set; }
    public double IndentWidth { get; set; }
    public double IndentWidthCompact { get; set; }
    public double TreeFontSize { get; set; }
    public double TreeMaxHeight { get; set; }
    public Thickness TreePadding { get; set; }
    public Thickness ToolbarPadding { get; set; }
    public double ToolSize { get; set; }
    public double InsetToolSize { get; set; }
    public CornerRadius ToolRadius { get; set; }
    public CornerRadius InsetToolRadius { get; set; }
    public CornerRadius SearchRadius { get; set; }
    public Thickness SearchPadding { get; set; }
    public Thickness PathBarPadding { get; set; }
    public double PathCopySize { get; set; }
    public CornerRadius PathCopyRadius { get; set; }
    public double CompactWidth { get; set; }

    /// <summary>Hovered row: foreground 3.5% over the surface.</summary>
    public Color RowHoverFill { get; set; }

    /// <summary>Search marks: warning 22% (48% on the current match).</summary>
    public Color MarkFill { get; set; }
    public Color MarkCurrentFill { get; set; }

    /// <summary>Pressed row action: foreground 8%.</summary>
    public Color ActionPressedFill { get; set; }

    public CornerRadius ToolFocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ToolFocusRingRadius = new CornerRadius(16);
        RowSlotHeight = 30;
        IndentWidth = 16;
        IndentWidthCompact = 12;
        TreeFontSize = 13;
        TreeMaxHeight = 420;
        TreePadding = new Thickness(6);
        ToolbarPadding = new Thickness(6);
        ToolSize = 34;
        InsetToolSize = 26;
        ToolRadius = new CornerRadius(14);
        InsetToolRadius = new CornerRadius(9);
        SearchRadius = new CornerRadius(14);
        SearchPadding = new Thickness(10, 0, 4, 0);
        PathBarPadding = new Thickness(14, 6, 6, 6);
        PathCopySize = 30;
        PathCopyRadius = new CornerRadius(10);
        CompactWidth = 420;

        var n = ArcPalette.Neutrals(isDarkMode);
        RowHoverFill = Oklch.MixOklab(n.Surface, n.Foreground, 0.035);
        MarkFill = Oklch.WithAlpha(n.Warning, 0.22);
        MarkCurrentFill = Oklch.WithAlpha(n.Warning, 0.48);
        ActionPressedFill = Oklch.WithAlpha(n.Foreground, 0.08);
    }
}
