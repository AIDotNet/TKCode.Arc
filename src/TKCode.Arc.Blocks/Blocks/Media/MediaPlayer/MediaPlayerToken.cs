using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="MediaPlayer"/>, measured from Arc's Media player (design reference only, ADR 0028). Colors, radii
/// and type come from ArcDesign tokens in the theme. The compact values apply while the player is narrower than
/// <see cref="CompactWidth"/>.
/// </summary>
[ControlDesignToken]
internal sealed class MediaPlayerToken : AbstractControlDesignToken
{
    /// <summary>Below this width of the player the compact layout applies (420).</summary>
    public double CompactWidth { get; set; }

    /// <summary>The card's padding (20) and its compact form (14).</summary>
    public Thickness FramePadding { get; set; }

    public Thickness CompactFramePadding { get; set; }

    /// <summary>The widest the player grows (520).</summary>
    public double PlayerMaxWidth { get; set; }

    /// <summary>The cover's width to height (2.2) and its compact form (1.7).</summary>
    public double CoverAspect { get; set; }

    public double CompactCoverAspect { get; set; }

    /// <summary>Around the title and the transport (18 above, 4 at the sides, 12 below); 16 between them (10 compact).</summary>
    public Thickness NowPlayingPadding { get; set; }

    public double NowPlayingGap { get; set; }

    public double CompactNowPlayingGap { get; set; }

    /// <summary>The heading: 28 on a 30.8 line, −0.84 apart; compact 22 on 24.2, −0.66 apart (1.1 lines, −0.03 em).</summary>
    public double TitleFontSize { get; set; }

    public double CompactTitleFontSize { get; set; }

    public double TitleLineHeight { get; set; }

    public double CompactTitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    public double CompactTitleLetterSpacing { get; set; }

    /// <summary>How far the outgoing title lifts and the incoming one rises (10).</summary>
    public double TitleTravel { get; set; }

    /// <summary>The previous and next buttons (34, compact 32), the play button (44, compact 42) and the mute button (34).</summary>
    public double SkipButtonSize { get; set; }

    public double CompactSkipButtonSize { get; set; }

    public double PlayButtonSize { get; set; }

    public double CompactPlayButtonSize { get; set; }

    public double MuteButtonSize { get; set; }

    /// <summary>Between the transport buttons (8, compact 2).</summary>
    public double TransportGap { get; set; }

    public double CompactTransportGap { get; set; }

    /// <summary>Glyph sizes: skip 18, play 20, volume 16.</summary>
    public double SkipGlyphSize { get; set; }

    public double PlayGlyphSize { get; set; }

    public double VolumeGlyphSize { get; set; }

    /// <summary>The time row's padding (0, 0, 18 below, 4 at the sides) and the gap between its parts (12).</summary>
    public Thickness TimelinePadding { get; set; }

    public double TimelineGap { get; set; }

    /// <summary>The playlist heading row's padding (12 above, 6 below, 4 at the sides) and the volume group's gap (4).</summary>
    public Thickness QueueHeadPadding { get; set; }

    public double VolumeGap { get; set; }

    /// <summary>The volume slider's width (88, compact 64).</summary>
    public double VolumeWidth { get; set; }

    public double CompactVolumeWidth { get; set; }

    /// <summary>A slider's height (18), track height (4), thumb diameter (12), the gap ring around the thumb (3) and the thumb's scale while dragged (1.2).</summary>
    public double RangeHeight { get; set; }

    public double RangeTrackHeight { get; set; }

    public double RangeThumbSize { get; set; }

    public double RangeThumbGap { get; set; }

    public double RangeDragScale { get; set; }

    /// <summary>A playlist row: 44 high, padded 14 left and 10 right, 12 between title and status, radius 12.</summary>
    public double RowHeight { get; set; }

    public Thickness RowPadding { get; set; }

    public double RowGap { get; set; }

    /// <summary>The selection rail: 2 wide, 20 high, 2 from the row's left edge.</summary>
    public double RailWidth { get; set; }

    public double RailHeight { get; set; }

    public double RailInset { get; set; }

    /// <summary>The playing bars: three bars 2 wide and 12 high, 2 apart.</summary>
    public double BarWidth { get; set; }

    public double BarHeight { get; set; }

    public double BarGap { get; set; }

    /// <summary>One sweep of a playing bar between 30% and full height (900 ms; it then sweeps back).</summary>
    public TimeSpan BarPeriod { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CompactWidth = 420;
        FramePadding = new Thickness(20);
        CompactFramePadding = new Thickness(14);
        PlayerMaxWidth = 520;
        CoverAspect = 2.2;
        CompactCoverAspect = 1.7;
        NowPlayingPadding = new Thickness(4, 18, 4, 12);
        NowPlayingGap = 16;
        CompactNowPlayingGap = 10;
        TitleFontSize = 28;
        CompactTitleFontSize = 22;
        TitleLineHeight = 30.8;
        CompactTitleLineHeight = 24.2;
        TitleLetterSpacing = -0.84;
        CompactTitleLetterSpacing = -0.66;
        TitleTravel = 10;
        SkipButtonSize = 34;
        CompactSkipButtonSize = 32;
        PlayButtonSize = 44;
        CompactPlayButtonSize = 42;
        MuteButtonSize = 34;
        TransportGap = 8;
        CompactTransportGap = 2;
        SkipGlyphSize = 18;
        PlayGlyphSize = 20;
        VolumeGlyphSize = 16;
        TimelinePadding = new Thickness(4, 0, 4, 18);
        TimelineGap = 12;
        QueueHeadPadding = new Thickness(4, 12, 4, 6);
        VolumeGap = 4;
        VolumeWidth = 88;
        CompactVolumeWidth = 64;
        RangeHeight = 18;
        RangeTrackHeight = 4;
        RangeThumbSize = 12;
        RangeThumbGap = 3;
        RangeDragScale = 1.2;
        RowHeight = 44;
        RowPadding = new Thickness(14, 0, 10, 0);
        RowGap = 12;
        RailWidth = 2;
        RailHeight = 20;
        RailInset = 2;
        BarWidth = 2;
        BarHeight = 12;
        BarGap = 2;
        BarPeriod = TimeSpan.FromMilliseconds(900);
    }
}
