using System.Text;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>A piece of a <see cref="ToneTunerParagraph"/>: a sentence, or the whitespace (a space, a line break) between two.</summary>
internal readonly record struct ToneTunerPiece(int Start, int Length, bool IsSentence);

/// <summary>A paragraph of a message: its text, where it starts in the whole message, and its pieces in order.</summary>
internal sealed record ToneTunerParagraph(string Text, int Offset, IReadOnlyList<ToneTunerPiece> Pieces)
{
    /// <summary>The sentences' text in order.</summary>
    public IReadOnlyList<string> Sentences =>
        Pieces.Where(piece => piece.IsSentence).Select(piece => Text.Substring(piece.Start, piece.Length)).ToList();
}

/// <summary>
/// The text model of <see cref="ToneTuner"/>: blank lines split paragraphs, single line breaks keep lines (a sign off),
/// and a paragraph splits into sentences at a full stop, question or exclamation mark (common abbreviations such as
/// "Ms." excepted) and at each line break. Also the alignment of two versions sentence by sentence, the inserted ranges
/// against the original (Core <see cref="WordDiff"/>) and the instruction string for a model.
/// </summary>
internal static class ToneTunerText
{
    private static readonly HashSet<string> s_abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "mr", "ms", "mrs", "dr", "st", "e.g", "i.e", "etc", "vs", "no", "inc", "ltd", "jr", "sr", "approx", "dept",
    };

    /// <summary>Line breaks as <c>\n</c>, outer blank space trimmed.</summary>
    public static string Normalize(string? text) =>
        (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();

    /// <summary>The paragraphs of a normalized message.</summary>
    public static List<ToneTunerParagraph> Paragraphs(string text)
    {
        var result = new List<ToneTunerParagraph>();
        var start = -1;
        var end = 0;
        var lineStart = 0;
        while (lineStart <= text.Length)
        {
            var newline = text.IndexOf('\n', lineStart);
            var lineEnd = newline < 0 ? text.Length : newline;
            var blank = text.AsSpan(lineStart, lineEnd - lineStart).IsWhiteSpace();
            if (blank)
            {
                if (start >= 0)
                {
                    Add(result, text, start, end);
                    start = -1;
                }
            }
            else
            {
                if (start < 0)
                {
                    start = lineStart;
                }
                end = lineEnd;
            }
            if (newline < 0)
            {
                break;
            }
            lineStart = newline + 1;
        }
        if (start >= 0)
        {
            Add(result, text, start, end);
        }
        return result;
    }

    private static void Add(List<ToneTunerParagraph> result, string text, int start, int end)
    {
        var paragraph = text.Substring(start, end - start);
        result.Add(new ToneTunerParagraph(paragraph, start, Pieces(paragraph)));
    }

    /// <summary>The sentences and separators of one paragraph; joined they give the paragraph back exactly.</summary>
    public static List<ToneTunerPiece> Pieces(string paragraph)
    {
        var pieces = new List<ToneTunerPiece>();
        var i = 0;
        var n = paragraph.Length;
        while (i < n)
        {
            if (char.IsWhiteSpace(paragraph[i]))
            {
                var s = i;
                while (i < n && char.IsWhiteSpace(paragraph[i]))
                {
                    i++;
                }
                pieces.Add(new ToneTunerPiece(s, i - s, false));
                continue;
            }
            var start = i;
            while (i < n)
            {
                var c = paragraph[i];
                if (c == '\n')
                {
                    break;
                }
                i++;
                if (c is '.' or '!' or '?' or '…')
                {
                    while (i < n && paragraph[i] is '.' or '!' or '?' or '"' or '\'' or ')' or ']' or '”' or '’')
                    {
                        i++;
                    }
                    if ((i == n || char.IsWhiteSpace(paragraph[i])) && !(c == '.' && EndsWithAbbreviation(paragraph, start, i)))
                    {
                        break;
                    }
                }
            }
            // A sentence never ends in whitespace: give trailing spaces before a line break to the separator.
            var stop = i;
            while (stop > start && char.IsWhiteSpace(paragraph[stop - 1]))
            {
                stop--;
            }
            pieces.Add(new ToneTunerPiece(start, stop - start, true));
            if (stop < i)
            {
                pieces.Add(new ToneTunerPiece(stop, i - stop, false));
            }
        }
        return Merge(pieces);
    }

    private static List<ToneTunerPiece> Merge(List<ToneTunerPiece> pieces)
    {
        var merged = new List<ToneTunerPiece>(pieces.Count);
        foreach (var piece in pieces)
        {
            if (merged.Count > 0 && !piece.IsSentence && !merged[^1].IsSentence)
            {
                var last = merged[^1];
                merged[^1] = last with { Length = last.Length + piece.Length };
            }
            else
            {
                merged.Add(piece);
            }
        }
        return merged;
    }

    private static bool EndsWithAbbreviation(string text, int sentenceStart, int end)
    {
        var dot = text.LastIndexOf('.', end - 1, end - sentenceStart);
        var wordStart = dot;
        while (wordStart > sentenceStart && !char.IsWhiteSpace(text[wordStart - 1]))
        {
            wordStart--;
        }
        if (dot <= wordStart)
        {
            return false;
        }
        var word = text.Substring(wordStart, dot - wordStart).TrimStart('(', '"', '\'');
        return s_abbreviations.Contains(word) || (word.Length == 1 && char.IsUpper(word[0]));
    }

    /// <summary>
    /// How two versions of one paragraph line up: the number of equal sentences at the start and at the end. Everything
    /// between is changed (old ones leave, new ones arrive).
    /// </summary>
    public static (int Prefix, int Suffix) Align(IReadOnlyList<string> before, IReadOnlyList<string> after)
    {
        var prefix = 0;
        while (prefix < before.Count && prefix < after.Count && before[prefix] == after[prefix])
        {
            prefix++;
        }
        var suffix = 0;
        while (suffix < before.Count - prefix && suffix < after.Count - prefix &&
               before[before.Count - 1 - suffix] == after[after.Count - 1 - suffix])
        {
            suffix++;
        }
        return (prefix, suffix);
    }

    /// <summary>
    /// Which sentence of <paramref name="before"/> each sentence of <paramref name="after"/> keeps (its index), or -1 for a
    /// changed or new one: the longest common subsequence of equal sentences, so a sentence that stays the same between
    /// two changed ones is kept too. Sentences of <paramref name="before"/> that no one keeps leave.
    /// </summary>
    public static int[] Match(IReadOnlyList<string> before, IReadOnlyList<string> after)
    {
        var n = before.Count;
        var m = after.Count;
        var table = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
        {
            for (var j = m - 1; j >= 0; j--)
            {
                table[i, j] = before[i] == after[j] ? table[i + 1, j + 1] + 1 : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }
        var match = new int[m];
        Array.Fill(match, -1);
        var a = 0;
        var b = 0;
        while (a < n && b < m)
        {
            if (before[a] == after[b])
            {
                match[b] = a;
                a++;
                b++;
            }
            else if (table[a + 1, b] >= table[a, b + 1])
            {
                a++;
            }
            else
            {
                b++;
            }
        }
        return match;
    }

    /// <summary>
    /// The ranges of <paramref name="text"/> that are new against <paramref name="original"/> (word level, whitespace at
    /// either end left out) and the number of new words.
    /// </summary>
    public static (List<(int Start, int Length)> Ranges, int Words) Inserted(string original, string text)
    {
        var ranges = new List<(int, int)>();
        if (original == text)
        {
            return (ranges, 0);
        }
        var diff = WordDiff.Compute(original, text);
        var position = 0;
        foreach (var segment in diff.Segments)
        {
            if (segment.Kind == WordDiffKind.Delete)
            {
                continue;
            }
            if (segment.Kind == WordDiffKind.Insert)
            {
                var lead = 0;
                while (lead < segment.Text.Length && char.IsWhiteSpace(segment.Text[lead]))
                {
                    lead++;
                }
                var trail = 0;
                while (trail < segment.Text.Length - lead && char.IsWhiteSpace(segment.Text[segment.Text.Length - 1 - trail]))
                {
                    trail++;
                }
                var length = segment.Text.Length - lead - trail;
                if (length > 0)
                {
                    ranges.Add((position + lead, length));
                }
            }
            position += segment.Text.Length;
        }
        return (ranges, diff.InsertedWords);
    }

    /// <summary>The instruction a model gets with the draft.</summary>
    public static string Instructions(ToneSettings value)
    {
        var builder = new StringBuilder("Rewrite the customer message below. ");
        builder.Append(value.F switch
        {
            0 => "Formality: casual (plain, relaxed words; contractions are fine). ",
            2 => "Formality: formal (polished and professional; no contractions or slang). ",
            _ => "Formality: balanced (professional but natural). ",
        });
        builder.Append(value.D switch
        {
            0 => "Detail: brief (only what the reader needs; drop background). ",
            2 => "Detail: detailed (explain the cause, the timing and what happens next). ",
            _ => "Detail: standard (about as long as the draft). ",
        });
        builder.Append(value.Voice switch
        {
            ToneVoice.Friendly => "Voice: friendly (warm and approachable). ",
            ToneVoice.Confident => "Voice: confident (direct and sure; no hedging or apologizing twice). ",
            ToneVoice.Empathetic => "Voice: empathetic (acknowledge how the reader feels before the facts). ",
            _ => "Voice: neutral (keep the draft's own voice). ",
        });
        builder.Append("Keep every fact, number, date, amount and name exactly as written. Keep the paragraph breaks, the greeting and the sign off lines. ");
        builder.Append("Return only the rewritten message as plain text, with no preamble or quotes.");
        return builder.ToString();
    }
}
