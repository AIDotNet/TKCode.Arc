using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Layout and the one paint function of <see cref="WindRoseChart"/>.</summary>
public partial class WindRoseChart
{
    private const double CompactWidth = 360;
    private const double MinRadius = 36;
    private const double Inset = 4;

    /// <summary>Where the ramp passes through the series hue itself; lighter (or darker, on a dark surface) steps below, deeper above.</summary>
    private const double RampPivot = 0.6;

    private readonly record struct SegmentKey(Point Center, double Inner, double Outer, double T0, double T1);

    private ChartsBRun[] _labelRuns = [];
    private Point[] _labelOrigins = [];
    private Rect[] _labelBoxes = [];
    private ChartsBRun[] _ringRuns = [];
    private double[] _ringValues = [];
    private SegmentKey[] _segmentKeys = [];
    private StreamGeometry?[] _segments = [];
    private SegmentKey _envelopeKey;
    private StreamGeometry? _envelope;
    private Color[] _colors = [];
    private int _colorVersion = -1;
    private int _labelVersion = -1;
    private bool _labelsStale = true;
    private int _ringVersion = -1;
    private bool _ringTextsStale = true;
    private ChartsBRun? _calmLabel;
    private ChartsBRun? _calmValue;
    private int _centerVersion = -1;
    private bool _centerStale = true;
    private FormattedText? _emptyText;
    private int _emptyVersion = -1;
    private bool _compact;
    private double _gap;
    private double _radius;
    private Point _center;
    private double _roseHeight;
    private double _labelWidth;
    private double _labelHeight;
    private int _labelStep = 1;

    /// <summary>+1 when the sectors run clockwise on screen, -1 when they run counter-clockwise.</summary>
    private int Direction => SweepDirection == SweepDirection.CounterClockwise ? -1 : 1;

    private double HoleRadius => _radius * ChartsBMath.Clamp(HoleRatio, 0, 0.6);

    internal Point RoseCenter => _center;
    internal double RoseRadius => _radius;
    internal double CalmRadius => HoleRadius;
    internal bool IsCompact => _compact;
    internal int LabelStep => _labelStep;

    /// <summary>Whether a direction label is drawn now (labels thin out when they would collide; the sector being read always shows).</summary>
    internal bool IsLabelShown(int sector) => sector % Math.Max(1, _labelStep) == 0 || sector == EffectiveActive;

    internal Rect LabelBox(int sector) => sector < _labelBoxes.Length ? _labelBoxes[sector] : default;

    /// <summary>Where a sector is centred, in turns clockwise from the top of the screen.</summary>
    internal double CenterTurn(int sector) => StartAngle / 360 + Direction * (double)sector / Math.Max(1, _model.SectorCount);

    /// <summary>Unit vector from the centre through the middle of a sector, in screen coordinates.</summary>
    private (double X, double Y) Ray(int sector)
    {
        var angle = CenterTurn(sector) * 2 * Math.PI - Math.PI / 2;
        return (Math.Cos(angle), Math.Sin(angle));
    }

    /// <summary>A point in a sector at a fraction of the radius between the calm circle (0) and the outer ring (1), for tests and tooltips.</summary>
    internal Point PointIn(int sector, double fraction)
    {
        var (ux, uy) = Ray(sector);
        var hole = HoleRadius;
        var r = hole + (_radius - hole) * fraction;
        return new Point(_center.X + ux * r, _center.Y + uy * r);
    }

    /// <summary>Distance from the centre of a displayed value on an axis whose outer ring is <paramref name="maximum"/>.</summary>
    internal double RadiusOf(double value, double maximum)
    {
        var hole = HoleRadius;
        return hole + (_radius - hole) * ChartsBMath.Clamp(maximum > 0 ? value / maximum : 0, 0, 1);
    }

    /// <summary>The ring values drawn when the axis is at rest (the outer ring last).</summary>
    internal IReadOnlyList<double> RingValues
    {
        get
        {
            Flush();
            return RingsOf(_model.Maximum);
        }
    }

    private double[] RingsOf(double maximum)
    {
        var step = _model.Step;
        var rings = new List<double>();
        if (step > 0 && maximum > 0)
        {
            for (var k = 1; k <= 50; k++)
            {
                var value = k * step;
                if (value >= maximum - step * 1e-6)
                {
                    break;
                }
                rings.Add(value);
            }
        }
        rings.Add(maximum);
        return rings.ToArray();
    }

    // ---- ink and text -----------------------------------------------------------------------------------------

