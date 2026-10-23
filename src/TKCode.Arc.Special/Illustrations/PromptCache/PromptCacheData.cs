using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One part of the stable prompt prefix of a <see cref="PromptCache"/> (tool schemas, instructions, documents, the chat so
/// far). Segments are matched against the cache from left to right, so put what changes least first.
/// </summary>
public class PromptCacheSegment : AvaloniaObject
{
    /// <summary>Defines the <see cref="Id"/> property.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<PromptCacheSegment, string>(nameof(Id), string.Empty);

    /// <summary>Defines the <see cref="Label"/> property.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<PromptCacheSegment, string>(nameof(Label), string.Empty);

    /// <summary>Defines the <see cref="Tokens"/> property.</summary>
    public static readonly StyledProperty<int> TokensProperty =
        AvaloniaProperty.Register<PromptCacheSegment, int>(nameof(Tokens));

    /// <summary>Defines the <see cref="Color"/> property.</summary>
    public static readonly StyledProperty<IBrush?> ColorProperty =
        AvaloniaProperty.Register<PromptCacheSegment, IBrush?>(nameof(Color));

    /// <summary>Creates an empty segment.</summary>
    public PromptCacheSegment()
    {
    }

    /// <summary>Creates a segment with an id, a label and its size in tokens.</summary>
    public PromptCacheSegment(string id, string label, int tokens)
    {
        Id = id;
        Label = label;
        Tokens = tokens;
    }

    /// <summary>A stable key, used by <see cref="PromptCache.ChangeSegmentId"/> and in request results.</summary>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <summary>The name in the legend, the change toggle and the spoken descriptions ("Tool schemas").</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The size in tokens. Bars use a square root scale, so a small segment stays visible beside a large one.</summary>
    public int Tokens { get => GetValue(TokensProperty); set => SetValue(TokensProperty, value); }

    /// <summary>The segment's color; null takes the next chart series color (series 3, 2, 4, 1, then again).</summary>
    public IBrush? Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
}

/// <summary>One request of a <see cref="PromptCache"/> round: the prefix plus a new message at the end.</summary>
public class PromptCacheRequest : AvaloniaObject
{
    /// <summary>Defines the <see cref="Message"/> property.</summary>
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<PromptCacheRequest, string?>(nameof(Message));

    /// <summary>Defines the <see cref="Tokens"/> property.</summary>
    public static readonly StyledProperty<int> TokensProperty =
        AvaloniaProperty.Register<PromptCacheRequest, int>(nameof(Tokens));

    /// <summary>Defines the <see cref="Gap"/> property.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<PromptCacheRequest, double>(nameof(Gap));

    /// <summary>Creates an empty request.</summary>
    public PromptCacheRequest()
    {
    }

    /// <summary>Creates a request with its message, the message's size in tokens and the simulated seconds since the one before.</summary>
    public PromptCacheRequest(string? message, int tokens, double gap = 0)
    {
        Message = message;
        Tokens = tokens;
        Gap = gap;
    }

    /// <summary>The new message; it is read out in the row's description and passed in the result, never drawn.</summary>
    public string? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    /// <summary>The size of the new message in tokens: the part of the prompt that is never cached.</summary>
    public int Tokens { get => GetValue(TokensProperty); set => SetValue(TokensProperty, value); }

    /// <summary>Simulated seconds since the request before; ignored for the first request of a round.</summary>
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }
}

/// <summary>
/// The prices behind every cost a <see cref="PromptCache"/> shows, in dollars per million input tokens. The defaults are
/// sample prices that follow a common pattern: cache writes at 1.25 times the input price and cache reads at a tenth of it.
/// Pass your provider's current prices; set <see cref="CacheWritePrice"/> equal to <see cref="InputPrice"/> when writes
/// cost nothing extra.
/// </summary>
public sealed class PromptCachePricing
{
    /// <summary>Dollars per million uncached input tokens (default 3.00).</summary>
    public double InputPrice { get; set; } = 3.00;

    /// <summary>Dollars per million tokens written to the cache (default 3.75).</summary>
    public double CacheWritePrice { get; set; } = 3.75;

    /// <summary>Dollars per million tokens read from the cache (default 0.30).</summary>
    public double CacheReadPrice { get; set; } = 0.30;
}

/// <summary>
/// Estimates the seconds to the first output token of a prompt with <paramref name="processedTokens"/> new tokens and
/// <paramref name="cachedTokens"/> tokens loaded from the cache.
/// </summary>
public delegate double PromptCacheFirstTokenEstimator(int processedTokens, int cachedTokens);

/// <summary>What a request of a <see cref="PromptCache"/> did with the cache.</summary>
public enum PromptCacheRequestKind
{
    /// <summary>Nothing usable was cached: the whole prompt was processed and the prefix written to the cache.</summary>
    Write,

    /// <summary>The whole prefix matched the cache and was read back; only the new message was processed.</summary>
    Hit,

    /// <summary>A changed segment broke the match: the prefix before it was read, the rest processed and written again.</summary>
    Partial
}

/// <summary>The outcome of one simulated request of a <see cref="PromptCache"/>.</summary>
public sealed class PromptCacheResult
{
    /// <summary>The request's position in its round, from 0.</summary>
    public int Index { get; init; }

    /// <summary>The simulated seconds since the round started when the request was sent.</summary>
    public double Time { get; init; }

    /// <summary>Write, hit or partial hit.</summary>
    public PromptCacheRequestKind Kind { get; init; }

    /// <summary>For a partial hit, the index of the first segment that did not match; otherwise null.</summary>
    public int? BreakIndex { get; init; }

    /// <summary>For a partial hit, the id of the first segment that did not match; otherwise null.</summary>
    public string? BreakSegmentId { get; init; }

    /// <summary>Tokens read from the cache.</summary>
    public int CachedTokens { get; init; }

    /// <summary>Tokens written to the cache.</summary>
    public int WrittenTokens { get; init; }

    /// <summary>Tokens processed: everything not read from the cache, the new message included.</summary>
    public int ProcessedTokens { get; init; }

    /// <summary>Input cost in dollars with the cache.</summary>
    public double Cost { get; init; }

    /// <summary>Input cost in dollars of the same prompt without any cache.</summary>
    public double UncachedCost { get; init; }

    /// <summary>Estimated seconds to the first output token.</summary>
    public double FirstToken { get; init; }

    /// <summary>Estimated seconds to the first output token without any cache.</summary>
    public double UncachedFirstToken { get; init; }

    /// <summary>True when the change toggle was armed for this request.</summary>
    public bool IsChanged { get; init; }

    /// <summary>True when the cache was empty or had expired, so the request had to write the prefix.</summary>
    public bool IsCold { get; init; }

    /// <summary>The request's message.</summary>
    public string? Message { get; init; }
}

/// <summary>Arguments of <see cref="PromptCache.RequestCompleted"/>: a simulated request finished.</summary>
public class PromptCacheRequestCompletedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The prompt cache control.</param>
    /// <param name="result">The request's outcome.</param>
    public PromptCacheRequestCompletedEventArgs(RoutedEvent routedEvent, object source, PromptCacheResult result)
        : base(routedEvent, source)
    {
        Result = result;
    }

    /// <summary>The request's outcome.</summary>
    public PromptCacheResult Result { get; }
}
