using System.Diagnostics;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Where a <see cref="ConfirmMorph"/> is in its life: resting, asking, working, finished, or failed.</summary>
public enum ConfirmMorphState
{
    Idle,
    Confirming,
    Pending,
    Done,
    Error
}

/// <summary>How the question is answered.</summary>
public enum ConfirmMorphMode
{
    /// <summary>A press on Confirm.</summary>
    Press,

    /// <summary>Confirm held until its fill completes (<see cref="ConfirmMorph.HoldDuration"/>).</summary>
    Hold,

    /// <summary>A press, then a short countdown that Cancel or Escape can still stop.</summary>
    Countdown
}

/// <summary>Color of the confirm answer.</summary>
public enum ConfirmMorphTone
{
    /// <summary>Danger color, for actions that destroy something (default).</summary>
    Danger,

    /// <summary>Accent color, for important actions that can be reversed.</summary>
    Neutral
}

/// <summary>Arguments of <see cref="ConfirmMorph.StateChanged"/>.</summary>
public sealed class ConfirmMorphStateChangedEventArgs : RoutedEventArgs
{
    public ConfirmMorphStateChangedEventArgs(RoutedEvent routedEvent, ConfirmMorphState oldState, ConfirmMorphState newState) : base(routedEvent)
    {
        OldState = oldState;
        NewState = newState;
    }

    public ConfirmMorphState OldState { get; }

    public ConfirmMorphState NewState { get; }
}

/// <summary>
/// A button for destructive or important actions that asks in place: pressing it morphs the same pill into an inline
/// question with Cancel and Confirm, then into a spinner, then into a result with Undo. Only the pill's width moves
/// (growing on the morph spring, shrinking on smooth, P5 / P9); each face sits at its natural width and its parts clear
/// a small blur in reading order (P20) while the old face fades (P2). Unanswered questions and results time out on a
/// pausable timer (P22, held while the pointer rests on the control), Escape and an outside press return it to rest,
/// and focus follows the morph. Answer with a press, a hold, or a press followed by a short countdown.
/// </summary>
[TemplatePart(PillPart, typeof(Control))]
[TemplatePart(StatusPart, typeof(Control))]
[PseudoClasses(CountingPseudoClass, HasUndoPseudoClass, IdlePseudoClass, ConfirmingPseudoClass, PendingPseudoClass, DonePseudoClass, ErrorPseudoClass)]
public class ConfirmMorph : TemplatedControl
{
    public const string PillPart = "PART_Pill";
    public const string StatusPart = "PART_Status";
    public const string CountingPseudoClass = ":counting";
    public const string HasUndoPseudoClass = ":has-undo";
    public const string IdlePseudoClass = ":idle";
    public const string ConfirmingPseudoClass = ":confirming";
    public const string PendingPseudoClass = ":pending";
    public const string DonePseudoClass = ":done";
    public const string ErrorPseudoClass = ":error";

    /// <summary>A press this soon after the question appears is the tail of the press that opened it, not an answer.</summary>
    internal static readonly TimeSpan ArmDelay = TimeSpan.FromMilliseconds(280);

    #region 公共属性定义

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ConfirmMorph, string>(nameof(Label), "Delete project");

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ConfirmMorph, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> PromptProperty =
        AvaloniaProperty.Register<ConfirmMorph, string?>(nameof(Prompt));

    public static readonly StyledProperty<string> ConfirmLabelProperty =
        AvaloniaProperty.Register<ConfirmMorph, string>(nameof(ConfirmLabel), "Delete");

    public static readonly StyledProperty<string> CancelLabelProperty =
        AvaloniaProperty.Register<ConfirmMorph, string>(nameof(CancelLabel), "Cancel");

    public static readonly StyledProperty<string> PendingLabelProperty =
        AvaloniaProperty.Register<ConfirmMorph, string>(nameof(PendingLabel), "Deleting");

    public static readonly StyledProperty<string> DoneLabelProperty =
        AvaloniaProperty.Register<ConfirmMorph, string>(nameof(DoneLabel), "Deleted");

    public static readonly StyledProperty<string> ErrorLabelProperty =
        AvaloniaProperty.Register<ConfirmMorph, string>(nameof(ErrorLabel), "Couldn’t finish");

    public static readonly StyledProperty<string> RetryLabelProperty =
        AvaloniaProperty.Register<ConfirmMorph, string>(nameof(RetryLabel), "Retry");

    public static readonly StyledProperty<string> UndoLabelProperty =
        AvaloniaProperty.Register<ConfirmMorph, string>(nameof(UndoLabel), "Undo");

    public static readonly StyledProperty<string> UndoingLabelProperty =
        AvaloniaProperty.Register<ConfirmMorph, string>(nameof(UndoingLabel), "Restoring");

    public static readonly StyledProperty<string?> CountdownLabelProperty =
        AvaloniaProperty.Register<ConfirmMorph, string?>(nameof(CountdownLabel));

    public static readonly StyledProperty<ConfirmMorphTone> ToneProperty =
        AvaloniaProperty.Register<ConfirmMorph, ConfirmMorphTone>(nameof(Tone));

    public static readonly StyledProperty<ConfirmMorphMode> ConfirmModeProperty =
        AvaloniaProperty.Register<ConfirmMorph, ConfirmMorphMode>(nameof(ConfirmMode));

