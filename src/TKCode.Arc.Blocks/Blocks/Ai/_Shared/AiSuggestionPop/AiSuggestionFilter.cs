namespace TKCode.Arc;

/// <summary>
/// Turns the composer's commands and context items into the rows of an <see cref="AiSuggestionPop"/> for the query typed
/// after "/" or "@". Pure: no state, no Avalonia layout. Matching ignores case (ordinal, culture-independent).
/// </summary>
internal static class AiSuggestionFilter
{
    /// <summary>
    /// The commands whose name starts with <paramref name="query"/> (all of them for an empty query), in source order.
    /// Commands without a name are skipped (they cannot become a token).
    /// </summary>
    public static IReadOnlyList<AiSuggestionRow> Commands(IEnumerable<AiCommand>? commands, string? query)
    {
        if (commands is null)
        {
            return Array.Empty<AiSuggestionRow>();
        }
        query ??= string.Empty;
        var rows = new List<AiSuggestionRow>();
        foreach (var command in commands)
        {
            if (command is null || string.IsNullOrEmpty(command.Name))
            {
                continue;
            }
            if (command.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                rows.Add(new AiSuggestionRow(command.Id, command.Name, command.Description, command.Icon, null));
            }
        }
        return rows;
    }

    /// <summary>
    /// The context items whose label or hint contains <paramref name="query"/> (all of them for an empty query), grouped by
    /// <see cref="AiContextItem.Group"/> in <paramref name="groupOrder"/> order. Items whose group is null or not in the order
    /// come last under no heading (their row's <see cref="AiSuggestionRow.Group"/> is null). Within a group, labels that
    /// start with the query come first; otherwise source order is kept. Groups without matches are dropped. Group names
    /// compare ordinally (case matters), as headings show them as given.
    /// </summary>
    public static IReadOnlyList<AiSuggestionRow> Mentions(IEnumerable<AiContextItem>? items, IReadOnlyList<string>? groupOrder,
                                                          string? query)
    {
        if (items is null)
        {
            return Array.Empty<AiSuggestionRow>();
        }
        query ??= string.Empty;
        var order = groupOrder ?? Array.Empty<string>();
        // One bucket per known group plus one for the rest, each split into label-prefix matches and other matches.
        var buckets = new List<AiContextItem>[(order.Count + 1) * 2];
        foreach (var item in items)
        {
            if (item is null)
            {
                continue;
            }
            var label = item.Label ?? string.Empty;
            var prefix = label.StartsWith(query, StringComparison.OrdinalIgnoreCase);
            if (!prefix && label.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                (item.Hint is not { } hint || hint.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0))
            {
                continue;
            }
            var group = GroupIndex(order, item.Group);
            var slot = group * 2 + (prefix ? 0 : 1);
            (buckets[slot] ??= new List<AiContextItem>()).Add(item);
        }
        var rows = new List<AiSuggestionRow>();
        for (var group = 0; group <= order.Count; group++)
        {
            var heading = group < order.Count ? order[group] : null;
            for (var part = 0; part < 2; part++)
            {
                if (buckets[group * 2 + part] is not { } bucket)
                {
                    continue;
                }
                foreach (var item in bucket)
                {
                    rows.Add(new AiSuggestionRow(item.Id, item.Label ?? string.Empty, item.Hint, item.Icon, heading));
                }
            }
        }
        return rows;
    }

    /// <summary>The index of <paramref name="group"/> in <paramref name="order"/> (first occurrence), or the order's length.</summary>
    private static int GroupIndex(IReadOnlyList<string> order, string? group)
    {
        if (group is not null)
        {
            for (var i = 0; i < order.Count; i++)
            {
                if (string.Equals(order[i], group, StringComparison.Ordinal))
                {
                    return i;
                }
            }
        }
        return order.Count;
    }
}
