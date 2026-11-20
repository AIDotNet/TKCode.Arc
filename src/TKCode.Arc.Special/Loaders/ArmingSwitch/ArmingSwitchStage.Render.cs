using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

// The drawing, back to front: the plate and everything printed on it (one perspective plane), the toggle assembly at
// its small heights, the hinge barrel, then the cover's five translucent faces sorted by depth. Every flat part is drawn
// in its own units through the projective matrix of its plane.
internal sealed partial class ArmingSwitchStage
{
    private static readonly Point LampCentre = new(0.73, 0.3);
    private static readonly Point Pivot = new(0.32, 0.34);
    private const double PivotZ = 0.016;
    private const double RingRadius = 0.1288;

    // Cover shadow by hinge angle (Arc, measured): angle, scaleY, x and y offset (W), opacity.
    private static readonly double[] ShadowAngles = { 0, 1.7, 5.6, 10.1, 16.2, 23.5, 32, 41.2, 51.2, 61.6, 72.3, 83.2, 94, 96 };
    private static readonly double[] ShadowScaleY = { 1.015, 1.0236, 1.0398, 1.0527, 1.0596, 1.0522, 1.0223, 0.9639, 0.8732, 0.7508, 0.5985, 0.4231, 0.2326, 0.1967 };
    private static readonly double[] ShadowX = { 0.0288, 0.0299, 0.0323, 0.0350, 0.0384, 0.0422, 0.0460, 0.0493, 0.0519, 0.0534, 0.0536, 0.0524, 0.0498, 0.0492 };
    private static readonly double[] ShadowY = { 0.1620, 0.1596, 0.1541, 0.1476, 0.1388, 0.1281, 0.1160, 0.1033, 0.0907, 0.0791, 0.0691, 0.0614, 0.0565, 0.0559 };
    private static readonly double[] ShadowOpacity = { 0.806, 0.789, 0.752, 0.710, 0.657, 0.599, 0.541, 0.489, 0.449, 0.426, 0.423, 0.441, 0.481, 0.490 };
    private static readonly double[] GlareY = { -0.1568, -0.1504, -0.1360, -0.1191, -0.0965, -0.0690, -0.0374, -0.0028, 0.0344, 0.0731, 0.1132, 0.1537, 0.1942, 0.2016 };
    private static readonly double[] GlareOpacity = { 0.42, 0.531, 0.721, 0.771, 0.844, 0.959, 0.999, 0.923, 0.72, 0.43, 0.42, 0.42, 0.42, 0.42 };

    private static readonly double[] EarX = { 0.166, 0.448 };
    private static readonly Geometry Hexagon = MakeHexagon();
    private static readonly Geometry ShaftOutline = MakeShaft();
    private static readonly Geometry FacetsA = MakeFacets(0);
    private static readonly Geometry FacetsB = MakeFacets(1);
    private static readonly IBrush FacetMask = ArmingSwitchPaint.Radial(0.5, 0.5, 0.5, 0.5,
        (0, Colors.Black), (0.55, Colors.Black), (1, Colors.Transparent));

    private readonly (ArmingVec O, ArmingVec U, ArmingVec V, int Kind, double Depth)[] _faces = new (ArmingVec, ArmingVec, ArmingVec, int, double)[5];
    private StreamGeometry? _arc;
    private double _arcValue = -1;
    private StreamGeometry? _spinArc;
    private IPen? _fillAccent, _fillGreen, _fillRed, _spinPen, _headPen;
    private Color _fillAccentColour;
    private IBrush? _headFill;
    private BoxShadows _plateShadowPx;
    private double _shadowFor = -1;

    private static double Interp(double[] xs, double[] ys, double x)
    {
        if (x <= xs[0])
        {
            return ys[0];
        }
        for (var i = 1; i < xs.Length; i++)
        {
            if (x <= xs[i])
            {
                var t = (x - xs[i - 1]) / (xs[i] - xs[i - 1]);
                return ys[i - 1] + ((ys[i] - ys[i - 1]) * t);
            }
        }
        return ys[^1];
    }

    /// <summary>A point of the cover (x across, y down from its hinge edge, z up from the plate) at the current angle.</summary>
    private ArmingVec CoverPoint(double x, double y, double z)
    {
        // The hinge axis runs along x at y -0.006 W and half the box's depth (0.08 W) above the plate.
        var r = new ArmingVec(0, y + 0.006, z - 0.08).RotateX(_cov);
        return new ArmingVec(0.19 + x, 0.114 + r.Y, 0.08 + r.Z);
    }

    private ArmingVec CoverAxis(double x, double y, double z) => new ArmingVec(x, y, z).RotateX(_cov);

