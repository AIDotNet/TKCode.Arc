namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The trial the banner demos show: an invented product's Pro plan on the invented Northwind workspace, which would lose
/// SAML SSO, team chat alerts, issue sync and its 12 seats. Demo content only (no real product is named).
/// </summary>
internal static class TrialBannerDemoData
{
    /// <summary>The plan on trial.</summary>
    public const string Plan = "Pro";

    /// <summary>The workspace on trial.</summary>
    public const string Workspace = "Northwind";

    /// <summary>The short loss phrase of the banner copy.</summary>
    public const string LossSummary = "SAML SSO and 12 seats";

    /// <summary>The four rows of what the workspace would lose (new instances each call: a banner owns its rows).</summary>
    public static IReadOnlyList<TrialFeature> Features() =>
    [
        new("sso", "SAML single sign-on", "for every member", ArcIcons.Key),
        new("alerts", "Alerts in team chat", "incidents and mentions", ArcIcons.Bell),
        new("sync", "Two-way issue sync", "status and comments", ArcIcons.RefreshCw),
        new("seats", "Team seats", "12 seats", ArcIcons.Users),
    ];

    /// <summary>Gives <paramref name="banner"/> the demo's trial: the plan, the workspace, the loss and the features.</summary>
    public static void Apply(TrialBanner banner)
    {
        banner.Plan = Plan;
        banner.Workspace = Workspace;
        banner.LossSummary = LossSummary;
        banner.Features.Clear();
        banner.Features.AddRange(Features());
    }
}
