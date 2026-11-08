using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CreditTopUpDeclinedDemo : UserControl
{
    public CreditTopUpDeclinedDemo()
    {
        InitializeComponent();
        // Stand-in for a payment API: declines the first charge, accepts the next, 1.2 seconds each.
        Card.PurchaseHandler = CreditTopUpSamples.DecliningPurchase(TimeSpan.FromMilliseconds(1200));
        Log.Text = "Buy once to see the declined state, then press Retry.";
        Card.PurchaseFailed += (_, e) => Log.Text = $"Declined: {e.Order.Credits:N0} credits for {e.Order.Price:0.00} {e.Order.Currency} · \"{e.Message}\"";
        Card.Purchased += (_, e) => Log.Text = $"Charged {e.Order.PaymentMethodId}: {e.Order.Total:N0} credits added, balance {e.Balance:N0}";
        Card.PaymentMethodChanged += (_, e) => Log.Text = $"Card changed to {e.PaymentMethod.DisplayBrand} ending {e.PaymentMethod.Last4}";
    }
}
