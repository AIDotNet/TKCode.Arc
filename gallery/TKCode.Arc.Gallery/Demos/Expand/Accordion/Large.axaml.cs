using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public sealed record AccordionFaq(string Question, string Answer);

public partial class AccordionLargeDemo : UserControl
{
    public AccordionLargeDemo()
    {
        InitializeComponent();
        Questions.ItemsSource = new AccordionFaq[]
        {
            new("What is included in the free plan?", "Unlimited projects, three collaborators and a week of version history. Upgrade any time to add more people and keep history for a year."),
            new("Can I change plans later?", "Yes. Plan changes apply immediately and the difference is prorated on your next invoice."),
            new("How do refunds work?", "Annual plans can be refunded within 30 days. Monthly plans can be cancelled at any time and stay active until the end of the period."),
            new("Do you offer discounts for nonprofits?", "Registered nonprofits and schools get 50% off every paid plan. Contact support with your registration details.")
        };
    }
}
