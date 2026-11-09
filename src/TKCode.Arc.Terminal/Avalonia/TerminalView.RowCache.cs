using System.Text;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using TKCode.Terminal.Core;

namespace TKCode.Terminal.Avalonia;

/// <summary>
/// 行级绘制缓存（05-rendering.md 的"整形缓存"）。每个 <see cref="RowSnapshot"/> 对象对应一组背景游程与 <see cref="GlyphRun"/>：
/// 同一字体、同一前景色且网格连续的 cell 合并为一个 run（每个 <see cref="GlyphInfo"/> 的 advance 就是 cell 宽，所以字形位置
/// 与逐格绘制完全相同）。快照里未变化的行沿用上一帧的同一对象（<see cref="FrameCapturer"/>），所以缓存按引用查找、只对脏行重建；
/// 行在屏幕上的 y 由绘制时的平移变换提供，滚动不重建。两代字典：当前帧用到的行留下，上一帧剩下的行释放。
/// 调色板或 cell 度量（字体、字号、缩放）变化时整体重建。
/// </summary>
public sealed partial class TerminalView
{
    private const int MaxCachedBrushes = 4096;

    private readonly Dictionary<TerminalRgb, IImmutableSolidColorBrush> _brushes = new();
    private Dictionary<RowSnapshot, RowRuns> _rowRuns = new(ReferenceEqualityComparer.Instance);
    private Dictionary<RowSnapshot, RowRuns> _rowRunsPrevious = new(ReferenceEqualityComparer.Instance);
    private TerminalPalette? _runsPalette;
    private CellMetrics? _runsMetrics;
    private double _runsPad = double.NaN;
    private Pen? _hollowPen;
    private IImmutableSolidColorBrush? _hollowPenBrush;
    private readonly List<GlyphInfo> _runInfos = new();
    private readonly StringBuilder _runChars = new();

    /// <summary>缓存的行数（测试：稳定的帧不再增长 <see cref="RowRunBuilds"/>）。</summary>
    internal int CachedRowCount => _rowRuns.Count + _rowRunsPrevious.Count;

    /// <summary>测试：无头环境的占位字体不映射任何字符，由它提供字形（字体固定为主字体）。</summary>
    internal Func<int, ushort>? GlyphOverride { get; set; }

    /// <summary>行缓存重建次数（测试）。</summary>
    internal int RowRunBuilds { get; private set; }

    /// <summary>该行已缓存的字形 run（测试）；没有缓存时为空。</summary>
    internal IReadOnlyList<(GlyphRun Run, IImmutableSolidColorBrush Brush)> GlyphRunsOf(RowSnapshot row) =>
        _rowRunsPrevious.TryGetValue(row, out var runs) || _rowRuns.TryGetValue(row, out runs) ? runs.Glyphs : [];

    /// <summary>
    /// 一行的绘制指令：背景矩形（x 与宽度为 DIP，y 由调用方给出）与字形 run（原点相对行顶）。
    /// </summary>
    private sealed class RowRuns
    {
        public readonly List<(double X, double Width, IImmutableSolidColorBrush Brush)> Backgrounds = new();
        public readonly List<(GlyphRun Run, IImmutableSolidColorBrush Brush)> Glyphs = new();

        public void Dispose()
        {
            foreach (var (run, _) in Glyphs)
            {
                run.Dispose();
            }
            Glyphs.Clear();
            Backgrounds.Clear();
        }
    }

    private IImmutableSolidColorBrush Brush(TerminalRgb c)
    {
        if (!_brushes.TryGetValue(c, out var brush))
        {
            // 真彩色输出可以有任意多种颜色：到上限就重新开始（已缓存的 run 仍持有自己的画刷）
            if (_brushes.Count >= MaxCachedBrushes)
            {
                _brushes.Clear();
            }
            brush = new ImmutableSolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
            _brushes[c] = brush;
        }
        return brush;
    }

    private Pen HollowCursorPen(IImmutableSolidColorBrush brush, double scale)
    {
        if (_hollowPen is null || !ReferenceEquals(_hollowPenBrush, brush) || _hollowPen.Thickness != 1 / scale)
        {
            _hollowPen = new Pen(brush, 1 / scale);
            _hollowPenBrush = brush;
        }
        return _hollowPen;
    }

    /// <summary>调色板、度量或内边距变了：所有行重建。</summary>
    private void EnsureRowCacheFor(TerminalPalette palette, CellMetrics m, double pad)
    {
        if (ReferenceEquals(_runsPalette, palette) && ReferenceEquals(_runsMetrics, m) && _runsPad == pad)
        {
            return;
        }
        ClearRowCache();
        _runsPalette = palette;
        _runsMetrics = m;
        _runsPad = pad;
    }

    private void ClearRowCache()
    {
        foreach (var runs in _rowRuns.Values)
        {
            runs.Dispose();
        }
        foreach (var runs in _rowRunsPrevious.Values)
        {
            runs.Dispose();
        }
        _rowRuns.Clear();
        _rowRunsPrevious.Clear();
    }

