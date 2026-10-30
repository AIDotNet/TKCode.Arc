using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A Cmd+K command palette with an AI mode (Arc AI command, design reference). Search pages and actions, or press Tab to ask a
/// question: the answer streams inside the palette with numbered sources, and the next steps it suggests run straight from
/// the list. The palette never changes size: the results area keeps <see cref="ResultsHeight"/> while typing, switching
/// modes and streaming, and its views crossfade in place.
/// <para>
/// Give it pages and actions in <see cref="Items"/> (or <see cref="ItemsSource"/>) and answer questions with
/// <see cref="Responder"/>: stream text with <see cref="AiCommandPaletteAskContext.Append"/>, land the sources early with
/// <see cref="AiCommandPaletteAskContext.SetSources"/>, and resolve with the suggested actions. With no data it is an empty
/// palette; a <see cref="Simulation"/> you pass in fills it with a workspace and a simulated model, and plays a looping
/// walkthrough while it is on screen until someone uses it.
/// </para>
/// <para>
/// Focus stays in the input: Up and Down move the highlight, Enter opens, asks, runs or retries, Tab asks the model about the
/// query (or opens Ask AI with suggested questions, and goes back from there), and Escape goes back to search, clears the
/// query, then raises <see cref="CloseRequested"/>. Put it in an <see cref="AiCommandPaletteDialog"/> to open it over the page
/// with Cmd+K or Ctrl+K.
/// </para>
/// </summary>
[TemplatePart(InputPart, typeof(AiCommandPaletteInput))]
[TemplatePart(SearchModePart, typeof(AiCommandPaletteModeButton))]
[TemplatePart(AskModePart, typeof(AiCommandPaletteModeButton))]
[TemplatePart(AskGlyphPart, typeof(ArcGlyph))]
[TemplatePart(SearchViewPart, typeof(ScrollViewer))]
[TemplatePart(QuestionsViewPart, typeof(ScrollViewer))]
[TemplatePart(AnswerViewPart, typeof(ScrollViewer))]
[TemplatePart(SearchListPart, typeof(AiCommandPaletteRowList))]
[TemplatePart(QuestionListPart, typeof(AiCommandPaletteRowList))]
[TemplatePart(ActionListPart, typeof(AiCommandPaletteRowList))]
[TemplatePart(QuestionPart, typeof(TextBlock))]
[TemplatePart(AnswerMarkPart, typeof(ArcGlyph))]
[TemplatePart(AnswerModelPart, typeof(TextBlock))]
[TemplatePart(ThinkingPart, typeof(ToolCallText))]
[TemplatePart(ThinkingDotPart, typeof(TextBlock))]
[TemplatePart(SourcesPart, typeof(WrapPanel))]
[TemplatePart(AnswerTextPart, typeof(AiCommandPaletteAnswerText))]
[TemplatePart(ErrorPart, typeof(Panel))]
[TemplatePart(ErrorTitlePart, typeof(TextBlock))]
[TemplatePart(ErrorTextPart, typeof(TextBlock))]
[TemplatePart(ModelMarkPart, typeof(ArcGlyph))]
[TemplatePart(ModelNamePart, typeof(TextBlock))]
[TemplatePart(HintsPart, typeof(ArcContentSwap))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(AskPseudoClass, ElevatedPseudoClass, NarrowPseudoClass, AnsweringPseudoClass)]
public partial class AiCommandPalette : TemplatedControl
{
    internal const string InputPart = "PART_Input";
    internal const string SearchModePart = "PART_SearchMode";
    internal const string AskModePart = "PART_AskMode";
    internal const string AskGlyphPart = "PART_AskGlyph";
    internal const string SearchViewPart = "PART_SearchView";
    internal const string QuestionsViewPart = "PART_QuestionsView";
    internal const string AnswerViewPart = "PART_AnswerView";
    internal const string SearchListPart = "PART_SearchList";
    internal const string QuestionListPart = "PART_QuestionList";
    internal const string ActionListPart = "PART_ActionList";
    internal const string QuestionPart = "PART_Question";
    internal const string AnswerMarkPart = "PART_AnswerMark";
    internal const string AnswerModelPart = "PART_AnswerModel";
    internal const string ThinkingPart = "PART_Thinking";
    internal const string ThinkingDotPart = "PART_ThinkingDot";
    internal const string SourcesPart = "PART_Sources";
    internal const string AnswerTextPart = "PART_AnswerText";
    internal const string ErrorPart = "PART_Error";
    internal const string ErrorTitlePart = "PART_ErrorTitle";
    internal const string ErrorTextPart = "PART_ErrorText";
    internal const string ModelMarkPart = "PART_ModelMark";
    internal const string ModelNamePart = "PART_ModelName";
    internal const string HintsPart = "PART_Hints";
    internal const string AnnouncerPart = "PART_Announcer";

    internal const string AskPseudoClass = ":ask";
    internal const string ElevatedPseudoClass = ":elevated";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string AnsweringPseudoClass = ":answering";

    /// <summary>Below this width the navigate hint leaves the footer.</summary>
    internal const double NarrowWidth = 540;

    #region Properties

    /// <summary>The pages and actions to search; when set it takes the place of <see cref="Items"/>.</summary>
    public static readonly StyledProperty<IEnumerable<AiCommandPaletteItem>?> ItemsSourceProperty =
        AvaloniaProperty.Register<AiCommandPalette, IEnumerable<AiCommandPaletteItem>?>(nameof(ItemsSource));

