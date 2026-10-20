using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>A person in a <see cref="CommentThread"/>: an author, the current user, or someone who can be mentioned.</summary>
public sealed record CommentAuthor
{
    /// <summary>Creates an empty author (for XAML and object initializers).</summary>
    public CommentAuthor()
    {
    }

    /// <summary>Creates an author.</summary>
    /// <param name="id">Stable id (decides whose comments the current user may edit and delete).</param>
    /// <param name="name">Display name; initials are its first letters when there is no portrait.</param>
    /// <param name="avatar">Square portrait URL (http(s), file or avares), or null for initials.</param>
    public CommentAuthor(string id, string name, string? avatar = null)
    {
        Id = id;
        Name = name;
        Avatar = avatar;
    }

    /// <summary>Stable id.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Display name ("Mina Park"). Mentions are written "@Mina Park".</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Square portrait URL. Initials are shown without one.</summary>
    public string? Avatar { get; init; }
}

/// <summary>One emoji reaction under a comment and the people who chose it (Arc's <c>CommentReaction</c>).</summary>
public sealed record CommentReaction
{
    /// <summary>Creates an empty reaction (for XAML and object initializers).</summary>
    public CommentReaction()
    {
    }

    /// <summary>Creates a reaction.</summary>
    public CommentReaction(string emoji, IReadOnlyList<string> userIds)
    {
        Emoji = emoji;
        UserIds = userIds;
    }

    /// <summary>The emoji ("👍").</summary>
    public string Emoji { get; init; } = string.Empty;

    /// <summary>Ids of the people who reacted.</summary>
    public IReadOnlyList<string> UserIds { get; init; } = Array.Empty<string>();
}

/// <summary>
/// A comment and its replies (Arc's <c>ThreadComment</c>). Immutable: every change made in a <see cref="CommentThread"/>
/// produces a new tree (unchanged comments are shared), so it can be stored as is.
/// </summary>
public sealed record ThreadComment
{
    /// <summary>Creates an empty comment (for XAML and object initializers).</summary>
    public ThreadComment()
    {
    }

    /// <summary>Creates a comment.</summary>
    public ThreadComment(string id, CommentAuthor author, string body, string createdAt)
    {
        Id = id;
        Author = author;
        Body = body;
        CreatedAt = createdAt;
    }

    /// <summary>Stable id.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Who wrote it.</summary>
    public CommentAuthor Author { get; init; } = new();

    /// <summary>Plain text. "@Full Name" mentions of known people are highlighted.</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>Display label such as "2h" or "Sep 18" (shown as is).</summary>
    public string CreatedAt { get; init; } = string.Empty;

    /// <summary>Shows "· edited" after the time.</summary>
    public bool IsEdited { get; init; }

    /// <summary>A deleted comment that still has replies keeps its place as a quiet placeholder.</summary>
    public bool IsDeleted { get; init; }

    /// <summary>Reactions, in the order they were first added.</summary>
    public IReadOnlyList<CommentReaction> Reactions { get; init; } = Array.Empty<CommentReaction>();

    /// <summary>Replies, oldest first.</summary>
    public IReadOnlyList<ThreadComment> Replies { get; init; } = Array.Empty<ThreadComment>();
}

/// <summary>What kind of change a <see cref="CommentThreadChange"/> describes.</summary>
public enum CommentThreadChangeKind
{
    /// <summary>A new comment or reply (<see cref="CommentThreadChange.Comment"/>, <see cref="CommentThreadChange.ParentId"/>).</summary>
    Reply,

    /// <summary>A comment's body was edited (<see cref="CommentThreadChange.Id"/>, <see cref="CommentThreadChange.Body"/>).</summary>
    Edit,

    /// <summary>A comment was deleted (<see cref="CommentThreadChange.Id"/>).</summary>
    Delete,

    /// <summary>A reaction was toggled (<see cref="CommentThreadChange.Id"/>, <see cref="CommentThreadChange.Emoji"/>, <see cref="CommentThreadChange.Added"/>).</summary>
    React
}

/// <summary>
/// One change made in a <see cref="CommentThread"/> (Arc's <c>CommentThreadEvent</c>), for syncing to a server
/// without diffing trees. Servers should still enforce that people edit and delete only their own comments.
/// </summary>
public sealed record CommentThreadChange
{
    /// <summary>What happened.</summary>
    public CommentThreadChangeKind Kind { get; init; }

    /// <summary>The comment that changed (for <see cref="CommentThreadChangeKind.Reply"/>, the new comment).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The new comment (Reply only).</summary>
    public ThreadComment? Comment { get; init; }

    /// <summary>Id of the comment the reply was attached to, or null for a top-level comment (Reply only).</summary>
    public string? ParentId { get; init; }

    /// <summary>The new body (Edit only).</summary>
    public string? Body { get; init; }

    /// <summary>The emoji toggled (React only).</summary>
    public string? Emoji { get; init; }

    /// <summary>True when the current user added the reaction, false when they removed it (React only).</summary>
    public bool Added { get; init; }

    /// <summary>A new comment.</summary>
    public static CommentThreadChange Reply(ThreadComment comment, string? parentId) =>
        new() { Kind = CommentThreadChangeKind.Reply, Id = comment.Id, Comment = comment, ParentId = parentId };

    /// <summary>An edited body.</summary>
    public static CommentThreadChange Edit(string id, string body) => new() { Kind = CommentThreadChangeKind.Edit, Id = id, Body = body };

