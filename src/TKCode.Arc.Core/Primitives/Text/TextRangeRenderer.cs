using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc.Primitives;

/// <summary>
/// Draws <see cref="TextRangeDecoration"/>s on a <see cref="TextLayout"/> without relayout: per-line rounded
/// backgrounds under the glyphs (<see cref="DrawBackgrounds"/>) and strikethroughs / underlines over them
/// (<see cref="DrawLines"/>). Anything that owns a layout and draws it at an origin can use it; a
/// <c>TextBlock</c> subclass calls both around the text in <c>RenderTextLayout</c>, as
/// <see cref="DecoratedTextBlock"/> does. <c>Run.Background</c> cannot do this: it has no radius, no outset, is not
/// repeated per wrapped line and is not animatable.
/// </summary>
/// <remarks>
/// A range's rectangles come from <see cref="TextLayout.HitTestTextRange"/>; the pieces of one visual line are merged
/// into one rectangle, whitespace hanging at the end of a wrapped left-to-right line is left out (as CSS removes it),
/// then the <see cref="TextRangeExtent"/> sets the height: the line box, or the text box (baseline minus the line's
/// largest ascent to baseline plus its largest descent, from the font metrics of the line's text runs; a line with
/// only embedded controls uses its line box).
/// </remarks>
public static class TextRangeRenderer
{
    /// <summary>Pieces narrower than this are dropped (a hard line break, a collapsed space).</summary>
    private const double MinWidth = 0.01;

    /// <summary>Fallback strikethrough height when a font reports none: this share of the ascent above the baseline.</summary>
    private const double StrikeAscentShare = 0.3;

    /// <summary>
    /// The rectangles of <paramref name="length"/> characters from <paramref name="start"/>: one per visual line,
    /// merged within the line, in layout coordinates, with <paramref name="extent"/> applied (no outset). Ranges
    /// outside the text are clipped; an empty or invalid range returns no rectangles.
    /// </summary>
    /// <param name="layout">The laid out text.</param>
    /// <param name="start">First character index.</param>
    /// <param name="length">Number of characters.</param>
    /// <param name="extent">Vertical extent of each rectangle.</param>
    public static IReadOnlyList<Rect> GetRects(TextLayout layout, int start, int length, TextRangeExtent extent = TextRangeExtent.Text)
    {
        ArgumentNullException.ThrowIfNull(layout);
        TextRangeLine[] lines = [];
        var count = Measure(layout, start, length, extent, ref lines);
        if (count == 0)
        {
            return [];
        }
        var rects = new Rect[count];
        for (var i = 0; i < count; i++)
        {
            rects[i] = lines[i].Box;
        }
        return rects;
    }

