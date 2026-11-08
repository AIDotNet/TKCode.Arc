using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CheckoutSummaryCustomLabelsDemo : UserControl
{
    public CheckoutSummaryCustomLabelsDemo()
    {
        InitializeComponent();
        Summary.Labels = new CheckoutSummaryLabels
        {
            CouponFieldName = "Gift code",
            CouponPlaceholder = "Gift code",
            Apply = "Redeem",
            CheckingCode = "Checking gift code",
            Applied = "redeemed",
            RemoveCode = code => $"Remove gift code {code}",
            Subtotal = "Items",
            TaxPending = "Added at the next step",
            Total = "To pay",
            TotalAnnouncement = amount => $"To pay {amount}",
            Pay = amount => $"Pay {amount} now",
            Processing = "Paying",
            Paid = "Done",
            TryAgain = "Retry payment",
            PayFailed = "That did not work. You were not charged."
        };
        Summary.CouponValidator = async (code, token) =>
        {
            await Task.Delay(700, token);
            return code.Trim().Equals("GIFT10", StringComparison.OrdinalIgnoreCase)
                ? CheckoutSummaryCouponResult.Valid("GIFT10", 10m, "Gift code")
                : CheckoutSummaryCouponResult.Invalid("We don't know that gift code.");
        };
        Summary.PayAsync = token => Task.Delay(1400, token);
    }
}
