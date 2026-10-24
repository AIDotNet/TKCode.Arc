using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Styling;

namespace TKCode.Arc;

/// <summary>
/// The picture column of <see cref="LoginSplit"/>: <see cref="Image"/> drawn like CSS <c>object-fit: cover</c> with a
/// focal point (<see cref="Alignment"/>, Arc's <c>object-position: 50% 58%</c>), or, without an image, our own drawing of
/// a brushed-metal facade under a pale sky: wide curved bands lightening towards the sky with seams along and across them,
/// a dark fin and a shadow in the lower left. Static (nothing moves) and decorative (hidden from assistive technology).
/// </summary>
internal sealed class LoginSplitBackdrop : Control
{
    public static readonly StyledProperty<IImage?> ImageProperty =
        AvaloniaProperty.Register<LoginSplitBackdrop, IImage?>(nameof(Image));

    public static readonly StyledProperty<Point> AlignmentProperty =
        AvaloniaProperty.Register<LoginSplitBackdrop, Point>(nameof(Alignment), new Point(0.5, 0.58));

    // The drawing's own canvas; it is scaled to cover the column like a photo would be.
    private const double CanvasWidth = 800;
    private const double CanvasHeight = 1000;

    private Scene? _scene;
    private bool _sceneDark;

    static LoginSplitBackdrop()
    {
        AffectsRender<LoginSplitBackdrop>(ImageProperty, AlignmentProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<LoginSplitBackdrop>(false);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<LoginSplitBackdrop>(AccessibilityView.Raw);
    }

    /// <summary>The picture; null draws the facade.</summary>
    public IImage? Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }

    /// <summary>The picture's focal point as fractions of its size (0.5, 0.58 by default).</summary>
    public Point Alignment { get => GetValue(AlignmentProperty); set => SetValue(AlignmentProperty, value); }

    public LoginSplitBackdrop()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    /// <summary>
    /// The part of an image of <paramref name="imageSize"/> that covers <paramref name="target"/>, centered on the focal
    /// point <paramref name="alignment"/> (fractions): the whole width or height is used and the rest is cropped.
    /// </summary>
    internal static Rect CoverSource(Size imageSize, Size target, Point alignment)
    {
        if (imageSize.Width <= 0 || imageSize.Height <= 0 || target.Width <= 0 || target.Height <= 0)
        {
            return default;
        }
        var scale = Math.Max(target.Width / imageSize.Width, target.Height / imageSize.Height);
        var width = target.Width / scale;
        var height = target.Height / scale;
        var x = (imageSize.Width - width) * Math.Clamp(alignment.X, 0, 1);
        var y = (imageSize.Height - height) * Math.Clamp(alignment.Y, 0, 1);
        return new Rect(x, y, width, height);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        if (Image is { } image)
        {
            // Bitmaps are cropped in pixels (DrawImage's source rect), other images in their own units.
            var size = image is Bitmap bitmap ? new Size(bitmap.PixelSize.Width, bitmap.PixelSize.Height) : image.Size;
            var source = CoverSource(size, bounds.Size, Alignment);
            if (source.Width > 0)
            {
                context.DrawImage(image, source, bounds);
            }
            return;
        }
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        if (_scene is null || _sceneDark != dark)
        {
            _scene = new Scene(dark);
            _sceneDark = dark;
        }
        var cover = CoverSource(new Size(CanvasWidth, CanvasHeight), bounds.Size, Alignment);
        var scale = bounds.Width / cover.Width;
        using (context.PushClip(bounds))
        using (context.PushTransform(Matrix.CreateTranslation(-cover.X, -cover.Y) * Matrix.CreateScale(scale, scale)))
        {
            _scene.Draw(context);
        }
    }

    /// <summary>The facade: geometry is fixed in canvas units, brushes follow the theme (lightness .15 lower in dark).</summary>
    private sealed class Scene
    {
        // Every band is a ring sector around a center far below and left of the canvas, so the bands sweep up to the right.
        private static readonly Point s_center = new(-420, 1460);
        private static readonly double[] s_radii = [930, 1070, 1210, 1350, 1500];

