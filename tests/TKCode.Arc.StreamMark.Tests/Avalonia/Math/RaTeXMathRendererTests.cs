using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using StreamMark.Avalonia.Extensions;
using StreamMark.RaTeX;
using Xunit;

namespace StreamMark.Avalonia.Tests.MathRendering;

public sealed class RaTeXMathRendererTests
{
    [Fact]
    public void Layout_Fraction_HasSensibleMetrics()
    {
        var renderer = new RaTeXMathRenderer();
        var layout = renderer.Layout(@"\frac{a}{b}", display: true, out var error);
        Assert.Null(error);
        Assert.NotNull(layout);
        Assert.InRange(layout.Width, 0.3, 2.0);
        Assert.InRange(layout.Ascent, 0.5, 2.0);
        Assert.InRange(layout.Descent, 0.3, 2.0);

        var inline = renderer.Layout(@"\frac{a}{b}", display: false, out _);
        Assert.NotNull(inline);
        // 行内模式使用较小的 \textstyle 分式
        Assert.True(inline.Ascent + inline.Descent < layout.Ascent + layout.Descent);
    }

    [Fact]
    public void Layout_PlainSymbol_BaselineMetrics()
    {
        var layout = new RaTeXMathRenderer().Layout("x", false, out var error);
        Assert.Null(error);
        Assert.NotNull(layout);
        Assert.InRange(layout.Width, 0.4, 0.7);
        Assert.InRange(layout.Ascent, 0.35, 0.6);
        Assert.InRange(layout.Descent, -0.01, 0.05);
    }

