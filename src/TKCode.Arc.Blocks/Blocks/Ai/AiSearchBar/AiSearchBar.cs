using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A search field that answers in plain language: ask a question and an answer with numbered citations appears above
/// the classic results, with the cited sources as cards and questions to ask next. Before anything is asked it offers
/// suggested questions. Answers come from <see cref="AnswerProvider"/>; without one the bar answers from its invented
/// sample handbook (<see cref="AiSearchBarSamples"/>).
/// <code>
/// &lt;arc:AiSearchBar AnswerProvider="{Binding Ask}" SuggestedQuestions="{Binding Questions}" /&gt;
/// </code>
/// Enter asks, Escape cancels a search or clears the field, Down moves from the field to the first suggestion or
/// source. A newer question cancels the one still running.
/// </summary>
[TemplatePart(InputPart, typeof(Input))]
[TemplatePart(SubmitPart, typeof(Button))]
[TemplatePart(AnswerTextPart, typeof(TextBlock))]
[PseudoClasses(IdlePseudoClass, SearchingPseudoClass, AnsweredPseudoClass, FailedPseudoClass, CompactPseudoClass)]
public class AiSearchBar : TemplatedControl
{
    public const string InputPart = "PART_Input";
    public const string SubmitPart = "PART_Submit";
    public const string AnswerTextPart = "PART_AnswerText";

    public const string IdlePseudoClass = ":idle";
    public const string SearchingPseudoClass = ":searching";
    public const string AnsweredPseudoClass = ":answered";
    public const string FailedPseudoClass = ":failed";
    public const string CompactPseudoClass = ":compact";

    #region Properties