        private readonly IBrush _sky;
        private readonly IBrush[] _bands;
        private readonly Pen _seam;
        private readonly Pen _edge;
        private readonly IBrush _fin;
        private readonly IBrush _finLight;
        private readonly IBrush _shadow;
        private readonly IBrush _plinth;
        private readonly Geometry[] _bandShapes;
        private readonly Geometry _seams;
        private readonly Geometry _fin1;
        private readonly Geometry _finEdge;
        private readonly Geometry _shadowShape;
        private readonly Geometry _plinthShape;
        private readonly Geometry _plinthSeams;

        public Scene(bool dark)
        {
            var d = dark ? 0.15 : 0;
            _sky = Vertical(Oklch.ToColor(0.78 - d, 0.05, 235), Oklch.ToColor(0.9 - d, 0.02, 230));
            _bands = new IBrush[s_radii.Length - 1];
            for (var i = 0; i < _bands.Length; i++)
            {
                // Lighter towards the sky; each band carries a soft sheen along its sweep.
                var l = 0.7 + 0.04 * i - d;
                _bands[i] = new ImmutableLinearGradientBrush(
                    [
                        new ImmutableGradientStop(0, Oklch.ToColor(l - 0.05, 0.022, 245)),
                        new ImmutableGradientStop(0.55, Oklch.ToColor(l + 0.05, 0.018, 238)),
                        new ImmutableGradientStop(1, Oklch.ToColor(l - 0.02, 0.02, 242))
                    ],
                    startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
                    endPoint: new RelativePoint(1, 1, RelativeUnit.Relative));
            }
            _seam = new Pen(new ImmutableSolidColorBrush(Oklch.ToColor(0.55 - d, 0.02, 240, 0.4)), 1.2);
            _edge = new Pen(new ImmutableSolidColorBrush(Oklch.ToColor(0.32 - d * 0.5, 0.015, 240, 0.55)), 1.6);
            _fin = Vertical(Oklch.ToColor(0.46 - d, 0.02, 245), Oklch.ToColor(0.34 - d, 0.015, 245));
            _finLight = Vertical(Oklch.ToColor(0.66 - d, 0.02, 240), Oklch.ToColor(0.5 - d, 0.02, 240));
            _shadow = new ImmutableLinearGradientBrush(
                [
                    new ImmutableGradientStop(0, Oklch.ToColor(0.3 - d * 0.6, 0.01, 240, 0)),
                    new ImmutableGradientStop(0.55, Oklch.ToColor(0.3 - d * 0.6, 0.01, 240, 0.78)),
                    new ImmutableGradientStop(1, Oklch.ToColor(0.24 - d * 0.6, 0.01, 240, 0.95))
                ],
                startPoint: new RelativePoint(1, 0, RelativeUnit.Relative),
                endPoint: new RelativePoint(0, 1, RelativeUnit.Relative));
            _plinth = new ImmutableLinearGradientBrush(
                [
                    new ImmutableGradientStop(0, Oklch.ToColor(0.86 - d, 0.012, 80)),
                    new ImmutableGradientStop(0.6, Oklch.ToColor(0.74 - d, 0.012, 70)),
                    new ImmutableGradientStop(1, Oklch.ToColor(0.6 - d, 0.01, 60))
                ],
                startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
                endPoint: new RelativePoint(1, 1, RelativeUnit.Relative));

            _bandShapes = new Geometry[s_radii.Length - 1];
            for (var i = 0; i < _bandShapes.Length; i++)
            {
                _bandShapes[i] = Sector(s_radii[i], s_radii[i + 1]);
            }
            _seams = Seams();
            _fin1 = Crescent(new Point(380, 190), new Point(590, 860), 0.6, 0.34);
            _finEdge = Crescent(new Point(610, 300), new Point(770, 1000), 0.36, 0.2);
            _shadowShape = Polygon(new Point(0, 610), new Point(240, 760), new Point(470, 1000), new Point(0, 1000));
            _plinthShape = Plinth();
            _plinthSeams = PlinthSeams();
        }

        private static IBrush Vertical(Color top, Color bottom) => new ImmutableLinearGradientBrush(
            [new ImmutableGradientStop(0, top), new ImmutableGradientStop(1, bottom)],
            startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
            endPoint: new RelativePoint(0, 1, RelativeUnit.Relative));

        private static Point OnCircle(double radius, double angle) =>
            new(s_center.X + radius * Math.Cos(angle), s_center.Y - radius * Math.Sin(angle));

