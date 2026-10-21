namespace TKCode.Arc;

/// <summary>A highlighted stretch of text: <see cref="Start"/> and <see cref="Length"/> in UTF-16 code units.</summary>
internal readonly record struct SearchMatchRange(int Start, int Length)
{
    public int End => Start + Length;
}

/// <summary>One matching item with its ranking tier and its position in the source (ties keep the source order).</summary>
internal readonly record struct SearchResultsHit(SearchResultItem Item, int Tier, int Order);

/// <summary>
/// Matching, ranking, highlighting and snippet windows for <see cref="SearchResults"/>. Queries split on white space
/// into terms; an item matches when every term occurs (ordinal, ignoring case) in its title, snippet, location or one
/// of its keywords. Ranking looks at the first term only: the title starts with it, then a word of the title starts
/// with it, then the title contains it, then everything else; ties keep the source order. Every method works on
/// spans and caller-owned lists, so a keystroke allocates only the terms and the result list's growth.
/// </summary>
internal static class SearchResultsMatcher
{
    /// <summary>Tier of an item whose title starts with the first term.</summary>
    public const int TierTitleStart = 0;

    /// <summary>Tier of an item where a word of the title starts with the first term.</summary>
    public const int TierWordStart = 1;

    /// <summary>Tier of an item whose title contains the first term inside a word.</summary>
    public const int TierTitleContains = 2;

    /// <summary>Tier of an item that matches only outside its title.</summary>
    public const int TierElsewhere = 3;

    private static readonly Comparison<SearchMatchRange> s_byStart = static (a, b) => a.Start != b.Start
        ? a.Start.CompareTo(b.Start)
        : b.Length.CompareTo(a.Length);

    private static readonly Comparison<SearchResultsHit> s_byRank = static (a, b) => a.Tier != b.Tier
        ? a.Tier.CompareTo(b.Tier)
        : a.Order.CompareTo(b.Order);

    /// <summary>Fills <paramref name="terms"/> with the distinct white-space separated words of <paramref name="query"/>.</summary>
    public static void Tokenize(string? query, List<string> terms)
    {
        terms.Clear();
        if (string.IsNullOrEmpty(query))
        {
            return;
        }
        var span = query.AsSpan();
        var i = 0;
        while (i < span.Length)
        {
            while (i < span.Length && char.IsWhiteSpace(span[i]))
            {
                i++;
            }
            var start = i;
            while (i < span.Length && !char.IsWhiteSpace(span[i]))
            {
                i++;
            }
            if (i > start)
            {
                var word = span.Slice(start, i - start);
                var known = false;
                foreach (var term in terms)
                {
                    if (word.Equals(term, StringComparison.OrdinalIgnoreCase))
                    {
                        known = true;
                        break;
                    }
                }
                if (!known)
                {
                    terms.Add(word.ToString());
                }
            }
        }
    }

    /// <summary>True when <paramref name="text"/> contains <paramref name="term"/> (ordinal, ignoring case).</summary>
    public static bool Contains(string? text, string term)
        => text is not null && text.AsSpan().IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>True when every term occurs in the title, snippet, location or a keyword (no terms match everything).</summary>
    public static bool Matches(string title, string? snippet, string? location, IList<string>? keywords, List<string> terms)
    {
        foreach (var term in terms)
        {
            if (Contains(title, term) || Contains(snippet, term) || Contains(location, term) || ContainsKeyword(keywords, term))
            {
                continue;
            }
            return false;
        }
        return true;
    }

