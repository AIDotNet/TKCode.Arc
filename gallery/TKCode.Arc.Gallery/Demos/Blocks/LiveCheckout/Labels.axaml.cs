using System.Globalization;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class LiveCheckoutLabelsDemo : UserControl
{
    public LiveCheckoutLabelsDemo()
    {
        InitializeComponent();
        Checkout.Wallets = [];
        Checkout.TestCard = null;
        Checkout.Labels = new LiveCheckoutLabels
        {
            Culture = CultureInfo.GetCultureInfo("en-GB"),
            PlanHeading = "Choose a plan",
            AddonsHeading = "Extras",
            BillingHeading = "Invoice details",
            CardHeading = "Payment card",
            OrderSummary = "Your order",
            DueToday = "To pay now",
            PaidToday = "Paid",
            Pay = "Subscribe for",
            Processing = "One moment…",
            PaymentSuccessful = "Welcome aboard",
            ReceiptSentence = (plan, seats) => $"{plan} is ready for your {seats} people.",
            SecureNote = "Sample only. No money moves."
        };
    }
}