        // Angles (radians from the +x axis, counter-clockwise) wide enough to cross the whole canvas.
        private const double From = 0.02;
        private const double To = 1.45;

        private static Geometry Sector(double inner, double outer)
        {
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(OnCircle(inner, From), true);
                g.LineTo(OnCircle(outer, From));
                g.ArcTo(OnCircle(outer, To), new Size(outer, outer), 0, false, SweepDirection.CounterClockwise);
                g.LineTo(OnCircle(inner, To));
                g.ArcTo(OnCircle(inner, From), new Size(inner, inner), 0, false, SweepDirection.Clockwise);
                g.EndFigure(true);
            }
            return geometry;
        }

        private static Geometry Seams()
        {
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                // Seams along each band (three per band) and joints across them, staggered from band to band.
                for (var i = 0; i < s_radii.Length - 1; i++)
                {
                    var inner = s_radii[i];
                    var outer = s_radii[i + 1];
                    for (var k = 1; k <= 2; k++)
                    {
                        var r = inner + (outer - inner) * k / 3.0;
                        g.BeginFigure(OnCircle(r, From), false);
                        g.ArcTo(OnCircle(r, To), new Size(r, r), 0, false, SweepDirection.CounterClockwise);
                        g.EndFigure(false);
                    }
                    for (var a = From + 0.04 + 0.04 * (i % 2); a < To; a += 0.085)
                    {
                        g.BeginFigure(OnCircle(inner, a), false);
                        g.LineTo(OnCircle(outer, a));
                        g.EndFigure(false);
                    }
                }
            }
            return geometry;
        }

        /// <summary>A tall curved fin: two arcs from <paramref name="top"/> to <paramref name="bottom"/>, bulging by different amounts.</summary>
        private static Geometry Crescent(Point top, Point bottom, double outerBulge, double innerBulge)
        {
            var length = Math.Sqrt((bottom.X - top.X) * (bottom.X - top.X) + (bottom.Y - top.Y) * (bottom.Y - top.Y));
            var outer = length / (2 * Math.Sin(outerBulge));
            var inner = length / (2 * Math.Sin(innerBulge));
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(top, true);
                g.ArcTo(bottom, new Size(outer, outer), 0, false, SweepDirection.Clockwise);
                g.ArcTo(top, new Size(inner, inner), 0, false, SweepDirection.CounterClockwise);
                g.EndFigure(true);
            }
            return geometry;
        }

        private static Geometry Polygon(params Point[] points)
        {
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(points[0], true);
                for (var i = 1; i < points.Length; i++)
                {
                    g.LineTo(points[i]);
                }
                g.EndFigure(true);
            }
            return geometry;
        }

        // The pale curved panel across the bottom, like the sunlit base of the facade.
        private static Geometry Plinth()
        {
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(new Point(0, 760), true);
                g.QuadraticBezierTo(new Point(330, 650), new Point(800, 700));
                g.LineTo(new Point(800, 1000));
                g.LineTo(new Point(0, 1000));
                g.EndFigure(true);
            }
            return geometry;
        }

        private static Geometry PlinthSeams()
        {
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                for (var k = 1; k <= 4; k++)
                {
                    var y = 760 + 60 * k;
                    g.BeginFigure(new Point(0, y), false);
                    g.QuadraticBezierTo(new Point(330, 650 + 66 * k), new Point(800, 700 + 58 * k));
                    g.EndFigure(false);
                }
                for (var x = 90.0; x < 800; x += 140)
                {
                    g.BeginFigure(new Point(x, 700), false);
                    g.LineTo(new Point(x - 60, 1000));
                    g.EndFigure(false);
                }
            }
            return geometry;
        }

        public void Draw(DrawingContext context)
        {
            context.DrawRectangle(_sky, null, new Rect(0, 0, CanvasWidth, CanvasHeight));
            for (var i = _bandShapes.Length - 1; i >= 0; i--)
            {
                context.DrawGeometry(_bands[i], _edge, _bandShapes[i]);
            }
            context.DrawGeometry(null, _seam, _seams);
            context.DrawGeometry(_finLight, _edge, _finEdge);
            context.DrawGeometry(_fin, _edge, _fin1);
            context.DrawGeometry(_plinth, _edge, _plinthShape);
            context.DrawGeometry(null, _seam, _plinthSeams);
            context.DrawGeometry(_shadow, null, _shadowShape);
        }
    }
}
