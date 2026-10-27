using Avalonia.Controls;
using System.Collections;
using System.Globalization;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

public partial class Webhooks
{
    private readonly HashSet<WebhookDelivery> _retrying = new();
    private readonly HashSet<WebhookEndpoint> _testing = new();
    private readonly List<CancellationTokenSource> _pending = new();
    private CancellationTokenSource? _createCts;
    private WebhookEndpoint? _fresh;
    private WebhookAddCloseReason? _closeReason;
    private bool _syncingOpen;

    #region Public methods

    /// <summary>Opens the "Add endpoint" panel (the form is reset and its URL field focused).</summary>
    public void OpenAddPanel() => SetCurrentValue(IsAddPanelOpenProperty, true);

    /// <summary>Closes the "Add endpoint" panel (a running request is cancelled; a shown secret is dropped).</summary>
    public void CloseAddPanel() => ClosePanel(WebhookAddCloseReason.Programmatic);

    /// <summary>
    /// Sends <paramref name="delivery"/> again, as its Retry button does: the new attempt is added at the top of its
    /// endpoint's deliveries and, when it went through, the delivery is marked resolved. Does nothing for a delivery
    /// that does not fail, is being retried, or belongs to no shown endpoint.
    /// </summary>
    public Task RetryAsync(WebhookDelivery delivery)
    {
        var endpoint = EffectiveEndpoints.FirstOrDefault(e => e.Deliveries.Contains(delivery));
        return endpoint is null ? Task.CompletedTask : RetryFromRowAsync(endpoint, delivery);
    }

    /// <summary>Sends a test event to <paramref name="endpoint"/>, as the empty list's button does.</summary>
    public Task SendTestAsync(WebhookEndpoint endpoint) => SendTestFromViewAsync(endpoint);

    /// <summary>
    /// Adds a delivery your server reported (a live feed) at the top of <paramref name="endpoint"/>'s list, growing in
    /// with a flash when it is shown, and counts it in the last slice of the chart.
    /// </summary>
    public void AppendDelivery(WebhookEndpoint endpoint, WebhookDelivery delivery, bool flash = true)
    {
        CountInVolume(endpoint, delivery);
        InsertDelivery(endpoint, delivery, flash ? DeveloperFlash.Timed : null);
    }

    #endregion

    #region Add panel

    private void OnIsAddPanelOpenChanged(bool open)
    {
        PseudoClasses.Set(AddingPseudoClass, open);
        if (_add is null || _syncingOpen)
        {
            return;
        }
        _syncingOpen = true;
        if (open)
        {
            _add.Open();
        }
        else
        {
            _closeReason ??= WebhookAddCloseReason.Programmatic;
            _add.Close();
        }
        _syncingOpen = false;
    }

    private void OnAddOpened(object? sender, EventArgs e)
    {
        if (_add is null)
        {
            return;
        }
        if (!ReferenceEquals(_add.Content, Form))
        {
            Form.Reset(EventTypes);
            _add.Content = Form;
        }
        _add.InitialFocus = Form.UrlInput;
        _add.Title = Labels.NewEndpoint;
        AddPanelState = WebhookAddState.Editing;
        _syncingOpen = true;
        SetCurrentValue(IsAddPanelOpenProperty, true);
        _syncingOpen = false;
        PseudoClasses.Set(AddingPseudoClass, true);
        RaiseEvent(new RoutedEventArgs(AddPanelOpenedEvent, this));
    }

    private void OnAddClosed(object? sender, DeveloperMorphClosedEventArgs e)
    {
        var reason = e.Reason switch
        {
            DeveloperMorphCloseReason.Toggle => WebhookAddCloseReason.Toggle,
            DeveloperMorphCloseReason.Escape => WebhookAddCloseReason.Escape,
            _ => _closeReason ?? WebhookAddCloseReason.Programmatic,
        };
        _closeReason = null;
        CancelCreate();
        _reveal?.Secret.ClearSecret();
        if (_fresh is not null)
        {
            _endpointList.EndFlash(_fresh);
            _fresh = null;
        }
        AddPanelState = WebhookAddState.Closed;
        if (_add is not null)
        {
            _add.Title = Labels.AddEndpoint;
        }
        _syncingOpen = true;
        SetCurrentValue(IsAddPanelOpenProperty, false);
        _syncingOpen = false;
        PseudoClasses.Set(AddingPseudoClass, false);
        ResetFormWhenHidden();
        RaiseEvent(new WebhookAddPanelClosedEventArgs(AddPanelClosedEvent, this, reason));
    }

