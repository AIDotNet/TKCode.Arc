using Avalonia.Media;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>The Northstar arch, the demo studio's mark (64 × 64, stroked).</summary>
internal static class DemoArt
{
    public static Geometry NorthstarMark { get; } = ArcIcons.Parse("M9 48V29C9 15 19 7 32 7s23 8 23 22v19 M20 48V31c0-8 5-13 12-13s12 5 12 13v17 M32 38v10");
}

/// <summary>A teammate at Northstar.</summary>
public sealed record DemoPerson(string Id, string Name, string Role, string Availability, string Place)
{
    public string FirstName => Name.Split(' ')[0];
}

/// <summary>How a project is doing, with its badge tone.</summary>
public enum DemoProjectStatus
{
    OnTrack,
    AtRisk,
    Review,
    Shipped
}

/// <summary>A client project at Northstar.</summary>
public sealed record DemoProject(string Id, string Name, string Client, string Lead, DemoProjectStatus Status, int Progress, DateTime Due);

/// <summary>
/// The data every page of the Northstar demo shares: the studio, its people and projects, and the notifications in the top
/// bar. Everything is invented, like uiarc.dev's demo; "now" is fixed at Tue Sep 22 2026, 15:10 UTC. Pages keep their own
/// data in their own files (<c>partial</c>).
/// </summary>
public static partial class DemoData
{
    public const string StudioName = "Northstar Studio";
    public const string StudioTagline = "Design studio · 10 people";
    public const string InviteLink = "https://northstar.studio/join/7fq2-kx9m";
    public const string Plan = "Growth";

    /// <summary>The demo's "now": Sep 22 2026, 15:10 UTC.</summary>
    public static DateTime Now { get; } = new(2026, 9, 22, 15, 10, 0, DateTimeKind.Utc);

    public static IReadOnlyList<DemoPerson> People { get; } =
    [
        new("emma", "Emma Collins", "Design director", "Available", "Lisbon · 4:10 PM"),
        new("marcus", "Marcus Johnson", "Senior product designer", "Available", "Brooklyn · 11:10 AM"),
        new("ava", "Ava Mitchell", "Research lead", "In interviews until 5 PM", "London · 4:10 PM"),
        new("jasmine", "Jasmine Brooks", "Design engineer", "Available", "Toronto · 11:10 AM"),
        new("sofia", "Sofia Ramirez", "Brand designer", "Out until Thursday", "Barcelona · 5:10 PM"),
        new("ryan", "Ryan Sullivan", "Motion designer", "Available", "Berlin · 5:10 PM"),
        new("olivia", "Olivia Bennett", "Content designer", "Available", "Mexico City · 9:10 AM"),
        new("chloe", "Chloe Nguyen", "Product designer", "Offline until morning", "Tokyo · 12:10 AM"),
        new("hannah", "Hannah Walsh", "Producer", "Available", "Lagos · 4:10 PM"),
        new("daniel", "Daniel Kim", "Design technologist", "Out sick today", "Milan · 5:10 PM")
    ];

    public static IReadOnlyList<DemoProject> Projects { get; } =
    [
        new("northwind", "Northwind app", "Northwind", "marcus", DemoProjectStatus.AtRisk, 54, new DateTime(2026, 9, 30)),
        new("atlas", "Atlas platform", "Orbit Labs", "emma", DemoProjectStatus.OnTrack, 72, new DateTime(2026, 10, 9)),
        new("fieldnotes", "Fieldnotes identity", "Fieldnotes Co.", "sofia", DemoProjectStatus.Review, 89, new DateTime(2026, 9, 28)),
        new("harbor", "Harbor design system", "Harbor Health", "jasmine", DemoProjectStatus.OnTrack, 41, new DateTime(2026, 11, 13)),
        new("tidewater", "Tidewater research", "Tidewater Bank", "ava", DemoProjectStatus.OnTrack, 63, new DateTime(2026, 10, 21)),
        new("mono", "Mono commerce", "Mono Studio", "chloe", DemoProjectStatus.Shipped, 100, new DateTime(2026, 9, 12))
    ];

    public static DemoPerson Person(string id) => People.First(person => person.Id == id);

    public static string Label(DemoProjectStatus status) => status switch
    {
        DemoProjectStatus.OnTrack => "On track",
        DemoProjectStatus.AtRisk => "At risk",
        DemoProjectStatus.Review => "In review",
        _ => "Shipped"
    };

    public static BadgeTone Tone(DemoProjectStatus status) => status switch
    {
        DemoProjectStatus.AtRisk => BadgeTone.Warning,
        DemoProjectStatus.Review => BadgeTone.Info,
        DemoProjectStatus.Shipped => BadgeTone.Success,
        _ => BadgeTone.Neutral
    };

    /// <summary>The top bar's notifications: five, three unread.</summary>
    public static IEnumerable<NotificationItem> Notifications() =>
    [
        new() { Id = "frames", Title = "Marcus Johnson shared new frames", Description = "14 Northwind onboarding frames are ready for your review.", Time = "18 min ago", ActorName = "Marcus Johnson" },
        new() { Id = "mention", Title = "Olivia Bennett mentioned you", Description = "“Can you check the tone on screen 3 before Thursday?”", Time = "1 hr ago", ActorName = "Olivia Bennett" },
        new() { Id = "risk", Title = "Northwind app is at risk", Description = "Onboarding is six days behind plan.", Time = "2 hr ago", Tone = NotificationTone.Warning },
        new() { Id = "logo", Title = "Fieldnotes Co. approved the logo suite", Description = "Brand guidelines are next.", Time = "4 hr ago", Tone = NotificationTone.Success, IsRead = true },
        new() { Id = "invoice", Title = "Invoice INV-2291 was paid", Description = "Orbit Labs paid $12,400.", Time = "Yesterday", Tone = NotificationTone.Success, IsRead = true }
    ];
}
