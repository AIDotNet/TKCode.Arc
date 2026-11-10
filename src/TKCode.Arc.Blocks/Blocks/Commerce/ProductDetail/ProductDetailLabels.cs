using System.Globalization;

namespace TKCode.Arc;

/// <summary>The built-in text of <see cref="ProductDetail"/>. Replace the whole object to localize the block.</summary>
public sealed class ProductDetailLabels
{
    /// <summary>Culture for prices and dates (en-US by default).</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The color line: name ⇒ "Color: Sand".</summary>
    public Func<string, string> ColorLine { get; init; } = name => $"Color: {name}";

    /// <summary>The size heading: "Size".</summary>
    public string Size { get; init; } = "Size";

    /// <summary>The quantity field: "Quantity".</summary>
    public string Quantity { get; init; } = "Quantity";

    /// <summary>The low-stock note: count ⇒ "Only 2 left".</summary>
    public Func<int, string> LowStock { get; init; } = count => $"Only {count} left";

    /// <summary>The add button: "Add to cart".</summary>
    public string AddToCart { get; init; } = "Add to cart";

    /// <summary>The add button just after adding: "Added to cart".</summary>
    public string Added { get; init; } = "Added to cart";

    /// <summary>The add button before a size is chosen: "Choose a size".</summary>
    public string ChooseSize { get; init; } = "Choose a size";

    /// <summary>The message when Add is pressed without a size: "Choose a size first.".</summary>
    public string NeedSize { get; init; } = "Choose a size first.";

    /// <summary>The save button: (saved) ⇒ "Save" / "Saved".</summary>
    public Func<bool, string> Save { get; init; } = saved => saved ? "Saved" : "Save";

    /// <summary>The details tab: "Details".</summary>
    public string DetailsTab { get; init; } = "Details";

    /// <summary>The reviews tab: count ⇒ "Reviews (128)".</summary>
    public Func<int, string> ReviewsTab { get; init; } = count => $"Reviews ({count})";

    /// <summary>The rating line: (rating, count) ⇒ "4.8 · 128 reviews".</summary>
    public Func<string, int, string> RatingLine { get; init; } = (rating, count) => $"{rating} · {count} {(count == 1 ? "review" : "reviews")}";

    /// <summary>Screen reader name of the rating: rating ⇒ "Rated 4.8 out of 5".</summary>
    public Func<string, string> RatingName { get; init; } = rating => $"Rated {rating} out of 5";

    /// <summary>Screen reader name of a thumbnail: (number, count) ⇒ "Picture 2 of 4".</summary>
    public Func<int, int, string> PictureName { get; init; } = (number, count) => $"Picture {number} of {count}";

    /// <summary>Screen reader name of a sold-out size: size ⇒ "M, sold out".</summary>
    public Func<string, string> SoldOutSize { get; init; } = size => $"{size}, sold out";

    /// <summary>Shown when there is no review: "No reviews yet.".</summary>
    public string NoReviews { get; init; } = "No reviews yet.";

    /// <summary>A review's date.</summary>
    public Func<DateTime, string> Date { get; init; } = date => date.ToString("MMM d, yyyy", CultureInfo.GetCultureInfo("en-US"));

    /// <summary>Announced after adding: (quantity, name) ⇒ "2 × Field tote added to cart".</summary>
    public Func<int, string, string> AnnounceAdded { get; init; } = (quantity, name) => $"{quantity} × {name} added to cart";
}
