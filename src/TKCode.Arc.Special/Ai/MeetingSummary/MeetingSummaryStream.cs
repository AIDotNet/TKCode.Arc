namespace TKCode.Arc;

/// <summary>
/// The pacing of the notes as they stream in (pure, no timer): the lines are revealed in order, a few whole words per
/// tick. The chunk is 3 to 8 words, sized when the stream starts so the whole notes take about 1.5 seconds at one tick
/// every 34ms. Targets can change while it streams (a streaming summarizer sends a longer snapshot): text that still
/// matches what is shown keeps streaming, text that no longer matches is cut back to the part both share.
/// </summary>
internal sealed class MeetingSummaryStream
{
    /// <summary>The time between two chunks.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(34);

    /// <summary>About how long the whole notes take.</summary>
    public static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(1500);

    /// <summary>The fewest words a chunk carries.</summary>
    public const int MinWords = 3;

    /// <summary>The most words a chunk carries.</summary>
    public const int MaxWords = 8;

    private readonly List<string> _targets = [];
    private readonly List<int> _shown = [];
    private int _chunk = MinWords;

    /// <summary>The number of lines.</summary>
    public int Count => _targets.Count;

    /// <summary>Words per tick for this stream.</summary>
    public int ChunkWords => _chunk;

    /// <summary>True when every line is fully shown.</summary>
    public bool IsDone
    {
        get
        {
            for (var i = 0; i < _targets.Count; i++)
            {
                if (_shown[i] < _targets[i].Length)
                {
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>How many characters of line <paramref name="index"/> are shown.</summary>
    public int Shown(int index) => _shown[index];

    /// <summary>The shown text of line <paramref name="index"/>.</summary>
    public string ShownText(int index) => _targets[index][.._shown[index]];

    /// <summary>True when line <paramref name="index"/> is fully shown.</summary>
    public bool IsLineDone(int index) => _shown[index] >= _targets[index].Length;

    /// <summary>Starts over with nothing shown; the chunk size follows the total word count.</summary>
    public void Start(IReadOnlyList<string> targets)
    {
        _targets.Clear();
        _shown.Clear();
        var words = 0;
        foreach (var target in targets)
        {
            _targets.Add(target);
            _shown.Add(0);
            words += CountWords(target);
        }
        var ticks = Math.Max(1, (int)(Budget.TotalMilliseconds / Interval.TotalMilliseconds));
        _chunk = Math.Clamp((int)Math.Ceiling(words / (double)ticks), MinWords, MaxWords);
    }

    /// <summary>Shows every line in full.</summary>
    public void Finish()
    {
        for (var i = 0; i < _targets.Count; i++)
        {
            _shown[i] = _targets[i].Length;
        }
    }

    /// <summary>
    /// Updates the targets while streaming. A line whose new text still starts with what is shown keeps it; otherwise it
    /// is cut back to the shared prefix. New lines start empty; lines that are gone are dropped.
    /// </summary>
    public void Retarget(IReadOnlyList<string> targets)
    {
        for (var i = 0; i < targets.Count; i++)
        {
            if (i == _targets.Count)
            {
                _targets.Add(targets[i]);
                _shown.Add(0);
                continue;
            }
            var shown = _shown[i];
            var next = targets[i];
            if (shown > next.Length || string.CompareOrdinal(_targets[i], 0, next, 0, shown) != 0)
            {
                shown = SharedPrefix(_targets[i].AsSpan(0, Math.Min(shown, _targets[i].Length)), next);
            }
            _targets[i] = next;
            _shown[i] = shown;
        }
        if (_targets.Count > targets.Count)
        {
            _targets.RemoveRange(targets.Count, _targets.Count - targets.Count);
            _shown.RemoveRange(targets.Count, _shown.Count - targets.Count);
        }
    }

    /// <summary>
    /// One tick: reveals the next <see cref="ChunkWords"/> whole words, in line order (a chunk can finish one line and
    /// start the next). Returns what was added to which line.
    /// </summary>
    public List<(int Line, string Text)> Tick()
    {
        var added = new List<(int Line, string Text)>();
        var words = _chunk;
        for (var i = 0; i < _targets.Count && words > 0; i++)
        {
            var target = _targets[i];
            var start = _shown[i];
            if (start >= target.Length)
            {
                continue;
            }
            var end = start;
            while (words > 0 && end < target.Length)
            {
                end = NextWordEnd(target, end);
                words--;
            }
            _shown[i] = end;
            added.Add((i, target[start..end]));
        }
        return added;
    }

    /// <summary>The words in <paramref name="text"/> (runs of non-space).</summary>
    public static int CountWords(string text)
    {
        var count = 0;
        var inWord = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                count++;
            }
        }
        return count;
    }

    /// <summary>The end of the word starting at or after <paramref name="from"/>, with the spaces after it.</summary>
    private static int NextWordEnd(string text, int from)
    {
        var i = from;
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }
        while (i < text.Length && !char.IsWhiteSpace(text[i]))
        {
            i++;
        }
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }
        return i;
    }

    private static int SharedPrefix(ReadOnlySpan<char> a, string b)
    {
        var n = Math.Min(a.Length, b.Length);
        var i = 0;
        while (i < n && a[i] == b[i])
        {
            i++;
        }
        return i;
    }
}
