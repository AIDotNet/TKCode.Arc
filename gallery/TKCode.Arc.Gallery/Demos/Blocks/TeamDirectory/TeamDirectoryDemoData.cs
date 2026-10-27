using AtomUI.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The TeamDirectory demos' data (all invented): six people of a made-up company in three teams of two, four available
/// and two away, with placeholder portraits. Priya works in Malmö, so searching "malmo" shows that accents are not folded,
/// and "operations" finds Camila by her team although her role does not say so.
/// </summary>
public static class TeamDirectoryDemoData
{
    public static TeamDirectoryPerson[] People() =>
    [
        Person("lena", "Lena Hartmann", "Interaction designer", "Design", true, "Checkout redesign", "Vienna", "CET", 47,
            "Turns rough ideas into flows people can follow. Ask Lena about usability reviews, motion details or a quick critique."),
        Person("theo", "Theo Brandt", "Mobile engineer", "Engineering", true, "Offline sync layer", "Manchester", "GMT", 12,
            "Builds the mobile apps and keeps them fast on slow networks. Talk to Theo about offline data, releases or crash reports."),
        Person("priya", "Priya Nair", "Head of design", "Design", false, "Brand refresh", "Malmö", "CET", 45,
            "Leads the design team and the brand work. Bring Priya your hiring questions, portfolio reviews or a half-finished deck."),
        Person("jonas", "Jonas Weber", "Backend engineer", "Engineering", true, "Billing service", "Munich", "CET", 59,
            "Looks after billing and the services behind it. Jonas is the person for invoices, retries and anything about webhooks."),
        Person("camila", "Camila Ortiz", "Office manager", "Operations", false, "Hiring logistics", "Boston", "ET", 44,
            "Keeps the office, travel and onboarding running. Ask Camila about desks, visitor passes or the next team offsite."),
        Person("fiona", "Fiona Byrne", "Account manager", "Operations", true, "Renewals program", "Cork", "GMT", 32,
            "Works with our larger customers from first call to renewal. Fiona can help with contracts, pricing or a demo."),
    ];

    /// <summary>A longer directory: the six above plus a support team, for the flat demo.</summary>
    public static TeamDirectoryPerson[] LargerDirectory() =>
    [
        .. People(),
        Person("ada", "Ada Kim", "Support lead", "Support", true, "Help center rewrite", "Seoul", "KST", 25,
            "Runs the support desk and the help center. Ask Ada about tricky tickets, macros or what customers keep asking."),
        Person("noah", "Noah Stein", "Support engineer", "Support", false, "Escalation tooling", "Zurich", "CET", 53,
            "Digs into the bugs that reach support first. Noah is the one for logs, reproductions and quick workarounds."),
    ];

    private static TeamDirectoryPerson Person(string id, string name, string role, string team, bool available, string focus,
        string city, string timeZone, int portrait, string bio) => new(name, role, team, id)
    {
        Availability = available ? TeamDirectoryAvailability.Available : TeamDirectoryAvailability.Away,
        Focus = focus,
        City = city,
        TimeZone = timeZone,
        Email = id + "@northwind.test",
        Bio = bio,
        Avatar = ImageSource.TryParse($"https://i.pravatar.cc/160?img={portrait}", out var source) ? source : null,
    };
}
