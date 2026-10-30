using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A pricing calculator for LLM workloads in one panel (Arc LLM cost calculator, design reference). The header shows the
/// selected model's monthly total in a large rolling figure. Below it come your workload presets, three sliders for input tokens per request, output tokens per request and requests per day, and a Cached input switch
/// that bills part of the input at the cache read rate. Every model sits in a ranked list, lowest cost first, with its
/// mark, a bar relative to the most expensive model and a rolling monthly cost; rows glide to their new rank as values
/// change. Picking a row selects that model for the total, the footer breakdown (input, cached, output, per request) and
/// its price line.
/// <para>
/// Prices are inputs: pass your own <see cref="Models"/> with list prices in US dollars per 1M tokens and say where they
/// came from in <see cref="Description"/>. With no models the panel shows its empty state. <see cref="ComputeCost"/> is the
/// arithmetic the panel uses, for server code and invoices.
/// </para>
/// </summary>
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(DescriptionPart, typeof(TextBlock))]
[TemplatePart(TotalPart, typeof(RollingDigits))]
[TemplatePart(TotalUnitPart, typeof(TextBlock))]
[TemplatePart(SelectedNamePart, typeof(ArcContentSwap))]
[TemplatePart(PresetsPart, typeof(SegmentedControl))]
[TemplatePart(InputSliderPart, typeof(Slider))]
[TemplatePart(OutputSliderPart, typeof(Slider))]
[TemplatePart(RequestsSliderPart, typeof(Slider))]
[TemplatePart(CacheSwitchPart, typeof(Switch))]
[TemplatePart(CacheSharePart, typeof(TextBlock))]
[TemplatePart(ListPart, typeof(LlmCostCalculatorList))]
[TemplatePart(SkeletonPart, typeof(LlmCostCalculatorSkeleton))]
[TemplatePart(ErrorPart, typeof(Panel))]
[TemplatePart(ErrorTitlePart, typeof(TextBlock))]
[TemplatePart(ErrorTextPart, typeof(TextBlock))]
[TemplatePart(RetryPart, typeof(AvaloniaButton))]
[TemplatePart(EmptyPart, typeof(Panel))]
[TemplatePart(EmptyTitlePart, typeof(TextBlock))]
[TemplatePart(EmptyTextPart, typeof(TextBlock))]
[TemplatePart(InputCostPart, typeof(RollingDigits))]
[TemplatePart(CachedCostPart, typeof(RollingDigits))]
[TemplatePart(OutputCostPart, typeof(RollingDigits))]
[TemplatePart(PerRequestCostPart, typeof(RollingDigits))]
[TemplatePart(PriceLinePart, typeof(ArcContentSwap))]
[TemplatePart(NotePart, typeof(TextBlock))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(LoadingPseudoClass, ErrorPseudoClass, EmptyPseudoClass, NoDataPseudoClass, NarrowPseudoClass, CompactPseudoClass,
    CachedPseudoClass)]
public class LlmCostCalculator : TemplatedControl
{
    internal const string TitlePart = "PART_Title";
    internal const string DescriptionPart = "PART_Description";
    internal const string TotalPart = "PART_Total";
    internal const string TotalUnitPart = "PART_TotalUnit";
    internal const string SelectedNamePart = "PART_SelectedName";
    internal const string PresetsPart = "PART_Presets";
    internal const string InputSliderPart = "PART_InputSlider";
    internal const string OutputSliderPart = "PART_OutputSlider";
    internal const string RequestsSliderPart = "PART_RequestsSlider";
    internal const string CacheSwitchPart = "PART_CacheSwitch";
    internal const string CacheSharePart = "PART_CacheShare";
    internal const string ListPart = "PART_List";
    internal const string SkeletonPart = "PART_Skeleton";
    internal const string ErrorPart = "PART_Error";
    internal const string ErrorTitlePart = "PART_ErrorTitle";
    internal const string ErrorTextPart = "PART_ErrorText";
    internal const string RetryPart = "PART_Retry";
    internal const string EmptyPart = "PART_Empty";
    internal const string EmptyTitlePart = "PART_EmptyTitle";
    internal const string EmptyTextPart = "PART_EmptyText";
    internal const string InputCostPart = "PART_InputCost";
    internal const string CachedCostPart = "PART_CachedCost";
    internal const string OutputCostPart = "PART_OutputCost";
    internal const string PerRequestCostPart = "PART_PerRequestCost";
    internal const string InputLabelPart = "PART_InputLabel";
    internal const string CachedLabelPart = "PART_CachedLabel";
    internal const string OutputLabelPart = "PART_OutputLabel";
    internal const string PerRequestLabelPart = "PART_PerRequestLabel";
    internal const string PriceLinePart = "PART_PriceLine";
    internal const string NotePart = "PART_Note";
    internal const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while <see cref="IsLoading"/> is true.</summary>
    public const string LoadingPseudoClass = ":loading";

    /// <summary>Set while <see cref="ErrorText"/> is shown in place of the list.</summary>
    public const string ErrorPseudoClass = ":error";

    /// <summary>Set when there are no models to compare.</summary>
    public const string EmptyPseudoClass = ":empty";

    /// <summary>Set whenever the list is not shown (loading, error or empty): the figures read as dashes.</summary>
    public const string NoDataPseudoClass = ":no-data";

