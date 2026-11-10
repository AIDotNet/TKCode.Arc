using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AccountHealthOwnAccountDemo : UserControl
{
    public AccountHealthOwnAccountDemo()
    {
        InitializeComponent();
        Account.HealthHistory.AddRange([58, 61, 63, 66, 70, 74, 79, 81]);
        Account.RenewalDate = DateTime.Today.AddDays(120);
        Account.Usage.Add(new AccountHealthUsageMetric("Active properties", [12, 14, 15, 18, 21, 23, 26, 28], "properties") { Limit = 40 });
        Account.Tickets.Add(new AccountHealthTicket("#311", "Room import skips suites", AccountHealthTicketPriority.Normal, AccountHealthTicketStatus.Pending, "2d"));
        Account.Risks.Add(new AccountHealthRisk("Only one admin", "Training a second admin lowers the risk of churn when people leave.", AccountHealthRiskLevel.Low));
        Account.Actions.Add(new AccountHealthAction("Train a second admin", "Offer a 30-minute session.", 3));
        Account.Actions.Add(new AccountHealthAction("Propose the multi-property plan", "They will pass the limit in two quarters.", 5));
    }
}
