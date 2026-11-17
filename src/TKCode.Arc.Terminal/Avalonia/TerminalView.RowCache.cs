using System.Text;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using TKCode.Terminal.Core;

namespace TKCode.Terminal.Avalonia;

/// <summary>
/// 行级绘制缓存（05-rendering.md 的"整形缓存"）。每个 <see cref="RowSnapshot"/> 对象对应一组背景游程、装饰线、<see cref="GlyphRun"/>
/// 与自绘字形：同一字体、同一前景色且网格连续的 cell 合并为一个 run（每个 <see cref="GlyphInfo"/> 的 advance 就是 cell 宽，所以字形位置
/// 与逐格绘制完全相同）；字素簇的 advance 之和等于它的格宽；emoji 各自成 run（按格子缩放）。快照里未变化的行沿用上一帧的同一对象
/// （<see cref="FrameCapturer"/>），所以缓存按引用查找、只对脏行重建；行在屏幕上的 y 由绘制时的平移变换提供，滚动不重建。
/// 两代字典：当前帧用到的行留下，上一帧剩下的行释放。调色板、cell 度量（字体、字号、缩放）或渲染选项变化时整体重建。
/// </summary>
public sealed partial class TerminalView
{
    private const int MaxCachedBrushes = 4096;

    private readonly Dictionary<TerminalRgb, IImmutableSolidColorBrush> _brushes = new();
    private readonly Dictionary<(TerminalRgb Color, double Opacity), IImmutableSolidColorBrush> _alphaBrushes = new();
    private readonly Dictionary<(IBrush Brush, double Thickness), Pen> _pens = new();
    private readonly Dictionary<(ITerminalGlyphProvider Provider, int Codepoint, int Width), TerminalGlyph?> _sprites = new();
    private Dictionary<RowSnapshot, RowRuns> _rowRuns = new(ReferenceEqualityComparer.Instance);
    private Dictionary<RowSnapshot, RowRuns> _rowRunsPrevious = new(ReferenceEqualityComparer.Instance);
    private readonly List<RowRuns> _frameRuns = new();
    private TerminalPalette? _runsPalette;
    private CellMetrics? _runsMetrics;
    private double _runsPad = double.NaN;
    private int _runsVersion = -1;
    private Pen? _hollowPen;
    private IImmutableSolidColorBrush? _hollowPenBrush;
    private readonly List<GlyphInfo> _runInfos = new();
    private readonly StringBuilder _runChars = new();
    private TerminalRgb[] _cellForegrounds = [];

    /// <summary>缓存的行数（测试：稳定的帧不再增长 <see cref="RowRunBuilds"/>）。</summary>
    internal int CachedRowCount => _rowRuns.Count + _rowRunsPrevious.Count;

    /// <summary>测试：无头环境的占位字体不映射任何字符，由它提供字形（字体为主字体对应样式的 face）。</summary>
    internal Func<int, ushort>? GlyphOverride { get; set; }

    /// <summary>行缓存重建次数（测试）。</summary>
    internal int RowRunBuilds { get; private set; }

    /// <summary>该行已缓存的字形 run（测试）；没有缓存时为空。</summary>
    internal IReadOnlyList<(GlyphRun Run, IImmutableSolidColorBrush Brush)> GlyphRunsOf(RowSnapshot row) =>
        CachedRuns(row) is { } runs ? runs.Glyphs.Select(g => (g.Run, g.Brush)).ToList() : [];

    /// <summary>该行已缓存的字形 run 及其是否闪烁（测试）。</summary>
    internal IReadOnlyList<(GlyphRun Run, IImmutableSolidColorBrush Brush, bool Blink)> AllGlyphRunsOf(RowSnapshot row) =>
        CachedRuns(row)?.Glyphs ?? [];

    /// <summary>该行已缓存的自绘字形（测试）：字形、所在列与画刷。</summary>
    internal IReadOnlyList<Sprite> SpritesOf(RowSnapshot row) => CachedRuns(row)?.Sprites ?? [];

    /// <summary>该行已缓存的装饰线（测试）：文字下方的在前，删除线在后。</summary>
    internal IReadOnlyList<Decoration> DecorationsOf(RowSnapshot row) =>
        CachedRuns(row) is { } runs ? [.. runs.Under, .. runs.Over] : [];

