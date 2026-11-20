using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// An aircraft-style guarded switch for an action that must never fire by accident, such as a production release or
/// deleting a workspace. A brushed aluminium plate carries a chrome bat toggle under a translucent hinged safety cover
/// and a lamp with a hold ring. Lift the cover, throw the switch up until it snaps over centre and the lamp flickers
/// on, then press and hold the lamp for <see cref="HoldDuration"/> to run <see cref="ConfirmAction"/>. Success lights
/// the lamp green and, with <see cref="IsAutoSafeEnabled"/>, drops the cover, which throws the switch back to safe.
/// Closing the cover at any time makes an armed switch safe. Tab moves between the cover, the switch and the lamp;
/// Enter, Space and the arrow keys operate them and Escape makes everything safe.
/// </summary>
/// <remarks>
/// The plate is drawn by an internal stage control: the cover is a 3D box on a hinge and the panel leans towards the
/// pointer, so every part is projected through a perspective camera and driven by springs in one frame loop, which no
/// ControlTheme transition can express. The title, description, status chip and hint are template parts.
/// </remarks>
[TemplatePart(StagePart, typeof(ArmingSwitchStage))]
[TemplatePart(ChipPart, typeof(ArmingSwitchChip))]
[TemplatePart(HintPart, typeof(ArcContentSwap))]
[PseudoClasses(NarrowPseudoClass, TightPseudoClass, LockedPseudoClass)]
public partial class ArmingSwitch : TemplatedControl
{
    internal const string StagePart = "PART_Stage";
    internal const string ChipPart = "PART_Status";
    internal const string HintPart = "PART_Hint";
    private const string NarrowPseudoClass = ":narrow";
    private const string TightPseudoClass = ":tight";
    private const string LockedPseudoClass = ":locked";

    internal const string CoverFirstText = "Lift the cover first.";
    internal const string ArmFirstText = "Arm the switch first.";
    internal const string LockedText = "The switch is locked.";
    internal const string LiftCoverName = "Lift the safety cover";
    internal const string CloseCoverName = "Close the safety cover";

    /// <summary>The heading of the card, naming the action.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ArmingSwitch, string>(nameof(Title), "Release to production");

    /// <summary>One line under the heading saying what runs and where; empty hides it.</summary>
    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<ArmingSwitch, string>(nameof(Description), string.Empty);

    /// <summary>Engraved beside the switch's up position; also names the switch for assistive technology ("Arm switch").</summary>
    public static readonly StyledProperty<string> ArmLabelProperty =
        AvaloniaProperty.Register<ArmingSwitch, string>(nameof(ArmLabel), "Arm");

    /// <summary>Engraved beside the switch's down position.</summary>
    public static readonly StyledProperty<string> SafeLabelProperty =
        AvaloniaProperty.Register<ArmingSwitch, string>(nameof(SafeLabel), "Safe");

    /// <summary>Engraved under the lamp, and the accessible name of the lamp.</summary>
    public static readonly StyledProperty<string> HoldLabelProperty =
        AvaloniaProperty.Register<ArmingSwitch, string>(nameof(HoldLabel), "Hold to release");

    /// <summary>Engraved beside the small ready light, which beats while idle, flickers while the action runs and stays lit once it is done.</summary>
    public static readonly StyledProperty<string> ReadyLabelProperty =
        AvaloniaProperty.Register<ArmingSwitch, string>(nameof(ReadyLabel), "Ready");

    /// <summary>The status chip's text while the cover is down and the switch safe.</summary>
    public static readonly StyledProperty<string> SafeStatusTextProperty =
        AvaloniaProperty.Register<ArmingSwitch, string>(nameof(SafeStatusText), "Safe");

    /// <summary>The status chip's text while the cover is up and the switch safe.</summary>
    public static readonly StyledProperty<string> OpenStatusTextProperty =
        AvaloniaProperty.Register<ArmingSwitch, string>(nameof(OpenStatusText), "Cover up");

    /// <summary>The status chip's text while the switch is armed.</summary>
    public static readonly StyledProperty<string> ArmedStatusTextProperty =
        AvaloniaProperty.Register<ArmingSwitch, string>(nameof(ArmedStatusText), "Armed");

    /// <summary>The status chip's text while the action runs.</summary>
    public static readonly StyledProperty<string> RunningStatusTextProperty =
        AvaloniaProperty.Register<ArmingSwitch, string>(nameof(RunningStatusText), "Running");