    private ArmingVec CoverCorner(int i) => CoverPoint((i & 1) == 0 ? 0 : 0.26, (i & 2) == 0 ? 0 : 0.4, (i & 4) == 0 ? 0 : 0.16);

    public override void Render(DrawingContext context)
    {
        var owner = Owner;
        if (owner is null || _w <= 0)
        {
            return;
        }
        EnsureLook();
        EnsureText();
        EnsureSizedInks();
        var status = owner.Status;
        DrawPlate(context, status);
        DrawAssembly(context);
        using (context.PushTransform(_view.Plane(0.08)))
        {
            context.DrawRectangle(_barrel, null, new RoundedRect(new Rect(0.172, 0.099, 0.296, 0.021), 0.0105));
        }
        DrawCover(context);
    }

    private void EnsureSizedInks()
    {
        if (Math.Abs(_shadowFor - _w) < 0.01)
        {
            return;
        }
        _shadowFor = _w;
        var drop = _plateShadow.Count > 0 ? _plateShadow[0].Color : Color.FromArgb(128, 0, 0, 0);
        _plateShadowPx = new BoxShadows(
            new BoxShadow { OffsetY = 0.026 * _w, Blur = 0.044 * _w, Spread = -0.018 * _w, Color = drop },
            new[] { new BoxShadow { OffsetY = 0.005 * _w, Blur = 0.01 * _w, Spread = -0.003 * _w, Color = drop } });
    }

    // --- plate ---

    private void DrawPlate(DrawingContext context, ArmingSwitchStatus status)
    {
        const double ph = ArmingSwitchView.PlateHeight;
        var w = _w;
        var plateRect = new Rect(0, 0, 1, ph);
        var plateRound = new RoundedRect(plateRect, 0.034);
        using (context.PushTransform(_view.Plane(0, 1 / w)))
        {
            context.DrawRectangle(_plateFill, null, new RoundedRect(new Rect(0, 0, w, ph * w), 0.034 * w), _plateShadowPx);
        }
        using (context.PushTransform(_view.Plane()))
        {
            using (context.PushClip(plateRound))
            {
                context.DrawRectangle(_plateSheen, null, plateRect);
                context.DrawRectangle(_plateChamfer, null, plateRect);
                if (_noise is not null)
                {
                    using (context.PushTransform(Matrix.CreateScale(1 / w, 1 / w)))
                    using (context.PushOpacity(_brushOpacity))
                    {
                        context.DrawRectangle(_noise, null, new Rect(0, 0, w, ph * w));
                    }
                }
                var (dx, dy) = Drift();
                context.DrawRectangle(_streak, null, new Rect(0.16 + dx, -0.0744 + dy, 0.36, 0.7688));
                context.DrawRectangle(_streakFaint, null, new Rect(0.62 + dx, -0.0744 + dy, 0.18, 0.7688));
                context.DrawRectangle(_glow, null, new Rect(-0.12, -0.1488, 1.24, 0.5332));
                DrawLights(context, spill: true);
                DrawBase(context);
                DrawCoverShadow(context);
                DrawLeverShadow(context);
                DrawEngraving(context, status);
                DrawScrews(context);
                DrawLamp(context);
                DrawGlint(context);
            }
            // Bevel: light along the top edge, dark along the bottom, and the 1 px edge ring.
            context.DrawLine(_bevelHi!, new Point(0.03, 0.00125), new Point(0.97, 0.00125));
            context.DrawLine(_bevelLo!, new Point(0.03, ph - 0.00125), new Point(0.97, ph - 0.00125));
            context.DrawRectangle(null, _plateEdge, plateRound);
        }
    }

    private void DrawBase(DrawingContext context)
    {
        var baseRect = new RoundedRect(new Rect(0.165, 0.085, 0.31, 0.47), 0.03);
        SoftRect(context, new Rect(0.17, 0.095, 0.31, 0.47), 0.03, 0.012, Brushes.Black, 0.45);
        context.DrawRectangle(_base, _baseRing, baseRect);
        context.DrawRectangle(_baseSheen, null, baseRect);
        context.DrawLine(_baseHi!, new Point(0.19, 0.0862), new Point(0.45, 0.0862));
        // Barrel shadow and the two hinge ears.
        SoftRect(context, new Rect(0.184, 0.122, 0.296, 0.026), 0.013, 0.006, _barrelShadow!, 1);
        foreach (var x in EarX)
        {
            SoftRect(context, new Rect(x + 0.005, 0.096, 0.026, 0.054), 0.009, 0.005, Brushes.Black, 0.4);
            context.DrawRectangle(_ear, null, new RoundedRect(new Rect(x, 0.089, 0.026, 0.054), 0.009));
        }
    }