    private RowRuns? CachedRuns(RowSnapshot row) =>
        _rowRunsPrevious.TryGetValue(row, out var runs) || _rowRuns.TryGetValue(row, out runs) ? runs : null;

    internal enum DecorationKind
    {
        Underline,
        DoubleUnderline,
        CurlyUnderline,
        DottedUnderline,
        DashedUnderline,
        Strikethrough,
        Overline,
    }

    /// <summary>
    /// A decoration span over columns [<see cref="StartColumn"/>, <see cref="EndColumn"/>): <see cref="Bounds"/> in DIP relative
    /// to the row's top; filled as a rectangle, or as <see cref="Geometry"/> (stroked when <see cref="Stroke"/> &gt; 0).
    /// </summary>
    internal sealed record Decoration(DecorationKind Kind, int StartColumn, int EndColumn, IImmutableSolidColorBrush Brush, Rect Bounds, Geometry? Geometry, double Stroke)
    {
        /// <summary>Curly underline: where the wave starts and ends (tests: continuity across spans).</summary>
        public Point Start { get; init; }
        public Point End { get; init; }
    }

    /// <summary>A cell drawn by a <see cref="ITerminalGlyphProvider"/>: <see cref="X"/> in DIP relative to the view, <see cref="Width"/> in device pixels.</summary>
    internal sealed record Sprite(TerminalGlyph Glyph, int Column, double X, int Width, IImmutableSolidColorBrush Brush, bool Blink);

    /// <summary>一行的绘制指令：背景矩形（x 与宽度为 DIP，y 由调用方给出），其余（原点相对行顶）在平移到行顶后绘制。</summary>
    private sealed class RowRuns
    {
        public readonly List<(double X, double Width, IImmutableSolidColorBrush Brush)> Backgrounds = new();
        public readonly List<Decoration> Under = new();
        public readonly List<(GlyphRun Run, IImmutableSolidColorBrush Brush, bool Blink)> Glyphs = new();
        public readonly List<Sprite> Sprites = new();
        public readonly List<Decoration> Over = new();
        public bool HasBlink;

        public bool HasForeground => Under.Count > 0 || Glyphs.Count > 0 || Sprites.Count > 0 || Over.Count > 0;

