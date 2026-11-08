using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>A product was added to, changed in or removed from the <see cref="CartDrawer"/>'s cart.</summary>
public sealed class CartDrawerItemEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CartDrawerItemEventArgs(RoutedEvent routedEvent, CartDrawerProduct product, int oldQuantity, int newQuantity)
        : base(routedEvent)
    {
        Product = product;
        OldQuantity = oldQuantity;
        NewQuantity = newQuantity;
    }

    /// <summary>The product.</summary>
    public CartDrawerProduct Product { get; }

    /// <summary>How many were in the cart before (0 when the product was just added).</summary>
    public int OldQuantity { get; }

    /// <summary>How many are in the cart now (0 when the row was removed).</summary>
    public int NewQuantity { get; }
}

/// <summary>A promo code was applied, refused or removed in a <see cref="CartDrawer"/>.</summary>
public sealed class CartDrawerPromoEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CartDrawerPromoEventArgs(RoutedEvent routedEvent, string code, CartDrawerPromo? promo) : base(routedEvent)
    {
        Code = code;
        Promo = promo;
    }

    /// <summary>The code as typed (trimmed).</summary>
    public string Code { get; }

    /// <summary>The accepted promo (null when the code was refused).</summary>
    public CartDrawerPromo? Promo { get; }
}

/// <summary>A <see cref="CartDrawer"/>'s order went through.</summary>
public sealed class CartDrawerOrderPlacedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CartDrawerOrderPlacedEventArgs(RoutedEvent routedEvent, CartDrawerOrder order, CartDrawerReceipt receipt) : base(routedEvent)
    {
        Order = order;
        Receipt = receipt;
    }

    /// <summary>What was ordered.</summary>
    public CartDrawerOrder Order { get; }

    /// <summary>The receipt the drawer shows.</summary>
    public CartDrawerReceipt Receipt { get; }
}
