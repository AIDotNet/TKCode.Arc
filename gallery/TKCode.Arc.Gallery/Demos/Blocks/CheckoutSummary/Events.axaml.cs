using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CheckoutSummaryEventsDemo : UserControl
{
    public CheckoutSummaryEventsDemo()
    {
        InitializeComponent();
        Summary.Labels = new CheckoutSummaryLabels { CouponPlaceholder = "Team code" };
        // A pretend backend: TEAM20 takes 20% off the adjusted subtotal; paying takes a second and always succeeds.
        Summary.CouponValidator = async (code, token) =>
        {
            await Task.Delay(500, token);
            return code.Trim().Equals("TEAM20", StringComparison.OrdinalIgnoreCase)
                ? CheckoutSummaryCouponResult.ValidPercent("TEAM20", 0.20m, "Team discount, 20% off")
                : CheckoutSummaryCouponResult.Invalid("That code is not for this plan.");
        };
        Summary.PayAsync = token => Task.Delay(1000, token);
        Summary.CouponApplied += (_, e) => Write($"CouponApplied: {e.Code}, total {Summary.FormattedTotal}");
        Summary.CouponRejected += (_, e) => Write($"CouponRejected: {e.Code} ({e.Message})");
        Summary.CouponRemoved += (_, e) => Write($"CouponRemoved: {e.Code}, total {Summary.FormattedTotal}");
        Summary.PayRequested += (_, e) => Write($"PayRequested: {e.FormattedTotal}");
        Summary.PayStateChanged += (_, e) => Write($"PayStateChanged: {e.OldState} → {e.NewState}");
    }

    private void Write(string line) => Log.Text = line;
}
