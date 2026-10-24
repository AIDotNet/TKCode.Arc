using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Pictures for the Immersive login demos, drawn in code.</summary>
internal static class LoginImmersiveDemoData
{
    private static IImage? s_harbor;

    /// <summary>A harbor at blue hour: a deep sky over a calm bay, a low moon, a breakwater with three lit windows and their reflections.</summary>
    public static IImage Harbor() => s_harbor ??= DrawHarbor();

    private static IImage DrawHarbor()
    {
        const int Width = 1600;
        const int Height = 1000;
        var bitmap = new RenderTargetBitmap(new PixelSize(Width, Height), new Vector(96, 96));
        using (var dc = bitmap.CreateDrawingContext())
        {
            dc.FillRectangle(Vertical(Color.Parse("#1B2A4A"), Color.Parse("#5B6F98"), Color.Parse("#C79A8A")), new Rect(0, 0, Width, 640));
            dc.FillRectangle(Vertical(Color.Parse("#7E7F99"), Color.Parse("#2D3A5A"), Color.Parse("#141C2E")), new Rect(0, 640, Width, 360));
            dc.DrawEllipse(new SolidColorBrush(Color.Parse("#F3EBDD")), null, new Point(1180, 250), 34, 34);
            dc.DrawEllipse(new RadialGradientBrush
            {
                GradientStops = { new GradientStop(Color.Parse("#66F3EBDD"), 0), new GradientStop(Color.Parse("#00F3EBDD"), 1) }
            }, null, new Point(1180, 250), 180, 180);
            // Hills across the bay.
            var hills = new StreamGeometry();
            using (var g = hills.Open())
            {
                g.BeginFigure(new Point(0, 640), true);
                g.LineTo(new Point(0, 560));
                g.CubicBezierTo(new Point(240, 500), new Point(420, 590), new Point(640, 560));
                g.CubicBezierTo(new Point(860, 530), new Point(1040, 600), new Point(1260, 570));
                g.CubicBezierTo(new Point(1420, 550), new Point(1520, 580), new Point(1600, 575));
                g.LineTo(new Point(1600, 640));
                g.EndFigure(true);
            }
            dc.DrawGeometry(new SolidColorBrush(Color.Parse("#232D46")), null, hills);
            // The breakwater and its lit windows, with long reflections in the water.
            dc.FillRectangle(new SolidColorBrush(Color.Parse("#151B2B")), new Rect(220, 600, 520, 40));
            var window = new SolidColorBrush(Color.Parse("#F2C27A"));
            var lamp = Fade(Color.Parse("#66F2C27A"));
            foreach (var x in new[] { 300.0, 420.0, 610.0 })
            {
                dc.FillRectangle(window, new Rect(x, 612, 14, 10));
                dc.FillRectangle(lamp, new Rect(x + 1, 648, 12, 140));
            }
            dc.FillRectangle(Fade(Color.Parse("#55F3EBDD")), new Rect(1160, 648, 40, 300));
        }
        return bitmap;
    }

    /// <summary>A reflection: <paramref name="color"/> at the waterline fading out below.</summary>
    private static IBrush Fade(Color color) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(color, 0), new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1) }
    };

    private static IBrush Vertical(Color top, Color middle, Color bottom) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(top, 0), new GradientStop(middle, 0.55), new GradientStop(bottom, 1) }
    };
}
