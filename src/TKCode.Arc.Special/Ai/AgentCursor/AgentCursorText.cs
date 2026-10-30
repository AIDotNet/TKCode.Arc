namespace TKCode.Arc;

/// <summary>
/// The pure text logic of <see cref="AgentCursor"/>: finding the word a fix replaces, cutting a rewrite into the 3 to 6
/// word chunks it streams in, applying edits to a document, comparing documents by content and the names on cursors.
/// </summary>
internal static class AgentCursorText
{
    /// <summary>How many words each streamed chunk holds, in turn (Arc streams 3 to 6 words a tick).</summary>
    private static readonly int[] s_chunkWords = [4, 3, 5, 6, 3, 5, 4];

    /// <summary>The first whole-word match of <paramref name="word"/> in <paramref name="text"/>, else the first match, else −1.</summary>
    public static int FindWord(string text, string? word)
    {
        if (string.IsNullOrEmpty(word) || string.IsNullOrEmpty(text))
        {
            return -1;
        }
        var at = 0;
        while ((at = text.IndexOf(word, at, StringComparison.Ordinal)) >= 0)
        {
            var before = at == 0 || !char.IsLetterOrDigit(text[at - 1]);
            var end = at + word.Length;
            var after = end >= text.Length || !char.IsLetterOrDigit(text[end]);
            if (before && after)
            {
                return at;
            }
            at++;
        }
        return text.IndexOf(word, StringComparison.Ordinal);
    }

    /// <summary>
    /// Where each streamed chunk of <paramref name="text"/> ends: 3 to 6 whole words at a time, each chunk keeping the spaces
    /// after it. The last end is the text's length.
    /// </summary>
    public static List<int> ChunkEnds(string text)
    {
        var ends = new List<int>();
        var length = text.Length;
        var words = 0;
        var turn = 0;
        var i = 0;
        while (i < length)
        {
            while (i < length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }
            if (i >= length)
            {
                break;
            }
            while (i < length && !char.IsWhiteSpace(text[i]))
            {
                i++;
            }
            words++;
            if (words == s_chunkWords[turn])
            {
                while (i < length && char.IsWhiteSpace(text[i]))
                {
                    i++;
                }
                ends.Add(i);
                words = 0;
                turn = (turn + 1) % s_chunkWords.Length;
            }
        }
        if (ends.Count == 0 || ends[^1] < length)
        {
            ends.Add(length);
        }
        return ends;
    }

    /// <summary>
    /// The document after <paramref name="edits"/>, as new block objects. Edits that address a missing block or word are
    /// skipped, as the card skips them.
    /// </summary>
    public static List<AgentCursorBlock> Apply(IEnumerable<AgentCursorBlock> blocks, IEnumerable<AgentCursorEdit> edits)
    {
        var result = Copy(blocks);
        var inserted = 0;
        foreach (var edit in edits)
        {
            var index = result.FindIndex(b => b.Id == edit.BlockId);
            switch (edit.Kind)
            {
                case AgentCursorEditKind.Rewrite when index >= 0 && edit.Text is not null:
                    result[index].Text = edit.Text;
                    break;
                case AgentCursorEditKind.Insert when edit.Text is not null:
                    inserted++;
                    var block = new AgentCursorBlock(NewId(edit, inserted), edit.InsertKind, edit.Text);
                    result.Insert(index >= 0 ? index + 1 : result.Count, block);
                    break;
                case AgentCursorEditKind.Fix when index >= 0:
                    var text = result[index].Text;
                    var at = FindWord(text, edit.Word);
                    if (at >= 0)
                    {
                        result[index].Text = text[..at] + (edit.Replacement ?? string.Empty) + text[(at + edit.Word!.Length)..];
                    }
                    break;
            }
        }
        return result;
    }

    /// <summary>The id of an inserted block: its own, or one made up from its position in the run.</summary>
    public static string NewId(AgentCursorEdit edit, int ordinal) =>
        string.IsNullOrEmpty(edit.NewBlockId) ? "inserted-" + ordinal : edit.NewBlockId;

    /// <summary>New block objects with the same ids, kinds and texts.</summary>
    public static List<AgentCursorBlock> Copy(IEnumerable<AgentCursorBlock> blocks) =>
        blocks.Select(b => new AgentCursorBlock(b.Id, b.Kind, b.Text)).ToList();

    /// <summary>Whether two documents hold the same blocks (ids, kinds and texts in order); null equals only null.</summary>
    public static bool SameContent(IReadOnlyList<AgentCursorBlock>? a, IReadOnlyList<AgentCursorBlock>? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }
        if (a.Count != b.Count)
        {
            return false;
        }
        for (var i = 0; i < a.Count; i++)
        {
            if (a[i].Id != b[i].Id || a[i].Kind != b[i].Kind || a[i].Text != b[i].Text)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>The first word of a name ("Maya Ortiz" → "Maya").</summary>
    public static string FirstName(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        var space = trimmed.IndexOf(' ');
        return space > 0 ? trimmed[..space] : trimmed;
    }

    /// <summary>Up to two initials ("Maya Ortiz" → "MO").</summary>
    public static string Initials(string? name)
    {
        var parts = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => string.Empty,
            1 => char.ToUpperInvariant(parts[0][0]).ToString(),
            _ => string.Concat(char.ToUpperInvariant(parts[0][0]), char.ToUpperInvariant(parts[1][0]))
        };
    }

    /// <summary>How long a cursor flight lasts: 280ms for a short hop up to 600ms across the card, by distance.</summary>
    public static double FlightSeconds(double distance)
    {
        var t = Math.Clamp((distance - 40) / 560, 0, 1);
        return 0.28 + 0.32 * t;
    }
}
