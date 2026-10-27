using Avalonia;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

internal sealed partial class HorizonPlot
{
    private StreamGeometry Geometry(int row, bool upper)
    {
        var cache = upper ? _upper : _lower;
        if (cache[row] is { } cached)
        {
            return cached;
        }
        // One polygon per row side in the unit box (x 0…days−1, y −v…0); the fold only changes the matrix and the alpha.
        var points = new Point[_days + 2];
        HorizonEngine.UnitPolygon(_values[row], _extent, upper, points);
        var geometry = AnalyticsGeometry.Polygon(points);
        cache[row] = geometry;
        return geometry;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        // The whole control takes pointer input, also where nothing is painted.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        EnsurePalette();
        if (!_palette.IsReady || Bounds.Width <= 0)
        {
            return;
        }
        var layout = CurrentLayout;
        var body = new Typeface(_palette.BodyFont);
        var m = layout.Metrics;
        if (_names.Length == 0 || _days == 0)
        {
            var empty = _text.GetTrimmed(_emptyText, body, 14, Math.Max(1, Bounds.Width - 32));
            empty.Draw(context, _palette.Brush(_palette.TextMuted),
                new Point((Bounds.Width - empty.Width) / 2, (Bounds.Height - empty.Height) / 2));
            DrawFocus(context);
            return;
        }

        var bands = _fold.Value;
        var alpha = HorizonEngine.QuantizeAlpha(HorizonEngine.LayerAlpha(bands));
        var upperBrush = _palette.Brush(_aboveColor ?? _palette.Series1, alpha);
        var lowerBrush = _palette.Brush(_belowColor ?? _palette.Series3, alpha);
        var surface = _palette.Brush(_palette.Surface);
        var muted = _palette.Brush(_palette.SurfaceMuted);
        var rule = _palette.Brush(_palette.BorderSubtle);
        var nameBrush = _palette.Brush(_palette.TextSecondary);
        var valueBrush = _palette.Brush(_palette.Foreground);
        var width = Bounds.Width;
        var radius = BandRadius;

        for (var slot = 0; slot < _rowAt.Length; slot++)
        {
            var row = _rowAt[slot];
            var y = layout.RowTop(slot) + _offset[row].Value;
            // Every row paints its own opaque ground, so a row sliding past another covers it in document order.
            context.FillRectangle(surface, new Rect(0, y, width, m.RowHeight));

            var name = _text.GetTrimmed(_names[row], body, m.FontSize, m.NameWidth);
            name.Draw(context, nameBrush, new Point(m.SidePadding, y + (m.RowHeight - name.Height) / 2));
            var value = _text.Get(_valueTexts[row], body, m.FontSize, tabular: true);
            value.Draw(context, valueBrush, new Point(layout.ValueRight - value.Width, y + (m.RowHeight - value.Height) / 2));

            var strip = new Rect(layout.PlotLeft, y + m.BandInset, layout.PlotWidth, m.RowHeight - 2 * m.BandInset);
            var round = new RoundedRect(strip, radius);
            context.DrawRectangle(muted, null, round);
            using (context.PushClip(round))
            {
                if (_hasLower[row])
                {
                    DrawSide(context, Geometry(row, false), lowerBrush, strip, bands);
                }
                if (_hasUpper[row])
                {
                    DrawSide(context, Geometry(row, true), upperBrush, strip, bands);
                }
            }
            context.FillRectangle(rule, new Rect(0, y + m.RowHeight - 1, width, 1));
        }

        DrawCrosshair(context, layout);
        DrawAxis(context, layout, body);
        DrawFocus(context);
    }

    private void DrawSide(DrawingContext context, StreamGeometry geometry, IBrush brush, Rect strip, double bands)
    {
        for (var layer = 1; layer <= HorizonEngine.MaxBands; layer++)
        {
            if (!HorizonEngine.IsLayerVisible(layer, bands))
            {
                break;
            }
            var matrix = HorizonEngine.LayerMatrix(strip.X, strip.Y, strip.Width, strip.Height, _days, bands, layer);
            using (context.PushTransform(matrix))
            {
                context.DrawGeometry(brush, null, geometry);
            }
        }
    }

    private void DrawCrosshair(DrawingContext context, HorizonLayout layout)
    {
        var opacity = _crossOpacity.Value;
        if (opacity <= 0.001)
        {
            return;
        }
        var x = layout.DayX(_scrub.Value, _days);
        var top = layout.TopPadding - CrosshairOverhang;
        var bottom = layout.AxisTop(_names.Length) + CrosshairOverhang;
        context.FillRectangle(_palette.Brush(_palette.Foreground, 0.55 * opacity), new Rect(x - 0.5, top, 1, bottom - top));
    }

    private void DrawAxis(DrawingContext context, HorizonLayout layout, Typeface body)
    {
        var axisTop = layout.AxisTop(_names.Length);
        var line = _palette.Brush(_palette.Border);
        var label = _palette.Brush(_palette.TextMuted);
        foreach (var tick in _ticks)
        {
            if (tick.Index < 0 || tick.Index >= _days)
            {
                continue;
            }
            var x = layout.DayX(tick.Index, _days);
            context.FillRectangle(line, new Rect(x, axisTop + 10, 1, 16));
            var text = _text.Get(tick.Label, body, 12);
            text.Draw(context, label, new Point(x + 5, axisTop + 10 + (16 - text.Height) / 2));
        }
    }

    private void DrawFocus(DrawingContext context)
    {
        // Arc's outside ring is cut off by the block's clip; ours is drawn inside the plot.
        if (AnalyticsFocus.IsVisible(this))
        {
            AnalyticsFocus.Draw(context, _palette, new Rect(Bounds.Size), 0, -2);
        }
    }
}
