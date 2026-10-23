using System;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="AiChat"/> shows or announces, in English by default. Replace the instance to translate or
/// reword; formatted texts are functions so that word order can change.
/// </summary>
public sealed class AiChatLabels
{
    /// <summary>The accessible name of the whole chat.</summary>
    public string Chat { get; init; } = "AI chat";

    /// <summary>The accessible name of the conversation menu button; the argument is the current title.</summary>
    public Func<string, string> Chats { get; init; } = title => $"Chats, current: {title}";

    /// <summary>The accessible name of the model button; the argument is the model's name.</summary>
    public Func<string, string> Model { get; init; } = name => $"Model: {name}";

    /// <summary>The new chat button, its tooltip and the first entry of the conversation menu.</summary>
    public string NewChat { get; init; } = "New chat";

    /// <summary>The title of a conversation that has none and no message yet.</summary>
    public string UntitledChat { get; init; } = "Untitled chat";

    /// <summary>The heading of the empty chat.</summary>
    public string Greeting { get; init; } = "What should we look at today?";

    /// <summary>The placeholder of the message box.</summary>
    public string Placeholder { get; init; } = "Ask anything";

    /// <summary>"Thinking" while the model has not said anything yet.</summary>
    public string Thinking { get; init; } = "Thinking";

    /// <summary>"Thought for 3s"; the argument is the seconds.</summary>
    public Func<int, string> ThoughtFor { get; init; } = seconds => $"Thought for {seconds}s";

    /// <summary>The copy button of a message and the label of a copied code block.</summary>
    public string Copy { get; init; } = "Copy";

    /// <summary>What the copy button shows for 1.6 seconds after copying.</summary>
    public string Copied { get; init; } = "Copied";

    /// <summary>The edit button of a message.</summary>
    public string Edit { get; init; } = "Edit";

    /// <summary>The accessible name of the box that edits a message.</summary>
    public string EditMessage { get; init; } = "Edit message";

    /// <summary>The cancel button of the editor.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The send button of the editor.</summary>
    public string Send { get; init; } = "Send";

    /// <summary>The retry button of a reply and of a failed reply.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The thumbs up button.</summary>
    public string GoodResponse { get; init; } = "Good response";

    /// <summary>The thumbs down button.</summary>
    public string BadResponse { get; init; } = "Bad response";

    /// <summary>The previous version button.</summary>
    public string PreviousVersion { get; init; } = "Previous version";

    /// <summary>The next version button.</summary>
    public string NextVersion { get; init; } = "Next version";

    /// <summary>The accessible name of the version switcher; arguments are the shown version and the count (both from 1).</summary>
    public Func<int, int, string> Version { get; init; } = (n, count) => $"Version {n} of {count}";

    /// <summary>The visible counter of the version switcher.</summary>
    public Func<int, int, string> VersionCount { get; init; } = (n, count) => $"{n} / {count}";

    /// <summary>The note under a reply the person stopped.</summary>
    public string Stopped { get; init; } = "Stopped";

    /// <summary>The note when the person stopped it before any text.</summary>
    public string StoppedBeforeAnswering { get; init; } = "Stopped before answering";

    /// <summary>The bold start of the error box.</summary>
    public string ReplyFailed { get; init; } = "Reply failed.";

    /// <summary>The accessible name and tooltip of the send button.</summary>
    public string SendMessage { get; init; } = "Send message";

    /// <summary>The accessible name and tooltip of the send button while a reply is generated.</summary>
    public string StopReply { get; init; } = "Stop reply";

    /// <summary>The attach button's tooltip and name.</summary>
    public string AttachFiles { get; init; } = "Attach files";

    /// <summary>The name of the button that removes an attachment; the argument is its name.</summary>
    public Func<string, string> Remove { get; init; } = name => $"Remove {name}";

    /// <summary>The accessible name of the message box.</summary>
    public string Message { get; init; } = "Message";

    /// <summary>The button that scrolls to the newest message.</summary>
    public string ScrollToLatest { get; init; } = "Scroll to latest message";

    /// <summary>The accessible name of the list of searches of a search tool.</summary>
    public string Searches { get; init; } = "Searches";

    /// <summary>The accessible name of the list of sources.</summary>
    public string Sources { get; init; } = "Sources";

    /// <summary>The label before the result of a query.</summary>
    public string Result { get; init; } = "Result";

    /// <summary>The accessible name of a source link; arguments are the number, the title and the domain.</summary>
    public Func<int, string, string?, string> SourceLink { get; init; } =
        (n, title, domain) => string.IsNullOrEmpty(domain) ? $"Source {n}: {title}" : $"Source {n}: {title}, {domain}";

    /// <summary>The accessible name of the attachments of a message.</summary>
    public string Attachments { get; init; } = "Attachments";

    /// <summary>The accessible name of the thread (a list of turns).</summary>
    public string Messages { get; init; } = "Messages";

    /// <summary>The accessible name of a turn of the thread; arguments are its place and the count (both from 1).</summary>
    public Func<int, int, string> Turn { get; init; } = (n, count) => $"Message {n} of {count}";

    /// <summary>The help text of the message box: how to send.</summary>
    public string KeyHint { get; init; } = "Press Enter to send and Shift+Enter for a new line.";

    /// <summary>
    /// The other words of the message box (upload errors, the drop cover, …); null keeps their English defaults. The chat
    /// sets the box's placeholder, name, send, stop and attach words from this object's own properties.
    /// </summary>
    public AiComposerLabels? Composer { get; init; }

    // Announcements (polite live region).

    /// <summary>Announced when a reply starts; the argument is the model's name.</summary>
    public Func<string, string> Replying { get; init; } = name => $"{name} is replying";

    /// <summary>Announced when a reply finishes.</summary>
    public string ReplyFinished { get; init; } = "Reply finished";

    /// <summary>Announced when a reply is stopped.</summary>
    public string ReplyStopped { get; init; } = "Reply stopped";

    /// <summary>Announced when a reply fails; the argument is the reason.</summary>
    public Func<string, string> ReplyFailedAnnounce { get; init; } = reason => $"Reply failed: {reason}";

    /// <summary>Announced when a model is picked; the argument is its name.</summary>
    public Func<string, string> ModelSelected { get; init; } = name => $"{name} selected. New replies use it.";

    /// <summary>Announced when a conversation is opened; the argument is its title.</summary>
    public Func<string, string> Opened { get; init; } = title => $"Opened {title}";

    /// <summary>Announced after copying.</summary>
    public string CopiedToClipboard { get; init; } = "Copied to clipboard";

    /// <summary>Announced after a rating; the argument is whether it was a good response.</summary>
    public Func<bool, string> FeedbackSent { get; init; } = good => good ? "Feedback sent: good response" : "Feedback sent: bad response";

    /// <summary>Announced when a rating is taken back.</summary>
    public string FeedbackRemoved { get; init; } = "Feedback removed";

    /// <summary>Announced when the version changes; arguments are the shown version and the count.</summary>
    public Func<int, int, string> VersionShown { get; init; } = (n, count) => $"Version {n} of {count}";
}
