using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>How a <see cref="ProductListing"/> orders its products.</summary>
public enum ListingSort
{
    /// <summary>The order the products were given in.</summary>
    Featured,

    /// <summary>Cheapest first.</summary>
    PriceLowToHigh,

    /// <summary>Most expensive first.</summary>
    PriceHighToLow,

    /// <summary>Best rated first (more reviews break ties).</summary>
    TopRated,

    /// <summary>Most recently added first.</summary>
    Newest
}

/// <summary>A product on a <see cref="ProductListing"/>.</summary>
public sealed class ListingProduct
{
    /// <summary>Stable identifier for events; defaults to the name when empty.</summary>
    public string? Id { get; set; }

    /// <summary>The product name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The category the filter matches ("Bags").</summary>
    public string? Category { get; set; }

    /// <summary>The price.</summary>
    public decimal Price { get; set; }

    /// <summary>The price before a reduction, shown struck through; null when not reduced.</summary>
    public decimal? CompareAtPrice { get; set; }

    /// <summary>Average rating out of 5; 0 hides the rating.</summary>
    public double Rating { get; set; }

    /// <summary>How many reviews the rating is based on.</summary>
    public int ReviewCount { get; set; }

    /// <summary>The colors the product comes in, shown as dots under the price.</summary>
    public IReadOnlyList<Color> Colors { get; set; } = [];

    /// <summary>The picture, shown at 4:5.</summary>
    public IImage? Image { get; set; }

    /// <summary>Whether it can be bought now; sold-out products keep their card with a disabled button.</summary>
    public bool IsInStock { get; set; } = true;

    /// <summary>A short tag over the picture ("New", "Bestseller"); a reduction shows "−20%" when this is empty.</summary>
    public string? Badge { get; set; }

    /// <summary>When the product was added, for the Newest sort.</summary>
    public DateTime AddedOn { get; set; }

    /// <summary>The identifier events use: <see cref="Id"/>, or the name.</summary>
    public string Key => string.IsNullOrEmpty(Id) ? Name : Id;
}

/// <summary>A product of a <see cref="ProductListing"/> was added to the cart or opened.</summary>
public sealed class ListingProductEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ListingProductEventArgs(RoutedEvent routedEvent, ListingProduct product) : base(routedEvent) => Product = product;

    /// <summary>The product.</summary>
    public ListingProduct Product { get; }
}

/// <summary>Filtering and sorting of <see cref="ProductListing"/>, without any UI.</summary>
internal static class ProductListingMath
{
    /// <summary>
    /// The products that pass the filters, in <paramref name="sort"/> order: in one of <paramref name="categories"/> (none
    /// selected passes every category), priced within [<paramref name="min"/>, <paramref name="max"/>] and, if asked, in stock.
    /// </summary>
    public static List<ListingProduct> Apply(IReadOnlyList<ListingProduct> products, IReadOnlyCollection<string> categories,
        decimal min, decimal max, bool inStockOnly, ListingSort sort)
    {
        var index = new Dictionary<ListingProduct, int>();
        for (var i = 0; i < products.Count; i++)
        {
            index[products[i]] = i;
        }
        var list = products.Where(p => (categories.Count == 0 || p.Category is not null && categories.Contains(p.Category))
                                       && p.Price >= min && p.Price <= max && (!inStockOnly || p.IsInStock)).ToList();
        int Featured(ListingProduct a, ListingProduct b) => index[a].CompareTo(index[b]);
        list.Sort(sort switch
        {
            ListingSort.PriceLowToHigh => (a, b) => a.Price != b.Price ? a.Price.CompareTo(b.Price) : Featured(a, b),
            ListingSort.PriceHighToLow => (a, b) => a.Price != b.Price ? b.Price.CompareTo(a.Price) : Featured(a, b),
            ListingSort.TopRated => (a, b) => a.Rating != b.Rating ? b.Rating.CompareTo(a.Rating)
                : a.ReviewCount != b.ReviewCount ? b.ReviewCount.CompareTo(a.ReviewCount) : Featured(a, b),
            ListingSort.Newest => (a, b) => a.AddedOn != b.AddedOn ? b.AddedOn.CompareTo(a.AddedOn) : Featured(a, b),
            _ => Featured,
        });
        return list;
    }

    /// <summary>How many columns of at least <paramref name="minWidth"/> fit in <paramref name="width"/> with <paramref name="gap"/> between them.</summary>
    public static int Columns(double width, double minWidth, double gap, int max)
        => Math.Clamp((int)Math.Floor((width + gap) / (minWidth + gap)), 1, Math.Max(1, max));
}
