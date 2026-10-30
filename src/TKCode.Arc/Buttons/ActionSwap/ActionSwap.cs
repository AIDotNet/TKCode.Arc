using System.Diagnostics;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Color of an <see cref="ActionSwapState"/>: filled tones carry the primary action, tinted ones confirm a result.</summary>
public enum ActionSwapTone
{
    /// <summary>Foreground fill with background text (default).</summary>
    Neutral,

    /// <summary>Accent fill.</summary>
    Accent,

    /// <summary>A success-tinted surface with a success-colored icon.</summary>
    Success,

    /// <summary>A warning-tinted surface with a warning-colored icon.</summary>
    Warning,

    /// <summary>A quiet bordered surface, for the "already done" side of a toggle.</summary>
    Quiet
}

/// <summary>Where an <see cref="ActionSwap"/> is in a press: resting, running its action, or showing a failure.</summary>
public enum ActionSwapPhase
{
    Idle,
    Working,
    Failed
}

/// <summary>
/// One face of an <see cref="ActionSwap"/>. Its properties can be bound in XAML (for example localized labels); the
/// control follows later changes.
/// </summary>
public sealed class ActionSwapState : AvaloniaObject
{
    /// <summary>Identifies the state in <see cref="ActionSwap.Value"/>.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<ActionSwapState, string>(nameof(Id), string.Empty);

    /// <summary>The label, also the accessible name while the state is shown.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ActionSwapState, string>(nameof(Label), string.Empty);

    /// <summary>A 16px Lucide-style glyph before the label (24x24 stroke path, see <see cref="ArcIcons"/>).</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ActionSwapState, Geometry?>(nameof(Icon));

    /// <summary>The tone of the pill while this state is shown.</summary>
    public static readonly StyledProperty<ActionSwapTone> ToneProperty =
        AvaloniaProperty.Register<ActionSwapState, ActionSwapTone>(nameof(Tone));

    /// <summary>
    /// Shown while <see cref="ActionSwap.AsyncAction"/> runs on the way into this state, such as "Sending" before "Sent".
    /// Without it the pill folds into a round spinner.
    /// </summary>
    public static readonly StyledProperty<string?> PendingLabelProperty =
        AvaloniaProperty.Register<ActionSwapState, string?>(nameof(PendingLabel));

    /// <summary>How long to hold this state before moving on by itself, for brief confirmations such as "Sent". Zero holds it.</summary>
    public static readonly StyledProperty<TimeSpan> HoldForProperty =
        AvaloniaProperty.Register<ActionSwapState, TimeSpan>(nameof(HoldFor));

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <inheritdoc cref="ToneProperty"/>
    public ActionSwapTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <inheritdoc cref="PendingLabelProperty"/>
    public string? PendingLabel { get => GetValue(PendingLabelProperty); set => SetValue(PendingLabelProperty, value); }

    /// <inheritdoc cref="HoldForProperty"/>
    public TimeSpan HoldFor { get => GetValue(HoldForProperty); set => SetValue(HoldForProperty, value); }
}

/// <summary>Arguments of <see cref="ActionSwap.ValueChanged"/>.</summary>
public sealed class ActionSwapValueChangedEventArgs : RoutedEventArgs
{
    public ActionSwapValueChangedEventArgs(RoutedEvent routedEvent, ActionSwapState state) : base(routedEvent) => State = state;

    /// <summary>The state that was committed.</summary>
    public ActionSwapState State { get; }

    /// <summary>The id of <see cref="State"/>.</summary>
    public string Value => State.Id;
}

/// <summary>
/// A compact action that morphs through a small set of related states (idle, working, done, and back) and keeps its
/// place and intent. Pressing it moves to the next state, wrapping around. Width, padding, tone and icon move together
/// on springs (P5); only the changed letters of the label rise in out of a soft blur while the rest glide (P4); icons
/// trade places through a blur and a success tick wipes in (P15). An action that returns a task shows a working phase
/// (<see cref="ActionSwapState.PendingLabel"/>, or a round spinner without one) for at least 450ms; a fault shows
/// <see cref="FailureLabel"/> for 1.8s. Every change can be interrupted mid-flight. Pending keeps keyboard focus:
/// activation is swallowed instead of disabling the button.
/// </summary>
[TemplatePart(SurfacePart, typeof(Control))]
[TemplatePart(StatusPart, typeof(Control))]
[PseudoClasses(NeutralPseudoClass, AccentPseudoClass, SuccessPseudoClass, WarningPseudoClass, QuietPseudoClass, DangerPseudoClass,
    WorkingPseudoClass, FoldedPseudoClass, FailedPseudoClass, InactivePseudoClass, WidePseudoClass)]