    private void ResetFormWhenHidden()
    {
        // The body fades out for 100ms: swap the reveal for a fresh form only once it is hidden, so it never shows.
        void Reset()
        {
            if (_add is null || IsAddPanelOpen)
            {
                return;
            }
            Form.Reset(EventTypes);
            _add.Content = Form;
            _add.InitialFocus = Form.UrlInput;
        }
        if (_add is null || !_add.IsBodyShown)
        {
            Reset();
            return;
        }
        DispatcherTimer.RunOnce(Reset, DeveloperSprings.BodyOut + TimeSpan.FromMilliseconds(80));
    }

    private void ClosePanel(WebhookAddCloseReason reason)
    {
        if (!IsAddPanelOpen)
        {
            return;
        }
        _closeReason = reason;
        SetCurrentValue(IsAddPanelOpenProperty, false);
    }

    internal void CancelFromForm()
    {
        if (AddPanelState == WebhookAddState.Submitting)
        {
            CancelCreate();
        }
        ClosePanel(WebhookAddCloseReason.Cancel);
    }

    internal void DoneFromReveal() => ClosePanel(WebhookAddCloseReason.Done);

    private void CancelCreate()
    {
        if (_createCts is null)
        {
            return;
        }
        _createCts.Cancel();
        _createCts = null;
        _form?.SetBusy(false);
        if (AddPanelState == WebhookAddState.Submitting)
        {
            AddPanelState = WebhookAddState.Editing;
        }
    }

    internal async Task SubmitFromFormAsync()
    {
        if (AddPanelState != WebhookAddState.Editing || _add is null)
        {
            return;
        }
        var form = Form;
        form.ShowCreateError(null);
        var draft = form.Validate();
        if (draft is null)
        {
            return;
        }
        var cts = new CancellationTokenSource();
        _createCts = cts;
        form.SetBusy(true);
        AddPanelState = WebhookAddState.Submitting;
        WebhookEndpointCreated result;
        try
        {
            result = CreateEndpointAction is { } action
                ? await action(draft, cts.Token)
                : await SimulateCreateAsync(draft, cts.Token);
            cts.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            if (cts.IsCancellationRequested || !ReferenceEquals(_createCts, cts))
            {
                return;
            }
            _createCts = null;
            form.SetBusy(false);
            AddPanelState = WebhookAddState.Editing;
            var message = Labels.CreateFailed(ex.Message);
            form.ShowCreateError(message);
            Announce(message);
            RaiseEvent(new WebhookActionFailedEventArgs(ActionFailedEvent, this, WebhookActionKind.CreateEndpoint, null, null, ex));
            return;
        }
        if (!ReferenceEquals(_createCts, cts) || !IsAddPanelOpen)
        {
            return;
        }
        _createCts = null;
        form.SetBusy(false);
        var endpoint = result.Endpoint;
        AddEndpointAtTop(endpoint);
        _fresh = endpoint;
        Reveal.Secret.SetSecret(result.Secret);
        _add.Title = Labels.EndpointReady;
        _add.Content = Reveal;
        AddPanelState = WebhookAddState.Revealing;
        Announce(Labels.EndpointAdded(endpoint.DisplayName));
        RaiseEvent(new WebhookEndpointEventArgs(EndpointCreatedEvent, this, endpoint));
        if (EndpointCreatedCommand is { } command && command.CanExecute(endpoint))
        {
            command.Execute(endpoint);
        }
        // Arc moves the selection to the new endpoint once its row has grown about half way (≈ 470ms).
        await FollowAsync(() =>
        {
            if (EffectiveEndpoints.Contains(endpoint))
            {
                SelectEndpoint(endpoint, WebhookChangeSource.Auto);
            }
        });
    }

    private void AddEndpointAtTop(WebhookEndpoint endpoint)
    {
        var motion = DeveloperListMotion.Insert with { Flash = DeveloperFlash.Held };
        if (_effective is IList list && !list.IsReadOnly && !list.IsFixedSize)
        {
            _endpointList.Change(motion, () =>
            {
                list.Insert(0, endpoint);
                if (_effective is not System.Collections.Specialized.INotifyCollectionChanged)
                {
                    _endpointList.Refresh();
                    OnEndpointsChanged();
                }
            });
        }
    }

