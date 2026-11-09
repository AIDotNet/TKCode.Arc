using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>Drawing helpers shared by the self-drawn parts of Table, TreeTable, ReorderableList and FilterToolbar.</summary>
internal static class TablesBDrawing
{
    /// <summary>
    /// A cached-and-drawn label: the <see cref="TextLayout"/> twin of a <c>FormattedText</c> with <c>MaxTextWidth</c>,
    /// <c>Trimming = CharacterEllipsis</c> and <c>MaxLineCount</c> (<paramref name="maxLines"/> 0 = unlimited). A
    /// <c>FormattedText</c> re-shapes its text on every <c>Draw</c>, so caching one did not spare the hover and sort tweens
    /// that redraw every frame; a layout keeps its lines and only emits glyphs. Line breaking matches <c>FormattedText</c>
    /// (measured against it) for a single line (the whole text trimmed with the ellipsis, no wrapping) and for unlimited
    /// lines (words wrap, a long word overflows); a capped multi-line text needs <see cref="TablesBText"/>.
    /// </summary>
    public static TextLayout Layout(string text, Typeface typeface, double fontSize, IBrush? brush, double maxWidth, int maxLines,
                                    TextAlignment alignment = TextAlignment.Left, FontFeatureCollection? features = null)
        => new(text, typeface, fontSize, brush, alignment, maxLines == 1 ? TextWrapping.NoWrap : TextWrapping.WrapWithOverflow,
            TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, Math.Max(1, maxWidth), double.PositiveInfinity, double.NaN,
            0, maxLines, features);
    public static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)Math.Round(a.A + (b.A - a.A) * t), (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
    }

    public static Color ColorOf(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid ? solid.Color : fallback;

    /// <summary>Returns a cached pen for a 24x24 Lucide glyph drawn at <paramref name="size"/> with a stroke in CSS pixels.</summary>
    public static ImmutablePen GlyphPen(ref ImmutablePen? cache, ref Color cachedColor, Color color, double stroke, double size)
    {
        if (cache is null || cachedColor != color)
        {
            cachedColor = color;
            cache = new ImmutablePen(new ImmutableSolidColorBrush(color), stroke * 24 / size, lineCap: PenLineCap.Round,
                lineJoin: PenLineJoin.Round);
        }
        return cache;
    }

    /// <summary>Draws a 24x24 stroked glyph centered on <paramref name="center"/> at <paramref name="size"/> px.</summary>
    public static void DrawGlyph(DrawingContext context, Geometry glyph, IPen pen, Point center, double size)
    {
        var factor = size / 24;
        using (context.PushTransform(Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(factor, factor) *
                                     Matrix.CreateTranslation(center.X, center.Y)))
        {
            context.DrawGeometry(null, pen, glyph);
        }
    }
}
