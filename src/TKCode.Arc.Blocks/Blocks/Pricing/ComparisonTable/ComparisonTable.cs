// Ported from Arc's free comparison-table block (MIT, Copyright (c) 2026 Elia Kuratli): comparison-table.tsx.
using System.Collections.Specialized;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace TKCode.Arc;

/// <summary>
/// An us versus them table: sections of feature rows with a check, half circle, dash or text per product, a tinted band behind
/// your column, a header that stays pinned while the table scrolls, a switch that keeps only the rows that differ and, below
/// <see cref="StackBelow"/>, your column beside one competitor chosen with a picker. Zero configuration shows an invented product
/// against generic alternatives.
/// </summary>
/// <remarks>Ported from Arc's free comparison-table block (MIT, Copyright (c) 2026 Elia Kuratli).</remarks>
[TemplatePart(TablePart, typeof(StackPanel))]
[TemplatePart(HeadPart, typeof(ComparisonRowPanel))]
[TemplatePart(BodyPart, typeof(StackPanel))]
[TemplatePart(FootPart, typeof(ComparisonRowPanel))]
[TemplatePart(PickerPart, typeof(GlidingPillBar))]
[TemplatePart(SwitchPart, typeof(Switch))]
[TemplatePart(ScrollerPart, typeof(ScrollViewer))]
[TemplatePart(AnnouncerPart, typeof(SiteAnnouncer))]
[PseudoClasses(StackedPseudoClass, MediumPseudoClass, TightPseudoClass, DifferencesPseudoClass)]
public partial class ComparisonTable : TemplatedControl
{
    public const string TablePart = "PART_Table";
    public const string HeadPart = "PART_Head";
    public const string BodyPart = "PART_Body";
    public const string FootPart = "PART_Foot";
    public const string PickerPart = "PART_Picker";
    public const string SwitchPart = "PART_Switch";
    public const string ScrollerPart = "PART_Scroller";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set below <see cref="StackBelow"/>: your column beside one competitor.</summary>
    public const string StackedPseudoClass = ":stacked";

    /// <summary>Set at 820 wide or less: a narrower feature column.</summary>
    public const string MediumPseudoClass = ":medium";

    /// <summary>Set at 640 wide or less: tighter spacing and a legend in one column.</summary>
    public const string TightPseudoClass = ":tight";

    /// <summary>Set while <see cref="IsDifferencesOnly"/> is true.</summary>
    public const string DifferencesPseudoClass = ":differences";

    /// <summary>The heading ("How Fieldnote compares").</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ComparisonTable, string?>(nameof(Title), "How Fieldnote compares");

    /// <summary>The line under the heading. Null or empty hides it.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ComparisonTable, string?>(nameof(Description), "What a growing team uses every day, without the enterprise bill or the pile of documents.");

    /// <summary>The products to compare. Mark yours with <see cref="ComparisonColumn.IsHighlighted"/>; otherwise the first is yours. Null shows the sample.</summary>
    public static readonly StyledProperty<IReadOnlyList<ComparisonColumn>?> ColumnsProperty =
        AvaloniaProperty.Register<ComparisonTable, IReadOnlyList<ComparisonColumn>?>(nameof(Columns));

    /// <summary>The grouped feature rows. Null shows the sample.</summary>
    public static readonly StyledProperty<IReadOnlyList<ComparisonSection>?> SectionsProperty =
        AvaloniaProperty.Register<ComparisonTable, IReadOnlyList<ComparisonSection>?>(nameof(Sections));

