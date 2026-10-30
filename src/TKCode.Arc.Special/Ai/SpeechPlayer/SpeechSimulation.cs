namespace TKCode.Arc;

/// <summary>Options of <see cref="SpeechSimulation.SynthesizeAsync"/>.</summary>
public sealed class SpeechSimulationOptions
{
    /// <summary>How long the stand-in request waits before it answers, like a network round trip (640ms).</summary>
    public TimeSpan Delay { get; init; } = TimeSpan.FromMilliseconds(640);

    /// <summary>Rejects instead of answering, to show the failed state.</summary>
    public bool Fail { get; init; }

    /// <summary>The message the rejection carries.</summary>
    public string FailureMessage { get; init; } = "The voice service is busy. Try again in a moment.";
}

/// <summary>
/// A self-playing reading for the <see cref="SpeechPlayer.Simulation"/> of a <see cref="SpeechPlayer"/>, for demos and
/// prototypes: the reply to read, its author and the voices to offer. A player that has no <see cref="SpeechPlayer.Text"/> reads it once it is in view and,
/// with full motion, reads it again with the other of the first two voices after a short pause, until someone interacts.
/// <para>
/// It is also the stand-in text to speech behind every player without <see cref="SpeechPlayer.Synthesize"/>:
/// <see cref="SynthesizeAsync"/> waits like a network round trip, then answers with estimated word timings and no audio.
/// Nothing is sent anywhere. <see cref="EstimateTimings"/> and <see cref="FitTimings"/> are what the player uses when a
/// real service returns audio without alignment data.
/// </para>
/// </summary>
public sealed class SpeechSimulation
{
    /// <summary>The speaking rate the estimate assumes when none is given (words per minute at 1×).</summary>
    public const double DefaultWordsPerMinute = 170;

    /// <summary>The average word length the per-word time is scaled around (characters).</summary>
    private const double AverageWordLength = 4.7;

    /// <summary>The reply the simulation reads. Blank lines start new paragraphs.</summary>
    public string? Text { get; set; }

    /// <summary>Who wrote the reply, shown above it while the simulation plays (unless the player has its own <see cref="SpeechPlayer.AuthorName"/>).</summary>
    public string? AuthorName { get; set; }

    /// <summary>The voices the picker offers while the simulation plays (unless the player has its own <see cref="SpeechPlayer.Voices"/>); the loop alternates the first two.</summary>
    public IReadOnlyList<SpeechVoice> Voices { get; set; } = [];

    /// <summary>
    /// Answers <paramref name="request"/> after <see cref="SpeechSimulationOptions.Delay"/> with estimated timings at the
    /// voice's own pace and no audio, or throws <see cref="InvalidOperationException"/> when
    /// <see cref="SpeechSimulationOptions.Fail"/> is set. Honors the request's cancellation.
    /// </summary>
    public static async Task<SpeechAudio> SynthesizeAsync(SpeechRequest request, SpeechSimulationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        options ??= new SpeechSimulationOptions();
        if (options.Delay > TimeSpan.Zero)
        {
            await Task.Delay(options.Delay, request.CancellationToken).ConfigureAwait(true);
        }
        request.CancellationToken.ThrowIfCancellationRequested();
        if (options.Fail)
        {
            throw new InvalidOperationException(options.FailureMessage);
        }
        return new SpeechAudio { Timings = EstimateTimings(request.Text, PaceOf(request.Voice)) };
    }

    /// <summary>
    /// One timing per whitespace separated word of <paramref name="text"/>: each word takes time by its length around
    /// <paramref name="wordsPerMinute"/>, a comma or colon adds a short pause, a sentence end a longer one and a blank line
    /// (a new paragraph) the longest.
    /// </summary>
    public static IReadOnlyList<SpeechWordTiming> EstimateTimings(string text, double wordsPerMinute = DefaultWordsPerMinute)
    {
        ArgumentNullException.ThrowIfNull(text);
        var perWord = 60.0 / (double.IsFinite(wordsPerMinute) && wordsPerMinute > 0 ? wordsPerMinute : DefaultWordsPerMinute);
        var timings = new List<SpeechWordTiming>();
        var at = 0.0;
        var i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }
            if (i >= text.Length)
            {
                break;
            }
            var start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
            {
                i++;
            }
            var length = i - start;
            var seconds = perWord * (0.45 + 0.55 * Math.Clamp(length / AverageWordLength, 0.4, 2.6));
            timings.Add(new SpeechWordTiming(TimeSpan.FromSeconds(at), TimeSpan.FromSeconds(at + seconds)));
            at += seconds + PauseAfter(text, i, perWord);
        }
        return timings;
    }

    /// <summary>
    /// Scales <paramref name="timings"/> so the last word ends at <paramref name="duration"/> (an audio file's length when the
    /// service sent no alignment). Empty timings or a zero length come back unchanged.
    /// </summary>
    public static IReadOnlyList<SpeechWordTiming> FitTimings(IReadOnlyList<SpeechWordTiming> timings, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(timings);
        if (timings.Count == 0 || duration <= TimeSpan.Zero || timings[^1].End <= TimeSpan.Zero)
        {
            return timings;
        }
        var scale = duration.TotalMilliseconds / timings[^1].End.TotalMilliseconds;
        var fitted = new SpeechWordTiming[timings.Count];
        for (var i = 0; i < timings.Count; i++)
        {
            fitted[i] = new SpeechWordTiming(TimeSpan.FromMilliseconds(timings[i].Start.TotalMilliseconds * scale),
                TimeSpan.FromMilliseconds(timings[i].End.TotalMilliseconds * scale));
        }
        return fitted;
    }

    /// <summary>Each voice's own pace, 170 ± 8 words per minute by its id, so switching voices is audible in the timing.</summary>
    internal static double PaceOf(SpeechVoice voice) => DefaultWordsPerMinute + StableHash(voice.Id) % 17 - 8;

    private static int StableHash(string text)
    {
        var hash = 0;
        foreach (var c in text)
        {
            hash = unchecked(hash * 31 + c) & 0x7fffffff;
        }
        return hash;
    }

    private static double PauseAfter(string text, int end, double perWord)
    {
        var last = text[end - 1];
        // A blank line after the word starts a new paragraph.
        var newlines = 0;
        for (var j = end; j < text.Length && char.IsWhiteSpace(text[j]); j++)
        {
            if (text[j] == '\n')
            {
                newlines++;
            }
        }
        if (newlines >= 2)
        {
            return perWord * 1.6;
        }
        return last switch
        {
            '.' or '!' or '?' => perWord * 1.1,
            ',' or ';' or ':' => perWord * 0.5,
            '—' or '–' => perWord * 0.4,
            _ => 0
        };
    }
}
