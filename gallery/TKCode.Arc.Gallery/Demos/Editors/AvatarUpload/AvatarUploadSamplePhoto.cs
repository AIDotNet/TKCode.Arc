using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A stand-in photo for the avatar demos, drawn in code: a portrait-like figure against a colored backdrop.</summary>
internal static class AvatarUploadSamplePhoto
{
    /// <summary>The people of the sample photos (invented): name, backdrop colors, skin, hair, clothes.</summary>
    internal static readonly (string Name, string From, string To, string Skin, string Neck, string Hair, string Clothes)[] People =
    [
        ("Imani Okoro", "#F6C28B", "#D9577A", "#C98B6B", "#B9785A", "#3B2A25", "#2F3E66"),
        ("Mateus Brandt", "#8FD3C8", "#3E7CB1", "#EBC3A1", "#DDB08C", "#7A4B24", "#33453C"),
        ("Soo-ah Lindqvist", "#C9B6F2", "#5B5BD6", "#F2D2B6", "#E4BD9C", "#1F1B24", "#7A3B4E")
    ];

    public static Bitmap Create(int width = 900, int height = 640) => Create(0, width, height);

    /// <summary>The photo of <c>People[person]</c>.</summary>
    public static Bitmap Create(int person, int width, int height)
    {
        var look = People[person % People.Length];
        var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using var context = bitmap.CreateDrawingContext();
        var backdrop = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse(look.From), 0), new GradientStop(Color.Parse(look.To), 1) }
        };
        context.FillRectangle(backdrop, new Rect(0, 0, width, height));
        var cx = width * (0.5 + 0.08 * (person % 3 - 1));
        // Shoulders, neck, head and hair; the hair differs per person.
        context.DrawEllipse(new SolidColorBrush(Color.Parse(look.Clothes)), null, new Point(cx, height * 1.02), width * 0.26, height * 0.34);
        context.DrawRectangle(new SolidColorBrush(Color.Parse(look.Neck)), null, new Rect(cx - 34, height * 0.52, 68, 90));
        var hair = new SolidColorBrush(Color.Parse(look.Hair));
        if (person % 3 == 1)
        {
            // Longer hair behind the head.
            context.DrawEllipse(hair, null, new Point(cx, height * 0.46), 128, 150);
        }
        context.DrawEllipse(new SolidColorBrush(Color.Parse(look.Skin)), null, new Point(cx, height * 0.42), 104, 124);
        context.DrawEllipse(hair, null, new Point(cx, height * (person % 3 == 2 ? 0.28 : 0.31)), person % 3 == 2 ? 108 : 116, person % 3 == 2 ? 50 : 70);
        return bitmap;
    }
}