    /// <summary>A rounded rect with a soft edge <paramref name="blur"/> wide, as stacked translucent copies (no blur filter in perspective).</summary>
    private static void SoftRect(DrawingContext context, Rect rect, double radius, double blur, IBrush brush, double opacity)
    {
        const int layers = 4;
        for (var i = 0; i < layers; i++)
        {
            var grow = blur * (1 - ((i + 0.5) / layers)) - (blur * 0.25);
            var r = rect.Inflate(grow);
            if (r.Width <= 0 || r.Height <= 0)
            {
                continue;
            }
            using (context.PushOpacity(opacity / layers * 1.15))
            {
                context.DrawRectangle(brush, null, new RoundedRect(r, Math.Max(0, radius + grow)));
            }
        }
    }

    private void DrawCoverShadow(DrawingContext context)
    {
        var angle = Math.Clamp(_cov, 0, CoverOpenAngle);
        var sy = Interp(ShadowAngles, ShadowScaleY, angle);
        var tx = Interp(ShadowAngles, ShadowX, angle);
        var ty = Interp(ShadowAngles, ShadowY, angle);
        var op = Interp(ShadowAngles, ShadowOpacity, angle) * _coverShadowOpacity;
        SoftRect(context, new Rect(0.19 + tx, ty, 0.26, 0.4 * sy), 0.022, 0.022, _coverShadow!, op);
        var contact = Math.Clamp(1 - (_cov / 12), 0, 1);
        if (contact > 0.01)
        {
            using (context.PushOpacity(contact))
            {
                context.DrawRectangle(null, _contact, new RoundedRect(new Rect(0.19, 0.12, 0.26, 0.4), 0.014));
            }
        }
    }

    private void DrawLeverShadow(DrawingContext context)
    {
        var r = _bat * Math.PI / 180;
        var tipY = Pivot.Y + (0.135 * Math.Sin(r));
        var tipZ = PivotZ + (0.135 * Math.Cos(r));
        var from = new Point(Pivot.X + (0.19 * PivotZ), Pivot.Y + (0.30 * PivotZ));
        var to = new Point(Pivot.X + (0.19 * tipZ), tipY + (0.30 * tipZ));
        context.DrawLine(_leverShadowSoft!, from, to);
        context.DrawLine(_leverShadow!, from, to);
    }

    private void DrawEngraving(DrawingContext context, ArmingSwitchStatus status)
    {
        var w = _w;
        // Ticks beside the labels (opacity 0.72) and the ready light.
        context.DrawRectangle(_tick, null, new RoundedRect(new Rect(0.132, 0.266 - 0.0017, 0.023, 0.0034), 0.002));
        context.DrawRectangle(_tick, null, new RoundedRect(new Rect(0.132, 0.414 - 0.0017, 0.023, 0.0034), 0.002));
        var led = new Point(0.673, 0.094);
        context.DrawEllipse(_ledOff, null, led, 0.009, 0.009);
        var beat = Heartbeat(status);
        using (context.PushOpacity(beat))
        {
            context.DrawEllipse(_bloom[1], null, led, 0.026, 0.026);
            context.DrawEllipse(_ledLit, null, led, 0.009, 0.009);
        }
        using (context.PushTransform(Matrix.CreateScale(1 / w, 1 / w)))
        {
            Engrave(context, _armText, (0.126 * w) - _armText.Width, 0.266 * w);
            Engrave(context, _safeText, (0.126 * w) - _safeText.Width, 0.414 * w);
            Engrave(context, _readyText, 0.694 * w, 0.094 * w);
            Engrave(context, _holdText, (0.73 * w) - (_holdText.Width / 2), 0.51 * w);
        }
    }

        private void Engrave(DrawingContext context, Engraved text, double x, double centreY)
    {
        if (text.Shape is null)
        {
            return;
        }
        var y = centreY - (text.Height / 2);
        // A groove: an edge below and an edge above (both dark in the dark theme, light below in the light theme), then the ink.
        using (context.PushTransform(Matrix.CreateTranslation(x, y + 0.6)))
        {
            context.DrawGeometry(_engraveLo, null, text.Shape);
        }
        using (context.PushTransform(Matrix.CreateTranslation(x, y - 0.5)))
        {
            context.DrawGeometry(_engraveHi, null, text.Shape);
        }
        using (context.PushTransform(Matrix.CreateTranslation(x, y)))
        {
            context.DrawGeometry(_engrave, null, text.Shape);
        }
    }

