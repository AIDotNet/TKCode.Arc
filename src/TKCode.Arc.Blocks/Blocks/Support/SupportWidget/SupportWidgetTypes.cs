using Avalonia.Collections;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>A help article a <see cref="SupportWidget"/> can show and search.</summary>
public sealed class SupportWidgetArticle
{
    /// <summary>Creates an empty article (XAML).</summary>
    public SupportWidgetArticle()
    {
    }

    /// <summary>Creates an article.</summary>
    public SupportWidgetArticle(string id, string title, string? summary, string body)
    {
        Id = id;
        Title = title;
        Summary = summary;
        Body = body;
    }

    /// <summary>Stable identity.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>A line shown under the title in lists; null shows none.</summary>
    public string? Summary { get; set; }

    /// <summary>The article as Markdown.</summary>
    public string Body { get; set; } = string.Empty;
}

/// <summary>One conversation between the visitor and the team in a <see cref="SupportWidget"/>.</summary>
public sealed class SupportWidgetConversation
{
    /// <summary>Creates an empty conversation (XAML).</summary>
    public SupportWidgetConversation()
    {
    }

    /// <summary>Creates a conversation.</summary>
    public SupportWidgetConversation(string id, string title)
    {
        Id = id;
        Title = title;
    }

    /// <summary>Stable identity.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>What it is about, shown in the message history.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The messages, oldest first. Add the team's replies here and the open chat follows.</summary>
    public AvaloniaList<ChatMessage> Messages { get; } = new();

    /// <summary>Whether the team replied since the visitor last looked; opening the chat clears it.</summary>
    public bool IsUnread { get; set; }

    /// <summary>When the last message was written; null without messages.</summary>
    public DateTimeOffset? UpdatedAt => Messages.Count == 0 ? null : Messages[^1].CreatedAt;
}

/// <summary>Arguments of <see cref="SupportWidget.MessageSent"/>.</summary>
public sealed class SupportWidgetMessageEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SupportWidgetMessageEventArgs(RoutedEvent routedEvent, SupportWidgetConversation conversation, ChatMessage message)
        : base(routedEvent)
    {
        Conversation = conversation;
        Message = message;
    }

    /// <summary>The conversation (new conversations are already in <see cref="SupportWidget.Conversations"/>).</summary>
    public SupportWidgetConversation Conversation { get; }

    /// <summary>The visitor's message, already in the conversation.</summary>
    public ChatMessage Message { get; }
}
