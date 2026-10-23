using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The story times of one request of a <see cref="PromptCache"/>, in story seconds (speed 1), all derived from its start.
/// Processing sweeps the segments that need it one after another at a steady speed; a hit fades the prefix to its cached
/// fill under one passing band; a write or partial hit then carries the new part into the cache (the flight), lands and
/// finishes. Under reduced motion every time equals the start.
/// </summary>
internal sealed class PromptCacheRequestPlan
{
    /// <summary>Write, hit or partial hit.</summary>
    public PromptCacheRequestKind Kind { get; init; }

    /// <summary>The first changed segment of a partial hit.</summary>
    public int? Break { get; init; }

    /// <summary>When the request is sent.</summary>
    public double Start { get; init; }

    /// <summary>When it finishes (its numbers roll and its status lands).</summary>
    public double Done { get; init; }

    /// <summary>Segments before this index are read from the cache (fade to the cached fill under the band).</summary>
    public int CachedTo { get; init; }

    /// <summary>Segments from this index on are processed by the sweep (the segment count when none are).</summary>
    public int SweepFrom { get; init; }

    /// <summary>The start of each prefix segment's processing sweep (NaN for segments that are not processed).</summary>
    public double[] SweepStarts { get; init; } = [];

    /// <summary>The length of each prefix segment's sweep.</summary>
    public double[] SweepDurations { get; init; } = [];

    /// <summary>When the new message starts filling, and for how long.</summary>
    public double MessageStart { get; init; }

    public double MessageDuration { get; init; }

    /// <summary>When the band starts to pass over the cached segments (NaN without cached segments).</summary>
    public double BandStart { get; init; } = double.NaN;

    /// <summary>The flight of the written part into the cache (NaN for a hit), and when it lands.</summary>
    public double FlightStart { get; init; } = double.NaN;

    public double Land { get; init; } = double.NaN;

    /// <summary>When the break marker of a partial hit rises (NaN otherwise).</summary>
    public double MarkStart { get; init; } = double.NaN;

    /// <summary>When the cache panel turns to Writing (writes: the flight; partial hits: early, as it drops the changed part).</summary>
    public double PanelWritingAt { get; init; } = double.NaN;

    /// <summary>When a hit refreshes the cache's time to live (the ring refills from there).</summary>
    public double RefreshAt { get; init; } = double.NaN;

    /// <summary>True for a write or partial hit: something is written into the cache.</summary>
    public bool Writes => Kind != PromptCacheRequestKind.Hit;
}

/// <summary>The pacing of a <see cref="PromptCache"/> story (measured at speed 1) and the request planner.</summary>
internal static class PromptCacheTimeline
{
    /// <summary>A full bar (prefix and message slot) is processed in this many seconds at a steady speed.</summary>
    public const double SweepSeconds = 1.1;

    /// <summary>The new message fills in this long after the prefix.</summary>
    public const double MessageSeconds = 0.12;

    /// <summary>The written part flies into the cache in this long (strong ease-out).</summary>
    public const double FlightSeconds = 0.46;

    /// <summary>The cache panel takes the written part this long after the flight starts.</summary>
    public const double LandAfter = 0.45;

    /// <summary>A write or partial hit finishes this long after landing.</summary>
    public const double DoneAfterLand = 0.067;

    /// <summary>A hit finishes this long after it starts.</summary>
    public const double HitSeconds = 0.584;

    /// <summary>A hit's message starts filling this long after the start.</summary>
    public const double HitMessageDelay = 0.28;

    /// <summary>The band passes over the cached prefix in this long (symmetric in-out).</summary>
    public const double BandSeconds = 0.52;

    /// <summary>The band is this share of the prefix wide.</summary>
    public const double BandWidth = 0.34;

    /// <summary>Cached fills fade in over this long.</summary>
    public const double CachedFadeSeconds = 0.16;

    /// <summary>A hit refreshes the time to live this long after it starts.</summary>
    public const double RefreshDelay = 0.19;

    /// <summary>A partial hit starts processing the changed part this long after it starts.</summary>
    public const double PartialSweepDelay = 0.24;

    /// <summary>The break marker of a partial hit rises this long after the start, over <see cref="MarkSeconds"/>.</summary>
    public const double MarkDelay = 0.14;

    public const double MarkSeconds = 0.2;

