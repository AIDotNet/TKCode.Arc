using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class BillingOverviewTrialDemo : UserControl
{
    public BillingOverviewTrialDemo()
    {
        InitializeComponent();
        // Relative to today: the trial ends in 12 days and the card expires next month, so it shows "Expires soon".
        var today = DateTime.Today;
        Billing.Plan!.RenewsOn = today.AddDays(12);
        var expiry = today.AddMonths(1);
        Billing.PaymentMethod!.ExpiryMonth = expiry.Month;
        Billing.PaymentMethod.ExpiryYear = expiry.Year;
    }
}
