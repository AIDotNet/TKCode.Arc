using System.Globalization;
using System.Text;

namespace TKCode.Arc;

/// <summary>
/// The text work of <see cref="KnowledgeBase"/>, kept pure so it can be tested and reused on a server: splitting a
/// document into chunks, the built-in keyword search (BM25 over the indexed chunks), the preview window around the first
/// match with the words to mark, and the size and kind of a file.
/// </summary>
internal static class KnowledgeBaseText
{
    /// <summary>The length a chunk grows to before a new one starts (about 480 characters, a short paragraph or two).</summary>
    public const int DefaultChunkLength = 480;

    // BM25's usual constants: term-frequency saturation and length normalization.
    private const double K1 = 1.2;
    private const double B = 0.75;

    private static readonly HashSet<string> s_stopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "can", "do", "does", "for", "from", "how", "i", "if", "in", "is",
        "it", "its", "my", "of", "on", "or", "our", "that", "the", "their", "there", "this", "to", "was", "we", "what",
        "when", "where", "which", "who", "why", "will", "with", "you", "your"
    };

    #region Chunks

    /// <summary>
    /// Splits <paramref name="text"/> into chunks of about <paramref name="target"/> characters: paragraphs (blank lines)
    /// are kept together while they fit, a long paragraph is cut at sentence ends, and a sentence longer than the target
    /// at a space. Empty text gives no chunks.
    /// </summary>
    public static IReadOnlyList<string> Split(string? text, int target = DefaultChunkLength)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return chunks;
        }
        target = Math.Max(40, target);
        var current = new StringBuilder();
        foreach (var paragraph in Paragraphs(text))
        {
            if (paragraph.Length > target)
            {
                Flush(current, chunks);
                foreach (var piece in Sentences(paragraph, target))
                {
                    if (current.Length > 0 && current.Length + 1 + piece.Length > target)
                    {
                        Flush(current, chunks);
                    }
                    if (current.Length > 0)
                    {
                        current.Append(' ');
                    }
                    current.Append(piece);
                }
                Flush(current, chunks);
                continue;
            }
            if (current.Length > 0 && current.Length + 2 + paragraph.Length > target)
            {
                Flush(current, chunks);
            }
            if (current.Length > 0)
            {
                current.Append("\n\n");
            }
            current.Append(paragraph);
        }
        Flush(current, chunks);
        return chunks;
    }

    /// <summary>Turns the pieces of <see cref="Split"/> into chunks of the document <paramref name="documentId"/>.</summary>
    public static IReadOnlyList<KnowledgeChunk> Chunk(string documentId, string? text, int target = DefaultChunkLength)
    {
        var pieces = Split(text, target);
        var chunks = new KnowledgeChunk[pieces.Count];
        for (var i = 0; i < pieces.Count; i++)
        {
            chunks[i] = new KnowledgeChunk(documentId, i, pieces[i]);
        }
        return chunks;
    }

    private static void Flush(StringBuilder current, List<string> chunks)
    {
        if (current.Length > 0)
        {
            chunks.Add(current.ToString());
            current.Clear();
        }
    }

    private static IEnumerable<string> Paragraphs(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        foreach (var block in normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            // Lines inside a paragraph join with a space; a paragraph of only white space is dropped.
            var joined = string.Join(' ', block.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            if (joined.Length > 0)
            {
                yield return joined;
            }
        }
    }

    private static IEnumerable<string> Sentences(string paragraph, int target)
    {
        var start = 0;
        for (var i = 0; i < paragraph.Length; i++)
        {
            var c = paragraph[i];
            var end = c is '。' or '！' or '？' || (c is '.' or '!' or '?' && (i + 1 == paragraph.Length || paragraph[i + 1] == ' '));
            if (!end)
            {
                continue;
            }
            foreach (var piece in Hard(paragraph[start..(i + 1)].Trim(), target))
            {
                yield return piece;
            }
            start = i + 1;
        }
        if (start < paragraph.Length)
        {
            foreach (var piece in Hard(paragraph[start..].Trim(), target))
            {
                yield return piece;
            }
        }
    }

    private static IEnumerable<string> Hard(string sentence, int target)
    {
        while (sentence.Length > target)
        {
            var cut = sentence.LastIndexOf(' ', target);
            if (cut <= 0)
            {
                cut = target;
            }
            yield return sentence[..cut].Trim();
            sentence = sentence[cut..].Trim();
        }
        if (sentence.Length > 0)
        {
            yield return sentence;
        }
    }

    #endregion

    #region Search

    /// <summary>
    /// The search terms of <paramref name="query"/>: lower-case words of letters and digits, without common words
    /// ("the", "how") and one-letter words; each CJK character is a term of its own. Repeated terms count once.
    /// </summary>
    public static IReadOnlyList<string> Terms(string? query)
    {
        var terms = new List<string>();
        if (string.IsNullOrWhiteSpace(query))
        {
            return terms;
        }
        foreach (var (start, length) in Words(query))
        {
            var word = query.Substring(start, length).ToLowerInvariant();
            var cjk = IsCjk(word[0]);
            if ((!cjk && word.Length < 2) || s_stopWords.Contains(word))
            {
                continue;
            }
            if (!terms.Contains(word))
            {
                terms.Add(word);
            }
        }
        return terms;
    }

    /// <summary>
    /// Whether the word <paramref name="word"/> (lower case) matches the term <paramref name="term"/>: the same word, or
    /// for a term of three letters or more, a word that starts with it once a plural "s" is set aside on both
    /// ("refund" finds "refunds" and "refunded"; "refunds" finds "refund").
    /// </summary>
    public static bool Matches(ReadOnlySpan<char> word, string term)
    {
        if (word.Equals(term, StringComparison.Ordinal))
        {
            return true;
        }
        var stem = Stem(term.AsSpan());
        if (stem.Length < 3)
        {
            return false;
        }
        return Stem(word).StartsWith(stem, StringComparison.Ordinal) || word.StartsWith(stem, StringComparison.Ordinal);
    }

    private static ReadOnlySpan<char> Stem(ReadOnlySpan<char> word) =>
        word.Length > 3 && word[^1] == 's' && word[^2] != 's' ? word[..^1] : word;

    /// <summary>
    /// The built-in search: ranks every chunk of the ready documents against the terms of <paramref name="query"/> with
    /// BM25 and returns the best <paramref name="limit"/> passages that match at least one term, best first. Scores
    /// are scaled to 0–1 against a chunk that holds every term at saturation, so a strong match reads about 0.8.
    /// </summary>
    public static IReadOnlyList<KnowledgePassage> Search(IEnumerable<KnowledgeDocument> documents, string? query, int limit)
    {
        var terms = Terms(query);
        if (terms.Count == 0 || limit <= 0)
        {
            return Array.Empty<KnowledgePassage>();
        }
        var entries = new List<(KnowledgeDocument Document, KnowledgeChunk Chunk, int Length, int[] Counts)>();
        var documentFrequency = new int[terms.Count];
        long totalLength = 0;
        var buffer = new char[64];
        foreach (var document in documents)
        {
            if (document.Status != KnowledgeDocumentStatus.Ready || document.Chunks is not { Count: > 0 } chunks)
            {
                continue;
            }
            foreach (var chunk in chunks)
            {
                var counts = new int[terms.Count];
                var length = 0;
                var text = chunk.Text;
                foreach (var (start, wordLength) in Words(text))
                {
                    length++;
                    if (wordLength > buffer.Length)
                    {
                        buffer = new char[wordLength];
                    }
                    var lower = buffer.AsSpan(0, wordLength);
                    text.AsSpan(start, wordLength).ToLowerInvariant(lower);
                    for (var t = 0; t < terms.Count; t++)
                    {
                        if (Matches(lower, terms[t]))
                        {
                            counts[t]++;
                        }
                    }
                }
                for (var t = 0; t < terms.Count; t++)
                {
                    if (counts[t] > 0)
                    {
                        documentFrequency[t]++;
                    }
                }
                totalLength += length;
                entries.Add((document, chunk, length, counts));
            }
        }
        if (entries.Count == 0)
        {
            return Array.Empty<KnowledgePassage>();
        }
        var n = entries.Count;
        var average = Math.Max(1, totalLength / (double)n);
        var idf = new double[terms.Count];
        var ideal = 0.0;
        for (var t = 0; t < terms.Count; t++)
        {
            idf[t] = Math.Log(1 + (n - documentFrequency[t] + 0.5) / (documentFrequency[t] + 0.5));
            ideal += idf[t] * (K1 + 1);
        }
        var scored = new List<KnowledgePassage>();
        foreach (var entry in entries)
        {
            var raw = 0.0;
            for (var t = 0; t < terms.Count; t++)
            {
                var tf = entry.Counts[t];
                if (tf == 0)
                {
                    continue;
                }
                raw += idf[t] * tf * (K1 + 1) / (tf + K1 * (1 - B + B * entry.Length / average));
            }
            if (raw <= 0)
            {
                continue;
            }
            var score = ideal <= 0 ? 0 : Math.Clamp(Math.Sqrt(raw / ideal), 0, 1);
            scored.Add(new KnowledgePassage(entry.Document, entry.Chunk.Index, entry.Chunk.Text, score));
        }
        // Best first; ties keep the list order (document, then chunk), so the result is stable.
        return scored
            .Select((passage, order) => (passage, order))
            .OrderByDescending(p => p.passage.Score)
            .ThenBy(p => p.order)
            .Take(limit)
            .Select(p => p.passage)
            .ToArray();
    }

    /// <summary>
    /// The preview of a passage: a window of at most <paramref name="maxLength"/> characters that starts a little before
    /// the first word matching <paramref name="terms"/> (at a word start, with "…" where text was cut), and the ranges of
    /// every matching word inside it. Without a match the window is the start of the passage.
    /// </summary>
    public static (string Text, IReadOnlyList<KnowledgeTextRange> Ranges) Preview(string text, IReadOnlyList<string> terms, int maxLength = 240)
    {
        text = text.Replace("\n\n", " ").Replace('\n', ' ');
        var first = -1;
        if (terms.Count > 0)
        {
            foreach (var (start, length) in Words(text))
            {
                if (IsMatch(text, start, length, terms))
                {
                    first = start;
                    break;
                }
            }
        }
        var from = 0;
        if (first > 0 && text.Length > maxLength)
        {
            // A little context before the match, starting at a word.
            from = Math.Max(0, first - maxLength / 5);
            if (from > 0)
            {
                var space = text.LastIndexOf(' ', from);
                from = space < 0 ? 0 : space + 1;
            }
            // Do not leave the window short at the end.
            if (text.Length - from < maxLength)
            {
                var back = Math.Max(0, text.Length - maxLength);
                var space = back == 0 ? -1 : text.IndexOf(' ', back);
                from = Math.Min(from, space < 0 ? back : space + 1);
            }
        }
        var to = Math.Min(text.Length, from + maxLength);
        if (to < text.Length)
        {
            var space = text.LastIndexOf(' ', to);
            if (space > from)
            {
                to = space;
            }
        }
        var lead = from > 0 ? "… " : string.Empty;
        var tail = to < text.Length ? " …" : string.Empty;
        var body = text[from..to].Trim();
        var preview = lead + body + tail;
        var ranges = new List<KnowledgeTextRange>();
        if (terms.Count > 0)
        {
            foreach (var (start, length) in Words(preview))
            {
                if (IsMatch(preview, start, length, terms))
                {
                    ranges.Add(new KnowledgeTextRange(start, length));
                }
            }
        }
        return (preview, ranges);
    }

    private static bool IsMatch(string text, int start, int length, IReadOnlyList<string> terms)
    {
        Span<char> lower = length <= 64 ? stackalloc char[length] : new char[length];
        text.AsSpan(start, length).ToLowerInvariant(lower);
        foreach (var term in terms)
        {
            if (Matches(lower, term))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The words of <paramref name="text"/> as (start, length): runs of letters and digits, each CJK character alone.</summary>
    public static IEnumerable<(int Start, int Length)> Words(string text)
    {
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var c = i < text.Length ? text[i] : ' ';
            if (i < text.Length && IsCjk(c))
            {
                if (start >= 0)
                {
                    yield return (start, i - start);
                    start = -1;
                }
                yield return (i, 1);
                continue;
            }
            if (char.IsLetterOrDigit(c))
            {
                if (start < 0)
                {
                    start = i;
                }
            }
            else if (start >= 0)
            {
                yield return (start, i - start);
                start = -1;
            }
        }
    }

    private static bool IsCjk(char c) => c is >= '぀' and <= 'ヿ' or >= '㐀' and <= '鿿' or >= '가' and <= '힯';

    #endregion

    #region Files

    /// <summary>The kind of a file from its extension; anything unknown is <see cref="KnowledgeDocumentKind.Text"/>.</summary>
    public static KnowledgeDocumentKind KindOf(string fileName) => Extension(fileName) switch
    {
        ".pdf" => KnowledgeDocumentKind.Pdf,
        ".md" or ".markdown" or ".mdx" => KnowledgeDocumentKind.Markdown,
        ".doc" or ".docx" or ".rtf" or ".odt" => KnowledgeDocumentKind.Document,
        ".csv" or ".tsv" or ".xls" or ".xlsx" => KnowledgeDocumentKind.Spreadsheet,
        ".json" or ".xml" or ".yaml" or ".yml" or ".cs" or ".py" or ".ts" or ".js" or ".sql" => KnowledgeDocumentKind.Code,
        ".html" or ".htm" => KnowledgeDocumentKind.Web,
        _ => KnowledgeDocumentKind.Text
    };

    /// <summary>Whether the built-in pipeline can read the file as text (no parser needed).</summary>
    public static bool IsPlainText(string fileName) => Extension(fileName) is ".txt" or ".md" or ".markdown" or ".mdx" or ".csv" or ".tsv"
        or ".json" or ".xml" or ".yaml" or ".yml" or ".cs" or ".py" or ".ts" or ".js" or ".sql" or ".html" or ".htm" or ".log";

    /// <summary>The lower-case extension with its dot, or empty.</summary>
    public static string Extension(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        return dot < 0 || dot == fileName.Length - 1 ? string.Empty : fileName[dot..].ToLowerInvariant();
    }

    /// <summary>A size as people read it: "820 B", "48 KB", "2.4 MB", "36 MB".</summary>
    public static string FormatSize(long bytes, CultureInfo culture)
    {
        if (bytes < 1024)
        {
            return string.Format(culture, "{0} B", Math.Max(0, bytes));
        }
        if (bytes < 1024 * 1024)
        {
            return string.Format(culture, "{0:0} KB", bytes / 1024.0);
        }
        var mb = bytes / (1024.0 * 1024.0);
        return mb < 10 ? string.Format(culture, "{0:0.0} MB", mb) : string.Format(culture, "{0:0} MB", mb);
    }

    #endregion
}
