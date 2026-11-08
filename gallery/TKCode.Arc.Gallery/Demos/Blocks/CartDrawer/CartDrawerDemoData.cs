using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Invented data for the cart drawer demos: a small tea shop with drawn tins.</summary>
internal static class CartDrawerDemoData
{
    /// <summary>Four teas in pounds with pence (a tin drawn in each tea's color on a warm wall).</summary>
    public static IReadOnlyList<CartDrawerProduct> TeaProducts() =>
    [
        Tea("Morning oolong", "Roasted, 100 g tin", 14.50m, "#9C6B3E"),
        Tea("Garden sencha", "First flush, 80 g", 12.00m, "#6F8A4E"),
        Tea("Smoked black", "Lapsang, 125 g", 9.75m, "#3F3934"),
        Tea("Iron kettle", "Cast iron, 0.8 L", 38.00m, "#55606B")
    ];

    private static CartDrawerProduct Tea(string name, string detail, decimal price, string color) => new()
    {
        Name = name,
        Detail = detail,
        Price = price,
        Image = Tin(Color.Parse(color))
    };

    /// <summary>A tin with a lid and a paper band, drawn at 400 × 500.</summary>
    private static DrawingImage Tin(Color color)
    {
        var group = new DrawingGroup();
        void Fill(Geometry geometry, IBrush brush) => group.Children.Add(new GeometryDrawing { Geometry = geometry, Brush = brush });
        Fill(new RectangleGeometry(new Rect(0, 0, 400, 500)), new ImmutableLinearGradientBrush(
            [new ImmutableGradientStop(0, Color.Parse("#EEE8DE")), new ImmutableGradientStop(1, Color.Parse("#DCD3C5"))],
            startPoint: new RelativePoint(0.5, 0, RelativeUnit.Relative), endPoint: new RelativePoint(0.5, 1, RelativeUnit.Relative)));
        Fill(new RectangleGeometry(new Rect(0, 380, 400, 120)), new ImmutableSolidColorBrush(Color.Parse("#C9BCA8")));
        Fill(new EllipseGeometry(new Rect(110, 386, 180, 20)), new ImmutableSolidColorBrush(Color.FromArgb(40, 0, 0, 0)));
        Fill(new RectangleGeometry(new Rect(130, 170, 140, 226), 14, 14), new ImmutableSolidColorBrush(color));
        Fill(new RectangleGeometry(new Rect(122, 150, 156, 40), 10, 10), new ImmutableSolidColorBrush(Color.FromArgb(255,
            (byte)(color.R * 0.8), (byte)(color.G * 0.8), (byte)(color.B * 0.8))));
        Fill(new RectangleGeometry(new Rect(130, 240, 140, 86)), new ImmutableSolidColorBrush(Color.Parse("#F4EFE6")));
        Fill(new RectangleGeometry(new Rect(160, 270, 80, 6), 3, 3), new ImmutableSolidColorBrush(color));
        Fill(new RectangleGeometry(new Rect(172, 288, 56, 4), 2, 2), new ImmutableSolidColorBrush(Color.FromArgb(120, color.R, color.G, color.B)));
        return new DrawingImage(group);
    }
}
