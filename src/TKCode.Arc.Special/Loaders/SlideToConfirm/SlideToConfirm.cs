using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A slider you drag to the end to confirm an action, for payments, transfers and other deliberate steps. A liquid fill
/// follows the thumb with a lag and necks down when the thumb pulls ahead, while the track swells around the pressed
/// thumb. Letting go before <see cref="Threshold"/> springs the thumb back. Letting go past it confirms: the track
/// draws into the thumb, a spinner shows while the task from <see cref="ConfirmAction"/> runs, and the arrow turns into
/// a check with <see cref="ConfirmedLabel"/> beside it. Arrow, Page, Home and End keys move the thumb; holding Space or
/// Enter glides it to the end.
/// </summary>
/// <remarks>
/// The control draws itself (track, liquid, label, thumb and status in one <see cref="Render"/>): the liquid's neck and
/// the track's swell are outlines rebuilt from the motion state on every frame, which no ControlTheme transition can
/// express. The motion runs in one <see cref="LiveLoop"/> that sleeps as soon as everything has settled.
/// </remarks>
[PseudoClasses(DraggingPseudoClass, PendingPseudoClass, DonePseudoClass, ErrorPseudoClass)]
public partial class SlideToConfirm : TemplatedControl
{
    private const string DraggingPseudoClass = ":dragging";
    private const string PendingPseudoClass = ":pending";
    private const string DonePseudoClass = ":done";
    private const string ErrorPseudoClass = ":error";

    /// <summary>The instruction drawn in the track and used as the slider's accessible name.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SlideToConfirm, string>(nameof(Label), "Slide to confirm");

    /// <summary>Shown beside the spinner and announced while the task from <see cref="ConfirmAction"/> runs.</summary>
    public static readonly StyledProperty<string> PendingLabelProperty =
        AvaloniaProperty.Register<SlideToConfirm, string>(nameof(PendingLabel), "Processing");

    /// <summary>Shown beside the check and announced once the action succeeds.</summary>
    public static readonly StyledProperty<string> ConfirmedLabelProperty =
        AvaloniaProperty.Register<SlideToConfirm, string>(nameof(ConfirmedLabel), "Confirmed");

    /// <summary>Replaces the label, in the danger color, after the action fails, until the next slide starts.</summary>
    public static readonly StyledProperty<string> ErrorLabelProperty =
        AvaloniaProperty.Register<SlideToConfirm, string>(nameof(ErrorLabel), "Didn't go through, slide to retry");

    /// <summary>
    /// The work to run once a slide completes. While its task runs the control shows the spinner and
    /// <see cref="PendingLabel"/>; success shows the check, an exception shows <see cref="ErrorLabel"/>. Null goes
    /// straight to success.
    /// </summary>
    public static readonly StyledProperty<Func<Task>?> ConfirmActionProperty =
        AvaloniaProperty.Register<SlideToConfirm, Func<Task>?>(nameof(ConfirmAction));

    /// <summary>The fraction of the track the thumb must pass before letting go confirms (0.5 to 1). Below it the thumb springs back.</summary>
    public static readonly StyledProperty<double> ThresholdProperty =
        AvaloniaProperty.Register<SlideToConfirm, double>(nameof(Threshold), 0.9, coerce: CoerceThreshold);

    /// <summary>How long after success the control expands back to its idle state; null stays confirmed until <see cref="Reset"/>.</summary>
    public static readonly StyledProperty<TimeSpan?> ResetAfterProperty =
        AvaloniaProperty.Register<SlideToConfirm, TimeSpan?>(nameof(ResetAfter));

    /// <summary>Nudges the thumb every few seconds while idle and untouched, so people see that it moves. Only with full motion.</summary>
    public static readonly StyledProperty<bool> IsHintEnabledProperty =
        AvaloniaProperty.Register<SlideToConfirm, bool>(nameof(IsHintEnabled), true);