    /// <summary>Set below <see cref="NarrowWidth"/>: the sliders stack.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>
    /// Set below <see cref="CompactWidth"/>: the total moves under the heading, each bar takes its own line under the model
    /// name (rows grow from 44 to 58px) and the breakdown wraps into two columns.
    /// </summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while Cached input is on.</summary>
    public const string CachedPseudoClass = ":cached";

    /// <summary>The panel width below which the sliders stack.</summary>
    public const double NarrowWidth = 600;

    /// <summary>The panel width below which the header, the rows and the breakdown take their compact layout.</summary>
    public const double CompactWidth = 520;

    /// <summary>How long the numbers must stay still before the status region speaks.</summary>
    public static readonly TimeSpan AnnounceDelay = TimeSpan.FromMilliseconds(450);

    private const string CustomPresetValue = "\u0000custom";

    #region Properties

    /// <summary>
    /// The models to compare, each with prices in US dollars per 1M tokens. Null or an empty list shows the empty state.
    /// </summary>
    public static readonly StyledProperty<IList<LlmModelPrice>?> ModelsProperty =
        AvaloniaProperty.Register<LlmCostCalculator, IList<LlmModelPrice>?>(nameof(Models));

    /// <summary>
    /// The workload (two-way): input and output tokens per request, requests a day and whether caching is on. Values between
    /// slider stops still compute exactly; the sliders show the nearest stop. Defaults to 16,000 tokens in, 500 out, 5,000
    /// requests a day, cached.
    /// </summary>
    public static readonly StyledProperty<LlmUsage> UsageProperty =
        AvaloniaProperty.Register<LlmCostCalculator, LlmUsage>(nameof(Usage), new LlmUsage(16_000, 500, 5_000, true),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The id of the selected model (two-way). Null selects the second model; an id that is not in the models falls back to
    /// the cheapest model.
    /// </summary>
    public static readonly StyledProperty<string?> SelectedIdProperty =
        AvaloniaProperty.Register<LlmCostCalculator, string?>(nameof(SelectedId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Workload shortcuts shown as a segmented control; a Custom segment lights up when the sliders match none. Null or an
    /// empty list hides the row.
    /// </summary>
    public static readonly StyledProperty<IList<LlmUsagePreset>?> PresetsProperty =
        AvaloniaProperty.Register<LlmCostCalculator, IList<LlmUsagePreset>?>(nameof(Presets));

    /// <summary>The share of input tokens billed at the cached rate while Cached input is on, from 0 to 1 (default 0.6).</summary>
    public static readonly StyledProperty<double> CacheHitRateProperty =
        AvaloniaProperty.Register<LlmCostCalculator, double>(nameof(CacheHitRate), 0.6,
            coerce: (_, value) => double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0.6);

    /// <summary>Days in a billing month (default 30).</summary>
    public static readonly StyledProperty<double> DaysPerMonthProperty =
        AvaloniaProperty.Register<LlmCostCalculator, double>(nameof(DaysPerMonth), 30,
            coerce: (_, value) => double.IsFinite(value) ? Math.Max(0, value) : 30);

    /// <summary>Shows placeholder rows with the list's exact geometry and disables the controls while prices load.</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<LlmCostCalculator, bool>(nameof(IsLoading));

    /// <summary>Shows "Prices did not load" with this reason in place of the list. Null or empty shows the list.</summary>
    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<LlmCostCalculator, string?>(nameof(ErrorText));

    /// <summary>Adds a Try again button to the error state; it raises <see cref="RetryRequested"/>.</summary>
    public static readonly StyledProperty<bool> AllowsRetryProperty =
        AvaloniaProperty.Register<LlmCostCalculator, bool>(nameof(AllowsRetry));

    /// <summary>The heading of the panel. Null uses the label ("Estimated monthly cost").</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<LlmCostCalculator, string?>(nameof(Title));

    /// <summary>The line under the heading: say where your prices come from and when they were checked. Null uses the label.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<LlmCostCalculator, string?>(nameof(Description));

    /// <summary>The fine print at the bottom of the panel. Null uses the label.</summary>
    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<LlmCostCalculator, string?>(nameof(Note));

    /// <summary>Every word the panel shows or says.</summary>
    public static readonly StyledProperty<LlmCostCalculatorLabels> LabelsProperty =
        AvaloniaProperty.Register<LlmCostCalculator, LlmCostCalculatorLabels>(nameof(Labels), LlmCostCalculatorLabels.Default);

    /// <summary>The model the total and the breakdown are for, or null when no model is shown.</summary>
    public static readonly DirectProperty<LlmCostCalculator, LlmModelPrice?> SelectedModelProperty =
        AvaloniaProperty.RegisterDirect<LlmCostCalculator, LlmModelPrice?>(nameof(SelectedModel), o => o.SelectedModel);

    /// <summary>What the selected model costs for the workload.</summary>
    public static readonly DirectProperty<LlmCostCalculator, LlmCost> SelectedCostProperty =
        AvaloniaProperty.RegisterDirect<LlmCostCalculator, LlmCost>(nameof(SelectedCost), o => o.SelectedCost);

    /// <summary>Raised on every slider step, preset and cache toggle with the whole workload. Bubbles.</summary>
    public static readonly RoutedEvent<LlmUsageChangedEventArgs> UsageChangedEvent =
        RoutedEvent.Register<LlmCostCalculator, LlmUsageChangedEventArgs>(nameof(UsageChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when someone picks a row by pointer or with the arrow keys, with the model and its cost. Bubbles.</summary>
    public static readonly RoutedEvent<LlmSelectedModelChangedEventArgs> SelectedModelChangedEvent =
        RoutedEvent.Register<LlmCostCalculator, LlmSelectedModelChangedEventArgs>(nameof(SelectedModelChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the person presses Try again in the error state. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RetryRequestedEvent =
        RoutedEvent.Register<LlmCostCalculator, RoutedEventArgs>(nameof(RetryRequested), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ModelsProperty"/>
    public IList<LlmModelPrice>? Models { get => GetValue(ModelsProperty); set => SetValue(ModelsProperty, value); }

    /// <inheritdoc cref="UsageProperty"/>
    public LlmUsage Usage { get => GetValue(UsageProperty); set => SetValue(UsageProperty, value); }

    /// <inheritdoc cref="SelectedIdProperty"/>
    public string? SelectedId { get => GetValue(SelectedIdProperty); set => SetValue(SelectedIdProperty, value); }

    /// <inheritdoc cref="PresetsProperty"/>
    public IList<LlmUsagePreset>? Presets { get => GetValue(PresetsProperty); set => SetValue(PresetsProperty, value); }

    /// <inheritdoc cref="CacheHitRateProperty"/>
    public double CacheHitRate { get => GetValue(CacheHitRateProperty); set => SetValue(CacheHitRateProperty, value); }

    /// <inheritdoc cref="DaysPerMonthProperty"/>
    public double DaysPerMonth { get => GetValue(DaysPerMonthProperty); set => SetValue(DaysPerMonthProperty, value); }

    /// <inheritdoc cref="IsLoadingProperty"/>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <inheritdoc cref="ErrorTextProperty"/>
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }

    /// <inheritdoc cref="AllowsRetryProperty"/>
    public bool AllowsRetry { get => GetValue(AllowsRetryProperty); set => SetValue(AllowsRetryProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="NoteProperty"/>
    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public LlmCostCalculatorLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="SelectedModelProperty"/>
    public LlmModelPrice? SelectedModel { get => _selectedModel; private set => SetAndRaise(SelectedModelProperty, ref _selectedModel, value); }

    /// <inheritdoc cref="SelectedCostProperty"/>
    public LlmCost SelectedCost { get => _selectedCost; private set => SetAndRaise(SelectedCostProperty, ref _selectedCost, value); }

    /// <inheritdoc cref="UsageChangedEvent"/>
    public event EventHandler<LlmUsageChangedEventArgs>? UsageChanged
    {
        add => AddHandler(UsageChangedEvent, value);
        remove => RemoveHandler(UsageChangedEvent, value);
    }

    /// <inheritdoc cref="SelectedModelChangedEvent"/>
    public event EventHandler<LlmSelectedModelChangedEventArgs>? SelectedModelChanged
    {
        add => AddHandler(SelectedModelChangedEvent, value);
        remove => RemoveHandler(SelectedModelChangedEvent, value);
    }

    /// <inheritdoc cref="RetryRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? RetryRequested
    {
        add => AddHandler(RetryRequestedEvent, value);
        remove => RemoveHandler(RetryRequestedEvent, value);
    }

    #endregion

    /// <summary>
    /// What <paramref name="model"/> costs a month for <paramref name="usage"/>, in US dollars: the same numbers the panel
    /// shows. Cached input bills <paramref name="cacheHitRate"/> of the input at the cache read price while caching is on.
    /// </summary>
    public static LlmCost ComputeCost(LlmModelPrice model, LlmUsage usage, double cacheHitRate = 0.6, double daysPerMonth = 30)
    {
        ArgumentNullException.ThrowIfNull(model);
        return LlmCostCalculatorMath.Compute(model, usage, cacheHitRate, daysPerMonth);
    }

    private enum ViewState
    {
        List,
        Loading,
        Error,
        Empty
    }

    private readonly List<LlmCostCalculatorRow> _rows = [];
    private LlmModelPrice? _selectedModel;
    private LlmCost _selectedCost;
    private LlmCost[] _costs = [];
    private int[] _ranked = [];
    private int _selectedIndex = -1;
    private INotifyCollectionChanged? _observedModels;
    private bool _syncing;
    private bool _compact;
    private bool _narrow;
    private DispatcherTimer? _announceTimer;
    private string? _announced;
    private LlmModelPrice? _shownNameModel;
    private string? _shownName;
    private string? _shownPriceLine;

    private TextBlock? _title;
    private TextBlock? _description;
    private RollingDigits? _total;
    private TextBlock? _totalUnit;
    private ArcContentSwap? _selectedName;
    private SegmentedControl? _presets;
    private Slider? _inputSlider;
    private Slider? _outputSlider;
    private Slider? _requestsSlider;
    private Switch? _cacheSwitch;
    private TextBlock? _cacheShare;
    private LlmCostCalculatorList? _list;
    private LlmCostCalculatorSkeleton? _skeleton;
    private Panel? _error;
    private TextBlock? _errorTitle;
    private TextBlock? _errorText;
    private AvaloniaButton? _retry;
    private Panel? _empty;
    private TextBlock? _emptyTitle;
    private TextBlock? _emptyText;
    private RollingDigits? _inputCost;
    private RollingDigits? _cachedCost;
    private RollingDigits? _outputCost;
    private RollingDigits? _perRequestCost;
    private TextBlock? _inputLabel;
    private TextBlock? _cachedLabel;
    private TextBlock? _outputLabel;
    private TextBlock? _perRequestLabel;
    private ArcContentSwap? _priceLine;
    private TextBlock? _note;
    private ExplainerAnnouncer? _announcer;

    #region Internal views (tests)

    /// <summary>The models on screen: <see cref="Models"/>, or none.</summary>
    internal IReadOnlyList<LlmModelPrice> DisplayedModels => Models is { } models ? [.. models] : [];

    /// <summary>The presets on screen.</summary>
    internal IReadOnlyList<LlmUsagePreset> DisplayedPresets => Presets is { } presets ? [.. presets] : [];

    /// <summary>The rows in model order.</summary>
    internal IReadOnlyList<LlmCostCalculatorRow> Rows => _rows;

    /// <summary>The rows by rank, cheapest first.</summary>
    internal IReadOnlyList<LlmCostCalculatorRow> RankedRows => [.. _ranked.Select(i => _rows[i])];

    internal RollingDigits? TotalDigits => _total;

    internal SegmentedControl? PresetsControl => _presets;

    internal Slider? InputSlider => _inputSlider;

    internal Slider? OutputSlider => _outputSlider;

    internal Slider? RequestsSlider => _requestsSlider;

    internal Switch? CacheSwitch => _cacheSwitch;

    internal LlmCostCalculatorList? List => _list;

    internal LlmCostCalculatorSkeleton? Skeleton => _skeleton;

    internal AvaloniaButton? RetryButton => _retry;

    internal ExplainerAnnouncer? Announcer => _announcer;

    internal string? ShownName => _shownName;

    internal string? ShownPriceLine => _shownPriceLine;

    internal RollingDigits? PerRequestDigits => _perRequestCost;

    internal bool IsAnnouncePending => _announceTimer?.IsEnabled == true;

    /// <summary>The id of the preset the workload matches, or null for Custom.</summary>
    internal string? MatchedPresetId => DisplayedPresets.FirstOrDefault(p => p.Usage == Usage)?.Id;

    #endregion

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        var scope = e.NameScope;
        _title = scope.Find<TextBlock>(TitlePart);
        _description = scope.Find<TextBlock>(DescriptionPart);
        _total = scope.Find<RollingDigits>(TotalPart);
        _totalUnit = scope.Find<TextBlock>(TotalUnitPart);
        _selectedName = scope.Find<ArcContentSwap>(SelectedNamePart);
        _presets = scope.Find<SegmentedControl>(PresetsPart);
        _inputSlider = scope.Find<Slider>(InputSliderPart);
        _outputSlider = scope.Find<Slider>(OutputSliderPart);
        _requestsSlider = scope.Find<Slider>(RequestsSliderPart);
        _cacheSwitch = scope.Find<Switch>(CacheSwitchPart);
        _cacheShare = scope.Find<TextBlock>(CacheSharePart);
        _list = scope.Find<LlmCostCalculatorList>(ListPart);
        _skeleton = scope.Find<LlmCostCalculatorSkeleton>(SkeletonPart);
        _error = scope.Find<Panel>(ErrorPart);
        _errorTitle = scope.Find<TextBlock>(ErrorTitlePart);
        _errorText = scope.Find<TextBlock>(ErrorTextPart);
        _retry = scope.Find<AvaloniaButton>(RetryPart);
        _empty = scope.Find<Panel>(EmptyPart);
        _emptyTitle = scope.Find<TextBlock>(EmptyTitlePart);
        _emptyText = scope.Find<TextBlock>(EmptyTextPart);
        _inputCost = scope.Find<RollingDigits>(InputCostPart);
        _cachedCost = scope.Find<RollingDigits>(CachedCostPart);
        _outputCost = scope.Find<RollingDigits>(OutputCostPart);
        _perRequestCost = scope.Find<RollingDigits>(PerRequestCostPart);
        _inputLabel = scope.Find<TextBlock>(InputLabelPart);
        _cachedLabel = scope.Find<TextBlock>(CachedLabelPart);
        _outputLabel = scope.Find<TextBlock>(OutputLabelPart);
        _perRequestLabel = scope.Find<TextBlock>(PerRequestLabelPart);
        _priceLine = scope.Find<ArcContentSwap>(PriceLinePart);
        _note = scope.Find<TextBlock>(NotePart);
        _announcer = scope.Find<ExplainerAnnouncer>(AnnouncerPart);

        SetupSlider(_inputSlider, LlmCostCalculatorMath.InputStops);
        SetupSlider(_outputSlider, LlmCostCalculatorMath.OutputStops);
        SetupSlider(_requestsSlider, LlmCostCalculatorMath.RequestStops);
        if (_presets is not null)
        {
            _presets.SelectionChanged += OnPresetChanged;
        }
        if (_cacheSwitch is not null)
        {
            _cacheSwitch.IsCheckedChanged += OnCacheToggled;
        }
        if (_retry is not null)
        {
            _retry.Click += OnRetryClick;
        }
        // The large total and the rolling figures are read through the status region and the row names instead.
        foreach (var digits in new[] { _total, _inputCost, _cachedCost, _outputCost, _perRequestCost })
        {
            if (digits is not null)
            {
                AutomationProperties.SetAccessibilityView(digits, AccessibilityView.Raw);
                digits.WheelMotion = LlmCostCalculatorRow.PriceRoll;
            }
        }
        if (_list is not null)
        {
            _list.Children.Clear();
        }
        _rows.Clear();
        _shownNameModel = null;
        _shownName = null;
        _shownPriceLine = null;
        _selectedName?.SetCurrentValue(ArcContentSwap.ContentProperty, null);
        _priceLine?.SetCurrentValue(ArcContentSwap.ContentProperty, null);

        RebuildPresets();
        ApplyTexts();
        RebuildRows();
        UpdateBreakpoints(Bounds.Width);
        SyncControls();
        Refresh(animate: false);
    }

    private void Unhook()
    {
        foreach (var slider in new[] { _inputSlider, _outputSlider, _requestsSlider })
        {
            if (slider is not null)
            {
                slider.ValueChanged -= OnSliderChanged;
            }
        }
        if (_presets is not null)
        {
            _presets.SelectionChanged -= OnPresetChanged;
        }
        if (_cacheSwitch is not null)
        {
            _cacheSwitch.IsCheckedChanged -= OnCacheToggled;
        }
        if (_retry is not null)
        {
            _retry.Click -= OnRetryClick;
        }
        foreach (var row in _rows)
        {
            row.Picked = null;
        }
    }

    private void SetupSlider(Slider? slider, int[] stops)
    {
        if (slider is null)
        {
            return;
        }
        // The slider moves over stop indexes; the readout and the spoken value are the real counts.
        slider.Minimum = 0;
        slider.Maximum = stops.Length - 1;
        slider.SmallChange = 1;
        slider.LargeChange = 2;
        slider.ValueFormatter = index => Labels.Count(stops[Math.Clamp((int)Math.Round(index), 0, stops.Length - 1)]);
        slider.ValueChanged += OnSliderChanged;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UsageProperty)
        {
            SyncControls();
            Refresh(animate: true);
        }
        else if (change.Property == ModelsProperty)
        {
            ObserveModels();
            RebuildRows();
            Refresh(animate: true);
        }
        else if (change.Property == SelectedIdProperty || change.Property == CacheHitRateProperty
                 || change.Property == DaysPerMonthProperty)
        {
            Refresh(animate: true);
        }
        else if (change.Property == PresetsProperty)
        {
            RebuildPresets();
            SyncControls();
        }
        else if (change.Property == IsLoadingProperty || change.Property == ErrorTextProperty || change.Property == AllowsRetryProperty)
        {
            Refresh(animate: true);
        }
        else if (change.Property == LabelsProperty)
        {
            RebuildPresets();
            ApplyTexts();
            SyncControls();
            _shownNameModel = null;
            Refresh(animate: false);
        }
        else if (change.Property == TitleProperty || change.Property == DescriptionProperty || change.Property == NoteProperty)
        {
            ApplyTexts();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ObserveModels();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _announceTimer?.Stop();
        if (_observedModels is not null)
        {
            _observedModels.CollectionChanged -= OnModelsCollectionChanged;
            _observedModels = null;
        }
    }

    /// <summary>Listens to the app's list while the panel is shown, so adding or removing a model updates it.</summary>
    private void ObserveModels()
    {
        if (_observedModels is not null)
        {
            _observedModels.CollectionChanged -= OnModelsCollectionChanged;
        }
        _observedModels = VisualRoot is not null ? Models as INotifyCollectionChanged : null;
        if (_observedModels is not null)
        {
            _observedModels.CollectionChanged += OnModelsCollectionChanged;
        }
    }

    private void OnModelsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildRows();
        Refresh(animate: true);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        // The layout is picked before the parts are measured, so the first pass already has the right row height and
        // columns (choosing it only after arrange would leave one pass measured for the old layout).
        if (double.IsFinite(availableSize.Width))
        {
            UpdateBreakpoints(availableSize.Width);
        }
        return base.MeasureOverride(availableSize);
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateBreakpoints(e.NewSize.Width);
    }

    /// <summary>The panel's own width decides the layout (a container query), never the window's.</summary>
    private void UpdateBreakpoints(double width)
    {
        if (!(width > 0))
        {
            return;
        }
        _narrow = width < NarrowWidth;
        _compact = width < CompactWidth;
        PseudoClasses.Set(NarrowPseudoClass, _narrow);
        PseudoClasses.Set(CompactPseudoClass, _compact);
        foreach (var row in _rows)
        {
            row.SetCompact(_compact);
        }
        if (_skeleton is not null)
        {
            _skeleton.IsCompact = _compact;
        }
    }

    #region Inputs

    private void OnSliderChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_syncing || sender is not Slider slider)
        {
            return;
        }
        var index = (int)Math.Round(slider.Value);
        var usage = Usage;
        if (ReferenceEquals(slider, _inputSlider))
        {
            usage = usage with { InputTokens = Stop(LlmCostCalculatorMath.InputStops, index) };
        }
        else if (ReferenceEquals(slider, _outputSlider))
        {
            usage = usage with { OutputTokens = Stop(LlmCostCalculatorMath.OutputStops, index) };
        }
        else if (ReferenceEquals(slider, _requestsSlider))
        {
            usage = usage with { RequestsPerDay = Stop(LlmCostCalculatorMath.RequestStops, index) };
        }
        SetUsageFromUser(usage);
    }

    private static int Stop(int[] stops, int index) => stops[Math.Clamp(index, 0, stops.Length - 1)];

    private void OnCacheToggled(object? sender, RoutedEventArgs e)
    {
        if (_syncing || _cacheSwitch is null)
        {
            return;
        }
        SetUsageFromUser(Usage with { Cached = _cacheSwitch.IsChecked == true });
    }

    private void OnPresetChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The segmented control reports its first selection while the template loads; only the person's picks count.
        if (_syncing || _presets?.SelectedValue is not string id || id == CustomPresetValue)
        {
            return;
        }
        if (DisplayedPresets.FirstOrDefault(p => p.Id == id) is { } preset)
        {
            SetUsageFromUser(preset.Usage);
        }
    }

    private void SetUsageFromUser(LlmUsage usage)
    {
        var old = Usage;
        if (old == usage)
        {
            return;
        }
        SetCurrentValue(UsageProperty, usage);
        RaiseEvent(new LlmUsageChangedEventArgs(UsageChangedEvent, old, usage));
    }

    private void OnRetryClick(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(RetryRequestedEvent));

    private void RebuildPresets()
    {
        if (_presets is null)
        {
            return;
        }
        _syncing = true;
        try
        {
            var presets = DisplayedPresets;
            var options = presets.Select(p => new SegmentedOption(p.Id, p.Label)).ToList();
            options.Add(new SegmentedOption(CustomPresetValue, Labels.Custom));
            _presets.ItemsSource = options;
            _presets.IsVisible = presets.Count > 0;
            AutomationProperties.SetName(_presets, Labels.Workload);
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>Brings the presets, the sliders (to the nearest stop) and the switch in line with <see cref="Usage"/>.</summary>
    private void SyncControls()
    {
        var usage = Usage;
        _syncing = true;
        try
        {
            SetSlider(_inputSlider, LlmCostCalculatorMath.InputStops, usage.InputTokens, Labels.InputTokens);
            SetSlider(_outputSlider, LlmCostCalculatorMath.OutputStops, usage.OutputTokens, Labels.OutputTokens);
            SetSlider(_requestsSlider, LlmCostCalculatorMath.RequestStops, usage.RequestsPerDay, Labels.RequestsPerDay);
            if (_cacheSwitch is not null)
            {
                _cacheSwitch.IsChecked = usage.Cached;
            }
            PseudoClasses.Set(CachedPseudoClass, usage.Cached);
            if (_presets is not null)
            {
                var matched = MatchedPresetId;
                _presets.SelectedValue = matched ?? CustomPresetValue;
                // Custom cannot be picked: it lights up on its own when the sliders match no preset.
                if (_presets.ItemsSource is IList<SegmentedOption> options && options.Count > 0)
                {
                    options[^1].IsDisabled = matched is not null;
                    if (_presets.ContainerFromIndex(options.Count - 1) is { } custom)
                    {
                        custom.IsEnabled = matched is null;
                    }
                }
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private static void SetSlider(Slider? slider, int[] stops, int value, string label)
    {
        if (slider is null)
        {
            return;
        }
        slider.Label = label;
        var index = LlmCostCalculatorMath.NearestStop(stops, value);
        if ((int)Math.Round(slider.Value) != index)
        {
            slider.Value = index;
        }
    }

    #endregion

    #region Rows

    /// <summary>One row per model, reused while the same model object stays in the list.</summary>
    private void RebuildRows()
    {
        if (_list is null)
        {
            return;
        }
        var models = DisplayedModels;
        var keep = new Dictionary<LlmModelPrice, LlmCostCalculatorRow>(ReferenceEqualityComparer.Instance);
        foreach (var row in _rows)
        {
            keep.TryAdd(row.Model, row);
        }
        var next = new List<LlmCostCalculatorRow>(models.Count);
        foreach (var model in models)
        {
            if (keep.Remove(model, out var row))
            {
                row.ShowModel();
            }
            else
            {
                row = new LlmCostCalculatorRow(model) { Picked = OnRowPicked };
                row.SetCompact(_compact);
            }
            next.Add(row);
        }
        foreach (var gone in keep.Values)
        {
            gone.Picked = null;
            _list.Children.Remove(gone);
        }
        foreach (var row in next)
        {
            if (row.Parent is null)
            {
                _list.Children.Add(row);
            }
        }
        _rows.Clear();
        _rows.AddRange(next);
    }

    private void OnRowPicked(LlmCostCalculatorRow row)
    {
        if (CurrentState != ViewState.List)
        {
            return;
        }
        Pick(row, NavigationMethod.Pointer);
    }

    /// <summary>Selects <paramref name="row"/>'s model as the person's choice, moves focus there and reports it.</summary>
    private void Pick(LlmCostCalculatorRow row, NavigationMethod method)
    {
        var index = _rows.IndexOf(row);
        if (index < 0)
        {
            return;
        }
        var changed = index != _selectedIndex;
        SetCurrentValue(SelectedIdProperty, row.Model.Id);
        row.Focus(method);
        if (changed)
        {
            RaiseEvent(new LlmSelectedModelChangedEventArgs(SelectedModelChangedEvent, row.Model, _costs[index]));
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || CurrentState != ViewState.List || _ranked.Length == 0)
        {
            return;
        }
        var row = (e.Source as Visual)?.FindAncestorOfType<LlmCostCalculatorRow>(includeSelf: true);
        if (row is null || !_rows.Contains(row))
        {
            return;
        }
        var rank = row.Rank;
        int? target = e.Key switch
        {
            Key.Down => Math.Min(_ranked.Length - 1, rank + 1),
            Key.Up => Math.Max(0, rank - 1),
            Key.Home => 0,
            Key.End => _ranked.Length - 1,
            Key.Space or Key.Enter => rank,
            _ => null
        };
        if (target is not { } next)
        {
            return;
        }
        e.Handled = true;
        Pick(_rows[_ranked[next]], NavigationMethod.Directional);
    }

    #endregion

    #region Refresh

    private ViewState CurrentState
    {
        get
        {
            if (IsLoading)
            {
                return ViewState.Loading;
            }
            if (!string.IsNullOrEmpty(ErrorText))
            {
                return ViewState.Error;
            }
            return _rows.Count == 0 && DisplayedModels.Count == 0 ? ViewState.Empty : ViewState.List;
        }
    }

    private void ApplyTexts()
    {
        var labels = Labels;
        var title = Title ?? labels.Title;
        if (_title is not null)
        {
            _title.Text = title;
        }
        if (_description is not null)
        {
            _description.Text = Description ?? labels.Description;
        }
        if (_note is not null)
        {
            _note.Text = Note ?? labels.Note;
        }
        if (_totalUnit is not null)
        {
            _totalUnit.Text = labels.PerMonth;
        }
        if (_cacheSwitch is not null)
        {
            _cacheSwitch.Content = labels.CachedInput;
        }
        SetText(_inputLabel, labels.Input);
        SetText(_cachedLabel, labels.Cached);
        SetText(_outputLabel, labels.Output);
        SetText(_perRequestLabel, labels.PerRequest);
        SetText(_errorTitle, labels.ErrorTitle);
        SetText(_emptyTitle, labels.EmptyTitle);
        SetText(_emptyText, labels.EmptyText);
        if (_retry is not null)
        {
            _retry.Content = labels.TryAgain;
        }
        if (_list is not null)
        {
            AutomationProperties.SetName(_list, labels.ModelList);
        }
        if (_skeleton is not null)
        {
            AutomationProperties.SetName(_skeleton, labels.Loading);
        }
        AutomationProperties.SetName(this, title);
    }

    private static void SetText(TextBlock? block, string text)
    {
        if (block is not null && block.Text != text)
        {
            block.Text = text;
        }
    }

    /// <summary>Recomputes every cost and brings the rows, the total, the breakdown and the state in line.</summary>
    private void Refresh(bool animate)
    {
        if (_list is null)
        {
            return;
        }
        animate &= IsLoaded;
        var usage = Usage;
        var labels = Labels;
        var state = CurrentState;
        PseudoClasses.Set(LoadingPseudoClass, state == ViewState.Loading);
        PseudoClasses.Set(ErrorPseudoClass, state == ViewState.Error);
        PseudoClasses.Set(EmptyPseudoClass, state == ViewState.Empty);
        PseudoClasses.Set(NoDataPseudoClass, state != ViewState.List);

        var enabled = state != ViewState.Loading;
        foreach (var control in new Control?[] { _presets, _inputSlider, _outputSlider, _requestsSlider, _cacheSwitch })
        {
            if (control is not null)
            {
                control.IsEnabled = enabled;
            }
        }
        SetLayer(_list, state == ViewState.List);
        SetLayer(_error, state == ViewState.Error);
        SetLayer(_empty, state == ViewState.Empty);
        SetLayer(_skeleton, state == ViewState.Loading);
        if (_skeleton is not null)
        {
            // The list area keeps one height in every state: as many placeholder rows as there are models (eight before any arrive).
            _skeleton.ModelRows = _rows.Count;
            _skeleton.IsPulsing = state == ViewState.Loading;
        }
        if (_errorText is not null)
        {
            _errorText.Text = ErrorText;
            _errorText.IsVisible = !string.IsNullOrEmpty(ErrorText);
        }
        if (_retry is not null)
        {
            _retry.IsVisible = AllowsRetry;
        }
        if (_cacheShare is not null)
        {
            _cacheShare.Text = labels.CacheShare(CacheHitRate);
        }

        // Costs, ranks and bars.
        var models = _rows.Select(r => r.Model).ToList();
        _costs = [.. models.Select(m => LlmCostCalculatorMath.Compute(m, usage, CacheHitRate, DaysPerMonth))];
        _ranked = LlmCostCalculatorMath.Rank(_costs);
        var max = _costs.Length > 0 ? _costs.Max(c => c.Total) : 0;
        _selectedIndex = ResolveSelected();
        for (var rank = 0; rank < _ranked.Length; rank++)
        {
            var index = _ranked[rank];
            var row = _rows[index];
            var figure = LlmCostCalculatorMath.Monthly(_costs[index].Total);
            row.SetCost(figure, max > 0 ? _costs[index].Total / max : 0, animate);
            row.SetRank(rank, _list.RowHeight, animate);
            row.SetSelected(index == _selectedIndex);
            AutomationProperties.SetName(row,
                labels.Row(row.Model.Name, row.Model.Provider, rank + 1, _ranked.Length, figure.Text));
        }

        var selected = _selectedIndex >= 0 ? _rows[_selectedIndex].Model : null;
        var cost = _selectedIndex >= 0 ? _costs[_selectedIndex] : default;
        SelectedModel = state == ViewState.List ? selected : null;
        SelectedCost = state == ViewState.List ? cost : default;

        SetFigure(_total, LlmCostCalculatorMath.Total(cost.Total), animate);
        SetFigure(_inputCost, LlmCostCalculatorMath.Monthly(cost.Input), animate);
        SetFigure(_cachedCost, LlmCostCalculatorMath.Monthly(cost.CachedInput), animate);
        SetFigure(_outputCost, LlmCostCalculatorMath.Monthly(cost.Output), animate);
        SetFigure(_perRequestCost, LlmCostCalculatorMath.PerRequest(cost.PerRequest), animate);
        ShowSelection(state == ViewState.List ? selected : null);
        ScheduleAnnouncement(state);
    }

    private static void SetLayer(Control? layer, bool shown)
    {
        if (layer is null)
        {
            return;
        }
        // Hidden layers stay measured (the area keeps its height) and fade; they take no input or focus.
        layer.IsEnabled = shown;
        layer.IsHitTestVisible = shown;
        layer.Opacity = shown ? 1 : 0;
    }

    private static void SetFigure(RollingDigits? digits, LlmCostFigure figure, bool animate)
    {
        if (digits is null)
        {
            return;
        }
        digits.IsInstant = !animate;
        LlmCostCalculatorRow.ApplyFigure(digits, figure);
        digits.IsInstant = false;
    }

    /// <summary>The selected model: the id given, the cheapest when that id is unknown, the second model when none is given.</summary>
    private int ResolveSelected()
    {
        if (_rows.Count == 0)
        {
            return -1;
        }
        if (SelectedId is { } id)
        {
            var found = _rows.FindIndex(r => r.Model.Id == id);
            return found >= 0 ? found : _ranked[0];
        }
        return _rows.Count > 1 ? 1 : 0;
    }

    /// <summary>The model name under the total and the price line crossfade (100ms out, 160ms in) only when the selection changes.</summary>
    private void ShowSelection(LlmModelPrice? model)
    {
        var labels = Labels;
        var name = model is null ? string.Empty : labels.Selected(model.Name, model.Provider);
        var priceLine = model is null
            ? string.Empty
            : labels.PriceLine(LlmCostCalculatorMath.Price(model.Input),
                model.CachedInput is { } cached ? LlmCostCalculatorMath.Price(cached) : null,
                LlmCostCalculatorMath.Price(model.Output));
        if (ReferenceEquals(model, _shownNameModel) && name == _shownName && priceLine == _shownPriceLine)
        {
            return;
        }
        _shownNameModel = model;
        if (_selectedName is not null && name != _shownName)
        {
            _selectedName.Content = new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis };
        }
        if (_priceLine is not null && priceLine != _shownPriceLine)
        {
            _priceLine.Content = new TextBlock { Text = priceLine, TextWrapping = TextWrapping.Wrap };
        }
        _shownName = name;
        _shownPriceLine = priceLine;
    }

    /// <summary>
    /// The status region speaks the selected model's cost and rank once the numbers have been still for 450ms, so a drag
    /// does not flood a screen reader.
    /// </summary>
    private void ScheduleAnnouncement(ViewState state)
    {
        if (!IsLoaded || state != ViewState.List || _announcer is null)
        {
            _announceTimer?.Stop();
            return;
        }
        if (_announceTimer is null)
        {
            _announceTimer = new DispatcherTimer { Interval = AnnounceDelay };
            _announceTimer.Tick += (_, _) =>
            {
                _announceTimer.Stop();
                Announce();
            };
        }
        _announceTimer.Stop();
        _announceTimer.Start();
    }

    /// <summary>Speaks the current status now (the timer calls it; tests call it to skip the wait).</summary>
    internal void Announce()
    {
        _announceTimer?.Stop();
        if (_announcer is null || _selectedIndex < 0 || CurrentState != ViewState.List)
        {
            return;
        }
        var labels = Labels;
        var model = _rows[_selectedIndex].Model;
        var text = labels.Status(model.Name, LlmCostCalculatorMath.Total(_costs[_selectedIndex].Total).Text,
            _rows[_selectedIndex].Rank + 1, _rows.Count);
        if (text == _announced)
        {
            return;
        }
        _announced = text;
        _announcer.Announce(text);
    }

    #endregion

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new PanelPeer(this);

    private sealed class PanelPeer(LlmCostCalculator owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => AutomationProperties.GetName(Owner);
    }
}
