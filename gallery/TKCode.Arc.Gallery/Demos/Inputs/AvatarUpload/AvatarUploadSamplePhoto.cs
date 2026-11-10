using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A stand-in photo for the avatar demos, drawn in code: a portrait-like figure against a warm backdrop.</summary>
internal static class AvatarUploadSamplePhoto
{
    public static Bitmap Create(int width = 900, int height = 640)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using var context = bitmap.CreateDrawingContext();
        var backdrop = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#F6C28B"), 0), new GradientStop(Color.Parse("#D9577A"), 1) }
        };
        context.FillRectangle(backdrop, new Rect(0, 0, width, height));
        var cx = width * 0.58;
        // Shoulders, neck and head.
        context.DrawEllipse(new SolidColorBrush(Color.Parse("#2F3E66")), null, new Point(cx, height * 1.02), width * 0.26, height * 0.34);
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#B9785A")), null, new Rect(cx - 34, height * 0.52, 68, 90));
        context.DrawEllipse(new SolidColorBrush(Color.Parse("#C98B6B")), null, new Point(cx, height * 0.42), 104, 124);
        context.DrawEllipse(new SolidColorBrush(Color.Parse("#3B2A25")), null, new Point(cx, height * 0.31), 116, 70);
        return bitmap;
    }
}
