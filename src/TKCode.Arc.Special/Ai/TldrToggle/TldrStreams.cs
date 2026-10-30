using System.Runtime.CompilerServices;
using System.Text;

namespace TKCode.Arc;

/// <summary>
/// Streams for a <see cref="TldrToggle"/>: the stand-in summarizer behind an article of yours with no summarizer, and
/// pacing for a provider that returns whole results. Both send chunks of 3 to 8 words every 30 to 40ms and stop when the
/// request's token is cancelled. Nothing is sent anywhere.
/// </summary>
public static class TldrStreams
{
    /// <summary>The fewest and most words in one chunk.</summary>
    internal const int MinChunkWords = 3;

    internal const int MaxChunkWords = 8;

    /// <summary>The shortest and longest wait between chunks.</summary>
    internal static readonly TimeSpan MinPace = TimeSpan.FromMilliseconds(30);

    internal static readonly TimeSpan MaxPace = TimeSpan.FromMilliseconds(40);

    /// <summary>The default thinking pause before the first chunk.</summary>
    public static readonly TimeSpan DefaultThinkTime = TimeSpan.FromMilliseconds(700);

    /// <summary>
    /// A stand-in summarizer for demos and tests, not a model: it waits <paramref name="thinkTime"/> (0.7s by default),
    /// then streams the article's lead sentences as a summary (one per paragraph in turn, up to the length's word target)
    /// or each paragraph's lead sentence as a key point. The card uses it for an article of yours with no summarizer;
    /// <see cref="TldrToggleSimulation"/> streams written summaries instead.
    /// </summary>
    /// <param name="request">The request the card passes to its summarizer.</param>
    /// <param name="fail">Throws after the thinking pause instead, to show the error state.</param>
    /// <param name="thinkTime">The thinking pause before the first chunk; null is 0.7s.</param>
    public static IAsyncEnumerable<TldrStreamEvent> Simulate(TldrRequest request, bool fail = false, TimeSpan? thinkTime = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SimulateCore(request, fail, thinkTime ?? DefaultThinkTime, paced: true);
    }

    /// <summary>
    /// Turns a finished summary into paced text events, honoring <paramref name="cancellationToken"/>. Use it when a
    /// provider returns whole results but the card should still stream.
    /// </summary>
    /// <param name="text">The finished summary.</param>
    /// <param name="cancellationToken">Stops the stream (pass <see cref="TldrRequest.CancellationToken"/>).</param>
    public static IAsyncEnumerable<TldrStreamEvent> FromResult(string text, CancellationToken cancellationToken) =>
        Paced(Events(text, null), TimeSpan.Zero, paced: true, cancellationToken);

    /// <summary>
    /// Turns finished key points into paced events (a point event, then its text in chunks), honoring
    /// <paramref name="cancellationToken"/>.
    /// </summary>
    /// <param name="points">The key points with their source paragraph ids.</param>
    /// <param name="cancellationToken">Stops the stream (pass <see cref="TldrRequest.CancellationToken"/>).</param>
    public static IAsyncEnumerable<TldrStreamEvent> FromResult(IReadOnlyList<TldrKeyPoint> points, CancellationToken cancellationToken) =>
        Paced(Events(null, points), TimeSpan.Zero, paced: true, cancellationToken);

    /// <summary>The stand-in with an optional pace (tests stream without waiting).</summary>
    internal static IAsyncEnumerable<TldrStreamEvent> SimulateCore(TldrRequest request, bool fail, TimeSpan thinkTime, bool paced) =>
        request.Mode == TldrMode.KeyPoints
            ? Stream(null, SimulatedPoints(request), thinkTime, paced, request.CancellationToken, fail)
            : Stream(SimulatedSummary(request), null, thinkTime, paced, request.CancellationToken, fail);

    /// <summary>A finished summary or list as paced events after <paramref name="thinkTime"/>.</summary>
    internal static IAsyncEnumerable<TldrStreamEvent> Stream(string? text, IReadOnlyList<TldrKeyPoint>? points, TimeSpan thinkTime, bool paced,
        CancellationToken cancellationToken, bool fail) =>
        Paced(Events(text, points), thinkTime, paced, cancellationToken, fail);