public class ActionSwap : AvaloniaButton
{
    public const string SurfacePart = "PART_Surface";
    public const string StatusPart = "PART_Status";
    public const string NeutralPseudoClass = ":neutral";
    public const string AccentPseudoClass = ":accent";
    public const string SuccessPseudoClass = ":success";
    public const string WarningPseudoClass = ":warning";
    public const string QuietPseudoClass = ":quiet";
    public const string DangerPseudoClass = ":danger";
    public const string WorkingPseudoClass = ":working";
    public const string FoldedPseudoClass = ":folded";
    public const string FailedPseudoClass = ":failed";
    public const string InactivePseudoClass = ":inactive";
    public const string WidePseudoClass = ":wide";

    /// <summary>A fast response still shows the working shape long enough to read as a step, never as a flash.</summary>
    internal static readonly TimeSpan MinWorking = TimeSpan.FromMilliseconds(450);

    /// <summary>How long a failure stays before the control returns to its current state.</summary>
    internal static readonly TimeSpan FailureHold = TimeSpan.FromMilliseconds(1800);

    #region 公共属性定义

    /// <summary>
    /// The states, in order; at least two are needed. A press moves to the next one and wraps around. In XAML, add
    /// <see cref="ActionSwapState"/> children.
    /// </summary>
    public static readonly DirectProperty<ActionSwap, AvaloniaList<ActionSwapState>> StatesProperty =
        AvaloniaProperty.RegisterDirect<ActionSwap, AvaloniaList<ActionSwapState>>(nameof(States), o => o.States);

    /// <summary>The id of the shown state (two-way); null shows the first state. A press sets it when the next state commits.</summary>
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ActionSwap, string?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Runs on press with the next and the current state, before the control moves on. Return a task to show the working
    /// phase, null to move on at once; a faulted task, or an exception, shows <see cref="FailureLabel"/> and stays on the
    /// current state.
    /// </summary>
    public static readonly StyledProperty<Func<ActionSwapState, ActionSwapState, Task?>?> AsyncActionProperty =
        AvaloniaProperty.Register<ActionSwap, Func<ActionSwapState, ActionSwapState, Task?>?>(nameof(AsyncAction));

    /// <summary>Shown for 1.8s after a failed action (default "Try again").</summary>
    public static readonly StyledProperty<string> FailureLabelProperty =
        AvaloniaProperty.Register<ActionSwap, string>(nameof(FailureLabel), "Try again");

    /// <summary>Height: Small (36), Medium (44, default) or Large (50).</summary>
    public static readonly StyledProperty<ControlSize> SizeProperty =
        AvaloniaProperty.Register<ActionSwap, ControlSize>(nameof(Size), ControlSize.Medium);

    /// <summary>Where the control is in a press: Idle, Working while the action runs, or Failed. Read-only.</summary>
    public static readonly DirectProperty<ActionSwap, ActionSwapPhase> PhaseProperty =
        AvaloniaProperty.RegisterDirect<ActionSwap, ActionSwapPhase>(nameof(Phase), o => o.Phase);

    /// <summary>Raised when a new state commits, by a press, by its hold running out, or after its action succeeds.</summary>
    public static readonly RoutedEvent<ActionSwapValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<ActionSwap, ActionSwapValueChangedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

    private readonly AvaloniaList<ActionSwapState> _states = new();
    // The states' properties may be bound: they are followed while the control is shown (the list itself always).
    private readonly ObservedItems _observedStates;

    /// <inheritdoc cref="StatesProperty"/>
    public AvaloniaList<ActionSwapState> States => _states;

    /// <inheritdoc cref="ValueProperty"/>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="AsyncActionProperty"/>
    public Func<ActionSwapState, ActionSwapState, Task?>? AsyncAction { get => GetValue(AsyncActionProperty); set => SetValue(AsyncActionProperty, value); }

