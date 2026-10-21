namespace TKCode.Arc;

/// <summary>
/// How <see cref="SettingsCommand"/> searches: case-insensitive words that must each appear in the header,
/// description, group or keywords, ranked by how well the header matches.
/// </summary>
internal static class SettingsCommandMatcher
{
    /// <summary>Header starts with the query.</summary>
    public const int RankHeaderPrefix = 0;

    /// <summary>Every query word starts a word of the header.</summary>
    public const int RankHeaderWord = 1;

    /// <summary>The header contains the query (or every query word).</summary>
    public const int RankHeaderContains = 2;

    /// <summary>Only the description, group or keywords match.</summary>
    public const int RankOther = 3;

    private static readonly char[] s_wordBreaks = [' ', '\t', '-', '_', '/', '(', ')', ',', '.', ':', ' '];

    /// <summary>The words of a query (split on white space).</summary>
    public static string[] Tokenize(string? query)
        => string.IsNullOrWhiteSpace(query) ? [] : query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Whether every word appears in one of the searchable fields.</summary>
    public static bool Matches(SettingsCommandItem item, string[] tokens)
    {
        if (tokens.Length == 0)
        {
            return true;
        }
        var header = item.Header;
        var description = item.Description;
        var group = item.Group;
        var keywords = item.Keywords;
        foreach (var token in tokens)
        {
            if (!Contains(header, token) && !Contains(description, token) && !Contains(group, token) && !Contains(keywords, token))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>The rank of a matching item (lower is better); <paramref name="query"/> is the trimmed query.</summary>
    public static int Rank(SettingsCommandItem item, string query, string[] tokens)
    {
        var header = item.Header ?? string.Empty;
        if (query.Length == 0)
        {
            return RankHeaderPrefix;
        }
        if (header.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return RankHeaderPrefix;
        }
        var allWordStarts = true;
        var allContained = true;
        foreach (var token in tokens)
        {
            if (!Contains(header, token))
            {
                allContained = false;
                allWordStarts = false;
                break;
            }
            if (!StartsAWord(header, token))
            {
                allWordStarts = false;
            }
        }
        if (allWordStarts)
        {
            return RankHeaderWord;
        }
        return allContained || Contains(header, query) ? RankHeaderContains : RankOther;
    }

    /// <summary>
    /// Marks the characters of <paramref name="header"/> that match a query word into <paramref name="mask"/>
    /// (resized as needed). Returns whether anything matched.
    /// </summary>
    public static bool MarkMatches(string header, string[] tokens, ref bool[]? mask)
    {
        if (mask is null || mask.Length < header.Length)
        {
            mask = new bool[Math.Max(header.Length, 16)];
        }
        Array.Clear(mask, 0, header.Length);
        var any = false;
        foreach (var token in tokens)
        {
            var start = 0;
            while (start <= header.Length - token.Length)
            {
                var at = header.IndexOf(token, start, StringComparison.OrdinalIgnoreCase);
                if (at < 0)
                {
                    break;
                }
                for (var i = at; i < at + token.Length; i++)
                {
                    mask[i] = true;
                }
                any = true;
                start = at + Math.Max(1, token.Length);
            }
        }
        return any;
    }

    private static bool Contains(string? text, string token)
        => text is not null && text.Contains(token, StringComparison.OrdinalIgnoreCase);

    private static bool StartsAWord(string text, string token)
    {
        var start = 0;
        while (start <= text.Length - token.Length)
        {
            var at = text.IndexOf(token, start, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                return false;
            }
            if (at == 0 || Array.IndexOf(s_wordBreaks, text[at - 1]) >= 0)
            {
                return true;
            }
            start = at + 1;
        }
        return false;
    }
}
