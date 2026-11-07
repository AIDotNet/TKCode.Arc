using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>A sample avatar choice for the <see cref="SheetStack"/> demos: initials on a color, or a drawn picture.</summary>
/// <param name="Id">Stable id of the choice.</param>
/// <param name="Name">Accessible name of the choice ("Ridge", "Initials, blue").</param>
/// <param name="Image">The drawn picture, or null for an initials avatar.</param>
/// <param name="Background">Fill behind the initials (and behind a picture while it loads).</param>
/// <param name="Foreground">Color of the initials.</param>
public sealed record SheetStackSampleAvatar(string Id, string Name, IImage? Image, IBrush Background, IBrush Foreground);

/// <summary>
/// Invented sample data for the <see cref="SheetStack"/> demos: the account settings of a fictional notes app, a person
/// with initials avatars and four code-drawn pictures (no photos), and the status sentences the preview announces.
/// </summary>
public static class SheetStackSamples
{
    /// <summary>The person's name.</summary>
    public const string Name = "Mira Rautio";

    /// <summary>The person's role.</summary>
    public const string Role = "Field researcher";

    /// <summary>The person's team.</summary>
    public const string Team = "Atlas crew";

    /// <summary>What the preview announces when it loads.</summary>
    public const string Intro =
        "Three levels deep: account, then profile, then avatar. Each level slides back the way it came.";

    /// <summary>Announced after the profile was saved.</summary>
    public const string Saved = "Saved. Your name card reflects it.";

    /// <summary>Announced after the avatar was cleared.</summary>
    public const string Cleared = "Back to plain initials.";

    /// <summary>Announced after a reset.</summary>
    public const string ResetDone = "Demo restored to its first state.";

    /// <summary>The announcement for a settings switch: its name and state, "Spell check: off".</summary>
    public static string Toggled(string what, bool on) => $"{what}: {(on ? "on" : "off")}";