    private void DrawScrews(DrawingContext context)
    {
        DrawScrew(context, new Point(0.05, 0.05), 14);
        DrawScrew(context, new Point(0.95, 0.05), 71);
        DrawScrew(context, new Point(0.05, 0.57), 43);
        DrawScrew(context, new Point(0.95, 0.57), 104);
    }

    private void DrawScrew(DrawingContext context, Point c, double turn)
    {
        context.DrawEllipse(_screwWell, null, c, 0.022, 0.022);
        context.DrawEllipse(Brushes.Black, null, new Point(c.X, c.Y + 0.0018), 0.0175, 0.0175);
        context.DrawEllipse(_screwHead, null, c, 0.0175, 0.0175);
        using (context.PushTransform(Matrix.CreateScale(0.008, 0.008) * Matrix.CreateRotation(turn * Math.PI / 180)
                                     * Matrix.CreateTranslation(c.X, c.Y)))
        {
            context.DrawGeometry(_screwSocket, null, Hexagon);
        }
        context.DrawEllipse(_screwSpec, null, new Point(c.X - 0.0065, c.Y - 0.008), 0.0075, 0.0057);
    }

    // --- lamp ---

    private (double A, double G, double R) LightWeights()
    {
        var sum = Math.Max(1, _amber + _green + _red);
        return (_amber / sum, _green / sum, _red / sum);
    }

    private void DrawLights(DrawingContext context, bool spill)
    {
        if (!spill || _level <= 0.002)
        {
            return;
        }
        var (a, g, r) = LightWeights();
        var breath = Breath();
        Span<double> weights = stackalloc double[] { a, g, r };
        for (var i = 0; i < 3; i++)
        {
            var o = 0.75 * _level * weights[i] * breath;
            if (o > 0.002)
            {
                using (context.PushOpacity(o))
                {
                    context.DrawEllipse(_spill[i], null, LampCentre, 0.287, 0.287);
                }
            }
        }
    }

    private void DrawLamp(DrawingContext context)
    {
        var c = LampCentre;
        EnsureRingPens();
        context.DrawEllipse(null, _ringTrack, c, RingRadius, RingRadius);
        context.DrawEllipse(null, _ringTrackHi, new Point(c.X, c.Y + 0.0012), RingRadius, RingRadius);

        // Hold ring fill, its leading head and the spinner.
        var fillPen = _doneClock >= 0 ? _fillGreen : _failClock >= 0 ? _fillRed : _fillAccent;
        if (_fillOpacity > 0.002 && _hold > 0.0005)
        {
            var arc = ArcGeometry(_hold);
            using (context.PushOpacity(_fillOpacity))
            {
                context.DrawGeometry(null, fillPen, arc);
            }
            if (_completeClock < 0 && _doneClock < 0 && _failClock < 0)
            {
                var head = Math.Clamp(_hold / 0.04, 0, 1) * Math.Clamp(_fillOpacity, 0, 1);
                var a = (_hold * 360 - 90) * Math.PI / 180;
                var hp = new Point(c.X + (RingRadius * Math.Cos(a)), c.Y + (RingRadius * Math.Sin(a)));
                using (context.PushOpacity(head))
                {
                    context.DrawEllipse(_headFill, _headPen, hp, 0.00672, 0.00672);
                }
            }
        }
        if (_spinOpacity > 0.002)
        {
            _spinArc ??= SpinGeometry();
            using (context.PushOpacity(_spinOpacity))
            using (context.PushTransform(Matrix.CreateRotation(_spin * Math.PI / 180) * Matrix.CreateTranslation(c.X, c.Y)))
            {
                context.DrawGeometry(null, _spinPen, _spinArc);
            }
        }

        // Bezel with its shadow, groove and fine turned rings.
        SoftCircle(context, new Point(c.X + 0.005, c.Y + 0.009), 0.1, 0.012, 0.45);
        context.DrawEllipse(_bezel, _bezelRing, c, 0.1, 0.1);
        for (var i = 0; i < 7; i++)
        {
            var r = 0.071 + (i * 0.0042);
            context.DrawEllipse(null, (i & 1) == 0 ? _fineRingLight : _fineRingDark, c, r, r);
        }
        context.DrawEllipse(_bezelGroove, null, c, 0.1, 0.1);

        // Lens: pressed in while held, lit by the three lights, with facets and a gloss.
        var lr = 0.07 * _lens;
        var lens = new Rect(c.X - lr, c.Y - lr, lr * 2, lr * 2);
        using (context.PushClip(new RoundedRect(lens, lr)))
        {
            context.DrawEllipse(_lensFill, null, c, lr, lr);
            var (a, g, r) = LightWeights();
            var breath = Breath();
            Span<double> weights = stackalloc double[] { a, g, r };
            for (var i = 0; i < 3; i++)
            {
                var o = _level * weights[i] * breath;
                if (o > 0.002)
                {
                    using (context.PushOpacity(Math.Min(1, o)))
                    {
                        context.DrawEllipse(_lit[i], null, c, lr, lr);
                    }
                }
            }
            using (context.PushOpacityMask(FacetMask, lens))
            using (context.PushTransform(Matrix.CreateScale(lr, lr) * Matrix.CreateTranslation(c.X, c.Y)))
            {
                context.DrawGeometry(_facetA, null, FacetsA);
                context.DrawGeometry(_facetB, null, FacetsB);
            }
            using (context.PushOpacityMask(FacetMask, lens))
            {
                // Turned rings 0.009 W apart, light then dark: with the facets they make the lens's checker.
                for (var rr = 0.0045; rr < 0.075; rr += 0.018)
                {
                    context.DrawEllipse(null, _lensRingLight, c, rr * _lens, rr * _lens);
                    context.DrawEllipse(null, _lensRingDark, c, (rr + 0.009) * _lens, (rr + 0.009) * _lens);
                }
            }
            context.DrawRectangle(_lensShade, null, lens);
            using (context.PushTransform(Matrix.CreateTranslation(-0.0364, -0.0238) * Matrix.CreateRotation(-22 * Math.PI / 180)
                                         * Matrix.CreateScale(_lens, _lens)
                                         * Matrix.CreateTranslation(c.X - 0.07 + 0.0196 + 0.0364, c.Y - 0.07 + 0.0098 + 0.0238)))
            {
                context.DrawEllipse(_gloss, null, new Point(0.0364, 0.0238), 0.0364, 0.0238);
            }
        }
        context.DrawEllipse(null, _lensRing, c, lr, lr);

        // Blooms over the lens, then the bursts that pop off the lamp.
        DrawBlooms(context);
        DrawBurst(context, _burstAccent, 0.7, 0.9, _fillAccentColour, 2);
        DrawBurst(context, _burstGreen, 1.05, 0.84, _lampColours[1], 3);
        DrawBurst(context, _burstRed, 0.7, 0.9, _lampColours[2], 2);
    }

