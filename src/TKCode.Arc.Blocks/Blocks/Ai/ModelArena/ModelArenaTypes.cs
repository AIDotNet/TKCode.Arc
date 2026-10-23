using Avalonia;

namespace TKCode.Arc;

/// <summary>Which of the two answers: the left one (A) or the right one (B).</summary>
public enum ArenaSlot
{
    /// <summary>The first answer.</summary>
    A,

    /// <summary>The second answer.</summary>
    B
}

/// <summary>What the person chose in a vote.</summary>
public enum ArenaChoice
{
    /// <summary>The first answer is better.</summary>
    A,

    /// <summary>The second answer is better.</summary>
    B,

    /// <summary>The answers are equally good.</summary>
    Tie,

    /// <summary>Both answers are bad.</summary>
    BothBad
}

/// <summary>Where a round of the arena is.</summary>
public enum ArenaPhase
{
    /// <summary>Nothing has run yet, or the round was reset.</summary>
    Idle,

    /// <summary>At least one model is thinking or writing.</summary>
    Running,

    /// <summary>Both answers are in; the vote is open.</summary>
    Voting,

    /// <summary>The person voted; the models are revealed.</summary>
    Voted,

    /// <summary>The round was stopped before both answers finished.</summary>
    Stopped,

    /// <summary>At least one model failed and the round cannot be voted on.</summary>
    Failed
}

/// <summary>Where one of the two answers is.</summary>
public enum ArenaPaneStatus
{
    /// <summary>Nothing has run.</summary>
    Idle,

    /// <summary>Waiting for the first text.</summary>
    Thinking,

    /// <summary>Text is arriving.</summary>
    Writing,

    /// <summary>The answer is complete (and the other one finished first or at the same time).</summary>
    Done,

    /// <summary>The answer is complete and it was the first to finish.</summary>
    FinishedFirst,

    /// <summary>The run was stopped before this answer was complete.</summary>
    Stopped,

    /// <summary>The stream failed.</summary>
    Failed
}

/// <summary>A sample prompt: a chip that fills the box and runs.</summary>
public class ArenaPrompt : AvaloniaObject
{
    /// <summary>The prompt's id.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<ArenaPrompt, string>(nameof(Id), string.Empty);

    /// <summary>The chip's text.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArenaPrompt, string>(nameof(Label), string.Empty);

    /// <summary>The whole prompt sent to both models.</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<ArenaPrompt, string>(nameof(Text), string.Empty);

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
}

/// <summary>What one model's answer cost and how fast it came, measured on the raw stream.</summary>
/// <param name="TimeToFirstToken">From the start of the run to the first text; null until it arrives.</param>
/// <param name="TokensPerSecond">Output tokens per second from the first text to the last; null before two samples exist.</param>
/// <param name="Total">From the start of the run to the end of the stream; null while it runs.</param>
/// <param name="InputTokens">Input tokens reported by the runner, or the prompt's length divided by four.</param>
/// <param name="OutputTokens">Output tokens reported by the runner, or the answer's length in characters divided by four.</param>
/// <param name="CostUsd">Input and output tokens times the model's prices per million tokens (zero when the model has no price).</param>
/// <param name="IsEstimated">True when the runner reported no token counts and they were estimated.</param>
public sealed record ArenaMetrics(
    TimeSpan? TimeToFirstToken,
    double? TokensPerSecond,
    TimeSpan? Total,
    int InputTokens,
    int OutputTokens,
    decimal CostUsd,
    bool IsEstimated)
{
    /// <summary>The metrics before anything arrived.</summary>
    public static ArenaMetrics Empty { get; } = new(null, null, null, 0, 0, 0m, true);
}

/// <summary>A vote: what was asked, who answered, what the person chose and what each answer cost.</summary>
/// <param name="Round">The round's number.</param>
/// <param name="Prompt">The prompt that was sent.</param>
/// <param name="ModelA">The model that answered in slot A.</param>
/// <param name="ModelB">The model that answered in slot B.</param>
/// <param name="Choice">The choice.</param>
/// <param name="Winner">The winning model; null for a tie or both bad.</param>
/// <param name="MetricsA">Slot A's metrics.</param>
/// <param name="MetricsB">Slot B's metrics.</param>
public sealed record ArenaVote(
    int Round,
    string Prompt,
    AiModel ModelA,
    AiModel ModelB,
    ArenaChoice Choice,
    AiModel? Winner,
    ArenaMetrics MetricsA,
    ArenaMetrics MetricsB);

/// <summary>A model's record in the standings.</summary>
/// <param name="Wins">Rounds won.</param>
/// <param name="Ties">Rounds tied.</param>
/// <param name="Losses">Rounds lost (a "both bad" vote counts as a loss for both).</param>
public readonly record struct ArenaRecord(int Wins, int Ties, int Losses)
{
    /// <summary>All rounds.</summary>
    public int Games => Wins + Ties + Losses;

    /// <summary>Wins plus half the ties, over all rounds; zero before the first round.</summary>
    public double WinRate => Games == 0 ? 0 : (Wins + 0.5 * Ties) / Games;
}

/// <summary>How the two answers are shown: one shared clock releases whole words, whatever the real stream's speed.</summary>
/// <param name="Interval">The shortest time between two releases (34 ms; at 60 Hz that lands on every third frame).</param>
/// <param name="WordsPerSecond">The reading speed (95 words a second).</param>
/// <param name="MaxWordsPerStep">The most whole words one release shows (8); a backlog is caught up at this rate.</param>
public sealed record ArenaPacing(TimeSpan Interval, double WordsPerSecond, int MaxWordsPerStep)
{
    /// <summary>Arc's measured cadence.</summary>
    public static ArenaPacing Default { get; } = new(TimeSpan.FromMilliseconds(34), 95, 8);
}
