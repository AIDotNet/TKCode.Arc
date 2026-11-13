using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="TocProgress"/>: the header, the rows (text size, padding, indent per level), the line (its lanes,
/// the bends between levels, the track and fill widths, the marker and the gap cut around it) and the compact button with
/// its ring and popup. The track color and the focus fill are derived from the neutral palette; the other colors, radii and
/// fonts come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class TocProgressToken : AbstractControlDesignToken
{
    /// <summary>Space between the header row and the list.</summary>
    public double HeaderGap { get; set; }

    /// <summary>Width of the line's track (the unread part).</summary>
    public double RailThickness { get; set; }

    /// <summary>Width of the line's read part.</summary>
    public double FillThickness { get; set; }

    /// <summary>Room from the list's left edge to the text of a top-level row; the line runs inside it.</summary>
    public double RailGutter { get; set; }

    /// <summary>Extra indent per level below the first; the line's lane moves by the same step.</summary>
    public double LevelIndent { get; set; }

    /// <summary>Distance from the list's left edge to the centre of the top-level lane.</summary>
    public double LaneStart { get; set; }

    /// <summary>Height of the S bend where the line moves between the lanes of two levels.</summary>
    public double BendHeight { get; set; }

    /// <summary>Diameter of the marker at the tip of the read part.</summary>
    public double DotSize { get; set; }

    /// <summary>Clear gap the marker cuts into the line around itself.</summary>
    public double DotGap { get; set; }

    /// <summary>Space around a row's text.</summary>
    public Thickness RowPadding { get; set; }

    /// <summary>Text size of a row.</summary>
    public double RowFontSize { get; set; }

    /// <summary>Line height of a row's text (long titles wrap).</summary>
    public double RowLineHeight { get; set; }

    /// <summary>Letter spacing of a row's text, in pixels.</summary>
    public double RowLetterSpacing { get; set; }

    /// <summary>The ring sits this far outside a row while it has keyboard focus.</summary>
    public Thickness FocusRingMargin { get; set; }

    /// <summary>Color of the line's track: a neutral between the border and the muted text.</summary>
    public Color TrackColor { get; set; }

    /// <summary>The quiet fill behind a row with keyboard focus (the text color at 4.5%).</summary>
    public Color FocusFill { get; set; }

    /// <summary>Space between the check and the word in the finished state of the time left.</summary>
    public double TimeGap { get; set; }

    /// <summary>Size of the check in the finished state of the time left.</summary>
    public double CheckSize { get; set; }

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
        var neutrals = ArcPalette.Neutrals(isDarkMode);
        HeaderGap = 10;
        RailThickness = 1.5;
        FillThickness = 2;
        RailGutter = 22;
        LevelIndent = 12;
        LaneStart = 5;
        BendHeight = 14;
        DotSize = 7;
        DotGap = 2;
        RowPadding = new Thickness(0, 4, 8, 4);
        RowFontSize = 13;
        RowLineHeight = 18;
        RowLetterSpacing = -0.13;
        FocusRingMargin = new Thickness(-4);
        TrackColor = isDarkMode ? neutrals.BorderStrong : neutrals.ControlTrackHover;
        FocusFill = Color.FromArgb(11, neutrals.Foreground.R, neutrals.Foreground.G, neutrals.Foreground.B);
        TimeGap = 4;
        CheckSize = 14;
        CompactHeight = 40;
        CompactPadding = new Thickness(10, 0, 14, 0);
        CompactRingSize = 20;
        CompactRingThickness = 2.5;
        PopupWidth = 300;
        PopupPadding = new Thickness(16, 14);
    }
}
