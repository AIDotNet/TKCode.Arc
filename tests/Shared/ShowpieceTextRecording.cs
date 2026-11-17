using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary>Records what a drawing does (glyph runs with their transform and brush) through a <see cref="DrawingGroup"/>.</summary>
internal static class ShowpieceTextRecording
{
    public readonly record struct Drawn(Matrix Transform, IBrush? Brush, GlyphRun Run);

    public static List<Drawn> Record(Action<DrawingContext> draw)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            draw(context);
        }
        var drawn = new List<Drawn>();
        Walk(group, Matrix.Identity, drawn);
        return drawn;
    }

    /// <summary>Each glyph run as text (position, size, bidi level, glyph indices and advances) for comparing layouts built apart.</summary>
    public static List<string> Glyphs(Action<DrawingContext> draw) =>
        Record(draw).Select(d =>
            FormattableString.Invariant($"{d.Transform.M31:0.###},{d.Transform.M32:0.###} {d.Run.FontRenderingEmSize} L{d.Run.BiDiLevel} ") +
            string.Join(' ', d.Run.GlyphInfos.Select(g => FormattableString.Invariant($"{g.GlyphIndex}:{g.GlyphAdvance:0.###}"))))
            .ToList();

    private static void Walk(Drawing drawing, Matrix transform, List<Drawn> drawn)
    {
        switch (drawing)
        {
            case DrawingGroup group:
                var inner = group.Transform is { } t ? t.Value * transform : transform;
                foreach (var child in group.Children)
                {
                    Walk(child, inner, drawn);
                }
                break;
            case GlyphRunDrawing glyphs when glyphs.GlyphRun is not null:
                drawn.Add(new Drawn(transform, glyphs.Foreground, glyphs.GlyphRun));
                break;
        }
    }
}
