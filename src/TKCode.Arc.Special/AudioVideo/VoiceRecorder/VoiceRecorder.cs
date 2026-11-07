using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A voice note composer with its thread (Arc Voice recorder, design reference). The thread lists voice bubbles (theirs on
/// the left with an avatar, yours on the right), each with play and pause and a waveform that fills in as it plays. Below
/// it, the mic stretches into a recording bar with a breathing dot, a running timer and a live waveform; stop turns the bar
/// into a review where the take plays back and can be scrubbed; send carries the bar up into the thread as your new bubble,
/// and the trash (or Escape) folds it back into the mic.
/// <para>
/// Recording is simulated: every take gets a deterministic voice (see <see cref="VoiceRecorderPeaks"/>) unless
/// <see cref="LevelSource"/> supplies the levels. <see cref="MessageSent"/> and <see cref="Cancelled"/> carry the take (its
/// length and bars); wire your own audio capture there. Sent takes are added to <see cref="Messages"/>.
/// </para>
/// </summary>
[TemplatePart(HeaderHostPart, typeof(Control))]
[TemplatePart(ListPart, typeof(ScrollViewer))]
[TemplatePart(MessagesPart, typeof(StackPanel))]
[TemplatePart(ComposerHostPart, typeof(Decorator))]
[TemplatePart(StatusPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(PlainPseudoClass, RecordingPseudoClass, ReviewPseudoClass)]
public class VoiceRecorder : TemplatedControl
{
    internal const string HeaderHostPart = "PART_HeaderHost";
    internal const string ListPart = "PART_List";
    internal const string MessagesPart = "PART_Messages";
    internal const string ComposerHostPart = "PART_ComposerHost";
    internal const string StatusPart = "PART_Status";

    internal const string PlainPseudoClass = ":plain";
    internal const string RecordingPseudoClass = ":recording";
    internal const string ReviewPseudoClass = ":review";

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    /// <summary>The thread, oldest first. Sent takes are appended to it (a read-only list is replaced by a copy with the take).</summary>
    public static readonly StyledProperty<IList<VoiceRecorderMessage>?> MessagesProperty =
        AvaloniaProperty.Register<VoiceRecorder, IList<VoiceRecorderMessage>?>(nameof(Messages));

    /// <summary>The longest take: recording stops by itself and opens the review when it is reached.</summary>
    public static readonly StyledProperty<TimeSpan> MaxDurationProperty =
        AvaloniaProperty.Register<VoiceRecorder, TimeSpan>(nameof(MaxDuration), TimeSpan.FromSeconds(60),
            coerce: static (_, value) => value < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : value);

    /// <summary>The meta line under a bubble you send.</summary>
    public static readonly StyledProperty<string?> SentMetaProperty =
        AvaloniaProperty.Register<VoiceRecorder, string?>(nameof(SentMeta), "Just now");

    /// <summary>The accessible name of the whole recorder, such as "Voice messages with Ada".</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<VoiceRecorder, string>(nameof(Label), "Voice messages");

    /// <summary>Optional content above the thread, such as the other person's avatar, name and a reset button; it sits over a hairline.</summary>
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<VoiceRecorder, object?>(nameof(Header));

    /// <summary>The template for <see cref="Header"/>.</summary>
    public static readonly StyledProperty<IDataTemplate?> HeaderTemplateProperty =
        AvaloniaProperty.Register<VoiceRecorder, IDataTemplate?>(nameof(HeaderTemplate));

    /// <summary>A bordered card (the default) or plain, to sit inside a page.</summary>
    public static readonly StyledProperty<VoiceRecorderVariant> VariantProperty =
        AvaloniaProperty.Register<VoiceRecorder, VoiceRecorderVariant>(nameof(Variant));

    /// <summary>Supplies the level of every 90ms sample while recording. Null: a simulated voice, the same for the same take number.</summary>
    public static readonly StyledProperty<VoiceRecorderLevelSource?> LevelSourceProperty =
        AvaloniaProperty.Register<VoiceRecorder, VoiceRecorderLevelSource?>(nameof(LevelSource));

    /// <summary>Every word the recorder shows or says.</summary>
    public static readonly StyledProperty<VoiceRecorderLabels> LabelsProperty =
        AvaloniaProperty.Register<VoiceRecorder, VoiceRecorderLabels>(nameof(Labels), VoiceRecorderLabels.Default);

    /// <summary>Where the composer is: idle, recording or reviewing.</summary>
    public static readonly DirectProperty<VoiceRecorder, VoiceRecorderPhase> PhaseProperty =
        AvaloniaProperty.RegisterDirect<VoiceRecorder, VoiceRecorderPhase>(nameof(Phase), o => o.Phase);

    /// <summary>The live bar's tint (from the theme).</summary>
    internal static readonly StyledProperty<IBrush?> RecordingBackgroundProperty =
        AvaloniaProperty.Register<VoiceRecorder, IBrush?>(nameof(RecordingBackground));

    /// <summary>The review bar's tint (from the theme).</summary>
    internal static readonly StyledProperty<IBrush?> ReviewBackgroundProperty =
        AvaloniaProperty.Register<VoiceRecorder, IBrush?>(nameof(ReviewBackground));

    /// <summary>The record dot's color (from the theme).</summary>
    internal static readonly StyledProperty<IBrush?> RecordInkProperty =
        AvaloniaProperty.Register<VoiceRecorder, IBrush?>(nameof(RecordInk));

    /// <summary>A take was sent; it is already in the thread. Bubbles.</summary>
    public static readonly RoutedEvent<VoiceRecorderMessageEventArgs> MessageSentEvent =
        RoutedEvent.Register<VoiceRecorder, VoiceRecorderMessageEventArgs>(nameof(MessageSent), RoutingStrategies.Bubble);

    /// <summary>A take was discarded with the trash button, Escape or <see cref="Discard"/>. Bubbles.</summary>
    public static readonly RoutedEvent<VoiceRecorderMessageEventArgs> CancelledEvent =
        RoutedEvent.Register<VoiceRecorder, VoiceRecorderMessageEventArgs>(nameof(Cancelled), RoutingStrategies.Bubble);

    private readonly List<VoiceRecorderBubble> _bubbles = [];
    private readonly LiveLoop _loop;

    // Composer parts (built once; the theme styles them by type and pseudo-class).
    private readonly TextBlock _hint;
    private readonly VoiceRecorderButton _discard;
    private readonly VoiceRecorderButton _stop;
    private readonly VoiceRecorderButton _send;
    private readonly VoiceRecorderButton _mic;
    private readonly VoiceRecorderButton _reviewPlay;
    private readonly Border _dot;
    private readonly TextBlock _recordTimer;
    private readonly VoiceRecorderLiveWave _liveWave;
    private readonly VoiceRecorderBars _reviewBars;
    private readonly TextBlock _reviewTimer;
    private readonly Grid _recordingRow;
    private readonly Grid _reviewRow;
    private readonly VoiceRecorderCapsule _capsule;
    private readonly VoiceRecorderComposer _composer;
    private readonly VoiceRecorderPresence _discardPresence;
    private readonly VoiceRecorderPresence _stopPresence;
    private readonly VoiceRecorderPresence _sendPresence;
    private readonly VoiceRecorderPresence _micPresence;
    private readonly VoiceRecorderBlurFade _hintFade;
    private readonly VoiceRecorderBlurFade _recordingFade;
    private readonly VoiceRecorderBlurFade _reviewFade;

    private VoiceRecorderPhase _phase;
    private Control? _headerHost;
    private ScrollViewer? _list;
    private StackPanel? _messagesPanel;
    private Decorator? _composerHost;
    private ExplainerAnnouncer? _status;
    private INotifyCollectionChanged? _observed;

    // The take.
    private int _take;
    private double[] _samples = [];
    private int _sampleCount;
    private double _time;
    private double[] _simulated = [];
    private long _shownTenths = -1;
    private long _shownSecond = -1;

    // The review.
    private double _reviewPosition;
    private bool _reviewPlaying;
    private long _reviewShownSecond = -1;

    // Motion.
    private MotionValue<Rect>? _morph;
    private bool _morphIsMic;
    private int _morphGeneration;
    private MotionValue<double>? _scroll;
    private int _sentCount;

    static VoiceRecorder()
    {
        FocusableProperty.OverrideDefaultValue<VoiceRecorder>(false);
    }

    /// <summary>Creates the recorder with an empty thread.</summary>
    public VoiceRecorder()
    {
        SetCurrentValue(MessagesProperty, new AvaloniaList<VoiceRecorderMessage>());
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Off, TracksViewport = false };

        _hint = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(_hint, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_hint, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);

        _discard = new VoiceRecorderButton(VoiceRecorderButtonKind.Discard);
        _stop = new VoiceRecorderButton(VoiceRecorderButtonKind.Stop);
        _send = new VoiceRecorderButton(VoiceRecorderButtonKind.Send);
        _mic = new VoiceRecorderButton(VoiceRecorderButtonKind.Mic);
        _reviewPlay = new VoiceRecorderButton(VoiceRecorderButtonKind.Play) { VerticalAlignment = VerticalAlignment.Center };

        _dot = new Border { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        _recordTimer = new TextBlock { FontFeatures = s_tabular, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(_recordTimer, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_recordTimer, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        _liveWave = new VoiceRecorderLiveWave { Height = 28, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(_liveWave, VoiceRecorderLiveWave.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _recordingRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"),
            ColumnSpacing = 10,
            Margin = new Thickness(16, 0, 14, 0),
        };
        Grid.SetColumn(_recordTimer, 1);
        Grid.SetColumn(_liveWave, 2);
        _recordingRow.Children.Add(_dot);
        _recordingRow.Children.Add(_recordTimer);
        _recordingRow.Children.Add(_liveWave);

        _reviewBars = new VoiceRecorderBars { FlexibleCount = true, Height = 24, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(_reviewBars, VoiceRecorderBars.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _reviewTimer = new TextBlock { FontFeatures = s_tabular, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(_reviewTimer, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_reviewTimer, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        _reviewRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 10,
            Margin = new Thickness(8, 0, 14, 0),
        };
        Grid.SetColumn(_reviewBars, 1);
        Grid.SetColumn(_reviewTimer, 2);
        _reviewRow.Children.Add(_reviewPlay);
        _reviewRow.Children.Add(_reviewBars);
        _reviewRow.Children.Add(_reviewTimer);
        AutomationProperties.SetAccessibilityView(_recordTimer, Avalonia.Automation.AccessibilityView.Raw);
        AutomationProperties.SetAccessibilityView(_reviewTimer, Avalonia.Automation.AccessibilityView.Raw);
        AutomationProperties.SetAccessibilityView(_dot, Avalonia.Automation.AccessibilityView.Raw);

        _capsule = new VoiceRecorderCapsule(_recordingRow, _reviewRow) { IsVisible = false };
        _composer = new VoiceRecorderComposer(_hint, _discard, _capsule, _stop, _send, _mic);

        _discardPresence = new VoiceRecorderPresence(_discard, VoiceRecorderMotion.SideFromScale, VoiceRecorderMotion.PopIn, VoiceRecorderMotion.ReducedPopIn);
        _stopPresence = new VoiceRecorderPresence(_stop, VoiceRecorderMotion.SideFromScale, VoiceRecorderMotion.PopIn, VoiceRecorderMotion.ReducedPopIn);
        _sendPresence = new VoiceRecorderPresence(_send, VoiceRecorderMotion.SideFromScale, VoiceRecorderMotion.PopIn, VoiceRecorderMotion.ReducedPopIn);
        _micPresence = new VoiceRecorderPresence(_mic, VoiceRecorderMotion.MicFromScale, VoiceRecorderMotion.MicReturn, VoiceRecorderMotion.ReducedMicReturn);
        _hintFade = new VoiceRecorderBlurFade(_hint);
        _recordingFade = new VoiceRecorderBlurFade(_recordingRow);
        _reviewFade = new VoiceRecorderBlurFade(_reviewRow);
        _discardPresence.Set(false);
        _stopPresence.Set(false);
        _sendPresence.Set(false);
        _micPresence.Set(true);

        _mic.Click += (_, e) => { e.Handled = true; Act(_mic, StartRecording); };
        _stop.Click += (_, e) => { e.Handled = true; Act(_stop, StopRecording); };
        _send.Click += (_, e) => { e.Handled = true; Act(_send, Send); };
        _discard.Click += (_, e) => { e.Handled = true; Act(_discard, Discard); };
        _reviewPlay.Click += (_, e) => { e.Handled = true; ToggleReview(); };
        _reviewBars.SeekRequested += SeekReview;
        _composer.SizeChanged += (_, _) => RetargetMorph();

        AutomationProperties.SetName(this, Label);
        ApplyLabels();
    }

    /// <inheritdoc cref="MessagesProperty"/>
    public IList<VoiceRecorderMessage>? Messages { get => GetValue(MessagesProperty); set => SetValue(MessagesProperty, value); }

    /// <inheritdoc cref="MaxDurationProperty"/>
    public TimeSpan MaxDuration { get => GetValue(MaxDurationProperty); set => SetValue(MaxDurationProperty, value); }

    /// <inheritdoc cref="SentMetaProperty"/>
    public string? SentMeta { get => GetValue(SentMetaProperty); set => SetValue(SentMetaProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="HeaderProperty"/>
    public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <inheritdoc cref="HeaderTemplateProperty"/>
    public IDataTemplate? HeaderTemplate { get => GetValue(HeaderTemplateProperty); set => SetValue(HeaderTemplateProperty, value); }

    /// <inheritdoc cref="VariantProperty"/>
    public VoiceRecorderVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <inheritdoc cref="LevelSourceProperty"/>
    public VoiceRecorderLevelSource? LevelSource { get => GetValue(LevelSourceProperty); set => SetValue(LevelSourceProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public VoiceRecorderLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="PhaseProperty"/>
    public VoiceRecorderPhase Phase { get => _phase; private set => SetAndRaise(PhaseProperty, ref _phase, value); }

    /// <summary>The length of the take so far (while recording) or of the take under review; zero at rest.</summary>
    public TimeSpan Elapsed => _phase == VoiceRecorderPhase.Idle ? TimeSpan.Zero : TimeSpan.FromSeconds(_time);

    internal IBrush? RecordingBackground { get => GetValue(RecordingBackgroundProperty); set => SetValue(RecordingBackgroundProperty, value); }

    internal IBrush? ReviewBackground { get => GetValue(ReviewBackgroundProperty); set => SetValue(ReviewBackgroundProperty, value); }

    internal IBrush? RecordInk { get => GetValue(RecordInkProperty); set => SetValue(RecordInkProperty, value); }

    /// <inheritdoc cref="MessageSentEvent"/>
    public event EventHandler<VoiceRecorderMessageEventArgs>? MessageSent { add => AddHandler(MessageSentEvent, value); remove => RemoveHandler(MessageSentEvent, value); }

    /// <inheritdoc cref="CancelledEvent"/>
    public event EventHandler<VoiceRecorderMessageEventArgs>? Cancelled { add => AddHandler(CancelledEvent, value); remove => RemoveHandler(CancelledEvent, value); }

    // ---- Internals for tests and the snapshot tool. ----

    internal VoiceRecorderComposer Composer => _composer;

    internal IReadOnlyList<VoiceRecorderBubble> Bubbles => _bubbles;

    internal ScrollViewer? List => _list;

    internal ExplainerAnnouncer? Status => _status;

    internal VoiceRecorderBars ReviewBars => _reviewBars;

    internal VoiceRecorderButton ReviewPlay => _reviewPlay;

    internal TextBlock RecordTimer => _recordTimer;

    internal TextBlock ReviewTimer => _reviewTimer;

    internal VoiceRecorderLiveWave LiveWave => _liveWave;

    internal Border RecordDot => _dot;

    internal VoiceRecorderBlurFade HintFade => _hintFade;

    internal VoiceRecorderPresence MicPresence => _micPresence;

    internal VoiceRecorderPresence StopPresence => _stopPresence;

    internal VoiceRecorderPresence SendPresence => _sendPresence;

    internal VoiceRecorderPresence DiscardPresence => _discardPresence;

    internal bool IsReviewPlaying => _reviewPlaying;

    internal double ReviewPosition => _reviewPosition;

    internal int TakeNumber => _take;

    internal int SampleCount => _sampleCount;

    internal MotionValue<Rect>? Morph => _morph;

    internal MotionValue<double>? ScrollMotion => _scroll;

    // ---- Public commands. ----

    /// <summary>Starts a take (like the mic button). Nothing happens unless the composer is idle.</summary>
    public void StartRecording()
    {
        if (_phase != VoiceRecorderPhase.Idle)
        {
            return;
        }
        PauseAll();
        _take = (Messages?.Count ?? 0) + 1;
        var max = MaxDuration.TotalSeconds;
        var capacity = VoiceRecorderPeaks.SampleCount(max) + 2;
        if (_samples.Length < capacity)
        {
            _samples = new double[capacity];
        }
        _simulated = LevelSource is null ? VoiceRecorderPeaks.Levels(_take, max + 0.2) : [];
        _sampleCount = 0;
        _time = 0;
        _shownTenths = -1;
        _shownSecond = -1;
        _liveWave.Smooth = ArcMotion.IsFull(this);
        _liveWave.Reset();
        _recordTimer.Text = VoiceRecorderClock.FormatTenths(0);
        _dot.Opacity = 1;
        _reviewPosition = 0;
        _reviewBars.Progress = 0;

        Phase = VoiceRecorderPhase.Recording;
        _capsule.Background = RecordingBackground;
        _capsule.IsVisible = true;
        _recordingFade.Set(true);
        _reviewFade.Set(false);
        _hintFade.Hide();
        _discardPresence.Show();
        _stopPresence.Show();
        _sendPresence.Set(false);
        // The mic becomes the bar: hand the morph from the mic's box (wherever it is now) to the bar.
        var from = _composer.MicFrame ?? _composer.MicRect(_composer.Bounds.Width);
        _micPresence.Set(false);
        _composer.MicFrame = null;
        StartMorph(from, toMic: false);
        UpdateStopName(0);
        Announce(Labels.AnnounceRecording);
        _loop.Start();
    }

    /// <summary>Stops the take and opens the review (like the stop button). Nothing happens unless recording.</summary>
    public void StopRecording()
    {
        if (_phase != VoiceRecorderPhase.Recording)
        {
            return;
        }
        _time = Math.Min(_time, MaxDuration.TotalSeconds);
        FillSamples();
        _reviewBars.Peaks = VoiceRecorderPeaks.Resample(new ArraySegment<double>(_samples, 0, _sampleCount), VoiceRecorderPeaks.ReviewBarCount);
        _reviewBars.Progress = 0;
        _reviewPosition = 0;
        _reviewPlaying = false;
        _reviewShownSecond = -1;
        _reviewPlay.IsPlaying = false;
        _reviewTimer.Text = VoiceRecorderClock.Format(_time);
        Phase = VoiceRecorderPhase.Review;
        _capsule.Background = ReviewBackground;
        _recordingFade.Hide();
        _reviewFade.Show();
        _stopPresence.Hide();
        _sendPresence.Show();
        ApplyReviewName();
        Announce(Labels.AnnounceRecorded(VoiceRecorderClock.Format(_time)));
        _liveWave.Update(_samples, _sampleCount, _time);
    }

    /// <summary>
    /// Sends the take (like the send button): it is appended to <see cref="Messages"/> and flies into the thread, then
    /// <see cref="MessageSent"/> is raised. While recording it stops first. Nothing happens at rest.
    /// </summary>
    public void Send()
    {
        if (_phase == VoiceRecorderPhase.Recording)
        {
            StopRecording();
        }
        if (_phase != VoiceRecorderPhase.Review)
        {
            return;
        }
        var message = TakeAsMessage(SentMeta);
        _reviewPlaying = false;
        _reviewPlay.IsPlaying = false;
        // Where the bar is now, in the list's frame, so the new bubble can start there.
        Point? origin = null;
        var capsuleSize = _capsule.Bounds.Size;
        if (_list is not null && _capsule.IsEffectivelyVisible && _capsule.TranslatePoint(default, _list) is { } p)
        {
            origin = p;
        }
        Phase = VoiceRecorderPhase.Idle;
        StopMorph();
        _capsule.IsVisible = false;
        _discardPresence.Hide();
        _sendPresence.Hide();
        _stopPresence.Set(false);
        _micPresence.Set(false);
        _micPresence.Show();
        _hintFade.Show();
        AppendMessage(message);
        var bubble = _bubbles.Count > 0 ? _bubbles[^1] : null;
        FlyIn(bubble, origin, capsuleSize);
        Announce(Labels.AnnounceSent);
        RaiseEvent(new VoiceRecorderMessageEventArgs(MessageSentEvent, message));
    }

    /// <summary>Discards the take (like the trash button or Escape) and raises <see cref="Cancelled"/>. Nothing happens at rest.</summary>
    public void Discard()
    {
        if (_phase == VoiceRecorderPhase.Idle)
        {
            return;
        }
        if (_phase == VoiceRecorderPhase.Recording)
        {
            _time = Math.Min(_time, MaxDuration.TotalSeconds);
            FillSamples();
        }
        var message = TakeAsMessage(null);
        _reviewPlaying = false;
        _reviewPlay.IsPlaying = false;
        var from = _composer.CapsuleFrame ?? _composer.CapsuleRect(_composer.Bounds.Width);
        Phase = VoiceRecorderPhase.Idle;
        _capsule.IsVisible = false;
        _composer.CapsuleFrame = null;
        _discardPresence.Hide();
        _stopPresence.Hide();
        _sendPresence.Hide();
        _micPresence.Set(true);
        _hintFade.Show();
        // The bar folds back into the mic, keeping its speed if it was still stretching out.
        StartMorph(from, toMic: true);
        Announce(Labels.AnnounceDiscarded);
        RaiseEvent(new VoiceRecorderMessageEventArgs(CancelledEvent, message));
    }

    /// <summary>Replaces the thread with <paramref name="messages"/> (stopping any playback) and shows its end.</summary>
    public void ResetMessages(IEnumerable<VoiceRecorderMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        PauseAll();
        var list = new AvaloniaList<VoiceRecorderMessage>(messages);
        SetCurrentValue(MessagesProperty, list);
        ScrollToEnd(animate: false);
    }

    /// <summary>Plays the message with <paramref name="id"/> from where it stopped (from the start once it has finished); any other playback pauses.</summary>
    public void PlayMessage(string id)
    {
        if (Find(id) is { } bubble && !bubble.IsPlaying)
        {
            ToggleBubble(bubble);
        }
    }

    /// <summary>Pauses the message with <paramref name="id"/>.</summary>
    public void PauseMessage(string id)
    {
        if (Find(id) is { } bubble && bubble.IsPlaying)
        {
            ToggleBubble(bubble);
        }
    }

    // ---- Template and properties. ----

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_composerHost is not null)
        {
            _composerHost.Child = null;
        }
        _messagesPanel?.Children.Clear();
        _headerHost = e.NameScope.Find<Control>(HeaderHostPart);
        _list = e.NameScope.Find<ScrollViewer>(ListPart);
        _messagesPanel = e.NameScope.Find<StackPanel>(MessagesPart);
        _composerHost = e.NameScope.Find<Decorator>(ComposerHostPart);
        _status = e.NameScope.Find<ExplainerAnnouncer>(StatusPart);
        if (_composerHost is not null)
        {
            _composerHost.Child = _composer;
        }
        if (_list is not null)
        {
            AutomationProperties.SetName(_list, Labels.MessagesList);
        }
        PseudoClasses.Set(PlainPseudoClass, Variant == VoiceRecorderVariant.Plain);
        RebuildRows();
        ApplyInks();
        Dispatcher.UIThread.Post(() => ScrollToEnd(animate: false), DispatcherPriority.Loaded);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MessagesProperty)
        {
            Observe(change.GetNewValue<IList<VoiceRecorderMessage>?>());
            RebuildRows();
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        else if (change.Property == LabelsProperty)
        {
            ApplyLabels();
        }
        else if (change.Property == VariantProperty)
        {
            PseudoClasses.Set(PlainPseudoClass, Variant == VoiceRecorderVariant.Plain);
        }
        else if (change.Property == RecordingBackgroundProperty || change.Property == ReviewBackgroundProperty
                 || change.Property == RecordInkProperty)
        {
            ApplyInks();
        }
        else if (change.Property == PhaseProperty)
        {
            PseudoClasses.Set(RecordingPseudoClass, _phase == VoiceRecorderPhase.Recording);
            PseudoClasses.Set(ReviewPseudoClass, _phase == VoiceRecorderPhase.Review);
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // Escape discards wherever focus is inside the recorder (Arc only reacted on some elements).
        if (!e.Handled && e.Key == Key.Escape && _phase != VoiceRecorderPhase.Idle)
        {
            e.Handled = true;
            var focusInside = FocusInside();
            Discard();
            if (focusInside)
            {
                _mic.Focus(NavigationMethod.Tab);
            }
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _reviewPlaying = false;
        _reviewPlay.IsPlaying = false;
        foreach (var bubble in _bubbles)
        {
            SetPlaying(bubble, false);
        }
    }

    private void ApplyInks()
    {
        _dot.Background = RecordInk;
        _capsule.Background = _phase == VoiceRecorderPhase.Review ? ReviewBackground : RecordingBackground;
    }

    private void ApplyLabels()
    {
        var labels = Labels;
        _hint.Text = labels.Hint;
        AutomationProperties.SetName(_mic, labels.Record);
        AutomationProperties.SetName(_discard, labels.Discard);
        AutomationProperties.SetName(_send, labels.Send);
        UpdateStopName(_time);
        ApplyReviewName();
        if (_list is not null)
        {
            AutomationProperties.SetName(_list, labels.MessagesList);
        }
        foreach (var bubble in _bubbles)
        {
            ApplyBubbleName(bubble);
        }
    }

    private void Announce(string text) => _status?.Announce(text);

    // ---- The thread. ----

    private void Observe(IList<VoiceRecorderMessage>? list)
    {
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnMessagesChanged;
        }
        _observed = list as INotifyCollectionChanged;
        if (_observed is not null)
        {
            _observed.CollectionChanged += OnMessagesChanged;
        }
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var messages = Messages;
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { } items && messages is not null
            && e.NewStartingIndex == messages.Count - items.Count && e.NewStartingIndex == _bubbles.Count)
        {
            foreach (var item in items)
            {
                if (item is VoiceRecorderMessage message)
                {
                    AddRow(message);
                }
            }
            return;
        }
        RebuildRows();
    }

    private void AppendMessage(VoiceRecorderMessage message)
    {
        var messages = Messages;
        if (messages is null || messages.IsReadOnly || messages is Array)
        {
            var copy = messages is null ? new AvaloniaList<VoiceRecorderMessage>() : new AvaloniaList<VoiceRecorderMessage>(messages);
            copy.Add(message);
            SetCurrentValue(MessagesProperty, copy);
            return;
        }
        messages.Add(message);
        if (messages is not INotifyCollectionChanged)
        {
            AddRow(message);
        }
    }

    private void RebuildRows()
    {
        foreach (var bubble in _bubbles)
        {
            SetPlaying(bubble, false);
        }
        _bubbles.Clear();
        _messagesPanel?.Children.Clear();
        if (Messages is not { } messages)
        {
            return;
        }
        foreach (var message in messages)
        {
            AddRow(message);
        }
    }

    private void AddRow(VoiceRecorderMessage message)
    {
        var bubble = new VoiceRecorderBubble(message);
        bubble.Play.Click += (_, e) =>
        {
            e.Handled = true;
            ToggleBubble(bubble);
        };
        bubble.Bars.SeekRequested += fraction => SeekBubble(bubble, fraction);
        ApplyBubbleName(bubble);
        _bubbles.Add(bubble);
        _messagesPanel?.Children.Add(BuildRow(message, bubble));
    }

    private static Control BuildRow(VoiceRecorderMessage message, VoiceRecorderBubble bubble)
    {
        var body = new StackPanel { Spacing = 4 };
        bubble.HorizontalAlignment = message.IsIncoming ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        body.Children.Add(bubble);
        if (!string.IsNullOrEmpty(message.Meta))
        {
            var meta = new TextBlock
            {
                Text = message.Meta,
                Margin = new Thickness(6, 0),
                FontFeatures = s_tabular,
                HorizontalAlignment = message.IsIncoming ? HorizontalAlignment.Left : HorizontalAlignment.Right,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            ArcTokens.Bind(meta, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            ArcTokens.Bind(meta, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
            body.Children.Add(meta);
        }
        if (!message.IsIncoming)
        {
            body.HorizontalAlignment = HorizontalAlignment.Right;
            return body;
        }
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8 };
        var avatar = new VoiceRecorderAvatar(message, 28)
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            // Level with the bubble, above the meta line.
            Margin = new Thickness(0, 0, 0, string.IsNullOrEmpty(message.Meta) ? 0 : 20),
        };
        Grid.SetColumn(body, 1);
        body.HorizontalAlignment = HorizontalAlignment.Left;
        row.Children.Add(avatar);
        row.Children.Add(body);
        return row;
    }

    private VoiceRecorderBubble? Find(string id)
    {
        foreach (var bubble in _bubbles)
        {
            if (bubble.Message.Id == id)
            {
                return bubble;
            }
        }
        return null;
    }

    private void ApplyBubbleName(VoiceRecorderBubble bubble)
    {
        var labels = Labels;
        var message = bubble.Message;
        var time = VoiceRecorderClock.Format(message.Duration.TotalSeconds);
        string name;
        if (message.IsIncoming)
        {
            var from = message.From ?? string.Empty;
            name = bubble.IsPlaying ? labels.PauseIncoming(from, time) : labels.PlayIncoming(from, time);
        }
        else
        {
            name = bubble.IsPlaying ? labels.PauseOwn(time) : labels.PlayOwn(time);
        }
        AutomationProperties.SetName(bubble.Play, name);
    }

    // ---- Playback (bubbles and the review): one sound at a time. ----

    private void ToggleBubble(VoiceRecorderBubble bubble)
    {
        if (bubble.IsPlaying)
        {
            SetPlaying(bubble, false);
            return;
        }
        // Starting one pauses every other (Arc let several play at once).
        PauseAll();
        var duration = bubble.Message.Duration.TotalSeconds;
        if (bubble.Position >= duration - 1e-6)
        {
            bubble.Position = 0;
            bubble.Bars.Progress = 0;
        }
        SetPlaying(bubble, true);
        _loop.Start();
    }

    private void SetPlaying(VoiceRecorderBubble bubble, bool playing)
    {
        if (bubble.IsPlaying == playing)
        {
            return;
        }
        bubble.IsPlaying = playing;
        bubble.Play.IsPlaying = playing;
        bubble.ShownSecond = -1;
        bubble.Time.Text = VoiceRecorderClock.Format(playing ? bubble.Position : bubble.Message.Duration.TotalSeconds);
        ApplyBubbleName(bubble);
    }

    private void SeekBubble(VoiceRecorderBubble bubble, double fraction)
    {
        var duration = bubble.Message.Duration.TotalSeconds;
        bubble.Position = fraction * duration;
        bubble.Bars.Progress = fraction;
        if (bubble.IsPlaying)
        {
            bubble.ShownSecond = -1;
        }
    }

    private void ToggleReview()
    {
        if (_phase != VoiceRecorderPhase.Review)
        {
            return;
        }
        if (_reviewPlaying)
        {
            _reviewPlaying = false;
        }
        else
        {
            PauseAll();
            if (_reviewPosition >= _time - 1e-6)
            {
                _reviewPosition = 0;
                _reviewBars.Progress = 0;
            }
            _reviewPlaying = true;
            _loop.Start();
        }
        _reviewPlay.IsPlaying = _reviewPlaying;
        _reviewShownSecond = -1;
        _reviewTimer.Text = VoiceRecorderClock.Format(_reviewPlaying ? _reviewPosition : _time);
        ApplyReviewName();
    }

    private void SeekReview(double fraction)
    {
        if (_phase != VoiceRecorderPhase.Review)
        {
            return;
        }
        _reviewPosition = fraction * _time;
        _reviewBars.Progress = fraction;
        _reviewShownSecond = -1;
    }

    private void ApplyReviewName() =>
        AutomationProperties.SetName(_reviewPlay, _reviewPlaying ? Labels.PausePreview : Labels.PlayPreview);

    private void PauseAll()
    {
        foreach (var bubble in _bubbles)
        {
            SetPlaying(bubble, false);
        }
        if (_reviewPlaying)
        {
            _reviewPlaying = false;
            _reviewPlay.IsPlaying = false;
            _reviewTimer.Text = VoiceRecorderClock.Format(_time);
            ApplyReviewName();
        }
    }

    // ---- The frame loop: the take's clock, the live waveform and playback. ----

    /// <summary>Advances everything by <paramref name="dt"/> seconds (the frame loop; tests may call it).</summary>
    internal bool Step(double dt)
    {
        var active = false;
        if (_phase == VoiceRecorderPhase.Recording)
        {
            var max = MaxDuration.TotalSeconds;
            _time = Math.Min(max, _time + dt);
            FillSamples();
            var tenths = (long)Math.Floor(_time * 10 + 1e-6);
            if (tenths != _shownTenths)
            {
                _shownTenths = tenths;
                _recordTimer.Text = VoiceRecorderClock.FormatTenths(_time);
            }
            UpdateStopName(_time);
            _dot.Opacity = ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off ? 1 : VoiceRecorderMotion.DotOpacity(_time);
            _liveWave.Update(_samples, _sampleCount, _time);
            if (_time >= max - 1e-9)
            {
                StopRecording();
            }
            else
            {
                active = true;
            }
        }
        if (_reviewPlaying)
        {
            _reviewPosition += dt;
            if (_reviewPosition >= _time)
            {
                _reviewPosition = _time;
                _reviewPlaying = false;
                _reviewPlay.IsPlaying = false;
                _reviewBars.Progress = 1;
                _reviewTimer.Text = VoiceRecorderClock.Format(_time);
                ApplyReviewName();
            }
            else
            {
                _reviewBars.Progress = _time <= 0 ? 1 : _reviewPosition / _time;
                var second = (long)Math.Floor(_reviewPosition + 1e-9);
                if (second != _reviewShownSecond)
                {
                    _reviewShownSecond = second;
                    _reviewTimer.Text = VoiceRecorderClock.Format(_reviewPosition);
                }
                active = true;
            }
        }
        foreach (var bubble in _bubbles)
        {
            if (!bubble.IsPlaying)
            {
                continue;
            }
            var duration = bubble.Message.Duration.TotalSeconds;
            bubble.Position += dt;
            if (bubble.Position >= duration)
            {
                bubble.Position = duration;
                bubble.Bars.Progress = 1;
                SetPlaying(bubble, false);
                continue;
            }
            bubble.Bars.Progress = duration <= 0 ? 1 : bubble.Position / duration;
            var second = (long)Math.Floor(bubble.Position + 1e-9);
            if (second != bubble.ShownSecond)
            {
                bubble.ShownSecond = second;
                bubble.Time.Text = VoiceRecorderClock.Format(bubble.Position);
            }
            active = true;
        }
        return active;
    }

    /// <summary>Records every sample born by now (one per 90ms).</summary>
    private void FillSamples()
    {
        var born = Math.Min(_samples.Length, VoiceRecorderPeaks.SampleCount(_time));
        var source = LevelSource;
        while (_sampleCount < born)
        {
            var i = _sampleCount;
            var level = source is not null ? source(_take, i) : i < _simulated.Length ? _simulated[i] : VoiceRecorderPeaks.Silence;
            _samples[i] = double.IsNaN(level) ? VoiceRecorderPeaks.Silence : Math.Clamp(level, VoiceRecorderPeaks.Silence, VoiceRecorderPeaks.Loudest);
            _sampleCount++;
        }
    }

    private void UpdateStopName(double seconds)
    {
        var second = (long)Math.Floor(Math.Max(0, seconds) + 1e-9);
        if (second == _shownSecond)
        {
            return;
        }
        _shownSecond = second;
        AutomationProperties.SetName(_stop, Labels.StopAt(VoiceRecorderClock.Format(seconds)));
    }

    private VoiceRecorderMessage TakeAsMessage(string? meta)
    {
        var samples = new ArraySegment<double>(_samples, 0, _sampleCount);
        var seconds = _time;
        return new VoiceRecorderMessage
        {
            Id = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"voice-note-{++_sentCount}-{_take}"),
            Duration = TimeSpan.FromSeconds(seconds),
            Peaks = VoiceRecorderPeaks.Resample(samples, VoiceRecorderPeaks.BarCount(seconds)),
            Meta = meta,
        };
    }

    // ---- Focus. ----

    /// <summary>Runs a button's command and moves focus to the part that replaced it, keeping it inside the recorder.</summary>
    private void Act(VoiceRecorderButton button, Action action)
    {
        var inside = FocusInside();
        var before = _phase;
        action();
        if (!inside || _phase == before)
        {
            return;
        }
        Control target = _phase switch
        {
            VoiceRecorderPhase.Recording => _stop,
            VoiceRecorderPhase.Review => _send,
            _ => _mic,
        };
        // Arc dropped focus to the page after a click; we keep it here, without a ring for a pointer.
        target.Focus(button.PressedByPointer ? NavigationMethod.Pointer : NavigationMethod.Tab);
    }

    private bool FocusInside()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        return focused is not null && (ReferenceEquals(focused, this) || this.IsVisualAncestorOf(focused));
    }

    // ---- Layout morphs. ----

    /// <summary>
    /// Starts (or retargets) the layout morph from <paramref name="from"/> to the mic's box or the bar's. A morph already
    /// in flight keeps its position and speed (Arc's layout spring does too).
    /// </summary>
    private void StartMorph(Rect from, bool toMic)
    {
        var width = _composer.Bounds.Width;
        var to = toMic ? _composer.MicRect(width) : _composer.CapsuleRect(width);
        var full = ArcMotion.IsFull(this) && width > 0 && IsAttachedToVisualTree();
        _morphIsMic = toMic;
        var generation = ++_morphGeneration;
        if (!full)
        {
            StopMorph();
            return;
        }
        if (_morph is null)
        {
            _morph = new MotionValue<Rect>(this, from, MotionVectors.Rect);
            _morph.Changed += ApplyMorph;
        }
        if (!_morph.IsAnimating)
        {
            _morph.Jump(from);
        }
        _ = RunMorph(to, generation);
    }

    private async Task RunMorph(Rect to, int generation)
    {
        var done = await _morph!.AnimateTo(to, MotionSpec.Spring(VoiceRecorderMotion.Layout));
        if (done && generation == _morphGeneration)
        {
            _composer.MicFrame = null;
            _composer.CapsuleFrame = null;
            _composer.InvalidateArrange();
        }
    }

    private void ApplyMorph(Rect rect)
    {
        if (_morphIsMic)
        {
            _composer.MicFrame = rect;
            _composer.CapsuleFrame = null;
        }
        else
        {
            _composer.CapsuleFrame = rect;
            _composer.MicFrame = null;
        }
        _composer.InvalidateArrange();
    }

    private void StopMorph()
    {
        _morphGeneration++;
        _morph?.Stop();
        _composer.MicFrame = null;
        _composer.CapsuleFrame = null;
        _composer.InvalidateArrange();
    }

    private bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) is not null;

    /// <summary>The composer was resized mid-morph: aim at the new box, keeping the speed.</summary>
    private void RetargetMorph()
    {
        if (_morph is not { IsAnimating: true })
        {
            return;
        }
        var width = _composer.Bounds.Width;
        _ = RunMorph(_morphIsMic ? _composer.MicRect(width) : _composer.CapsuleRect(width), _morphGeneration);
    }

    /// <summary>
    /// The sent bubble grows out of the bar: its background starts on the bar's box and springs onto its own place at the
    /// end of the list, while the list glides to the bottom. The list clips it, so it rises from the list's bottom edge.
    /// </summary>
    private void FlyIn(VoiceRecorderBubble? bubble, Point? origin, Size originSize)
    {
        var full = ArcMotion.IsFull(this);
        if (_list is null || bubble is null)
        {
            return;
        }
        // Lay the new row out first, so the spring starts from settled geometry (Arc re-measured mid-flight and stalled a frame).
        _list.UpdateLayout();
        if (full && origin is { } start && originSize.Width > 0 && bubble.TranslatePoint(default, _list) is { } at)
        {
            var from = new Rect(start.X - at.X, start.Y - at.Y, originSize.Width, originSize.Height);
            var to = new Rect(bubble.Bounds.Size);
            var motion = new MotionValue<Rect>(bubble, from, MotionVectors.Rect);
            motion.Changed += rect => bubble.Frame = rect;
            bubble.Frame = from;
            _ = FlyInAsync(bubble, motion, to);
        }
        ScrollToEnd(animate: full);
    }

    private static async Task FlyInAsync(VoiceRecorderBubble bubble, MotionValue<Rect> motion, Rect to)
    {
        await motion.AnimateTo(to, MotionSpec.Spring(VoiceRecorderMotion.Layout));
        bubble.Frame = null;
        motion.Dispose();
    }

    private void ScrollToEnd(bool animate)
    {
        if (_list is not { } list)
        {
            return;
        }
        var target = Math.Max(0, list.Extent.Height - list.Viewport.Height);
        if (!animate || Math.Abs(target - list.Offset.Y) < 0.5)
        {
            _scroll?.Stop();
            list.Offset = new Vector(list.Offset.X, target);
            return;
        }
        if (_scroll is null)
        {
            _scroll = new MotionValue<double>(this, list.Offset.Y, MotionVectors.Double);
            _scroll.Changed += y =>
            {
                if (_list is { } current)
                {
                    current.Offset = new Vector(current.Offset.X, y);
                }
            };
        }
        if (!_scroll.IsAnimating)
        {
            _scroll.Jump(list.Offset.Y);
        }
        _ = _scroll.AnimateTo(target, MotionSpec.Tween(VoiceRecorderMotion.Scroll, ArcEasings.InOut));
    }
}