    /// <summary>Hides the rows where every visible column has the same value (two way).</summary>
    public static readonly StyledProperty<bool> IsDifferencesOnlyProperty =
        AvaloniaProperty.Register<ComparisonTable, bool>(nameof(IsDifferencesOnly), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The id of the competitor beside yours in the stacked layout (two way). Null means the first other column.</summary>
    public static readonly StyledProperty<string?> CompareWithProperty =
        AvaloniaProperty.Register<ComparisonTable, string?>(nameof(CompareWith), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The call to action at the foot of your column. Null shows none.</summary>
    public static readonly StyledProperty<ComparisonCta?> CtaProperty =
        AvaloniaProperty.Register<ComparisonTable, ComparisonCta?>(nameof(Cta));

    /// <summary>How far below the top of the scrolled view the header pins, such as the height of a fixed site header (0).</summary>
    public static readonly StyledProperty<double> StickyTopProperty =
        AvaloniaProperty.Register<ComparisonTable, double>(nameof(StickyTop));

    /// <summary>Caps the table's height and scrolls it inside the block, with the header pinned. Infinity (the default) lets the table grow.</summary>
    public static readonly StyledProperty<double> MaxTableHeightProperty =
        AvaloniaProperty.Register<ComparisonTable, double>(nameof(MaxTableHeight), double.PositiveInfinity);

    /// <summary>The block's width below which it stacks into two columns (640).</summary>
    public static readonly StyledProperty<double> StackBelowProperty =
        AvaloniaProperty.Register<ComparisonTable, double>(nameof(StackBelow), 640);

    /// <summary>The switch's label ("Only differences").</summary>
    public static readonly StyledProperty<string> DifferencesLabelProperty =
        AvaloniaProperty.Register<ComparisonTable, string>(nameof(DifferencesLabel), "Only differences");

    /// <summary>The word for a check ("Included").</summary>
    public static readonly StyledProperty<string> IncludedLabelProperty =
        AvaloniaProperty.Register<ComparisonTable, string>(nameof(IncludedLabel), "Included");

    /// <summary>The word for a half circle ("Partial").</summary>
    public static readonly StyledProperty<string> PartialLabelProperty =
        AvaloniaProperty.Register<ComparisonTable, string>(nameof(PartialLabel), "Partial");

    /// <summary>The word for a dash ("Not included").</summary>
    public static readonly StyledProperty<string> NotIncludedLabelProperty =
        AvaloniaProperty.Register<ComparisonTable, string>(nameof(NotIncludedLabel), "Not included");

    /// <summary>The picker's screen reader name; {0} is your product ("Compare {0} with").</summary>
    public static readonly StyledProperty<string> PickerLabelFormatProperty =
        AvaloniaProperty.Register<ComparisonTable, string>(nameof(PickerLabelFormat), "Compare {0} with");

    /// <summary>What a screen reader hears when rows are filtered ("Showing only rows that differ").</summary>
    public static readonly StyledProperty<string> DifferencesAnnouncementProperty =
        AvaloniaProperty.Register<ComparisonTable, string>(nameof(DifferencesAnnouncement), "Showing only rows that differ");

    /// <summary>What a screen reader hears when every row returns ("Showing all rows").</summary>
    public static readonly StyledProperty<string> AllRowsAnnouncementProperty =
        AvaloniaProperty.Register<ComparisonTable, string>(nameof(AllRowsAnnouncement), "Showing all rows");

    /// <summary>The call to action was pressed. Mark the event handled to stop a link from opening.</summary>
    public static readonly RoutedEvent<ComparisonCtaEventArgs> CtaInvokedEvent =
        RoutedEvent.Register<ComparisonTable, ComparisonCtaEventArgs>(nameof(CtaInvoked), RoutingStrategies.Bubble);

    internal static readonly DirectProperty<ComparisonTable, double> TitleFontSizeProperty =
        AvaloniaProperty.RegisterDirect<ComparisonTable, double>(nameof(TitleFontSize), o => o.TitleFontSize);

    internal static readonly DirectProperty<ComparisonTable, bool> HasDescriptionProperty =
        AvaloniaProperty.RegisterDirect<ComparisonTable, bool>(nameof(HasDescription), o => o.HasDescription);

    private double _titleFontSize = 36;
    private bool _hasDescription = true;

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="ColumnsProperty"/>
    public IReadOnlyList<ComparisonColumn>? Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    /// <inheritdoc cref="SectionsProperty"/>
    public IReadOnlyList<ComparisonSection>? Sections { get => GetValue(SectionsProperty); set => SetValue(SectionsProperty, value); }

    /// <inheritdoc cref="IsDifferencesOnlyProperty"/>
    public bool IsDifferencesOnly { get => GetValue(IsDifferencesOnlyProperty); set => SetValue(IsDifferencesOnlyProperty, value); }

    /// <inheritdoc cref="CompareWithProperty"/>
    public string? CompareWith { get => GetValue(CompareWithProperty); set => SetValue(CompareWithProperty, value); }

    /// <inheritdoc cref="CtaProperty"/>
    public ComparisonCta? Cta { get => GetValue(CtaProperty); set => SetValue(CtaProperty, value); }

    /// <inheritdoc cref="StickyTopProperty"/>
    public double StickyTop { get => GetValue(StickyTopProperty); set => SetValue(StickyTopProperty, value); }

    /// <inheritdoc cref="MaxTableHeightProperty"/>
    public double MaxTableHeight { get => GetValue(MaxTableHeightProperty); set => SetValue(MaxTableHeightProperty, value); }

    /// <inheritdoc cref="StackBelowProperty"/>
    public double StackBelow { get => GetValue(StackBelowProperty); set => SetValue(StackBelowProperty, value); }

    /// <inheritdoc cref="DifferencesLabelProperty"/>
    public string DifferencesLabel { get => GetValue(DifferencesLabelProperty); set => SetValue(DifferencesLabelProperty, value); }

    /// <inheritdoc cref="IncludedLabelProperty"/>
    public string IncludedLabel { get => GetValue(IncludedLabelProperty); set => SetValue(IncludedLabelProperty, value); }

    /// <inheritdoc cref="PartialLabelProperty"/>
    public string PartialLabel { get => GetValue(PartialLabelProperty); set => SetValue(PartialLabelProperty, value); }

    /// <inheritdoc cref="NotIncludedLabelProperty"/>
    public string NotIncludedLabel { get => GetValue(NotIncludedLabelProperty); set => SetValue(NotIncludedLabelProperty, value); }

    /// <inheritdoc cref="PickerLabelFormatProperty"/>
    public string PickerLabelFormat { get => GetValue(PickerLabelFormatProperty); set => SetValue(PickerLabelFormatProperty, value); }

    /// <inheritdoc cref="DifferencesAnnouncementProperty"/>
    public string DifferencesAnnouncement { get => GetValue(DifferencesAnnouncementProperty); set => SetValue(DifferencesAnnouncementProperty, value); }

    /// <inheritdoc cref="AllRowsAnnouncementProperty"/>
    public string AllRowsAnnouncement { get => GetValue(AllRowsAnnouncementProperty); set => SetValue(AllRowsAnnouncementProperty, value); }

    /// <inheritdoc cref="CtaInvokedEvent"/>
    public event EventHandler<ComparisonCtaEventArgs>? CtaInvoked
    {
        add => AddHandler(CtaInvokedEvent, value);
        remove => RemoveHandler(CtaInvokedEvent, value);
    }

    /// <summary>clamp(28, 16 + 3cqi, 52).</summary>
    internal double TitleFontSize { get => _titleFontSize; private set => SetAndRaise(TitleFontSizeProperty, ref _titleFontSize, value); }

    internal bool HasDescription { get => _hasDescription; private set => SetAndRaise(HasDescriptionProperty, ref _hasDescription, value); }

    private sealed class HeadSlot
    {
        public required ComparisonCellGround Cell { get; init; }
        public required BlockSwap Swap { get; init; }
        public string? ColumnId { get; set; }
    }

    private sealed class RowView
    {
        public required ComparisonRow Row { get; init; }
        public required BlockCollapse Collapse { get; init; }
        public required ComparisonRowPanel Panel { get; init; }
        public required ComparisonCellGround Feature { get; init; }
        public required List<ComparisonCell> Cells { get; init; }
        public ViewportVisibility? Watch { get; set; }
        public int Index { get; set; }
        public bool Revealed { get; set; }
    }

    private sealed class SectionView
    {
        public required ComparisonSection Section { get; init; }
        public required BlockCollapse Title { get; init; }
        public required ComparisonRowPanel TitlePanel { get; init; }
        public required List<ComparisonCellGround> TitleBands { get; init; }
        public required List<RowView> Rows { get; init; }
    }

    private static readonly BlockSwapMotion s_headMotion = new()
    {
        EnterY = 8,
        ExitY = -8,
        EnterFade = MotionSpec.Spring(ArcSpring.Smooth),
        EnterMove = MotionSpec.Spring(ArcSpring.Smooth),
        Exit = MotionSpec.Spring(ArcSpring.Smooth),
    };

    private readonly List<HeadSlot> _headSlots = new();
    private readonly List<SectionView> _sections = new();
    private readonly List<ComparisonCellGround> _footBands = new();
    private readonly TranslateTransform _headOffset = new();
    private IReadOnlyList<ComparisonColumn> _columns = [];
    private ComparisonColumn? _own;
    private List<ComparisonColumn> _others = new();
    private List<ComparisonColumn> _visible = new();
    private StackPanel? _table;
    private ComparisonRowPanel? _head;
    private StackPanel? _body;
    private ComparisonRowPanel? _foot;
    private GlidingPillBar? _picker;
    private Switch? _switch;
    private TextBlock? _switchLabel;
    private SiteAnnouncer? _announcer;
    private ComparisonCtaButton? _ctaButton;
    private double _viewportTop = double.NegativeInfinity;
    private bool _stacked;
    private bool _built;
    private bool _syncing;

    static ComparisonTable()
    {
        FocusableProperty.OverrideDefaultValue<ComparisonTable>(false);
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<ComparisonTable>(AutomationLandmarkType.Region);
    }

    #region Test hooks

    internal IReadOnlyList<ComparisonColumn> VisibleColumns => _visible;

    internal ComparisonColumn? OwnColumn => _own;

    internal GlidingPillBar? Picker => _picker;

    internal Switch? DifferencesSwitch => _switch;

    internal ComparisonCtaButton? CtaButton => _ctaButton;

    internal SiteAnnouncer? Announcer => _announcer;

    internal bool IsStacked => _stacked;

    internal double HeadOffset => _headOffset.Y;

    /// <summary>The ids of the rows that show.</summary>
    internal IEnumerable<string> ExpandedRowIds => _sections.SelectMany(s => s.Rows).Where(r => r.Collapse.IsExpanded).Select(r => r.Row.Id);

    /// <summary>The ids of the sections whose title shows.</summary>
    internal IEnumerable<string> ExpandedSectionIds => _sections.Where(s => s.Title.IsExpanded).Select(s => s.Section.Id);

    internal IReadOnlyList<ComparisonCell> CellsOf(string rowId) => _sections.SelectMany(s => s.Rows).First(r => r.Row.Id == rowId).Cells;

    internal IEnumerable<string?> HeadColumnIds => _headSlots.Where(s => s.Cell.IsVisible).Select(s => s.ColumnId);

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_picker is not null)
        {
            _picker.ItemPressed -= OnPickerPressed;
        }
        if (_switch is not null)
        {
            _switch.IsCheckedChanged -= OnSwitchChanged;
        }
        if (_switchLabel is not null)
        {
            _switchLabel.Tapped -= OnSwitchLabelTapped;
        }
        if (_table is not null)
        {
            _table.EffectiveViewportChanged -= OnTableViewportChanged;
        }
        _table = e.NameScope.Find<StackPanel>(TablePart);
        _head = e.NameScope.Find<ComparisonRowPanel>(HeadPart);
        _body = e.NameScope.Find<StackPanel>(BodyPart);
        _foot = e.NameScope.Find<ComparisonRowPanel>(FootPart);
        _picker = e.NameScope.Find<GlidingPillBar>(PickerPart);
        _switch = e.NameScope.Find<Switch>(SwitchPart);
        _switchLabel = e.NameScope.Find<TextBlock>("PART_SwitchLabel");
        _announcer = e.NameScope.Find<SiteAnnouncer>(AnnouncerPart);
        if (_picker is not null)
        {
            _picker.ItemPressed += OnPickerPressed;
        }
        if (_switch is not null)
        {
            _switch.IsChecked = IsDifferencesOnly;
            _switch.IsCheckedChanged += OnSwitchChanged;
            AutomationProperties.SetName(_switch, DifferencesLabel);
        }
        if (_switchLabel is not null)
        {
            _switchLabel.Tapped += OnSwitchLabelTapped;
        }
        if (_head is not null)
        {
            _head.RenderTransform = _headOffset;
        }
        if (_table is not null)
        {
            _table.EffectiveViewportChanged += OnTableViewportChanged;
        }
        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ColumnsProperty || property == SectionsProperty || property == CtaProperty
            || property == IncludedLabelProperty || property == PartialLabelProperty || property == NotIncludedLabelProperty)
        {
            Rebuild();
        }
        else if (property == IsDifferencesOnlyProperty)
        {
            PseudoClasses.Set(DifferencesPseudoClass, IsDifferencesOnly);
            if (_switch is not null && !_syncing)
            {
                _syncing = true;
                _switch.IsChecked = IsDifferencesOnly;
                _syncing = false;
            }
            ApplyRowStates();
            if (_built)
            {
                _announcer?.Announce(IsDifferencesOnly ? DifferencesAnnouncement : AllRowsAnnouncement);
            }
        }
        else if (property == CompareWithProperty)
        {
            ApplyVisibleColumns(animate: true);
        }
        else if (property == StackBelowProperty)
        {
            UpdateWidth(Bounds.Width);
        }
        else if (property == StickyTopProperty)
        {
            UpdateStick();
        }
        else if (property == DescriptionProperty)
        {
            HasDescription = !string.IsNullOrEmpty(Description);
        }
        else if (property == TitleProperty)
        {
            AutomationProperties.SetName(this, Title);
        }
        else if (property == DifferencesLabelProperty && _switch is not null)
        {
            AutomationProperties.SetName(_switch, DifferencesLabel);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AutomationProperties.SetName(this, Title);
        ActualThemeVariantChanged += OnThemeVariantChanged;
        PrepareReveals();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeVariantChanged;
        DisposeWatches();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateWidth(e.NewSize.Width);
        UpdateStick();
    }

