namespace TKCode.Arc;

/// <summary>
/// Every word a <see cref="ContextWindow"/> shows or announces, in English by default: the readout, the legend, the
/// caption under the grid, the actions, the status line and the accessible names. Replace the instance to translate or
/// reword; sentences with numbers are functions so that word order can change. Token counts and percentages arrive
/// already formatted ("31K", "80").
/// </summary>
public sealed class ContextWindowLabels
{
    /// <summary>The accessible name of the card.</summary>
    public string Region { get; init; } = "Context window";

    /// <summary>The readout after the large number; the argument is the window ("1M").</summary>
    public Func<string, string> OfTokens { get; init; } = static window => $"of {window} tokens";

    /// <summary>The word after the header percentage.</summary>
    public string Used { get; init; } = "used";

    /// <summary>The free space in the readout; the argument is the free tokens ("509K").</summary>
    public Func<string, string> FreeTokens { get; init; } = static free => $"{free} free";

    /// <summary>The caption under the grid; the argument is the tokens per cell ("5K").</summary>
    public Func<string, string> CellSize { get; init; } = static perCell => $"Each cell is {perCell} tokens";

    /// <summary>The threshold caption under the grid; the argument is the threshold in percent.</summary>
    public Func<int, string> CompactsAt { get; init; } = static percent => $"Compacts at {percent}%";

    /// <summary>The legend row of the system prompt.</summary>
    public string SystemPrompt { get; init; } = "System prompt";

    /// <summary>The legend row of the tool definitions.</summary>
    public string Tools { get; init; } = "Tools";

    /// <summary>The legend row of files and memory.</summary>
    public string Files { get; init; } = "Files & memory";

    /// <summary>The legend row of the summary.</summary>
    public string Summary { get; init; } = "Summary";

    /// <summary>The legend row of the conversation.</summary>
    public string Messages { get; init; } = "Conversation";

    /// <summary>The legend row of the free space.</summary>
    public string Free { get; init; } = "Free";

    /// <summary>The legend row of the output reserve.</summary>
    public string Reserve { get; init; } = "Output reserve";

    /// <summary>The Summary row's value while there is no summary.</summary>
    public string NoSummary { get; init; } = "No summary yet";

    /// <summary>The label of the summary item that the first compaction creates.</summary>
    public string SummaryItem { get; init; } = "Summary of earlier messages";

    /// <summary>The accessible name of the legend.</summary>
    public string Legend { get; init; } = "Context breakdown";

    /// <summary>The accessible name of a legend row; the arguments are the row's name, its tokens and its percent ("" when empty).</summary>
    public Func<string, string, string, string> LegendRow { get; init; } =
        static (name, tokens, percent) => percent.Length == 0 ? $"{name}, {tokens}" : $"{name}, {tokens}, {percent}";

    /// <summary>The Send message button.</summary>
    public string Send { get; init; } = "Send message";

    /// <summary>The Attach file button.</summary>
    public string Attach { get; init; } = "Attach file";

    /// <summary>The Compact now button.</summary>
    public string Compact { get; init; } = "Compact now";

    /// <summary>The model pill's accessible name; the arguments are the model's name and its window ("1M").</summary>
    public Func<string, string, string> ModelPill { get; init; } = static (name, window) => $"Model {name}, {window} window. Change model";

    /// <summary>The accessible name of a model in the menu; the arguments are its name and its window.</summary>
    public Func<string, string, string> ModelOption { get; init; } = static (name, window) => $"{name}, {window} window";

    /// <summary>The accessible name of the model menu.</summary>
    public string ModelMenu { get; init; } = "Models";

    /// <summary>One part of the grid's description; the arguments are the part's name and its tokens.</summary>
    public Func<string, string, string> GridPart { get; init; } = static (name, tokens) => $"{name} {tokens}";

    /// <summary>
    /// The grid's description (it is one image to assistive technology); the arguments are the model's name (empty without
    /// one), the tokens used, the window, the parts joined with commas and the threshold in percent.
    /// </summary>
    public Func<string, string, string, string, int, string> GridDescription { get; init; } =
        static (model, used, window, parts, threshold) =>
            (model.Length == 0 ? "Context window" : $"{model} context window") +
            $": {used} of {window} tokens used. {parts}. Compacts at {threshold}%.";

    /// <summary>The status line before anything happens; the argument is the threshold in percent.</summary>
    public Func<int, string> Idle { get; init; } = static percent => $"Auto-compacts at {percent}%";

    /// <summary>The status after Send message; the arguments are the message and its tokens ("31K").</summary>
    public Func<string, string, string> Sent { get; init; } = static (message, tokens) => $"Added “{message}” (+{tokens})";

    /// <summary>The status after Attach file; the arguments are the file and its tokens.</summary>
    public Func<string, string, string> Attached { get; init; } = static (file, tokens) => $"Attached {file} (+{tokens})";

    /// <summary>The status after <see cref="ContextWindow.AddItem"/> of an item that is not a file; the arguments are its label and tokens.</summary>
    public Func<string, string, string> Added { get; init; } = static (label, tokens) => $"Added {label} (+{tokens})";

    /// <summary>Appended to a send or an attachment from 85% of the threshold on; the argument is the threshold in percent.</summary>
    public Func<int, string> NearThreshold { get; init; } = static percent => $" · compacts at {percent}%";

    /// <summary>Appended to a send or an attachment that reached the threshold; the argument is the threshold in percent.</summary>
    public Func<int, string> PastThreshold { get; init; } = static percent => $" · past {percent}%";

    /// <summary>The status while Compact now folds; the argument is how many messages.</summary>
    public Func<int, string> Folding { get; init; } = static count =>
        count == 1 ? "Folding 1 earlier message…" : $"Folding {count} earlier messages…";

    /// <summary>The status while the card folds after passing the threshold; the arguments are the threshold in percent and how many messages.</summary>
    public Func<int, int, string> FoldingPastThreshold { get; init; } = static (percent, count) =>
        count == 1 ? $"Past {percent}%: folding 1 earlier message…" : $"Past {percent}%: folding {count} earlier messages…";

    /// <summary>The status while the card folds after a switch to a smaller model; the arguments are the model and how many messages.</summary>
    public Func<string, int, string> FoldingForModel { get; init; } = static (model, count) =>
        count == 1 ? $"Too full for {model}: folding 1 earlier message…" : $"Too full for {model}: folding {count} earlier messages…";

    /// <summary>The status after a compaction; the arguments are the folded tokens and the summary's new tokens.</summary>
    public Func<string, string, string> Folded { get; init; } = static (folded, summary) => $"Folded {folded} into a {summary} summary";

    /// <summary>The status after a compaction that left the window past the threshold; the arguments are the folded tokens and the threshold in percent.</summary>
    public Func<string, int, string> StillOver { get; init; } = static (folded, percent) =>
        $"Folded {folded}; still over {percent}%. Remove files to make room";

    /// <summary>The status when the threshold is passed but only the newest messages are left.</summary>
    public string NothingToFold { get; init; } = "Nothing to fold yet";

    /// <summary>The status after a switch to a model the window fits; the arguments are the model and its window ("512K").</summary>
    public Func<string, string, string> Switched { get; init; } = static (model, window) => $"Now on {model} ({window} window)";

    /// <summary>The status after a switch to a model the window overfills; the arguments are the tokens used, the threshold in percent and the model.</summary>
    public Func<string, int, string, string> OverModel { get; init; } = static (used, percent, model) => $"{used} is over {percent}% of {model}";
}
