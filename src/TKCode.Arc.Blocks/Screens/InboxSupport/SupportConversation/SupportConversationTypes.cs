using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where a <see cref="SupportConversation"/> stands.</summary>
public enum SupportConversationStatus
{
    /// <summary>Waiting on the team.</summary>
    Open,

    /// <summary>Waiting on the customer.</summary>
    Pending,

    /// <summary>Done.</summary>
    Resolved
}

/// <summary>What the composer of a <see cref="SupportConversation"/> writes.</summary>
public enum SupportComposerMode
{
    /// <summary>A reply the customer sees.</summary>
    Reply,

    /// <summary>An internal note only the team sees.</summary>
    Note
}

/// <summary>The customer in a <see cref="SupportConversation"/>: who they are and the facts an agent needs at a glance.</summary>
public sealed class SupportCustomer
{
    /// <summary>The customer's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Their email address; null shows none.</summary>
    public string? Email { get; set; }

    /// <summary>Their company; null shows none.</summary>
    public string? Company { get; set; }

    /// <summary>A portrait URL; null shows initials.</summary>
    public string? Avatar { get; set; }

    /// <summary>Their plan, such as "Team, 12 seats"; null shows none.</summary>
    public string? Plan { get; set; }

    /// <summary>Where they are, such as "Lisbon, PT"; null shows none.</summary>
    public string? Location { get; set; }

    /// <summary>When they became a customer; null shows none.</summary>
    public DateTimeOffset? CustomerSince { get; set; }

    /// <summary>What they have spent, already formatted ("$4,280"); null shows none.</summary>
    public string? LifetimeValue { get; set; }

    /// <summary>How many earlier conversations they had.</summary>
    public int PastConversations { get; set; }

    /// <summary>Tags on the customer, such as "VIP" or "Beta".</summary>
    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
}

/// <summary>An internal note on a <see cref="SupportConversation"/>: seen by the team, never by the customer.</summary>
public sealed class SupportNote
{
    /// <summary>Creates an empty note (XAML).</summary>
    public SupportNote()
    {
    }

    /// <summary>Creates a note.</summary>
    public SupportNote(string id, string author, string text, DateTimeOffset createdAt)
    {
        Id = id;
        Author = author;
        Text = text;
        CreatedAt = createdAt;
    }

    /// <summary>Stable identity.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Who wrote it.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>The note.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>When it was written.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>A saved reply an agent can drop into the composer. <c>{name}</c> in <see cref="Text"/> becomes the customer's first name.</summary>
public sealed class SupportCannedReply
{
    /// <summary>Creates an empty reply (XAML).</summary>
    public SupportCannedReply()
    {
    }

    /// <summary>Creates a reply.</summary>
    public SupportCannedReply(string title, string text)
    {
        Title = title;
        Text = text;
    }

    /// <summary>The short name in the list.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The text inserted.</summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>Arguments of <see cref="SupportConversation.ReplySent"/>.</summary>
public sealed class SupportReplyEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SupportReplyEventArgs(RoutedEvent routedEvent, string text, SupportComposerMode mode, object item)
        : base(routedEvent)
    {
        Text = text;
        Mode = mode;
        Item = item;
    }

    /// <summary>The trimmed text.</summary>
    public string Text { get; }

    /// <summary>Whether it was a reply or an internal note.</summary>
    public SupportComposerMode Mode { get; }

    /// <summary>What was added: the <see cref="ChatMessage"/> of a reply or the <see cref="SupportNote"/> of a note.</summary>
    public object Item { get; }
}

/// <summary>Arguments of <see cref="SupportConversation.StatusChanged"/>.</summary>
public sealed class SupportStatusChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SupportStatusChangedEventArgs(RoutedEvent routedEvent, SupportConversationStatus oldStatus, SupportConversationStatus newStatus)
        : base(routedEvent)
    {
        OldStatus = oldStatus;
        NewStatus = newStatus;
    }

    /// <summary>The status before.</summary>
    public SupportConversationStatus OldStatus { get; }

    /// <summary>The status now.</summary>
    public SupportConversationStatus NewStatus { get; }
}
