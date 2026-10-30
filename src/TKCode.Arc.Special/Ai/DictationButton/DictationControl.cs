using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Just the mic and its recording pill, for a composer you already have (Arc Dictation button, design reference). Pressing
/// the mic springs it leftward into a 216px pill: cancel, a live waveform, the elapsed time and Insert, which sits where the
/// mic was. Words arrive through <see cref="TranscriptChanged"/> (render them in your field, the interim part muted) and the
/// final text through <see cref="Committed"/>. A blocked microphone or a failure turns the slot into Try again.
/// <para>
/// Speech comes from <see cref="Recognizer"/>; for demos, a <see cref="Simulation"/> speaks the scripts you give it and
/// records nothing. With neither, pressing the mic says dictation is not available. Same state machine as
/// <see cref="DictationButton"/>, no field.
/// </para>
/// </summary>
[TemplatePart(PillPart, typeof(DictationPill))]
[TemplatePart(MicPart, typeof(DictationIconButton))]
[TemplatePart(RecordingPart, typeof(Control))]
[TemplatePart(CancelPart, typeof(DictationIconButton))]
[TemplatePart(WaveformPart, typeof(DictationWaveform))]
[TemplatePart(TimerPart, typeof(TextBlock))]
[TemplatePart(InsertPart, typeof(DictationIconButton))]
[TemplatePart(RetryPart, typeof(DictationIconButton))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(ListeningPseudoClass, ProcessingPseudoClass, FailedPseudoClass)]
public class DictationControl : TemplatedControl
{
    internal const string PillPart = "PART_Pill";
    internal const string MicPart = "PART_Mic";
    internal const string RecordingPart = "PART_Recording";
    internal const string CancelPart = "PART_Cancel";
    internal const string WaveformPart = "PART_Waveform";
    internal const string TimerPart = "PART_Timer";
    internal const string InsertPart = "PART_Insert";
    internal const string RetryPart = "PART_Retry";
    internal const string AnnouncerPart = "PART_Announcer";

    internal const string ListeningPseudoClass = ":listening";
    internal const string ProcessingPseudoClass = ":processing";
    internal const string FailedPseudoClass = ":failed";

    /// <summary>Lucide mic-off.</summary>
    internal static readonly Geometry MicOff = ArcIcons.Parse(
        "M2 2l20 20 M18.89 13.23A7.12 7.12 0 0 0 19 12v-2 M5 10v2a7 7 0 0 0 12 5 M15 9.34V5a3 3 0 0 0-5.68-1.33 M9 9v3a3 3 0 0 0 5.12 2.12 M12 19v3");

    /// <summary>The speech source. Null uses <see cref="Simulation"/>; with neither, starting fails as unavailable.</summary>
    public static readonly StyledProperty<IDictationRecognizer?> RecognizerProperty =
        AvaloniaProperty.Register<DictationControl, IDictationRecognizer?>(nameof(Recognizer));

    /// <summary>
    /// A simulated voice to dictate with while <see cref="Recognizer"/> is null: it never touches the microphone and speaks
    /// its scripts, and the control says nothing is recorded. Null (the default) leaves the mic unavailable. Give each
    /// control its own simulation.
    /// </summary>
    public static readonly StyledProperty<SimulatedDictationRecognizer?> SimulationProperty =
        AvaloniaProperty.Register<DictationControl, SimulatedDictationRecognizer?>(nameof(Simulation));

    /// <summary>Seconds before listening stops and inserts on its own; 0 turns the limit off.</summary>
    public static readonly StyledProperty<double> MaxDurationProperty =
        AvaloniaProperty.Register<DictationControl, double>(nameof(MaxDuration), 120);

    /// <summary>Every word the control shows or says.</summary>
    public static readonly StyledProperty<DictationButtonLabels> LabelsProperty =
        AvaloniaProperty.Register<DictationControl, DictationButtonLabels>(nameof(Labels), DictationButtonLabels.Default);

    /// <summary>Where the dictation is.</summary>
    public static readonly DirectProperty<DictationControl, DictationStatus> StatusProperty =
        AvaloniaProperty.RegisterDirect<DictationControl, DictationStatus>(nameof(Status), o => o.Status);

