using Avalonia;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace TKCode.Arc;

public partial class AiChat
{
    /// <summary>The turn view an event of one of its parts came from.</summary>
    private static AiChatTurnView? TurnOf(object? source) => source switch
    {
        AiChatTurnView view => view,
        Visual visual => visual.FindAncestorOfType<AiChatTurnView>(),
        _ => null
    };

    #region Conversations

    /// <summary>The person picked a conversation in the title menu (the menu has closed and the pill has the focus again).</summary>
    private void OnConversationChosen(object? sender, AiConversationChosenEventArgs e)
    {
        e.Handled = true;
        if (!e.IsChange)
        {
            return;
        }
        SetCurrentValue(CurrentConversationProperty, e.Conversation);
        RaiseEvent(new AiChatConversationEventArgs(ConversationOpenedEvent, this, e.Conversation));
        Announce(Labels.Opened(TitleOf(e.Conversation)));
    }

    #endregion

    #region Messages

    private void OnReplyCopyRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (TurnOf(e.Source)?.ReplyView?.Reply is { } reply)
        {
            _ = CopyTextAsync(reply.Text ?? string.Empty);
        }
    }

    private void OnReplyCopied(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Announce(Labels.CopiedToClipboard);
    }

    private void OnUserCopyRequested(object? sender, AiUserBubbleCopyEventArgs e)
    {
        // Handled: the chat writes the clipboard (the bubble would otherwise do it itself) and announces it.
        e.Handled = true;
        _ = CopyTextAsync(e.Text);
        Announce(Labels.CopiedToClipboard);
    }

    private void OnRetryRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (TurnOf(e.Source)?.Turn is { } turn)
        {
            Retry(turn);
        }
    }

    /// <summary>The error box's Retry: the failed reply runs again in place.</summary>
    private void OnStatusActionRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (TurnOf(e.Source)?.Turn is { } turn)
        {
            Retry(turn);
        }
    }

    private void OnEditSubmitted(object? sender, AiUserBubbleEditEventArgs e)
    {
        e.Handled = true;
        if (TurnOf(e.Source)?.Turn is { } turn)
        {
            Edit(turn, e.Text);
        }
    }

    /// <summary>A rating: mutually exclusive, pressing it again takes it back; <see cref="FeedbackChanged"/> and an announcement.</summary>
    private void OnFeedbackRequested(object? sender, AiMessageActionsFeedbackEventArgs e)
    {
        e.Handled = true;
        if (TurnOf(e.Source)?.ReplyView?.Reply is not { } reply)
        {
            return;
        }
        reply.Feedback = e.Value;
        RaiseEvent(new AiChatFeedbackEventArgs(FeedbackChangedEvent, this, reply, e.Value));
        Announce(e.Value switch
        {
            AiChatFeedback.Up => Labels.FeedbackSent(true),
            AiChatFeedback.Down => Labels.FeedbackSent(false),
            _ => Labels.FeedbackRemoved
        });
    }

    /// <summary>The pager moved: the turn shows that version and the turns that followed it.</summary>
    private void OnVersionPagerIndexChanged(object? sender, AiVersionPagerEventArgs e)
    {
        e.Handled = true;
        if (TurnOf(e.Source)?.Turn is not { } turn || turn.SelectedVersionIndex == e.Index)
        {
            return;
        }
        turn.SelectedVersionIndex = e.Index;
        Announce(Labels.VersionShown(e.Index + 1, e.Count));
    }

    #endregion

    #region Citations

    private void OnReplyCitationInvoked(object? sender, AiReplyTextSourceEventArgs e)
    {
        e.Handled = true;
        InvokeCitation(e.Item);
    }

    private void OnToolSourceInvoked(object? sender, AiChatSourceEventArgs e)
    {
        e.Handled = true;
        InvokeCitation(e.Item);
    }

    /// <summary><see cref="CitationInvoked"/>; unless it is handled, the source's address opens with the system launcher.</summary>
    private void InvokeCitation(AiSource source)
    {
        var args = new AiChatSourceEventArgs(CitationInvokedEvent, this, source);
        RaiseEvent(args);
        if (!args.Handled && source.Url is { IsAbsoluteUri: true } url && url.Scheme is "http" or "https" or "mailto")
        {
            OpenUri(url);
        }
    }

    #endregion

    #region Following

    private void OnFollowingChanged(object? sender, EventArgs e) => IsFollowingLatest = _scroller?.IsFollowing ?? true;

    #endregion
}
