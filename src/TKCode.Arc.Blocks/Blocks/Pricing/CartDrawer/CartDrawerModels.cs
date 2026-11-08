using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Where a <see cref="CartDrawer"/> is in the checkout.</summary>
public enum CartDrawerState
{
    /// <summary>Browsing and editing the cart.</summary>
    Shopping,

    /// <summary>Checkout was pressed and the order is being placed: the drawer cannot be closed or edited.</summary>
    Placing,

    /// <summary>The order went through and the drawer shows its receipt.</summary>
    Placed
}

/// <summary>
/// A product on the <see cref="CartDrawer"/>'s grid. Changes to the name, detail, price or image reach the grid and the
/// cart rows at once.
/// </summary>
public class CartDrawerProduct : AvaloniaObject
{
    /// <summary>Stable identifier (used in orders and events); defaults to the name when empty.</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<CartDrawerProduct, string?>(nameof(Id));

    /// <summary>The product name ("Ash reading chair"), on one line.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<CartDrawerProduct, string>(nameof(Name), string.Empty);

    /// <summary>The secondary line under the name: material, size or set ("Steam-bent ash, wool").</summary>
    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<CartDrawerProduct, string?>(nameof(Detail));

    /// <summary>Unit price in the block's currency.</summary>
    public static readonly StyledProperty<decimal> PriceProperty =
        AvaloniaProperty.Register<CartDrawerProduct, decimal>(nameof(Price));

    /// <summary>The product picture, shown at 4:5 on the grid, as a square thumbnail in the cart and in the flight.</summary>
    public static readonly StyledProperty<IImage?> ImageProperty =
        AvaloniaProperty.Register<CartDrawerProduct, IImage?>(nameof(Image));

    /// <summary>Most of this product one cart row may hold (default 9, at least 1).</summary>
    public static readonly StyledProperty<int> MaxQuantityProperty =
        AvaloniaProperty.Register<CartDrawerProduct, int>(nameof(MaxQuantity), CartDrawerMath.DefaultMaxQuantity,
            coerce: (_, v) => Math.Max(1, v));

    /// <summary>How many are already in the cart when the block first shows its products (default 0).</summary>
    public static readonly StyledProperty<int> InitialQuantityProperty =
        AvaloniaProperty.Register<CartDrawerProduct, int>(nameof(InitialQuantity));

    /// <inheritdoc cref="IdProperty"/>
    public string? Id
    {
        get => GetValue(IdProperty);
        set => SetValue(IdProperty, value);
    }

    /// <inheritdoc cref="NameProperty"/>
    public string Name
    {
        get => GetValue(NameProperty);
        set => SetValue(NameProperty, value);
    }

    /// <inheritdoc cref="DetailProperty"/>
    public string? Detail
    {
        get => GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    /// <inheritdoc cref="PriceProperty"/>
    public decimal Price
    {
        get => GetValue(PriceProperty);
        set => SetValue(PriceProperty, value);
    }

    /// <inheritdoc cref="ImageProperty"/>
    public IImage? Image
    {
        get => GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }

    /// <inheritdoc cref="MaxQuantityProperty"/>
    public int MaxQuantity
    {
        get => GetValue(MaxQuantityProperty);
        set => SetValue(MaxQuantityProperty, value);
    }

    /// <inheritdoc cref="InitialQuantityProperty"/>
    public int InitialQuantity
    {
        get => GetValue(InitialQuantityProperty);
        set => SetValue(InitialQuantityProperty, value);
    }

    /// <summary>The identifier orders and events use: <see cref="Id"/>, or the name when no id is set.</summary>
    public string Key => string.IsNullOrEmpty(Id) ? Name : Id;
}

/// <summary>One row of the cart: a product, how many, and what they cost together.</summary>
/// <param name="Product">The product.</param>
/// <param name="Quantity">How many (1 to the product's maximum).</param>
public sealed record CartDrawerLine(CartDrawerProduct Product, int Quantity)
{
    /// <summary>The product's price times the quantity.</summary>
    public decimal LineTotal => Product.Price * Quantity;
}

/// <summary>An accepted promo code.</summary>
/// <param name="Code">The code as it should be shown (upper case in the sample: "WORKSHOP10").</param>
/// <param name="PercentOff">The share taken off the subtotal, 0–1 (0.10 for 10%).</param>
public sealed record CartDrawerPromo(string Code, decimal PercentOff)
{
    /// <summary>Text after the code on the applied chip; null uses the labels' "10% off" pattern.</summary>
    public string? Label { get; init; }
}

/// <summary>What a <see cref="CartDrawer"/> sends to its place-order handler.</summary>
/// <param name="Lines">The cart rows, in order.</param>
/// <param name="Promo">The applied promo code, if any.</param>
/// <param name="Subtotal">Sum of the line totals.</param>
/// <param name="Discount">What the promo code takes off.</param>
/// <param name="Shipping">The shipping charge (0 once the free-shipping threshold is reached).</param>
/// <param name="Total">Subtotal − discount + shipping.</param>
/// <param name="Currency">ISO 4217 code of the amounts.</param>
public sealed record CartDrawerOrder(
    IReadOnlyList<CartDrawerLine> Lines,
    CartDrawerPromo? Promo,
    decimal Subtotal,
    decimal Discount,
    decimal Shipping,
    decimal Total,
    string Currency)
{
    /// <summary>How many items the order holds (the sum of the quantities).</summary>
    public int ItemCount => Lines.Sum(l => l.Quantity);
}

/// <summary>What the drawer shows once an order is placed.</summary>
/// <param name="Title">The thank-you line ("All set, Mira").</param>
/// <param name="Message">One or two sentences under it (order number, where the receipt went).</param>
/// <param name="ItemCount">The "Items" row.</param>
/// <param name="Paid">The "Paid" row.</param>
/// <param name="Arrives">The "Arrives" row, already formatted ("Nov 11 – Nov 13").</param>
public sealed record CartDrawerReceipt(string Title, string Message, int ItemCount, decimal Paid, string Arrives);
