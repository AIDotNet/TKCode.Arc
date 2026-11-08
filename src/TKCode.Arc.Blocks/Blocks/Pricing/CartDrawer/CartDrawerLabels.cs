using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The built-in text of <see cref="CartDrawer"/>. Replace the whole object to localize the block; entries with numbers
/// are functions that receive already formatted amounts and counts. Amounts and dates use <see cref="Culture"/>.
/// </summary>
public sealed class CartDrawerLabels
{
    /// <summary>Culture for amounts, counts and dates (default en-US).</summary>
    public CultureInfo Culture { get; init; } = SettingsFormat.DefaultCulture;

    /// <summary>The cart button's text: "Cart" (hidden in very narrow blocks).</summary>
    public string Cart { get; init; } = "Cart";

    /// <summary>The cart button's accessible name: count ⇒ "Cart, 1 item", "Cart, 3 items".</summary>
    public Func<int, string> CartButtonName { get; init; } = n => n == 1 ? "Cart, 1 item" : $"Cart, {n} items";

    /// <summary>The add button: "Add to cart".</summary>
    public string AddToCart { get; init; } = "Add to cart";

    /// <summary>The add button for a moment after a press: "Added".</summary>
    public string Added { get; init; } = "Added";

    /// <summary>The add button's accessible name: product ⇒ "Add Fluted water jug to cart".</summary>
    public Func<string, string> AddToCartName { get; init; } = name => $"Add {name} to cart";

    /// <summary>The drawer's title: "Your cart".</summary>
    public string DrawerTitle { get; init; } = "Your cart";

    /// <summary>The count beside the title (the number rolls on its own): count ⇒ "item" / "items".</summary>
    public Func<int, string> ItemNoun { get; init; } = n => n == 1 ? "item" : "items";

    /// <summary>The close button: "Close cart".</summary>
    public string Close { get; init; } = "Close cart";

    /// <summary>Above the shipping bar while short of the threshold: missing amount ⇒ "$182 away from free shipping".</summary>
    public Func<string, string> FreeShippingRemaining { get; init; } = amount => $"{amount} away from free shipping";

    /// <summary>Above the shipping bar once the threshold is reached: "Free shipping unlocked".</summary>
    public string FreeShippingUnlocked { get; init; } = "Free shipping unlocked";

    /// <summary>The shipping bar's accessible name: "Progress to free shipping".</summary>
    public string FreeShippingProgress { get; init; } = "Progress to free shipping";

    /// <summary>A quantity stepper's group name: product ⇒ "Fluted water jug quantity".</summary>
    public Func<string, string> QuantityGroup { get; init; } = name => $"{name} quantity";

    /// <summary>The stepper's minus button: product ⇒ "Decrease Fluted water jug".</summary>
    public Func<string, string> Decrease { get; init; } = name => $"Decrease {name}";

    /// <summary>The stepper's plus button: product ⇒ "Increase Fluted water jug".</summary>
    public Func<string, string> Increase { get; init; } = name => $"Increase {name}";

    /// <summary>The row's remove buttons (× and the stepper's bin at quantity 1): product ⇒ "Remove Fluted water jug".</summary>
    public Func<string, string> RemoveItem { get; init; } = name => $"Remove {name}";

    /// <summary>Revealed behind a row swiped left: "Remove".</summary>
    public string SwipeRemove { get; init; } = "Remove";

    /// <summary>The promo code field's placeholder and accessible name: "Promo code".</summary>
    public string PromoPlaceholder { get; init; } = "Promo code";

    /// <summary>The promo code button: "Apply".</summary>
    public string PromoApply { get; init; } = "Apply";

    /// <summary>The hint under the promo field; null or empty hides it (the sample: "Use WORKSHOP10 to save 10%").</summary>
    public string? PromoHint { get; init; }

    /// <summary>Replaces the hint after a code is refused: the code in upper case ⇒ "SPRING doesn't match an offer".</summary>
    public Func<string, string> PromoInvalid { get; init; } = code => $"{code} doesn't match an offer";

    /// <summary>After the code on the applied chip: percent ⇒ "10% off".</summary>
    public Func<string, string> PromoPercentOff { get; init; } = percent => $"{percent} off";

    /// <summary>The applied chip's remove button: "Remove".</summary>
    public string PromoRemove { get; init; } = "Remove";

    /// <summary>The applied chip's remove button, read aloud: code ⇒ "Remove code WORKSHOP10".</summary>
    public Func<string, string> PromoRemoveName { get; init; } = code => $"Remove code {code}";

    /// <summary>Summary row: "Subtotal".</summary>
    public string Subtotal { get; init; } = "Subtotal";

    /// <summary>Summary row: "Discount".</summary>
    public string Discount { get; init; } = "Discount";

    /// <summary>Summary row: "Shipping".</summary>
    public string Shipping { get; init; } = "Shipping";

    /// <summary>Shipping once it costs nothing: "Free".</summary>
    public string Free { get; init; } = "Free";

    /// <summary>Summary row: "Total".</summary>
    public string Total { get; init; } = "Total";

    /// <summary>The checkout button: "Check out".</summary>
    public string Checkout { get; init; } = "Check out";

    /// <summary>The checkout button's accessible name: total ⇒ "Check out, $76".</summary>
    public Func<string, string> CheckoutName { get; init; } = total => $"Check out, {total}";

    /// <summary>The checkout button while the order is placed: "Placing order".</summary>
    public string PlacingOrder { get; init; } = "Placing order";

    /// <summary>The drawer's title on the receipt: "Order placed".</summary>
    public string OrderPlacedTitle { get; init; } = "Order placed";

    /// <summary>Receipt row: "Items".</summary>
    public string ReceiptItems { get; init; } = "Items";

    /// <summary>Receipt row: "Paid".</summary>
    public string ReceiptPaid { get; init; } = "Paid";

    /// <summary>Receipt row: "Arrives".</summary>
    public string ReceiptArrives { get; init; } = "Arrives";

    /// <summary>The receipt's button, which closes the drawer and starts a new cart: "Continue shopping".</summary>
    public string ContinueShopping { get; init; } = "Continue shopping";

    /// <summary>The empty cart's sentence: "Nothing in your cart yet".</summary>
    public string EmptyCart { get; init; } = "Nothing in your cart yet";

    /// <summary>The empty cart's button, which closes the drawer: "Browse products".</summary>
    public string BrowseProducts { get; init; } = "Browse products";

    /// <summary>Announced when a product is added: product ⇒ "Fluted water jug added to cart".</summary>
    public Func<string, string> AnnounceAdded { get; init; } = name => $"{name} added to cart";

    /// <summary>Announced when a row is removed: product ⇒ "Fluted water jug removed from cart".</summary>
    public Func<string, string> AnnounceRemoved { get; init; } = name => $"{name} removed from cart";

    /// <summary>Announced when a code is applied: code ⇒ "Code WORKSHOP10 applied".</summary>
    public Func<string, string> AnnouncePromoApplied { get; init; } = code => $"Code {code} applied";

    /// <summary>Announced when a code is removed: code ⇒ "Code WORKSHOP10 removed".</summary>
    public Func<string, string> AnnouncePromoRemoved { get; init; } = code => $"Code {code} removed";

    /// <summary>Announced when the order goes through: (count, total) ⇒ "Order placed. 2 items, $134."</summary>
    public Func<int, string, string> AnnounceOrderPlaced { get; init; } =
        (n, total) => n == 1 ? $"Order placed. 1 item, {total}." : $"Order placed. {n} items, {total}.";
}
