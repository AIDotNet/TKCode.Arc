using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// Arc's sortable data table: compare structured records with sortable columns. Click a header (or press
/// Enter / Space on it) to sort ascending, again for descending; rows slide to their new places on a smooth
/// spring and the sort arrow flips on a snappy one. <see cref="IsSelectable"/> adds a checkbox column, row-click
/// selection, Shift range selection, Escape to clear and a count line with Clear selection. Rows are drawn as
/// light text cells, virtualized for thousands of records, sorted by index (never by container), and the header
/// sticks to the top of the visible area. Below 620px rows fold into two lines and the header becomes a strip of
/// sort pills, as in Arc.
/// </summary>
[TemplatePart(ScrollViewerPart, typeof(ScrollViewer))]
[TemplatePart(AnnouncerPart, typeof(TextBlock))]
[TemplatePart(BodyPart, typeof(Grid))]
[PseudoClasses(CompactPseudoClass, SelectablePseudoClass, LoadingPseudoClass, EmptyPseudoClass, SelectionPseudoClass)]
public partial class Table : TemplatedControl
{
    public const string ScrollViewerPart = "PART_ScrollViewer";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>The two-row grid of the template that holds the scroll viewer and receives the loading veil and the footer.</summary>
    public const string BodyPart = "PART_Body";

    /// <summary>The Clear selection button in the footer (built with the footer the first time <see cref="IsSelectable"/> is true).</summary>
    public const string ClearPart = "PART_Clear";

    /// <summary>The loading veil over the body (built the first time <see cref="IsLoading"/> is true).</summary>
    public const string LoadingPart = "PART_Loading";

    /// <summary>The count line under the body (built the first time <see cref="IsSelectable"/> is true).</summary>
    public const string FooterPart = "PART_Footer";
    public const string CompactPseudoClass = ":compact";
    public const string SelectablePseudoClass = ":selectable";
    public const string LoadingPseudoClass = ":loading";
    public const string EmptyPseudoClass = ":empty";
    public const string SelectionPseudoClass = ":has-selection";