    private void DrawBlooms(DrawingContext context)
    {
        if (_level <= 0.002)
        {
            return;
        }
        var (a, g, r) = LightWeights();
        var breath = Breath();
        var scale = (0.84 + (0.24 * _level)) * (1 + (0.1 * (_holding ? _hold : 0)));
        var radius = 0.18 * scale;
        Span<double> weights = stackalloc double[] { a, g, r };
        for (var i = 0; i < 3; i++)
        {
            var o = 0.95 * _level * weights[i] * breath;
            if (o > 0.002)
            {
                using (context.PushOpacity(Math.Min(1, o)))
                {
                    context.DrawEllipse(_bloom[i], null, LampCentre, radius, radius);
                }
            }
        }
    }

    private void DrawBurst(DrawingContext context, double clock, double grow, double startOpacity, Color colour, double px)
    {
        if (clock < 0)
        {
            return;
        }
        var u = Math.Clamp(clock / 0.78, 0, 1);
        var scale = 1 + (grow * (1 - Math.Pow(1 - u, 2.6)));
        var opacity = startOpacity * Math.Pow(1 - u, 1.3);
        if (opacity <= 0.002)
        {
            return;
        }
        var r = 0.14 * scale;
        using (context.PushOpacity(opacity))
        {
            context.DrawEllipse(null, BurstPen(colour, px), LampCentre, r, r);
        }
    }

    private readonly Dictionary<(Color, double, double), IPen> _burstPens = new();

    /// <summary>A burst ring's pen (px wide whatever the plate's size), cached per colour, width and size.</summary>
    private IPen BurstPen(Color colour, double px)
    {
        var key = (colour, px, Math.Round(_w, 1));
        if (!_burstPens.TryGetValue(key, out var pen))
        {
            if (_burstPens.Count > 12)
            {
                _burstPens.Clear();
            }
            pen = ArmingSwitchPaint.Pen(colour, px / _w);
            _burstPens[key] = pen;
        }
        return pen;
    }

    private static void SoftCircle(DrawingContext context, Point c, double radius, double blur, double opacity)
    {
        const int layers = 4;
        for (var i = 0; i < layers; i++)
        {
            var r = radius + (blur * (1 - ((i + 0.5) / layers))) - (blur * 0.25);
            using (context.PushOpacity(opacity / layers * 1.15))
            {
                context.DrawEllipse(Brushes.Black, null, c, r, r);
            }
        }
    }

