using Avalonia.Controls;
using Avalonia.Threading;

namespace TKCode.Arc;

// Switching, creating and joining: the app's async delegates or the built-in simulation.
public partial class TeamSwitcher
{
    private CancellationTokenSource? _switchCts;
    private TeamSwitcherWorkspace? _pending;
    private string? _error;
    private bool _settingValue;
    private int _errorGeneration;

    /// <summary>How long a switch error stays on the trigger before the plan line comes back.</summary>
    internal static readonly TimeSpan ErrorDuration = TimeSpan.FromSeconds(4);

    /// <summary>The workspace being loaded, or null.</summary>
    internal TeamSwitcherWorkspace? PendingWorkspace => _pending;

    /// <summary>Whether a workspace is loading.</summary>
    internal bool IsSwitching => _pending is not null;

    /// <summary>The message of the last failed switch while it shows, or null.</summary>
    internal string? ErrorText => _error;

    /// <summary>
    /// Switches to the workspace with <paramref name="id"/> the way picking it does: the trigger shows it with a loading
    /// state while <see cref="SwitchHandler"/> runs (or the simulated load), then <see cref="Value"/> changes. Finishes when
    /// the switch has finished, failed or been replaced; an unknown id does nothing.
    /// </summary>
    /// <param name="id">The id of a workspace in the list.</param>
    public Task SwitchToAsync(string id)
    {
        var workspace = EffectiveWorkspaces.FirstOrDefault(w => w.Id == id);
        return workspace is null ? Task.CompletedTask : SwitchToAsync(workspace);
    }

    /// <summary>Picks a workspace from the menu or a shortcut: the menu closes (focus back on the trigger) and it loads.</summary>
    internal void Choose(TeamSwitcherWorkspace workspace)
    {
        CloseMenu(returnFocus: IsOpen);
        _ = SwitchToAsync(workspace);
    }

