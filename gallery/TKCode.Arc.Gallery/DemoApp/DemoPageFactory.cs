namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>Builds the view of each page of the Northstar demo (<c>Pages/Demo&lt;Page&gt;Page.cs</c>).</summary>
public static class DemoPageFactory
{
    public static DemoPageView Create(DemoPage page, IDemoHost host) => page.Slug switch
    {
        "overview" => new DemoOverviewPage(host, page),
        "analytics" => new DemoAnalyticsPage(host, page),
        "customers" => new DemoCustomersPage(host, page),
        "projects" => new DemoProjectsPage(host, page),
        "team" => new DemoTeamPage(host, page),
        "activity" => new DemoActivityPage(host, page),
        "status" => new DemoStatusPage(host, page),
        "integrations" => new DemoIntegrationsPage(host, page),
        "developers" => new DemoDevelopersPage(host, page),
        "billing" => new DemoBillingPage(host, page),
        "settings" => new DemoSettingsPage(host, page),
        _ => throw new ArgumentOutOfRangeException(nameof(page), page.Slug, "The demo has no such page.")
    };
}
