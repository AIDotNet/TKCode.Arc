namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="TldrToggle"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; counts and names go through format functions.
/// </summary>
public sealed class TldrToggleLabels
{
    /// <summary>The English defaults.</summary>
    public static TldrToggleLabels Default { get; } = new();

    /// <summary>The mode switch's segment for the article.</summary>
    public string Full { get; init; } = "Full";

    /// <summary>The mode switch's segment for the summary.</summary>
    public string Summary { get; init; } = "Summary";

    /// <summary>The mode switch's segment for the key points.</summary>
    public string KeyPoints { get; init; } = "Key points";

    /// <summary>The name of the mode switch.</summary>
    public string ModeGroup { get; init; } = "Reading mode";

    /// <summary>The length slider's label.</summary>
    public string Length { get; init; } = "Length";

    /// <summary>The length slider's value, shown and spoken: "~60 words" for a summary, "4 points" for key points.</summary>
    public Func<TldrMode, int, string> LengthValue { get; init; } = static (mode, length) => mode == TldrMode.KeyPoints
        ? TldrToggle.PointCountFor(length) + " points"
        : "~" + TldrToggle.TargetWordsFor(length) + " words";

    /// <summary>The worked-out reading time from whole minutes: "5 min read".</summary>
    public Func<int, string> ReadingTime { get; init; } = static minutes => minutes + " min read";

    /// <summary>The shimmering line in the body while the model thinks.</summary>
    public string Reading { get; init; } = "Reading the article";

    /// <summary>The footer while the model thinks.</summary>
    public string Thinking { get; init; } = "Thinking";

    /// <summary>The footer's shimmering label while text arrives.</summary>
    public string Writing { get; init; } = "Writing";

    /// <summary>The footer in Full mode.</summary>
    public string FullArticle { get; init; } = "Full article";

    /// <summary>The footer of a finished summary, from its words: "58 words".</summary>
    public Func<int, string> Words { get; init; } = static words => words == 1 ? "1 word" : words + " words";

    /// <summary>The footer of a finished list, from its points: "4 key points".</summary>
    public Func<int, string> Points { get; init; } = static points => points == 1 ? "1 key point" : points + " key points";

    /// <summary>The footer of a stopped result.</summary>
    public string Stopped { get; init; } = "Stopped";

    /// <summary>The footer of a failed result.</summary>
    public string Failed { get; init; } = "Failed";

    /// <summary>The Stop button while a result streams.</summary>
    public string Stop { get; init; } = "Stop";

    /// <summary>The name and tooltip of the regenerate button.</summary>
    public string Regenerate { get; init; } = "Regenerate";

    /// <summary>The name of the copy button.</summary>
    public string Copy { get; init; } = "Copy";

    /// <summary>The button of a failed result.</summary>
    public string TryAgain { get; init; } = "Try again";

    /// <summary>The message of a failure that carries none.</summary>
    public string FailedMessage { get; init; } = "The summary could not be made.";

    /// <summary>The body of a result stopped before any text arrived.</summary>
    public string StoppedEarly { get; init; } = "Stopped before any text arrived.";

    /// <summary>The body of an article with no paragraphs.</summary>
    public string Empty { get; init; } = "This article has no text to summarize yet.";

    /// <summary>The name of a key point's jump link, from the paragraph's number and the point: "Jump to paragraph 3: …".</summary>
    public Func<int, string, string> JumpTo { get; init; } = static (number, point) => "Jump to paragraph " + number + ": " + point;

    /// <summary>The tooltip of a key point's jump link: "Jump to paragraph 3".</summary>
    public Func<int, string> JumpTip { get; init; } = static number => "Jump to paragraph " + number;

    /// <summary>Announced when a run starts, from the model's name: "Summarizing with" and the name, or "Summarizing" without one.</summary>
    public Func<string, string> Summarizing { get; init; } = static model => string.IsNullOrWhiteSpace(model) ? "Summarizing" : "Summarizing with " + model;

    /// <summary>Announced when a summary is done, from its words: "Summary ready, 58 words".</summary>
    public Func<int, string> SummaryReady { get; init; } = static words => "Summary ready, " + words + " words";

    /// <summary>Announced when a list is done, from its points: "4 key points ready".</summary>
    public Func<int, string> PointsReady { get; init; } = static points => points + " key points ready";

    /// <summary>Added to announcements and error text while the built-in simulation runs.</summary>
    public string Simulated { get; init; } = "Simulated, nothing was sent";
}