    private void UpdateWidth(double width)
    {
        if (width <= 0)
        {
            return;
        }
        TitleFontSize = Math.Clamp(16 + 0.03 * width, 28, 52);
        var medium = width <= 820;
        var tight = width <= 640;
        PseudoClasses.Set(MediumPseudoClass, medium);
        PseudoClasses.Set(TightPseudoClass, tight);
        var stacked = width < StackBelow;
        PseudoClasses.Set(StackedPseudoClass, stacked);
        var feature = tight ? 1.3 : medium ? 1.4 : 1.7;
        var column = tight ? 0.9 : 1;
        ApplyWeights(feature, column);
        foreach (var slot in _headSlots)
        {
            slot.Cell.Classes.Set("tight", tight);
        }
        if (stacked != _stacked)
        {
            _stacked = stacked;
            ApplyVisibleColumns(animate: false);
        }
    }

    private void ApplyWeights(double feature, double column)
    {
        _head?.SetWeights(feature, column);
        _foot?.SetWeights(feature, column);
        foreach (var section in _sections)
        {
            section.TitlePanel.SetWeights(feature, column);
            foreach (var row in section.Rows)
            {
                row.Panel.SetWeights(feature, column);
            }
        }
    }

    #endregion

    #region Building

    private void Rebuild()
    {
        if (_head is null || _body is null || _foot is null)
        {
            return;
        }
        DisposeWatches();
        _columns = Columns ?? ComparisonTableSamples.Columns();
        var sections = Sections ?? ComparisonTableSamples.Sections();
        _own = _columns.FirstOrDefault(c => c.IsHighlighted) ?? _columns.FirstOrDefault();
        _others = _columns.Where(c => !ReferenceEquals(c, _own)).ToList();

        _headSlots.Clear();
        _head.Children.Clear();
        _head.Children.Add(new ComparisonCellGround { MinHeight = 0 });
        foreach (var _ in _columns)
        {
            var swap = new BlockSwap { HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Bottom };
            var cell = new ComparisonCellGround { Child = swap, BandCorners = new CornerRadius(20, 20, 0, 0) };
            cell.Classes.Add("head");
            _headSlots.Add(new HeadSlot { Cell = cell, Swap = swap });
            _head.Children.Add(cell);
        }

        _sections.Clear();
        _body.Children.Clear();
        foreach (var section in sections)
        {
            _sections.Add(BuildSection(section));
        }

        BuildFoot();
        ApplyPicker();
        _built = true;
        ApplyVisibleColumns(animate: false);
        PrepareReveals();
    }

