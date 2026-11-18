namespace TKCode.Arc;

/// <summary>A run of consecutive lines that a step changed: the band behind them and its accent hairline.</summary>
/// <param name="Line">The first line.</param>
/// <param name="Count">The number of lines.</param>
internal readonly record struct CodeMagicMoveBand(int Line, int Count);

/// <summary>
/// How the words of one version of a snippet become the words of the next. Words are matched along the longest common
/// subsequence of their texts; words left over on both sides that have the same text are then matched by distance, the
/// nearest first, so a line that moved as a whole glides instead of being typed again. A new word with no match is
/// typed in; an old word with no match lifts away.
/// </summary>
internal static class CodeMagicMoveDiff
{
    /// <summary>Above this many cells the subsequence table is skipped and words are matched by distance only.</summary>
    private const int MaxCells = 1 << 20;

    /// <summary>
    /// For each word of <paramref name="to"/>, the index of the word of <paramref name="from"/> it continues, or -1 for a
    /// new word.
    /// </summary>
    public static int[] Match(CodeMagicMoveSource from, CodeMagicMoveSource to)
    {
        var a = from.Words;
        var b = to.Words;
        var match = new int[b.Length];
        Array.Fill(match, -1);
        var used = new bool[a.Length];
        if ((long)a.Length * b.Length <= MaxCells)
        {
            MatchSubsequence(a, b, match, used);
        }
        MatchByDistance(a, b, match, used);
        return match;
    }

    private static void MatchSubsequence(CodeMagicMoveWord[] a, CodeMagicMoveWord[] b, int[] match, bool[] used)
    {
        var n = a.Length;
        var m = b.Length;
        // lengths[i, j] = LCS of a[i..] and b[j..], stored row by row.
        var lengths = new int[(n + 1) * (m + 1)];
        var stride = m + 1;
        for (var i = n - 1; i >= 0; i--)
        {
            for (var j = m - 1; j >= 0; j--)
            {
                lengths[i * stride + j] = string.Equals(a[i].Text, b[j].Text, StringComparison.Ordinal)
                    ? lengths[(i + 1) * stride + j + 1] + 1
                    : Math.Max(lengths[(i + 1) * stride + j], lengths[i * stride + j + 1]);
            }
        }
        var x = 0;
        var y = 0;
        while (x < n && y < m)
        {
            if (string.Equals(a[x].Text, b[y].Text, StringComparison.Ordinal))
            {
                match[y] = x;
                used[x] = true;
                x++;
                y++;
            }
            else if (lengths[(x + 1) * stride + y] >= lengths[x * stride + y + 1])
            {
                x++;
            }
            else
            {
                y++;
            }
        }
    }

    private static void MatchByDistance(CodeMagicMoveWord[] a, CodeMagicMoveWord[] b, int[] match, bool[] used)
    {
        List<(double Distance, int From, int To)>? candidates = null;
        for (var j = 0; j < b.Length; j++)
        {
            if (match[j] >= 0)
            {
                continue;
            }
            for (var i = 0; i < a.Length; i++)
            {
                if (used[i] || !string.Equals(a[i].Text, b[j].Text, StringComparison.Ordinal))
                {
                    continue;
                }
                // Distance in em: a line is 1.7em, a column about 0.6em.
                var dy = (a[i].Line - b[j].Line) * 1.7;
                var dx = (a[i].Column - b[j].Column) * 0.6;
                (candidates ??= []).Add((dx * dx + dy * dy, i, j));
            }
        }
        if (candidates is null)
        {
            return;
        }
        candidates.Sort(static (p, q) =>
        {
            var c = p.Distance.CompareTo(q.Distance);
            return c != 0 ? c : p.To != q.To ? p.To.CompareTo(q.To) : p.From.CompareTo(q.From);
        });
        foreach (var (_, from, to) in candidates)
        {
            if (!used[from] && match[to] < 0)
            {
                used[from] = true;
                match[to] = from;
            }
        }
    }

    /// <summary>The old words that no new word continues (they lift away).</summary>
    public static bool[] Removed(CodeMagicMoveSource from, int[] match)
    {
        var removed = new bool[from.Words.Length];
        Array.Fill(removed, true);
        foreach (var index in match)
        {
            if (index >= 0)
            {
                removed[index] = false;
            }
        }
        return removed;
    }

    /// <summary>
    /// The runs of lines of <paramref name="to"/> that hold at least one new word (consecutive lines join into one band).
    /// </summary>
    public static CodeMagicMoveBand[] Bands(CodeMagicMoveSource to, int[] match)
    {
        var changed = new bool[to.LineCount];
        for (var j = 0; j < match.Length; j++)
        {
            if (match[j] < 0)
            {
                changed[to.Words[j].Line] = true;
            }
        }
        var bands = new List<CodeMagicMoveBand>();
        var start = -1;
        for (var line = 0; line <= changed.Length; line++)
        {
            var on = line < changed.Length && changed[line];
            if (on && start < 0)
            {
                start = line;
            }
            else if (!on && start >= 0)
            {
                bands.Add(new CodeMagicMoveBand(start, line - start));
                start = -1;
            }
        }
        return bands.ToArray();
    }

    /// <summary>
    /// Lines added and removed between two versions, as a line diff would count them (blank lines are not counted).
    /// </summary>
    public static (int Added, int Removed) LineCounts(CodeMagicMoveSource from, CodeMagicMoveSource to)
    {
        var a = NonBlank(from.Lines);
        var b = NonBlank(to.Lines);
        var n = a.Count;
        var m = b.Count;
        var lengths = new int[(n + 1) * (m + 1)];
        var stride = m + 1;
        for (var i = n - 1; i >= 0; i--)
        {
            for (var j = m - 1; j >= 0; j--)
            {
                lengths[i * stride + j] = string.Equals(a[i], b[j], StringComparison.Ordinal)
                    ? lengths[(i + 1) * stride + j + 1] + 1
                    : Math.Max(lengths[(i + 1) * stride + j], lengths[i * stride + j + 1]);
            }
        }
        var common = n == 0 || m == 0 ? 0 : lengths[0];
        return (m - common, n - common);
    }

    private static List<string> NonBlank(string[] lines)
    {
        var result = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd();
            if (trimmed.Length > 0)
            {
                result.Add(trimmed);
            }
        }
        return result;
    }
}
