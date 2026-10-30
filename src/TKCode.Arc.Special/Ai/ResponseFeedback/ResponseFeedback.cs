using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// An AI reply card with its feedback (Arc Response feedback, design reference): the person's message in a bubble, the
/// model's mark and name, the reply and an action row with copy, an optional regenerate and the thumbs of
/// <see cref="FeedbackActions"/>. Thumbs down opens an inline panel with reason chips and an optional note, thumbs up
/// fills and asks what was good, and a submit shows a short thank-you before the panel folds back into the row.
/// <para>
/// With no data it shows the frame alone: the model's mark, an empty reply and the action row, whose feedback is
/// simulated until you set a <see cref="FeedbackHandler"/>. Every property of <see cref="FeedbackActions"/> except <see cref="FeedbackActions.Leading"/> is available here.
/// </para>
/// </summary>
[TemplatePart(PromptPart, typeof(Control))]
[TemplatePart(MarkPart, typeof(ArcGlyph))]
[TemplatePart(BodyPart, typeof(ContentPresenter))]
[TemplatePart(ActionsPart, typeof(FeedbackActions))]
[TemplatePart(CopyPart, typeof(CopyButton))]
[TemplatePart(RegeneratePart, typeof(Avalonia.Controls.Button))]
[PseudoClasses(CompactPseudoClass)]
public class ResponseFeedback : TemplatedControl
{
    internal const string PromptPart = "PART_Prompt";
    internal const string MarkPart = "PART_Mark";
    internal const string BodyPart = "PART_Body";
    internal const string ActionsPart = "PART_Actions";
    internal const string CopyPart = "PART_Copy";
    internal const string RegeneratePart = "PART_Regenerate";
    internal const string CompactPseudoClass = ":compact";

    /// <summary>Below this width the card pads 16px instead of 24.</summary>
    internal const double CompactWidth = 480;

    /// <summary>The name of the model that wrote the reply; null (the default) shows the mark alone.</summary>
    public static readonly StyledProperty<string?> ModelNameProperty =
        AvaloniaProperty.Register<ResponseFeedback, string?>(nameof(ModelName));

    /// <summary>The model's mark, a 24 × 24 stroked glyph drawn at 16px; a neutral sparkle by default.</summary>
    public static readonly StyledProperty<Geometry?> ModelMarkProperty =
        AvaloniaProperty.Register<ResponseFeedback, Geometry?>(nameof(ModelMark), ArcIcons.Sparkles);

    /// <summary>The person's message shown above the reply; null (the default) hides it.</summary>
    public static readonly StyledProperty<object?> PromptProperty =
        AvaloniaProperty.Register<ResponseFeedback, object?>(nameof(Prompt));

    /// <summary>The reply body: text or any control. Null leaves the reply empty.</summary>
    public static readonly StyledProperty<object?> ReplyProperty =
        AvaloniaProperty.Register<ResponseFeedback, object?>(nameof(Reply));

    /// <summary>Plain text for the copy action; null (the default) hides copy.</summary>
    public static readonly StyledProperty<string?> CopyTextProperty =
        AvaloniaProperty.Register<ResponseFeedback, string?>(nameof(CopyText));

    /// <summary>Shows a regenerate action in the row; it raises <see cref="RegenerateRequested"/>.</summary>
    public static readonly StyledProperty<bool> AllowsRegenerateProperty =
        AvaloniaProperty.Register<ResponseFeedback, bool>(nameof(AllowsRegenerate));

    /// <summary>Content pinned to the end of the row, such as a timestamp or a token count.</summary>
    public static readonly StyledProperty<object?> TrailingProperty = FeedbackActions.TrailingProperty.AddOwner<ResponseFeedback>();

    /// <summary>Passed back on every submission so feedback can be stored against a message.</summary>
    public static readonly StyledProperty<string?> MessageIdProperty = FeedbackActions.MessageIdProperty.AddOwner<ResponseFeedback>();

    /// <summary>
    /// The rating (two-way). Set it to show a rating loaded from your database; bind it to own the state. A thumb press sets
    /// it and pressing the active thumb sets <see cref="FeedbackRating.None"/>.
    /// </summary>
    public static readonly StyledProperty<FeedbackRating> RatingProperty = FeedbackActions.RatingProperty.AddOwner<ResponseFeedback>();

