namespace TKCode.Arc;

/// <summary>
/// The directory's filtering and selection rules, free of UI (Arc Team directory, design reference): the search text is
/// trimmed and matched case-insensitively as one substring of the name, role, team or city (no word splitting, no accent
/// folding); the team filter and the search combine; the shown person is the selected one while visible, otherwise the
/// first visible person.
/// </summary>
internal static class TeamDirectoryQuery
{
    /// <summary>True when <paramref name="person"/> matches the search text <paramref name="query"/> (empty matches everyone).</summary>
    public static bool Matches(TeamDirectoryPerson person, string? query)
    {
        var q = query?.Trim();
        if (string.IsNullOrEmpty(q))
        {
            return true;
        }
        return Contains(person.Name, q) || Contains(person.Role, q) || Contains(person.Team, q) || Contains(person.City, q);
    }

    private static bool Contains(string? field, string query) =>
        !string.IsNullOrEmpty(field) && field.Contains(query, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when <paramref name="person"/> belongs to <paramref name="team"/>; a null team means every team.</summary>
    public static bool InTeam(TeamDirectoryPerson person, string? team) =>
        string.IsNullOrEmpty(team) || string.Equals(person.Team, team, StringComparison.Ordinal);

    /// <summary>The people shown for a team filter and a search, in their order.</summary>
    public static List<TeamDirectoryPerson> Visible(IEnumerable<TeamDirectoryPerson> people, string? team, string? query,
        Func<TeamDirectoryPerson, string, bool>? filter)
    {
        var result = new List<TeamDirectoryPerson>();
        var text = query ?? string.Empty;
        foreach (var person in people)
        {
            if (!InTeam(person, team))
            {
                continue;
            }
            if (filter is not null ? filter(person, text) : Matches(person, text))
            {
                result.Add(person);
            }
        }
        return result;
    }

    /// <summary>The person whose profile shows: the selected one if visible, otherwise the first visible person.</summary>
    public static TeamDirectoryPerson? Displayed(IReadOnlyList<TeamDirectoryPerson> visible, string? selectedKey)
    {
        if (!string.IsNullOrEmpty(selectedKey))
        {
            foreach (var person in visible)
            {
                if (person.Key == selectedKey)
                {
                    return person;
                }
            }
        }
        return visible.Count > 0 ? visible[0] : null;
    }

    /// <summary>The teams of the filter: <paramref name="teams"/> when given, otherwise every team in order of first appearance.</summary>
    public static List<string> Teams(IEnumerable<TeamDirectoryPerson> people, IEnumerable<string>? teams)
    {
        var result = new List<string>();
        if (teams is not null)
        {
            foreach (var team in teams)
            {
                if (!string.IsNullOrEmpty(team) && !result.Contains(team))
                {
                    result.Add(team);
                }
            }
            if (result.Count > 0)
            {
                return result;
            }
        }
        foreach (var person in people)
        {
            if (!string.IsNullOrEmpty(person.Team) && !result.Contains(person.Team))
            {
                result.Add(person.Team);
            }
        }
        return result;
    }

    /// <summary>How many people belong to <paramref name="team"/> (null counts everyone); the search does not change it.</summary>
    public static int Count(IEnumerable<TeamDirectoryPerson> people, string? team) => people.Count(p => InTeam(p, team));
}