    /// <summary>The status chip's text after the action succeeded.</summary>
    public static readonly StyledProperty<string> DoneStatusTextProperty =
        AvaloniaProperty.Register<ArmingSwitch, string>(nameof(DoneStatusText), "Done");

    /// <summary>The status chip's text after the action failed.</summary>
    public static readonly StyledProperty<string> FailedStatusTextProperty =
        AvaloniaProperty.Register<ArmingSwitch, string>(nameof(FailedStatusText), "Failed");

    /// <summary>The next step shown under the panel (and announced) while the cover is down.</summary>
    public static readonly StyledProperty<string> SafeHintProperty =
        AvaloniaProperty.Register<ArmingSwitch, string>(nameof(SafeHint), "Lift the cover to start.");

    /// <summary>The next step while the cover is up and the switch safe.</summary>
    public static readonly StyledProperty<string> OpenHintProperty =
        AvaloniaProperty.Register<ArmingSwitch, string>(nameof(OpenHint), "Flip the switch up to arm it.");

    /// <summary>The next step while the switch is armed.</summary>
    public static readonly StyledProperty<string> ArmedHintProperty =
        AvaloniaProperty.Register<ArmingSwitch, string>(nameof(ArmedHint), "Press and hold the lamp to run it.");

    /// <summary>The line shown while the action runs.</summary>
    public static readonly StyledProperty<string> RunningHintProperty =
        AvaloniaProperty.Register<ArmingSwitch, string>(nameof(RunningHint), "Running the action.");

    /// <summary>The line shown after the action succeeded.</summary>
    public static readonly StyledProperty<string> DoneHintProperty =
        AvaloniaProperty.Register<ArmingSwitch, string>(nameof(DoneHint), "Finished. Lift the cover to run it again.");

    /// <summary>The line shown after the action failed.</summary>
    public static readonly StyledProperty<string> FailedHintProperty =
        AvaloniaProperty.Register<ArmingSwitch, string>(nameof(FailedHint), "It did not go through. Hold the lamp to try again.");

    /// <summary>How long the lamp must be held to run the action (0.3 to 6 seconds, 1.2 by default). Letting go early drains the ring; pressing again picks up from there.</summary>
    public static readonly StyledProperty<TimeSpan> HoldDurationProperty =
        AvaloniaProperty.Register<ArmingSwitch, TimeSpan>(nameof(HoldDuration), TimeSpan.FromMilliseconds(1200),
            coerce: CoerceHoldDuration);

    /// <summary>
    /// The work to run when the hold completes. While its task runs the status is <see cref="ArmingSwitchStatus.Running"/>
    /// (the lamp pulses and the ring spins). True means success; false, or an exception, means failure. Null succeeds at once.
    /// </summary>
    public static readonly StyledProperty<Func<Task<bool>>?> ConfirmActionProperty =
        AvaloniaProperty.Register<ArmingSwitch, Func<Task<bool>>?>(nameof(ConfirmAction));

    /// <summary>After a success, drop the cover on its own a moment later, which throws the switch back to safe. The green lamp stays lit until the cover is lifted again.</summary>
    public static readonly StyledProperty<bool> IsAutoSafeEnabledProperty =
        AvaloniaProperty.Register<ArmingSwitch, bool>(nameof(IsAutoSafeEnabled), true);

    /// <summary>Locks the cover, the switch and the lamp while keeping them focusable: attempts rattle the cover and say that the switch is locked.</summary>
    public static readonly StyledProperty<bool> IsLockedProperty =
        AvaloniaProperty.Register<ArmingSwitch, bool>(nameof(IsLocked));

    /// <summary>The color of the hold ring; null uses the theme's accent.</summary>
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<ArmingSwitch, IBrush?>(nameof(AccentBrush));

    /// <summary>The color of the translucent cover and of the tinted shadow it casts; null derives it from the theme's accent.</summary>
    public static readonly StyledProperty<IBrush?> CoverBrushProperty =
        AvaloniaProperty.Register<ArmingSwitch, IBrush?>(nameof(CoverBrush));

    /// <summary>Where the control is in its lift, arm, hold and run ritual.</summary>
    public static readonly DirectProperty<ArmingSwitch, ArmingSwitchStatus> StatusProperty =
        AvaloniaProperty.RegisterDirect<ArmingSwitch, ArmingSwitchStatus>(nameof(Status), o => o.Status);

