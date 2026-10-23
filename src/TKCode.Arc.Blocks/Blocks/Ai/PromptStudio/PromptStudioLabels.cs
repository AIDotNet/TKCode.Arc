using System;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="PromptStudio"/> shows or announces, in English by default. Replace the instance to translate or
/// reword; formatted texts are functions so that word order can change.
/// </summary>
public sealed class PromptStudioLabels
{
    /// <summary>The system tab.</summary>
    public string SystemTab { get; init; } = "System";

    /// <summary>The user tab.</summary>
    public string UserTab { get; init; } = "User";

    /// <summary>The placeholder of an empty system editor.</summary>
    public string SystemPlaceholder { get; init; } = "Describe the assistant's role and rules";

    /// <summary>The placeholder of an empty user editor.</summary>
    public string UserPlaceholder { get; init; } = "Write the message the assistant answers";

    /// <summary>The hint under the editor; the key cap "{{" is shown by the block between the two parts.</summary>
    public string TypeToAddBefore { get; init; } = "Type";

    /// <summary>The part after the key cap.</summary>
    public string TypeToAddAfter { get; init; } = "to add a variable";

    /// <summary>The estimate of the tokens of the shown part; the argument is the count.</summary>
    public Func<int, string> Tokens { get; init; } = count => count == 1 ? "1 token" : $"{count:N0} tokens";

    /// <summary>The placeholder of an empty variable field.</summary>
    public string EmptyValue { get; init; } = "Empty";

    /// <summary>The accessible name of the variable fields group.</summary>
    public string VariablesName { get; init; } = "Variables";

    /// <summary>The accessible name of the group of test cases.</summary>
    public string TestCasesName { get; init; } = "Test cases";

    /// <summary>The run button.</summary>
    public string Run { get; init; } = "Run";

    /// <summary>The stop button.</summary>
    public string Stop { get; init; } = "Stop";

    /// <summary>The retry button.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The hint on the run button (shown as key caps by the block).</summary>
    public string RunShortcutName { get; init; } = "Run (Ctrl+Enter)";

    /// <summary>The idle text of the output panel; the argument is the selected test case's label.</summary>
    public Func<string, string> Idle { get; init; } = label => $"Run {label} to see the reply here.";

    /// <summary>The idle text without a test case.</summary>
    public string IdleNoCase { get; init; } = "Run the prompt to see the reply here.";

    /// <summary>The note that the runs are simulated.</summary>
    public string RunsAreSimulated { get; init; } = "Runs are simulated.";

    /// <summary>The status while the model has not started to write.</summary>
    public string Thinking { get; init; } = "Thinking";

    /// <summary>The status while the reply is written.</summary>
    public string Writing { get; init; } = "Writing";

    /// <summary>The status of a finished reply.</summary>
    public string Reply { get; init; } = "Reply";

    /// <summary>The status of a stopped run.</summary>
    public string Stopped { get; init; } = "Stopped";

    /// <summary>The status of a failed run.</summary>
    public string RunFailed { get; init; } = "Run failed";

    /// <summary>The footer while a run is going; arguments are the model and the effort.</summary>
    public Func<string, string, bool, string> RunningFooter { get; init; } =
        (model, effort, simulated) => simulated ? $"Simulated · {model} · {effort} effort" : $"{model} · {effort} effort";

    /// <summary>The footer of a finished run; arguments are the seconds, the output tokens, the cost text and whether it was simulated.</summary>
    public Func<double, int, string?, bool, string> DoneFooter { get; init; } = (seconds, tokens, cost, simulated) =>
        string.Join(" · ", new[] { simulated ? "Simulated" : null, $"{seconds:0.00} s", $"{tokens:N0} tokens", cost }.Where(p => p is not null));

    /// <summary>The footer of a failed run; the argument is the model.</summary>
    public Func<string, bool, string> FailedFooter { get; init; } = (model, simulated) => simulated ? $"Simulated error · {model}" : model;

    /// <summary>The copy reply button.</summary>
    public string CopyReply { get; init; } = "Copy reply";