    /// <summary>The color of the liquid fill and of the success circle; null uses the theme's control fill, which follows the accent.</summary>
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<SlideToConfirm, IBrush?>(nameof(AccentBrush));

    /// <summary>Where the control is in its slide and confirm cycle.</summary>
    public static readonly DirectProperty<SlideToConfirm, SlideToConfirmState> StateProperty =
        AvaloniaProperty.RegisterDirect<SlideToConfirm, SlideToConfirmState>(nameof(State), o => o.State);

    /// <summary>How far the thumb is along the track, from 0 (start) to 1 (end).</summary>
    public static readonly DirectProperty<SlideToConfirm, double> ProgressProperty =
        AvaloniaProperty.RegisterDirect<SlideToConfirm, double>(nameof(Progress), o => o.Progress);

    /// <summary>Raised once when a slide completes, before <see cref="ConfirmAction"/> runs.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ConfirmedEvent =
        RoutedEvent.Register<SlideToConfirm, RoutedEventArgs>(nameof(Confirmed), RoutingStrategies.Bubble);

    /// <summary>Raised whenever <see cref="State"/> changes, with the exception when the action failed.</summary>
    public static readonly RoutedEvent<SlideToConfirmStateChangedEventArgs> StateChangedEvent =
        RoutedEvent.Register<SlideToConfirm, SlideToConfirmStateChangedEventArgs>(nameof(StateChanged), RoutingStrategies.Bubble);

    // Inks and geometry the theme sets from ArcDesign and SlideToConfirmToken.
    internal static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<SlideToConfirm, IBrush?>(nameof(TrackBrush));

    internal static readonly StyledProperty<IBrush?> TrackHoverBrushProperty =
        AvaloniaProperty.Register<SlideToConfirm, IBrush?>(nameof(TrackHoverBrush));

    internal static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<SlideToConfirm, IBrush?>(nameof(FillBrush));

    internal static readonly StyledProperty<IBrush?> OnFillBrushProperty =
        AvaloniaProperty.Register<SlideToConfirm, IBrush?>(nameof(OnFillBrush));

    internal static readonly StyledProperty<IBrush?> ThumbBrushProperty =
        AvaloniaProperty.Register<SlideToConfirm, IBrush?>(nameof(ThumbBrush));

    internal static readonly StyledProperty<IBrush?> ThumbInkProperty =
        AvaloniaProperty.Register<SlideToConfirm, IBrush?>(nameof(ThumbInk));

    internal static readonly StyledProperty<IBrush?> StatusBrushProperty =
        AvaloniaProperty.Register<SlideToConfirm, IBrush?>(nameof(StatusBrush));

    internal static readonly StyledProperty<IBrush?> DangerBrushProperty =
        AvaloniaProperty.Register<SlideToConfirm, IBrush?>(nameof(DangerBrush));

    internal static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<SlideToConfirm, IBrush?>(nameof(FocusRingBrush));

    internal static readonly StyledProperty<IBrush?> InsetShadowProperty =
        AvaloniaProperty.Register<SlideToConfirm, IBrush?>(nameof(InsetShadow));

    internal static readonly StyledProperty<BoxShadows> ThumbShadowProperty =
        AvaloniaProperty.Register<SlideToConfirm, BoxShadows>(nameof(ThumbShadow));

    internal static readonly StyledProperty<double> TrackHeightProperty =
        AvaloniaProperty.Register<SlideToConfirm, double>(nameof(TrackHeight), 56);

    internal static readonly StyledProperty<double> TallTrackHeightProperty =
        AvaloniaProperty.Register<SlideToConfirm, double>(nameof(TallTrackHeight), 64);

    internal static readonly StyledProperty<double> TallFromWidthProperty =
        AvaloniaProperty.Register<SlideToConfirm, double>(nameof(TallFromWidth), 360);

    internal static readonly StyledProperty<double> LabelFontSizeProperty =
        AvaloniaProperty.Register<SlideToConfirm, double>(nameof(LabelFontSize), 13);

