using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The drawing of <see cref="GhostMascot"/>, in a 100 unit square: the sheet (a domed head, slightly flared sides and a
/// hem of two traveling waves drawn as exact cubic Hermite curves through rounded corners), a quiet form shade clipped to
/// the sheet, two eyes (ovals, happy arcs or a droop), a small smile, the thinking swirl, the writing pen and line, the
/// done sparkle and the floating ground shadow. The sheet's path objects are created once and their points are moved in
/// place each frame; the brushes and pens are cached and only recolored.
/// </summary>
public partial class GhostMascot
{
    // The sheet at rest (drawing units, before the pose).
    private const double HeadCenterY = 42;
    private const double HeadRadius = 24;
    private const double HemBaseline = 78;
    private const double HemHalfWidth = 26;
    private const double HemCorner = 5;
    private const int HemSegments = 10;
    private const double BodyPivotY = 60;
    internal const double BodyEyeY = 45;
    private const double EyeSpacing = 8.5;
    private const double EyeRadiusX = 3.2;
    private const double EyeRadiusY = 4.4;
    private const int SwirlSegments = 20;

    private readonly SolidColorBrush _bodyBrush = new(Colors.Gray);
    private readonly SolidColorBrush _eyeBrush = new(Colors.White);
    private readonly SolidColorBrush _markBrush = new(Colors.Gray);
    private readonly SolidColorBrush _shadeBrush = new(Colors.Transparent);
    private readonly SolidColorBrush _groundBrush = new(Colors.Transparent);
    private readonly SolidColorBrush _hairlineBrush = new(Colors.Transparent);
    private bool _hasHairline;

    private Pen _hairlinePen = null!;
    private Pen _markPen = null!;
    private Pen _shaftPen = null!;
    private Pen _nibPen = null!;
    private Pen _featurePen = null!;

    private PathGeometry _body = null!;
    private PathFigure _bodyFigure = null!;
    private ArcSegment _headLeft = null!;
    private ArcSegment _headRight = null!;
    private BezierSegment _rightSide = null!;
    private BezierSegment _rightCorner = null!;
    private BezierSegment[] _hem = null!;
    private BezierSegment _leftCorner = null!;
    private BezierSegment _leftSide = null!;

    private PathGeometry _star = null!;
    private PathGeometry _happyLeft = null!;
    private PathGeometry _happyRight = null!;
    private PathGeometry _smile = null!;
    private QuadraticBezierSegment _happyLeftCurve = null!;
    private QuadraticBezierSegment _happyRightCurve = null!;
    private QuadraticBezierSegment _smileCurve = null!;

    /// <summary>The drawing in use: compact under 56px, regular from there up.</summary>
    internal GhostMascotLayout Layout => IsCompact ? GhostMascotLayout.Compact : GhostMascotLayout.Regular;

    /// <summary>The sheet's path (test hook).</summary>
    internal PathGeometry BodyGeometry => _body;

    internal Color BodyColor => _bodyBrush.Color;
    internal Color EyeColor => _eyeBrush.Color;
    internal Color MarkColor => _markBrush.Color;
    internal bool HasHairline => _hasHairline;

    private void CreateDrawing()
    {
        _hairlinePen = new Pen(_hairlineBrush, 1);
        _markPen = new Pen(_markBrush, 1.7, lineCap: PenLineCap.Round);
        _shaftPen = new Pen(_markBrush, 2.6, lineCap: PenLineCap.Round);
        _nibPen = new Pen(_markBrush, 1.1, lineCap: PenLineCap.Round);
        _featurePen = new Pen(_eyeBrush, 1.6, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        _headLeft = new ArcSegment { Size = new Size(HeadRadius, HeadRadius), SweepDirection = SweepDirection.Clockwise };
        _headRight = new ArcSegment { Size = new Size(HeadRadius, HeadRadius), SweepDirection = SweepDirection.Clockwise };
        _rightSide = new BezierSegment();
        _rightCorner = new BezierSegment();
        _leftCorner = new BezierSegment();
        _leftSide = new BezierSegment();
        _hem = new BezierSegment[HemSegments];
        _bodyFigure = new PathFigure { IsClosed = true, IsFilled = true };
        _bodyFigure.Segments!.Add(_headLeft);
        _bodyFigure.Segments.Add(_headRight);
        _bodyFigure.Segments.Add(_rightSide);
        _bodyFigure.Segments.Add(_rightCorner);
        for (var i = 0; i < HemSegments; i++)
        {
            _hem[i] = new BezierSegment();
            _bodyFigure.Segments.Add(_hem[i]);
        }
        _bodyFigure.Segments.Add(_leftCorner);
        _bodyFigure.Segments.Add(_leftSide);
        _body = new PathGeometry();
        _body.Figures!.Add(_bodyFigure);

        // A four-point star (unit half size), filled.
        var star = new PathFigure { StartPoint = new Point(0, -1), IsClosed = true, IsFilled = true };
        star.Segments!.Add(new QuadraticBezierSegment { Point1 = new Point(0.14, -0.14), Point2 = new Point(1, 0) });
        star.Segments.Add(new QuadraticBezierSegment { Point1 = new Point(0.14, 0.14), Point2 = new Point(0, 1) });
        star.Segments.Add(new QuadraticBezierSegment { Point1 = new Point(-0.14, 0.14), Point2 = new Point(-1, 0) });
        star.Segments.Add(new QuadraticBezierSegment { Point1 = new Point(-0.14, -0.14), Point2 = new Point(0, -1) });
        _star = new PathGeometry();
        _star.Figures!.Add(star);

        (_happyLeft, _happyLeftCurve) = StrokeCurve();
        (_happyRight, _happyRightCurve) = StrokeCurve();
        (_smile, _smileCurve) = StrokeCurve();
        UpdatePens();
        UpdateColors();
    }

    private static (PathGeometry Geometry, QuadraticBezierSegment Curve) StrokeCurve()
    {
        var curve = new QuadraticBezierSegment();
        var figure = new PathFigure { IsClosed = false, IsFilled = false };
        figure.Segments!.Add(curve);
        var geometry = new PathGeometry();
        geometry.Figures!.Add(figure);
        return (geometry, curve);
    }

    /// <summary>Stroke weights follow the drawing (heavier in the compact one) and the hairline stays 1px at any size.</summary>
    private void UpdatePens()
    {
        if (_markPen is null)
        {
            return;
        }
        var layout = Layout;
        var unit = Math.Max(0.01, Size / 100 * layout.Frame);
        _hairlinePen.Thickness = GetValue(HairlineWidthProperty) / unit;
        _markPen.Thickness = 1.7 * layout.Stroke;
        _shaftPen.Thickness = 2.8 * layout.Stroke;
        _nibPen.Thickness = 1.1 * layout.Stroke;
        _featurePen.Thickness = 1.6 * layout.Stroke;
    }

    /// <summary>Moves the sheet's and the face's path points to the pose now (frames and property changes, never Render).</summary>
    private void Sync()
    {
        if (_body is null)
        {
            return;
        }
        UpdateBody();
        UpdateFace();
    }

    private void UpdateBody()
    {
        var velocity = FloatVelocity;
        // The hem trails the float: it hangs lower on the way up and lifts and flares on the way down.
        var baseline = HemBaseline - 0.33 * velocity;
        var half = HemHalfWidth + 0.25 * Math.Max(0, velocity);
        var amp = _pose[GhostMascotPose.HemAmp];
        var phase = IsStill ? 0 : _hemPhase;
        const double cx = 50;
        var left = cx - HeadRadius;
        var right = cx + HeadRadius;

        _bodyFigure.StartPoint = new Point(left, HeadCenterY);
        _headLeft.Point = new Point(cx, HeadCenterY - HeadRadius);
        _headRight.Point = new Point(right, HeadCenterY);

        var xr = cx + half;
        var xl = cx - half;
        var hemRight = xr - HemCorner;
        var hemLeft = xl + HemCorner;
        var (yr, mr) = GhostMascotPose.Hem(hemRight, baseline, amp, phase);
        var (yl, ml) = GhostMascotPose.Hem(hemLeft, baseline, amp, phase);
        var sideRight = yr - HemCorner;
        var sideLeft = yl - HemCorner;

        _rightSide.Point1 = new Point(right, HeadCenterY + 14);
        _rightSide.Point2 = new Point(xr, sideRight - 12);
        _rightSide.Point3 = new Point(xr, sideRight);

        // The rounded corner leaves the side going straight down and meets the hem along its slope (heading left).
        var nr = Math.Sqrt(1 + mr * mr);
        _rightCorner.Point1 = new Point(xr, sideRight + 0.55 * HemCorner);
        _rightCorner.Point2 = new Point(hemRight + 0.55 * HemCorner / nr, yr + 0.55 * HemCorner * mr / nr);
        _rightCorner.Point3 = new Point(hemRight, yr);

        // The hem from right to left: cubic Hermite pieces through samples of the two waves, exact in x.
        var x0 = hemRight;
        var y0 = yr;
        var m0 = mr;
        var step = (hemLeft - hemRight) / HemSegments;
        for (var i = 0; i < HemSegments; i++)
        {
            var x1 = i == HemSegments - 1 ? hemLeft : x0 + step;
            var (y1, m1) = GhostMascotPose.Hem(x1, baseline, amp, phase);
            var h = x1 - x0;
            var segment = _hem[i];
            segment.Point1 = new Point(x0 + h / 3, y0 + m0 * h / 3);
            segment.Point2 = new Point(x1 - h / 3, y1 - m1 * h / 3);
            segment.Point3 = new Point(x1, y1);
            x0 = x1;
            y0 = y1;
            m0 = m1;
        }

        var nl = Math.Sqrt(1 + ml * ml);
        _leftCorner.Point1 = new Point(hemLeft - 0.55 * HemCorner / nl, yl - 0.55 * HemCorner * ml / nl);
        _leftCorner.Point2 = new Point(xl, sideLeft + 0.55 * HemCorner);
        _leftCorner.Point3 = new Point(xl, sideLeft);

        _leftSide.Point1 = new Point(xl, sideLeft - 12);
        _leftSide.Point2 = new Point(left, HeadCenterY + 14);
        _leftSide.Point3 = new Point(left, HeadCenterY);
    }

    private void UpdateFace()
    {
        var layout = Layout;
        var (lx, rx, ey, _, _) = EyeGeometry(layout);
        var arcRx = EyeRadiusX * layout.EyeScale * 1.1;
        var arcRy = EyeRadiusY * layout.EyeScale;
        SetArc(_happyLeft, _happyLeftCurve, lx, ey, arcRx, arcRy);
        SetArc(_happyRight, _happyRightCurve, rx, ey, arcRx, arcRy);

        var smile = _pose[GhostMascotPose.Smile];
        var mx = (lx + rx) / 2;
        var my = ey + EyeRadiusY * layout.EyeScale + 3;
        var half = 3.6 * Math.Min(1.25, layout.EyeScale);
        _smile.Figures![0].StartPoint = new Point(mx - half, my);
        _smileCurve.Point1 = new Point(mx, my + 3.4 * smile);
        _smileCurve.Point2 = new Point(mx + half, my);
    }

    private static void SetArc(PathGeometry geometry, QuadraticBezierSegment curve, double cx, double cy, double rx, double ry)
    {
        geometry.Figures![0].StartPoint = new Point(cx - rx, cy + ry * 0.3);
        curve.Point1 = new Point(cx, cy - ry * 1.2);
        curve.Point2 = new Point(cx + rx, cy + ry * 0.3);
    }

    /// <summary>The eyes now (body coordinates): left and right centers, their height and radii, gaze included.</summary>
    private (double Left, double Right, double Y, double RadiusX, double RadiusY) EyeGeometry(GhostMascotLayout layout)
    {
        var size = layout.EyeScale * _pose[GhostMascotPose.EyeSize];
        var spacing = EyeSpacing * (layout.EyeScale > 1 ? 1.08 : 1);
        var ox = _gazeX * 2.4 * layout.EyeScale;
        var oy = _gazeY * 2.0 * layout.EyeScale;
        var droop = _pose[GhostMascotPose.Droop];
        var y = BodyEyeY + oy + 1.2 * droop;
        var radiusY = EyeRadiusY * size * Math.Max(0, _pose[GhostMascotPose.EyeOpen]) * (1 - 0.9 * BlinkNow) * (1 - 0.38 * droop);
        return (50 - spacing + ox, 50 + spacing + ox, y, EyeRadiusX * size, radiusY);
    }

    /// <summary>The pen tip now, in drawing units before the framing.</summary>
    private (double X, double Y) PenTip(GhostMascotLayout layout)
    {
        var (_, pen, lift) = WritingNow;
        return (layout.LineStart + (layout.LineEnd - layout.LineStart) * pen, layout.LineY - lift);
    }

    // Colors are set on property changes, never in Render: the brushes are reused and only recolored.
    private void UpdateColors()
    {
        var accent = ColorOf(GetValue(AccentInkProperty), Color.FromRgb(0x77, 0x47, 0xFF));
        var accentForeground = ColorOf(GetValue(AccentForegroundInkProperty), Colors.White);
        var foreground = ColorOf(GetValue(ForegroundInkProperty), Color.FromRgb(0x1A, 0x1A, 0x1A));
        var background = ColorOf(GetValue(BackgroundInkProperty), Colors.White);
        var raised = ColorOf(GetValue(RaisedInkProperty), Colors.White);
        var hairline = ColorOf(GetValue(HairlineInkProperty), Color.FromRgb(0xE4, 0xE4, 0xE7));

        Color body;
        Color eye;
        Color mark;
        switch (Tone)
        {
            case GhostMascotTone.Sheet:
                body = raised;
                eye = foreground;
                mark = accent;
                break;
            case GhostMascotTone.Ink:
                body = foreground;
                eye = background;
                mark = foreground;
                break;
            default:
                body = accent;
                eye = accentForeground;
                mark = accent;
                break;
        }
        _hasHairline = Tone == GhostMascotTone.Sheet && SheetBrush is null;
        if (SheetBrush is { } sheet)
        {
            body = ColorOf(sheet, body);
            eye = Oklch.MixOklab(body, Colors.Black, Math.Clamp(GetValue(EyeDarkMixProperty), 0, 1));
        }
        if (EyeBrush is { } eyeBrush)
        {
            eye = ColorOf(eyeBrush, eye);
        }
        if (MarkBrush is { } markBrush)
        {
            mark = ColorOf(markBrush, mark);
        }
        var shade = ShadeBrush is { } shadeBrush
            ? ColorOf(shadeBrush, Colors.Transparent)
            : Oklch.WithAlpha(Oklch.MixOklab(body, Colors.Black, 0.55), Math.Clamp(GetValue(ShadeOpacityProperty), 0, 1));
        var ground = GroundBrush is { } groundBrush
            ? ColorOf(groundBrush, Colors.Transparent)
            : Oklch.WithAlpha(foreground, Math.Clamp(GetValue(GroundOpacityProperty), 0, 1));

        Recolor(_bodyBrush, body);
        Recolor(_eyeBrush, eye);
        Recolor(_markBrush, mark);
        Recolor(_shadeBrush, shade);
        Recolor(_groundBrush, ground);
        Recolor(_hairlineBrush, hairline);
    }

    private static void Recolor(SolidColorBrush brush, Color color)
    {
        if (brush.Color != color)
        {
            brush.Color = color;
        }
    }

    private static Color ColorOf(IBrush? brush, Color fallback) => brush switch
    {
        ISolidColorBrush solid => solid.Color,
        IGradientBrush { GradientStops.Count: > 0 } gradient => gradient.GradientStops[0].Color,
        _ => fallback
    };

    public override void Render(DrawingContext context)
    {
        var side = Math.Min(Bounds.Width, Bounds.Height);
        if (side <= 0)
        {
            return;
        }
        var layout = Layout;
        var unit = side / 100;
        var pivot = layout.FramePivot;
        var offset = new Point((Bounds.Width - side) / 2, (Bounds.Height - side) / 2);
        var frame = Matrix.CreateTranslation(-pivot.X, -pivot.Y) * Matrix.CreateScale(layout.Frame, layout.Frame) *
                    Matrix.CreateTranslation(pivot.X, pivot.Y) * Matrix.CreateScale(unit, unit) *
                    Matrix.CreateTranslation(offset.X, offset.Y);

        var motion = layout.Motion;
        var wobble = WobbleNow;
        var lift = (_pose[GhostMascotPose.Lift] + FloatOffset) * motion;
        var shift = (_pose[GhostMascotPose.Shift] + 0.2 * wobble) * motion;
        var tilt = (_pose[GhostMascotPose.Tilt] + wobble) * Math.PI / 180;
        var scale = _pose[GhostMascotPose.Scale];

        using (context.PushTransform(frame))
        {
            if (layout.HasGround && ShowGround)
            {
                DrawGround(context, lift, shift);
            }

            var bodyMatrix = Matrix.CreateTranslation(-50, -BodyPivotY) * Matrix.CreateScale(scale, scale) *
                             Matrix.CreateRotation(tilt) * Matrix.CreateTranslation(50 + shift, BodyPivotY + lift);
            using (context.PushTransform(bodyMatrix))
            {
                using (context.PushOpacity(Math.Clamp(_pose[GhostMascotPose.Opacity], 0, 1)))
                {
                    context.DrawGeometry(_bodyBrush, null, _body);
                    using (context.PushGeometryClip(_body))
                    {
                        context.DrawEllipse(_shadeBrush, null, new Point(66, 66), 18, 25);
                    }
                    if (_hasHairline)
                    {
                        context.DrawGeometry(null, _hairlinePen, _body);
                    }
                }
                DrawFace(context, layout);
            }

            DrawSwirl(context, layout, shift, lift);
            DrawSparkle(context, layout, shift, lift);
            DrawWriting(context, layout);
        }
    }

    private void DrawGround(DrawingContext context, double lift, double shift)
    {
        // The shadow shrinks and fades as the ghost rises (lift is negative going up).
        var rise = Math.Clamp(-lift, -4, 10);
        var opacity = Math.Clamp(1 - 0.06 * rise, 0.2, 1.2) * 0.85;
        var radius = 17 * Math.Clamp(1 - 0.035 * rise, 0.6, 1.15);
        using (context.PushOpacity(Math.Min(1, opacity)))
        {
            context.DrawEllipse(_groundBrush, null, new Point(50 + shift * 0.5, 93), radius, 2.6);
        }
    }

    private void DrawFace(DrawingContext context, GhostMascotLayout layout)
    {
        var (lx, rx, ey, radiusX, radiusY) = EyeGeometry(layout);
        var happy = Math.Clamp(_pose[GhostMascotPose.Happy], 0, 1);
        var droop = _pose[GhostMascotPose.Droop];
        if (happy < 0.98 && radiusY > 0.05)
        {
            var ovalY = radiusY * (1 - happy);
            using (context.PushOpacity(1 - happy))
            {
                DrawEye(context, lx, ey, radiusX, ovalY, -16 * droop);
                DrawEye(context, rx, ey, radiusX, ovalY, 16 * droop);
            }
        }
        if (happy > 0.02)
        {
            using (context.PushOpacity(happy))
            {
                context.DrawGeometry(null, _featurePen, _happyLeft);
                context.DrawGeometry(null, _featurePen, _happyRight);
            }
        }
        var smile = _pose[GhostMascotPose.Smile];
        if (smile > 0.02)
        {
            using (context.PushOpacity(Math.Clamp(smile, 0, 1)))
            {
                context.DrawGeometry(null, _featurePen, _smile);
            }
        }
    }

    private void DrawEye(DrawingContext context, double x, double y, double radiusX, double radiusY, double degrees)
    {
        if (Math.Abs(degrees) < 0.05)
        {
            context.DrawEllipse(_eyeBrush, null, new Point(x, y), radiusX, radiusY);
            return;
        }
        var turn = Matrix.CreateRotation(degrees * Math.PI / 180) * Matrix.CreateTranslation(x, y);
        using (context.PushTransform(turn))
        {
            context.DrawEllipse(_eyeBrush, null, default, radiusX, radiusY);
        }
    }

    private void DrawSwirl(DrawingContext context, GhostMascotLayout layout, double shift, double lift)
    {
        var visible = Math.Clamp(_pose[GhostMascotPose.Swirl], 0, 1);
        if (visible < 0.01)
        {
            return;
        }
        var (tail, head) = SwirlNow;
        if (head - tail < 0.004)
        {
            return;
        }
        var center = new Point(layout.SwirlCenter.X + shift, layout.SwirlCenter.Y + lift);
        using (context.PushOpacity(visible))
        {
            var previous = SwirlPoint(center, layout.SwirlRadius, tail);
            for (var i = 1; i <= SwirlSegments; i++)
            {
                var u = tail + (head - tail) * i / SwirlSegments;
                var next = SwirlPoint(center, layout.SwirlRadius, u);
                context.DrawLine(_markPen, previous, next);
                previous = next;
            }
        }
    }

    /// <summary>A point on the swirl: <paramref name="u"/> 0 is its outer end, 1 its center (an inward spiral, 1.6 turns).</summary>
    internal static Point SwirlPoint(Point center, double radius, double u)
    {
        var r = radius * (1 - 0.8 * u);
        var angle = -0.35 * Math.PI + u * 1.6 * 2 * Math.PI;
        return new Point(center.X + r * Math.Cos(angle), center.Y + r * Math.Sin(angle));
    }

    private void DrawSparkle(DrawingContext context, GhostMascotLayout layout, double shift, double lift)
    {
        var visible = Math.Clamp(_pose[GhostMascotPose.Sparkle], 0, 1) * SparkleGrowNow;
        if (visible < 0.01)
        {
            return;
        }
        var grow = SparkleGrowNow;
        // The sparkle grows with a quarter turn, then twinkles slowly (full motion only).
        var turn = (1 - grow) * Math.PI / 2;
        var twinkle = IsStill ? 1 : 1 + 0.12 * Math.Sin(2 * Math.PI * 0.7 * _clock);
        var twinkleSmall = IsStill ? 1 : 1 + 0.18 * Math.Sin(2 * Math.PI * 0.7 * _clock + 2.1);
        var center = new Point(layout.SparkleCenter.X + shift, layout.SparkleCenter.Y + lift);
        var size = layout.SparkleSize * visible;
        var big = Matrix.CreateScale(size * twinkle, size * twinkle) * Matrix.CreateRotation(turn) *
                  Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushTransform(big))
        {
            context.DrawGeometry(_markBrush, null, _star);
        }
        var small = Matrix.CreateScale(size * 0.45 * twinkleSmall, size * 0.45 * twinkleSmall) * Matrix.CreateRotation(-turn) *
                    Matrix.CreateTranslation(center.X + layout.SparkleSize * 0.9, center.Y + layout.SparkleSize * 1.5);
        using (context.PushTransform(small))
        {
            context.DrawGeometry(_markBrush, null, _star);
        }
    }

    private void DrawWriting(DrawingContext context, GhostMascotLayout layout)
    {
        var visible = Math.Clamp(_pose[GhostMascotPose.Pen], 0, 1);
        if (visible < 0.01)
        {
            return;
        }
        var (written, _, _) = WritingNow;
        var (tipX, tipY) = PenTip(layout);
        var length = layout.LineEnd - layout.LineStart;
        using (context.PushOpacity(visible))
        {
            for (var i = 0; i < GhostMascotPose.WordStarts.Length; i++)
            {
                var a = GhostMascotPose.WordStarts[i];
                var b = Math.Min(GhostMascotPose.WordEnds[i], written);
                if (b > a + 0.002)
                {
                    context.DrawLine(_markPen, new Point(layout.LineStart + length * a, layout.LineY),
                        new Point(layout.LineStart + length * b, layout.LineY));
                }
            }
            // A tiny pen leaning up and back toward the ghost from its tip.
            const double dx = -0.42;
            const double dy = -0.907;
            var tip = new Point(tipX, tipY);
            var nib = new Point(tipX + dx * 2.6, tipY + dy * 2.6);
            var end = new Point(tipX + dx * layout.PenLength, tipY + dy * layout.PenLength);
            context.DrawLine(_nibPen, tip, nib);
            context.DrawLine(_shaftPen, new Point(nib.X + dx * 0.6, nib.Y + dy * 0.6), end);
        }
    }
}