    private void EnsureRingPens()
    {
        var accent = (Owner?.AccentBrush ?? Owner?.AccentInk) is ISolidColorBrush b ? b.Color : _accent;
        if (_fillAccent is null || accent != _fillAccentColour)
        {
            _fillAccentColour = accent;
            _fillAccent = ArmingSwitchPaint.Pen(accent, 0.00896, PenLineCap.Round);
            _headPen = ArmingSwitchPaint.Pen(accent, 0.0045);
            _headFill = ArmingSwitchPaint.Solid(Oklch.ToColor(0.24, 0, 0));
        }
        _fillGreen ??= ArmingSwitchPaint.Pen(_lampColours[1], 0.00896, PenLineCap.Round);
        _fillRed ??= ArmingSwitchPaint.Pen(_lampColours[2], 0.00896, PenLineCap.Round);
        _spinPen ??= ArmingSwitchPaint.Pen(_lampColours[0], 0.00896, PenLineCap.Round);
    }

    private Geometry ArcGeometry(double value)
    {
        value = Math.Clamp(value, 0, 1);
        if (_arc is not null && Math.Abs(value - _arcValue) < 0.0005)
        {
            return _arc;
        }
        _arcValue = value;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            var c = LampCentre;
            var start = new Point(c.X, c.Y - RingRadius);
            ctx.BeginFigure(start, false);
            if (value >= 0.9995)
            {
                ctx.ArcTo(new Point(c.X, c.Y + RingRadius), new Size(RingRadius, RingRadius), 0, false, SweepDirection.Clockwise);
                ctx.ArcTo(start, new Size(RingRadius, RingRadius), 0, false, SweepDirection.Clockwise);
            }
            else
            {
                var a = (value * 360 - 90) * Math.PI / 180;
                var end = new Point(c.X + (RingRadius * Math.Cos(a)), c.Y + (RingRadius * Math.Sin(a)));
                ctx.ArcTo(end, new Size(RingRadius, RingRadius), 0, value > 0.5, SweepDirection.Clockwise);
            }
            ctx.EndFigure(false);
        }
        _arc = g;
        return g;
    }

    private static StreamGeometry SpinGeometry()
    {
        // 22% of the ring, centred on the origin.
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            var a0 = -90 * Math.PI / 180;
            var a1 = (0.22 * 360 - 90) * Math.PI / 180;
            ctx.BeginFigure(new Point(RingRadius * Math.Cos(a0), RingRadius * Math.Sin(a0)), false);
            ctx.ArcTo(new Point(RingRadius * Math.Cos(a1), RingRadius * Math.Sin(a1)), new Size(RingRadius, RingRadius), 0, false,
                SweepDirection.Clockwise);
            ctx.EndFigure(false);
        }
        return g;
    }

    private void DrawGlint(DrawingContext context)
    {
        if (_glint < 0)
        {
            return;
        }
        var u = Math.Clamp(_glint / 1.0, 0, 1);
        var e = u < 0.5 ? 4 * u * u * u : 1 - (Math.Pow((-2 * u) + 2, 3) / 2);
        var x = -0.4 + (1.5 * e);
        using (context.PushOpacity(0.95 * Math.Sin(Math.PI * u)))
        {
            context.DrawRectangle(_glintBand, null, new Rect(x, -0.062, 0.34, 0.744));
        }
    }

    // --- the toggle ---

    private void DrawAssembly(DrawingContext context)
    {
        using (context.PushTransform(_view.Plane(0.0025)))
        {
            SoftCircle(context, new Point(Pivot.X + 0.003, Pivot.Y + 0.005), 0.056, 0.008, 0.45);
            context.DrawEllipse(_washer, null, Pivot, 0.056, 0.056);
            context.DrawEllipse(_washerGroove, null, Pivot, 0.056, 0.056);
        }
        using (context.PushTransform(Matrix.CreateScale(0.046, 0.04) * Matrix.CreateTranslation(Pivot.X, Pivot.Y) * _view.Plane(0.008)))
        {
            for (var i = 0; i < 6; i++)
            {
                context.DrawGeometry(_nutFaces[i], null, NutFace(i));
            }
        }
        using (context.PushTransform(_view.Plane(0.015)))
        {
            context.DrawEllipse(Brushes.Black, null, new Point(Pivot.X + 0.002, Pivot.Y + 0.0035), 0.028, 0.028);
            context.DrawEllipse(_bushing, null, Pivot, 0.028, 0.028);
            for (var i = 0; i < 5; i++)
            {
                var r = 0.012 + (i * 0.0032);
                context.DrawEllipse(null, (i & 1) == 0 ? _fineRingLight : _fineRingDark, Pivot, r, r);
            }
            context.DrawEllipse(_bushingHole, null, Pivot, 0.028, 0.028);
        }

        // The bat: a tapering chrome shaft from the pivot and a ball on its tip, facing the viewer.
        var r0 = _bat * Math.PI / 180;
        var dir = new ArmingVec(0, Math.Sin(r0), Math.Cos(r0));
        var origin = new ArmingVec(Pivot.X - 0.02, Pivot.Y, PivotZ);
        using (context.PushTransform(_view.Face(origin, new ArmingVec(1, 0, 0), dir)))
        {
            context.DrawGeometry(_shaft, null, ShaftOutline);
            using (context.PushGeometryClip(ShaftOutline))
            {
                // The reflected horizon slides along the shaft as it tilts.
                var slide = -0.0459 * (_bat / BatThrow);
                context.DrawRectangle(_band, null, new Rect(0, -0.0675 + slide, 0.04, 0.27));
            }
        }
        var tip = new ArmingVec(Pivot.X, Pivot.Y + (0.135 * dir.Y), PivotZ + (0.135 * dir.Z));
        var centre = _view.Project(tip);
        var radius = 0.026 * _w * _view.PerspectiveAt(tip);
        var ball = new Rect(centre.X - radius, centre.Y - radius, radius * 2, radius * 2);
        context.DrawEllipse(_ball, null, centre, radius, radius);
        context.DrawEllipse(_ballShade, null, centre, radius, radius);
        context.DrawEllipse(_ballSpec, null, new Point(ball.X + (0.318 * ball.Width), ball.Y + (0.28 * ball.Height)),
            radius * 0.36, radius * 0.3);
    }

    // --- the cover ---

    private void DrawCover(DrawingContext context)
    {
        // Five faces: lid, hinge wall, lip wall, two side walls; drawn far to near.
        _faces[0] = (CoverPoint(0, 0, 0.16), CoverAxis(1, 0, 0), CoverAxis(0, 1, 0), 0, 0);
        _faces[1] = (CoverPoint(0, 0, 0), CoverAxis(1, 0, 0), CoverAxis(0, 0, 1), 1, 0);
        _faces[2] = (CoverPoint(0, 0.4, 0.16), CoverAxis(1, 0, 0), CoverAxis(0, 0, -1), 2, 0);
        _faces[3] = (CoverPoint(0, 0, 0), CoverAxis(0, 0, 1), CoverAxis(0, 1, 0), 3, 0);
        _faces[4] = (CoverPoint(0.26, 0, 0.16), CoverAxis(0, 0, -1), CoverAxis(0, 1, 0), 4, 0);
        for (var i = 0; i < 5; i++)
        {
            var f = _faces[i];
            var (uLen, vLen) = FaceSize(f.Kind);
            var mid = f.O + (f.U * (uLen / 2)) + (f.V * (vLen / 2));
            _faces[i].Depth = _view.DepthOf(mid);
        }
        // Insertion sort by depth (five items, no allocation).
        for (var i = 1; i < 5; i++)
        {
            var item = _faces[i];
            var j = i - 1;
            while (j >= 0 && _faces[j].Depth > item.Depth)
            {
                _faces[j + 1] = _faces[j];
                j--;
            }
            _faces[j + 1] = item;
        }
        for (var i = 0; i < 5; i++)
        {
            var f = _faces[i];
            using (context.PushTransform(_view.Face(f.O, f.U, f.V)))
            {
                DrawFace(context, f.Kind);
            }
        }
    }

    private static (double U, double V) FaceSize(int kind) => kind switch
    {
        0 => (0.26, 0.4),
        1 or 2 => (0.26, 0.16),
        _ => (0.16, 0.4)
    };

    private void DrawFace(DrawingContext context, int kind)
    {
        switch (kind)
        {
            case 0:
                DrawLid(context);
                break;
            case 1:
                context.DrawRectangle(_wallTop, null, new Rect(0, 0, 0.26, 0.16));
                break;
            case 2:
                context.DrawRectangle(_wallBottom, null, new Rect(0, 0, 0.26, 0.16));
                context.DrawRectangle(_wallBottomSheen, null, new Rect(0, 0, 0.26, 0.16));
                break;
            default:
                context.DrawRectangle(_wallSide, null, new Rect(0, 0, 0.16, 0.4));
                break;
        }
    }

    private void DrawLid(DrawingContext context)
    {
        var lid = new RoundedRect(new Rect(0, 0, 0.26, 0.4), 0.014);
        // The finger tab under the lip, in the lid's plane.
        var tab = new RoundedRect(new Rect(0.07, 0.395, 0.12, 0.03), new CornerRadius(0, 0, 0.015, 0.015));
        context.DrawRectangle(_tab, _tabRing, tab);
        context.DrawRectangle(_lid, null, lid);
        using (context.PushClip(lid))
        {
            context.DrawRectangle(_lidSheen, null, lid.Rect);
            // The ribbed grip near the lip.
            using (context.PushOpacity(0.8))
            {
                for (var i = 0; i < 5; i++)
                {
                    var y = 0.328 + (i * 0.009);
                    context.DrawRectangle(RibLight, null, new Rect(0.0312, y, 0.1976, 0.0022));
                    context.DrawRectangle(_rib, null, new Rect(0.0312, y + 0.0022, 0.1976, 0.0068));
                }
            }
            // The glare that sweeps down the lid as it rises.
            var angle = Math.Clamp(_cov, 0, CoverOpenAngle);
            var gy = Interp(ShadowAngles, GlareY, angle);
            using (context.PushOpacity(Interp(ShadowAngles, GlareOpacity, angle)))
            {
                context.DrawRectangle(_glare, null, new Rect(-0.078, gy, 0.416, 0.28));
            }
            context.DrawLine(_lidHi!, new Point(0.01, 0.0018), new Point(0.25, 0.0018));
            context.DrawLine(_lidLo!, new Point(0.01, 0.3975), new Point(0.25, 0.3975));
        }
        context.DrawRectangle(null, _lidRing, new RoundedRect(new Rect(0.001, 0.001, 0.258, 0.398), 0.013));
    }

    private static readonly IBrush RibLight = new ImmutableSolidColorBrush(Color.FromArgb(56, 255, 255, 255));

    // --- static outlines ---

    private static Geometry MakeHexagon()
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            // Flat top and bottom, unit "radius" (Arc's 25% 7%, 75% 7%, 100% 50% ... socket).
            ctx.BeginFigure(new Point(-0.5, -0.86), true);
            ctx.LineTo(new Point(0.5, -0.86));
            ctx.LineTo(new Point(1, 0));
            ctx.LineTo(new Point(0.5, 0.86));
            ctx.LineTo(new Point(-0.5, 0.86));
            ctx.LineTo(new Point(-1, 0));
            ctx.EndFigure(true);
        }
        return g;
    }

    private static readonly Geometry[] NutFaces = MakeNutFaces();

    private static Geometry NutFace(int i) => NutFaces[i];

    /// <summary>The hex nut's six facets (a unit hexagon with a flat top), lit as Arc's conic from -30°.</summary>
    private static Geometry[] MakeNutFaces()
    {
        // Corners clockwise from the top-left: (-0.5,-1) (0.5,-1) (1,0) (0.5,1) (-0.5,1) (-1,0).
        var corners = new[] { new Point(-0.5, -1), new Point(0.5, -1), new Point(1, 0), new Point(0.5, 1), new Point(-0.5, 1), new Point(-1, 0) };
        var faces = new Geometry[6];
        for (var i = 0; i < 6; i++)
        {
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(new Point(0, 0), true);
                ctx.LineTo(corners[i]);
                ctx.LineTo(corners[(i + 1) % 6]);
                ctx.EndFigure(true);
            }
            faces[i] = g;
        }
        return faces;
    }

    private static Geometry MakeShaft()
    {
        // 0.04 wide; narrow (24%..76%) at the pivot (v 0) and full width 4% short of the tip (Arc's clip polygon).
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(0.0096, 0), true);
            ctx.LineTo(new Point(0.0304, 0));
            ctx.LineTo(new Point(0.04, 0.1296));
            ctx.LineTo(new Point(0, 0.1296));
            ctx.EndFigure(true);
        }
        return g;
    }

    private static Geometry MakeFacets(int phase)
    {
        // Twelve 15° wedges (every other one of 24) of a unit circle, from 15°.
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            for (var i = phase; i < 24; i += 2)
            {
                var a0 = ((15 + (i * 15)) - 90) * Math.PI / 180;
                var a1 = ((15 + ((i + 1) * 15)) - 90) * Math.PI / 180;
                ctx.BeginFigure(new Point(0, 0), true);
                ctx.LineTo(new Point(Math.Cos(a0), Math.Sin(a0)));
                ctx.ArcTo(new Point(Math.Cos(a1), Math.Sin(a1)), new Size(1, 1), 0, false, SweepDirection.Clockwise);
                ctx.EndFigure(true);
            }
        }
        return g;
    }
}
