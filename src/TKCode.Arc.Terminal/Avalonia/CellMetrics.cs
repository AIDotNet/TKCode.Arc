using Avalonia.Media;

namespace TKCode.Terminal.Avalonia;

/// <summary>
/// 等宽网格的 cell 度量，全部以**设备像素整数**计（05-rendering.md §5.2）：cell 宽高取 round，
/// 基线到 cell 顶的距离取整，x 坐标强制对齐网格。
/// </summary>
public sealed record CellMetrics(int CellWidth, int CellHeight, int BaselineY, int UnderlineThickness, double Scaling, double EmSizePx)
{
    /// <summary>
    /// Top of the underline, in device pixels from the top of the cell. From the font's underline position when it has
    /// one, otherwise one underline thickness below the baseline; always kept inside the cell
    /// (<c>UnderlinePosition + UnderlineThickness &lt;= CellHeight</c>).
    /// </summary>
    public int UnderlinePosition { get; init; } = Math.Clamp(BaselineY + UnderlineThickness, 0, Math.Max(0, CellHeight - UnderlineThickness));

    /// <summary>Top of the strikethrough line, in device pixels from the top of the cell (font metrics, or about half the x-height above the baseline).</summary>
    public int StrikethroughPosition { get; init; } = Math.Clamp(BaselineY - (int)Math.Round(EmSizePx * 0.25) - UnderlineThickness / 2, 0, Math.Max(0, CellHeight - UnderlineThickness));

    /// <summary>Thickness of the strikethrough line in device pixels (font metrics, or the underline thickness).</summary>
    public int StrikethroughThickness { get; init; } = UnderlineThickness;

    /// <summary>
    /// Thickness of a light line of the box drawing characters the view draws itself (<see cref="TerminalView.CustomGlyphs"/>),
    /// in device pixels; heavy lines are twice as thick. The underline thickness, as in Ghostty.
    /// </summary>
    public int BoxThickness { get; init; } = UnderlineThickness;

    public static CellMetrics Compute(GlyphTypeface face, double emSizeDip, double scaling, double lineHeight = 1.0)
    {
        var m = face.Metrics;
        var emPx = emSizeDip * scaling;
        var s = emPx / m.DesignEmHeight;

        // ASCII 可见字符的最大 advance（等宽字体里都相同；防止误配到比例字体时字形重叠）
        double maxAdvance = 0;
        for (var c = 32; c < 127; c++)
        {
            if (face.CharacterToGlyphMap.TryGetGlyph(c, out var glyph) && face.TryGetHorizontalGlyphAdvance(glyph, out var advance))
            {
                maxAdvance = Math.Max(maxAdvance, advance);
            }
        }
        var cellW = Math.Max(1, (int)Math.Round(maxAdvance * s));

        // Avalonia 的 Ascent 为负（基线以上）
        var ascent = Math.Abs(m.Ascent) * s;
        var descent = Math.Abs(m.Descent) * s;
        var gap = Math.Max(0, m.LineGap) * s;
        var faceH = ascent + descent + gap;
        var cellH = Math.Max(1, (int)Math.Round(faceH * lineHeight));
        var baselineFromBottom = Math.Round(gap / 2 + descent - (cellH - faceH) / 2);
        var baselineY = (int)(cellH - baselineFromBottom);
        var underline = Math.Max(1, (int)Math.Ceiling(m.UnderlineThickness > 0 ? m.UnderlineThickness * s : emPx * 0.06));
        var metrics = new CellMetrics(cellW, cellH, baselineY, underline, scaling, emPx);

        // Avalonia's font metrics grow downwards: the underline position is positive (below the baseline), the
        // strikethrough position negative. Both name the top of the line.
        var strikeThickness = m.StrikethroughThickness > 0 ? Math.Max(1, (int)Math.Round(m.StrikethroughThickness * s)) : underline;
        return metrics with
        {
            UnderlinePosition = m.UnderlinePosition > 0
                ? Math.Clamp(baselineY + (int)Math.Round(m.UnderlinePosition * s), 0, Math.Max(0, cellH - underline))
                : metrics.UnderlinePosition,
            StrikethroughPosition = m.StrikethroughPosition < 0
                ? Math.Clamp(baselineY + (int)Math.Round(m.StrikethroughPosition * s), 0, Math.Max(0, cellH - strikeThickness))
                : metrics.StrikethroughPosition,
            StrikethroughThickness = strikeThickness,
        };
    }
}
