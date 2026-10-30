using System.Globalization;
using System.Text;

namespace TKCode.Arc;

/// <summary>
/// The plain logic behind a <see cref="TeamSwitcher"/>'s menu (search, letters, tints, ids, invites) and its simulated
/// create and join, kept free of UI so it is tested on its own. It holds no workspaces: those come from the app or from a
/// <see cref="TeamSwitcherSimulation"/>.
/// </summary>
internal static class TeamSwitcherLogic
{
    /// <summary>The invite codes of <paramref name="invites"/> as one line for the join view's note.</summary>
    public static string InviteCodes(IReadOnlyList<TeamSwitcherSimulationInvite> invites) =>
        string.Join(", ", invites.Select(i => InviteCode(i.Code)));

    /// <summary>
    /// The workspaces whose name, plan or role contains <paramref name="query"/> (ignoring case and surrounding spaces),
    /// in list order; all of them for an empty query. A plain in-memory scan, fine for hundreds of workspaces.
    /// </summary>
    public static List<int> Filter(IReadOnlyList<TeamSwitcherWorkspace> workspaces, string? query)
    {
        var result = new List<int>(workspaces.Count);
        var q = query?.Trim() ?? string.Empty;
        for (var i = 0; i < workspaces.Count; i++)
        {
            var w = workspaces[i];
            if (q.Length == 0 || Contains(w.Name, q) || Contains(w.Plan, q) || Contains(w.Role, q))
            {
                result.Add(i);
            }
        }
        return result;
    }

    private static bool Contains(string? text, string query) =>
        text is not null && text.Contains(query, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>The letter a tile shows: the first letter or digit of the name, upper case; "?" for none.</summary>
    public static string Monogram(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }
        var enumerator = StringInfo.GetTextElementEnumerator(name.Trim());
        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            if (element.Length > 0 && (char.IsLetterOrDigit(element[0]) || char.IsSurrogate(element[0])))
            {
                return element.ToUpper(CultureInfo.CurrentCulture);
            }
        }
        return "?";
    }

    /// <summary>Which of the five tints (accent, then series 1 to 4) a lettered tile gets: stable for a name.</summary>
    public static int TintIndex(string? name)
    {
        // FNV-1a over the characters: string.GetHashCode is randomized per process.
        var hash = 2166136261u;
        foreach (var c in name ?? string.Empty)
        {
            hash = (hash ^ char.ToUpperInvariant(c)) * 16777619u;
        }
        return (int)(hash % 5);
    }

    /// <summary>An id for a created workspace: the name in lower case with dashes, unique among <paramref name="taken"/>.</summary>
    public static string Slug(string name, IEnumerable<string> taken)
    {
        var builder = new StringBuilder();
        foreach (var c in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }
        var slug = builder.ToString().Trim('-');
        if (slug.Length == 0)
        {
            slug = "workspace";
        }
        var set = new HashSet<string>(taken, StringComparer.Ordinal);
        var candidate = slug;
        for (var n = 2; set.Contains(candidate); n++)
        {
            candidate = slug + "-" + n.ToString(CultureInfo.InvariantCulture);
        }
        return candidate;
    }

    /// <summary>
    /// The invite code in what someone pasted: the last path segment of a link ("https://example.com/invite/ABCD-1234"),
    /// or the text itself, trimmed and in upper case.
    /// </summary>
    public static string InviteCode(string? invite)
    {
        var text = (invite ?? string.Empty).Trim();
        var query = text.IndexOfAny(['?', '#']);
        if (query >= 0)
        {
            text = text[..query];
        }
        text = text.TrimEnd('/');
        var slash = text.LastIndexOf('/');
        if (slash >= 0)
        {
            text = text[(slash + 1)..];
        }
        return text.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// The simulated create: checks the name and makes a one-member workspace on <paramref name="plan"/> with
    /// <paramref name="role"/> (a simulation's <see cref="TeamSwitcherSimulation.CreatedPlan"/> and
    /// <see cref="TeamSwitcherSimulation.CreatedRole"/>; none without one).
    /// </summary>
    public static TeamSwitcherWorkspace Create(string name, IReadOnlyList<TeamSwitcherWorkspace> existing, TeamSwitcherLabels labels,
        string? plan = null, string? role = null)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            throw new InvalidOperationException(labels.NameRequired);
        }
        if (existing.Any(w => string.Equals(w.Name.Trim(), trimmed, StringComparison.CurrentCultureIgnoreCase)))
        {
            throw new InvalidOperationException(labels.NameTaken(trimmed));
        }
        return new TeamSwitcherWorkspace(Slug(trimmed, existing.Select(w => w.Id)), trimmed, plan, 1, role);
    }

    /// <summary>
    /// The simulated join: accepts the codes of <paramref name="invites"/> (a simulation's; none without one), refuses
    /// anything else and invites to a workspace you are already in.
    /// </summary>
    public static TeamSwitcherWorkspace Join(string invite, IReadOnlyList<TeamSwitcherWorkspace> existing, TeamSwitcherLabels labels,
        IReadOnlyList<TeamSwitcherSimulationInvite> invites)
    {
        var code = InviteCode(invite);
        if (code.Length == 0)
        {
            throw new InvalidOperationException(labels.InviteRequired);
        }
        foreach (var known in invites)
        {
            if (known.Workspace is { } workspace && string.Equals(InviteCode(known.Code), code, StringComparison.Ordinal))
            {
                if (existing.Any(w => w.Id == workspace.Id))
                {
                    throw new InvalidOperationException(labels.AlreadyMember(workspace.Name));
                }
                return workspace;
            }
        }
        throw new InvalidOperationException(labels.InviteInvalid);
    }
}
