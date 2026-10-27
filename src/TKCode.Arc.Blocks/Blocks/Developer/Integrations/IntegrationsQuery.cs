namespace TKCode.Arc;

/// <summary>
/// The filter and search rules of <see cref="Integrations"/> (pure, tested on their own): a filter is "all", "connected"
/// (connected apps only, not those still connecting) or a category key; a query matches the name, the category's label or
/// the description, ignoring case, after trimming. Counts on the filters ignore the search (Arc design reference).
/// </summary>
internal static class IntegrationsQuery
{
    /// <summary>The filter that shows every app.</summary>
    public const string All = "all";

    /// <summary>The filter that shows the connected apps.</summary>
    public const string Connected = "connected";

    /// <summary>The query as it is matched and announced: trimmed and lower case.</summary>
    public static string Normalize(string? query) => (query ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>True when <paramref name="item"/> belongs to <paramref name="filter"/>.</summary>
    public static bool MatchesFilter(IntegrationItem item, string? filter)
    {
        if (string.IsNullOrEmpty(filter) || filter == All)
        {
            return true;
        }
        if (filter == Connected)
        {
            return item.Status == IntegrationStatus.Connected;
        }
        return string.Equals(item.Category, filter, StringComparison.Ordinal);
    }

    /// <summary>True when the normalized <paramref name="query"/> is empty or found in the name, the category label or the description.</summary>
    public static bool MatchesQuery(IntegrationItem item, string query, string? categoryLabel)
    {
        if (query.Length == 0)
        {
            return true;
        }
        return Contains(item.Name, query) || Contains(categoryLabel, query) || Contains(item.Description, query);
    }

    /// <summary>The apps the list shows, in their order.</summary>
    public static List<IntegrationItem> Visible(IEnumerable<IntegrationItem> items, string? filter, string? query,
        Func<string?, string?> categoryLabel)
    {
        var q = Normalize(query);
        var result = new List<IntegrationItem>();
        foreach (var item in items)
        {
            if (MatchesFilter(item, filter) && MatchesQuery(item, q, categoryLabel(item.Category)))
            {
                result.Add(item);
            }
        }
        return result;
    }

    /// <summary>How many apps belong to <paramref name="filter"/> (the number on its pill; the search is ignored).</summary>
    public static int Count(IEnumerable<IntegrationItem> items, string filter)
    {
        var count = 0;
        foreach (var item in items)
        {
            if (MatchesFilter(item, filter))
            {
                count++;
            }
        }
        return count;
    }

    private static bool Contains(string? text, string query) =>
        !string.IsNullOrEmpty(text) && text.Contains(query, StringComparison.OrdinalIgnoreCase);
}