    /// <summary>The color of a speed bin: its own, or its step of the sequential ramp.</summary>
    internal Color BinColor(int bin)
    {
        EnsureColors();
        return bin >= 0 && bin < _colors.Length ? _colors[bin] : _palette.TextMuted;
    }

    private void EnsureColors()
    {
        if (_colorVersion == _palette.Version && _colors.Length == _bins.Count)
        {
            return;
        }
        _colorVersion = _palette.Version;
        _colors = new Color[_bins.Count];
        for (var k = 0; k < _colors.Length; k++)
        {
            _colors[k] = _bins[k].Color ?? Ramp(k, _colors.Length);
        }
    }

    /// <summary>
    /// One hue, ordered by lightness: from a light tint of <c>Series1</c> through the hue to a deep shade on a light surface,
    /// and from a dark tint to a bright shade on a dark one, because the ends mix with the surface and the foreground.
    /// </summary>
    private Color Ramp(int bin, int count)
    {
        var p = _palette;
        var hue = p.Series(0);
        var t = count <= 1 ? RampPivot : (double)bin / (count - 1);
        var low = ChartsBMath.MixIn(hue, 0.38, p.Surface);
        var high = ChartsBMath.MixIn(hue, 0.55, p.Foreground);
        return t <= RampPivot
            ? Oklch.MixOklab(low, hue, t / RampPivot)
            : Oklch.MixOklab(hue, high, (t - RampPivot) / (1 - RampPivot));
    }

    private void EnsureLabels()
    {
        if (!_labelsStale && _labelVersion == _palette.Version && _labelRuns.Length == _labels.Length)
        {
            return;
        }
        _labelsStale = false;
        _labelVersion = _palette.Version;
        foreach (var run in _labelRuns)
        {
            run.Dispose();
        }
        _labelRuns = new ChartsBRun[_labels.Length];
        _labelWidth = 0;
        _labelHeight = 0;
        for (var i = 0; i < _labels.Length; i++)
        {
            var run = ChartsBText.Run(_labels[i], _palette, _palette.TypeXS, _palette.TextSecondary);
            _labelRuns[i] = run;
            _labelWidth = Math.Max(_labelWidth, run.Width);
            _labelHeight = Math.Max(_labelHeight, run.Height);
        }
        if (_labelOrigins.Length != _labels.Length)
        {
            _labelOrigins = new Point[_labels.Length];
            _labelBoxes = new Rect[_labels.Length];
        }
    }

    private void EnsureRingTexts()
    {
        if (!_ringTextsStale && _ringVersion == _palette.Version)
        {
            return;
        }
        _ringTextsStale = false;
        _ringVersion = _palette.Version;
        foreach (var run in _ringRuns)
        {
            run.Dispose();
        }
        _ringValues = RingsOf(_model.Maximum);
        _ringRuns = new ChartsBRun[_ringValues.Length];
        var digits = Digits(_model.Step);
        for (var i = 0; i < _ringValues.Length; i++)
        {
            var value = _ringValues[i];
            var text = ValueFormatter?.Invoke(value) ?? value.ToString(digits > 0 ? "#,0." + new string('#', digits) : "#,0", ChartsBMath.EnUs) +
                (_model.IsPercent ? "%" : string.Empty);
            _ringRuns[i] = ChartsBText.Run(text, _palette, _palette.TypeXS, _palette.TextMuted);
        }
    }

    /// <summary>Fraction digits that show every multiple of <paramref name="step"/> exactly (2.5 needs one, 0.25 two).</summary>
    private static int Digits(double step)
    {
        var digits = 0;
        while (digits < 4 && Math.Abs(step * Math.Pow(10, digits) - Math.Round(step * Math.Pow(10, digits))) > 1e-6)
        {
            digits++;
        }
        return digits;
    }

    private void EnsureCenter()
    {
        if (!_centerStale && _centerVersion == _palette.Version && _calmLabel is not null)
        {
            return;
        }
        _centerStale = false;
        _centerVersion = _palette.Version;
        _calmLabel?.Dispose();
        _calmValue?.Dispose();
        _calmLabel = ChartsBText.Run(CalmText, _palette, _palette.TypeXS, _palette.TextSecondary);
        _calmValue = ChartsBText.Run(Format(_model.CalmDisplay), _palette, _palette.TypeSM, _palette.Foreground, FontWeight.Medium);
    }

    // ---- layout -------------------------------------------------------------------------------------------------

