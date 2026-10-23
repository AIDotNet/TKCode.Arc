using System.Globalization;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

internal sealed partial class RevenueGlobeCanvas
{
    /// <summary>The sphere rotation of one frame, inlined (the same formula as <see cref="ShowpieceSphereView"/>).</summary>
    private readonly struct Projector
    {
        private readonly double _cosLon, _sinLon, _cosLat, _sinLat, _cx, _cy, _r;

        public Projector(in ShowpieceSphereView view)
        {
            _cosLon = Math.Cos(view.CenterLongitude);
            _sinLon = Math.Sin(view.CenterLongitude);
            _cosLat = Math.Cos(view.CenterLatitude);
            _sinLat = Math.Sin(view.CenterLatitude);
            _cx = view.Center.X;
            _cy = view.Center.Y;
            _r = view.Radius;
        }

        public double Project(double x, double y, double z, out double sx, out double sy)
        {
            var a = x * _cosLon + y * _sinLon;
            var b = -x * _sinLon + y * _cosLon;
            sx = _cx + _r * b;
            sy = _cy - _r * (z * _cosLat - a * _sinLat);
            return z * _sinLat + a * _cosLat;
        }

        public double Project(in ShowpieceVec3 p, out Point screen)
        {
            var depth = Project(p.X, p.Y, p.Z, out var sx, out var sy);
            screen = new Point(sx, sy);
            return depth;
        }
    }

    private void EnsureColors()
    {
        if (_colorsValid)
        {
            return;
        }
        _colorsValid = true;
        _foreground = ArcTokens.GetColor(this, ArcDesignTokenKind.Foreground);
        _border = ArcTokens.GetColor(this, ArcDesignTokenKind.Border);
        _accent = ArcTokens.GetColor(this, ArcDesignTokenKind.Accent);
        _background = ArcTokens.GetColor(this, ArcDesignTokenKind.Background);
        _textSecondary = ArcTokens.GetColor(this, ArcDesignTokenKind.TextSecondary);
        _brushes.Clear();
        _pens.Clear();
        _labels.Clear();
        FontFamily family;
        try
        {
            family = ArcTokens.Get<FontFamily>(this, ArcDesignTokenKind.BodyFontFamily) ?? FontFamily.Default;
        }
        catch (Exception)
        {
            family = FontFamily.Default;
        }
        _labelTypeface = new Typeface(family);
        _haloPen = new ImmutablePen(new ImmutableSolidColorBrush(_background), 3.5, null, PenLineCap.Round, PenLineJoin.Round);
        _focusPen = null;
    }

