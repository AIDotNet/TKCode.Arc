using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="BookingPill"/>. The pill, the raised chip, the pickers and their text take their colors, radii,
/// shadows and fonts from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class BookingPillToken : AbstractControlDesignToken
{
    /// <summary>Height of the pill.</summary>
    public double PillHeight { get; set; }

    /// <summary>Space inside the pill around the segments and the search button.</summary>
    public Thickness PillPadding { get; set; }

    /// <summary>Space inside a segment around its label and value.</summary>
    public Thickness SegmentPadding { get; set; }

    /// <summary>How much wider the open segment gets, as a share of an even split (0.3 = 30 percent).</summary>
    public double OpenGrow { get; set; }

    /// <summary>Height of the thin lines between segments.</summary>
    public double DividerHeight { get; set; }

    /// <summary>Diameter of the round search button.</summary>
    public double SearchSize { get; set; }

    /// <summary>Space between the pill and its picker.</summary>
    public double PopupGap { get; set; }

    /// <summary>Space inside a picker.</summary>
    public Thickness PanelPadding { get; set; }

    /// <summary>Width of a picker.</summary>
    public double PanelWidth { get; set; }

    /// <summary>Size of one day in the date picker.</summary>
    public double DayCellSize { get; set; }

    /// <summary>Size of the icons in the pill and the pickers.</summary>
    public double GlyphSize { get; set; }

    /// <summary>Margin of the keyboard focus ring around a segment or the search button (negative: outside).</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PillHeight = 64;
        PillPadding = new Thickness(6);
        SegmentPadding = new Thickness(22, 8, 16, 8);
        OpenGrow = 0.3;
        DividerHeight = 28;
        SearchSize = 48;
        PopupGap = 10;
        PanelPadding = new Thickness(16);
        PanelWidth = 340;
        DayCellSize = 40;
        GlyphSize = 18;
        FocusRingMargin = new Thickness(-3);
    }
}