    /// <summary>
    /// Draws the backgrounds of <paramref name="decorations"/> in order, each line's rectangle grown by the
    /// decoration's outset and rounded by its radius. Call before drawing the text.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="layout">The layout the decorations index into.</param>
    /// <param name="decorations">The decorations; those without a background or with opacity 0 are skipped.</param>
    /// <param name="origin">Where the layout is drawn (the same origin passed to <see cref="TextLayout.Draw"/>).</param>
    public static void DrawBackgrounds(DrawingContext context, TextLayout layout, IReadOnlyList<TextRangeDecoration> decorations, Point origin)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(decorations);
        for (var i = 0; i < decorations.Count; i++)
        {
            var decoration = decorations[i];
            var opacity = decoration.Opacity;
            if (decoration.Background is not { } brush || !(opacity > 0))
            {
                continue;
            }
            var lines = decoration.GetLines(layout);
            if (lines.IsEmpty)
            {
                continue;
            }
            using (opacity < 1 ? context.PushOpacity(opacity) : default)
            {
                foreach (var line in lines)
                {
                    if (TryGetBackground(decoration, line, origin, out var rounded))
                    {
                        context.DrawRectangle(brush, null, rounded);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Draws the strikethroughs and underlines of <paramref name="decorations"/> across each line's text (the outset
    /// does not widen them). Call after drawing the text.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="layout">The layout the decorations index into.</param>
    /// <param name="decorations">The decorations; those without pens or with opacity 0 are skipped.</param>
    /// <param name="origin">Where the layout is drawn (the same origin passed to <see cref="TextLayout.Draw"/>).</param>
    public static void DrawLines(DrawingContext context, TextLayout layout, IReadOnlyList<TextRangeDecoration> decorations, Point origin)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(decorations);
        for (var i = 0; i < decorations.Count; i++)
        {
            var decoration = decorations[i];
            var opacity = decoration.Opacity;
            var strike = decoration.StrikeThrough;
            var underline = decoration.Underline;
            if ((strike is null && underline is null) || !(opacity > 0))
            {
                continue;
            }
            var lines = decoration.GetLines(layout);
            if (lines.IsEmpty)
            {
                continue;
            }
            using (opacity < 1 ? context.PushOpacity(opacity) : default)
            {
                foreach (var line in lines)
                {
                    var left = origin.X + line.Box.Left;
                    var right = origin.X + line.Box.Right;
                    if (strike is not null)
                    {
                        var y = origin.Y + line.StrikeY;
                        context.DrawLine(strike, new Point(left, y), new Point(right, y));
                    }
                    if (underline is not null)
                    {
                        var y = origin.Y + line.Baseline + decoration.UnderlineOffset;
                        context.DrawLine(underline, new Point(left, y), new Point(right, y));
                    }
                }
            }
        }
    }

    /// <summary>
    /// The rounded rectangle one line's background is drawn with: the line's box grown by the decoration's outset and
    /// moved to <paramref name="origin"/>, its radius clamped to half the shorter side (a negative or NaN radius is a
    /// square corner). False when the outset leaves nothing to draw.
    /// </summary>
    internal static bool TryGetBackground(TextRangeDecoration decoration, in TextRangeLine line, Point origin, out RoundedRect rounded)
    {
        var box = line.Box;
        var outset = decoration.Outset;
        var width = box.Width + outset.Left + outset.Right;
        var height = box.Height + outset.Top + outset.Bottom;
        if (!(width > 0) || !(height > 0))
        {
            rounded = default;
            return false;
        }
        var requested = decoration.CornerRadius;
        var radius = double.IsNaN(requested) ? 0 : Math.Max(0, Math.Min(requested, Math.Min(width, height) / 2));
        rounded = new RoundedRect(new Rect(origin.X + box.X - outset.Left, origin.Y + box.Y - outset.Top, width, height), radius);
        return true;
    }

    /// <summary>
    /// Measures a range into <paramref name="buffer"/> (grown when needed) and returns the number of lines written.
    /// Never throws for ranges outside the text.
    /// </summary>
    internal static int Measure(TextLayout layout, int start, int length, TextRangeExtent extent, ref TextRangeLine[] buffer)
    {
        var lines = layout.TextLines;
        if (lines.Count == 0 || length <= 0)
        {
            return 0;
        }
        var last = lines[lines.Count - 1];
        var textLength = last.FirstTextSourceIndex + last.Length;
        var from = Math.Max(0L, start);
        var to = Math.Min(textLength, (long)start + length);
        if (to <= from)
        {
            return 0;
        }
        return MeasureLines(lines, layout.HitTestTextRange((int)from, (int)(to - from)), extent, ref buffer);
    }

    /// <summary>
    /// Assigns hit-test rectangles to their lines (a rectangle's top is its line's top), merges each line's pieces and
    /// applies the extent. The lines and rectangles can come from any layout that stacks <see cref="TextLine"/>s.
    /// </summary>
    private static int MeasureLines(IReadOnlyList<TextLine> lines, IEnumerable<Rect> hits, TextRangeExtent extent, ref TextRangeLine[] buffer)
    {
        var count = 0;
        var lineIndex = 0;
        var lineTop = 0.0;
        var current = -1;
        var currentTop = 0.0;
        var left = 0.0;
        var right = 0.0;
        foreach (var hit in hits)
        {
            // Rectangles arrive in line order; move on while the next line starts at or above this one's top.
            while (lineIndex + 1 < lines.Count && lineTop + lines[lineIndex].Height <= hit.Y + 0.01)
            {
                lineTop += lines[lineIndex].Height;
                lineIndex++;
            }
            if (lineIndex == current)
            {
                left = Math.Min(left, hit.Left);
                right = Math.Max(right, hit.Right);
                continue;
            }
            if (current >= 0)
            {
                Append(lines[current], currentTop, left, right, extent, ref buffer, ref count);
            }
            current = lineIndex;
            currentTop = lineTop;
            left = hit.Left;
            right = hit.Right;
        }
        if (current >= 0)
        {
            Append(lines[current], currentTop, left, right, extent, ref buffer, ref count);
        }
        return count;
    }

    private static void Append(TextLine line, double top, double left, double right, TextRangeExtent extent,
                               ref TextRangeLine[] buffer, ref int count)
    {
        // Whitespace hanging past the end of a wrapped line is not part of the decoration (CSS removes it too).
        var contentRight = line.Start + line.Width;
        if (right > contentRight && line.WidthIncludingTrailingWhitespace > line.Width && IsLeftToRight(line))
        {
            right = Math.Max(left, contentRight);
        }
        if (right - left < MinWidth)
        {
            return;
        }
        var baseline = top + line.Baseline;
        var boxTop = top;
        var boxBottom = top + line.Height;
        var strikeY = top + line.Height / 2;
        if (TryGetTextMetrics(line, out var ascent, out var descent, out var strike))
        {
            strikeY = baseline + strike;
            if (extent == TextRangeExtent.Text)
            {
                boxTop = baseline + ascent;
                boxBottom = baseline + descent;
            }
        }
        if (count == buffer.Length)
        {
            Array.Resize(ref buffer, Math.Max(4, buffer.Length * 2));
        }
        buffer[count++] = new TextRangeLine(new Rect(left, boxTop, right - left, Math.Max(0, boxBottom - boxTop)), baseline, strikeY);
    }

    /// <summary>
    /// The line's largest ascent (negative, above the baseline) and descent over its text runs, and the strikethrough
    /// offset from the baseline of the run with the largest ascent. False when the line has no text run.
    /// </summary>
    private static bool TryGetTextMetrics(TextLine line, out double ascent, out double descent, out double strike)
    {
        ascent = 0;
        descent = 0;
        strike = 0;
        var found = false;
        var runs = line.TextRuns;
        for (var i = 0; i < runs.Count; i++)
        {
            if (runs[i] is not ShapedTextRun shaped)
            {
                continue;
            }
            var metrics = shaped.TextMetrics;
            if (!found || metrics.Ascent < ascent)
            {
                ascent = metrics.Ascent;
                strike = metrics.StrikethroughPosition < 0 ? metrics.StrikethroughPosition : metrics.Ascent * StrikeAscentShare;
            }
            if (!found || metrics.Descent > descent)
            {
                descent = metrics.Descent;
            }
            found = true;
        }
        return found;
    }

    /// <summary>True when no text run on the line is right-to-left (only then does trailing whitespace hang on the right).</summary>
    private static bool IsLeftToRight(TextLine line)
    {
        var runs = line.TextRuns;
        for (var i = 0; i < runs.Count; i++)
        {
            if (runs[i] is ShapedTextRun { BidiLevel: var level } && (level & 1) == 1)
            {
                return false;
            }
        }
        return true;
    }
}