    /// <summary>该行的绘制指令：当前帧已有的、上一帧留下的，或新建。</summary>
    private RowRuns RowRunsOf(RowSnapshot row, TerminalPalette palette, CellMetrics m, double pad)
    {
        if (_rowRuns.TryGetValue(row, out var runs))
        {
            return runs;
        }
        if (!_rowRunsPrevious.Remove(row, out runs))
        {
            runs = BuildRowRuns(row, palette, m, pad);
            RowRunBuilds++;
        }
        _rowRuns[row] = runs;
        return runs;
    }

    /// <summary>一帧画完：上一帧没再用到的行释放，当前帧的行成为下一帧的"上一帧"。</summary>
    private void EndRowCacheFrame()
    {
        foreach (var runs in _rowRunsPrevious.Values)
        {
            runs.Dispose();
        }
        _rowRunsPrevious.Clear();
        (_rowRunsPrevious, _rowRuns) = (_rowRuns, _rowRunsPrevious);
    }

    private RowRuns BuildRowRuns(RowSnapshot row, TerminalPalette palette, CellMetrics m, double pad)
    {
        var runs = new RowRuns();
        var cells = row.Cells;
        var scale = m.Scaling;
        var cellWidth = m.CellWidth / scale;

        // 背景：同色的连续格合并成一个矩形（inverse 时前景作背景）
        for (var x = 0; x < cells.Length;)
        {
            var bg = BackgroundOf(row, cells[x], palette);
            var end = x + 1;
            while (end < cells.Length && BackgroundOf(row, cells[end], palette) == bg)
            {
                end++;
            }
            if (bg is { } color)
            {
                runs.Backgrounds.Add(((pad + x * m.CellWidth) / scale, (end - x) * cellWidth, Brush(color)));
            }
            x = end;
        }

        // 字形：同字体、同色且网格连续的格合并成一个 run；原点对齐网格（宽字符占两格）
        GlyphTypeface? runFace = null;
        IImmutableSolidColorBrush? runBrush = null;
        var runStart = 0;
        var runNext = -1;
        var baseline = m.BaselineY / scale;
        var emSize = m.EmSizePx / scale;
        _runInfos.Clear();
        _runChars.Clear();

        void Flush()
        {
            if (runFace is null || _runInfos.Count == 0)
            {
                return;
            }
            var origin = new Point((pad + runStart * m.CellWidth) / scale, baseline);
            var glyphRun = new GlyphRun(runFace, emSize, _runChars.ToString().AsMemory(), _runInfos.ToArray(), origin);
            runs.Glyphs.Add((glyphRun, runBrush!));
            _runInfos.Clear();
            _runChars.Clear();
            runFace = null;
        }

        for (var x = 0; x < cells.Length; x++)
        {
            var cell = cells[x];
            var cp = TCell.Codepoint(cell);
            if (cp is 0 or ' ' || TCell.Width(cell) == CellWidth.SpacerTail)
            {
                continue;
            }
            var style = row.Styles[TCell.StyleIndex(cell)];
            if (style.Flags.HasFlag(StyleFlags.Invisible))
            {
                continue;
            }
            GlyphTypeface? face;
            ushort glyph;
            if (GlyphOverride is { } lookup)
            {
                face = _face ??= new Typeface(FontFamily).GlyphTypeface;
                glyph = lookup((int)cp);
            }
            else
            {
                face = FaceFor((int)cp);
                if (face is null || !face.CharacterToGlyphMap.TryGetGlyph((int)cp, out glyph))
                {
                    continue;
                }
            }
            var brush = Brush(ForegroundOf(style, palette));
            if (runFace is not null && (x != runNext || !ReferenceEquals(face, runFace) || !ReferenceEquals(brush, runBrush)))
            {
                Flush();
            }
            if (runFace is null)
            {
                runFace = face;
                runBrush = brush;
                runStart = x;
            }
            var columns = TCell.Width(cell) == CellWidth.Wide ? 2 : 1;
            var cluster = _runChars.Length;
            _runChars.Append(char.ConvertFromUtf32((int)cp));
            _runInfos.Add(new GlyphInfo(glyph, cluster, columns * cellWidth));
            runNext = x + columns;
        }
        Flush();
        return runs;
    }

    /// <summary>画一行：背景按行 y 直接填充，字形在平移到行顶的变换下绘制（run 原点相对行顶，所以滚动时 run 可复用）。</summary>
    private static void DrawRow(DrawingContext context, RowRuns runs, double rowY, double cellHeight)
    {
        foreach (var (x, width, brush) in runs.Backgrounds)
        {
            context.FillRectangle(brush, new Rect(x, rowY, width, cellHeight));
        }
        if (runs.Glyphs.Count == 0)
        {
            return;
        }
        using (context.PushTransform(Matrix.CreateTranslation(0, rowY)))
        {
            foreach (var (run, brush) in runs.Glyphs)
            {
                context.DrawGlyphRun(brush, run);
            }
        }
    }
}
