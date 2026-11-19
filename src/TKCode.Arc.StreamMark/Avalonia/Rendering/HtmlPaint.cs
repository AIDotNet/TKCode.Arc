using Avalonia.Media;
using Avalonia.Media.Immutable;
using StreamMark.Avalonia.Extensions;
using StreamMark.Core.Model;

namespace StreamMark.Avalonia.Rendering;

/// <summary>HTML 作者颜色到画刷的转换（按 <see cref="MarkdownServices.HtmlColors"/> 处理可读性）。</summary>
internal static class HtmlPaint
{
    private static readonly Dictionary<CssColor, IBrush> s_brushes = [];

    public static IBrush Brush(CssColor color)
    {
        lock (s_brushes)
        {
            if (!s_brushes.TryGetValue(color, out var brush))
            {
                brush = new ImmutableSolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
                s_brushes[color] = brush;
            }
            return brush;
        }
    }

    /// <summary>文字颜色：Ignore 模式或对比度不足（Readable 模式）时返回 null，表示用主题颜色。</summary>
    public static IBrush? Foreground(RenderContext context, CssColor? color, CssColor? background = null)
    {
        if (color is not { } c || context.Services.HtmlColors == HtmlColorMode.Ignore)
        {
            return null;
        }
        if (context.Services.HtmlColors == HtmlColorMode.Readable)
        {
            var bg = background ?? context.HtmlBackground ?? ThemeBackground(context);
            if (Contrast(c, bg) < 2.2)
            {
                return null;
            }
        }
        return Brush(c);
    }

    public static IBrush? Background(RenderContext context, CssColor? color) =>
        color is { A: > 0 } c && context.Services.HtmlColors != HtmlColorMode.Ignore ? Brush(c) : null;

    // 没有 HTML 背景时按主题文字色推断背景：浅色文字说明是暗色背景
    private static CssColor ThemeBackground(RenderContext context)
    {
        if (context.Style.Foreground is ISolidColorBrush { Color: var fg } && Luminance(fg.R, fg.G, fg.B) > 0.5)
        {
            return new CssColor(255, 0x1E, 0x1E, 0x1E);
        }
        return new CssColor(255, 0xFF, 0xFF, 0xFF);
    }

    private static double Contrast(CssColor a, CssColor b)
    {
        var la = Luminance(a.R, a.G, a.B);
        var lb = Luminance(b.R, b.G, b.B);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(byte r, byte g, byte b)
    {
        static double Channel(byte v)
        {
            var c = v / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);
    }
}
