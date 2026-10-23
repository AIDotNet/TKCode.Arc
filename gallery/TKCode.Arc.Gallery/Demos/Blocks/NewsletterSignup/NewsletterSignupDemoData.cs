using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The NewsletterSignup demos' publication: Arc's sample "Margins" with its copy, and thumbnails and reader faces drawn in
/// code (gradient scenes and abstract portraits, never photos). Every picture has a one-line English description in
/// <see cref="Descriptions"/>. Deterministic: the same key always gives the same picture.
/// </summary>
public static class NewsletterSignupDemoData
{
    private const int TilePixels = 88;
    private const int FacePixels = 56;

    private enum Kind { Chair, Bowls, Hall, Sunroom, Lamp, Tram, Ridges, Facade, Carafe, Office, Dinner }

    private static readonly Dictionary<string, (Kind Kind, double Hue, string Description)> s_tiles = new()
    {
        ["reading-chair"] = (Kind.Chair, 150, "A grey armchair silhouette on a deep green wall."),
        ["stacked-bowls"] = (Kind.Bowls, 40, "Two stacked speckled bowls against a dark wall."),
        ["concert-hall"] = (Kind.Hall, 230, "Curved steel panels against a blue sky."),
        ["sunroom"] = (Kind.Sunroom, 80, "A round table in a sunroom with tall windows."),
        ["ceramic-lamp"] = (Kind.Lamp, 55, "A ceramic lamp with a linen shade on a sideboard."),
        ["lisbon-tram"] = (Kind.Tram, 85, "A yellow tram on a street of pale buildings."),
        ["mountain-ridges"] = (Kind.Ridges, 20, "Layered mountain ridges under a warm evening sky."),
        ["table-lamp"] = (Kind.Lamp, 280, "A glowing table lamp beside a small vase."),
        ["curved-facade"] = (Kind.Facade, 200, "A white facade with curved balconies."),
        ["glass-carafe"] = (Kind.Carafe, 190, "A ribbed glass carafe pouring into a tumbler."),
        ["home-office"] = (Kind.Office, 160, "A wooden desk against deep green walls."),
        ["salmon-dinner"] = (Kind.Dinner, 30, "A plate of seared salmon beside a glass of wine.")
    };

    private static readonly Dictionary<string, IImage> s_images = new();

    /// <summary>One-line English description of each generated picture, by key (the stories and faces).</summary>
    public static IReadOnlyDictionary<string, string> Descriptions { get; } = BuildDescriptions();

    private static Dictionary<string, string> BuildDescriptions()
    {
        var map = s_tiles.ToDictionary(p => p.Key, p => p.Value.Description);
        map["face-jasmine"] = "An abstract portrait on a warm sand ground.";
        map["face-daniel"] = "An abstract portrait on a dusty blue ground.";
        map["face-ava"] = "An abstract portrait on a sage ground.";
        return map;
    }

    /// <summary>Arc's publication "Margins" (issues 146 to 149) with a generated thumbnail for every story.</summary>
    public static NewsletterPublication CreatePublication()
    {
        var publication = new NewsletterPublication
        {
            Name = "Margins",
            Upcoming = Issue(149, "Friday, October 2", "Designing for the second visit, not the first",
                Story("Empty states that invite a first step", "reading-chair", 5),
                Story("Texture without noise, from a ceramics studio", "stacked-bowls", 4),
                Story("How a concert hall seats two thousand people", "concert-hall", 7))
        };
        publication.Recent.Add(Issue(148, "Friday, September 25", "Why the best settings pages feel quiet",
            Story("What a sunroom teaches about contrast", "sunroom", 4),
            Story("The case for a single accent color", "ceramic-lamp", 3),
            Story("Lisbon tram signs and wayfinding at scale", "lisbon-tram", 6)));
        publication.Recent.Add(Issue(147, "Friday, September 18", "Springs that settle instead of bounce",
            Story("Reading the ridge line of an animation curve", "mountain-ridges", 5),
            Story("A table lamp and the warmth of dark mode", "table-lamp", 3),
            Story("Curves in architecture and in corners", "curved-facade", 4)));
        publication.Recent.Add(Issue(146, "Friday, September 11", "Copy that sounds like a person",
            Story("Error messages that take the blame", "glass-carafe", 4),
            Story("Writing buttons as verbs", "home-office", 3),
            Story("Tone of voice at the dinner table", "salmon-dinner", 5)));
        return publication;
    }

    /// <summary>A custom issue for the event-log demo: three stories with generated thumbnails.</summary>
    public static NewsletterIssue CreateIssue(int number, string date, string subject) =>
        Issue(number, date, subject,
            Story("Notes on a quiet interface", "table-lamp", 4),
            Story("Why lists need room to breathe", "sunroom", 5),
            Story("The long view of a design system", "mountain-ridges", 6));

    /// <summary>Three generated reader faces.</summary>
    public static List<IImage> CreateFaces() =>
    [
        Face("face-jasmine", 55, 40),
        Face("face-daniel", 235, 255),
        Face("face-ava", 150, 120)
    ];

    private static NewsletterIssue Issue(int number, string date, string subject, params NewsletterStory[] stories)
    {
        var issue = new NewsletterIssue { Number = number, Date = date, Subject = subject };
        issue.Stories.AddRange(stories);
        return issue;
    }

    private static NewsletterStory Story(string title, string key, int minutes) =>
        new() { Title = title, Minutes = minutes, Image = Tile(key) };