    /// <summary>A partial hit turns the panel to Writing and drops the changed part this long after the start.</summary>
    public const double PartialPanelDelay = 0.134;

    /// <summary>Cache segments close over this long, the rightmost first, each next one later by <see cref="CloseStagger"/>.</summary>
    public const double CloseSeconds = 0.18;

    public const double CloseStagger = 0.03;

    /// <summary>Numbers roll to their new value over this long (ease-out cubic).</summary>
    public const double RollSeconds = 0.42;

    /// <summary>A new round's rows fade out over this long.</summary>
    public const double RowFadeSeconds = 0.24;

    /// <summary>The pause before the first request of the story.</summary>
    public const double OpeningSeconds = 0.704;

    /// <summary>The pause after a request before the simulated clock moves on.</summary>
    public const double DwellSeconds = 0.9;

    /// <summary>The pause after a looping restart (the first request is already written) before the clock moves on.</summary>
    public const double ContinueSeconds = 0.235;

    /// <summary>The simulated gap between two requests passes in this long, on an S curve.</summary>
    public const double GapSeconds = 0.555;

    /// <summary>The pause after the last request before the idle cache starts to drain.</summary>
    public const double IdleSeconds = 0.9;

    /// <summary>A whole time to live drains in this long on the accelerated idle clock.</summary>
    public const double DrainSeconds = 4.2;

    /// <summary>Reduced motion: the pause before the first request, between requests, before expiring, and after expiring.</summary>
    public const double ReducedOpening = 0.7;

    public const double ReducedBetween = 1.4;

    public const double ReducedIdle = 1.4;

    public const double ReducedExpired = 1.7;

    /// <summary>The S curve of the gap clock.</summary>
    public static double SmoothStep(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }

    /// <summary>Ease-out cubic (the number rolls).</summary>
    public static double EaseOutCubic(double t)
    {
        t = Math.Clamp(t, 0, 1);
        var u = 1 - t;
        return 1 - u * u * u;
    }

    /// <summary>The square root growth of each segment (bar widths use it).</summary>
    public static double[] Grows(IReadOnlyList<int> tokens)
    {
        var grows = new double[tokens.Count];
        for (var i = 0; i < grows.Length; i++)
        {
            grows[i] = Math.Sqrt(Math.Max(0, tokens[i]));
        }
        return grows;
    }

    /// <summary>
    /// Each prefix segment's sweep time: <see cref="SweepSeconds"/> spread over the whole row bar (prefix and message slot)
    /// in proportion to the segments' growth, so the sweep runs at one steady speed.
    /// </summary>
    public static double[] SweepDurations(IReadOnlyList<double> grows, double messageGrow)
    {
        double total = messageGrow;
        foreach (var g in grows)
        {
            total += g;
        }
        var durations = new double[grows.Count];
        if (total <= 0)
        {
            return durations;
        }
        for (var i = 0; i < durations.Length; i++)
        {
            durations[i] = SweepSeconds * grows[i] / total;
        }
        return durations;
    }

