using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

internal sealed partial class CohortRetentionPlot
{
    // Drawing order (the measured svg): y grid, row labels, x axis, lines, cells / points, crosshair, cursor mark, y tag,
    // then the card and the focus ring.
    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        EnsurePalette();
        if (!_palette.IsReady)
        {
            return;
        }
        var body = new Typeface(_palette.BodyFont);
        var medium = new Typeface(_palette.BodyFont, FontStyle.Normal, _palette.Medium);

        if (_model.IsEmpty || _count == 0)
        {
            var empty = _text.GetTrimmed(_options.EmptyText, body, 14, Math.Max(1, size.Width - 32));
            empty.Draw(context, _palette.Brush(_palette.TextMuted), new Point((size.Width - empty.Width) / 2, (size.Height - empty.Height) / 2));
            DrawFocus(context, size);
            return;
        }

        var layout = LayoutFor(size.Width);
        var p = _morph.Value;
        var curvesView = _view == CohortRetentionView.Curves;
        var cursor = _cursor;

        DrawGrid(context, layout, body, CohortRetentionMorph.Grid(p));
        DrawRowLabels(context, layout, body, medium, CohortRetentionMorph.Labels(p), cursor);
        DrawAxis(context, layout, body, medium, cursor);
        DrawLines(context, layout, CohortRetentionMorph.Curves(p), cursor);
        DrawCells(context, layout, body, p, cursor, curvesView);

        if (cursor is { } c && IndexOf(c) is { } index)
        {
            if (curvesView)
            {
                DrawCurvesCursor(context, layout, medium, c, index);
            }
            else
            {
                var x0 = layout.X0(c.MonthIndex);
                var x1 = layout.X1(c.MonthIndex);
                var top = layout.CellTop(c.Row, c.IsAverage);
                var box = new Rect(x0 - 1, top - 1, x1 - x0 + 2, layout.CellHeight + 2);
                context.DrawRectangle(null, _palette.Pen(_palette.Foreground, 1.5), new RoundedRect(box, layout.CellRadius + 1));
            }
        }

