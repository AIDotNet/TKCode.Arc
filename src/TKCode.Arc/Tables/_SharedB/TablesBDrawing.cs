using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>Drawing helpers shared by the self-drawn parts of SortableDataTable, ReorderableList and FilterToolbar.</summary>
internal static class TablesBDrawing
{
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
