using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class UsagePricingLocalizedDemo : UserControl
{
    public UsagePricingLocalizedDemo()
    {
        InitializeComponent();
        Pricing.Labels = UsagePricingDemoData.GermanFormatLabels;
    }
}
