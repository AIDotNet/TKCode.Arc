using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Invented sample content and the simulated store behind the <see cref="CartDrawer"/> preview: six homeware products
/// with drawn pictures, one promo code and an order desk that confirms after 1.3 seconds. Replace them with your catalog
/// and backend; none of it is required.
/// </summary>
public static class CartDrawerSamples
{
    /// <summary>The grid title of the sample store.</summary>
    public const string Title = "Fresh from the workshop";

    /// <summary>The sample promo code (10% off).</summary>
    public const string PromoCode = "WORKSHOP10";

    /// <summary>The sample free-shipping threshold.</summary>
    public const decimal FreeShippingThreshold = 240m;

    /// <summary>The sample shipping fee below the threshold.</summary>
    public const decimal ShippingFee = 9m;

    /// <summary>How long the sample order desk takes to confirm.</summary>
    public static readonly TimeSpan PlaceOrderDelay = TimeSpan.FromMilliseconds(1300);

    /// <summary>
    /// Six new sample products (a fresh set on every call): one four-digit, two three-digit and three two-digit prices; the
    /// water jug starts in the cart.
    /// </summary>
    public static IReadOnlyList<CartDrawerProduct> CreateProducts() =>
    [
        Product("ash-chair", "Ash reading chair", "Steam-bent ash, wool", 1180m, CartDrawerArtworkKind.Chair),
        Product("arc-light", "Arc floor light", "Frosted glass, 3000K", 172m, CartDrawerArtworkKind.Lamp),
        Product("fluted-jug", "Fluted water jug", "Recycled glass, 0.9 L", 58m, CartDrawerArtworkKind.Jug, initial: 1),
        Product("speckled-mugs", "Speckled mugs", "Pair, reactive glaze", 42m, CartDrawerArtworkKind.Mugs),
        Product("stacking-dishes", "Stacking dishes", "Matte stoneware, trio", 66m, CartDrawerArtworkKind.Dishes),
        Product("waffle-blanket", "Waffle blanket", "Organic cotton, tasselled", 124m, CartDrawerArtworkKind.Blanket)
    ];

    /// <summary>The labels of the sample store: the default English text plus the promo hint "Use WORKSHOP10 to save 10%".</summary>
    public static CartDrawerLabels CreateLabels(CultureInfo? culture = null) => new()
    {
        Culture = culture ?? SettingsFormat.DefaultCulture,
        PromoHint = $"Use {PromoCode} to save 10%"
    };

    /// <summary>
    /// The sample code check: <see cref="PromoCode"/> in any letter case is 10% off, anything else is refused. Answers at
    /// once.
    /// </summary>
    public static Task<CartDrawerPromo?> ValidatePromoAsync(string code, CancellationToken cancellationToken)
    {
        var accepted = string.Equals(code.Trim(), PromoCode, StringComparison.OrdinalIgnoreCase)
            ? new CartDrawerPromo(PromoCode, 0.10m)
            : null;
        return Task.FromResult(accepted);
    }

    /// <summary>
    /// The sample order desk: waits <see cref="PlaceOrderDelay"/> and confirms with an invented receipt (order W-58302 for
    /// Mira, delivery 3 to 5 days from today). Use <see cref="CreatePlaceOrder"/> to change the wait or the clock.
    /// </summary>
    public static Task<CartDrawerReceipt> PlaceOrderAsync(CartDrawerOrder order, CancellationToken cancellationToken)
        => CreatePlaceOrder()(order, cancellationToken);

    /// <summary>
    /// Builds a sample order desk with its own wait (default 1.3 s), delay function (tests step it) and "today" (default the
    /// local date), formatting dates with <paramref name="culture"/>.
    /// </summary>
    public static Func<CartDrawerOrder, CancellationToken, Task<CartDrawerReceipt>> CreatePlaceOrder(
        TimeSpan? delay = null,
        Func<TimeSpan, CancellationToken, Task>? wait = null,
        Func<DateTime>? today = null,
        CultureInfo? culture = null)
    {
        var duration = delay ?? PlaceOrderDelay;
        var waiter = wait ?? ((d, ct) => Task.Delay(d, ct));
        var clock = today ?? (() => DateTime.Today);
        var format = culture ?? SettingsFormat.DefaultCulture;
        return async (order, ct) =>
        {
            if (duration > TimeSpan.Zero)
            {
                await waiter(duration, ct).ConfigureAwait(true);
            }
            return new CartDrawerReceipt(
                "All set, Mira",
                "Order W-58302 is confirmed. We emailed the receipt to mira@lumen-house.test.",
                order.ItemCount,
                order.Total,
                CartDrawerMath.ArrivalWindow(clock(), format));
        };
    }

    private static CartDrawerProduct Product(string id, string name, string detail, decimal price, CartDrawerArtworkKind art, int initial = 0) => new()
    {
        Id = id,
        Name = name,
        Detail = detail,
        Price = price,
        Image = CartDrawerArtwork.Get(art),
        InitialQuantity = initial
    };
}
