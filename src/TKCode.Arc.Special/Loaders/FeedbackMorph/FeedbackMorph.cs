using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// A feedback button that becomes a small form and then a thank-you, in one shape. The button springs open into a panel
/// with a question, a row of mood faces and a note; <see cref="SubmitHandler"/> receives what was sent while the send
/// button spins, and the panel folds into a thank-you pill that turns back into the button after <see cref="ResetAfter"/>.
/// A failed send keeps the panel open with an error and the draft intact. Escape, the close button, a press outside or
/// tabbing away fold the panel and keep the draft.
/// </summary>
/// <remarks>
/// The control takes the button's size in the layout; the panel and the pill grow past it over what is around the control
/// (<see cref="Side"/> and <see cref="Align"/> say which way), so give it room or place it above the content it covers.
/// </remarks>
[TemplatePart(RootPart, typeof(Panel))]
[TemplatePart(SurfacePart, typeof(FeedbackMorphSurface))]
[TemplatePart(StatusPart, typeof(TextBlock))]
[PseudoClasses(OpenPseudoClass, SentPseudoClass, PressedPseudoClass)]
public partial class FeedbackMorph : TemplatedControl
{
    internal const string RootPart = "PART_Root";
    internal const string SurfacePart = "PART_Surface";
    internal const string StatusPart = "PART_Status";
    internal const string OpenPseudoClass = ":open";
    internal const string SentPseudoClass = ":sent";
    internal const string PressedPseudoClass = ":pressed";

    /// <summary>The button's text and accessible name.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<FeedbackMorph, string>(nameof(Label), "Give feedback");

    /// <summary>The question at the top of the panel; also the panel's accessible name.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<FeedbackMorph, string>(nameof(Title), "How are we doing?");

    /// <summary>
    /// The mood scale, from unhappy to happy (five read best, three or more work); null uses five built-in faces
    /// (<see cref="FeedbackMood.CreateDefaults"/>).
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<FeedbackMood>?> MoodsProperty =
        AvaloniaProperty.Register<FeedbackMorph, IReadOnlyList<FeedbackMood>?>(nameof(Moods));

