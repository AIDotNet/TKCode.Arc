using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CheckoutSummaryPreviewDemo : UserControl
{
    public CheckoutSummaryPreviewDemo()
    {
        InitializeComponent();
        // The simulated backend: a 700 ms code check (HELLO15, FRIEND5) and a 1400 ms payment that fails while the switch is on.
        Summary.Labels = CheckoutSummarySamples.Labels;
        Summary.CouponValidator = CheckoutSummarySamples.CreateCouponValidator();
        Summary.PayAsync = CheckoutSummarySamples.CreatePayHandler(() => FailNext.IsChecked == true);
        Reset.Click += (_, _) =>
        {
            Summary.ResetPayState();
            Summary.RemoveCoupon();
        };
    }
}
