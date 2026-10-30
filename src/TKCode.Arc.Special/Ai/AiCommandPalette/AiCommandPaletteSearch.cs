namespace TKCode.Arc;

/// <summary>
/// The palette's search: a synchronous word match over the items. Every word of the query must start a word of the label,
/// the group, the meta text or the keywords (case and accents of ASCII letters aside). Rows keep their order inside a
/// group; a group appears where its first match is, and a label that starts with the query goes before the rest of its group.
/// An empty query lists the suggested items (all of them when none is marked).
/// </summary>
internal static class AiCommandPaletteSearch
{
    private static readonly char[] s_separators = [' ', '\t', ',', '·', '/', '-', '_', '.', ':', '(', ')', '#', '“', '”', '"', '\''];

    /// <summary>The matches, grouped: each group's name (null for rows without one) and its rows.</summary>
    public static List<(string? Group, List<AiCommandPaletteItem> Items)> Filter(IReadOnlyList<AiCommandPaletteItem> items, string? query)
    {
        var words = Words(query);
        var matches = new List<(AiCommandPaletteItem Item, bool Leading)>();
        if (words.Length == 0)
        {
            var anySuggested = false;
            foreach (var item in items)
            {
                anySuggested |= item.IsSuggested;
            }
            foreach (var item in items)
            {
                if (!anySuggested || item.IsSuggested)
                {
                    matches.Add((item, false));
                }
            }
        }
        else
        {
            var whole = (query ?? string.Empty).Trim();
            foreach (var item in items)
            {
                if (Matches(item, words))
                {
                    matches.Add((item, item.Label.StartsWith(whole, StringComparison.OrdinalIgnoreCase)));
                }
            }
        }
        var groups = new List<(string? Group, List<AiCommandPaletteItem> Items)>();
        foreach (var (item, _) in matches)
        {
            var index = groups.FindIndex(g => string.Equals(g.Group, item.Group, StringComparison.Ordinal));
            if (index < 0)
            {
                groups.Add((item.Group, []));
                index = groups.Count - 1;
            }
            groups[index].Items.Add(item);
        }
        // Inside a group, rows whose label starts with the query go first (stable).
        foreach (var (_, list) in groups)
        {
            var leading = list.Where(i => matches.First(m => ReferenceEquals(m.Item, i)).Leading).ToList();
            if (leading.Count > 0 && leading.Count < list.Count)
            {
                var rest = list.Where(i => !leading.Contains(i)).ToList();
                list.Clear();
                list.AddRange(leading);
                list.AddRange(rest);
            }
        }
        return groups;
    }

    /// <summary>True when every query word starts a word of the item's label, group, meta or keywords.</summary>
    public static bool Matches(AiCommandPaletteItem item, string[] queryWords)
    {
        var haystack = Words(item.Label + " " + item.Group + " " + item.Meta + " " + item.Keywords);
        foreach (var word in queryWords)
        {
            var found = false;
            foreach (var candidate in haystack)
            {
                if (candidate.StartsWith(word, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>The lower-case words of a text.</summary>
    public static string[] Words(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.ToLowerInvariant().Split(s_separators, StringSplitOptions.RemoveEmptyEntries);
}

/// <summary>One piece of streamed answer text: a word (with the citations stuck to it), or a paragraph break.</summary>
internal readonly record struct AiCommandPaletteWord(string Text, bool IsBreak);

/// <summary>
/// Splits streamed text into whole words. Text arrives in pieces of any size; a word is only released once the whitespace
/// after it has arrived (or the stream has ended), so a half-streamed word never shows. A line break between two words
/// starts a new paragraph.
/// </summary>
internal sealed class AiCommandPaletteWordQueue
{
    private readonly System.Text.StringBuilder _pending = new();
    private readonly Queue<AiCommandPaletteWord> _words = new();
    private bool _complete;
    private bool _emitted;

    /// <summary>Words (and breaks) ready to show.</summary>
    public int Count => _words.Count;

    /// <summary>True once <see cref="Complete"/> was called and every word has been taken.</summary>
    public bool IsDrained => _complete && _words.Count == 0;

    /// <summary>True once the stream has ended.</summary>
    public bool IsComplete => _complete;

    public void Push(string text)
    {
        if (_complete || string.IsNullOrEmpty(text))
        {
            return;
        }
        _pending.Append(text);
        Scan(final: false);
    }

    public void Complete()
    {
        if (_complete)
        {
            return;
        }
        Scan(final: true);
        _complete = true;
    }

    /// <summary>Takes up to <paramref name="count"/> words; a paragraph break comes along with the word after it.</summary>
    public List<AiCommandPaletteWord> Take(int count)
    {
        var taken = new List<AiCommandPaletteWord>(Math.Min(count + 1, _words.Count));
        var words = 0;
        while (words < count && _words.Count > 0)
        {
            var next = _words.Dequeue();
            taken.Add(next);
            if (!next.IsBreak)
            {
                words++;
            }
        }
        return taken;
    }

    private void Scan(bool final)
    {
        var text = _pending.ToString();
        var i = 0;
        var consumed = 0;
        while (true)
        {
            var spaceStart = i;
            var newline = false;
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                newline |= text[i] == '\n';
                i++;
            }
            if (i == text.Length)
            {
                // A run of whitespace at the end may still grow into a paragraph break.
                consumed = final ? text.Length : spaceStart;
                break;
            }
            if (newline && _emitted)
            {
                _words.Enqueue(new AiCommandPaletteWord(string.Empty, true));
            }
            var start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
            {
                i++;
            }
            if (i == text.Length && !final)
            {
                // The word may go on in the next piece.
                consumed = start;
                break;
            }
            _words.Enqueue(new AiCommandPaletteWord(text[start..i], false));
            _emitted = true;
            consumed = i;
        }
        _pending.Remove(0, consumed);
    }
}

/// <summary>Finds the [n] citations in a word: the pieces of text and the 1-based source numbers between them.</summary>
internal static class AiCommandPaletteCitations
{
    /// <summary>The parts of <paramref name="word"/>: text (Number 0) or a citation (Number ≥ 1) of an existing source.</summary>
    public static List<(string Text, int Number)> Split(string word, int sourceCount)
    {
        var parts = new List<(string, int)>();
        var i = 0;
        var textStart = 0;
        while (i < word.Length)
        {
            if (word[i] == '[')
            {
                var close = word.IndexOf(']', i + 1);
                if (close > i + 1 && int.TryParse(word.AsSpan(i + 1, close - i - 1), out var number) && number >= 1 && number <= sourceCount)
                {
                    if (i > textStart)
                    {
                        parts.Add((word[textStart..i], 0));
                    }
                    parts.Add((word[i..(close + 1)], number));
                    i = close + 1;
                    textStart = i;
                    continue;
                }
            }
            i++;
        }
        if (textStart < word.Length)
        {
            parts.Add((word[textStart..], 0));
        }
        return parts;
    }

    /// <summary>The text without its citations, for the announcement and the copy.</summary>
    public static string Plain(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == '[')
            {
                var close = text.IndexOf(']', i + 1);
                if (close > i + 1 && int.TryParse(text.AsSpan(i + 1, close - i - 1), out _))
                {
                    while (sb.Length > 0 && sb[^1] == ' ')
                    {
                        sb.Length--;
                    }
                    i = close + 1;
                    continue;
                }
            }
            sb.Append(text[i]);
            i++;
        }
        return sb.ToString();
    }
}