    private async Task<WebhookEndpointCreated> SimulateCreateAsync(WebhookEndpointDraft draft, CancellationToken token)
    {
        await DelayAsync(DeveloperSprings.SimulatedAddEndpoint, token);
        var now = Time.GetLocalNow();
        var secret = WebhookMath.RandomSecret();
        var endpoint = new WebhookEndpoint
        {
            Name = draft.Description,
            Url = draft.Url,
            AddedBy = CurrentUser,
            AddedOn = now,
            SecretHint = DeveloperText.Mask("sig_", secret[^4..]),
        };
        endpoint.Events.AddRange(draft.Events);
        endpoint.Volume.AddRange(WebhookMath.EmptyDay(now));
        return new WebhookEndpointCreated(endpoint, secret);
    }

    #endregion

    #region Retry

    internal bool IsRetrying(WebhookDelivery delivery) => _retrying.Contains(delivery);

    internal async Task RetryFromRowAsync(WebhookEndpoint endpoint, WebhookDelivery delivery)
    {
        if (!delivery.IsOpenFailure || !_retrying.Add(delivery))
        {
            return;
        }
        RowOf(endpoint, delivery)?.RefreshAction();
        RaiseEvent(new WebhookDeliveryEventArgs(RetryStartedEvent, this, endpoint, delivery));
        if (RetryCommand is { } command && command.CanExecute(delivery))
        {
            command.Execute(delivery);
        }
        var oldRate = endpoint.DeliveredRate;
        var started = Time.GetLocalNow();
        var cts = Track();
        WebhookDelivery? result = null;
        Exception? error = null;
        try
        {
            result = RetryAction is { } action
                ? await action(delivery, cts.Token)
                : await SimulateRetryAsync(delivery, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _retrying.Remove(delivery);
            Untrack(cts);
            return;
        }
        catch (Exception ex)
        {
            error = ex;
        }
        Untrack(cts);
        _retrying.Remove(delivery);
        result ??= FailedAttempt(delivery, error, started);
        if (result.Attempt <= delivery.Attempt)
        {
            result.Attempt = delivery.Attempt + 1;
        }

        // Where the focus is now: on this delivery's Retry button it must move before the button goes away.
        var row = RowOf(endpoint, delivery);
        var retryHadFocus = row is not null && (row.RetryButton.IsFocused || row.RetryButton.IsKeyboardFocusWithin);
        var keyboard = retryHadFocus && row!.RetryButton.Classes.Contains(":focus-visible");

        var bucketIndex = WebhookMath.BucketIndexFor(endpoint.Volume, delivery.SentAt);
        if (bucketIndex >= 0 && endpoint.Volume[bucketIndex].Failed > 0)
        {
            endpoint.Volume[bucketIndex].Failed--;
        }
        CountInVolume(endpoint, result);
        delivery.RetriedBy = result;
        delivery.IsResolved = !result.IsFailure;
        InsertDelivery(endpoint, result, DeveloperFlash.Timed);
        row?.RefreshAction();

        var method = keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer;
        if (retryHadFocus)
        {
            // Hold the focus on the row now (its Retry button is leaving), then hand it to the new attempt once its
            // row is in the tree.
            row?.Button.Focus(method);
            FocusLater(() =>
            {
                var newRow = RowOf(endpoint, result);
                return newRow is null ? null : result.IsFailure && ShowsRetry ? newRow.RetryButton : newRow.Button;
            }, method);
        }

        var labels = Labels;
        if (result.IsFailure)
        {
            var reason = error?.Message ?? WebhookMath.StatusTextOf(result, labels.NoResponse);
            Announce(labels.RetryFailed(delivery.EventType, result.Attempt, reason));
            RaiseEvent(new WebhookActionFailedEventArgs(ActionFailedEvent, this, WebhookActionKind.Retry, endpoint, result, error));
        }
        else
        {
            Announce(labels.RetrySucceeded(delivery.EventType, result.Attempt, endpoint.Host,
                WebhookMath.CodeText(result.StatusCode), WebhookMath.FormatDuration(result.Duration)));
            RaiseEvent(new WebhookRetryCompletedEventArgs(RetryCompletedEvent, this, endpoint, delivery, result, oldRate, endpoint.DeliveredRate));
        }

        // The selection follows the new attempt once its row has grown about half way (Arc ≈ 470–545ms).
        await FollowAsync(() =>
        {
            if (ReferenceEquals(SelectedEndpoint, endpoint) && endpoint.Deliveries.Contains(result))
            {
                SelectDelivery(result, WebhookChangeSource.Auto);
            }
        });
    }

    private WebhookDelivery FailedAttempt(WebhookDelivery original, Exception? error, DateTimeOffset started)
    {
        var now = Time.GetLocalNow();
        var attempt = new WebhookDelivery
        {
            EventType = original.EventType,
            EventId = original.EventId,
            Attempt = original.Attempt + 1,
            IsTest = original.IsTest,
            StatusCode = 0,
            StatusText = Labels.NoResponse,
            Duration = now - started,
            SentAt = now,
            ResponseBody = error?.Message,
            Payload = original.Payload,
        };
        attempt.Headers.AddRange(original.Headers.Select(h => new WebhookHeader(h.Name, h.Value)));
        return attempt;
    }

    private async Task<WebhookDelivery> SimulateRetryAsync(WebhookDelivery original, CancellationToken token)
    {
        await DelayAsync(DeveloperSprings.SimulatedRetry, token);
        var attempt = new WebhookDelivery
        {
            EventType = original.EventType,
            EventId = original.EventId,
            Attempt = original.Attempt + 1,
            IsTest = original.IsTest,
            StatusCode = 200,
            StatusText = "OK",
            Duration = TimeSpan.FromMilliseconds(80 + Random.Shared.Next(120)),
            SentAt = Time.GetLocalNow(),
            ResponseBody = "{\"ok\":true}",
            Payload = original.Payload,
        };
        foreach (var header in original.Headers)
        {
            var value = header.Name.EndsWith("-Attempt", StringComparison.OrdinalIgnoreCase)
                ? attempt.Attempt.ToString(CultureInfo.InvariantCulture)
                : header.Value;
            attempt.Headers.Add(new WebhookHeader(header.Name, value));
        }
        return attempt;
    }

    #endregion

    #region Test event

    internal async Task SendTestFromViewAsync(WebhookEndpoint endpoint)
    {
        if (!_testing.Add(endpoint))
        {
            return;
        }
        var view = _split is not null && ReferenceEquals(_split.Endpoint, endpoint) ? _split : null;
        view?.SetSendingTest(true);
        if (SendTestCommand is { } command && command.CanExecute(endpoint))
        {
            command.Execute(endpoint);
        }
        var started = Time.GetLocalNow();
        var cts = Track();
        WebhookDelivery? result = null;
        Exception? error = null;
        try
        {
            result = SendTestAction is { } action
                ? await action(endpoint, cts.Token)
                : await SimulateTestAsync(endpoint, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _testing.Remove(endpoint);
            Untrack(cts);
            return;
        }
        catch (Exception ex)
        {
            error = ex;
        }
        Untrack(cts);
        _testing.Remove(endpoint);
        view = _split is not null && ReferenceEquals(_split.Endpoint, endpoint) ? _split : null;
        var buttonHadFocus = view is not null && view.SendTestButton.IsFocused;
        var keyboard = buttonHadFocus && view!.SendTestButton.Classes.Contains(":focus-visible");
        view?.SetSendingTest(false);
        result ??= new WebhookDelivery
        {
            EventType = endpoint.Events.FirstOrDefault() ?? "test",
            IsTest = true,
            StatusCode = 0,
            StatusText = Labels.NoResponse,
            Duration = Time.GetLocalNow() - started,
            SentAt = Time.GetLocalNow(),
            ResponseBody = error?.Message,
        };
        result.IsTest = true;
        CountInVolume(endpoint, result);
        InsertDelivery(endpoint, result, DeveloperFlash.Timed);
        if (ReferenceEquals(SelectedEndpoint, endpoint))
        {
            // Arc: the inspector switches from its placeholder to the test delivery at once.
            SelectDelivery(result, WebhookChangeSource.Auto);
        }
        if (buttonHadFocus)
        {
            var row = RowOf(endpoint, result);
            var method = keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer;
            FocusLater(() =>
            {
                var target = RowOf(endpoint, result);
                return target is null ? null : result.IsFailure && ShowsRetry ? target.RetryButton : target.Button;
            }, method);
        }
        var labels = Labels;
        if (result.IsFailure)
        {
            Announce(labels.TestFailed(endpoint.Host, error?.Message ?? WebhookMath.StatusTextOf(result, labels.NoResponse)));
            RaiseEvent(new WebhookActionFailedEventArgs(ActionFailedEvent, this, WebhookActionKind.SendTest, endpoint, result, error));
        }
        else
        {
            Announce(labels.TestSent(result.EventType, endpoint.Host, WebhookMath.CodeText(result.StatusCode)));
            RaiseEvent(new WebhookDeliveryEventArgs(TestEventSentEvent, this, endpoint, result));
        }
    }

    internal bool IsSendingTest(WebhookEndpoint endpoint) => _testing.Contains(endpoint);

    private async Task<WebhookDelivery> SimulateTestAsync(WebhookEndpoint endpoint, CancellationToken token)
    {
        await DelayAsync(DeveloperSprings.SimulatedSendTest, token);
        var now = Time.GetLocalNow();
        var type = endpoint.Events.FirstOrDefault() ?? "test.ping";
        var id = "evt_test" + Random.Shared.Next(100000, 999999).ToString(CultureInfo.InvariantCulture);
        var delivery = new WebhookDelivery
        {
            EventType = type,
            EventId = id,
            IsTest = true,
            StatusCode = 200,
            StatusText = "OK",
            Duration = TimeSpan.FromMilliseconds(90 + Random.Shared.Next(90)),
            SentAt = now,
            ResponseBody = "{\"ok\":true}",
            Payload = "{\"id\":\"" + id + "\",\"type\":\"" + type + "\",\"created\":" +
                      now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) +
                      ",\"live\":false,\"data\":{\"object\":{\"id\":\"test_object\",\"test\":true}}}",
        };
        delivery.Headers.Add(new WebhookHeader("Content-Type", "application/json; charset=utf-8"));
        delivery.Headers.Add(new WebhookHeader("Webhook-Event", type));
        delivery.Headers.Add(new WebhookHeader("Webhook-Id", id));
        delivery.Headers.Add(new WebhookHeader("Webhook-Attempt", "1"));
        return delivery;
    }

    #endregion

    #region Shared

    private WebhookDeliveryRow? RowOf(WebhookEndpoint endpoint, WebhookDelivery delivery) =>
        _split is not null && ReferenceEquals(_split.Endpoint, endpoint) ? _split.RowFor(delivery) : null;

    private void InsertDelivery(WebhookEndpoint endpoint, WebhookDelivery delivery, DeveloperFlash? flash)
    {
        if (_split is not null && ReferenceEquals(_split.Endpoint, endpoint))
        {
            _split.List.Change(DeveloperListMotion.Insert with { Flash = flash }, () => endpoint.Deliveries.Insert(0, delivery));
        }
        else
        {
            endpoint.Deliveries.Insert(0, delivery);
        }
    }

    private void CountInVolume(WebhookEndpoint endpoint, WebhookDelivery delivery)
    {
        if (endpoint.Volume.Count == 0)
        {
            return;
        }
        var index = WebhookMath.BucketIndexFor(endpoint.Volume, delivery.SentAt);
        var bucket = endpoint.Volume[index < 0 ? endpoint.Volume.Count - 1 : index];
        bucket.Total++;
        if (delivery.IsFailure)
        {
            bucket.Failed++;
        }
    }

    private Task FollowAsync(Action follow)
    {
        if (!CanAnimate)
        {
            follow();
            return Task.CompletedTask;
        }
        return FollowLaterAsync(follow);
    }

    private async Task FollowLaterAsync(Action follow)
    {
        var cts = Track();
        try
        {
            await DelayAsync(DeveloperSprings.SelectionFollow, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            Untrack(cts);
        }
        follow();
    }

    /// <summary>Focuses what <paramref name="target"/> returns now if it can, else once layout has attached it.</summary>
    private void FocusLater(Func<Avalonia.Input.InputElement?> target, NavigationMethod method)
    {
        if (target() is { IsEffectivelyVisible: true } now && TopLevel.GetTopLevel(now) is not null && now.Focus(method))
        {
            return;
        }
        Dispatcher.UIThread.Post(() => target()?.Focus(method), DispatcherPriority.Loaded);
    }

    private CancellationTokenSource Track()
    {
        var cts = new CancellationTokenSource();
        _pending.Add(cts);
        return cts;
    }

    private void Untrack(CancellationTokenSource cts)
    {
        _pending.Remove(cts);
        cts.Dispose();
    }

    private void CancelAll()
    {
        foreach (var cts in _pending.ToList())
        {
            cts.Cancel();
        }
        _createCts?.Cancel();
        _createCts = null;
    }

    #endregion
}
