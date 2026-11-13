namespace TKCode.Arc;

/// <summary>
/// Ranks <see cref="MorphNavSearchEntry"/> items for a query in memory: an empty query suggests the first entries; otherwise
/// entries whose label starts with the query come first, then those with a word of the label starting with it, then labels
/// that contain it, then entries found by their description, group or keywords. Ties keep the entries' order.
/// </summary>
internal static class MorphNavSearch
{
    /// <summary>How many entries an empty query suggests.</summary>
    public const int SuggestedCount = 5;

    /// <summary>How many results a query shows at most.</summary>
    public const int MaxResults = 6;

    /// <summary>The entries to show for <paramref name="query"/>, best first, written into <paramref name="results"/>.</summary>
    public static void Rank(IReadOnlyList<MorphNavSearchEntry> entries, string? query, List<MorphNavSearchEntry> results)
    {
        results.Clear();
        var text = query?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            for (var i = 0; i < entries.Count && results.Count < SuggestedCount; i++)
            {
                results.Add(entries[i]);
            }
            return;
        }
        // Four tiers, filled in entry order; at most MaxResults survive.
        Span<int> counts = stackalloc int[4];
        var tiers = new (int Tier, int Index)[entries.Count];
        var found = 0;
        for (var i = 0; i < entries.Count; i++)
        {
            var tier = TierOf(entries[i], text);
            if (tier >= 0)
            {
                tiers[found++] = (tier, i);
                counts[tier]++;
            }
        }
        for (var tier = 0; tier < 4 && results.Count < MaxResults; tier++)
        {
            if (counts[tier] == 0)
            {
                continue;
            }
            for (var k = 0; k < found && results.Count < MaxResults; k++)
            {
                if (tiers[k].Tier == tier)
                {
                    results.Add(entries[tiers[k].Index]);
                }
            }
        }
    }

    /// <summary>0 label prefix, 1 label word prefix, 2 label contains, 3 description, group or keywords; -1 no match.</summary>
    internal static int TierOf(MorphNavSearchEntry entry, string query)
    {
        var label = entry.Label ?? string.Empty;
        if (label.StartsWith(query, StringComparison.CurrentCultureIgnoreCase))
        {
            return 0;
        }
        if (HasWordStartingWith(label, query))
        {
            return 1;
        }
        if (label.Contains(query, StringComparison.CurrentCultureIgnoreCase))
        {
            return 2;
        }
        if (Contains(entry.Description, query) || Contains(entry.Group, query) || Contains(entry.Keywords, query))
        {
            return 3;
        }
        return -1;
    }

    private static bool Contains(string? text, string query) =>
        text is { Length: > 0 } && text.Contains(query, StringComparison.CurrentCultureIgnoreCase);

    private static bool HasWordStartingWith(string text, string query)
    {
        for (var i = 1; i < text.Length; i++)
        {
            if (!char.IsLetterOrDigit(text[i - 1]) && char.IsLetterOrDigit(text[i])
                && text.AsSpan(i).StartsWith(query, StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