        _card.Draw(context, _text, _palette);
        DrawFocus(context, size);
    }

    private void DrawFocus(DrawingContext context, Size size)
    {
        if (AnalyticsFocus.IsVisible(this))
        {
            AnalyticsFocus.Draw(context, _palette, new Rect(size));
        }
    }

    private void DrawGrid(DrawingContext context, CohortRetentionLayout layout, Typeface body, double opacity)
    {
        if (opacity <= 0.001)
        {
            return;
        }
        using (context.PushOpacity(opacity))
        {
            var labelRight = layout.LabelWidth - 12;
            for (var i = 0; i < 5; i++)
            {
                var y = layout.GridY(i);
                var pen = _palette.Pen(i == 4 ? _palette.BorderStrong : _palette.Border, 1, cap: PenLineCap.Flat);
                context.DrawLine(pen, new Point(layout.LabelWidth, y), new Point(layout.Width, y));
                var label = _text.Get(s_gridLabels[i], body, 11, true);
                label.Draw(context, _palette.Brush(_palette.TextMuted), new Point(labelRight - label.Width, y - 0.5 - label.Height / 2));
            }
        }
    }

    private void DrawRowLabels(DrawingContext context, CohortRetentionLayout layout, Typeface body, Typeface medium, double opacity,
        CohortCell? cursor)
    {
        if (opacity <= 0.001)
        {
            return;
        }
        var showSizes = !layout.IsCompact && _options.ShowSizeColumn;
        var sizeRight = layout.LabelWidth - SizeInset;
        using (context.PushOpacity(opacity))
        {
            for (var r = 0; r < _model.Rows; r++)
            {
                var y = layout.RowCenter(r, false);
                var on = cursor is { IsAverage: false } c && c.Row == r;
                var sizeWidth = 0.0;
                if (showSizes)
                {
                    var sizeText = _text.Get(_sizeTexts[r], body, 12, true);
                    sizeWidth = sizeText.Width;
                    sizeText.Draw(context, _palette.Brush(on ? _palette.Foreground : _palette.TextMuted), new Point(sizeRight - sizeWidth, y - sizeText.Height / 2));
                }
                var maxWidth = showSizes ? sizeRight - sizeWidth - 6 : layout.LabelWidth - 6;
                var name = _text.GetTrimmed(layout.IsCompact ? _model.ShortLabels[r] : _model.Labels[r], body, 12, Math.Max(1, maxWidth), true);
                name.Draw(context, _palette.Brush(on ? _palette.Foreground : _palette.TextSecondary), new Point(0, y - name.Height / 2));
            }
            if (_hasAverage)
            {
                var y = layout.RowCenter(0, true);
                var sizeWidth = 0.0;
                if (showSizes)
                {
                    var total = _text.Get(_totalText, body, 12, true);
                    sizeWidth = total.Width;
                    var on = cursor is { IsAverage: true };
                    total.Draw(context, _palette.Brush(on ? _palette.Foreground : _palette.TextMuted), new Point(sizeRight - sizeWidth, y - total.Height / 2));
                }
                var maxWidth = showSizes ? sizeRight - sizeWidth - 6 : layout.LabelWidth - 6;
                var name = _text.GetTrimmed(_options.AverageLabel, medium, 12, Math.Max(1, maxWidth));
                name.Draw(context, _palette.Brush(_palette.Foreground), new Point(0, y - name.Height / 2));
            }
        }
    }

    private void DrawAxis(DrawingContext context, CohortRetentionLayout layout, Typeface body, Typeface medium, CohortCell? cursor)
    {
        var y = layout.AxisY;
        var title = _text.GetTrimmed(layout.IsCompact ? _options.AxisTitleShort : _options.AxisTitle, body, 11,
            Math.Max(1, layout.LabelWidth - 6));
        title.Draw(context, _palette.Brush(_palette.TextMuted), new Point(0, y - title.Height / 2));
        for (var m = 0; m < _model.Months && m < _monthTexts.Length; m++)
        {
            var on = cursor is { } c && c.MonthIndex == m;
            var text = _text.Get(_monthTexts[m], on ? medium : body, 11, true);
            text.Draw(context, _palette.Brush(on ? _palette.Foreground : _palette.TextMuted), new Point(layout.Cx(m) - text.Width / 2, y - text.Height / 2));
        }
    }

    private void DrawLines(DrawingContext context, CohortRetentionLayout layout, double opacity, CohortCell? cursor)
    {
        if (opacity <= 0.001)
        {
            return;
        }
        if (_linesDirty)
        {
            RebuildLines(layout);
        }
        var target = cursor is { IsAverage: false } c && _view == CohortRetentionView.Curves ? c.Row : -1;
        using (context.PushOpacity(opacity))
        {
            for (var r = 0; r < _lines.Length; r++)
            {
                if (_lines[r] is { } line && r != target)
                {
                    context.DrawGeometry(null, _palette.Pen(_palette.TextMuted, 1.25, _lineAlpha[r].Value), line);
                }
            }
            if (_averageLine is { } average)
            {
                context.DrawGeometry(null, _palette.Pen(_palette.Accent, 2.25), average);
            }
            // The highlighted cohort is drawn once more above the average line (Arc's 14th path).
            if (target >= 0 && target < _lines.Length && _lines[target] is { } on)
            {
                context.DrawGeometry(null, _palette.Pen(_palette.Foreground, 1.75, _lineAlpha[target].Value), on);
            }
        }
    }

    private void RebuildLines(CohortRetentionLayout layout)
    {
        _linesDirty = false;
        for (var r = 0; r < _model.Rows; r++)
        {
            _lines[r] = BuildLine(layout, _index[r], false);
        }
        _averageLine = _hasAverage ? BuildLine(layout, _averageIndex, true) : null;
    }

    private StreamGeometry? BuildLine(CohortRetentionLayout layout, int[] indexes, bool average)
    {
        if (indexes.Length < 2)
        {
            return null;
        }
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (var m = 0; m < indexes.Length; m++)
            {
                var i = indexes[m];
                var cell = _cells[i];
                var point = new Point(layout.Cx(m), PointYAt(i, cell, layout));
                if (m == 0)
                {
                    ctx.BeginFigure(point, false);
                }
                else
                {
                    ctx.LineTo(point);
                }
            }
            ctx.EndFigure(false);
        }
        return geometry;
    }

    private double PointYAt(int i, CohortCell cell, CohortRetentionLayout layout)
    {
        var yp = CohortRetentionMorph.Y(_morph.Value, _keys[i]);
        var rowY = layout.RowCenter(cell.Row, cell.IsAverage);
        return rowY + (layout.YOf(_value[i].Value) - rowY) * yp;
    }

    private void DrawCells(DrawingContext context, CohortRetentionLayout layout, Typeface body, double p, CohortCell? cursor, bool curvesView)
    {
        var numbers = layout.ColumnWidth >= NumberMinWidth;
        var mix = Math.Clamp(_pointMix.Value, 0, 1);
        var pointAlpha = Math.Clamp(_pointAlpha.Value, 0, 1);
        var targetRow = curvesView && cursor is { IsAverage: false } c ? c.Row : -1;
        var foreground = _palette.Brush(_palette.Foreground);
        var light = _palette.Brush(_accentForeground);
        for (var i = 0; i < _count; i++)
        {
            var cell = _cells[i];
            var key = _keys[i];
            var s = CohortRetentionMorph.Size(p, key);
            var x0 = layout.X0(cell.MonthIndex);
            var x1 = layout.X1(cell.MonthIndex);
            var cx = (x0 + x1) / 2;
            var d = cell.IsAverage ? AveragePointDiameter : PointDiameter;
            var w = Lerp(x1 - x0, d, s);
            var h = Lerp(layout.CellHeight, d, s);
            var radius = Lerp(layout.CellRadius, d / 2, s);
            var cy = PointYAt(i, cell, layout);
            var rect = new Rect(cx - w / 2, cy - h / 2, w, h);

            Color fill;
            var isTarget = !cell.IsAverage && cell.Row == targetRow;
            if (isTarget)
            {
                fill = _palette.Foreground;
            }
            else
            {
                var heat = HeatColor(i);
                var point = cell.IsAverage ? _palette.Accent : _palette.TextMuted;
                fill = mix <= 0 ? heat : mix >= 1 ? point : AnalyticsColors.Lerp(heat, point, mix);
                if (!cell.IsAverage && pointAlpha > 0)
                {
                    fill = AnalyticsColors.Fade(fill, 1 - 0.2 * pointAlpha);
                }
            }

            using (context.PushOpacity(Math.Clamp(_dim[i].Value, 0, 1)))
            {
                context.DrawRectangle(FillBrush(i, fill), null, new RoundedRect(rect, radius));
                if (!numbers)
                {
                    continue;
                }
                var textOpacity = CohortRetentionMorph.TextOpacity(s);
                if (textOpacity <= 0.001)
                {
                    continue;
                }
                var percent = (int)Math.Round(Math.Clamp(_value[i].Value, 0, 1) * 100, MidpointRounding.AwayFromZero);
                var text = _text.Get(s_percents[percent], body, 11, true);
                var brush = TextIsLight(i) ? light : foreground;
                using (context.PushOpacity(textOpacity))
                {
                    text.Draw(context, brush, new Point(cx - text.Width / 2, cy - text.Height / 2));
                }
            }
        }
    }

    private void DrawCurvesCursor(DrawingContext context, CohortRetentionLayout layout, Typeface medium, CohortCell cell, int index)
    {
        var x = layout.Cx(cell.MonthIndex);
        var y = PointYAt(index, cell, layout);
        var line = _palette.Pen(_palette.BorderStrong, 1, cap: PenLineCap.Flat);
        context.DrawLine(line, new Point(x + 0.5, 4), new Point(x + 0.5, layout.PlotBottom));
        context.DrawLine(_palette.Pen(_palette.BorderStrong, 1, dash: 2, gap: 3, cap: PenLineCap.Flat), new Point(layout.LabelWidth, y),
            new Point(x + 0.5, y));

        var ring = cell.IsAverage ? AverageRingDiameter : RingDiameter;
        context.DrawEllipse(null, _palette.Pen(_palette.Foreground, 1.5), new Point(x, y), ring / 2, ring / 2);

        // The y tag: a 52 × 20 Foreground pill right-aligned at the label column, the exact rate in AccentForeground.
        var value = _value[index].Value;
        if (_tagText is null || Math.Abs(value - _tagValue) > 1e-6)
        {
            _tagValue = value;
            _tagText = _options.Percent(value);
        }
        var right = layout.LabelWidth - 12;
        var tagWidth = 52.0;
        var tagLeft = Math.Max(0, right + 6 - tagWidth);
        var tag = new Rect(tagLeft, y - 10, tagWidth, 20);
        context.DrawRectangle(_palette.Brush(_palette.Foreground), null, new RoundedRect(tag, 6));
        var text = _text.Get(_tagText, medium, 11, true);
        text.Draw(context, _palette.Brush(_accentForeground), new Point(Math.Min(right, tag.Right - 4) - text.Width, y - text.Height / 2));
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
}
