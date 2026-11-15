using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

// Drawing: the track (a capsule whose outline swells around the thumb, stretches past the end and retreats into the
// thumb), its inner shadow, the label twice (plain, and clipped to the liquid in the on-fill ink), the liquid (a capsule
// with a swollen head at the tail plus the neck to the thumb), the success circle, the status text, the thumb with its
// spinner and its chevron-to-check glyph, and the keyboard focus ring.
public partial class SlideToConfirm
{
    private const int CapSteps = 28;

    private Point[] _outline = new Point[256];
    private int _outlineCount;
    private bool _textDirty = true;
    private TextLayout? _labelLayout;
    private TextLayout? _labelOnFillLayout;
    private TextLayout? _statusLayout;
    private string? _statusLayoutText;

    private IBrush? _tintBrush;
    private double _tintBrushKey = -1;
    private IBrush? _tintSourceA;
    private IBrush? _tintSourceB;
    private Pen? _borderPen;
    private IBrush? _borderPenBrush;
    private Pen? _insetPen;
    private IBrush? _insetPenBrush;
    private Pen? _inkPen;
    private IBrush? _inkPenBrush;
    private Pen? _spinnerPen;
    private Pen? _checkPen;
    private IBrush? _checkPenBrush;
    private Pen? _focusPen;
    private IBrush? _focusPenBrush;

    private void InvalidateText()
    {
        _textDirty = true;
        InvalidateVisual();
    }

    private double CurrentLabelSize => _h >= TallTrackHeight - 0.01 && _w >= TallFromWidth ? TallLabelFontSize : LabelFontSize;