    [Theory]
    [InlineData(@"\frac{a}{")]
    [InlineData(@"\sqrt{x")]
    [InlineData(@"\begin{pmatrix} 1 & 2")]
    [InlineData(@"\left( x")]
    public void Layout_IncompleteInput_ReturnsError(string latex)
    {
        var layout = new RaTeXMathRenderer().Layout(latex, true, out var error);
        Assert.Null(layout);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Layout_Cache_ReturnsSameInstance_AndIsBounded()
    {
        var renderer = new RaTeXMathRenderer(capacity: 4);
        var first = renderer.Layout(@"x^2", false, out _);
        var second = renderer.Layout(@"x^2", false, out _);
        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.NotSame(first, renderer.Layout(@"x^2", true, out _));

        renderer.Layout(@"\frac{1}{", false, out var e1);
        renderer.Layout(@"\frac{1}{", false, out var e2);
        Assert.NotNull(e1);
        Assert.Equal(e1, e2);

        for (var i = 0; i < 10; i++)
        {
            renderer.Layout($"y_{i}", false, out _);
        }
        Assert.Equal(4, renderer.CacheCount);
        Assert.NotSame(first, renderer.Layout(@"x^2", false, out _));
    }

    [Fact]
    public async Task Layout_IsThreadSafe()
    {
        var renderer = new RaTeXMathRenderer(capacity: 16);
        var tasks = Enumerable.Range(0, 8).Select(t => Task.Run(() =>
        {
            for (var i = 0; i < 200; i++)
            {
                var latex = $@"\frac{{{i % 20}}}{{{t}}}";
                Assert.NotNull(renderer.Layout(latex, (i & 1) == 0, out var error));
                Assert.Null(error);
                Assert.Null(renderer.Layout(@"\frac{", false, out error));
                Assert.NotNull(error);
            }
        }));
        await Task.WhenAll(tasks);
        Assert.True(renderer.CacheCount <= 16);
    }

    [Fact]
    public void Layout_ExplicitColor_IsPreserved_DefaultUsesSentinel()
    {
        var layout = Assert.IsType<RaTeXMathLayout>(new RaTeXMathRenderer().Layout(@"{\color{red} x} + y", false, out _));
        var glyphs = layout.DisplayList.Items.Where(i => i.Type == "GlyphPath").ToList();
        Assert.Contains(glyphs, g => g.Color is { R: > 0.99f, G: < 0.01f, B: < 0.01f });
        Assert.Contains(glyphs, g => g.Color is { } c && Math.Abs(c.R - 0.0123f) < 1e-4);
    }

    [Fact]
    public void Layout_Chinese_ReportsFallbackFont()
    {
        var layout = Assert.IsType<RaTeXMathLayout>(new RaTeXMathRenderer().Layout(@"\text{中文}", false, out var error));
        Assert.Null(error);
        var glyphs = layout.DisplayList.Items.Where(i => i.Type == "GlyphPath").ToList();
        Assert.Equal(2, glyphs.Count);
        Assert.All(glyphs, g => Assert.StartsWith("CJK", g.Font));
    }

    public static readonly string[] Formulas =
    [
        @"\frac{a+b}{c-d} + \dfrac{1}{2} = \tfrac{3}{4}",
        @"\sum_{i=1}^{n} i^2 = \frac{n(n+1)(2n+1)}{6}",
        @"\int_0^\infty e^{-x^2}\,dx = \frac{\sqrt{\pi}}{2} \quad \oint_C \vec F \cdot d\vec r",
        @"\begin{pmatrix} 1 & 2 & 3 \\ 4 & 5 & 6 \end{pmatrix} \begin{bmatrix} a & b \\ c & d \end{bmatrix} \begin{vmatrix} x & y \\ z & w \end{vmatrix}",
        @"f(x) = \begin{cases} x^2 & \text{if } x \ge 0 \\ -x & \text{otherwise} \end{cases}",
        @"\begin{aligned} a &= b + c \\ d + e &= \lim_{h \to 0} \frac{f(x+h) - f(x)}{h} \end{aligned}",
        @"\ce{2H2 + O2 -> 2H2O} \qquad \ce{SO4^2- + Ba^2+ -> BaSO4 v}",
        @"\text{中文公式：} E = mc^2 \text{（质能方程）}",
        @"{\color{#ff6b6b}\alpha} + \textcolor{#4ea1ff}{\beta} = \colorbox{#2f5d2f}{box} + \fcolorbox{#ffcc00}{#333333}{$\gamma$} + \delta",
        @"\sqrt[3]{x^3 + y^3} + \sqrt{\frac{a}{b}} + \sqrt{2}",
        @"\left( \frac{a}{b} \right)^2 \Bigg[ \bigg( x \bigg) \Bigg] \left\{ \sum_k \right\} \left\| v \right\|",
        @"\overbrace{a+b+c}^{n} \underbrace{x+y}_{2} \widehat{abc} \widetilde{xyz} \cancel{x} \boxed{E=mc^2} \oiint_S \sqrt{\dfrac{1}{\dfrac{2}{3}}}",
        @"\left[\begin{array}{c:c} 1 & 2 \\ \hdashline 3 & 4 \end{array}\right] \xleftrightarrow{f} \overleftarrow{AB} \underline{u} \text{😀 Ünïcødé}",
        @"\mathbb{R}^n,\ \mathcal{L},\ \mathbf{v},\ \mathfrak{g},\ \mathrm{d}x,\ \hat{x}\ \overline{AB}\ \overrightarrow{PQ}\ \xrightarrow[below]{above}",
    ];

    [Fact]
    public async Task Snapshot_FormulaGallery()
    {
        await HeadlessApp.Session.Dispatch(() =>
        {
            var renderer = new RaTeXMathRenderer();
            foreach (var (fontSize, name) in new[] { (16d, "math-gallery-16"), (28d, "math-gallery-28") })
            {
                var layouts = Formulas.Select(f =>
                {
                    var layout = renderer.Layout(f, display: true, out var error);
                    Assert.True(layout is not null, $"{f}: {error}");
                    return layout;
                }).ToList();
                var control = new MathGallery(layouts, fontSize);
                var window = new Window
                {
                    Width = Math.Ceiling(control.DesiredWidth),
                    Height = Math.Ceiling(control.DesiredHeight),
                    Background = new SolidColorBrush(Color.FromRgb(0x1e, 0x1f, 0x22)),
                    Content = control,
                };
                window.Show();
                HeadlessApp.Save(window, name);
                window.Close();
            }
            return Task.CompletedTask;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Snapshot_InlineBaselineWithText()
    {
        await HeadlessApp.Session.Dispatch(() =>
        {
            var renderer = new RaTeXMathRenderer();
            var items = new[] { @"x^2", @"\frac{1}{2}", @"\sqrt{a}", @"\sum_i a_i", @"\text{中}y", @"\int f" }
                .Select(f => renderer.Layout(f, false, out _)!).ToList();
            var control = new InlineRow(items, 20);
            var window = new Window
            {
                Width = 520,
                Height = 80,
                Background = new SolidColorBrush(Color.FromRgb(0x1e, 0x1f, 0x22)),
                Content = control,
            };
            window.Show();
            HeadlessApp.Save(window, "math-inline-baseline");
            window.Close();
            return Task.CompletedTask;
        }, CancellationToken.None);
    }

    /// <summary>逐行绘制公式，并画出暗色基线参考线以检查基线位置。</summary>
    private sealed class MathGallery(IReadOnlyList<MathLayout> layouts, double fontSize) : Control
    {
        private const double Pad = 12;
        private static readonly IBrush Foreground = new ImmutableSolidColorBrush(Color.FromRgb(0xe6, 0xe6, 0xe6));
        private static readonly IBrush Guide = new ImmutableSolidColorBrush(Color.FromArgb(0x50, 0x80, 0xff, 0x80));

        public double DesiredWidth => layouts.Max(l => l.Width) * fontSize + 2 * Pad;

        public double DesiredHeight => layouts.Sum(l => (l.Ascent + l.Descent) * fontSize + Pad) + Pad;

        public override void Render(DrawingContext context)
        {
            var y = Pad;
            foreach (var layout in layouts)
            {
                var baseline = y + layout.Ascent * fontSize;
                context.DrawRectangle(Guide, null, new Rect(0, baseline, Bounds.Width, 1));
                layout.Draw(context, new Point(Pad, y), fontSize, Foreground);
                y += (layout.Ascent + layout.Descent) * fontSize + Pad;
            }
        }
    }

    /// <summary>与普通文本混排：所有公式按共同基线对齐。</summary>
    private sealed class InlineRow(IReadOnlyList<MathLayout> layouts, double fontSize) : Control
    {
        private static readonly IBrush Foreground = new ImmutableSolidColorBrush(Color.FromRgb(0xe6, 0xe6, 0xe6));

        public override void Render(DrawingContext context)
        {
            var baseline = 50.0;
            var x = 10.0;
            var typeface = new Typeface(FontFamily.Default);
            context.DrawRectangle(new ImmutableSolidColorBrush(Color.FromArgb(0x50, 0x80, 0xff, 0x80)), null, new Rect(0, baseline, Bounds.Width, 1));
            foreach (var layout in layouts)
            {
                var text = new FormattedText("ab", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, Foreground);
                context.DrawText(text, new Point(x, baseline - text.Baseline));
                x += text.Width + 4;
                layout.Draw(context, new Point(x, baseline - layout.Ascent * fontSize), fontSize, Foreground);
                x += layout.Width * fontSize + 12;
            }
        }
    }
}
