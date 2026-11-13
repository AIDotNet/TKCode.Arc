namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="TeamDirectory"/> shows or announces, in English by default. Replace the instance to translate or
/// reword; texts with values in them are functions so that word order can change.
/// </summary>
public sealed class TeamDirectoryLabels
{
    /// <summary>The heading; the number of people follows it.</summary>
    public string Title { get; init; } = "People";

    /// <summary>The hint in the empty search box.</summary>
    public string SearchPlaceholder { get; init; } = "Find by name, role or city";

    /// <summary>The screen reader name of the search box.</summary>
    public string SearchName { get; init; } = "Search people";

    /// <summary>The screen reader name of the button that empties the search box.</summary>
    public string ClearSearchName { get; init; } = "Clear search";

    /// <summary>The screen reader name of the row of team buttons.</summary>
    public string FilterGroupName { get; init; } = "Team filter";

    /// <summary>The team button that shows everyone.</summary>
    public string AllTeams { get; init; } = "All";

    /// <summary>The screen reader name of a team button; the arguments are the team's label and its number of people.</summary>
    public Func<string, int, string> FilterButtonName { get; init; } = (team, count) => $"{team}, {count}";

    /// <summary>The screen reader name of the list of people.</summary>
    public string ListName { get; init; } = "Directory";

    /// <summary>The line shown when nobody matches; the argument is the search text as typed (it may be empty).</summary>
    public Func<string, string> NoMatches { get; init; } =
        query => string.IsNullOrWhiteSpace(query) ? "Nobody in this team yet" : $"Nobody found for “{query}”";

    /// <summary>The button under that line, which empties the search and shows every team again.</summary>
    public string ClearFilters { get; init; } = "Clear filters";

    /// <summary>The first line of the profile area when nobody matches.</summary>
    public string NoneTitle { get; init; } = "No one to show";

    /// <summary>The second line of the profile area when nobody matches.</summary>
    public string NoneHint { get; init; } = "Try another search or team.";

    /// <summary>The label of the profile's status field.</summary>
    public string StatusLabel { get; init; } = "Status";

    /// <summary>The label of the profile's team field.</summary>
    public string TeamLabel { get; init; } = "Team";

    /// <summary>The label of the profile's current work field.</summary>
    public string FocusLabel { get; init; } = "Working on";

    /// <summary>The label of the profile's location field.</summary>
    public string LocationLabel { get; init; } = "Location";

    /// <summary>The label of the profile's email field.</summary>
    public string EmailLabel { get; init; } = "Email";

    /// <summary>The status text of an available person.</summary>
    public string Available { get; init; } = "Available";

    /// <summary>The status text of a person who is away.</summary>
    public string Away { get; init; } = "Away";

    /// <summary>The copy button's text at rest.</summary>
    public string Copy { get; init; } = "Copy";

    /// <summary>The copy button's text after a copy worked.</summary>
    public string Copied { get; init; } = "Copied";

    /// <summary>The copy button's text after a copy failed.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The copy button's screen reader name at rest and after a failure.</summary>
    public string CopyName { get; init; } = "Copy email address";

    /// <summary>The copy button's screen reader name after a copy worked.</summary>
    public string CopiedName { get; init; } = "Email address copied";

    /// <summary>Announced when a copy worked; the argument is the person's name.</summary>
    public Func<string, string> CopiedAnnouncement { get; init; } = name => $"Email address of {name} copied.";

    /// <summary>Announced when a copy failed.</summary>
    public string CopyFailedAnnouncement { get; init; } = "Could not copy the email address. Try again.";

    /// <summary>The screen reader name of a row's avatar; the arguments are the name and whether the person is available.</summary>
    public Func<string, bool, string> AvatarName { get; init; } = (name, available) => available ? $"{name}, online" : $"{name}, away";

    /// <summary>The screen reader name of the profile; the arguments are the name and the role.</summary>
    public Func<string, string, string> ProfileName { get; init; } =
        (name, role) => string.IsNullOrEmpty(role) ? name : $"{name}, {role}";
}
