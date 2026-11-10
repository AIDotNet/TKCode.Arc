// Ported from Arc's free logo-marquee block (MIT, Copyright (c) 2026 Elia Kuratli): the data model of logo-marquee.tsx.
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>How a <see cref="LogoMarquee"/> colors its marks.</summary>
public enum LogoMarqueeTone
{
    /// <summary>Each mark in its own color; the names read at full contrast.</summary>
    Color,

    /// <summary>Every mark and name in the secondary text color, for a quieter row.</summary>
    Mono
}

/// <summary>One entry of a <see cref="LogoMarquee"/>: a visible name and its mark.</summary>
public sealed class LogoMarqueeBrand
{
    /// <summary>The visible name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The mark as a filled shape in any coordinate space; it is scaled to fit. Drawn in the text color unless <see cref="Color"/> applies.</summary>
    public Geometry? Icon { get; set; }

    /// <summary>The mark's single color in <see cref="LogoMarqueeTone.Color"/>. Leave it out for a mark that follows the text.</summary>
    public Color? Color { get; set; }

    /// <summary>A full color mark, used in <see cref="LogoMarqueeTone.Color"/> instead of <see cref="Icon"/>.</summary>
    public IImage? ColorIcon { get; set; }
}

/// <summary>Invented generic marks and names for the default row; none of them is a real brand.</summary>
internal static class LogoMarqueeSamples
{
    public static IReadOnlyList<LogoMarqueeBrand> Create() =>
    [
        new() { Name = "Orbitkit", Icon = Geometry.Parse("M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20zm0 5a5 5 0 1 1 0 10 5 5 0 0 1 0-10z"), Color = Avalonia.Media.Color.Parse("#4F6BED") },
        new() { Name = "Fernway", Icon = Geometry.Parse("M12 2 22 22H2z"), Color = Avalonia.Media.Color.Parse("#2F9E6E") },
        new() { Name = "Quillbase", Icon = Geometry.Parse("M3 3h8v8H3zM13 3h8v8h-8zM3 13h8v8H3z") },
        new() { Name = "Tessera Labs", ColorIcon = CreateTiles(), Icon = Geometry.Parse("M2 2h9v9H2zM13 2h9v9h-9zM2 13h9v9H2zM13 13h9v9h-9z") },
        new() { Name = "Brightloop", Icon = Geometry.Parse("M12 2a10 10 0 0 1 10 10h-5a5 5 0 0 0-5-5zM12 22A10 10 0 0 1 2 12h5a5 5 0 0 0 5 5z"), Color = Avalonia.Media.Color.Parse("#D9822B") },
        new() { Name = "Harborline", Icon = Geometry.Parse("M2 6h20v4H2zM2 14h20v4H2z") },
        new() { Name = "Mosswork", Icon = Geometry.Parse("M12 2 22 12 12 22 2 12z"), Color = Avalonia.Media.Color.Parse("#7A5AF8") },
    ];

    /// <summary>Four colored tiles: the sample of a full color mark.</summary>
    private static DrawingImage CreateTiles()
    {
        var group = new DrawingGroup();
        // A clear full box first: a drawing image measures from its content's bounds.
        group.Children.Add(Tile("#00000000", "M0 0h24v24H0z"));
        group.Children.Add(Tile("#E5534B", "M2 2h9v9H2z"));
        group.Children.Add(Tile("#3D8BD9", "M13 2h9v9h-9z"));
        group.Children.Add(Tile("#E9B23C", "M2 13h9v9H2z"));
        group.Children.Add(Tile("#3FA66F", "M13 13h9v9h-9z"));
        return new DrawingImage { Drawing = group };

        static GeometryDrawing Tile(string color, string path) => new()
        {
            Brush = new ImmutableSolidColorBrush(Avalonia.Media.Color.Parse(color)),
            Geometry = Geometry.Parse(path)
        };
    }
}
