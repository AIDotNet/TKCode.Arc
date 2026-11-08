using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CheckoutSummaryNarrowDemo : UserControl
{
    public CheckoutSummaryNarrowDemo()
    {
        InitializeComponent();
        Summary.Labels = CheckoutSummarySamples.Labels;
        Summary.CouponValidator = CheckoutSummarySamples.CreateCouponValidator();
        Summary.PayAsync = CheckoutSummarySamples.CreatePayHandler();
    }
}
