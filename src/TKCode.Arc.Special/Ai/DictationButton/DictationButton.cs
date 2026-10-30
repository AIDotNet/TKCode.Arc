using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Primitives;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// A chat composer with voice input (Arc Dictation button, design reference): the field, a model label, the dictation mic
/// and send. Pressing the mic springs it into a recording pill with a live waveform, a timer, cancel and Insert; words
/// stream into the field as the person speaks, the recognizer's guesses muted until they settle. Insert appends the text
/// and puts the caret at its end; Enter sends.
/// <para>
/// Speech comes from <see cref="Recognizer"/> (an <see cref="IDictationRecognizer"/> you write for your platform or
/// service). For demos, a <see cref="Simulation"/> speaks the scripts you give it without touching the microphone and,
/// unless <see cref="AutoPlay"/> says otherwise, loops a scripted dictation until the person first touches the card. With
/// neither, the composer is a plain field whose mic says dictation is not available. Handle <see cref="SendRequested"/> to
/// send; without a handler sending is simulated.
/// </para>
/// </summary>
[TemplatePart(FieldPart, typeof(DictationButtonField))]
[TemplatePart(LabelPart, typeof(ArcContentSwap))]
[TemplatePart(DictationPart, typeof(DictationControl))]
[TemplatePart(SendPart, typeof(DictationIconButton))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(DictatingPseudoClass, NarrowPseudoClass, CompactPseudoClass, TinyPseudoClass)]
public partial class DictationButton : TemplatedControl
{
    internal const string FieldPart = "PART_Field";
    internal const string LabelPart = "PART_Label";
    internal const string DictationPart = "PART_Dictation";
    internal const string SendPart = "PART_Send";
    internal const string AnnouncerPart = "PART_Announcer";

    internal const string DictatingPseudoClass = ":dictating";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string CompactPseudoClass = ":compact";
    internal const string TinyPseudoClass = ":tiny";

    /// <summary>How long a simulated send takes.</summary>
    internal static readonly TimeSpan SimulatedSend = TimeSpan.FromMilliseconds(900);

    /// <summary>The field's text (two-way): typing, an inserted dictation and the clear after sending change it.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<DictationButton, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The field's placeholder, such as "Message" and the assistant's name; null (the default) shows none.</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<DictationButton, string?>(nameof(Placeholder));

    /// <summary>The speech source. Null uses <see cref="Simulation"/>; with neither, the mic says dictation is not available.</summary>
    public static readonly StyledProperty<IDictationRecognizer?> RecognizerProperty =
        DictationControl.RecognizerProperty.AddOwner<DictationButton>();

    /// <summary>
    /// A simulated voice to dictate with while <see cref="Recognizer"/> is null: it never touches the microphone and speaks
    /// its scripts, and with <see cref="AutoPlay"/> left null the composer loops it on its own. Its clock stands still while
    /// the card is off screen. Null (the default) plays nothing. Give each composer its own simulation.
    /// </summary>
    public static readonly StyledProperty<SimulatedDictationRecognizer?> SimulationProperty =
        DictationControl.SimulationProperty.AddOwner<DictationButton>();

    /// <summary>Seconds before listening stops and inserts on its own; 0 turns the limit off.</summary>
    public static readonly StyledProperty<double> MaxDurationProperty =
        DictationControl.MaxDurationProperty.AddOwner<DictationButton>();

    /// <summary>The model label at the start of the toolbar (the model the composer sends to); null (the default) hides it.</summary>
    public static readonly StyledProperty<DictationModel?> ModelProperty =
        AvaloniaProperty.Register<DictationButton, DictationModel?>(nameof(Model));

    /// <summary>
    /// Loops a scripted dictation (listen, insert, send) until the first pointer, key or focus inside the card; it pauses
    /// off screen. Null plays it only with a <see cref="Simulation"/> and no <see cref="Recognizer"/>. For docs and demos,
    /// not production.
    /// </summary>
    public static readonly StyledProperty<bool?> AutoPlayProperty =
        AvaloniaProperty.Register<DictationButton, bool?>(nameof(AutoPlay));

