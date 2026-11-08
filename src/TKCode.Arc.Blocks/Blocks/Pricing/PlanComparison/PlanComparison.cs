// Ported from Arc's free plan-comparison block (MIT, Copyright (c) 2026 Elia Kuratli).
using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A side-by-side plan comparison: a heading with a monthly / yearly switch, a feature count with a "Show differences
/// only" filter, one column per plan with its price and a select button, and a feature matrix. Switching the billing
/// period counts every price to the new amount; selecting a plan tints its column and slides a rail under it; the
/// filter collapses the rows every plan shares. Without plans and features it shows Arc's sample offering
/// (<see cref="PlanComparisonSamples"/>).
/// </summary>
/// <example><code>
/// &lt;arc:PlanComparison Billing="{Binding Billing}" SelectedPlan="{Binding Plan}"&gt;
///     &lt;arc:PlanComparison.Plans&gt;
///         &lt;arc:PlanComparisonPlan Name="Starter" Description="For one person" MonthlyPrice="9" YearlyPrice="7" /&gt;
///         &lt;arc:PlanComparisonPlan Name="Pro" Description="For a whole team" MonthlyPrice="24" YearlyPrice="19" /&gt;
///     &lt;/arc:PlanComparison.Plans&gt;
/// &lt;/arc:PlanComparison&gt;
/// </code></example>
[TemplatePart(BillingPart, typeof(SegmentedControl))]
[TemplatePart(DifferencesSwitchPart, typeof(Switch))]
[TemplatePart(MatrixPart, typeof(Border))]
[PseudoClasses(CompactPseudoClass, NarrowPseudoClass, TinyPseudoClass, FlatPseudoClass)]
public class PlanComparison : TemplatedControl
{
    public const string TitlePart = "PART_Title";
    public const string DescriptionPart = "PART_Description";
    public const string BillingPart = "PART_Billing";
    public const string BillingNotePart = "PART_BillingNote";
    public const string CompareTitlePart = "PART_CompareTitle";
    public const string FeatureCountPart = "PART_FeatureCount";
    public const string DifferencesSwitchPart = "PART_DifferencesSwitch";
    public const string MatrixPart = "PART_Matrix";
    public const string FootnotePart = "PART_Footnote";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Medium, narrow and tiny layouts (Arc's md, sm and xs, cumulative like Arc's selectors).</summary>
    public const string CompactPseudoClass = ":compact";
    public const string NarrowPseudoClass = ":narrow";
    public const string TinyPseudoClass = ":tiny";

    /// <summary>Set while <see cref="IsFramed"/> is false.</summary>
    public const string FlatPseudoClass = ":flat";

    /// <summary>Arc's <c>spring.responsive</c> (stiffness 520, damping 44) for the selected plan's rail.</summary>
    internal static readonly SpringSpec RailSpring = SpringSpec.FromPhysical(520, 44);

    /// <summary>Lucide "check".</summary>
    private static readonly Geometry s_check = ArcIcons.Parse("M20 6 9 17l-5-5");

    #region Properties

