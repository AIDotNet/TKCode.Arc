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
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// Natural language to chart in one card (Arc Ask your data, design reference). A question field with the data source; a
/// collapsible Generated SQL panel where the query streams in with syntax tints; a running state with a rolling count of
/// the rows scanned over a breathing skeleton; then a result that builds as stacked bars, lines or a table, with a legend,
/// a pointer and keyboard readout, and a one line insight streamed underneath. Failures show the database's message with
/// Try again; a query that matches nothing offers the suggestions.
/// <para>
/// Wire <see cref="GenerateSql"/> (a whole string, revealed four tokens a tick at a fixed final size) or
/// <see cref="StreamSql"/> (chunks from your model; the panel grows with them) to your model, and <see cref="RunQuery"/>
/// to a read only database role. Without them the card plays its <see cref="Simulation"/>, answers you supply, and asks
/// its question once it is 35% on screen; with neither, it waits in its empty state and Ask stays unavailable.
/// </para>
/// </summary>
[TemplatePart(FieldPart, typeof(Input))]
[TemplatePart(SourceNamePart, typeof(TextBlock))]
[TemplatePart(SourceMarkPart, typeof(ArcGlyph))]
[TemplatePart(AskPart, typeof(AvaloniaButton))]
[TemplatePart(AskGlyphPart, typeof(ArcGlyph))]
[TemplatePart(SqlBlockPart, typeof(Border))]
[TemplatePart(SqlTogglePart, typeof(AskYourDataSqlToggle))]
[TemplatePart(SqlStatusPart, typeof(ToolCallStatusGlyph))]
[TemplatePart(SqlTitlePart, typeof(TextBlock))]
[TemplatePart(SqlMetaPart, typeof(ArcContentSwap))]
[TemplatePart(CopySqlPart, typeof(CopyButton))]
[TemplatePart(SqlFoldPart, typeof(AiHeightBox))]
[TemplatePart(SqlScrollPart, typeof(ScrollViewer))]
[TemplatePart(SqlPart, typeof(AskYourDataRevealText))]
[TemplatePart(StageStatusPart, typeof(ToolCallStatusGlyph))]
[TemplatePart(TitlePart, typeof(ArcContentSwap))]
[TemplatePart(SubtitlePart, typeof(ArcContentSwap))]
[TemplatePart(SwitchPart, typeof(SegmentedControl))]
[TemplatePart(CompactSwitchPart, typeof(SegmentedControl))]
[TemplatePart(SuggestionsViewPart, typeof(Control))]
[TemplatePart(SuggestionsTitlePart, typeof(TextBlock))]
[TemplatePart(SuggestionsPart, typeof(Panel))]
[TemplatePart(RunningViewPart, typeof(Control))]
[TemplatePart(RowsReservePart, typeof(TextBlock))]
[TemplatePart(RowsPart, typeof(RollingDigits))]
[TemplatePart(RowsLabelPart, typeof(TextBlock))]
[TemplatePart(ResultViewPart, typeof(Control))]
[TemplatePart(LegendPart, typeof(Panel))]
[TemplatePart(BarChartPart, typeof(AskYourDataChart))]
[TemplatePart(LineChartPart, typeof(AskYourDataChart))]
[TemplatePart(TablePart, typeof(AskYourDataTable))]
[TemplatePart(InsightPart, typeof(AskYourDataRevealText))]
[TemplatePart(ErrorViewPart, typeof(Control))]
[TemplatePart(ErrorWellPart, typeof(Border))]
[TemplatePart(ErrorTextPart, typeof(SelectableTextBlock))]
[TemplatePart(RetryPart, typeof(AvaloniaButton))]
[TemplatePart(LivePart, typeof(ExplainerAnnouncer))]
[PseudoClasses(WorkingPseudoClass, SqlOpenPseudoClass, CompactPseudoClass, ErrorPseudoClass)]
public partial class AskYourData : TemplatedControl
{
    internal const string FieldPart = "PART_Field";
    internal const string SourceNamePart = "PART_SourceName";
    internal const string SourceMarkPart = "PART_SourceMark";
    internal const string AskPart = "PART_Ask";
    internal const string AskGlyphPart = "PART_AskGlyph";
    internal const string SqlBlockPart = "PART_SqlBlock";
    internal const string SqlTogglePart = "PART_SqlToggle";
    internal const string SqlStatusPart = "PART_SqlStatus";
    internal const string SqlTitlePart = "PART_SqlTitle";
    internal const string SqlMetaPart = "PART_SqlMeta";
    internal const string CopySqlPart = "PART_CopySql";
    internal const string SqlFoldPart = "PART_SqlFold";
    internal const string SqlScrollPart = "PART_SqlScroll";
    internal const string SqlPart = "PART_Sql";
    internal const string StageStatusPart = "PART_StageStatus";
    internal const string TitlePart = "PART_Title";
    internal const string SubtitlePart = "PART_Subtitle";
    internal const string SwitchPart = "PART_Switch";
    internal const string CompactSwitchPart = "PART_CompactSwitch";
    internal const string SuggestionsViewPart = "PART_SuggestionsView";
    internal const string SuggestionsTitlePart = "PART_SuggestionsTitle";
    internal const string SuggestionsPart = "PART_Suggestions";
    internal const string RunningViewPart = "PART_RunningView";
    internal const string RowsReservePart = "PART_RowsReserve";
    internal const string RowsPart = "PART_Rows";
    internal const string RowsLabelPart = "PART_RowsLabel";
    internal const string ResultViewPart = "PART_ResultView";
    internal const string LegendPart = "PART_Legend";
    internal const string BarChartPart = "PART_BarChart";
    internal const string LineChartPart = "PART_LineChart";
    internal const string TablePart = "PART_Table";
    internal const string InsightPart = "PART_Insight";
    internal const string ErrorViewPart = "PART_ErrorView";
    internal const string ErrorWellPart = "PART_ErrorWell";
    internal const string ErrorTextPart = "PART_ErrorText";
    internal const string RetryPart = "PART_Retry";
    internal const string LivePart = "PART_Live";

