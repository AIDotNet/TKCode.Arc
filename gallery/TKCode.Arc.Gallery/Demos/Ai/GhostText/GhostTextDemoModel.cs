using System.Runtime.CompilerServices;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The rule based stand-in for a model that the ghost text demos use. It streams pull request writing continuations in 3
/// to 6 word chunks after a short think, answers the demo's scripted lines, completes a partial word from a small
/// vocabulary, and fails with a timeout when the text contains the word "timeout" so the error state can be seen. Nothing
/// is sent anywhere; the phrases are demo content.
/// </summary>
internal static class GhostTextDemoModel
{
    /// <summary>How long the demo "thinks" before its first chunk.</summary>
    public static TimeSpan ThinkTime { get; set; } = TimeSpan.FromMilliseconds(520);

    /// <summary>The pause between two streamed chunks.</summary>
    public static TimeSpan ChunkGap { get; set; } = TimeSpan.FromMilliseconds(90);

    /// <summary>The reply that makes the demo throw (the text contained "timeout").</summary>
    public const string TimeoutMarker = "\u0000timeout";

    /// <summary>Creates the demo function.</summary>
    public static GhostTextSuggest Create() => (text, cancellationToken) =>
    {
        var continuation = Continue(text);
        if (continuation is null)
        {
            return null;
        }
        return GhostTextResponse.FromStream(Stream(continuation, cancellationToken));
    };

    /// <summary>
    /// The continuation the demo gives for <paramref name="text"/>: the scripted one, a completed partial word followed by a
    /// phrase, or a phrase picked from the last words. A text that mentions "timeout" returns <see cref="TimeoutMarker"/> (the
    /// stream then throws a <see cref="TimeoutException"/>); null means no suggestion.
    /// </summary>
    public static string? Continue(string text)
    {
        foreach (var line in GhostTextDemoData.Script)
        {
            if (text.EndsWith(line.Typed, StringComparison.Ordinal))
            {
                return line.Reply;
            }
        }
        if (text.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return TimeoutMarker;
        }
        var trimmed = text.TrimEnd();
        if (trimmed.Length == 0)
        {
            return null;
        }
        var endsWithSpace = text.Length > 0 && char.IsWhiteSpace(text[^1]);
        var lastWord = LastWord(trimmed);
        if (!endsWithSpace && lastWord.Length >= 2)
        {
            foreach (var word in Vocabulary)
            {
                if (word.Length > lastWord.Length && word.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase))
                {
                    return word[lastWord.Length..] + " " + Phrase(trimmed).TrimStart();
                }
            }
        }
        var phrase = Phrase(trimmed);
        if (trimmed[^1] is '.' or '!' or '?')
        {
            phrase = " " + Sentences[Math.Abs(Hash(trimmed)) % Sentences.Length];
        }
        return endsWithSpace ? phrase.TrimStart() : phrase;
    }

    /// <summary>Splits <paramref name="text"/> into chunks of 3 to 6 words (each keeps its leading space).</summary>
    public static List<string> Chunks(string text)
    {
        var chunks = new List<string>();
        var start = 0;
        var words = 0;
        var target = 3;
        var i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
            {
                i++;
            }
            words++;
            if (words >= target)
            {
                chunks.Add(text[start..i]);
                start = i;
                words = 0;
                target = 3 + chunks.Count * 2 % 4;
            }
        }
        if (start < text.Length)
        {
            chunks.Add(text[start..]);
        }
        return chunks;
    }

    private static async IAsyncEnumerable<string> Stream(string continuation, [EnumeratorCancellation] CancellationToken token)
    {
        await Task.Delay(ThinkTime, token);
        if (continuation == TimeoutMarker)
        {
            throw new TimeoutException("Autocomplete timed out");
        }
        var first = true;
        foreach (var chunk in Chunks(continuation))
        {
            if (!first)
            {
                await Task.Delay(ChunkGap, token);
            }
            first = false;
            yield return chunk;
        }
    }

    private static string LastWord(string text)
    {
        var i = text.Length;
        while (i > 0 && char.IsLetter(text[i - 1]))
        {
            i--;
        }
        return text[i..];
    }

    private static string Phrase(string text)
    {
        var lower = text.ToLowerInvariant();
        foreach (var (keyword, phrase) in Phrases)
        {
            if (lower.EndsWith(keyword, StringComparison.Ordinal) || lower.Contains(" " + keyword + " ", StringComparison.Ordinal))
            {
                return phrase;
            }
        }
        return Fallbacks[Math.Abs(Hash(text)) % Fallbacks.Length];
    }

    private static int Hash(string text)
    {
        unchecked
        {
            var hash = 17;
            foreach (var c in text)
            {
                hash = hash * 31 + c;
            }
            return hash == int.MinValue ? 0 : hash;
        }
    }

    private static readonly string[] Vocabulary =
    [
        "checkout", "webhook", "retries", "payment", "migration", "regression", "flaky", "endpoint", "dashboard",
        "description", "deployment", "release", "rollback", "feature", "refactor", "behavior", "because", "before",
        "should", "screenshot", "reviewers", "tests", "timeouts", "customers",
    ];

    private static readonly (string Keyword, string Phrase)[] Phrases =
    [
        ("fixes", " the flaky checkout test that failed on slow runners."),
        ("this pr", " moves the retry logic into one helper so every caller behaves the same."),
        ("because", " the old code retried before the webhook had arrived."),
        ("test", " now waits for the payment webhook before it asserts."),
        ("tests", " cover the empty cart and the expired card cases."),
        ("to test", ", run the checkout suite twice with the network throttled."),
        ("adds", " a short backoff between retries so we stop hammering the API."),
        ("removes", " the sleep that hid the race in the payment step."),
        ("the", " checkout flow now waits for the webhook before it continues."),
    ];

    private static readonly string[] Fallbacks =
    [
        " so the checkout flow stays reliable on slow networks.",
        " and keeps the existing behavior for every other caller.",
        " without changing the public API.",
    ];

    private static readonly string[] Sentences =
    [
        "No migration is needed and the change is behind the existing flag.",
        "Reviewers can focus on the retry helper; the rest is renames.",
        "I ran the suite fifty times locally without a failure.",
    ];
}