    /// <summary>Finds the largest rose, up to <see cref="Size"/>, whose direction labels fit the box, and how far labels thin out.</summary>
    private void Fit(double width, double height)
    {
        _compact = width < CompactWidth;
        _gap = _compact ? CompactLabelGap : LabelGap;
        EnsureLabels();
        var radius = Math.Max(MinRadius, Size / 2);
        radius = Math.Min(radius, width / 2 - Inset - _gap - _labelWidth);
        if (double.IsFinite(height))
        {
            radius = Math.Min(radius, (height - 2 * Inset) / 2 - _gap - _labelHeight);
        }
        _radius = Math.Max(MinRadius, radius);
        _roseHeight = 2 * (_radius + _gap + _labelHeight + Inset);
        _center = new Point(width / 2, Inset + _labelHeight + _gap + _radius);
        _labelStep = LabelStepFor(_labels.Length, _radius + _gap + _labelHeight / 2, _labelWidth + 6);
    }

    /// <summary>
    /// The smallest step between drawn labels that keeps neighbours apart. Steps divide the count, and when the count is a
    /// multiple of four they divide a quarter of it, so the four cardinal points always stay.
    /// </summary>
    internal static int LabelStepFor(int count, double ringRadius, double labelWidth)
    {
        if (count <= 0)
        {
            return 1;
        }
        var pitch = 2 * Math.PI * Math.Max(1, ringRadius) / count;
        var limit = count % 4 == 0 ? count / 4 : count;
        for (var step = 1; step <= limit; step++)
        {
            if (limit % step == 0 && pitch * step >= labelWidth)
            {
                return step;
            }
        }
        return limit;
    }

    private void PlaceLabels()
    {
        for (var i = 0; i < _labelRuns.Length; i++)
        {
            var run = _labelRuns[i];
            var (ux, uy) = Ray(i);
            var w = run.Width;
            var h = run.Height;
            // The box touches the label ring at the point nearest the centre, so labels at every angle keep the same gap.
            var reach = Math.Min(Math.Abs(ux) > 1e-6 ? w / 2 / Math.Abs(ux) : double.PositiveInfinity,
                Math.Abs(uy) > 1e-6 ? h / 2 / Math.Abs(uy) : double.PositiveInfinity);
            var distance = _radius + _gap + reach;
            var cx = _center.X + ux * distance;
            var cy = _center.Y + uy * distance;
            _labelOrigins[i] = new Point(cx - w / 2, cy - h / 2);
            _labelBoxes[i] = new Rect(cx - w / 2, cy - h / 2, w, h);
        }
    }

    private void OnGeometryChanged()
    {
        InvalidateMeasure();
        InvalidateVisual();
        SyncTooltip();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? PreferredWidth() : availableSize.Width;
        _legend.IsVisible = IsLegendVisible && !IsEmpty;
        _legend.Measure(new Size(width, double.PositiveInfinity));
        _overlay.Measure(new Size(width, double.PositiveInfinity));
        if (IsEmpty)
        {
            return new Size(width, Math.Min(availableSize.Height, 160));
        }
        var legend = LegendHeight();
        Fit(width, double.IsInfinity(availableSize.Height) ? double.PositiveInfinity : availableSize.Height - legend);
        return new Size(width, _roseHeight + legend);
    }

    private double PreferredWidth()
    {
        EnsureLabels();
        return Size + 2 * (LabelGap + _labelWidth + Inset);
    }

    private double LegendHeight() => _legend.IsVisible ? _legend.DesiredSize.Height + LegendGap : 0;

    protected override Size ArrangeOverride(Size finalSize)
    {
        var legend = LegendHeight();
        _overlay.Arrange(new Rect(finalSize));
        if (IsEmpty)
        {
            _legend.Arrange(default);
            SyncTooltip();
            return finalSize;
        }
        Fit(finalSize.Width, finalSize.Height - legend);
        var spare = Math.Max(0, finalSize.Height - _roseHeight - legend) / 2;
        _center = _center.WithY(_center.Y + spare);
        _legend.Arrange(_legend.IsVisible ? new Rect(0, spare + _roseHeight + LegendGap, finalSize.Width, _legend.DesiredSize.Height) : default);
        PlaceLabels();
        SyncTooltip();
        return finalSize;
    }

    // ---- painting -----------------------------------------------------------------------------------------------

