using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and timing of <see cref="KnowledgeBase"/> and the accent-independent tints it derives from the neutrals.
/// Colors that follow the accent, radii and fonts come from ArcDesign tokens in the theme. The card is two columns (the
/// documents and the search preview) from <see cref="WideWidth"/>, one column below, and tightens its paddings below
/// <see cref="CompactWidth"/>; both read the card's own width.
/// </summary>
[ControlDesignToken]
internal sealed class KnowledgeBaseToken : AbstractControlDesignToken
{
    /// <summary>The card is at most this wide (960) and centered.</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>From this card width the search preview sits beside the list (760).</summary>
    public double WideWidth { get; set; }

    /// <summary>Below this card width the paddings tighten and the row hides its date (480).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Width of the search column in the two-column layout (340).</summary>
    public double SearchColumnWidth { get; set; }

    /// <summary>Header padding (20 20 20 16).</summary>
    public Thickness HeaderPadding { get; set; }

    /// <summary>Header padding in a narrow card (16 16 16 12).</summary>
    public Thickness CompactHeaderPadding { get; set; }

    /// <summary>Space around the drop zone (20 at the sides, 16 below).</summary>
    public Thickness DropZoneMargin { get; set; }

    /// <summary>Space around the drop zone in a narrow card (16 at the sides, 12 below).</summary>
    public Thickness CompactDropZoneMargin { get; set; }

    /// <summary>Padding inside the drop zone (16 12).</summary>
    public Thickness DropZonePadding { get; set; }

    /// <summary>Padding of a pane (the list or the search): 12 at the sides, 12 above and below.</summary>
    public Thickness PanePadding { get; set; }

    /// <summary>Padding of the search pane (20 16).</summary>
    public Thickness SearchPadding { get; set; }

    /// <summary>Padding of the search pane in a narrow card (16 12).</summary>
    public Thickness CompactSearchPadding { get; set; }

    /// <summary>The list scrolls inside the card above this height (420).</summary>
    public double ListMaxHeight { get; set; }

    /// <summary>Minimum height of a document row (60).</summary>
    public double RowMinHeight { get; set; }

    /// <summary>Padding of a document row (8 10).</summary>
    public Thickness RowPadding { get; set; }

    /// <summary>Side of the kind mark in a row (32).</summary>
    public double MarkSize { get; set; }

    /// <summary>Side of a row's icon buttons (32).</summary>
    public double IconButtonSize { get; set; }

    /// <summary>Diameter of the progress ring in a row (16).</summary>
    public double RingSize { get; set; }

    /// <summary>Width of a passage's score bar (40).</summary>
    public double ScoreBarWidth { get; set; }

    /// <summary>Length of a passage preview in characters (240).</summary>
    public int PreviewLength { get; set; }

    /// <summary>The tint behind a hovered or located row: the muted surface at 70%.</summary>
    public Color RowHoverBackground { get; set; }

    /// <summary>The tint behind the drop zone at rest: the muted surface at 55%.</summary>
    public Color DropZoneBackground { get; set; }

    /// <summary>The track of a passage's score bar and of the ring: the foreground at 10%.</summary>
    public Color TrackColor { get; set; }

    /// <summary>The wait after the last keystroke before the preview searches (180ms).</summary>
    public TimeSpan SearchDelay { get; set; }

    /// <summary>One step of the built-in simulated pipeline (260ms).</summary>
    public TimeSpan PipelineStep { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CardMaxWidth = 960;
        WideWidth = 760;
        CompactWidth = 480;
        SearchColumnWidth = 340;
        HeaderPadding = new Thickness(20, 20, 20, 16);
        CompactHeaderPadding = new Thickness(16, 16, 16, 12);
        DropZoneMargin = new Thickness(20, 0, 20, 16);
        CompactDropZoneMargin = new Thickness(16, 0, 16, 12);
        DropZonePadding = new Thickness(16, 12);
        PanePadding = new Thickness(12);
        SearchPadding = new Thickness(20, 16);
        CompactSearchPadding = new Thickness(16, 12);
        ListMaxHeight = 420;
        RowMinHeight = 60;
        RowPadding = new Thickness(8, 10);
        MarkSize = 32;
        IconButtonSize = 32;
        RingSize = 16;
        ScoreBarWidth = 40;
        PreviewLength = 240;
        RowHoverBackground = Oklch.WithAlpha(n.SurfaceMuted, 0.7);
        DropZoneBackground = Oklch.WithAlpha(n.SurfaceMuted, 0.55);
        TrackColor = Oklch.WithAlpha(n.Foreground, 0.1);
        SearchDelay = TimeSpan.FromMilliseconds(180);
        PipelineStep = TimeSpan.FromMilliseconds(260);
    }
}
