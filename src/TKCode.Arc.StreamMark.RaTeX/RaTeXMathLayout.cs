using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using StreamMark.Avalonia.Extensions;

namespace StreamMark.RaTeX;

/// <summary>
/// RaTeX 显示列表对应的 <see cref="MathLayout"/>。排版数据不可变、可跨线程共享；
/// Avalonia 绘制图元（GlyphRun / 几何）在首次按某个字号绘制时于界面线程构建并缓存。
/// </summary>
public sealed class RaTeXMathLayout : MathLayout
{
    /// <summary>传给 RaTeX 的默认颜色哨兵：带此颜色的条目在绘制时使用 foreground 画刷。</summary>
    internal static readonly DisplayColor DefaultColorSentinel = new(0.0123f, 0.0456f, 0.0789f, 1f);

    private const int MaxCachedSizes = 4;

    private readonly List<(double FontSize, DrawOp[] Ops)> _plans = new(MaxCachedSizes);

    public RaTeXMathLayout(DisplayList displayList)
        : this(displayList, false)
    {
    }

    /// <param name="displayList">RaTeX 显示列表。</param>
    /// <param name="blackFramesAreDefault">
    /// RaTeX 的 <c>\fbox</c> / <c>\boxed</c> 边框固定为黑色（ratex-layout <c>layout_enclose</c> 中
    /// <c>unwrap_or(Color::BLACK)</c>，未使用默认颜色）。为 true 时把纯黑的细边框矩形视为默认颜色。
    /// </param>
    internal RaTeXMathLayout(DisplayList displayList, bool blackFramesAreDefault)
    {
        DisplayList = displayList ?? throw new ArgumentNullException(nameof(displayList));
        _blackFramesAreDefault = blackFramesAreDefault;
    }

    private readonly bool _blackFramesAreDefault;

    /// <summary>RaTeX 原始显示列表（em 单位）。</summary>
    public DisplayList DisplayList { get; }

    public override double Width => DisplayList.Width;

    public override double Ascent => DisplayList.Height;

    public override double Descent => DisplayList.Depth;

