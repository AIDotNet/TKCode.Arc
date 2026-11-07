using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and accent-independent colors of <see cref="VoiceRecorder"/>, measured from Arc Voice recorder (design
/// reference): the card's padding and gaps, the 44px bubbles with 32px play buttons, the 48px composer row, and the live
/// bar's tint and record dot.
/// </summary>
[ControlDesignToken]
internal sealed class VoiceRecorderToken : AbstractControlDesignToken
{
    /// <summary>The card's padding (14, 14, 14, 12).</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>The list's inner padding.</summary>
    public Thickness ListPadding { get; set; }

    /// <summary>The gap between messages.</summary>
    public double MessageGap { get; set; }

    /// <summary>A bubble's height.</summary>
    public double BubbleHeight { get; set; }

    /// <summary>A bubble's padding (6 left, 14 right).</summary>
    public Thickness BubblePadding { get; set; }

    /// <summary>The gap between a bubble's play button, waveform and time.</summary>
    public double BubbleGap { get; set; }

    /// <summary>The round play buttons of the bubbles and the review.</summary>
    public double PlaySize { get; set; }

    /// <summary>The opacity of the time inside a bubble.</summary>
    public double BubbleTimeOpacity { get; set; }

    /// <summary>The composer row's height (the mic, the bar and the send button).</summary>
    public double ComposerHeight { get; set; }

    /// <summary>The gap between the composer's parts.</summary>
    public double ComposerGap { get; set; }

    /// <summary>The trash button.</summary>
    public double DiscardSize { get; set; }

    /// <summary>The live bar's tint: the muted surface with 5.5% of the foreground.</summary>
    public Color RecordingBackground { get; set; }

    /// <summary>The record dot and the trash glyph under the pointer: a rose red, apart from the danger color.</summary>
    public Color RecordColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CardPadding = new Thickness(14, 14, 14, 12);
        ListPadding = new Thickness(2, 4);
        MessageGap = 12;
        BubbleHeight = 44;
        BubblePadding = new Thickness(6, 6, 14, 6);
        BubbleGap = 10;
        PlaySize = 32;
        BubbleTimeOpacity = 0.8;
        ComposerHeight = 48;
        ComposerGap = 8;
        DiscardSize = 40;
        RecordingBackground = Oklch.MixOklab(n.SurfaceMuted, n.Foreground, 0.055);
        RecordColor = isDarkMode ? Oklch.ToColor(0.71, 0.155, 8) : Oklch.ToColor(0.53, 0.18, 10);
    }
}
