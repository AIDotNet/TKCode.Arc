using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class AnnouncementBarToken : AbstractControlDesignToken
{
    /// <summary>Bar padding (6px 8px 6px 16px); narrow bars start at 14px.</summary>
    public Thickness BarPadding { get; set; }

    public Thickness NarrowBarPadding { get; set; }

    /// <summary>Gap between the mirror column, the message and the controls (space-3; narrow space-2).</summary>
    public double ColumnGap { get; set; }

    public double NarrowColumnGap { get; set; }

    /// <summary>Round icon buttons (32px) and the gap between them (2px).</summary>
    public double IconDiameter { get; set; }

    public double ControlGap { get; set; }

    /// <summary>Minimum height of the message window (32px).</summary>
    public double FaceMinHeight { get; set; }

    /// <summary>Message line height (text-sm × leading-body).</summary>
    public double MessageLineHeight { get; set; }

    /// <summary>Icon hover / press fill: the hover color at 10% / 12%.</summary>
    public Color IconHoverFill { get; set; }

    public Color IconPressedFill { get; set; }

    /// <summary>Inverted tone: secondary text is the background at 66%.</summary>
    public Color InvertedMutedForeground { get; set; }

    public Color InvertedIconHoverFill { get; set; }

    public Color InvertedIconPressedFill { get; set; }

    public CornerRadius LinkFocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        LinkFocusRingRadius = new CornerRadius(8);
        BarPadding = new Thickness(16, 6, 8, 6);
        NarrowBarPadding = new Thickness(14, 6, 8, 6);
        ColumnGap = 12;
        NarrowColumnGap = 8;
        IconDiameter = 32;
        ControlGap = 2;
        FaceMinHeight = 32;
        MessageLineHeight = 14 * 1.4;
        var n = ArcPalette.Neutrals(isDarkMode);
        IconHoverFill = Oklch.WithAlpha(n.Foreground, 0.10);
        IconPressedFill = Oklch.WithAlpha(n.Foreground, 0.12);
        InvertedMutedForeground = Oklch.WithAlpha(n.Background, 0.66);
        InvertedIconHoverFill = Oklch.WithAlpha(n.Background, 0.10);
        InvertedIconPressedFill = Oklch.WithAlpha(n.Background, 0.12);
    }
}
