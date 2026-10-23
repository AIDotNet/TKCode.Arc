using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A page that answers one question, written like an article: the reasoning first, then the sources as small cards, then
/// the answer streamed in with headings, lists, a table, code and numbered citations. Pointing at a citation lights the
/// sentence it supports and its source card; pointing at a card lights every sentence that cites it. Under the answer are
/// actions (copy, answer again, rate, share, versions), the model line and related questions, and a box asks a follow-up.
/// Asking again replaces the answer in place: this is one question at a time, not a conversation.
/// <code>
/// &lt;arc:AiAnswer Question="How long does a cold start take?" Responder="{Binding Responder}" /&gt;
/// </code>
/// </summary>
public partial class AiAnswer : TemplatedControl
{
    #region Properties

    /// <summary>The question (two-way). Null or empty with no answer shows the empty page. Setting it asks it when there is a <see cref="Responder"/>.</summary>
    public static readonly StyledProperty<string?> QuestionProperty =
        AvaloniaProperty.Register<AiAnswer, string?>(nameof(Question), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The title of the top bar; null shows the question, or "New thread" on the empty page.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AiAnswer, string?>(nameof(Title));

    /// <summary>A finished answer in Markdown (with "[n]" citations) to show without a responder; it is shown whole, without the word-by-word reveal.</summary>
    public static readonly StyledProperty<string?> AnswerProperty =
        AvaloniaProperty.Register<AiAnswer, string?>(nameof(Answer));

    /// <summary>The reasoning summary of <see cref="Answer"/>, in paragraphs separated by blank lines.</summary>
    public static readonly StyledProperty<string?> ReasoningProperty =
        AvaloniaProperty.Register<AiAnswer, string?>(nameof(Reasoning));

    /// <summary>What <see cref="Answer"/> cost.</summary>
    public static readonly StyledProperty<AiAnswerUsage?> UsageProperty =
        AvaloniaProperty.Register<AiAnswer, AiAnswerUsage?>(nameof(Usage));

    /// <summary>The model; null takes the first of <see cref="Models"/>.</summary>
    public static readonly StyledProperty<AiModel?> ModelProperty =
        AvaloniaProperty.Register<AiAnswer, AiModel?>(nameof(Model), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Answers a question; null shows only <see cref="Answer"/>.</summary>
    public static readonly StyledProperty<IAiAnswerResponder?> ResponderProperty =
        AvaloniaProperty.Register<AiAnswer, IAiAnswerResponder?>(nameof(Responder));

    /// <summary>
    /// Returns the link to copy for "Share thread" (the whole thread) and the link button of the action bar (one answer).
    /// Null hides both buttons: the block never copies a made-up link.
    /// </summary>
    public static readonly StyledProperty<Func<AiAnswerShareInfo, CancellationToken, ValueTask<string>>?> ShareLinkProviderProperty =
        AvaloniaProperty.Register<AiAnswer, Func<AiAnswerShareInfo, CancellationToken, ValueTask<string>>?>(nameof(ShareLinkProvider));

    /// <summary>The text of the follow-up box (two-way).</summary>
    public static readonly StyledProperty<string?> ComposerTextProperty =
        AvaloniaProperty.Register<AiAnswer, string?>(nameof(ComposerText), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The follow-up box grows to this many lines and then scrolls.</summary>
    public static readonly StyledProperty<int> MaxComposerLinesProperty =
        AvaloniaProperty.Register<AiAnswer, int>(nameof(MaxComposerLines), 6);

    /// <summary>Whether the reasoning is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsReasoningExpandedProperty =
        AvaloniaProperty.Register<AiAnswer, bool>(nameof(IsReasoningExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the sources are open (two-way).</summary>
    public static readonly StyledProperty<bool> IsSourcesExpandedProperty =
        AvaloniaProperty.Register<AiAnswer, bool>(nameof(IsSourcesExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How often streamed text is released (50 ms); each step shows a few whole words.</summary>
    public static readonly StyledProperty<TimeSpan> RevealIntervalProperty =
        AvaloniaProperty.Register<AiAnswer, TimeSpan>(nameof(RevealInterval), TimeSpan.FromMilliseconds(50));

    /// <summary>How many whole words each step shows (4: about 80 words a second).</summary>
    public static readonly StyledProperty<int> WordsPerStepProperty =
        AvaloniaProperty.Register<AiAnswer, int>(nameof(WordsPerStep), 4);

    /// <summary>Where the answer is in its life (read-only).</summary>
    public static readonly DirectProperty<AiAnswer, AiAnswerStatus> StatusProperty =
        AvaloniaProperty.RegisterDirect<AiAnswer, AiAnswerStatus>(nameof(Status), o => o.Status);

    /// <summary>Whether the model is thinking or writing (read-only).</summary>
    public static readonly DirectProperty<AiAnswer, bool> IsBusyProperty =
        AvaloniaProperty.RegisterDirect<AiAnswer, bool>(nameof(IsBusy), o => o.IsBusy);

    /// <summary>Why the answer failed (read-only).</summary>
    public static readonly DirectProperty<AiAnswer, string?> ErrorMessageProperty =
        AvaloniaProperty.RegisterDirect<AiAnswer, string?>(nameof(ErrorMessage), o => o.ErrorMessage);

    /// <summary>The shown version (two-way); out of range values are clamped.</summary>
    public static readonly StyledProperty<int> CurrentVersionIndexProperty =
        AvaloniaProperty.Register<AiAnswer, int>(nameof(CurrentVersionIndex), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Runs when the person asks a question (the box, a related question, a suggestion), with the question as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> AskCommandProperty =
        AvaloniaProperty.Register<AiAnswer, ICommand?>(nameof(AskCommand));

    /// <summary>Runs when the person stops the answer (the stop button, Escape in the box).</summary>
    public static readonly StyledProperty<ICommand?> StopCommandProperty =
        AvaloniaProperty.Register<AiAnswer, ICommand?>(nameof(StopCommand));

    /// <summary>Runs when the person answers again, with the <see cref="AiAnswerMode"/> (or the chosen <see cref="AiModel"/> for "Switch model") as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> RegenerateCommandProperty =
        AvaloniaProperty.Register<AiAnswer, ICommand?>(nameof(RegenerateCommand));

    /// <summary>Runs when the person starts a new thread.</summary>
    public static readonly StyledProperty<ICommand?> NewThreadCommandProperty =
        AvaloniaProperty.Register<AiAnswer, ICommand?>(nameof(NewThreadCommand));

    /// <summary>Every word the block shows or announces.</summary>
    public static readonly StyledProperty<AiAnswerLabels> LabelsProperty =
        AvaloniaProperty.Register<AiAnswer, AiAnswerLabels>(nameof(Labels), new AiAnswerLabels());

    #endregion

    #region Events

    /// <summary>Raised before a question is answered; set <c>Cancel</c> to ask nothing.</summary>
    public static readonly RoutedEvent<AiAnswerQuestionEventArgs> QuestionAskedEvent =
        RoutedEvent.Register<AiAnswer, AiAnswerQuestionEventArgs>(nameof(QuestionAsked), RoutingStrategies.Bubble);

    /// <summary>Raised when the person rates an answer, finishes a down rating with a reason, or takes a rating back.</summary>
    public static readonly RoutedEvent<AiAnswerFeedbackEventArgs> FeedbackChangedEvent =
        RoutedEvent.Register<AiAnswer, AiAnswerFeedbackEventArgs>(nameof(FeedbackChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a source card or a citation is pressed; set <c>Handled</c> to open it yourself.</summary>
    public static readonly RoutedEvent<AiAnswerSourceEventArgs> SourceInvokedEvent =
        RoutedEvent.Register<AiAnswer, AiAnswerSourceEventArgs>(nameof(SourceInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised after something was copied.</summary>
    public static readonly RoutedEvent<AiAnswerCopiedEventArgs> CopiedEvent =
        RoutedEvent.Register<AiAnswer, AiAnswerCopiedEventArgs>(nameof(Copied), RoutingStrategies.Bubble);

    /// <summary>Raised when the status changes.</summary>
    public static readonly RoutedEvent<AiAnswerStatusEventArgs> StatusChangedEvent =
        RoutedEvent.Register<AiAnswer, AiAnswerStatusEventArgs>(nameof(StatusChanged), RoutingStrategies.Bubble);

    /// <summary>Raised before a question is answered.</summary>
    public event EventHandler<AiAnswerQuestionEventArgs>? QuestionAsked { add => AddHandler(QuestionAskedEvent, value); remove => RemoveHandler(QuestionAskedEvent, value); }

    /// <summary>Raised when the person rates an answer.</summary>
    public event EventHandler<AiAnswerFeedbackEventArgs>? FeedbackChanged { add => AddHandler(FeedbackChangedEvent, value); remove => RemoveHandler(FeedbackChangedEvent, value); }

    /// <summary>Raised when a source card or a citation is pressed.</summary>
    public event EventHandler<AiAnswerSourceEventArgs>? SourceInvoked { add => AddHandler(SourceInvokedEvent, value); remove => RemoveHandler(SourceInvokedEvent, value); }

    /// <summary>Raised after something was copied.</summary>
    public event EventHandler<AiAnswerCopiedEventArgs>? Copied { add => AddHandler(CopiedEvent, value); remove => RemoveHandler(CopiedEvent, value); }

    /// <summary>Raised when the status changes.</summary>
    public event EventHandler<AiAnswerStatusEventArgs>? StatusChanged { add => AddHandler(StatusChangedEvent, value); remove => RemoveHandler(StatusChangedEvent, value); }

    #endregion

    private AiAnswerStatus _status;
    private bool _isBusy;
    private string? _errorMessage;

    /// <summary>The sources of <see cref="Answer"/>, numbered from 1.</summary>
    public AvaloniaList<AiSource> Sources { get; } = new();

    /// <summary>Questions to ask next, under <see cref="Answer"/>.</summary>
    public AvaloniaList<string> FollowUps { get; } = new();

    /// <summary>The questions of the empty page.</summary>
    public AvaloniaList<string> Suggestions { get; } = new();

    /// <summary>The models of "Switch model", the current one left out. The first is the default model.</summary>
    public AvaloniaList<AiModel> Models { get; } = new();

    /// <summary>The providers of the models, for the marks of models that have none of their own.</summary>
    public AvaloniaList<AiModelProvider> Providers { get; } = new();

    /// <summary>The reasons offered when an answer is rated down; empty uses <see cref="AiAnswerLabels.FeedbackReasons"/>.</summary>
    public AvaloniaList<string> FeedbackReasons { get; } = new();

    /// <summary>Every version of the shown question, in the order they were made (read-only list; the block adds to it).</summary>
    public AvaloniaList<AiAnswerVersion> Versions { get; } = new();

    /// <inheritdoc cref="QuestionProperty"/>
    public string? Question { get => GetValue(QuestionProperty); set => SetValue(QuestionProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="AnswerProperty"/>
    public string? Answer { get => GetValue(AnswerProperty); set => SetValue(AnswerProperty, value); }

    /// <inheritdoc cref="ReasoningProperty"/>
    public string? Reasoning { get => GetValue(ReasoningProperty); set => SetValue(ReasoningProperty, value); }

    /// <inheritdoc cref="UsageProperty"/>
    public AiAnswerUsage? Usage { get => GetValue(UsageProperty); set => SetValue(UsageProperty, value); }

    /// <inheritdoc cref="ModelProperty"/>
    public AiModel? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <inheritdoc cref="ResponderProperty"/>
    public IAiAnswerResponder? Responder { get => GetValue(ResponderProperty); set => SetValue(ResponderProperty, value); }

    /// <inheritdoc cref="ShareLinkProviderProperty"/>
    public Func<AiAnswerShareInfo, CancellationToken, ValueTask<string>>? ShareLinkProvider { get => GetValue(ShareLinkProviderProperty); set => SetValue(ShareLinkProviderProperty, value); }

    /// <inheritdoc cref="ComposerTextProperty"/>
    public string? ComposerText { get => GetValue(ComposerTextProperty); set => SetValue(ComposerTextProperty, value); }

    /// <inheritdoc cref="MaxComposerLinesProperty"/>
    public int MaxComposerLines { get => GetValue(MaxComposerLinesProperty); set => SetValue(MaxComposerLinesProperty, value); }

    /// <inheritdoc cref="IsReasoningExpandedProperty"/>
    public bool IsReasoningExpanded { get => GetValue(IsReasoningExpandedProperty); set => SetValue(IsReasoningExpandedProperty, value); }

    /// <inheritdoc cref="IsSourcesExpandedProperty"/>
    public bool IsSourcesExpanded { get => GetValue(IsSourcesExpandedProperty); set => SetValue(IsSourcesExpandedProperty, value); }

    /// <inheritdoc cref="RevealIntervalProperty"/>
    public TimeSpan RevealInterval { get => GetValue(RevealIntervalProperty); set => SetValue(RevealIntervalProperty, value); }

    /// <inheritdoc cref="WordsPerStepProperty"/>
    public int WordsPerStep { get => GetValue(WordsPerStepProperty); set => SetValue(WordsPerStepProperty, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public AiAnswerStatus Status { get => _status; private set => SetAndRaise(StatusProperty, ref _status, value); }

    /// <inheritdoc cref="IsBusyProperty"/>
    public bool IsBusy { get => _isBusy; private set => SetAndRaise(IsBusyProperty, ref _isBusy, value); }

    /// <inheritdoc cref="ErrorMessageProperty"/>
    public string? ErrorMessage { get => _errorMessage; private set => SetAndRaise(ErrorMessageProperty, ref _errorMessage, value); }

    /// <inheritdoc cref="CurrentVersionIndexProperty"/>
    public int CurrentVersionIndex { get => GetValue(CurrentVersionIndexProperty); set => SetValue(CurrentVersionIndexProperty, value); }

    /// <inheritdoc cref="AskCommandProperty"/>
    public ICommand? AskCommand { get => GetValue(AskCommandProperty); set => SetValue(AskCommandProperty, value); }

    /// <inheritdoc cref="StopCommandProperty"/>
    public ICommand? StopCommand { get => GetValue(StopCommandProperty); set => SetValue(StopCommandProperty, value); }

    /// <inheritdoc cref="RegenerateCommandProperty"/>
    public ICommand? RegenerateCommand { get => GetValue(RegenerateCommandProperty); set => SetValue(RegenerateCommandProperty, value); }

    /// <inheritdoc cref="NewThreadCommandProperty"/>
    public ICommand? NewThreadCommand { get => GetValue(NewThreadCommandProperty); set => SetValue(NewThreadCommandProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AiAnswerLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }
}
