namespace TKCode.Arc;

/// <summary>
/// Every word a <see cref="PromptCache"/> shows or speaks, in English by default. Replace the instance (or set only the
/// members you need) to translate or reword; sentences with values are functions so their word order can change. Token
/// counts, costs and times arrive already formatted ("48.6K", "$0.0144", "0.48s"). The playback buttons' names live in
/// <see cref="Explainer.PlaybackLabels"/>.
/// </summary>
public sealed class PromptCacheLabels
{
    /// <summary>The card's accessible name; the argument is the title.</summary>
    public Func<string, string> CardName { get; init; } = title => $"{title}, a simulation with sample prices";

    /// <summary>The header's time-to-live note; the argument is the time to live in seconds.</summary>
    public Func<double, string> TtlNote { get; init; } = ttl => $"{PromptCacheMath.DescribeTtl(ttl)} cache";

    /// <summary>The accessible name of the legend.</summary>
    public string LegendName { get; init; } = "Prompt segments";

    /// <summary>The legend entry of the message at the end of every prompt.</summary>
    public string MessageLabel { get; init; } = "New message";

    /// <summary>The key of solid (processed) fills above the requests.</summary>
    public string ProcessedKey { get; init; } = "Processed";

    /// <summary>The key of light (cached) fills above the requests.</summary>
    public string CachedKey { get; init; } = "From cache";

    /// <summary>The heading of the processed tokens column.</summary>
    public string ProcessedColumn { get; init; } = "Processed";

    /// <summary>The heading of the input cost column.</summary>
    public string CostColumn { get; init; } = "Input cost";

    /// <summary>The heading of the first token column.</summary>
    public string FirstTokenColumn { get; init; } = "First token";

    /// <summary>The accessible name of the list of requests.</summary>
    public string RequestsName { get; init; } = "Requests";

    /// <summary>A request's title; the argument is its number from 1.</summary>
    public Func<int, string> RequestTitle { get; init; } = n => $"Request {n}";

    /// <summary>A row's status while it processes tokens.</summary>
    public string Processing { get; init; } = "Processing";

    /// <summary>A row's status while it reads the cached prefix.</summary>
    public string ReadingCache { get; init; } = "Reading cache";

    /// <summary>The outcome of a write.</summary>
    public string CacheWrite { get; init; } = "Cache write";

    /// <summary>The outcome of a hit.</summary>
    public string CacheHit { get; init; } = "Cache hit";

    /// <summary>The outcome of a partial hit.</summary>
    public string PartialHit { get; init; } = "Partial hit";

    /// <summary>The name of the cache panel.</summary>
    public string CacheName { get; init; } = "Cache";

    /// <summary>The panel's status before anything is cached.</summary>
    public string Empty { get; init; } = "Empty";

    /// <summary>The panel's status while a request writes into it.</summary>
    public string Writing { get; init; } = "Writing";

    /// <summary>The panel's status while a request reads from it.</summary>
    public string Reading { get; init; } = "Reading";

    /// <summary>The panel's countdown; the argument is the remaining time as m:ss.</summary>
    public Func<string, string> ExpiresIn { get; init; } = clock => $"Expires in {clock}";

    /// <summary>The panel's status once the time to live ran out.</summary>
    public string Expired { get; init; } = "Expired";

    /// <summary>The line under the panel's bar; the argument is the cached token count.</summary>
    public Func<string, string> TokensCached { get; init; } = tokens => $"{tokens} tokens cached";

    /// <summary>The input price label.</summary>
    public string InputPrice { get; init; } = "Input";

    /// <summary>The cache write price label.</summary>
    public string WritePrice { get; init; } = "Cache write";

    /// <summary>The cache read price label.</summary>
    public string ReadPrice { get; init; } = "Cache read";

    /// <summary>The unit note under the prices.</summary>
    public string PriceUnit { get; init; } = "Per million tokens";

    /// <summary>The cost statistic's label while the cache costs less than no cache.</summary>
    public string LowerCost { get; init; } = "lower input cost";

    /// <summary>The cost statistic's label while the cache costs more (the first write).</summary>
    public string HigherCost { get; init; } = "higher input cost";

    /// <summary>The cost statistic's detail; the arguments are the totals with and without the cache.</summary>
    public Func<string, string, string> CostDetail { get; init; } = (cached, uncached) => $"{cached} instead of {uncached}";

    /// <summary>The cost statistic's detail before any request.</summary>
    public string NoRequests { get; init; } = "No requests yet";

    /// <summary>The speed statistic's label.</summary>
    public string FasterFirstToken { get; init; } = "faster first token";

    /// <summary>The speed statistic's detail; the arguments are the average first token on hits with and without the cache.</summary>
    public Func<string, string, string> SpeedDetail { get; init; } = (cached, uncached) => $"{cached} instead of {uncached} on hits";

    /// <summary>The speed statistic's detail before the first hit.</summary>
    public string NoHits { get; init; } = "No cache hits yet";

    /// <summary>The change toggle; the argument is the segment's label.</summary>
    public Func<string, string> ChangeToggle { get; init; } = segment => $"Edit the {Lower(segment)}";

    /// <summary>The opening caption; the argument is the prefix size.</summary>
    public Func<string, string> Opening { get; init; } =
        prefix => $"Every request starts with the same {prefix}-token prefix. Only the message at the end is new.";