    /// <summary>The billing period the prices are shown for. Two-way: the billing switch edits it.</summary>
    public static readonly StyledProperty<PlanComparisonBilling> BillingProperty =
        AvaloniaProperty.Register<PlanComparison, PlanComparisonBilling>(nameof(Billing), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether only the features the plans do not share are listed. Two-way: the filter switch edits it.</summary>
    public static readonly StyledProperty<bool> IsDifferencesOnlyProperty =
        AvaloniaProperty.Register<PlanComparison, bool>(nameof(IsDifferencesOnly), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The chosen plan, or null. Two-way: the select buttons edit it (pressing the chosen plan's button clears it).</summary>
    public static readonly StyledProperty<PlanComparisonPlan?> SelectedPlanProperty =
        AvaloniaProperty.Register<PlanComparison, PlanComparisonPlan?>(nameof(SelectedPlan), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>A list of plans that replaces <see cref="Plans"/> when set; a list that raises collection changes is followed.</summary>
    public static readonly StyledProperty<IEnumerable<PlanComparisonPlan>?> PlansSourceProperty =
        AvaloniaProperty.Register<PlanComparison, IEnumerable<PlanComparisonPlan>?>(nameof(PlansSource));

    /// <summary>A list of features that replaces <see cref="Features"/> when set; a list that raises collection changes is followed.</summary>
    public static readonly StyledProperty<IEnumerable<PlanComparisonFeature>?> FeaturesSourceProperty =
        AvaloniaProperty.Register<PlanComparison, IEnumerable<PlanComparisonFeature>?>(nameof(FeaturesSource));

    /// <summary>ISO 4217 code of the prices ("USD" by default).</summary>
    public static readonly StyledProperty<string> CurrencyProperty =
        AvaloniaProperty.Register<PlanComparison, string>(nameof(Currency), "USD");

    /// <summary>
    /// Whether the block draws its own card (border, corner radius, surface and the lines between its sections, at most
    /// 1060 wide). Turn it off to set the block flush into a page section, as Arc's preview does.
    /// </summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<PlanComparison, bool>(nameof(IsFramed), true);

    /// <summary>Whether the monthly / yearly switch and its note show (true by default).</summary>
    public static readonly StyledProperty<bool> IsBillingVisibleProperty =
        AvaloniaProperty.Register<PlanComparison, bool>(nameof(IsBillingVisible), true);

    /// <summary>Whether the footnote under the matrix shows (true by default).</summary>
    public static readonly StyledProperty<bool> IsFootnoteVisibleProperty =
        AvaloniaProperty.Register<PlanComparison, bool>(nameof(IsFootnoteVisible), true);

    /// <summary>The built-in text and its culture; replace the object to localize the block.</summary>
    public static readonly StyledProperty<PlanComparisonLabels> LabelsProperty =
        AvaloniaProperty.Register<PlanComparison, PlanComparisonLabels>(nameof(Labels), new PlanComparisonLabels(),
            coerce: (_, value) => value ?? new PlanComparisonLabels());

    /// <summary>Invoked when someone presses a select button, with the plan selected now (null when it was cleared).</summary>
    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<PlanComparison, ICommand?>(nameof(SelectCommand));

    /// <summary>How many features are listed now.</summary>
    public static readonly DirectProperty<PlanComparison, int> VisibleFeatureCountProperty =
        AvaloniaProperty.RegisterDirect<PlanComparison, int>(nameof(VisibleFeatureCount), o => o.VisibleFeatureCount);

    /// <summary>Raised when <see cref="SelectedPlan"/> changes, by a select button or by the app.</summary>
    public static readonly RoutedEvent<PlanComparisonSelectionChangedEventArgs> SelectedPlanChangedEvent =
        RoutedEvent.Register<PlanComparison, PlanComparisonSelectionChangedEventArgs>(nameof(SelectedPlanChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="Billing"/> changes, by the billing switch or by the app.</summary>
    public static readonly RoutedEvent<PlanComparisonBillingChangedEventArgs> BillingChangedEvent =
        RoutedEvent.Register<PlanComparison, PlanComparisonBillingChangedEventArgs>(nameof(BillingChanged), RoutingStrategies.Bubble);

    internal static readonly StyledProperty<double> FrameMaxWidthProperty =
        AvaloniaProperty.Register<PlanComparison, double>(nameof(FrameMaxWidth), 1060);

    internal static readonly StyledProperty<double> MediumWidthProperty =
        AvaloniaProperty.Register<PlanComparison, double>(nameof(MediumWidth), 720);

    internal static readonly StyledProperty<double> SmallWidthProperty =
        AvaloniaProperty.Register<PlanComparison, double>(nameof(SmallWidth), 540);

    internal static readonly StyledProperty<double> TinyWidthProperty =
        AvaloniaProperty.Register<PlanComparison, double>(nameof(TinyWidth), 340);

    /// <summary>Side gutter for the current layout (set by the theme per layout).</summary>
    internal static readonly StyledProperty<double> SideInsetProperty =
        AvaloniaProperty.Register<PlanComparison, double>(nameof(SideInset), 32);

    /// <summary>Inline padding of plan and value cells for the current layout.</summary>
    internal static readonly StyledProperty<double> CellInsetProperty =
        AvaloniaProperty.Register<PlanComparison, double>(nameof(CellInset), 20);

    /// <summary>Padding above and below plan headers for the current layout.</summary>
    internal static readonly StyledProperty<double> PlanInsetYProperty =
        AvaloniaProperty.Register<PlanComparison, double>(nameof(PlanInsetY), 20);

    internal static readonly StyledProperty<double> RowMinHeightProperty =
        AvaloniaProperty.Register<PlanComparison, double>(nameof(RowMinHeight), 44);

    internal static readonly StyledProperty<double> RowInsetYProperty =
        AvaloniaProperty.Register<PlanComparison, double>(nameof(RowInsetY), 10);

    internal static readonly StyledProperty<double> FeatureHeaderBottomProperty =
        AvaloniaProperty.Register<PlanComparison, double>(nameof(FeatureHeaderBottom), 20);

    internal static readonly StyledProperty<Thickness> PriceMarginProperty =
        AvaloniaProperty.Register<PlanComparison, Thickness>(nameof(PriceMargin), new Thickness(0, 16, 0, 14));

    internal static readonly StyledProperty<double> PriceGapProperty =
        AvaloniaProperty.Register<PlanComparison, double>(nameof(PriceGap), 6);

    internal static readonly StyledProperty<double> PriceRowGapProperty =
        AvaloniaProperty.Register<PlanComparison, double>(nameof(PriceRowGap), 2);

    internal static readonly StyledProperty<double> RailHeightProperty =
        AvaloniaProperty.Register<PlanComparison, double>(nameof(RailHeight), 2);

    internal static readonly StyledProperty<double> CheckSizeProperty =
        AvaloniaProperty.Register<PlanComparison, double>(nameof(CheckSize), 16);

    internal static readonly StyledProperty<double> CheckGapProperty =
        AvaloniaProperty.Register<PlanComparison, double>(nameof(CheckGap), 6);

    /// <inheritdoc cref="BillingProperty"/>
    public PlanComparisonBilling Billing
    {
        get => GetValue(BillingProperty);
        set => SetValue(BillingProperty, value);
    }

    /// <inheritdoc cref="IsDifferencesOnlyProperty"/>
    public bool IsDifferencesOnly
    {
        get => GetValue(IsDifferencesOnlyProperty);
        set => SetValue(IsDifferencesOnlyProperty, value);
    }

    /// <inheritdoc cref="SelectedPlanProperty"/>
    public PlanComparisonPlan? SelectedPlan
    {
        get => GetValue(SelectedPlanProperty);
        set => SetValue(SelectedPlanProperty, value);
    }

    /// <summary>The plans, one column each, in display order. Empty (and no <see cref="PlansSource"/>) shows the sample plans.</summary>
    public AvaloniaList<PlanComparisonPlan> Plans { get; } = new();

    /// <summary>
    /// The feature rows, in display order, each with one value per plan. Empty (and no <see cref="FeaturesSource"/>)
    /// shows the sample features.
    /// </summary>
    public AvaloniaList<PlanComparisonFeature> Features { get; } = new();

    /// <inheritdoc cref="PlansSourceProperty"/>
    public IEnumerable<PlanComparisonPlan>? PlansSource
    {
        get => GetValue(PlansSourceProperty);
        set => SetValue(PlansSourceProperty, value);
    }

    /// <inheritdoc cref="FeaturesSourceProperty"/>
    public IEnumerable<PlanComparisonFeature>? FeaturesSource
    {
        get => GetValue(FeaturesSourceProperty);
        set => SetValue(FeaturesSourceProperty, value);
    }

    /// <inheritdoc cref="CurrencyProperty"/>
    public string Currency
    {
        get => GetValue(CurrencyProperty);
        set => SetValue(CurrencyProperty, value);
    }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed
    {
        get => GetValue(IsFramedProperty);
        set => SetValue(IsFramedProperty, value);
    }

    /// <inheritdoc cref="IsBillingVisibleProperty"/>
    public bool IsBillingVisible
    {
        get => GetValue(IsBillingVisibleProperty);
        set => SetValue(IsBillingVisibleProperty, value);
    }

    /// <inheritdoc cref="IsFootnoteVisibleProperty"/>
    public bool IsFootnoteVisible
    {
        get => GetValue(IsFootnoteVisibleProperty);
        set => SetValue(IsFootnoteVisibleProperty, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public PlanComparisonLabels Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    /// <inheritdoc cref="SelectCommandProperty"/>
    public ICommand? SelectCommand
    {
        get => GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    /// <inheritdoc cref="VisibleFeatureCountProperty"/>
    public int VisibleFeatureCount
    {
        get => _visibleCount;
        private set => SetAndRaise(VisibleFeatureCountProperty, ref _visibleCount, value);
    }

    /// <inheritdoc cref="SelectedPlanChangedEvent"/>
    public event EventHandler<PlanComparisonSelectionChangedEventArgs>? SelectedPlanChanged
    {
        add => AddHandler(SelectedPlanChangedEvent, value);
        remove => RemoveHandler(SelectedPlanChangedEvent, value);
    }

    /// <inheritdoc cref="BillingChangedEvent"/>
    public event EventHandler<PlanComparisonBillingChangedEventArgs>? BillingChanged
    {
        add => AddHandler(BillingChangedEvent, value);
        remove => RemoveHandler(BillingChangedEvent, value);
    }

    internal double FrameMaxWidth { get => GetValue(FrameMaxWidthProperty); set => SetValue(FrameMaxWidthProperty, value); }
    internal double MediumWidth { get => GetValue(MediumWidthProperty); set => SetValue(MediumWidthProperty, value); }
    internal double SmallWidth { get => GetValue(SmallWidthProperty); set => SetValue(SmallWidthProperty, value); }
    internal double TinyWidth { get => GetValue(TinyWidthProperty); set => SetValue(TinyWidthProperty, value); }
    internal double SideInset { get => GetValue(SideInsetProperty); set => SetValue(SideInsetProperty, value); }
    internal double CellInset { get => GetValue(CellInsetProperty); set => SetValue(CellInsetProperty, value); }
    internal double PlanInsetY { get => GetValue(PlanInsetYProperty); set => SetValue(PlanInsetYProperty, value); }
    internal double RowMinHeight { get => GetValue(RowMinHeightProperty); set => SetValue(RowMinHeightProperty, value); }
    internal double RowInsetY { get => GetValue(RowInsetYProperty); set => SetValue(RowInsetYProperty, value); }
    internal double FeatureHeaderBottom { get => GetValue(FeatureHeaderBottomProperty); set => SetValue(FeatureHeaderBottomProperty, value); }
    internal Thickness PriceMargin { get => GetValue(PriceMarginProperty); set => SetValue(PriceMarginProperty, value); }
    internal double PriceGap { get => GetValue(PriceGapProperty); set => SetValue(PriceGapProperty, value); }
    internal double PriceRowGap { get => GetValue(PriceRowGapProperty); set => SetValue(PriceRowGapProperty, value); }
    internal double RailHeight { get => GetValue(RailHeightProperty); set => SetValue(RailHeightProperty, value); }
    internal double CheckSize { get => GetValue(CheckSizeProperty); set => SetValue(CheckSizeProperty, value); }
    internal double CheckGap { get => GetValue(CheckGapProperty); set => SetValue(CheckGapProperty, value); }

    #endregion

    /// <summary>The parts of one plan's column.</summary>
    private sealed class PlanView
    {
        public required PlanComparisonPlan Plan { get; init; }
        public required PlanComparisonCell Cell { get; init; }
        public required StackPanel Top { get; init; }
        public required PlanComparisonText Name { get; init; }
        public required PlanComparisonText Description { get; init; }
        public required WrapPanel PriceRow { get; init; }
        public required PlanComparisonText Price { get; init; }
        public required PlanComparisonText Period { get; init; }
        public required Button Select { get; init; }
        public PlanComparisonCounter? Counter { get; set; }
    }

    /// <summary>The parts of one feature row.</summary>
    private sealed class RowView
    {
        public required PlanComparisonRow Row { get; init; }
        public required Grid Grid { get; init; }
        public required PlanComparisonText Name { get; init; }
        public required PlanComparisonCell[] Cells { get; init; }
        public required PlanComparisonText[] Texts { get; init; }
    }

    private readonly List<PlanView> _plans = new();
    private readonly List<RowView> _rows = new();
    private readonly HashSet<AvaloniaObject> _observed = new();
    private IReadOnlyList<PlanComparisonPlan>? _samplePlans;
    private IReadOnlyList<PlanComparisonFeature>? _sampleFeatures;
    private INotifyCollectionChanged? _plansSourceWatched;
    private INotifyCollectionChanged? _featuresSourceWatched;
    private PlanComparisonSize _size = PlanComparisonSize.Large;
    private int _visibleCount;
    private bool _syncingParts;
    private bool _userSelecting;

    private PlanComparisonText? _featureHeader;
    private Grid? _header;
    private Grid? _matrix;
    private Border? _rowsHost;
    private StackPanel? _rowsPanel;
    private GlidingIndicator? _rail;
    private PlanComparisonCounter? _featureCounter;

    private TextBlock? _title;
    private TextBlock? _description;
    private SegmentedControl? _billing;
    private TextBlock? _billingNote;
    private TextBlock? _compareTitle;
    private TextBlock? _featureCount;
    private Switch? _differencesSwitch;
    private Border? _matrixPart;
    private TextBlock? _footnote;
    private SiteAnnouncer? _announcer;

    static PlanComparison()
    {
        FocusableProperty.OverrideDefaultValue<PlanComparison>(false);
        AffectsMeasure<PlanComparison>(FrameMaxWidthProperty, MediumWidthProperty, SmallWidthProperty, TinyWidthProperty, IsFramedProperty);
    }

    public PlanComparison()
    {
        Plans.CollectionChanged += (_, _) => Rebuild();
        Features.CollectionChanged += (_, _) => Rebuild();
        PseudoClasses.Set(FlatPseudoClass, false);
        _featureCounter = new PlanComparisonCounter(this, ShowFeatureCount);
    }

    /// <summary>The plans the block shows: <see cref="PlansSource"/>, else <see cref="Plans"/>, else the samples.</summary>
    internal IReadOnlyList<PlanComparisonPlan> EffectivePlans
    {
        get
        {
            if (PlansSource is { } source)
            {
                return source.Where(p => p is not null).ToList();
            }
            if (Plans.Count > 0)
            {
                return Plans;
            }
            return _samplePlans ??= PlanComparisonSamples.Plans();
        }
    }

    /// <summary>The features the block lists: <see cref="FeaturesSource"/>, else <see cref="Features"/>, else the samples.</summary>
    internal IReadOnlyList<PlanComparisonFeature> EffectiveFeatures
    {
        get
        {
            if (FeaturesSource is { } source)
            {
                return source.Where(f => f is not null).ToList();
            }
            if (Features.Count > 0)
            {
                return Features;
            }
            return _sampleFeatures ??= PlanComparisonSamples.Features();
        }
    }

    /// <summary>The current width tier (tests).</summary>
    internal PlanComparisonSize Size => _size;

    /// <summary>The selected plan's rail (tests).</summary>
    internal GlidingIndicator? Rail => _rail;

    /// <summary>The live region that announces the selection (tests).</summary>
    internal SiteAnnouncer? Announcer => _announcer;

    /// <summary>The feature rows in display order (tests).</summary>
    internal IReadOnlyList<PlanComparisonRow> Rows => _rows.Select(r => r.Row).ToList();

    /// <summary>The select buttons in plan order (tests).</summary>
    internal IReadOnlyList<Button> SelectButtons => _plans.Select(p => p.Select).ToList();

    /// <summary>The price texts in plan order (tests).</summary>
    internal IReadOnlyList<TextBlock> PriceTexts => _plans.Select(p => (TextBlock)p.Price).ToList();

    /// <summary>The plan header cells in plan order (tests).</summary>
    internal IReadOnlyList<Border> PlanCells => _plans.Select(p => (Border)p.Cell).ToList();

    /// <summary>The feature count text (tests).</summary>
    internal TextBlock? FeatureCountText => _featureCount;

    /// <summary>The billing note (tests).</summary>
    internal TextBlock? BillingNoteText => _billingNote;

    #region Public methods

    /// <summary>Selects <paramref name="plan"/> (null clears the selection), as if its button were pressed, without the command.</summary>
    public void Select(PlanComparisonPlan? plan) => SetCurrentValue(SelectedPlanProperty, plan);

    /// <summary>Clears the selection.</summary>
    public void ClearSelection() => SetCurrentValue(SelectedPlanProperty, null);

    /// <summary>Back to the first look: monthly prices, every feature listed, nothing selected.</summary>
    public void Reset()
    {
        SetCurrentValue(BillingProperty, PlanComparisonBilling.Monthly);
        SetCurrentValue(IsDifferencesOnlyProperty, false);
        SetCurrentValue(SelectedPlanProperty, null);
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_billing is not null)
        {
            _billing.SelectionChanged -= OnBillingSelectionChanged;
        }
        if (_differencesSwitch is not null)
        {
            _differencesSwitch.IsCheckedChanged -= OnDifferencesChanged;
        }
        if (_matrixPart is not null)
        {
            _matrixPart.Child = null;
        }
        base.OnApplyTemplate(e);

        var scope = e.NameScope;
        _title = scope.Find<TextBlock>(TitlePart);
        _description = scope.Find<TextBlock>(DescriptionPart);
        _billing = scope.Find<SegmentedControl>(BillingPart);
        _billingNote = scope.Find<TextBlock>(BillingNotePart);
        _compareTitle = scope.Find<TextBlock>(CompareTitlePart);
        _featureCount = scope.Find<TextBlock>(FeatureCountPart);
        _differencesSwitch = scope.Find<Switch>(DifferencesSwitchPart);
        _matrixPart = scope.Find<Border>(MatrixPart);
        _footnote = scope.Find<TextBlock>(FootnotePart);
        _announcer = scope.Find<SiteAnnouncer>(AnnouncerPart);

        if (_billing is not null)
        {
            _billing.SelectionChanged += OnBillingSelectionChanged;
        }
        if (_differencesSwitch is not null)
        {
            _differencesSwitch.IsCheckedChanged += OnDifferencesChanged;
        }
        EnsureMatrix();
        if (_matrixPart is not null)
        {
            _matrixPart.Child = _matrix;
        }
        UpdateTexts();
        SyncParts();
        Rebuild();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The width comes from the parent, not the content, so switching layouts here cannot oscillate.
        if (!double.IsInfinity(availableSize.Width))
        {
            var width = IsFramed ? Math.Min(availableSize.Width, FrameMaxWidth) - 2 : availableSize.Width;
            var size = PlanComparisonMath.SizeFor(width, MediumWidth, SmallWidth, TinyWidth);
            if (size != _size)
            {
                _size = size;
                UpdateSizePseudoClasses();
                ApplyLayout();
            }
        }
        return base.MeasureOverride(availableSize);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        WatchSources();
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UnwatchSources();
        Unobserve();
    }

    private void UpdateSizePseudoClasses()
    {
        PseudoClasses.Set(CompactPseudoClass, _size != PlanComparisonSize.Large);
        PseudoClasses.Set(NarrowPseudoClass, _size is PlanComparisonSize.Small or PlanComparisonSize.Tiny);
        PseudoClasses.Set(TinyPseudoClass, _size == PlanComparisonSize.Tiny);
    }

    private bool IsNarrow => _size is PlanComparisonSize.Small or PlanComparisonSize.Tiny;

    #endregion

    #region Changes

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == BillingProperty)
        {
            SyncParts();
            UpdateBillingNote();
            UpdatePrices(animate: true);
            RaiseEvent(new PlanComparisonBillingChangedEventArgs(BillingChangedEvent,
                change.GetOldValue<PlanComparisonBilling>(), change.GetNewValue<PlanComparisonBilling>()));
        }
        else if (property == IsDifferencesOnlyProperty)
        {
            SyncParts();
            UpdateRows(animate: true);
        }
        else if (property == SelectedPlanProperty)
        {
            var oldPlan = change.GetOldValue<PlanComparisonPlan?>();
            var newPlan = change.GetNewValue<PlanComparisonPlan?>();
            UpdateSelection(animate: true);
            AnnounceSelection(newPlan);
            RaiseEvent(new PlanComparisonSelectionChangedEventArgs(SelectedPlanChangedEvent, oldPlan, newPlan, _userSelecting));
        }
        else if (property == PlansSourceProperty || property == FeaturesSourceProperty)
        {
            WatchSources();
            Rebuild();
        }
        else if (property == LabelsProperty || property == CurrencyProperty)
        {
            UpdateTexts();
            Rebuild();
        }
        else if (property == IsFramedProperty)
        {
            PseudoClasses.Set(FlatPseudoClass, !IsFramed);
        }
        else if (property == SideInsetProperty || property == CellInsetProperty || property == PlanInsetYProperty ||
                 property == RowMinHeightProperty || property == RowInsetYProperty || property == FeatureHeaderBottomProperty ||
                 property == PriceMarginProperty || property == PriceGapProperty || property == PriceRowGapProperty ||
                 property == CheckGapProperty || property == CheckSizeProperty)
        {
            ApplyLayout();
        }
        else if (property == RailHeightProperty)
        {
            UpdateRail(animate: false);
        }
    }

    private void OnBillingSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingParts || _billing is null || _billing.SelectedIndex < 0)
        {
            return;
        }
        SetCurrentValue(BillingProperty, _billing.SelectedIndex == 1 ? PlanComparisonBilling.Yearly : PlanComparisonBilling.Monthly);
    }

    private void OnDifferencesChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncingParts || _differencesSwitch is null)
        {
            return;
        }
        SetCurrentValue(IsDifferencesOnlyProperty, _differencesSwitch.IsChecked == true);
    }

    private void OnSelectClick(PlanComparisonPlan plan)
    {
        var next = ReferenceEquals(SelectedPlan, plan) ? null : plan;
        _userSelecting = true;
        try
        {
            SetCurrentValue(SelectedPlanProperty, next);
        }
        finally
        {
            _userSelecting = false;
        }
        if (SelectCommand is { } command && command.CanExecute(next))
        {
            command.Execute(next);
        }
    }

    private void SyncParts()
    {
        _syncingParts = true;
        try
        {
            if (_billing is not null)
            {
                _billing.SelectedIndex = Billing == PlanComparisonBilling.Yearly ? 1 : 0;
            }
            if (_differencesSwitch is not null)
            {
                _differencesSwitch.IsChecked = IsDifferencesOnly;
            }
        }
        finally
        {
            _syncingParts = false;
        }
    }

    private void WatchSources()
    {
        UnwatchSources();
        if (!IsAttached)
        {
            return;
        }
        _plansSourceWatched = PlansSource as INotifyCollectionChanged;
        _featuresSourceWatched = FeaturesSource as INotifyCollectionChanged;
        if (_plansSourceWatched is not null)
        {
            _plansSourceWatched.CollectionChanged += OnSourceChanged;
        }
        if (_featuresSourceWatched is not null)
        {
            _featuresSourceWatched.CollectionChanged += OnSourceChanged;
        }
    }

    private void UnwatchSources()
    {
        if (_plansSourceWatched is not null)
        {
            _plansSourceWatched.CollectionChanged -= OnSourceChanged;
            _plansSourceWatched = null;
        }
        if (_featuresSourceWatched is not null)
        {
            _featuresSourceWatched.CollectionChanged -= OnSourceChanged;
            _featuresSourceWatched = null;
        }
    }

    private bool IsAttached => VisualRoot is not null;

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Observe(IEnumerable<AvaloniaObject> items)
    {
        Unobserve();
        if (!IsAttached)
        {
            return;
        }
        foreach (var item in items)
        {
            if (_observed.Add(item))
            {
                item.PropertyChanged += OnItemPropertyChanged;
                if (item is PlanComparisonFeature feature)
                {
                    feature.Values.CollectionChanged += OnFeatureValuesChanged;
                }
            }
        }
    }

    private void Unobserve()
    {
        foreach (var item in _observed)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
            if (item is PlanComparisonFeature feature)
            {
                feature.Values.CollectionChanged -= OnFeatureValuesChanged;
            }
        }
        _observed.Clear();
    }

    private void OnFeatureValuesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void OnItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == PlanComparisonPlan.MonthlyPriceProperty || e.Property == PlanComparisonPlan.YearlyPriceProperty)
        {
            UpdateBillingNote();
            UpdatePrices(animate: true);
        }
        else if (e.Property == PlanComparisonFeature.IsSharedProperty)
        {
            UpdateRows(animate: true);
        }
        else
        {
            Rebuild();
        }
    }

    #endregion

    #region Texts

    private void UpdateTexts()
    {
        var labels = Labels;
        if (_title is not null)
        {
            _title.Text = labels.Title;
        }
        if (_description is not null)
        {
            _description.Text = labels.Description;
        }
        if (_billing is not null)
        {
            _syncingParts = true;
            try
            {
                _billing.ItemsSource = new[]
                {
                    new SegmentedOption(PlanComparisonBilling.Monthly, labels.Monthly),
                    new SegmentedOption(PlanComparisonBilling.Yearly, labels.Yearly)
                };
                _billing.SelectedIndex = Billing == PlanComparisonBilling.Yearly ? 1 : 0;
            }
            finally
            {
                _syncingParts = false;
            }
            AutomationProperties.SetName(_billing, labels.BillingPeriod);
        }
        if (_compareTitle is not null)
        {
            _compareTitle.Text = labels.CompareTitle;
        }
        if (_differencesSwitch is not null)
        {
            _differencesSwitch.Content = labels.DifferencesOnly;
            AutomationProperties.SetName(_differencesSwitch, labels.DifferencesOnly);
        }
        if (_footnote is not null)
        {
            _footnote.Text = labels.Footnote;
        }
        UpdateBillingNote();
    }

    private void UpdateBillingNote()
    {
        if (_billingNote is null)
        {
            return;
        }
        var labels = Labels;
        _billingNote.Text = Billing == PlanComparisonBilling.Yearly
            ? labels.YearlyNote(PlanComparisonMath.MaxYearlySaving(EffectivePlans))
            : labels.MonthlyNote;
    }

    private void ShowFeatureCount(decimal shown)
    {
        if (_featureCount is null)
        {
            return;
        }
        var total = EffectiveFeatures.Count;
        _featureCount.Text = Labels.FeatureCount((int)shown, total);
        AutomationProperties.SetName(_featureCount, Labels.FeatureCount(_visibleCount, total));
    }

    private string FormatPrice(decimal value) => SettingsFormat.MoneyShort(value, Currency, Labels.Culture);

    private void AnnounceSelection(PlanComparisonPlan? plan)
    {
        if (_announcer is null)
        {
            return;
        }
        if (plan is null)
        {
            _announcer.Clear();
        }
        else
        {
            _announcer.Announce(Labels.SelectedAnnouncement(plan.Name));
        }
    }

    #endregion

    #region Matrix

    private void EnsureMatrix()
    {
        if (_matrix is not null)
        {
            return;
        }
        _featureHeader = new PlanComparisonText("header") { VerticalAlignment = VerticalAlignment.Bottom };
        Grid.SetRowSpan(_featureHeader, 3);
        _header = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto") };
        _rowsPanel = new StackPanel();
        _rowsHost = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Child = _rowsPanel };
        ArcTokens.Bind(_rowsHost, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        Grid.SetRow(_rowsHost, 1);
        _rail = new GlidingIndicator { IsHitTestVisible = false, FadeDuration = TimeSpan.Zero };
        ArcTokens.Bind(_rail, GlidingIndicator.FillProperty, ArcDesignTokenKind.Accent);
        Grid.SetRowSpan(_rail, 2);
        _matrix = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto") };
        _matrix.Children.Add(_header);
        _matrix.Children.Add(_rowsHost);
        _matrix.Children.Add(_rail);
        _header.SizeChanged += (_, _) => UpdateRail(animate: false);
    }

    /// <summary>Builds the plan columns and feature rows from the current data (no animation).</summary>
    private void Rebuild()
    {
        if (_header is null || _rowsPanel is null || _featureHeader is null)
        {
            return;
        }
        var labels = Labels;
        var plans = EffectivePlans;
        var features = EffectiveFeatures;
        Observe(plans.Cast<AvaloniaObject>().Concat(features));

        foreach (var row in _rows)
        {
            row.Row.Collapsed -= OnRowCollapsed;
        }
        _plans.Clear();
        _rows.Clear();
        _header.Children.Clear();
        _rowsPanel.Children.Clear();

        _featureHeader.Text = labels.FeatureHeader;
        _header.Children.Add(_featureHeader);
        foreach (var plan in plans)
        {
            _plans.Add(CreatePlanView(plan, labels));
        }
        foreach (var view in _plans)
        {
            _header.Children.Add(view.Cell);
            _header.Children.Add(view.Top);
            _header.Children.Add(view.PriceRow);
            _header.Children.Add(view.Select);
        }
        foreach (var feature in features)
        {
            var row = CreateRowView(feature, plans, labels);
            row.Row.Collapsed += OnRowCollapsed;
            _rows.Add(row);
            _rowsPanel.Children.Add(row.Row);
        }
        if (_matrix is not null)
        {
            AutomationProperties.SetName(_matrix, labels.TableName(plans.Select(p => p.Name).ToList()));
        }

        ApplyLayout();
        UpdatePrices(animate: false);
        UpdateRows(animate: false);
        UpdateSelection(animate: false);
        UpdateBillingNote();
    }

    private PlanView CreatePlanView(PlanComparisonPlan plan, PlanComparisonLabels labels)
    {
        var cell = new PlanComparisonCell();
        Grid.SetRowSpan(cell, 3);
        cell.SizeChanged += (_, _) => UpdateRail(animate: false);
        AutomationProperties.SetName(cell, labels.PlanHeader(plan.Name));
        var name = new PlanComparisonText("plan-name") { Text = plan.Name };
        var description = new PlanComparisonText("plan-description") { Text = plan.Description, IsVisible = !string.IsNullOrEmpty(plan.Description) };
        var top = new StackPanel { Children = { name, description } };
        var price = new PlanComparisonText("price");
        AutomationProperties.SetLiveSetting(price, AutomationLiveSetting.Polite);
        var period = new PlanComparisonText("period") { Text = labels.PricePeriod, VerticalAlignment = VerticalAlignment.Bottom };
        var priceRow = new WrapPanel { Children = { price, period } };
        Grid.SetRow(priceRow, 1);
        var select = new Button { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
        Grid.SetRow(select, 2);
        select.Click += (_, _) => OnSelectClick(plan);
        var view = new PlanView
        {
            Plan = plan, Cell = cell, Top = top, Name = name, Description = description,
            PriceRow = priceRow, Price = price, Period = period, Select = select
        };
        view.Counter = new PlanComparisonCounter(price, value =>
        {
            price.Text = FormatPrice(value);
        });
        return view;
    }

    private RowView CreateRowView(PlanComparisonFeature feature, IReadOnlyList<PlanComparisonPlan> plans, PlanComparisonLabels labels)
    {
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto") };
        var name = new PlanComparisonText("name") { Text = feature.Label, VerticalAlignment = VerticalAlignment.Center };
        grid.Children.Add(name);
        var cells = new PlanComparisonCell[plans.Count];
        var texts = new PlanComparisonText[plans.Count];
        for (var i = 0; i < plans.Count; i++)
        {
            var value = feature.ValueAt(i);
            var included = value == labels.Included;
            var text = new PlanComparisonText(included ? "included" : value == labels.NotIncluded ? "muted" : "value")
            {
                Text = value,
                VerticalAlignment = VerticalAlignment.Center
            };
            Control content = text;
            if (included)
            {
                var glyph = new ArcGlyph { Data = s_check, Size = CheckSize, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center };
                ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Success);
                content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = CheckGap,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children = { glyph, text }
                };
            }
            var cell = new PlanComparisonCell { Child = content };
            AutomationProperties.SetName(cell, $"{plans[i].Name}: {value}");
            cells[i] = cell;
            texts[i] = text;
            grid.Children.Add(cell);
        }
        var row = new PlanComparisonRow(feature) { Child = grid };
        AutomationProperties.SetName(row, feature.Label);
        return new RowView { Row = row, Grid = grid, Name = name, Cells = cells, Texts = texts };
    }

    /// <summary>Places the header and row parts for the current width tier and the theme's gutters.</summary>
    private void ApplyLayout()
    {
        if (_header is null || _featureHeader is null)
        {
            return;
        }
        var narrow = IsNarrow;
        var side = SideInset;
        var inset = CellInset;
        var planY = PlanInsetY;
        var count = _plans.Count;
        var offset = narrow ? 0 : 1;
        var columns = narrow ? Columns(count, false) : Columns(count, true);

        _header.ColumnDefinitions = new ColumnDefinitions(columns);
        _featureHeader.IsVisible = !narrow;
        _featureHeader.Margin = new Thickness(side, 0, 16, FeatureHeaderBottom);
        for (var i = 0; i < count; i++)
        {
            var view = _plans[i];
            var column = i + offset;
            // Narrow: the first plan lines up with the block's gutter and has no divider on its left.
            var left = narrow && i == 0 ? side : inset;
            var divider = !narrow || i > 0 ? 1 : 0;
            Grid.SetColumn(view.Cell, column);
            Grid.SetColumn(view.Top, column);
            Grid.SetColumn(view.PriceRow, column);
            Grid.SetColumn(view.Select, column);
            view.Cell.BorderThickness = new Thickness(divider, 0, 0, 0);
            view.Top.Margin = new Thickness(left + divider, planY, inset, 0);
            var price = PriceMargin;
            view.PriceRow.Margin = new Thickness(left + divider, price.Top, inset, price.Bottom);
            view.PriceRow.ItemSpacing = PriceGap;
            view.PriceRow.LineSpacing = PriceRowGap;
            view.Price.IsNarrow = narrow;
            view.Select.Margin = new Thickness(left + divider, 0, inset, planY);
        }

        foreach (var row in _rows)
        {
            row.Grid.ColumnDefinitions = new ColumnDefinitions(columns);
            row.Name.IsNarrow = narrow;
            Grid.SetRow(row.Name, 0);
            Grid.SetColumn(row.Name, 0);
            Grid.SetColumnSpan(row.Name, narrow ? Math.Max(1, count) : 1);
            row.Name.Margin = narrow
                ? new Thickness(side, RowInsetY, side, 0)
                : new Thickness(side, RowInsetY, inset, RowInsetY);
            row.Name.MinHeight = narrow ? 0 : RowMinHeight - 2 * RowInsetY;
            for (var i = 0; i < row.Cells.Length; i++)
            {
                var cell = row.Cells[i];
                Grid.SetRow(cell, narrow ? 1 : 0);
                Grid.SetColumn(cell, i + offset);
                var left = narrow && i == 0 ? side : inset;
                cell.Padding = narrow ? new Thickness(left, 4, inset, 10) : new Thickness(inset, RowInsetY, inset, RowInsetY);
                cell.MinHeight = narrow ? 0 : RowMinHeight;
                cell.BorderThickness = new Thickness(!narrow || i > 0 ? 1 : 0, 0, 0, 0);
                cell.Classes.Set("subtle", narrow);
            }
        }
        UpdateRail(animate: false);
    }

    private static string Columns(int plans, bool withFeature)
    {
        var parts = new List<string>(plans + 1);
        if (withFeature)
        {
            parts.Add("1.1*");
        }
        for (var i = 0; i < plans; i++)
        {
            parts.Add("*");
        }
        return parts.Count == 0 ? "*" : string.Join(",", parts);
    }

    private void UpdatePrices(bool animate)
    {
        var billing = Billing;
        foreach (var view in _plans)
        {
            var target = view.Plan.PriceFor(billing);
            view.Counter?.Set(target, animate);
            // Screen readers hear the final price, never the numbers it counts through (Arc: aria-atomic on the value).
            AutomationProperties.SetName(view.Price, FormatPrice(target));
        }
    }

    private void UpdateRows(bool animate)
    {
        var differencesOnly = IsDifferencesOnly;
        foreach (var row in _rows)
        {
            row.Row.SetShown(PlanComparisonMath.IsRowShown(row.Row.Feature, differencesOnly), animate);
        }
        UpdateDividers();
        VisibleFeatureCount = PlanComparisonMath.VisibleCount(EffectiveFeatures, differencesOnly);
        _featureCounter?.Set(VisibleFeatureCount, animate);
        ShowFeatureCount(_featureCounter?.Shown ?? VisibleFeatureCount);
    }

    private void OnRowCollapsed(object? sender, EventArgs e) => UpdateDividers();

    /// <summary>Every row that takes space draws its divider except the last (Arc's <c>:last-child</c>).</summary>
    private void UpdateDividers()
    {
        var last = -1;
        for (var i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Row.IsPresent)
            {
                last = i;
            }
        }
        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].Row.HasDivider = i != last;
        }
    }

    private void UpdateSelection(bool animate)
    {
        var labels = Labels;
        var selected = SelectedPlan;
        for (var i = 0; i < _plans.Count; i++)
        {
            var view = _plans[i];
            var on = ReferenceEquals(view.Plan, selected);
            view.Cell.IsSelected = on;
            view.Select.Variant = on ? ButtonVariant.Primary : ButtonVariant.Secondary;
            view.Select.Content = on ? labels.Selected : labels.Select(view.Plan.Name);
            AutomationProperties.SetName(view.Select, labels.Select(view.Plan.Name));
            AutomationProperties.SetItemStatus(view.Select, on ? labels.Selected : string.Empty);
            foreach (var row in _rows)
            {
                if (i < row.Cells.Length)
                {
                    row.Cells[i].IsSelected = on;
                    row.Texts[i].IsSelected = on;
                }
            }
        }
        UpdateRail(animate);
    }

    /// <summary>
    /// Moves the rail under the selected plan's header (Arc's shared <c>layoutId</c> rail): it appears in place, glides
    /// on the responsive spring when the selection moves to another plan, and goes at once when the selection clears.
    /// </summary>
    private void UpdateRail(bool animate)
    {
        if (_rail is null || _header is null)
        {
            return;
        }
        var view = _plans.FirstOrDefault(p => ReferenceEquals(p.Plan, SelectedPlan));
        if (view is null)
        {
            _rail.Hide(animate: false);
            return;
        }
        var bounds = view.Cell.Bounds;
        if (bounds.Width <= 0)
        {
            return;
        }
        var height = RailHeight;
        // bottom: -1px: the rail covers the line under the plan headers.
        var rect = new Rect(bounds.X, _header.Bounds.Height + 1 - height, bounds.Width, height);
        if (_rail.HasTarget && animate)
        {
            _rail.MoveTo(rect, RailSpring);
        }
        else
        {
            _rail.MoveTo(rect, RailSpring, animate: false);
        }
    }

    #endregion
}
