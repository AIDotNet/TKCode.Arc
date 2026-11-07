using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="VideoPlayer"/> (design/components/video/video.md section 7), plus the neutral colors of the controls
/// drawn over the picture. Those stay the same in light and dark themes because the picture under them is always video on
/// black; they do not follow the accent.
/// </summary>
[ControlDesignToken]
public sealed class VideoPlayerToken : AbstractControlDesignToken
{
    /// <summary>The control bar's button size: 32 (as MediaPlayer).</summary>
    public double ButtonSize { get; set; }

    /// <summary>The control bar's padding: 12 at the sides, 8 above and below.</summary>
    public Thickness ControlBarPadding { get; set; }

    /// <summary>The control bar's padding below <see cref="CompactWidth"/>: 8 at the sides, 6 above and below.</summary>
    public Thickness CompactControlBarPadding { get; set; }

    /// <summary>The gap between the control bar's buttons: 4 (2 when compact).</summary>
    public double ControlBarGap { get; set; }

    /// <summary>The gap between the control bar's buttons when compact: 2.</summary>
    public double CompactControlBarGap { get; set; }

    /// <summary>The control bar's glyph size: 18.</summary>
    public double GlyphSize { get; set; }

    /// <summary>The seek bar's track height at rest: 4.</summary>
    public double SeekTrackHeight { get; set; }

    /// <summary>The seek bar's track height while hovered, dragged or focused: 6.</summary>
    public double SeekTrackHeightActive { get; set; }

    /// <summary>The seek bar's thumb diameter: 12.</summary>
    public double SeekThumbSize { get; set; }

    /// <summary>The seek bar's hit height (the strip above the buttons that takes the pointer): 16.</summary>
    public double SeekBarHeight { get; set; }

    /// <summary>The gap cut between chapter segments: 2.</summary>
    public double ChapterGap { get; set; }

    /// <summary>The width the volume slider expands to: 64.</summary>
    public double VolumeSliderWidth { get; set; }

    /// <summary>The width of the open volume slider's frame (the slider plus 8 at each side): 80.</summary>
    public double VolumeOpenWidth { get; set; }

    /// <summary>The scrim's margin inside the control bar: it covers the bar's padding and reaches <see cref="ScrimHeightExtra"/> above it.</summary>
    public Thickness ScrimMargin { get; set; }

    /// <summary>The scrim's margin in the compact layout.</summary>
    public Thickness CompactScrimMargin { get; set; }

    /// <summary>The big play (and replay) button's diameter: 64.</summary>
    public double BigPlaySize { get; set; }

    /// <summary>The big play button's glyph size: 28.</summary>
    public double BigPlayGlyphSize { get; set; }

    /// <summary>How far the bottom scrim reaches above the control bar: 32.</summary>
    public double ScrimHeightExtra { get; set; }

    /// <summary>Caption text size as a share of the picture height (WebVTT's 5vh, adjusted): 0.045.</summary>
    public double CaptionHeightRatio { get; set; }

    /// <summary>Below this width the player uses its compact layout (pseudo-class <c>:compact</c>): 420.</summary>
    public double CompactWidth { get; set; }

    /// <summary>The control glyphs and the time text over the picture: white at 0.92.</summary>
    public Color OnVideoForeground { get; set; }

    /// <summary>A control bar button's hover plate: white at 0.12.</summary>
    public Color OnVideoHover { get; set; }

    /// <summary>The seek bar's empty track: white at 0.24.</summary>
    public Color SeekTrackColor { get; set; }

    /// <summary>The seek bar's buffered ranges: white at 0.4.</summary>
    public Color SeekBufferedColor { get; set; }

    /// <summary>The bottom of the scrim behind the controls: black at 0.6.</summary>
    public Color ScrimColor { get; set; }

    /// <summary>The plate behind the centre play / pause feedback and the double-tap skip text: black at 0.45.</summary>
    public Color FeedbackPlateColor { get; set; }

    /// <inheritdoc />
    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ButtonSize = 32;
        ControlBarPadding = new Thickness(12, 8);
        CompactControlBarPadding = new Thickness(8, 6);
        ControlBarGap = 4;
        CompactControlBarGap = 2;
        GlyphSize = 18;
        SeekTrackHeight = 4;
        SeekTrackHeightActive = 6;
        SeekThumbSize = 12;
        SeekBarHeight = 16;
        ChapterGap = 2;
        VolumeSliderWidth = 64;
        VolumeOpenWidth = VolumeSliderWidth + 16;
        BigPlaySize = 64;
        BigPlayGlyphSize = 28;
        ScrimHeightExtra = 32;
        CaptionHeightRatio = 0.045;
        ScrimMargin = new Thickness(-ControlBarPadding.Left, -(ControlBarPadding.Top + ScrimHeightExtra), -ControlBarPadding.Right, -ControlBarPadding.Bottom);
        CompactScrimMargin = new Thickness(-CompactControlBarPadding.Left, -(CompactControlBarPadding.Top + ScrimHeightExtra),
            -CompactControlBarPadding.Right, -CompactControlBarPadding.Bottom);
        CompactWidth = 420;
        OnVideoForeground = Color.FromArgb(235, 255, 255, 255);
        OnVideoHover = Color.FromArgb(31, 255, 255, 255);
        SeekTrackColor = Color.FromArgb(61, 255, 255, 255);
        SeekBufferedColor = Color.FromArgb(102, 255, 255, 255);
        ScrimColor = Color.FromArgb(153, 0, 0, 0);
        FeedbackPlateColor = Color.FromArgb(115, 0, 0, 0);
    }
}