    /// <summary>
    /// Loads <paramref name="workspace"/>: the trigger shows it at once with a shimmering "Switching…" and a spinner, then
    /// its plan once the load finished (and <see cref="Value"/> changed); a failure goes back to the previous workspace and
    /// shows the message. A newer switch cancels this one.
    /// </summary>
    internal async Task SwitchToAsync(TeamSwitcherWorkspace workspace)
    {
        var current = CurrentWorkspace;
        if (ReferenceEquals(workspace, current) || workspace.Id == current?.Id)
        {
            if (_pending is not null)
            {
                // Back to where we are: the load in flight is abandoned.
                CancelSwitch();
                UpdateFace();
            }
            return;
        }
        if (ReferenceEquals(workspace, _pending))
        {
            return;
        }
        CancelSwitch();
        var cts = new CancellationTokenSource();
        _switchCts = cts;
        _pending = workspace;
        _error = null;
        UpdateFace();
        var labels = Labels;
        Announce(labels.SwitchingTo(workspace.Name));
        var handler = SwitchHandler;
        try
        {
            if (handler is null)
            {
                await Delay(cts.Token);
            }
            else
            {
                await handler(workspace, cts.Token);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return;
        }
        catch (Exception error)
        {
            if (!ReferenceEquals(_switchCts, cts))
            {
                return;
            }
            _switchCts = null;
            _pending = null;
            ShowError(string.IsNullOrWhiteSpace(error.Message) ? labels.GenericError : error.Message);
            Announce(labels.SwitchFailed(workspace.Name, _error!));
            RaiseEvent(new TeamSwitcherSwitchFailedEventArgs(SwitchFailedEvent, workspace, error));
            return;
        }
        if (!ReferenceEquals(_switchCts, cts))
        {
            return;
        }
        _switchCts = null;
        _pending = null;
        _settingValue = true;
        try
        {
            SetCurrentValue(ValueProperty, workspace.Id);
        }
        finally
        {
            _settingValue = false;
        }
        UpdateFace();
        _menu?.RefreshRows();
        Announce(labels.WithSimulated(labels.SwitchedTo(workspace.Name), handler is null));
        RaiseEvent(new TeamSwitcherValueChangedEventArgs(ValueChangedEvent, workspace, current));
    }

    private Task Delay(CancellationToken token) =>
        SimulatedDelay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(SimulatedDelay, token);

    private void CancelSwitch()
    {
        _pending = null;
        if (_switchCts is { } cts)
        {
            _switchCts = null;
            cts.Cancel();
        }
    }

    /// <summary>The app set <see cref="Value"/>: a load in flight is abandoned and the trigger shows the new workspace.</summary>
    private void OnValueChanged()
    {
        if (!_settingValue)
        {
            CancelSwitch();
            _error = null;
        }
        UpdateFace();
        _menu?.RefreshRows();
    }

    private void ShowError(string message)
    {
        _error = message;
        UpdateFace();
        var generation = ++_errorGeneration;
        DispatcherTimer.RunOnce(() =>
        {
            if (generation == _errorGeneration && _error is not null && _pending is null)
            {
                _error = null;
                UpdateFace();
            }
        }, ErrorDuration);
    }

    /// <summary>Brings the trigger in line: the workspace (pending or active), its line and the busy state.</summary>
    private void UpdateFace()
    {
        var labels = Labels;
        var shown = _pending ?? CurrentWorkspace;
        PseudoClasses.Set(SwitchingPseudoClass, _pending is not null);
        PseudoClasses.Set(ErrorPseudoClass, _error is not null && _pending is null);
        if (_face is not null)
        {
            _face.EmptyName = labels.NoWorkspace;
            if (_pending is not null)
            {
                _face.Show(shown, TeamSwitcherFaceLine.Switching, labels.Switching);
            }
            else if (_error is not null)
            {
                _face.Show(shown, TeamSwitcherFaceLine.Error, _error);
            }
            else
            {
                _face.Show(shown, TeamSwitcherFaceLine.Plan, shown is null ? string.Empty : labels.PlanLine(shown.Plan, shown.Members));
            }
        }
        if (_trigger is not null)
        {
            var name = shown is null ? labels.NoWorkspace : labels.Trigger(shown.Name, shown.Plan);
            Avalonia.Automation.AutomationProperties.SetName(_trigger, name);
            // Arc: aria-busy while switching.
            Avalonia.Automation.AutomationProperties.SetItemStatus(_trigger, _pending is not null ? labels.Switching : _error);
        }
    }

    /// <summary>Creates or joins through the app's delegate, or the simulated one (which validates and makes the workspace).</summary>
    internal async Task<(TeamSwitcherWorkspace? Workspace, bool Simulated)> AddAsync(TeamSwitcherAddKind kind, string text,
        CancellationToken token)
    {
        var handler = kind == TeamSwitcherAddKind.Created ? CreateHandler : JoinHandler;
        if (handler is not null)
        {
            return (await handler(text, token), false);
        }
        await Delay(token);
        token.ThrowIfCancellationRequested();
        var existing = EffectiveWorkspaces;
        var made = kind == TeamSwitcherAddKind.Created
            ? TeamSwitcherLogic.Create(text, existing, Labels, ActiveSimulation?.CreatedPlan, ActiveSimulation?.CreatedRole)
            : TeamSwitcherLogic.Join(text, existing, Labels, SimulatedInvites);
        return (made, true);
    }

    /// <summary>A workspace was created or joined: it joins the list (by id), the menu closes and the switcher moves there.</summary>
    internal void Added(TeamSwitcherWorkspace? workspace, TeamSwitcherAddKind kind, bool simulated)
    {
        var labels = Labels;
        CloseMenu(returnFocus: true);
        if (workspace is null)
        {
            return;
        }
        if (!EffectiveWorkspaces.Any(w => w.Id == workspace.Id))
        {
            _added.Add(workspace);
            WorkspacesChanged();
        }
        var known = EffectiveWorkspaces.First(w => w.Id == workspace.Id);
        var said = kind == TeamSwitcherAddKind.Created ? labels.Created(known.Name) : labels.Joined(known.Name);
        Announce(labels.WithSimulated(said, simulated));
        RaiseEvent(new TeamSwitcherWorkspaceAddedEventArgs(WorkspaceAddedEvent, known, kind, simulated));
        _ = SwitchToAsync(known);
    }
}