    /// <summary>A write starts; the arguments are the request number and the tokens it processes.</summary>
    public Func<int, string, string> WriteStarted { get; init; } =
        (n, tokens) => $"Nothing is cached yet, so request {n} processes all {tokens} tokens and stores the prefix.";

    /// <summary>A write finished; the arguments are the time to live in words and the write price as a multiple of the input price.</summary>
    public Func<string, string, string> WriteDone { get; init; } =
        (ttl, ratio) => $"Cache write. The prefix is kept for {ttl}; storing it costs {ratio}x the input price.";

    /// <summary>A hit starts; the argument is the request number.</summary>
    public Func<int, string> HitStarted { get; init; } =
        n => $"Request {n} begins with the cached prefix, so it reads it back instead of processing it again.";

    /// <summary>A hit finished; the arguments are the cached tokens, the read price as a multiple of the input price and the new tokens.</summary>
    public Func<string, string, string, string> HitDone { get; init; } =
        (cached, ratio, fresh) => $"Cache hit. {cached} tokens read at {ratio}x the input price; only {fresh} new tokens processed.";

    /// <summary>A partial hit starts; the arguments are the changed segment's label and the request number.</summary>
    public Func<string, int, string> PartialStarted { get; init; } =
        (segment, n) => $"The {Lower(segment)} changed before request {n}, so it matches the cache only up to that point.";

    /// <summary>A partial hit finished; the arguments are the cached tokens, the reprocessed tokens and the changed segment's label.</summary>
    public Func<string, string, string, string> PartialDone { get; init; } =
        (cached, written, segment) => $"Partial hit. {cached} tokens came from the cache; {written} from the {Lower(segment)} on were processed and stored again.";

    /// <summary>The idle caption after the last request; the argument is the time to live in words.</summary>
    public Func<string, string> Idle { get; init; } =
        ttl => $"No more requests. The cache expires {ttl} after its last use.";

    /// <summary>The caption once the cache expired.</summary>
    public string ExpiredCaption { get; init; } = "The cache expired. The next request has to store the prefix again.";

    /// <summary>The change was armed; the argument is the segment's label.</summary>
    public Func<string, string> ChangeArmed { get; init; } =
        segment => $"Edit armed for the {Lower(segment)}: the next request matches the cache only up to that point.";

    /// <summary>The change was armed while nothing is cached; the argument is the segment's label.</summary>
    public Func<string, string> ChangeArmedCold { get; init; } =
        segment => $"Edit armed for the {Lower(segment)}, but nothing is cached: the next request processes everything anyway.";

    /// <summary>The change was released; the argument is the segment's label.</summary>
    public Func<string, string> ChangeReleased { get; init; } =
        segment => $"Edit cleared: the {Lower(segment)} stays as it was.";

    /// <summary>Spoken when the story is paused.</summary>
    public string Paused { get; init; } = "Paused.";

    /// <summary>Spoken when the story restarts.</summary>
    public string Restarted { get; init; } = "Restarted. The cache is empty.";

    /// <summary>Appended to a stepped request's outcome when it is spoken.</summary>
    public string Simulated { get; init; } = "Simulated.";

    /// <summary>A row's description before it is sent; the argument is the request number.</summary>
    public Func<int, string> RowPending { get; init; } = n => $"Request {n}: not sent yet.";

    /// <summary>A row's description once done, from its formatted values.</summary>
    public Func<PromptCacheRowText, string> RowDone { get; init; } = DefaultRowDone;

    /// <summary>The outcome words inside a row's description; the argument is the result and the changed segment's label.</summary>
    public Func<PromptCacheRequestKind, string?, string> Outcome { get; init; } = (kind, segment) => kind switch
    {
        PromptCacheRequestKind.Hit => "cache hit",
        PromptCacheRequestKind.Partial => $"partial hit, changed at the {Lower(segment ?? string.Empty)}",
        _ => "cache write"
    };

    private static string DefaultRowDone(PromptCacheRowText row)
    {
        var message = string.IsNullOrWhiteSpace(row.Message) ? string.Empty : $", “{row.Message}”";
        return $"Request {row.Number} at {row.Time}{message}: {row.Outcome}. {row.CachedTokens} tokens read from the cache, " +
               $"{row.ProcessedTokens} processed. Input cost {row.Cost}, first token after {row.FirstToken}.";
    }

    // "Tool schemas" reads "the tool schemas" inside a sentence; an acronym ("API docs") keeps its case.
    internal static string Lower(string text)
        => text.Length == 0 || (text.Length > 1 && char.IsUpper(text[1])) ? text : char.ToLowerInvariant(text[0]) + text[1..];
}

/// <summary>The formatted values of one finished request, for <see cref="PromptCacheLabels.RowDone"/>.</summary>
/// <param name="Number">The request number from 1.</param>
/// <param name="Time">The simulated time it was sent (m:ss).</param>
/// <param name="Message">Its message, or null.</param>
/// <param name="Outcome">The outcome words from <see cref="PromptCacheLabels.Outcome"/>.</param>
/// <param name="CachedTokens">Tokens read from the cache ("48.4K").</param>
/// <param name="ProcessedTokens">Tokens processed ("240").</param>
/// <param name="Cost">The input cost ("$0.0144").</param>
/// <param name="FirstToken">The first token ("0.48s").</param>
public readonly record struct PromptCacheRowText(int Number, string Time, string? Message, string Outcome, string CachedTokens,
    string ProcessedTokens, string Cost, string FirstToken);
