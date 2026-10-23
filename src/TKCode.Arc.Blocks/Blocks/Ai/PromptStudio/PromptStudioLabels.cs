using System;
using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="PromptStudio"/> shows or announces, in English by default. Replace the instance to translate or
/// reword; formatted texts are functions so that word order can change.
/// </summary>
public sealed class PromptStudioLabels
{
    private static readonly CultureInfo s_invariant = CultureInfo.InvariantCulture;

    /// <summary>The accessible name of the card; the argument is the prompt's name.</summary>
    public Func<string, string> CardName { get; init; } = title => string.IsNullOrEmpty(title) ? "Prompt" : $"{title} prompt";

    // Editor.

    /// <summary>The accessible name of the system / user tabs.</summary>
    public string PartsName { get; init; } = "Prompt part";

    /// <summary>The system tab.</summary>
    public string SystemTab { get; init; } = "System";

    /// <summary>The user tab.</summary>
    public string UserTab { get; init; } = "User";

    /// <summary>The accessible name of a tab whose part changed in the compare view; the argument is the tab's name.</summary>
    public Func<string, string> ChangedPart { get; init; } = tab => $"{tab}, changed";

    /// <summary>The accessible name of the system editor.</summary>
    public string SystemEditorName { get; init; } = "System prompt";

    /// <summary>The accessible name of the user editor.</summary>
    public string UserEditorName { get; init; } = "User prompt";

    /// <summary>The placeholder of an empty system editor.</summary>
    public string SystemPlaceholder { get; init; } = "Describe the assistant's role and rules";

    /// <summary>The placeholder of an empty user editor.</summary>
    public string UserPlaceholder { get; init; } = "Write the message the assistant answers";

    /// <summary>The hint under the editor; the key cap "{{" is shown by the block between the two parts.</summary>
    public string TypeToAddBefore { get; init; } = "Type";

    /// <summary>The part after the key cap.</summary>
    public string TypeToAddAfter { get; init; } = "to add a variable";

    /// <summary>The last row of the "{{" list, for a name that is not a variable yet; the argument is the name.</summary>
    public Func<string, string> AddVariable { get; init; } = name => $"Add {name}";

    /// <summary>Announced when the "{{" list shows or filters; the argument is the number of rows.</summary>
    public Func<int, string> VariableCount { get; init; } = count => count == 1 ? "1 variable" : $"{count} variables";

    /// <summary>The estimate of the tokens of the shown part; the argument is the count.</summary>
    public Func<int, string> Tokens { get; init; } = count => count == 1 ? "1 token" : string.Create(s_invariant, $"{count:N0} tokens");

    // Variables and test cases.

    /// <summary>The placeholder of an empty variable field.</summary>
    public string EmptyValue { get; init; } = "Empty";

    /// <summary>The accessible name of the variable fields group and of the "{{" list.</summary>
    public string VariablesName { get; init; } = "Variables";

    /// <summary>The accessible name of the group of test cases.</summary>
    public string TestCasesName { get; init; } = "Test case";

    // Run.

    /// <summary>The run button.</summary>
    public string Run { get; init; } = "Run";

    /// <summary>The stop button.</summary>
    public string Stop { get; init; } = "Stop";

    /// <summary>The accessible name of the run button while idle.</summary>
    public string RunPrompt { get; init; } = "Run prompt";

    /// <summary>The accessible name of the run button while a run is going.</summary>
    public string StopRun { get; init; } = "Stop run";

    /// <summary>The help text of the run button; the argument is the platform's modifier key ("Cmd" or "Ctrl").</summary>
    public Func<string, string> RunShortcutName { get; init; } = modifier => $"Run ({modifier}+Enter)";

    /// <summary>The retry button.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The accessible name of the output panel.</summary>
    public string OutputName { get; init; } = "Output";

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

    /// <summary>The footer while a run is going; arguments are the model, the effort (null for a model without one) and whether it is simulated.</summary>
    public Func<string, string?, bool, string> RunningFooter { get; init; } = (model, effort, simulated) =>
        string.Join(" · ", new[] { simulated ? "Simulated" : null, model, effort is null ? null : $"{effort} effort" }.Where(p => !string.IsNullOrEmpty(p)));

