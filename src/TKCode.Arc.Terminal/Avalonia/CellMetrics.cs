using Avalonia.Media;

namespace TKCode.Terminal.Avalonia;

/// <summary>
/// 等宽网格的 cell 度量，全部以**设备像素整数**计（05-rendering.md §5.2）：cell 宽高取 round，
/// 基线到 cell 顶的距离取整，x 坐标强制对齐网格。
/// </summary>
public sealed record CellMetrics(int CellWidth, int CellHeight, int BaselineY, int UnderlineThickness, double Scaling, double EmSizePx)
{
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
        return new CellMetrics(cellW, cellH, baselineY, underline, scaling, emPx);
    }
}