    /// <summary>The stand-in summary: lead sentences, one per paragraph in turn, until the length's word target.</summary>
    internal static string SimulatedSummary(TldrRequest request)
    {
        var target = request.TargetWords;
        var builder = new StringBuilder();
        var words = 0;
        // One lead sentence per paragraph, then the next sentences, until the target is reached.
        var sentences = request.Article.Paragraphs.Select(p => Sentences(p.Text)).ToList();
        for (var round = 0; words < target && sentences.Any(s => s.Count > round); round++)
        {
            foreach (var paragraph in sentences)
            {
                if (round >= paragraph.Count || words >= target)
                {
                    continue;
                }
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }
                builder.Append(paragraph[round]);
                words += WordCount(paragraph[round]);
            }
        }
        return builder.ToString();
    }

    /// <summary>The stand-in key points: each paragraph's lead sentence.</summary>
    internal static IReadOnlyList<TldrKeyPoint> SimulatedPoints(TldrRequest request)
    {
        return request.Article.Paragraphs
            .Where(p => !string.IsNullOrWhiteSpace(p.Text))
            .Take(request.PointCount)
            .Select(p => new TldrKeyPoint(p.Id, Sentences(p.Text).FirstOrDefault() ?? p.Text.Trim()))
            .ToArray();
    }

    /// <summary>Splits text into chunks of 3 to 8 words; the chunks joined give the text back exactly.</summary>
    internal static List<string> Chunk(string text, Random random)
    {
        var chunks = new List<string>();
        var start = 0;
        var i = 0;
        while (i < text.Length)
        {
            var want = random.Next(MinChunkWords, MaxChunkWords + 1);
            var words = 0;
            // A chunk is leading spaces plus up to `want` words (each with the spaces before it).
            while (i < text.Length && words < want)
            {
                while (i < text.Length && char.IsWhiteSpace(text[i]))
                {
                    i++;
                }
                if (i >= text.Length)
                {
                    break;
                }
                while (i < text.Length && !char.IsWhiteSpace(text[i]))
                {
                    i++;
                }
                words++;
            }
            if (i > start)
            {
                chunks.Add(text[start..i]);
            }
            start = i;
        }
        return chunks;
    }

    /// <summary>The words in <paramref name="text"/> (runs of non-space characters).</summary>
    internal static int WordCount(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }
        var count = 0;
        var inWord = false;
        foreach (var c in text)
        {
            var space = char.IsWhiteSpace(c);
            if (!space && !inWord)
            {
                count++;
            }
            inWord = !space;
        }
        return count;
    }

    /// <summary>Splits a paragraph into sentences at ". ", "! " and "? ".</summary>
    internal static List<string> Sentences(string text)
    {
        var result = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var end = text[i] is '.' or '!' or '?' && (i + 1 == text.Length || char.IsWhiteSpace(text[i + 1]));
            if (end)
            {
                var sentence = text[start..(i + 1)].Trim();
                if (sentence.Length > 0)
                {
                    result.Add(sentence);
                }
                start = i + 1;
            }
        }
        var rest = text[start..].Trim();
        if (rest.Length > 0)
        {
            result.Add(rest);
        }
        return result;
    }

    private static IEnumerable<TldrStreamEvent> Events(string? text, IReadOnlyList<TldrKeyPoint>? points)
    {
        var random = new Random((text?.Length ?? 0) * 31 + (points?.Count ?? 0) * 17 + 7);
        if (text is not null)
        {
            foreach (var chunk in Chunk(text, random))
            {
                yield return TldrStreamEvent.OfText(chunk);
            }
        }
        if (points is not null)
        {
            foreach (var point in points)
            {
                yield return TldrStreamEvent.OfPoint(point.Source);
                foreach (var chunk in Chunk(point.Text, random))
                {
                    yield return TldrStreamEvent.OfText(chunk);
                }
            }
        }
    }

    private static async IAsyncEnumerable<TldrStreamEvent> Paced(IEnumerable<TldrStreamEvent> events, TimeSpan thinkTime, bool paced,
        [EnumeratorCancellation] CancellationToken cancellationToken, bool fail = false)
    {
        if (thinkTime > TimeSpan.Zero)
        {
            await Task.Delay(thinkTime, cancellationToken).ConfigureAwait(true);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (fail)
        {
            throw new InvalidOperationException("The model did not answer in time.");
        }
        var random = new Random(11);
        var first = true;
        foreach (var item in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // A point marker goes out with the text after it, so only text waits.
            if (paced && !first && item.Kind == TldrStreamEventKind.Text)
            {
                var ms = random.Next((int)MinPace.TotalMilliseconds, (int)MaxPace.TotalMilliseconds + 1);
                await Task.Delay(TimeSpan.FromMilliseconds(ms), cancellationToken).ConfigureAwait(true);
            }
            first = false;
            yield return item;
        }
    }
}
