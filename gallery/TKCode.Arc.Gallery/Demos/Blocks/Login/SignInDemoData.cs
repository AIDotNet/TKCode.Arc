using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// Sample data for the SignIn demos: Arc's five sample teammates with portraits drawn in code (abstract head and
/// shoulders on a warm gradient, never photos: Arc's portraits are not part of its MIT registry). Deterministic: the same
/// name always gives the same picture.
/// </summary>
public static class SignInDemoData
{
    private const int Size = 160;

    private static readonly (string Name, string Email, double Hue, double Skin, double Hair)[] s_teammates =
    [
        ("Emma Collins", "emma@northwind.studio", 40, 0.80, 0.30),
        ("Marcus Johnson", "marcus@northwind.studio", 230, 0.58, 0.20),
        ("Ava Mitchell", "ava@northwind.studio", 330, 0.82, 0.55),
        ("Jasmine Brooks", "jasmine@northwind.studio", 150, 0.55, 0.18),
        ("Sofia Ramirez", "sofia@northwind.studio", 20, 0.72, 0.24)
    ];

    private static readonly Dictionary<string, IImage> s_portraits = new();

    /// <summary>Arc's sample teammates, each with a drawn portrait.</summary>
    public static IEnumerable<SignInAccount> Teammates() =>
        s_teammates.Select(t => new SignInAccount(t.Name, t.Email, Portrait(t.Name, t.Hue, t.Skin, t.Hair)));

    /// <summary>A drawn portrait for a name (warm gradient ground, shoulders, head and a cap of hair).</summary>
    public static IImage Portrait(string name, double hue, double skin, double hair)
    {
        if (s_portraits.TryGetValue(name, out var cached))
        {
            return cached;
        }
        var bitmap = new RenderTargetBitmap(new PixelSize(Size, Size), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            const double w = Size;
            var ground = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Oklch.ToColor(0.86, 0.05, hue), 0),
                    new GradientStop(Oklch.ToColor(0.72, 0.07, (hue + 30) % 360), 1)
                }
            };
            context.DrawRectangle(ground, null, new Rect(0, 0, w, w));

            // Shoulders in a darker shade of the ground's complement.
            var shoulders = new StreamGeometry();
            using (var g = shoulders.Open())
            {
                g.BeginFigure(new Point(-10, w + 10), true);
                g.LineTo(new Point(18, w * 0.86));
                g.QuadraticBezierTo(new Point(30, w * 0.74), new Point(56, w * 0.72));
                g.LineTo(new Point(w - 56, w * 0.72));
                g.QuadraticBezierTo(new Point(w - 30, w * 0.74), new Point(w - 18, w * 0.86));
                g.LineTo(new Point(w + 10, w + 10));
                g.EndFigure(true);
            }
            context.DrawGeometry(new SolidColorBrush(Oklch.ToColor(0.42, 0.06, (hue + 180) % 360)), null, shoulders);

            var skinColor = Oklch.ToColor(skin, 0.05, 55);
            var shade = Oklch.ToColor(skin - 0.08, 0.05, 50);
            context.DrawRectangle(new SolidColorBrush(shade), null, new RoundedRect(new Rect(w * 0.43, w * 0.5, w * 0.14, w * 0.26), 10));
            var head = new RadialGradientBrush
            {
                Center = new RelativePoint(0.42, 0.38, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(0.42, 0.38, RelativeUnit.Relative),
                RadiusX = new RelativeScalar(0.75, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(0.75, RelativeUnit.Relative),
                GradientStops = { new GradientStop(skinColor, 0), new GradientStop(shade, 1) }
            };
            var center = new Point(w * 0.5, w * 0.42);
            var radius = w * 0.2;
            context.DrawEllipse(head, null, center, radius, radius * 1.1);

            // Hair: a cap over the top of the head.
            var cap = new StreamGeometry();
            using (var g = cap.Open())
            {
                var outer = radius * 1.16;
                g.BeginFigure(new Point(center.X - outer, center.Y + radius * 0.1), true);
                g.ArcTo(new Point(center.X + outer, center.Y + radius * 0.1), new Size(outer, outer * 1.1), 0, false, SweepDirection.Clockwise);
                g.QuadraticBezierTo(new Point(center.X, center.Y - radius * 0.5), new Point(center.X - outer, center.Y + radius * 0.1));
                g.EndFigure(true);
            }
            context.DrawGeometry(new SolidColorBrush(Oklch.ToColor(hair, 0.03, 50)), null, cap);
        }
        s_portraits[name] = bitmap;
        return bitmap;
    }
}