    internal static readonly StyledProperty<double> TallLabelFontSizeProperty =
        AvaloniaProperty.Register<SlideToConfirm, double>(nameof(TallLabelFontSize), 15);

    internal static readonly StyledProperty<double> StatusGapProperty =
        AvaloniaProperty.Register<SlideToConfirm, double>(nameof(StatusGap), 14);

    internal static readonly StyledProperty<double> GrabReachProperty =
        AvaloniaProperty.Register<SlideToConfirm, double>(nameof(GrabReach), 36);

    internal static readonly StyledProperty<double> DisabledOpacityProperty =
        AvaloniaProperty.Register<SlideToConfirm, double>(nameof(DisabledOpacity), 0.52);

    private const string DefaultHelpText = "Drag the thumb to the end, or hold Space or Enter, to confirm.";

    private SlideToConfirmState _state;
    private double _progress;
    private readonly ExplainerAnnouncer _announcer = new();
    private int _actionVersion;
    private IDisposable? _resetTimer;

    static SlideToConfirm()
    {
        FocusableProperty.OverrideDefaultValue<SlideToConfirm>(true);
        ClipToBoundsProperty.OverrideDefaultValue<SlideToConfirm>(false);
        AffectsMeasure<SlideToConfirm>(TrackHeightProperty, TallTrackHeightProperty, TallFromWidthProperty);
        AffectsRender<SlideToConfirm>(TrackBrushProperty, TrackHoverBrushProperty, FillBrushProperty, OnFillBrushProperty,
            ThumbBrushProperty, ThumbInkProperty, StatusBrushProperty, DangerBrushProperty, FocusRingBrushProperty,
            InsetShadowProperty, ThumbShadowProperty, AccentBrushProperty, ForegroundProperty, BorderBrushProperty);
    }