    /// <summary>Plans one request that starts at <paramref name="start"/>.</summary>
    public static PromptCacheRequestPlan Plan(PromptCacheRequestKind kind, int? breakIndex, IReadOnlyList<double> sweepDurations,
        double start, bool full)
    {
        var n = sweepDurations.Count;
        var starts = new double[n];
        Array.Fill(starts, double.NaN);
        if (!full)
        {
            var cachedTo = kind switch
            {
                PromptCacheRequestKind.Hit => n,
                PromptCacheRequestKind.Partial => breakIndex ?? 0,
                _ => 0
            };
            for (var i = cachedTo; i < n; i++)
            {
                starts[i] = start;
            }
            return new PromptCacheRequestPlan
            {
                Kind = kind,
                Break = kind == PromptCacheRequestKind.Partial ? breakIndex : null,
                Start = start,
                Done = start,
                CachedTo = cachedTo,
                SweepFrom = cachedTo,
                SweepStarts = starts,
                SweepDurations = new double[n],
                MessageStart = start,
                MessageDuration = 0,
                BandStart = cachedTo > 0 ? start : double.NaN,
                FlightStart = kind == PromptCacheRequestKind.Hit ? double.NaN : start,
                Land = kind == PromptCacheRequestKind.Hit ? double.NaN : start,
                MarkStart = kind == PromptCacheRequestKind.Partial ? start : double.NaN,
                PanelWritingAt = kind == PromptCacheRequestKind.Hit ? double.NaN : start,
                RefreshAt = start
            };
        }
        var durations = sweepDurations.ToArray();
        switch (kind)
        {
            case PromptCacheRequestKind.Hit:
                return new PromptCacheRequestPlan
                {
                    Kind = kind,
                    Start = start,
                    Done = start + HitSeconds,
                    CachedTo = n,
                    SweepFrom = n,
                    SweepStarts = starts,
                    SweepDurations = durations,
                    MessageStart = start + HitMessageDelay,
                    MessageDuration = MessageSeconds,
                    BandStart = n > 0 ? start : double.NaN,
                    RefreshAt = start + RefreshDelay
                };
            case PromptCacheRequestKind.Partial:
            {
                var k = Math.Clamp(breakIndex ?? 0, 0, n);
                var t = start + PartialSweepDelay;
                for (var i = k; i < n; i++)
                {
                    starts[i] = t;
                    t += durations[i];
                }
                var flight = t;
                var land = flight + LandAfter;
                return new PromptCacheRequestPlan
                {
                    Kind = kind,
                    Break = k,
                    Start = start,
                    Done = land + DoneAfterLand,
                    CachedTo = k,
                    SweepFrom = k,
                    SweepStarts = starts,
                    SweepDurations = durations,
                    MessageStart = t,
                    MessageDuration = MessageSeconds,
                    BandStart = k > 0 ? start : double.NaN,
                    FlightStart = flight,
                    Land = land,
                    MarkStart = start + MarkDelay,
                    PanelWritingAt = start + PartialPanelDelay,
                    RefreshAt = land
                };
            }
            default:
            {
                var t = start;
                for (var i = 0; i < n; i++)
                {
                    starts[i] = t;
                    t += durations[i];
                }
                var land = t + LandAfter;
                return new PromptCacheRequestPlan
                {
                    Kind = kind,
                    Start = start,
                    Done = land + DoneAfterLand,
                    CachedTo = 0,
                    SweepFrom = 0,
                    SweepStarts = starts,
                    SweepDurations = durations,
                    MessageStart = t,
                    MessageDuration = MessageSeconds,
                    FlightStart = t,
                    Land = land,
                    PanelWritingAt = t,
                    RefreshAt = land
                };
            }
        }
    }

    /// <summary>The longest a request can take (a write: the whole sweep, the flight and the landing).</summary>
    public static double LongestRequest => SweepSeconds + LandAfter + DoneAfterLand + PartialSweepDelay;

    /// <summary>Story seconds the idle clock takes to drain the <paramref name="remaining"/> simulated seconds of a <paramref name="ttl"/>.</summary>
    public static double DrainTime(double remaining, double ttl)
        => ttl > 0 ? DrainSeconds * Math.Clamp(remaining, 0, ttl) / ttl : 0;

    /// <summary>
    /// Lays segments out in a bar of <paramref name="width"/> starting at <paramref name="x"/>: a square root scale with
    /// <paramref name="gap"/> between segments; with a positive <paramref name="messageGrow"/> the bar ends with a message
    /// slot after one more gap (the row bars), whose rectangle is returned (empty otherwise).
    /// </summary>
    public static Rect Layout(IReadOnlyList<double> grows, int from, int to, double messageGrow, double x, double y, double width,
        double height, double gap, Span<Rect> segments)
    {
        double prefixGrow = 0;
        for (var i = from; i < to; i++)
        {
            prefixGrow += grows[i];
        }
        var count = Math.Max(0, to - from);
        var prefixWidth = width;
        var message = default(Rect);
        if (messageGrow > 0)
        {
            var share = prefixGrow + messageGrow > 0 ? prefixGrow / (prefixGrow + messageGrow) : 0;
            prefixWidth = Math.Max(0, width - gap) * share;
            var messageX = x + prefixWidth + (count > 0 ? gap : 0);
            message = new Rect(messageX, y, Math.Max(0, x + width - messageX), height);
        }
        var available = Math.Max(0, prefixWidth - gap * Math.Max(0, count - 1));
        var cursor = x;
        for (var i = from; i < to; i++)
        {
            var w = prefixGrow > 0 ? available * grows[i] / prefixGrow : (count > 0 ? available / count : 0);
            segments[i - from] = new Rect(cursor, y, w, height);
            cursor += w + gap;
        }
        return message;
    }
}
