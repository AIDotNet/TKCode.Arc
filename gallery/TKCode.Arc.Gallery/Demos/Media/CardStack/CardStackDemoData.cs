using Avalonia.Controls;
using Avalonia.Data.Converters;
using TKCode.Arc.Gallery.Pages;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A place to visit; the cards of the CardStack demos show one each.</summary>
/// <param name="Name">City name.</param>
/// <param name="Country">Country name.</param>
/// <param name="Note">One line about the place.</param>
/// <param name="Scene">Palette of the drawn picture (see <see cref="MediaArt"/>).</param>
/// <param name="Photo">Remote photo shown over the drawn picture.</param>
public sealed record CardStackDestination(string Name, string Country, string Note, int Scene, ImageSource Photo)
{
    /// <summary>The line under the name.</summary>
    public string Details => $"{Country} · {Note}";
}

/// <summary>The destinations and the picture helper of the CardStack demos.</summary>
public static class CardStackDemoData
{
    public static IReadOnlyList<CardStackDestination> Destinations { get; } =
    [
        new("Lisbon", "Portugal", "Tiled streets, trams and late dinners.", 2, ImageSource.Parse("https://picsum.photos/seed/arc-lisbon/640/400")),
        new("Kyoto", "Japan", "Temples, gardens and quiet mornings.", 5, ImageSource.Parse("https://picsum.photos/seed/arc-kyoto/640/400")),
        new("Oaxaca", "Mexico", "Markets, mezcal and mountain light.", 0, ImageSource.Parse("https://picsum.photos/seed/arc-oaxaca/640/400")),
        new("Tbilisi", "Georgia", "Sulphur baths and balconies.", 4, ImageSource.Parse("https://picsum.photos/seed/arc-tbilisi/640/400")),
        new("Hobart", "Australia", "Harbour walks and wild coast.", 3, ImageSource.Parse("https://picsum.photos/seed/arc-hobart/640/400")),
        new("Bergen", "Norway", "Fjords on the doorstep.", 1, ImageSource.Parse("https://picsum.photos/seed/arc-bergen/640/400"))
    ];

    /// <summary>
    /// Builds the drawn landscape for a scene number. The gallery's MediaArt control is a stand-in for photography that
    /// works offline, and its Scene is not bindable, so the templates reach it through this converter.
    /// </summary>
    public static IValueConverter Art { get; } = new FuncValueConverter<int, Control?>(scene => new MediaArt { Scene = scene });
}
