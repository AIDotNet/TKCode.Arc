using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CartDrawerOwnStoreDemo : UserControl
{
    private readonly List<string> _events = new();

    public CartDrawerOwnStoreDemo()
    {
        InitializeComponent();
        Store.ProductsSource = CartDrawerDemoData.TeaProducts();
        Store.Labels = new CartDrawerLabels
        {
            Culture = System.Globalization.CultureInfo.GetCultureInfo("en-GB"),
            PromoHint = "Members: try STEEP15",
            FreeShippingRemaining = amount => $"Add {amount} for free delivery",
            FreeShippingUnlocked = "Free delivery unlocked"
        };

        // Your backend: here STEEP15 is 15% off and every other code is refused after a short check.
        Store.ValidatePromoAsync = async (code, cancellation) =>
        {
            await Task.Delay(250, cancellation);
            return string.Equals(code, "STEEP15", StringComparison.OrdinalIgnoreCase) ? new CartDrawerPromo("STEEP15", 0.15m) : null;
        };
        Store.PlaceOrderAsync = async (order, cancellation) =>
        {
            await Task.Delay(900, cancellation);
            return new CartDrawerReceipt(
                "Thank you, Ines",
                "Order LK-1182 is on its way to the roastery. A copy went to ines@leafandkettle.test.",
                order.ItemCount,
                order.Total,
                "Tomorrow");
        };

        Store.ItemAdded += (_, e) => Add($"Added {e.Product.Name} ({e.NewQuantity})");
        Store.QuantityChanged += (_, e) => Add($"{e.Product.Name}: {e.OldQuantity} → {e.NewQuantity}");
        Store.ItemRemoved += (_, e) => Add($"Removed {e.Product.Name}");
        Store.PromoApplied += (_, e) => Add($"Code {e.Promo!.Code} applied");
        Store.PromoRejected += (_, e) => Add($"Code {e.Code} refused");
        Store.PromoRemoved += (_, e) => Add($"Code {e.Code} removed");
        Store.OrderPlaced += (_, e) => Add($"Order placed: {e.Order.ItemCount} items, £{e.Order.Total:0.00}");
    }

    private void Add(string line)
    {
        _events.Insert(0, line);
        if (_events.Count > 12)
        {
            _events.RemoveAt(_events.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _events);
    }
}