    public SlideToConfirm()
    {
        LogicalChildren.Add(_announcer);
        VisualChildren.Add(_announcer);
        _loop = new LiveLoop(this, StepFrame) { MinimumLevel = ArcMotionLevel.Reduced };
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="PendingLabelProperty"/>
    public string PendingLabel
    {
        get => GetValue(PendingLabelProperty);
        set => SetValue(PendingLabelProperty, value);
    }

    /// <inheritdoc cref="ConfirmedLabelProperty"/>
    public string ConfirmedLabel
    {
        get => GetValue(ConfirmedLabelProperty);
        set => SetValue(ConfirmedLabelProperty, value);
    }

    /// <inheritdoc cref="ErrorLabelProperty"/>
    public string ErrorLabel
    {
        get => GetValue(ErrorLabelProperty);
        set => SetValue(ErrorLabelProperty, value);
    }

    /// <inheritdoc cref="ConfirmActionProperty"/>
    public Func<Task>? ConfirmAction
    {
        get => GetValue(ConfirmActionProperty);
        set => SetValue(ConfirmActionProperty, value);
    }

    /// <inheritdoc cref="ThresholdProperty"/>
    public double Threshold
    {
        get => GetValue(ThresholdProperty);
        set => SetValue(ThresholdProperty, value);
    }

    /// <inheritdoc cref="ResetAfterProperty"/>
    public TimeSpan? ResetAfter
    {
        get => GetValue(ResetAfterProperty);
        set => SetValue(ResetAfterProperty, value);
    }

    /// <inheritdoc cref="IsHintEnabledProperty"/>
    public bool IsHintEnabled
    {
        get => GetValue(IsHintEnabledProperty);
        set => SetValue(IsHintEnabledProperty, value);
    }

    /// <inheritdoc cref="AccentBrushProperty"/>
    public IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    /// <inheritdoc cref="StateProperty"/>
    public SlideToConfirmState State
    {
        get => _state;
        private set
        {
            var old = _state;
            if (SetAndRaise(StateProperty, ref _state, value))
            {
                PseudoClasses.Set(DraggingPseudoClass, value == SlideToConfirmState.Dragging);
                PseudoClasses.Set(PendingPseudoClass, value == SlideToConfirmState.Pending);
                PseudoClasses.Set(DonePseudoClass, value == SlideToConfirmState.Done);
                PseudoClasses.Set(ErrorPseudoClass, value == SlideToConfirmState.Error);
                RaiseEvent(new SlideToConfirmStateChangedEventArgs(StateChangedEvent, old, value,
                    value == SlideToConfirmState.Error ? _lastError : null));
            }
        }
    }

    /// <inheritdoc cref="ProgressProperty"/>
    public double Progress
    {
        get => _progress;
        private set => SetAndRaise(ProgressProperty, ref _progress, value);
    }

    /// <inheritdoc cref="ConfirmedEvent"/>
    public event EventHandler<RoutedEventArgs>? Confirmed
    {
        add => AddHandler(ConfirmedEvent, value);
        remove => RemoveHandler(ConfirmedEvent, value);
    }

    /// <inheritdoc cref="StateChangedEvent"/>
    public event EventHandler<SlideToConfirmStateChangedEventArgs>? StateChanged
    {
        add => AddHandler(StateChangedEvent, value);
        remove => RemoveHandler(StateChangedEvent, value);
    }

    internal IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    internal IBrush? TrackHoverBrush { get => GetValue(TrackHoverBrushProperty); set => SetValue(TrackHoverBrushProperty, value); }

    internal IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    internal IBrush? OnFillBrush { get => GetValue(OnFillBrushProperty); set => SetValue(OnFillBrushProperty, value); }

    internal IBrush? ThumbBrush { get => GetValue(ThumbBrushProperty); set => SetValue(ThumbBrushProperty, value); }

    internal IBrush? ThumbInk { get => GetValue(ThumbInkProperty); set => SetValue(ThumbInkProperty, value); }

    internal IBrush? StatusBrush { get => GetValue(StatusBrushProperty); set => SetValue(StatusBrushProperty, value); }

    internal IBrush? DangerBrush { get => GetValue(DangerBrushProperty); set => SetValue(DangerBrushProperty, value); }

    internal IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }

    internal IBrush? InsetShadow { get => GetValue(InsetShadowProperty); set => SetValue(InsetShadowProperty, value); }

    internal BoxShadows ThumbShadow { get => GetValue(ThumbShadowProperty); set => SetValue(ThumbShadowProperty, value); }

    internal double TrackHeight { get => GetValue(TrackHeightProperty); set => SetValue(TrackHeightProperty, value); }

    internal double TallTrackHeight { get => GetValue(TallTrackHeightProperty); set => SetValue(TallTrackHeightProperty, value); }

    internal double TallFromWidth { get => GetValue(TallFromWidthProperty); set => SetValue(TallFromWidthProperty, value); }

    internal double LabelFontSize { get => GetValue(LabelFontSizeProperty); set => SetValue(LabelFontSizeProperty, value); }

    internal double TallLabelFontSize { get => GetValue(TallLabelFontSizeProperty); set => SetValue(TallLabelFontSizeProperty, value); }

    internal double StatusGap { get => GetValue(StatusGapProperty); set => SetValue(StatusGapProperty, value); }

    internal double GrabReach { get => GetValue(GrabReachProperty); set => SetValue(GrabReachProperty, value); }

    internal double DisabledOpacity { get => GetValue(DisabledOpacityProperty); set => SetValue(DisabledOpacityProperty, value); }

    /// <summary>The hidden polite live region (tests read its history).</summary>
    internal ExplainerAnnouncer Announcer => _announcer;

    /// <summary>True while the pending, done or error choreography owns the control (input ignored except a new slide after an error).</summary>
    private bool IsBusy => _state is SlideToConfirmState.Pending or SlideToConfirmState.Done || _returning;

