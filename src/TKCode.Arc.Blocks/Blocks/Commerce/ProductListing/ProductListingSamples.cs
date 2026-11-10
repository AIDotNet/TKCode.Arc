using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Invented sample catalog for the <see cref="ProductListing"/> preview: twelve everyday goods of a made-up shop in four
/// categories, with drawn pictures (<see cref="ProductListingArtwork"/>). Replace it with your catalog; none of it is required.
/// </summary>
public static class ProductListingSamples
{
    /// <summary>The sample shop's heading.</summary>
    public const string Title = "Everyday goods";

    /// <summary>Twelve new sample products (a fresh set on every call).</summary>
    public static IReadOnlyList<ListingProduct> CreateProducts()
    {
        var day = new DateTime(2026, 10, 1);
        ListingProduct Make(string id, string name, string category, decimal price, ProductListingArtworkKind kind, string[] colors,
            double rating, int reviews, int age, decimal? compareAt = null, string? badge = null, bool inStock = true)
        {
            var palette = colors.Select(Color.Parse).ToArray();
            return new ListingProduct
            {
                Id = id,
                Name = name,
                Category = category,
                Price = price,
                CompareAtPrice = compareAt,
                Rating = rating,
                ReviewCount = reviews,
                Colors = palette,
                Image = ProductListingArtwork.Create(kind, palette[0]),
                Badge = badge,
                IsInStock = inStock,
                AddedOn = day.AddDays(-age)
            };
        }

        return
        [
            Make("tote", "Market tote", "Bags", 48, ProductListingArtworkKind.Tote, ["#C9B79C", "#3F5A4A", "#2F3A56"], 4.8, 126, 40, badge: "Bestseller"),
            Make("bottle", "Trail bottle, 750 ml", "Outdoor", 36, ProductListingArtworkKind.Bottle, ["#3D7A8C", "#D9734E", "#2B2B2E"], 4.6, 88, 12),
            Make("mug", "Speckled mug", "Kitchen", 22, ProductListingArtworkKind.Mug, ["#E6DCCB", "#8E7C69"], 4.9, 204, 90),
            Make("cap", "Six-panel cap", "Outdoor", 28, ProductListingArtworkKind.Cap, ["#5B6B48", "#B5523B", "#22324A"], 4.3, 41, 5, badge: "New"),
            Make("candle", "Cedar and fig candle", "Home", 34, ProductListingArtworkKind.Candle, ["#B98B5E"], 4.7, 63, 60, compareAt: 42),
            Make("notebook", "Linen notebook, A5", "Home", 18, ProductListingArtworkKind.Notebook, ["#3E5C76", "#A23E48", "#6A7F5C"], 4.5, 150, 120),
            Make("tote-mini", "Mini tote", "Bags", 32, ProductListingArtworkKind.Tote, ["#2F3A56", "#C9B79C"], 4.4, 37, 8, badge: "New"),
            Make("flask", "Coffee flask, 350 ml", "Kitchen", 29, ProductListingArtworkKind.Bottle, ["#A9A49A", "#3D7A8C"], 4.2, 22, 30, inStock: false),
            Make("mug-pair", "Stacking mugs, pair", "Kitchen", 38, ProductListingArtworkKind.Mug, ["#5E7F8F", "#E6DCCB"], 4.6, 59, 75, compareAt: 46),
            Make("cap-wool", "Wool cap", "Outdoor", 34, ProductListingArtworkKind.Cap, ["#7A6A58", "#2B2B2E"], 4.1, 18, 3),
            Make("candle-trio", "Votive trio", "Home", 26, ProductListingArtworkKind.Candle, ["#D7A6A0", "#9DB5A1"], 4.8, 47, 20),
            Make("sketchbook", "Sketchbook, A4", "Home", 24, ProductListingArtworkKind.Notebook, ["#2D2D30", "#C2A878"], 4.7, 95, 150, inStock: false),
        ];
    }
}