    /// <summary>The first letters of the first two words, upper case ("MR").</summary>
    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var first = char.ToUpperInvariant(parts[0][0]).ToString();
        return parts.Length > 1 ? first + char.ToUpperInvariant(parts[1][0]) : first;
    }

    private static readonly IBrush s_white = new ImmutableSolidColorBrush(Colors.White);

    /// <summary>The six avatar choices of the preview, in grid order.</summary>
    public static IReadOnlyList<SheetStackSampleAvatar> Avatars { get; } =
    [
        new("initials-blue", "Initials, blue", null, Solid(0x1F, 0x7D, 0xCF), s_white),
        new("initials-green", "Initials, green", null, Solid(0x00, 0x8C, 0x75), s_white),
        new("ridge", "Ridge", Ridge(), Solid(0x6D, 0x8F, 0xC7), s_white),
        new("stripes", "Stripes", Stripes(), Solid(0xE8, 0x6A, 0x3A), s_white),
        new("sunset-sea", "Sunset sea", SunsetSea(), Solid(0xF4, 0x84, 0x5F), s_white),
        new("circles", "Circles", Circles(), Solid(0xF1, 0xED, 0xE4), s_white),
    ];

    /// <summary>The choice the preview starts with.</summary>
    public static SheetStackSampleAvatar DefaultAvatar => Avatars[2];

    private static ImmutableSolidColorBrush Solid(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));

    private static IImage Picture(params Drawing[] layers)
    {
        var group = new DrawingGroup { ClipGeometry = new RectangleGeometry(new Rect(0, 0, 120, 120)) };
        // A transparent frame fixes the picture's extent at 120×120 whatever the layers cover.
        group.Children.Add(new GeometryDrawing { Brush = Brushes.Transparent, Geometry = new RectangleGeometry(new Rect(0, 0, 120, 120)) });
        foreach (var layer in layers)
        {
            group.Children.Add(layer);
        }
        return new DrawingImage(group);
    }

    private static GeometryDrawing Fill(IBrush brush, Geometry geometry) => new() { Brush = brush, Geometry = geometry };

    private static StreamGeometry Polygon(params Point[] points)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(points[0], true);
        for (var i = 1; i < points.Length; i++)
        {
            context.LineTo(points[i]);
        }
        context.EndFigure(true);
        return geometry;
    }

    private static LinearGradientBrush Vertical(Color top, Color bottom) => new()
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(top, 0), new GradientStop(bottom, 1) }
    };

    /// <summary>Two mountain ridges under a dawn sky.</summary>
    private static IImage Ridge() => Picture(
        Fill(Vertical(Color.FromRgb(0x6D, 0x8F, 0xC7), Color.FromRgb(0xF3, 0xC9, 0xA1)), new RectangleGeometry(new Rect(0, 0, 120, 120))),
        Fill(new ImmutableSolidColorBrush(Color.FromRgb(0x7A, 0x86, 0xA8)), Polygon(new(0, 86), new(30, 52), new(52, 70), new(80, 38), new(120, 78), new(120, 120), new(0, 120))),
        Fill(new ImmutableSolidColorBrush(Color.FromRgb(0x33, 0x42, 0x5C)), Polygon(new(0, 100), new(24, 80), new(46, 94), new(70, 66), new(96, 92), new(120, 84), new(120, 120), new(0, 120))));

    /// <summary>Diagonal bands in two colors, 8 units wide.</summary>
    private static IImage Stripes() => Picture(
        Fill(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(11.31, 11.31, RelativeUnit.Absolute),
            SpreadMethod = GradientSpreadMethod.Repeat,
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0xE8, 0x6A, 0x3A), 0), new GradientStop(Color.FromRgb(0xE8, 0x6A, 0x3A), 0.5),
                new GradientStop(Color.FromRgb(0x2B, 0x59, 0xC3), 0.5), new GradientStop(Color.FromRgb(0x2B, 0x59, 0xC3), 1)
            }
        }, new RectangleGeometry(new Rect(0, 0, 120, 120))));

    /// <summary>A low sun over a calm sea with a few swell lines.</summary>
    private static IImage SunsetSea()
    {
        var swell = new GeometryGroup();
        for (var i = 0; i < 4; i++)
        {
            var y = 76 + i * 11;
            swell.Children.Add(new LineGeometry(new Point(14 + i * 6, y), new Point(106 - i * 9, y)));
        }
        return Picture(
            Fill(Vertical(Color.FromRgb(0xF7, 0xB2, 0x67), Color.FromRgb(0xE0, 0x6C, 0x6C)), new RectangleGeometry(new Rect(0, 0, 120, 66))),
            Fill(new ImmutableSolidColorBrush(Color.FromRgb(0xFF, 0xE2, 0x9A)), new EllipseGeometry(new Rect(40, 34, 40, 40))),
            Fill(Vertical(Color.FromRgb(0x2E, 0x4A, 0x62), Color.FromRgb(0x1C, 0x2D, 0x40)), new RectangleGeometry(new Rect(0, 64, 120, 56))),
            new GeometryDrawing { Pen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(0xB0, 0x8F, 0xB0, 0xC8)), 2.5, lineCap: PenLineCap.Round), Geometry = swell });
    }

    /// <summary>Three overlapping circles.</summary>
    private static IImage Circles() => Picture(
        Fill(new ImmutableSolidColorBrush(Color.FromRgb(0xF1, 0xED, 0xE4)), new RectangleGeometry(new Rect(0, 0, 120, 120))),
        Fill(new ImmutableSolidColorBrush(Color.FromArgb(0xCC, 0x3A, 0x7B, 0xD5)), new EllipseGeometry(new Rect(14, 22, 58, 58))),
        Fill(new ImmutableSolidColorBrush(Color.FromArgb(0xCC, 0x9B, 0x4D, 0xCA)), new EllipseGeometry(new Rect(48, 22, 58, 58))),
        Fill(new ImmutableSolidColorBrush(Color.FromArgb(0xCC, 0xE5, 0x8F, 0x00)), new EllipseGeometry(new Rect(31, 50, 58, 58))));
}
