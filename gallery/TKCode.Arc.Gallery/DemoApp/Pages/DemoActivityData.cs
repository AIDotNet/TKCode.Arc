using System.Globalization;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>A category of the audit log (a chip and the icon on an event's meta line).</summary>
internal enum AuditCategory
{
    Members,
    Billing,
    Security,
    Integrations
}

/// <summary>Who did something in the audit log, and from where.</summary>
internal sealed record AuditActor(string Name, string Ip, string Location, string Client);

/// <summary>One changed field of an audit event; null is "Not set".</summary>
internal sealed record AuditChange(string Field, string? Before, string? After);

/// <summary>
/// One audit event. <see cref="At"/> is in seconds relative to the log's clock at start (negative: that long ago); its
/// absolute time is Sep 22, 2026 14:32:00 UTC plus <see cref="At"/>.
/// </summary>
internal sealed record AuditEvent(string Id, AuditCategory Category, AuditActor Actor, string Action, string Target, double At,
                                  IReadOnlyList<AuditChange> Changes);

/// <summary>The Activity page's data: uiarc.dev's audit log (21 events and 8 live templates) and the team's updates.</summary>
internal static class DemoActivityData
{
    private static readonly CultureInfo s_culture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The audit log's clock at start: Sep 22, 2026 14:32:00 UTC.</summary>
    public static readonly DateTimeOffset AuditBase = new(2026, 9, 22, 14, 32, 0, TimeSpan.Zero);

    public static Geometry PlugIcon { get; } = ArcIcons.Parse("M12 22v-5 M15 8V2 M17 8a1 1 0 0 1 1 1v4a4 4 0 0 1-4 4h-4a4 4 0 0 1-4-4V9a1 1 0 0 1 1-1z M9 8V2");

    private static readonly AuditActor s_emma = new("Emma Collins", "203.0.113.24", "Zurich, Switzerland", "Chrome 129 on macOS");
    private static readonly AuditActor s_jasmine = new("Jasmine Brooks", "198.51.100.7", "London, United Kingdom", "Firefox 130 on Ubuntu");
    private static readonly AuditActor s_sofia = new("Sofia Ramirez", "192.0.2.146", "Lisbon, Portugal", "Safari 18 on macOS");
    private static readonly AuditActor s_ava = new("Ava Mitchell", "203.0.113.81", "Toronto, Canada", "Chrome 129 on Windows");
    private static readonly AuditActor s_marcus = new("Marcus Johnson", "198.51.100.63", "New York, United States", "Edge 129 on Windows");
    private static readonly AuditActor s_okta = new("Okta SCIM", "198.51.100.212", "Okta provisioning", "SCIM 2.0 API");

    public static string Label(AuditCategory category) => category switch
    {
        AuditCategory.Members => "Members",
        AuditCategory.Billing => "Billing",
        AuditCategory.Security => "Security",
        _ => "Integrations"
    };

    public static Geometry Icon(AuditCategory category) => category switch
    {
        AuditCategory.Members => ArcIcons.Users,
        AuditCategory.Billing => ArcIcons.CreditCard,
        AuditCategory.Security => ArcIcons.ShieldCheck,
        _ => PlugIcon
    };

    public static DateTimeOffset Absolute(AuditEvent item) => AuditBase.AddSeconds(item.At);

    /// <summary>"Sep 22, 2026 at 10:20:00 UTC".</summary>
    public static string AbsoluteLabel(AuditEvent item) =>
        Absolute(item).ToString("MMM d, yyyy 'at' HH:mm:ss 'UTC'", s_culture);

    /// <summary>uiarc.dev's relative time from an age in seconds.</summary>
    public static string Relative(double seconds)
    {
        if (seconds < 45)
        {
            return "Just now";
        }
        if (seconds < 3600)
        {
            return $"{Math.Max(1, (int)Math.Round(seconds / 60, MidpointRounding.AwayFromZero))} min ago";
        }
        if (seconds < 86400)
        {
            return $"{(int)Math.Floor(seconds / 3600)} h ago";
        }
        var days = (int)Math.Floor(seconds / 86400);
        return days == 1 ? "Yesterday" : $"{days} days ago";
    }

    /// <summary>A stable event id: <c>evt_</c> and 14 characters from an FNV-1a hash of the id, stirred by xorshift.</summary>
    public static string EventId(string id)
    {
        const string alphabet = "0123456789abcdefghjkmnpqrstvwxyz";
        var hash = 2166136261u;
        foreach (var c in id)
        {
            hash ^= c;
            hash *= 16777619u;
        }
        var chars = new char[14];
        for (var i = 0; i < chars.Length; i++)
        {
            hash ^= hash << 13;
            hash ^= hash >> 17;
            hash ^= hash << 5;
            chars[i] = alphabet[(int)(hash % (uint)alphabet.Length)];
        }
        return "evt_" + new string(chars);
    }

    public static IReadOnlyList<AuditEvent> Events() =>
    [
        E("e01", AuditCategory.Security, s_ava, "required", "two-factor authentication", 252,
            C("Two-factor authentication", "Optional", "Required for all members"), C("Grace period", null, "7 days")),
        E("e02", AuditCategory.Members, s_emma, "changed the role of", "Marcus Johnson", 1120, C("Role", "Member", "Admin")),
        E("e03", AuditCategory.Billing, s_sofia, "updated the billing email to", "ap@northwind.example", 2825,
            C("Billing email", "finance@northwind.example", "ap@northwind.example")),
        E("e04", AuditCategory.Integrations, s_jasmine, "connected Slack to", "#ops-alerts", 7800,
            C("Channel", null, "#ops-alerts"), C("Notifications", null, "Deploys, incidents")),
        E("e05", AuditCategory.Members, s_okta, "deactivated", "jordan.kim@northwind.example", 12300,
            C("Status", "Active", "Deactivated"), C("Seats in use", "48", "47")),
        E("e06", AuditCategory.Security, s_ava, "created the API key", "Production ingest", 19080,
            C("Scopes", null, "events:write"), C("Expires", null, "Mar 22, 2027")),
        E("e07", AuditCategory.Integrations, s_marcus, "rotated the signing secret for", "Stripe webhooks", 33600,
            C("Signing secret", "whsec_••••a91f", "whsec_••••7c2e")),
        E("e08", AuditCategory.Billing, s_sofia, "changed the plan to", "Scale", 76200,
            C("Plan", "Growth", "Scale"), C("Seats", "40", "60"), C("Monthly total", "$1,960.00", "$2,940.00")),
        E("e09", AuditCategory.Members, s_emma, "invited 3 people to", "Design", 100800,
            C("Pending invites", "2", "5"), C("Invited", null, "Hana Sato, Leo Brandt, Ines Duarte")),
        E("e10", AuditCategory.Security, s_ava, "added an IP allowlist range", "185.94.188.0/24", 176400,
            C("Allowed ranges", "2", "3"), C("New range", null, "185.94.188.0/24")),
        E("e11", AuditCategory.Integrations, s_jasmine, "limited GitHub access for", "northwind-labs", 266400,
            C("Repositories", "All (14)", "Selected (12)")),
        E("e12", AuditCategory.Billing, s_sofia, "replaced the payment method with", "Mastercard ending 5530", 352800,
            C("Card", "Visa ending 1881", "Mastercard ending 5530"), C("Expires", "04/26", "11/29")),
        E("e13", AuditCategory.Security, s_jasmine, "reset the password for", "their account", 435600,
            C("Password changed", "Mar 2, 2026", "Sep 17, 2026"), C("Active sessions", "4", "1")),
        E("e14", AuditCategory.Members, s_marcus, "removed", "Priya Nair", 532800, C("Role", "Member", null), C("Seats in use", "49", "48")),
        E("e15", AuditCategory.Security, s_emma, "shortened the session timeout to", "12 hours", 709200,
            C("Session timeout", "30 days", "12 hours")),
        E("e16", AuditCategory.Billing, s_sofia, "downloaded the invoice", "INV-2026-0814", 954000),
        E("e17", AuditCategory.Integrations, s_jasmine, "installed Linear for", "Platform", 1220400,
            C("Status", null, "Connected"), C("Team", null, "Platform")),
        E("e18", AuditCategory.Security, s_ava, "turned on SAML single sign-on with", "Okta", 1476000,
            C("Single sign-on", "Off", "Okta SAML"), C("Enforcement", null, "Optional")),
        E("e19", AuditCategory.Members, s_emma, "changed the default role to", "Viewer", 1836000, C("Default role", "Member", "Viewer")),
        E("e20", AuditCategory.Billing, s_sofia, "added the VAT ID", "CHE-431.927.118 MWST", 2170800,
            C("VAT ID", null, "CHE-431.927.118 MWST")),
        E("e21", AuditCategory.Integrations, s_marcus, "disconnected", "Datadog", 2422800,
            C("Status", "Connected", "Disconnected"), C("Forwarded logs", "api, worker", null))
    ];

    /// <summary>The events the Live switch adds, in order (cycled); their id and time are set when they land.</summary>
    public static IReadOnlyList<AuditEvent> LiveTemplates() =>
    [
        E("t1", AuditCategory.Members, s_okta, "provisioned", "hana.sato@northwind.example", 0,
            C("Status", null, "Active"), C("Seats in use", "47", "48")),
        E("t2", AuditCategory.Security, s_marcus, "signed in from", "a new device", 0,
            C("Trusted devices", "2", "3"), C("Device", null, "MacBook Pro, Safari 18")),
        E("t3", AuditCategory.Integrations, s_jasmine, "moved Slack alerts to", "#incidents", 0, C("Channel", "#ops-alerts", "#incidents")),
        E("t4", AuditCategory.Billing, s_sofia, "added 4 seats to", "Scale", 0,
            C("Seats", "60", "64"), C("Monthly total", "$2,940.00", "$3,136.00")),
        E("t5", AuditCategory.Security, s_ava, "revoked the API key", "Staging ingest", 0, C("Status", "Active", "Revoked")),
        E("t6", AuditCategory.Members, s_emma, "changed the role of", "Sofia Ramirez", 0, C("Role", "Billing admin", "Admin")),
        E("t7", AuditCategory.Billing, s_sofia, "paid the invoice", "INV-2026-0922", 0,
            C("Status", "Open", "Paid"), C("Paid with", null, "Mastercard ending 5530")),
        E("t8", AuditCategory.Integrations, s_marcus, "sent a test delivery to", "Stripe webhooks", 0)
    ];

    private static AuditEvent E(string id, AuditCategory category, AuditActor actor, string action, string target, double secondsAgo,
                                params AuditChange[] changes) => new(id, category, actor, action, target, -secondsAgo, changes);

    private static AuditChange C(string field, string? before, string? after) => new(field, before, after);

    /// <summary>Team updates for the Timeline beside the log (Sep 17 to 22, UTC).</summary>
    public static IReadOnlyList<TimelineEvent> TeamUpdates() =>
    [
        T("e-frames", 22, 14, 52, "shared 14 frames of the Northwind onboarding", "Marcus Johnson", "Northwind app",
            "Covers sign up, workspace setup, and every empty state. Ready for a critique before Thursday."),
        T("e-copy", 22, 13, 30, "commented on the welcome screen", "Olivia Bennett", "Northwind app", Quote("Screen 3 reads a little formal. Could we try “Let’s set up your workspace” instead?")),
        new("e-approved", At(22, 11, 5), "Fieldnotes Co. approved the logo suite")
        {
            Meta = "Fieldnotes identity", Tone = TimelineTone.Success, Icon = ArcIcons.Check
        },
        T("e-tokens", 22, 9, 40, "published Harbor tokens 0.9", "Jasmine Brooks", "Harbor design system",
            "Adds dark mode surfaces and the new focus ring. Eighteen components picked up the change."),
        T("e-recordings", 21, 17, 20, "uploaded 6 interview recordings", "Ava Mitchell", "Tidewater research",
            "Four more sessions are booked this week. Highlights are tagged by topic."),
        new("e-invoice", At(21, 15, 45), "Invoice INV-2291 was paid")
        {
            Meta = "Orbit Labs · $12,400", Tone = TimelineTone.Success, Icon = ArcIcons.Check
        },
        T("e-checkout", 21, 10, 15, "approved the Atlas checkout flows", "Emma Collins", "Atlas platform"),
        T("e-motion", 18, 16, 30, "exported the launch animation", "Ryan Sullivan", "Atlas platform · 24 seconds"),
        new("e-risk", At(18, 11, 0), "Northwind app was flagged at risk")
        {
            Meta = "Six days behind plan", Tone = TimelineTone.Danger, Icon = ArcIcons.TriangleAlert
        },
        T("e-audit", 17, 14, 25, "started the Harbor accessibility audit", "Chloe Nguyen", "Harbor design system")
    ];

    private static DateTimeOffset At(int day, int hour, int minute) => new(2026, 9, day, hour, minute, 0, TimeSpan.Zero);

    // A person's update. There are no photos in the gallery, so the portrait is a placeholder the avatar cannot load: it
    // shows the person's initials.
    private static TimelineEvent T(string id, int day, int hour, int minute, string title, string actor, string meta, object? detail = null) =>
        new(id, At(day, hour, minute), title, actor) { Meta = meta, Detail = detail, Avatar = DemoUi.InitialsPortrait };

    // uiarc.dev quotes the comment: a muted bar on the left of the text.
    private static Control Quote(string text)
    {
        var quote = new Border
        {
            BorderThickness = new Thickness(2, 0, 0, 0),
            Padding = new Thickness(10, 0, 0, 0),
            Child = DemoUi.Text(text, ArcDesignTokenKind.TextSecondary)
        };
        ArcTokens.Bind(quote, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        return quote;
    }
}
