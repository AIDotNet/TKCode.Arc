using Avalonia.Media;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A teammate on the gravity well's team demo: initials on a tinted tile.</summary>
/// <param name="Name">Full name (the tile's accessible name).</param>
/// <param name="Initials">Two letters shown on the tile.</param>
/// <param name="Tint">Tile color.</param>
public sealed record GravityWellPerson(string Name, string Initials, IBrush Tint);

/// <summary>The sample photos and people of the Gravity well demos.</summary>
public static class GravityWellDemoData
{
    /// <summary>42 square photos for the moodboard (remote, so the grid shows its muted tiles while they load).</summary>
    public static IReadOnlyList<ImageSource> Photos { get; } =
        Enumerable.Range(1, 42).Select(i => ImageSource.Parse($"https://picsum.photos/seed/arc-well-{i}/160/160")).ToArray();

    private static readonly string[] Names =
    [
        "Ines Moreau", "Kofi Mensah", "Lena Fischer", "Arjun Patel", "Mira Holm", "Tomás Ruiz", "Yuki Sato", "Noor Haddad",
        "Owen Price", "Sana Iqbal", "Elias Berg", "Priya Nair", "Hugo Lambert", "Zara Okafor", "Felix Wagner", "Ana Costa",
        "Rhys Morgan", "Lea Novak", "Omar Farouk", "Ivy Chen", "Jonas Keller", "Maya Levi", "Theo Laurent", "Aiko Mori",
    ];

    private static readonly string[] Tints = ["#d9c2a5", "#b8c7a3", "#a9bcd6", "#d7a99a", "#c9b6d8", "#e2d3a4", "#a7cfc9", "#cfcfcf"];

    /// <summary>24 invented teammates with their initials and a tile color.</summary>
    public static IReadOnlyList<GravityWellPerson> People { get; } =
        Names.Select((name, i) => new GravityWellPerson(name,
            string.Concat(name.Split(' ').Select(part => part[0])),
            new SolidColorBrush(Color.Parse(Tints[i * 5 % Tints.Length])))).ToArray();
}
