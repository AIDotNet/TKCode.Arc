using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// An animated explainer of a sparse mixture-of-experts layer. Tokens enter one at a time; a router scores every expert
/// (a small bar each), the token travels to its top-k experts, which light up in a first and second choice color and
/// count it, and their outputs merge into the token's output chip. Experts have a capacity: a full expert skips the
/// token, and a token whose every choice is full passes through unchanged. The header switches between top 1 and top 2
/// and shows the parameters that run per token; a load balance bar sits under the stage. Pointing at, focusing or
/// pinning a token or an expert freezes the story on it and shows its route or its load. The routing is computed from
/// the tokens' scores (or stable scores derived from their text); the numbers are illustrative.
/// </summary>
[TemplatePart(StagePart, typeof(MixtureOfExpertsStage))]
[TemplatePart(SequencePart, typeof(MixtureOfExpertsSequence))]
[TemplatePart(TopKPart, typeof(SegmentedControl))]
[PseudoClasses(NarrowPseudoClass, CompactPseudoClass, InspectingPseudoClass)]
public partial class MixtureOfExperts : Explainer
{
    /// <summary>The stage part (drawn router, lines and packets, with the expert tiles).</summary>
    public const string StagePart = "PART_Stage";

    /// <summary>The token sequence part.</summary>
    public const string SequencePart = "PART_Sequence";

    /// <summary>The top-k switch part.</summary>
    public const string TopKPart = "PART_TopK";

    /// <summary>Set when the card is narrower than 568 px: the stage runs top to bottom.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set when the card is narrower than 480 px: tighter padding, the readout on its own line.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while a token or an expert is inspected (the story is frozen on it).</summary>
    public const string InspectingPseudoClass = ":inspecting";

    /// <summary>The narrowest card with the wide (left to right) stage.</summary>
    public const double WideMinWidth = 568;

    /// <summary>The narrowest card with the regular padding and a one-line header.</summary>
    public const double CompactMaxWidth = 480;

    /// <summary>
    /// The tokens to route, in order. Leave it empty for the default sentence (see <see cref="MixtureOfExpertsSamples"/>).
    /// Changing the tokens starts the story over.
    /// </summary>
    public static readonly StyledProperty<AvaloniaList<MixtureOfExpertsTokenItem>> TokensProperty =
        AvaloniaProperty.Register<MixtureOfExperts, AvaloniaList<MixtureOfExpertsTokenItem>>(nameof(Tokens));

    /// <summary>
    /// The experts, 2 to 12 (more are cut to the first 12). Leave it empty, or give fewer than two, for the eight default
    /// experts. Changing the experts starts the story over.
    /// </summary>
    public static readonly StyledProperty<AvaloniaList<MixtureOfExpertsExpert>> ExpertsProperty =
        AvaloniaProperty.Register<MixtureOfExperts, AvaloniaList<MixtureOfExpertsExpert>>(nameof(Experts));

