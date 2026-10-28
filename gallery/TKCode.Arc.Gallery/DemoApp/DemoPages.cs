using Avalonia.Media;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>One page of the Northstar demo: its address under <c>/demo</c>, its sidebar label and icon.</summary>
/// <param name="Slug">The last segment of the address, such as <c>overview</c>.</param>
/// <param name="Title">The sidebar label, the breadcrumb and the window title.</param>
/// <param name="Icon">The sidebar icon (Lucide).</param>
/// <param name="IsWorkspace">True for the pages under the sidebar's "Workspace" heading.</param>
public sealed record DemoPage(string Slug, string Title, Geometry Icon, bool IsWorkspace = false);

/// <summary>
/// The pages of the Northstar demo, a copy of uiarc.dev/demo: the eleven app pages in sidebar order and the sign-in page.
/// </summary>
public static class DemoPages
{
    public const string OverviewSlug = "overview";
    public const string LoginSlug = "login";

    /// <summary>The app pages in sidebar order; the last five sit under "Workspace".</summary>
    public static IReadOnlyList<DemoPage> App { get; } =
    [
        new("overview", "Overview", ArcIcons.LayoutGrid),
        new("analytics", "Analytics", ArcIcons.ChartLine),
        new("customers", "Customers", DemoIcons.Building),
        new("projects", "Projects", DemoIcons.FolderKanban),
        new("team", "Team", ArcIcons.Users),
        new("activity", "Activity", ArcIcons.Activity),
        new("status", "Status", DemoIcons.HeartPulse, IsWorkspace: true),
        new("integrations", "Integrations", DemoIcons.Blocks, IsWorkspace: true),
        new("developers", "Developers", DemoIcons.CodeXml, IsWorkspace: true),
        new("billing", "Billing", ArcIcons.CreditCard, IsWorkspace: true),
        new("settings", "Settings", ArcIcons.Settings, IsWorkspace: true)
    ];

    /// <summary>The sign-in page; it has no place in the sidebar.</summary>
    public static DemoPage Login { get; } = new(LoginSlug, "Sign in", DemoIcons.LogIn);

    public static DemoPage Overview => App[0];

    /// <summary>The page with <paramref name="slug"/>, or null.</summary>
    public static DemoPage? Find(string? slug) =>
        slug is null ? null : slug == LoginSlug ? Login : App.FirstOrDefault(page => page.Slug == slug);
}
