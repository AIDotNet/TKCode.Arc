using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One person of a <see cref="TeamShowcase"/>: name, role, department, a short bio and, optionally, a place and a
/// picture. Without a picture the person gets a generated portrait that depends on <see cref="Seed"/> (or the name).
/// Set the properties before the member is added; later changes are not observed.
/// </summary>
public sealed class TeamShowcaseMember
{
    /// <summary>The person's name, shown under the portrait and used as the card's screen reader name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The job title under the name.</summary>
    public string? Role { get; set; }

    /// <summary>The department the filter groups by; null leaves the person out of every department but "All".</summary>
    public string? Department { get; set; }

    /// <summary>A sentence or two shown over the portrait on hover, keyboard focus or a tap.</summary>
    public string? Bio { get; set; }

    /// <summary>Where the person works from, shown under the bio; null leaves it out.</summary>
    public string? Location { get; set; }

    /// <summary>A picture that replaces the generated portrait.</summary>
    public IImage? Image { get; set; }

    /// <summary>Chooses the generated portrait; null derives it from <see cref="Name"/>.</summary>
    public int? Seed { get; set; }

    /// <summary>Anything the app wants to keep with the person (an id, a profile link).</summary>
    public object? Tag { get; set; }
}

/// <summary>Arguments of <see cref="TeamShowcase.MemberInvoked"/>.</summary>
public sealed class TeamShowcaseMemberEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="member"/>.</summary>
    public TeamShowcaseMemberEventArgs(RoutedEvent routedEvent, TeamShowcaseMember member) : base(routedEvent)
    {
        Member = member;
    }

    /// <summary>The person whose card was pressed.</summary>
    public TeamShowcaseMember Member { get; }
}

/// <summary>The invented team <see cref="TeamShowcase"/> shows with no configuration.</summary>
internal static class TeamShowcaseSamples
{
    public static IEnumerable<TeamShowcaseMember> Create() =>
    [
        new() { Name = "Mara Okonjo", Role = "Head of Design", Department = "Design", Location = "Lisbon",
            Bio = "Draws the first version of everything on paper, then argues with it for a week." },
        new() { Name = "Teo Lindgren", Role = "Staff Engineer", Department = "Engineering", Location = "Stockholm",
            Bio = "Owns the sync engine and the habit of deleting more code than he writes." },
        new() { Name = "Priya Ramaswamy", Role = "Product Lead", Department = "Product", Location = "Bengaluru",
            Bio = "Turns forty customer calls into one page that everybody actually reads." },
        new() { Name = "Jonah Whitfield", Role = "Brand Designer", Department = "Design", Location = "Bristol",
            Bio = "Keeps the type scale honest and the icon set down to the ones we need." },
        new() { Name = "Sofia Marchetti", Role = "Frontend Engineer", Department = "Engineering", Location = "Turin",
            Bio = "Makes every animation feel inevitable, and every list scroll at sixty frames." },
        new() { Name = "Kwame Asante", Role = "Operations Manager", Department = "Operations", Location = "Accra",
            Bio = "Runs payroll, travel and the office plants with the same calm spreadsheet." },
        new() { Name = "Lena Hoffmann", Role = "Product Designer", Department = "Design", Location = "Leipzig",
            Bio = "Prototypes in code when the mockup stops telling the truth." },
        new() { Name = "Rafael Quintero", Role = "Platform Engineer", Department = "Engineering", Location = "Bogotá",
            Bio = "Keeps the builds under four minutes and the pager quiet at night." },
        new() { Name = "Hana Sato", Role = "Product Manager", Department = "Product", Location = "Osaka",
            Bio = "Writes the release notes first, then helps the team make them true." },
        new() { Name = "Noor Haddad", Role = "People Partner", Department = "Operations", Location = "Amman",
            Bio = "Built our onboarding so that week one ends with something shipped." },
    ];
}
