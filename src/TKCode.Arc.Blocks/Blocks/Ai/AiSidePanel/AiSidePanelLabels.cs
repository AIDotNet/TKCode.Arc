using System;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="AiSidePanel"/> shows or announces, in English by default. Replace the instance to translate or
/// reword; formatted texts are functions so that word order can change.
/// </summary>
public sealed class AiSidePanelLabels
{
    /// <summary>The accessible name of the assistant panel; the argument is the assistant's name.</summary>
    public Func<string, string> Panel { get; init; } = name => $"{name} panel";

    /// <summary>The accessible name of the close button.</summary>
    public string Close { get; init; } = "Close assistant";

    /// <summary>The tooltip of the close button.</summary>
    public string CloseTip { get; init; } = "Close";

    /// <summary>The button that reopens the panel; the argument is the assistant's name.</summary>
    public Func<string, string> Reopen { get; init; } = name => name;

    /// <summary>The accessible name of the row of context pills.</summary>
    public string Context { get; init; } = "Context";

    /// <summary>The context pill that shows the assistant reads the whole page.</summary>
    public string ThisPage { get; init; } = "This page";

    /// <summary>The accessible name of the thread.</summary>
    public string Conversation { get; init; } = "Conversation";

    /// <summary>The title of the empty thread; the argument is the page's title.</summary>
    public Func<string, string> EmptyTitle { get; init; } = title => $"Ask about {title}";

    /// <summary>The line under the empty thread's title; the argument is the assistant's name.</summary>
    public Func<string, string> EmptyDescription { get; init; } =
        name => $"{name} reads this page and suggests edits. The page only changes when you apply one.";

    /// <summary>The accessible name of a row of suggestions.</summary>
    public string Suggestions { get; init; } = "Suggestions";

    /// <summary>The shimmering text while the assistant reads the page.</summary>
    public string ReadingThePage { get; init; } = "Reading the page";

    /// <summary>The button that appears under a selection on the page.</summary>
    public string AskAboutSelection { get; init; } = "Ask about selection";

    /// <summary>The pill for a selection; the argument is the block's label.</summary>
    public Func<string, string> SelectionPill { get; init; } = label => $"Selected: {label}";

    /// <summary>The name of the button that removes the selection pill; the argument is the block's label.</summary>
    public Func<string, string> RemoveSelection { get; init; } = label => $"Remove {label} from context";

    /// <summary>The line above a message asked about a selection; the argument is the label.</summary>
    public Func<string, string> SelectionContext { get; init; } = label => $"Selected: {label}";

    /// <summary>The placeholder of the box.</summary>
    public string Placeholder { get; init; } = "Ask about this page";

    /// <summary>The placeholder with a selection; the argument is the block's label in lower case.</summary>
    public Func<string, string> PlaceholderForSelection { get; init; } = label => $"Ask about {label}";

    /// <summary>The accessible name of the box.</summary>
    public string Message { get; init; } = "Message";

    /// <summary>The send button.</summary>
    public string Send { get; init; } = "Send message";

    /// <summary>The stop button.</summary>
    public string Stop { get; init; } = "Stop";

    /// <summary>The note under a stopped answer.</summary>
    public string Stopped { get; init; } = "Stopped";

    /// <summary>The title of the error card; the argument is the assistant's name.</summary>
    public Func<string, string> DidNotRespond { get; init; } = name => $"{name} could not answer";

    /// <summary>The retry button.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The accessible name of a change card; the argument is the block's label.</summary>
    public Func<string, string> ChangeCard { get; init; } = label => $"Suggested edit to {label}";

    /// <summary>The discard button of a change card.</summary>
    public string Discard { get; init; } = "Discard";

    /// <summary>The apply button of a change card.</summary>
    public string Apply { get; init; } = "Apply";

    /// <summary>The footer of an applied change.</summary>
    public string AppliedToThePage { get; init; } = "Applied to page";

    /// <summary>The footer of an undone change.</summary>
    public string Undone { get; init; } = "Undone";

    /// <summary>The footer of a discarded change.</summary>
    public string Discarded { get; init; } = "Discarded";

    /// <summary>The summary of a changed paragraph; arguments are the new and the old number of words.</summary>
    public Func<int, int, string> WordsWas { get; init; } = (now, was) => $"{now} words (was {was})";

    /// <summary>The summary of a table that gained a column; the argument is its header.</summary>
    public Func<string, string> AddsColumn { get; init; } = header => $"New column: {header}";

    /// <summary>The summary of a table change that is not a new column.</summary>
    public string ChangesTable { get; init; } = "Changes the table";

    /// <summary>The undo pill; the argument is the block's label.</summary>
    public Func<string, string> Updated { get; init; } = label => $"{label} changed";

    /// <summary>The undo button of the pill.</summary>
    public string Undo { get; init; } = "Undo";

    /// <summary>The accessible name of the scroll-to-latest button of the thread.</summary>
    public string ScrollToLatest { get; init; } = "Scroll to latest";

    /// <summary>The accessible name of the table on the page.</summary>
    public Func<string?, string> Table { get; init; } = label => label ?? "Table";

    // Announcements (polite live region).

    /// <summary>Announced when a question starts; the argument is the assistant's name.</summary>
    public Func<string, string> AnnounceReading { get; init; } = name => $"{name} is reading the page";

    /// <summary>Announced when the answer is complete.</summary>
    public string AnnounceDone { get; init; } = "Reply ready";

    /// <summary>Announced when the answer is stopped.</summary>
    public string AnnounceStopped { get; init; } = "Reply stopped";

    /// <summary>Announced when the answer fails; the arguments are the assistant's name and the error.</summary>
    public Func<string, string, string> AnnounceFailed { get; init; } = (name, error) => $"{name} could not answer: {error}. You can retry.";

    /// <summary>Announced when the answer is complete with a proposed change; the argument is the block's label.</summary>
    public Func<string, string> AnnounceProposed { get; init; } = label => $"Reply ready, with a suggested edit to {label}";

    /// <summary>Announced when a change is applied; the argument is the block's label.</summary>
    public Func<string, string> AnnounceApplied { get; init; } = label => $"{label} changed. You can undo it.";

    /// <summary>Announced when a change is undone; the argument is the block's label.</summary>
    public Func<string, string> AnnounceUndone { get; init; } = label => $"{label} restored.";

    /// <summary>Announced when a change is discarded.</summary>
    public string AnnounceDiscarded { get; init; } = "Edit discarded";

    /// <summary>Announced when a selection is added; the argument is the block's label.</summary>
    public Func<string, string> AnnounceSelection { get; init; } = label => $"Asking about the selection in {label}";

    /// <summary>Announced when the selection is removed.</summary>
    public string AnnounceSelectionRemoved { get; init; } = "Selection cleared";

    /// <summary>Announced when the panel opens or closes; the arguments are whether it is open and the assistant's name.</summary>
    public Func<bool, string, string> AnnounceOpen { get; init; } = (open, name) => open ? $"{name} opened" : $"{name} closed";
}
