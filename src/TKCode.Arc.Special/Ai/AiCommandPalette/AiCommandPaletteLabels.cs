namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="AiCommandPalette"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; the texts that carry a value are format functions.
/// </summary>
public sealed class AiCommandPaletteLabels
{
    /// <summary>The English defaults.</summary>
    public static AiCommandPaletteLabels Default { get; } = new();

    /// <summary>The accessible name of the palette.</summary>
    public string PaletteName { get; init; } = "Command palette";

    /// <summary>The input's placeholder in search mode.</summary>
    public string SearchPlaceholder { get; init; } = "Search pages and actions";

    /// <summary>The input's placeholder in Ask AI mode.</summary>
    public string AskPlaceholder { get; init; } = "Ask anything about your workspace";

    /// <summary>The search half of the mode toggle.</summary>
    public string SearchMode { get; init; } = "Search";

    /// <summary>The Ask AI half of the mode toggle.</summary>
    public string AskMode { get; init; } = "Ask AI";

    /// <summary>The row that asks the model about the query: "Ask AI about “billing”".</summary>
    public Func<string, string> AskAbout { get; init; } = query => "Ask AI about “" + query + "”";

    /// <summary>The section heading over the row that asks the model.</summary>
    public string AskGroup { get; init; } = "Ask AI";

    /// <summary>The heading of rows that have no group.</summary>
    public string DefaultGroup { get; init; } = "Results";

    /// <summary>The line shown when nothing matches the query.</summary>
    public Func<string, string> NoResults { get; init; } = query => "No pages or actions match “" + query + "”";

    /// <summary>The heading over the suggested questions.</summary>
    public string SuggestedQuestions { get; init; } = "Suggested questions";

    /// <summary>The status while the model works, before any text arrives.</summary>
    public string Thinking { get; init; } = "Searching your workspace";

    /// <summary>The heading over the suggested actions under an answer.</summary>
    public string SuggestedActions { get; init; } = "Suggested actions";

    /// <summary>The line of an answer that failed.</summary>
    public string AnswerFailed { get; init; } = "Couldn’t get an answer";

    /// <summary>The row that asks the failed question again.</summary>
    public string Retry { get; init; } = "Try again";

    /// <summary>An action's label while it runs, from its label: "Create issue in Linear…".</summary>
    public Func<string, string> Running { get; init; } = label => label + "…";

    /// <summary>An action's label once done, when it returned nothing.</summary>
    public string Done { get; init; } = "Done";

    /// <summary>An action's label after a failure.</summary>
    public string ActionFailed { get; init; } = "Failed. Press Enter to retry";

    /// <summary>What a simulated action says it did: nothing was sent.</summary>
    public string Simulated { get; init; } = "Simulated, nothing was sent";

    /// <summary>The accessible name of a citation or chip without a description: "Source 2: billing-api #1893".</summary>
    public Func<int, string, string> SourceName { get; init; } = (number, title) => "Source " + number + ": " + title;

    /// <summary>The accessible name of the model in the footer and beside the answer: "Answered by" and the model's name.</summary>
    public Func<string, string> AnsweredBy { get; init; } = model => "Answered by " + model;

    /// <summary>The announcement when a question is asked.</summary>
    public Func<string, string> Asking { get; init; } = question => "Asking: " + question;

    /// <summary>The footer hint for the arrow keys.</summary>
    public string HintNavigate { get; init; } = "Navigate";

    /// <summary>The footer hint for Enter on a page or action.</summary>
    public string HintOpen { get; init; } = "Open";

    /// <summary>The footer hint for Enter on a question.</summary>
    public string HintAsk { get; init; } = "Ask";

    /// <summary>The footer hint for Enter on a suggested action.</summary>
    public string HintRun { get; init; } = "Run";

    /// <summary>The footer hint for Enter on a failed answer or action.</summary>
    public string HintRetry { get; init; } = "Retry";

    /// <summary>The footer hint for Tab in search mode.</summary>
    public string HintAskAi { get; init; } = "Ask AI";

    /// <summary>The footer hint for Tab in an empty Ask AI.</summary>
    public string HintSearch { get; init; } = "Search";

    /// <summary>The footer hint for Escape in Ask AI.</summary>
    public string HintBack { get; init; } = "Back";

    /// <summary>The footer hint for Escape in search with a query.</summary>
    public string HintClear { get; init; } = "Clear";

    /// <summary>The footer hint for Escape in an empty search.</summary>
    public string HintClose { get; init; } = "Close";
}