    /// <inheritdoc cref="FailureLabelProperty"/>
    public string FailureLabel { get => GetValue(FailureLabelProperty); set => SetValue(FailureLabelProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public ControlSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    private ActionSwapPhase _phase;

    /// <inheritdoc cref="PhaseProperty"/>
    public ActionSwapPhase Phase
    {
        get => _phase;
        private set => SetAndRaise(PhaseProperty, ref _phase, value);
    }

    /// <summary>Occurs when a new state commits; see <see cref="ValueChangedEvent"/>.</summary>
    public event EventHandler<ActionSwapValueChangedEventArgs>? ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    /// <summary>The shown state (the first one when <see cref="Value"/> is unset), or null without states.</summary>
    public ActionSwapState? CurrentState => ActiveIndex < 0 ? null : _states[ActiveIndex];

    /// <summary>The state a press moves to.</summary>
    public ActionSwapState? NextState => _states.Count == 0 ? null : _states[(ActiveIndex + 1) % _states.Count];

    #endregion

    private ActionSwapSurface? _surface;
    private ButtonsLiveRegion? _status;
    private ActionSwapState? _target;
    private PausableTimer? _holdTimer;
    private PausableTimer? _failureTimer;
    private string? _holdSignature;
    private int _run;

    static ActionSwap()
    {
        ValueProperty.Changed.AddClassHandler<ActionSwap>((c, _) => c.Refresh());
        FailureLabelProperty.Changed.AddClassHandler<ActionSwap>((c, _) =>
        {
            if (c._phase == ActionSwapPhase.Failed)
            {
                c.Refresh();
            }
        });
    }

    public ActionSwap()
    {
        _states.CollectionChanged += OnStatesChanged;
        _observedStates = new ObservedItems(state =>
        {
            if (state is not null)
            {
                Refresh();
            }
        });
    }

    private int ActiveIndex
    {
        get
        {
            if (_states.Count == 0)
            {
                return -1;
            }
            var id = Value;
            for (var i = 0; i < _states.Count; i++)
            {
                if (_states[i].Id == id)
                {
                    return i;
                }
            }
            return 0;
        }
    }

    private bool IsInactive => !IsEnabled || _states.Count < 2;

    private void OnStatesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Refresh();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _surface = e.NameScope.Find<ActionSwapSurface>(SurfacePart);
        _status = e.NameScope.Find<ButtonsLiveRegion>(StatusPart);
        Refresh();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _observedStates.Observe(_states);
        // The states may have changed while the control was away.
        Refresh();
        RefreshHold();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _observedStates.Observe(null);
        // Pending work finishes into nothing, and no timer outlives the control.
        _run++;
        _holdTimer?.Stop();
        _failureTimer?.Stop();
        _holdSignature = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEnabledProperty)
        {
            PseudoClasses.Set(InactivePseudoClass, IsInactive);
        }
        else if (change.Property == BoundsProperty)
        {
            PseudoClasses.Set(WidePseudoClass, Bounds.Width > 220);
        }
    }

    #region 视图

    private readonly record struct View(string Key, string Label, Geometry? Icon, bool Spinner, ActionSwapTone Tone, bool Danger);

    private View CurrentView()
    {
        var active = CurrentState;
        var tone = active?.Tone ?? ActionSwapTone.Neutral;
        return _phase switch
        {
            ActionSwapPhase.Working when _target is { } target => new View("working", target.PendingLabel ?? string.Empty, null, true, tone, false),
            ActionSwapPhase.Failed => new View("failed", FailureLabel, ArcIcons.CircleAlert, false, tone, true),
            _ => new View($"state:{active?.Id}", active?.Label ?? string.Empty, active?.Icon, false, tone, false)
        };
    }

    private void Refresh()
    {
        PseudoClasses.Set(InactivePseudoClass, IsInactive);
        var active = CurrentState;
        if (active is null)
        {
            _surface?.Show(string.Empty, null, null, false, ActionSwapTint.Text, false);
            return;
        }
        var view = CurrentView();
        PseudoClasses.Set(NeutralPseudoClass, !view.Danger && view.Tone == ActionSwapTone.Neutral);
        PseudoClasses.Set(AccentPseudoClass, !view.Danger && view.Tone == ActionSwapTone.Accent);
        PseudoClasses.Set(SuccessPseudoClass, !view.Danger && view.Tone == ActionSwapTone.Success);
        PseudoClasses.Set(WarningPseudoClass, !view.Danger && view.Tone == ActionSwapTone.Warning);
        PseudoClasses.Set(QuietPseudoClass, !view.Danger && view.Tone == ActionSwapTone.Quiet);
        PseudoClasses.Set(DangerPseudoClass, view.Danger);
        var working = _phase == ActionSwapPhase.Working;
        var folded = working && string.IsNullOrEmpty(_target?.PendingLabel);
        PseudoClasses.Set(WorkingPseudoClass, working);
        PseudoClasses.Set(FoldedPseudoClass, folded);
        PseudoClasses.Set(FailedPseudoClass, _phase == ActionSwapPhase.Failed);
        AutomationProperties.SetName(this, view.Label.Length > 0 ? view.Label : active.Label);
        var tint = view.Danger ? ActionSwapTint.Danger : view.Tone switch
        {
            ActionSwapTone.Success => ActionSwapTint.Success,
            ActionSwapTone.Warning => ActionSwapTint.Warning,
            _ => ActionSwapTint.Text
        };
        var draw = !view.Danger && view.Tone == ActionSwapTone.Success && ArcMotion.IsFull(this);
        // Arc's state icons take lucide's default stroke (2); the failure icon is drawn at 1.75.
        _surface?.Show(view.Label, view.Key, view.Icon, view.Spinner, tint, draw, view.Danger ? 1.75 : 2);
        _surface?.SetFolded(folded);
        RefreshHold();
    }