    private void EnsureText()
    {
        var status = _state switch
        {
            SlideToConfirmState.Pending => PendingLabel,
            SlideToConfirmState.Done => ConfirmedLabel,
            _ => _statusText.Length > 0 ? _statusText : ConfirmedLabel
        };
        if (_state is SlideToConfirmState.Pending or SlideToConfirmState.Done)
        {
            _statusText = status;
        }
        if (!_textDirty && status == _statusLayoutText)
        {
            return;
        }
        _textDirty = false;
        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight.Medium);
        var size = CurrentLabelSize;
        var spacing = -0.01 * size;
        var text = _showError ? ErrorLabel : Label;
        var plain = _showError ? DangerBrush ?? Foreground : Foreground;
        _labelLayout?.Dispose();
        _labelOnFillLayout?.Dispose();
        _statusLayout?.Dispose();
        _labelLayout = Layout(text ?? "", typeface, size, plain, spacing);
        _labelOnFillLayout = Layout(text ?? "", typeface, size, OnFillBrush, spacing);
        _statusLayout = Layout(status ?? "", typeface, LabelFontSize, StatusBrush, -0.01 * LabelFontSize);
        _statusLayoutText = status;
    }

    private static TextLayout Layout(string text, Typeface typeface, double size, IBrush? brush, double spacing)
        => new(text, typeface, size, brush ?? Brushes.Gray, TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.None,
            null, FlowDirection.LeftToRight, double.PositiveInfinity, double.PositiveInfinity, double.NaN, spacing, 1);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var w = _w;
        var h = _h;
        if (w <= 0 || h <= 0)
        {
            return;
        }
        EnsureText();
        UpdatePens();

        // The whole box takes the pointer, not just the drawn pixels.
        context.FillRectangle(Brushes.Transparent, new Rect(0, 0, w, h));

        if (IsEffectivelyEnabled)
        {
            RenderParts(context, w, h);
        }
        else
        {
            using (context.PushOpacity(DisabledOpacity))
            {
                RenderParts(context, w, h);
            }
        }
    }

    private void RenderParts(DrawingContext context, double w, double h)
    {
        var full = ArcMotion.IsFull(this);
        var cy = h / 2;
        var cx = (h / 2) + _pos;
        var scale = Scale;

        // Track: retreats from the left, stretches past the end, swells around the thumb.
        var left = _retreat * _travel;
        var right = Math.Max(w + SlideToConfirmMath.TrackStretch(_pos - _travel), left + h);
        BuildOutline(left, right, 0, h / 2, cx, _vSwell, SlideToConfirmMath.SwellSigma(scale));
        var track = OutlineGeometry();
        context.DrawGeometry(TrackFill(), _borderPen, track);
        if (_insetPen is not null)
        {
            using (context.PushGeometryClip(track))
            using (context.PushTransform(Matrix.CreateTranslation(0, 1.5)))
            {
                context.DrawGeometry(null, _insetPen, track);
            }
        }

        // Label, drifting right and fading as the thumb moves.
        var labelX = ((h + w) / 2) + SlideToConfirmMath.LabelDrift(_travel > 0 ? _pos / _travel : 0);
        if (_vLabel > 0.001 && _labelLayout is not null)
        {
            using (context.PushOpacity(_vLabel))
            {
                _labelLayout.Draw(context, new Point(labelX - (_labelLayout.Width / 2), cy - (_labelLayout.Height / 2)));
            }
        }

        // The liquid and the label on it.
        var fillBrush = AccentBrush ?? FillBrush;
        var fill = FillGeometry(left + SlideToConfirmMath.Inset, cy, cx, (h / 2) + _tail, full);
        context.DrawGeometry(fillBrush, null, fill);
        if (_vLabel > 0.001 && _labelOnFillLayout is not null)
        {
            using (context.PushGeometryClip(fill))
            using (context.PushOpacity(_vLabel))
            {
                _labelOnFillLayout.Draw(context, new Point(labelX - (_labelOnFillLayout.Width / 2), cy - (_labelOnFillLayout.Height / 2)));
            }
        }

        // Success: the shrunken track fills with the fill color.
        if (_vSuccess > 0.001)
        {
            using (context.PushOpacity(_vSuccess))
            {
                context.DrawGeometry(fillBrush, null, track);
            }
        }

        // Pending / confirmed text, right-aligned to the retreating edge.
        if (_vStatus > 0.001 && _statusLayout is not null)
        {
            using (context.PushOpacity(_vStatus))
            {
                _statusLayout.Draw(context, new Point(left - StatusGap - _statusLayout.Width, cy - (_statusLayout.Height / 2)));
            }
        }

        // Thumb.
        if (_vDisc > 0.001)
        {
            using (context.PushOpacity(_vDisc))
            {
                var disc = new Rect(cx - _vRx, cy - _vRy, _vRx * 2, _vRy * 2);
                context.DrawRectangle(ThumbBrush, null, new RoundedRect(disc, _vRx, _vRy), ThumbShadow);
            }
        }
        if (_vSpinner > 0.001 && _spinnerPen is not null)
        {
            using (context.PushOpacity(_vSpinner * 0.6))
            {
                context.DrawGeometry(null, _spinnerPen, SpinnerGeometry(cx, cy, full ? _spinAngle : 0));
            }
        }
        if (_vInk > 0.001 && _inkPen is not null)
        {
            using (context.PushOpacity(_vInk))
            {
                context.DrawGeometry(null, _inkPen, GlyphGeometry(cx, cy, _vMorph));
            }
        }
        if (_vCheck > 0.001 && _checkPen is not null)
        {
            using (context.PushOpacity(_vCheck))
            {
                context.DrawGeometry(null, _checkPen, GlyphGeometry(cx, cy, _vMorph));
            }
        }

        // Keyboard focus: a capsule ring 2 px outside the control.
        if (IsFocusVisible && _focusPen is not null)
        {
            var ring = new Rect(0, 0, w, h).Inflate(3);
            context.DrawRectangle(null, _focusPen, new RoundedRect(ring, (h / 2) + 3));
        }
    }

    private void UpdatePens()
    {
        if (!ReferenceEquals(_borderPenBrush, BorderBrush))
        {
            _borderPenBrush = BorderBrush;
            _borderPen = BorderBrush is null ? null : new Pen(BorderBrush, 1);
        }
        if (!ReferenceEquals(_insetPenBrush, InsetShadow))
        {
            _insetPenBrush = InsetShadow;
            _insetPen = InsetShadow is null ? null : new Pen(InsetShadow, 3);
        }
        if (!ReferenceEquals(_inkPenBrush, ThumbInk))
        {
            _inkPenBrush = ThumbInk;
            _inkPen = ThumbInk is null ? null : new Pen(ThumbInk, 2.25, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _spinnerPen = ThumbInk is null ? null : new Pen(ThumbInk, 2.25, lineCap: PenLineCap.Round);
        }
        if (!ReferenceEquals(_checkPenBrush, OnFillBrush))
        {
            _checkPenBrush = OnFillBrush;
            _checkPen = OnFillBrush is null ? null : new Pen(OnFillBrush, 2.25, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        }
        if (!ReferenceEquals(_focusPenBrush, FocusRingBrush))
        {
            _focusPenBrush = FocusRingBrush;
            _focusPen = FocusRingBrush is null ? null : new Pen(FocusRingBrush, 2);
        }
    }

    private IBrush? TrackFill()
    {
        if (_tint <= 0.001 || TrackHoverBrush is null)
        {
            return TrackBrush;
        }
        if (_tint >= 0.999 || TrackBrush is not ISolidColorBrush a || TrackHoverBrush is not ISolidColorBrush b)
        {
            return _tint >= 0.5 ? TrackHoverBrush : TrackBrush;
        }
        var key = Math.Round(_tint * 64) / 64;
        if (_tintBrush is null || key != _tintBrushKey || !ReferenceEquals(_tintSourceA, TrackBrush) || !ReferenceEquals(_tintSourceB, TrackHoverBrush))
        {
            _tintBrushKey = key;
            _tintSourceA = TrackBrush;
            _tintSourceB = TrackHoverBrush;
            _tintBrush = new ImmutableSolidColorBrush(Lerp(a.Color, b.Color, key));
        }
        return _tintBrush;
    }

    private static Color Lerp(Color a, Color b, double t)
        => Color.FromArgb(
            (byte)Math.Round(a.A + ((b.A - a.A) * t)),
            (byte)Math.Round(a.R + ((b.R - a.R) * t)),
            (byte)Math.Round(a.G + ((b.G - a.G) * t)),
            (byte)Math.Round(a.B + ((b.B - a.B) * t)));

    /// <summary>
    /// Fills <see cref="_outline"/> with the capsule [<paramref name="left"/>, <paramref name="right"/>] × 2·hh,
    /// clockwise from the top-left, each point pushed out along its normal by the swell bell around <paramref name="cx"/>.
    /// </summary>
    internal void BuildOutline(double left, double right, double top, double hh, double cx, double amp, double sigma)
    {
        _outlineCount = 0;
        var cy = top + hh;
        var a = left + hh;
        var b = Math.Max(a, right - hh);
        var straight = Math.Max(1, (int)Math.Ceiling((b - a) / 4));
        for (var i = 0; i < straight; i++)
        {
            var x = a + ((b - a) * i / straight);
            Add(x, cy - hh, 0, -1);
        }
        for (var i = 0; i < CapSteps; i++)
        {
            var t = -Math.PI / 2 + (Math.PI * i / CapSteps);
            var nx = Math.Cos(t);
            var ny = Math.Sin(t);
            Add(b + (hh * nx), cy + (hh * ny), nx, ny);
        }
        for (var i = 0; i < straight; i++)
        {
            var x = b - ((b - a) * i / straight);
            Add(x, cy + hh, 0, 1);
        }
        for (var i = 0; i < CapSteps; i++)
        {
            var t = (Math.PI / 2) + (Math.PI * i / CapSteps);
            var nx = Math.Cos(t);
            var ny = Math.Sin(t);
            Add(a + (hh * nx), cy + (hh * ny), nx, ny);
        }

        void Add(double x, double y, double nx, double ny)
        {
            if (_outlineCount == _outline.Length)
            {
                Array.Resize(ref _outline, _outline.Length * 2);
            }
            var d = amp > 0.001 ? amp * Math.Exp(-((x - cx) * (x - cx)) / (2 * sigma * sigma)) : 0;
            _outline[_outlineCount++] = new Point(x + (nx * d), y + (ny * d));
        }
    }

    /// <summary>The number of points of the last outline (tests).</summary>
    internal int OutlineCount => _outlineCount;

    /// <summary>The topmost point of the last outline within ±<paramref name="reach"/> of <paramref name="x"/> (tests).</summary>
    internal double OutlineTopNear(double x, double reach)
    {
        var top = double.PositiveInfinity;
        for (var i = 0; i < _outlineCount; i++)
        {
            if (Math.Abs(_outline[i].X - x) <= reach)
            {
                top = Math.Min(top, _outline[i].Y);
            }
        }
        return top;
    }

    private StreamGeometry OutlineGeometry()
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.SetFillRule(FillRule.NonZero);
        ctx.BeginFigure(_outline[0], true);
        for (var i = 1; i < _outlineCount; i++)
        {
            ctx.LineTo(_outline[i]);
        }
        ctx.EndFigure(true);
        return geometry;
    }

    /// <summary>
    /// The liquid: a capsule from <paramref name="x0"/> to the tail (radius = the thumb's), a swollen head at the tail
    /// that blends into the body over 50 px, and the neck from the tail to the thumb that thins as they pull apart. One
    /// geometry, all figures clockwise, so it fills as their union.
    /// </summary>
    private StreamGeometry FillGeometry(double x0, double cy, double thumbX, double tailX, bool full)
    {
        var r0 = Radius;
        var head = _vHead;
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.SetFillRule(FillRule.NonZero);
        var a = x0 + r0;
        var b = Math.Max(a, tailX);
        AddCapsule(ctx, a, b, cy, r0);
        if (head > r0 + 0.05)
        {
            AddCapsule(ctx, tailX, tailX, cy, head);
            var span = 50 * Scale;
            var from = Math.Max(a, tailX - span);
            if (tailX - from > 1)
            {
                const int n = 16;
                ctx.BeginFigure(new Point(from, cy - SwellHalf(from)), true);
                for (var i = 1; i <= n; i++)
                {
                    var x = from + ((tailX - from) * i / n);
                    ctx.LineTo(new Point(x, cy - SwellHalf(x)));
                }
                for (var i = n; i >= 0; i--)
                {
                    var x = from + ((tailX - from) * i / n);
                    ctx.LineTo(new Point(x, cy + SwellHalf(x)));
                }
                ctx.EndFigure(true);
            }

            double SwellHalf(double x)
            {
                var t = Math.Clamp((x - (tailX - span)) / span, 0, 1);
                return r0 + ((head - r0) * t * t * (3 - (2 * t)));
            }
        }
        var gap = Math.Abs(thumbX - tailX);
        if (full && gap > 0.5)
        {
            var from = Math.Min(thumbX, tailX);
            var waist = SlideToConfirmMath.NeckWaist(gap, r0);
            var n = Math.Clamp((int)(gap / 3), 6, 48);
            ctx.BeginFigure(new Point(from, cy - r0), true);
            for (var i = 1; i <= n; i++)
            {
                var u = (double)i / n;
                ctx.LineTo(new Point(from + (gap * u), cy - Neck(u)));
            }
            for (var i = n; i >= 0; i--)
            {
                var u = (double)i / n;
                ctx.LineTo(new Point(from + (gap * u), cy + Neck(u)));
            }
            ctx.EndFigure(true);

            double Neck(double u) => r0 - ((r0 - waist) * Math.Sin(Math.PI * u));
        }
        return geometry;
    }

    /// <summary>A clockwise capsule between circle centres <paramref name="a"/> and <paramref name="b"/> (a circle when equal).</summary>
    private static void AddCapsule(StreamGeometryContext ctx, double a, double b, double cy, double r)
    {
        var size = new Size(r, r);
        ctx.BeginFigure(new Point(a, cy - r), true);
        if (b > a)
        {
            ctx.LineTo(new Point(b, cy - r));
        }
        ctx.ArcTo(new Point(b + r, cy), size, 0, false, SweepDirection.Clockwise);
        ctx.ArcTo(new Point(b, cy + r), size, 0, false, SweepDirection.Clockwise);
        if (b > a)
        {
            ctx.LineTo(new Point(a, cy + r));
        }
        ctx.ArcTo(new Point(a - r, cy), size, 0, false, SweepDirection.Clockwise);
        ctx.ArcTo(new Point(a, cy - r), size, 0, false, SweepDirection.Clockwise);
        ctx.EndFigure(true);
    }

    private static StreamGeometry SpinnerGeometry(double cx, double cy, double angle)
    {
        const double r = 9;
        // A 28% arc (about 101°), rotating clockwise.
        var start = (angle - 90) * Math.PI / 180;
        var end = start + (0.28 * 2 * Math.PI);
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(new Point(cx + (r * Math.Cos(start)), cy + (r * Math.Sin(start))), false);
        ctx.ArcTo(new Point(cx + (r * Math.Cos(end)), cy + (r * Math.Sin(end))), new Size(r, r), 0, false, SweepDirection.Clockwise);
        ctx.EndFigure(false);
        return geometry;
    }

    private static StreamGeometry GlyphGeometry(double cx, double cy, double morph)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        for (var i = 0; i < 3; i++)
        {
            var (ax, ay) = SlideToConfirmMath.Chevron[i];
            var (bx, by) = SlideToConfirmMath.Check[i];
            var p = new Point(cx + ax + ((bx - ax) * morph), cy + ay + ((by - ay) * morph));
            if (i == 0)
            {
                ctx.BeginFigure(p, false);
            }
            else
            {
                ctx.LineTo(p);
            }
        }
        ctx.EndFigure(false);
        return geometry;
    }
}
