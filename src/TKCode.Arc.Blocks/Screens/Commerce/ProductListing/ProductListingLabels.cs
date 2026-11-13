using System.Globalization;

namespace TKCode.Arc;

/// <summary>The built-in text of <see cref="ProductListing"/>. Replace the whole object to localize the block.</summary>
public sealed class ProductListingLabels
{
    /// <summary>Culture for prices (en-US by default).</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The result count: count ⇒ "12 products".</summary>
    public Func<int, string> ResultCount { get; init; } = count => count == 1 ? "1 product" : $"{count} products";

    /// <summary>Label of the sort select: "Sort by".</summary>
    public string SortBy { get; init; } = "Sort by";

    /// <summary>The name of each sort.</summary>
    public Func<ListingSort, string> Sort { get; init; } = sort => sort switch
    {
        ListingSort.PriceLowToHigh => "Price: low to high",
        ListingSort.PriceHighToLow => "Price: high to low",
        ListingSort.TopRated => "Top rated",
        ListingSort.Newest => "Newest",
        _ => "Featured",
    };

    /// <summary>The filters toggle of a narrow block: "Filters".</summary>
    public string Filters { get; init; } = "Filters";

    /// <summary>Heading of the categories: "Category".</summary>
    public string Category { get; init; } = "Category";

    /// <summary>Heading of the price range: "Price".</summary>
    public string Price { get; init; } = "Price";

    /// <summary>The price range readout: (min, max) ⇒ "$12 – $180".</summary>
    public Func<string, string, string> PriceRange { get; init; } = (min, max) => $"{min} – {max}";

    /// <summary>The stock switch: "In stock only".</summary>
    public string InStockOnly { get; init; } = "In stock only";

    /// <summary>The reset button: "Clear filters".</summary>
    public string ClearFilters { get; init; } = "Clear filters";

    /// <summary>Shown when nothing matches: "No products match these filters.".</summary>
    public string NoResults { get; init; } = "No products match these filters.";

    /// <summary>The quick-add button: "Add to cart".</summary>
    public string AddToCart { get; init; } = "Add to cart";

    /// <summary>The quick-add button just after a press: "Added".</summary>
    public string Added { get; init; } = "Added";

    /// <summary>The quick-add button of a sold-out product: "Sold out".</summary>
    public string SoldOut { get; init; } = "Sold out";

    /// <summary>Screen reader name of a quick-add button: name ⇒ "Add Market tote to cart".</summary>
    public Func<string, string> AddName { get; init; } = name => $"Add {name} to cart";

    /// <summary>The cart button: count ⇒ "Cart, 2 items".</summary>
    public Func<int, string> CartName { get; init; } = count => count == 1 ? "Cart, 1 item" : $"Cart, {count} items";

    /// <summary>A reduction badge: percent ⇒ "−20%".</summary>
    public Func<int, string> Reduction { get; init; } = percent => $"−{percent}%";

    /// <summary>A product's screen reader name: (name, price, rating) ⇒ "Market tote, $48, rated 4.8 of 5".</summary>
    public Func<string, string, string?, string> CardName { get; init; } = (name, price, rating)
        => rating is null ? $"{name}, {price}" : $"{name}, {price}, rated {rating} of 5";

    /// <summary>Announced after a quick add: name ⇒ "Market tote added to cart".</summary>
    public Func<string, string> AnnounceAdded { get; init; } = name => $"{name} added to cart";

    /// <summary>Announced after the filters change: the result count sentence.</summary>
    public Func<int, string> AnnounceResults { get; init; } = count => count == 1 ? "1 product shown" : $"{count} products shown";
}
