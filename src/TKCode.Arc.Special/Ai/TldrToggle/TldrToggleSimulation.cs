namespace TKCode.Arc;

/// <summary>
/// An article a <see cref="TldrToggle"/> plays without a summarizer, from data you give it: the article, the model
/// credited, a summary written for each length and key points ranked by weight. Set it as
/// <see cref="TldrToggle.Simulation"/> while the card has no <see cref="TldrToggle.Article"/>: the card shows the article,
/// streams the written summary or points after <see cref="ThinkTime"/> in chunks of 3 to 8 words, says it is simulated
/// (nothing is sent) and, unless <see cref="TldrToggle.AutoPlay"/> is false, opens the summary by itself once it has been
/// half in view for a second.
/// <para>
/// A length without a written summary gets the article's lead sentences, and without <see cref="KeyPoints"/> each
/// paragraph's lead sentence is a point (the same stand-in as <see cref="TldrStreams.Simulate"/>; not a model).
/// </para>
/// </summary>
public sealed class TldrToggleSimulation
{
    /// <summary>The article to show and summarize.</summary>
    public TldrArticle? Article { get; set; }

    /// <summary>The model credited in the footer when the card has no <see cref="TldrToggle.Model"/>.</summary>
    public AgentModel? Model { get; set; }

    /// <summary>
    /// The summaries written for lengths 1 to 5, shortest first (aim for about 35, 60, 85, 100 and 130 words). A length
    /// without one gets the article's lead sentences.
    /// </summary>
    public IReadOnlyList<string>? Summaries { get; set; }

    /// <summary>
    /// The key points, most important first, each naming the paragraph it came from. A list of N (3 to 7, from the length)
    /// takes the first N and shows them in article order.
    /// </summary>
    public IReadOnlyList<TldrKeyPoint>? KeyPoints { get; set; }

    /// <summary>The thinking pause before the first chunk (0.7s by default).</summary>
    public TimeSpan ThinkTime { get; set; } = TldrStreams.DefaultThinkTime;

    /// <summary>
    /// Streams the written summary or key points for <paramref name="request"/> the way the card plays them, after
    /// <see cref="ThinkTime"/>. Use it to wrap the simulation in a summarizer of your own (set
    /// <see cref="TldrToggle.IsSimulated"/> then).
    /// </summary>
    /// <param name="request">The request the card passes to its summarizer.</param>
    /// <param name="fail">Throws after the thinking pause instead, to show the error state.</param>
    public IAsyncEnumerable<TldrStreamEvent> Summarize(TldrRequest request, bool fail = false)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Stream(request, fail, ThinkTime, paced: true);
    }

    /// <summary>The stream with an optional pace (tests stream without waiting).</summary>
    internal IAsyncEnumerable<TldrStreamEvent> Stream(TldrRequest request, bool fail, TimeSpan thinkTime, bool paced) =>
        request.Mode == TldrMode.KeyPoints
            ? TldrStreams.Stream(null, Points(request), thinkTime, paced, request.CancellationToken, fail)
            : TldrStreams.Stream(Summary(request), null, thinkTime, paced, request.CancellationToken, fail);

    /// <summary>The written summary for the request's length, else the lead sentences.</summary>
    internal string Summary(TldrRequest request) =>
        Summaries is { } summaries && request.Length - 1 < summaries.Count && !string.IsNullOrWhiteSpace(summaries[request.Length - 1])
            ? summaries[request.Length - 1]
            : TldrStreams.SimulatedSummary(request);

    /// <summary>The first <see cref="TldrRequest.PointCount"/> ranked points in article order, else the lead sentences.</summary>
    internal IReadOnlyList<TldrKeyPoint> Points(TldrRequest request)
    {
        if (KeyPoints is not { Count: > 0 } ranked)
        {
            return TldrStreams.SimulatedPoints(request);
        }
        var order = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < request.Article.Paragraphs.Count; i++)
        {
            order.TryAdd(request.Article.Paragraphs[i].Id, i);
        }
        return ranked.Take(Math.Clamp(request.PointCount, 1, ranked.Count))
            .OrderBy(p => p.Source is { } source && order.TryGetValue(source, out var index) ? index : int.MaxValue)
            .ToArray();
    }
}
