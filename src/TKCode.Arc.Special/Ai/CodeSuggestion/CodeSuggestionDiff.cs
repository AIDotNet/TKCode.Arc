namespace TKCode.Arc;

/// <summary>One hunk as it applies to the file: clamped to the file and frozen for one layout pass.</summary>
internal sealed record CodeSuggestionSpan(CodeSuggestionHunk Hunk, string Id, int Start, int Remove, IReadOnlyList<string> Insert)
{
    /// <summary>The first original line after the removed ones.</summary>
    public int End => Start + Remove;
}

/// <summary>
/// The pure diff logic of <see cref="CodeSuggestion"/>: splitting the file into lines, turning hunks into spans that fit
/// the file (sorted, overlaps after the first dropped) and applying the accepted ones.
/// </summary>
internal static class CodeSuggestionDiff
{
    /// <summary>The file's lines, split on <c>\n</c> (a <c>\r</c> before it is dropped).</summary>
    public static string[] SplitLines(string? code) =>
        (code ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    /// <summary>
    /// The hunks that apply, in file order: each is clamped to the file, and a hunk that overlaps one before it in
    /// <paramref name="hunks"/> (or repeats its id) is ignored. Two hunks overlap when their removed lines meet, when they
    /// start at the same line, or when an insertion falls inside the other's removed lines.
    /// </summary>
    public static List<CodeSuggestionSpan> Normalize(IEnumerable<CodeSuggestionHunk> hunks, int lineCount)
    {
        var kept = new List<CodeSuggestionSpan>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var hunk in hunks)
        {
            var start = Math.Clamp(hunk.Start, 0, lineCount);
            var remove = Math.Clamp(hunk.Remove, 0, lineCount - start);
            var id = string.IsNullOrEmpty(hunk.Id) ? "#" + kept.Count : hunk.Id;
            var span = new CodeSuggestionSpan(hunk, id, start, remove, hunk.Insert ?? Array.Empty<string>());
            if (!ids.Add(id) || kept.Exists(other => Overlaps(other, span)))
            {
                continue;
            }
            kept.Add(span);
        }
        kept.Sort((a, b) => a.Start.CompareTo(b.Start));
        return kept;
    }

    /// <summary>Whether two spans touch the same lines.</summary>
    public static bool Overlaps(CodeSuggestionSpan a, CodeSuggestionSpan b)
    {
        if (a.Start == b.Start)
        {
            return true;
        }
        if (Math.Max(a.Start, b.Start) < Math.Min(a.End, b.End))
        {
            return true;
        }
        return (a.Remove == 0 && b.Start < a.Start && a.Start < b.End) || (b.Remove == 0 && a.Start < b.Start && b.Start < a.End);
    }

    /// <summary>The file with the spans <paramref name="applies"/> picks replaced by their new lines.</summary>
    public static string Apply(IReadOnlyList<string> lines, IReadOnlyList<CodeSuggestionSpan> spans, Func<CodeSuggestionSpan, bool> applies) =>
        string.Join('\n', ApplyLines(lines, spans, applies, null));

    /// <summary>
    /// The lines of the file with the spans <paramref name="applies"/> picks applied; <paramref name="firstInserted"/>, when
    /// given, receives for each span the index of its first new line in the result (spans in order).
    /// </summary>
    public static List<string> ApplyLines(IReadOnlyList<string> lines, IReadOnlyList<CodeSuggestionSpan> spans,
        Func<CodeSuggestionSpan, bool> applies, int[]? firstInserted)
    {
        var result = new List<string>(lines.Count + 8);
        var cursor = 0;
        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];
            for (; cursor < span.Start && cursor < lines.Count; cursor++)
            {
                result.Add(lines[cursor]);
            }
            if (firstInserted is not null)
            {
                firstInserted[i] = result.Count;
            }
            if (applies(span))
            {
                result.AddRange(span.Insert);
                cursor = span.End;
            }
        }
        for (; cursor < lines.Count; cursor++)
        {
            result.Add(lines[cursor]);
        }
        return result;
    }

    /// <summary>Lines added and removed by the spans <paramref name="counts"/> picks.</summary>
    public static (int Added, int Removed) Count(IEnumerable<CodeSuggestionSpan> spans, Func<CodeSuggestionSpan, bool> counts)
    {
        var added = 0;
        var removed = 0;
        foreach (var span in spans)
        {
            if (counts(span))
            {
                added += span.Insert.Count;
                removed += span.Remove;
            }
        }
        return (added, removed);
    }

    /// <summary>The language the highlighter gets for a file name: its extension (<c>fetch-invoice.ts</c> → <c>ts</c>).</summary>
    public static string LanguageOf(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return string.Empty;
        }
        var dot = fileName.LastIndexOf('.');
        return dot < 0 || dot == fileName.Length - 1 ? string.Empty : fileName[(dot + 1)..].ToLowerInvariant();
    }

    /// <summary>The letters of the language mark for a file name: <c>TS</c>, <c>JS</c>, <c>PY</c>; empty without an extension.</summary>
    public static string MonogramOf(string? fileName)
    {
        var language = LanguageOf(fileName);
        return language switch
        {
            "" => string.Empty,
            "tsx" or "mts" or "cts" => "TS",
            "jsx" or "mjs" or "cjs" => "JS",
            "csx" => "C#",
            "cs" => "C#",
            _ => language.Length <= 2 ? language.ToUpperInvariant() : language[..2].ToUpperInvariant()
        };
    }
}