        public void Dispose()
        {
            foreach (var (run, _, _) in Glyphs)
            {
                run.Dispose();
            }
            Glyphs.Clear();
            Backgrounds.Clear();
            Under.Clear();
            Sprites.Clear();
            Over.Clear();
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

    private IImmutableSolidColorBrush Brush(TerminalRgb c, double opacity)
    {
        if (opacity >= 1)
        {
            return Brush(c);
        }
        if (!_alphaBrushes.TryGetValue((c, opacity), out var brush))
        {
            if (_alphaBrushes.Count >= MaxCachedBrushes)
            {
                _alphaBrushes.Clear();
            }
            brush = new ImmutableSolidColorBrush(Color.FromRgb(c.R, c.G, c.B), opacity);
            _alphaBrushes[(c, opacity)] = brush;
        }
        return brush;
    }

    private Pen PenOf(IBrush brush, double thickness)
    {
        if (!_pens.TryGetValue((brush, thickness), out var pen))
        {
            if (_pens.Count >= 256)
            {
                _pens.Clear();
            }
            pen = new Pen(brush, thickness);
            _pens[(brush, thickness)] = pen;
        }
        return pen;
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

    /// <summary>调色板、度量、内边距或渲染选项变了：所有行重建。</summary>
    private void EnsureRowCacheFor(TerminalPalette palette, CellMetrics m, double pad)
    {
        if (ReferenceEquals(_runsPalette, palette) && ReferenceEquals(_runsMetrics, m) && _runsPad == pad && _runsVersion == _renderVersion)
        {
            return;
        }
        ClearRowCache();
        if (!ReferenceEquals(_runsMetrics, m))
        {
            _sprites.Clear();
            _shaped.Clear();
        }
        _runsPalette = palette;
        _runsMetrics = m;
        _runsPad = pad;
        _runsVersion = _renderVersion;
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

    /// <summary>The provider that draws <paramref name="codepoint"/>: a host provider, the built-in glyphs when <see cref="CustomGlyphs"/> is on, or none.</summary>
    internal ITerminalGlyphProvider? SpriteProviderFor(int codepoint)
    {
        foreach (var provider in GlyphProviders)
        {
            if (provider.Handles(codepoint))
            {
                return provider;
            }
        }
        return CustomGlyphs && TerminalBuiltInGlyphs.Instance.Handles(codepoint) ? TerminalBuiltInGlyphs.Instance : null;
    }

    private TerminalGlyph? SpriteOf(ITerminalGlyphProvider provider, int codepoint, int widthPx, CellMetrics m)
    {
        if (!_sprites.TryGetValue((provider, codepoint, widthPx), out var glyph))
        {
            glyph = provider.Create(codepoint, new TerminalGlyphRequest(widthPx, m.CellHeight, m.BoxThickness, m.Scaling));
            _sprites[(provider, codepoint, widthPx)] = glyph;
        }
        return glyph;
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

        // 字形：同字体、同色、同闪烁状态且网格连续的格合并成一个 run；原点对齐网格（宽字符占两格）
        GlyphTypeface? runFace = null;
        IImmutableSolidColorBrush? runBrush = null;
        var runBlink = false;
        var runStart = 0;
        var runNext = -1;
        var baseline = m.BaselineY / scale;
        var emSize = m.EmSizePx / scale;
        _runInfos.Clear();
        _runChars.Clear();
        if (_cellForegrounds.Length < cells.Length)
        {
            _cellForegrounds = new TerminalRgb[cells.Length];
        }

        void Flush()
        {
            if (runFace is null || _runInfos.Count == 0)
            {
                runFace = null;
                return;
            }
            var origin = new Point((pad + runStart * m.CellWidth) / scale, baseline);
            var glyphRun = new GlyphRun(runFace, emSize, _runChars.ToString().AsMemory(), _runInfos.ToArray(), origin);
            runs.Glyphs.Add((glyphRun, runBrush!, runBlink));
            _runInfos.Clear();
            _runChars.Clear();
            runFace = null;
        }

        for (var x = 0; x < cells.Length; x++)
        {
            var cell = cells[x];
            var style = row.Styles[TCell.StyleIndex(cell)];
            var cp = (int)TCell.Codepoint(cell);
            var drawn = cp is not (0 or ' ') && TCell.Width(cell) != CellWidth.SpacerTail && !style.Flags.HasFlag(StyleFlags.Invisible);
            if (!drawn)
            {
                // Blank cells need a foreground only for their decorations.
                if (style.Underline != UnderlineStyle.None || (style.Flags & (StyleFlags.Strikethrough | StyleFlags.Overline)) != 0)
                {
                    _cellForegrounds[x] = ResolveCellColors(row, cell, palette, contrastExempt: false).Foreground;
                }
                continue;
            }
            var provider = SpriteProviderFor(cp);
            var emoji = provider is null && (cp >= 0x231A || TCell.HasGrapheme(cell)) && TerminalUnicode.IsEmojiPresentation(cp, GraphemesOf(row, x, cell));
            // Custom glyphs and emoji keep their colors whatever the minimum contrast (05-rendering.md §10.5).
            var fg = ResolveCellColors(row, cell, palette, contrastExempt: provider is not null || emoji).Foreground;
            _cellForegrounds[x] = fg;
            var blink = style.Flags.HasFlag(StyleFlags.Blink);
            var columns = TCell.Width(cell) == CellWidth.Wide ? 2 : 1;
            runs.HasBlink |= blink;

            if (provider is not null)
            {
                Flush();
                if (SpriteOf(provider, cp, columns * m.CellWidth, m) is { } glyph)
                {
                    runs.Sprites.Add(new Sprite(glyph, x, (pad + x * m.CellWidth) / scale, columns * m.CellWidth, Brush(fg, glyph.Opacity), blink));
                }
                continue;
            }

            if (!TryCellGlyphs(row, x, StyleOf(style), columns * cellWidth, m, out var g))
            {
                continue;
            }
            var brush = Brush(fg);
            if (g.OwnRun)
            {
                Flush();
                var origin = new Point((pad + x * m.CellWidth) / scale + g.OffsetX, g.Baseline);
                runs.Glyphs.Add((new GlyphRun(g.Face, g.EmSize, g.Text.AsMemory(), g.Shaped!, origin), brush, blink));
                continue;
            }
            if (runFace is not null && (x != runNext || !ReferenceEquals(g.Face, runFace) || !ReferenceEquals(brush, runBrush) || blink != runBlink))
            {
                Flush();
            }
            if (runFace is null)
            {
                runFace = g.Face;
                runBrush = brush;
                runBlink = blink;
                runStart = x;
            }
            var cluster = _runChars.Length;
            _runChars.Append(g.Text);
            if (g.Shaped is { } shaped)
            {
                foreach (var info in shaped)
                {
                    _runInfos.Add(new GlyphInfo(info.GlyphIndex, cluster + info.GlyphCluster, info.GlyphAdvance, info.GlyphOffset));
                }
            }
            else
            {
                _runInfos.Add(new GlyphInfo(g.Glyph, cluster, columns * cellWidth));
            }
            runNext = x + columns;
        }
        Flush();

        BuildDecorations(row, runs, m, pad);
        return runs;
    }

    private static int[]? GraphemesOf(RowSnapshot row, int column, ulong cell) =>
        TCell.HasGrapheme(cell) && row.Graphemes is { } graphemes && graphemes.TryGetValue(column, out var extra) ? extra : null;

    /// <summary>画一行的背景（按行 y 直接填充）。</summary>
    private static void DrawBackgrounds(DrawingContext context, RowRuns runs, double rowY, double cellHeight)
    {
        foreach (var (x, width, brush) in runs.Backgrounds)
        {
            context.FillRectangle(brush, new Rect(x, rowY, width, cellHeight));
        }
    }

    /// <summary>
    /// 画一行的前景：下划线与上划线、字形、自绘字形、删除线，都在平移到行顶的变换下绘制（原点相对行顶，所以滚动时可复用）。
    /// <paramref name="hideBlinking"/>：闪烁的"灭"相位，跳过闪烁格子的字形（背景与装饰线照画）。
    /// </summary>
    private void DrawRowText(DrawingContext context, RowRuns runs, double rowY, double scale, bool hideBlinking)
    {
        if (!runs.HasForeground)
        {
            return;
        }
        using (context.PushTransform(Matrix.CreateTranslation(0, rowY)))
        {
            foreach (var decoration in runs.Under)
            {
                DrawDecoration(context, decoration);
            }
            foreach (var (run, brush, blink) in runs.Glyphs)
            {
                if (hideBlinking && blink)
                {
                    continue;
                }
                context.DrawGlyphRun(brush, run);
            }
            foreach (var sprite in runs.Sprites)
            {
                if (hideBlinking && sprite.Blink)
                {
                    continue;
                }
                DrawSprite(context, sprite, scale);
            }
            foreach (var decoration in runs.Over)
            {
                DrawDecoration(context, decoration);
            }
        }
    }

    private void DrawDecoration(DrawingContext context, Decoration d)
    {
        if (d.Geometry is null)
        {
            context.FillRectangle(d.Brush, d.Bounds);
        }
        else if (d.Stroke > 0)
        {
            context.DrawGeometry(null, PenOf(d.Brush, d.Stroke), d.Geometry);
        }
        else
        {
            context.DrawGeometry(d.Brush, null, d.Geometry);
        }
    }

    /// <summary>自绘字形：几何以设备像素、cell 左上角为原点，所以先缩放到 DIP 再平移到格子。</summary>
    private void DrawSprite(DrawingContext context, Sprite sprite, double scale)
    {
        var glyph = sprite.Glyph;
        using (context.PushTransform(Matrix.CreateScale(1 / scale, 1 / scale) * Matrix.CreateTranslation(sprite.X, 0)))
        {
            if (glyph.StrokeThickness > 0)
            {
                // A stroke reaches half its thickness past the path: keep it inside the cell so it does not overlap neighbors.
                using (context.PushClip(new Rect(0, 0, sprite.Width, Metrics.CellHeight)))
                {
                    context.DrawGeometry(null, PenOf(sprite.Brush, glyph.StrokeThickness), glyph.Geometry);
                }
            }
            else
            {
                context.DrawGeometry(sprite.Brush, null, glyph.Geometry);
            }
        }
    }
}
