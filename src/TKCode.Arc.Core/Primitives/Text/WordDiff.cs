namespace TKCode.Arc.Primitives;

/// <summary>What a <see cref="WordDiffSegment"/> does to the text.</summary>
public enum WordDiffKind
{
    /// <summary>Text in both versions.</summary>
    Equal,

    /// <summary>Text only in the new version.</summary>
    Insert,

    /// <summary>Text only in the old version.</summary>
    Delete
}

/// <summary>One run of a <see cref="WordDiffResult"/>.</summary>
/// <param name="Kind">Whether the text is kept, inserted or deleted.</param>
/// <param name="Text">The text of the run, never empty.</param>
public readonly record struct WordDiffSegment(WordDiffKind Kind, string Text);

/// <summary>
/// The result of <see cref="WordDiff.Compute"/>: segments in reading order. Equal and Delete segments joined give the
/// old text exactly, Equal and Insert segments joined give the new text exactly. Every changed region is one Delete
/// followed by one Insert (either may be missing), so segments of the same kind are never adjacent.
/// </summary>
public sealed class WordDiffResult
{
    internal WordDiffResult(IReadOnlyList<WordDiffSegment> segments, int insertedWords, int deletedWords)
    {
        Segments = segments;
        InsertedWords = insertedWords;
        DeletedWords = deletedWords;
        HasChanges = segments.Any(segment => segment.Kind != WordDiffKind.Equal);
    }

    /// <summary>The segments in reading order (empty when both texts are empty).</summary>
    public IReadOnlyList<WordDiffSegment> Segments { get; }

    /// <summary>Inserted words: non-whitespace tokens, an atom counting as one.</summary>
    public int InsertedWords { get; }

    /// <summary>Deleted words: non-whitespace tokens, an atom counting as one.</summary>
    public int DeletedWords { get; }

    /// <summary>True when any segment is an Insert or a Delete (a whitespace-only change counts).</summary>
    public bool HasChanges { get; }
}

/// <summary>Options for <see cref="WordDiff"/>.</summary>
public sealed record WordDiffOptions
{
    /// <summary>No atoms, the default cell budget.</summary>
    public static WordDiffOptions Default { get; } = new();

    /// <summary>
    /// Markers of spans that compare as one word even when they contain spaces, such as template variables
    /// <c>("{{", "}}")</c>: from an <c>Open</c> marker to the next <c>Close</c> marker inclusive. An atom is always its
    /// own token (text glued to it on either side is a separate token), and an <c>Open</c> marker without a later
    /// <c>Close</c> is ordinary text. Pairs are tried in list order; markers must not be empty.
    /// </summary>
    public IReadOnlyList<(string Open, string Close)> Atoms { get; init; } = [];

    /// <summary>
    /// The largest LCS table (old tokens × new tokens left after trimming the common prefix and suffix). A larger
    /// middle is reported as one Delete followed by one Insert instead.
    /// </summary>
    public int MaxCells { get; init; } = 4_000_000;
}

/// <summary>
/// Word-level diff for reviewing AI edits and comparing prompt versions: whitespace is kept exactly, punctuation stays
/// with its word, atoms such as <c>{{ name }}</c> are never split, and each CJK ideograph, kana or hangul element is a
/// word. The common prefix and suffix are trimmed and the middle is aligned by longest common subsequence; the result
/// is cleaned up so each changed region reads as one deleted run followed by one inserted run.
/// </summary>
public static class WordDiff
{
    /// <summary>Compares <paramref name="before"/> with <paramref name="after"/>.</summary>
    /// <param name="before">The old text.</param>
    /// <param name="after">The new text.</param>
    /// <param name="options">Atoms and the cell budget; defaults to <see cref="WordDiffOptions.Default"/>.</param>
    public static WordDiffResult Compute(string before, string after, WordDiffOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        options ??= WordDiffOptions.Default;
        var a = new List<Token>();
        var b = new List<Token>();
        Split(before, options.Atoms, a);
        Split(after, options.Atoms, b);

        var builder = new Builder();
        var prefix = 0;
        while (prefix < a.Count && prefix < b.Count && SameToken(before, a[prefix], after, b[prefix]))
        {
            prefix++;
        }
        var suffix = 0;
        while (suffix < a.Count - prefix && suffix < b.Count - prefix &&
               SameToken(before, a[a.Count - 1 - suffix], after, b[b.Count - 1 - suffix]))
        {
            suffix++;
        }
        builder.Equal(a, 0, prefix);
        AlignMiddle(before, a, prefix, a.Count - suffix, after, b, prefix, b.Count - suffix, options.MaxCells, builder);
        builder.Equal(a, a.Count - suffix, a.Count);
        return builder.Build(before, after);
    }