    /// <summary>
    /// Persists feedback: called with a <see cref="FeedbackStage.Rating"/> submission on every thumb press and a
    /// <see cref="FeedbackStage.Details"/> one when reasons or a note are submitted. Throw (or fault the task) to show the
    /// inline error and Try again; the token is cancelled when the panel closes or the rating changes. Null simulates sends.
    /// </summary>
    public static readonly StyledProperty<Func<FeedbackSubmission, CancellationToken, Task>?> FeedbackHandlerProperty =
        FeedbackActions.FeedbackHandlerProperty.AddOwner<ResponseFeedback>();

    /// <summary>Reason chips offered after a thumbs down. A chip labelled Other moves focus to the note and changes its placeholder.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> NegativeReasonsProperty =
        FeedbackActions.NegativeReasonsProperty.AddOwner<ResponseFeedback>();

    /// <summary>Chips offered after a thumbs up. An empty list skips the follow-up row.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> PositiveReasonsProperty =
        FeedbackActions.PositiveReasonsProperty.AddOwner<ResponseFeedback>();

    /// <summary>Shows the optional note field after a thumbs down.</summary>
    public static readonly StyledProperty<bool> AllowCommentProperty = FeedbackActions.AllowCommentProperty.AddOwner<ResponseFeedback>();

    /// <summary>The note field's placeholder.</summary>
    public static readonly StyledProperty<string?> CommentPlaceholderProperty =
        FeedbackActions.CommentPlaceholderProperty.AddOwner<ResponseFeedback>();

    /// <summary>The note's maximum length in characters.</summary>
    public static readonly StyledProperty<int> CommentMaxLengthProperty = FeedbackActions.CommentMaxLengthProperty.AddOwner<ResponseFeedback>();

    /// <summary>A quiet line next to Submit about what gets shared. Hidden below 400px of width; null hides it.</summary>
    public static readonly StyledProperty<string?> PrivacyNoteProperty = FeedbackActions.PrivacyNoteProperty.AddOwner<ResponseFeedback>();

    /// <summary>
    /// The second line of the thank-you state. Null reads "It helps improve future replies." with a
    /// <see cref="FeedbackHandler"/>, and says nothing was sent without one.
    /// </summary>
    public static readonly StyledProperty<string?> ThanksMessageProperty = FeedbackActions.ThanksMessageProperty.AddOwner<ResponseFeedback>();

    /// <summary>How long the thank-you state holds before the panel folds away.</summary>
    public static readonly StyledProperty<TimeSpan> ThanksDurationProperty = FeedbackActions.ThanksDurationProperty.AddOwner<ResponseFeedback>();

    /// <summary>Whether the thumbs can be pressed; turn it off while the reply is still streaming.</summary>
    public static readonly StyledProperty<bool> IsRatingEnabledProperty = FeedbackActions.IsRatingEnabledProperty.AddOwner<ResponseFeedback>();

    /// <summary>Every word the control shows or announces.</summary>
    public static readonly StyledProperty<ResponseFeedbackLabels> LabelsProperty = FeedbackActions.LabelsProperty.AddOwner<ResponseFeedback>();

    /// <summary>Raised when the person presses regenerate. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RegenerateRequestedEvent =
        RoutedEvent.Register<ResponseFeedback, RoutedEventArgs>(nameof(RegenerateRequested), RoutingStrategies.Bubble);

    private Control? _prompt;
    private ArcGlyph? _mark;
    private ContentPresenter? _body;
    private FeedbackActions? _actions;
    private CopyButton? _copy;
    private Avalonia.Controls.Button? _regenerate;

    /// <inheritdoc cref="ModelNameProperty"/>
    public string? ModelName { get => GetValue(ModelNameProperty); set => SetValue(ModelNameProperty, value); }

    /// <inheritdoc cref="ModelMarkProperty"/>
    public Geometry? ModelMark { get => GetValue(ModelMarkProperty); set => SetValue(ModelMarkProperty, value); }

