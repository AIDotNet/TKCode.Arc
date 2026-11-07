using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and tone colours of <see cref="StretchRefreshActivityRow"/> (Arc Stretch refresh, design reference): a
/// [32, 1fr, 8] grid with 12 px gaps, 20 px glyphs at stroke 1.75, a 240 × 180 photo with 12 px corners and an 8 px dot
/// 7.2 px down. The success and danger glyph colours are the measured literals (accent independent):
/// oklch(0.5 0.12 162) | oklch(0.79 0.14 162) and oklch(0.53 0.18 10) | oklch(0.71 0.155 8).
/// </summary>
[ControlDesignToken]
internal sealed class StretchRefreshActivityRowToken : AbstractControlDesignToken
{
    /// <summary>The leading column (32) and the gap between columns (12).</summary>
    public double LeadSize { get; set; }

    public double ColumnGap { get; set; }

    /// <summary>The status glyph's size (20) and stroke (1.75).</summary>
    public double GlyphSize { get; set; }

    public double GlyphStroke { get; set; }

    /// <summary>The unread dot (8) and its top offset (7.2, centred on the first line).</summary>
    public double DotSize { get; set; }

    public Thickness DotMargin { get; set; }

    /// <summary>The photo's widest size (240; 4:3) and its margin (6 above, 2 below).</summary>
    public double PhotoMaxWidth { get; set; }

    public Thickness PhotoMargin { get; set; }

    /// <summary>The sentence and quote line height (19.6) and the meta line height (16.8).</summary>
    public double BodyLineHeight { get; set; }

    public double MetaLineHeight { get; set; }

    /// <summary>The success glyph (green 162°) and the danger glyph (red 10°).</summary>
    public Color SuccessColor { get; set; }

    public Color DangerColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        LeadSize = 32;
        ColumnGap = 12;
        GlyphSize = 20;
        GlyphStroke = 1.75;
        DotSize = 8;
        DotMargin = new Thickness(0, 7.2, 0, 0);
        PhotoMaxWidth = 240;
        PhotoMargin = new Thickness(0, 6, 0, 2);
        BodyLineHeight = 19.6;
        MetaLineHeight = 16.8;
        SuccessColor = isDarkMode ? Oklch.ToColor(0.79, 0.14, 162) : Oklch.ToColor(0.5, 0.12, 162);
        DangerColor = isDarkMode ? Oklch.ToColor(0.71, 0.155, 8) : Oklch.ToColor(0.53, 0.18, 10);
    }
}
