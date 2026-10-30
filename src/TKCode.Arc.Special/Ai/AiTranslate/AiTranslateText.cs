using System.Text;

namespace TKCode.Arc;

/// <summary>Pure text helpers of <see cref="AiTranslate"/>: language codes, detection, sentence splitting and streaming chunks.</summary>
internal static class AiTranslateText
{
    /// <summary>Words per streamed chunk, in turn (Arc: chunks of 3 to 6 words).</summary>
    private static readonly int[] s_wordSizes = [4, 3, 5, 6, 3, 5, 4, 6];

    /// <summary>Characters per streamed chunk in Japanese, in turn (about 10).</summary>
    private static readonly int[] s_charSizes = [10, 9, 11, 10, 8, 12];

    /// <summary>All six languages, in the picker's default order.</summary>
    public static readonly IReadOnlyList<AiTranslateLanguage> All =
    [
        AiTranslateLanguage.English, AiTranslateLanguage.German, AiTranslateLanguage.French,
        AiTranslateLanguage.Spanish, AiTranslateLanguage.Japanese, AiTranslateLanguage.Portuguese,
    ];

    /// <summary>The BCP 47 code of a language: en, de, fr, es, ja, pt.</summary>
    public static string Code(AiTranslateLanguage language) => language switch
    {
        AiTranslateLanguage.German => "de",
        AiTranslateLanguage.French => "fr",
        AiTranslateLanguage.Spanish => "es",
        AiTranslateLanguage.Japanese => "ja",
        AiTranslateLanguage.Portuguese => "pt",
        _ => "en",
    };

    /// <summary>Whether a character is Japanese or Chinese script or full-width punctuation (no spaces between words).</summary>
    public static bool IsCjk(char c) =>
        c is >= '　' and <= 'ヿ' or >= '㐀' and <= '䶿' or >= '一' and <= '鿿' or >= '＀' and <= '￯';

