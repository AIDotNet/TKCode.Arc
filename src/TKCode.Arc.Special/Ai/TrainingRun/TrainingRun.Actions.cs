using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ArcButton = TKCode.Arc.Button;

namespace TKCode.Arc;

public sealed partial class TrainingRun
{
    /// <summary>Where a deploy is.</summary>
    internal enum DeployState
    {
        Idle,
        Deploying,
        Deployed,
        Failed
    }

    private ArcButton? _cancel;
    private ArcButton? _pause;
    private ArcButton? _resume;
    private ArcButton? _retry;
    private ArcButton? _restart;
    private ArcButton? _deploy;
    private CopyButton? _copyEndpoint;
    private bool _confirmingCancel;
    private ITimer? _confirmTimer;
    private long _confirmToken;
    private DeployState _deployState;
    private CancellationTokenSource? _deployCancellation;
    private string? _endpoint;
    private string? _deployError;

    /// <summary>True between the first press on Cancel and the second (or the end of the 3 second window).</summary>
    internal bool IsConfirmingCancel => _confirmingCancel;

    internal DeployState Deploy => _deployState;

    internal string? Endpoint => _endpoint;

    internal ArcButton? CancelButton => _cancel;

    internal ArcButton? PauseButton => _pause;

    internal ArcButton? ResumeButton => _resume;

    internal ArcButton? RetryButton => _retry;

    internal ArcButton? RestartButton => _restart;

    internal ArcButton? DeployButton => _deploy;

    internal CopyButton? CopyEndpointButton => _copyEndpoint;

    private void HookButtons()
    {
        if (_cancel is not null)
        {
            _cancel.Click += OnCancel;
        }
        if (_pause is not null)
        {
            _pause.Click += OnPause;
        }
        if (_resume is not null)
        {
            _resume.Click += OnResume;
        }
        if (_retry is not null)
        {
            _retry.Click += OnRetry;
        }
        if (_restart is not null)
        {
            _restart.Click += OnRestart;
        }
        if (_deploy is not null)
        {
            _deploy.Click += OnDeploy;
        }
    }

    private void UnhookButtons()
    {
        if (_cancel is not null)
        {
            _cancel.Click -= OnCancel;
        }
        if (_pause is not null)
        {
            _pause.Click -= OnPause;
        }
        if (_resume is not null)
        {
            _resume.Click -= OnResume;
        }
        if (_retry is not null)
        {
            _retry.Click -= OnRetry;
        }
        if (_restart is not null)
        {
            _restart.Click -= OnRestart;
        }
        if (_deploy is not null)
        {
            _deploy.Click -= OnDeploy;
        }
    }

    /// <summary>Each button shows when its action is allowed and the status calls for it.</summary>
    private void RefreshButtons(TrainingRunLabels labels, TrainingRunStatus status)
    {
        var actions = Actions;
        var live = status is TrainingRunStatus.Queued or TrainingRunStatus.Running or TrainingRunStatus.Paused;
        if (!live || !actions.HasFlag(TrainingRunActions.Cancel))
        {
            EndConfirmCancel();
        }
        if (_cancel is not null)
        {
            _cancel.IsVisible = live && actions.HasFlag(TrainingRunActions.Cancel);
            _cancel.Content = _confirmingCancel ? labels.ConfirmCancel : labels.Cancel;
            _cancel.Variant = _confirmingCancel ? ButtonVariant.Danger : ButtonVariant.Secondary;
        }
        if (_pause is not null)
        {
            _pause.IsVisible = status == TrainingRunStatus.Running && actions.HasFlag(TrainingRunActions.Pause);
            _pause.Content = labels.Pause;
        }
        if (_resume is not null)
        {
            _resume.IsVisible = status == TrainingRunStatus.Paused && actions.HasFlag(TrainingRunActions.Resume);
            _resume.Content = labels.Resume;
        }
        if (_retry is not null)
        {
            _retry.IsVisible = status == TrainingRunStatus.Failed && actions.HasFlag(TrainingRunActions.Retry);
            _retry.Content = labels.RetryFromCheckpoint;
        }
        if (_restart is not null)
        {
            _restart.IsVisible = status is TrainingRunStatus.Completed or TrainingRunStatus.Failed or TrainingRunStatus.Cancelled
                                 && actions.HasFlag(TrainingRunActions.Restart);
            _restart.Content = labels.RunAgain;
        }
        if (_deploy is not null)
        {
            var canDeploy = actions.HasFlag(TrainingRunActions.Deploy) && (DeployHandler is not null || IsSimulated);
            _deploy.IsVisible = status == TrainingRunStatus.Completed && canDeploy && _deployState != DeployState.Deployed;
            _deploy.Content = _deployState == DeployState.Failed ? labels.RetryDeploy : labels.Deploy;
            _deploy.IsLoading = _deployState == DeployState.Deploying;
            AutomationProperties.SetName(_deploy, _deployState == DeployState.Deploying ? labels.Deploying
                : _deployState == DeployState.Failed ? labels.RetryDeploy : labels.Deploy);
        }
    }