    /// <summary>Whether the safety cover is lifted.</summary>
    public static readonly DirectProperty<ArmingSwitch, bool> IsCoverOpenProperty =
        AvaloniaProperty.RegisterDirect<ArmingSwitch, bool>(nameof(IsCoverOpen), o => o.IsCoverOpen);

    /// <summary>Whether the switch is thrown up to armed.</summary>
    public static readonly DirectProperty<ArmingSwitch, bool> IsArmedProperty =
        AvaloniaProperty.RegisterDirect<ArmingSwitch, bool>(nameof(IsArmed), o => o.IsArmed);

    /// <summary>Raised whenever <see cref="Status"/> changes, with the exception when the action threw.</summary>
    public static readonly RoutedEvent<ArmingSwitchStatusChangedEventArgs> StatusChangedEvent =
        RoutedEvent.Register<ArmingSwitch, ArmingSwitchStatusChangedEventArgs>(nameof(StatusChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the cover is lifted or closed (<see cref="IsCoverOpen"/> changed), including when a drag carries it past halfway.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CoverOpenChangedEvent =
        RoutedEvent.Register<ArmingSwitch, RoutedEventArgs>(nameof(CoverOpenChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the switch snaps to armed or back to safe (<see cref="IsArmed"/> changed), including when a closing cover pushes it back.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ArmedChangedEvent =
        RoutedEvent.Register<ArmingSwitch, RoutedEventArgs>(nameof(ArmedChanged), RoutingStrategies.Bubble);

    /// <summary>Raised once when a hold completes, before <see cref="ConfirmAction"/> runs.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ConfirmedEvent =
        RoutedEvent.Register<ArmingSwitch, RoutedEventArgs>(nameof(Confirmed), RoutingStrategies.Bubble);

    // Inks the theme sets from ArcDesign (the status tones and the accent).
    internal static readonly StyledProperty<IBrush?> AccentInkProperty =
        AvaloniaProperty.Register<ArmingSwitch, IBrush?>(nameof(AccentInk));

    private ArmingSwitchStatus _status;
    private bool _coverOpen;
    private bool _armed;
    private bool _running;
    private ArmingSwitchStatus? _result;
    private Exception? _lastError;
    private int _runVersion;
    private IDisposable? _autoSafeTimer;
    private IDisposable? _refusalTimer;
    private string? _refusal;
    private readonly ExplainerAnnouncer _announcer = new();
    private ArmingSwitchStage? _stage;
    private ArmingSwitchChip? _chip;
    private ArcContentSwap? _hint;
    private string? _shownHint;
    private bool _shownRefusal;

    static ArmingSwitch()
    {
        ClipToBoundsProperty.OverrideDefaultValue<ArmingSwitch>(false);
        FocusableProperty.OverrideDefaultValue<ArmingSwitch>(false);
    }

    public ArmingSwitch()
    {
        LogicalChildren.Add(_announcer);
        VisualChildren.Add(_announcer);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="ArmLabelProperty"/>
    public string ArmLabel { get => GetValue(ArmLabelProperty); set => SetValue(ArmLabelProperty, value); }

    /// <inheritdoc cref="SafeLabelProperty"/>
    public string SafeLabel { get => GetValue(SafeLabelProperty); set => SetValue(SafeLabelProperty, value); }

    /// <inheritdoc cref="HoldLabelProperty"/>
    public string HoldLabel { get => GetValue(HoldLabelProperty); set => SetValue(HoldLabelProperty, value); }

    /// <inheritdoc cref="ReadyLabelProperty"/>
    public string ReadyLabel { get => GetValue(ReadyLabelProperty); set => SetValue(ReadyLabelProperty, value); }

    /// <inheritdoc cref="SafeStatusTextProperty"/>
    public string SafeStatusText { get => GetValue(SafeStatusTextProperty); set => SetValue(SafeStatusTextProperty, value); }

    /// <inheritdoc cref="OpenStatusTextProperty"/>
    public string OpenStatusText { get => GetValue(OpenStatusTextProperty); set => SetValue(OpenStatusTextProperty, value); }

    /// <inheritdoc cref="ArmedStatusTextProperty"/>
    public string ArmedStatusText { get => GetValue(ArmedStatusTextProperty); set => SetValue(ArmedStatusTextProperty, value); }

    /// <inheritdoc cref="RunningStatusTextProperty"/>
    public string RunningStatusText { get => GetValue(RunningStatusTextProperty); set => SetValue(RunningStatusTextProperty, value); }

    /// <inheritdoc cref="DoneStatusTextProperty"/>
    public string DoneStatusText { get => GetValue(DoneStatusTextProperty); set => SetValue(DoneStatusTextProperty, value); }

    /// <inheritdoc cref="FailedStatusTextProperty"/>
    public string FailedStatusText { get => GetValue(FailedStatusTextProperty); set => SetValue(FailedStatusTextProperty, value); }

    /// <inheritdoc cref="SafeHintProperty"/>
    public string SafeHint { get => GetValue(SafeHintProperty); set => SetValue(SafeHintProperty, value); }

    /// <inheritdoc cref="OpenHintProperty"/>
    public string OpenHint { get => GetValue(OpenHintProperty); set => SetValue(OpenHintProperty, value); }

    /// <inheritdoc cref="ArmedHintProperty"/>
    public string ArmedHint { get => GetValue(ArmedHintProperty); set => SetValue(ArmedHintProperty, value); }

    /// <inheritdoc cref="RunningHintProperty"/>
    public string RunningHint { get => GetValue(RunningHintProperty); set => SetValue(RunningHintProperty, value); }

    /// <inheritdoc cref="DoneHintProperty"/>
    public string DoneHint { get => GetValue(DoneHintProperty); set => SetValue(DoneHintProperty, value); }

    /// <inheritdoc cref="FailedHintProperty"/>
    public string FailedHint { get => GetValue(FailedHintProperty); set => SetValue(FailedHintProperty, value); }

    /// <inheritdoc cref="HoldDurationProperty"/>
    public TimeSpan HoldDuration { get => GetValue(HoldDurationProperty); set => SetValue(HoldDurationProperty, value); }

    /// <inheritdoc cref="ConfirmActionProperty"/>
    public Func<Task<bool>>? ConfirmAction { get => GetValue(ConfirmActionProperty); set => SetValue(ConfirmActionProperty, value); }

    /// <inheritdoc cref="IsAutoSafeEnabledProperty"/>
    public bool IsAutoSafeEnabled { get => GetValue(IsAutoSafeEnabledProperty); set => SetValue(IsAutoSafeEnabledProperty, value); }

    /// <inheritdoc cref="IsLockedProperty"/>
    public bool IsLocked { get => GetValue(IsLockedProperty); set => SetValue(IsLockedProperty, value); }

    /// <inheritdoc cref="AccentBrushProperty"/>
    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    /// <inheritdoc cref="CoverBrushProperty"/>
    public IBrush? CoverBrush { get => GetValue(CoverBrushProperty); set => SetValue(CoverBrushProperty, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public ArmingSwitchStatus Status
    {
        get => _status;
        private set => SetAndRaise(StatusProperty, ref _status, value);
    }

    /// <inheritdoc cref="IsCoverOpenProperty"/>
    public bool IsCoverOpen
    {
        get => _coverOpen;
        private set => SetAndRaise(IsCoverOpenProperty, ref _coverOpen, value);
    }

    /// <inheritdoc cref="IsArmedProperty"/>
    public bool IsArmed
    {
        get => _armed;
        private set => SetAndRaise(IsArmedProperty, ref _armed, value);
    }

    /// <inheritdoc cref="StatusChangedEvent"/>
    public event EventHandler<ArmingSwitchStatusChangedEventArgs>? StatusChanged
    {
        add => AddHandler(StatusChangedEvent, value);
        remove => RemoveHandler(StatusChangedEvent, value);
    }

    /// <inheritdoc cref="CoverOpenChangedEvent"/>
    public event EventHandler<RoutedEventArgs>? CoverOpenChanged
    {
        add => AddHandler(CoverOpenChangedEvent, value);
        remove => RemoveHandler(CoverOpenChangedEvent, value);
    }

    /// <inheritdoc cref="ArmedChangedEvent"/>
    public event EventHandler<RoutedEventArgs>? ArmedChanged
    {
        add => AddHandler(ArmedChangedEvent, value);
        remove => RemoveHandler(ArmedChangedEvent, value);
    }

    /// <inheritdoc cref="ConfirmedEvent"/>
    public event EventHandler<RoutedEventArgs>? Confirmed
    {
        add => AddHandler(ConfirmedEvent, value);
        remove => RemoveHandler(ConfirmedEvent, value);
    }

    internal IBrush? AccentInk { get => GetValue(AccentInkProperty); set => SetValue(AccentInkProperty, value); }

    /// <summary>The hidden polite live region (tests read its history).</summary>
    internal ExplainerAnnouncer Announcer => _announcer;

    internal ArmingSwitchStage? Stage => _stage;

    /// <summary>The line under the panel right now: a refusal while one shows, otherwise the hint of the status.</summary>
    internal string CurrentHint => _refusal ?? HintFor(_status);

    /// <summary>True while the action runs (the cover and the switch ignore input then).</summary>
    internal bool IsRunning => _running;

    internal double HoldSeconds => HoldDuration.TotalSeconds;

    /// <summary>Lifts the safety cover, as clicking it does. Ignored while locked or running.</summary>
    public void OpenCover() => SetCover(true);

    /// <summary>Closes the safety cover, which also makes an armed switch safe. Ignored while running.</summary>
    public void CloseCover() => SetCover(false);

    /// <summary>Throws the switch up to armed. Ignored while the cover is closed, while locked and while running.</summary>
    public void Arm() => SetArmed(true);

    /// <summary>Makes everything safe, as Escape does: ends a hold, closes the cover and returns the switch to safe. Ignored while running.</summary>
    public void MakeSafe()
    {
        if (_running)
        {
            return;
        }
        _stage?.CancelHold();
        SetCover(false);
    }

    private static TimeSpan CoerceHoldDuration(AvaloniaObject _, TimeSpan value)
    {
        var ms = Math.Clamp(value.TotalMilliseconds, 300, 6000);
        return double.IsNaN(ms) ? TimeSpan.FromMilliseconds(1200) : TimeSpan.FromMilliseconds(ms);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _stage?.Detach();
        _stage = e.NameScope.Find<ArmingSwitchStage>(StagePart);
        _chip = e.NameScope.Find<ArmingSwitchChip>(ChipPart);
        _hint = e.NameScope.Find<ArcContentSwap>(HintPart);
        _stage?.Attach(this);
        _shownHint = null;
        UpdateChip(animate: false);
        UpdateHint(animate: false);
        UpdateAutomation();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == BoundsProperty)
        {
            UpdateWidthClasses(change.GetNewValue<Rect>().Width);
        }
        else if (p == IsLockedProperty)
        {
            PseudoClasses.Set(LockedPseudoClass, IsLocked);
        }
        else if (p == SafeStatusTextProperty || p == OpenStatusTextProperty || p == ArmedStatusTextProperty
                 || p == RunningStatusTextProperty || p == DoneStatusTextProperty || p == FailedStatusTextProperty)
        {
            UpdateChip(animate: false);
        }
        else if (p == SafeHintProperty || p == OpenHintProperty || p == ArmedHintProperty || p == RunningHintProperty
                 || p == DoneHintProperty || p == FailedHintProperty)
        {
            UpdateHint(animate: false);
        }
        else if (p == ArmLabelProperty || p == SafeLabelProperty || p == HoldLabelProperty || p == ReadyLabelProperty
                 || p == AccentBrushProperty || p == CoverBrushProperty || p == AccentInkProperty)
        {
            _stage?.InvalidateLook();
            UpdateAutomation();
        }
        else if (p == TitleProperty)
        {
            UpdateAutomation();
        }
    }

    private void UpdateWidthClasses(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var wide = Token(ArmingSwitchTokenKind.WideFromWidth, 720);
        var tight = Token(ArmingSwitchTokenKind.TightBelowWidth, 420);
        PseudoClasses.Set(NarrowPseudoClass, width < wide);
        PseudoClasses.Set(TightPseudoClass, width < tight);
    }

    internal double Token(ArmingSwitchTokenKind kind, double fallback) =>
        this.TryFindResource(ArmingSwitchTokens.Key(kind), ActualThemeVariant, out var value) && value is double d ? d : fallback;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateWidthClasses(Bounds.Width);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopTimers();
        _stage?.CancelHold();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && !_running && (_coverOpen || _armed))
        {
            MakeSafe();
            e.Handled = true;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _announcer.Measure(default);
        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _announcer.Arrange(default);
        return base.ArrangeOverride(finalSize);
    }

    // --- the ritual ---

    /// <summary>Opens or closes the cover from the user or code; returns whether the state changed.</summary>
    internal bool SetCover(bool open, bool fromDrag = false)
    {
        if (_running || open == _coverOpen)
        {
            return false;
        }
        if (open && IsLocked)
        {
            Refuse(LockedText, rattle: true);
            return false;
        }
        if (open)
        {
            // Lifting the cover again starts a new round: a finished result goes away.
            if (_result == ArmingSwitchStatus.Done)
            {
                _result = null;
            }
        }
        StopAutoSafe();
        IsCoverOpen = open;
        RaiseEvent(new RoutedEventArgs(CoverOpenChangedEvent));
        if (!open && _armed)
        {
            // Closing the cover pushes an armed switch back to safe, as on the real thing.
            _stage?.CancelHold();
            SetArmedCore(false);
        }
        _stage?.OnCoverChanged(open, fromDrag);
        RefreshStatus();
        UpdateAutomation();
        return true;
    }

    /// <summary>Arms or safes the switch from the user or code; returns whether the state changed.</summary>
    internal bool SetArmed(bool armed, bool fromUser = false)
    {
        if (_running || armed == _armed)
        {
            return false;
        }
        if (IsLocked)
        {
            Refuse(LockedText, rattle: true);
            return false;
        }
        if (armed && !_coverOpen)
        {
            if (fromUser)
            {
                _stage?.Knock();
                Refuse(CoverFirstText, rattle: false);
            }
            return false;
        }
        SetArmedCore(armed);
        RefreshStatus();
        UpdateAutomation();
        return true;
    }

    private void SetArmedCore(bool armed)
    {
        if (!armed)
        {
            _stage?.CancelHold();
            if (_result == ArmingSwitchStatus.Failed)
            {
                _result = null;
            }
        }
        IsArmed = armed;
        _stage?.OnArmedChanged(armed);
        RaiseEvent(new RoutedEventArgs(ArmedChangedEvent));
    }

    /// <summary>Whether a press on the lamp may start a hold; refuses (and says why) when it may not.</summary>
    internal bool CanHold(bool refuse)
    {
        if (_running)
        {
            return false;
        }
        if (IsLocked)
        {
            if (refuse)
            {
                Refuse(LockedText, rattle: true);
            }
            return false;
        }
        if (!_armed)
        {
            if (refuse)
            {
                Refuse(_coverOpen ? ArmFirstText : CoverFirstText, rattle: false);
            }
            return false;
        }
        return true;
    }

    /// <summary>Called by the stage when the hold ring fills: raises <see cref="Confirmed"/> and runs the action.</summary>
    internal void CompleteHold()
    {
        if (_running || !_armed)
        {
            return;
        }
        RaiseEvent(new RoutedEventArgs(ConfirmedEvent));
        _result = null;
        _lastError = null;
        var version = ++_runVersion;
        var action = ConfirmAction;
        _stage?.OnHoldCompleted();
        if (action is null)
        {
            Finish(version, true, null);
            return;
        }
        Task<bool>? task = null;
        Exception? thrown = null;
        try
        {
            task = action();
        }
        catch (Exception ex)
        {
            thrown = ex;
        }
        if (thrown is not null || task is null)
        {
            Finish(version, false, thrown);
            return;
        }
        if (task.IsCompleted)
        {
            FinishFromTask(version, task);
            return;
        }
        _running = true;
        RefreshStatus();
        UpdateAutomation();
        Await(version, task);
    }

    private async void Await(int version, Task<bool> task)
    {
        try
        {
            await task;
        }
        catch
        {
            // Read below from the task itself.
        }
        FinishFromTask(version, task);
    }

    private void FinishFromTask(int version, Task<bool> task)
    {
        if (task.IsCompletedSuccessfully)
        {
            Finish(version, task.Result, null);
        }
        else
        {
            Finish(version, false, task.Exception?.InnerException ?? task.Exception);
        }
    }

    private void Finish(int version, bool success, Exception? error)
    {
        if (version != _runVersion)
        {
            return;
        }
        _running = false;
        _lastError = error;
        _result = success ? ArmingSwitchStatus.Done : ArmingSwitchStatus.Failed;
        _stage?.OnResult(success);
        RefreshStatus();
        UpdateAutomation();
        if (success && IsAutoSafeEnabled)
        {
            StopAutoSafe();
            _autoSafeTimer = DispatcherTimer.RunOnce(() =>
            {
                _autoSafeTimer = null;
                if (version == _runVersion && _status == ArmingSwitchStatus.Done && _coverOpen)
                {
                    SetCover(false);
                }
            }, TimeSpan.FromMilliseconds(1150));
        }
    }

    private void StopAutoSafe()
    {
        _autoSafeTimer?.Dispose();
        _autoSafeTimer = null;
    }

    private void StopTimers()
    {
        StopAutoSafe();
        _refusalTimer?.Dispose();
        _refusalTimer = null;
    }

    private ArmingSwitchStatus Compute()
    {
        if (_running)
        {
            return ArmingSwitchStatus.Running;
        }
        if (_result == ArmingSwitchStatus.Done)
        {
            return ArmingSwitchStatus.Done;
        }
        if (_result == ArmingSwitchStatus.Failed && _armed)
        {
            return ArmingSwitchStatus.Failed;
        }
        if (_armed)
        {
            return ArmingSwitchStatus.Armed;
        }
        return _coverOpen ? ArmingSwitchStatus.Open : ArmingSwitchStatus.Safe;
    }

    private void RefreshStatus()
    {
        var old = _status;
        var next = Compute();
        if (old == next)
        {
            return;
        }
        ClearRefusal();
        Status = next;
        UpdateChip(animate: true);
        UpdateHint(animate: true);
        _stage?.OnStatusChanged(old, next);
        _announcer.Announce(StatusText(next) + ". " + HintFor(next));
        RaiseEvent(new ArmingSwitchStatusChangedEventArgs(StatusChangedEvent, old, next,
            next == ArmingSwitchStatus.Failed ? _lastError : null));
    }

    /// <summary>Replaces the hint with a short refusal for about two seconds and announces it.</summary>
    internal void Refuse(string text, bool rattle)
    {
        if (rattle)
        {
            _stage?.Rattle();
        }
        _refusal = text;
        UpdateHint(animate: true);
        _announcer.Announce(text);
        _refusalTimer?.Dispose();
        _refusalTimer = DispatcherTimer.RunOnce(() =>
        {
            _refusalTimer = null;
            ClearRefusal();
            UpdateHint(animate: true);
        }, TimeSpan.FromMilliseconds(1900));
        UpdateAutomation();
    }

    private void ClearRefusal()
    {
        _refusal = null;
        _refusalTimer?.Dispose();
        _refusalTimer = null;
    }

    internal string StatusText(ArmingSwitchStatus status) => status switch
    {
        ArmingSwitchStatus.Open => OpenStatusText,
        ArmingSwitchStatus.Armed => ArmedStatusText,
        ArmingSwitchStatus.Running => RunningStatusText,
        ArmingSwitchStatus.Done => DoneStatusText,
        ArmingSwitchStatus.Failed => FailedStatusText,
        _ => SafeStatusText
    };

    internal string HintFor(ArmingSwitchStatus status) => status switch
    {
        ArmingSwitchStatus.Open => OpenHint,
        ArmingSwitchStatus.Armed => ArmedHint,
        ArmingSwitchStatus.Running => RunningHint,
        ArmingSwitchStatus.Done => DoneHint,
        ArmingSwitchStatus.Failed => FailedHint,
        _ => SafeHint
    };

    private void UpdateChip(bool animate)
    {
        if (_chip is null)
        {
            return;
        }
        _chip.SetTexts(SafeStatusText, OpenStatusText, ArmedStatusText, RunningStatusText, DoneStatusText, FailedStatusText);
        _chip.SetStatus(_status, animate);
    }

    private void UpdateHint(bool animate)
    {
        if (_hint is null)
        {
            return;
        }
        var text = CurrentHint;
        var refusal = _refusal is not null;
        if (text == _shownHint && refusal == _shownRefusal)
        {
            return;
        }
        _shownHint = text;
        _shownRefusal = refusal;
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty,
            refusal ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.TextSecondary);
        var mode = _hint.Mode;
        if (!animate)
        {
            _hint.Mode = ContentSwapMode.None;
        }
        _hint.Content = block;
        _hint.Mode = mode;
    }

    private void UpdateAutomation() => _stage?.UpdateAutomation();

    protected override AutomationPeer OnCreateAutomationPeer() => new ArmingSwitchAutomationPeer(this);

    private sealed class ArmingSwitchAutomationPeer : ControlAutomationPeer
    {
        public ArmingSwitchAutomationPeer(ArmingSwitch owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : ((ArmingSwitch)Owner).Title;
    }
}