    public static readonly StyledProperty<TimeSpan> HoldDurationProperty =
        AvaloniaProperty.Register<ConfirmMorph, TimeSpan>(nameof(HoldDuration), TimeSpan.FromMilliseconds(1000));

    public static readonly StyledProperty<int> CountdownSecondsProperty =
        AvaloniaProperty.Register<ConfirmMorph, int>(nameof(CountdownSeconds), 3);

    public static readonly StyledProperty<Func<Task>?> ConfirmActionProperty =
        AvaloniaProperty.Register<ConfirmMorph, Func<Task>?>(nameof(ConfirmAction));

    public static readonly StyledProperty<Func<Task>?> UndoActionProperty =
        AvaloniaProperty.Register<ConfirmMorph, Func<Task>?>(nameof(UndoAction));

    public static readonly StyledProperty<ConfirmMorphState> StateProperty =
        AvaloniaProperty.Register<ConfirmMorph, ConfirmMorphState>(nameof(State), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<TimeSpan> ConfirmTimeoutProperty =
        AvaloniaProperty.Register<ConfirmMorph, TimeSpan>(nameof(ConfirmTimeout), TimeSpan.FromMilliseconds(6000));

    public static readonly StyledProperty<TimeSpan> ResultTimeoutProperty =
        AvaloniaProperty.Register<ConfirmMorph, TimeSpan>(nameof(ResultTimeout), TimeSpan.FromMilliseconds(5000));

    public static readonly StyledProperty<bool> IsOutsidePressCancelEnabledProperty =
        AvaloniaProperty.Register<ConfirmMorph, bool>(nameof(IsOutsidePressCancelEnabled), true);

    internal static readonly DirectProperty<ConfirmMorph, string> ShownPromptProperty =
        AvaloniaProperty.RegisterDirect<ConfirmMorph, string>(nameof(ShownPrompt), o => o.ShownPrompt);

    internal static readonly DirectProperty<ConfirmMorph, string> WorkingLabelProperty =
        AvaloniaProperty.RegisterDirect<ConfirmMorph, string>(nameof(WorkingLabel), o => o.WorkingLabel);

    internal static readonly DirectProperty<ConfirmMorph, string> ShownCountdownLabelProperty =
        AvaloniaProperty.RegisterDirect<ConfirmMorph, string>(nameof(ShownCountdownLabel), o => o.ShownCountdownLabel);

    internal static readonly DirectProperty<ConfirmMorph, int> SecondsLeftProperty =
        AvaloniaProperty.RegisterDirect<ConfirmMorph, int>(nameof(SecondsLeft), o => o.SecondsLeft);

    internal static readonly DirectProperty<ConfirmMorph, double> CountdownProgressProperty =
        AvaloniaProperty.RegisterDirect<ConfirmMorph, double>(nameof(CountdownProgress), o => o.CountdownProgress);

    public static readonly RoutedEvent<ConfirmMorphStateChangedEventArgs> StateChangedEvent =
        RoutedEvent.Register<ConfirmMorph, ConfirmMorphStateChangedEventArgs>(nameof(StateChanged), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RoutedEventArgs> CancelledEvent =
        RoutedEvent.Register<ConfirmMorph, RoutedEventArgs>(nameof(Cancelled), RoutingStrategies.Bubble);

    /// <summary>The resting label, such as "Delete project".</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>A plain 16px glyph before the resting label.</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>The question, such as "Delete 3 files?"; null uses the label with a question mark.</summary>
    public string? Prompt { get => GetValue(PromptProperty); set => SetValue(PromptProperty, value); }

    public string ConfirmLabel { get => GetValue(ConfirmLabelProperty); set => SetValue(ConfirmLabelProperty, value); }
    public string CancelLabel { get => GetValue(CancelLabelProperty); set => SetValue(CancelLabelProperty, value); }

    /// <summary>Shown beside the spinner while <see cref="ConfirmAction"/> runs.</summary>
    public string PendingLabel { get => GetValue(PendingLabelProperty); set => SetValue(PendingLabelProperty, value); }

    public string DoneLabel { get => GetValue(DoneLabelProperty); set => SetValue(DoneLabelProperty, value); }
    public string ErrorLabel { get => GetValue(ErrorLabelProperty); set => SetValue(ErrorLabelProperty, value); }
    public string RetryLabel { get => GetValue(RetryLabelProperty); set => SetValue(RetryLabelProperty, value); }
    public string UndoLabel { get => GetValue(UndoLabelProperty); set => SetValue(UndoLabelProperty, value); }

    /// <summary>Shown beside the spinner while <see cref="UndoAction"/> runs.</summary>
    public string UndoingLabel { get => GetValue(UndoingLabelProperty); set => SetValue(UndoingLabelProperty, value); }

    /// <summary>Text before the seconds in countdown mode; null uses the pending label followed by "in".</summary>
    public string? CountdownLabel { get => GetValue(CountdownLabelProperty); set => SetValue(CountdownLabelProperty, value); }

    /// <summary>Danger (default) colors the confirm answer for destructive actions; Neutral uses the accent.</summary>
    public ConfirmMorphTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    public ConfirmMorphMode ConfirmMode { get => GetValue(ConfirmModeProperty); set => SetValue(ConfirmModeProperty, value); }

    /// <summary>How long Confirm must be held in <see cref="ConfirmMorphMode.Hold"/> (default 1s).</summary>
    public TimeSpan HoldDuration { get => GetValue(HoldDurationProperty); set => SetValue(HoldDurationProperty, value); }

    /// <summary>Seconds counted down in <see cref="ConfirmMorphMode.Countdown"/> (default 3).</summary>
    public int CountdownSeconds { get => GetValue(CountdownSecondsProperty); set => SetValue(CountdownSecondsProperty, value); }

    /// <summary>Runs on confirm. A task that has not completed shows the pending face; a fault shows the error face with Retry.</summary>
    public Func<Task>? ConfirmAction { get => GetValue(ConfirmActionProperty); set => SetValue(ConfirmActionProperty, value); }

    /// <summary>Offering it adds Undo to the result.</summary>
    public Func<Task>? UndoAction { get => GetValue(UndoActionProperty); set => SetValue(UndoActionProperty, value); }

    /// <summary>The face shown (two-way). Setting it from outside moves the control without running actions.</summary>
    public ConfirmMorphState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }

    /// <summary>Time before an unanswered question returns to rest; the pointer resting on the control holds it. Zero turns it off.</summary>
    public TimeSpan ConfirmTimeout { get => GetValue(ConfirmTimeoutProperty); set => SetValue(ConfirmTimeoutProperty, value); }

    /// <summary>Time a result stays before returning to rest; the pointer resting on the control holds it. Zero turns it off.</summary>
    public TimeSpan ResultTimeout { get => GetValue(ResultTimeoutProperty); set => SetValue(ResultTimeoutProperty, value); }

    /// <summary>A press outside the control cancels an open question (default true).</summary>
    public bool IsOutsidePressCancelEnabled { get => GetValue(IsOutsidePressCancelEnabledProperty); set => SetValue(IsOutsidePressCancelEnabledProperty, value); }

    public event EventHandler<ConfirmMorphStateChangedEventArgs>? StateChanged
    {
        add => AddHandler(StateChangedEvent, value);
        remove => RemoveHandler(StateChangedEvent, value);
    }

    /// <summary>Raised when a question or countdown is cancelled (Cancel, Escape, outside press, timeout).</summary>
    public event EventHandler<RoutedEventArgs>? Cancelled
    {
        add => AddHandler(CancelledEvent, value);
        remove => RemoveHandler(CancelledEvent, value);
    }

    private string _shownPrompt = "Delete project?";
    internal string ShownPrompt { get => _shownPrompt; private set => SetAndRaise(ShownPromptProperty, ref _shownPrompt, value); }

    private string _workingLabel = "Deleting";
    internal string WorkingLabel { get => _workingLabel; private set => SetAndRaise(WorkingLabelProperty, ref _workingLabel, value); }

    private string _shownCountdownLabel = "Deleting in";
    internal string ShownCountdownLabel { get => _shownCountdownLabel; private set => SetAndRaise(ShownCountdownLabelProperty, ref _shownCountdownLabel, value); }

    private int _secondsLeft = 3;
    internal int SecondsLeft { get => _secondsLeft; private set => SetAndRaise(SecondsLeftProperty, ref _secondsLeft, value); }

    private double _countdownProgress = 1;
    internal double CountdownProgress { get => _countdownProgress; private set => SetAndRaise(CountdownProgressProperty, ref _countdownProgress, value); }

    #endregion

    private enum Face
    {
        Idle,
        Confirming,
        Counting,
        Pending,
        Done,
        Error
    }

    private enum Work
    {
        Confirm,
        Undo
    }

    private static readonly string[] FaceParts = ["PART_IdleFace", "PART_ConfirmFace", "PART_CountFace", "PART_PendingFace", "PART_DoneFace", "PART_ErrorFace"];

    // Duration springs restated as stiffness and damping: growing follows new content with a little life, shrinking settles.
    private static readonly SpringSpec Grow = ArcMotionPresets.Morph;
    private static readonly SpringSpec Shrink = ArcMotionPresets.Smooth;
    private static readonly SpringSpec HoldRewind = SpringSpec.FromVisual(0.3, 0);

    private readonly Panel?[] _faces = new Panel?[6];
    private readonly MotionValue<double>?[] _faceMotion = new MotionValue<double>?[6];
    private Control? _pill;
    private ButtonsLiveRegion? _status;
    private AvaloniaButton? _trigger, _cancel, _confirm, _countCancel, _undo, _retry;
    private ConfirmMorphCheck? _check;
    private Control? _holdFill;
    private readonly RectangleGeometry _holdClip = new();
    private Face? _face;
    private bool _counting;
    private Work _working;
    private int _run;
    private long _askedAt;
    private bool _pendingFocus;
    private bool _keyboard;
    private bool _hovered;
    private bool _hidden;
    private bool _syncing;
    private MotionValue<double>? _width;
    private int _flight;
    private PausableTimer? _timeout;
    private MotionTimeline? _countdown;
    private MotionTimeline? _hold;
    private bool _holdingByKey;
    private TopLevel? _outsideWatch;
    private Window? _window;

    public ConfirmMorph()
    {
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnPreviewKeyUp, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, (_, _) => _keyboard = false, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    static ConfirmMorph()
    {
        FocusableProperty.OverrideDefaultValue<ConfirmMorph>(true);
        IsTabStopProperty.OverrideDefaultValue<ConfirmMorph>(false);
    }

    #region 模板

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _pill = e.NameScope.Find<Control>(PillPart);
        _status = e.NameScope.Find<ButtonsLiveRegion>(StatusPart);
        for (var i = 0; i < FaceParts.Length; i++)
        {
            _faces[i] = e.NameScope.Find<Panel>(FaceParts[i]);
            if (_faces[i] is { } face)
            {
                face.IsVisible = false;
            }
        }
        _trigger = e.NameScope.Find<AvaloniaButton>("PART_Trigger");
        _cancel = e.NameScope.Find<AvaloniaButton>("PART_Cancel");
        _confirm = e.NameScope.Find<AvaloniaButton>("PART_Confirm");
        _countCancel = e.NameScope.Find<AvaloniaButton>("PART_CountCancel");
        _undo = e.NameScope.Find<AvaloniaButton>("PART_Undo");
        _retry = e.NameScope.Find<AvaloniaButton>("PART_Retry");
        _check = e.NameScope.Find<ConfirmMorphCheck>("PART_Check");
        Hook();
        _face = null;
        ShowFace(FaceOf(State), animate: false);
        UpdateTexts();
    }

    private void Hook()
    {
        Listen(_trigger, OnTriggerClick, true);
        Listen(_cancel, OnCancelClick, true);
        Listen(_countCancel, OnCancelClick, true);
        Listen(_undo, OnUndoClick, true);
        Listen(_retry, OnRetryClick, true);
        if (_confirm is not null)
        {
            _confirm.Click += OnConfirmClick;
            _confirm.AddHandler(PointerPressedEvent, OnConfirmPointerPressed, RoutingStrategies.Tunnel);
            _confirm.AddHandler(PointerReleasedEvent, OnConfirmPointerReleased, RoutingStrategies.Tunnel);
            _confirm.AddHandler(PointerCaptureLostEvent, OnConfirmCaptureLost, RoutingStrategies.Direct | RoutingStrategies.Bubble);
            _confirm.LostFocus += OnConfirmLostFocus;
            _confirm.TemplateApplied += OnConfirmTemplateApplied;
            UpdateConfirmHelp();
        }
    }

    private void Unhook()
    {
        Listen(_trigger, OnTriggerClick, false);
        Listen(_cancel, OnCancelClick, false);
        Listen(_countCancel, OnCancelClick, false);
        Listen(_undo, OnUndoClick, false);
        Listen(_retry, OnRetryClick, false);
        if (_confirm is not null)
        {
            _confirm.Click -= OnConfirmClick;
            _confirm.RemoveHandler(PointerPressedEvent, OnConfirmPointerPressed);
            _confirm.RemoveHandler(PointerReleasedEvent, OnConfirmPointerReleased);
            _confirm.RemoveHandler(PointerCaptureLostEvent, OnConfirmCaptureLost);
            _confirm.LostFocus -= OnConfirmLostFocus;
            _confirm.TemplateApplied -= OnConfirmTemplateApplied;
        }
    }

    private static void Listen(AvaloniaButton? button, EventHandler<RoutedEventArgs> handler, bool on)
    {
        if (button is null)
        {
            return;
        }
        if (on)
        {
            button.Click += handler;
        }
        else
        {
            button.Click -= handler;
        }
    }

    private void OnConfirmTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        // The hold fill is a solid copy of the confirm button, revealed from the leading edge.
        _holdFill = e.NameScope.Find<Control>("PART_HoldFill");
        if (_holdFill is not null)
        {
            _holdFill.Clip = _holdClip;
            UpdateHoldFill();
        }
    }