    #region Public properties

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<Table, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<DataTableColumn?> SortColumnProperty =
        AvaloniaProperty.Register<Table, DataTableColumn?>(nameof(SortColumn), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> SortKeyProperty =
        AvaloniaProperty.Register<Table, string?>(nameof(SortKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Sort direction of the sorted column (two-way): None, Ascending or Descending.</summary>
    public static readonly StyledProperty<DataTableSortDirection> SortDirectionProperty =
        AvaloniaProperty.Register<Table, DataTableSortDirection>(nameof(SortDirection), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsSortClearableProperty =
        AvaloniaProperty.Register<Table, bool>(nameof(IsSortClearable));

    public static readonly StyledProperty<bool> IsSelectableProperty =
        AvaloniaProperty.Register<Table, bool>(nameof(IsSelectable));

    public static readonly StyledProperty<bool> SelectsOnRowClickProperty =
        AvaloniaProperty.Register<Table, bool>(nameof(SelectsOnRowClick), true);

    /// <summary>
    /// Alternates the row background: every second row, counted by its place in the shown order (after sorting; scrolling,
    /// inserts and removals keep the alternation), gets a slightly different fill. Hover and selection stay on top of the stripe.
    /// Default false.
    /// </summary>
    public static readonly StyledProperty<bool> IsStripedProperty =
        AvaloniaProperty.Register<Table, bool>(nameof(IsStriped));

    public static readonly StyledProperty<IList?> SelectedItemsProperty =
        AvaloniaProperty.Register<Table, IList?>(nameof(SelectedItems), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<Func<object, object>?> RowKeySelectorProperty =
        AvaloniaProperty.Register<Table, Func<object, object>?>(nameof(RowKeySelector));

    public static readonly StyledProperty<string> CaptionProperty =
        AvaloniaProperty.Register<Table, string>(nameof(Caption), "Data table");

    /// <summary>Text shown when there are no rows (default "No rows to show").</summary>
    public static readonly StyledProperty<string> EmptyMessageProperty =
        AvaloniaProperty.Register<Table, string>(nameof(EmptyMessage), "No rows to show");

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<Table, bool>(nameof(IsLoading));

    /// <summary>Text shown with the loading spinner (default "Loading rows").</summary>
    public static readonly StyledProperty<string> LoadingMessageProperty =
        AvaloniaProperty.Register<Table, string>(nameof(LoadingMessage), "Loading rows");

    public static readonly StyledProperty<string> ItemNameOneProperty =
        AvaloniaProperty.Register<Table, string>(nameof(ItemNameOne), "row");

    public static readonly StyledProperty<string> ItemNameOtherProperty =
        AvaloniaProperty.Register<Table, string>(nameof(ItemNameOther), "rows");

    /// <summary>Word for the selected rows in the count line, as in "2 selected" (default "selected").</summary>
    public static readonly StyledProperty<string> SelectedNounProperty =
        AvaloniaProperty.Register<Table, string>(nameof(SelectedNoun), "selected");

    /// <summary>Accessible name of the select-all checkbox in the header (default "Select all rows").</summary>
    public static readonly StyledProperty<string> SelectAllLabelProperty =
        AvaloniaProperty.Register<Table, string>(nameof(SelectAllLabel), "Select all rows");

    /// <summary>Text of the Clear selection action in the count line (default "Clear selection").</summary>
    public static readonly StyledProperty<string> ClearSelectionLabelProperty =
        AvaloniaProperty.Register<Table, string>(nameof(ClearSelectionLabel), "Clear selection");

    public static readonly RoutedEvent<DataTableSortChangedEventArgs> SortChangedEvent =
        RoutedEvent.Register<Table, DataTableSortChangedEventArgs>(nameof(SortChanged), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<DataTableSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<Table, DataTableSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>Row items (any enumerable; <see cref="INotifyCollectionChanged"/> sources are followed).</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>Column definitions (the XAML content of the table).</summary>
    [Content]
    public AvaloniaList<DataTableColumn> Columns { get; } = new();

    /// <summary>The sorted column (two-way). Setting a column while <see cref="SortDirection"/> is None sorts ascending.</summary>
    public DataTableColumn? SortColumn { get => GetValue(SortColumnProperty); set => SetValue(SortColumnProperty, value); }

    /// <summary>The sorted column's <see cref="DataTableColumn.EffectiveKey"/> (two-way, for view models).</summary>
    public string? SortKey { get => GetValue(SortKeyProperty); set => SetValue(SortKeyProperty, value); }

    /// <inheritdoc cref="SortDirectionProperty"/>
    public DataTableSortDirection SortDirection { get => GetValue(SortDirectionProperty); set => SetValue(SortDirectionProperty, value); }

    /// <summary>When true a third click on a sorted header clears the sort (Arc toggles between ascending and descending).</summary>
    public bool IsSortClearable { get => GetValue(IsSortClearableProperty); set => SetValue(IsSortClearableProperty, value); }

    /// <summary>Adds the checkbox column, row-click selection and the count line with Clear selection.</summary>
    public bool IsSelectable { get => GetValue(IsSelectableProperty); set => SetValue(IsSelectableProperty, value); }

    /// <summary>
    /// When true (the default) a click anywhere on a row toggles it; when false only the checkbox cell does, and clicks on
    /// the rest of the row are left to the app. Keyboard selection is the same either way.
    /// </summary>
    public bool SelectsOnRowClick { get => GetValue(SelectsOnRowClickProperty); set => SetValue(SelectsOnRowClickProperty, value); }

    /// <inheritdoc cref="IsStripedProperty"/>
    public bool IsStriped { get => GetValue(IsStripedProperty); set => SetValue(IsStripedProperty, value); }

    /// <summary>Selected row items in display order (two-way). A bound mutable list is updated in place.</summary>
    public IList? SelectedItems { get => GetValue(SelectedItemsProperty); set => SetValue(SelectedItemsProperty, value); }

    /// <summary>Row identity (Arc <c>rowKey</c>); defaults to the item itself. Selection and row motion follow keys across source changes.</summary>
    public Func<object, object>? RowKeySelector { get => GetValue(RowKeySelectorProperty); set => SetValue(RowKeySelectorProperty, value); }

    /// <summary>Accessible name of the table.</summary>
    public string Caption { get => GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

    /// <inheritdoc cref="EmptyMessageProperty"/>
    public string EmptyMessage { get => GetValue(EmptyMessageProperty); set => SetValue(EmptyMessageProperty, value); }

    /// <summary>Shows a spinner over the body (in place of the empty message when there are no rows).</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <inheritdoc cref="LoadingMessageProperty"/>
    public string LoadingMessage { get => GetValue(LoadingMessageProperty); set => SetValue(LoadingMessageProperty, value); }

    /// <summary>Singular noun of the count line, as in "1 project".</summary>
    public string ItemNameOne { get => GetValue(ItemNameOneProperty); set => SetValue(ItemNameOneProperty, value); }

    /// <summary>Plural noun of the count line, as in "6 projects".</summary>
    public string ItemNameOther { get => GetValue(ItemNameOtherProperty); set => SetValue(ItemNameOtherProperty, value); }

    /// <inheritdoc cref="SelectedNounProperty"/>
    public string SelectedNoun { get => GetValue(SelectedNounProperty); set => SetValue(SelectedNounProperty, value); }

    /// <inheritdoc cref="SelectAllLabelProperty"/>
    public string SelectAllLabel { get => GetValue(SelectAllLabelProperty); set => SetValue(SelectAllLabelProperty, value); }

    /// <inheritdoc cref="ClearSelectionLabelProperty"/>
    public string ClearSelectionLabel { get => GetValue(ClearSelectionLabelProperty); set => SetValue(ClearSelectionLabelProperty, value); }

    /// <summary>Raised when the user sorts (header click or keyboard) or <see cref="Sort"/> is called.</summary>
    public event EventHandler<DataTableSortChangedEventArgs>? SortChanged
    {
        add => AddHandler(SortChangedEvent, value);
        remove => RemoveHandler(SortChangedEvent, value);
    }

    /// <summary>Raised when the user changes the selection (checkboxes, row clicks, Select all, Clear, Escape).</summary>
    public event EventHandler<DataTableSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    #endregion

    #region Theme-provided values

    internal static readonly StyledProperty<IBrush?> ColumnTintBrushProperty =
        AvaloniaProperty.Register<Table, IBrush?>(nameof(ColumnTintBrush));
    internal static readonly StyledProperty<IBrush?> StripeBrushProperty =
        AvaloniaProperty.Register<Table, IBrush?>(nameof(StripeBrush));
    internal static readonly StyledProperty<IBrush?> StripeColumnBrushProperty =
        AvaloniaProperty.Register<Table, IBrush?>(nameof(StripeColumnBrush));
    internal static readonly StyledProperty<IBrush?> SecondaryForegroundProperty =
        AvaloniaProperty.Register<Table, IBrush?>(nameof(SecondaryForeground));
    internal static readonly StyledProperty<IBrush?> MutedForegroundProperty =
        AvaloniaProperty.Register<Table, IBrush?>(nameof(MutedForeground));
    internal static readonly StyledProperty<IBrush?> RuleBrushProperty =
        AvaloniaProperty.Register<Table, IBrush?>(nameof(RuleBrush));
    internal static readonly StyledProperty<IBrush?> DividerBrushProperty =
        AvaloniaProperty.Register<Table, IBrush?>(nameof(DividerBrush));
    internal static readonly StyledProperty<IBrush?> CheckboxBorderBrushProperty =
        AvaloniaProperty.Register<Table, IBrush?>(nameof(CheckboxBorderBrush));
    internal static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<Table, IBrush?>(nameof(AccentBrush));
    internal static readonly StyledProperty<IBrush?> CheckboxOnBrushProperty =
        AvaloniaProperty.Register<Table, IBrush?>(nameof(CheckboxOnBrush));
    internal static readonly StyledProperty<IBrush?> CheckmarkBrushProperty =
        AvaloniaProperty.Register<Table, IBrush?>(nameof(CheckmarkBrush));
    internal static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<Table, IBrush?>(nameof(FocusRingBrush));
    internal static readonly StyledProperty<double> SmallFontSizeProperty =
        AvaloniaProperty.Register<Table, double>(nameof(SmallFontSize), 12);
    internal static readonly StyledProperty<FontWeight> MediumWeightProperty =
        AvaloniaProperty.Register<Table, FontWeight>(nameof(MediumWeight), FontWeight.Medium);
    internal static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<Table, double>(nameof(RowHeight), 52);
    internal static readonly StyledProperty<double> HeaderHeightProperty =
        AvaloniaProperty.Register<Table, double>(nameof(HeaderHeight), 44);
    internal static readonly StyledProperty<double> CompactRowHeightProperty =
        AvaloniaProperty.Register<Table, double>(nameof(CompactRowHeight), 64);
    internal static readonly StyledProperty<double> CompactHeaderHeightProperty =
        AvaloniaProperty.Register<Table, double>(nameof(CompactHeaderHeight), 41);
    internal static readonly StyledProperty<double> EmptyRowHeightProperty =
        AvaloniaProperty.Register<Table, double>(nameof(EmptyRowHeight), 120);
    internal static readonly StyledProperty<double> CellInlinePaddingProperty =
        AvaloniaProperty.Register<Table, double>(nameof(CellInlinePadding), 16);
    internal static readonly StyledProperty<double> CellBlockPaddingProperty =
        AvaloniaProperty.Register<Table, double>(nameof(CellBlockPadding), 14);
    internal static readonly StyledProperty<double> CompactCellBlockPaddingProperty =
        AvaloniaProperty.Register<Table, double>(nameof(CompactCellBlockPadding), 10);
    internal static readonly StyledProperty<double> WrapColumnMaxWidthProperty =
        AvaloniaProperty.Register<Table, double>(nameof(WrapColumnMaxWidth), 320);
    internal static readonly StyledProperty<double> PrimaryInsetSelectableProperty =
        AvaloniaProperty.Register<Table, double>(nameof(PrimaryInsetSelectable), 8);
    internal static readonly StyledProperty<double> SelectColumnWidthProperty =
        AvaloniaProperty.Register<Table, double>(nameof(SelectColumnWidth), 44);
    internal static readonly StyledProperty<double> MinTableWidthProperty =
        AvaloniaProperty.Register<Table, double>(nameof(MinTableWidth), 560);
    internal static readonly StyledProperty<double> CompactBreakpointProperty =
        AvaloniaProperty.Register<Table, double>(nameof(CompactBreakpoint), 620);
    internal static readonly StyledProperty<double> ControlRadiusProperty =
        AvaloniaProperty.Register<Table, double>(nameof(ControlRadius), 18);

    internal IBrush? ColumnTintBrush { get => GetValue(ColumnTintBrushProperty); set => SetValue(ColumnTintBrushProperty, value); }
    internal IBrush? StripeBrush { get => GetValue(StripeBrushProperty); set => SetValue(StripeBrushProperty, value); }
    internal IBrush? StripeColumnBrush { get => GetValue(StripeColumnBrushProperty); set => SetValue(StripeColumnBrushProperty, value); }
    internal IBrush? SecondaryForeground { get => GetValue(SecondaryForegroundProperty); set => SetValue(SecondaryForegroundProperty, value); }
    internal IBrush? MutedForeground { get => GetValue(MutedForegroundProperty); set => SetValue(MutedForegroundProperty, value); }
    internal IBrush? RuleBrush { get => GetValue(RuleBrushProperty); set => SetValue(RuleBrushProperty, value); }
    internal IBrush? DividerBrush { get => GetValue(DividerBrushProperty); set => SetValue(DividerBrushProperty, value); }
    internal IBrush? CheckboxBorderBrush { get => GetValue(CheckboxBorderBrushProperty); set => SetValue(CheckboxBorderBrushProperty, value); }
    internal IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    internal IBrush? CheckboxOnBrush { get => GetValue(CheckboxOnBrushProperty); set => SetValue(CheckboxOnBrushProperty, value); }
    internal IBrush? CheckmarkBrush { get => GetValue(CheckmarkBrushProperty); set => SetValue(CheckmarkBrushProperty, value); }
    internal IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }
    internal double SmallFontSize { get => GetValue(SmallFontSizeProperty); set => SetValue(SmallFontSizeProperty, value); }
    internal FontWeight MediumWeight { get => GetValue(MediumWeightProperty); set => SetValue(MediumWeightProperty, value); }
    internal double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }
    internal double HeaderHeight { get => GetValue(HeaderHeightProperty); set => SetValue(HeaderHeightProperty, value); }
    internal double CompactRowHeight { get => GetValue(CompactRowHeightProperty); set => SetValue(CompactRowHeightProperty, value); }
    internal double CompactHeaderHeight { get => GetValue(CompactHeaderHeightProperty); set => SetValue(CompactHeaderHeightProperty, value); }
    internal double EmptyRowHeight { get => GetValue(EmptyRowHeightProperty); set => SetValue(EmptyRowHeightProperty, value); }
    internal double CellInlinePadding { get => GetValue(CellInlinePaddingProperty); set => SetValue(CellInlinePaddingProperty, value); }
    internal double CellBlockPadding { get => GetValue(CellBlockPaddingProperty); set => SetValue(CellBlockPaddingProperty, value); }
    internal double CompactCellBlockPadding { get => GetValue(CompactCellBlockPaddingProperty); set => SetValue(CompactCellBlockPaddingProperty, value); }
    internal double WrapColumnMaxWidth { get => GetValue(WrapColumnMaxWidthProperty); set => SetValue(WrapColumnMaxWidthProperty, value); }
    internal double PrimaryInsetSelectable { get => GetValue(PrimaryInsetSelectableProperty); set => SetValue(PrimaryInsetSelectableProperty, value); }
    internal double SelectColumnWidth { get => GetValue(SelectColumnWidthProperty); set => SetValue(SelectColumnWidthProperty, value); }
    internal double MinTableWidth { get => GetValue(MinTableWidthProperty); set => SetValue(MinTableWidthProperty, value); }
    internal double CompactBreakpoint { get => GetValue(CompactBreakpointProperty); set => SetValue(CompactBreakpointProperty, value); }
    internal double ControlRadius { get => GetValue(ControlRadiusProperty); set => SetValue(ControlRadiusProperty, value); }

    internal static readonly DirectProperty<Table, string> CountTextProperty =
        AvaloniaProperty.RegisterDirect<Table, string>(nameof(CountText), o => o.CountText);

    internal static readonly DirectProperty<Table, string> CountNounProperty =
        AvaloniaProperty.RegisterDirect<Table, string>(nameof(CountNoun), o => o.CountNoun);

    internal static readonly DirectProperty<Table, int> CountDirectionProperty =
        AvaloniaProperty.RegisterDirect<Table, int>(nameof(CountDirection), o => o.CountDirection);

    internal static readonly DirectProperty<Table, int> NounDirectionProperty =
        AvaloniaProperty.RegisterDirect<Table, int>(nameof(NounDirection), o => o.NounDirection);

    internal static readonly DirectProperty<Table, bool> HasSelectionProperty =
        AvaloniaProperty.RegisterDirect<Table, bool>(nameof(HasSelection), o => o.HasSelection);

    private string _countText = "0";
    private string _countNoun = "rows";
    private int _countDirection = 1;
    private int _nounDirection = -1;
    private bool _hasSelection;

    internal string CountText { get => _countText; private set => SetAndRaise(CountTextProperty, ref _countText, value); }
    internal string CountNoun { get => _countNoun; private set => SetAndRaise(CountNounProperty, ref _countNoun, value); }
    internal int CountDirection { get => _countDirection; private set => SetAndRaise(CountDirectionProperty, ref _countDirection, value); }
    internal int NounDirection { get => _nounDirection; private set => SetAndRaise(NounDirectionProperty, ref _nounDirection, value); }
    internal bool HasSelection { get => _hasSelection; private set => SetAndRaise(HasSelectionProperty, ref _hasSelection, value); }

    #endregion

    private sealed class ColumnState
    {
        public ColumnState(DataTableColumn column)
        {
            Column = column;
            if (column.ValueSelector is null && column.Binding is { } binding)
            {
                ValueProbe = new TableBindingProbe(binding);
            }
            if (column.SortValueSelector is null && column.SortBinding is { } sortBinding)
            {
                SortProbe = new TableBindingProbe(sortBinding);
            }
        }

        public DataTableColumn Column { get; }
        public TableBindingProbe? ValueProbe { get; }
        public TableBindingProbe? SortProbe { get; }
        public bool? Numeric { get; set; }
        public object?[]? SortValues { get; set; }
        public double Natural { get; set; } = -1;
    }

    private List<object> _items = new();
    private List<object> _keys = new();
    private Dictionary<object, int> _indexByKey = new();
    private int[] _order = [];
    private int[] _position = [];
    private HashSet<object> _selected = new();
    private object? _anchor;
    private ColumnState[] _states = [];
    private bool _naturalLocked;
    private int _compactLead = -1;
    private bool _wraps;
    private bool _wrapsTemplated;
    private double[] _rowHeights = [];
    private HeightKey _heightKey;
    private int[]? _topsOrder;
    private bool _topsDirty;
    private TablePresenter? _presenter;
    private ScrollViewer? _scroll;
    private TextBlock? _announcer;
    private Grid? _body;
    private Panel? _loading;
    private Border? _footer;
    private Border? _countHost;
    private IDisposable? _countForeground;
    private bool _countForegroundSelected;
    private AvaloniaButton? _clear;
    private INotifyCollectionChanged? _observedSource;
    private INotifyCollectionChanged? _observedSelection;
    private bool _syncingSort;
    private bool _syncingSelection;
    private int _lastShown = -1;
    private int _currentRow = -1;
    private double _widthHint = double.NaN;

    internal TableLayout Layout { get; } = new();
    internal TablePalette Palette { get; } = new();

    public Table()
    {
        Columns.CollectionChanged += OnColumnsChanged;
        SetCurrentValue(SelectedItemsProperty, new AvaloniaList<object>());
        _presenter = new TablePresenter(this);
    }

    #region Template, palette, count

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_scroll is not null)
        {
            _scroll.Content = null;
        }
        if (_clear is not null)
        {
            _clear.Click -= OnClearClick;
        }
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollViewerPart);
        _announcer = e.NameScope.Find<TextBlock>(AnnouncerPart);
        _body = e.NameScope.Find<Grid>(BodyPart);
        // The old template took the lazily built parts with it; they come back on demand.
        _clear = null;
        _loading = null;
        _footer = null;
        _countHost = null;
        _countForeground?.Dispose();
        _countForeground = null;
        _presenter ??= new TablePresenter(this);
        if (_scroll is not null)
        {
            _scroll.Content = _presenter;
        }
        if (IsLoading)
        {
            EnsureLoading();
        }
        if (IsSelectable)
        {
            EnsureFooter();
        }
        UpdatePalette();
        _presenter.Header.Rebuild();
        _presenter.Reset();
        UpdateHeaderSort(animate: false);
        ApplySelectionVisuals(animate: false);
        UpdateCount();
    }

    private void OnClearClick(object? sender, RoutedEventArgs e) => ClearSelection();

    /// <summary>The loading veil (PART_Loading): a 60% surface over the body, a spinner and the message; bound like the template part was.</summary>
    private void EnsureLoading()
    {
        if (_loading is not null || _body is null)
        {
            return;
        }
        var veil = new Border { Opacity = 0.6 };
        veil.Bind(Border.BackgroundProperty, this.GetObservable(BackgroundProperty), BindingPriority.Template);
        var message = new TextBlock { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        message.Bind(TextBlock.TextProperty, this.GetObservable(LoadingMessageProperty), BindingPriority.Template);
        message.Bind(TextBlock.FontSizeProperty, this.GetObservable(FontSizeProperty), BindingPriority.Template);
        ArcTokens.Bind(message, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        var row = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Children =
            {
                new ArcSpinner { Width = 18, Height = 18, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
                message
            }
        };
        _loading = new Panel { Name = LoadingPart, Children = { veil, row } };
        _loading.Bind(IsVisibleProperty, this.GetObservable(IsLoadingProperty), BindingPriority.Template);
        _loading.Bind(MarginProperty, _loading.GetResourceObservable(TableTokens.Key(TableTokenKind.LoadingInset)), BindingPriority.Template);
        // Above the scroll viewer, below the footer and the announcer, as in the template.
        _body.Children.Insert(Math.Min(1, _body.Children.Count), _loading);
    }

    /// <summary>The footer (PART_Footer): the rolling count line and the Clear selection button (PART_Clear) in a presence host.</summary>
    private void EnsureFooter()
    {
        if (_footer is not null || _body is null)
        {
            return;
        }
        var count = new ArcContentSwap { Mode = ContentSwapMode.Roll };
        count.Bind(ArcContentSwap.DirectionProperty, this.GetObservable(CountDirectionProperty), BindingPriority.Template);
        count.Bind(ArcContentSwap.ContentProperty, this.GetObservable(CountTextProperty), BindingPriority.Template);
        var noun = new ArcContentSwap { Mode = ContentSwapMode.Roll };
        noun.Bind(ArcContentSwap.DirectionProperty, this.GetObservable(NounDirectionProperty), BindingPriority.Template);
        noun.Bind(ArcContentSwap.ContentProperty, this.GetObservable(CountNounProperty), BindingPriority.Template);
        var words = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Children =
            {
                new SpringSizeDecorator { Axis = SpringSizeAxis.Width, Spring = ArcSpring.Morph, Child = count },
                new SpringSizeDecorator { Axis = SpringSizeAxis.Width, Spring = ArcSpring.Morph, Child = noun }
            }
        };
        words.Bind(StackPanel.SpacingProperty, words.GetResourceObservable(TableTokens.Key(TableTokenKind.CountGap)), BindingPriority.Template);
        _countHost = new Border
        {
            Name = "PART_CountHost",
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            ClipToBounds = false,
            Child = words
        };
        _countForegroundSelected = !HasSelection;
        UpdateCountForeground();
        var clear = new Button { Name = ClearPart, Variant = ButtonVariant.Ghost };
        clear.Bind(ContentControl.ContentProperty, this.GetObservable(ClearSelectionLabelProperty), BindingPriority.Template);
        ArcTokens.Bind(clear, ForegroundProperty, ArcDesignTokenKind.Foreground);
        clear.Bind(MinHeightProperty, clear.GetResourceObservable(TableTokens.Key(TableTokenKind.ClearButtonHeight)), BindingPriority.Template);
        clear.Bind(HeightProperty, clear.GetResourceObservable(TableTokens.Key(TableTokenKind.ClearButtonHeight)), BindingPriority.Template);
        clear.Bind(PaddingProperty, clear.GetResourceObservable(TableTokens.Key(TableTokenKind.ClearButtonInset)), BindingPriority.Template);
        clear.Click += OnClearClick;
        _clear = clear;
        var presence = new PresenceHost
        {
            Mode = PresenceMode.Scale,
            EnterScale = 0.96,
            Spring = ArcSpring.Snappy,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Child = clear
        };
        presence.Bind(PresenceHost.IsPresentProperty, this.GetObservable(HasSelectionProperty), BindingPriority.Template);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(presence, 1);
        grid.Children.Add(_countHost);
        grid.Children.Add(presence);
        _footer = new Border { Name = FooterPart, Child = grid };
        Grid.SetRow(_footer, 1);
        _footer.Bind(IsVisibleProperty, this.GetObservable(IsSelectableProperty), BindingPriority.Template);
        _footer.Bind(HeightProperty, _footer.GetResourceObservable(TableTokens.Key(TableTokenKind.FooterHeight)), BindingPriority.Template);
        _footer.Bind(Border.PaddingProperty, _footer.GetResourceObservable(TableTokens.Key(TableTokenKind.FooterInset)), BindingPriority.Template);
        ArcTokens.Bind(_footer, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessTop);
        _footer.Bind(Border.BorderBrushProperty, this.GetObservable(BorderBrushProperty), BindingPriority.Template);
        // Before the announcer (the template's last child) so the z-order matches the template.
        var at = _announcer is not null && _body.Children.Count > 0 && ReferenceEquals(_body.Children[^1], _announcer)
            ? _body.Children.Count - 1
            : _body.Children.Count;
        _body.Children.Insert(at, _footer);
    }

    /// <summary>The count words are muted until something is selected (the template styled this on <c>:has-selection</c>).</summary>
    private void UpdateCountForeground()
    {
        if (_countHost is null || _countForegroundSelected == HasSelection)
        {
            return;
        }
        _countForegroundSelected = HasSelection;
        _countForeground?.Dispose();
        _countForeground = ArcTokens.Bind(_countHost, TextElement.ForegroundProperty,
            HasSelection ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.TextMuted);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var inner = availableSize.Width - BorderThickness.Left - BorderThickness.Right;
        var hint = double.IsFinite(inner) ? Math.Max(0, inner) : double.NaN;
        var changed = double.IsNaN(hint) ? !double.IsNaN(_widthHint) : double.IsNaN(_widthHint) || Math.Abs(hint - _widthHint) > 0.01;
        if (changed)
        {
            _widthHint = hint;
            _presenter?.InvalidateMeasure();
        }
        return base.MeasureOverride(availableSize);
    }

    private void UpdatePalette()
    {
        static Color ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;
        Palette.Update(ColorOf(Background), ColorOf(ColumnTintBrush), ColorOf(Foreground), ColorOf(SecondaryForeground),
            ColorOf(MutedForeground), ColorOf(RuleBrush), ColorOf(DividerBrush), ColorOf(CheckboxBorderBrush),
            ColorOf(AccentBrush), ColorOf(CheckboxOnBrush), ColorOf(CheckmarkBrush),
            FocusRingBrush is ISolidColorBrush ring ? ring.Color : null, FontFamily, FontSize, SmallFontSize, MediumWeight,
            ColorOf(StripeBrush), ColorOf(StripeColumnBrush));
        _presenter?.InvalidateRows();
        _presenter?.InvalidateMeasure();
    }

    private void UpdateCount()
    {
        var selected = SelectedPresentCount();
        var total = _items.Count;
        var shown = selected > 0 ? selected : total;
        if (_lastShown >= 0 && shown != _lastShown)
        {
            CountDirection = shown > _lastShown ? 1 : -1;
        }
        _lastShown = shown;
        NounDirection = selected > 0 ? 1 : -1;
        CountText = shown.ToString(CultureInfo.CurrentCulture);
        CountNoun = selected > 0 ? SelectedNoun : total == 1 ? ItemNameOne : ItemNameOther;
        HasSelection = selected > 0;
        UpdateCountForeground();
    }

    private void Announce(string message)
    {
        if (_announcer is null)
        {
            return;
        }
        _announcer.Text = message;
        AutomationProperties.SetName(_announcer, message);
    }

    internal void OnLayoutModeChanged() => UpdatePseudoClasses();

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(SelectablePseudoClass, IsSelectable);
        PseudoClasses.Set(LoadingPseudoClass, IsLoading);
        PseudoClasses.Set(EmptyPseudoClass, _items.Count == 0);
        PseudoClasses.Set(SelectionPseudoClass, SelectedPresentCount() > 0);
        PseudoClasses.Set(CompactPseudoClass, Layout.Compact);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ItemsSourceProperty)
        {
            OnItemsSourceChanged(change.OldValue as IEnumerable, change.NewValue as IEnumerable);
        }
        else if (property == SortColumnProperty || property == SortDirectionProperty)
        {
            if (_syncingSort)
            {
                return;
            }
            if (property == SortColumnProperty && SortColumn is not null && SortDirection == DataTableSortDirection.None)
            {
                SetSortState(SortColumn, DataTableSortDirection.Ascending);
            }
            else
            {
                SetSortState(SortColumn, SortDirection);
            }
            ApplySort(animate: IsLoaded);
        }
        else if (property == SortKeyProperty)
        {
            if (_syncingSort)
            {
                return;
            }
            var key = SortKey;
            var column = key is null ? null : Columns.FirstOrDefault(c => c.EffectiveKey == key);
            SetSortState(column, column is not null && SortDirection == DataTableSortDirection.None
                ? DataTableSortDirection.Ascending
                : key is null ? DataTableSortDirection.None : SortDirection);
            ApplySort(animate: IsLoaded);
        }
        else if (property == IsSelectableProperty)
        {
            if (IsSelectable)
            {
                EnsureFooter();
            }
            _naturalLocked = false;
            foreach (var state in _states)
            {
                state.Natural = -1;
            }
            _presenter?.Header.Rebuild();
            _presenter?.Reset();
            UpdateHeaderSort(animate: false);
            ApplySelectionVisuals(animate: false);
            UpdatePseudoClasses();
        }
        else if (property == SelectedItemsProperty)
        {
            OnSelectedItemsChanged(change.NewValue as IList);
        }
        else if (property == RowKeySelectorProperty)
        {
            RebuildItems(animate: false);
        }
        else if (property == IsStripedProperty)
        {
            _presenter?.InvalidateStripes();
        }
        else if (property == IsLoadingProperty)
        {
            if (IsLoading)
            {
                EnsureLoading();
            }
            UpdatePseudoClasses();
            _presenter?.InvalidateVisual();
        }
        else if (property == EmptyMessageProperty)
        {
            _presenter?.InvalidateVisual();
        }
        else if (property == ItemNameOneProperty || property == ItemNameOtherProperty || property == SelectedNounProperty)
        {
            UpdateCount();
        }
        else if (property == SelectAllLabelProperty)
        {
            _presenter?.Header.CellFor(-1)?.UpdateAutomation();
        }
        else if (property == CaptionProperty)
        {
            AutomationProperties.SetName(this, Caption);
        }
        else if (property == CornerRadiusProperty)
        {
            ControlRadius = CornerRadius.TopLeft;
        }
        else if (property == BackgroundProperty || property == ForegroundProperty || property == ColumnTintBrushProperty ||
                 property == SecondaryForegroundProperty || property == MutedForegroundProperty || property == RuleBrushProperty ||
                 property == DividerBrushProperty || property == CheckboxBorderBrushProperty || property == AccentBrushProperty ||
                 property == CheckboxOnBrushProperty || property == CheckmarkBrushProperty || property == FocusRingBrushProperty ||
                 property == StripeBrushProperty || property == StripeColumnBrushProperty ||
                 property == FontFamilyProperty || property == FontSizeProperty || property == SmallFontSizeProperty ||
                 property == MediumWeightProperty)
        {
            UpdatePalette();
        }
        else if (property == WrapColumnMaxWidthProperty)
        {
            _naturalLocked = false;
            _presenter?.InvalidateMeasure();
        }
        else if (property == RowHeightProperty || property == HeaderHeightProperty || property == CompactRowHeightProperty ||
                 property == CompactHeaderHeightProperty || property == EmptyRowHeightProperty || property == CellInlinePaddingProperty ||
                 property == PrimaryInsetSelectableProperty || property == SelectColumnWidthProperty ||
                 property == CellBlockPaddingProperty || property == CompactCellBlockPaddingProperty ||
                 property == MinTableWidthProperty || property == CompactBreakpointProperty)
        {
            _presenter?.InvalidateMeasure();
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AutomationProperties.SetName(this, Caption);
        UpdatePseudoClasses();
    }

    protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new TablePeer(this);

    private sealed class TablePeer : Avalonia.Automation.Peers.ControlAutomationPeer
    {
        public TablePeer(Table owner) : base(owner)
        {
        }

        protected override Avalonia.Automation.Peers.AutomationControlType GetAutomationControlTypeCore() =>
            Avalonia.Automation.Peers.AutomationControlType.Table;
    }

    #endregion
}