    /// <summary>The live transcript of the current dictation.</summary>
    public static readonly DirectProperty<DictationControl, DictationTranscript> TranscriptProperty =
        AvaloniaProperty.RegisterDirect<DictationControl, DictationTranscript>(nameof(Transcript), o => o.Transcript);

    /// <summary>Raised whenever the live transcript changes (render it in your field, the interim part muted). Bubbles.</summary>
    public static readonly RoutedEvent<DictationTranscriptEventArgs> TranscriptChangedEvent =
        RoutedEvent.Register<DictationControl, DictationTranscriptEventArgs>(nameof(TranscriptChanged), RoutingStrategies.Bubble);

    /// <summary>Raised with the final text of a dictation, to append to your field. Bubbles.</summary>
    public static readonly RoutedEvent<DictationCommittedEventArgs> CommittedEvent =
        RoutedEvent.Register<DictationControl, DictationCommittedEventArgs>(nameof(Committed), RoutingStrategies.Bubble);

    /// <summary>Raised when the person cancels; drop any live text you rendered. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CancelledEvent =
        RoutedEvent.Register<DictationControl, RoutedEventArgs>(nameof(Cancelled), RoutingStrategies.Bubble);

    /// <summary>Raised when the recognizer detects the end of an utterance. Do nothing, or confirm hands-free. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SpeechEndedEvent =
        RoutedEvent.Register<DictationControl, RoutedEventArgs>(nameof(SpeechEnded), RoutingStrategies.Bubble);

    /// <summary>Raised on every status change, with the error code for denied and error. Bubbles.</summary>
    public static readonly RoutedEvent<DictationStatusChangedEventArgs> StatusChangedEvent =
        RoutedEvent.Register<DictationControl, DictationStatusChangedEventArgs>(nameof(StatusChanged), RoutingStrategies.Bubble);

    private readonly DictationController _controller = new();
    private readonly LiveLoop _clock;
    private DictationPill? _pill;
    private DictationIconButton? _mic;
    private Control? _recording;
    private DictationIconButton? _cancel;
    private DictationWaveform? _waveform;
    private TextBlock? _timer;
    private DictationIconButton? _insert;
    private DictationIconButton? _retry;
    private ExplainerAnnouncer? _announcer;
    private DictationStatus _status;
    private DictationTranscript _transcript = DictationTranscript.Empty;
    private long _shownSeconds = -1;
    private bool _focusInsertWhenShown;
    private bool _focusAtStop;

    static DictationControl()
    {
        SpecialFocusModality.Watch();
        FocusableProperty.OverrideDefaultValue<DictationControl>(false);
    }

    /// <summary>Creates the control.</summary>
    public DictationControl()
    {
        _controller.StatusChanged += OnStatusChanged;
        _controller.TranscriptChanged += OnTranscriptChanged;
        _controller.Committed += OnCommitted;
        _controller.Cancelled += OnCancelled;
        _controller.SpeechEnded += OnSpeechEnded;
        _clock = new LiveLoop(this, _ =>
        {
            UpdateTimer();
            return _controller.Status == DictationStatus.Listening;
        })
        { MinimumLevel = ArcMotionLevel.Off };
        ApplyRecognizer();
    }