    /// <inheritdoc cref="PromptProperty"/>
    public object? Prompt { get => GetValue(PromptProperty); set => SetValue(PromptProperty, value); }

    /// <inheritdoc cref="ReplyProperty"/>
    [Content]
    public object? Reply { get => GetValue(ReplyProperty); set => SetValue(ReplyProperty, value); }

    /// <inheritdoc cref="CopyTextProperty"/>
    public string? CopyText { get => GetValue(CopyTextProperty); set => SetValue(CopyTextProperty, value); }

    /// <inheritdoc cref="AllowsRegenerateProperty"/>
    public bool AllowsRegenerate { get => GetValue(AllowsRegenerateProperty); set => SetValue(AllowsRegenerateProperty, value); }

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

    /// <inheritdoc cref="RegenerateRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? RegenerateRequested
    {
        add => AddHandler(RegenerateRequestedEvent, value);
        remove => RemoveHandler(RegenerateRequestedEvent, value);
    }

    /// <summary>Raised when a thumb press changes the rating (the event of the inner <see cref="FeedbackActions"/>, which bubbles here).</summary>
    public event EventHandler<FeedbackRatingChangedEventArgs>? RatingChanged
    {
        add => AddHandler(FeedbackActions.RatingChangedEvent, value);
        remove => RemoveHandler(FeedbackActions.RatingChangedEvent, value);
    }

    /// <summary>Raised for every submission (the event of the inner <see cref="FeedbackActions"/>, which bubbles here).</summary>
    public event EventHandler<FeedbackSubmittedEventArgs>? FeedbackSubmitted
    {
        add => AddHandler(FeedbackActions.FeedbackSubmittedEvent, value);
        remove => RemoveHandler(FeedbackActions.FeedbackSubmittedEvent, value);
    }

    /// <summary>The feedback control of the row (null before the template is applied).</summary>
    public FeedbackActions? Actions => _actions;

    /// <summary>The text the copy action copies, or null when copy is hidden.</summary>
    internal string? EffectiveCopyText => CopyText;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_regenerate is not null)
        {
            _regenerate.Click -= OnRegenerate;
        }
        if (_body is not null)
        {
            _body.Content = null;
        }
        _prompt = e.NameScope.Find<Control>(PromptPart);
        _mark = e.NameScope.Find<ArcGlyph>(MarkPart);
        _body = e.NameScope.Find<ContentPresenter>(BodyPart);
        _actions = e.NameScope.Find<FeedbackActions>(ActionsPart);
        _copy = e.NameScope.Find<CopyButton>(CopyPart);
        _regenerate = e.NameScope.Find<Avalonia.Controls.Button>(RegeneratePart);
        if (_regenerate is not null)
        {
            _regenerate.Click += OnRegenerate;
        }
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ReplyProperty || change.Property == PromptProperty || change.Property == CopyTextProperty
            || change.Property == AllowsRegenerateProperty || change.Property == ModelMarkProperty
            || change.Property == LabelsProperty)
        {
            Refresh();
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        PseudoClasses.Set(CompactPseudoClass, e.NewSize.Width < CompactWidth);
    }

    private void OnRegenerate(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(RegenerateRequestedEvent));

    private void Refresh()
    {
        var labels = Labels;
        if (_prompt is not null)
        {
            _prompt.IsVisible = Prompt is not null;
        }
        if (_mark is not null)
        {
            _mark.Data = ModelMark ?? ArcIcons.Sparkles;
        }
        if (_body is not null)
        {
            var body = Reply;
            if (!ReferenceEquals(_body.Content, body))
            {
                _body.Content = body;
            }
        }
        if (_copy is not null)
        {
            var text = EffectiveCopyText;
            _copy.IsVisible = text is not null;
            _copy.Value = text;
            _copy.Label = labels.CopyReply;
            AutomationProperties.SetName(_copy, labels.CopyReply);
            Tooltip.SetTip(_copy, labels.CopyReply);
        }
        if (_regenerate is not null)
        {
            _regenerate.IsVisible = AllowsRegenerate;
            AutomationProperties.SetName(_regenerate, labels.Regenerate);
            Tooltip.SetTip(_regenerate, labels.Regenerate);
        }
    }
}