    private IImmutableSolidColorBrush Brush(Color color, double alpha)
    {
        var a = (byte)Math.Clamp(Math.Round(color.A * Math.Clamp(alpha, 0, 1)), 0, 255);
        var key = ((uint)a << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
        if (!_brushes.TryGetValue(key, out var brush))
        {
            brush = new ImmutableSolidColorBrush(Color.FromArgb(a, color.R, color.G, color.B));
            _brushes[key] = brush;
        }
        return brush;
    }

    private IPen Pen(Color color, double alpha, double width)
    {
        var a = (byte)Math.Clamp(Math.Round(color.A * Math.Clamp(alpha, 0, 1)), 0, 255);
        var key = (((uint)a << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B, (int)Math.Round(width * 20));
        if (!_pens.TryGetValue(key, out var pen))
        {
            pen = new ImmutablePen(Brush(color, alpha), key.Item2 / 20.0, null, PenLineCap.Round, PenLineJoin.Round);
            _pens[key] = pen;
        }
        return pen;
    }

    private Geometry? Label(string text)
    {
        if (!_labels.TryGetValue(text, out var geometry))
        {
            var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _labelTypeface, 12, Brushes.Black);
            // Origin on the alphabetic baseline, so a label sits at "point + (dx, +4)" like canvas fillText.
            geometry = formatted.BuildGeometry(new Point(0, -formatted.Baseline));
            _labels[text] = geometry;
        }
        return geometry;
    }

    protected override void RenderStage(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        EnsureColors();
        var view = View;
        var projector = new Projector(view);
        var r = view.Radius;
        var scale = r / RevenueGlobeMath.ReferenceRadius;
        var center = view.Center;

        // 1. The disc: Foreground at 3% and a 1px Border outline (no gradient, glow or graticule).
        context.DrawEllipse(Brush(_foreground, 0.03), Pen(_border, 1, 1), center, r, r);

        // 2. Land dots, 8 depth buckets far to near; 3. sea squares, the same buckets.
        DrawDots(context, projector, _land.LandPoints.Span, scale, land: true);
        DrawDots(context, projector, _land.OceanPoints.Span, scale, land: false);

        // 4. Cities.
        DrawCities(context, projector, scale);

        // 5. Arcs, 6. rings.
        if (_stillArcs.Length > 0)
        {
            DrawStillArcs(context, projector, r);
        }
        DrawArcs(context, projector, r);
        DrawRings(context, projector);

        // 7. HQ, 8. hover / selection rings and region labels.
        DrawHeadquarters(context, projector);
        DrawHighlights(context, projector, scale);

        // The globe area is clipped by the block, so its keyboard focus ring hugs the inside of the area.
        if (IsFocused && Classes.Contains(":focus-visible"))
        {
            _focusPen ??= new ImmutablePen(new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.FocusRing)), 2);
            context.DrawRectangle(null, _focusPen, new Rect(size).Deflate(1));
        }
    }

    private void DrawDots(DrawingContext context, in Projector projector, ReadOnlySpan<float> points, double scale, bool land)
    {
        var n = points.Length / 3;
        if (n == 0)
        {
            return;
        }
        if (_dotX.Length < n)
        {
            _dotX = new float[n];
            _dotY = new float[n];
            _order = new int[n];
            _dotBucket = new byte[n];
        }
        Span<int> counts = stackalloc int[RevenueGlobeMath.Buckets];
        counts.Clear();
        var buckets = _dotBucket;
        for (var i = 0; i < n; i++)
        {
            var depth = projector.Project(points[i * 3], points[i * 3 + 1], points[i * 3 + 2], out var sx, out var sy);
            if (depth <= 0)
            {
                buckets[i] = 255;
                continue;
            }
            var b = RevenueGlobeMath.Bucket(depth);
            buckets[i] = (byte)b;
            counts[b]++;
            _dotX[i] = (float)sx;
            _dotY[i] = (float)sy;
        }
        _bucketStart[0] = 0;
        for (var b = 0; b < RevenueGlobeMath.Buckets; b++)
        {
            _bucketStart[b + 1] = _bucketStart[b] + counts[b];
            _bucketFill[b] = _bucketStart[b];
        }
        for (var i = 0; i < n; i++)
        {
            var b = buckets[i];
            if (b != 255)
            {
                _order[_bucketFill[b]++] = i;
            }
        }
        for (var b = 0; b < RevenueGlobeMath.Buckets; b++)
        {
            var from = _bucketStart[b];
            var to = _bucketStart[b + 1];
            if (from == to)
            {
                continue;
            }
            if (land)
            {
                var brush = Brush(_foreground, RevenueGlobeMath.LandDotAlpha(b));
                var radius = RevenueGlobeMath.LandDotRadius(b) * scale;
                for (var k = from; k < to; k++)
                {
                    var i = _order[k];
                    context.DrawEllipse(brush, null, new Point(_dotX[i], _dotY[i]), radius, radius);
                }
            }
            else
            {
                var brush = Brush(_foreground, RevenueGlobeMath.OceanDotAlpha(b));
                var side = RevenueGlobeMath.OceanSquareSize * scale;
                var half = side / 2;
                for (var k = from; k < to; k++)
                {
                    var i = _order[k];
                    context.DrawRectangle(brush, null, new Rect(_dotX[i] - half, _dotY[i] - half, side, side));
                }
            }
        }
    }

    private double CityDim(int city)
    {
        if (SelectedRegion < 0)
        {
            return 1;
        }
        return _scene.CityRegion[city] == SelectedRegion ? 1 : 0.2;
    }

    private void DrawCities(DrawingContext context, in Projector projector, double scale)
    {
        var points = _scene.CityPoints;
        for (var i = 0; i < points.Length; i++)
        {
            if (i == _scene.HqIndex)
            {
                continue;
            }
            var depth = projector.Project(points[i], out var p);
            if (depth <= 0)
            {
                continue;
            }
            var radius = RevenueGlobeMath.CityDotRadius(_scene.CityRevenue[i], _scene.MaxCityRevenue) * scale;
            context.DrawEllipse(Brush(_foreground, RevenueGlobeMath.CityDotAlpha(depth) * CityDim(i)), null, p, radius, radius);
        }
    }

    private void DrawArcs(DrawingContext context, in Projector projector, double r)
    {
        for (var a = 0; a < _arcCount; a++)
        {
            ref readonly var arc = ref _arcs[a];
            ArcSpan(arc.Age, arc.Duration, out var tail, out var head, out var fade, out var flying);
            var dim = CityDim(arc.City);
            var width = 1 + arc.Weight;
            // Ground line first (front side only), then the lifted arc in back-side tiers.
            DrawPolyline(context, projector, _scene.ArcGround[arc.City], tail, head, _foreground, (0.04 + 0.06 * arc.Weight) * fade * dim, 1, ground: true);
            var headDepth = DrawPolyline(context, projector, _scene.ArcLifted[arc.City], tail, head, _accent, RevenueGlobeMath.ArcAlpha * fade * dim, width,
                ground: false, out var headPoint);
            if (flying)
            {
                var tier = RevenueGlobeMath.ArcTier(headDepth);
                var radius = 2 + 0.9 * arc.Weight;
                context.DrawEllipse(Brush(_accent, RevenueGlobeMath.ArcAlpha * tier / 4 * dim), null, headPoint, radius, radius);
            }
        }
    }

    private void DrawStillArcs(DrawingContext context, in Projector projector, double r)
    {
        const double last = RevenueGlobeMath.ArcPoints - 1;
        foreach (var (city, _) in _stillArcs)
        {
            if (!_scene.HasArc(city))
            {
                continue;
            }
            DrawPolyline(context, projector, _scene.ArcLifted[city], 0, last, _accent, RevenueGlobeMath.StillArcAlpha * CityDim(city),
                RevenueGlobeMath.StillArcWidth, ground: false, out _);
        }
    }

    private void DrawPolyline(DrawingContext context, in Projector projector, float[] points, double tail, double head, Color color,
                              double alpha, double width, bool ground) =>
        DrawPolyline(context, projector, points, tail, head, color, alpha, width, ground, out _);

    /// <summary>
    /// Draws points [tail, head] of an arc (fractional ends interpolated). The ground line keeps only its front side; the
    /// lifted arc is cut into runs of one back-side tier, each stroked with the alpha × tier / 4. Returns the head's depth.
    /// </summary>
    private double DrawPolyline(DrawingContext context, in Projector projector, float[] points, double tail, double head, Color color,
                                double alpha, double width, bool ground, out Point headPoint)
    {
        headPoint = default;
        if (points.Length == 0 || head - tail <= 1e-6 || alpha <= 0.002)
        {
            return 0;
        }
        var first = (int)Math.Floor(tail);
        var lastIndex = (int)Math.Ceiling(head);
        var count = 0;
        var runTier = -1;
        double headDepth = 0;
        for (var k = first; k <= lastIndex; k++)
        {
            // Fractional ends: the tail and the head sit between two points.
            double t = k == first ? tail : k == lastIndex ? head : k;
            var i0 = Math.Min((int)Math.Floor(t), RevenueGlobeMath.ArcPoints - 1);
            var i1 = Math.Min(i0 + 1, RevenueGlobeMath.ArcPoints - 1);
            var f = t - i0;
            var x = points[i0 * 3] + (points[i1 * 3] - points[i0 * 3]) * f;
            var y = points[i0 * 3 + 1] + (points[i1 * 3 + 1] - points[i0 * 3 + 1]) * f;
            var z = points[i0 * 3 + 2] + (points[i1 * 3 + 2] - points[i0 * 3 + 2]) * f;
            var depth = projector.Project(x, y, z, out var sx, out var sy);
            var point = new Point(sx, sy);
            headDepth = depth;
            headPoint = point;
            var tier = ground ? (depth > 0 ? 4 : 0) : RevenueGlobeMath.ArcTier(depth);
            if (tier != runTier)
            {
                if (count > 0)
                {
                    // Close the run at this point so runs join without gaps.
                    _run[count++] = point;
                    StrokeRun(context, count, color, alpha * runTier / 4, width);
                }
                count = 0;
                runTier = tier;
            }
            _run[count++] = point;
        }
        if (count > 0)
        {
            StrokeRun(context, count, color, alpha * runTier / 4, width);
        }
        return headDepth;
    }

    private void StrokeRun(DrawingContext context, int count, Color color, double alpha, double width)
    {
        if (count < 2 || alpha <= 0.002)
        {
            return;
        }
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(_run[0], false);
            for (var i = 1; i < count; i++)
            {
                g.LineTo(_run[i]);
            }
            g.EndFigure(false);
        }
        context.DrawGeometry(null, Pen(color, alpha, width), geometry);
    }

    private void DrawRings(DrawingContext context, in Projector projector)
    {
        for (var k = 0; k < _ringCount; k++)
        {
            ref readonly var ring = ref _rings[k];
            if (projector.Project(ring.Center, out _) <= 0)
            {
                continue;
            }
            var t = ring.Age / ring.Duration;
            var angle = ring.Angle * RevenueGlobeMath.EaseOutExpo(t);
            if (angle <= 1e-5)
            {
                continue;
            }
            var alpha = (ring.Arrival ? 0.84 : 0.45) * RevenueGlobeMath.RingFade(t);
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);
            for (var j = 0; j <= RingSegments; j++)
            {
                var phi = j * 2 * Math.PI / RingSegments;
                var c = Math.Cos(phi) * sin;
                var s = Math.Sin(phi) * sin;
                var p = ring.Center * cos + ring.U * c + ring.V * s;
                projector.Project(p.X, p.Y, p.Z, out var sx, out var sy);
                _ringPoints[j] = new Point(sx, sy);
            }
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(_ringPoints[0], false);
                for (var j = 1; j <= RingSegments; j++)
                {
                    g.LineTo(_ringPoints[j]);
                }
                g.EndFigure(true);
            }
            context.DrawGeometry(null, ring.Arrival ? Pen(_accent, alpha, 1.4) : Pen(_foreground, alpha, 1), geometry);
        }
    }

    private void DrawHeadquarters(DrawingContext context, in Projector projector)
    {
        if (!_scene.HasHq)
        {
            return;
        }
        var depth = projector.Project(_scene.HqPoint, out var p);
        if (depth <= 0)
        {
            return;
        }
        var alpha = RevenueGlobeMath.HqAlpha(depth);
        context.DrawEllipse(Brush(_background, alpha), null, p, 5.2, 5.2);
        context.DrawEllipse(Brush(_accent, alpha), null, p, 3.6, 3.6);
        DrawLabel(context, _scene.Headquarters!.Name, new Point(p.X + 10, p.Y + 4), _textSecondary, alpha);
    }

    private void DrawLabel(DrawingContext context, string text, Point baseline, Color color, double alpha)
    {
        if (string.IsNullOrEmpty(text) || Label(text) is not { } geometry)
        {
            return;
        }
        using (context.PushOpacity(Math.Clamp(alpha, 0, 1)))
        using (context.PushTransform(Matrix.CreateTranslation(baseline.X, baseline.Y)))
        {
            context.DrawGeometry(null, _haloPen, geometry);
            context.DrawGeometry(Brush(color, 1), null, geometry);
        }
    }

    private void DrawHighlights(DrawingContext context, in Projector projector, double scale)
    {
        foreach (var city in LabeledCities)
        {
            if ((uint)city >= (uint)_scene.CityPoints.Length)
            {
                continue;
            }
            var depth = projector.Project(_scene.CityPoints[city], out var p);
            if (depth <= 0)
            {
                continue;
            }
            var alpha = RevenueGlobeMath.CityRingAlpha(depth);
            context.DrawEllipse(null, Pen(_foreground, alpha, 1.25), p, RevenueGlobeMath.CityRingRadius, RevenueGlobeMath.CityRingRadius);
            DrawLabel(context, _scene.Cities[city].Name, new Point(p.X + 11, p.Y + 4), _foreground, Math.Clamp(depth * 1.2, 0, 1));
        }
        DrawCityRing(context, projector, SelectedCity);
        var hover = _hoverCity >= 0 ? _hoverCity : _listHoverCity;
        if (hover != SelectedCity)
        {
            DrawCityRing(context, projector, hover);
        }
    }

    private void DrawCityRing(DrawingContext context, in Projector projector, int city)
    {
        if ((uint)city >= (uint)_scene.CityPoints.Length)
        {
            return;
        }
        var depth = projector.Project(_scene.CityPoints[city], out var p);
        if (depth <= 0)
        {
            return;
        }
        context.DrawEllipse(null, Pen(_foreground, RevenueGlobeMath.CityRingAlpha(depth), 1.25), p,
            RevenueGlobeMath.CityRingRadius, RevenueGlobeMath.CityRingRadius);
    }
}