    /// <summary>Every word the composer shows or says.</summary>
    public static readonly StyledProperty<DictationButtonLabels> LabelsProperty =
        DictationControl.LabelsProperty.AddOwner<DictationButton>();

    /// <summary>Where the dictation is.</summary>
    public static readonly DirectProperty<DictationButton, DictationStatus> StatusProperty =
        AvaloniaProperty.RegisterDirect<DictationButton, DictationStatus>(nameof(Status), o => o.Status);

    /// <summary>True while a send is pending (the send button shows a spinner).</summary>
    public static readonly DirectProperty<DictationButton, bool> IsSendingProperty =
        AvaloniaProperty.RegisterDirect<DictationButton, bool>(nameof(IsSending), o => o.IsSending);

    /// <summary>Raised whenever the live transcript of the current dictation changes. Bubbles.</summary>
    public static readonly RoutedEvent<DictationTranscriptEventArgs> TranscriptChangedEvent =
        RoutedEvent.Register<DictationButton, DictationTranscriptEventArgs>(nameof(TranscriptChanged), RoutingStrategies.Bubble);

    /// <summary>Raised with the final text of a dictation, after it has been appended to the field. Bubbles.</summary>
    public static readonly RoutedEvent<DictationCommittedEventArgs> CommittedEvent =
        RoutedEvent.Register<DictationButton, DictationCommittedEventArgs>(nameof(Committed), RoutingStrategies.Bubble);