    /// <summary>The picked mood, null when none. Kept when the panel closes; cleared after a successful send.</summary>
    public static readonly StyledProperty<FeedbackMood?> SelectedMoodProperty =
        AvaloniaProperty.Register<FeedbackMorph, FeedbackMood?>(nameof(SelectedMood), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The note's text. Kept when the panel closes; cleared after a successful send.</summary>
    public static readonly StyledProperty<string> MessageProperty =
        AvaloniaProperty.Register<FeedbackMorph, string>(nameof(Message), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The note's prompt while no face is picked (a picked face shows its own <see cref="FeedbackMood.Prompt"/>).</summary>
    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<FeedbackMorph, string>(nameof(Placeholder), "Share what's on your mind");

    /// <summary>The note's character limit (the status line counts down to it).</summary>
    public static readonly StyledProperty<int> MaxLengthProperty =
        AvaloniaProperty.Register<FeedbackMorph, int>(nameof(MaxLength), 500, coerce: (_, v) => Math.Max(1, v));

    /// <summary>The words of the thank-you pill, also announced after a successful send.</summary>
    public static readonly StyledProperty<string> SuccessLabelProperty =
        AvaloniaProperty.Register<FeedbackMorph, string>(nameof(SuccessLabel), "Got it, thank you for writing");

    /// <summary>How long the thank-you pill stays before it folds back into the button.</summary>
    public static readonly StyledProperty<TimeSpan> ResetAfterProperty =
        AvaloniaProperty.Register<FeedbackMorph, TimeSpan>(nameof(ResetAfter), TimeSpan.FromMilliseconds(3200));

    /// <summary>Whether the panel grows down from the button (Bottom, the default) or up from it (Top).</summary>
    public static readonly StyledProperty<FeedbackMorphSide> SideProperty =
        AvaloniaProperty.Register<FeedbackMorph, FeedbackMorphSide>(nameof(Side));

    /// <summary>Which horizontal point of the button the panel stays anchored to (Center by default).</summary>
    public static readonly StyledProperty<FeedbackMorphAlign> AlignProperty =
        AvaloniaProperty.Register<FeedbackMorph, FeedbackMorphAlign>(nameof(Align), FeedbackMorphAlign.Center);

    /// <summary>Whether the panel is open (the button when false). Setting it opens or folds the panel.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<FeedbackMorph, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Receives each submission. While its task runs the send button spins and the note is read-only; when it completes
    /// the thank-you pill shows, and when it throws the panel stays open with <see cref="ErrorMessage"/> and the draft.
    /// The token is cancelled when the panel is closed meanwhile. Null accepts every submission at once.
    /// </summary>
    public static readonly StyledProperty<Func<FeedbackMorphSubmission, CancellationToken, Task>?> SubmitHandlerProperty =
        AvaloniaProperty.Register<FeedbackMorph, Func<FeedbackMorphSubmission, CancellationToken, Task>?>(nameof(SubmitHandler));

    /// <summary>The close button's accessible name.</summary>
    public static readonly StyledProperty<string> CloseLabelProperty =
        AvaloniaProperty.Register<FeedbackMorph, string>(nameof(CloseLabel), "Close feedback form");

    /// <summary>The send button's text.</summary>
    public static readonly StyledProperty<string> SendLabelProperty =
        AvaloniaProperty.Register<FeedbackMorph, string>(nameof(SendLabel), "Send");

    /// <summary>The send button's text while <see cref="SubmitHandler"/> runs, also announced.</summary>
    public static readonly StyledProperty<string> SendingLabelProperty =
        AvaloniaProperty.Register<FeedbackMorph, string>(nameof(SendingLabel), "Sending");

    /// <summary>The caption under the faces until one is hovered or picked.</summary>
    public static readonly StyledProperty<string> CaptionHintProperty =
        AvaloniaProperty.Register<FeedbackMorph, string>(nameof(CaptionHint), "Tap the face closest to how you feel");

    /// <summary>Shown beside the send button and announced when there is nothing to send yet.</summary>
    public static readonly StyledProperty<string> EmptyHintProperty =
        AvaloniaProperty.Register<FeedbackMorph, string>(nameof(EmptyHint), "Tap a face or add a few words");

    /// <summary>Shown beside the send button and announced when a send failed.</summary>
    public static readonly StyledProperty<string> ErrorMessageProperty =
        AvaloniaProperty.Register<FeedbackMorph, string>(nameof(ErrorMessage), "Not sent. Please try once more.");

    /// <summary>The word after the remaining-characters count in the status line ("480 left").</summary>
    public static readonly StyledProperty<string> RemainingLabelProperty =
        AvaloniaProperty.Register<FeedbackMorph, string>(nameof(RemainingLabel), "left");

    /// <summary>The accessible name of the row of faces.</summary>
    public static readonly StyledProperty<string> MoodsLabelProperty =
        AvaloniaProperty.Register<FeedbackMorph, string>(nameof(MoodsLabel), "Mood");

    /// <summary>Where the control is in its open, send and thank-you cycle.</summary>
    public static readonly DirectProperty<FeedbackMorph, FeedbackMorphState> StateProperty =
        AvaloniaProperty.RegisterDirect<FeedbackMorph, FeedbackMorphState>(nameof(State), o => o.State);

    /// <summary>Raised after a submission was accepted (after <see cref="SubmitHandler"/> finished). Bubbles.</summary>
    public static readonly RoutedEvent<FeedbackMorphSubmittedEventArgs> SubmittedEvent =
        RoutedEvent.Register<FeedbackMorph, FeedbackMorphSubmittedEventArgs>(nameof(Submitted), RoutingStrategies.Bubble);

    /// <summary>Raised whenever <see cref="State"/> changes, with the exception when a send failed. Bubbles.</summary>
    public static readonly RoutedEvent<FeedbackMorphStateChangedEventArgs> StateChangedEvent =
        RoutedEvent.Register<FeedbackMorph, FeedbackMorphStateChangedEventArgs>(nameof(StateChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="SelectedMood"/> changes, from code or by the person. Bubbles.</summary>
    public static readonly RoutedEvent<FeedbackMorphSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<FeedbackMorph, FeedbackMorphSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    private readonly FeedbackMorphTrigger _trigger;
    private FeedbackMorphPanel? _panel;
    private FeedbackMorphSentFace? _sent;
    private FeedbackMorphSurface? _surface;
    private TextBlock? _status;
    private IReadOnlyList<FeedbackMood>? _defaultMoods;
    private FeedbackMorphState _state;

    static FeedbackMorph()
    {
        FocusableProperty.OverrideDefaultValue<FeedbackMorph>(false);
        ClipToBoundsProperty.OverrideDefaultValue<FeedbackMorph>(false);
        SpecialFocusModality.Watch();
    }

    /// <summary>Creates the control with its button showing.</summary>
    public FeedbackMorph()
    {
        _trigger = new FeedbackMorphTrigger(this) { Label = Label };
        _trigger.Click += OnTriggerClick;
        AutomationProperties.SetName(_trigger, Label);
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="MoodsProperty"/>
    public IReadOnlyList<FeedbackMood>? Moods { get => GetValue(MoodsProperty); set => SetValue(MoodsProperty, value); }

    /// <inheritdoc cref="SelectedMoodProperty"/>
    public FeedbackMood? SelectedMood { get => GetValue(SelectedMoodProperty); set => SetValue(SelectedMoodProperty, value); }

    /// <inheritdoc cref="MessageProperty"/>
    public string Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    /// <inheritdoc cref="PlaceholderProperty"/>
    public string Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <inheritdoc cref="MaxLengthProperty"/>
    public int MaxLength { get => GetValue(MaxLengthProperty); set => SetValue(MaxLengthProperty, value); }

    /// <inheritdoc cref="SuccessLabelProperty"/>
    public string SuccessLabel { get => GetValue(SuccessLabelProperty); set => SetValue(SuccessLabelProperty, value); }

    /// <inheritdoc cref="ResetAfterProperty"/>
    public TimeSpan ResetAfter { get => GetValue(ResetAfterProperty); set => SetValue(ResetAfterProperty, value); }

    /// <inheritdoc cref="SideProperty"/>
    public FeedbackMorphSide Side { get => GetValue(SideProperty); set => SetValue(SideProperty, value); }

    /// <inheritdoc cref="AlignProperty"/>
    public FeedbackMorphAlign Align { get => GetValue(AlignProperty); set => SetValue(AlignProperty, value); }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <inheritdoc cref="SubmitHandlerProperty"/>
    public Func<FeedbackMorphSubmission, CancellationToken, Task>? SubmitHandler
    {
        get => GetValue(SubmitHandlerProperty);
        set => SetValue(SubmitHandlerProperty, value);
    }

    /// <inheritdoc cref="CloseLabelProperty"/>
    public string CloseLabel { get => GetValue(CloseLabelProperty); set => SetValue(CloseLabelProperty, value); }

    /// <inheritdoc cref="SendLabelProperty"/>
    public string SendLabel { get => GetValue(SendLabelProperty); set => SetValue(SendLabelProperty, value); }

    /// <inheritdoc cref="SendingLabelProperty"/>
    public string SendingLabel { get => GetValue(SendingLabelProperty); set => SetValue(SendingLabelProperty, value); }

    /// <inheritdoc cref="CaptionHintProperty"/>
    public string CaptionHint { get => GetValue(CaptionHintProperty); set => SetValue(CaptionHintProperty, value); }

    /// <inheritdoc cref="EmptyHintProperty"/>
    public string EmptyHint { get => GetValue(EmptyHintProperty); set => SetValue(EmptyHintProperty, value); }

    /// <inheritdoc cref="ErrorMessageProperty"/>
    public string ErrorMessage { get => GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }

    /// <inheritdoc cref="RemainingLabelProperty"/>
    public string RemainingLabel { get => GetValue(RemainingLabelProperty); set => SetValue(RemainingLabelProperty, value); }

    /// <inheritdoc cref="MoodsLabelProperty"/>
    public string MoodsLabel { get => GetValue(MoodsLabelProperty); set => SetValue(MoodsLabelProperty, value); }

    /// <inheritdoc cref="StateProperty"/>
    public FeedbackMorphState State
    {
        get => _state;
        private set => SetAndRaise(StateProperty, ref _state, value);
    }

    /// <inheritdoc cref="SubmittedEvent"/>
    public event EventHandler<FeedbackMorphSubmittedEventArgs>? Submitted
    {
        add => AddHandler(SubmittedEvent, value);
        remove => RemoveHandler(SubmittedEvent, value);
    }

    /// <inheritdoc cref="StateChangedEvent"/>
    public event EventHandler<FeedbackMorphStateChangedEventArgs>? StateChanged
    {
        add => AddHandler(StateChangedEvent, value);
        remove => RemoveHandler(StateChangedEvent, value);
    }

    /// <inheritdoc cref="SelectionChangedEvent"/>
    public event EventHandler<FeedbackMorphSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <summary>The moods on screen: <see cref="Moods"/>, or the built-in five.</summary>
    internal IReadOnlyList<FeedbackMood> EffectiveMoods => Moods ?? (_defaultMoods ??= FeedbackMood.CreateDefaults());

    /// <summary>Whether there is something to send: a picked face or a note that is not blank.</summary>
    internal bool HasContent => SelectedMood is not null || !string.IsNullOrWhiteSpace(Message);

    /// <summary>The surface (tests).</summary>
    internal FeedbackMorphSurface? Surface => _surface;

    /// <summary>The button face (tests).</summary>
    internal FeedbackMorphTrigger Trigger => _trigger;

    /// <summary>The panel face, once it was first opened (tests).</summary>
    internal FeedbackMorphPanel? Panel => _panel;

    /// <summary>The thank-you face, once it was first shown (tests).</summary>
    internal FeedbackMorphSentFace? SentFace => _sent;

    /// <summary>The live region's text (tests).</summary>
    internal string? Announcement => _status?.Text;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _surface?.FinishExits();
        _surface = e.NameScope.Find<FeedbackMorphSurface>(SurfacePart);
        _status = e.NameScope.Find<TextBlock>(StatusPart);
        if (_status is not null)
        {
            AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        }
        if (_surface is not null)
        {
            _surface.Side = Side;
            _surface.Align = Align;
            _surface.Anchor = _trigger;
            _surface.Show(FaceFor(State), TimeSpan.Zero, TimeSpan.Zero, scaleIn: false);
        }
        UpdatePseudoClasses();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            if (_syncingOpen)
            {
                return;
            }
            if (change.GetNewValue<bool>())
            {
                Open();
            }
            else
            {
                Close();
            }
        }
        else if (change.Property == LabelProperty)
        {
            _trigger.Label = Label;
            AutomationProperties.SetName(_trigger, Label);
            _surface?.InvalidateMeasure();
        }
        else if (change.Property == SelectedMoodProperty)
        {
            var oldMood = change.GetOldValue<FeedbackMood?>();
            var newMood = change.GetNewValue<FeedbackMood?>();
            _panel?.SyncSelection(animate: true);
            RaiseEvent(new FeedbackMorphSelectionChangedEventArgs(SelectionChangedEvent, oldMood, newMood));
        }
        else if (change.Property == MessageProperty)
        {
            _panel?.SyncNote();
        }
        else if (change.Property == MoodsProperty)
        {
            if (SelectedMood is { } picked && !EffectiveMoods.Contains(picked))
            {
                SetCurrentValue(SelectedMoodProperty, null);
            }
            _panel?.Refresh();
        }
        else if (change.Property == SideProperty || change.Property == AlignProperty)
        {
            if (_surface is not null)
            {
                _surface.Side = Side;
                _surface.Align = Align;
            }
        }
        else if (change.Property == SuccessLabelProperty)
        {
            if (_sent is not null)
            {
                _sent.Text = SuccessLabel;
            }
        }
        else if (change.Property == ResetAfterProperty && _resetTimer is { IsEnabled: true } timer)
        {
            timer.Stop();
            timer.Interval = Positive(ResetAfter);
            timer.Start();
        }
        else if (_panel is not null && IsPanelWord(change.Property))
        {
            ApplyWords(_panel);
            _panel.Refresh();
        }
    }

    private static bool IsPanelWord(AvaloniaProperty property) =>
        property == TitleProperty || property == CloseLabelProperty || property == SendLabelProperty
        || property == SendingLabelProperty || property == CaptionHintProperty || property == EmptyHintProperty
        || property == ErrorMessageProperty || property == RemainingLabelProperty || property == PlaceholderProperty
        || property == MaxLengthProperty || property == MoodsLabelProperty;

    private void ApplyWords(FeedbackMorphPanel panel)
    {
        panel.Title = Title;
        panel.CloseLabel = CloseLabel;
        panel.SendLabel = SendLabel;
        panel.SendingLabel = SendingLabel;
        panel.MaxLength = MaxLength;
        AutomationProperties.SetName(panel, Title);
        if (panel.Note is { } note)
        {
            AutomationProperties.SetName(note, Placeholder);
        }
        if (panel.Moods is { } moods)
        {
            AutomationProperties.SetName(moods, MoodsLabel);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (State is FeedbackMorphState.Open or FeedbackMorphState.Sending)
        {
            WatchOutsidePresses(true);
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        WatchOutsidePresses(false);
        CancelSubmit();
        if (State == FeedbackMorphState.Sent)
        {
            EndSent(animate: false);
        }
        _surface?.FinishExits();
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(OpenPseudoClass, State is FeedbackMorphState.Open or FeedbackMorphState.Sending);
        PseudoClasses.Set(SentPseudoClass, State == FeedbackMorphState.Sent);
    }

    internal void SetTriggerPressed(bool pressed) => PseudoClasses.Set(PressedPseudoClass, pressed);

    private void Announce(string? message)
    {
        if (_status is null)
        {
            return;
        }
        // The same words twice in a row are still announced: clear first.
        _status.Text = string.Empty;
        _status.Text = message ?? string.Empty;
    }

    private static TimeSpan Positive(TimeSpan value) => value > TimeSpan.Zero ? value : TimeSpan.FromMilliseconds(1);

    private void PostFocus(Func<bool> focus)
    {
        var request = ++_focusRequest;
        Dispatcher.UIThread.Post(() =>
        {
            if (request == _focusRequest)
            {
                focus();
            }
        }, DispatcherPriority.Loaded);
    }
}