    /// <summary>The footer of a finished run; arguments are the seconds, the output tokens, the cost text (null without prices) and whether it was simulated.</summary>
    public Func<double, int, string?, bool, string> DoneFooter { get; init; } = (seconds, tokens, cost, simulated) =>
        string.Join(" · ", new[]
        {
            simulated ? "Simulated" : null,
            string.Create(s_invariant, $"{seconds:0.00} s"),
            tokens == 1 ? "1 token" : string.Create(s_invariant, $"{tokens:N0} tokens"),
            cost
        }.Where(p => p is not null));

    /// <summary>The footer of a failed run; arguments are the model and whether it was simulated.</summary>
    public Func<string, bool, string> FailedFooter { get; init; } = (model, simulated) => simulated ? $"Simulated error · {model}" : model;

    /// <summary>The cost of a run in the footer (US dollars, four decimals).</summary>
    public Func<decimal, string> Cost { get; init; } = cost => "$" + cost.ToString("0.0000", s_invariant);

    /// <summary>The copy reply button.</summary>
    public string CopyReply { get; init; } = "Copy reply";

    /// <summary>The copy button's name just after a copy.</summary>
    public string Copied { get; init; } = "Copied";

    /// <summary>The reply of the built-in simulator (used when there is no run handler); blank lines separate paragraphs.</summary>
    public string SimulatedReply { get; init; } =
        "Thanks for the details. This reply is simulated: no model was called, so the words are the same for every run.\n\n" +
        "The prompt, the values of this test case and the chosen model all reached the simulator. It waited a moment for the " +
        "effort you picked, then wrote this text at a model's pace.\n\n" +
        "Connect a run handler to see real replies, times and costs.";

    // Versions.

    /// <summary>The accessible name of the version button; arguments are the label and whether it is in production.</summary>
    public Func<string, bool, string> VersionButton { get; init; } = (label, production) => production ? $"Version {label}, in production" : $"Version {label}";

    /// <summary>The tag of the version in production.</summary>
    public string Production { get; init; } = "Production";

    /// <summary>The header tag of an open version that is not in production.</summary>
    public string Latest { get; init; } = "Latest";

    /// <summary>The accessible name of the version menu.</summary>
    public string VersionsName { get; init; } = "Versions";

    /// <summary>The accessible name of the list of versions in the menu.</summary>
    public string VersionsGroup { get; init; } = "Pick a version to compare or promote";

    /// <summary>The meta line of a version row; arguments are the label, the author and the relative time.</summary>
    public Func<string, string, string, string> VersionMeta { get; init; } = (label, author, when) => $"{label} · {author} · {when}";

    /// <summary>A relative time under a week; the argument is the whole days (0 to 6).</summary>
    public Func<int, string> DaysAgo { get; init; } = days => days switch
    {
        0 => "today",
        1 => "yesterday",
        _ => $"{days} days ago"
    };

    /// <summary>A relative time of 7 to 13 days.</summary>
    public string LastWeek { get; init; } = "last week";

    /// <summary>A relative time from two weeks on; the argument is the whole weeks.</summary>
    public Func<int, string> WeeksAgo { get; init; } = weeks => $"{weeks} weeks ago";

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

    // Compare view.

    /// <summary>The button that leaves the compare view.</summary>
    public string BackToEditing { get; init; } = "Back to editing";

    /// <summary>The hint line of the compare view; arguments are the two labels.</summary>
    public Func<string, string, string> CompareHint { get; init; } = (from, to) => $"{from} to {to}";

    /// <summary>The key of added text in the compare view.</summary>
    public string Added { get; init; } = "added";

    /// <summary>The key of removed text in the compare view.</summary>
    public string Removed { get; init; } = "removed";

    /// <summary>The count of added words above the compare view.</summary>
    public Func<int, string> DiffAdded { get; init; } = added => string.Create(s_invariant, $"+{added}");

