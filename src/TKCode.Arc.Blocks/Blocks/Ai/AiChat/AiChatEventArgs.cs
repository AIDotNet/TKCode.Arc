using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <c>AiChat.MessageSent</c>.</summary>
public class AiChatMessageSentEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiChatMessageSentEventArgs(RoutedEvent routedEvent, object source, AiChatConversation conversation, AiChatTurn turn, AiChatUserMessage prompt)
        : base(routedEvent, source)
    {
        Conversation = conversation;
        Turn = turn;
        Prompt = prompt;
    }

    /// <summary>The conversation.</summary>
    public AiChatConversation Conversation { get; }

    /// <summary>The turn the message belongs to.</summary>
    public AiChatTurn Turn { get; }

    /// <summary>What was sent.</summary>
    public AiChatUserMessage Prompt { get; }
}

/// <summary>Arguments of <c>AiChat.ReplyCompleted</c>.</summary>
public class AiChatReplyEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiChatReplyEventArgs(RoutedEvent routedEvent, object source, AiChatReply reply)
        : base(routedEvent, source)
    {
        Reply = reply;
    }

    /// <summary>The reply; its <see cref="AiChatReply.Status"/> says how it ended (done, stopped or failed).</summary>
    public AiChatReply Reply { get; }
}

/// <summary>Arguments of <c>AiChat.FeedbackChanged</c>.</summary>
public class AiChatFeedbackEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiChatFeedbackEventArgs(RoutedEvent routedEvent, object source, AiChatReply reply, AiChatFeedback value)
        : base(routedEvent, source)
    {
        Reply = reply;
        Value = value;
    }

    /// <summary>The rated reply.</summary>
    public AiChatReply Reply { get; }

    /// <summary>The rating; <see cref="AiChatFeedback.None"/> when it was taken back.</summary>
    public AiChatFeedback Value { get; }
}

/// <summary>Arguments of <c>AiChat.ConversationOpened</c> and <c>ConversationCreated</c>.</summary>
public class AiChatConversationEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiChatConversationEventArgs(RoutedEvent routedEvent, object source, AiChatConversation conversation)
        : base(routedEvent, source)
    {
        Conversation = conversation;
    }

    /// <summary>The conversation.</summary>
    public AiChatConversation Conversation { get; }
}

/// <summary>Arguments of <c>AiChat.CitationInvoked</c>: the person pressed a citation or a source row.</summary>
public class AiChatSourceEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiChatSourceEventArgs(RoutedEvent routedEvent, object source, AiSource item)
        : base(routedEvent, source)
    {
        Item = item;
    }

    /// <summary>The source. Set <see cref="RoutedEventArgs.Handled"/> to open it yourself; otherwise its <see cref="AiSource.Url"/> is opened.</summary>
    public AiSource Item { get; }
}
