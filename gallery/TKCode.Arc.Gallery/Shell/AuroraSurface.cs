using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// A page header's ground: the muted surface with four soft glows (sand, sage, sky and rose) and a field of dots that
/// fades out toward the edges, all drawn with gradients and a tiled drawing whose colors follow the theme. Nothing is
/// live, so it costs one paint.
/// </summary>
public sealed class AuroraSurface : Panel
{
    private const double DotPitch = 22;

    private readonly List<(GradientStop Inner, GradientStop Outer, Color Light, Color Dark)> _glows = new();
    private readonly SolidColorBrush _dot = new(Colors.Gray);

    public AuroraSurface()
    {
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        AddGlow(new RelativePoint(0.04, 1.0, RelativeUnit.Relative), 0.75, 0.95, Color.FromRgb(0xF2, 0xDC, 0xAE), Color.FromRgb(0x4A, 0x37, 0x18));
        AddGlow(new RelativePoint(0.92, 0.0, RelativeUnit.Relative), 0.62, 0.9, Color.FromRgb(0xCD, 0xE6, 0xDB), Color.FromRgb(0x17, 0x33, 0x2E));
        AddGlow(new RelativePoint(0.55, -0.1, RelativeUnit.Relative), 0.5, 0.8, Color.FromRgb(0xCF, 0xDF, 0xF7), Color.FromRgb(0x1B, 0x2A, 0x4A));
        AddGlow(new RelativePoint(0.98, 1.05, RelativeUnit.Relative), 0.42, 0.7, Color.FromRgb(0xF6, 0xD3, 0xDC), Color.FromRgb(0x44, 0x1F, 0x2E));

        var dots = new Border
        {
            IsHitTestVisible = false,
            Background = new DrawingBrush
            {
                TileMode = TileMode.Tile,
                DestinationRect = new RelativeRect(0, 0, DotPitch, DotPitch, RelativeUnit.Absolute),
                SourceRect = new RelativeRect(0, 0, DotPitch, DotPitch, RelativeUnit.Absolute),
                Drawing = new GeometryDrawing { Brush = _dot, Geometry = new EllipseGeometry(new Rect(DotPitch / 2 - 1, DotPitch / 2 - 1, 2, 2)) }
            },
            OpacityMask = new RadialGradientBrush
            {
                Center = new RelativePoint(0.5, 0.45, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(0.5, 0.45, RelativeUnit.Relative),
                RadiusX = new RelativeScalar(0.7, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(0.9, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Colors.Black, 0), new GradientStop(Colors.Transparent, 1) }
            }
        };
        Children.Add(dots);
        ActualThemeVariantChanged += (_, _) => Apply();
        Apply();
    }

    private void AddGlow(RelativePoint center, double radiusX, double radiusY, Color light, Color dark)
    {
        var inner = new GradientStop(Colors.Transparent, 0);
        var outer = new GradientStop(Colors.Transparent, 1);
        var brush = new RadialGradientBrush
        {
            Center = center,
            GradientOrigin = center,
            RadiusX = new RelativeScalar(radiusX, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(radiusY, RelativeUnit.Relative),
            GradientStops = { inner, outer }
        };
        Children.Add(new Border { Background = brush, IsHitTestVisible = false });
        _glows.Add((inner, outer, light, dark));
    }

    private void Apply()
    {
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        foreach (var (inner, outer, light, darkColor) in _glows)
        {
            var color = dark ? darkColor : light;
            inner.Color = color;
            outer.Color = Color.FromArgb(0, color.R, color.G, color.B);
        }
        _dot.Color = dark ? Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x30, 0x20, 0x20, 0x20);
    }
}
