using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Invented sample stories for <see cref="CardToArticle"/> demos and tests: six short magazine pieces (a field guide, an
/// interview, a recipe, a science note, a craft how-to and a sky-watching tip, all written for this library) with 4:3
/// pictures drawn in code as simple geometric scenes (no photos). Every call returns new items, so two controls never
/// share an item.
/// </summary>
public static class CardToArticleSamples
{
    /// <summary>Four stories for the preview: a field guide, an interview, a recipe and a science note.</summary>
    public static IList<CardToArticleItem> Stories() => new AvaloniaList<CardToArticleItem>
    {
        new()
        {
            Id = "cloud-reading",
            Tag = "Field guide",
            Title = "Reading the clouds before a long hike",
            Meta = "5 min read",
            Image = CardToArticleSampleArt.Clouds(),
            ImageAlt = "Tall white clouds building over layered mountain ridges at midday",
            Author = "Sigrid Vale",
            Published = "Oct 21, 2026",
            Body = """
                Most mountain storms announce themselves hours ahead. You only need to know which clouds are messengers and which are just passing through.

                Flat grey sheets that thicken from the west usually mean a front by evening. White heaps that keep growing before noon are the ones to watch from a summer ridge.

                > If the cloud tops are still climbing at lunch, plan to be off the summit by two.

                Keep a small notebook of what you saw at the trailhead and what the afternoon brought. After a season, the sky over your own hills becomes easy to read.
                """
        },
        new()
        {
            Id = "island-ferry",
            Tag = "Interview",
            Title = "A morning aboard the island ferry",
            Meta = "4 min read",
            Image = CardToArticleSampleArt.Ferry(),
            ImageAlt = "A small white ferry crossing dark water in front of a row of painted waterfront houses",
            Author = "Tomas Brekke",
            Published = "Oct 14, 2026",
            Body = """
                Captain Elin Haug has crossed the same eleven kilometres of water four times a day for nineteen years. She still checks the wind before her coffee.

                Her crew of three loads bicycles, a mail van and, on Tuesdays, a crate of live crabs for the restaurant across the sound.

                > Every crossing looks the same from the shore. From the bridge, no two are alike.

                Asked what she would change, she points at the worn wooden bench by the stern rail. Nothing, she says, as long as that stays.
                """
        },
        new()
        {
            Id = "overnight-bread",
            Tag = "Recipe",
            Title = "Slow-proof bread for weeknight bakers",
            Meta = "6 min read",
            Image = CardToArticleSampleArt.Bread(),
            ImageAlt = "A round scored loaf on a wooden board with a dusting of flour under a warm light",
            Author = "Hana Okafor",
            Published = "Oct 3, 2026",
            Body = """
                Mix the dough after dinner, leave it in the fridge, and bake before work. The cold does the kneading while you sleep.

                You need 500 g of bread flour, 375 g of water, 10 g of salt and a pinch of yeast. Stir until no dry flour remains, cover the bowl and chill it for twelve to eighteen hours.

                > A slow rise gives the yeast time to build flavour that a quick loaf never reaches.

                In the morning, shape the dough on a floured board, let it rest for half an hour and bake it in a lidded pot at 240 °C.
                """
        },
        new()
        {
            Id = "desert-moss",
            Tag = "Science",
            Title = "How moss survives a year without rain",
            Meta = "3 min read",
            Image = CardToArticleSampleArt.Moss(),
            ImageAlt = "Grey stones covered in patches of bright green moss with a few drops of water",
            Author = "Arjun Mehra",
            Published = "Sep 26, 2026",
            Body = """
                Some mosses dry out completely in summer, turning brittle and brown. Months later, a single shower brings them back to green within hours.

                Special proteins protect the cell walls while the plant is dry, and repair work starts the moment water returns.

                > A dried moss is not dead. It is simply waiting.

                Researchers study these plants to learn how crops might cope with longer droughts.
                """
        }
    };

    /// <summary>Two more stories (a craft how-to and a sky-watching tip) for demos that need a longer list.</summary>
    public static IList<CardToArticleItem> MoreStories() => new AvaloniaList<CardToArticleItem>
    {
        new()
        {
            Id = "chisel-sharpening",
            Tag = "Craft",
            Title = "Sharpening a chisel, one stone at a time",
            Meta = "7 min read",
            Image = CardToArticleSampleArt.Workshop(),
            ImageAlt = "Stacked planks of pale and dark wood under a warm lamp in a workshop",
            Author = "Lucas Ferrand",
            Published = "Sep 12, 2026",
            Body = """
                A sharp chisel cuts end grain cleanly and needs less force, which also makes it safer to use.

                Work through three stones, from coarse to fine, and hold the bevel angle with a simple guide. Flatten the back first: an edge is only as good as the face it meets.

                > Sharpening rewards patience far more than strength.

                Finish with a few passes on a leather strop and test the edge on a scrap of pine.
                """
        },
        new()
        {
            Id = "city-meteors",
            Tag = "Sky",
            Title = "Counting meteors from a city balcony",
            Meta = "2 min read",
            Image = CardToArticleSampleArt.NightSky(),
            ImageAlt = "A dark blue night sky with stars and three meteor streaks above a city skyline",
            Author = "Odile Marchetti",
            Published = "Aug 30, 2026",
            Body = """
                Even under city lights, a good shower shows a dozen meteors an hour if you know where and when to look.

                Turn off the balcony lamp, give your eyes twenty minutes to adjust and face away from the brightest part of the sky.

                > The best equipment for a meteor shower is a reclining chair.

                Note each streak and the time you saw it; your counts can join a public tally that astronomers use.
                """
        }
    };

    /// <summary>All six stories.</summary>
    public static IList<CardToArticleItem> AllStories() => new AvaloniaList<CardToArticleItem>([.. Stories(), .. MoreStories()]);
}

/// <summary>The sample pictures: 800 × 600 vector scenes (gradients and polygons), drawn at any size without blur.</summary>
internal static class CardToArticleSampleArt
{
    private const double W = 800;
    private const double H = 600;

    private static DrawingImage Scene(params Drawing[] drawings)
    {
        var group = new DrawingGroup { ClipGeometry = new RectangleGeometry(new Rect(0, 0, W, H)) };
        foreach (var drawing in drawings)
        {
            group.Children.Add(drawing);
        }
        return new DrawingImage(group);
    }

    private static GeometryDrawing Fill(Geometry geometry, IBrush brush) => new() { Geometry = geometry, Brush = brush };

    private static GeometryDrawing Rect(double x, double y, double w, double h, IBrush brush) =>
        Fill(new RectangleGeometry(new Rect(x, y, w, h)), brush);

    private static GeometryDrawing Path(string data, IBrush brush) => Fill(Geometry.Parse(data), brush);

    private static SolidColorBrush Solid(uint argb) => new(Color.FromUInt32(argb));

    private static LinearGradientBrush Vertical(params (double Offset, uint Argb)[] stops)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative)
        };
        foreach (var (offset, argb) in stops)
        {
            brush.GradientStops.Add(new GradientStop(Color.FromUInt32(argb), offset));
        }
        return brush;
    }

    private static RadialGradientBrush Glow(uint inner, uint outer)
    {
        var brush = new RadialGradientBrush();
        brush.GradientStops.Add(new GradientStop(Color.FromUInt32(inner), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromUInt32(outer), 1));
        return brush;
    }

    /// <summary>Tall cumulus clouds building over four mountain ridges under a clear blue sky.</summary>
    public static IImage Clouds() => Scene(
        Rect(0, 0, W, H, Vertical((0, 0xFF5B8FD0), (0.6, 0xFF9CC2E8), (1, 0xFFD8E6F2))),
        Fill(new EllipseGeometry(new Rect(250, 60, 220, 200)), Solid(0xFFF7F9FC)),
        Fill(new EllipseGeometry(new Rect(180, 140, 200, 160)), Solid(0xFFEDF2F8)),
        Fill(new EllipseGeometry(new Rect(370, 120, 220, 180)), Solid(0xFFE6EDF5)),
        Fill(new EllipseGeometry(new Rect(560, 190, 160, 110)), Solid(0xFFF2F6FA)),
        Path("M0 330 L130 280 L250 320 L380 250 L520 310 L660 260 L800 300 L800 600 L0 600 Z", Solid(0xFF7E95A8)),
        Path("M0 390 L150 330 L280 380 L420 320 L560 370 L700 330 L800 360 L800 600 L0 600 Z", Solid(0xFF56707F)),
        Path("M0 450 L170 390 L320 440 L470 380 L620 440 L800 400 L800 600 L0 600 Z", Solid(0xFF34505A)),
        Path("M0 510 L190 460 L350 510 L530 450 L700 500 L800 470 L800 600 L0 600 Z", Solid(0xFF1F343A)));

    /// <summary>A small white ferry on dark water in front of narrow painted houses in a grey-blue mist.</summary>
    public static IImage Ferry()
    {
        var drawings = new List<Drawing>
        {
            Rect(0, 0, W, H, Vertical((0, 0xFF9AA8B8), (0.6, 0xFFC4CDD6), (1, 0xFFAEB9C4))),
            Path("M0 200 L160 150 L330 190 L520 130 L800 180 L800 300 L0 300 Z", Solid(0xFF7D8C9C))
        };
        uint[] walls = [0xFF2F6E73, 0xFFE0D7C6, 0xFFB86B3A, 0xFF5A7FA8, 0xFFE0D7C6, 0xFF8E4B5C, 0xFF2F6E73, 0xFFD9B44A];
        var x = 10.0;
        for (var i = 0; i < walls.Length; i++)
        {
            var width = 88 + (i % 3) * 8;
            var top = 220 + (i % 2) * 14;
            drawings.Add(Rect(x, top - 10, width, 10, Solid(0xFF3D3A3A)));
            drawings.Add(Rect(x, top, width, 330 - top, Solid(walls[i])));
            for (var col = 0; col < 3; col++)
            {
                drawings.Add(Rect(x + 12 + col * (width - 24) / 3, top + 24, 14, 22, Solid(0xFF2F3A44)));
            }
            x += width + 6;
        }
        drawings.Add(Rect(0, 330, W, 270, Vertical((0, 0xFF27394D), (1, 0xFF152232))));
        for (var i = 0; i < 9; i++)
        {
            drawings.Add(Rect(40 + i * 83 % 640, 360 + i * 21, 90 + i * 7 % 60, 3, Solid(0x40FFFFFF)));
        }
        drawings.Add(Path("M250 330 L560 330 L530 380 L280 380 Z", Solid(0xFFF2F2EE)));
        drawings.Add(Rect(330, 290, 150, 40, Solid(0xFFE4E6E8)));
        drawings.Add(Rect(360, 262, 70, 28, Solid(0xFFD8DBDE)));
        drawings.Add(Rect(440, 250, 14, 40, Solid(0xFFB5402F)));
        drawings.Add(Rect(345, 300, 120, 10, Solid(0xFF2F3A44)));
        drawings.Add(Rect(280, 372, 250, 8, Solid(0xFFB5402F)));
        return Scene([.. drawings]);
    }

    /// <summary>A round scored loaf on a wooden board under a warm glow, with flour dust.</summary>
    public static IImage Bread()
    {
        var drawings = new List<Drawing>
        {
            Rect(0, 0, W, H, Vertical((0, 0xFF4A2F22), (1, 0xFF2A1A12))),
            Fill(new EllipseGeometry(new Rect(180, 0, 440, 320)), Glow(0x80FFC27A, 0x00FFC27A)),
            Path("M60 420 L740 420 L780 600 L20 600 Z", Solid(0xFF9C6B43)),
            Rect(20, 470, 760, 4, Solid(0x30000000)),
            Rect(20, 530, 760, 4, Solid(0x30000000)),
            Fill(new EllipseGeometry(new Rect(230, 230, 340, 230)), Glow(0xFFE2A866, 0xFFB06E35)),
            Path("M300 300 Q400 270 500 300 L498 310 Q400 282 302 310 Z", Solid(0xFFF3D9A8)),
            Path("M285 345 Q400 315 515 345 L513 355 Q400 327 287 355 Z", Solid(0xFFF3D9A8)),
            Path("M300 390 Q400 362 500 390 L498 400 Q400 372 302 400 Z", Solid(0xFFF3D9A8))
        };
        for (var i = 0; i < 14; i++)
        {
            drawings.Add(Fill(new EllipseGeometry(new Rect(120 + i * 47 % 560, 440 + i * 29 % 120, 6, 6)), Solid(0xB3FFF8EC)));
        }
        return Scene([.. drawings]);
    }

    /// <summary>Grey stones with patches of bright moss and a few water drops on a pale green ground.</summary>
    public static IImage Moss()
    {
        var drawings = new List<Drawing>
        {
            Rect(0, 0, W, H, Vertical((0, 0xFFC9D4C2), (1, 0xFF8FA083)))
        };
        (double X, double Y, double Rx, double Ry)[] stones = [(60, 250, 300, 200), (330, 200, 340, 240), (540, 300, 250, 180), (180, 400, 360, 180)];
        foreach (var (sx, sy, rx, ry) in stones)
        {
            drawings.Add(Fill(new EllipseGeometry(new Rect(sx, sy, rx, ry)), Glow(0xFF9A9D98, 0xFF6C706B)));
            drawings.Add(Fill(new EllipseGeometry(new Rect(sx + rx * 0.15, sy + ry * 0.05, rx * 0.55, ry * 0.35)), Solid(0xFF5E9B3C)));
            drawings.Add(Fill(new EllipseGeometry(new Rect(sx + rx * 0.45, sy + ry * 0.2, rx * 0.4, ry * 0.3)), Solid(0xFF79B24A)));
        }
        for (var i = 0; i < 6; i++)
        {
            drawings.Add(Fill(new EllipseGeometry(new Rect(150 + i * 97, 230 + i * 37 % 120, 12, 16)), Solid(0xCCE8F4FF)));
        }
        return Scene([.. drawings]);
    }

    /// <summary>A starry night sky with three meteor streaks above a dark city skyline.</summary>
    public static IImage NightSky()
    {
        var drawings = new List<Drawing>
        {
            Rect(0, 0, W, H, Vertical((0, 0xFF0B1330), (0.7, 0xFF1D2A5A), (1, 0xFF3A3F6E)))
        };
        for (var i = 0; i < 60; i++)
        {
            var size = 2 + i % 3;
            drawings.Add(Fill(new EllipseGeometry(new Rect(i * 131 % 790, i * 71 % 380, size, size)), Solid(0xCCFFFFFF)));
        }
        drawings.Add(Path("M520 60 L640 160 L636 164 L516 64 Z", Solid(0xE6FFFFFF)));
        drawings.Add(Path("M260 110 L350 190 L347 193 L257 113 Z", Solid(0xB3FFFFFF)));
        drawings.Add(Path("M640 230 L700 280 L698 283 L638 233 Z", Solid(0x99FFFFFF)));
        double[] heights = [140, 200, 120, 240, 170, 110, 220, 150, 190, 130];
        var x = 0.0;
        for (var i = 0; i < heights.Length && x < W; i++)
        {
            var width = Math.Min(70 + i * 13 % 30, W - x);
            drawings.Add(Rect(x, 600 - heights[i], width, heights[i], Solid(0xFF0A0E1C)));
            for (var w = 0; w < 4; w++)
            {
                drawings.Add(Rect(x + 6 + w * 14 % Math.Max(1, width - 20), 600 - heights[i] + 20 + w * 31 % (heights[i] - 40), 8, 10, Solid(0xFFF2C86B)));
            }
            x += width;
        }
        return Scene([.. drawings]);
    }

    /// <summary>Stacked pale and dark planks under a warm lamp glow.</summary>
    public static IImage Workshop()
    {
        var drawings = new List<Drawing>
        {
            Rect(0, 0, W, H, Vertical((0, 0xFF3A2E26), (1, 0xFF1E1813))),
            Fill(new EllipseGeometry(new Rect(240, 0, 320, 260)), Glow(0x99FFD08A, 0x00FFD08A)),
            Path("M370 0 L430 0 L450 60 L350 60 Z", Solid(0xFF2A221C))
        };
        uint[] woods = [0xFFD9B98C, 0xFF6B4A33, 0xFFC9A675, 0xFF8A603F, 0xFFE3C79E, 0xFF5A3C28];
        for (var i = 0; i < woods.Length; i++)
        {
            var y = 300 + i * 40;
            var inset = (i % 3) * 30;
            drawings.Add(Rect(80 + inset, y, 640 - inset * 2, 34, Solid(woods[i])));
            drawings.Add(Rect(80 + inset, y + 30, 640 - inset * 2, 4, Solid(0x40000000)));
        }
        return Scene([.. drawings]);
    }
}