    /// <summary>
    /// Confirms from code as the End key does: the thumb glides to the end and the slide completes. Ignored while
    /// pending or confirmed, and while disabled.
    /// </summary>
    public void Confirm()
    {
        if (!IsEffectivelyEnabled || IsBusy)
        {
            return;
        }
        BeginCommit();
    }

    /// <summary>
    /// Returns a confirmed (or failed) control to its idle state: the track expands back and the thumb returns. Ignored
    /// while the action is still running.
    /// </summary>
    public void Reset()
    {
        if (_state is SlideToConfirmState.Done)
        {
            BeginReturn(failed: false);
        }
        else if (_state is SlideToConfirmState.Error)
        {
            _showError = false;
            State = SlideToConfirmState.Idle;
            InvalidateVisual();
        }
    }

    private static double CoerceThreshold(AvaloniaObject _, double value)
        => double.IsNaN(value) ? 0.9 : Math.Clamp(value, 0.5, 1);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty || change.Property == ErrorLabelProperty
            || change.Property == PendingLabelProperty || change.Property == ConfirmedLabelProperty
            || change.Property == FontFamilyProperty || change.Property == ForegroundProperty
            || change.Property == OnFillBrushProperty || change.Property == DangerBrushProperty
            || change.Property == StatusBrushProperty || change.Property == LabelFontSizeProperty
            || change.Property == TallLabelFontSizeProperty)
        {
            InvalidateText();
        }
        else if (change.Property == IsEffectivelyEnabledProperty)
        {
            if (!IsEffectivelyEnabled)
            {
                CancelGesture();
            }
            InvalidateVisual();
        }
        else if (change.Property == IsHintEnabledProperty)
        {
            UpdateHintTimer();
        }
        else if (change.Property == ArcMotion.LevelProperty)
        {
            UpdateHintTimer();
            Kick();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        _hintFirst = true;
        UpdateHintTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        StopHintTimer();
        _resetTimer?.Dispose();
        _resetTimer = null;
        CancelGesture();
        SnapAll();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _announcer.Measure(default);
        var width = double.IsInfinity(availableSize.Width) ? 350 : Math.Max(200, availableSize.Width);
        return new Size(width, HeightFor(width));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _announcer.Arrange(default);
        var width = Math.Max(200, finalSize.Width);
        var height = HeightFor(width);
        if (Math.Abs(width - _w) > 0.01 || Math.Abs(height - _h) > 0.01)
        {
            var progress = _travel > 0 ? _pos / _travel : 0;
            var tail = _travel > 0 ? _tail / _travel : 0;
            _w = width;
            _h = height;
            _travel = Math.Max(0, _w - _h);
            // Keep the thumb at the same fraction of the track when the width changes.
            _pos = progress * _travel;
            _tail = tail * _travel;
            if (_mode is Mode.Spring && _committing)
            {
                _target = _travel;
            }
            UpdateVisuals();
            InvalidateText();
        }
        return new Size(width, height);
    }

    private double HeightFor(double width) => width >= TallFromWidth ? TallTrackHeight : TrackHeight;

    // --- the cycle ---

    private void BeginCommit()
    {
        CancelHint();
        _committing = true;
        _showError = false;
        _lastError = null;
        _doneClock = -1;
        _returnClock = -1;
        _spinnerAtEnd = 0;
        _pressTarget = 0;
        _glideKey = null;
        SetMode(Mode.Spring, _travel, SlideToConfirmMath.Commit);
        RaiseEvent(new RoutedEventArgs(ConfirmedEvent));
        var action = ConfirmAction;
        var version = ++_actionVersion;
        if (action is null)
        {
            EnterDone(version);
        }
        else
        {
            _statusText = PendingLabel;
            State = SlideToConfirmState.Pending;
            _announcer.Announce(PendingLabel);
            RunAction(action, version);
        }
        InvalidateText();
        Kick();
    }

    private async void RunAction(Func<Task> action, int version)
    {
        Exception? failure = null;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        if (version != _actionVersion || _state != SlideToConfirmState.Pending)
        {
            return;
        }
        if (failure is null)
        {
            EnterDone(version);
        }
        else
        {
            _lastError = failure;
            BeginReturn(failed: true);
        }
    }

    private void EnterDone(int version)
    {
        _statusText = ConfirmedLabel;
        _lastError = null;
        State = SlideToConfirmState.Done;
        _announcer.Announce(ConfirmedLabel);
        InvalidateText();
        if (ResetAfter is { } after)
        {
            _resetTimer?.Dispose();
            _resetTimer = DispatcherTimer.RunOnce(() =>
            {
                if (version == _actionVersion && _state == SlideToConfirmState.Done)
                {
                    BeginReturn(failed: false);
                }
            }, after < TimeSpan.Zero ? TimeSpan.Zero : after);
        }
        Kick();
    }

    /// <summary>Expands the track back and returns the thumb: after a success (reset) or a failure (error).</summary>
    private void BeginReturn(bool failed)
    {
        _resetTimer?.Dispose();
        _resetTimer = null;
        _returning = true;
        _returnFailed = failed;
        _returnClock = 0;
        _spinnerAtEnd = _spinnerShown;
        _committing = false;
        _retreatTarget = 0;
        _awaitThumbReturn = true;
        if (failed)
        {
            _showError = true;
            State = SlideToConfirmState.Error;
            _announcer.Announce(ErrorLabel);
        }
        else
        {
            _showError = false;
            State = SlideToConfirmState.Idle;
        }
        _actionVersion++;
        InvalidateText();
        Kick();
    }

    private void FinishReturn()
    {
        _returning = false;
        _returnClock = -1;
        _doneClock = -1;
        _spinnerAtEnd = 0;
        _spinnerShown = 0;
        _hintFirst = false;
        UpdateHintTimer();
    }

    // --- accessibility ---

    protected override AutomationPeer OnCreateAutomationPeer() => new SlideToConfirmAutomationPeer(this);

    private void RaiseValueChanged(double old, double value)
    {
        if (ControlAutomationPeer.FromElement(this) is { } peer)
        {
            peer.RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, old, value);
        }
    }

    /// <summary>The value read by assistive technology: the thumb's position in percent (whole numbers).</summary>
    internal double AutomationValue => Math.Round(Math.Clamp(_progress, 0, 1) * 100);

    /// <summary>The value text: "N percent" while idle, the pending or confirmed text otherwise.</summary>
    internal string AutomationValueText => _state switch
    {
        SlideToConfirmState.Pending => PendingLabel,
        SlideToConfirmState.Done => ConfirmedLabel,
        _ => $"{AutomationValue:0} percent"
    };

    private sealed class SlideToConfirmAutomationPeer : ControlAutomationPeer, IRangeValueProvider
    {
        public SlideToConfirmAutomationPeer(SlideToConfirm owner) : base(owner)
        {
        }

        private SlideToConfirm Slider => (SlideToConfirm)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Slider.Label;

        protected override string? GetHelpTextCore()
        {
            var help = base.GetHelpTextCore();
            var text = string.IsNullOrEmpty(help) ? DefaultHelpText : help;
            return Slider.AutomationValueText + ". " + text;
        }

        protected override bool IsEnabledCore() => base.IsEnabledCore() && !Slider.IsBusy;

        public bool IsReadOnly => !Slider.IsEffectivelyEnabled || Slider.IsBusy;

        public double Minimum => 0;

        public double Maximum => 100;

        public double Value => Slider.AutomationValue;

        public double SmallChange => 10;

        public double LargeChange => 25;

        public void SetValue(double value)
        {
            if (IsReadOnly)
            {
                return;
            }
            if (value >= 100)
            {
                Slider.Confirm();
            }
            else
            {
                Slider.MoveByKey(Math.Clamp(value / 100, 0, 1));
            }
        }
    }
}
