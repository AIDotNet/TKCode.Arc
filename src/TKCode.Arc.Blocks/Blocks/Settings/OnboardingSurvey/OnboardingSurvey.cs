using TKCode.Arc.DesignTokens;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Metadata;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A short first-run survey asked one question at a time in a card: a counter with "Skip survey", a progress bar, the
/// question with cards for one choice, checkboxes for several (with an optional limit) or a box for a few sentences,
/// and Back, "Skip question" for optional questions and Continue (Finish on the last). Questions slide in the direction
/// of travel while the card's height follows; the answers are kept when the person goes back, and the thank-you
/// reports every answer in <see cref="Completed"/>. Enter continues, Alt+Left goes back and the number keys pick an
/// option. Without <see cref="Questions"/> it asks four invented example questions.
/// </summary>
[TemplatePart(StagePart, typeof(ArcContentSwap))]
[TemplatePart(ContinueButtonPart, typeof(AvaloniaButton))]
[TemplatePart(BackButtonPart, typeof(AvaloniaButton))]
[TemplatePart(SkipQuestionButtonPart, typeof(AvaloniaButton))]
[TemplatePart(SkipSurveyButtonPart, typeof(AvaloniaButton))]
[TemplatePart(ProgressPart, typeof(Progress))]
[PseudoClasses(NarrowPseudoClass, DonePseudoClass)]
public class OnboardingSurvey : TemplatedControl
{
    public const string StagePart = "PART_Stage";
    public const string ContinueButtonPart = "PART_ContinueButton";
    public const string BackButtonPart = "PART_BackButton";
    public const string SkipQuestionButtonPart = "PART_SkipQuestionButton";
    public const string SkipSurveyButtonPart = "PART_SkipSurveyButton";
    public const string ProgressPart = "PART_Progress";

    /// <summary>Set below <c>NarrowWidth</c>: the buttons fill the row.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the thank-you is shown.</summary>
    public const string DonePseudoClass = ":done";

    internal const string CounterPart = "PART_Counter";
    internal const string DoneTitlePart = "PART_DoneTitle";
    internal const string AnnouncerPart = "PART_Announcer";

    #region Properties

    /// <summary>The index of the question on screen (0-based); the question count while the thank-you shows.</summary>
    public static readonly DirectProperty<OnboardingSurvey, int> CurrentIndexProperty =
        AvaloniaProperty.RegisterDirect<OnboardingSurvey, int>(nameof(CurrentIndex), o => o.CurrentIndex);

    /// <summary>True once the survey is finished or skipped (the thank-you is shown).</summary>
    public static readonly DirectProperty<OnboardingSurvey, bool> IsCompleteProperty =
        AvaloniaProperty.RegisterDirect<OnboardingSurvey, bool>(nameof(IsComplete), o => o.IsComplete);

    /// <summary>Whether "Skip survey" is offered.</summary>
    public static readonly StyledProperty<bool> CanSkipSurveyProperty =
        AvaloniaProperty.Register<OnboardingSurvey, bool>(nameof(CanSkipSurvey), true);

    /// <summary>The built-in text; replace the whole object to localize the survey.</summary>
    public static readonly StyledProperty<OnboardingSurveyLabels> LabelsProperty =
        AvaloniaProperty.Register<OnboardingSurvey, OnboardingSurveyLabels>(nameof(Labels), new OnboardingSurveyLabels());

    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<OnboardingSurvey, double>(nameof(NarrowWidth), 440);

    private int _currentIndex;

    /// <inheritdoc cref="CurrentIndexProperty"/>
    public int CurrentIndex
    {
        get => _currentIndex;
        private set => SetAndRaise(CurrentIndexProperty, ref _currentIndex, value);
    }

    private bool _isComplete;

    /// <inheritdoc cref="IsCompleteProperty"/>
    public bool IsComplete
    {
        get => _isComplete;
        private set => SetAndRaise(IsCompleteProperty, ref _isComplete, value);
    }