    /// <summary>
    /// Splits <paramref name="text"/> into the tokens <see cref="Compute"/> compares: maximal whitespace runs, maximal
    /// runs of other characters (punctuation stays with its word), atoms, and single CJK elements. The tokens joined
    /// give <paramref name="text"/> back exactly.
    /// </summary>
    /// <param name="text">The text to split.</param>
    /// <param name="options">The atoms to keep whole; defaults to <see cref="WordDiffOptions.Default"/>.</param>
    public static IReadOnlyList<string> Tokenize(string text, WordDiffOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var tokens = new List<Token>();
        Split(text, (options ?? WordDiffOptions.Default).Atoms, tokens);
        var result = new string[tokens.Count];
        for (var i = 0; i < tokens.Count; i++)
        {
            result[i] = text.Substring(tokens[i].Start, tokens[i].Length);
        }
        return result;
    }

    /// <summary>A token of the compared text; <see cref="IsWord"/> is false for whitespace runs.</summary>
    private readonly record struct Token(int Start, int Length, bool IsWord);

    private static void Split(string text, IReadOnlyList<(string Open, string Close)> atoms, List<Token> tokens)
    {
        foreach (var (open, close) in atoms)
        {
            if (string.IsNullOrEmpty(open) || string.IsNullOrEmpty(close))
            {
                throw new ArgumentException("Atom markers must not be empty.", nameof(atoms));
            }
        }
        // Where the next Close of each pair is, cached so text full of unclosed Open markers stays linear.
        var closes = new int[atoms.Count];
        Array.Fill(closes, int.MinValue);
        var span = text.AsSpan();
        var runStart = -1;
        var runIsWord = false;
        var i = 0;
        while (i < text.Length)
        {
            if (atoms.Count > 0 && MatchAtom(text, i, atoms, closes) is var atomEnd and > 0)
            {
                Flush(i);
                tokens.Add(new Token(i, atomEnd - i, true));
                i = atomEnd;
                continue;
            }
            var length = WordText.ElementLength(span[i..]);
            var kind = WordText.Classify(span.Slice(i, length));
            if (kind == WordElementKind.Cjk)
            {
                Flush(i);
                tokens.Add(new Token(i, length, true));
            }
            else
            {
                var isWord = kind == WordElementKind.Word;
                if (runStart >= 0 && runIsWord != isWord)
                {
                    Flush(i);
                }
                if (runStart < 0)
                {
                    runStart = i;
                    runIsWord = isWord;
                }
            }
            i += length;
        }
        Flush(text.Length);

        void Flush(int end)
        {
            if (runStart >= 0)
            {
                tokens.Add(new Token(runStart, end - runStart, runIsWord));
                runStart = -1;
            }
        }
    }

    /// <summary>End of the atom that starts at <paramref name="index"/>, or 0 when none does.</summary>
    private static int MatchAtom(string text, int index, IReadOnlyList<(string Open, string Close)> atoms, int[] closes)
    {
        for (var k = 0; k < atoms.Count; k++)
        {
            var (open, close) = atoms[k];
            if (string.CompareOrdinal(text, index, open, 0, open.Length) != 0)
            {
                continue;
            }
            var from = index + open.Length;
            // A cached hit at or after `from` is still the first Close there; a cached miss stays a miss.
            if (closes[k] != -1 && closes[k] < from)
            {
                closes[k] = text.IndexOf(close, from, StringComparison.Ordinal);
            }
            if (closes[k] >= 0)
            {
                return closes[k] + close.Length;
            }
        }
        return 0;
    }

    private static bool SameToken(string a, Token x, string b, Token y)
        => x.Length == y.Length && a.AsSpan(x.Start, x.Length).SequenceEqual(b.AsSpan(y.Start, y.Length));

    /// <summary>Aligns tokens a[aFrom..aTo) with b[bFrom..bTo) by longest common subsequence.</summary>
    private static void AlignMiddle(string before, List<Token> a, int aFrom, int aTo, string after, List<Token> b,
                                    int bFrom, int bTo, int maxCells, Builder builder)
    {
        var n = aTo - aFrom;
        var m = bTo - bFrom;
        if (n == 0 || m == 0 || (long)n * m > maxCells || (long)(n + 1) * (m + 1) > Array.MaxLength)
        {
            builder.Delete(a, aFrom, aTo);
            builder.Insert(b, bFrom, bTo);
            return;
        }
        // Tokens become ids so the table compares ints.
        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        var x = new int[n];
        var y = new int[m];
        for (var i = 0; i < n; i++)
        {
            x[i] = Id(before, a[aFrom + i]);
        }
        for (var j = 0; j < m; j++)
        {
            y[j] = Id(after, b[bFrom + j]);
        }
        // lcs[i, j] = LCS length of x[i..] and y[j..]; the last row and column stay 0.
        var width = m + 1;
        var lcs = new int[(n + 1) * width];
        for (var i = n - 1; i >= 0; i--)
        {
            var row = i * width;
            var next = row + width;
            for (var j = m - 1; j >= 0; j--)
            {
                lcs[row + j] = x[i] == y[j] ? lcs[next + j + 1] + 1 : Math.Max(lcs[next + j], lcs[row + j + 1]);
            }
        }
        var p = 0;
        var q = 0;
        while (p < n && q < m)
        {
            if (x[p] == y[q])
            {
                builder.Equal(a, aFrom + p, aFrom + p + 1);
                p++;
                q++;
            }
            else if (lcs[(p + 1) * width + q] >= lcs[p * width + q + 1])
            {
                builder.Delete(a, aFrom + p, aFrom + p + 1);
                p++;
            }
            else
            {
                builder.Insert(b, bFrom + q, bFrom + q + 1);
                q++;
            }
        }
        builder.Delete(a, aFrom + p, aTo);
        builder.Insert(b, bFrom + q, bTo);

        int Id(string text, Token token)
        {
            var key = text.Substring(token.Start, token.Length);
            if (!ids.TryGetValue(key, out var id))
            {
                id = ids.Count;
                ids.Add(key, id);
            }
            return id;
        }
    }

