using System.Globalization;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class PricingCalculatorLocalizedDemo : UserControl
{
    public PricingCalculatorLocalizedDemo()
    {
        InitializeComponent();
        Calculator.Labels = new PricingCalculatorLabels
        {
            Culture = CultureInfo.GetCultureInfo("de-DE"),
            TeamSize = "Seats",
            Recommended = "Our pick",
            Selected = "Chosen",
            Pick = plan => $"Choose {plan}",
            StartTrial = plan => $"Start {plan}",
            SavingsFormat = "{0} less per year",
            SwitchToYearly = amount => $"Pay yearly, keep {amount}"
        };
        // Plans without a yearly price take the 15% discount; these samples drop theirs.
        var plans = PricingCalculatorSamples.CreatePlans();
        foreach (var plan in plans)
        {
            plan.YearlySeatPrice = null;
        }
        Calculator.PlansSource = plans;
    }
}
