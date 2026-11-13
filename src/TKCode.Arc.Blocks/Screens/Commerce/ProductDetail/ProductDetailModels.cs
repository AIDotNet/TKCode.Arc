using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>A color a <see cref="ProductDetail"/> product comes in, with its own pictures.</summary>
public sealed class ProductColorOption
{
    /// <summary>The color's name ("Sand").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The swatch drawn for it.</summary>
    public Color Swatch { get; set; }

    /// <summary>The pictures of the product in this color, the first one the main picture.</summary>
    public IReadOnlyList<IImage> Images { get; set; } = [];
}

/// <summary>A size a <see cref="ProductDetail"/> product comes in.</summary>
public sealed class ProductSizeOption
{
    /// <summary>Creates an empty size (set the properties).</summary>
    public ProductSizeOption()
    {
    }

    /// <summary>Creates a size with its stock.</summary>
    public ProductSizeOption(string label, int stock)
    {
        Label = label;
        Stock = stock;
    }

    /// <summary>The size as shown ("M", "42").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>How many are left; 0 is sold out.</summary>
    public int Stock { get; set; } = 10;
}

/// <summary>A customer review of a <see cref="ProductDetail"/> product.</summary>
public sealed class ProductReview
{
    /// <summary>Who wrote it ("Mara L.").</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>Stars given, 1 to 5.</summary>
    public int Rating { get; set; } = 5;

    /// <summary>The headline.</summary>
    public string? Title { get; set; }

    /// <summary>The review text.</summary>
    public string? Body { get; set; }

    /// <summary>When it was written.</summary>
    public DateTime Date { get; set; }
}

/// <summary>A <see cref="ProductDetail"/> added its product to the cart.</summary>
public sealed class ProductDetailAddedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ProductDetailAddedEventArgs(RoutedEvent routedEvent, ProductColorOption? color, ProductSizeOption? size, int quantity)
        : base(routedEvent)
    {
        Color = color;
        Size = size;
        Quantity = quantity;
    }

    /// <summary>The chosen color, if the product has colors.</summary>
    public ProductColorOption? Color { get; }

    /// <summary>The chosen size, if the product has sizes.</summary>
    public ProductSizeOption? Size { get; }

    /// <summary>How many.</summary>
    public int Quantity { get; }
}