    /// <summary>
    /// Answers a question: stream text with <c>Append</c>, show sources early with <c>SetSources</c>, and resolve with the
    /// actions (and the text, if it was not streamed). Throw to show the failed state with a retry row. Null uses the
    /// <see cref="Simulation"/>'s model; with neither, a question gets no answer.
    /// </summary>
    public static readonly StyledProperty<Func<AiCommandPaletteAskContext, Task<AiCommandPaletteAnswer?>>?> ResponderProperty =
        AvaloniaProperty.Register<AiCommandPalette, Func<AiCommandPaletteAskContext, Task<AiCommandPaletteAnswer?>>?>(nameof(Responder));

    /// <summary>The questions offered while Ask AI is open and empty; null offers the <see cref="Simulation"/>'s, if any.</summary>
    public static readonly StyledProperty<IList<string>?> SuggestedQuestionsProperty =
        AvaloniaProperty.Register<AiCommandPalette, IList<string>?>(nameof(SuggestedQuestions));

    /// <summary>The model that answers, shown in the footer and beside each answer; null (the default) shows the mark alone.</summary>
    public static readonly StyledProperty<string?> ModelNameProperty =
        AvaloniaProperty.Register<AiCommandPalette, string?>(nameof(ModelName));

    /// <summary>The model's mark (a 24 × 24 stroked glyph) in the footer, the input and beside each answer; the sparkle by default.</summary>
    public static readonly StyledProperty<Geometry?> ModelMarkProperty =
        AvaloniaProperty.Register<AiCommandPalette, Geometry?>(nameof(ModelMark), ArcIcons.Sparkles);