    /// <summary>
    /// The only paint function: a resting chart, a morph frame and the first-view growth all draw through here. A band's
    /// geometry is rebuilt only when its numbers change, so a chart at rest builds nothing.
    /// </summary>
    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0)
        {
            return;
        }
        var p = _palette;
        if (IsEmpty)
        {
            if (_emptyText is null || _emptyVersion != p.Version)
            {
                _emptyText = ChartsBText.Make(EmptyLabel, p, p.TypeSM, p.TextMuted);
                _emptyVersion = p.Version;
            }
            context.DrawText(_emptyText, new Point((Bounds.Width - _emptyText.Width) / 2, (Bounds.Height - _emptyText.Height) / 2));
            return;
        }
        if (!_reveal.IsRevealed || _radius <= 0)
        {
            return;
        }
        EnsureColors();
        EnsureLabels();
        using var fade = context.PushOpacity(_reveal.Fade);
        var maximum = Math.Max(1e-9, _maximum.Value);
        PaintGrid(context, maximum);
        PaintPetals(context, maximum);
        PaintCenter(context);
        PaintRingLabels(context, maximum);
        PaintDirectionLabels(context);
    }

    private void PaintGrid(DrawingContext context, double maximum)
    {
        var p = _palette;
        var ringPen = p.Pen(ChartsBMath.Alpha(p.BorderStrong, 0.5), 1);
        var outerPen = p.Pen(p.BorderStrong, 1);
        var hole = HoleRadius;
        var step = _model.Step;
        if (step > 0)
        {
            for (var k = 1; k <= 50; k++)
            {
                var r = RadiusOf(k * step, maximum);
                if (k * step >= maximum - step * 1e-6 || r > _radius - 0.5)
                {
                    break;
                }
                context.DrawEllipse(null, ringPen, _center, r, r);
            }
        }
        for (var i = 0; i < _labelRuns.Length; i++)
        {
            if (i % Math.Max(1, _labelStep) != 0)
            {
                continue;
            }
            var (ux, uy) = Ray(i);
            context.DrawLine(ringPen, new Point(_center.X + ux * hole, _center.Y + uy * hole), new Point(_center.X + ux * _radius, _center.Y + uy * _radius));
        }
        context.DrawEllipse(null, outerPen, _center, _radius, _radius);
        if (hole > 1)
        {
            context.DrawEllipse(null, outerPen, _center, hole, hole);
        }
    }

    private void PaintPetals(DrawingContext context, double maximum)
    {
        var p = _palette;
        var count = _model.SectorCount;
        var bins = _model.BinCount;
        var values = _springs.Values;
        if (values.Length != count * bins || bins == 0)
        {
            return;
        }
        var half = ChartsBMath.Clamp(SectorFill, 0.05, 1) / (2 * count);
        var gap = Math.Max(0, SegmentGap);
        var corner = Math.Max(0, SegmentCorner);
        var active = EffectiveActive;
        var dim = _dim.Value;
        var binDim = _binDim.Value;
        var highlight = _highlightBin;
        var selection = EffectiveSelected;
        for (var s = 0; s < count; s++)
        {
            var center = CenterTurn(s);
            var t0 = center - half;
            var t1 = center + half;
            // The selected petal stays strong while another one is read, so the choice stays in view.
            var sectorAlpha = active < 0 || s == active || s == selection ? 1 : 1 - 0.55 * dim;
            var below = 0.0;
            for (var k = 0; k < bins; k++)
            {
                var at = s * bins + k;
                var top = Math.Max(below, values[at]);
                var inner = RadiusOf(below, maximum) + gap / 2;
                var outer = RadiusOf(top, maximum) - gap / 2;
                below = top;
                if (outer - inner < 0.6)
                {
                    continue;
                }
                var geometry = Segment(at, inner, outer, t0, t1, corner);
                if (geometry is null)
                {
                    continue;
                }
                var alpha = sectorAlpha * (highlight < 0 || highlight == k ? 1 : 1 - 0.65 * binDim);
                if (alpha < 0.999)
                {
                    using (context.PushOpacity(alpha))
                    {
                        context.DrawGeometry(p.Brush(_colors[k]), null, geometry);
                    }
                }
                else
                {
                    context.DrawGeometry(p.Brush(_colors[k]), null, geometry);
                }
            }
        }
        if (selection >= 0)
        {
            var top = Math.Max(0, values[selection * bins + bins - 1]);
            var c = CenterTurn(selection);
            var key = new SegmentKey(_center, HoleRadius, Math.Max(HoleRadius + 1, RadiusOf(top, maximum)), c - half, c + half);
            if (_envelope is null || _envelopeKey != key)
            {
                _envelopeKey = key;
                _envelope = ChartGeometry.Sector(_center, key.Outer, Math.Max(0.5, key.Inner), key.T0, key.T1, 0, corner);
            }
            if (_envelope is not null)
            {
                context.DrawGeometry(null, p.Pen(p.Foreground, 2), _envelope);
            }
        }
        if (active >= 0 && ActiveBin is var bin and >= 0 && bin < bins && _segments[active * bins + bin] is { } lit)
        {
            context.DrawGeometry(null, p.Pen(p.Foreground, 1.5), lit);
        }
    }

    private StreamGeometry? Segment(int at, double inner, double outer, double t0, double t1, double corner)
    {
        var key = new SegmentKey(_center, inner, outer, t0, t1);
        if (_segmentKeys[at] != key || _segments[at] is null)
        {
            _segmentKeys[at] = key;
            _segments[at] = ChartGeometry.Sector(_center, outer, Math.Max(0.5, inner), t0, t1, 0, corner);
        }
        return _segments[at];
    }

    /// <summary>"Calm" over its share, when the calm circle is wide enough to hold them; the value alone when only it fits.</summary>
    private void PaintCenter(DrawingContext context)
    {
        EnsureCenter();
        var room = HoleRadius * 2 - 6;
        if (_calmLabel is not { } label || _calmValue is not { } value || room < value.Width)
        {
            return;
        }
        var showLabel = room >= label.Width && HoleRadius * 2 - 4 >= label.Height + value.Height;
        var height = showLabel ? label.Height + value.Height : value.Height;
        var top = _center.Y - height / 2;
        if (showLabel)
        {
            label.Draw(context, new Point(_center.X - label.Width / 2, top));
            top += label.Height;
        }
        value.Draw(context, new Point(_center.X - value.Width / 2, top));
    }

    /// <summary>Ring values along the boundary between sector 0 and its neighbour, each on a surface pill so a petal under it does not hide it.</summary>
    private void PaintRingLabels(DrawingContext context, double maximum)
    {
        EnsureRingTexts();
        var p = _palette;
        var count = Math.Max(1, _model.SectorCount);
        var angle = (CenterTurn(0) + Direction * 0.5 / count) * 2 * Math.PI - Math.PI / 2;
        var ux = Math.Cos(angle);
        var uy = Math.Sin(angle);
        var hole = HoleRadius;
        var spacing = (_radius - hole) / Math.Max(1, _ringValues.Length);
        var every = _ringRuns.Length > 0 && spacing < _ringRuns[0].Height + 2 ? 2 : 1;
        var pill = p.Brush(p.Surface, 0.85);
        for (var i = _ringRuns.Length - 1; i >= 0; i--)
        {
            // Thinned from the outer ring inward, so the maximum always reads.
            if ((_ringRuns.Length - 1 - i) % every != 0)
            {
                continue;
            }
            var value = _ringValues[i];
            if (value > maximum * 1.0001)
            {
                continue;
            }
            var run = _ringRuns[i];
            var r = RadiusOf(value, maximum);
            var cx = _center.X + ux * r;
            var cy = _center.Y + uy * r;
            var box = new Rect(cx - run.Width / 2 - 3, cy - run.Height / 2, run.Width + 6, run.Height);
            context.DrawRectangle(pill, null, new RoundedRect(box, box.Height / 2));
            run.Draw(context, new Point(cx - run.Width / 2, cy - run.Height / 2));
        }
    }

    private void PaintDirectionLabels(DrawingContext context)
    {
        var active = EffectiveActive;
        var foreground = _palette.Brush(_palette.Foreground);
        for (var i = 0; i < _labelRuns.Length && i < _labelOrigins.Length; i++)
        {
            if (!IsLabelShown(i))
            {
                continue;
            }
            if (i == active)
            {
                _labelRuns[i].Draw(context, _labelOrigins[i], foreground);
            }
            else
            {
                _labelRuns[i].Draw(context, _labelOrigins[i]);
            }
        }
    }

    /// <summary>Above the legend: the keyboard focus ring around the rose and the tooltip.</summary>
    private void PaintOverlay(DrawingContext context)
    {
        var p = _palette;
        if (IsFocused && Classes.Contains(":focus-visible") && !IsEmpty && _radius > 0)
        {
            context.DrawEllipse(null, p.Pen(p.FocusRing, 2), _center, _radius + 4, _radius + 4);
        }
        _tooltip.Draw(context, p);
    }
}
