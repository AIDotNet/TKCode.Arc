using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// Text that self-drawn labels paint every frame (SplitButtonLabel, ActionSwapSurface, TextMorph, CardStatusLine): shaped
/// once into a <see cref="TextLayout"/> and drawn as glyph runs in the brush of the moment. Avalonia 12's
/// <c>FormattedText.Draw</c> re-shapes on every call (about 3 KB and 0.3 ms per Latin label, far more for CJK), so a
/// cached FormattedText saved nothing; a TextLayout keeps its lines, and drawing its runs with the caller's brush means a
/// foreground change (a theme transition) needs no new layout either. <see cref="Draw"/> makes the calls
/// <see cref="TextLayout.Draw"/> makes, so the pixels are those of the former <c>DrawText</c>. Mirrors Core's RollingDigits.
/// </summary>
internal static class ShapedText
{
    /// <summary>Shapes <paramref name="text"/> on one unbounded line (what a FormattedText with no MaxTextWidth drew).</summary>
    public static TextLayout Shape(string text, Typeface typeface, double fontSize) => new(text, typeface, fontSize, Brushes.Black);

    /// <summary>
    /// Shapes <paramref name="text"/> as a FormattedText with <c>MaxTextWidth</c>, <c>MaxLineCount = 1</c> and
    /// <c>CharacterEllipsis</c> trimming did: wrapped at <paramref name="maxWidth"/> and collapsed to one line.
    /// </summary>
    public static TextLayout ShapeTrimmed(string text, Typeface typeface, double fontSize, double maxWidth) =>
        new(text, typeface, fontSize, Brushes.Black, textWrapping: TextWrapping.Wrap, textTrimming: TextTrimming.CharacterEllipsis,
            maxWidth: maxWidth, maxLines: 1);

    /// <summary>Draws <paramref name="layout"/> with its top left at <paramref name="origin"/>, its glyph runs in <paramref name="brush"/>.</summary>
    public static void Draw(DrawingContext context, TextLayout layout, IBrush brush, Point origin)
    {
        var y = origin.Y;
        var lines = layout.TextLines;
        for (var l = 0; l < lines.Count; l++)
        {
            var line = lines[l];
            var x = origin.X + line.Start;
            var runs = line.TextRuns;
            for (var r = 0; r < runs.Count; r++)
            {
                if (runs[r] is not DrawableTextRun drawable)
                {
                    continue;
                }
                if (drawable is ShapedTextRun shaped && shaped.GlyphRun.GlyphInfos.Count > 0)
                {
                    using (context.PushTransform(Matrix.CreateTranslation(x, y + (line.Baseline - shaped.Baseline))))
                    {
                        context.DrawGlyphRun(brush, shaped.GlyphRun);
                    }
                }
                x += drawable.Size.Width;
            }
            y += line.Height;
        }
    }
}
