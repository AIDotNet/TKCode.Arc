using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

internal sealed partial class UsageForecastPlot
{
    private const double PanelHeight = 82;

    /// <summary>The label of the present day (set by the block).</summary>
    public string Today { get; set; } = "Today";

    /// <summary>The handle's word.</summary>
    public string BudgetWord { get; set; } = "Budget";

    /// <summary>The handle's value text (set by the block).</summary>
    public string BudgetValueText { get; set; } = string.Empty;

    // Drawing order (the measured svg): grid, y labels, wash after today, today, cone / forecast / measured (each twice,
    // clipped under and over the budget line), today's dot, alert ticks, crossing marker, budget line and handle capsule,
    // date labels and the crossing pill, inspect crosshair, the card, then the focus ring.
    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        EnsurePalette();
        if (!_palette.IsReady || _y.Length == 0 || size.Width <= 0 || Series.Days < 1)
        {
            return;
        }
        var p = _palette;
        var body = new Typeface(p.BodyFont);
        var medium = new Typeface(p.BodyFont, FontStyle.Normal, p.Medium);
        var today = Series.Today;
        var left = Left;
        var right = Right;
        var top = Top;
        var bottom = Bottom;
        var budgetY = _budgetY.Value;

        DrawGrid(context, body, left, right, top, bottom);

        if (today >= 1)
        {
            var tx = X(today) + 0.5;
            context.FillRectangle(p.Brush(p.Foreground, 0.025), new Rect(tx, top, Math.Max(0, right - tx), bottom - top));
            context.DrawLine(p.Pen(p.BorderStrong, 1), new Point(tx, 22), new Point(tx, bottom));
            var label = _text.Get(Today, medium, 12);
            label.DrawAtBaseline(context, p.Brush(p.TextSecondary), tx - label.Width / 2, 16);
        }

        if (_geoDirty)
        {
            BuildGeometry();
        }
        var cut = Math.Clamp(budgetY, 0, size.Height);
        using (context.PushClip(new Rect(0, cut, size.Width, size.Height - cut)))
        {
            DrawSeries(context, danger: false);
        }
        using (context.PushClip(new Rect(0, 0, size.Width, cut)))
        {
            DrawSeries(context, danger: true);
        }

        if (today >= 1)
        {
            context.DrawEllipse(p.Brush(p.Accent), p.Pen(p.Surface, 2), new Point(X(today), _y[today].Value), TodayDotRadius, TodayDotRadius);
        }
        DrawThresholds(context, medium);
        DrawCrossing(context, bottom);
        context.DrawLine(p.Pen(p.Foreground, 1), new Point(left, budgetY + 0.5), new Point(right, budgetY + 0.5));
        DrawHandle(context, body, medium, budgetY);
        DrawDates(context, body, medium);
        DrawInspect(context, body, medium, top, bottom);