    private SectionView BuildSection(ComparisonSection section)
    {
        var titlePanel = new ComparisonRowPanel();
        // Arc: extra small muted medium text with padding 32 8 8 0.
        var title = BlockText.Create(section.Title, ArcDesignTokenKind.TypeXS, 12, ArcDesignTokenKind.TextMuted, medium: true);
        var titleCell = new ComparisonCellGround { Child = title, Padding = new Thickness(0, 32, 8, 8) };
        titlePanel.Children.Add(titleCell);
        var bands = new List<ComparisonCellGround>();
        foreach (var _ in _columns)
        {
            var band = new ComparisonCellGround();
            bands.Add(band);
            titlePanel.Children.Add(band);
        }
        var titleCollapse = new BlockCollapse { Child = titlePanel, FadesChild = true };
        _body!.Children.Add(titleCollapse);

        var rows = new List<RowView>();
        foreach (var row in section.Rows)
        {
            var view = BuildRow(row);
            rows.Add(view);
            _body.Children.Add(view.Collapse);
        }
        return new SectionView { Section = section, Title = titleCollapse, TitlePanel = titlePanel, TitleBands = bands, Rows = rows };
    }

    private RowView BuildRow(ComparisonRow row)
    {
        var panel = new ComparisonRowPanel { Background = Brushes.Transparent };
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(BlockText.Create(row.Feature, ArcDesignTokenKind.TypeSM, 14, medium: true));
        if (!string.IsNullOrEmpty(row.Hint))
        {
            text.Children.Add(BlockText.Create(row.Hint, ArcDesignTokenKind.TypeXS, 12, ArcDesignTokenKind.TextMuted));
        }
        var feature = new ComparisonCellGround { Child = text, HasRule = true };
        feature.Classes.Add("feature-cell");
        panel.Children.Add(feature);
        var cells = new List<ComparisonCell>();
        foreach (var _ in _columns)
        {
            var cell = new ComparisonCell { HasRule = true };
            cells.Add(cell);
            panel.Children.Add(cell);
        }
        var view = new RowView { Row = row, Collapse = new BlockCollapse { Child = panel, FadesChild = true }, Panel = panel, Feature = feature, Cells = cells };
        panel.PointerEntered += (_, e) =>
        {
            if (e.Pointer.Type != PointerType.Touch)
            {
                SetRowHover(view, true);
            }
        };
        panel.PointerExited += (_, _) => SetRowHover(view, false);
        return view;
    }

