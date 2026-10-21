using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Where an <see cref="ActionButton"/> is in its run.</summary>
public enum ActionButtonState
{
    Idle,
    Pending,
    Success
}

/// <summary>Arguments of <see cref="ActionButton.ActionFailed"/>.</summary>
public sealed class ActionButtonFailedEventArgs : RoutedEventArgs
{
    public ActionButtonFailedEventArgs(RoutedEvent routedEvent, Exception exception) : base(routedEvent) => Exception = exception;

    /// <summary>What the action threw; the button is back at idle.</summary>
    public Exception Exception { get; }
}

/// <summary>
/// A call to action for one asynchronous submit (Save, Publish): pending and success show on the button itself. The
/// changed letters of the label rise out of a soft blur while kept letters glide (P4) and the width follows on a spring
/// (P5); the trailing arrow leaves in the direction of the action, a spinner turns while <see cref="AsyncAction"/>
/// runs, a check draws itself on success (P15), and the button returns to idle after <see cref="ResetAfter"/>.
/// Pending keeps keyboard focus: activation is swallowed instead of disabling the button.
/// </summary>
[TemplatePart(IconPart, typeof(Control))]
[TemplatePart(StatusPart, typeof(Control))]
[PseudoClasses(PendingPseudoClass, SuccessPseudoClass, WidePseudoClass)]
public class ActionButton : AvaloniaButton
{
    public const string IconPart = "PART_Icon";
    public const string StatusPart = "PART_Status";
    public const string PendingPseudoClass = ":pending";
    public const string SuccessPseudoClass = ":success";
    public const string WidePseudoClass = ":wide";

    #region 公共属性定义

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ActionButton, string>(nameof(Label), "Save");

    public static readonly StyledProperty<string> PendingLabelProperty =
        AvaloniaProperty.Register<ActionButton, string>(nameof(PendingLabel), "Saving");

    public static readonly StyledProperty<string> SuccessLabelProperty =
        AvaloniaProperty.Register<ActionButton, string>(nameof(SuccessLabel), "Saved");

    public static readonly StyledProperty<TimeSpan> ResetAfterProperty =
        AvaloniaProperty.Register<ActionButton, TimeSpan>(nameof(ResetAfter), TimeSpan.FromMilliseconds(2400));

    public static readonly StyledProperty<Func<Task>?> AsyncActionProperty =
        AvaloniaProperty.Register<ActionButton, Func<Task>?>(nameof(AsyncAction));

    /// <summary>Current phase: Idle, Pending while AsyncAction runs, or Success after it completes. Read-only.</summary>
    public static readonly DirectProperty<ActionButton, ActionButtonState> StateProperty =
        AvaloniaProperty.RegisterDirect<ActionButton, ActionButtonState>(nameof(State), o => o.State);

    internal static readonly DirectProperty<ActionButton, string> DisplayLabelProperty =
        AvaloniaProperty.RegisterDirect<ActionButton, string>(nameof(DisplayLabel), o => o.DisplayLabel);

    public static readonly RoutedEvent<ActionButtonFailedEventArgs> ActionFailedEvent =
        RoutedEvent.Register<ActionButton, ActionButtonFailedEventArgs>(nameof(ActionFailed), RoutingStrategies.Bubble);

    /// <summary>The idle label and, always, the accessible name (default "Save").</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Shown while <see cref="AsyncAction"/> runs (default "Saving").</summary>
    public string PendingLabel { get => GetValue(PendingLabelProperty); set => SetValue(PendingLabelProperty, value); }

    /// <summary>Shown after the action succeeds (default "Saved").</summary>
    public string SuccessLabel { get => GetValue(SuccessLabelProperty); set => SetValue(SuccessLabelProperty, value); }

    /// <summary>How long success stays before returning to idle (default 2.4s); zero keeps it.</summary>
    public TimeSpan ResetAfter { get => GetValue(ResetAfterProperty); set => SetValue(ResetAfterProperty, value); }

    /// <summary>
    /// The work to run. The button is pending until the task completes; a fault returns it to idle and raises
    /// <see cref="ActionFailed"/>. <see cref="AvaloniaButton.Command"/> and <see cref="AvaloniaButton.Click"/> run first and,
    /// without an async action, count as an immediate success.
    /// </summary>
    public Func<Task>? AsyncAction { get => GetValue(AsyncActionProperty); set => SetValue(AsyncActionProperty, value); }

    /// <summary>Raised when <see cref="AsyncAction"/> throws.</summary>
    public event EventHandler<ActionButtonFailedEventArgs>? ActionFailed
    {
        add => AddHandler(ActionFailedEvent, value);
        remove => RemoveHandler(ActionFailedEvent, value);
    }

    private ActionButtonState _state;

    /// <inheritdoc cref="StateProperty"/>
    public ActionButtonState State
    {
        get => _state;
        private set => SetAndRaise(StateProperty, ref _state, value);
    }

