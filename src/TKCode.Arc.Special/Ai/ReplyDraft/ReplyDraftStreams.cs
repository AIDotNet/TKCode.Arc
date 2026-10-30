using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace TKCode.Arc;

/// <summary>
/// Streams for <see cref="ReplyDraft"/>: a wrapper that streams a whole text locally and the chunker it and
/// <see cref="ReplyDraftSimulation"/> use (3 to 8 words every 34ms). Nothing is sent.
/// </summary>
public static partial class ReplyDraftStreams
{
    /// <summary>The time between two chunks.</summary>
    internal static readonly TimeSpan ChunkInterval = TimeSpan.FromMilliseconds(34);

    /// <summary>How long a simulation "reads" before a fresh draft starts.</summary>
    internal static readonly TimeSpan ReadDelay = TimeSpan.FromMilliseconds(480);

    /// <summary>How long a simulated rewrite takes before its text arrives.</summary>
    internal static readonly TimeSpan RewriteDelay = TimeSpan.FromMilliseconds(560);

    /// <summary>
    /// Wraps a writer that returns the whole reply at once: its text is streamed locally in chunks of 3 to 8 words every
    /// 34ms, so the card looks the same as with a real stream.
    /// </summary>
    /// <param name="write">Returns the whole reply for a request.</param>
    public static ReplyDraftGenerator FromText(Func<ReplyDraftRequest, Task<string>> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        return request => FromTextCore(write, request);
    }

    private static async IAsyncEnumerable<string> FromTextCore(Func<ReplyDraftRequest, Task<string>> write, ReplyDraftRequest request)
    {
        var text = await write(request).ConfigureAwait(true);
        await foreach (var chunk in Chunk(text, request.CancellationToken).ConfigureAwait(true))
        {
            yield return chunk;
        }
    }

    /// <summary>Streams <paramref name="text"/> in chunks of 3 to 8 words (with their spaces) every 34ms.</summary>
    /// <param name="text">The whole text.</param>
    /// <param name="cancellationToken">Stops the stream.</param>
    public static async IAsyncEnumerable<string> Chunk(string? text, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var first = true;
        foreach (var chunk in Split(text))
        {
            if (!first)
            {
                await Task.Delay(ChunkInterval, cancellationToken).ConfigureAwait(true);
            }
            first = false;
            cancellationToken.ThrowIfCancellationRequested();
            yield return chunk;
        }
    }

    /// <summary>Cuts a text into chunks of 3 to 8 words, each with the whitespace that follows it (deterministic).</summary>
    internal static IReadOnlyList<string> Split(string? text)
    {
        var chunks = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            return chunks;
        }
        var words = Words().Matches(text);
        var random = new Random(text.Length);
        var index = 0;
        while (index < words.Count)
        {
            var take = Math.Min(random.Next(3, 9), words.Count - index);
            // The first chunk keeps any leading whitespace so the chunks add up to the text.
            var start = index == 0 ? 0 : words[index].Index;
            var last = words[index + take - 1];
            chunks.Add(text[start..(last.Index + last.Length)]);
            index += take;
        }
        return chunks;
    }

    /// <summary>The first word of a name ("Sam" from "Sam Lee"), or the whole name.</summary>
    internal static string FirstName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        var space = trimmed.IndexOf(' ');
        return space > 0 ? trimmed[..space] : trimmed;
    }

    /// <summary>The number of words in a text.</summary>
    internal static int WordCount(string? text) => string.IsNullOrWhiteSpace(text) ? 0 : Words().Matches(text).Count;

    [GeneratedRegex(@"\S+\s*")]
    private static partial Regex Words();
}