    /// <summary>
    /// Splits text into sentences. Each sentence keeps its closing marks and the whitespace after it, so the pieces join
    /// back into the text. A period inside a number (4.2) or a word does not end a sentence; 。！？ end one without a space.
    /// </summary>
    public static List<string> Sentences(string? text)
    {
        var sentences = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            return sentences;
        }
        var start = 0;
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            var latin = c is '.' or '!' or '?' or '…';
            var full = c is '。' or '！' or '？';
            if (!latin && !full)
            {
                i++;
                continue;
            }
            var end = i + 1;
            while (end < text.Length && (text[end] is '.' or '!' or '?' or '…' or '。' or '！' or '？'))
            {
                end++;
            }
            while (end < text.Length && IsCloser(text[end]))
            {
                end++;
            }
            var spaced = end < text.Length && char.IsWhiteSpace(text[end]);
            if (end >= text.Length || spaced || full)
            {
                while (end < text.Length && char.IsWhiteSpace(text[end]))
                {
                    end++;
                }
                sentences.Add(text[start..end]);
                start = end;
            }
            i = end;
        }
        if (start < text.Length)
        {
            sentences.Add(text[start..]);
        }
        return sentences;
    }

    /// <summary>How many sentences have started in <paramref name="text"/> (a sentence of only whitespace does not count).</summary>
    public static int StartedSentences(string? text)
    {
        var count = 0;
        foreach (var sentence in Sentences(text))
        {
            if (!string.IsNullOrWhiteSpace(sentence))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// The language of a text from a light check: kana means Japanese; otherwise the most common short words and the
    /// letters only one language uses decide. English when nothing matches.
    /// </summary>
    public static AiTranslateLanguage Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return AiTranslateLanguage.English;
        }
        var cjk = 0;
        foreach (var c in text)
        {
            if (c is >= '぀' and <= 'ヿ')
            {
                return AiTranslateLanguage.Japanese;
            }
            if (IsCjk(c))
            {
                cjk++;
            }
        }
        if (cjk > text.Length / 4)
        {
            return AiTranslateLanguage.Japanese;
        }
        Span<int> scores = stackalloc int[6];
        foreach (var c in text)
        {
            switch (char.ToLowerInvariant(c))
            {
                case 'ß' or 'ä' or 'ö' or 'ü':
                    scores[(int)AiTranslateLanguage.German] += 2;
                    break;
                case 'è' or 'ë' or 'ù' or 'î' or 'œ':
                    scores[(int)AiTranslateLanguage.French] += 2;
                    break;
                case 'ñ' or '¿' or '¡':
                    scores[(int)AiTranslateLanguage.Spanish] += 2;
                    break;
                case 'ã' or 'õ':
                    scores[(int)AiTranslateLanguage.Portuguese] += 2;
                    break;
            }
        }
        var word = new StringBuilder();
        foreach (var c in text + " ")
        {
            if (char.IsLetter(c) || c == '\'')
            {
                word.Append(char.ToLowerInvariant(c));
                continue;
            }
            if (word.Length > 0)
            {
                Score(word.ToString(), scores);
                word.Clear();
            }
        }
        var best = AiTranslateLanguage.English;
        var top = 0;
        for (var i = 0; i < scores.Length; i++)
        {
            if (scores[i] > top)
            {
                top = scores[i];
                best = (AiTranslateLanguage)i;
            }
        }
        return best;
    }

    private static void Score(string word, Span<int> scores)
    {
        if (s_stopwords.TryGetValue(word, out var languages))
        {
            foreach (var language in languages)
            {
                scores[(int)language]++;
            }
        }
    }

    private static readonly Dictionary<string, AiTranslateLanguage[]> s_stopwords = BuildStopwords();

    private static Dictionary<string, AiTranslateLanguage[]> BuildStopwords()
    {
        var lists = new (AiTranslateLanguage Language, string Words)[]
        {
            (AiTranslateLanguage.English, "the and is are to of you this with for that it we our thanks now was were have has be will"),
            (AiTranslateLanguage.German, "der die das und ist sind nicht mit für ein eine jetzt danke ich wir zu auf den dem des von im auch"),
            (AiTranslateLanguage.French, "le la les et est des une un pour avec sont que qui du merci nous vous dans ce cette pas au aux leur"),
            (AiTranslateLanguage.Spanish, "el la los las y es son de que para con una ya gracias está por del en al su esta"),
            (AiTranslateLanguage.Portuguese, "o a os as e é são de que para com uma já obrigado está em não do da dos das na no até sua seu mais também você"),
        };
        var map = new Dictionary<string, List<AiTranslateLanguage>>(StringComparer.Ordinal);
        foreach (var (language, words) in lists)
        {
            foreach (var w in words.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!map.TryGetValue(w, out var list))
                {
                    list = [];
                    map[w] = list;
                }
                list.Add(language);
            }
        }
        var result = new Dictionary<string, AiTranslateLanguage[]>(StringComparer.Ordinal);
        foreach (var (key, value) in map)
        {
            result[key] = [.. value];
        }
        return result;
    }

    /// <summary>
    /// Takes the next streamed chunk from the front of <paramref name="pending"/>: 3 to 6 whole words (the
    /// <paramref name="index"/>-th size in turn), or about 10 characters in Japanese. A word still arriving (no space after
    /// it yet) waits unless <paramref name="final"/>. Returns false when nothing can go yet.
    /// </summary>
    public static bool TakeChunk(StringBuilder pending, bool final, bool cjk, int index, out string chunk)
    {
        chunk = string.Empty;
        if (pending.Length == 0)
        {
            return false;
        }
        int end;
        if (cjk)
        {
            var size = s_charSizes[index % s_charSizes.Length];
            if (pending.Length < size && !final)
            {
                return false;
            }
            end = Math.Min(size, pending.Length);
            if (end < pending.Length && char.IsLowSurrogate(pending[end]))
            {
                end++;
            }
            // Trailing punctuation and spaces go with the chunk they close.
            while (end < pending.Length && (pending[end] is '、' or '。' or '！' or '？' or '」' or '）' || char.IsWhiteSpace(pending[end])))
            {
                end++;
            }
        }
        else
        {
            var words = s_wordSizes[index % s_wordSizes.Length];
            var i = 0;
            end = 0;
            var taken = 0;
            while (i < pending.Length && char.IsWhiteSpace(pending[i]))
            {
                i++;
            }
            while (taken < words && i < pending.Length)
            {
                while (i < pending.Length && !char.IsWhiteSpace(pending[i]))
                {
                    i++;
                }
                var spaceStart = i;
                while (i < pending.Length && char.IsWhiteSpace(pending[i]))
                {
                    i++;
                }
                if (i == spaceStart && !final)
                {
                    break;
                }
                taken++;
                end = i;
            }
            if (taken == 0)
            {
                return false;
            }
        }
        chunk = pending.ToString(0, end);
        pending.Remove(0, end);
        return chunk.Length > 0;
    }

    /// <summary>
    /// The paragraph while a translation streams: the new text, then what is left of the old one, with a space between them
    /// unless one side already ends or starts with one or both sides are Japanese.
    /// </summary>
    public static string Separator(string streamed, string rest)
    {
        if (streamed.Length == 0 || rest.Length == 0 || char.IsWhiteSpace(streamed[^1]) || char.IsWhiteSpace(rest[0]))
        {
            return string.Empty;
        }
        return IsCjk(streamed[^1]) && IsCjk(rest[0]) ? string.Empty : " ";
    }

    private static bool IsCloser(char c) => c is '"' or '\'' or ')' or ']' or '»' or '”' or '’' or '」' or '』' or '）';
}