    /// <inheritdoc cref="RecognizerProperty"/>
    public IDictationRecognizer? Recognizer { get => GetValue(RecognizerProperty); set => SetValue(RecognizerProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public SimulatedDictationRecognizer? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="MaxDurationProperty"/>
    public double MaxDuration { get => GetValue(MaxDurationProperty); set => SetValue(MaxDurationProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public DictationButtonLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public DictationStatus Status => _status;

    /// <inheritdoc cref="TranscriptProperty"/>
    public DictationTranscript Transcript => _transcript;

    /// <inheritdoc cref="TranscriptChangedEvent"/>
    public event EventHandler<DictationTranscriptEventArgs>? TranscriptChanged
    {
        add => AddHandler(TranscriptChangedEvent, value);
        remove => RemoveHandler(TranscriptChangedEvent, value);
    }

    /// <inheritdoc cref="CommittedEvent"/>
    public event EventHandler<DictationCommittedEventArgs>? Committed
    {
        add => AddHandler(CommittedEvent, value);
        remove => RemoveHandler(CommittedEvent, value);
    }

    /// <inheritdoc cref="CancelledEvent"/>
    public event EventHandler<RoutedEventArgs>? Cancelled
    {
        add => AddHandler(CancelledEvent, value);
        remove => RemoveHandler(CancelledEvent, value);
    }

    /// <inheritdoc cref="SpeechEndedEvent"/>
    public event EventHandler<RoutedEventArgs>? SpeechEnded
    {
        add => AddHandler(SpeechEndedEvent, value);
        remove => RemoveHandler(SpeechEndedEvent, value);
    }

    /// <inheritdoc cref="StatusChangedEvent"/>
    public event EventHandler<DictationStatusChangedEventArgs>? StatusChanged
    {
        add => AddHandler(StatusChangedEvent, value);
        remove => RemoveHandler(StatusChangedEvent, value);
    }

    /// <summary>The state machine (tests and the composer read it).</summary>
    internal DictationController Controller => _controller;

    /// <summary>The error of a denied or failed dictation, otherwise null.</summary>
    internal DictationErrorCode? Error => _controller.Error;

    internal DictationPill? Pill => _pill;
    internal DictationIconButton? MicButton => _mic;
    internal DictationIconButton? CancelButton => _cancel;
    internal DictationIconButton? InsertButton => _insert;
    internal DictationIconButton? RetryButton => _retry;
    internal DictationWaveform? Waveform => _waveform;
    internal TextBlock? TimerText => _timer;
    internal ExplainerAnnouncer? Announcer => _announcer;

    /// <summary>True when the focus is on one of the control's buttons.</summary>
    internal bool HasFocusWithin
    {
        get
        {
            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
            return focused is not null && (focused == this || this.IsVisualAncestorOf(focused));
        }
    }

    /// <summary>Starts dictating (from idle, or retrying after an error).</summary>
    public void Start() => _ = _controller.StartAsync();

    /// <summary>Stops listening and inserts the transcript.</summary>
    public void Confirm() => _ = _controller.ConfirmAsync();

    /// <summary>Cancels the dictation and discards its words.</summary>
    public void Cancel() => _controller.Cancel();

    /// <summary>Dismisses a denied or failed state.</summary>
    public void Dismiss() => _controller.Dismiss();

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _pill = e.NameScope.Find<DictationPill>(PillPart);
        _mic = e.NameScope.Find<DictationIconButton>(MicPart);
        _recording = e.NameScope.Find<Control>(RecordingPart);
        _cancel = e.NameScope.Find<DictationIconButton>(CancelPart);
        _waveform = e.NameScope.Find<DictationWaveform>(WaveformPart);
        _timer = e.NameScope.Find<TextBlock>(TimerPart);
        _insert = e.NameScope.Find<DictationIconButton>(InsertPart);
        _retry = e.NameScope.Find<DictationIconButton>(RetryPart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        if (_mic is not null)
        {
            _mic.Click += OnMic;
        }
        if (_cancel is not null)
        {
            _cancel.Click += OnCancelClick;
        }
        if (_insert is not null)
        {
            _insert.Click += OnInsert;
        }
        if (_retry is not null)
        {
            _retry.Click += OnMic;
        }
        if (_waveform is not null)
        {
            _waveform.LevelSource = () => _controller.Level;
        }
        _shownSeconds = -1;
        Refresh(instant: true);
    }

    private void Unhook()
    {
        if (_mic is not null)
        {
            _mic.Click -= OnMic;
        }
        if (_cancel is not null)
        {
            _cancel.Click -= OnCancelClick;
        }
        if (_insert is not null)
        {
            _insert.Click -= OnInsert;
        }
        if (_retry is not null)
        {
            _retry.Click -= OnMic;
        }
        if (_waveform is not null)
        {
            _waveform.LevelSource = null;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RecognizerProperty || change.Property == SimulationProperty)
        {
            ApplyRecognizer();
        }
        else if (change.Property == MaxDurationProperty)
        {
            _controller.MaxDuration = MaxDuration > 0 ? TimeSpan.FromSeconds(MaxDuration) : TimeSpan.Zero;
        }
        else if (change.Property == LabelsProperty)
        {
            Refresh(instant: false);
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Leaving the screen releases the microphone.
        _controller.Cancel();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled)
        {
            e.Handled = HandleKey(e.Key, e.KeyModifiers);
        }
    }

    /// <summary>Enter inserts and Escape cancels while dictating; Escape dismisses Try again. Returns whether it acted.</summary>
    internal bool HandleKey(Key key, KeyModifiers modifiers)
    {
        if (modifiers != KeyModifiers.None)
        {
            return false;
        }
        var status = _controller.Status;
        if (key == Key.Escape)
        {
            if (_controller.IsActive)
            {
                var hadFocus = HasFocusWithin;
                _controller.Cancel();
                if (hadFocus)
                {
                    FocusMic();
                }
                return true;
            }
            if (status is DictationStatus.Denied or DictationStatus.Error)
            {
                var hadFocus = HasFocusWithin;
                _controller.Dismiss();
                if (hadFocus)
                {
                    FocusMic();
                }
                return true;
            }
            return false;
        }
        if (key == Key.Enter && status == DictationStatus.Listening)
        {
            Confirm();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Hands the controller the app's recognizer, else the simulation, else nothing (starting then fails as unavailable). A
    /// dictation that is running on a source being replaced is cancelled: its words belong to the old one.
    /// </summary>
    private void ApplyRecognizer()
    {
        IDictationRecognizer? source = Recognizer ?? Simulation;
        if (ReferenceEquals(source, _controller.Recognizer))
        {
            return;
        }
        if (_controller.IsActive)
        {
            _controller.Cancel();
        }
        _controller.Recognizer = source;
    }

    private void OnMic(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _focusInsertWhenShown = HasFocusWithin || (sender as InputElement)?.IsFocused == true;
        Start();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var hadFocus = HasFocusWithin;
        Cancel();
        if (hadFocus)
        {
            FocusMic();
        }
    }

    private void OnInsert(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Confirm();
    }

    private void OnStatusChanged(object? sender, DictationStatusChangedEventArgs e)
    {
        var old = _status;
        SetAndRaise(StatusProperty, ref _status, e.Status);
        PseudoClasses.Set(ListeningPseudoClass, e.Status is DictationStatus.Starting or DictationStatus.Listening or DictationStatus.Processing);
        PseudoClasses.Set(ProcessingPseudoClass, e.Status == DictationStatus.Processing);
        PseudoClasses.Set(FailedPseudoClass, e.Status is DictationStatus.Denied or DictationStatus.Error);
        // Read before the faces change: hiding the focused face moves the focus away.
        var hadFocus = HasFocusWithin;
        _focusAtStop = hadFocus;
        Refresh(instant: false);
        if (e.Status == DictationStatus.Listening)
        {
            _shownSeconds = -1;
            UpdateTimer();
            _clock.Start();
            Announce(_controller.IsSimulated ? Labels.SimulatedAnnouncement : Labels.ListeningAnnouncement);
        }
        else
        {
            _clock.Stop();
            UpdateTimer();
        }
        if (e.Status == DictationStatus.Processing)
        {
            Announce(Labels.TranscribingAnnouncement);
        }
        if (e.Status is DictationStatus.Starting && _focusInsertWhenShown)
        {
            FocusLater(() => _insert);
        }
        if (e.Status is DictationStatus.Denied or DictationStatus.Error && e.Error is { } code)
        {
            Announce(Labels.ErrorAnnouncement(code));
            if (hadFocus || _focusInsertWhenShown)
            {
                FocusLater(() => _retry);
            }
        }
        if (e.Status == DictationStatus.Idle && old is not DictationStatus.Idle)
        {
            _focusInsertWhenShown = false;
        }
        RaiseEvent(new DictationStatusChangedEventArgs(StatusChangedEvent, e.Status, e.Error));
    }

    private void OnTranscriptChanged(object? sender, DictationTranscriptEventArgs e)
    {
        SetAndRaise(TranscriptProperty, ref _transcript, e.Transcript);
        RaiseEvent(new DictationTranscriptEventArgs(TranscriptChangedEvent, e.Transcript));
    }

    private void OnCommitted(object? sender, DictationCommittedEventArgs e)
    {
        Announce(Labels.InsertedAnnouncement(e.WordCount));
        RaiseEvent(new DictationCommittedEventArgs(CommittedEvent, e.Text));
        // A composer moves the focus to its field; on its own the control gives it back to the mic.
        if (_focusAtStop && !FocusMovedElsewhere())
        {
            FocusMic();
        }
    }

    /// <summary>True when the focus was on one of the control's buttons when the dictation last stopped.</summary>
    internal bool HadFocusAtStop => _focusAtStop;

    private bool FocusMovedElsewhere()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        return focused is not null && focused != this && !this.IsVisualAncestorOf(focused);
    }

    private void OnCancelled(object? sender, EventArgs e)
    {
        Announce(Labels.CancelledAnnouncement);
        RaiseEvent(new RoutedEventArgs(CancelledEvent));
    }

    private void OnSpeechEnded(object? sender, EventArgs e) => RaiseEvent(new RoutedEventArgs(SpeechEndedEvent));

    private void Announce(string text) => _announcer?.Announce(text);

    private void FocusMic() => FocusLater(() => _mic);

    /// <summary>Focuses a part once it is on screen (a face that just became active is made visible in the same pass).</summary>
    private void FocusLater(Func<Control?> target)
    {
        // With the keyboard ring only when a key led here (a click on the mic does not light it up).
        var method = SpecialFocusModality.For(this);
        target()?.Focus(method);
        Dispatcher.UIThread.Post(() =>
        {
            var control = target();
            if (control is not null && !control.IsFocused && control.IsEffectivelyVisible)
            {
                control.Focus(method);
            }
        }, DispatcherPriority.Loaded);
    }

    private void UpdateTimer()
    {
        if (_timer is null)
        {
            return;
        }
        var elapsed = _controller.Elapsed;
        var seconds = (long)Math.Floor(elapsed.TotalSeconds);
        if (seconds != _shownSeconds)
        {
            _shownSeconds = seconds;
            _timer.Text = Labels.Time(TimeSpan.FromSeconds(seconds));
        }
    }

    /// <summary>Brings every part in line with the status.</summary>
    private void Refresh(bool instant)
    {
        var labels = Labels;
        var status = _controller.Status;
        if (_mic is not null)
        {
            _mic.Icon = ArcIcons.Mic;
            AutomationProperties.SetName(_mic, labels.Dictate);
            Tooltip.SetTip(_mic, labels.Dictate);
        }
        if (_cancel is not null)
        {
            AutomationProperties.SetName(_cancel, labels.Cancel);
            Tooltip.SetTip(_cancel, labels.Cancel);
        }
        if (_insert is not null)
        {
            var processing = status == DictationStatus.Processing;
            _insert.IsBusy = processing;
            var name = processing ? labels.Transcribing : labels.Insert;
            AutomationProperties.SetName(_insert, name);
            Tooltip.SetTip(_insert, name);
        }
        if (_timer is not null)
        {
            AutomationProperties.SetName(_timer, labels.Elapsed);
        }
        if (_retry is not null)
        {
            _retry.Text = labels.TryAgain;
            _retry.Icon = _controller.Error == DictationErrorCode.NotAllowed ? MicOff : ArcIcons.CircleAlert;
            AutomationProperties.SetName(_retry, labels.TryAgain);
            if (_controller.Error is { } code)
            {
                AutomationProperties.SetHelpText(_retry, labels.Error(code));
            }
        }
        if (_waveform is not null)
        {
            _waveform.IsLive = status == DictationStatus.Listening;
        }
        if (_pill is null)
        {
            return;
        }
        switch (status)
        {
            case DictationStatus.Starting or DictationStatus.Listening or DictationStatus.Processing:
                if (_recording is not null)
                {
                    _pill.Show(_recording, filled: true, instant);
                }
                break;
            case DictationStatus.Denied or DictationStatus.Error:
                if (_retry is not null)
                {
                    _pill.Show(_retry, filled: true, instant);
                }
                break;
            default:
                if (_mic is not null)
                {
                    _pill.Show(_mic, filled: false, instant);
                }
                break;
        }
    }
}