    internal const string WorkingPseudoClass = ":working";
    internal const string SqlOpenPseudoClass = ":sql-open";
    internal const string CompactPseudoClass = ":compact";
    internal const string ErrorPseudoClass = ":error";

    /// <summary>Below this width the source keeps only its icon and the chart switch moves into the result.</summary>
    internal const double CompactWidth = 520;

    /// <summary>The SQL panel's fold: 300ms, no bounce.</summary>
    internal static readonly SpringSpec SqlFoldSpring = SpringSpec.FromVisual(0.3, 0);

    /// <summary>
    /// The question (two-way); the field writes it as the person types. Null shows the simulation's question, or an empty
    /// field without one.
    /// </summary>
    public static readonly StyledProperty<string?> QuestionProperty =
        AvaloniaProperty.Register<AskYourData, string?>(nameof(Question), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The placeholder of the empty field.</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<AskYourData, string?>(nameof(Placeholder), "Ask a question about your data");

    /// <summary>
    /// The database the question runs against: its name shows in the field and the empty state, its dialect leads the SQL
    /// meta. Null uses the simulation's source, or names none.
    /// </summary>
    public static readonly StyledProperty<AskYourDataSource?> SourceProperty =
        AvaloniaProperty.Register<AskYourData, AskYourDataSource?>(nameof(Source));

    /// <summary>
    /// Questions offered in the empty state and after a question that matches no data. Null offers the simulation's
    /// suggestions, or none; an empty list hides them.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> SuggestionsProperty =
        AvaloniaProperty.Register<AskYourData, IReadOnlyList<string>?>(nameof(Suggestions));

    /// <summary>
    /// Writes the SQL for a question as one string. The card reveals it four tokens every 34ms at its final size. Honour the
    /// request's cancellation token: Stop cancels it. Without this and <see cref="StreamSql"/> the <see cref="Simulation"/>
    /// writes the SQL.
    /// </summary>
    public static readonly StyledProperty<Func<AskYourDataSqlRequest, Task<string>>?> GenerateSqlProperty =
        AvaloniaProperty.Register<AskYourData, Func<AskYourDataSqlRequest, Task<string>>?>(nameof(GenerateSql));

    /// <summary>
    /// Streams the SQL for a question as chunks from your model; each chunk fades in and the panel grows with them. Takes
    /// precedence over <see cref="GenerateSql"/>. Honour the cancellation token.
    /// </summary>
    public static readonly StyledProperty<Func<AskYourDataSqlRequest, IAsyncEnumerable<string>>?> StreamSqlProperty =
        AvaloniaProperty.Register<AskYourData, Func<AskYourDataSqlRequest, IAsyncEnumerable<string>>?>(nameof(StreamSql));

    /// <summary>
    /// Runs the SQL (read only) and returns a chart ready result. Call <see cref="AskYourDataQueryRequest.ReportProgress"/>
    /// to roll the count in the running state; throw to show the database error. Without it the <see cref="Simulation"/>
    /// answers.
    /// </summary>
    public static readonly StyledProperty<Func<AskYourDataQueryRequest, Task<AskYourDataResult>>?> RunQueryProperty =
        AvaloniaProperty.Register<AskYourData, Func<AskYourDataQueryRequest, Task<AskYourDataResult>>?>(nameof(RunQuery));

    /// <summary>
    /// The view on show (two-way). Null leaves it to the card: each result opens on its own view or
    /// <see cref="DefaultChart"/>, and the switch changes it until the next result.
    /// </summary>
    public static readonly StyledProperty<AskYourDataChartKind?> ChartProperty =
        AvaloniaProperty.Register<AskYourData, AskYourDataChartKind?>(nameof(Chart), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The view for results that do not name one.</summary>
    public static readonly StyledProperty<AskYourDataChartKind> DefaultChartProperty =
        AvaloniaProperty.Register<AskYourData, AskYourDataChartKind>(nameof(DefaultChart), AskYourDataChartKind.Bar);

    /// <summary>
    /// Asks the question once the card is 35% on screen. Null: on when a <see cref="Simulation"/> writes the SQL, off when
    /// you wire a real model.
    /// </summary>
    public static readonly StyledProperty<bool?> AutoRunProperty =
        AvaloniaProperty.Register<AskYourData, bool?>(nameof(AutoRun));

    /// <summary>
    /// Keeps the SQL panel open after the query starts instead of folding it away. Once the person opens or closes the
    /// panel during a run, their choice wins.
    /// </summary>
    public static readonly StyledProperty<bool> KeepSqlOpenProperty =
        AvaloniaProperty.Register<AskYourData, bool>(nameof(KeepSqlOpen));

    /// <summary>
    /// The model and database the card plays while you wire none: its question (asked on its own once the card is on
    /// screen), suggestions, source and answers, run exactly like your own delegates (the SQL revealed, the rows scanned
    /// rolling, the chart building, the insight streaming). It stands in only for what you have not wired. Null leaves the
    /// card in its empty state.
    /// </summary>
    public static readonly StyledProperty<AskYourDataSimulation?> SimulationProperty =
        AvaloniaProperty.Register<AskYourData, AskYourDataSimulation?>(nameof(Simulation));

    /// <summary>Every word the card shows or says.</summary>
    public static readonly StyledProperty<AskYourDataLabels> LabelsProperty =
        AvaloniaProperty.Register<AskYourData, AskYourDataLabels>(nameof(Labels), AskYourDataLabels.Default);

    /// <summary>Where the run is: idle, writing, running, done, empty, failed or stopped.</summary>
    public static readonly DirectProperty<AskYourData, AskYourDataStage> StageProperty =
        AvaloniaProperty.RegisterDirect<AskYourData, AskYourDataStage>(nameof(Stage), o => o.Stage);

    /// <summary>The result on show, or null.</summary>
    public static readonly DirectProperty<AskYourData, AskYourDataResult?> ResultProperty =
        AvaloniaProperty.RegisterDirect<AskYourData, AskYourDataResult?>(nameof(Result), o => o.Result);

    /// <summary>The SQL of the current run, as written so far.</summary>
    public static readonly DirectProperty<AskYourData, string> SqlProperty =
        AvaloniaProperty.RegisterDirect<AskYourData, string>(nameof(Sql), o => o.Sql);

    /// <summary>Raised once a result is charted (for saving the question to a dashboard or logging). Bubbles.</summary>
    public static readonly RoutedEvent<AskYourDataResultEventArgs> ResultReadyEvent =
        RoutedEvent.Register<AskYourData, AskYourDataResultEventArgs>(nameof(ResultReady), RoutingStrategies.Bubble);

    /// <summary>Raised when writing or running fails; not when the person stops a run. Bubbles.</summary>
    public static readonly RoutedEvent<AskYourDataErrorEventArgs> FailedEvent =
        RoutedEvent.Register<AskYourData, AskYourDataErrorEventArgs>(nameof(Failed), RoutingStrategies.Bubble);

    /// <summary>Raised when the person switches the view with the chart switch. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ChartChangedEvent =
        RoutedEvent.Register<AskYourData, RoutedEventArgs>(nameof(ChartChanged), RoutingStrategies.Bubble);

    private AskYourDataStage _stage;
    private AskYourDataResult? _result;
    private string _sql = string.Empty;

    private Input? _field;
    private TextBlock? _sourceName;
    private ArcGlyph? _sourceMark;
    private AvaloniaButton? _ask;
    private ArcGlyph? _askGlyph;
    private Border? _sqlBlock;
    private AskYourDataSqlToggle? _sqlToggle;
    private ToolCallStatusGlyph? _sqlStatus;
    private TextBlock? _sqlTitle;
    private ArcContentSwap? _sqlMeta;
    private CopyButton? _copySql;
    private AiHeightBox? _sqlFold;
    private ScrollViewer? _sqlScroll;
    private AskYourDataRevealText? _sqlText;
    private ToolCallStatusGlyph? _stageStatus;
    private ArcContentSwap? _title;
    private ArcContentSwap? _subtitle;
    private SegmentedControl? _switch;
    private SegmentedControl? _compactSwitch;
    private Control? _suggestionsView;
    private TextBlock? _suggestionsTitle;
    private Panel? _suggestions;
    private Control? _runningView;
    private TextBlock? _rowsReserve;
    private RollingDigits? _rows;
    private TextBlock? _rowsLabel;
    private Control? _resultView;
    private Panel? _legend;
    private AskYourDataChart? _barChart;
    private AskYourDataChart? _lineChart;
    private AskYourDataTable? _table;
    private AskYourDataRevealText? _insight;
    private Control? _errorView;
    private Border? _errorWell;
    private SelectableTextBlock? _errorText;
    private AvaloniaButton? _retry;
    private ExplainerAnnouncer? _live;

    private string? _shownTitle;
    private string? _shownSubtitle;
    private string? _shownMeta;
    private Control? _shownBody;
    private bool _compact;
    private bool _syncingSwitch;
    private bool _syncingField;
    private AskYourDataChartKind _view = AskYourDataChartKind.Bar;
    private ViewportVisibility? _visibility;
    private bool _autoRan;
    private readonly Dictionary<Control, MotionValue<double>> _fades = new();

    static AskYourData()
    {
        BackgroundProperty.Changed.AddClassHandler<AskYourData>((card, _) => card.UpdateTheme());
    }

    /// <summary>Creates the card.</summary>
    public AskYourData()
    {
        SizeChanged += OnSizeChanged;
    }

    /// <inheritdoc cref="QuestionProperty"/>
    public string? Question { get => GetValue(QuestionProperty); set => SetValue(QuestionProperty, value); }

    /// <inheritdoc cref="PlaceholderProperty"/>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <inheritdoc cref="SourceProperty"/>
    public AskYourDataSource? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public AskYourDataSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="SuggestionsProperty"/>
    public IReadOnlyList<string>? Suggestions { get => GetValue(SuggestionsProperty); set => SetValue(SuggestionsProperty, value); }

    /// <inheritdoc cref="GenerateSqlProperty"/>
    public Func<AskYourDataSqlRequest, Task<string>>? GenerateSql { get => GetValue(GenerateSqlProperty); set => SetValue(GenerateSqlProperty, value); }

    /// <inheritdoc cref="StreamSqlProperty"/>
    public Func<AskYourDataSqlRequest, IAsyncEnumerable<string>>? StreamSql { get => GetValue(StreamSqlProperty); set => SetValue(StreamSqlProperty, value); }

    /// <inheritdoc cref="RunQueryProperty"/>
    public Func<AskYourDataQueryRequest, Task<AskYourDataResult>>? RunQuery { get => GetValue(RunQueryProperty); set => SetValue(RunQueryProperty, value); }

    /// <inheritdoc cref="ChartProperty"/>
    public AskYourDataChartKind? Chart { get => GetValue(ChartProperty); set => SetValue(ChartProperty, value); }

    /// <inheritdoc cref="DefaultChartProperty"/>
    public AskYourDataChartKind DefaultChart { get => GetValue(DefaultChartProperty); set => SetValue(DefaultChartProperty, value); }

    /// <inheritdoc cref="AutoRunProperty"/>
    public bool? AutoRun { get => GetValue(AutoRunProperty); set => SetValue(AutoRunProperty, value); }

    /// <inheritdoc cref="KeepSqlOpenProperty"/>
    public bool KeepSqlOpen { get => GetValue(KeepSqlOpenProperty); set => SetValue(KeepSqlOpenProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AskYourDataLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="StageProperty"/>
    public AskYourDataStage Stage { get => _stage; private set => SetAndRaise(StageProperty, ref _stage, value); }

    /// <inheritdoc cref="ResultProperty"/>
    public AskYourDataResult? Result { get => _result; private set => SetAndRaise(ResultProperty, ref _result, value); }

    /// <inheritdoc cref="SqlProperty"/>
    public string Sql { get => _sql; private set => SetAndRaise(SqlProperty, ref _sql, value); }

    /// <inheritdoc cref="ResultReadyEvent"/>
    public event EventHandler<AskYourDataResultEventArgs>? ResultReady
    {
        add => AddHandler(ResultReadyEvent, value);
        remove => RemoveHandler(ResultReadyEvent, value);
    }

    /// <inheritdoc cref="FailedEvent"/>
    public event EventHandler<AskYourDataErrorEventArgs>? Failed
    {
        add => AddHandler(FailedEvent, value);
        remove => RemoveHandler(FailedEvent, value);
    }

    /// <inheritdoc cref="ChartChangedEvent"/>
    public event EventHandler<RoutedEventArgs>? ChartChanged
    {
        add => AddHandler(ChartChangedEvent, value);
        remove => RemoveHandler(ChartChangedEvent, value);
    }

    /// <summary>The view on show now.</summary>
    internal AskYourDataChartKind DisplayedChart => _view;

    /// <summary>Whether the card is in its narrow layout.</summary>
    internal bool IsCompact => _compact;

    /// <summary>Whether the SQL panel is open (or opening).</summary>
    internal bool IsSqlOpen => _sqlOpen;

    /// <summary>Whether no SQL writer and no query runner are wired, so the simulation answers.</summary>
    internal bool IsSimulated => Simulation is not null && GenerateSql is null && StreamSql is null && RunQuery is null;

    /// <summary>Whether a question can run: something writes the SQL (yours or the simulation) and something answers it.</summary>
    internal bool CanAsk => (GenerateSql is not null || StreamSql is not null || Simulation is not null) && (RunQuery is not null || Simulation is not null);

    /// <summary>The question in the field: <see cref="Question"/>, else the simulation's.</summary>
    internal string? EffectiveQuestion => Question ?? Simulation?.Question;

    /// <summary>The database: <see cref="Source"/>, else the simulation's, else an unnamed one.</summary>
    internal AskYourDataSource EffectiveSource => Source ?? Simulation?.Source ?? s_unnamedSource;

    private static readonly AskYourDataSource s_unnamedSource = new();

    /// <summary>What the live region says now.</summary>
    internal string? Announcement => _live?.Announcement;

    private bool EffectiveAutoRun => CanAsk && (AutoRun ?? (Simulation is not null && GenerateSql is null && StreamSql is null));

    private bool Working => Stage is AskYourDataStage.Writing or AskYourDataStage.Running;

    private IReadOnlyList<string> EffectiveSuggestions => Suggestions ?? Simulation?.Suggestions ?? [];

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        var scope = e.NameScope;
        _field = scope.Find<Input>(FieldPart);
        _sourceName = scope.Find<TextBlock>(SourceNamePart);
        _sourceMark = scope.Find<ArcGlyph>(SourceMarkPart);
        _ask = scope.Find<AvaloniaButton>(AskPart);
        _askGlyph = scope.Find<ArcGlyph>(AskGlyphPart);
        _sqlBlock = scope.Find<Border>(SqlBlockPart);
        _sqlToggle = scope.Find<AskYourDataSqlToggle>(SqlTogglePart);
        _sqlStatus = scope.Find<ToolCallStatusGlyph>(SqlStatusPart);
        _sqlTitle = scope.Find<TextBlock>(SqlTitlePart);
        _sqlMeta = scope.Find<ArcContentSwap>(SqlMetaPart);
        _copySql = scope.Find<CopyButton>(CopySqlPart);
        _sqlFold = scope.Find<AiHeightBox>(SqlFoldPart);
        if (_sqlFold is not null)
        {
            _sqlFold.HeightSpring = SqlFoldSpring;
        }
        _sqlScroll = scope.Find<ScrollViewer>(SqlScrollPart);
        _sqlText = scope.Find<AskYourDataRevealText>(SqlPart);
        _stageStatus = scope.Find<ToolCallStatusGlyph>(StageStatusPart);
        _title = scope.Find<ArcContentSwap>(TitlePart);
        _subtitle = scope.Find<ArcContentSwap>(SubtitlePart);
        _switch = scope.Find<SegmentedControl>(SwitchPart);
        _compactSwitch = scope.Find<SegmentedControl>(CompactSwitchPart);
        _suggestionsView = scope.Find<Control>(SuggestionsViewPart);
        _suggestionsTitle = scope.Find<TextBlock>(SuggestionsTitlePart);
        _suggestions = scope.Find<Panel>(SuggestionsPart);
        _runningView = scope.Find<Control>(RunningViewPart);
        _rowsReserve = scope.Find<TextBlock>(RowsReservePart);
        _rows = scope.Find<RollingDigits>(RowsPart);
        _rowsLabel = scope.Find<TextBlock>(RowsLabelPart);
        _resultView = scope.Find<Control>(ResultViewPart);
        _legend = scope.Find<Panel>(LegendPart);
        _barChart = scope.Find<AskYourDataChart>(BarChartPart);
        _lineChart = scope.Find<AskYourDataChart>(LineChartPart);
        _table = scope.Find<AskYourDataTable>(TablePart);
        _insight = scope.Find<AskYourDataRevealText>(InsightPart);
        _errorView = scope.Find<Control>(ErrorViewPart);
        _errorWell = scope.Find<Border>(ErrorWellPart);
        _errorText = scope.Find<SelectableTextBlock>(ErrorTextPart);
        _retry = scope.Find<AvaloniaButton>(RetryPart);
        _live = scope.Find<ExplainerAnnouncer>(LivePart);

        if (_field is not null)
        {
            _syncingField = true;
            _field.Text = EffectiveQuestion;
            _syncingField = false;
            _field.PropertyChanged += OnFieldPropertyChanged;
            _field.AddHandler(KeyDownEvent, OnFieldKeyDown, RoutingStrategies.Tunnel);
        }
        if (_ask is not null)
        {
            _ask.Click += OnAskClick;
        }
        if (_sqlToggle is not null)
        {
            _sqlToggle.Click += OnSqlToggle;
        }
        if (_retry is not null)
        {
            _retry.Click += OnRetry;
        }
        foreach (var chart in Charts())
        {
            chart.ActiveIndexChanged += OnReadout;
        }
        foreach (var segmented in Switches())
        {
            segmented.ItemsSource = SwitchOptions();
            segmented.SelectionChanged += OnSwitchChanged;
        }
        if (_sqlFold is not null)
        {
            _sqlFold.IsExpanded = _sqlOpen;
        }
        _shownTitle = _shownSubtitle = _shownMeta = null;
        _shownBody = null;
        _fades.Clear();
        UpdateTheme();
        BuildSuggestions();
        ApplyResultToViews(replay: false);
        ShowView(_view, animate: false);
        Refresh();
    }

    private void Unhook()
    {
        if (_field is not null)
        {
            _field.PropertyChanged -= OnFieldPropertyChanged;
            _field.RemoveHandler(KeyDownEvent, OnFieldKeyDown);
        }
        if (_ask is not null)
        {
            _ask.Click -= OnAskClick;
        }
        if (_sqlToggle is not null)
        {
            _sqlToggle.Click -= OnSqlToggle;
        }
        if (_retry is not null)
        {
            _retry.Click -= OnRetry;
        }
        foreach (var chart in Charts())
        {
            chart.ActiveIndexChanged -= OnReadout;
        }
        foreach (var segmented in Switches())
        {
            segmented.SelectionChanged -= OnSwitchChanged;
        }
    }

    private IEnumerable<AskYourDataChart> Charts()
    {
        if (_barChart is not null)
        {
            yield return _barChart;
        }
        if (_lineChart is not null)
        {
            yield return _lineChart;
        }
    }

    private IEnumerable<SegmentedControl> Switches()
    {
        if (_switch is not null)
        {
            yield return _switch;
        }
        if (_compactSwitch is not null)
        {
            yield return _compactSwitch;
        }
    }

    private SegmentedOption[] SwitchOptions() =>
    [
        new(AskYourDataChartKind.Bar, Labels.Bars),
        new(AskYourDataChartKind.Line, Labels.Lines),
        new(AskYourDataChartKind.Table, Labels.Table),
    ];

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == QuestionProperty)
        {
            SyncField();
            Refresh();
        }
        else if (p == SimulationProperty)
        {
            OnSimulationChanged(change.GetOldValue<AskYourDataSimulation?>());
        }
        else if (p == ChartProperty)
        {
            if (Chart is { } chart && chart != _view)
            {
                ShowView(chart, animate: true);
            }
        }
        else if (p == SuggestionsProperty)
        {
            BuildSuggestions();
            Refresh();
        }
        else if (p == LabelsProperty)
        {
            foreach (var segmented in Switches())
            {
                _syncingSwitch = true;
                segmented.ItemsSource = SwitchOptions();
                _syncingSwitch = false;
            }
            BuildSuggestions();
            ApplyResultToViews(replay: false);
            ShowView(_view, animate: false);
            _shownTitle = _shownSubtitle = _shownMeta = null;
            Refresh();
        }
        else if (p == SourceProperty || p == PlaceholderProperty || p == KeepSqlOpenProperty || p == GenerateSqlProperty
                 || p == StreamSqlProperty || p == RunQueryProperty)
        {
            Refresh();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        WatchForFirstSight();
    }

    /// <summary>Asks the question once the card is first seen (when <see cref="EffectiveAutoRun"/>).</summary>
    private void WatchForFirstSight()
    {
        _visibility?.Dispose();
        _visibility = null;
        if (!_autoRan && Stage == AskYourDataStage.Idle)
        {
            _visibility = new ViewportVisibility(this, 0.35, once: true);
            _visibility.VisibilityChanged += OnFirstSeen;
        }
    }

    private void SyncField()
    {
        var text = EffectiveQuestion;
        if (_field is not null && _field.Text != text)
        {
            _syncingField = true;
            _field.Text = text;
            _syncingField = false;
        }
    }

    /// <summary>
    /// Another simulation (or none): a run it was playing stops and the card goes back to its empty state, showing the new
    /// question and suggestions, and asks again once it is seen.
    /// </summary>
    private void OnSimulationChanged(AskYourDataSimulation? old)
    {
        if (old is not null && Stage != AskYourDataStage.Idle && GenerateSql is null && StreamSql is null && RunQuery is null)
        {
            ResetToIdle();
        }
        SyncField();
        BuildSuggestions();
        Refresh();
        if (Stage == AskYourDataStage.Idle)
        {
            _autoRan = false;
            if (TopLevel.GetTopLevel(this) is not null)
            {
                WatchForFirstSight();
            }
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _visibility?.Dispose();
        _visibility = null;
    }

    private void OnFirstSeen(object? sender, bool visible)
    {
        if (!visible || _autoRan || Stage != AskYourDataStage.Idle || !EffectiveAutoRun)
        {
            return;
        }
        _autoRan = true;
        Ask();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Width > 0 && e.NewSize.Width < CompactWidth;
        if (compact != _compact)
        {
            _compact = compact;
            PseudoClasses.Set(CompactPseudoClass, compact);
            Refresh();
        }
    }

    private void UpdateTheme()
    {
        var dark = Background is ISolidColorBrush solid && Oklch.FromColor(solid.Color).L < 0.5;
        _sqlText?.SetTheme(dark);
    }

    private void OnFieldPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBox.TextProperty || _syncingField || _field is null)
        {
            return;
        }
        SetCurrentValue(QuestionProperty, _field.Text);
    }

    private void OnFieldKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            Ask();
        }
        else if (e.Key == Key.Escape && Working)
        {
            e.Handled = true;
            Stop();
        }
    }

    private void OnAskClick(object? sender, RoutedEventArgs e)
    {
        if (Working)
        {
            Stop();
        }
        else
        {
            Ask();
        }
    }

    private void OnRetry(object? sender, RoutedEventArgs e) => Ask(_asked ?? EffectiveQuestion ?? string.Empty);

    private void OnSqlToggle(object? sender, RoutedEventArgs e)
    {
        // The person's choice wins over the automatic fold for the rest of the run.
        _sqlChoice = !_sqlOpen;
        SetSqlOpen(_sqlChoice.Value);
    }

    private void OnSwitchChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingSwitch || sender is not SegmentedControl segmented || segmented.SelectedItem is not SegmentedOption { Value: AskYourDataChartKind kind })
        {
            return;
        }
        if (kind == _view)
        {
            return;
        }
        if (Chart is not null)
        {
            SetCurrentValue(ChartProperty, kind);
        }
        ShowView(kind, animate: true);
        RaiseEvent(new RoutedEventArgs(ChartChangedEvent));
    }

    private void OnReadout(object? sender, EventArgs e)
    {
        if (sender is not AskYourDataChart chart || Result is not { } result)
        {
            return;
        }
        if (chart.ActiveIndex is { } index)
        {
            var text = AskYourDataFormat.Readout(result, index, chart.Format, Labels);
            AutomationProperties.SetHelpText(chart, text);
            Announce(text);
        }
        else
        {
            AutomationProperties.SetHelpText(chart, Labels.ChartHint);
        }
    }

    /// <summary>Says <paramref name="text"/> in the polite live region.</summary>
    private void Announce(string text)
    {
        if (_live is not null && text != _live.Announcement)
        {
            _live.Announce(text);
        }
    }

    private void SetSqlOpen(bool open)
    {
        _sqlOpen = open;
        if (_sqlFold is not null)
        {
            _sqlFold.IsExpanded = open;
        }
        PseudoClasses.Set(SqlOpenPseudoClass, open);
        Refresh();
    }

    /// <summary>The suggestion chips, rebuilt when the list or the words change.</summary>
    private void BuildSuggestions()
    {
        if (_suggestions is null)
        {
            return;
        }
        foreach (var child in _suggestions.Children)
        {
            if (child is AvaloniaButton old)
            {
                old.Click -= OnSuggestion;
            }
        }
        _suggestions.Children.Clear();
        foreach (var suggestion in EffectiveSuggestions)
        {
            var chip = new Button
            {
                Content = suggestion,
                Variant = ButtonVariant.Secondary,
                Size = ControlSize.Small,
                Tag = suggestion,
            };
            chip.Classes.Add("ayd-chip");
            chip.Click += OnSuggestion;
            _suggestions.Children.Add(chip);
        }
    }

    private void OnSuggestion(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string question })
        {
            Ask(question);
        }
    }

    /// <summary>The legend: a swatch, the label and the legend value per series.</summary>
    private void BuildLegend(AskYourDataResult? result)
    {
        if (_legend is null)
        {
            return;
        }
        _legend.Children.Clear();
        if (result is not { HasData: true })
        {
            return;
        }
        var format = result.FormatValue ?? AskYourDataFormat.Value;
        for (var s = 0; s < result.Series.Count; s++)
        {
            var series = result.Series[s];
            var swatch = new Border { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center }
                .BindCornerRadius(ArcDesignTokenKind.RadiusStep2);
            if (series.Color is { } color)
            {
                swatch.Background = new SolidColorBrush(color);
            }
            else
            {
                ArcTokens.Bind(swatch, Border.BackgroundProperty, (s % 4) switch
                {
                    0 => ArcDesignTokenKind.Series1,
                    1 => ArcDesignTokenKind.Series2,
                    2 => ArcDesignTokenKind.Series3,
                    _ => ArcDesignTokenKind.Series4
                });
            }
            var label = new TextBlock { Text = series.Label, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
            ArcTokens.Bind(label, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            var entry = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Height = 20 };
            entry.Children.Add(swatch);
            entry.Children.Add(label);
            if (AskYourDataFormat.LegendNumber(result, series) is { } number)
            {
                var value = new TextBlock
                {
                    Text = format(number),
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 12,
                    FontFeatures = new FontFeatureCollection { new FontFeature { Tag = "tnum", Value = 1 } },
                };
                ArcTokens.Bind(value, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
                entry.Children.Add(value);
            }
            _legend.Children.Add(entry);
        }
    }

    /// <summary>Hands the result to the charts, the table and the legend (a new result only, not every refresh).</summary>
    private void ApplyResultToViews(bool replay)
    {
        var result = Result is { HasData: true } r ? r : null;
        foreach (var chart in Charts())
        {
            chart.Labels = Labels;
            chart.Result = result;
            AutomationProperties.SetName(chart, result is null ? null : Labels.ChartName(result.Title));
            AutomationProperties.SetHelpText(chart, Labels.ChartHint);
        }
        if (_table is not null)
        {
            _table.Labels = Labels;
            _table.Result = result;
            AutomationProperties.SetName(_table, result?.Title);
        }
        BuildLegend(result);
        if (replay)
        {
            if (ViewControl(_view) is AskYourDataChart shown)
            {
                shown.Replay();
            }
        }
    }

    private Control? ViewControl(AskYourDataChartKind kind) => kind switch
    {
        AskYourDataChartKind.Line => _lineChart,
        AskYourDataChartKind.Table => _table,
        _ => _barChart
    };

    /// <summary>
    /// Puts a view on show. Switching crossfades in the fixed chart area: the old view fades out over 100ms while the new
    /// one fades in over 160ms; a chart that comes in builds again (bars rise, lines draw).
    /// </summary>
    private void ShowView(AskYourDataChartKind kind, bool animate)
    {
        _view = kind;
        var animates = animate && IsLoaded && ArcMotion.IsFull(this);
        foreach (var candidate in new[] { AskYourDataChartKind.Bar, AskYourDataChartKind.Line, AskYourDataChartKind.Table })
        {
            if (ViewControl(candidate) is not { } view)
            {
                continue;
            }
            if (candidate == kind)
            {
                view.IsVisible = true;
                Fade(view, 1, animates ? ArcMotionPresets.Fast : TimeSpan.Zero, hideAtZero: false);
                if (animate && view is AskYourDataChart chart)
                {
                    chart.Replay();
                }
            }
            else if (view.IsVisible)
            {
                Fade(view, 0, animates ? TimeSpan.FromMilliseconds(100) : TimeSpan.Zero, hideAtZero: true);
            }
        }
        _syncingSwitch = true;
        foreach (var segmented in Switches())
        {
            segmented.SelectedIndex = (int)kind;
        }
        _syncingSwitch = false;
    }

    /// <summary>Fades a part to <paramref name="target"/>; a part faded out leaves the layout once it is gone.</summary>
    private void Fade(Control part, double target, TimeSpan duration, bool hideAtZero)
    {
        if (!_fades.TryGetValue(part, out var value))
        {
            value = new MotionValue<double>(part, part.Opacity, MotionVectors.Double);
            value.Changed += v => part.Opacity = Math.Clamp(v, 0, 1);
            _fades[part] = value;
        }
        if (duration <= TimeSpan.Zero)
        {
            value.Jump(target);
            part.Opacity = target;
            if (hideAtZero && target <= 0)
            {
                part.IsVisible = false;
            }
            return;
        }
        _ = FadeAsync(part, value, target, duration, hideAtZero);
    }

    private static async Task FadeAsync(Control part, MotionValue<double> value, double target, TimeSpan duration, bool hideAtZero)
    {
        var reached = await value.AnimateTo(target, MotionSpec.Tween(duration, ArcEasings.Standard));
        if (reached && hideAtZero && target <= 0)
        {
            part.IsVisible = false;
        }
    }

    /// <summary>Shows one body view; a view that comes in fades in over 160ms.</summary>
    private void ShowBody(Control? body)
    {
        if (ReferenceEquals(body, _shownBody))
        {
            return;
        }
        var animates = IsLoaded && ArcMotion.IsFull(this) && _shownBody is not null;
        _shownBody = body;
        foreach (var view in new[] { _suggestionsView, _runningView, _resultView, _errorView })
        {
            if (view is null)
            {
                continue;
            }
            var shown = ReferenceEquals(view, body);
            view.IsVisible = shown;
            if (shown)
            {
                if (animates)
                {
                    view.Opacity = 0;
                }
                Fade(view, 1, animates ? ArcMotionPresets.Fast : TimeSpan.Zero, hideAtZero: false);
            }
        }
    }

    private static TextBlock Line(string text) => new()
    {
        Text = text,
        TextTrimming = TextTrimming.CharacterEllipsis,
        MaxLines = 1,
    };

    private static void Swap(ArcContentSwap? swap, ref string? shown, string text)
    {
        if (swap is null || shown == text)
        {
            return;
        }
        shown = text;
        swap.Content = text.Length == 0 ? null : Line(text);
    }

    /// <summary>Brings every part in line with the state; a part that already shows the value is left alone.</summary>
    private void Refresh()
    {
        if (_field is null)
        {
            return;
        }
        var labels = Labels;
        var stage = Stage;
        var working = Working;
        var source = EffectiveSource;
        PseudoClasses.Set(WorkingPseudoClass, working);
        PseudoClasses.Set(ErrorPseudoClass, stage == AskYourDataStage.Error);
        PseudoClasses.Set(SqlOpenPseudoClass, _sqlOpen);

        _field.Placeholder = Placeholder;
        AutomationProperties.SetName(_field, labels.Question);
        if (_sourceMark is not null)
        {
            _sourceMark.Data = source.Mark ?? ArcIcons.Database;
        }
        if (_sourceName is not null)
        {
            _sourceName.Text = source.Name;
            _sourceName.IsVisible = !_compact && !string.IsNullOrEmpty(source.Name);
        }
        if (_ask is not null)
        {
            _ask.IsEnabled = working || CanAsk && !string.IsNullOrWhiteSpace(EffectiveQuestion);
            AutomationProperties.SetName(_ask, working ? labels.Stop : labels.Ask);
        }
        if (_askGlyph is not null)
        {
            _askGlyph.Data = working ? ArcIcons.Square : ArcIcons.ArrowUp;
        }

        // The Generated SQL panel.
        if (_sqlBlock is not null)
        {
            _sqlBlock.IsVisible = stage != AskYourDataStage.Idle;
        }
        if (_sqlStatus is not null)
        {
            _sqlStatus.Status = SqlStatus();
        }
        if (_sqlTitle is not null)
        {
            _sqlTitle.Text = labels.GeneratedSql;
        }
        var meta = AskYourDataFormat.Meta(source.Dialect,
            stage == AskYourDataStage.Writing ? labels.WritingMeta : stage == AskYourDataStage.Running ? labels.RunningMeta : null,
            stage is AskYourDataStage.Done or AskYourDataStage.Empty ? Result : null, labels);
        Swap(_sqlMeta, ref _shownMeta, meta);
        if (_sqlToggle is not null)
        {
            _sqlToggle.IsExpanded = _sqlOpen;
            AutomationProperties.SetName(_sqlToggle, meta.Length > 0 ? labels.GeneratedSql + ", " + meta : labels.GeneratedSql);
        }
        if (_copySql is not null)
        {
            if (_copySql.Value != Sql)
            {
                _copySql.Value = Sql;
            }
            _copySql.IsVisible = Sql.Length > 0;
            AutomationProperties.SetName(_copySql, labels.CopySql);
        }
        if (_sqlScroll is not null)
        {
            AutomationProperties.SetName(_sqlScroll, labels.SqlCode);
        }

        // The stage header.
        var status = StageStatus();
        if (_stageStatus is not null)
        {
            _stageStatus.IsVisible = status is not null;
            if (status is { } s)
            {
                _stageStatus.Status = s;
            }
        }
        var (title, subtitle) = Header();
        Swap(_title, ref _shownTitle, title);
        Swap(_subtitle, ref _shownSubtitle, subtitle);

        var done = stage == AskYourDataStage.Done;
        if (_switch is not null)
        {
            _switch.IsVisible = done && !_compact;
            AutomationProperties.SetName(_switch, labels.ChartView);
        }
        if (_compactSwitch is not null)
        {
            _compactSwitch.IsVisible = done && _compact;
            AutomationProperties.SetName(_compactSwitch, labels.ChartView);
        }

        // The body of the stage.
        var suggestions = EffectiveSuggestions.Count > 0;
        Control? body = stage switch
        {
            AskYourDataStage.Idle or AskYourDataStage.Empty => suggestions ? _suggestionsView : null,
            AskYourDataStage.Writing or AskYourDataStage.Running => _runningView,
            AskYourDataStage.Done => _resultView,
            AskYourDataStage.Error or AskYourDataStage.Stopped => _errorView,
            _ => null
        };
        ShowBody(body);
        if (_suggestionsTitle is not null)
        {
            _suggestionsTitle.Text = labels.Suggestions;
            _suggestionsTitle.IsVisible = stage == AskYourDataStage.Idle;
        }
        if (_rowsLabel is not null)
        {
            _rowsLabel.Text = labels.RowsScanned;
        }
        if (_rows?.Parent is Control { Parent: Control countRow })
        {
            countRow.IsVisible = stage == AskYourDataStage.Running;
        }
        if (_errorWell is not null)
        {
            _errorWell.IsVisible = stage == AskYourDataStage.Error && !string.IsNullOrEmpty(_errorMessage);
        }
        if (_errorText is not null && _errorText.Text != _errorMessage)
        {
            _errorText.Text = _errorMessage;
        }
        if (_retry is not null)
        {
            _retry.Content = labels.TryAgain;
        }
    }

    private ToolCallStatus SqlStatus()
    {
        switch (Stage)
        {
            case AskYourDataStage.Writing:
                return ToolCallStatus.Running;
            case AskYourDataStage.Error when _errorStage == AskYourDataErrorStage.Sql:
                return ToolCallStatus.Error;
            case AskYourDataStage.Stopped when !_sqlWritten:
                return ToolCallStatus.Denied;
            default:
                return ToolCallStatus.Success;
        }
    }

    private ToolCallStatus? StageStatus() => Stage switch
    {
        AskYourDataStage.Writing or AskYourDataStage.Running => ToolCallStatus.Running,
        AskYourDataStage.Done => ToolCallStatus.Success,
        AskYourDataStage.Error => ToolCallStatus.Error,
        AskYourDataStage.Stopped => ToolCallStatus.Denied,
        _ => null
    };

    /// <summary>The stage header's two lines for the current state.</summary>
    internal (string Title, string Subtitle) Header()
    {
        var labels = Labels;
        var question = _asked ?? string.Empty;
        return Stage switch
        {
            AskYourDataStage.Writing => (labels.WritingSql, question),
            AskYourDataStage.Running => (labels.RunningQuery, question),
            AskYourDataStage.Done => (Result?.Title ?? string.Empty, Result?.Subtitle ?? string.Empty),
            AskYourDataStage.Empty => (labels.NoData, EffectiveSuggestions.Count > 0 ? labels.NoDataHint : question),
            AskYourDataStage.Error => (_errorStage == AskYourDataErrorStage.Sql ? labels.SqlFailed : labels.QueryFailed, question),
            AskYourDataStage.Stopped => (labels.Stopped, labels.StoppedHint),
            _ => (labels.EmptyTitle(EffectiveSource.Name), labels.EmptyHint)
        };
    }
}