    private static void SetRowHover(RowView view, bool hover)
    {
        view.Feature.SetRowHover(hover);
        foreach (var cell in view.Cells)
        {
            cell.SetRowHover(hover);
        }
    }

    private void BuildFoot()
    {
        _foot!.Children.Clear();
        _footBands.Clear();
        _ctaButton = null;
        var legend = new WrapPanel { ItemSpacing = 16, LineSpacing = 8 };
        legend.Children.Add(LegendItem(ComparisonMark.Included, IncludedLabel));
        legend.Children.Add(LegendItem(ComparisonMark.Partial, PartialLabel));
        legend.Children.Add(LegendItem(ComparisonMark.NotIncluded, NotIncludedLabel));
        var legendCell = new ComparisonCellGround { Child = legend, Padding = new Thickness(0, 20, 16, 0) };
        _foot.Children.Add(legendCell);
        foreach (var column in _columns)
        {
            var band = new ComparisonCellGround { BandCorners = new CornerRadius(0, 0, 20, 20) };
            band.Classes.Add("foot");
            if (ReferenceEquals(column, _own) && Cta is { } cta)
            {
                _ctaButton = new ComparisonCtaButton { Label = cta.Label, DoneLabel = cta.DoneLabel };
                _ctaButton.Click += OnCtaClick;
                band.Child = _ctaButton;
            }
            _footBands.Add(band);
            _foot.Children.Add(band);
        }
    }