    /// <inheritdoc cref="CanSkipSurveyProperty"/>
    public bool CanSkipSurvey { get => GetValue(CanSkipSurveyProperty); set => SetValue(CanSkipSurveyProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public OnboardingSurveyLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    /// <summary>The questions, in order (the XAML content). Empty asks the four examples of <see cref="SampleQuestions"/>.</summary>
    [Content]
    public AvaloniaList<OnboardingSurveyQuestion> Questions { get; } = new();

    /// <summary>The questions being asked: <see cref="Questions"/>, or the examples while it is empty.</summary>
    public IReadOnlyList<OnboardingSurveyQuestion> ShownQuestions => _shown;

    /// <summary>The answers so far, one per question in order (empty for questions not answered).</summary>
    public IReadOnlyList<OnboardingSurveyAnswer> Answers => _shown.Select(AnswerOf).ToArray();

    #endregion

    #region Events

    /// <summary>Raised after the question changes (also when the thank-you opens), with the old and the new index.</summary>
    public static readonly RoutedEvent<OnboardingSurveyQuestionChangedEventArgs> QuestionChangedEvent =
        RoutedEvent.Register<OnboardingSurvey, OnboardingSurveyQuestionChangedEventArgs>(nameof(QuestionChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the survey ends, by Finish or by "Skip survey", with every answer.</summary>
    public static readonly RoutedEvent<OnboardingSurveyCompletedEventArgs> CompletedEvent =
        RoutedEvent.Register<OnboardingSurvey, OnboardingSurveyCompletedEventArgs>(nameof(Completed), RoutingStrategies.Bubble);

    /// <inheritdoc cref="QuestionChangedEvent"/>
    public event EventHandler<OnboardingSurveyQuestionChangedEventArgs>? QuestionChanged
    {
        add => AddHandler(QuestionChangedEvent, value);
        remove => RemoveHandler(QuestionChangedEvent, value);
    }

    /// <inheritdoc cref="CompletedEvent"/>
    public event EventHandler<OnboardingSurveyCompletedEventArgs>? Completed
    {
        add => AddHandler(CompletedEvent, value);
        remove => RemoveHandler(CompletedEvent, value);
    }

    #endregion

    /// <summary>Four invented example questions: role, team size, goals (up to three) and an optional note.</summary>
    public static IReadOnlyList<OnboardingSurveyQuestion> SampleQuestions()
    {
        OnboardingSurveyQuestion Choice(string id, string title, string? description, OnboardingSurveyQuestionKind kind, params OnboardingSurveyOption[] options)
        {
            var question = new OnboardingSurveyQuestion { Id = id, Title = title, Description = description, Kind = kind };
            question.Options.AddRange(options);
            return question;
        }

        var goals = Choice("goals", "What do you want to do first?", "We'll pin these to your home page.", OnboardingSurveyQuestionKind.MultipleChoice,
            new OnboardingSurveyOption("Plan a project", "Milestones, owners and dates"),
            new OnboardingSurveyOption("Track bugs", "Triage, assign and close"),
            new OnboardingSurveyOption("Write docs", "Specs and how-tos next to the work"),
            new OnboardingSurveyOption("Run a weekly review", "One page that sums up the week"),
            new OnboardingSurveyOption("Bring in my team", "Invite people and set roles"));
        goals.MaxSelections = 3;
        return
        [
            Choice("role", "What best describes your work?", "This picks the templates you see first.", OnboardingSurveyQuestionKind.SingleChoice,
                new OnboardingSurveyOption("Engineering", "Build and ship software", ArcIcons.Code),
                new OnboardingSurveyOption("Design", "Shape products and screens", ArcIcons.Palette),
                new OnboardingSurveyOption("Product", "Decide what to build next", ArcIcons.Lightbulb),
                new OnboardingSurveyOption("Operations", "Keep the team running", ArcIcons.Settings)),
            Choice("size", "How many people are on your team?", null, OnboardingSurveyQuestionKind.SingleChoice,
                new OnboardingSurveyOption("Just me", value: "1"),
                new OnboardingSurveyOption("2 to 10", value: "2-10"),
                new OnboardingSurveyOption("11 to 50", value: "11-50"),
                new OnboardingSurveyOption("More than 50", value: "51+")),
            goals,
            new OnboardingSurveyQuestion
            {
                Id = "note",
                Title = "Anything we should know?",
                Description = "Tools you are moving from, or something you hope to fix.",
                Kind = OnboardingSurveyQuestionKind.Text,
                IsOptional = true,
                Placeholder = "We keep our roadmap in a shared sheet today..."
            }
        ];
    }

    private sealed class QuestionView
    {
        public required Control Root { get; init; }
        public required TextBlock Title { get; init; }
        public RadioCards? Cards { get; init; }
        public List<Checkbox>? Checks { get; init; }
        public TextBlock? Hint { get; init; }
        public Textarea? Text { get; init; }
        public TextBlock? Count { get; init; }
    }

    private IReadOnlyList<OnboardingSurveyQuestion> _shown = [];
    private readonly Dictionary<OnboardingSurveyQuestion, QuestionView> _views = new();
    private readonly Dictionary<OnboardingSurveyQuestion, OnboardingSurveyAnswer> _answers = new();
    private ArcContentSwap? _stage;
    private AvaloniaButton? _continue;
    private AvaloniaButton? _back;
    private AvaloniaButton? _skipQuestion;
    private AvaloniaButton? _skipSurvey;
    private Progress? _progress;
    private TextBlock? _counter;
    private PresenceHost? _headerHost;
    private Control? _actions;
    private Control? _doneView;
    private TextBlock? _doneTitle;
    private TextBlock? _doneDescription;
    private StrokeDrawGlyph? _doneGlyph;
    private AuthAnnouncer? _announcer;
    private bool _skipped;
    private bool _syncing;

    static OnboardingSurvey()
    {
        FocusableProperty.OverrideDefaultValue<OnboardingSurvey>(false);
    }

    public OnboardingSurvey()
    {
        Questions.CollectionChanged += OnQuestionsChanged;
        _shown = SampleQuestions();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    #region Public methods

    /// <summary>Goes to the next question (or finishes) when the current one is answered or optional; false otherwise.</summary>
    public bool Next()
    {
        if (IsComplete || CurrentQuestion is not { } question || !CanContinue(question))
        {
            return false;
        }
        if (CurrentIndex + 1 >= _shown.Count)
        {
            Finish(skipped: false);
        }
        else
        {
            GoTo(CurrentIndex + 1, 1);
        }
        return true;
    }

    /// <summary>Goes back one question, keeping the answers. False on the first question and on the thank-you.</summary>
    public bool GoBack()
    {
        if (IsComplete || CurrentIndex == 0)
        {
            return false;
        }
        GoTo(CurrentIndex - 1, -1);
        return true;
    }

    /// <summary>Skips the current optional question (its answer is emptied) and moves on.</summary>
    public bool SkipQuestion()
    {
        if (IsComplete || CurrentQuestion is not { IsOptional: true } question)
        {
            return false;
        }
        _answers.Remove(question);
        SyncView(question);
        return Next();
    }

    /// <summary>Ends the survey now, as "Skip survey" does; the answers so far are reported.</summary>
    public void SkipSurvey()
    {
        if (!IsComplete)
        {
            Finish(skipped: true);
        }
    }

    /// <summary>Clears every answer and returns to the first question.</summary>
    public void Reset()
    {
        _answers.Clear();
        foreach (var question in _shown)
        {
            SyncView(question);
        }
        IsComplete = false;
        _skipped = false;
        GoTo(0, -1);
    }

    /// <summary>The answer to <paramref name="question"/> so far.</summary>
    public OnboardingSurveyAnswer AnswerOf(OnboardingSurveyQuestion question)
        => _answers.TryGetValue(question, out var answer) ? answer : new OnboardingSurveyAnswer(question.Key, [], null);

    /// <summary>Sets the answer to <paramref name="question"/> (option values, or the text of a text question).</summary>
    public void SetAnswer(OnboardingSurveyQuestion question, IReadOnlyList<string> values, string? text = null)
    {
        _answers[question] = new OnboardingSurveyAnswer(question.Key, values, text);
        SyncView(question);
        UpdateChrome();
    }

    #endregion

    #region Template

    private OnboardingSurveyQuestion? CurrentQuestion => CurrentIndex < _shown.Count ? _shown[CurrentIndex] : null;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        Detach();
        base.OnApplyTemplate(e);
        _stage = e.NameScope.Find<ArcContentSwap>(StagePart);
        _continue = e.NameScope.Find<AvaloniaButton>(ContinueButtonPart);
        _back = e.NameScope.Find<AvaloniaButton>(BackButtonPart);
        _skipQuestion = e.NameScope.Find<AvaloniaButton>(SkipQuestionButtonPart);
        _skipSurvey = e.NameScope.Find<AvaloniaButton>(SkipSurveyButtonPart);
        _progress = e.NameScope.Find<Progress>(ProgressPart);
        _counter = e.NameScope.Find<TextBlock>(CounterPart);
        _headerHost = e.NameScope.Find<PresenceHost>("PART_HeaderHost");
        _actions = e.NameScope.Find<Control>("PART_Actions");
        _doneView = e.NameScope.Find<Control>("PART_DoneView");
        _doneTitle = e.NameScope.Find<TextBlock>(DoneTitlePart);
        _doneDescription = e.NameScope.Find<TextBlock>("PART_DoneDescription");
        _doneGlyph = e.NameScope.Find<StrokeDrawGlyph>("PART_DoneGlyph");
        _announcer = e.NameScope.Find<AuthAnnouncer>(AnnouncerPart);
        if (_continue is not null)
        {
            _continue.Click += OnContinueClick;
        }
        if (_back is not null)
        {
            _back.Click += OnBackClick;
        }
        if (_skipQuestion is not null)
        {
            _skipQuestion.Click += OnSkipQuestionClick;
        }
        if (_skipSurvey is not null)
        {
            _skipSurvey.Click += OnSkipSurveyClick;
        }
        if (_doneGlyph is not null)
        {
            _doneGlyph.AttachedToVisualTree += OnDoneGlyphAttached;
        }
        _views.Clear();
        ShowCurrent(1, animate: false);
        UpdateChrome();
    }

    private void Detach()
    {
        if (_continue is not null)
        {
            _continue.Click -= OnContinueClick;
        }
        if (_back is not null)
        {
            _back.Click -= OnBackClick;
        }
        if (_skipQuestion is not null)
        {
            _skipQuestion.Click -= OnSkipQuestionClick;
        }
        if (_skipSurvey is not null)
        {
            _skipSurvey.Click -= OnSkipSurveyClick;
        }
        if (_doneGlyph is not null)
        {
            _doneGlyph.AttachedToVisualTree -= OnDoneGlyphAttached;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelsProperty)
        {
            _views.Clear();
            ShowCurrent(1, animate: false);
            UpdateChrome();
        }
        else if (change.Property == CanSkipSurveyProperty)
        {
            UpdateChrome();
        }
        else if (change.Property == BoundsProperty || change.Property == NarrowWidthProperty)
        {
            PseudoClasses.Set(NarrowPseudoClass, Bounds.Width > 0 && Bounds.Width < NarrowWidth);
        }
    }

    private void OnQuestionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _shown = Questions.Count > 0 ? Questions.ToArray() : SampleQuestions();
        _views.Clear();
        _answers.Clear();
        IsComplete = false;
        CurrentIndex = 0;
        ShowCurrent(1, animate: false);
        UpdateChrome();
    }

    #endregion

    #region Views

    private static TextBlock Text(string? text, ArcDesignTokenKind size, ArcDesignTokenKind color)
    {
        var block = new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, size);
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        return block;
    }

    private void BindToken(Control target, AvaloniaProperty property, OnboardingSurveyTokenKind kind)
        => target.Bind(property, target.GetResourceObservable(OnboardingSurveyTokens.Key(kind)));

    private QuestionView ViewOf(OnboardingSurveyQuestion question)
    {
        if (_views.TryGetValue(question, out var existing))
        {
            return existing;
        }
        var labels = Labels;
        var root = new StackPanel();
        var title = Text(question.Title, ArcDesignTokenKind.TypeXL, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(title, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        BindToken(title, TextBlock.LineHeightProperty, OnboardingSurveyTokenKind.TitleLineHeight);
        title.Focusable = true;
        title.FocusAdorner = null;
        KeyboardNavigation.SetIsTabStop(title, false);
        root.Children.Add(title);
        if (!string.IsNullOrWhiteSpace(question.Description))
        {
            var description = Text(question.Description, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.TextSecondary);
            BindToken(description, MarginProperty, OnboardingSurveyTokenKind.DescriptionMargin);
            BindToken(description, TextBlock.LineHeightProperty, OnboardingSurveyTokenKind.BodyLineHeight);
            root.Children.Add(description);
        }

        QuestionView view;
        switch (question.Kind)
        {
            case OnboardingSurveyQuestionKind.SingleChoice:
            {
                var cards = new RadioCards
                {
                    Layout = RadioCardsLayout.List,
                    IsRequired = !question.IsOptional,
                    ItemsSource = question.Options.Select(o => new RadioCardOption(o.EffectiveValue, o.Label, o.Description) { Icon = o.Icon }).ToArray()
                };
                AutomationProperties.SetName(cards, question.Title);
                BindToken(cards, MarginProperty, OnboardingSurveyTokenKind.BodyMargin);
                cards.SelectionChanged += (_, _) => OnCardsChanged(question);
                root.Children.Add(cards);
                view = new QuestionView { Root = root, Title = title, Cards = cards };
                break;
            }
            case OnboardingSurveyQuestionKind.MultipleChoice:
            {
                var list = new StackPanel();
                BindToken(list, StackPanel.SpacingProperty, OnboardingSurveyTokenKind.OptionGap);
                BindToken(list, MarginProperty, OnboardingSurveyTokenKind.BodyMargin);
                var checks = new List<Checkbox>();
                foreach (var option in question.Options)
                {
                    var check = new Checkbox { Content = option.Label, Description = option.Description, Tag = option };
                    check.IsCheckedChanged += (_, _) => OnChecksChanged(question);
                    checks.Add(check);
                    list.Children.Add(check);
                }
                AutomationProperties.SetName(list, question.Title);
                root.Children.Add(list);
                var hint = Text(labels.ChooseHint(question.MaxSelections), ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted);
                hint.Margin = new Thickness(0, 12, 0, 0);
                root.Children.Add(hint);
                view = new QuestionView { Root = root, Title = title, Checks = checks, Hint = hint };
                break;
            }
            default:
            {
                var box = new Textarea
                {
                    Label = question.Title,
                    IsLabelVisible = false,
                    Placeholder = question.Placeholder,
                    IsResizable = false,
                    MaxLength = Math.Max(0, question.MaxLength)
                };
                BindToken(box, Textarea.FrameHeightProperty, OnboardingSurveyTokenKind.TextHeight);
                BindToken(box, MarginProperty, OnboardingSurveyTokenKind.BodyMargin);
                box.PropertyChanged += (_, e) =>
                {
                    if (e.Property == TextBox.TextProperty)
                    {
                        OnTextChanged(question);
                    }
                };
                root.Children.Add(box);
                TextBlock? count = null;
                if (question.MaxLength > 0)
                {
                    count = Text(labels.CharacterCount(0, question.MaxLength), ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted);
                    count.HorizontalAlignment = HorizontalAlignment.Right;
                    count.Margin = new Thickness(0, 6, 0, 0);
                    root.Children.Add(count);
                }
                view = new QuestionView { Root = root, Title = title, Text = box, Count = count };
                break;
            }
        }
        _views[question] = view;
        SyncView(question);
        return view;
    }

    /// <summary>Brings a question's controls in step with its stored answer.</summary>
    private void SyncView(OnboardingSurveyQuestion question)
    {
        if (!_views.TryGetValue(question, out var view))
        {
            return;
        }
        var answer = AnswerOf(question);
        _syncing = true;
        try
        {
            if (view.Cards is { } cards)
            {
                var index = question.Options.ToList().FindIndex(o => answer.Values.Contains(o.EffectiveValue));
                cards.SelectedIndex = index;
            }
            if (view.Checks is { } checks)
            {
                foreach (var check in checks)
                {
                    check.IsChecked = check.Tag is OnboardingSurveyOption option && answer.Values.Contains(option.EffectiveValue);
                }
                UpdateLimit(question, view);
            }
            if (view.Text is { } box && box.Text != answer.Text)
            {
                box.Text = answer.Text;
            }
            UpdateCount(question, view);
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>At the limit, the unchecked options are disabled so the choice cannot grow past it.</summary>
    private static void UpdateLimit(OnboardingSurveyQuestion question, QuestionView view)
    {
        if (view.Checks is not { } checks)
        {
            return;
        }
        var chosen = checks.Count(c => c.IsChecked == true);
        var full = question.MaxSelections > 0 && chosen >= question.MaxSelections;
        foreach (var check in checks)
        {
            check.IsEnabled = !full || check.IsChecked == true;
        }
    }

    private void UpdateCount(OnboardingSurveyQuestion question, QuestionView view)
    {
        if (view.Count is { } count)
        {
            count.Text = Labels.CharacterCount(view.Text?.Text?.Length ?? 0, question.MaxLength);
        }
    }

    private void ShowCurrent(int direction, bool animate)
    {
        if (_stage is null)
        {
            return;
        }
        Control? view = IsComplete ? _doneView : CurrentQuestion is { } question ? ViewOf(question).Root : null;
        if (view is null)
        {
            return;
        }
        if (_stage.Content is OnboardingSurveyHost { Child: { } shown } && ReferenceEquals(shown, view))
        {
            return;
        }
        if (view.Parent is OnboardingSurveyHost previous)
        {
            previous.Child = null;
        }
        else if (view.Parent is Panel store)
        {
            store.Children.Remove(view);
        }
        var host = new OnboardingSurveyHost { Child = view };
        _stage.Direction = direction;
        if (animate)
        {
            _stage.Content = host;
        }
        else
        {
            var mode = _stage.Mode;
            _stage.Mode = ContentSwapMode.None;
            _stage.Content = host;
            _stage.Mode = mode;
        }
    }

    private bool CanContinue(OnboardingSurveyQuestion question) => question.IsOptional || !AnswerOf(question).IsEmpty;

    private void UpdateChrome()
    {
        var labels = Labels;
        var total = _shown.Count;
        PseudoClasses.Set(DonePseudoClass, IsComplete);
        if (_headerHost is not null)
        {
            _headerHost.IsPresent = !IsComplete;
        }
        if (_counter is not null)
        {
            _counter.Text = labels.Counter(Math.Min(CurrentIndex + 1, total), total);
        }
        if (_progress is not null)
        {
            // The bar counts finished questions, so it fills as the person moves on, not when a question opens.
            _progress.Value = total == 0 ? 0 : 100.0 * (IsComplete ? total : CurrentIndex) / total;
            _progress.Label = labels.ProgressName;
            _progress.IsValueVisible = false;
        }
        if (_skipSurvey is not null)
        {
            _skipSurvey.Content = labels.SkipSurvey;
            _skipSurvey.IsVisible = CanSkipSurvey && !IsComplete;
        }
        if (_back is not null)
        {
            _back.Content = labels.Back;
            _back.IsVisible = !IsComplete && CurrentIndex > 0;
        }
        if (_skipQuestion is not null)
        {
            _skipQuestion.Content = labels.SkipQuestion;
            _skipQuestion.IsVisible = !IsComplete && CurrentQuestion is { IsOptional: true };
        }
        if (_continue is not null)
        {
            var last = CurrentIndex + 1 >= total;
            _continue.Content = IsComplete ? labels.Restart : last ? labels.Finish : labels.Continue;
            _continue.IsEnabled = IsComplete || (CurrentQuestion is { } q && CanContinue(q));
            if (_continue is Button arc)
            {
                arc.Variant = IsComplete ? ButtonVariant.Secondary : ButtonVariant.Primary;
            }
        }
        if (_doneTitle is not null)
        {
            _doneTitle.Text = _skipped ? labels.SkippedTitle : labels.DoneTitle;
        }
        if (_doneDescription is not null)
        {
            _doneDescription.Text = _skipped ? labels.SkippedDescription : labels.DoneDescription;
        }
    }

    private void GoTo(int index, int direction)
    {
        var old = CurrentIndex;
        CurrentIndex = Math.Clamp(index, 0, Math.Max(0, _shown.Count - 1));
        ShowCurrent(direction, animate: true);
        UpdateChrome();
        if (CurrentQuestion is { } question)
        {
            _announcer?.Announce(Labels.Counter(CurrentIndex + 1, _shown.Count) + ": " + question.Title);
        }
        FocusCurrent();
        if (old != CurrentIndex)
        {
            RaiseEvent(new OnboardingSurveyQuestionChangedEventArgs(QuestionChangedEvent, old, CurrentIndex));
        }
    }

    private void Finish(bool skipped)
    {
        var old = CurrentIndex;
        _skipped = skipped;
        IsComplete = true;
        CurrentIndex = _shown.Count;
        ShowCurrent(1, animate: true);
        UpdateChrome();
        _announcer?.Announce(_doneTitle?.Text);
        FocusCurrent();
        RaiseEvent(new OnboardingSurveyQuestionChangedEventArgs(QuestionChangedEvent, old, CurrentIndex));
        RaiseEvent(new OnboardingSurveyCompletedEventArgs(CompletedEvent, Answers, skipped));
    }

    /// <summary>Focus goes to the answer: the chosen card (or the first), the first checkbox, the text box; the title on the thank-you.</summary>
    private void FocusCurrent()
    {
        var index = CurrentIndex;
        Dispatcher.UIThread.Post(() =>
        {
            if (CurrentIndex != index)
            {
                return;
            }
            if (IsComplete)
            {
                _doneTitle?.Focus();
                return;
            }
            if (CurrentQuestion is not { } question || !_views.TryGetValue(question, out var view))
            {
                return;
            }
            Control? target = view.Text ?? (Control?)view.Checks?.FirstOrDefault(c => c.IsEnabled)
                ?? (view.Cards is { } cards ? cards.ContainerFromIndex(Math.Max(0, cards.SelectedIndex)) : null);
            if (target is null || !target.Focus(NavigationMethod.Tab))
            {
                view.Title.Focus();
            }
        }, DispatcherPriority.Loaded);
    }

    #endregion

    #region Handlers

    private void OnCardsChanged(OnboardingSurveyQuestion question)
    {
        if (_syncing || !_views.TryGetValue(question, out var view) || view.Cards is not { } cards)
        {
            return;
        }
        _answers[question] = new OnboardingSurveyAnswer(question.Key,
            cards.SelectedItem is RadioCardOption { Value: string value } ? [value] : [], null);
        UpdateChrome();
    }

    private void OnChecksChanged(OnboardingSurveyQuestion question)
    {
        if (_syncing || !_views.TryGetValue(question, out var view) || view.Checks is not { } checks)
        {
            return;
        }
        var values = checks.Where(c => c.IsChecked == true && c.Tag is OnboardingSurveyOption)
            .Select(c => ((OnboardingSurveyOption)c.Tag!).EffectiveValue).ToArray();
        _answers[question] = new OnboardingSurveyAnswer(question.Key, values, null);
        UpdateLimit(question, view);
        UpdateChrome();
    }

    private void OnTextChanged(OnboardingSurveyQuestion question)
    {
        if (_syncing || !_views.TryGetValue(question, out var view) || view.Text is not { } box)
        {
            return;
        }
        _answers[question] = new OnboardingSurveyAnswer(question.Key, [], string.IsNullOrWhiteSpace(box.Text) ? null : box.Text);
        UpdateCount(question, view);
        UpdateChrome();
    }

    private void OnContinueClick(object? sender, RoutedEventArgs e)
    {
        if (IsComplete)
        {
            Reset();
        }
        else
        {
            Next();
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => GoBack();

    private void OnSkipQuestionClick(object? sender, RoutedEventArgs e) => SkipQuestion();

    private void OnSkipSurveyClick(object? sender, RoutedEventArgs e) => SkipSurvey();

    private void OnDoneGlyphAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_doneGlyph is null)
        {
            return;
        }
        // The check draws itself each time the thank-you arrives (the view is reused), which a theme transition on
        // Progress cannot express; below full motion it is simply there.
        if (ArcMotion.IsFull(this))
        {
            _doneGlyph.Progress = 0;
            _ = _doneGlyph.Play(ArcMotionPresets.Considered, ArcEasings.Standard, ArcMotionPresets.Fast);
        }
        else
        {
            _doneGlyph.Progress = 1;
        }
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (IsComplete)
        {
            return;
        }
        if (e.Key == Key.Left && e.KeyModifiers == KeyModifiers.Alt)
        {
            if (GoBack())
            {
                e.Handled = true;
            }
            return;
        }
        var inText = e.Source is TextBox;
        if (e.Key == Key.Enter)
        {
            // Enter in the text box adds a line; Ctrl or Cmd+Enter continues from there.
            var modified = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
            if (e.Source is AvaloniaButton || (inText && !modified) || (!inText && e.KeyModifiers != KeyModifiers.None))
            {
                return;
            }
            if (Next())
            {
                e.Handled = true;
            }
            return;
        }
        // The number keys pick an option of a choice question.
        if (!inText && e.KeyModifiers == KeyModifiers.None && e.Key >= Key.D1 && e.Key <= Key.D9
            && CurrentQuestion is { } question && _views.TryGetValue(question, out var view))
        {
            var index = e.Key - Key.D1;
            if (view.Cards is { } cards && index < question.Options.Count)
            {
                cards.SelectedIndex = index;
                cards.ContainerFromIndex(index)?.Focus(NavigationMethod.Tab);
                e.Handled = true;
            }
            else if (view.Checks is { } checks && index < checks.Count && checks[index].IsEnabled)
            {
                checks[index].IsChecked = checks[index].IsChecked != true;
                checks[index].Focus(NavigationMethod.Tab);
                e.Handled = true;
            }
        }
    }

    #endregion
}

/// <summary>Carries one question view of an <see cref="OnboardingSurvey"/> onto the stage; it always takes the full width it is given.</summary>
internal sealed class OnboardingSurveyHost : Decorator
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        return double.IsInfinity(availableSize.Width) ? size : size.WithWidth(availableSize.Width);
    }
}