    private static bool ContainsKeyword(IList<string>? keywords, string term)
    {
        if (keywords is null)
        {
            return false;
        }
        for (var i = 0; i < keywords.Count; i++)
        {
            if (Contains(keywords[i], term))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The ranking tier of <paramref name="title"/> for the query's first term (lower ranks first).</summary>
    public static int Tier(string title, string firstTerm)
    {
        var span = title.AsSpan();
        var at = span.IndexOf(firstTerm, StringComparison.OrdinalIgnoreCase);
        if (at < 0)
        {
            return TierElsewhere;
        }
        if (at == 0)
        {
            return TierTitleStart;
        }
        var offset = 0;
        while (at >= 0)
        {
            var index = offset + at;
            if (IsWordStart(span, index))
            {
                return TierWordStart;
            }
            offset = index + 1;
            if (offset >= span.Length)
            {
                break;
            }
            at = span.Slice(offset).IndexOf(firstTerm, StringComparison.OrdinalIgnoreCase);
        }
        return TierTitleContains;
    }

    private static bool IsWordStart(ReadOnlySpan<char> text, int index) => index == 0 || !char.IsLetterOrDigit(text[index - 1]);

    /// <summary>
    /// Fills <paramref name="hits"/> with the items that match <paramref name="terms"/>, ranked; with
    /// <paramref name="filter"/> false every item is kept in its source order (the server already searched).
    /// </summary>
    public static void Search(IReadOnlyList<SearchResultItem> items, List<string> terms, bool filter, List<SearchResultsHit> hits)
    {
        hits.Clear();
        for (var order = 0; order < items.Count; order++)
        {
            var item = items[order];
            if (!filter || terms.Count == 0)
            {
                hits.Add(new SearchResultsHit(item, TierTitleStart, order));
                continue;
            }
            if (Matches(item.Title, item.Snippet, item.Location, item.Keywords, terms))
            {
                hits.Add(new SearchResultsHit(item, Tier(item.Title, terms[0]), order));
            }
        }
        if (filter && terms.Count > 0 && hits.Count > 1)
        {
            hits.Sort(s_byRank);
        }
    }

    /// <summary>
    /// Fills <paramref name="ranges"/> with every occurrence of every term in <paramref name="text"/>, sorted, with
    /// overlapping and touching occurrences merged into one range.
    /// </summary>
    public static void FindRanges(ReadOnlySpan<char> text, List<string> terms, List<SearchMatchRange> ranges)
    {
        ranges.Clear();
        if (text.IsEmpty)
        {
            return;
        }
        foreach (var term in terms)
        {
            if (term.Length == 0)
            {
                continue;
            }
            var offset = 0;
            while (offset < text.Length)
            {
                var at = text.Slice(offset).IndexOf(term, StringComparison.OrdinalIgnoreCase);
                if (at < 0)
                {
                    break;
                }
                ranges.Add(new SearchMatchRange(offset + at, term.Length));
                offset += at + term.Length;
            }
        }
        Merge(ranges);
    }

    /// <summary>Sorts <paramref name="ranges"/> and merges the ones that overlap or touch.</summary>
    public static void Merge(List<SearchMatchRange> ranges)
    {
        if (ranges.Count < 2)
        {
            return;
        }
        ranges.Sort(s_byStart);
        var write = 0;
        for (var read = 1; read < ranges.Count; read++)
        {
            var last = ranges[write];
            var next = ranges[read];
            if (next.Start <= last.End)
            {
                ranges[write] = new SearchMatchRange(last.Start, Math.Max(last.End, next.End) - last.Start);
            }
            else
            {
                ranges[++write] = next;
            }
        }
        ranges.RemoveRange(write + 1, ranges.Count - write - 1);
    }

    /// <summary>
    /// The part of a long <paramref name="text"/> worth showing. When the first match fits in the first
    /// <paramref name="budget"/> characters (what the snippet's lines hold) the text starts at the beginning;
    /// otherwise it starts about <paramref name="lead"/> characters before the first match, at a word start. The
    /// result is at most <paramref name="maxLength"/> characters long (ending at a word end); cut ends get an
    /// ellipsis. <paramref name="ranges"/> receives the highlights of the returned text.
    /// </summary>
    public static string Window(string text, List<string> terms, int lead, int budget, int maxLength, List<SearchMatchRange> ranges)
    {
        maxLength = Math.Max(8, maxLength);
        lead = Math.Clamp(lead, 0, maxLength / 2);
        var span = text.AsSpan();
        var first = -1;
        var firstLength = 0;
        foreach (var term in terms)
        {
            if (term.Length == 0)
            {
                continue;
            }
            var at = span.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (at >= 0 && (first < 0 || at < first))
            {
                first = at;
                firstLength = term.Length;
            }
        }
        var start = 0;
        if (first >= 0 && first + firstLength > budget && first > lead)
        {
            start = first - lead;
            // Start at the next word so the window never opens mid-word.
            var space = span.Slice(start, first - start).IndexOfAny(' ', '\n', '\t');
            if (space >= 0)
            {
                start += space + 1;
            }
            while (start < first && char.IsWhiteSpace(span[start]))
            {
                start++;
            }
        }
        var end = Math.Min(span.Length, start + maxLength);
        // End after a whole word, unless that would cut the first match.
        var keep = Math.Max(first + firstLength, start + 1);
        if (end < span.Length && keep < end)
        {
            var space = span.Slice(keep, end - keep).LastIndexOfAny(' ', '\n', '\t');
            if (space >= 0)
            {
                end = keep + space;
            }
            while (end > start && char.IsWhiteSpace(span[end - 1]))
            {
                end--;
            }
        }
        if (start == 0 && end == span.Length)
        {
            FindRanges(span, terms, ranges);
            return text;
        }
        var body = span.Slice(start, end - start);
        var window = string.Concat(start > 0 ? "…" : string.Empty, body, end < span.Length ? "…" : string.Empty);
        FindRanges(window.AsSpan(), terms, ranges);
        return window;
    }
}