    /// <summary>The accessible name of the version button; arguments are the label and whether it is in production.</summary>
    public Func<string, bool, string> VersionButton { get; init; } = (label, production) => production ? $"Version {label}, in production" : $"Version {label}";

    /// <summary>The tag of the version in production.</summary>
    public string Production { get; init; } = "Production";

    /// <summary>The header tag of an open version that is not in production.</summary>
    public string Latest { get; init; } = "Latest";

    /// <summary>The accessible name of the list of versions.</summary>
    public string VersionsName { get; init; } = "Versions";

    /// <summary>The meta line of a version row; arguments are the label, the author and the relative time.</summary>
    public Func<string, string, string, string> VersionMeta { get; init; } = (label, author, when) => $"{label} · {author} · {when}";

    /// <summary>The compare action; the argument is the other version's label.</summary>
    public Func<string, string> CompareWith { get; init; } = label => $"Compare with {label}";

    /// <summary>The promote action.</summary>
    public string Promote { get; init; } = "Promote to production";

    /// <summary>The line under the actions while a version is in production; the argument is its label.</summary>
    public Func<string, string> ProductionServes { get; init; } = label => $"Production serves {label}";

    /// <summary>The line after a promotion; the argument is the label of the version in production.</summary>
    public Func<string, string> InProduction { get; init; } = label => $"{label} is in production";

    /// <summary>The undo button of the promotion line.</summary>
    public string Undo { get; init; } = "Undo";

    /// <summary>The button that leaves the compare view.</summary>
    public string BackToEditing { get; init; } = "Back to editing";

    /// <summary>The hint line of the compare view; arguments are the two labels.</summary>
    public Func<string, string, string> CompareHint { get; init; } = (from, to) => $"{from} to {to}";

    /// <summary>The key of added text in the compare view.</summary>
    public string Added { get; init; } = "added";

    /// <summary>The key of removed text in the compare view.</summary>
    public string Removed { get; init; } = "removed";

    /// <summary>The summary of the compare view; arguments are the added and the removed words.</summary>
    public Func<int, int, string> DiffSummary { get; init; } = (added, removed) => $"+{added} −{removed}";

    /// <summary>The accessible name of the model button; arguments are the model and the effort.</summary>
    public Func<string, string, string> ModelButton { get; init; } = (model, effort) => $"Model: {model}, {effort} effort";

    /// <summary>The note under the model list when prices are examples.</summary>
    public string PriceNote { get; init; } = "Prices are in USD per million tokens (input · output). Some are examples.";

    // Announcements (polite live region).

    /// <summary>Announced when a run starts.</summary>
    public string AnnounceRunning { get; init; } = "Running";

    /// <summary>Announced when a run is complete.</summary>
    public string AnnounceDone { get; init; } = "Reply ready";

    /// <summary>Announced when a run is stopped.</summary>
    public string AnnounceStopped { get; init; } = "Run stopped";

    /// <summary>Announced when a run fails; the argument is the message.</summary>
    public Func<string, string> AnnounceFailed { get; init; } = message => $"Run failed: {message}";

    /// <summary>Announced when a variable is added; the argument is its name.</summary>
    public Func<string, string> AnnounceVariableAdded { get; init; } = name => $"Variable {name} added";

    /// <summary>Announced when a version is promoted; the argument is its label.</summary>
    public Func<string, string> AnnouncePromoted { get; init; } = label => $"{label} is now in production";

    /// <summary>Announced when a promotion is undone; the argument is the label that is in production again.</summary>
    public Func<string, string> AnnouncePromotionUndone { get; init; } = label => $"Production serves {label} again";

    /// <summary>Announced when the compare view opens; arguments are the two labels.</summary>
    public Func<string, string, string> AnnounceCompare { get; init; } = (from, to) => $"Comparing {from} with {to}";

    /// <summary>Announced when the reply is copied.</summary>
    public string AnnounceCopied { get; init; } = "Reply copied";
}