    /// <summary>Raised on every status change, with the error code for denied and error. Bubbles.</summary>
    public static readonly RoutedEvent<DictationStatusChangedEventArgs> StatusChangedEvent =
        RoutedEvent.Register<DictationButton, DictationStatusChangedEventArgs>(nameof(StatusChanged), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised to send the message (Enter in the field or the send button). Set <see cref="DictationSendEventArgs.Completion"/>
    /// to your sending task: the button spins until it completes and the field clears when it succeeds. Without a handler the
    /// send is simulated. Bubbles.
    /// </summary>
    public static readonly RoutedEvent<DictationSendEventArgs> SendRequestedEvent =
        RoutedEvent.Register<DictationButton, DictationSendEventArgs>(nameof(SendRequested), RoutingStrategies.Bubble);

    private DictationButtonField? _field;
    private ArcContentSwap? _label;
    private DictationControl? _dictation;
    private DictationIconButton? _send;
    private ExplainerAnnouncer? _announcer;
    private DictationStatus _status;
    private bool _sending;
    private bool _sendAfterCommit;
    private string? _labelKey;

    static DictationButton()
    {
        FocusableProperty.OverrideDefaultValue<DictationButton>(false);
    }

    /// <summary>Creates the composer.</summary>
    public DictationButton()
    {
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnPointerPressedTunnel, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(GotFocusEvent, OnGotFocus, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="PlaceholderProperty"/>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <inheritdoc cref="RecognizerProperty"/>
    public IDictationRecognizer? Recognizer { get => GetValue(RecognizerProperty); set => SetValue(RecognizerProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public SimulatedDictationRecognizer? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="MaxDurationProperty"/>
    public double MaxDuration { get => GetValue(MaxDurationProperty); set => SetValue(MaxDurationProperty, value); }

    /// <inheritdoc cref="ModelProperty"/>
    public DictationModel? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <inheritdoc cref="AutoPlayProperty"/>
    public bool? AutoPlay { get => GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public DictationButtonLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public DictationStatus Status => _status;

    /// <inheritdoc cref="IsSendingProperty"/>
    public bool IsSending => _sending;

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

    /// <inheritdoc cref="StatusChangedEvent"/>
    public event EventHandler<DictationStatusChangedEventArgs>? StatusChanged
    {
        add => AddHandler(StatusChangedEvent, value);
        remove => RemoveHandler(StatusChangedEvent, value);
    }

    /// <inheritdoc cref="SendRequestedEvent"/>
    public event EventHandler<DictationSendEventArgs>? SendRequested
    {
        add => AddHandler(SendRequestedEvent, value);
        remove => RemoveHandler(SendRequestedEvent, value);
    }

    internal DictationButtonField? Field => _field;
    internal ArcContentSwap? ToolbarLabel => _label;
    internal DictationControl? Dictation => _dictation;
    internal DictationIconButton? SendButton => _send;
    internal ExplainerAnnouncer? Announcer => _announcer;

    /// <summary>What the toolbar label shows: "model", "hints", "error" or "none".</summary>
    internal string LabelKind => _labelKey is null ? "none" : _labelKey.Split(':')[0];

    /// <summary>Starts dictating.</summary>
    public void StartDictation() => _dictation?.Start();

    /// <summary>Stops listening and inserts the transcript.</summary>
    public void ConfirmDictation() => _dictation?.Confirm();

    /// <summary>Cancels the dictation and discards its words.</summary>
    public void CancelDictation() => _dictation?.Cancel();

    /// <summary>Sends the message, as Enter in the field does (while listening, it inserts first, then sends).</summary>
    public void Send() => _ = SendAsync();

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _field = e.NameScope.Find<DictationButtonField>(FieldPart);
        _label = e.NameScope.Find<ArcContentSwap>(LabelPart);
        _dictation = e.NameScope.Find<DictationControl>(DictationPart);
        _send = e.NameScope.Find<DictationIconButton>(SendPart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        if (_dictation is not null)
        {
            _dictation.StatusChanged += OnDictationStatus;
            _dictation.TranscriptChanged += OnDictationTranscript;
            _dictation.Committed += OnDictationCommitted;
            _dictation.Cancelled += OnDictationCancelled;
        }
        if (_send is not null)
        {
            _send.Click += OnSendClick;
        }
        _labelKey = null;
        UpdateWidthClasses(Bounds.Width);
        Refresh();
        UpdateAutoPlay();
    }

    private void Unhook()
    {
        if (_dictation is not null)
        {
            _dictation.StatusChanged -= OnDictationStatus;
            _dictation.TranscriptChanged -= OnDictationTranscript;
            _dictation.Committed -= OnDictationCommitted;
            _dictation.Cancelled -= OnDictationCancelled;
        }
        if (_send is not null)
        {
            _send.Click -= OnSendClick;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == ModelProperty || change.Property == LabelsProperty
            || change.Property == IsEnabledProperty)
        {
            Refresh();
        }
        else if (change.Property == RecognizerProperty || change.Property == AutoPlayProperty || change.Property == SimulationProperty)
        {
            if (change.Property == SimulationProperty)
            {
                PauseOffScreen(change.GetOldValue<SimulatedDictationRecognizer?>(), change.GetNewValue<SimulatedDictationRecognizer?>());
            }
            UpdateAutoPlay(changed: true);
        }
        else if (change.Property == BoundsProperty)
        {
            UpdateWidthClasses(change.GetNewValue<Rect>().Width);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateAutoPlay();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopAutoPlayLoop();
    }

    /// <summary>The breakpoints are container queries on the card's own width.</summary>
    private void UpdateWidthClasses(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var token = DictationButtonTokens();
        var narrow = width < token.Narrow;
        var compact = width < token.Compact;
        var tiny = width <= token.Tiny;
        if (PseudoClasses.Contains(NarrowPseudoClass) == narrow && PseudoClasses.Contains(CompactPseudoClass) == compact
            && PseudoClasses.Contains(TinyPseudoClass) == tiny)
        {
            return;
        }
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        PseudoClasses.Set(CompactPseudoClass, compact);
        PseudoClasses.Set(TinyPseudoClass, tiny);
        Dispatcher.UIThread.Post(UpdateLabel, DispatcherPriority.Loaded);
    }

    /// <summary>The breakpoints from the token (fixed fallbacks before the theme resolves).</summary>
    private (double Narrow, double Compact, double Tiny) DictationButtonTokens()
    {
        var theme = ActualThemeVariant;
        var narrow = this.TryFindResource(DictationButtonTokenKind.NarrowWidth, theme, out var n) && n is double nv ? nv : 460;
        var compact = this.TryFindResource(DictationButtonTokenKind.CompactWidth, theme, out var c) && c is double cv ? cv : 400;
        var tiny = this.TryFindResource(DictationButtonTokenKind.TinyWidth, theme, out var t) && t is double tv ? tv : 360;
        return (narrow, compact, tiny);
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }
        var status = _dictation?.Status ?? DictationStatus.Idle;
        var active = status is DictationStatus.Starting or DictationStatus.Listening or DictationStatus.Processing;
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            if (e.Source is Avalonia.Controls.Button button && !ReferenceEquals(button, _dictation?.InsertButton))
            {
                // A focused mic, cancel, Try again or send keeps its own action.
                return;
            }
            if (status == DictationStatus.Listening)
            {
                // Enter inserts from anywhere inside the card while listening.
                ConfirmDictation();
                e.Handled = true;
            }
            else if (active)
            {
                e.Handled = true;
            }
            else if (e.Source is Visual source && _field is not null && (ReferenceEquals(_field, source) || _field.IsVisualAncestorOf(source)))
            {
                // Enter in the field sends; Shift+Enter adds a line (the field handles it).
                e.Handled = true;
                Send();
            }
        }
        else if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && _dictation is not null)
        {
            e.Handled = _dictation.HandleKey(Key.Escape, KeyModifiers.None);
        }
    }

    private void OnSendClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Send();
    }

    internal async Task SendAsync()
    {
        if (_sending || _dictation is null)
        {
            return;
        }
        var status = _dictation.Status;
        if (status == DictationStatus.Listening)
        {
            _sendAfterCommit = true;
            ConfirmDictation();
            return;
        }
        if (status is DictationStatus.Starting or DictationStatus.Processing)
        {
            return;
        }
        var text = Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        var args = new DictationSendEventArgs(SendRequestedEvent, text);
        RaiseEvent(args);
        var task = args.Completion ?? (args.Handled ? Task.CompletedTask : Task.Delay(SimulatedSend));
        SetAndRaise(IsSendingProperty, ref _sending, true);
        Refresh();
        var sent = false;
        try
        {
            await task;
            sent = true;
        }
        catch (Exception)
        {
            // The message stays in the field so the person can send it again.
        }
        SetAndRaise(IsSendingProperty, ref _sending, false);
        if (sent)
        {
            SetCurrentValue(TextProperty, string.Empty);
            _announcer?.Announce(Labels.SentAnnouncement);
        }
        Refresh();
    }

    private void OnDictationStatus(object? sender, DictationStatusChangedEventArgs e)
    {
        e.Handled = true;
        SetAndRaise(StatusProperty, ref _status, e.Status);
        var active = e.Status is DictationStatus.Starting or DictationStatus.Listening or DictationStatus.Processing;
        PseudoClasses.Set(DictatingPseudoClass, active);
        if (_field is not null)
        {
            _field.IsReadOnly = active;
        }
        if (e.Status is DictationStatus.Denied or DictationStatus.Error)
        {
            _sendAfterCommit = false;
        }
        Refresh();
        RaiseEvent(new DictationStatusChangedEventArgs(StatusChangedEvent, e.Status, e.Error));
    }

    private void OnDictationTranscript(object? sender, DictationTranscriptEventArgs e)
    {
        e.Handled = true;
        if (e.Transcript.IsEmpty)
        {
            _field?.ClearLive();
        }
        else
        {
            _field?.SetLive(e.Transcript);
        }
        RaiseEvent(new DictationTranscriptEventArgs(TranscriptChangedEvent, e.Transcript));
    }

    private void OnDictationCommitted(object? sender, DictationCommittedEventArgs e)
    {
        e.Handled = true;
        var hadFocus = _dictation?.HadFocusAtStop == true;
        if (_field is not null)
        {
            _field.ClearLive();
            _field.Append(e.Text);
            _field.KeepNewestInView();
            if (hadFocus)
            {
                // Insert hands the focus back to the field, at the end of the text.
                _field.Focus(NavigationMethod.Tab);
                _field.CaretToEnd();
            }
        }
        else
        {
            SetCurrentValue(TextProperty, DictationText.Join(Text, e.Text));
        }
        RaiseEvent(new DictationCommittedEventArgs(CommittedEvent, e.Text));
        if (_sendAfterCommit)
        {
            _sendAfterCommit = false;
            Send();
        }
    }

    private void OnDictationCancelled(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _sendAfterCommit = false;
        _field?.ClearLive();
    }

    /// <summary>Brings the send button, the field's name and the toolbar label in line with the state.</summary>
    private void Refresh()
    {
        var labels = Labels;
        if (_field is not null)
        {
            AutomationProperties.SetName(_field, labels.Field);
        }
        if (_send is not null)
        {
            var dictating = _status is DictationStatus.Starting or DictationStatus.Processing;
            _send.IsBusy = _sending;
            _send.IsEnabled = _sending || (!dictating && (!string.IsNullOrWhiteSpace(Text) || _status == DictationStatus.Listening));
            AutomationProperties.SetName(_send, labels.Send);
            Tooltip.SetTip(_send, labels.Send);
        }
        UpdateLabel();
    }

    /// <summary>The toolbar label: the model, the keyboard hints while listening, or the error with its icon.</summary>
    private void UpdateLabel()
    {
        if (_label is null)
        {
            return;
        }
        var labels = Labels;
        var status = _status;
        var active = status is DictationStatus.Starting or DictationStatus.Listening or DictationStatus.Processing;
        var narrow = PseudoClasses.Contains(NarrowPseudoClass);
        var compact = PseudoClasses.Contains(CompactPseudoClass);
        var tiny = PseudoClasses.Contains(TinyPseudoClass);
        string? key;
        Func<Control>? build = null;
        if (status is DictationStatus.Denied or DictationStatus.Error && _dictation?.Error is { } code)
        {
            var text = compact ? labels.ErrorShort(code) : labels.Error(code);
            key = "error:" + text;
            build = () => ErrorLabel(code, text);
        }
        else if (active && tiny)
        {
            key = null;
        }
        else if (active && !narrow && status != DictationStatus.Processing)
        {
            key = "hints:" + labels.InsertKey + labels.InsertHint + labels.CancelKey + labels.CancelHint;
            build = () => HintsLabel(labels);
        }
        else if (Model is { } model && !string.IsNullOrEmpty(model.Name))
        {
            key = "model:" + model.Name;
            build = () => new DictationModelLabel(model);
        }
        else
        {
            key = null;
        }
        if (key == _labelKey)
        {
            return;
        }
        _labelKey = key;
        _label.Content = build?.Invoke();
    }

    /// <summary>Binds a property of a part built in code to one of this control's tokens.</summary>
    private static void Token(Control target, AvaloniaProperty property, DictationButtonTokenKind kind) =>
        target.Bind(property, target.GetResourceObservable(kind));

    private static Control ErrorLabel(DictationErrorCode code, string text)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var glyph = new ArcGlyph
        {
            Data = code == DictationErrorCode.NotAllowed ? DictationControl.MicOff : ArcIcons.CircleAlert,
            Size = 16,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Danger);
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        ArcTokens.Bind(label, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        panel.Children.Add(glyph);
        panel.Children.Add(label);
        return panel;
    }

    private static Control HintsLabel(DictationButtonLabels labels)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(Key(labels.InsertKey));
        panel.Children.Add(Hint(labels.InsertHint, new Thickness(0, 0, 7, 0)));
        panel.Children.Add(Key(labels.CancelKey));
        panel.Children.Add(Hint(labels.CancelHint, default));
        return panel;

        static Control Key(string text)
        {
            var caption = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
            Token(caption, TextBlock.FontSizeProperty, DictationButtonTokenKind.KeyFontSize);
            ArcTokens.Bind(caption, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            ArcTokens.Bind(caption, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
            var cap = new Border { Child = caption, VerticalAlignment = VerticalAlignment.Center };
            Token(cap, Border.PaddingProperty, DictationButtonTokenKind.KeyPadding);
            Token(cap, Border.CornerRadiusProperty, DictationButtonTokenKind.KeyCornerRadius);
            ArcTokens.Bind(cap, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
            ArcTokens.Bind(cap, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
            ArcTokens.Bind(cap, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
            return cap;
        }

        static Control Hint(string text, Thickness margin)
        {
            var hint = new TextBlock { Text = text, Margin = margin, VerticalAlignment = VerticalAlignment.Center };
            Token(hint, TextBlock.FontSizeProperty, DictationButtonTokenKind.HintFontSize);
            ArcTokens.Bind(hint, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            return hint;
        }
    }
}
