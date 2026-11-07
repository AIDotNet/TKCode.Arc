using Avalonia.Automation;
using Avalonia.Interactivity;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public sealed partial class LinkUnfurl
{
    /// <summary>The send's content exit (180ms, exit: fade and 8px up), the height easing (480ms, standard) and the new content's fade (240ms after 120ms).</summary>
    internal static readonly TimeSpan ContentExit = TimeSpan.FromMilliseconds(180);

    internal static readonly TimeSpan HeightSettle = TimeSpan.FromMilliseconds(480);

    internal static readonly TimeSpan ContentEnter = TimeSpan.FromMilliseconds(240);

    internal static readonly TimeSpan ContentEnterDelay = TimeSpan.FromMilliseconds(120);

    /// <summary>
    /// Sends the message as Enter or the send button would: nothing happens when it is empty or a send is pending.
    /// Completes when the composer has cleared (or kept the message after a failure).
    /// </summary>
    public Task Send() => SendAsync();

    private void OnEditorSubmit(object? sender, TokenTextSubmitRequestedEventArgs e)
    {
        e.Handled = true;
        _ = SendAsync();
    }

    private void OnSendClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = SendAsync();
    }

    /// <summary>The message as sent: the text (links are their addresses there already) and the resolved previews in order.</summary>
    internal LinkUnfurlMessage BuildMessage()
    {
        var previews = new List<LinkUnfurlPreview>();
        foreach (var entry in _entries)
        {
            if (!entry.IsLeaving && entry.Link is { Status: LinkUnfurlStatus.Ready, Preview: { } preview })
            {
                previews.Add(preview);
            }
        }
        return new LinkUnfurlMessage(_editor?.Text ?? Text, previews);
    }

    private async Task SendAsync()
    {
        _interacted = true;
        if (IsSending)
        {
            return;
        }
        var message = BuildMessage();
        if (string.IsNullOrWhiteSpace(message.Text))
        {
            return;
        }
        var args = new LinkUnfurlSendRequestedEventArgs(message);
        RaiseEvent(args);
        if (args.Cancel)
        {
            return;
        }
        var pending = args.Pending;
        if (SendHandler is { } handler)
        {
            Task sent;
            try
            {
                sent = handler(message);
            }
            catch (Exception ex)
            {
                sent = Task.FromException(ex);
            }
            pending = pending is null ? sent : Task.WhenAll(pending, sent);
        }
        SendError = null;
        if (pending is not null && !pending.IsCompletedSuccessfully)
        {
            SetSending(true);
            try
            {
                await pending;
            }
            catch (Exception ex)
            {
                SetSending(false);
                SendError = ErrorMessage(ex);
                return;
            }
            SetSending(false);
        }
        await Sent();
    }

    private string ErrorMessage(Exception ex)
    {
        var inner = ex is AggregateException { InnerException: { } first } ? first : ex;
        return inner is OperationCanceledException || string.IsNullOrWhiteSpace(inner.Message) ? Labels.SendFailed : inner.Message;
    }

    private void SetSending(bool sending)
    {
        IsSending = sending;
        if (_editor is not null)
        {
            _editor.IsReadOnly = sending;
        }
        if (_send is not null)
        {
            _send.IsSending = sending;
            AutomationProperties.SetName(_send, sending ? Labels.Sending : Labels.Send);
        }
        if (_samples is not null)
        {
            _samples.IsEnabled = !sending;
        }
    }

    /// <summary>
    /// After a send: the content fades up and out (180ms), the message and links clear, the message area eases to its new
    /// height (480ms) while the empty content fades back (240ms after 120ms), and the button shows its check for 1.6s.
    /// </summary>
    private async Task Sent()
    {
        var generation = ++_sendGeneration;
        Announce(Labels.AnnounceSent);
        if (_send is not null)
        {
            _send.IsSent = true;
        }
        _ = HoldSent(generation);
        _exit ??= CreateExitMotion();
        var full = ArcMotion.IsFull(this);
        if (ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off)
        {
            _exit.Jump(0);
            await _exit.AnimateTo(1, MotionSpec.Tween(ContentExit, ArcEasings.Exit));
        }
        var oldHeight = _body?.Bounds.Height ?? 0;
        ClearAll(removeEvents: true);
        _converting = true;
        try
        {
            _editor?.Clear();
            SetCurrentValue(TextProperty, string.Empty);
        }
        finally
        {
            _converting = false;
        }
        if (_body is { } body && full && oldHeight > 0)
        {
            _settle ??= CreateSettleMotion();
            body.From = oldHeight;
            _settle.Jump(0);
            _ = _settle.AnimateTo(1, MotionSpec.Tween(HeightSettle, ArcEasings.Standard)).ContinueWith(_ => body.From = null,
                TaskScheduler.FromCurrentSynchronizationContext());
        }
        _contentShift.Y = 0;
        if (ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off)
        {
            await _exit.AnimateTo(0, MotionSpec.Tween(ContentEnter, ArcEasings.Standard, ContentEnterDelay));
        }
        else
        {
            _exit.Jump(0);
        }
        _contentShift.Y = 0;
        if (_content is not null)
        {
            _content.Opacity = 1;
        }
    }

    private async Task HoldSent(int generation)
    {
        try
        {
            await _scheduler.Delay(SentHold);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (generation == _sendGeneration && _send is not null)
        {
            _send.IsSent = false;
        }
    }

    private MotionValue<double> CreateExitMotion()
    {
        var motion = new MotionValue<double>(this, 0, MotionVectors.Double);
        motion.Changed += value =>
        {
            if (_content is null)
            {
                return;
            }
            _content.Opacity = 1 - value;
            // Only the way out travels (8px up); the way back is a plain fade. Reduced motion fades only.
            _contentShift.Y = motion.Target > 0.5 && ArcMotion.IsFull(this) ? -8 * value : 0;
        };
        return motion;
    }

    private MotionValue<double> CreateSettleMotion()
    {
        var motion = new MotionValue<double>(this, 1, MotionVectors.Double);
        motion.Changed += value =>
        {
            if (_body is not null)
            {
                _body.Progress = value;
            }
        };
        return motion;
    }

    /// <summary>Cancels every fetch and removes every link and card at once.</summary>
    private void ClearAll(bool removeEvents)
    {
        var entries = _entries.ToArray();
        foreach (var entry in entries)
        {
            entry.Fetch?.Cancel();
            entry.IsLeaving = true;
            if (removeEvents)
            {
                RaiseEvent(new LinkUnfurlLinkEventArgs(LinkRemovedEvent, entry.Link));
            }
            Forget(entry);
        }
        _lane?.Children.Clear();
    }
}
