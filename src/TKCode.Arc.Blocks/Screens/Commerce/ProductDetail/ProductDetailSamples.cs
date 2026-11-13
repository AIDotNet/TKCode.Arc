using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Invented sample content for the <see cref="ProductDetail"/> preview: a canvas tote of a made-up maker in three colors with
/// four drawn pictures each, five sizes and four reviews. Replace it with your product; none of it is required.
/// </summary>
public static class ProductDetailSamples
{
    /// <summary>Fills <paramref name="detail"/> with the sample product.</summary>
    public static void Apply(ProductDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        detail.Brand = "Fernhill Supply";
        detail.ProductName = "Field tote";
        detail.Price = 58m;
        detail.CompareAtPrice = 72m;
        detail.Rating = 4.7;
        detail.ReviewCount = 128;
        detail.Description = "A roomy everyday bag cut from heavy waxed canvas, with an inside pocket for keys and a base that stands on its own.";
        detail.Details =
        [
            "Waxed cotton canvas, 18 oz",
            "Inside zip pocket and key loop",
            "Handles long enough to wear on the shoulder",
            "Wipe clean; re-wax once a year",
        ];
        detail.Colors.Clear();
        detail.Colors.AddRange(
        [
            Color("Sand", "#C9B79C"),
            Color("Moss", "#5B6B48"),
            Color("Ink", "#2F3A56"),
        ]);
        detail.Sizes.Clear();
        detail.Sizes.AddRange(
        [
            new ProductSizeOption("XS", 0),
            new ProductSizeOption("S", 6),
            new ProductSizeOption("M", 2),
            new ProductSizeOption("L", 9),
            new ProductSizeOption("XL", 4),
        ]);
        detail.Reviews.Clear();
        detail.Reviews.AddRange(
        [
            new ProductReview { Author = "Mara L.", Rating = 5, Title = "Carries everything", Body = "Fits a laptop, lunch and a jumper with room to spare.", Date = new DateTime(2026, 10, 28) },
            new ProductReview { Author = "Teo R.", Rating = 5, Title = "Better with age", Body = "The canvas softened after a month and looks great.", Date = new DateTime(2026, 10, 14) },
            new ProductReview { Author = "Ines D.", Rating = 4, Title = "Handles a bit long", Body = "Lovely bag; I tied a knot in the handles to carry it by hand.", Date = new DateTime(2026, 9, 30) },
            new ProductReview { Author = "Kofi M.", Rating = 3, Body = "Good bag, but the pocket is smaller than I hoped.", Date = new DateTime(2026, 9, 2) },
        ]);
    }

    private static ProductColorOption Color(string name, string hex)
    {
        var color = Avalonia.Media.Color.Parse(hex);
        return new ProductColorOption
        {
            Name = name,
            Swatch = color,
            Images = Enumerable.Range(0, 4).Select(view => ProductListingArtwork.Create(ProductListingArtworkKind.Tote, color, view)).ToList()
        };
    }
}
