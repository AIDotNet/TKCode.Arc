using Avalonia;
using Avalonia.Automation;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public sealed partial class AgentStatus
{
    /// <summary>What a button currently is; the same button changes roles (Pause, Resume, Approve) so focus stays on it.</summary>
    private sealed record ButtonState(AgentStatusActions Action, string Label, Geometry? Icon, bool Primary, bool IconOnly, bool Inline, bool TrailingIcon)
    {
        public IDisposable? Bindings { get; set; }
    }

    private sealed class BindingGroup : IDisposable
    {
        private readonly IDisposable[] _bindings;

        public BindingGroup(params IDisposable[] bindings) => _bindings = bindings;

        public void Dispose()
        {
            foreach (var binding in _bindings)
            {
                binding.Dispose();
            }
        }
    }

    private void SetButton(Button button, bool visible, AgentStatusActions action, string label, Geometry? icon, bool primary,
        bool iconOnly, bool inline, bool trailingIcon = false)
    {
        if (!visible)
        {
            if (button.IsVisible && (button.IsFocused || button.IsKeyboardFocusWithin))
            {
                // The focused control goes away: focus the pill, never the page.
                FocusHead();
            }
            button.IsVisible = false;
            return;
        }
        var state = new ButtonState(action, label, icon, primary, iconOnly, inline, trailingIcon);
        var current = button.Tag as ButtonState;
        if (current is null || current.Action != action || current.Label != label || !ReferenceEquals(current.Icon, icon)
            || current.Primary != primary || current.IconOnly != iconOnly || current.Inline != inline)
        {
            if (current is null || current.Primary != primary || current.Inline != inline || current.IconOnly != iconOnly)
            {
                current?.Bindings?.Dispose();
                ConfigureButton(button, state);
            }
            else
            {
                state.Bindings = current.Bindings;
            }
            button.Tag = state;
            button.Content = BuildContent(label, icon, iconOnly, trailingIcon);
            AutomationProperties.SetName(button, label);
        }
        button.IsVisible = true;
    }

    private static void ConfigureButton(Button button, ButtonState state)
    {
        button.Variant = state.Primary ? ButtonVariant.Primary : ButtonVariant.Secondary;
        if (state.Primary)
        {
            state.Bindings = new BindingGroup(
                ArcTokens.Bind(button, Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty, ArcDesignTokenKind.Accent, BindingPriority.LocalValue),
                ArcTokens.Bind(button, Avalonia.Controls.Primitives.TemplatedControl.BorderBrushProperty, ArcDesignTokenKind.Accent, BindingPriority.LocalValue),
                ArcTokens.Bind(button, Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty, ArcDesignTokenKind.AccentForeground, BindingPriority.LocalValue));
        }
        if (state.Inline)
        {
            button.MinHeight = 30;
            button.Height = 30;
            button.CornerRadius = new CornerRadius(15);
            button.Padding = state.IconOnly ? default : new Thickness(12, 0);
            button.Width = state.IconOnly ? 30 : double.NaN;
        }
        else
        {
            button.ClearValue(Layoutable.MinHeightProperty);
            button.ClearValue(Layoutable.HeightProperty);
            button.ClearValue(Avalonia.Controls.Primitives.TemplatedControl.CornerRadiusProperty);
            button.Height = 36;
            button.Padding = state.IconOnly ? default : new Thickness(12, 0);
            button.Width = state.IconOnly ? 36 : double.NaN;
        }
        button.VerticalAlignment = VerticalAlignment.Center;
    }

    private static Control BuildContent(string label, Geometry? icon, bool iconOnly, bool trailing)
    {
        if (iconOnly && icon is not null)
        {
            return new ArcGlyph { Data = icon, Size = 16 };
        }
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        if (icon is not null && !trailing)
        {
            panel.Children.Add(new ArcGlyph { Data = icon, Size = 16, VerticalAlignment = VerticalAlignment.Center });
        }
        panel.Children.Add(text);
        if (icon is not null && trailing)
        {
            panel.Children.Add(new ArcGlyph { Data = icon, Size = 16, VerticalAlignment = VerticalAlignment.Center });
        }
        return panel;
    }

    /// <summary>Shows the buttons the state and <see cref="Actions"/> ask for: inline in the pill, and in the open panel.</summary>
    private void UpdateButtons()
    {
        var labels = Labels;
        var actions = Actions;
        var state = _display;
        var inline = InlineVisible();
        var iconOnly = _compact;
        SetButton(_inlineDeny, inline && state == AgentStatusRunState.NeedsInput && (actions & AgentStatusActions.Deny) != 0,
            AgentStatusActions.Deny, labels.Deny, ArcIcons.X, primary: false, iconOnly, inline: true);
        SetButton(_inlineApprove, inline && state == AgentStatusRunState.NeedsInput && (actions & AgentStatusActions.Approve) != 0,
            AgentStatusActions.Approve, labels.Approve, ArcIcons.Check, primary: true, iconOnly, inline: true);
        SetButton(_inlineRetry, inline && state == AgentStatusRunState.Failed && (actions & AgentStatusActions.Retry) != 0,
            AgentStatusActions.Retry, labels.Retry, ArcIcons.RotateCcw, primary: true, iconOnly, inline: true);
        var anyInline = _inlineDeny.IsVisible || _inlineApprove.IsVisible || _inlineRetry.IsVisible;
        if (anyInline && !_actionsShown)
        {
            _actions.IsVisible = true;
            _actionsOpacity.Jump(0);
            _actions.Opacity = 0;
            _ = _actionsOpacity.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(140), ArcEasings.Standard));
        }
        else if (!anyInline)
        {
            _actions.IsVisible = false;
        }
        _actionsShown = anyInline;

        // The panel's footer: the first two buttons keep their place and change role, so a focused one stays focused.
        var (firstAction, firstLabel, firstIcon, firstPrimary) = state switch
        {
            AgentStatusRunState.Working when (actions & AgentStatusActions.Pause) != 0 => (AgentStatusActions.Pause, labels.Pause, (Geometry?)ArcIcons.Pause, false),
            AgentStatusRunState.Paused when (actions & AgentStatusActions.Resume) != 0 => (AgentStatusActions.Resume, labels.Resume, ArcIcons.Play, true),
            AgentStatusRunState.NeedsInput when (actions & AgentStatusActions.Approve) != 0 => (AgentStatusActions.Approve, labels.Approve, ArcIcons.Check, true),
            AgentStatusRunState.Failed when (actions & AgentStatusActions.Retry) != 0 => (AgentStatusActions.Retry, labels.Retry, ArcIcons.RotateCcw, true),
            _ => (AgentStatusActions.None, string.Empty, null, false)
        };
        var (secondAction, secondLabel, secondIcon) = state switch
        {
            AgentStatusRunState.Working or AgentStatusRunState.Paused when (actions & AgentStatusActions.Stop) != 0 => (AgentStatusActions.Stop, labels.Stop, (Geometry?)ArcIcons.Square),
            AgentStatusRunState.NeedsInput when (actions & AgentStatusActions.Deny) != 0 => (AgentStatusActions.Deny, labels.Deny, ArcIcons.X),
            _ => (AgentStatusActions.None, string.Empty, null)
        };
        SetButton(_primary, firstAction != AgentStatusActions.None, firstAction, firstLabel, firstIcon, firstPrimary, false, inline: false);
        SetButton(_secondary, secondAction != AgentStatusActions.None, secondAction, secondLabel, secondIcon, false, false, inline: false);
        var openLabel = _opened ? labels.Opened : labels.OpenRun;
        SetButton(_openRun, (actions & AgentStatusActions.OpenRun) != 0, AgentStatusActions.OpenRun, openLabel,
            _opened ? ArcIcons.Check : ArcIcons.ArrowUpRight, primary: false, iconOnly: _compact, inline: false, trailingIcon: true);
        if (_openRun.IsVisible)
        {
            _openRun.Variant = ButtonVariant.Ghost;
        }
        _footer.IsVisible = _primary.IsVisible || _secondary.IsVisible || _openRun.IsVisible;
    }

    /// <summary>Fills the panel: the agent and the counter, the segments, the latest steps and the empty text.</summary>
    private void UpdateBody(DateTimeOffset now)
    {
        var labels = Labels;
        var agentName = AgentName;
        var all = EffSteps;
        var shown = new List<AgentStatusStep>(Math.Min(all.Count, MaxSteps));
        foreach (var step in all)
        {
            if (step.State != AgentStatusStepState.Pending)
            {
                shown.Add(step);
            }
        }
        if (shown.Count > MaxSteps)
        {
            shown.RemoveRange(0, shown.Count - MaxSteps);
        }
        var states = new AgentStatusStepState[all.Count];
        var finished = 0;
        var current = -1;
        for (var i = 0; i < all.Count; i++)
        {
            var s = all[i].State;
            states[i] = s;
            if (s is AgentStatusStepState.Done or AgentStatusStepState.Skipped)
            {
                finished++;
            }
            else if (current < 0 && s is AgentStatusStepState.Active or AgentStatusStepState.Waiting or AgentStatusStepState.Failed)
            {
                current = i;
            }
        }
        _agentNameText.Text = agentName;
        var live = _display is AgentStatusRunState.Working or AgentStatusRunState.NeedsInput or AgentStatusRunState.Paused or AgentStatusRunState.Failed;
        _counterText.Text = all.Count == 0
            ? string.Empty
            : live ? labels.StepOf(current >= 0 ? current + 1 : Math.Min(all.Count, finished + 1), all.Count) : labels.StepsFinished(finished, all.Count);
        _segments.RunProgressName = labels.RunProgress;
        _segments.SetStates(states, finished, labels.ProgressValueText(finished, all.Count));
        _stepList.IsPaused = _display == AgentStatusRunState.Paused;
        _stepList.SetSteps(shown, animate: _bodyOpacity.Value > 0.05 && _body.IsVisible);
        _empty.Text = labels.EmptyPanel(agentName);
        _empty.IsVisible = shown.Count == 0;
        AutomationProperties.SetName(_body, labels.RunDetails(agentName));
        RetargetPanelHeight(animate: true);
    }

    /// <summary>Raises the action's event and, with a simulation, applies it to the run (tests call it directly).</summary>
    internal void Invoke(AgentStatusActions action)
    {
        var routed = action switch
        {
            AgentStatusActions.Pause => PauseRequestedEvent,
            AgentStatusActions.Resume => ResumeRequestedEvent,
            AgentStatusActions.Stop => StopRequestedEvent,
            AgentStatusActions.Approve => ApproveRequestedEvent,
            AgentStatusActions.Deny => DenyRequestedEvent,
            AgentStatusActions.Retry => RetryRequestedEvent,
            AgentStatusActions.OpenRun => OpenRunRequestedEvent,
            _ => null
        };
        if (routed is null)
        {
            return;
        }
        RaiseEvent(new AgentStatusActionEventArgs(routed, action, _display));
        switch (action)
        {
            case AgentStatusActions.Pause:
                _sim?.Pause();
                break;
            case AgentStatusActions.Resume:
                _sim?.Resume();
                break;
            case AgentStatusActions.Stop:
                _sim?.Stop();
                break;
            case AgentStatusActions.Approve:
                _sim?.Approve();
                break;
            case AgentStatusActions.Deny:
                _sim?.Deny();
                break;
            case AgentStatusActions.Retry:
                _sim?.Retry();
                break;
            case AgentStatusActions.OpenRun:
                _sim?.OpenRun();
                ConfirmOpened();
                break;
        }
    }

    /// <summary>Open run reads "Opened" with a check for a moment, then goes back.</summary>
    private void ConfirmOpened()
    {
        _opened = true;
        _openedTimer?.Dispose();
        _openedTimer = TimeProvider.CreateTimer(static state => Dispatcher.UIThread.Post(((AgentStatus)state!).EndOpened), this,
            TimeSpan.FromMilliseconds(1600), Timeout.InfiniteTimeSpan);
        UpdateButtons();
        UpdateSizes();
    }

    internal void EndOpened()
    {
        _openedTimer?.Dispose();
        _openedTimer = null;
        _opened = false;
        UpdateButtons();
        UpdateSizes();
    }
}
