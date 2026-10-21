namespace TKCode.Arc;

/// <summary>A Blocks group: the sidebar category a block is filed under, with its code and English and Chinese titles.</summary>
/// <param name="Code">The group's folder name under <c>Blocks/</c>.</param>
/// <param name="Title">The English sidebar title.</param>
/// <param name="ChineseTitle">The Simplified Chinese sidebar title.</param>
public sealed record BlockGroup(string Code, string Title, string ChineseTitle);

/// <summary>An implemented block: its control class, sidebar title, group and one-line description.</summary>
/// <param name="ClassName">The block's control class (also its folder name under <c>Blocks/&lt;Group&gt;/</c>).</param>
/// <param name="Title">The English sidebar title.</param>
/// <param name="Slug">The kebab-case name; for a port of a free Arc block, its uiarc.dev slug.</param>
/// <param name="Group">The <see cref="BlockGroup.Code"/> it is filed under.</param>
/// <param name="Description">One sentence on what it is for.</param>
/// <param name="IsArcPort">True for a port of a free (MIT) Arc block; false for an original TKCode design.</param>
public sealed record BlockInfo(string ClassName, string Title, string Slug, string Group, string Description, bool IsArcPort);

/// <summary>The Blocks taxonomy, in sidebar order, and the blocks implemented so far.</summary>
public static class BlockCatalog
{
    public static readonly BlockGroup[] Groups =
    [
        new("Analytics", "Analytics", "分析"),
        new("Finance", "Finance", "财务"),
        new("LineCharts", "Line charts", "折线图"),
        new("Ai", "AI", "AI"),
        new("AppShell", "App shell", "应用外壳"),
        new("Auth", "Auth", "认证"),
        new("Settings", "Settings", "设置"),
        new("People", "People", "人员"),
        new("Developer", "Developer", "开发者"),
        new("Pricing", "Pricing", "定价"),
        new("Commerce", "Commerce", "电商"),
        new("Support", "Support", "支持"),
        new("Media", "Media", "媒体"),
        new("Projects", "Projects", "项目"),
        new("Heroes", "Heroes", "首屏"),
        new("Features", "Features", "功能"),
        new("SocialProof", "Social proof", "社会证明"),
        new("Content", "Content", "内容"),
        new("Showpieces", "Showpieces", "展示"),
    ];

    /// <summary>The implemented blocks, in sidebar order within their groups.</summary>
    public static readonly BlockInfo[] Blocks =
    [
        new("Sidebar", "Sidebar", "sidebar", "AppShell",
            "Workspace navigation with sections, nested pages, counts and a gliding selection marker; collapses to icons.",
            IsArcPort: false),
        new("PageHeader", "Page header", "page-header", "AppShell",
            "A project page header that folds into a compact bar as you scroll, with tabs whose counts roll.",
            IsArcPort: true),
        new("CommandPalette", "Command palette", "command-palette", "AppShell",
            "A complete keyboard driven action surface with search, grouped results, and shortcuts.",
            IsArcPort: true),
        new("NotificationCenter", "Notification center", "notification-center", "AppShell",
            "A home for updates with read state, grouped information, and animated disclosure.",
            IsArcPort: true),
        new("EmptyStates", "Empty states", "empty-states", "AppShell",
            "Four empty states in one illustration whose shapes morph between scenes as you switch tabs.",
            IsArcPort: true),
        new("SearchResults", "Search results", "search-results", "AppShell",
            "A workspace search page with a suggestions popup, filters whose counts follow the query, a date range, sorting, removable filter chips, highlighted results and a detail sheet.",
            IsArcPort: false),
        new("SettingsPage", "Settings page", "settings-page", "Settings",
            "A full settings page: section navigation with an account card, the chosen section beside it, and a save bar for unsaved changes.",
            IsArcPort: false),
        new("SettingsCommand", "Settings command", "settings-command", "Settings",
            "Find any setting from one search field and change it in place: toggles flip, choices cycle and links open, all from the keyboard.",
            IsArcPort: false),
        new("SecuritySettings", "Security settings", "security-settings", "Settings",
            "A security checkup, password change, two-step verification and signed-in sessions, each handled in place.",
            IsArcPort: false),
        new("BillingOverview", "Billing overview", "billing-overview", "Settings",
            "The current plan, next charge, payment method and invoice history on one page, with the actions next to each.",
            IsArcPort: false),
        new("UsageBilling", "Usage billing", "usage-billing", "Settings",
            "A metered billing period: spend so far, a projected total, daily spend, overage per meter and a spending limit.",
            IsArcPort: false),
        new("CancelFlow", "Cancel flow", "cancel-flow", "Settings",
            "A considerate cancellation: ask why, offer a fitting alternative, show what ends, hold to confirm, and allow a quick return.",
            IsArcPort: false),
    ];
}