    /// <summary>The count of removed words above the compare view.</summary>
    public Func<int, string> DiffRemoved { get; init; } = removed => string.Create(s_invariant, $"−{removed}");

    /// <summary>What screen readers hear for the counts; arguments are the added and the removed words.</summary>
    public Func<int, int, string> DiffSummaryName { get; init; } = (added, removed) =>
        $"{added} {(added == 1 ? "word" : "words")} added, {removed} {(removed == 1 ? "word" : "words")} removed";

    /// <summary>The accessible name of the compare view; arguments are the two labels and the part.</summary>
    public Func<string, string, PromptPart, string> DiffName { get; init; } = (from, to, part) =>
        $"Changes from {from} to {to}, {(part == PromptPart.System ? "system" : "user")} prompt";

    // Model menu.

    /// <summary>The accessible name of the model button; arguments are the model and the effort (null for a model without one).</summary>
    public Func<string, string?, string> ModelButton { get; init; } = (model, effort) =>
        effort is null ? $"Model: {model}. Change model" : $"Model: {model}, {effort} effort. Change model";

    /// <summary>The accessible name of the model menu.</summary>
    public string ModelMenuName { get; init; } = "Model and effort";

    /// <summary>The accessible name of the list of models.</summary>
    public string ModelsGroup { get; init; } = "Model";

    /// <summary>The title of the effort row in the model menu.</summary>
    public string EffortTitle { get; init; } = "Effort";

    /// <summary>The name of an effort level (the button, the menu's screen reader name and the footer).</summary>
    public Func<ModelEffort, string> EffortName { get; init; } = ModelEffortNames.Name;

    /// <summary>The short word of an effort level in the menu's segmented row.</summary>
    public Func<ModelEffort, string> EffortShort { get; init; } = effort => effort switch
    {
        ModelEffort.Low => "Low",
        ModelEffort.Medium => "Med",
        ModelEffort.High => "High",
        ModelEffort.ExtraHigh => "XHigh",
        _ => "Max"
    };

    /// <summary>What screen readers hear for a model's prices; arguments are the input and output price texts and whether they are samples.</summary>
    public Func<string, string, bool, string> PriceName { get; init; } = (input, output, sample) =>
        $"{input} input, {output} output per million tokens{(sample ? ", sample price" : string.Empty)}";

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

    /// <summary>Announced when a test case is chosen; the argument is its label.</summary>
    public Func<string, string> AnnounceCaseLoaded { get; init; } = label => $"{label} test case loaded";

    /// <summary>Announced when a variable is added; the argument is its name.</summary>
    public Func<string, string> AnnounceVariableAdded { get; init; } = name => $"Variable {name} added";

    /// <summary>Announced when a version is promoted; the argument is its label.</summary>
    public Func<string, string> AnnouncePromoted { get; init; } = label => $"{label} is now in production";

    /// <summary>Announced when a promotion is undone; the argument is the label that is in production again.</summary>
    public Func<string, string> AnnouncePromotionUndone { get; init; } = label => $"Production serves {label} again";

    /// <summary>Announced when the compare view opens; arguments are the two labels.</summary>
    public Func<string, string, string> AnnounceCompare { get; init; } = (from, to) => $"Comparing {from} with {to}";

    /// <summary>Announced when the compare view closes.</summary>
    public string AnnounceBackToEditing { get; init; } = "Back to editing";

    /// <summary>Announced when the person picks a model; arguments are the model and its effort (null for a model without one).</summary>
    public Func<string, string?, string> AnnounceModel { get; init; } = (model, effort) =>
        effort is null ? $"{model} selected" : $"{model} selected, {effort} effort";

    /// <summary>Announced when the person picks an effort; the argument is its name.</summary>
    public Func<string, string> AnnounceEffort { get; init; } = effort => $"{effort} effort";

    /// <summary>Announced when the reply is copied.</summary>
    public string AnnounceCopied { get; init; } = "Reply copied";
}