    /// <summary>A run of same-kind tokens: chars [AStart, AEnd) of the old text and [BStart, BEnd) of the new text.</summary>
    private struct Run
    {
        public WordDiffKind Kind;
        public int AStart;
        public int AEnd;
        public int BStart;
        public int BEnd;
        public bool IsSpace;
    }

    /// <summary>Collects the alignment as runs, counts words, and cleans the runs up into segments.</summary>
    private sealed class Builder
    {
        private readonly List<Run> _runs = [];
        private int _a;
        private int _b;
        private int _inserted;
        private int _deleted;

        public void Equal(List<Token> tokens, int from, int to)
        {
            for (var i = from; i < to; i++)
            {
                Add(WordDiffKind.Equal, tokens[i], tokens[i].Length, tokens[i].Length);
            }
        }

        public void Delete(List<Token> tokens, int from, int to)
        {
            for (var i = from; i < to; i++)
            {
                _deleted += tokens[i].IsWord ? 1 : 0;
                Add(WordDiffKind.Delete, tokens[i], tokens[i].Length, 0);
            }
        }

        public void Insert(List<Token> tokens, int from, int to)
        {
            for (var i = from; i < to; i++)
            {
                _inserted += tokens[i].IsWord ? 1 : 0;
                Add(WordDiffKind.Insert, tokens[i], 0, tokens[i].Length);
            }
        }

        private void Add(WordDiffKind kind, Token token, int aLength, int bLength)
        {
            if (_runs.Count > 0 && _runs[^1].Kind == kind)
            {
                var last = _runs[^1];
                last.AEnd += aLength;
                last.BEnd += bLength;
                last.IsSpace = false;
                _runs[^1] = last;
            }
            else
            {
                _runs.Add(new Run
                {
                    Kind = kind, AStart = _a, AEnd = _a + aLength, BStart = _b, BEnd = _b + bLength, IsSpace = !token.IsWord
                });
            }
            _a += aLength;
            _b += bLength;
        }

        public WordDiffResult Build(string before, string after)
        {
            var segments = new List<WordDiffSegment>();
            var k = 0;
            while (k < _runs.Count)
            {
                var run = _runs[k];
                if (run.Kind == WordDiffKind.Equal && !JoinsChanges(k))
                {
                    segments.Add(new WordDiffSegment(WordDiffKind.Equal, before.Substring(run.AStart, run.AEnd - run.AStart)));
                    k++;
                    continue;
                }
                // A changed region: its old chars and its new chars are each contiguous, so the region becomes one
                // Delete then one Insert. Whitespace between two changes is deleted and inserted with them.
                var aStart = run.AStart;
                var bStart = run.BStart;
                var aEnd = aStart;
                var bEnd = bStart;
                while (k < _runs.Count && (_runs[k].Kind != WordDiffKind.Equal || JoinsChanges(k)))
                {
                    aEnd = _runs[k].AEnd;
                    bEnd = _runs[k].BEnd;
                    k++;
                }
                if (aEnd > aStart)
                {
                    segments.Add(new WordDiffSegment(WordDiffKind.Delete, before.Substring(aStart, aEnd - aStart)));
                }
                if (bEnd > bStart)
                {
                    segments.Add(new WordDiffSegment(WordDiffKind.Insert, after.Substring(bStart, bEnd - bStart)));
                }
            }
            return new WordDiffResult(segments, _inserted, _deleted);
        }

        /// <summary>A whitespace-only Equal run with a change on both sides.</summary>
        private bool JoinsChanges(int index)
        {
            var run = _runs[index];
            return run.Kind == WordDiffKind.Equal && run.IsSpace && index > 0 && index < _runs.Count - 1 &&
                   _runs[index - 1].Kind != WordDiffKind.Equal && _runs[index + 1].Kind != WordDiffKind.Equal;
        }
    }
}
