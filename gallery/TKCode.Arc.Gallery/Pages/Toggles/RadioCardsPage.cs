using TKCode.Arc.Gallery.Shell;
using Avalonia.Automation;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class RadioCardsPage
{
    public static Control Create()
    {
        var plans = new RadioCards
        {
            ItemsSource = new[]
            {
                new RadioCardOption("starter", "Starter", "For individuals getting started.", "$0") { Icon = ArcIcons.User },
                new RadioCardOption("pro", "Pro", "Unlimited projects and priority support.", "$12") { Icon = ArcIcons.Zap },
                new RadioCardOption("team", "Team", "Shared workspaces and roles for small teams.", "$32") { Icon = ArcIcons.Users },
                new RadioCardOption("enterprise", "Enterprise", "SSO, audit logs and a dedicated contact.", "Custom")
                {
                    Icon = ArcIcons.Shield, IsDisabled = true, DisabledReason = "Talk to sales to enable Enterprise."
                }
            },
            SelectedValue = "pro"
        };
        AutomationProperties.SetName(plans, "Plan");

        var shipping = new RadioCards
        {
            Layout = RadioCardsLayout.List,
            ItemsSource = new[]
            {
                new RadioCardOption("standard", "Standard", "4 to 6 business days", "Free") { Icon = ArcIcons.CalendarDays },
                new RadioCardOption("express", "Express", "2 to 3 business days", "$9.00") { Icon = ArcIcons.Clock },
                new RadioCardOption("overnight", "Overnight", "Next business day before noon", "$24.00") { Icon = ArcIcons.Zap }
            },
            SelectedValue = "standard"
        };
        AutomationProperties.SetName(shipping, "Shipping speed");

        return GalleryUi.Column(20,
            GalleryUi.Card("Grid", "Cards fill columns at least 180px wide. One selection ring springs from card to card; arrows (mirrored right-to-left), Home and End move and select.", plans),
            GalleryUi.Card("List", "Full-width rows with the price at the end; long descriptions wrap.", shipping));
    }
}