        if (AnalyticsFocus.IsVisible(this))
        {
            AnalyticsFocus.Draw(context, p, new Rect(left, 0, right - left, bottom));
        }
    }

    private void DrawGrid(DrawingContext context, Typeface body, double left, double right, double top, double bottom)
    {
        var p = _palette;
        var rows = Math.Max(1, _tickNew.Length - 1);
        for (var i = 0; i <= rows; i++)
        {
            var y = bottom - i * (bottom - top) / rows;
            var crisp = AnalyticsPixels.Crisp(y);
            context.DrawLine(p.Pen(i == 0 ? p.BorderStrong : p.Border, 1), new Point(left, crisp), new Point(right, crisp));
            if (i >= _tickNew.Length)
            {
                continue;
            }
            DrawTick(context, body, _tickNew[i], _tickNewAlpha[i].Value, y);
            if (_tickOldAlpha[i].Value > 0.001 && !string.IsNullOrEmpty(_tickOld[i]))
            {
                DrawTick(context, body, _tickOld[i], _tickOldAlpha[i].Value, y);
            }
        }
    }

    private void DrawTick(DrawingContext context, Typeface body, string text, double alpha, double y)
    {
        if (alpha <= 0.001 || text.Length == 0)
        {
            return;
        }
        var label = _text.Get(text, body, 12, tabular: true);
        label.Draw(context, _palette.Brush(_palette.TextMuted, alpha), new Point(AxisLabelWidth - label.Width, y - label.Height / 2));
    }

    private void BuildGeometry()
    {
        var n = Series.Days;
        var stride = n + 1;
        var t = Series.Today;
        for (var d = 0; d <= t && d < _pa.Length; d++)
        {
            _pa[d] = new Point(X(d), _y[d].Value);
        }
        for (var h = 0; h < _pf.Length; h++)
        {
            var d = t + h;
            var x = X(d);
            _pf[h] = new Point(x, _y[d].Value);
            _pu[h] = new Point(x, _y[stride + d].Value);
            _pl[h] = new Point(x, _y[2 * stride + d].Value);
        }
        _actualGeo = _pa.Length >= 2 ? AnalyticsGeometry.Line(_pa) : null;
        _forecastGeo = _pf.Length >= 2 ? AnalyticsGeometry.Line(_pf) : null;
        _coneGeo = _pf.Length >= 2 ? AnalyticsGeometry.Band(_pu, _pl) : null;
        _geoDirty = false;
    }

    // The measured design draws every path twice with a clipPath on each side of the budget line; so do we, with the clip
    // rectangle following the displayed budget (no geometry is rebuilt when only the budget moves).
    private void DrawSeries(DrawingContext context, bool danger)
    {
        var p = _palette;
        var color = danger ? p.Danger : p.Accent;
        if (_coneGeo is not null)
        {
            context.DrawGeometry(p.Brush(color, danger ? 0.10 : 0.12), null, _coneGeo);
        }
        if (_forecastGeo is not null)
        {
            context.DrawGeometry(null, p.Pen(color, 1.75, 1, 4, 4, PenLineCap.Flat), _forecastGeo);
        }
        if (_actualGeo is not null)
        {
            context.DrawGeometry(null, p.Pen(color, 2), _actualGeo);
        }
    }

    private void DrawThresholds(DrawingContext context, Typeface medium)
    {
        var p = _palette;
        var length = ThresholdTickLength;
        for (var k = 0; k < _thX.Length; k++)
        {
            var wipe = _thWipe[k].Value;
            var alpha = _thLabel[k].Value;
            if (wipe <= 0.001 && alpha <= 0.001)
            {
                continue;
            }
            var x = X(_thX[k].Value);
            var y = LineYAt(_thX[k].Value);
            if (wipe > 0.001)
            {
                context.DrawLine(p.Pen(p.Foreground, 1.5), new Point(x, y + length / 2), new Point(x, y + length / 2 - length * Math.Clamp(wipe, 0, 1)));
            }
            if (alpha > 0.001 && k < _thresholdTexts.Length)
            {
                var label = _text.Get(_thresholdTexts[k], medium, 12, tabular: true);
                var ox = x - 6 - label.Width;
                var oy = y - 6;
                var halo = p.Brush(p.Surface, alpha);
                for (var i = 0; i < 8; i++)
                {
                    var a = i * Math.PI / 4;
                    label.DrawAtBaseline(context, halo, ox + Math.Cos(a) * 1.5, oy + Math.Sin(a) * 1.5);
                }
                label.DrawAtBaseline(context, p.Brush(p.TextSecondary, alpha), ox, oy);
            }
        }
    }

    private void DrawCrossing(DrawingContext context, double bottom)
    {
        var alpha = Math.Clamp(_crossAlpha.Value, 0, 1);
        if (alpha <= 0.001)
        {
            return;
        }
        var p = _palette;
        var x = X(_crossX.Value);
        var y = LineYAt(_crossX.Value);
        var r = CrossingRadius;
        using (context.PushOpacity(alpha))
        {
            context.DrawLine(p.Pen(p.Danger, 1, 1, 2, 3, PenLineCap.Flat), new Point(x + 0.5, y + r), new Point(x + 0.5, bottom));
            context.DrawEllipse(p.Brush(p.Surface), p.Pen(p.Danger, 2), new Point(x, y), r, r);
        }
    }

    private void DrawHandle(DrawingContext context, Typeface body, Typeface medium, double budgetY)
    {
        var p = _palette;
        var hover = Math.Clamp(_handleHover.Value, 0, 1);
        var word = _text.Get(BudgetWord, body, 12);
        var value = _text.Get(BudgetValueText, medium, 12, tabular: true);
        const double padLeft = 7;
        const double padRight = 10;
        const double icon = 14;
        const double gap = 6;
        var width = padLeft + icon + gap + word.Width + gap + value.Width + padRight;
        var height = HandleCapsuleHeight;
        var cy = Math.Round(budgetY);
        var rect = new Rect(Left + HandleCapsuleInset, cy - height / 2, width, height);
        var border = AnalyticsColors.Lerp(p.BorderStrong, p.TextSecondary, hover);
        context.DrawRectangle(p.Brush(p.Surface), p.Pen(border, 1), new RoundedRect(rect.Deflate(0.5), height / 2));
        var iconBrush = p.Brush(AnalyticsColors.Lerp(p.TextMuted, p.Foreground, hover));
        var ix = rect.X + padLeft;
        var scale = icon / 24;
        for (var row = 0; row < 2; row++)
        {
            for (var col = 0; col < 3; col++)
            {
                var gx = 5 + col * 7;
                var gy = row == 0 ? 9 : 15;
                context.DrawEllipse(iconBrush, null, new Point(ix + gx * scale, cy - icon / 2 + gy * scale), 0.75, 0.75);
            }
        }
        var tx = ix + icon + gap;
        word.Draw(context, p.Brush(p.TextSecondary), new Point(tx, cy - word.Height / 2));
        value.Draw(context, p.Brush(p.Foreground), new Point(tx + word.Width + gap, cy - value.Height / 2));
    }

    private void DrawDates(DrawingContext context, Typeface body, Typeface medium)
    {
        var p = _palette;
        var n = Series.Days;
        var baseline = Bounds.Height - 10;
        var crossAlpha = Math.Clamp(_crossAlpha.Value, 0, 1);
        var markerX = X(_crossX.Value);
        for (var d = 1; d <= n; d += 7)
        {
            var text = _text.Get(_data.DayText(d), body, 12, tabular: true);
            var x = X(d);
            var alpha = 1 - crossAlpha * (1 - UsageForecastEngine.LabelFade(x, markerX));
            if (alpha <= 0.001)
            {
                continue;
            }
            text.DrawAtBaseline(context, p.Brush(p.TextMuted, alpha), x - text.Width / 2, baseline);
        }
        if (crossAlpha > 0.001 && !string.IsNullOrEmpty(_crossText))
        {
            var text = _text.Get(_crossText, medium, 12, tabular: true);
            var w = text.Width + 16;
            var center = Math.Min(markerX, Bounds.Width - 32);
            var rect = new Rect(center - w / 2, Bounds.Height - 24, w, 20);
            var border = AnalyticsColors.Mix(p.Danger, 0.5, p.Surface);
            using (context.PushOpacity(crossAlpha))
            {
                context.DrawRectangle(p.Brush(p.Surface), p.Pen(border, 1), new RoundedRect(rect.Deflate(0.5), 10));
                text.Draw(context, p.Brush(p.Danger), new Point(rect.X + 8, rect.Y + (rect.Height - text.Height) / 2));
            }
        }
    }

    private void DrawInspect(DrawingContext context, Typeface body, Typeface medium, double top, double bottom)
    {
        var p = _palette;
        var alpha = Math.Clamp(_panelAlpha.Value, 0, 1);
        if (alpha <= 0.001)
        {
            return;
        }
        var day = _inspectDay;
        using (context.PushOpacity(alpha))
        {
            if (day > 0)
            {
                var x = X(day);
                context.DrawLine(p.Pen(p.BorderStrong, 1), new Point(x + 0.5, top), new Point(x + 0.5, bottom));
                context.DrawEllipse(p.Brush(p.Accent), p.Pen(p.Surface, 2), new Point(x, _y[day].Value), 4, 4);
            }
            var rect = new Rect(_panelX.Value, top, PanelWidth, PanelHeight);
            context.DrawRectangle(p.Brush(p.SurfaceRaised), p.Pen(p.Border, 1), new RoundedRect(rect.Deflate(0.5), PanelRadius), p.ShadowFloating);
            const double padX = 12;
            var y = rect.Y + 10;
            var date = _text.GetTrimmed(_panel.Date ?? string.Empty, medium, 12, rect.Width - 2 * padX - 56);
            date.Draw(context, p.Brush(p.Foreground), new Point(rect.X + padX, y + (16.8 - date.Height) / 2));
            var tag = _text.Get(_panel.Tag ?? string.Empty, body, 12);
            tag.Draw(context, p.Brush(p.TextMuted), new Point(rect.Right - padX - tag.Width, y + (16.8 - tag.Height) / 2));
            y += 16.8 + 3;
            var value = _text.GetTrimmed(_panel.Value ?? string.Empty, medium, 16, rect.Width - 2 * padX, tabular: true);
            value.Draw(context, p.Brush(p.Foreground), new Point(rect.X + padX, y + (22.4 - value.Height) / 2));
            y += 22.4 + 3;
            var detail = _text.GetTrimmed(_panel.Detail ?? string.Empty, body, 12, rect.Width - 2 * padX, tabular: true);
            detail.Draw(context, p.Brush(p.TextSecondary), new Point(rect.X + padX, y + (16.8 - detail.Height) / 2));
        }
    }
}
