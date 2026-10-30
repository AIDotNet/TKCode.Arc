using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class ConnectorSetup
{
    private CancellationTokenSource? _script;
    private ViewportVisibility? _viewport;
    private bool _played;
    private TopLevel? _listening;

    /// <summary>The scripted run stops at the first pointer or key press anywhere in the window, so it listens there.</summary>
    private void ListenForInteraction(bool listen)
    {
        var top = listen ? TopLevel.GetTopLevel(this) : null;
        if (ReferenceEquals(top, _listening))
        {
            return;
        }
        if (_listening is not null)
        {
            _listening.RemoveHandler(PointerPressedEvent, OnInteractionPointer);
            _listening.RemoveHandler(KeyDownEvent, OnInteractionKey);
        }
        _listening = top;
        if (top is not null)
        {
            top.AddHandler(PointerPressedEvent, OnInteractionPointer, RoutingStrategies.Tunnel, handledEventsToo: true);
            top.AddHandler(KeyDownEvent, OnInteractionKey, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
    }

    private void OnInteractionPointer(object? sender, PointerPressedEventArgs e) => StopAutoPlay();

    private void OnInteractionKey(object? sender, KeyEventArgs e) => StopAutoPlay();

    /// <summary>
    /// How the simulation and the scripted run wait (tests replace it). It must complete on the UI thread's context or
    /// throw <see cref="OperationCanceledException"/> when the token is cancelled.
    /// </summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = Task.Delay;

    /// <summary>The running scripted run, if any (tests await it).</summary>
    internal Task? ScriptTask { get; private set; }

    /// <summary>Whether the scripted run is playing.</summary>
    internal bool IsAutoPlaying => _script is not null;

    /// <summary>Starts connecting an app (Connect and Retry do this). Does nothing while it connects or is connected.</summary>
    public void StartConnect(string id)
    {
        if (Find(id) is { } row && row.Phase is ConnectorPhase.Idle or ConnectorPhase.Failed)
        {
            _ = ConnectAsync(row, failure: null);
        }
    }

    /// <summary>Cancels a running connect: the handler's token is cancelled and the row returns to rest.</summary>
    public void CancelConnect(string id)
    {
        if (Find(id) is not { IsBusy: true } row)
        {
            return;
        }
        CancelRow(row);
        row.SetIdle();
        Announce(Labels.AnnounceCancelled(row.App.Name), IsSimulated);
    }

    /// <summary>Opens or closes the access details of a connected app (the Connected button does this).</summary>
    public void SetAccessOpen(string id, bool open)
    {
        Find(id)?.SetExpanded(open);
    }

    /// <summary>Whether the access details of an app are open.</summary>
    public bool IsAccessOpen(string id) => Find(id)?.IsExpanded == true;

    internal void ToggleDetails(string id)
    {
        if (Find(id) is { } row)
        {
            row.SetExpanded(!row.IsExpanded);
        }
    }

    /// <summary>Revokes access to a connected app (the confirmation's Disconnect does this).</summary>
    public void Disconnect(string id)
    {
        if (Find(id) is { Phase: ConnectorPhase.Connected } row)
        {
            _ = DisconnectAsync(row);
        }
    }

    private static void CancelRow(ConnectorSetupRow row)
    {
        row.Run++;
        var cts = row.Cts;
        row.Cts = null;
        if (cts is not null)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    /// <summary>Runs one connect: the handler (or the simulation) reports the steps, then the row shows the result.</summary>
    internal async Task ConnectAsync(ConnectorSetupRow row, string? failure)
    {
        CancelRow(row);
        var cts = new CancellationTokenSource();
        row.Cts = cts;
        var run = row.Run;
        var simulated = ConnectHandler is null;
        var handler = ConnectHandler;
        row.BeginConnect();
        Announce(Labels.AnnounceConnecting(row.App.Name), simulated);
        var context = new ConnectorConnectContext(cts.Token, (phase, synced, total) =>
        {
            if (run == row.Run)
            {
                row.Report(phase, synced, total);
            }
        });
        try
        {
            var result = handler is null
                ? await ConnectorSetupSimulatedFlow.ConnectAsync(row.App, context, Delay, failure)
                : await handler(row.App, context);
            if (run != row.Run)
            {
                return;
            }
            result ??= new ConnectorConnectResult();
            var scopes = result.Scopes ?? [.. row.App.Scopes];
            var finished = new ConnectorConnectResult
            {
                Account = result.Account ?? row.App.Account,
                Scopes = scopes,
                Synced = result.Synced ?? (row.Phase == ConnectorPhase.Syncing && row.Synced > 0 ? row.Synced : null),
            };
            Release(row, cts);
            row.SetConnected(finished.Account, scopes, finished.Synced);
            Announce(Labels.AnnounceConnected(row.App.Name, row.ConnectedDetail()), simulated);
            RaiseEvent(new ConnectorConnectedEventArgs(AppConnectedEvent, row.App, finished));
            WriteConnectedIds();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested || run != row.Run)
        {
            // Cancel already returned the row to rest.
        }
        catch (Exception ex)
        {
            if (run != row.Run)
            {
                return;
            }
            Release(row, cts);
            var reason = string.IsNullOrWhiteSpace(ex.Message) ? Labels.ConnectFailed : ex.Message;
            row.SetFailed(reason);
            Announce(Labels.AnnounceFailed(row.App.Name, reason), simulated);
        }
    }

    private static void Release(ConnectorSetupRow row, CancellationTokenSource cts)
    {
        if (ReferenceEquals(row.Cts, cts))
        {
            row.Cts = null;
        }
        cts.Dispose();
    }

    private async Task DisconnectAsync(ConnectorSetupRow row)
    {
        CancelRow(row);
        var cts = new CancellationTokenSource();
        row.Cts = cts;
        var run = row.Run;
        var handler = DisconnectHandler;
        var simulated = handler is null;
        row.SetDisconnecting();
        try
        {
            if (handler is null)
            {
                await Delay(ConnectorSetupSimulatedFlow.DisconnectTime, cts.Token);
            }
            else
            {
                await handler(row.App, cts.Token);
            }
            if (run != row.Run)
            {
                return;
            }
            Release(row, cts);
            row.SetIdle();
            Announce(Labels.AnnounceDisconnected(row.App.Name), simulated);
            RaiseEvent(new ConnectorAppEventArgs(AppDisconnectedEvent, row.App));
            WriteConnectedIds();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested || run != row.Run)
        {
        }
        catch (Exception ex)
        {
            if (run != row.Run)
            {
                return;
            }
            Release(row, cts);
            row.SetConnected(row.Account, row.Scopes, row.Total);
            Announce(Labels.AnnounceFailed(row.App.Name, string.IsNullOrWhiteSpace(ex.Message) ? Labels.ConnectFailed : ex.Message), simulated);
        }
    }

    // The scripted run ----------------------------------------------------------------------------------------------------

    /// <summary>Stops the scripted run (the first pointer or key press does this); a connect it started finishes.</summary>
    public void StopAutoPlay()
    {
        _played = true;
        _viewport?.Dispose();
        _viewport = null;
        HaltScript();
    }

    private void HaltScript()
    {
        if (_script is not null)
        {
            _played = true;
        }
        var script = _script;
        _script = null;
        if (script is not null)
        {
            script.Cancel();
            script.Dispose();
        }
        if (_viewport is null)
        {
            ListenForInteraction(false);
        }
    }

    private void UpdateAutoPlay()
    {
        if (!EffectiveAutoPlay)
        {
            HaltScript();
            _viewport?.Dispose();
            _viewport = null;
            ListenForInteraction(false);
            return;
        }
        if (_played || _script is not null || _viewport is not null || VisualRoot is null)
        {
            return;
        }
        // The run starts only once the card is on screen.
        _viewport = new ViewportVisibility(this, threshold: 0.25);
        _viewport.VisibilityChanged += OnViewportVisibility;
        ListenForInteraction(true);
    }

    private void OnViewportVisibility(object? sender, bool visible)
    {
        if (!visible || _played || _script is not null || !EffectiveAutoPlay)
        {
            return;
        }
        _viewport = null;
        _played = true;
        _script = new CancellationTokenSource();
        ScriptTask = PlayAsync(_script.Token);
    }

    /// <summary>The simulation's scripted run: each step waits its pause, then connects its app if it is idle or failed.</summary>
    internal async Task PlayAsync(CancellationToken token)
    {
        try
        {
            foreach (var step in Simulation?.Steps?.ToList() ?? [])
            {
                if (step is null)
                {
                    continue;
                }
                await Delay(step.Pause, token);
                token.ThrowIfCancellationRequested();
                if (Find(step.AppId) is { Phase: ConnectorPhase.Idle or ConnectorPhase.Failed } row)
                {
                    await ConnectAsync(row, step.Failure);
                }
                token.ThrowIfCancellationRequested();
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _script?.Dispose();
                    _script = null;
                    ListenForInteraction(false);
                });
            }
        }
    }
}