    private void OnPause(object? sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(PauseRequestedEvent));
        if (IsSimulated)
        {
            Player.Pause();
            AfterSimulationAction();
        }
    }

    private void OnResume(object? sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(ResumeRequestedEvent));
        if (IsSimulated)
        {
            Player.Resume();
            AfterSimulationAction();
        }
    }

    /// <summary>The first press turns Cancel into Confirm cancel for 3 seconds; the second press cancels.</summary>
    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        if (!_confirmingCancel)
        {
            _confirmingCancel = true;
            var token = ++_confirmToken;
            _confirmTimer?.Dispose();
            _confirmTimer = Clock.CreateTimer(_ => Dispatcher.UIThread.Post(() =>
            {
                if (token == _confirmToken)
                {
                    EndConfirmCancel();
                    Refresh();
                }
            }), null, ConfirmCancelWindow, Timeout.InfiniteTimeSpan);
            _announcer?.Announce(Labels.ConfirmCancelNotice);
            Refresh();
            return;
        }
        EndConfirmCancel();
        RaiseEvent(new RoutedEventArgs(CancelRequestedEvent));
        if (IsSimulated)
        {
            Player.Cancel();
            AfterSimulationAction();
        }
        else
        {
            Refresh();
        }
    }

    private void EndConfirmCancel()
    {
        _confirmToken++;
        _confirmTimer?.Dispose();
        _confirmTimer = null;
        _confirmingCancel = false;
    }

    private void OnRetry(object? sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(RetryRequestedEvent));
        if (IsSimulated)
        {
            Player.Retry();
            AfterSimulationAction();
        }
    }

    private void OnRestart(object? sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(RestartRequestedEvent));
        if (IsSimulated)
        {
            Player.Restart();
            ResetDeploy();
            AfterSimulationAction();
        }
    }

    private void AfterSimulationAction()
    {
        Refresh();
        UpdateSimulationLoop();
    }

    /// <summary>
    /// Runs <see cref="DeployHandler"/> (or the simulated deploy): the button loads and keeps focus, then the footer shows the
    /// endpoint with a copy button (which takes the focus if Deploy had it), or the failure with Retry deploy.
    /// </summary>
    private async void OnDeploy(object? sender, RoutedEventArgs e)
    {
        if (_deployState == DeployState.Deploying || EffectiveStatus != TrainingRunStatus.Completed)
        {
            return;
        }
        var handler = DeployHandler ?? (IsSimulated ? Player.DeployAsync : null);
        if (handler is null)
        {
            return;
        }
        _deployCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _deployCancellation = cancellation;
        _deployState = DeployState.Deploying;
        _deployError = null;
        Refresh();
        try
        {
            var endpoint = await handler(cancellation.Token);
            if (cancellation.IsCancellationRequested || !ReferenceEquals(_deployCancellation, cancellation))
            {
                return;
            }
            var hadFocus = _deploy?.IsFocused == true;
            _endpoint = endpoint;
            _deployState = DeployState.Deployed;
            Refresh();
            _announcer?.Announce(Labels.Deployed);
            if (hadFocus && _copyEndpoint is { } copy)
            {
                // Deploy clicked with the mouse hands focus on without the keyboard ring.
                var method = SpecialFocusModality.For(this);
                Dispatcher.UIThread.Post(() => copy.Focus(method), DispatcherPriority.Loaded);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Aborted: the card left, or the run left Completed.
        }
        catch (Exception ex)
        {
            if (!ReferenceEquals(_deployCancellation, cancellation))
            {
                return;
            }
            _deployState = DeployState.Failed;
            _deployError = string.IsNullOrWhiteSpace(ex.Message) ? null : ex.Message;
            Refresh();
            _announcer?.Announce(_deployError ?? Labels.DeployFailed);
        }
    }

    /// <summary>Aborts a deploy in flight and forgets its result.</summary>
    private void ResetDeploy()
    {
        if (_deployState == DeployState.Idle && _deployCancellation is null)
        {
            return;
        }
        _deployCancellation?.Cancel();
        _deployCancellation = null;
        _deployState = DeployState.Idle;
        _endpoint = null;
        _deployError = null;
    }
}
