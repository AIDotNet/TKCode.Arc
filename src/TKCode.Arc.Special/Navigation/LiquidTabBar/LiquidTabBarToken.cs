using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="LiquidTabBar"/> and its tabs. The track, the drop, the text colors and the radii come from the
/// ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class LiquidTabBarToken : AbstractControlDesignToken
{
    /// <summary>Height of a tab.</summary>
    public double ItemHeight { get; set; }

    /// <summary>Space around a tab's glyph and label.</summary>
    public Thickness ItemPadding { get; set; }

    /// <summary>Gap between a tab's glyph and its label.</summary>
    public double ItemSpacing { get; set; }

    /// <summary>Size of a tab's glyph.</summary>
    public double GlyphSize { get; set; }

    /// <summary>Space between the track's edge and the tabs.</summary>
    public Thickness TrackPadding { get; set; }

    /// <summary>
    /// How much the drop thins while it is stretched between two tabs, as a share of its height (0 keeps it full height).
    /// </summary>
    public double Squash { get; set; }

    /// <summary>How deep the drop's waist pinches in while it is stretched, as a share of its height.</summary>
    public double Pinch { get; set; }

    /// <summary>Margin of a tab's keyboard focus ring (negative: outside the tab).</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ItemHeight = 36;
        ItemPadding = new Thickness(16, 0);
        ItemSpacing = 6;
        GlyphSize = 16;
        TrackPadding = new Thickness(4);
        Squash = 0.22;
        Pinch = 0.2;
        FocusRingMargin = new Thickness(-2);
    }
}
