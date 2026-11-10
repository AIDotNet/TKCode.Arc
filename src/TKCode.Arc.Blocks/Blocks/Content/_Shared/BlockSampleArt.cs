using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Abstract pictures for the Content blocks' sample data, drawn from a hue: never photos, and nothing to download.</summary>
internal static class BlockSampleArt
{
    private static readonly Dictionary<(double, int), IImage> s_cache = new();

    /// <summary>A 300 by 200 scene: a gradient ground, a low sun and two ridges.</summary>
    public static IImage Scene(double hue, int variant = 0)
    {
        if (s_cache.TryGetValue((hue, variant), out var cached))
        {
            return cached;
        }
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing
        {
            Geometry = new RectangleGeometry(new Rect(0, 0, 300, 200)),
            Brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Hsl(hue, 0.42, 0.78), 0),
                    new GradientStop(Hsl(hue + 24, 0.5, 0.58), 1)
                }
            }
        });
        var sunX = 70 + variant % 4 * 50;
        group.Children.Add(new GeometryDrawing
        {
            Geometry = new EllipseGeometry(new Rect(sunX, 44, 64, 64)),
            Brush = new SolidColorBrush(Hsl(hue + 48, 0.7, 0.86))
        });
        group.Children.Add(new GeometryDrawing
        {
            Geometry = Geometry.Parse(variant % 2 == 0 ? "M0 140 Q70 96 150 134 T300 118 V200 H0Z" : "M0 120 Q90 150 170 112 T300 138 V200 H0Z"),
            Brush = new SolidColorBrush(Hsl(hue + 8, 0.36, 0.42))
        });
        group.Children.Add(new GeometryDrawing
        {
            Geometry = Geometry.Parse(variant % 2 == 0 ? "M0 172 Q110 142 200 170 T300 160 V200 H0Z" : "M0 160 Q80 182 180 156 T300 176 V200 H0Z"),
            Brush = new SolidColorBrush(Hsl(hue - 12, 0.32, 0.28))
        });
        var image = new DrawingImage { Drawing = group };
        s_cache[(hue, variant)] = image;
        return image;
    }

    private static Color Hsl(double hue, double saturation, double lightness) =>
        new HslColor(1, ((hue % 360) + 360) % 360, saturation, lightness).ToRgb();
}

/// <summary>Scrolls a <see cref="ScrollViewer"/> to an offset on Arc's smooth spring; reduced motion and Off jump.</summary>
/// <remarks>Code-driven because a scroll offset is not a property a ControlTheme transition can reach from its owner.</remarks>
internal sealed class BlockScroller
{
    private readonly Visual _owner;
    private MotionValue<double>? _value;
    private ScrollViewer? _target;

    public BlockScroller(Visual owner) => _owner = owner;

    public void To(ScrollViewer scroller, double y)
    {
        _target = scroller;
        if (_value is null)
        {
            _value = new MotionValue<double>(_owner, scroller.Offset.Y, MotionVectors.Double);
            _value.Changed += v =>
            {
                if (_target is { } target)
                {
                    target.Offset = new Vector(target.Offset.X, Math.Max(0, v));
                }
            };
        }
        _value.Jump(scroller.Offset.Y);
        _ = _value.AnimateTo(Math.Max(0, y), MotionSpec.Spring(ArcSpring.Smooth));
    }
}
