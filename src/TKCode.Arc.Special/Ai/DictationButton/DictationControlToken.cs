using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="DictationControl"/>: the 36px mic, the 216px recording pill with its 4px padding and 28px
/// buttons, the waveform's bars and the timer, and Try again. Colors come from the ArcDesign tokens in the theme; the
/// hover and pressed tints are derived from the neutrals.
/// </summary>
[ControlDesignToken]
internal sealed class DictationControlToken : AbstractControlDesignToken
{
    /// <summary>The mic and the pill's height (every toolbar target is 36px).</summary>
    public double ButtonSize { get; set; }

    /// <summary>The mic glyph.</summary>
    public double IconSize { get; set; }

    /// <summary>The pill's fixed width, so nothing reflows while recording.</summary>
    public double PillWidth { get; set; }

    /// <summary>The pill's padding around its 28px buttons.</summary>
    public Thickness PillPadding { get; set; }

    /// <summary>Cancel and Insert inside the pill.</summary>
    public double PillButtonSize { get; set; }

    public double PillIconSize { get; set; }

    /// <summary>The waveform: its height, the space around it, a bar's width, the gap and the dot a silent bar becomes.</summary>
    public double WaveformHeight { get; set; }

    public Thickness WaveformMargin { get; set; }

    public double BarWidth { get; set; }

    public double BarGap { get; set; }

    public double BarMinHeight { get; set; }

    /// <summary>The timer: 12px tabular numerals in a reserved width.</summary>
    public double TimerFontSize { get; set; }

    public double TimerWidth { get; set; }

    public Thickness TimerMargin { get; set; }

    /// <summary>Try again: its padding and the space between its icon and label.</summary>
    public Thickness RetryPadding { get; set; }

    public double RetryGap { get; set; }

    public double RetryFontSize { get; set; }

    /// <summary>The icon buttons' tints: Foreground at 6% under the pointer, 10% pressed.</summary>
    public Color HoverBackground { get; set; }

    public Color PressedBackground { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        ButtonSize = 36;
        IconSize = 18;
        PillWidth = 216;
        PillPadding = new Thickness(4);
        PillButtonSize = 28;
        PillIconSize = 16;
        WaveformHeight = 18;
        WaveformMargin = new Thickness(8, 0, 8, 0);
        BarWidth = 3;
        BarGap = 2;
        BarMinHeight = 3;
        TimerFontSize = 12;
        TimerWidth = 34;
        TimerMargin = new Thickness(0, 0, 8, 0);
        RetryPadding = new Thickness(10, 0, 14, 0);
        RetryGap = 6;
        RetryFontSize = 13;
        HoverBackground = Oklch.WithAlpha(n.Foreground, 0.06);
        PressedBackground = Oklch.WithAlpha(n.Foreground, 0.10);
    }
}
