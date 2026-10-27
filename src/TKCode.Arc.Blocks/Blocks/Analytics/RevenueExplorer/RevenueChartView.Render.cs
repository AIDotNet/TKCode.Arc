using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

internal sealed partial class RevenueChartView
{
    // Drawing order (the measured svg): y grid and labels, clipped layers (expansion, new, churn), previous-period dashes and
    // lines (expansion, churn, new on top), month labels, crosshair and dots, readout, empty text, focus ring.
    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        EnsurePalette();
        if (!_palette.IsReady || size.Width <= LeftInset || _data.Count == 0)
        {
            return;
        }
        var body = new Typeface(_palette.BodyFont);
        var bottom = PlotBottom;
        var scale = _axis.Scale(bottom, 0);

        DrawGrid(context, size, body, scale);
        if (_geometryDirty)
        {
            BuildGeometry(scale);
            _geometryDirty = false;
        }
        DrawSeries(context, size, bottom);
        DrawMonthLabels(context, size, body);
        DrawCrosshair(context, bottom, scale);
        if (_empty.Value > 0.001)
        {
            DrawEmpty(context, size, body, bottom);
        }
        _readout.Draw(context, _text, _palette);
        if (AnalyticsFocus.IsVisible(this))
        {
            AnalyticsFocus.Draw(context, _palette, HitRect, FocusRadius);
        }
    }

    private void DrawGrid(DrawingContext context, Size size, Typeface body, AnalyticsLinearScale scale)
    {
        for (var i = 0; i < _axis.TickCount; i++)
        {
            var tick = _axis[i];
            if (tick.Opacity <= 0.001)
            {
                continue;
            }
            var y = AnalyticsPixels.Crisp(scale.Map(tick.Value));
            using (context.PushOpacity(tick.Opacity))
            {
                var pen = _palette.Pen(tick.Value == 0 ? _palette.BorderStrong : _palette.Border, 1, cap: PenLineCap.Flat);
                context.DrawLine(pen, new Point(LeftInset, y), new Point(size.Width, y));
                var label = _text.Get(_options.Axis(tick.Value), body, 11, true);
                label.Draw(context, _palette.Brush(_palette.TextMuted), new Point(38 - label.Width, y - 0.5 - label.Height / 2));
            }
        }
    }

    /// <summary>Rebuilds the layer and line geometry of the frame from the springs (months from one left of the window to one right).</summary>
    private void BuildGeometry(AnalyticsLinearScale scale)
    {
        var s = _viewport?.ChartStart ?? _rs;
        var e = _viewport?.ChartEnd ?? _re;
        var first = Math.Max(0, (int)Math.Floor(s) - 1);
        var last = Math.Min(_data.Count - 1, (int)Math.Ceiling(e) + 1);
        var m = last - first + 1;
        _pointCount = m;
        if (m < 2)
        {
            _gExpArea = _gNewArea = _gChurnArea = _gNewLine = _gExpLine = _gChurnLine = _gPrevTop = _gPrevChurn = null;
            return;
        }
        var fN = _fNew.Value;
        var fE = _fExp.Value;
        var fC = _fChurn.Value;
        var c = _cmp.Value;
        var n = _data.Count;
        var length = _re - _rs + 1;
        var y0 = scale.Map(0);
        var prev = _prev.Values;
        for (var k = 0; k < m; k++)
        {
            var i = first + k;
            var x = XOf(i);
            var newTop = _data.New[i] * fN;
            var expTop = newTop + _data.Expansion[i] * fE;
            var churn = _data.Churn[i] * fC;
            _pNew[k] = new Point(x, scale.Map(newTop));
            _pExp[k] = new Point(x, scale.Map(expTop));
            _pZero[k] = new Point(x, y0);
            _pChurn[k] = new Point(x, scale.Map(churn));
            if (i - length >= 0 && prev.Length >= 3 * n)
            {
                var prevTop = prev[i] * fN + prev[n + i] * fE;
                var prevChurn = prev[2 * n + i] * fC;
                _pPrevTop[k] = new Point(x, scale.Map(expTop + (prevTop - expTop) * c));
                _pPrevChurn[k] = new Point(x, scale.Map(churn + (prevChurn - churn) * c));
            }
            else
            {
                _pPrevTop[k] = _pExp[k];
                _pPrevChurn[k] = _pChurn[k];
            }
        }
        var curve = AnalyticsCurve.Monotone;
        _gExpArea = AnalyticsGeometry.Band(_pExp.AsSpan(0, m), _pNew.AsSpan(0, m), curve);
        _gNewArea = AnalyticsGeometry.Band(_pNew.AsSpan(0, m), _pZero.AsSpan(0, m), curve);
        _gChurnArea = AnalyticsGeometry.Band(_pZero.AsSpan(0, m), _pChurn.AsSpan(0, m), curve);
        _gNewLine = AnalyticsGeometry.Line(_pNew.AsSpan(0, m), curve);
        _gExpLine = AnalyticsGeometry.Line(_pExp.AsSpan(0, m), curve);
        _gChurnLine = AnalyticsGeometry.Line(_pChurn.AsSpan(0, m), curve);
        _gPrevTop = c > 0.001 ? AnalyticsGeometry.Line(_pPrevTop.AsSpan(0, m), curve) : null;
        _gPrevChurn = c > 0.001 ? AnalyticsGeometry.Line(_pPrevChurn.AsSpan(0, m), curve) : null;
    }

    private Color ExpansionColor => AnalyticsColors.Mix(_palette.Accent, 0.55, _palette.Surface);

    private void DrawSeries(DrawingContext context, Size size, double bottom)
    {
        if (_gNewArea is null)
        {
            return;
        }
        var fN = _fNew.Value;
        var fE = _fExp.Value;
        var fC = _fChurn.Value;
        using (context.PushClip(new Rect(LeftInset, 0, size.Width - LeftInset, bottom + 1)))
        {
            if (fE > 0.002)
            {
                context.DrawGeometry(_palette.Brush(_palette.Accent, ExpansionAreaOpacity), null, _gExpArea!);
            }
            if (fN > 0.002)
            {
                context.DrawGeometry(_palette.Brush(_palette.Accent, NewAreaOpacity), null, _gNewArea);
            }
            if (fC > 0.002)
            {
                context.DrawGeometry(_palette.Brush(_palette.Danger, ChurnAreaOpacity), null, _gChurnArea!);
            }
            var dashOpacity = Math.Clamp(1.6 * _cmp.Value, 0, 1);
            if (dashOpacity > 0.002 && _gPrevTop is not null && _gPrevChurn is not null)
            {
                var dash = _palette.Pen(_palette.TextSecondary, SecondaryLineWidth, 1, 2, 4);
                using (context.PushOpacity(dashOpacity))
                {
                    context.DrawGeometry(null, dash, _gPrevTop);
                    context.DrawGeometry(null, dash, _gPrevChurn);
                }
            }
            DrawLine(context, _gExpLine, _palette.Pen(ExpansionColor, SecondaryLineWidth), fE);
            DrawLine(context, _gChurnLine, _palette.Pen(_palette.Danger, SecondaryLineWidth), fC);
            DrawLine(context, _gNewLine, _palette.Pen(_palette.Accent, PrimaryLineWidth), fN);
        }
    }

    private static void DrawLine(DrawingContext context, StreamGeometry? geometry, IPen pen, double opacity)
    {
        if (geometry is null || opacity <= 0.002)
        {
            return;
        }
        using (context.PushOpacity(Math.Clamp(opacity, 0, 1)))
        {
            context.DrawGeometry(null, pen, geometry);
        }
    }

    private void DrawMonthLabels(DrawingContext context, Size size, Typeface body)
    {
        var baseline = CurrentHeight - 8;
        var left = LeftInset + LabelLeftInset;
        var right = size.Width - LabelRightInset;
        var brush = _palette.Brush(_palette.TextMuted);
        for (var i = 0; i < _label.Length; i++)
        {
            var opacity = _label[i].Value;
            if (opacity <= 0.002)
            {
                continue;
            }
            // A label that left the range parks at the left clamp (and fades there); one inside follows its month, clamped.
            var x = _labelOn[i] ? Math.Clamp(XOf(i), left, right) : left;
            var text = _text.Get(_labelText[i] ?? string.Empty, body, 11);
            using (context.PushOpacity(Math.Clamp(opacity, 0, 1)))
            {
                text.DrawAtBaseline(context, brush, x - text.Width / 2, baseline);
            }
        }
    }

    private void DrawCrosshair(DrawingContext context, double bottom, AnalyticsLinearScale scale)
    {
        if (_scrub is not { } i || i < 0 || i >= _data.Count)
        {
            return;
        }
        // The last month sits on the right edge: its line is drawn just inside.
        var x = Math.Min(CrossX(i), Bounds.Width - 0.5);
        if (x < LeftInset)
        {
            return;
        }
        context.DrawLine(_palette.Pen(_palette.BorderStrong, 1, cap: PenLineCap.Flat), new Point(x, CrosshairTop), new Point(x, bottom));
        var px = XOf(i);
        var fN = _fNew.Value;
        var fE = _fExp.Value;
        var fC = _fChurn.Value;
        var newTop = _data.New[i] * fN;
        DrawDot(context, px, scale.Map(newTop), _palette.Accent, fN);
        DrawDot(context, px, scale.Map(newTop + _data.Expansion[i] * fE), ExpansionColor, fE);
        DrawDot(context, px, scale.Map(_data.Churn[i] * fC), _palette.Danger, fC);
    }

    private void DrawDot(DrawingContext context, double x, double y, Color color, double opacity)
    {
        if (opacity <= 0.002)
        {
            return;
        }
        using (context.PushOpacity(Math.Clamp(opacity, 0, 1)))
        {
            context.DrawEllipse(_palette.Brush(color), _palette.Pen(_palette.Surface, 2), new Point(x, y), DotRadius, DotRadius);
        }
    }

    private void DrawEmpty(DrawingContext context, Size size, Typeface body, double bottom)
    {
        var text = _text.GetTrimmed(_options.EmptyText, body, 14, Math.Max(1, size.Width - LeftInset - 32));
        var cx = LeftInset + (size.Width - LeftInset) / 2;
        using (context.PushOpacity(Math.Clamp(_empty.Value, 0, 1)))
        {
            text.Draw(context, _palette.Brush(_palette.TextMuted), new Point(cx - text.Width / 2, bottom / 2 - text.Height / 2));
        }
    }
}
