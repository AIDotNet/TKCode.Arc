using Avalonia.Controls;
using Avalonia.Data.Converters;
using TKCode.Arc.Gallery.Pages;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A hotel suite; the slides of the Carousel demos show one each.</summary>
/// <param name="Name">Suite name.</param>
/// <param name="Detail">View, size and price on one line.</param>
/// <param name="Scene">Palette of the drawn picture (see <see cref="MediaArt"/>).</param>
/// <param name="Photo">Remote photo shown over the drawn picture.</param>
public sealed record CarouselSuite(string Name, string Detail, int Scene, ImageSource Photo);

/// <summary>The suites and the picture helper of the Carousel demos.</summary>
public static class CarouselDemoData
{
    public static IReadOnlyList<CarouselSuite> Suites { get; } =
    [
        new("Harbour suite", "Sea view · 42 m² · from €310", 0, ImageSource.Parse("https://picsum.photos/seed/arc-harbour/680/440")),
        new("Garden loft", "Courtyard · 36 m² · from €240", 5, ImageSource.Parse("https://picsum.photos/seed/arc-garden/680/440")),
        new("Lighthouse room", "Cliff edge · 28 m² · from €190", 3, ImageSource.Parse("https://picsum.photos/seed/arc-lighthouse/680/440")),
        new("Dune studio", "Beach access · 31 m² · from €220", 2, ImageSource.Parse("https://picsum.photos/seed/arc-dune/680/440")),
        new("Night terrace", "Rooftop · 54 m² · from €420", 4, ImageSource.Parse("https://picsum.photos/seed/arc-terrace/680/440"))
    ];

    /// <summary>
    /// Builds the drawn landscape for a scene number. The gallery's MediaArt control is a stand-in for photography that
    /// works offline, and its Scene is not bindable, so the templates reach it through this converter.
    /// </summary>
    public static IValueConverter Art { get; } = new FuncValueConverter<int, Control?>(scene => new MediaArt { Scene = scene });
}
