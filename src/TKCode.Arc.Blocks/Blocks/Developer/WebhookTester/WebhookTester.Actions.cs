using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class WebhookTester
{
    /// <summary>The list keeps this many deliveries; older ones fall off the bottom.</summary>
    private const int MaxDeliveries = 20;

    private static readonly TimeSpan s_signingPause = TimeSpan.FromMilliseconds(220);
    private static readonly TimeSpan s_autoPlayDelay = TimeSpan.FromSeconds(1);

    private readonly HashSet<string> _retrying = new(StringComparer.Ordinal);
    private CancellationTokenSource? _lifetime;
    private ViewportVisibility? _viewport;
    private DispatcherTimer? _autoPlayTimer;
    private WebhookTestDelivery? _shown;
    private bool _busy;
    private bool _interacted;
    private bool _autoPlayed;
    private int _counter;

    #region Lifetime

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _lifetime = new CancellationTokenSource();
        if (!_autoPlayed)
        {
            _viewport = new ViewportVisibility(this, 0.5, once: true);
            _viewport.VisibilityChanged += OnViewportChanged;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _autoPlayTimer?.Stop();
        _autoPlayTimer = null;
        if (_viewport is not null)
        {
            _viewport.VisibilityChanged -= OnViewportChanged;
            _viewport.Dispose();
            _viewport = null;
        }
        _lifetime?.Cancel();
        _lifetime?.Dispose();
        _lifetime = null;
    }

    private void OnViewportChanged(object? sender, bool visible)
    {
        if (!visible || _autoPlayed || !AutoPlay || SendAction is not null)
        {
            return;
        }
        _autoPlayTimer?.Stop();
        _autoPlayTimer = new DispatcherTimer { Interval = s_autoPlayDelay };
        _autoPlayTimer.Tick += (_, _) =>
        {
            _autoPlayTimer?.Stop();
            _autoPlayTimer = null;
            if (!_interacted && !_busy && AutoPlay && SendAction is null && !_autoPlayed)
            {
                _autoPlayed = true;
                _ = SendAsync();
            }
        };
        _autoPlayTimer.Start();
    }

    private void OnInteracted(object? sender, PointerPressedEventArgs e) => _interacted = true;

    private void OnInteractedKey(object? sender, KeyEventArgs e) => _interacted = true;

    #endregion

    #region Sending

    /// <summary>
    /// Sends the event as it stands (the same as the Send button): checks the endpoint and the payload, signs, awaits
    /// <see cref="SendAction"/> (or simulates), then inserts and selects the delivery. Returns it, or null when the endpoint
    /// or payload is not valid, a send is already running, or the block left the window meanwhile.
    /// </summary>
    public async Task<WebhookTestDelivery?> SendAsync()
    {
        if (!_built || _busy)
        {
            return null;
        }
        var labels = Labels;
        _endpointTouched = true;
        if (!ValidateEndpoint(showError: true))
        {
            _endpointInput!.Focus();
            _announcer!.Announce(labels.EndpointInvalid);
            return null;
        }
        var body = _editor!.Text ?? string.Empty;
        var check = WebhookTesterJson.Check(body);
        if (!check.IsValid)
        {
            _editor.Focus();
            _announcer!.Announce(labels.CannotSendInvalid);
            return null;
        }
        var type = SelectedEvent?.Type ?? EffectiveEvents.FirstOrDefault()?.Type ?? string.Empty;
        var request = Sign(Endpoint!.Trim(), type, body);
        var send = SendAction;
        return await RunAsync(type, request, send is null
            ? ct => SimulateAsync(request, ct)
            : ct => send(request, ct), sourceId: null);
    }

    /// <summary>
    /// Sends a delivery again (the same as its Retry button) under a fresh timestamp and signature: awaits
    /// <see cref="RetryAction"/>, else <see cref="SendAction"/>, else simulates. Returns the new delivery, or null when
    /// another send is running or the block left the window meanwhile.
    /// </summary>
    public async Task<WebhookTestDelivery?> RetryAsync(WebhookTestDelivery delivery)
    {
        if (!_built || _busy)
        {
            return null;
        }
        var request = Sign(delivery.Request.Url, delivery.Event, delivery.Request.Body);
        var retry = RetryAction;
        var send = SendAction;
        _retrying.Add(delivery.Id);
        RebuildRows();
        return await RunAsync(delivery.Event, request, retry is not null
            ? ct => retry(delivery, ct)
            : send is not null
                ? ct => send(request, ct)
                : ct => SimulateAsync(request, ct), sourceId: delivery.Id, simulated: retry is null && send is null);
    }

    private async Task<WebhookTestDelivery?> RunAsync(string type, WebhookTestRequest request,
        Func<CancellationToken, Task<WebhookTestResponse>> run, string? sourceId, bool? simulated = null)
    {
        var labels = Labels;
        var isSimulated = simulated ?? SendAction is null;
        var note = isSimulated ? " " + labels.Simulated : string.Empty;
        var token = _lifetime?.Token ?? CancellationToken.None;
        _busy = true;
        _shown = null;
        SetPhase(WebhookTestPhase.Signing);
        RefreshResponse(null);
        _announcer!.Announce(labels.AnnounceSending(type) + note);
        WebhookTestResponse response;
        try
        {
            if (ArcMotion.IsFull(this))
            {
                await Task.Delay(s_signingPause, token);
            }
            SetPhase(WebhookTestPhase.Sending);
            try
            {
                response = await run(token) ?? new WebhookTestResponse { Error = labels.ChipFailed };
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                response = new WebhookTestResponse { Status = 0, Error = string.IsNullOrWhiteSpace(ex.Message) ? labels.ChipFailed : ex.Message };
            }
        }
        catch (OperationCanceledException)
        {
            // The block left the window: nothing to show anymore.
            _busy = false;
            if (sourceId is not null)
            {
                _retrying.Remove(sourceId);
            }
            return null;
        }

        var delivery = new WebhookTestDelivery(
            "dlv_" + (++_counter).ToString(CultureInfo.InvariantCulture) + "_" + request.Timestamp.ToString(CultureInfo.InvariantCulture),
            type, DateTimeOffset.Now, request, response);
        _busy = false;
        if (sourceId is not null)
        {
            _retrying.Remove(sourceId);
        }
        _deliveries.Insert(0, delivery);
        while (_deliveries.Count > MaxDeliveries)
        {
            _deliveries.RemoveAt(_deliveries.Count - 1);
        }
        SetCurrentValue(SelectedDeliveryProperty, delivery);
        ShowDelivery(delivery);
        _announcer.Announce((response.IsSuccess
            ? labels.AnnounceSucceeded(type, response.Status, response.DurationMs)
            : labels.AnnounceFailed(type, FailureReason(response))) + note);
        Delivered?.Invoke(this, new WebhookTesterDeliveryEventArgs(delivery));
        if (DeliveredCommand is { } command && command.CanExecute(delivery))
        {
            command.Execute(delivery);
        }
        return delivery;
    }

    private string FailureReason(WebhookTestResponse response)
    {
        if (response.TimedOut)
        {
            return Labels.StepTimedOut.ToLowerInvariant();
        }
        if (response.Status == 0)
        {
            return string.IsNullOrWhiteSpace(response.Error) ? Labels.StepNoAnswer.ToLowerInvariant() : response.Error!;
        }
        return Labels.StepStatus(response.Status, WebhookTesterJson.Reason(response.Status));
    }

    private WebhookTestRequest Sign(string url, string type, string body)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = WebhookTesterSigner.Sign(Secret, timestamp, body);
        var header = string.IsNullOrWhiteSpace(SignatureHeader) ? "x-webhook-signature" : SignatureHeader;
        var value = WebhookTesterSigner.HeaderValue(timestamp, signature);
        var agent = (Source?.Name ?? "Webhooks").Replace(' ', '-') + "-Webhooks/1.0";
        return new WebhookTestRequest(url, type, body, timestamp, value, header,
        [
            new("content-type", "application/json"),
            new("user-agent", agent),
            new("x-webhook-event", type),
            new(header, value),
        ]);
    }

    private async Task<WebhookTestResponse> SimulateAsync(WebhookTestRequest request, CancellationToken token)
    {
        var latency = SimulatedLatency;
        if (latency > TimeSpan.Zero)
        {
            await Task.Delay(latency, token);
        }
        // A plausible, repeatable duration: the same payload always takes about the same time.
        var duration = 120 + (int)((request.Timestamp * 31 + request.Body.Length) % 120);
        return new WebhookTestResponse
        {
            Status = 200,
            DurationMs = duration,
            Body = "{\n  \"received\": true\n}",
            Headers =
            [
                new("content-type", "application/json; charset=utf-8"),
                new("content-length", "21"),
                new("x-request-id", "req_" + request.Timestamp.ToString("x", CultureInfo.InvariantCulture)),
            ],
        };
    }

    #endregion

    #region Timeline and response

    private void SetPhase(WebhookTestPhase phase)
    {
        var old = _phase;
        _phase = phase;
        if (old != phase)
        {
            RaisePropertyChanged(PhaseProperty, old, phase);
        }
        UpdateTimeline();
        SyncSendButton();
    }

    private void ShowDelivery(WebhookTestDelivery delivery)
    {
        _shown = delivery;
        SetPhase(delivery.Response.IsSuccess ? WebhookTestPhase.Succeeded : WebhookTestPhase.Failed);
        RefreshResponse(delivery);
    }

    private void UpdateTimeline()
    {
        if (_connectors[0] is null)
        {
            return;
        }
        var labels = Labels;
        var phase = _phase;
        var done = phase is WebhookTestPhase.Succeeded or WebhookTestPhase.Failed;
        SetStep(0, phase == WebhookTestPhase.Idle ? StepState.Pending : phase == WebhookTestPhase.Signing ? StepState.Active : StepState.Done);
        SetStep(1, phase is WebhookTestPhase.Idle or WebhookTestPhase.Signing ? StepState.Pending
            : phase == WebhookTestPhase.Sending ? StepState.Active : StepState.Done);
        SetStep(2, phase == WebhookTestPhase.Succeeded ? StepState.Done : phase == WebhookTestPhase.Failed ? StepState.Failed : StepState.Pending);
        _stepLabels[0].Text = labels.StepSigned;
        _stepDetails[0].Text = labels.StepSignedDetail;
        _stepLabels[1].Text = labels.StepSent;
        _stepDetails[1].Text = labels.StepSentDetail;
        if (done && _shown is { } shown)
        {
            var response = shown.Response;
            _stepLabels[2].Text = response.TimedOut ? labels.StepTimedOut
                : response.Status == 0 ? labels.StepNoAnswer
                : labels.StepStatus(response.Status, WebhookTesterJson.Reason(response.Status));
            _stepDetails[2].Text = response.Status == 0 && !response.TimedOut && !string.IsNullOrWhiteSpace(response.Error)
                ? response.Error
                : labels.StepDuration(response.DurationMs);
        }
        else
        {
            _stepLabels[2].Text = labels.StepResponse;
            _stepDetails[2].Text = phase == WebhookTestPhase.Sending ? labels.StepResponseDetail : string.Empty;
        }
        _connectors[0].SetProgress(phase is WebhookTestPhase.Idle or WebhookTestPhase.Signing ? 0 : 1);
        _connectors[1].SetProgress(done ? 1 : 0);
        ArcTokens.Bind(_connectors[1], WebhookTesterConnector.FillBrushProperty,
            phase == WebhookTestPhase.Failed ? ArcDesignTokenKind.Danger : ArcDesignTokenKind.Accent);
        _connectors[0].IsTraveling = false;
        _connectors[1].IsTraveling = phase == WebhookTestPhase.Sending;
    }

    private enum StepState
    {
        Pending,
        Active,
        Done,
        Failed
    }

    private void SetStep(int index, StepState state)
    {
        var circle = _stepCircles[index];
        circle.Tone = state switch
        {
            StepState.Active => WebhookTesterTone.StepActive,
            StepState.Done => WebhookTesterTone.StepDone,
            StepState.Failed => WebhookTesterTone.StepFailed,
            _ => WebhookTesterTone.StepPending
        };
        circle.Content = state switch
        {
            StepState.Active => new ArcSpinner { Width = 12, Height = 12, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
            StepState.Done => WebhookTesterUi.Glyph(ArcIcons.Check, 12, ArcDesignTokenKind.Success, 2.25),
            StepState.Failed => WebhookTesterUi.Glyph(ArcIcons.X, 12, ArcDesignTokenKind.Danger, 2.25),
            _ => null
        };
        circle.HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        circle.VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center;
    }

    /// <summary>What the response panel shows for <paramref name="delivery"/> under the chosen tab.</summary>
    private (string Text, bool IsJson, bool IsPlaceholder) PanelContent(WebhookTestDelivery? delivery)
    {
        var labels = Labels;
        if (delivery is null)
        {
            return (labels.NothingSent, false, true);
        }
        var response = delivery.Response;
        switch (_tab)
        {
            case ResponseTab.Body:
            {
                var body = response.Body;
                if (string.IsNullOrWhiteSpace(body))
                {
                    return response.Status == 0 && !string.IsNullOrWhiteSpace(response.Error)
                        ? (response.Error!, false, false)
                        : (labels.EmptyBody, false, true);
                }
                return WebhookTesterJson.Check(body).IsValid ? (WebhookTesterJson.Format(body), true, false) : (body, false, false);
            }
            case ResponseTab.Headers:
            {
                if (response.Headers is not { Count: > 0 } headers)
                {
                    return (labels.EmptyHeaders, false, true);
                }
                return (string.Join('\n', headers.Select(h => $"{h.Key}: {h.Value}")), false, false);
            }
            default:
            {
                var request = delivery.Request;
                var text = new StringBuilder();
                text.Append("POST ").Append(request.Url).Append('\n');
                foreach (var header in request.Headers)
                {
                    text.Append(header.Key).Append(": ").Append(header.Value).Append('\n');
                }
                text.Append('\n').Append(request.Body);
                return (text.ToString(), false, false);
            }
        }
    }

    private void RefreshResponse(WebhookTestDelivery? delivery)
    {
        if (_panel is null)
        {
            return;
        }
        var (text, isJson, placeholder) = PanelContent(delivery);
        _panel.IsTinted = isJson;
        _panel.TextWrapping = placeholder ? Avalonia.Media.TextWrapping.Wrap : Avalonia.Media.TextWrapping.NoWrap;
        _panel.Text = text;
        _panel.Classes.Set("placeholder", placeholder);
        _copyPanel!.Value = placeholder ? null : text;
        _copyPanel.IsEnabled = !placeholder;
    }

    #endregion
}
