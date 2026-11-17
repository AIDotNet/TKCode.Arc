using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Terminal.Core;

namespace TKCode.Terminal.Avalonia;

/// <summary>
/// Underline (single, double, curly, dotted, dashed), strikethrough and overline (05-rendering.md §8.5). Cells with the same
/// decoration and color are merged into one span drawn as one rectangle or path, so curly and dotted lines run on across
/// cells; their phase comes from the absolute x, so they also continue across spans. Underline and overline are drawn under
/// the glyphs, strikethrough over them; invisible cells have none.
/// </summary>
public sealed partial class TerminalView
{
    private readonly record struct DecorationKey(DecorationKind Kind, IImmutableSolidColorBrush Brush);

    private void BuildDecorations(RowSnapshot row, RowRuns runs, CellMetrics m, double pad)
    {
        var cells = row.Cells;
        for (var pass = 0; pass < 3; pass++)
        {
            for (var x = 0; x < cells.Length;)
            {
                var key = DecorationAt(row, x, pass);
                var end = x + 1;
                while (end < cells.Length && DecorationAt(row, end, pass) == key)
                {
                    end++;
                }
                if (key is { } k)
                {
                    var target = k.Kind == DecorationKind.Strikethrough ? runs.Over : runs.Under;
                    target.Add(CreateDecoration(k.Kind, x, end, k.Brush, m, pad));
                }
                x = end;
            }
        }
    }

    /// <summary>The decoration of one cell for a pass (0 underline, 1 strikethrough, 2 overline), or <c>null</c>.</summary>
    private DecorationKey? DecorationAt(RowSnapshot row, int x, int pass)
    {
        var style = row.Styles[TCell.StyleIndex(row.Cells[x])];
        if (style.Flags.HasFlag(StyleFlags.Invisible))
        {
            return null;
        }
        var fg = _cellForegrounds[x];
        switch (pass)
        {
            case 0:
                if (style.Underline == UnderlineStyle.None)
                {
                    return null;
                }
                var color = style.UnderlineColor.IsDefault ? fg : _runsPalette!.Resolve(style.UnderlineColor, foreground: true);
                var kind = style.Underline switch
                {
                    UnderlineStyle.Double => DecorationKind.DoubleUnderline,
                    UnderlineStyle.Curly => DecorationKind.CurlyUnderline,
                    UnderlineStyle.Dotted => DecorationKind.DottedUnderline,
                    UnderlineStyle.Dashed => DecorationKind.DashedUnderline,
                    _ => DecorationKind.Underline,
                };
                return new DecorationKey(kind, Brush(color));
            case 1:
                return style.Flags.HasFlag(StyleFlags.Strikethrough) ? new DecorationKey(DecorationKind.Strikethrough, Brush(fg)) : null;
            default:
                return style.Flags.HasFlag(StyleFlags.Overline) ? new DecorationKey(DecorationKind.Overline, Brush(fg)) : null;
        }
    }

