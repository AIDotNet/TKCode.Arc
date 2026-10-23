using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The CtaSection demos' pictures: eight invented people drawn in code (a gradient ground, shoulders, a head and hair),
/// never photographs. Four are the faces beside the Centered note, four are the teammates on the setup card. Each picture
/// has a one-line description for screen readers; the block draws them as decoration.
/// </summary>
public static class CtaSectionDemoData
{
    private sealed record Person(string Name, string Description, double GroundHue, double ShirtHue, Color Skin, Color Hair, int Style);

    private static readonly Person[] s_people =
    [
        new("Sofia Ramirez", "Sofia: warm skin, long dark hair, a green top.", 150, 160, Color.FromRgb(196, 144, 112), Color.FromRgb(40, 28, 24), 0),
        new("Nathan Cole", "Nathan: fair skin, short light hair, a blue shirt.", 235, 250, Color.FromRgb(232, 200, 176), Color.FromRgb(190, 160, 110), 1),
        new("Chloe Nguyen", "Chloe: light brown skin, black hair in a bob, a clay top.", 30, 20, Color.FromRgb(214, 168, 136), Color.FromRgb(24, 22, 26), 2),
        new("Tyler Hayes", "Tyler: deep brown skin, short black hair, a grey shirt.", 60, 285, Color.FromRgb(120, 80, 60), Color.FromRgb(20, 18, 16), 1),
        new("Emma Collins", "Emma: fair skin, auburn hair, a rose top.", 340, 350, Color.FromRgb(236, 206, 184), Color.FromRgb(140, 70, 44), 0),
        new("Marcus Johnson", "Marcus: brown skin, a short beard, a teal shirt.", 190, 200, Color.FromRgb(150, 104, 78), Color.FromRgb(30, 24, 22), 1),
        new("Jasmine Brooks", "Jasmine: medium skin, curly dark hair, an ochre top.", 75, 60, Color.FromRgb(180, 126, 96), Color.FromRgb(34, 24, 20), 2),
        new("Daniel Kim", "Daniel: light skin, black hair, an indigo shirt.", 270, 235, Color.FromRgb(226, 190, 160), Color.FromRgb(22, 20, 24), 1)
    ];

    private static IImage[]? s_images;

    /// <summary>The faces beside the Centered note: four people.</summary>
    public static IList<IImage> CreateFaces() => Take(0, 4);

    /// <summary>The teammates on the setup card's last step: four other people.</summary>
    public static IList<IImage> CreateSetupFaces() => Take(4, 4);

    /// <summary>One-line descriptions of the eight pictures, in order (faces first, then teammates).</summary>
    public static IReadOnlyList<string> Descriptions { get; } = s_people.Select(p => p.Description).ToArray();

    private static List<IImage> Take(int start, int count)
    {
        s_images ??= s_people.Select(Draw).ToArray();
        return s_images.Skip(start).Take(count).ToList();
    }

    private static IImage Draw(Person person)
    {
        const int size = 96;
        var bitmap = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            var ground = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.2, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.8, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Oklch.ToColor(0.80, 0.06, person.GroundHue), 0),
                    new GradientStop(Oklch.ToColor(0.62, 0.07, person.GroundHue + 14), 1)
                }
            };
            context.DrawRectangle(ground, null, new Rect(0, 0, size, size));
            var shirt = new SolidColorBrush(Oklch.ToColor(0.46, 0.08, person.ShirtHue));
            var shoulders = new StreamGeometry();
            using (var g = shoulders.Open())
            {
                g.BeginFigure(new Point(-4, size + 2), true);
                g.LineTo(new Point(10, 74));
                g.QuadraticBezierTo(new Point(20, 62), new Point(38, 61));
                g.LineTo(new Point(58, 61));
                g.QuadraticBezierTo(new Point(76, 62), new Point(86, 74));
                g.LineTo(new Point(size + 4, size + 2));
                g.EndFigure(true);
            }
            context.DrawGeometry(shirt, null, shoulders);
            var skin = new SolidColorBrush(person.Skin);
            var hair = new SolidColorBrush(person.Hair);
            context.DrawRectangle(skin, null, new RoundedRect(new Rect(41, 50, 14, 18), 5));
            if (person.Style == 2)
            {
                // A fuller back layer of hair behind the head.
                context.DrawEllipse(hair, null, new Point(48, 40), 25, 27);
            }
            context.DrawEllipse(skin, null, new Point(48, 38), 17, 19);
            var cap = new StreamGeometry();
            using (var g = cap.Open())
            {
                g.BeginFigure(new Point(29, 36), true);
                g.ArcTo(new Point(67, 36), new Size(19, 22), 0, false, SweepDirection.Clockwise);
                g.QuadraticBezierTo(new Point(48, person.Style == 0 ? 28 : 24), new Point(29, 36));
                g.EndFigure(true);
            }
            context.DrawGeometry(hair, null, cap);
            if (person.Style == 0)
            {
                // Long hair falls past the jaw on both sides.
                context.DrawRectangle(hair, null, new RoundedRect(new Rect(27, 34, 7, 30), 4));
                context.DrawRectangle(hair, null, new RoundedRect(new Rect(62, 34, 7, 30), 4));
            }
        }
        return bitmap;
    }
}
