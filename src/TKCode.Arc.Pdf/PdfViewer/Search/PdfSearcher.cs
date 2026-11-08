using System.Runtime.CompilerServices;
using System.Text;
using Avalonia;

namespace TKCode.Arc;

/// <summary>What a search looks for besides the text.</summary>
internal readonly record struct PdfSearchOptions(bool MatchCase, bool WholeWord);

/// <summary>One result: a range of a page's original text and where it is on the page.</summary>
internal sealed class PdfSearchMatch
{
    public PdfSearchMatch(int pageIndex, int start, int length, Rect[] rects)
    {
        PageIndex = pageIndex;
        Start = start;
        Length = length;
        Rects = rects;
    }

    public int PageIndex { get; }

    /// <summary>Where it starts in <see cref="PdfPageText.Text"/>.</summary>
    public int Start { get; }

    /// <summary>How many UTF-16 units of <see cref="PdfPageText.Text"/> it covers.</summary>
    public int Length { get; }

    /// <summary>One rectangle per line, in page coordinates; kept because the page's text may have left the cache by the time it is drawn.</summary>
    public Rect[] Rects { get; }

    /// <summary>The area around all of <see cref="Rects"/>; empty when the characters have no boxes.</summary>
    public Rect Bounds
    {
        get
        {
            var bounds = default(Rect);
            for (var i = 0; i < Rects.Length; i++)
            {
                bounds = i == 0 ? Rects[i] : bounds.Union(Rects[i]);
            }
            return bounds;
        }
    }
}

/// <summary>
/// Finds text in a document (design/components/pdf/pdf.md section 5.5). All of it is managed, so every backend behaves the same:
/// a backend only provides <see cref="PdfPageText"/>. A scan walks from a page to the end and around to the page before it on
/// the thread pool, reports each page as soon as it is done and stops when cancelled.
/// </summary>
internal sealed class PdfSearcher
{
    private readonly PdfPageTextCache _pages;
    private readonly int _pageCount;
    // One entry per text object, gone with it: a repeated search skips the normalization of the pages still in the cache.
    private readonly ConditionalWeakTable<PdfPageText, PdfNormalizedText> _folded = new();
    private readonly ConditionalWeakTable<PdfPageText, PdfNormalizedText> _exact = new();

    public PdfSearcher(PdfPageTextCache pages, int pageCount)
    {
        _pages = pages;
        _pageCount = pageCount;
    }

    /// <summary>The text a query really looks for; empty when there is nothing to look for (nothing but white space and the like).</summary>
    public static string PrepareQuery(string query, bool matchCase)
    {
        var text = PdfTextNormalizer.Normalize(query, matchCase).Text;
        return string.IsNullOrWhiteSpace(text) ? string.Empty : text;
    }

    /// <summary>Every match of a prepared query in normalized text, left to right, not overlapping; as ranges of the normalized text.</summary>
    public static List<(int Start, int Length)> Find(string normalizedText, string preparedQuery, bool wholeWord)
    {
        var found = new List<(int, int)>();
        if (preparedQuery.Length == 0)
        {
            return found;
        }
        var at = 0;
        while (at <= normalizedText.Length - preparedQuery.Length)
        {
            var hit = normalizedText.IndexOf(preparedQuery, at, StringComparison.Ordinal);
            if (hit < 0)
            {
                break;
            }
            if (!wholeWord || IsWholeWord(normalizedText, hit, preparedQuery.Length))
            {
                found.Add((hit, preparedQuery.Length));
                at = hit + preparedQuery.Length;
            }
            else
            {
                at = hit + 1;
            }
        }
        return found;
    }

    /// <summary>
    /// The matches as ranges of the original. Several matches can come from one character ("f" twice in the ligature for "ffi"):
    /// to a reader that is one place, so it counts once.
    /// </summary>
    public static List<(int Start, int Length)> FindOriginal(PdfNormalizedText normalized, string preparedQuery, bool wholeWord)
    {
        var found = Find(normalized.Text, preparedQuery, wholeWord);
        var ranges = new List<(int Start, int Length)>(found.Count);
        foreach (var (start, length) in found)
        {
            var range = normalized.ToOriginal(start, length);
            if (ranges.Count == 0 || ranges[^1] != range)
            {
                ranges.Add(range);
            }
        }
        return ranges;
    }

