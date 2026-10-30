namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="ReasoningBlock"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; the times are format functions.
/// </summary>
public sealed class ReasoningBlockLabels
{
    /// <summary>The English defaults.</summary>
    public static ReasoningBlockLabels Default { get; } = new();

    /// <summary>The label while the model thinks.</summary>
    public string Thinking { get; init; } = "Thinking";

    /// <summary>The label once it has finished ("Thought for 7s").</summary>
    public string Thought { get; init; } = "Thought";

    /// <summary>The label of a finished block whose time is not known.</summary>
    public string ThoughtProcess { get; init; } = "Thought process";

    /// <summary>The label of a block the person stopped ("Stopped after 4s").</summary>
    public string Stopped { get; init; } = "Stopped";

    /// <summary>The label of a block whose thinking failed.</summary>
    public string Interrupted { get; init; } = "Thinking interrupted";

    /// <summary>The time after "Thinking" and "Thought", from the whole seconds: "for 7s", "for 1m 5s".</summary>
    public Func<long, string> Duration { get; init; } = seconds => "for " + Time(seconds);

    /// <summary>The time after "Stopped", from the whole seconds: "after 4s".</summary>
    public Func<long, string> After { get; init; } = seconds => "after " + Time(seconds);

    /// <summary>The footer of a finished block.</summary>
    public string Done { get; init; } = "Done";

    /// <summary>The footer of a stopped block.</summary>
    public string StoppedStatus { get; init; } = "Stopped";

    /// <summary>The Stop button while the model thinks.</summary>
    public string Stop { get; init; } = "Stop";

    /// <summary>The Retry button of a block whose thinking failed.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The name of the copy button.</summary>
    public string CopyReasoning { get; init; } = "Copy reasoning";

    /// <summary>The name of the head button when the reasoning is closed (it opens the steps).</summary>
    public string Show { get; init; } = "Show reasoning";

    /// <summary>The name of the head button when the reasoning is open.</summary>
    public string Hide { get; init; } = "Hide reasoning";

    private static string Time(long seconds) =>
        seconds < 60 ? AiDuration.Seconds(seconds) : AiDuration.MinutesSeconds(seconds);
}
