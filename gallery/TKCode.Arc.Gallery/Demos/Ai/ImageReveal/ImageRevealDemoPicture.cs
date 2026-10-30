using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The "generated" picture the demos hand to <see cref="ImageReveal"/>: a painted greenhouse at noon (gradients and simple
/// shapes) rendered once into a bitmap. A real app passes the decoded image its model returned.
/// </summary>
internal static class ImageRevealDemoPicture
{
    private static Bitmap? s_picture;

    /// <summary>The picture, painted on first use.</summary>
    public static Bitmap Get()
    {
        if (s_picture is not null)
        {
            return s_picture;
        }
        var target = new RenderTargetBitmap(new PixelSize(960, 1200), new Vector(96, 96));
        using (var context = target.CreateDrawingContext())
        {
            Paint(context, new Rect(0, 0, 960, 1200));
        }
        s_picture = target;
        return target;
    }

    private static void Paint(DrawingContext context, Rect bounds)
    {
        var sky = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#CFE8D6"), 0), new GradientStop(Color.Parse("#F3EBD2"), 0.55), new GradientStop(Color.Parse("#7EA36F"), 1) },
        };
        context.FillRectangle(sky, bounds);
        var w = bounds.Width;
        var h = bounds.Height;
        var glass = new SolidColorBrush(Color.Parse("#FFFFFF"), 0.35);
        var frame = new Pen(new SolidColorBrush(Color.Parse("#3F5A48")), 6);
        var house = Geometry.Parse($"M{w * 0.12},{h * 0.86} L{w * 0.12},{h * 0.42} L{w * 0.5},{h * 0.18} L{w * 0.88},{h * 0.42} L{w * 0.88},{h * 0.86} Z");
        context.DrawGeometry(glass, frame, house);
        for (var i = 1; i < 6; i++)
        {
            var x = w * (0.12 + 0.76 * i / 6);
            context.DrawLine(frame, new Point(x, h * 0.86), new Point(x, h * (0.42 - 0.24 * (1 - Math.Abs(i - 3) / 3.0))));
        }
        context.DrawLine(frame, new Point(w * 0.12, h * 0.6), new Point(w * 0.88, h * 0.6));
        var leaf = new SolidColorBrush(Color.Parse("#4E8A4F"));
        var deep = new SolidColorBrush(Color.Parse("#2F6B3F"));
        var fruit = new SolidColorBrush(Color.Parse("#F2A93B"));
        for (var i = 0; i < 9; i++)
        {
            var cx = w * (0.18 + i * 0.08);
            var cy = h * (0.74 - (i % 3) * 0.05);
            context.DrawEllipse(i % 2 == 0 ? leaf : deep, null, new Point(cx, cy), w * 0.07, h * 0.06);
            context.DrawEllipse(fruit, null, new Point(cx + w * 0.02, cy - h * 0.01), w * 0.012, w * 0.012);
        }
        context.FillRectangle(new SolidColorBrush(Color.Parse("#8A6A4A")), new Rect(0, h * 0.86, w, h * 0.14));
        context.DrawEllipse(new SolidColorBrush(Color.Parse("#FFF6DA"), 0.8), null, new Point(w * 0.78, h * 0.12), w * 0.06, w * 0.06);
    }
}