    /// <summary>
    /// Both ends must be word boundaries. An end that is a CJK character needs none (those scripts have no spaces to tell a word
    /// by), and a CJK neighbour counts as one: "abc" is a whole word in 中abc文.
    /// </summary>
    private static bool IsWholeWord(string text, int start, int length)
    {
        var first = PdfTextCharacters.RuneAt(text, start);
        if (!PdfTextCharacters.IsCjk(first.Value) && start > 0 && TryRuneBefore(text, start, out var before)
            && PdfTextCharacters.IsWordCharacter(before) && !PdfTextCharacters.IsCjk(before.Value) && PdfTextCharacters.IsWordCharacter(first))
        {
            return false;
        }
        var end = start + length;
        if (end >= text.Length || !TryRuneBefore(text, end, out var last) || PdfTextCharacters.IsCjk(last.Value) || !PdfTextCharacters.IsWordCharacter(last))
        {
            return true;
        }
        var after = PdfTextCharacters.RuneAt(text, end);
        return !PdfTextCharacters.IsWordCharacter(after) || PdfTextCharacters.IsCjk(after.Value);
    }

    private static bool TryRuneBefore(string text, int index, out Rune rune) =>
        Rune.DecodeLastFromUtf16(text.AsSpan(0, index), out rune, out _) == System.Buffers.OperationStatus.Done;

    /// <summary>The matches of a prepared query on one page.</summary>
    public PdfSearchMatch[] FindOnPage(int pageIndex, PdfPageText page, string preparedQuery, PdfSearchOptions options)
    {
        var normalized = options.MatchCase
            ? _exact.GetValue(page, static p => PdfTextNormalizer.Normalize(p.Text, matchCase: true))
            : _folded.GetValue(page, static p => PdfTextNormalizer.Normalize(p.Text, matchCase: false));
        var found = FindOriginal(normalized, preparedQuery, options.WholeWord);
        if (found.Count == 0)
        {
            return [];
        }
        var matches = new PdfSearchMatch[found.Count];
        for (var i = 0; i < found.Count; i++)
        {
            var (start, length) = found[i];
            matches[i] = new PdfSearchMatch(pageIndex, start, length, page.GetRects(start, length).ToArray());
        }
        return matches;
    }

    /// <summary>
    /// Walks every page once, starting at <paramref name="startPage"/> and wrapping around. <paramref name="report"/> hears of every
    /// page, in that order, on a thread pool thread. Ends with <see cref="OperationCanceledException"/> when cancelled or when the
    /// document was closed meanwhile.
    /// </summary>
    public Task ScanAsync(string query, PdfSearchOptions options, int startPage, Action<int, PdfSearchMatch[]> report, CancellationToken cancellationToken)
    {
        var prepared = PrepareQuery(query, options.MatchCase);
        if (prepared.Length == 0 || _pageCount == 0)
        {
            return Task.CompletedTask;
        }
        var first = Math.Clamp(startPage, 0, _pageCount - 1);
        return Task.Run(async () =>
        {
            for (var step = 0; step < _pageCount; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = (first + step) % _pageCount;
                var text = await _pages.GetAsync(page, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                report(page, FindOnPage(page, text, prepared, options));
            }
        }, cancellationToken);
    }
}

/// <summary>
/// The results so far, in document order, while they arrive in scan order: from the start page to the end into one list and then
/// from page 0 up to the start page into another, which keeps every arrival an append and every index O(1).
/// </summary>
internal sealed class PdfSearchResults
{
    private readonly List<PdfSearchMatch> _before = new();
    private readonly List<PdfSearchMatch> _fromStart = new();
    private int _startPage;

    public int Count => _before.Count + _fromStart.Count;

    public PdfSearchMatch this[int index] => index < _before.Count ? _before[index] : _fromStart[index - _before.Count];

    public void Reset(int startPage)
    {
        _before.Clear();
        _fromStart.Clear();
        _startPage = startPage;
    }

    /// <summary>Adds a page's matches and returns how many positions the results already there moved by.</summary>
    public int Add(int pageIndex, PdfSearchMatch[] matches)
    {
        if (pageIndex >= _startPage)
        {
            _fromStart.AddRange(matches);
            return 0;
        }
        _before.AddRange(matches);
        return matches.Length;
    }

    /// <summary>The first match of a page: where in which list it is. Reading on until another page takes no allocation.</summary>
    public bool TryFindPage(int pageIndex, out List<PdfSearchMatch> list, out int first)
    {
        list = pageIndex >= _startPage ? _fromStart : _before;
        var low = 0;
        var high = list.Count;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (list[middle].PageIndex < pageIndex)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }
        first = low;
        return low < list.Count && list[low].PageIndex == pageIndex;
    }
}
