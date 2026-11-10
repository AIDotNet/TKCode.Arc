using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="TocProgress"/>: the space under the heading, the rail (its width, the gutter it sits in and the
/// dot at the fill's end), the row padding, indent and line height, and the compact button with its ring and popup.
/// Colors, radii and fonts come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class TocProgressToken : AbstractControlDesignToken
{
    /// <summary>Space between the heading row and the list.</summary>
    public double HeaderGap { get; set; }

    /// <summary>Width of the rail's track and fill.</summary>
    public double RailThickness { get; set; }

    /// <summary>Room from the list's left edge to the text of a top-level row; the rail runs inside it.</summary>
    public double RailGutter { get; set; }

    /// <summary>Extra indent per level below the first.</summary>
    public double LevelIndent { get; set; }

    /// <summary>Diameter of the dot at the end of the fill.</summary>
    public double DotSize { get; set; }

    /// <summary>Space around a row's text.</summary>
    public Thickness RowPadding { get; set; }

    /// <summary>Line height of a row's text (titles wrap to two lines at most).</summary>
    public double RowLineHeight { get; set; }

    /// <summary>The ring sits this far outside a row while it has keyboard focus.</summary>
    public Thickness FocusRingMargin { get; set; }

    /// <summary>Height of the compact button.</summary>
    public double CompactHeight { get; set; }

    /// <summary>Inner padding of the compact button.</summary>
    public Thickness CompactPadding { get; set; }

    /// <summary>Diameter of the progress ring in the compact button.</summary>
    public double CompactRingSize { get; set; }

    /// <summary>Stroke of the progress ring in the compact button.</summary>
    public double CompactRingThickness { get; set; }

    /// <summary>Width of the popup that holds the list in compact mode.</summary>
    public double PopupWidth { get; set; }

    /// <summary>Space between the popup's edge and the list.</summary>
    public Thickness PopupPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        HeaderGap = 12;
        RailThickness = 2;
        RailGutter = 20;
        LevelIndent = 14;
        DotSize = 8;
        RowPadding = new Thickness(0, 5, 4, 5);
        RowLineHeight = 20;
        FocusRingMargin = new Thickness(12, 0, -4, 0);
        CompactHeight = 40;
        CompactPadding = new Thickness(10, 0, 14, 0);
        CompactRingSize = 20;
        CompactRingThickness = 2.5;
        PopupWidth = 300;
        PopupPadding = new Thickness(16, 14);
    }
}