    #endregion

    #region 状态

    private Face FaceOf(ConfirmMorphState state) => state switch
    {
        ConfirmMorphState.Confirming => _counting ? Face.Counting : Face.Confirming,
        ConfirmMorphState.Pending => Face.Pending,
        ConfirmMorphState.Done => Face.Done,
        ConfirmMorphState.Error => Face.Error,
        _ => Face.Idle
    };

    private void Go(ConfirmMorphState next)
    {
        if (next != ConfirmMorphState.Confirming)
        {
            _counting = false;
        }
        if (next == State)
        {
            ShowFace(FaceOf(next), IsLoaded);
            SyncTimeout();
            return;
        }
        KeepFocus();
        if (next == ConfirmMorphState.Confirming)
        {
            _askedAt = Stopwatch.GetTimestamp();
        }
        _syncing = true;
        try
        {
            SetCurrentValue(StateProperty, next);
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>Opens the question, as a press on the resting button would.</summary>
    public void Ask()
    {
        if (State != ConfirmMorphState.Idle || !IsEffectivelyEnabled)
        {
            return;
        }
        _status?.Announce(ShownPrompt);
        Go(ConfirmMorphState.Confirming);
    }

    /// <summary>Cancels an open question or countdown.</summary>
    public void Cancel()
    {
        _countdown?.Stop();
        RaiseEvent(new RoutedEventArgs(CancelledEvent));
        ToIdle();
        _status?.Announce("Cancelled");
    }

    private void ToIdle()
    {
        _run++;
        Go(ConfirmMorphState.Idle);
    }

    private void KeepFocus()
    {
        // Focus follows the control between faces, but only when it was already inside: a timeout never steals focus.
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        _pendingFocus = focused is null || (focused is Visual visual && (ReferenceEquals(visual, this) || this.IsVisualAncestorOf(visual)));
    }

    private void Answer()
    {
        if (Stopwatch.GetElapsedTime(_askedAt) < ArmDelay)
        {
            return;
        }
        if (ConfirmMode == ConfirmMorphMode.Countdown)
        {
            StartCountdown();
            return;
        }
        _ = PerformAsync(Work.Confirm);
    }

    private void StartCountdown()
    {
        KeepFocus();
        _counting = true;
        var seconds = Math.Max(1, CountdownSeconds);
        SecondsLeft = seconds;
        CountdownProgress = 1;
        _status?.Announce($"{ShownCountdownLabel} {seconds} seconds");
        _countdown ??= CreateCountdown();
        ShowFace(Face.Counting, IsLoaded);
        SyncTimeout();
        _countdown.Start(TimeSpan.FromSeconds(seconds));
    }

    private MotionTimeline CreateCountdown()
    {
        // P22: the Core timeline keeps time with a pausable timer and reports every frame, at every motion level.
        var timeline = new MotionTimeline(this);
        timeline.Completed += () => _ = PerformAsync(Work.Confirm);
        timeline.Changed += p =>
        {
            var remaining = 1 - p;
            CountdownProgress = remaining;
            SecondsLeft = Math.Max(1, (int)Math.Ceiling(remaining * Math.Max(1, CountdownSeconds) - 1e-6));
        };
        return timeline;
    }

    private async Task PerformAsync(Work kind)
    {
        var token = ++_run;
        _working = kind;
        _counting = false;
        _countdown?.Stop();
        var handler = kind == Work.Confirm ? ConfirmAction : UndoAction;
        Task? task;
        try
        {
            task = handler?.Invoke();
        }
        catch (Exception)
        {
            Fail();
            return;
        }
        if (task is not null && !task.IsCompleted)
        {
            WorkingLabel = kind == Work.Confirm ? PendingLabel : UndoingLabel;
            Go(ConfirmMorphState.Pending);
            _status?.Announce(WorkingLabel);
            try
            {
                await task;
            }
            catch (Exception)
            {
                if (token == _run)
                {
                    Fail();
                }
                return;
            }
            if (token != _run)
            {
                return;
            }
        }
        else if (task is { IsFaulted: true } or { IsCanceled: true })
        {
            Fail();
            return;
        }
        if (kind == Work.Undo)
        {
            Go(ConfirmMorphState.Idle);
            _status?.Announce("Undone");
            return;
        }
        Go(ConfirmMorphState.Done);
        _status?.Announce(UndoAction is not null ? $"{DoneLabel}. {UndoLabel} is available." : DoneLabel);
    }

    private void Fail()
    {
        Go(ConfirmMorphState.Error);
        _status?.Announce(ErrorLabel);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty)
        {
            var oldState = change.GetOldValue<ConfirmMorphState>();
            var newState = change.GetNewValue<ConfirmMorphState>();
            if (!_syncing)
            {
                // A parent moving the state on its own: the countdown never survives that.
                _counting = false;
                _run++;
            }
            if (newState != ConfirmMorphState.Confirming)
            {
                _countdown?.Stop();
                _hold?.Jump(0);
            }
            if (newState == ConfirmMorphState.Confirming && !_syncing)
            {
                _askedAt = Stopwatch.GetTimestamp();
            }
            UpdateStatePseudoClasses();
            ShowFace(FaceOf(newState), IsLoaded);
            SyncTimeout();
            WatchOutside();
            RaiseEvent(new ConfirmMorphStateChangedEventArgs(StateChangedEvent, oldState, newState));
        }
        else if (change.Property == LabelProperty || change.Property == PromptProperty || change.Property == PendingLabelProperty ||
                 change.Property == UndoingLabelProperty || change.Property == CountdownLabelProperty)
        {
            UpdateTexts();
        }
        else if (change.Property == UndoActionProperty)
        {
            PseudoClasses.Set(HasUndoPseudoClass, UndoAction is not null);
        }
        else if (change.Property == ConfirmModeProperty || change.Property == ConfirmLabelProperty)
        {
            UpdateConfirmHelp();
        }
        else if (change.Property == ConfirmTimeoutProperty || change.Property == ResultTimeoutProperty)
        {
            SyncTimeout();
        }
        else if (change.Property == IsOutsidePressCancelEnabledProperty)
        {
            WatchOutside();
        }
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        UpdateTexts();
        UpdateStatePseudoClasses();
        PseudoClasses.Set(HasUndoPseudoClass, UndoAction is not null);
    }

    private void UpdateStatePseudoClasses()
    {
        PseudoClasses.Set(IdlePseudoClass, State == ConfirmMorphState.Idle);
        PseudoClasses.Set(ConfirmingPseudoClass, State == ConfirmMorphState.Confirming);
        PseudoClasses.Set(PendingPseudoClass, State == ConfirmMorphState.Pending);
        PseudoClasses.Set(DonePseudoClass, State == ConfirmMorphState.Done);
        PseudoClasses.Set(ErrorPseudoClass, State == ConfirmMorphState.Error);
    }

    private void UpdateTexts()
    {
        ShownPrompt = Prompt ?? $"{Label}?";
        ShownCountdownLabel = CountdownLabel ?? $"{PendingLabel} in";
        if (State != ConfirmMorphState.Pending)
        {
            WorkingLabel = _working == Work.Confirm ? PendingLabel : UndoingLabel;
        }
        AutomationProperties.SetName(this, Label);
        if (_faces[(int)Face.Confirming] is { } ask)
        {
            AutomationProperties.SetName(ask, ShownPrompt);
        }
    }

    private void UpdateConfirmHelp()
    {
        if (_confirm is not null)
        {
            AutomationProperties.SetHelpText(_confirm, ConfirmMode == ConfirmMorphMode.Hold ? "Press and hold" : string.Empty);
        }
    }

    #endregion

    #region 形变

    /// <summary>
    /// Switches faces: the pill's width springs from the old face to the new one while the old face fades in place and the
    /// new face's parts arrive in reading order, each clearing a small blur.
    /// </summary>
    private void ShowFace(Face face, bool animate)
    {
        PseudoClasses.Set(CountingPseudoClass, face == Face.Counting);
        if (_face == face)
        {
            return;
        }
        var previous = _face;
        _face = face;
        var incoming = _faces[(int)face];
        var outgoing = previous is { } p ? _faces[(int)p] : null;
        if (incoming is null)
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        animate &= previous is not null && level != ArcMotionLevel.Off && IsEffectivelyVisible;
        incoming.IsVisible = true;
        incoming.IsHitTestVisible = true;
        if (face == Face.Done)
        {
            _check?.Play(animate && level == ArcMotionLevel.Full);
        }
        if (!animate)
        {
            if (outgoing is not null)
            {
                Hide(outgoing, previous!.Value);
            }
            ResetParts(incoming);
            _flight++;
            if (_pill is not null)
            {
                _pill.Width = double.NaN;
            }
            AfterFace();
            return;
        }
        var full = level == ArcMotionLevel.Full;
        MorphWidth(incoming, full);
        if (outgoing is not null)
        {
            ExitFace(outgoing, previous!.Value, full);
        }
        EnterFace(incoming, face, full);
        AfterFace();
    }

    private void MorphWidth(Panel incoming, bool full)
    {
        if (_pill is null)
        {
            return;
        }
        var from = _width?.IsAnimating == true ? _width.Value : _pill.Bounds.Width;
        incoming.Measure(Size.Infinity);
        var target = incoming.DesiredSize.Width;
        var token = ++_flight;
        if (!full || from <= 0 || Math.Abs(target - from) < 0.25)
        {
            _pill.Width = double.NaN;
            return;
        }
        if (_width is null)
        {
            _width = new MotionValue<double>(_pill, from, MotionVectors.Double);
            _width.Changed += OnWidthChanged;
        }
        if (!_width.IsAnimating)
        {
            _width.Jump(from);
        }
        _widthToken = token;
        _pill.Width = from;
        _ = _width.AnimateTo(target, MotionSpec.Spring(target > from ? Grow : Shrink));
        if (!_width.IsAnimating)
        {
            _pill.Width = double.NaN;
        }
    }

    private int _widthToken;

    private void OnWidthChanged(double value)
    {
        if (_pill is null || _width is null || !_width.IsAnimating)
        {
            return;
        }
        _pill.Width = Math.Max(0, value);
        // The final step lands exactly on the target at rest: hand back to auto so the pill keeps following its face.
        if (value == _width.Target && _width.Velocity == 0 && _widthToken == _flight)
        {
            _pill.Width = double.NaN;
        }
    }

    private void ExitFace(Panel outgoing, Face which, bool full)
    {
        outgoing.IsHitTestVisible = false;
        var motion = _faceMotion[(int)which] ??= CreateFaceMotion(outgoing, which);
        // A leaving face stays where it was and fades (with a subtle blur) while the pill springs to the next one.
        var blur = full ? new BlurEffect { Radius = 0 } : null;
        outgoing.Effect = blur;
        var start = outgoing.Opacity;
        motion.Stop();
        _exiting[(int)which] = (start, blur);
        motion.Jump(0);
        _ = motion.AnimateTo(1, MotionSpec.Tween(full ? ArcMotionPresets.Instant : TimeSpan.FromMilliseconds(100), ArcEasings.Standard));
    }

    private readonly (double Start, BlurEffect? Blur)?[] _exiting = new (double, BlurEffect?)?[6];
    private readonly bool[] _entering = new bool[6];

    private MotionValue<double> CreateFaceMotion(Panel panel, Face which)
    {
        var motion = new MotionValue<double>(this, 0, MotionVectors.Double);
        motion.Changed += v => StepFace(panel, which, v);
        return motion;
    }

    private void StepFace(Panel panel, Face which, double v)
    {
        var index = (int)which;
        if (_exiting[index] is { } exit)
        {
            panel.Opacity = exit.Start * (1 - v);
            if (exit.Blur is not null)
            {
                exit.Blur.Radius = 2 * v;
            }
            if (v >= 1)
            {
                // Faded out: leave the stage (after this frame's callbacks).
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (_face != which && _exiting[index] is not null)
                    {
                        Hide(panel, which);
                    }
                });
            }
            return;
        }
        if (!_entering[index])
        {
            return;
        }
        if (!ArcMotion.IsFull(this))
        {
            panel.Opacity = v;
            return;
        }
        // v is seconds since the face appeared: parts start 50ms in, 35ms apart, each over 260ms (enter).
        var parts = panel.Children;
        for (var i = 0; i < parts.Count; i++)
        {
            var local = Math.Clamp((v - 0.05 - i * 0.035) / 0.26, 0, 1);
            var eased = ArcEasings.Enter.Ease(local);
            var part = parts[i];
            part.Opacity = eased;
            var radius = 4 * (1 - eased);
            if (radius < 0.05)
            {
                part.Effect = null;
            }
            else if (part.Effect is BlurEffect blur)
            {
                blur.Radius = radius;
            }
            else
            {
                part.Effect = new BlurEffect { Radius = radius };
            }
        }
    }

    private void EnterFace(Panel incoming, Face which, bool full)
    {
        var index = (int)which;
        _exiting[index] = null;
        _entering[index] = true;
        incoming.Effect = null;
        var motion = _faceMotion[index] ??= CreateFaceMotion(incoming, which);
        motion.Stop();
        if (full)
        {
            incoming.Opacity = 1;
            var count = incoming.Children.Count;
            var total = 0.05 + Math.Max(0, count - 1) * 0.035 + 0.26;
            motion.Jump(0);
            _ = motion.AnimateTo(total, MotionSpec.Tween(TimeSpan.FromSeconds(total), new Avalonia.Animation.Easings.LinearEasing()));
        }
        else
        {
            ResetParts(incoming);
            incoming.Opacity = 0;
            motion.Jump(0);
            _ = motion.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(140), ArcEasings.Standard));
        }
    }

    private void Hide(Panel panel, Face which)
    {
        var index = (int)which;
        _exiting[index] = null;
        _entering[index] = false;
        _faceMotion[index]?.Stop();
        panel.IsVisible = false;
        panel.Opacity = 1;
        panel.Effect = null;
    }

    private void ResetParts(Panel panel)
    {
        panel.Opacity = 1;
        panel.Effect = null;
        foreach (var part in panel.Children)
        {
            part.Opacity = 1;
            part.Effect = null;
        }
    }

    /// <summary>Focus moves with the morph: Confirm while asking, the root while counting or working, Undo or Retry on a result.</summary>
    private void AfterFace()
    {
        if (!_pendingFocus || _face is not { } face)
        {
            return;
        }
        _pendingFocus = false;
        Control? target = face switch
        {
            Face.Idle => _trigger,
            Face.Confirming => _confirm,
            Face.Done => UndoAction is not null ? _undo : null,
            Face.Error => _retry,
            _ => null
        };
        var method = _keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (target is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true })
            {
                target.Focus(method);
            }
            else
            {
                Focus(method);
            }
        });
    }

    #endregion

    #region 计时

    private void SyncTimeout()
    {
        var timeout = State == ConfirmMorphState.Confirming && !_counting ? ConfirmTimeout
            : State is ConfirmMorphState.Done or ConfirmMorphState.Error ? ResultTimeout
            : TimeSpan.Zero;
        if (timeout <= TimeSpan.Zero || !IsLoaded)
        {
            _timeout?.Stop();
            return;
        }
        _timeout ??= new PausableTimer(timeout, Expire);
        _timeout.Restart(timeout);
        SyncHold();
    }

    /// <summary>A pointer resting on the control holds the timeout, and so does a minimized window.</summary>
    private void SyncHold()
    {
        if (_timeout is null)
        {
            return;
        }
        if (_hovered || _hidden)
        {
            _timeout.Pause();
        }
        else
        {
            _timeout.Resume();
        }
    }

    private void Expire()
    {
        if (State == ConfirmMorphState.Confirming)
        {
            Cancel();
        }
        else if (State is ConfirmMorphState.Done or ConfirmMorphState.Error)
        {
            ToIdle();
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _hovered = true;
        SyncHold();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovered = false;
        SyncHold();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window is not null)
        {
            _window.PropertyChanged += OnWindowPropertyChanged;
        }
        WatchOutside();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        SyncTimeout();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_window is not null)
        {
            _window.PropertyChanged -= OnWindowPropertyChanged;
            _window = null;
        }
        _timeout?.Stop();
        _countdown?.Stop();
        _outsideWatch?.RemoveHandler(PointerPressedEvent, OnOutsidePointerPressed);
        _outsideWatch = null;
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty && sender is Window window)
        {
            _hidden = window.WindowState == WindowState.Minimized;
            SyncHold();
            if (_countdown is not null)
            {
                if (_hidden)
                {
                    _countdown.Pause();
                }
                else
                {
                    _countdown.Resume();
                }
            }
        }
    }

    #endregion

    #region 输入

    private void WatchOutside()
    {
        var watch = State == ConfirmMorphState.Confirming && IsOutsidePressCancelEnabled ? TopLevel.GetTopLevel(this) : null;
        if (ReferenceEquals(watch, _outsideWatch))
        {
            return;
        }
        _outsideWatch?.RemoveHandler(PointerPressedEvent, OnOutsidePointerPressed);
        _outsideWatch = watch;
        _outsideWatch?.AddHandler(PointerPressedEvent, OnOutsidePointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>An outside press answers the question with no.</summary>
    private void OnOutsidePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (State != ConfirmMorphState.Confirming)
        {
            return;
        }
        if (e.Source is Visual source && (ReferenceEquals(source, this) || this.IsVisualAncestorOf(source)))
        {
            return;
        }
        Cancel();
    }

    private void OnTriggerClick(object? sender, RoutedEventArgs e) => Ask();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Cancel();

    private void OnUndoClick(object? sender, RoutedEventArgs e) => _ = PerformAsync(Work.Undo);

    private void OnRetryClick(object? sender, RoutedEventArgs e) => _ = PerformAsync(_working);

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        if (ConfirmMode != ConfirmMorphMode.Hold && State == ConfirmMorphState.Confirming && !_counting)
        {
            Answer();
        }
    }

    private bool _enterDown;

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        _keyboard = true;
        var repeat = e.Key == Key.Enter && _enterDown;
        if (e.Key == Key.Enter)
        {
            _enterDown = true;
        }
        if (e.Key == Key.Escape)
        {
            if (State == ConfirmMorphState.Confirming)
            {
                e.Handled = true;
                Cancel();
            }
            else if (State is ConfirmMorphState.Done or ConfirmMorphState.Error)
            {
                e.Handled = true;
                ToIdle();
            }
            return;
        }
        if (State != ConfirmMorphState.Confirming)
        {
            return;
        }
        if (ConfirmMode == ConfirmMorphMode.Hold && ReferenceEquals(e.Source, _confirm) && e.Key is Key.Space or Key.Enter)
        {
            // Hold mode: keys fill while held instead of clicking.
            e.Handled = true;
            if (!_holdingByKey && !repeat)
            {
                _holdingByKey = true;
                BeginHold();
            }
            return;
        }
        if (e.Key != Key.Enter)
        {
            return;
        }
        // Auto-repeat of the Enter that opened the question is not an answer.
        if (repeat)
        {
            e.Handled = true;
            return;
        }
        // Enter answers yes from anywhere in the question except a button of its own (Cancel keeps meaning Cancel).
        if (e.Source is AvaloniaButton && !ReferenceEquals(e.Source, _confirm))
        {
            return;
        }
        e.Handled = true;
        if (_counting)
        {
            _ = PerformAsync(Work.Confirm);
        }
        else if (ConfirmMode != ConfirmMorphMode.Hold)
        {
            Answer();
        }
    }

    private void OnPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _enterDown = false;
        }
        if (_holdingByKey && e.Key is Key.Space or Key.Enter)
        {
            e.Handled = true;
            _holdingByKey = false;
            ReleaseHold();
        }
    }

    private MotionTimeline Hold
    {
        get
        {
            if (_hold is null)
            {
                // P22: holding runs the timeline from wherever the fill is; letting go early rewinds on a spring.
                _hold = new MotionTimeline(this);
                _hold.Completed += () =>
                {
                    _holdingByKey = false;
                    _ = PerformAsync(Work.Confirm);
                };
                _hold.Changed += _ => UpdateHoldFill();
            }
            return _hold;
        }
    }

    private void BeginHold()
    {
        if (State != ConfirmMorphState.Confirming || _counting)
        {
            return;
        }
        Hold.Start(HoldDuration, Hold.Value);
    }

    private void ReleaseHold()
    {
        if (_hold is not { IsActive: true })
        {
            return;
        }
        if (_hold.Value < 1)
        {
            _hold.Rewind(HoldRewind);
            _status?.Announce($"Keep holding to {ConfirmLabel.ToLowerInvariant()}");
        }
    }

    private void UpdateHoldFill()
    {
        if (_holdFill is null)
        {
            return;
        }
        var value = Math.Clamp(_hold?.Value ?? 0, 0, 1);
        _holdFill.IsVisible = value > 0.0005;
        _holdClip.Rect = new Rect(0, 0, _holdFill.Bounds.Width * value, Math.Max(_holdFill.Bounds.Height, 1));
        // A clip geometry changed in place reaches the compositor only when its visual is invalidated.
        _holdFill.InvalidateVisual();
    }

    private void OnConfirmPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ConfirmMode != ConfirmMorphMode.Hold || _confirm is null)
        {
            return;
        }
        if (!e.GetCurrentPoint(_confirm).Properties.IsLeftButtonPressed)
        {
            return;
        }
        e.Handled = true;
        _confirm.Focus(NavigationMethod.Pointer);
        e.Pointer.Capture(_confirm);
        BeginHold();
    }

    private void OnConfirmPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (ConfirmMode != ConfirmMorphMode.Hold)
        {
            return;
        }
        e.Handled = true;
        e.Pointer.Capture(null);
        ReleaseHold();
    }

    private void OnConfirmCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (ConfirmMode == ConfirmMorphMode.Hold && !_holdingByKey)
        {
            ReleaseHold();
        }
    }

    private void OnConfirmLostFocus(object? sender, RoutedEventArgs e)
    {
        _holdingByKey = false;
        ReleaseHold();
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new ConfirmMorphAutomationPeer(this);

    private sealed class ConfirmMorphAutomationPeer : ControlAutomationPeer
    {
        public ConfirmMorphAutomationPeer(ConfirmMorph owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        // aria-busy while the action runs.
        protected override string? GetItemStatusCore() => ((ConfirmMorph)Owner).State == ConfirmMorphState.Pending ? "Busy" : base.GetItemStatusCore();
    }
}