    public override void Draw(DrawingContext context, Point origin, double fontSize, IBrush foreground)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (fontSize <= 0 || DisplayList.Items.Count == 0)
        {
            return;
        }
        var ops = GetPlan(fontSize);
        using (context.PushTransform(Matrix.CreateTranslation(origin.X, origin.Y)))
        {
            foreach (var op in ops)
            {
                var brush = op.Brush ?? foreground;
                switch (op)
                {
                    case GlyphOp g:
                        context.DrawGlyphRun(brush, g.Run);
                        break;
                    case RectOp r:
                        context.DrawRectangle(brush, null, r.Rect);
                        break;
                    case GeometryOp p when p.StrokeThickness > 0:
                        context.DrawGeometry(null, new Pen(brush, p.StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), p.Geometry);
                        break;
                    case GeometryOp p:
                        context.DrawGeometry(brush, null, p.Geometry);
                        break;
                }
            }
        }
    }

    private DrawOp[] GetPlan(double fontSize)
    {
        lock (_plans)
        {
            foreach (var (size, cached) in _plans)
            {
                if (size == fontSize)
                {
                    return cached;
                }
            }
            var ops = BuildPlan(fontSize);
            if (_plans.Count == MaxCachedSizes)
            {
                _plans.RemoveAt(0);
            }
            _plans.Add((fontSize, ops));
            return ops;
        }
    }

    private DrawOp[] BuildPlan(double em)
    {
        var ops = new List<DrawOp>(DisplayList.Items.Count);
        var batch = new GlyphBatch();
        foreach (var item in DisplayList.Items)
        {
            switch (item.Type)
            {
                case "GlyphPath":
                    if (KaTeXFonts.Resolve(item.Font, item.CharCode) is not { } glyph)
                    {
                        continue;
                    }
                    var size = em * (item.Scale > 0 ? item.Scale : 1);
                    var brush = BrushFor(item.Color);
                    var x = item.X * em;
                    var y = item.Y * em;
                    if (!batch.TryAppend(glyph, size, brush, x, y))
                    {
                        batch.Flush(ops);
                        batch.Start(glyph, size, brush, x, y);
                    }
                    break;
                case "Line":
                    batch.Flush(ops);
                    AddLine(ops, item, em);
                    break;
                case "Rect":
                    batch.Flush(ops);
                    if (item.Width > 0 && item.Height > 0)
                    {
                        var rectBrush = _blackFramesAreDefault && IsFrameRule(item) ? null : BrushFor(item.Color);
                        ops.Add(new RectOp(rectBrush, new Rect(item.X * em, item.Y * em, item.Width * em, item.Height * em)));
                    }
                    break;
                case "Path":
                    batch.Flush(ops);
                    AddPath(ops, item, em);
                    break;
            }
        }
        batch.Flush(ops);
        return ops.ToArray();
    }

    private static void AddLine(List<DrawOp> ops, DisplayItem item, double em)
    {
        if (item.Width <= 0)
        {
            return;
        }
        var brush = BrushFor(item.Color);
        // 与 ratex-render 一致：y 为线条中心，厚度至少 1 像素以免细分数线消失
        var t = Math.Max(item.Thickness * em, 1.0);
        var x = item.X * em;
        var top = item.Y * em - t / 2;
        var width = item.Width * em;
        if (item.Dashed == true)
        {
            var dash = Math.Max(4 * t, 2);
            for (var cx = x; cx < x + width; cx += 2 * dash)
            {
                ops.Add(new RectOp(brush, new Rect(cx, top, Math.Min(dash, x + width - cx), t)));
            }
        }
        else
        {
            ops.Add(new RectOp(brush, new Rect(x, top, width, t)));
        }
    }

    private static void AddPath(List<DrawOp> ops, DisplayItem item, double em)
    {
        var commands = item.Commands;
        if (commands is null || commands.Count == 0)
        {
            return;
        }
        var brush = BrushFor(item.Color);
        if (item.Fill)
        {
            // 与 ratex-render 一致：每个子路径单独以 EvenOdd 填充，避免组合箭头的反向子路径相互抵消
            var start = 0;
            for (var i = 1; i <= commands.Count; i++)
            {
                if (i == commands.Count || commands[i].Type == "MoveTo")
                {
                    if (BuildGeometry(commands, start, i, item.X, item.Y, em, true) is { } geometry)
                    {
                        ops.Add(new GeometryOp(brush, geometry, 0));
                    }
                    start = i;
                }
            }
        }
        else if (BuildGeometry(commands, 0, commands.Count, item.X, item.Y, em, false) is { } geometry)
        {
            // 描边宽度约为默认规则厚度（ratex-svg 在 40px 时为 1.5px）
            ops.Add(new GeometryOp(brush, geometry, Math.Max(em * 0.0375, 0.75)));
        }
    }

    private static StreamGeometry? BuildGeometry(IReadOnlyList<PathCommand> commands, int start, int end, double ox, double oy, double em, bool fill)
    {
        var geometry = new StreamGeometry();
        var any = false;
        using (var ctx = geometry.Open())
        {
            ctx.SetFillRule(FillRule.EvenOdd);
            var open = false;
            Point P(double x, double y) => new((ox + x) * em, (oy + y) * em);
            for (var i = start; i < end; i++)
            {
                var c = commands[i];
                switch (c.Type)
                {
                    case "MoveTo":
                        if (open)
                        {
                            ctx.EndFigure(false);
                        }
                        ctx.BeginFigure(P(c.X, c.Y), fill);
                        open = true;
                        break;
                    case "LineTo" when open:
                        ctx.LineTo(P(c.X, c.Y));
                        any = true;
                        break;
                    case "CubicTo" when open:
                        ctx.CubicBezierTo(P(c.X1, c.Y1), P(c.X2, c.Y2), P(c.X, c.Y));
                        any = true;
                        break;
                    case "QuadTo" when open:
                        ctx.QuadraticBezierTo(P(c.X1, c.Y1), P(c.X, c.Y));
                        any = true;
                        break;
                    case "Close" when open:
                        ctx.EndFigure(true);
                        open = false;
                        break;
                }
            }
            if (open)
            {
                ctx.EndFigure(false);
            }
        }
        return any ? geometry : null;
    }

    private static bool IsFrameRule(DisplayItem item) =>
        item.Color is { R: 0, G: 0, B: 0, A: 1 } && Math.Min(item.Width, item.Height) <= 0.05;

    /// <summary>哨兵颜色（默认颜色）返回 null，表示绘制时使用 foreground。</summary>
    private static IBrush? BrushFor(DisplayColor? color)
    {
        if (color is not { } c || IsSentinel(c))
        {
            return null;
        }
        static byte B(float v) => (byte)Math.Round(Math.Clamp(v, 0f, 1f) * 255);
        return new ImmutableSolidColorBrush(Color.FromArgb(B(c.A), B(c.R), B(c.G), B(c.B)));
    }

    private static bool IsSentinel(DisplayColor c)
    {
        var s = DefaultColorSentinel;
        return Math.Abs(c.R - s.R) < 1e-4 && Math.Abs(c.G - s.G) < 1e-4 && Math.Abs(c.B - s.B) < 1e-4;
    }

    private abstract record DrawOp(IBrush? Brush);

    private sealed record GlyphOp(IBrush? Brush, GlyphRun Run) : DrawOp(Brush);

    private sealed record RectOp(IBrush? Brush, Rect Rect) : DrawOp(Brush);

    private sealed record GeometryOp(IBrush? Brush, Geometry Geometry, double StrokeThickness) : DrawOp(Brush);

    /// <summary>把同字体、同字号、同颜色、同基线且从左到右的连续字形合并成一个 GlyphRun。</summary>
    private sealed class GlyphBatch
    {
        private readonly List<(ushort Glyph, string Text, double X)> _glyphs = [];
        private GlyphTypeface? _typeface;
        private double _size;
        private IBrush? _brush;
        private double _baseline;

        public bool TryAppend(ResolvedGlyph glyph, double size, IBrush? brush, double x, double y)
        {
            if (_typeface is null || !ReferenceEquals(_typeface, glyph.Typeface) || _size != size
                || !SameBrush(_brush, brush) || Math.Abs(_baseline - y) > 1e-6 || x < _glyphs[^1].X)
            {
                return false;
            }
            _glyphs.Add((glyph.GlyphIndex, glyph.Text, x));
            return true;
        }

        public void Start(ResolvedGlyph glyph, double size, IBrush? brush, double x, double y)
        {
            _typeface = glyph.Typeface;
            _size = size;
            _brush = brush;
            _baseline = y;
            _glyphs.Add((glyph.GlyphIndex, glyph.Text, x));
        }

        public void Flush(List<DrawOp> ops)
        {
            if (_typeface is null || _glyphs.Count == 0)
            {
                _typeface = null;
                _glyphs.Clear();
                return;
            }
            var infos = new GlyphInfo[_glyphs.Count];
            var text = new System.Text.StringBuilder();
            for (var i = 0; i < _glyphs.Count; i++)
            {
                var (glyph, chars, x) = _glyphs[i];
                double advance;
                if (i + 1 < _glyphs.Count)
                {
                    advance = _glyphs[i + 1].X - x;
                }
                else
                {
                    advance = _typeface.TryGetHorizontalGlyphAdvance(glyph, out var a)
                        ? a * _size / _typeface.Metrics.DesignEmHeight
                        : 0;
                }
                infos[i] = new GlyphInfo(glyph, text.Length, advance);
                text.Append(chars);
            }
            var run = new GlyphRun(_typeface, _size, text.ToString().AsMemory(), infos, new Point(_glyphs[0].X, _baseline));
            ops.Add(new GlyphOp(_brush, run));
            _typeface = null;
            _glyphs.Clear();
        }

        private static bool SameBrush(IBrush? a, IBrush? b) =>
            a is null ? b is null
            : b is not null && a is ISolidColorBrush sa && b is ISolidColorBrush sb && sa.Color == sb.Color && sa.Opacity == sb.Opacity;
    }
}