    /// <summary>The geometry of a decoration over columns [<paramref name="start"/>, <paramref name="end"/>), in DIP relative to the row's top.</summary>
    internal static Decoration CreateDecoration(DecorationKind kind, int start, int end, IImmutableSolidColorBrush brush, CellMetrics m, double pad)
    {
        var s = m.Scaling;
        var t = Math.Max(1, m.UnderlineThickness);
        var x0 = pad + start * m.CellWidth;
        var x1 = pad + end * m.CellWidth;
        Rect Px(double x, double y, double w, double h) => new(x / s, y / s, w / s, h / s);
        var bounds = Px(x0, m.UnderlinePosition, x1 - x0, t);

        switch (kind)
        {
            case DecorationKind.Strikethrough:
                return new Decoration(kind, start, end, brush, Px(x0, m.StrikethroughPosition, x1 - x0, Math.Max(1, m.StrikethroughThickness)), null, 0);
            case DecorationKind.Overline:
                return new Decoration(kind, start, end, brush, Px(x0, 0, x1 - x0, t), null, 0);
            case DecorationKind.DoubleUnderline:
            {
                // Two lines a thickness apart, moved up together when the second would leave the cell.
                var y1 = m.UnderlinePosition;
                var y2 = y1 + 2 * t;
                var overflow = y2 + t - m.CellHeight;
                if (overflow > 0)
                {
                    y1 = Math.Max(0, y1 - overflow);
                    y2 = Math.Max(y1 + t, y2 - overflow);
                }
                var geometry = new StreamGeometry();
                using (var c = geometry.Open())
                {
                    AddRect(c, Px(x0, y1, x1 - x0, t));
                    AddRect(c, Px(x0, y2, x1 - x0, t));
                }
                return new Decoration(kind, start, end, brush, Px(x0, y1, x1 - x0, y2 + t - y1), geometry, 0);
            }
            case DecorationKind.CurlyUnderline:
            {
                // A sine wave with a period of one cell; its phase comes from the absolute x, so spans continue each other.
                var amplitude = Math.Max(1, Math.Round(m.CellWidth / (2 * Math.PI)));
                var center = Math.Min(m.UnderlinePosition + t / 2.0 + amplitude, m.CellHeight - amplitude - t / 2.0);
                double Y(double x) => center - amplitude * Math.Sin(2 * Math.PI * (x - pad) / m.CellWidth);
                var geometry = new StreamGeometry();
                var first = new Point(x0 / s, Y(x0) / s);
                var last = new Point(x1 / s, Y(x1) / s);
                using (var c = geometry.Open())
                {
                    c.BeginFigure(first, false);
                    var step = Math.Max(1.0, m.CellWidth / 8.0);
                    for (var x = x0 + step; x < x1; x += step)
                    {
                        c.LineTo(new Point(x / s, Y(x) / s));
                    }
                    c.LineTo(last);
                    c.EndFigure(false);
                }
                var top = Math.Max(0, center - amplitude - t / 2.0);
                return new Decoration(kind, start, end, brush, Px(x0, top, x1 - x0, 2 * amplitude + t), geometry, t / s) { Start = first, End = last };
            }
            case DecorationKind.DottedUnderline:
            {
                // Dots of the line's thickness every two thicknesses, aligned to the absolute x.
                var geometry = new StreamGeometry();
                using (var c = geometry.Open())
                {
                    var pitch = 2 * t;
                    var k = (long)Math.Ceiling((x0 - pad) / pitch);
                    for (var x = pad + k * pitch; x + t <= x1; x += pitch)
                    {
                        AddRect(c, Px(x, m.UnderlinePosition, t, t));
                    }
                }
                return new Decoration(kind, start, end, brush, bounds, geometry, 0);
            }
            case DecorationKind.DashedUnderline:
            {
                // One centered dash of half a cell per cell, so the gaps are even across cells.
                var geometry = new StreamGeometry();
                var dash = Math.Max(1, (int)Math.Round(m.CellWidth / 2.0));
                var lead = (m.CellWidth - dash) / 2;
                using (var c = geometry.Open())
                {
                    for (var col = start; col < end; col++)
                    {
                        AddRect(c, Px(pad + col * m.CellWidth + lead, m.UnderlinePosition, dash, t));
                    }
                }
                return new Decoration(kind, start, end, brush, bounds, geometry, 0);
            }
            default:
                return new Decoration(kind, start, end, brush, bounds, null, 0);
        }
    }

    private static void AddRect(StreamGeometryContext c, Rect r)
    {
        c.BeginFigure(r.TopLeft, true);
        c.LineTo(r.TopRight);
        c.LineTo(r.BottomRight);
        c.LineTo(r.BottomLeft);
        c.EndFigure(true);
    }

    /// <summary>
    /// <see cref="HoveredLink"/>: a single underline over the link's cells, or a double one where the text is already
    /// underlined (Ghostty), in <see cref="LinkForeground"/> or each cell's foreground.
    /// </summary>
    private void DrawHoveredLink(DrawingContext context, FrameSnapshot frame, TerminalPalette palette, CellMetrics m, double pad)
    {
        if (HoveredLink is not { Count: > 0 } spans)
        {
            return;
        }
        var linkColor = LinkForeground is { } c ? new TerminalRgb(c.R, c.G, c.B) : (TerminalRgb?)null;
        foreach (var span in spans)
        {
            if (span.Row < 0 || span.Row >= frame.Rows.Length)
            {
                continue;
            }
            var row = frame.Rows[span.Row];
            var last = Math.Min(span.EndColumn, row.Cells.Length - 1);
            var rowY = (pad + span.Row * m.CellHeight) / m.Scaling;
            for (var x = Math.Max(0, span.StartColumn); x <= last;)
            {
                var (kind, color) = LinkDecorationAt(row, x, palette, linkColor);
                var end = x + 1;
                while (end <= last && LinkDecorationAt(row, end, palette, linkColor) == (kind, color))
                {
                    end++;
                }
                var d = CreateDecoration(kind, x, end, Brush(color), m, pad);
                using (context.PushTransform(Matrix.CreateTranslation(0, rowY)))
                {
                    DrawDecoration(context, d);
                }
                x = end;
            }
        }
    }

    private (DecorationKind Kind, TerminalRgb Color) LinkDecorationAt(RowSnapshot row, int x, TerminalPalette palette, TerminalRgb? linkColor)
    {
        var cell = row.Cells[x];
        var style = row.Styles[TCell.StyleIndex(cell)];
        var kind = style.Underline == UnderlineStyle.None ? DecorationKind.Underline : DecorationKind.DoubleUnderline;
        return (kind, linkColor ?? ResolveCellColors(row, cell, palette, contrastExempt: false).Foreground);
    }
}