    /// <summary>A deleted comment.</summary>
    public static CommentThreadChange Delete(string id) => new() { Kind = CommentThreadChangeKind.Delete, Id = id };

    /// <summary>A toggled reaction.</summary>
    public static CommentThreadChange React(string id, string emoji, bool added) =>
        new() { Kind = CommentThreadChangeKind.React, Id = id, Emoji = emoji, Added = added };
}

/// <summary>Arguments of <see cref="CommentThread.CommentsChanged"/> (Arc's <c>onCommentsChange(comments, event)</c>).</summary>
public sealed class CommentsChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CommentsChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<ThreadComment> comments, CommentThreadChange change)
        : base(routedEvent)
    {
        Comments = comments;
        Change = change;
    }

    /// <summary>The full next tree.</summary>
    public IReadOnlyList<ThreadComment> Comments { get; }

    /// <summary>What changed.</summary>
    public CommentThreadChange Change { get; }
}

/// <summary>Arguments of <see cref="CommentThread.ResolvedChanged"/>.</summary>
public sealed class CommentThreadResolvedChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CommentThreadResolvedChangedEventArgs(RoutedEvent routedEvent, bool isResolved)
        : base(routedEvent)
    {
        IsResolved = isResolved;
    }

    /// <summary>The new state: true when the thread was resolved, false when it was reopened.</summary>
    public bool IsResolved { get; }
}

/// <summary>
/// Every word a <see cref="CommentThread"/> shows or announces (Arc's <c>CommentThreadLabels</c>). Each has an English
/// default; words with a count or a name are functions, so an app can apply its own plural rules. Override single
/// words with an initializer: <c>new CommentThreadLabels { Resolve = "Erledigen" }</c>.
/// </summary>
public sealed record CommentThreadLabels
{
    /// <summary>The English defaults.</summary>
    public static CommentThreadLabels Default { get; } = new();

    /// <summary>Accessible name of the whole thread.</summary>
    public string Thread { get; init; } = "Comment thread";

    /// <summary>Header and resolved chip, such as "3 comments".</summary>
    public Func<int, string> CommentCount { get; init; } = static count => $"{count} {(count == 1 ? "comment" : "comments")}";

    /// <summary>Resolved chip, such as "2 people".</summary>
    public Func<int, string> PeopleCount { get; init; } = static count => $"{count} {(count == 1 ? "person" : "people")}";

    /// <summary>The resolve button.</summary>
    public string Resolve { get; init; } = "Resolve";

    /// <summary>Title of the resolved chip.</summary>
    public string Resolved { get; init; } = "Resolved";

    /// <summary>The reopen button of the resolved chip.</summary>
    public string Reopen { get; init; } = "Reopen";

    /// <summary>Shown when there are no comments.</summary>
    public string Empty { get; init; } = "No comments yet. Start the conversation below.";

    /// <summary>The reply action.</summary>
    public string Reply { get; init; } = "Reply";

    /// <summary>The edit action.</summary>
    public string Edit { get; init; } = "Edit";

    /// <summary>The delete action and its confirmation button.</summary>
    public string Delete { get; init; } = "Delete";

    /// <summary>Cancels an edit, a reply or a deletion.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>Sends a new comment.</summary>
    public string Send { get; init; } = "Send";

    /// <summary>Saves an edit.</summary>
    public string Save { get; init; } = "Save";

    /// <summary>Placeholder and accessible name of the edit field.</summary>
    public string EditComment { get; init; } = "Edit comment";

    /// <summary>After the time of an edited comment.</summary>
    public string Edited { get; init; } = "edited";

    /// <summary>The deletion prompt.</summary>
    public string DeleteConfirm { get; init; } = "Delete this comment?";

    /// <summary>Placeholder text of a deleted comment that still has replies.</summary>
    public string DeletedText { get; init; } = "This comment was deleted";

    /// <summary>Accessible name of that placeholder.</summary>
    public string DeletedComment { get; init; } = "Deleted comment";

    /// <summary>Above the composer while replying. The name inside it is highlighted.</summary>
    public Func<string, string> ReplyingTo { get; init; } = static name => $"Replying to {name}";

    /// <summary>Accessible name of the button that cancels a reply.</summary>
    public string CancelReply { get; init; } = "Cancel reply";

    /// <summary>The collapsed replies toggle, such as "Show 2 replies".</summary>
    public Func<int, string> ShowReplies { get; init; } = static count => $"Show {count} {(count == 1 ? "reply" : "replies")}";

    /// <summary>The expanded replies toggle.</summary>
    public string HideReplies { get; init; } = "Hide replies";

    /// <summary>Accessible name of the add reaction button.</summary>
    public string AddReaction { get; init; } = "Add reaction";

    /// <summary>Accessible name of the button that closes the reaction picker.</summary>
    public string CloseReactions { get; init; } = "Close reactions";

    /// <summary>Accessible name of the reaction picker.</summary>
    public string Reactions { get; init; } = "Reactions";

    /// <summary>Accessible name of an emoji in the picker.</summary>
    public Func<string, string> ReactWith { get; init; } = static emoji => $"React with {emoji}";

    /// <summary>Accessible name of a reaction under a comment.</summary>
    public Func<string, int, bool, string> ReactionCount { get; init; } =
        static (emoji, count, includesYou) => $"{emoji} {count}{(includesYou ? ", including you" : string.Empty)}";

    /// <summary>Accessible name of the @mention suggestions.</summary>
    public string Mentions { get; init; } = "People";
}