    /// <summary>Search or Ask AI (two-way). Switching back to search cancels a streaming answer.</summary>
    public static readonly StyledProperty<AiCommandPaletteMode> ModeProperty =
        AvaloniaProperty.Register<AiCommandPalette, AiCommandPaletteMode>(nameof(Mode), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The text in the input (two-way): the search query, or the question being typed in Ask AI.</summary>
    public static readonly StyledProperty<string> QueryProperty =
        AvaloniaProperty.Register<AiCommandPalette, string>(nameof(Query), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Plays a looping walkthrough while on screen. It stops for good on the first real pointer or key press inside, pauses
    /// off screen and starts over from a clean state, and never runs with reduced motion. Null plays it only when the palette
    /// has a <see cref="Simulation"/> and neither items nor a responder of its own.
    /// </summary>
    public static readonly StyledProperty<bool?> AutoPlayProperty =
        AvaloniaProperty.Register<AiCommandPalette, bool?>(nameof(AutoPlay));

    /// <summary>
    /// A simulated workspace (demos and prototypes): its items, questions and model fill in what the palette lacks, and with
    /// no items and no responder of its own the palette plays its walkthrough. Null leaves the palette to its own data.
    /// </summary>
    public static readonly StyledProperty<AiCommandPaletteSimulation?> SimulationProperty =
        AvaloniaProperty.Register<AiCommandPalette, AiCommandPaletteSimulation?>(nameof(Simulation));

    /// <summary>Listens for Cmd+K or Ctrl+K in the window and focuses the input.</summary>
    public static readonly StyledProperty<bool> IsHotkeyEnabledProperty =
        AvaloniaProperty.Register<AiCommandPalette, bool>(nameof(IsHotkeyEnabled));

    /// <summary>The height of the results area in px; the palette keeps this size while typing, switching modes and streaming.</summary>
    public static readonly StyledProperty<double> ResultsHeightProperty =
        AvaloniaProperty.Register<AiCommandPalette, double>(nameof(ResultsHeight), 392);

    /// <summary>Adds the floating shadow, for use inside a dialog or a popover.</summary>
    public static readonly StyledProperty<bool> IsElevatedProperty =
        AvaloniaProperty.Register<AiCommandPalette, bool>(nameof(IsElevated));

    /// <summary>Every word the palette shows or says.</summary>
    public static readonly StyledProperty<AiCommandPaletteLabels> LabelsProperty =
        AvaloniaProperty.Register<AiCommandPalette, AiCommandPaletteLabels>(nameof(Labels), AiCommandPaletteLabels.Default);

    /// <summary>Raised when a page or action is chosen with Enter or a click. Close your dialog or navigate here. Bubbles.</summary>
    public static readonly RoutedEvent<AiCommandPaletteItemEventArgs> ItemSelectedEvent =
        RoutedEvent.Register<AiCommandPalette, AiCommandPaletteItemEventArgs>(nameof(ItemSelected), RoutingStrategies.Bubble);

    /// <summary>Raised after a suggested action finishes, with the label it ended on ("Created ENG-2481"). Bubbles.</summary>
    public static readonly RoutedEvent<AiCommandPaletteActionEventArgs> ActionCompletedEvent =
        RoutedEvent.Register<AiCommandPalette, AiCommandPaletteActionEventArgs>(nameof(ActionCompleted), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised when a source chip or a citation is chosen. Bubbles. Unless handled, a source with an address opens it with the
    /// system launcher.
    /// </summary>
    public static readonly RoutedEvent<AiCommandPaletteSourceEventArgs> SourceSelectedEvent =
        RoutedEvent.Register<AiCommandPalette, AiCommandPaletteSourceEventArgs>(nameof(SourceSelected), RoutingStrategies.Bubble);

    /// <summary>Raised when the palette switches between Search and Ask AI. Bubbles.</summary>
    public static readonly RoutedEvent<AiCommandPaletteModeChangedEventArgs> ModeChangedEvent =
        RoutedEvent.Register<AiCommandPalette, AiCommandPaletteModeChangedEventArgs>(nameof(ModeChanged), RoutingStrategies.Bubble);

    /// <summary>Raised by Escape in an empty search: close the surrounding dialog here. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CloseRequestedEvent =
        RoutedEvent.Register<AiCommandPalette, RoutedEventArgs>(nameof(CloseRequested), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable<AiCommandPaletteItem>? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <inheritdoc cref="ResponderProperty"/>
    public Func<AiCommandPaletteAskContext, Task<AiCommandPaletteAnswer?>>? Responder { get => GetValue(ResponderProperty); set => SetValue(ResponderProperty, value); }

    /// <inheritdoc cref="SuggestedQuestionsProperty"/>
    public IList<string>? SuggestedQuestions { get => GetValue(SuggestedQuestionsProperty); set => SetValue(SuggestedQuestionsProperty, value); }

    /// <inheritdoc cref="ModelNameProperty"/>
    public string? ModelName { get => GetValue(ModelNameProperty); set => SetValue(ModelNameProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public AiCommandPaletteSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="ModelMarkProperty"/>
    public Geometry? ModelMark { get => GetValue(ModelMarkProperty); set => SetValue(ModelMarkProperty, value); }

    /// <inheritdoc cref="ModeProperty"/>
    public AiCommandPaletteMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    /// <inheritdoc cref="QueryProperty"/>
    public string Query { get => GetValue(QueryProperty); set => SetValue(QueryProperty, value); }

    /// <inheritdoc cref="AutoPlayProperty"/>
    public bool? AutoPlay { get => GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }

    /// <inheritdoc cref="IsHotkeyEnabledProperty"/>
    public bool IsHotkeyEnabled { get => GetValue(IsHotkeyEnabledProperty); set => SetValue(IsHotkeyEnabledProperty, value); }

    /// <inheritdoc cref="ResultsHeightProperty"/>
    public double ResultsHeight { get => GetValue(ResultsHeightProperty); set => SetValue(ResultsHeightProperty, value); }

    /// <inheritdoc cref="IsElevatedProperty"/>
    public bool IsElevated { get => GetValue(IsElevatedProperty); set => SetValue(IsElevatedProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AiCommandPaletteLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>The pages and actions, when they are not given through <see cref="ItemsSource"/>. Changes are followed.</summary>
    [Content]
    public AvaloniaList<AiCommandPaletteItem> Items { get; } = [];

    /// <inheritdoc cref="ItemSelectedEvent"/>
    public event EventHandler<AiCommandPaletteItemEventArgs>? ItemSelected
    {
        add => AddHandler(ItemSelectedEvent, value);
        remove => RemoveHandler(ItemSelectedEvent, value);
    }

    /// <inheritdoc cref="ActionCompletedEvent"/>
    public event EventHandler<AiCommandPaletteActionEventArgs>? ActionCompleted
    {
        add => AddHandler(ActionCompletedEvent, value);
        remove => RemoveHandler(ActionCompletedEvent, value);
    }

    /// <inheritdoc cref="SourceSelectedEvent"/>
    public event EventHandler<AiCommandPaletteSourceEventArgs>? SourceSelected
    {
        add => AddHandler(SourceSelectedEvent, value);
        remove => RemoveHandler(SourceSelectedEvent, value);
    }

    /// <inheritdoc cref="ModeChangedEvent"/>
    public event EventHandler<AiCommandPaletteModeChangedEventArgs>? ModeChanged
    {
        add => AddHandler(ModeChangedEvent, value);
        remove => RemoveHandler(ModeChangedEvent, value);
    }

    /// <inheritdoc cref="CloseRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? CloseRequested
    {
        add => AddHandler(CloseRequestedEvent, value);
        remove => RemoveHandler(CloseRequestedEvent, value);
    }

    #endregion

    /// <summary>Which view fills the results area.</summary>
    internal enum View
    {
        Search,
        Questions,
        Answer
    }

    private AiCommandPaletteInput? _input;
    private AiCommandPaletteModeButton? _searchMode;
    private AiCommandPaletteModeButton? _askMode;
    private ArcGlyph? _askGlyph;
    private ScrollViewer? _searchView;
    private ScrollViewer? _questionsView;
    private ScrollViewer? _answerView;
    private AiCommandPaletteRowList? _searchList;
    private AiCommandPaletteRowList? _questionList;
    private AiCommandPaletteRowList? _actionList;
    private TextBlock? _question;
    private ArcGlyph? _answerMark;
    private TextBlock? _answerModel;
    private ToolCallText? _thinking;
    private TextBlock? _thinkingDot;
    private WrapPanel? _sourcesPanel;
    private AiCommandPaletteAnswerText? _answerText;
    private Panel? _error;
    private TextBlock? _errorTitle;
    private TextBlock? _errorText;
    private ArcGlyph? _modelMark;
    private TextBlock? _modelName;
    private ArcContentSwap? _hints;
    private ExplainerAnnouncer? _announcer;

    private readonly Dictionary<Control, AiCommandPaletteViewFade> _fades = [];
    private INotifyCollectionChanged? _observedSource;
    private View _view = View.Search;
    private string _savedSearchQuery = string.Empty;
    private bool _syncingQuery;
    private bool _switchingMode;
    private string? _hintsKey;
    private bool _narrow;
    private TopLevel? _hotkeyHost;

    static AiCommandPalette()
    {
        FocusableProperty.OverrideDefaultValue<AiCommandPalette>(false);
    }

    /// <summary>Creates a palette; with no items or responder it shows the sample workspace.</summary>
    public AiCommandPalette()
    {
        Items.CollectionChanged += (_, _) => OnItemsChanged();
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    #region Internal state (tests)

    internal IAiCommandPaletteScheduler Scheduler { get; set; } = AiCommandPaletteDispatcherScheduler.Instance;

    internal View CurrentView => _view;

    internal AiCommandPaletteInput? Input => _input;

    internal AiCommandPaletteRowList? SearchList => _searchList;

    internal AiCommandPaletteRowList? QuestionList => _questionList;

    internal AiCommandPaletteRowList? ActionList => _actionList;

    internal AiCommandPaletteAnswerText? AnswerText => _answerText;

    internal WrapPanel? SourcesPanel => _sourcesPanel;

    internal ArcContentSwap? Hints => _hints;

    internal string? Announcement => _announcer?.Announcement;

    internal AiCommandPaletteModeButton? AskModeButton => _askMode;

    internal AiCommandPaletteModeButton? SearchModeButton => _searchMode;

    /// <summary>The list the keys act on.</summary>
    internal AiCommandPaletteRowList? ActiveList => _view switch
    {
        View.Search => _searchList,
        View.Questions => _questionList,
        _ => _actionList,
    };

    /// <summary>The items searched: the source, the inline items, or the sample workspace.</summary>
    internal IReadOnlyList<AiCommandPaletteItem> EffectiveItems
    {
        get
        {
            if (ItemsSource is { } source)
            {
                return source as IReadOnlyList<AiCommandPaletteItem> ?? source.ToList();
            }
            if (Items.Count > 0)
            {
                return Items;
            }
            return Simulation?.Items ?? [];
        }
    }

    internal IReadOnlyList<string> EffectiveQuestions =>
        SuggestedQuestions is { } questions ? (IReadOnlyList<string>)questions.ToList() : Simulation?.Questions ?? [];

    /// <summary>True when the palette plays its <see cref="Simulation"/>: it has one, and no items and no responder of its own.</summary>
    internal bool IsSample => ItemsSource is null && Items.Count == 0 && Responder is null && Simulation is not null;

    #endregion

    #region Public methods

    /// <summary>Moves the keyboard focus to the input.</summary>
    public void FocusInput() => _input?.Focus(NavigationMethod.Tab);

    /// <summary>Switches to Ask AI and asks <paramref name="question"/>; the answer streams in the palette.</summary>
    public void Ask(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return;
        }
        if (Mode != AiCommandPaletteMode.Ask)
        {
            SwitchMode(AiCommandPaletteMode.Ask);
        }
        StartAsk(question.Trim());
    }

    /// <summary>Goes back to an empty search: cancels any answer and clears the query.</summary>
    public void Reset()
    {
        CancelAsk();
        _savedSearchQuery = string.Empty;
        if (Mode != AiCommandPaletteMode.Search)
        {
            SwitchMode(AiCommandPaletteMode.Search);
        }
        SetQuery(string.Empty);
        RefreshSearch();
        ShowView(View.Search, animate: false);
    }

    #endregion

    #region Template

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        var scope = e.NameScope;
        _input = scope.Find<AiCommandPaletteInput>(InputPart);
        _searchMode = scope.Find<AiCommandPaletteModeButton>(SearchModePart);
        _askMode = scope.Find<AiCommandPaletteModeButton>(AskModePart);
        _askGlyph = scope.Find<ArcGlyph>(AskGlyphPart);
        _searchView = scope.Find<ScrollViewer>(SearchViewPart);
        _questionsView = scope.Find<ScrollViewer>(QuestionsViewPart);
        _answerView = scope.Find<ScrollViewer>(AnswerViewPart);
        _searchList = scope.Find<AiCommandPaletteRowList>(SearchListPart);
        _questionList = scope.Find<AiCommandPaletteRowList>(QuestionListPart);
        _actionList = scope.Find<AiCommandPaletteRowList>(ActionListPart);
        _question = scope.Find<TextBlock>(QuestionPart);
        _answerMark = scope.Find<ArcGlyph>(AnswerMarkPart);
        _answerModel = scope.Find<TextBlock>(AnswerModelPart);
        _thinking = scope.Find<ToolCallText>(ThinkingPart);
        _thinkingDot = scope.Find<TextBlock>(ThinkingDotPart);
        _sourcesPanel = scope.Find<WrapPanel>(SourcesPart);
        _answerText = scope.Find<AiCommandPaletteAnswerText>(AnswerTextPart);
        _error = scope.Find<Panel>(ErrorPart);
        _errorTitle = scope.Find<TextBlock>(ErrorTitlePart);
        _errorText = scope.Find<TextBlock>(ErrorTextPart);
        _modelMark = scope.Find<ArcGlyph>(ModelMarkPart);
        _modelName = scope.Find<TextBlock>(ModelNamePart);
        _hints = scope.Find<ArcContentSwap>(HintsPart);
        _announcer = scope.Find<ExplainerAnnouncer>(AnnouncerPart);

        if (_input is not null)
        {
            _input.Text = Query;
            _input.TextChanged += OnInputTextChanged;
        }
        if (_searchMode is not null)
        {
            _searchMode.Click += OnSearchModeClick;
        }
        if (_askMode is not null)
        {
            _askMode.Click += OnAskModeClick;
        }
        foreach (var list in new[] { _searchList, _questionList, _actionList })
        {
            if (list is not null)
            {
                list.RowEvent += OnRowEvent;
            }
        }
        if (_answerView is not null)
        {
            _answerView.ScrollChanged += OnAnswerScrollChanged;
        }
        _fades.Clear();
        foreach (var view in new Control?[] { _searchView, _questionsView, _answerView })
        {
            if (view is not null)
            {
                _fades[view] = new AiCommandPaletteViewFade(this, view);
            }
        }
        _hintsKey = null;
        PseudoClasses.Set(ElevatedPseudoClass, IsElevated);
        PseudoClasses.Set(AskPseudoClass, Mode == AiCommandPaletteMode.Ask);
        ApplyLabels();
        ApplyModel();
        RefreshSearch();
        RefreshQuestions();
        RebuildAnswer();
        ShowView(_view, animate: false);
    }

    private void Unhook()
    {
        if (_input is not null)
        {
            _input.TextChanged -= OnInputTextChanged;
        }
        if (_searchMode is not null)
        {
            _searchMode.Click -= OnSearchModeClick;
        }
        if (_askMode is not null)
        {
            _askMode.Click -= OnAskModeClick;
        }
        foreach (var list in new[] { _searchList, _questionList, _actionList })
        {
            if (list is not null)
            {
                list.RowEvent -= OnRowEvent;
            }
        }
        if (_answerView is not null)
        {
            _answerView.ScrollChanged -= OnAnswerScrollChanged;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == QueryProperty)
        {
            OnQueryChanged();
        }
        else if (property == ModeProperty)
        {
            OnModeChanged(change.GetOldValue<AiCommandPaletteMode>(), change.GetNewValue<AiCommandPaletteMode>());
        }
        else if (property == ItemsSourceProperty)
        {
            ObserveSource();
            OnItemsChanged();
        }
        else if (property == SuggestedQuestionsProperty)
        {
            RefreshQuestions();
        }
        else if (property == LabelsProperty)
        {
            ApplyLabels();
            RefreshSearch();
            RefreshQuestions();
            RebuildAnswer();
        }
        else if (property == ModelNameProperty || property == ModelMarkProperty)
        {
            ApplyModel();
        }
        else if (property == IsElevatedProperty)
        {
            PseudoClasses.Set(ElevatedPseudoClass, IsElevated);
        }
        else if (property == IsHotkeyEnabledProperty)
        {
            UpdateHotkey();
        }
        else if (property == ResponderProperty || property == AutoPlayProperty)
        {
            UpdateWalkthrough();
        }
        else if (property == SimulationProperty)
        {
            OnItemsChanged();
            RefreshQuestions();
            UpdateWalkthrough();
        }
        else if (property == BoundsProperty)
        {
            var narrow = Bounds.Width > 0 && Bounds.Width < NarrowWidth;
            if (narrow != _narrow)
            {
                _narrow = narrow;
                PseudoClasses.Set(NarrowPseudoClass, narrow);
                UpdateHints();
            }
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateHotkey();
        UpdateWalkthrough();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // In-flight answers and actions abort through their tokens.
        CancelAsk();
        UpdateHotkey();
        UpdateWalkthrough();
    }

    private void ApplyLabels()
    {
        var labels = Labels;
        AutomationProperties.SetName(this, labels.PaletteName);
        if (_input is not null)
        {
            _input.PlaceholderText = Mode == AiCommandPaletteMode.Ask ? labels.AskPlaceholder : labels.SearchPlaceholder;
            AutomationProperties.SetName(_input, labels.PaletteName);
        }
        if (_searchMode is not null)
        {
            _searchMode.Content = labels.SearchMode;
            _searchMode.IsChecked = Mode == AiCommandPaletteMode.Search;
        }
        if (_askMode is not null)
        {
            _askMode.Content = labels.AskMode;
            _askMode.IsChecked = Mode == AiCommandPaletteMode.Ask;
        }
        if (_errorTitle is not null)
        {
            _errorTitle.Text = labels.AnswerFailed;
        }
        _hintsKey = null;
        UpdateHints();
    }

    private void ApplyModel()
    {
        var mark = ModelMark ?? ArcIcons.Sparkles;
        foreach (var glyph in new[] { _modelMark, _answerMark, _askGlyph })
        {
            if (glyph is not null)
            {
                glyph.Data = mark;
            }
        }
        if (_modelName is not null)
        {
            _modelName.Text = ModelName;
        }
        if (_answerModel is not null)
        {
            _answerModel.Text = ModelName;
            AutomationProperties.SetName(_answerModel, string.IsNullOrEmpty(ModelName) ? string.Empty : Labels.AnsweredBy(ModelName));
        }
    }

    #endregion

    #region Query, mode and items

    private void OnInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncingQuery || _input is null)
        {
            return;
        }
        SetCurrentValue(QueryProperty, _input.Text ?? string.Empty);
    }

    private void SetQuery(string text)
    {
        SetCurrentValue(QueryProperty, text);
        if (_input is not null && _input.Text != text)
        {
            _syncingQuery = true;
            _input.Text = text;
            _input.CaretIndex = text.Length;
            _syncingQuery = false;
        }
    }

    private void OnQueryChanged()
    {
        var query = Query ?? string.Empty;
        if (_input is not null && _input.Text != query)
        {
            _syncingQuery = true;
            _input.Text = query;
            _input.CaretIndex = query.Length;
            _syncingQuery = false;
        }
        if (Mode == AiCommandPaletteMode.Search)
        {
            RefreshSearch();
        }
        UpdateHints();
    }

    private void OnItemsChanged()
    {
        RefreshSearch();
        UpdateWalkthrough();
    }

    private void ObserveSource()
    {
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged -= OnSourceCollectionChanged;
        }
        _observedSource = ItemsSource as INotifyCollectionChanged;
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged += OnSourceCollectionChanged;
        }
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnItemsChanged();

    private void OnSearchModeClick(object? sender, RoutedEventArgs e)
    {
        SwitchMode(AiCommandPaletteMode.Search);
        _input?.Focus();
    }

    private void OnAskModeClick(object? sender, RoutedEventArgs e)
    {
        EnterAsk(null);
        _input?.Focus();
    }

    /// <summary>Changes <see cref="Mode"/> from inside: the property change handler then does the rest.</summary>
    private void SwitchMode(AiCommandPaletteMode mode)
    {
        _switchingMode = true;
        try
        {
            SetCurrentValue(ModeProperty, mode);
        }
        finally
        {
            _switchingMode = false;
        }
    }

    /// <summary>Opens Ask AI: with a question it asks it at once, otherwise it offers the suggested questions.</summary>
    private void EnterAsk(string? question)
    {
        if (Mode != AiCommandPaletteMode.Ask)
        {
            SwitchMode(AiCommandPaletteMode.Ask);
        }
        if (!string.IsNullOrWhiteSpace(question))
        {
            StartAsk(question.Trim());
        }
    }

    private void OnModeChanged(AiCommandPaletteMode old, AiCommandPaletteMode mode)
    {
        if (old == mode)
        {
            return;
        }
        PseudoClasses.Set(AskPseudoClass, mode == AiCommandPaletteMode.Ask);
        if (_searchMode is not null)
        {
            _searchMode.IsChecked = mode == AiCommandPaletteMode.Search;
        }
        if (_askMode is not null)
        {
            _askMode.IsChecked = mode == AiCommandPaletteMode.Ask;
        }
        if (_input is not null)
        {
            _input.PlaceholderText = mode == AiCommandPaletteMode.Ask ? Labels.AskPlaceholder : Labels.SearchPlaceholder;
        }
        if (mode == AiCommandPaletteMode.Ask)
        {
            // The search query is kept for the way back; the input starts empty for the question.
            _savedSearchQuery = Query ?? string.Empty;
            if (_switchingMode)
            {
                SetQuery(string.Empty);
            }
            RefreshQuestions();
            ShowView(View.Questions, animate: true);
        }
        else
        {
            CancelAsk();
            SetQuery(_savedSearchQuery);
            RefreshSearch();
            ShowView(View.Search, animate: true);
        }
        UpdateHints();
        RaiseEvent(new AiCommandPaletteModeChangedEventArgs(ModeChangedEvent, mode));
    }

    #endregion

    #region Lists

    /// <summary>Rebuilds the search results for the query; the highlight lands on the first row without travel.</summary>
    private void RefreshSearch()
    {
        if (_searchList is null)
        {
            return;
        }
        var labels = Labels;
        var query = (Query ?? string.Empty).Trim();
        var groups = AiCommandPaletteSearch.Filter(EffectiveItems, Mode == AiCommandPaletteMode.Search ? query : string.Empty);
        var entries = new List<Control>();
        var titled = groups.Count > 1 || groups.Any(g => g.Group is not null);
        foreach (var (group, items) in groups)
        {
            if (titled)
            {
                entries.Add(new AiCommandPaletteGroupHeading { Text = group ?? labels.DefaultGroup });
            }
            foreach (var item in items)
            {
                entries.Add(new AiCommandPaletteRow
                {
                    Kind = AiCommandPaletteRowKind.Item,
                    Payload = item,
                    Icon = item.Icon ?? ArcIcons.Circle,
                    Title = item.Label,
                    Meta = item.Meta,
                    Trailing = string.IsNullOrWhiteSpace(item.Shortcut) ? null : AiCommandPaletteKeycap.Row(item.Shortcut),
                });
            }
        }
        if (query.Length > 0 && Mode == AiCommandPaletteMode.Search)
        {
            // Search falls back to the model: the last row asks it about the query.
            if (groups.Count == 0)
            {
                entries.Add(new AiCommandPaletteEmpty { Text = labels.NoResults(query) });
            }
            entries.Add(new AiCommandPaletteGroupHeading { Text = labels.AskGroup });
            var ask = new AiCommandPaletteRow
            {
                Kind = AiCommandPaletteRowKind.AskQuery,
                Payload = query,
                Icon = ModelMark ?? ArcIcons.Sparkles,
                Title = labels.AskAbout(query),
                Trailing = AiCommandPaletteKeycap.Row("Tab"),
            };
            ask.SetAccent(true);
            entries.Add(ask);
        }
        _searchList.SetEntries(entries, 0);
        _searchView?.ScrollToHome();
        UpdateHints();
    }

    private void RefreshQuestions()
    {
        if (_questionList is null)
        {
            return;
        }
        var entries = new List<Control> { new AiCommandPaletteGroupHeading { Text = Labels.SuggestedQuestions } };
        foreach (var question in EffectiveQuestions)
        {
            var row = new AiCommandPaletteRow
            {
                Kind = AiCommandPaletteRowKind.Question,
                Payload = question,
                Icon = ArcIcons.MessageCircle,
                Title = question,
            };
            entries.Add(row);
        }
        _questionList.SetEntries(entries, 0);
    }

    private void OnRowEvent(AiCommandPaletteRow row, bool invoked)
    {
        if (ActiveList is not { } list)
        {
            return;
        }
        var index = IndexOf(list, row);
        if (index < 0)
        {
            return;
        }
        if (!invoked)
        {
            if (list.ActiveIndex != index)
            {
                list.SetActive(index, animate: true);
                UpdateHints();
            }
            return;
        }
        list.SetActive(index, animate: true);
        Activate(row);
        _input?.Focus();
    }

    private static int IndexOf(AiCommandPaletteRowList list, AiCommandPaletteRow row)
    {
        for (var i = 0; i < list.Rows.Count; i++)
        {
            if (ReferenceEquals(list.Rows[i], row))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Opens the page, runs the action, asks the question, or retries.</summary>
    private void Activate(AiCommandPaletteRow row)
    {
        switch (row.Kind)
        {
            case AiCommandPaletteRowKind.Item when row.Payload is AiCommandPaletteItem item:
                SelectItem(item);
                break;
            case AiCommandPaletteRowKind.AskQuery when row.Payload is string query:
                EnterAsk(query);
                break;
            case AiCommandPaletteRowKind.Question when row.Payload is string question:
                StartAsk(question);
                break;
            case AiCommandPaletteRowKind.Action when row.Payload is AiCommandPaletteAction action:
                RunAction(row, action);
                break;
            case AiCommandPaletteRowKind.Retry:
                if (_session is { } session)
                {
                    StartAsk(session.Question);
                }
                break;
        }
    }

    private void SelectItem(AiCommandPaletteItem item)
    {
        RaiseEvent(new AiCommandPaletteItemEventArgs(ItemSelectedEvent, item));
        var parameter = item.CommandParameter ?? item;
        if (item.Command is { } command && command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }

    #endregion

    #region Keyboard

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }
        if (!_isScripted)
        {
            StopWalkthroughForGood();
        }
        // Keys only drive the palette from the input (a focused chip or citation keeps its own keys).
        if (!ReferenceEquals(e.Source, _input))
        {
            return;
        }
        var modifiers = e.KeyModifiers;
        switch (e.Key)
        {
            case Key.Down when modifiers == KeyModifiers.None:
                Move(1);
                e.Handled = true;
                break;
            case Key.Up when modifiers == KeyModifiers.None:
                Move(-1);
                e.Handled = true;
                break;
            case Key.Enter when modifiers == KeyModifiers.None:
                PressEnter();
                e.Handled = true;
                break;
            case Key.Tab when modifiers == KeyModifiers.None:
                PressTab();
                e.Handled = true;
                break;
            case Key.Escape:
                e.Handled = PressEscape();
                break;
        }
    }

    /// <summary>Moves the highlight through the rows (wrapping at the ends).</summary>
    internal void Move(int delta)
    {
        if (ActiveList is not { Rows.Count: > 0 } list)
        {
            return;
        }
        var count = list.Rows.Count;
        var next = list.ActiveIndex < 0 ? (delta > 0 ? 0 : count - 1) : ((list.ActiveIndex + delta) % count + count) % count;
        list.SetActive(next, animate: true);
        UpdateHints();
    }

    internal void PressEnter()
    {
        if (Mode == AiCommandPaletteMode.Ask && !string.IsNullOrWhiteSpace(Query))
        {
            StartAsk(Query.Trim());
            return;
        }
        if (ActiveList?.ActiveRow is { } row)
        {
            Activate(row);
        }
    }

    internal void PressTab()
    {
        if (Mode == AiCommandPaletteMode.Search)
        {
            EnterAsk(Query);
            return;
        }
        if (string.IsNullOrWhiteSpace(Query))
        {
            SwitchMode(AiCommandPaletteMode.Search);
        }
    }

    /// <summary>Escape: Ask AI goes back to search, a search query clears, an empty search asks to close.</summary>
    internal bool PressEscape()
    {
        if (Mode == AiCommandPaletteMode.Ask)
        {
            SwitchMode(AiCommandPaletteMode.Search);
            return true;
        }
        if (!string.IsNullOrEmpty(Query))
        {
            SetQuery(string.Empty);
            return true;
        }
        var args = new RoutedEventArgs(CloseRequestedEvent);
        RaiseEvent(args);
        return true;
    }

    private void UpdateHotkey()
    {
        var wanted = IsHotkeyEnabled && this.IsAttachedToVisualTree() ? TopLevel.GetTopLevel(this) : null;
        if (ReferenceEquals(wanted, _hotkeyHost))
        {
            return;
        }
        _hotkeyHost?.RemoveHandler(KeyDownEvent, OnHotkey);
        _hotkeyHost = wanted;
        _hotkeyHost?.AddHandler(KeyDownEvent, OnHotkey, RoutingStrategies.Tunnel);
    }

    private void OnHotkey(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.K || (e.KeyModifiers != KeyModifiers.Control && e.KeyModifiers != KeyModifiers.Meta) ||
            !IsEffectivelyVisible || !IsEffectivelyEnabled)
        {
            return;
        }
        e.Handled = true;
        StopWalkthroughForGood();
        FocusInput();
        _input?.SelectAll();
    }

    #endregion

    #region Views, hints and announcements

    /// <summary>Crossfades the results area to <paramref name="view"/> (100ms out, 160ms in with a 2px rise).</summary>
    internal void ShowView(View view, bool animate)
    {
        _view = view;
        PseudoClasses.Set(AnsweringPseudoClass, view == View.Answer);
        var target = view switch
        {
            View.Search => _searchView,
            View.Questions => _questionsView,
            _ => _answerView,
        };
        foreach (var (control, fade) in _fades)
        {
            if (ReferenceEquals(control, target))
            {
                fade.Show(animate);
            }
            else
            {
                fade.Hide(animate);
            }
        }
        UpdateHints();
    }

    /// <summary>The footer hints for the current state, swapped with one crossfade when they change.</summary>
    private void UpdateHints()
    {
        if (_hints is null)
        {
            return;
        }
        var labels = Labels;
        var hints = new List<(string Keys, string Label)>();
        var rows = ActiveList?.Rows.Count ?? 0;
        var active = ActiveList?.ActiveRow;
        var typed = !string.IsNullOrWhiteSpace(Query);
        if (Mode == AiCommandPaletteMode.Search)
        {
            if (rows > 1 && !_narrow)
            {
                hints.Add(("↑ ↓", labels.HintNavigate));
            }
            if (active is not null)
            {
                hints.Add(("↵", active.Kind == AiCommandPaletteRowKind.AskQuery ? labels.HintAsk : labels.HintOpen));
            }
            hints.Add(("Tab", labels.HintAskAi));
            hints.Add(("Esc", typed ? labels.HintClear : labels.HintClose));
        }
        else
        {
            if (typed)
            {
                hints.Add(("↵", labels.HintAsk));
            }
            else
            {
                if (rows > 1 && !_narrow)
                {
                    hints.Add(("↑ ↓", labels.HintNavigate));
                }
                if (active is not null && ActiveIsActionable(active))
                {
                    hints.Add(("↵", active.Kind switch
                    {
                        AiCommandPaletteRowKind.Question => labels.HintAsk,
                        AiCommandPaletteRowKind.Retry => labels.HintRetry,
                        AiCommandPaletteRowKind.Action when _session?.StateOf(active) == AiCommandPaletteActionState.Failed => labels.HintRetry,
                        _ => labels.HintRun,
                    }));
                }
                hints.Add(("Tab", labels.HintSearch));
            }
            hints.Add(("Esc", labels.HintBack));
        }
        var key = string.Join("|", hints.Select(h => h.Keys + ":" + h.Label));
        if (key == _hintsKey)
        {
            return;
        }
        _hintsKey = key;
        var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 14 };
        foreach (var (keys, label) in hints)
        {
            panel.Children.Add(new AiCommandPaletteHint { Keys = keys, Label = label });
        }
        _hints.Content = panel;
    }

    private bool ActiveIsActionable(AiCommandPaletteRow row) => row.Kind switch
    {
        AiCommandPaletteRowKind.Action => _session?.StateOf(row) is AiCommandPaletteActionState.Idle or AiCommandPaletteActionState.Failed,
        _ => true,
    };

    /// <summary>Says <paramref name="text"/> through the polite live region.</summary>
    internal void Announce(string text)
    {
        if (_announcer is null || text == _announcer.Announcement)
        {
            return;
        }
        _announcer.Announce(text);
    }

    #endregion
}

/// <summary>
/// The crossfade of one view of the results area: out over 100ms; in over 160ms on the enter curve with a 2px rise. Under
/// reduced motion both are a 120ms fade without travel; with motion off the views swap at once. Code-driven because three
/// persistent views trade places on keys and the outgoing one must leave the tree (hit testing, automation) only after its
/// fade, which a ControlTheme transition cannot sequence.
/// </summary>
internal sealed class AiCommandPaletteViewFade
{
    private static readonly TimeSpan s_out = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan s_in = TimeSpan.FromMilliseconds(160);
    private static readonly TimeSpan s_reduced = TimeSpan.FromMilliseconds(120);
    private const double Rise = 2;

    private readonly Visual _owner;
    private readonly Control _view;
    private readonly TranslateTransform _translate = new();
    private readonly MotionValue<double> _progress;
    private bool _shown;

    public AiCommandPaletteViewFade(Visual owner, Control view)
    {
        _owner = owner;
        _view = view;
        _view.RenderTransform = _translate;
        _shown = view.IsVisible;
        _progress = new MotionValue<double>(owner, _shown ? 1 : 0, MotionVectors.Double);
        _progress.Changed += Apply;
        Apply(_progress.Value);
    }

    private void Apply(double value)
    {
        _view.Opacity = value;
        _translate.Y = _shown && ArcMotion.IsFull(_owner) ? Rise * (1 - value) : 0;
    }

    public async void Show(bool animate)
    {
        if (_shown && _view.IsVisible)
        {
            return;
        }
        _shown = true;
        _view.IsVisible = true;
        _view.IsHitTestVisible = true;
        var level = animate && _owner.IsAttachedToVisualTree() ? ArcMotion.GetEffectiveLevel(_owner) : ArcMotionLevel.Off;
        if (level == ArcMotionLevel.Off)
        {
            _progress.Jump(1);
            Apply(1);
            return;
        }
        if (_progress.Value >= 0.999)
        {
            _progress.Jump(0);
        }
        await _progress.AnimateTo(1, MotionSpec.Tween(level == ArcMotionLevel.Full ? s_in : s_reduced,
            level == ArcMotionLevel.Full ? ArcEasings.Enter : ArcEasings.Standard));
    }

    public async void Hide(bool animate)
    {
        if (!_shown && !_view.IsVisible)
        {
            return;
        }
        _shown = false;
        _view.IsHitTestVisible = false;
        var level = animate && _owner.IsAttachedToVisualTree() ? ArcMotion.GetEffectiveLevel(_owner) : ArcMotionLevel.Off;
        if (level == ArcMotionLevel.Off)
        {
            _progress.Jump(0);
            Apply(0);
            _view.IsVisible = false;
            return;
        }
        var done = await _progress.AnimateTo(0, MotionSpec.Tween(level == ArcMotionLevel.Full ? s_out : s_reduced, ArcEasings.Standard));
        if (done && !_shown)
        {
            _view.IsVisible = false;
        }
    }
}