    /// <summary>The text in the field (two-way).</summary>
    public static readonly StyledProperty<string?> QueryProperty =
        AvaloniaProperty.Register<AiSearchBar, string?>(nameof(Query), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Questions offered before anything is asked; null or empty hides them.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> SuggestedQuestionsProperty =
        AvaloniaProperty.Register<AiSearchBar, IReadOnlyList<string>?>(nameof(SuggestedQuestions), AiSearchBarSamples.Questions());

    /// <summary>
    /// Answers a question: called with the question and a token that a newer question, <see cref="Cancel"/>, Escape or
    /// the bar leaving the window cancels. An exception shows the error with a retry button. Null answers from the sample.
    /// </summary>
    public static readonly StyledProperty<Func<string, CancellationToken, Task<AiSearchAnswer>>?> AnswerProviderProperty =
        AvaloniaProperty.Register<AiSearchBar, Func<string, CancellationToken, Task<AiSearchAnswer>>?>(nameof(AnswerProvider));

    /// <summary>Every word the bar shows or reads out, with English defaults.</summary>
    public static readonly StyledProperty<AiSearchBarLabels> LabelsProperty =
        AvaloniaProperty.Register<AiSearchBar, AiSearchBarLabels>(nameof(Labels), new AiSearchBarLabels());

    /// <summary>Where the bar stands.</summary>
    public static readonly DirectProperty<AiSearchBar, AiSearchBarState> StateProperty =
        AvaloniaProperty.RegisterDirect<AiSearchBar, AiSearchBarState>(nameof(State), o => o.State);

    /// <summary>The answer shown; null unless <see cref="State"/> is Answered.</summary>
    public static readonly DirectProperty<AiSearchBar, AiSearchAnswer?> AnswerProperty =
        AvaloniaProperty.RegisterDirect<AiSearchBar, AiSearchAnswer?>(nameof(Answer), o => o.Answer);

    /// <summary>The question the shown answer (or the running search) belongs to.</summary>
    public static readonly DirectProperty<AiSearchBar, string?> AskedQueryProperty =
        AvaloniaProperty.RegisterDirect<AiSearchBar, string?>(nameof(AskedQuery), o => o.AskedQuery);

    /// <summary>The error line of the last failed search; null unless <see cref="State"/> is Failed.</summary>
    public static readonly DirectProperty<AiSearchBar, string?> ErrorTextProperty =
        AvaloniaProperty.RegisterDirect<AiSearchBar, string?>(nameof(ErrorText), o => o.ErrorText);

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<AiSearchBar, double>(nameof(CompactWidth), 440);

    /// <inheritdoc cref="QueryProperty"/>
    public string? Query { get => GetValue(QueryProperty); set => SetValue(QueryProperty, value); }

    /// <inheritdoc cref="SuggestedQuestionsProperty"/>
    public IReadOnlyList<string>? SuggestedQuestions { get => GetValue(SuggestedQuestionsProperty); set => SetValue(SuggestedQuestionsProperty, value); }

    /// <inheritdoc cref="AnswerProviderProperty"/>
    public Func<string, CancellationToken, Task<AiSearchAnswer>>? AnswerProvider
    {
        get => GetValue(AnswerProviderProperty);
        set => SetValue(AnswerProviderProperty, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public AiSearchBarLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="StateProperty"/>
    public AiSearchBarState State { get => _state; private set => SetAndRaise(StateProperty, ref _state, value); }

    /// <inheritdoc cref="AnswerProperty"/>
    public AiSearchAnswer? Answer { get => _answer; private set => SetAndRaise(AnswerProperty, ref _answer, value); }

    /// <inheritdoc cref="AskedQueryProperty"/>
    public string? AskedQuery { get => _askedQuery; private set => SetAndRaise(AskedQueryProperty, ref _askedQuery, value); }

    /// <inheritdoc cref="ErrorTextProperty"/>
    public string? ErrorText { get => _errorText; private set => SetAndRaise(ErrorTextProperty, ref _errorText, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    #endregion

    #region Events

    /// <summary>Raised when a question is asked (from the field, a suggestion or code), before it is answered.</summary>
    public static readonly RoutedEvent<AiSearchQueryEventArgs> QuerySubmittedEvent =
        RoutedEvent.Register<AiSearchBar, AiSearchQueryEventArgs>(nameof(QuerySubmitted), RoutingStrategies.Bubble);

    /// <summary>Raised when a cited source is opened (its card or a citation in the answer).</summary>
    public static readonly RoutedEvent<AiSearchSourceEventArgs> SourceInvokedEvent =
        RoutedEvent.Register<AiSearchBar, AiSearchSourceEventArgs>(nameof(SourceInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when a classic result is opened.</summary>
    public static readonly RoutedEvent<AiSearchResultEventArgs> ResultInvokedEvent =
        RoutedEvent.Register<AiSearchBar, AiSearchResultEventArgs>(nameof(ResultInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when a question is asked (from the field, a suggestion or code), before it is answered.</summary>
    public event EventHandler<AiSearchQueryEventArgs>? QuerySubmitted
    {
        add => AddHandler(QuerySubmittedEvent, value);
        remove => RemoveHandler(QuerySubmittedEvent, value);
    }

    /// <summary>Raised when a cited source is opened (its card or a citation in the answer).</summary>
    public event EventHandler<AiSearchSourceEventArgs>? SourceInvoked
    {
        add => AddHandler(SourceInvokedEvent, value);
        remove => RemoveHandler(SourceInvokedEvent, value);
    }

    /// <summary>Raised when a classic result is opened.</summary>
    public event EventHandler<AiSearchResultEventArgs>? ResultInvoked
    {
        add => AddHandler(ResultInvokedEvent, value);
        remove => RemoveHandler(ResultInvokedEvent, value);
    }

    #endregion

    private AiSearchBarState _state;
    private AiSearchAnswer? _answer;
    private string? _askedQuery;
    private string? _errorText;
    private Input? _input;
    private Button? _submit;
    private Button? _clear;
    private Button? _retry;
    private TextBlock? _answerText;
    private Panel? _suggestions;
    private Panel? _sources;
    private Panel? _followUps;
    private Panel? _results;
    private TextShimmer? _searchingText;
    private AiAnnouncer? _announcer;
    private readonly Dictionary<string, TextBlock> _headings = new();
    private CancellationTokenSource? _cts;
    private int _generation;

    static AiSearchBar()
    {
        FocusableProperty.OverrideDefaultValue<AiSearchBar>(false);
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<AiSearchBar>(AutomationLandmarkType.Search);
    }

    public AiSearchBar()
    {
        PseudoClasses.Set(IdlePseudoClass, true);
    }

    /// <summary>Waits the simulated answer time; tests replace it.</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = static (time, token) => Task.Delay(time, token);

    internal Panel? SuggestionsPanel => _suggestions;

    internal Panel? SourcesPanel => _sources;

    internal Panel? ResultsPanel => _results;

    internal Panel? FollowUpsPanel => _followUps;

    internal TextBlock? AnswerTextBlock => _answerText;

    internal Input? Field => _input;

    internal string? Announcement => _announcer?.Announcement;

    #region Public actions

    /// <summary>Asks <paramref name="query"/> (or the field's text): puts it in the field and answers it.</summary>
    /// <param name="query">The question; null asks what is in the field.</param>
    /// <returns>A task that completes when the answer (or the error) shows, or when the search is cancelled.</returns>
    public Task SearchAsync(string? query = null)
    {
        query ??= Query;
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.CompletedTask;
        }
        query = query.Trim();
        SetCurrentValue(QueryProperty, query);
        return RunAsync(query);
    }

    /// <summary>Cancels a running search and goes back to the suggestions (an answer already shown stays).</summary>
    public void Cancel()
    {
        if (State != AiSearchBarState.Searching)
        {
            return;
        }
        _cts?.Cancel();
        _generation++;
        SetState(Answer is null ? AiSearchBarState.Idle : AiSearchBarState.Answered);
    }

    /// <summary>Clears the field and the answer and shows the suggestions again.</summary>
    public void Clear()
    {
        _cts?.Cancel();
        _generation++;
        SetCurrentValue(QueryProperty, null);
        Answer = null;
        AskedQuery = null;
        ErrorText = null;
        SetState(AiSearchBarState.Idle);
        RebuildAnswer();
    }

    #endregion

    private async Task RunAsync(string query)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        var cts = new CancellationTokenSource();
        _cts = cts;
        var generation = ++_generation;
        AskedQuery = query;
        ErrorText = null;
        RaiseEvent(new AiSearchQueryEventArgs(QuerySubmittedEvent, query));
        SetState(AiSearchBarState.Searching);
        _announcer?.Announce(Labels.Searching);
        try
        {
            AiSearchAnswer answer;
            if (AnswerProvider is { } provider)
            {
                answer = await provider(query, cts.Token);
            }
            else
            {
                await Delay(AiSearchBarSamples.SimulatedDelay, cts.Token);
                answer = AiSearchBarSamples.Answer(query);
            }
            if (generation != _generation)
            {
                return;
            }
            Answer = answer;
            RebuildAnswer();
            SetState(AiSearchBarState.Answered);
            _announcer?.Announce(Labels.Answered(answer.Results.Count));
        }
        catch (OperationCanceledException)
        {
            // A newer question or Cancel took over.
        }
        catch (Exception ex)
        {
            if (generation != _generation)
            {
                return;
            }
            ErrorText = string.IsNullOrWhiteSpace(ex.Message) ? Labels.Failed : ex.Message;
            SetState(AiSearchBarState.Failed);
            _announcer?.Announce(ErrorText);
        }
    }

    private void SetState(AiSearchBarState state)
    {
        State = state;
        PseudoClasses.Set(IdlePseudoClass, state == AiSearchBarState.Idle);
        PseudoClasses.Set(SearchingPseudoClass, state == AiSearchBarState.Searching);
        PseudoClasses.Set(AnsweredPseudoClass, state == AiSearchBarState.Answered);
        PseudoClasses.Set(FailedPseudoClass, state == AiSearchBarState.Failed);
        if (_searchingText is not null)
        {
            _searchingText.IsActive = state == AiSearchBarState.Searching;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_input is not null)
        {
            _input.RemoveHandler(KeyDownEvent, OnInputKeyDown);
        }
        if (_submit is not null)
        {
            _submit.Click -= OnSubmitClick;
        }
        if (_clear is not null)
        {
            _clear.Click -= OnClearClick;
        }
        if (_retry is not null)
        {
            _retry.Click -= OnRetryClick;
        }

        _input = e.NameScope.Find<Input>(InputPart);
        _submit = e.NameScope.Find<Button>(SubmitPart);
        _clear = e.NameScope.Find<Button>("PART_Clear");
        _retry = e.NameScope.Find<Button>("PART_Retry");
        _answerText = e.NameScope.Find<TextBlock>(AnswerTextPart);
        _suggestions = e.NameScope.Find<Panel>("PART_Suggestions");
        _sources = e.NameScope.Find<Panel>("PART_Sources");
        _followUps = e.NameScope.Find<Panel>("PART_FollowUps");
        _results = e.NameScope.Find<Panel>("PART_Results");
        _searchingText = e.NameScope.Find<TextShimmer>("PART_SearchingText");
        _announcer = e.NameScope.Find<AiAnnouncer>("PART_Announcer");
        _headings.Clear();
        foreach (var name in new[] { "SuggestionsHeading", "AnswerHeading", "SourcesHeading", "FollowUpsHeading", "ResultsHeading" })
        {
            if (e.NameScope.Find<TextBlock>("PART_" + name) is { } heading)
            {
                _headings[name] = heading;
            }
        }

        if (_input is not null)
        {
            _input.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
        }
        if (_submit is not null)
        {
            _submit.Click += OnSubmitClick;
        }
        if (_clear is not null)
        {
            _clear.Click += OnClearClick;
        }
        if (_retry is not null)
        {
            _retry.Click += OnRetryClick;
        }
        UpdateLabels();
        BuildSuggestions();
        RebuildAnswer();
        SetState(State);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SuggestedQuestionsProperty)
        {
            BuildSuggestions();
        }
        else if (change.Property == LabelsProperty)
        {
            UpdateLabels();
            RebuildAnswer();
        }
        else if (change.Property == BoundsProperty || change.Property == CompactWidthProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, Bounds.Width > 0 && Bounds.Width <= CompactWidth);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Cancel();
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Enter:
                _ = SearchAsync();
                e.Handled = true;
                break;
            case Key.Escape when State == AiSearchBarState.Searching:
                Cancel();
                e.Handled = true;
                break;
            case Key.Escape when !string.IsNullOrEmpty(Query) || State != AiSearchBarState.Idle:
                Clear();
                e.Handled = true;
                break;
            case Key.Down:
                var first = FirstItem();
                if (first is not null)
                {
                    first.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                }
                break;
        }
    }

    private Control? FirstItem()
    {
        var panel = State switch
        {
            AiSearchBarState.Idle => _suggestions,
            AiSearchBarState.Answered => _sources is { Children.Count: > 0 } ? _sources : _results,
            _ => null,
        };
        return panel?.Children.FirstOrDefault(c => c.IsVisible && c.Focusable);
    }

    private void OnSubmitClick(object? sender, RoutedEventArgs e) => _ = SearchAsync();

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        Clear();
        _input?.Focus(NavigationMethod.Pointer);
    }

    private void OnRetryClick(object? sender, RoutedEventArgs e) => _ = SearchAsync(AskedQuery);

    private void OnQuestionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is AiSearchBarItem { Payload: string question })
        {
            _ = SearchAsync(question);
        }
    }

    private void OnSourceClick(object? sender, RoutedEventArgs e)
    {
        if (sender is AiSearchBarItem { Payload: AiSearchSource source } item && int.TryParse(item.Number, out var number))
        {
            RaiseEvent(new AiSearchSourceEventArgs(SourceInvokedEvent, source, number));
        }
    }

    private void OnResultClick(object? sender, RoutedEventArgs e)
    {
        if (sender is AiSearchBarItem { Payload: AiSearchResult result })
        {
            RaiseEvent(new AiSearchResultEventArgs(ResultInvokedEvent, result));
        }
    }

    private void UpdateLabels()
    {
        var labels = Labels;
        if (_input is not null)
        {
            _input.Placeholder = labels.Placeholder;
            AutomationProperties.SetName(_input, labels.Field);
        }
        if (_submit is not null)
        {
            _submit.Content = labels.Submit;
        }
        if (_clear is not null)
        {
            AutomationProperties.SetName(_clear, labels.Clear);
        }
        if (_retry is not null)
        {
            _retry.Content = labels.Retry;
        }
        if (_searchingText is not null)
        {
            _searchingText.Text = labels.Searching;
        }
        SetHeading("SuggestionsHeading", labels.Suggestions);
        SetHeading("AnswerHeading", labels.AnswerHeading);
        SetHeading("SourcesHeading", labels.Sources);
        SetHeading("FollowUpsHeading", labels.FollowUps);
        SetHeading("ResultsHeading", labels.Results);
    }

    private void SetHeading(string name, string text)
    {
        if (_headings.TryGetValue(name, out var heading))
        {
            heading.Text = text;
        }
    }

    private void BuildSuggestions()
    {
        if (_suggestions is null)
        {
            return;
        }
        _suggestions.Children.Clear();
        foreach (var question in SuggestedQuestions ?? [])
        {
            _suggestions.Children.Add(Chip(question));
        }
        PseudoClasses.Set(":has-suggestions", _suggestions.Children.Count > 0);
    }

    private AiSearchBarChip Chip(string question)
    {
        var chip = new AiSearchBarChip { Title = question, Payload = question }.With(OnQuestionClick);
        AutomationProperties.SetName(chip, question);
        return chip;
    }

    private void RebuildAnswer()
    {
        if (_answerText is null || _sources is null || _results is null || _followUps is null)
        {
            return;
        }
        var answer = Answer;
        var labels = Labels;
        _answerText.Inlines?.Clear();
        _sources.Children.Clear();
        _results.Children.Clear();
        _followUps.Children.Clear();
        if (answer is null)
        {
            return;
        }
        var inlines = _answerText.Inlines ??= new InlineCollection();
        foreach (var piece in AiSearchBarCitations.Split(answer.Text))
        {
            if (piece.Text is not null)
            {
                inlines.Add(new Run(piece.Text));
                continue;
            }
            if (piece.Number > answer.Sources.Count)
            {
                continue;
            }
            var source = answer.Sources[piece.Number - 1];
            var number = piece.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var citation = new AiSearchBarCitation { Number = number, Payload = source }.With(OnSourceClick);
            AutomationProperties.SetName(citation, labels.Citation(piece.Number, source.Title));
            inlines.Add(new InlineUIContainer(citation) { BaselineAlignment = BaselineAlignment.Center });
        }
        for (var i = 0; i < answer.Sources.Count; i++)
        {
            var source = answer.Sources[i];
            var number = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var card = new AiSearchBarSourceCard { Number = number, Title = source.Title, Detail = source.Location, Payload = source }
                .With(OnSourceClick);
            AutomationProperties.SetName(card, labels.Citation(i + 1, source.Title));
            _sources.Children.Add(card);
        }
        foreach (var result in answer.Results)
        {
            var row = new AiSearchBarResultRow { Title = result.Title, Detail = result.Location, Number = result.Snippet, Payload = result }
                .With(OnResultClick);
            AutomationProperties.SetName(row, result.Title);
            if (!string.IsNullOrWhiteSpace(result.Snippet))
            {
                AutomationProperties.SetHelpText(row, result.Snippet);
            }
            _results.Children.Add(row);
        }
        if (answer.Results.Count == 0)
        {
            _results.Children.Add(new TextBlock { Text = labels.NoResults, Classes = { "search-empty" } });
        }
        foreach (var question in answer.FollowUps)
        {
            _followUps.Children.Add(Chip(question));
        }
        PseudoClasses.Set(":has-sources", answer.Sources.Count > 0);
        PseudoClasses.Set(":has-follow-ups", answer.FollowUps.Count > 0);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AiSearchBarPeer(this);

    private sealed class AiSearchBarPeer(AiSearchBar owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore()
            => base.GetNameCore() is { Length: > 0 } name ? name : ((AiSearchBar)Owner).Labels.Field;
    }
}
