using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The feedback control of an AI reply (Arc Response feedback, design reference), for your own message rows: thumbs up and
/// down after any <see cref="Leading"/> actions and <see cref="Trailing"/> content pinned to the end of the row. Thumbs
/// down opens an inline panel with reason chips and an optional note; thumbs up fills and asks what was good; a submit
/// shows a short thank-you before the panel folds back into the row. Pressing the active thumb clears the rating.
/// <para>
/// Every thumb press sends a <see cref="FeedbackStage.Rating"/> submission and the panel sends a
/// <see cref="FeedbackStage.Details"/> one, through <see cref="FeedbackHandler"/> (a task that throws shows the inline error
/// and Try again; its cancellation token fires when the panel closes or the rating changes) and
/// <see cref="FeedbackSubmitted"/>. Without a handler the sends are simulated and the control says so.
/// </para>
/// </summary>
[TemplatePart(UpPart, typeof(ToggleButton))]
[TemplatePart(DownPart, typeof(ToggleButton))]
[TemplatePart(PanelPart, typeof(Control))]
[TemplatePart(SwapPart, typeof(ArcContentSwap))]
[TemplatePart(RowErrorPart, typeof(Control))]
[TemplatePart(RowErrorTextPart, typeof(TextBlock))]
[TemplatePart(RowRetryPart, typeof(AvaloniaButton))]
[TemplatePart(LivePart, typeof(ExplainerAnnouncer))]
[PseudoClasses(OpenPseudoClass, NarrowPseudoClass)]
public class FeedbackActions : TemplatedControl
{
    internal const string UpPart = "PART_Up";
    internal const string DownPart = "PART_Down";
    internal const string PanelPart = "PART_Panel";
    internal const string SwapPart = "PART_Swap";
    internal const string RowErrorPart = "PART_RowError";
    internal const string RowErrorTextPart = "PART_RowErrorText";
    internal const string RowRetryPart = "PART_RowRetry";
    internal const string LivePart = "PART_Live";
    internal const string OpenPseudoClass = ":open";
    internal const string NarrowPseudoClass = ":narrow";

    /// <summary>Below this width the privacy note and Cancel give way.</summary>
    internal const double NarrowWidth = 400;

    /// <summary>How long a simulated details send takes (no <see cref="FeedbackHandler"/>).</summary>
    internal static TimeSpan SimulatedDelay { get; set; } = TimeSpan.FromMilliseconds(700);

    /// <summary>The negative reasons offered by default.</summary>
    public static IReadOnlyList<string> DefaultNegativeReasons { get; } = ["Inaccurate", "Not helpful", "Too long", "Unsafe", "Other"];

    /// <summary>The positive reasons offered by default.</summary>
    public static IReadOnlyList<string> DefaultPositiveReasons { get; } = ["Accurate", "Easy to follow", "Good examples", "Saved me time"];

    /// <summary>Actions rendered before the thumbs, such as copy or regenerate. Use 16px icons in small (36px) buttons to match.</summary>
    public static readonly StyledProperty<object?> LeadingProperty =
        AvaloniaProperty.Register<FeedbackActions, object?>(nameof(Leading));

    /// <summary>Content pinned to the end of the row, such as a timestamp or a token count.</summary>
    public static readonly StyledProperty<object?> TrailingProperty =
        AvaloniaProperty.Register<FeedbackActions, object?>(nameof(Trailing));

    /// <summary>Passed back on every submission so feedback can be stored against a message.</summary>
    public static readonly StyledProperty<string?> MessageIdProperty =
        AvaloniaProperty.Register<FeedbackActions, string?>(nameof(MessageId));