    /// <summary>
    /// How many experts each token goes to: 1 or 2 (default 2). The header's switch sets it; a change re-plans every
    /// route and carries on from the same point of the story.
    /// </summary>
    public static readonly StyledProperty<int> TopKProperty =
        AvaloniaProperty.Register<MixtureOfExperts, int>(nameof(TopK), 2, coerce: CoerceTopK, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The parameter counts in billions behind the active-parameters readout (default 480 in all, 6 shared, 12 per
    /// expert): shared + k × per expert run for each token.
    /// </summary>
    public static readonly StyledProperty<MixtureOfExpertsParameters> ParametersProperty =
        AvaloniaProperty.Register<MixtureOfExperts, MixtureOfExpertsParameters>(nameof(Parameters), MixtureOfExpertsParameters.Default);

    /// <summary>
    /// Scales each expert's capacity, ⌈factor × tokens × k / experts⌉ tokens (default 1). A full expert skips further
    /// tokens. <see cref="double.PositiveInfinity"/> never drops a token; zero, negative and NaN values count as 1.
    /// </summary>
    public static readonly StyledProperty<double> CapacityFactorProperty =
        AvaloniaProperty.Register<MixtureOfExperts, double>(nameof(CapacityFactor), 1, coerce: CoerceCapacityFactor);

    /// <summary>The first choice's color (null: chart series 1, which follows the accent).</summary>
    public static readonly StyledProperty<IBrush?> FirstChoiceBrushProperty =
        AvaloniaProperty.Register<MixtureOfExperts, IBrush?>(nameof(FirstChoiceBrush));

    /// <summary>The second choice's color (null: chart series 2).</summary>
    public static readonly StyledProperty<IBrush?> SecondChoiceBrushProperty =
        AvaloniaProperty.Register<MixtureOfExperts, IBrush?>(nameof(SecondChoiceBrush));

    // Declared before the property that uses it: static fields initialize in order.
    private static readonly MixtureOfExpertsLabels s_defaultLabels = new();

    /// <summary>Every word the card shows or speaks; replace it to translate or reword.</summary>
    public static readonly StyledProperty<MixtureOfExpertsLabels> LabelsProperty =
        AvaloniaProperty.Register<MixtureOfExperts, MixtureOfExpertsLabels>(nameof(Labels), s_defaultLabels, coerce: (_, value) => value ?? s_defaultLabels);

    /// <summary>The planned route of every token for the current top k and capacity.</summary>
    public static readonly DirectProperty<MixtureOfExperts, IReadOnlyList<MixtureOfExpertsRoute>> RoutesProperty =
        AvaloniaProperty.RegisterDirect<MixtureOfExperts, IReadOnlyList<MixtureOfExpertsRoute>>(nameof(Routes), o => o.Routes);

    /// <summary>The tokens each expert takes before it is full, or null when nothing is ever dropped.</summary>
    public static readonly DirectProperty<MixtureOfExperts, int?> CapacityProperty =
        AvaloniaProperty.RegisterDirect<MixtureOfExperts, int?>(nameof(Capacity), o => o.Capacity);

    /// <summary>The parameters that run for one token at the current top k, in billions.</summary>
    public static readonly DirectProperty<MixtureOfExperts, double> ActiveParametersProperty =
        AvaloniaProperty.RegisterDirect<MixtureOfExperts, double>(nameof(ActiveParameters), o => o.ActiveParameters);

    /// <summary>The token being routed, or −1 while the summary shows.</summary>
    public static readonly DirectProperty<MixtureOfExperts, int> CurrentTokenIndexProperty =
        AvaloniaProperty.RegisterDirect<MixtureOfExperts, int>(nameof(CurrentTokenIndex), o => o.CurrentTokenIndex);

    /// <summary>The load balance the card shows, 0 to 1; null before anything is routed.</summary>
    public static readonly DirectProperty<MixtureOfExperts, double?> LoadBalanceProperty =
        AvaloniaProperty.RegisterDirect<MixtureOfExperts, double?>(nameof(LoadBalance), o => o.LoadBalance);

    /// <summary>The token being inspected (hovered, keyboard-focused or pinned), or −1.</summary>
    public static readonly DirectProperty<MixtureOfExperts, int> InspectedTokenIndexProperty =
        AvaloniaProperty.RegisterDirect<MixtureOfExperts, int>(nameof(InspectedTokenIndex), o => o.InspectedTokenIndex);

    /// <summary>The expert being inspected (hovered, keyboard-focused or pinned), or −1.</summary>
    public static readonly DirectProperty<MixtureOfExperts, int> InspectedExpertIndexProperty =
        AvaloniaProperty.RegisterDirect<MixtureOfExperts, int>(nameof(InspectedExpertIndex), o => o.InspectedExpertIndex);

    /// <summary>Raised when a token leaves the router for its experts, with its route.</summary>
    public static readonly RoutedEvent<MixtureOfExpertsRoutedEventArgs> RoutedEvent =
        Avalonia.Interactivity.RoutedEvent.Register<MixtureOfExperts, MixtureOfExpertsRoutedEventArgs>(nameof(Routed), RoutingStrategies.Bubble);

    // Ink: ArcDesign token colors set by the theme, captured into the palette the drawn parts use.
    internal static readonly StyledProperty<IBrush?> TextSecondaryInkProperty =
        AvaloniaProperty.Register<MixtureOfExperts, IBrush?>(nameof(TextSecondaryInk));
    internal static readonly StyledProperty<IBrush?> TextMutedInkProperty =
        AvaloniaProperty.Register<MixtureOfExperts, IBrush?>(nameof(TextMutedInk));
    internal static readonly StyledProperty<IBrush?> BorderInkProperty =
        AvaloniaProperty.Register<MixtureOfExperts, IBrush?>(nameof(BorderInk));
    internal static readonly StyledProperty<IBrush?> BorderStrongInkProperty =
        AvaloniaProperty.Register<MixtureOfExperts, IBrush?>(nameof(BorderStrongInk));
    internal static readonly StyledProperty<IBrush?> SurfaceInkProperty =
        AvaloniaProperty.Register<MixtureOfExperts, IBrush?>(nameof(SurfaceInk));
    internal static readonly StyledProperty<IBrush?> SurfaceMutedInkProperty =
        AvaloniaProperty.Register<MixtureOfExperts, IBrush?>(nameof(SurfaceMutedInk));
    internal static readonly StyledProperty<IBrush?> WarningInkProperty =
        AvaloniaProperty.Register<MixtureOfExperts, IBrush?>(nameof(WarningInk));
    internal static readonly StyledProperty<IBrush?> SuccessInkProperty =
        AvaloniaProperty.Register<MixtureOfExperts, IBrush?>(nameof(SuccessInk));
    internal static readonly StyledProperty<IBrush?> FocusRingInkProperty =
        AvaloniaProperty.Register<MixtureOfExperts, IBrush?>(nameof(FocusRingInk));
    internal static readonly StyledProperty<IBrush?> Series1InkProperty =
        AvaloniaProperty.Register<MixtureOfExperts, IBrush?>(nameof(Series1Ink));
    internal static readonly StyledProperty<IBrush?> Series2InkProperty =
        AvaloniaProperty.Register<MixtureOfExperts, IBrush?>(nameof(Series2Ink));

    private static readonly ExplainerPlaybackLabels s_playbackLabels = new() { Replay = "Route again" };

    private readonly MixtureOfExpertsPalette _palette = new();
    private readonly List<(string Name, string? Handles, Geometry? Icon)> _experts = new();
    private readonly List<(string Text, IReadOnlyList<double>? Scores)> _tokens = new();
    private AvaloniaList<MixtureOfExpertsTokenItem>? _observedTokens;
    private AvaloniaList<MixtureOfExpertsExpert>? _observedExperts;
    private readonly List<AvaloniaObject> _observedItems = new();
    private MixtureOfExpertsPlan _plan = MixtureOfExpertsPlanner.Plan([], MixtureOfExpertsPlanner.MinExperts, 2, 1);
    private int _planVersion;
    private bool _dataDirty = true;
    private bool _dataPosted;
    private IReadOnlyList<MixtureOfExpertsRoute> _routes = [];
    private int? _capacity;
    private double _activeParameters;
    private int _currentTokenIndex = -1;
    private double? _loadBalance;
    private int _inspectedTokenIndex = -1;
    private int _inspectedExpertIndex = -1;

    // Template parts.
    private MixtureOfExpertsStage? _stage;
    private MixtureOfExpertsSequence? _sequence;
    private SegmentedControl? _topK;
    private Control? _parameters;
    private TextBlock? _parametersLabel;
    private RollingDigits? _parametersValue;
    private TextBlock? _parametersTotal;
    private ArcContentSwap? _routeLine;
    private ArcContentSwap? _note;
    private Control? _loadHead;
    private TextBlock? _loadLabel;
    private RollingDigits? _loadValue;
    private TextBlock? _loadEmpty;
    private MixtureOfExpertsLoadBar? _loadBar;
    private AvaloniaButton? _playButton;
    private bool _syncingTopK;

    static MixtureOfExperts()
    {
        IsLoopingProperty.OverrideMetadata<MixtureOfExperts>(new StyledPropertyMetadata<bool>(true, coerce: CoerceLooping));
        TitleProperty.OverrideDefaultValue<MixtureOfExperts>("Mixture of experts routing");
        PlaybackLabelsProperty.OverrideDefaultValue<MixtureOfExperts>(s_playbackLabels);
    }

    /// <summary>Creates the card with the default story (eight experts, fifteen tokens).</summary>
    public MixtureOfExperts()
    {
        SetCurrentValue(TokensProperty, new AvaloniaList<MixtureOfExpertsTokenItem>());
        SetCurrentValue(ExpertsProperty, new AvaloniaList<MixtureOfExpertsExpert>());
        AutomationProperties.SetName(this, Title);
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Group);
        RefreshData();
    }

    /// <inheritdoc cref="TokensProperty"/>
    [Content]
    public AvaloniaList<MixtureOfExpertsTokenItem> Tokens { get => GetValue(TokensProperty); set => SetValue(TokensProperty, value); }

    /// <inheritdoc cref="ExpertsProperty"/>
    public AvaloniaList<MixtureOfExpertsExpert> Experts { get => GetValue(ExpertsProperty); set => SetValue(ExpertsProperty, value); }

    /// <inheritdoc cref="TopKProperty"/>
    public int TopK { get => GetValue(TopKProperty); set => SetValue(TopKProperty, value); }

    /// <inheritdoc cref="ParametersProperty"/>
    public MixtureOfExpertsParameters Parameters { get => GetValue(ParametersProperty); set => SetValue(ParametersProperty, value); }

    /// <inheritdoc cref="CapacityFactorProperty"/>
    public double CapacityFactor { get => GetValue(CapacityFactorProperty); set => SetValue(CapacityFactorProperty, value); }

    /// <inheritdoc cref="FirstChoiceBrushProperty"/>
    public IBrush? FirstChoiceBrush { get => GetValue(FirstChoiceBrushProperty); set => SetValue(FirstChoiceBrushProperty, value); }

    /// <inheritdoc cref="SecondChoiceBrushProperty"/>
    public IBrush? SecondChoiceBrush { get => GetValue(SecondChoiceBrushProperty); set => SetValue(SecondChoiceBrushProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public MixtureOfExpertsLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="RoutesProperty"/>
    public IReadOnlyList<MixtureOfExpertsRoute> Routes { get => _routes; private set => SetAndRaise(RoutesProperty, ref _routes, value); }

    /// <inheritdoc cref="CapacityProperty"/>
    public int? Capacity { get => _capacity; private set => SetAndRaise(CapacityProperty, ref _capacity, value); }

    /// <inheritdoc cref="ActiveParametersProperty"/>
    public double ActiveParameters { get => _activeParameters; private set => SetAndRaise(ActiveParametersProperty, ref _activeParameters, value); }

    /// <inheritdoc cref="CurrentTokenIndexProperty"/>
    public int CurrentTokenIndex { get => _currentTokenIndex; private set => SetAndRaise(CurrentTokenIndexProperty, ref _currentTokenIndex, value); }

    /// <inheritdoc cref="LoadBalanceProperty"/>
    public double? LoadBalance { get => _loadBalance; private set => SetAndRaise(LoadBalanceProperty, ref _loadBalance, value); }

    /// <inheritdoc cref="InspectedTokenIndexProperty"/>
    public int InspectedTokenIndex { get => _inspectedTokenIndex; private set => SetAndRaise(InspectedTokenIndexProperty, ref _inspectedTokenIndex, value); }

    /// <inheritdoc cref="InspectedExpertIndexProperty"/>
    public int InspectedExpertIndex { get => _inspectedExpertIndex; private set => SetAndRaise(InspectedExpertIndexProperty, ref _inspectedExpertIndex, value); }

    /// <inheritdoc cref="RoutedEvent"/>
    public event EventHandler<MixtureOfExpertsRoutedEventArgs>? Routed
    {
        add => AddHandler(RoutedEvent, value);
        remove => RemoveHandler(RoutedEvent, value);
    }

    internal IBrush? TextSecondaryInk { get => GetValue(TextSecondaryInkProperty); set => SetValue(TextSecondaryInkProperty, value); }
    internal IBrush? TextMutedInk { get => GetValue(TextMutedInkProperty); set => SetValue(TextMutedInkProperty, value); }
    internal IBrush? BorderInk { get => GetValue(BorderInkProperty); set => SetValue(BorderInkProperty, value); }
    internal IBrush? BorderStrongInk { get => GetValue(BorderStrongInkProperty); set => SetValue(BorderStrongInkProperty, value); }
    internal IBrush? SurfaceInk { get => GetValue(SurfaceInkProperty); set => SetValue(SurfaceInkProperty, value); }
    internal IBrush? SurfaceMutedInk { get => GetValue(SurfaceMutedInkProperty); set => SetValue(SurfaceMutedInkProperty, value); }
    internal IBrush? WarningInk { get => GetValue(WarningInkProperty); set => SetValue(WarningInkProperty, value); }
    internal IBrush? SuccessInk { get => GetValue(SuccessInkProperty); set => SetValue(SuccessInkProperty, value); }
    internal IBrush? FocusRingInk { get => GetValue(FocusRingInkProperty); set => SetValue(FocusRingInkProperty, value); }
    internal IBrush? Series1Ink { get => GetValue(Series1InkProperty); set => SetValue(Series1InkProperty, value); }
    internal IBrush? Series2Ink { get => GetValue(Series2InkProperty); set => SetValue(Series2InkProperty, value); }

    internal MixtureOfExpertsPalette Palette => _palette;

    internal MixtureOfExpertsPlan Plan => _plan;

    internal MixtureOfExpertsStage? Stage => _stage;

    internal MixtureOfExpertsSequence? Sequence => _sequence;

    internal MixtureOfExpertsLoadBar? LoadBar => _loadBar;

    /// <summary>Wall time for the parts' fades.</summary>
    internal double Wall => WallTime;

    /// <summary>
    /// Pins a token: the story pauses on its route until <see cref="Unpin"/>, Escape, or a second press on it. An index out
    /// of range does nothing.
    /// </summary>
    public void PinToken(int index)
    {
        EnsureData();
        if (index >= 0 && index < _tokens.Count)
        {
            Pin(MixtureOfExpertsInspectKind.Token, index);
        }
    }

    /// <summary>Pins an expert: the story pauses on its load and the tokens it took until <see cref="Unpin"/>.</summary>
    public void PinExpert(int index)
    {
        EnsureData();
        if (index >= 0 && index < _experts.Count)
        {
            Pin(MixtureOfExpertsInspectKind.Expert, index);
        }
    }

    /// <summary>Releases a pinned token or expert; the story plays on if pinning paused it.</summary>
    public void Unpin() => ReleasePin(announce: true);

    private static int CoerceTopK(AvaloniaObject owner, int value) => Math.Clamp(value, 1, 2);

    private static double CoerceCapacityFactor(AvaloniaObject owner, double value)
        => double.IsPositiveInfinity(value) || (double.IsFinite(value) && value > 0) ? value : 1;

    private static bool CoerceLooping(AvaloniaObject owner, bool value) => value && !((MixtureOfExperts)owner)._reducedRun;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_topK is not null)
        {
            _topK.SelectionChanged -= OnTopKSelectionChanged;
        }
        if (_playButton is not null)
        {
            _playButton.Click -= OnPlayClick;
        }
        base.OnApplyTemplate(e);
        _stage = e.NameScope.Find<MixtureOfExpertsStage>(StagePart);
        _sequence = e.NameScope.Find<MixtureOfExpertsSequence>(SequencePart);
        _topK = e.NameScope.Find<SegmentedControl>(TopKPart);
        _parameters = e.NameScope.Find<Control>("PART_Parameters");
        _parametersLabel = e.NameScope.Find<TextBlock>("PART_ParametersLabel");
        _parametersValue = e.NameScope.Find<RollingDigits>("PART_ParametersValue");
        _parametersTotal = e.NameScope.Find<TextBlock>("PART_ParametersTotal");
        _routeLine = e.NameScope.Find<ArcContentSwap>("PART_RouteLine");
        _note = e.NameScope.Find<ArcContentSwap>("PART_Note");
        _loadHead = e.NameScope.Find<Control>("PART_LoadHead");
        _loadLabel = e.NameScope.Find<TextBlock>("PART_LoadLabel");
        _loadValue = e.NameScope.Find<RollingDigits>("PART_LoadValue");
        _loadEmpty = e.NameScope.Find<TextBlock>("PART_LoadEmpty");
        _loadBar = e.NameScope.Find<MixtureOfExpertsLoadBar>("PART_LoadBar");
        _playButton = e.NameScope.Find<AvaloniaButton>(PlayButtonPart);
        if (_playButton is not null)
        {
            _playButton.Click += OnPlayClick;
        }
        foreach (var child in new Control?[] { _parametersLabel, _parametersValue, _parametersTotal, _loadLabel, _loadValue, _loadEmpty })
        {
            if (child is not null)
            {
                AutomationProperties.SetAccessibilityView(child, AccessibilityView.Raw);
            }
        }
        if (_topK is not null)
        {
            _topK.SelectionChanged += OnTopKSelectionChanged;
        }
        CapturePalette();
        EnsureData();
        PushData();
        UpdateTexts();
        UpdateParameters(animate: false);
        _sceneApplied = false;
        _footerKey = default;
        RefreshScene(force: true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == TokensProperty || property == ExpertsProperty)
        {
            ObserveCollections();
            ScheduleDataChanged();
        }
        else if (property == TopKProperty)
        {
            OnRoutingChanged(announceTopK: true);
        }
        else if (property == CapacityFactorProperty)
        {
            OnRoutingChanged(announceTopK: false);
        }
        else if (property == ParametersProperty)
        {
            UpdateParameters(animate: true);
            _footerKey = default;
            RefreshScene(force: true);
        }
        else if (property == LabelsProperty)
        {
            UpdateTexts();
            UpdateTokenNames();
            _footerKey = default;
            _stage?.InvalidateVisual();
            RefreshScene(force: true);
        }
        else if (property == TitleProperty)
        {
            AutomationProperties.SetName(this, Title);
        }
        else if (property == BoundsProperty)
        {
            var width = Bounds.Width;
            PseudoClasses.Set(NarrowPseudoClass, width > 0 && width < WideMinWidth);
            PseudoClasses.Set(CompactPseudoClass, width > 0 && width < CompactMaxWidth);
            if (_isCompact != (width > 0 && width < CompactMaxWidth))
            {
                _isCompact = !_isCompact;
                _footerKey = default;
                RefreshScene(force: true);
            }
        }
        else if (property == ForegroundProperty || property == FontFamilyProperty || property == FirstChoiceBrushProperty ||
                 property == SecondChoiceBrushProperty || (property.OwnerType == typeof(MixtureOfExperts) && property.Name.EndsWith("Ink", StringComparison.Ordinal)))
        {
            SchedulePalette();
        }
    }

    private bool _palettePosted;

    /// <summary>A theme switch changes every ink at once: capture the palette once, before the next frame renders.</summary>
    private void SchedulePalette()
    {
        if (!_palette.IsReady)
        {
            CapturePalette();
            return;
        }
        if (_palettePosted)
        {
            return;
        }
        _palettePosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _palettePosted = false;
            CapturePalette();
        }, DispatcherPriority.Normal);
    }

    private bool _isCompact;

    private void CapturePalette()
    {
        _palette.Capture(this);
        _footerKey = default;
        if (_stage is not null)
        {
            _stage.InvalidateVisual();
            _stage.Packets.InvalidateVisual();
            foreach (var tile in _stage.Tiles)
            {
                tile.Repaint();
            }
        }
        _sequence?.RemeasureTokens();
        if (_sequence is not null)
        {
            foreach (var token in _sequence.Tokens)
            {
                token.InvalidateVisual();
            }
        }
        _loadBar?.InvalidateVisual();
        if (_sceneApplied)
        {
            RefreshScene(force: true);
        }
    }

    // Data ---------------------------------------------------------------------------------------------------------------

    private void ObserveCollections()
    {
        if (_observedTokens is not null)
        {
            _observedTokens.CollectionChanged -= OnCollectionChanged;
        }
        if (_observedExperts is not null)
        {
            _observedExperts.CollectionChanged -= OnCollectionChanged;
        }
        _observedTokens = Tokens;
        _observedExperts = Experts;
        if (_observedTokens is not null)
        {
            _observedTokens.CollectionChanged += OnCollectionChanged;
        }
        if (_observedExperts is not null)
        {
            _observedExperts.CollectionChanged += OnCollectionChanged;
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleDataChanged();

    private void OnItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => ScheduleDataChanged();

    /// <summary>Coalesces data changes (XAML adds items one by one) into one re-plan and restart.</summary>
    private void ScheduleDataChanged()
    {
        _dataDirty = true;
        if (_dataPosted)
        {
            return;
        }
        _dataPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _dataPosted = false;
            if (!_dataDirty)
            {
                return;
            }
            RefreshData();
            PushData();
            Rebuild();
            RefreshScene(force: true);
        }, DispatcherPriority.Background);
    }

    private void EnsureData()
    {
        if (_dataDirty)
        {
            RefreshData();
        }
    }

    /// <summary>Reads the effective experts and tokens (defaults when empty) and plans every route.</summary>
    private void RefreshData()
    {
        _dataDirty = false;
        foreach (var item in _observedItems)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }
        _observedItems.Clear();
        _experts.Clear();
        var experts = Experts;
        if (experts is { Count: >= MixtureOfExpertsPlanner.MinExperts })
        {
            foreach (var expert in experts.Take(MixtureOfExpertsPlanner.MaxExperts))
            {
                _experts.Add((expert.Name, expert.Handles, expert.Icon));
                expert.PropertyChanged += OnItemPropertyChanged;
                _observedItems.Add(expert);
            }
        }
        else
        {
            foreach (var expert in MixtureOfExpertsSamples.CreateExperts())
            {
                _experts.Add((expert.Name, expert.Handles, expert.Icon));
            }
        }
        _tokens.Clear();
        var tokens = Tokens;
        if (tokens is { Count: > 0 })
        {
            foreach (var token in tokens)
            {
                _tokens.Add((token.Text ?? string.Empty, token.Scores));
                token.PropertyChanged += OnItemPropertyChanged;
                _observedItems.Add(token);
            }
        }
        else
        {
            foreach (var token in MixtureOfExpertsSamples.CreateTokens())
            {
                _tokens.Add((token.Text, token.Scores));
            }
        }
        Replan();
    }

    private void Replan()
    {
        _plan = MixtureOfExpertsPlanner.Plan(_tokens, _experts.Count, TopK, CapacityFactor);
        _planVersion++;
        Routes = _plan.Routes;
        Capacity = _plan.IsUnlimited ? null : _plan.Capacity;
        ActiveParameters = Parameters.Active(_plan.K);
        UpdateTokenNames();
    }

    /// <summary>Hands the experts and tokens to the stage, the sequence and the load bar.</summary>
    private void PushData()
    {
        _stage?.SetExperts(this, _experts.Select(e => (e.Name, e.Icon)).ToArray());
        _sequence?.SetTokens(this, _tokens.Select(t => t.Text).ToArray());
        _loadBar?.Attach(this, _experts.Count);
        UpdateTokenNames();
        _sceneApplied = false;
    }

    /// <summary>Top k or capacity changed: re-plan every route and carry on from the same point of the story.</summary>
    private void OnRoutingChanged(bool announceTopK)
    {
        if (_dataDirty)
        {
            RefreshData();
        }
        else
        {
            Replan();
        }
        if (_syncingTopK is false && _topK is not null && _topK.SelectedIndex != TopK - 1)
        {
            _syncingTopK = true;
            _topK.SelectedIndex = TopK - 1;
            _syncingTopK = false;
        }
        UpdateParameters(animate: true);
        _footerKey = default;
        RefreshScene(force: true);
        if (announceTopK)
        {
            var p = Parameters;
            Announce(Labels.TopKChanged(TopK, FormatBillions(p.Active(TopK)), FormatBillions(p.Total)));
        }
    }

    private void OnTopKSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingTopK || _topK is null || _topK.SelectedIndex < 0)
        {
            return;
        }
        SetCurrentValue(TopKProperty, _topK.SelectedIndex + 1);
    }

    private void UpdateTexts()
    {
        var labels = Labels;
        if (_topK is not null)
        {
            _syncingTopK = true;
            _topK.ItemsSource = new[] { new SegmentedOption(1, labels.TopOne), new SegmentedOption(2, labels.TopTwo) };
            _topK.SelectedIndex = TopK - 1;
            AutomationProperties.SetName(_topK, labels.TopKGroup);
            _syncingTopK = false;
        }
        if (_parametersLabel is not null)
        {
            _parametersLabel.Text = labels.ActiveParameters;
        }
        if (_loadLabel is not null)
        {
            _loadLabel.Text = labels.LoadBalance;
        }
        if (_loadEmpty is not null)
        {
            _loadEmpty.Text = labels.NothingRouted;
        }
        if (_sequence is not null)
        {
            AutomationProperties.SetName(_sequence, labels.TokensGroup);
        }
        if (_stage is not null)
        {
            AutomationProperties.SetName(_stage, labels.ExpertsGroup);
        }
        UpdateParameters(animate: false);
    }

    private void UpdateParameters(bool animate)
    {
        var p = Parameters;
        var active = p.Active(TopK);
        ActiveParameters = active;
        if (_parametersValue is not null)
        {
            _parametersValue.Decimals = HasFraction(active) || HasFraction(p.Total) ? 1 : 0;
            _parametersValue.Value = active;
        }
        if (_parametersTotal is not null)
        {
            _parametersTotal.Text = Labels.OfTotal(FormatBillions(p.Total));
        }
        if (_parameters is not null)
        {
            AutomationProperties.SetName(_parameters, Labels.ParametersSpoken(FormatBillions(active), FormatBillions(p.Total)));
        }
    }

    private static bool HasFraction(double value) => Math.Abs(value - Math.Round(value)) > 1e-9;

    /// <summary>Billions as the readout shows them: whole numbers plain, others with one decimal ("2.5").</summary>
    internal static string FormatBillions(double value)
        => HasFraction(value) ? value.ToString("0.#", CultureInfo.InvariantCulture) : value.ToString("0", CultureInfo.InvariantCulture);

    /// <summary>The stage built a new layout (width or expert count): parts arranged, scene applies as is.</summary>
    internal void OnStageLayoutChanged()
    {
        _stage?.Packets.InvalidateVisual();
    }

    private void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        // The base has already paused, resumed or restarted the story.
        if (IsPlaying)
        {
            Announce(Now <= 0.0001 && _runs > 1 ? Labels.Restarted : Labels.Resumed);
        }
        else
        {
            Announce(Labels.Paused);
        }
    }
}
