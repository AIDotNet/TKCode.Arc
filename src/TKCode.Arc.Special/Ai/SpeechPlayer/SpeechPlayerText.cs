using System.Text.RegularExpressions;

namespace TKCode.Arc;

/// <summary>One word of a paragraph: where it is in the paragraph's text.</summary>
internal readonly record struct SpeechPlayerWord(int Start, int Length);

/// <summary>One paragraph of the reply: its text, its words and the index of its first word in the whole reply.</summary>
internal sealed class SpeechPlayerParagraphText(string text, int wordOffset, IReadOnlyList<SpeechPlayerWord> words)
{
    public string Text { get; } = text;

    public int WordOffset { get; } = wordOffset;

    public IReadOnlyList<SpeechPlayerWord> Words { get; } = words;
}

/// <summary>
/// The reply as the player reads it: blank lines split paragraphs and every whitespace separated word is one highlight
/// step, numbered across the whole reply (so the numbering matches a provider's per-word timings for the same text).
/// </summary>
internal sealed partial class SpeechPlayerText
{
    public static readonly SpeechPlayerText Empty = new([], []);

    private SpeechPlayerText(IReadOnlyList<SpeechPlayerParagraphText> paragraphs, IReadOnlyList<string> words)
    {
        Paragraphs = paragraphs;
        Words = words;
    }

    public IReadOnlyList<SpeechPlayerParagraphText> Paragraphs { get; }

    /// <summary>Every word of the reply in reading order.</summary>
    public IReadOnlyList<string> Words { get; }

    [GeneratedRegex(@"\r?\n[ \t]*(?:\r?\n[ \t]*)+")]
    private static partial Regex BlankLines();

    public static SpeechPlayerText Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Empty;
        }
        var paragraphs = new List<SpeechPlayerParagraphText>();
        var all = new List<string>();
        foreach (var raw in BlankLines().Split(text))
        {
            var paragraph = raw.Trim();
            if (paragraph.Length == 0)
            {
                continue;
            }
            var words = new List<SpeechPlayerWord>();
            var offset = all.Count;
            var i = 0;
            while (i < paragraph.Length)
            {
                while (i < paragraph.Length && char.IsWhiteSpace(paragraph[i]))
                {
                    i++;
                }
                var start = i;
                while (i < paragraph.Length && !char.IsWhiteSpace(paragraph[i]))
                {
                    i++;
                }
                if (i > start)
                {
                    words.Add(new SpeechPlayerWord(start, i - start));
                    all.Add(paragraph.Substring(start, i - start));
                }
            }
            paragraphs.Add(new SpeechPlayerParagraphText(paragraph, offset, words));
        }
        return new SpeechPlayerText(paragraphs, all);
    }

    /// <summary>The text exactly as it is spoken: the paragraphs joined by blank lines.</summary>
    public string Spoken => string.Join("\n\n", Paragraphs.Select(p => p.Text));

    /// <summary>
    /// The word spoken at <paramref name="seconds"/>: the last word that has started (−1 before the first). Binary search, so
    /// a frame costs O(log n).
    /// </summary>
    public static int WordAt(IReadOnlyList<SpeechWordTiming> timings, double seconds)
    {
        var lo = 0;
        var hi = timings.Count - 1;
        var found = -1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (timings[mid].Start.TotalSeconds <= seconds + 1e-9)
            {
                found = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return found;
    }

    /// <summary>"0:04", "1:05", "1:02:09" from whole seconds.</summary>
    public static string Clock(double seconds)
    {
        var whole = (long)Math.Max(0, Math.Floor(seconds + 1e-6));
        var h = whole / 3600;
        var m = whole / 60 % 60;
        var s = whole % 60;
        return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m}:{s:00}";
    }
}