    private static Control LegendItem(ComparisonMark mark, string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(new ComparisonLegendMark { Mark = mark, VerticalAlignment = VerticalAlignment.Center });
        var label = BlockText.Create(text, ArcDesignTokenKind.TypeXS, 12, ArcDesignTokenKind.TextMuted, wrapping: TextWrapping.NoWrap);
        label.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(label);
        return row;
    }

    private void ApplyPicker()
    {
        if (_picker is null)
        {
            return;
        }
        _picker.SetItems(_others.Select(c => c.Name).ToArray());
        if (_own is not null)
        {
            AutomationProperties.SetName(_picker, string.Format(PickerLabelFormat, _own.Name));
        }
        _picker.IsVisible = _stacked && _others.Count > 1;
    }

    #endregion

    #region Visible columns and rows

    /// <summary>The competitor beside yours: <see cref="CompareWith"/> when it names one, else the first other column.</summary>
    internal ComparisonColumn? EffectiveCompare =>
        _others.FirstOrDefault(c => c.Id == CompareWith) ?? _others.FirstOrDefault();

    private void ApplyVisibleColumns(bool animate)
    {
        if (!_built || _head is null)
        {
            return;
        }
        var compare = EffectiveCompare;
        _visible = _stacked
            ? _columns.Where(c => ReferenceEquals(c, _own) || ReferenceEquals(c, compare)).ToList()
            : _columns.ToList();
        for (var i = 0; i < _headSlots.Count; i++)
        {
            var slot = _headSlots[i];
            var column = i < _visible.Count ? _visible[i] : null;
            slot.Cell.IsVisible = column is not null;
            if (column is null)
            {
                slot.ColumnId = null;
                continue;
            }
            slot.Cell.IsOwn = ReferenceEquals(column, _own);
            if (slot.ColumnId != column.Id)
            {
                var content = HeadText(column);
                if (animate)
                {
                    slot.Swap.Swap(content, s_headMotion);
                }
                else
                {
                    slot.Swap.Set(content);
                }
                slot.ColumnId = column.Id;
            }
        }
        for (var i = 0; i < _footBands.Count; i++)
        {
            var column = i < _visible.Count ? _visible[i] : null;
            var band = _footBands[i];
            band.IsVisible = column is not null;
            if (column is null)
            {
                continue;
            }
            band.IsOwn = ReferenceEquals(column, _own);
            // The button stays with your column, whichever slot that is.
            if (_ctaButton is not null)
            {
                if (band.IsOwn && !ReferenceEquals(band.Child, _ctaButton))
                {
                    foreach (var other in _footBands)
                    {
                        if (ReferenceEquals(other.Child, _ctaButton))
                        {
                            other.Child = null;
                        }
                    }
                    band.Child = _ctaButton;
                }
                else if (!band.IsOwn && ReferenceEquals(band.Child, _ctaButton))
                {
                    band.Child = null;
                }
            }
        }
        foreach (var section in _sections)
        {
            for (var i = 0; i < section.TitleBands.Count; i++)
            {
                var column = i < _visible.Count ? _visible[i] : null;
                section.TitleBands[i].IsVisible = column is not null;
                section.TitleBands[i].IsOwn = column is not null && ReferenceEquals(column, _own);
            }
            foreach (var row in section.Rows)
            {
                ApplyCells(row, animate);
            }
        }
        if (_picker is not null)
        {
            _picker.IsVisible = _stacked && _others.Count > 1;
            _picker.SelectedIndex = compare is null ? -1 : _others.IndexOf(compare);
        }
        ApplyRowStates();
    }