    private string _displayLabel = "Save";

    internal string DisplayLabel
    {
        get => _displayLabel;
        private set => SetAndRaise(DisplayLabelProperty, ref _displayLabel, value);
    }

    #endregion

    private ButtonsGlyphSwap? _icon;
    private ButtonsLiveRegion? _status;
    private PausableTimer? _resetTimer;
    private int _run;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _icon = e.NameScope.Find<ButtonsGlyphSwap>(IconPart);
        _status = e.NameScope.Find<ButtonsLiveRegion>(StatusPart);
        _icon?.Show(CreateIcon(State), default, default);
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        DisplayLabel = Label;
        AutomationProperties.SetName(this, Label);
    }

    protected override async void OnClick()
    {
        // Pending swallows activation but keeps focus (aria-disabled instead of disabled).
        if (State == ActionButtonState.Pending)
        {
            return;
        }
        _resetTimer?.Stop();
        base.OnClick();
        var action = AsyncAction;
        var run = ++_run;
        if (action is null)
        {
            Succeed(run);
            return;
        }
        SetState(ActionButtonState.Pending);
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            if (run == _run)
            {
                SetState(ActionButtonState.Idle);
                RaiseEvent(new ActionButtonFailedEventArgs(ActionFailedEvent, exception));
            }
            return;
        }
        Succeed(run);
    }

    private void Succeed(int run)
    {
        if (run != _run)
        {
            return;
        }
        SetState(ActionButtonState.Success);
        if (ResetAfter > TimeSpan.Zero)
        {
            _resetTimer ??= new PausableTimer(ResetAfter, () => SetState(ActionButtonState.Idle));
            _resetTimer.Restart(ResetAfter);
        }
    }

    /// <summary>Returns to idle at once (cancels a pending success reset).</summary>
    public void Reset()
    {
        _run++;
        _resetTimer?.Stop();
        SetState(ActionButtonState.Idle);
    }

    private void SetState(ActionButtonState state)
    {
        if (State == state)
        {
            return;
        }
        var previous = State;
        State = state;
        PseudoClasses.Set(PendingPseudoClass, state == ActionButtonState.Pending);
        PseudoClasses.Set(SuccessPseudoClass, state == ActionButtonState.Success);
        DisplayLabel = LabelFor(state);
        _icon?.Show(CreateIcon(state),
            state == ActionButtonState.Idle ? ButtonsGlyphPose.ArrowIn : ButtonsGlyphPose.Pop,
            previous == ActionButtonState.Idle ? ButtonsGlyphPose.ArrowOut : ButtonsGlyphPose.Pop);
        _status?.Announce(state switch
        {
            ActionButtonState.Pending => PendingLabel,
            ActionButtonState.Success => SuccessLabel,
            _ => string.Empty
        });
    }

    private string LabelFor(ActionButtonState state) => state switch
    {
        ActionButtonState.Pending => PendingLabel,
        ActionButtonState.Success => SuccessLabel,
        _ => Label
    };

    private Control CreateIcon(ActionButtonState state)
    {
        switch (state)
        {
            case ActionButtonState.Pending:
                return new ArcSpinner { Width = 16, Height = 16 };
            case ActionButtonState.Success:
            {
                var check = new StrokeDrawGlyph { Data = ArcIcons.Parse("M4 12l5 5L20 6"), Size = 17, StrokeThickness = 2 };
                if (IsLoaded && ArcMotion.IsFull(this))
                {
                    // The tick draws itself from its short stroke, the way a hand would write it.
                    check.Progress = 0;
                    check.AttachedToVisualTree += (_, _) => _ = check.Play(ArcMotionPresets.Standard, ArcEasings.Enter, TimeSpan.FromSeconds(0.05));
                }
                return check;
            }
            default:
                return new ArcGlyph { Data = ArcIcons.ArrowRight, Size = 17, StrokeThickness = 2 };
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
            if (State == ActionButtonState.Idle)
            {
                DisplayLabel = Label;
            }
        }
        else if (change.Property == PendingLabelProperty || change.Property == SuccessLabelProperty)
        {
            DisplayLabel = LabelFor(State);
        }
        else if (change.Property == BoundsProperty)
        {
            PseudoClasses.Set(WidePseudoClass, Bounds.Width > 220);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _resetTimer?.Stop();
        if (State == ActionButtonState.Success)
        {
            SetState(ActionButtonState.Idle);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ActionButtonAutomationPeer(this);

    private sealed class ActionButtonAutomationPeer : ButtonAutomationPeer
    {
        public ActionButtonAutomationPeer(ActionButton owner) : base(owner)
        {
        }

        // aria-busy + aria-disabled while pending; the button itself stays focusable.
        protected override bool IsEnabledCore() => base.IsEnabledCore() && ((ActionButton)Owner).State != ActionButtonState.Pending;

        protected override string? GetNameCore() => ((ActionButton)Owner).Label;
    }
}