    #endregion

    #region 按下与状态流转

    protected override void OnClick()
    {
        // Working swallows activation but keeps focus (aria-disabled instead of disabled).
        if (_phase == ActionSwapPhase.Working || IsInactive)
        {
            return;
        }
        base.OnClick();
        Advance();
    }

    private void Advance()
    {
        if (CurrentState is not { } current || NextState is not { } next)
        {
            return;
        }
        _failureTimer?.Stop();
        var run = ++_run;
        Task? task;
        try
        {
            task = AsyncAction?.Invoke(next, current);
        }
        catch
        {
            Fail();
            return;
        }
        if (task is null)
        {
            Commit(next);
            return;
        }
        _target = next;
        SetPhase(ActionSwapPhase.Working);
        Announce(next.PendingLabel ?? "Working");
        _ = Await(task, run, next, Stopwatch.GetTimestamp());
    }

    private async Task Await(Task task, int run, ActionSwapState next, long started)
    {
        var ok = true;
        try
        {
            await task;
        }
        catch
        {
            ok = false;
        }
        var remaining = MinWorking - Stopwatch.GetElapsedTime(started);
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining);
        }
        if (run != _run)
        {
            return;
        }
        if (ok)
        {
            Commit(next);
        }
        else
        {
            Fail();
        }
    }

    private void Commit(ActionSwapState next)
    {
        _target = null;
        Phase = ActionSwapPhase.Idle;
        SetCurrentValue(ValueProperty, next.Id);
        // A controlled value that did not follow still leaves the working face.
        Refresh();
        Announce(next.Label);
        RaiseEvent(new ActionSwapValueChangedEventArgs(ValueChangedEvent, next));
    }

    private void Fail()
    {
        _target = null;
        SetPhase(ActionSwapPhase.Failed);
        Announce(FailureLabel);
        _failureTimer ??= new PausableTimer(FailureHold, () =>
        {
            if (_phase == ActionSwapPhase.Failed)
            {
                SetPhase(ActionSwapPhase.Idle);
            }
        });
        _failureTimer.Restart(FailureHold);
    }

    private void SetPhase(ActionSwapPhase phase)
    {
        if (_phase == phase)
        {
            return;
        }
        Phase = phase;
        Refresh();
    }

    private void Announce(string message) => _status?.Announce(message);

    /// <summary>
    /// Moves on by itself after <see cref="ActionSwapState.HoldFor"/>. The timer is keyed by the state and the hold, so a
    /// refresh that changes neither never restarts it.
    /// </summary>
    private void RefreshHold()
    {
        var hold = _phase == ActionSwapPhase.Idle && TopLevel.GetTopLevel(this) is not null ? CurrentState : null;
        var holdFor = hold?.HoldFor ?? TimeSpan.Zero;
        var signature = holdFor > TimeSpan.Zero ? $"{hold!.Id}:{holdFor.TotalMilliseconds}" : null;
        if (signature == _holdSignature)
        {
            return;
        }
        _holdSignature = signature;
        _holdTimer?.Stop();
        if (signature is null)
        {
            return;
        }
        _holdTimer ??= new PausableTimer(holdFor, OnHoldElapsed);
        _holdTimer.Restart(holdFor);
    }

    private void OnHoldElapsed()
    {
        _holdSignature = null;
        if (_phase == ActionSwapPhase.Idle && NextState is { } next && !IsInactive)
        {
            Commit(next);
        }
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new ActionSwapAutomationPeer(this);

    private sealed class ActionSwapAutomationPeer : ButtonAutomationPeer
    {
        public ActionSwapAutomationPeer(ActionSwap owner) : base(owner)
        {
        }

        // aria-busy + aria-disabled while working; the button itself stays focusable.
        protected override bool IsEnabledCore() => base.IsEnabledCore() && ((ActionSwap)Owner).Phase != ActionSwapPhase.Working;
    }
}