    /// <summary>
    /// The rating (two-way). Set it to show a rating loaded from your database; bind it to own the state. A thumb press sets
    /// it and pressing the active thumb sets <see cref="FeedbackRating.None"/>.
    /// </summary>
    public static readonly StyledProperty<FeedbackRating> RatingProperty =
        AvaloniaProperty.Register<FeedbackActions, FeedbackRating>(nameof(Rating), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Persists feedback: called with a <see cref="FeedbackStage.Rating"/> submission on every thumb press and a
    /// <see cref="FeedbackStage.Details"/> one when reasons or a note are submitted. Throw (or fault the task) to show the
    /// inline error and Try again; the token is cancelled when the panel closes or the rating changes. Null simulates sends.
    /// </summary>
    public static readonly StyledProperty<Func<FeedbackSubmission, CancellationToken, Task>?> FeedbackHandlerProperty =
        AvaloniaProperty.Register<FeedbackActions, Func<FeedbackSubmission, CancellationToken, Task>?>(nameof(FeedbackHandler));

    /// <summary>Reason chips offered after a thumbs down. A chip labelled Other moves focus to the note and changes its placeholder.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> NegativeReasonsProperty =
        AvaloniaProperty.Register<FeedbackActions, IReadOnlyList<string>?>(nameof(NegativeReasons), DefaultNegativeReasons);

    /// <summary>Chips offered after a thumbs up. An empty list skips the follow-up row.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> PositiveReasonsProperty =
        AvaloniaProperty.Register<FeedbackActions, IReadOnlyList<string>?>(nameof(PositiveReasons), DefaultPositiveReasons);

    /// <summary>Shows the optional note field after a thumbs down.</summary>
    public static readonly StyledProperty<bool> AllowCommentProperty =
        AvaloniaProperty.Register<FeedbackActions, bool>(nameof(AllowComment), true);

    /// <summary>The note field's placeholder.</summary>
    public static readonly StyledProperty<string?> CommentPlaceholderProperty =
        AvaloniaProperty.Register<FeedbackActions, string?>(nameof(CommentPlaceholder), "Add details (optional)");

    /// <summary>The note's maximum length in characters.</summary>
    public static readonly StyledProperty<int> CommentMaxLengthProperty =
        AvaloniaProperty.Register<FeedbackActions, int>(nameof(CommentMaxLength), 1000);

    /// <summary>A quiet line next to Submit about what gets shared. Hidden below 400px of width; null hides it.</summary>
    public static readonly StyledProperty<string?> PrivacyNoteProperty =
        AvaloniaProperty.Register<FeedbackActions, string?>(nameof(PrivacyNote), "Shares this reply for review");

    /// <summary>
    /// The second line of the thank-you state. Null reads "It helps improve future replies." with a
    /// <see cref="FeedbackHandler"/>, and says nothing was sent without one.
    /// </summary>
    public static readonly StyledProperty<string?> ThanksMessageProperty =
        AvaloniaProperty.Register<FeedbackActions, string?>(nameof(ThanksMessage));

    /// <summary>How long the thank-you state holds before the panel folds away.</summary>
    public static readonly StyledProperty<TimeSpan> ThanksDurationProperty =
        AvaloniaProperty.Register<FeedbackActions, TimeSpan>(nameof(ThanksDuration), TimeSpan.FromMilliseconds(1600));

    /// <summary>Whether the thumbs can be pressed; turn it off while the reply is still streaming.</summary>
    public static readonly StyledProperty<bool> IsRatingEnabledProperty =
        AvaloniaProperty.Register<FeedbackActions, bool>(nameof(IsRatingEnabled), true);

    /// <summary>Every word the control shows or announces.</summary>
    public static readonly StyledProperty<ResponseFeedbackLabels> LabelsProperty =
        AvaloniaProperty.Register<FeedbackActions, ResponseFeedbackLabels>(nameof(Labels), ResponseFeedbackLabels.Default);

    /// <summary>Raised when a thumb press changes the rating, including clearing it. Bubbles.</summary>
    public static readonly RoutedEvent<FeedbackRatingChangedEventArgs> RatingChangedEvent =
        RoutedEvent.Register<FeedbackActions, FeedbackRatingChangedEventArgs>(nameof(RatingChanged), RoutingStrategies.Bubble);

    /// <summary>Raised for every submission, before <see cref="FeedbackHandler"/> runs (and when the send is simulated). Bubbles.</summary>
    public static readonly RoutedEvent<FeedbackSubmittedEventArgs> FeedbackSubmittedEvent =
        RoutedEvent.Register<FeedbackActions, FeedbackSubmittedEventArgs>(nameof(FeedbackSubmitted), RoutingStrategies.Bubble);

    private enum PanelPhase
    {
        Form,
        Sending,
        Thanks
    }

    private readonly FeedbackForm _form = new();
    private readonly FeedbackThanks _thanks = new();
    private ToggleButton? _up;
    private ToggleButton? _down;
    private AiHeightBox? _panel;
    private ArcContentSwap? _swap;
    private Control? _rowError;
    private TextBlock? _rowErrorText;
    private AvaloniaButton? _rowRetry;
    private ExplainerAnnouncer? _live;
    private FeedbackRating _panelFor;
    private PanelPhase _phase;
    private ToggleButton? _opener;
    private CancellationTokenSource? _ratingSend;
    private CancellationTokenSource? _detailsSend;
    private DispatcherTimer? _thanksTimer;
    private bool _keyboard;
    private bool _coarse;
    private bool _ratingFailed;
    private bool _focusThanks;

    /// <summary>Creates the control.</summary>
    public FeedbackActions()
    {
        SpecialFocusModality.Watch();
        _form.PrimaryRequested += (_, _) => SubmitDetails();
        _form.SecondaryRequested += (_, _) => ClosePanel();
        _form.CloseRequested += (_, _) => ClosePanel();
        AddHandler(KeyDownEvent, OnTunnelKeyDown, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnTunnelPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <inheritdoc cref="LeadingProperty"/>
    public object? Leading { get => GetValue(LeadingProperty); set => SetValue(LeadingProperty, value); }

    /// <inheritdoc cref="TrailingProperty"/>
    public object? Trailing { get => GetValue(TrailingProperty); set => SetValue(TrailingProperty, value); }

    /// <inheritdoc cref="MessageIdProperty"/>
    public string? MessageId { get => GetValue(MessageIdProperty); set => SetValue(MessageIdProperty, value); }

    /// <inheritdoc cref="RatingProperty"/>
    public FeedbackRating Rating { get => GetValue(RatingProperty); set => SetValue(RatingProperty, value); }

    /// <inheritdoc cref="FeedbackHandlerProperty"/>
    public Func<FeedbackSubmission, CancellationToken, Task>? FeedbackHandler
    {
        get => GetValue(FeedbackHandlerProperty);
        set => SetValue(FeedbackHandlerProperty, value);
    }

    /// <inheritdoc cref="NegativeReasonsProperty"/>
    public IReadOnlyList<string>? NegativeReasons { get => GetValue(NegativeReasonsProperty); set => SetValue(NegativeReasonsProperty, value); }

    /// <inheritdoc cref="PositiveReasonsProperty"/>
    public IReadOnlyList<string>? PositiveReasons { get => GetValue(PositiveReasonsProperty); set => SetValue(PositiveReasonsProperty, value); }

    /// <inheritdoc cref="AllowCommentProperty"/>
    public bool AllowComment { get => GetValue(AllowCommentProperty); set => SetValue(AllowCommentProperty, value); }

    /// <inheritdoc cref="CommentPlaceholderProperty"/>
    public string? CommentPlaceholder { get => GetValue(CommentPlaceholderProperty); set => SetValue(CommentPlaceholderProperty, value); }

    /// <inheritdoc cref="CommentMaxLengthProperty"/>
    public int CommentMaxLength { get => GetValue(CommentMaxLengthProperty); set => SetValue(CommentMaxLengthProperty, value); }

    /// <inheritdoc cref="PrivacyNoteProperty"/>
    public string? PrivacyNote { get => GetValue(PrivacyNoteProperty); set => SetValue(PrivacyNoteProperty, value); }

    /// <inheritdoc cref="ThanksMessageProperty"/>
    public string? ThanksMessage { get => GetValue(ThanksMessageProperty); set => SetValue(ThanksMessageProperty, value); }

    /// <inheritdoc cref="ThanksDurationProperty"/>
    public TimeSpan ThanksDuration { get => GetValue(ThanksDurationProperty); set => SetValue(ThanksDurationProperty, value); }

    /// <inheritdoc cref="IsRatingEnabledProperty"/>
    public bool IsRatingEnabled { get => GetValue(IsRatingEnabledProperty); set => SetValue(IsRatingEnabledProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ResponseFeedbackLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="RatingChangedEvent"/>
    public event EventHandler<FeedbackRatingChangedEventArgs>? RatingChanged
    {
        add => AddHandler(RatingChangedEvent, value);
        remove => RemoveHandler(RatingChangedEvent, value);
    }

    /// <inheritdoc cref="FeedbackSubmittedEvent"/>
    public event EventHandler<FeedbackSubmittedEventArgs>? FeedbackSubmitted
    {
        add => AddHandler(FeedbackSubmittedEvent, value);
        remove => RemoveHandler(FeedbackSubmittedEvent, value);
    }

    /// <summary>Whether the panel (reasons, the What was good row or the thank-you state) is open.</summary>
    public bool IsPanelOpen => _panelFor != FeedbackRating.None;

    internal FeedbackForm Form => _form;

    internal FeedbackThanks Thanks => _thanks;

    internal AiHeightBox? PanelBox => _panel;

    internal ToggleButton? UpThumb => _up;

    internal ToggleButton? DownThumb => _down;

    internal bool IsShowingThanks => _phase == PanelPhase.Thanks && IsPanelOpen;

    internal bool IsSending => _phase == PanelPhase.Sending;

    internal bool IsRatingErrorShown => _ratingFailed;

    /// <summary>The last text given to the polite live region.</summary>
    internal string? Announcement => _live?.Announcement;

    /// <summary>Closes the panel without sending; the rating stays. Focus inside the panel returns to the thumb that opened it.</summary>
    public void ClosePanel()
    {
        if (!IsPanelOpen)
        {
            return;
        }
        var returnFocus = _panel?.IsKeyboardFocusWithin == true || _thanks.IsFocused;
        _panelFor = FeedbackRating.None;
        CancelDetails();
        StopThanksTimer();
        if (_phase == PanelPhase.Sending)
        {
            _phase = PanelPhase.Form;
        }
        if (_panel is not null)
        {
            _panel.IsExpanded = false;
        }
        PseudoClasses.Set(OpenPseudoClass, false);
        UpdateThumbs();
        if (returnFocus)
        {
            // A close clicked with the mouse hands focus back without the keyboard ring.
            _opener?.Focus(SpecialFocusModality.For(this));
        }
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_up is not null)
        {
            _up.Click -= OnUp;
        }
        if (_down is not null)
        {
            _down.Click -= OnDown;
        }
        if (_rowRetry is not null)
        {
            _rowRetry.Click -= OnRowRetry;
        }
        if (_panel is not null)
        {
            _panel.Content = null;
        }
        if (_swap is not null)
        {
            _swap.Content = null;
        }
        _up = e.NameScope.Find<ToggleButton>(UpPart);
        _down = e.NameScope.Find<ToggleButton>(DownPart);
        _panel = e.NameScope.Find<AiHeightBox>(PanelPart);
        if (_panel is not null)
        {
            _panel.HeightSpring = AiHeightBox.PanelSpring;
        }
        _swap = e.NameScope.Find<ArcContentSwap>(SwapPart);
        _rowError = e.NameScope.Find<Control>(RowErrorPart);
        _rowErrorText = e.NameScope.Find<TextBlock>(RowErrorTextPart);
        _rowRetry = e.NameScope.Find<AvaloniaButton>(RowRetryPart);
        _live = e.NameScope.Find<ExplainerAnnouncer>(LivePart);
        if (_up is not null)
        {
            _up.Click += OnUp;
        }
        if (_down is not null)
        {
            _down.Click += OnDown;
        }
        if (_rowRetry is not null)
        {
            _rowRetry.Click += OnRowRetry;
        }
        if (_swap is not null)
        {
            _swap.Content = _phase == PanelPhase.Thanks ? _thanks : _form;
        }
        if (_panel is not null && _panel.IsExpanded != IsPanelOpen)
        {
            _panel.IsExpanded = IsPanelOpen;
        }
        SyncForm();
        UpdateThumbs();
        UpdateRowError();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RatingProperty)
        {
            // A rating set from outside closes a panel that belongs to another rating.
            if (IsPanelOpen && Rating != _panelFor)
            {
                ClosePanel();
            }
            UpdateThumbs();
        }
        else if (change.Property == IsRatingEnabledProperty || change.Property == LabelsProperty)
        {
            UpdateThumbs();
            SyncForm();
            UpdateRowError();
        }
        else if (change.Property == NegativeReasonsProperty || change.Property == PositiveReasonsProperty
                 || change.Property == AllowCommentProperty || change.Property == CommentPlaceholderProperty
                 || change.Property == CommentMaxLengthProperty || change.Property == PrivacyNoteProperty)
        {
            SyncForm();
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var narrow = e.NewSize.Width < NarrowWidth;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        _form.IsNarrow = narrow;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // No timers or sends outlive the control.
        StopThanksTimer();
        CancelDetails();
        _ratingSend?.Cancel();
        _ratingSend = null;
        if (_phase == PanelPhase.Thanks && IsPanelOpen)
        {
            _panelFor = FeedbackRating.None;
            PseudoClasses.Set(OpenPseudoClass, false);
            if (_panel is not null)
            {
                _panel.IsExpanded = false;
            }
        }
        else if (_phase == PanelPhase.Sending)
        {
            _phase = PanelPhase.Form;
            _form.IsSending = false;
        }
    }

    private void OnTunnelKeyDown(object? sender, KeyEventArgs e)
    {
        _keyboard = true;
        if (e.Key == Key.Escape && IsPanelOpen)
        {
            e.Handled = true;
            var opener = _opener;
            ClosePanel();
            opener?.Focus(NavigationMethod.Tab);
        }
    }

    private void OnTunnelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _keyboard = false;
        var coarse = e.Pointer.Type == PointerType.Touch;
        if (coarse != _coarse)
        {
            _coarse = coarse;
            _form.IsCoarse = coarse;
        }
    }

    private void OnUp(object? sender, RoutedEventArgs e) => Press(FeedbackRating.Up, _up);

    private void OnDown(object? sender, RoutedEventArgs e) => Press(FeedbackRating.Down, _down);

    /// <summary>A thumb press: toggles the rating, opens or closes the panel and sends the rating.</summary>
    private void Press(FeedbackRating thumb, ToggleButton? source)
    {
        if (!IsRatingEnabled)
        {
            return;
        }
        var keyboard = _keyboard;
        var old = Rating;
        var rating = old == thumb ? FeedbackRating.None : thumb;
        // The open panel belongs to the old rating: close it first (its send is aborted).
        if (IsPanelOpen)
        {
            ClosePanel();
        }
        SetCurrentValue(RatingProperty, rating);
        UpdateThumbs();
        RaiseEvent(new FeedbackRatingChangedEventArgs(RatingChangedEvent, old, rating));
        if (rating == FeedbackRating.Down)
        {
            OpenPanel(FeedbackRating.Down, source, keyboard);
        }
        else if (rating == FeedbackRating.Up && PositiveReasons is { Count: > 0 })
        {
            OpenPanel(FeedbackRating.Up, source, keyboard);
        }
        SendRating(rating);
    }

    private void OpenPanel(FeedbackRating rating, ToggleButton? opener, bool focusFirst)
    {
        StopThanksTimer();
        _panelFor = rating;
        _opener = opener;
        _phase = PanelPhase.Form;
        if (_swap is not null)
        {
            _swap.Content = _form;
        }
        SyncForm();
        _form.Reset();
        PseudoClasses.Set(OpenPseudoClass, true);
        UpdateThumbs();
        if (_panel is not null)
        {
            _panel.IsExpanded = true;
        }
        if (focusFirst)
        {
            // A keyboard press moves focus to the first reason; a pointer press leaves it where it is.
            Dispatcher.UIThread.Post(() =>
            {
                if (_panelFor == rating && _phase == PanelPhase.Form)
                {
                    _form.FocusFirst();
                }
            }, DispatcherPriority.Loaded);
        }
    }

    private void SyncForm()
    {
        var labels = Labels;
        var negative = _panelFor != FeedbackRating.Up;
        _form.Labels = labels;
        _form.Title = negative ? labels.NegativeTitle : labels.PositiveTitle;
        _form.Reasons = negative ? NegativeReasons : PositiveReasons;
        _form.ShowsNote = negative && AllowComment;
        _form.NotePlaceholder = CommentPlaceholder;
        _form.NoteMaxLength = CommentMaxLength;
        _form.Footnote = negative ? PrivacyNote : null;
        _form.PrimaryLabel = negative ? labels.Submit : labels.Send;
        _form.SecondaryLabel = negative ? labels.Cancel : labels.Skip;
        _form.SecondaryCollapses = negative;
        _thanks.Title = labels.Thanks;
    }

    private void UpdateThumbs()
    {
        var labels = Labels;
        if (_up is not null)
        {
            _up.IsChecked = Rating == FeedbackRating.Up;
            _up.IsEnabled = IsRatingEnabled;
            AutomationProperties.SetName(_up, labels.GoodResponse);
            Tooltip.SetTip(_up, labels.GoodResponse);
        }
        if (_down is not null)
        {
            _down.IsChecked = Rating == FeedbackRating.Down;
            _down.IsEnabled = IsRatingEnabled;
            AutomationProperties.SetName(_down, labels.BadResponse);
            Tooltip.SetTip(_down, labels.BadResponse);
        }
    }

    private void UpdateRowError()
    {
        if (_rowError is not null)
        {
            _rowError.IsVisible = _ratingFailed;
        }
        if (_rowErrorText is not null)
        {
            _rowErrorText.Text = Labels.RatingFailed;
        }
        if (_rowRetry is not null)
        {
            _rowRetry.Content = Labels.TryAgain;
        }
    }

    private void Announce(string text)
    {
        // A text block's name only changed (and was read) when the text did; keep that.
        if (_live is not null && text != _live.Announcement)
        {
            _live.Announce(text);
        }
    }

    private void OnRowRetry(object? sender, RoutedEventArgs e) => SendRating(Rating);

    private async void SendRating(FeedbackRating rating)
    {
        _ratingSend?.Cancel();
        var cts = new CancellationTokenSource();
        _ratingSend = cts;
        _ratingFailed = false;
        UpdateRowError();
        var handler = FeedbackHandler;
        var submission = new FeedbackSubmission(MessageId, FeedbackStage.Rating, rating, [], null);
        RaiseEvent(new FeedbackSubmittedEventArgs(FeedbackSubmittedEvent, submission, handler is null));
        var labels = Labels;
        var said = rating switch
        {
            FeedbackRating.Up => labels.RatedUp,
            FeedbackRating.Down => labels.RatedDown,
            _ => labels.RatingCleared
        };
        Announce(handler is null ? said + ". " + labels.Simulated : said);
        if (handler is null)
        {
            return;
        }
        try
        {
            await handler(submission, cts.Token);
        }
        catch (Exception) when (cts.IsCancellationRequested)
        {
            return;
        }
        catch (Exception)
        {
            if (ReferenceEquals(_ratingSend, cts))
            {
                _ratingFailed = true;
                UpdateRowError();
                Announce(labels.RatingFailed);
            }
            return;
        }
        if (ReferenceEquals(_ratingSend, cts))
        {
            _ratingSend = null;
        }
    }

    private void CancelDetails()
    {
        _detailsSend?.Cancel();
        _detailsSend = null;
        _form.IsSending = false;
    }

    /// <summary>Sends the reasons and the note; on success shows the thank-you state, on failure the error and Try again.</summary>
    private async void SubmitDetails()
    {
        if (!IsPanelOpen || _phase != PanelPhase.Form || !_form.CanSubmit)
        {
            return;
        }
        var keyboard = _keyboard;
        var rating = _panelFor;
        var cts = new CancellationTokenSource();
        _detailsSend = cts;
        _phase = PanelPhase.Sending;
        _form.IsSending = true;
        var labels = Labels;
        Announce(labels.Sending);
        var handler = FeedbackHandler;
        var submission = new FeedbackSubmission(MessageId, FeedbackStage.Details, rating, _form.SelectedReasons, _form.Note);
        RaiseEvent(new FeedbackSubmittedEventArgs(FeedbackSubmittedEvent, submission, handler is null));
        try
        {
            if (handler is null)
            {
                await Task.Delay(SimulatedDelay, cts.Token);
            }
            else
            {
                await handler(submission, cts.Token);
            }
        }
        catch (Exception) when (cts.IsCancellationRequested)
        {
            return;
        }
        catch (Exception)
        {
            if (ReferenceEquals(_detailsSend, cts))
            {
                _detailsSend = null;
                _phase = PanelPhase.Form;
                _form.IsSending = false;
                _form.ErrorText = labels.SendFailed;
                Announce(labels.SendFailed);
            }
            return;
        }
        if (!ReferenceEquals(_detailsSend, cts) || cts.IsCancellationRequested)
        {
            return;
        }
        _detailsSend = null;
        _form.IsSending = false;
        ShowThanks(handler is null, keyboard);
    }

    private void ShowThanks(bool simulated, bool keyboard)
    {
        var labels = Labels;
        _phase = PanelPhase.Thanks;
        _thanks.Title = labels.Thanks;
        _thanks.Message = ThanksMessage ?? (simulated ? labels.SimulatedThanks : labels.ThanksMessage);
        // Keep focus inside the panel through the swap: a focused Submit that leaves the tree would drop it to the window.
        _focusThanks = keyboard || _form.IsKeyboardFocusWithin;
        if (_swap is not null)
        {
            _swap.Content = _thanks;
        }
        _thanks.PlayTick();
        Announce(labels.Thanks + ". " + _thanks.Message);
        if (_focusThanks)
        {
            // Submit pressed with a key keeps the keyboard ring; clicked, the thanks take focus without it.
            var method = keyboard ? NavigationMethod.Tab : SpecialFocusModality.For(this);
            _thanks.Focus(method);
            Dispatcher.UIThread.Post(() =>
            {
                if (IsShowingThanks && !_thanks.IsFocused)
                {
                    _thanks.Focus(method);
                }
            }, DispatcherPriority.Loaded);
        }
        StopThanksTimer();
        _thanksTimer = new DispatcherTimer { Interval = ThanksDuration > TimeSpan.Zero ? ThanksDuration : TimeSpan.FromMilliseconds(1) };
        _thanksTimer.Tick += OnThanksElapsed;
        _thanksTimer.Start();
    }

    private void OnThanksElapsed(object? sender, EventArgs e)
    {
        StopThanksTimer();
        if (IsShowingThanks)
        {
            ClosePanel();
        }
    }

    private void StopThanksTimer()
    {
        if (_thanksTimer is not null)
        {
            _thanksTimer.Stop();
            _thanksTimer.Tick -= OnThanksElapsed;
            _thanksTimer = null;
        }
    }
}