    private static IImage Tile(string key)
    {
        if (s_images.TryGetValue(key, out var cached))
        {
            return cached;
        }
        var (kind, hue, _) = s_tiles[key];
        var bitmap = new RenderTargetBitmap(new PixelSize(TilePixels, TilePixels), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            const double s = TilePixels;
            var sky = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Oklch.ToColor(0.82, 0.06, hue), 0),
                    new GradientStop(Oklch.ToColor(0.60, 0.07, hue + 20), 1)
                }
            };
            context.DrawRectangle(sky, null, new Rect(0, 0, s, s));
            var ink = new SolidColorBrush(Oklch.ToColor(0.32, 0.04, hue + 10));
            var pale = new SolidColorBrush(Oklch.ToColor(0.94, 0.02, hue));
            switch (kind)
            {
                case Kind.Chair:
                    context.DrawRectangle(ink, null, new RoundedRect(new Rect(24, 30, 40, 30), 10));
                    context.DrawRectangle(ink, null, new RoundedRect(new Rect(18, 46, 52, 22), 8));
                    break;
                case Kind.Bowls:
                    context.DrawEllipse(pale, null, new Point(44, 54), 26, 14);
                    context.DrawEllipse(ink, null, new Point(44, 38), 18, 10);
                    break;
                case Kind.Hall:
                    for (var i = 0; i < 4; i++)
                    {
                        context.DrawEllipse(i % 2 == 0 ? pale : ink, null, new Point(18 + i * 18, 60), 20, 34);
                    }
                    break;
                case Kind.Sunroom:
                    context.DrawRectangle(pale, null, new RoundedRect(new Rect(14, 14, 24, 40), 12));
                    context.DrawRectangle(pale, null, new RoundedRect(new Rect(50, 14, 24, 40), 12));
                    context.DrawEllipse(ink, null, new Point(44, 66), 22, 7);
                    break;
                case Kind.Lamp:
                    context.DrawRectangle(ink, null, new Rect(41, 40, 6, 32));
                    context.DrawGeometry(pale, null, Trapezoid(44, 18, 34, 18, 26));
                    break;
                case Kind.Tram:
                    context.DrawRectangle(new SolidColorBrush(Oklch.ToColor(0.86, 0.15, 90)), null, new RoundedRect(new Rect(12, 32, 64, 30), 6));
                    context.DrawEllipse(ink, null, new Point(26, 64), 5, 5);
                    context.DrawEllipse(ink, null, new Point(62, 64), 5, 5);
                    break;
                case Kind.Ridges:
                    context.DrawGeometry(new SolidColorBrush(Oklch.ToColor(0.55, 0.06, hue + 200)), null, Trapezoid(30, 30, 0, 60, 30));
                    context.DrawGeometry(ink, null, Trapezoid(60, 40, 0, 70, 20));
                    break;
                case Kind.Facade:
                    for (var i = 0; i < 3; i++)
                    {
                        context.DrawEllipse(pale, null, new Point(18 + i * 26, 40), 12, 12);
                        context.DrawRectangle(pale, null, new Rect(6 + i * 26, 40, 24, 36));
                    }
                    break;
                case Kind.Carafe:
                    context.DrawRectangle(pale, null, new RoundedRect(new Rect(32, 24, 24, 44), 10));
                    context.DrawRectangle(pale, null, new Rect(38, 12, 12, 14));
                    break;
                case Kind.Office:
                    context.DrawRectangle(ink, null, new Rect(10, 46, 68, 6));
                    context.DrawRectangle(ink, null, new Rect(16, 52, 5, 22));
                    context.DrawRectangle(ink, null, new Rect(67, 52, 5, 22));
                    context.DrawRectangle(pale, null, new RoundedRect(new Rect(32, 22, 26, 20), 3));
                    break;
                default:
                    context.DrawEllipse(pale, null, new Point(40, 48), 26, 26);
                    context.DrawEllipse(new SolidColorBrush(Oklch.ToColor(0.7, 0.14, 40)), null, new Point(40, 48), 12, 8);
                    context.DrawRectangle(ink, null, new RoundedRect(new Rect(66, 28, 10, 36), 5));
                    break;
            }
        }
        s_images[key] = bitmap;
        return bitmap;
    }

    private static Geometry Trapezoid(double centerX, double top, double topInset, double bottomWidth, double height)
    {
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            var half = bottomWidth / 2;
            g.BeginFigure(new Point(centerX - half + topInset, top), true);
            g.LineTo(new Point(centerX + half - topInset, top));
            g.LineTo(new Point(centerX + half, top + height));
            g.LineTo(new Point(centerX - half, top + height));
            g.EndFigure(true);
        }
        return geometry;
    }

    private static IImage Face(string key, double groundHue, double shirtHue)
    {
        if (s_images.TryGetValue(key, out var cached))
        {
            return cached;
        }
        var bitmap = new RenderTargetBitmap(new PixelSize(FacePixels, FacePixels), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            const double s = FacePixels;
            context.DrawRectangle(new SolidColorBrush(Oklch.ToColor(0.78, 0.07, groundHue)), null, new Rect(0, 0, s, s));
            context.DrawEllipse(new SolidColorBrush(Oklch.ToColor(0.45, 0.08, shirtHue)), null, new Point(s / 2, s + 6), 24, 22);
            context.DrawEllipse(new SolidColorBrush(Oklch.ToColor(0.70, 0.03, 55)), null, new Point(s / 2, 24), 10, 11);
            context.DrawEllipse(new SolidColorBrush(Oklch.ToColor(0.25, 0.02, groundHue)), null, new Point(s / 2, 17), 11, 7);
        }
        s_images[key] = bitmap;
        return bitmap;
    }
}