    private void ApplyCells(RowView row, bool animate)
    {
        for (var i = 0; i < row.Cells.Count; i++)
        {
            var cell = row.Cells[i];
            var column = i < _visible.Count ? _visible[i] : null;
            cell.IsVisible = column is not null;
            if (column is null)
            {
                continue;
            }
            cell.IsOwn = ReferenceEquals(column, _own);
            var value = row.Row.Values.TryGetValue(column.Id, out var v) ? v : ComparisonValue.NotIncluded;
            if (animate && value != cell.Value)
            {
                cell.PopTo(value);
            }
            else if (!animate || cell.Value == default)
            {
                cell.SetValueInstantly(value);
            }
            AutomationProperties.SetName(cell, $"{column.Name}: {value.AccessibleName(IncludedLabel, PartialLabel, NotIncludedLabel)}");
        }
    }

    private Control HeadText(ComparisonColumn column)
    {
        var own = ReferenceEquals(column, _own);
        // Your column's name reads in the strong accent, or the foreground in the dark theme (Arc's rule).
        var color = own && ActualThemeVariant != ThemeVariant.Dark ? ArcDesignTokenKind.AccentStrong : ArcDesignTokenKind.Foreground;
        var stack = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center };
        var name = BlockText.Create(column.Name, ArcDesignTokenKind.TypeBase, 16, color, medium: true);
        name.TextAlignment = TextAlignment.Center;
        stack.Children.Add(name);
        if (!string.IsNullOrEmpty(column.Caption))
        {
            var caption = BlockText.Create(column.Caption, ArcDesignTokenKind.TypeXS, 12, ArcDesignTokenKind.TextMuted);
            caption.TextAlignment = TextAlignment.Center;
            stack.Children.Add(caption);
        }
        return stack;
    }

    private void OnThemeVariantChanged(object? sender, EventArgs e)
    {
        foreach (var slot in _headSlots)
        {
            slot.ColumnId = null;
        }
        ApplyVisibleColumns(animate: false);
    }

    private void ApplyRowStates()
    {
        var index = 0;
        foreach (var section in _sections)
        {
            var any = false;
            foreach (var row in section.Rows)
            {
                var show = !IsDifferencesOnly || Differs(row.Row);
                row.Collapse.IsExpanded = show;
                if (show)
                {
                    row.Index = index++;
                    any = true;
                }
            }
            section.Title.IsExpanded = any;
        }
    }

    /// <summary>Whether the visible columns disagree on <paramref name="row"/>; a missing value counts as not included, notes do not count.</summary>
    private bool Differs(ComparisonRow row)
    {
        string? first = null;
        foreach (var column in _visible)
        {
            var key = (row.Values.TryGetValue(column.Id, out var v) ? v : ComparisonValue.NotIncluded).Key;
            if (first is null)
            {
                first = key;
            }
            else if (first != key)
            {
                return true;
            }
        }
        return false;
    }

    #endregion

    #region Reveal

    /// <summary>Checks draw in once, when their row is fully in view. Below full motion every mark simply shows.</summary>
    private void PrepareReveals()
    {
        if (!_built || !ArcMotion.IsFull(this))
        {
            return;
        }
        foreach (var row in _sections.SelectMany(s => s.Rows))
        {
            if (row.Revealed || row.Watch is not null)
            {
                continue;
            }
            foreach (var cell in row.Cells)
            {
                cell.PrepareDraw();
            }
            var watch = new ViewportVisibility(row.Panel, 1.0);
            row.Watch = watch;
            var view = row;
            watch.VisibilityChanged += (_, visible) =>
            {
                if (visible)
                {
                    Reveal(view);
                }
            };
        }
    }

    private void Reveal(RowView row)
    {
        row.Revealed = true;
        row.Watch?.Dispose();
        row.Watch = null;
        foreach (var cell in row.Cells)
        {
            var delay = cell.IsOwn ? TimeSpan.FromSeconds(0.06 + Math.Min(row.Index, 12) * 0.035) : TimeSpan.Zero;
            cell.PlayDraw(delay);
        }
    }

    private void DisposeWatches()
    {
        foreach (var row in _sections.SelectMany(s => s.Rows))
        {
            row.Watch?.Dispose();
            row.Watch = null;
        }
    }

    #endregion

    #region Input

    private void OnPickerPressed(int index)
    {
        if (index >= 0 && index < _others.Count)
        {
            SetCurrentValue(CompareWithProperty, _others[index].Id);
        }
    }

    private void OnSwitchChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncing || _switch is null)
        {
            return;
        }
        _syncing = true;
        SetCurrentValue(IsDifferencesOnlyProperty, _switch.IsChecked == true);
        _syncing = false;
    }

    private void OnSwitchLabelTapped(object? sender, TappedEventArgs e) => SetCurrentValue(IsDifferencesOnlyProperty, !IsDifferencesOnly);

    private void OnCtaClick(object? sender, RoutedEventArgs e)
    {
        if (Cta is not { } cta || _ctaButton is null)
        {
            return;
        }
        var args = new ComparisonCtaEventArgs(CtaInvokedEvent, this, cta);
        RaiseEvent(args);
        if (!string.IsNullOrEmpty(cta.Href) && cta.Command is null)
        {
            if (!args.Handled)
            {
                SiteLinks.TryLaunch(this, cta.Href);
            }
            return;
        }
        if (cta.Command is { } command && command.CanExecute(cta.CommandParameter))
        {
            command.Execute(cta.CommandParameter);
        }
        _ctaButton.MarkDone();
    }

    #endregion

    #region Pinned header

    private void OnTableViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        if (Math.Abs(e.EffectiveViewport.Top - _viewportTop) > 0.01)
        {
            _viewportTop = e.EffectiveViewport.Top;
            UpdateStick();
        }
    }

    /// <summary>Holds the header <see cref="StickyTop"/> below the top of the scrolled view while the table passes under it (Arc's <c>position: sticky</c>).</summary>
    private void UpdateStick()
    {
        if (_head is null || _table is null || double.IsNegativeInfinity(_viewportTop))
        {
            return;
        }
        var room = Math.Max(0, _table.Bounds.Height - _head.Bounds.Height);
        _headOffset.Y = Math.Clamp(_viewportTop + StickyTop, 0, room);
    }

    #endregion
}
