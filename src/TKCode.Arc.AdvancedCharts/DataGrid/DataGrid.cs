using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
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
using Avalonia.Threading;
using AtomUI.Localization;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>
/// A spreadsheet grid for editing tabular data in place: range selection, a fill handle, Excel and Google Sheets
/// clipboard, undo, multi-column sort, a filter row and quick search, pinned and hidden columns, row checks with bulk
/// actions, a totals row, grouping with subtotals, CSV and Excel export, and rows and columns that render only while in
/// view. Every piece of view state (sort, filters, search, checked rows, hidden and pinned columns, density, grouping) is
/// a two-way property, so it works bound or left to the grid.
/// <para>
/// Rows come from <see cref="ItemsSource"/> (<see cref="DataGridRow"/> objects or any class read through column
/// bindings or accessors), or block by block from a <see cref="DataSource"/>. Edits write back into the row items and
/// raise <see cref="CellsChanged"/>; editing never re-sorts or re-filters, so rows never jump under the cursor.
/// </para>
/// </summary>
[TemplatePart(ScrollViewerPart, typeof(ScrollViewer))]
[TemplatePart(ToolbarPart, typeof(DataGridToolbar))]
[TemplatePart(StatusBarPart, typeof(DataGridStatusBar))]
[TemplatePart(AnnouncerPart, typeof(TextBlock))]
[PseudoClasses(CompactPseudoClass, ComfortablePseudoClass, FilteringPseudoClass, CheckedRowsPseudoClass, EditingPseudoClass,
    GroupedPseudoClass, ScrolledXPseudoClass, LoadingPseudoClass, EmptyPseudoClass, ReadOnlyPseudoClass,
    ToolbarCompactPseudoClass, ToolbarMinimalPseudoClass, StatusWrapPseudoClass, FillPseudoClass)]
public partial class DataGrid : TemplatedControl
{
    public const string ScrollViewerPart = "PART_ScrollViewer";
    public const string ToolbarPart = "PART_Toolbar";
    public const string StatusBarPart = "PART_StatusBar";
    public const string AnnouncerPart = "PART_Announcer";
    public const string CompactPseudoClass = ":density-compact";
    public const string ComfortablePseudoClass = ":density-comfortable";
    public const string FilteringPseudoClass = ":filtering";
    public const string CheckedRowsPseudoClass = ":checked-rows";
    public const string EditingPseudoClass = ":editing";
    public const string GroupedPseudoClass = ":grouped";
    public const string ScrolledXPseudoClass = ":scrolled-x";
    public const string LoadingPseudoClass = ":loading";
    public const string EmptyPseudoClass = ":empty";
    public const string ReadOnlyPseudoClass = ":read-only";
    public const string ToolbarCompactPseudoClass = ":toolbar-compact";
    public const string ToolbarMinimalPseudoClass = ":toolbar-minimal";
    public const string StatusWrapPseudoClass = ":status-wrap";
    public const string FillPseudoClass = ":fill";

    private static readonly IReadOnlyList<DataGridSort> s_noSort = Array.Empty<DataGridSort>();
    private static readonly IReadOnlyDictionary<string, DataGridFilter> s_noFilters = new Dictionary<string, DataGridFilter>();
    private static readonly IReadOnlyList<object> s_noKeys = Array.Empty<object>();
    private static readonly IReadOnlyList<string> s_noStrings = Array.Empty<string>();

    #region Public properties

    /// <summary>Row items: <see cref="DataGridRow"/> objects or any objects the columns can read. <see cref="INotifyCollectionChanged"/> sources are followed.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<DataGrid, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Stable row identity; defaults to <see cref="DataGridRow.Key"/>, else the item itself. Required with <see cref="DataSource"/>.</summary>
    public static readonly StyledProperty<Func<object, object>?> RowKeySelectorProperty =
        AvaloniaProperty.Register<DataGrid, Func<object, object>?>(nameof(RowKeySelector));

    /// <summary>Accessible name of the grid; also names exported files and the xlsx worksheet.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<DataGrid, string>(nameof(Label), string.Empty);

    /// <summary>Tallest the scrolling area grows before it scrolls, in px (default 400); infinity fills the available height.</summary>
    public static readonly StyledProperty<double> MaxViewportHeightProperty =
        AvaloniaProperty.Register<DataGrid, double>(nameof(MaxViewportHeight), 400);

    /// <summary>Sort keys in priority order (two-way). Header clicks, Shift-clicks and the column menu change it.</summary>
    public static readonly StyledProperty<IReadOnlyList<DataGridSort>> SortDescriptionsProperty =
        AvaloniaProperty.Register<DataGrid, IReadOnlyList<DataGridSort>>(nameof(SortDescriptions), s_noSort, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Column filters by column key (two-way). The filter row opens when any filter is set at start.</summary>
    public static readonly StyledProperty<IReadOnlyDictionary<string, DataGridFilter>> FiltersProperty =
        AvaloniaProperty.Register<DataGrid, IReadOnlyDictionary<string, DataGridFilter>>(nameof(Filters), s_noFilters, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Quick search text (two-way): every word must appear in some visible column.</summary>
    public static readonly StyledProperty<string> SearchTextProperty =
        AvaloniaProperty.Register<DataGrid, string>(nameof(SearchText), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Keys of the checked rows, in data order (two-way).</summary>
    public static readonly StyledProperty<IReadOnlyList<object>> CheckedRowKeysProperty =
        AvaloniaProperty.Register<DataGrid, IReadOnlyList<object>>(nameof(CheckedRowKeys), s_noKeys, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Keys of the hidden columns (two-way); null uses each column's <see cref="DataGridColumn.IsHidden"/>.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> HiddenColumnsProperty =
        AvaloniaProperty.Register<DataGrid, IReadOnlyList<string>?>(nameof(HiddenColumns), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Keys of the pinned columns (two-way); null uses <see cref="DataGridColumn.IsPinned"/>, or the first column when no column sets it.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> PinnedColumnsProperty =
        AvaloniaProperty.Register<DataGrid, IReadOnlyList<string>?>(nameof(PinnedColumns), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Row density (two-way): Compact 32px, Standard 40px or Comfortable 48px rows.</summary>
    public static readonly StyledProperty<DataGridDensity> DensityProperty =
        AvaloniaProperty.Register<DataGrid, DataGridDensity>(nameof(Density), DataGridDensity.Standard, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Group-by column keys, outermost first (two-way). Group rows show a toggle, the value, a count and subtotals.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> GroupByProperty =
        AvaloniaProperty.Register<DataGrid, IReadOnlyList<string>>(nameof(GroupBy), s_noStrings, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the filter row is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsFilterRowOpenProperty =
        AvaloniaProperty.Register<DataGrid, bool>(nameof(IsFilterRowOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Shows the row gutter with row numbers and checkboxes for checking rows.</summary>
    public static readonly StyledProperty<bool> IsRowCheckEnabledProperty =
        AvaloniaProperty.Register<DataGrid, bool>(nameof(IsRowCheckEnabled), true);

    /// <summary>Shows the row number in the gutter. When false the gutter narrows to 40px and its checkboxes always show.</summary>
    public static readonly StyledProperty<bool> ShowRowNumbersProperty =
        AvaloniaProperty.Register<DataGrid, bool>(nameof(ShowRowNumbers), true);

    /// <summary>Shows the selection address and the Sum, Average and Count of the selected cells in the status bar.</summary>
    public static readonly StyledProperty<bool> ShowSelectionStatsProperty =
        AvaloniaProperty.Register<DataGrid, bool>(nameof(ShowSelectionStats), true);

    /// <summary>Shows the toolbar with search, undo and redo, filters, columns, density and export.</summary>
    public static readonly StyledProperty<bool> ShowToolbarProperty =
        AvaloniaProperty.Register<DataGrid, bool>(nameof(ShowToolbar), true);

    /// <summary>Shows the sticky totals row; it covers every row the filters keep, collapsed groups included.</summary>
    public static readonly StyledProperty<bool> ShowTotalsProperty =
        AvaloniaProperty.Register<DataGrid, bool>(nameof(ShowTotals), true);

    /// <summary>Formats Export offers; with more than one, Export opens a menu.</summary>
    public static readonly StyledProperty<DataGridExportFormats> ExportFormatsProperty =
        AvaloniaProperty.Register<DataGrid, DataGridExportFormats>(nameof(ExportFormats), DataGridExportFormats.Csv);

    /// <summary>Export file name without the extension; null derives one from <see cref="Label"/>.</summary>
    public static readonly StyledProperty<string?> ExportFileNameProperty =
        AvaloniaProperty.Register<DataGrid, string?>(nameof(ExportFileName));

    /// <summary>Shows Delete for checked rows; deleting can be undone.</summary>
    public static readonly StyledProperty<bool> CanDeleteRowsProperty =
        AvaloniaProperty.Register<DataGrid, bool>(nameof(CanDeleteRows), true);

    /// <summary>
    /// Loads rows from a server one block at a time as people scroll, instead of <see cref="ItemsSource"/>. The grid is
    /// then read only, does not group, hides Select all and exports only checked rows.
    /// </summary>
    public static readonly StyledProperty<IDataGridDataSource?> DataSourceProperty =
        AvaloniaProperty.Register<DataGrid, IDataGridDataSource?>(nameof(DataSource));

    /// <summary>Shows six placeholder rows while data loads.</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<DataGrid, bool>(nameof(IsLoading));

    /// <summary>Shown when there are no rows at all; null uses the built-in "No rows yet".</summary>
    public static readonly StyledProperty<string?> EmptyMessageProperty =
        AvaloniaProperty.Register<DataGrid, string?>(nameof(EmptyMessage));

    /// <summary>Culture for formatting and parsing values; null follows the AtomUI language.</summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<DataGrid, CultureInfo?>(nameof(Culture));

    /// <summary>Steps kept for undo (default 100; 0 turns undo off).</summary>
    public static readonly StyledProperty<int> UndoLimitProperty =
        AvaloniaProperty.Register<DataGrid, int>(nameof(UndoLimit), 100, coerce: (_, v) => Math.Max(0, v));

    /// <summary>Whether there is a step to undo.</summary>
    public static readonly DirectProperty<DataGrid, bool> CanUndoProperty =
        AvaloniaProperty.RegisterDirect<DataGrid, bool>(nameof(CanUndo), o => o.CanUndo);

    /// <summary>Whether there is a step to redo.</summary>
    public static readonly DirectProperty<DataGrid, bool> CanRedoProperty =
        AvaloniaProperty.RegisterDirect<DataGrid, bool>(nameof(CanRedo), o => o.CanRedo);

    /// <summary>The active cell (display row and visible column), or null when the grid has no cells.</summary>
    public static readonly DirectProperty<DataGrid, DataGridCellAddress?> ActiveCellProperty =
        AvaloniaProperty.RegisterDirect<DataGrid, DataGridCellAddress?>(nameof(ActiveCell), o => o.ActiveCell);

    /// <summary>The selected range, or null when the grid has no cells.</summary>
    public static readonly DirectProperty<DataGrid, DataGridCellRange?> SelectedRangeProperty =
        AvaloniaProperty.RegisterDirect<DataGrid, DataGridCellRange?>(nameof(SelectedRange), o => o.SelectedRange);

    /// <summary>Number of rows the filters keep (the data source's row count with a <see cref="DataSource"/>).</summary>
    public static readonly DirectProperty<DataGrid, int> VisibleRowCountProperty =
        AvaloniaProperty.RegisterDirect<DataGrid, int>(nameof(VisibleRowCount), o => o.VisibleRowCount);

    /// <summary>Whether a cell editor is open.</summary>
    public static readonly DirectProperty<DataGrid, bool> IsEditingProperty =
        AvaloniaProperty.RegisterDirect<DataGrid, bool>(nameof(IsEditing), o => o.IsEditing);

    /// <summary>Raised before an edit is written; cancel it to keep the editor open with an error.</summary>
    public static readonly RoutedEvent<DataGridCellEditEndingEventArgs> CellEditEndingEvent =
        RoutedEvent.Register<DataGrid, DataGridCellEditEndingEventArgs>(nameof(CellEditEnding), RoutingStrategies.Bubble);

    /// <summary>Raised after an edit, paste, cut, clear, fill, fill down, undo or redo wrote cells.</summary>
    public static readonly RoutedEvent<DataGridCellsChangedEventArgs> CellsChangedEvent =
        RoutedEvent.Register<DataGrid, DataGridCellsChangedEventArgs>(nameof(CellsChanged), RoutingStrategies.Bubble);

    /// <summary>Raised after checked rows were deleted (removed from the source when it is a writable list).</summary>
    public static readonly RoutedEvent<DataGridRowsEventArgs> RowsDeletedEvent =
        RoutedEvent.Register<DataGrid, DataGridRowsEventArgs>(nameof(RowsDeleted), RoutingStrategies.Bubble);

    /// <summary>Raised after a delete was undone (rows put back into a writable source at their old indices).</summary>
    public static readonly RoutedEvent<DataGridRowsEventArgs> RowsRestoredEvent =
        RoutedEvent.Register<DataGrid, DataGridRowsEventArgs>(nameof(RowsRestored), RoutingStrategies.Bubble);

    /// <summary>Raised when a bulk action is clicked (its command runs too).</summary>
    public static readonly RoutedEvent<DataGridBulkActionEventArgs> BulkActionInvokedEvent =
        RoutedEvent.Register<DataGrid, DataGridBulkActionEventArgs>(nameof(BulkActionInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised before the save dialog of an export opens.</summary>
    public static readonly RoutedEvent<DataGridExportingEventArgs> ExportingEvent =
        RoutedEvent.Register<DataGrid, DataGridExportingEventArgs>(nameof(Exporting), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>Column definitions in definition order (the XAML content of the grid). Pinned columns are drawn first.</summary>
    [Content]
    public DataGridColumnCollection Columns { get; } = new();

    /// <summary>Extra toolbar actions for checked rows, shown between Export and Delete.</summary>
    public AvaloniaList<DataGridBulkAction> BulkActions { get; } = new();

    /// <inheritdoc cref="RowKeySelectorProperty"/>
    public Func<object, object>? RowKeySelector { get => GetValue(RowKeySelectorProperty); set => SetValue(RowKeySelectorProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="MaxViewportHeightProperty"/>
    public double MaxViewportHeight { get => GetValue(MaxViewportHeightProperty); set => SetValue(MaxViewportHeightProperty, value); }

    /// <inheritdoc cref="SortDescriptionsProperty"/>
    public IReadOnlyList<DataGridSort> SortDescriptions { get => GetValue(SortDescriptionsProperty); set => SetValue(SortDescriptionsProperty, value); }

    /// <inheritdoc cref="FiltersProperty"/>
    public IReadOnlyDictionary<string, DataGridFilter> Filters { get => GetValue(FiltersProperty); set => SetValue(FiltersProperty, value); }

    /// <inheritdoc cref="SearchTextProperty"/>
    public string SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }

    /// <inheritdoc cref="CheckedRowKeysProperty"/>
    public IReadOnlyList<object> CheckedRowKeys { get => GetValue(CheckedRowKeysProperty); set => SetValue(CheckedRowKeysProperty, value); }

    /// <inheritdoc cref="HiddenColumnsProperty"/>
    public IReadOnlyList<string>? HiddenColumns { get => GetValue(HiddenColumnsProperty); set => SetValue(HiddenColumnsProperty, value); }

    /// <inheritdoc cref="PinnedColumnsProperty"/>
    public IReadOnlyList<string>? PinnedColumns { get => GetValue(PinnedColumnsProperty); set => SetValue(PinnedColumnsProperty, value); }

    /// <inheritdoc cref="DensityProperty"/>
    public DataGridDensity Density { get => GetValue(DensityProperty); set => SetValue(DensityProperty, value); }

    /// <inheritdoc cref="GroupByProperty"/>
    public IReadOnlyList<string> GroupBy { get => GetValue(GroupByProperty); set => SetValue(GroupByProperty, value); }

    /// <inheritdoc cref="IsFilterRowOpenProperty"/>
    public bool IsFilterRowOpen { get => GetValue(IsFilterRowOpenProperty); set => SetValue(IsFilterRowOpenProperty, value); }

    /// <inheritdoc cref="IsRowCheckEnabledProperty"/>
    public bool IsRowCheckEnabled { get => GetValue(IsRowCheckEnabledProperty); set => SetValue(IsRowCheckEnabledProperty, value); }

    /// <inheritdoc cref="ShowRowNumbersProperty"/>
    public bool ShowRowNumbers { get => GetValue(ShowRowNumbersProperty); set => SetValue(ShowRowNumbersProperty, value); }

    /// <inheritdoc cref="ShowSelectionStatsProperty"/>
    public bool ShowSelectionStats { get => GetValue(ShowSelectionStatsProperty); set => SetValue(ShowSelectionStatsProperty, value); }

    /// <inheritdoc cref="ShowToolbarProperty"/>
    public bool ShowToolbar { get => GetValue(ShowToolbarProperty); set => SetValue(ShowToolbarProperty, value); }

    /// <inheritdoc cref="ShowTotalsProperty"/>
    public bool ShowTotals { get => GetValue(ShowTotalsProperty); set => SetValue(ShowTotalsProperty, value); }

    /// <inheritdoc cref="ExportFormatsProperty"/>
    public DataGridExportFormats ExportFormats { get => GetValue(ExportFormatsProperty); set => SetValue(ExportFormatsProperty, value); }

    /// <inheritdoc cref="ExportFileNameProperty"/>
    public string? ExportFileName { get => GetValue(ExportFileNameProperty); set => SetValue(ExportFileNameProperty, value); }

    /// <inheritdoc cref="CanDeleteRowsProperty"/>
    public bool CanDeleteRows { get => GetValue(CanDeleteRowsProperty); set => SetValue(CanDeleteRowsProperty, value); }

    /// <inheritdoc cref="DataSourceProperty"/>
    public IDataGridDataSource? DataSource { get => GetValue(DataSourceProperty); set => SetValue(DataSourceProperty, value); }

    /// <inheritdoc cref="IsLoadingProperty"/>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <inheritdoc cref="EmptyMessageProperty"/>
    public string? EmptyMessage { get => GetValue(EmptyMessageProperty); set => SetValue(EmptyMessageProperty, value); }

    /// <inheritdoc cref="CultureProperty"/>
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }

    /// <inheritdoc cref="UndoLimitProperty"/>
    public int UndoLimit { get => GetValue(UndoLimitProperty); set => SetValue(UndoLimitProperty, value); }

    private bool _canUndo;
    private bool _canRedo;
    private DataGridCellAddress? _activeCell;
    private DataGridCellRange? _selectedRange;
    private int _visibleRowCount;
    private bool _isEditing;

    /// <inheritdoc cref="CanUndoProperty"/>
    public bool CanUndo { get => _canUndo; private set => SetAndRaise(CanUndoProperty, ref _canUndo, value); }

    /// <inheritdoc cref="CanRedoProperty"/>
    public bool CanRedo { get => _canRedo; private set => SetAndRaise(CanRedoProperty, ref _canRedo, value); }

    /// <inheritdoc cref="ActiveCellProperty"/>
    public DataGridCellAddress? ActiveCell { get => _activeCell; private set => SetAndRaise(ActiveCellProperty, ref _activeCell, value); }

    /// <inheritdoc cref="SelectedRangeProperty"/>
    public DataGridCellRange? SelectedRange { get => _selectedRange; private set => SetAndRaise(SelectedRangeProperty, ref _selectedRange, value); }

    /// <inheritdoc cref="VisibleRowCountProperty"/>
    public int VisibleRowCount { get => _visibleRowCount; private set => SetAndRaise(VisibleRowCountProperty, ref _visibleRowCount, value); }

    /// <inheritdoc cref="IsEditingProperty"/>
    public bool IsEditing { get => _isEditing; private set => SetAndRaise(IsEditingProperty, ref _isEditing, value); }

    /// <inheritdoc cref="CellEditEndingEvent"/>
    public event EventHandler<DataGridCellEditEndingEventArgs>? CellEditEnding
    {
        add => AddHandler(CellEditEndingEvent, value);
        remove => RemoveHandler(CellEditEndingEvent, value);
    }

    /// <inheritdoc cref="CellsChangedEvent"/>
    public event EventHandler<DataGridCellsChangedEventArgs>? CellsChanged
    {
        add => AddHandler(CellsChangedEvent, value);
        remove => RemoveHandler(CellsChangedEvent, value);
    }

    /// <inheritdoc cref="RowsDeletedEvent"/>
    public event EventHandler<DataGridRowsEventArgs>? RowsDeleted
    {
        add => AddHandler(RowsDeletedEvent, value);
        remove => RemoveHandler(RowsDeletedEvent, value);
    }

    /// <inheritdoc cref="RowsRestoredEvent"/>
    public event EventHandler<DataGridRowsEventArgs>? RowsRestored
    {
        add => AddHandler(RowsRestoredEvent, value);
        remove => RemoveHandler(RowsRestoredEvent, value);
    }

    /// <inheritdoc cref="BulkActionInvokedEvent"/>
    public event EventHandler<DataGridBulkActionEventArgs>? BulkActionInvoked
    {
        add => AddHandler(BulkActionInvokedEvent, value);
        remove => RemoveHandler(BulkActionInvokedEvent, value);
    }

    /// <inheritdoc cref="ExportingEvent"/>
    public event EventHandler<DataGridExportingEventArgs>? Exporting
    {
        add => AddHandler(ExportingEvent, value);
        remove => RemoveHandler(ExportingEvent, value);
    }

    #endregion

    #region Theme-provided values

    internal static readonly StyledProperty<IBrush?> SurfaceRaisedBrushProperty =
        AvaloniaProperty.Register<DataGrid, IBrush?>(nameof(SurfaceRaisedBrush));
    internal static readonly StyledProperty<IBrush?> SurfaceMutedBrushProperty =
        AvaloniaProperty.Register<DataGrid, IBrush?>(nameof(SurfaceMutedBrush));
    internal static readonly StyledProperty<IBrush?> SubtleBorderBrushProperty =
        AvaloniaProperty.Register<DataGrid, IBrush?>(nameof(SubtleBorderBrush));
    internal static readonly StyledProperty<IBrush?> StrongBorderBrushProperty =
        AvaloniaProperty.Register<DataGrid, IBrush?>(nameof(StrongBorderBrush));
    internal static readonly StyledProperty<IBrush?> SecondaryForegroundProperty =
        AvaloniaProperty.Register<DataGrid, IBrush?>(nameof(SecondaryForeground));
    internal static readonly StyledProperty<IBrush?> MutedForegroundProperty =
        AvaloniaProperty.Register<DataGrid, IBrush?>(nameof(MutedForeground));
    internal static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<DataGrid, IBrush?>(nameof(AccentBrush));
    internal static readonly StyledProperty<IBrush?> ControlOnBrushProperty =
        AvaloniaProperty.Register<DataGrid, IBrush?>(nameof(ControlOnBrush));
    internal static readonly StyledProperty<IBrush?> ControlGlyphBrushProperty =
        AvaloniaProperty.Register<DataGrid, IBrush?>(nameof(ControlGlyphBrush));
    internal static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<DataGrid, IBrush?>(nameof(FocusRingBrush));
    internal static readonly StyledProperty<double> SmallFontSizeProperty =
        AvaloniaProperty.Register<DataGrid, double>(nameof(SmallFontSize), 12);
    internal static readonly StyledProperty<FontWeight> MediumWeightProperty =
        AvaloniaProperty.Register<DataGrid, FontWeight>(nameof(MediumWeight), FontWeight.Medium);
    internal static readonly StyledProperty<double> RowHeightCompactProperty =
        AvaloniaProperty.Register<DataGrid, double>(nameof(RowHeightCompact), 32);
    internal static readonly StyledProperty<double> RowHeightStandardProperty =
        AvaloniaProperty.Register<DataGrid, double>(nameof(RowHeightStandard), 40);
    internal static readonly StyledProperty<double> RowHeightComfortableProperty =
        AvaloniaProperty.Register<DataGrid, double>(nameof(RowHeightComfortable), 48);
    internal static readonly StyledProperty<double> HeaderRowHeightProperty =
        AvaloniaProperty.Register<DataGrid, double>(nameof(HeaderRowHeight), 40);
    internal static readonly StyledProperty<double> FilterRowHeightProperty =
        AvaloniaProperty.Register<DataGrid, double>(nameof(FilterRowHeight), 40);
    internal static readonly StyledProperty<double> TotalsRowHeightProperty =
        AvaloniaProperty.Register<DataGrid, double>(nameof(TotalsRowHeight), 40);
    internal static readonly StyledProperty<double> GutterWidthProperty =
        AvaloniaProperty.Register<DataGrid, double>(nameof(GutterWidth), 48);
    internal static readonly StyledProperty<double> GutterWidthNarrowProperty =
        AvaloniaProperty.Register<DataGrid, double>(nameof(GutterWidthNarrow), 40);
    internal static readonly StyledProperty<double> ColumnMaxWidthProperty =
        AvaloniaProperty.Register<DataGrid, double>(nameof(ColumnMaxWidth), 560);
    internal static readonly StyledProperty<double> ColumnResizeStepProperty =
        AvaloniaProperty.Register<DataGrid, double>(nameof(ColumnResizeStep), 16);
    internal static readonly StyledProperty<double> PinnedColumnMinWidthProperty =
        AvaloniaProperty.Register<DataGrid, double>(nameof(PinnedColumnMinWidth), 96);
    internal static readonly StyledProperty<double> CellInlinePaddingProperty =
        AvaloniaProperty.Register<DataGrid, double>(nameof(CellInlinePadding), 12);
    internal static readonly StyledProperty<double> CompactCellFontSizeProperty =
        AvaloniaProperty.Register<DataGrid, double>(nameof(CompactCellFontSize), 13);
    internal static readonly StyledProperty<double> SelectionFillOpacityProperty =
        AvaloniaProperty.Register<DataGrid, double>(nameof(SelectionFillOpacity), 0.09);
    internal static readonly StyledProperty<double> CheckedRowFillOpacityProperty =
        AvaloniaProperty.Register<DataGrid, double>(nameof(CheckedRowFillOpacity), 0.05);
    internal static readonly StyledProperty<double> WashOpacityProperty =
        AvaloniaProperty.Register<DataGrid, double>(nameof(WashOpacity), 0.2);
    internal static readonly StyledProperty<double> GroupIndentProperty =
        AvaloniaProperty.Register<DataGrid, double>(nameof(GroupIndent), 16);
    internal static readonly StyledProperty<TimeSpan> WashDurationProperty =
        AvaloniaProperty.Register<DataGrid, TimeSpan>(nameof(WashDuration), TimeSpan.FromMilliseconds(900));
    internal static readonly StyledProperty<TimeSpan> SortSlideWindowProperty =
        AvaloniaProperty.Register<DataGrid, TimeSpan>(nameof(SortSlideWindow), TimeSpan.FromMilliseconds(700));
    internal static readonly StyledProperty<TimeSpan> StatusMessageDurationProperty =
        AvaloniaProperty.Register<DataGrid, TimeSpan>(nameof(StatusMessageDuration), TimeSpan.FromMilliseconds(2400));
    internal static readonly StyledProperty<TimeSpan> StatusActionDurationProperty =
        AvaloniaProperty.Register<DataGrid, TimeSpan>(nameof(StatusActionDuration), TimeSpan.FromMilliseconds(6000));

    internal IBrush? SurfaceRaisedBrush { get => GetValue(SurfaceRaisedBrushProperty); set => SetValue(SurfaceRaisedBrushProperty, value); }
    internal IBrush? SurfaceMutedBrush { get => GetValue(SurfaceMutedBrushProperty); set => SetValue(SurfaceMutedBrushProperty, value); }
    internal IBrush? SubtleBorderBrush { get => GetValue(SubtleBorderBrushProperty); set => SetValue(SubtleBorderBrushProperty, value); }
    internal IBrush? StrongBorderBrush { get => GetValue(StrongBorderBrushProperty); set => SetValue(StrongBorderBrushProperty, value); }
    internal IBrush? SecondaryForeground { get => GetValue(SecondaryForegroundProperty); set => SetValue(SecondaryForegroundProperty, value); }
    internal IBrush? MutedForeground { get => GetValue(MutedForegroundProperty); set => SetValue(MutedForegroundProperty, value); }
    internal IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    internal IBrush? ControlOnBrush { get => GetValue(ControlOnBrushProperty); set => SetValue(ControlOnBrushProperty, value); }
    internal IBrush? ControlGlyphBrush { get => GetValue(ControlGlyphBrushProperty); set => SetValue(ControlGlyphBrushProperty, value); }
    internal IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }
    internal double SmallFontSize { get => GetValue(SmallFontSizeProperty); set => SetValue(SmallFontSizeProperty, value); }
    internal FontWeight MediumWeight { get => GetValue(MediumWeightProperty); set => SetValue(MediumWeightProperty, value); }
    internal double RowHeightCompact { get => GetValue(RowHeightCompactProperty); set => SetValue(RowHeightCompactProperty, value); }
    internal double RowHeightStandard { get => GetValue(RowHeightStandardProperty); set => SetValue(RowHeightStandardProperty, value); }
    internal double RowHeightComfortable { get => GetValue(RowHeightComfortableProperty); set => SetValue(RowHeightComfortableProperty, value); }
    internal double HeaderRowHeight { get => GetValue(HeaderRowHeightProperty); set => SetValue(HeaderRowHeightProperty, value); }
    internal double FilterRowHeight { get => GetValue(FilterRowHeightProperty); set => SetValue(FilterRowHeightProperty, value); }
    internal double TotalsRowHeight { get => GetValue(TotalsRowHeightProperty); set => SetValue(TotalsRowHeightProperty, value); }
    internal double GutterWidth { get => GetValue(GutterWidthProperty); set => SetValue(GutterWidthProperty, value); }
    internal double GutterWidthNarrow { get => GetValue(GutterWidthNarrowProperty); set => SetValue(GutterWidthNarrowProperty, value); }
    internal double ColumnMaxWidth { get => GetValue(ColumnMaxWidthProperty); set => SetValue(ColumnMaxWidthProperty, value); }
    internal double ColumnResizeStep { get => GetValue(ColumnResizeStepProperty); set => SetValue(ColumnResizeStepProperty, value); }
    internal double PinnedColumnMinWidth { get => GetValue(PinnedColumnMinWidthProperty); set => SetValue(PinnedColumnMinWidthProperty, value); }
    internal double CellInlinePadding { get => GetValue(CellInlinePaddingProperty); set => SetValue(CellInlinePaddingProperty, value); }
    internal double CompactCellFontSize { get => GetValue(CompactCellFontSizeProperty); set => SetValue(CompactCellFontSizeProperty, value); }
    internal double SelectionFillOpacity { get => GetValue(SelectionFillOpacityProperty); set => SetValue(SelectionFillOpacityProperty, value); }
    internal double CheckedRowFillOpacity { get => GetValue(CheckedRowFillOpacityProperty); set => SetValue(CheckedRowFillOpacityProperty, value); }
    internal double WashOpacity { get => GetValue(WashOpacityProperty); set => SetValue(WashOpacityProperty, value); }
    internal double GroupIndent { get => GetValue(GroupIndentProperty); set => SetValue(GroupIndentProperty, value); }
    internal TimeSpan WashDuration { get => GetValue(WashDurationProperty); set => SetValue(WashDurationProperty, value); }
    internal TimeSpan SortSlideWindow { get => GetValue(SortSlideWindowProperty); set => SetValue(SortSlideWindowProperty, value); }
    internal TimeSpan StatusMessageDuration { get => GetValue(StatusMessageDurationProperty); set => SetValue(StatusMessageDurationProperty, value); }
    internal TimeSpan StatusActionDuration { get => GetValue(StatusActionDurationProperty); set => SetValue(StatusActionDurationProperty, value); }

    #endregion

    private readonly DataGridStore _store = new();
    private ScrollViewer? _scroll;
    private DataGridToolbar? _toolbar;
    private DataGridStatusBar? _statusBar;
    private TextBlock? _announcer;
    private ILanguageManager? _languages;

    internal DataGridPalette Palette { get; } = new();

    internal DataGridViewport Viewport { get; }

    internal DataGridStore Store => _store;

    internal DataGridToolbar? Toolbar => _toolbar;

    internal DataGridStatusBar? StatusBar => _statusBar;

    static DataGrid()
    {
        FocusableProperty.OverrideDefaultValue<DataGrid>(false);
    }

    public DataGrid()
    {
        Viewport = new DataGridViewport(this);
        Columns.CollectionChanged += OnColumnsChanged;
        BulkActions.CollectionChanged += (_, _) => _toolbar?.RebuildBulkActions();
        _store.Reset += OnStoreReset;
        _store.RowChanged += OnStoreRowChanged;
    }

    /// <summary>The effective formatting culture: <see cref="Culture"/>, else the AtomUI language's formatting culture.</summary>
    internal CultureInfo EffectiveCulture => Culture ?? DataGridText.FormattingCulture;

    internal bool HasDataSource => DataSource is not null;

    internal double RowHeight => Density switch
    {
        DataGridDensity.Compact => RowHeightCompact,
        DataGridDensity.Comfortable => RowHeightComfortable,
        _ => RowHeightStandard
    };

    internal double CellFontSize => Density == DataGridDensity.Compact ? CompactCellFontSize : FontSize;

    /// <summary>Width of the row gutter (0 without one).</summary>
    internal double EffectiveGutterWidth => !IsRowCheckEnabled ? 0 : ShowRowNumbers ? GutterWidth : GutterWidthNarrow;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_scroll is not null)
        {
            _scroll.Content = null;
            _scroll.ScrollChanged -= OnScrollChanged;
        }
        _toolbar?.Detach();
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollViewerPart);
        _toolbar = e.NameScope.Find<DataGridToolbar>(ToolbarPart);
        _statusBar = e.NameScope.Find<DataGridStatusBar>(StatusBarPart);
        _announcer = e.NameScope.Find<TextBlock>(AnnouncerPart);
        if (_scroll is not null)
        {
            _scroll.Content = Viewport;
            _scroll.ScrollChanged += OnScrollChanged;
        }
        _toolbar?.Attach(this);
        _statusBar?.Attach(this);
        UpdatePalette();
        UpdateTexts();
        UpdatePseudoClasses();
    }

    private bool _detached;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_detached)
        {
            // The source may have changed while the grid was away; follow it again and re-read it.
            _detached = false;
            _store.Attach();
        }
        _languages = DataGridText.Languages;
        if (_languages is not null)
        {
            _languages.LanguageChanged += OnLanguageChanged;
        }
        // Whether a save dialog exists depends on the top level.
        _toolbar?.UpdateState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_languages is not null)
        {
            _languages.LanguageChanged -= OnLanguageChanged;
            _languages = null;
        }
        CancelBlockRequests();
        // Stop following the source and its items so a long-lived collection does not keep the grid alive.
        _store.Detach();
        _detached = true;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AutomationProperties.SetName(this, AccessibleName);
        UpdatePseudoClasses();
    }

    private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            OnCultureChanged();
        }
        else
        {
            Dispatcher.UIThread.Post(OnCultureChanged);
        }
    }

    private void OnCultureChanged()
    {
        InvalidateOptions();
        InvalidateView();
        Viewport.InvalidateText();
        UpdateTexts();
        AutomationProperties.SetName(this, AccessibleName);
    }

    internal string AccessibleName => string.IsNullOrWhiteSpace(Label) ? DataGridText.Get(DataGridLangResourceKind.GridName) : Label;

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => SetScrolledX(Viewport.Offset.X > 0.5);

    internal void SetScrolledX(bool scrolled) => PseudoClasses.Set(ScrolledXPseudoClass, scrolled);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ItemsSourceProperty || property == RowKeySelectorProperty)
        {
            OnItemsSourceChanged();
        }
        else if (property == SortDescriptionsProperty)
        {
            OnSortChanged();
        }
        else if (property == FiltersProperty || property == SearchTextProperty)
        {
            InvalidateView();
            if (HasDataSource)
            {
                ResetBlocks();
            }
            UpdateToolbarState();
        }
        else if (property == GroupByProperty)
        {
            _collapsed.Clear();
            InvalidateView();
            UpdatePseudoClasses();
            UpdateToolbarState();
        }
        else if (property == HiddenColumnsProperty || property == PinnedColumnsProperty)
        {
            InvalidateColumns();
        }
        else if (property == CheckedRowKeysProperty)
        {
            OnCheckedRowKeysChanged();
        }
        else if (property == DensityProperty)
        {
            Viewport.OnDensityChanged();
            UpdatePseudoClasses();
            UpdateToolbarState();
        }
        else if (property == IsFilterRowOpenProperty)
        {
            Viewport.OnFilterRowToggled(IsLoaded);
            UpdatePseudoClasses();
            UpdateToolbarState();
        }
        else if (property == IsRowCheckEnabledProperty || property == ShowRowNumbersProperty)
        {
            InvalidateColumns();
        }
        else if (property == ShowTotalsProperty || property == MaxViewportHeightProperty)
        {
            Viewport.InvalidateMeasure();
            UpdatePseudoClasses();
        }
        else if (property == ShowToolbarProperty || property == ShowSelectionStatsProperty || property == ExportFormatsProperty ||
                 property == CanDeleteRowsProperty)
        {
            UpdateToolbarState();
            UpdateStatus();
        }
        else if (property == DataSourceProperty)
        {
            OnDataSourceChanged();
        }
        else if (property == IsLoadingProperty)
        {
            UpdatePseudoClasses();
            Viewport.InvalidateMeasure();
            Viewport.InvalidateLayers();
            Viewport.UpdateTotals();
            Viewport.UpdateEmptyState();
            PublishSelection();
        }
        else if (property == EmptyMessageProperty)
        {
            Viewport.UpdateEmptyState();
        }
        else if (property == CultureProperty)
        {
            OnCultureChanged();
        }
        else if (property == UndoLimitProperty)
        {
            _undo.Limit = UndoLimit;
            _undo.Trim();
            UpdateUndoState();
        }
        else if (property == LabelProperty)
        {
            AutomationProperties.SetName(this, AccessibleName);
            UpdateTexts();
        }
        else if (property == BoundsProperty)
        {
            UpdateResponsive();
        }
        else if (property == ForegroundProperty || property == BackgroundProperty || property == BorderBrushProperty ||
                 property == FontFamilyProperty || property == FontSizeProperty || property == SurfaceRaisedBrushProperty ||
                 property == SurfaceMutedBrushProperty || property == SubtleBorderBrushProperty || property == StrongBorderBrushProperty ||
                 property == SecondaryForegroundProperty || property == MutedForegroundProperty || property == AccentBrushProperty ||
                 property == ControlOnBrushProperty || property == ControlGlyphBrushProperty || property == FocusRingBrushProperty ||
                 property == SmallFontSizeProperty || property == MediumWeightProperty || property == SelectionFillOpacityProperty ||
                 property == CheckedRowFillOpacityProperty || property == WashOpacityProperty || property == CompactCellFontSizeProperty)
        {
            UpdatePalette();
        }
        else if (property == RowHeightCompactProperty || property == RowHeightStandardProperty || property == RowHeightComfortableProperty ||
                 property == HeaderRowHeightProperty || property == FilterRowHeightProperty || property == TotalsRowHeightProperty ||
                 property == GutterWidthProperty || property == GutterWidthNarrowProperty || property == CellInlinePaddingProperty ||
                 property == PinnedColumnMinWidthProperty || property == GroupIndentProperty)
        {
            Viewport.InvalidateMeasure();
            Viewport.InvalidateText();
        }
    }

    private void UpdatePalette()
    {
        static Color ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;
        var surface = ColorOf(Background);
        Palette.Update(surface, ColorOf(SurfaceRaisedBrush), ColorOf(SurfaceMutedBrush), ColorOf(BorderBrush), ColorOf(SubtleBorderBrush),
            ColorOf(StrongBorderBrush), ColorOf(Foreground), ColorOf(SecondaryForeground), ColorOf(MutedForeground), ColorOf(AccentBrush),
            ColorOf(ControlOnBrush), ColorOf(ControlGlyphBrush), FocusRingBrush is ISolidColorBrush ring ? ring.Color : null,
            SelectionFillOpacity, CheckedRowFillOpacity, WashOpacity, IsDark(surface), FontFamily, FontSize, SmallFontSize, MediumWeight);
        Viewport.InvalidateText();
    }

    private static bool IsDark(Color color) => (color.R * 299 + color.G * 587 + color.B * 114) / 1000 < 128 && color.A > 0;

    internal void UpdatePseudoClasses()
    {
        PseudoClasses.Set(CompactPseudoClass, Density == DataGridDensity.Compact);
        PseudoClasses.Set(ComfortablePseudoClass, Density == DataGridDensity.Comfortable);
        PseudoClasses.Set(FilteringPseudoClass, IsFilterRowOpen);
        PseudoClasses.Set(CheckedRowsPseudoClass, _checked.Count > 0);
        PseudoClasses.Set(EditingPseudoClass, IsEditing);
        PseudoClasses.Set(GroupedPseudoClass, View.IsGrouped);
        PseudoClasses.Set(LoadingPseudoClass, IsShowingSkeleton);
        PseudoClasses.Set(EmptyPseudoClass, !IsShowingSkeleton && DisplayCount == 0);
        PseudoClasses.Set(ReadOnlyPseudoClass, HasDataSource);
        PseudoClasses.Set(FillPseudoClass, double.IsPositiveInfinity(MaxViewportHeight));
    }

    private void UpdateResponsive()
    {
        var width = Bounds.Width;
        var compact = width > 0 && width < 640;
        var minimal = width > 0 && width < 460;
        var wrap = width > 0 && width < 480;
        if (PseudoClasses.Contains(ToolbarCompactPseudoClass) != compact || PseudoClasses.Contains(ToolbarMinimalPseudoClass) != minimal ||
            PseudoClasses.Contains(StatusWrapPseudoClass) != wrap)
        {
            PseudoClasses.Set(ToolbarCompactPseudoClass, compact);
            PseudoClasses.Set(ToolbarMinimalPseudoClass, minimal);
            PseudoClasses.Set(StatusWrapPseudoClass, wrap);
            _toolbar?.SetResponsive(compact, minimal);
            _statusBar?.SetWrap(wrap);
        }
    }

    /// <summary>Writes a status message and announces it.</summary>
    internal void Say(string text, string? actionLabel = null, Action? action = null)
    {
        _statusBar?.ShowMessage(text, actionLabel, action,
            actionLabel is null ? StatusMessageDuration : StatusActionDuration);
        Announce(text);
    }

    /// <summary>Puts a message in the polite live region.</summary>
    internal void Announce(string text)
    {
        if (_announcer is null)
        {
            return;
        }
        _announcer.Text = text;
        AutomationProperties.SetName(_announcer, text);
    }

    internal void UpdateTexts()
    {
        _toolbar?.UpdateTexts();
        _statusBar?.UpdateTexts();
        Viewport.UpdateTexts();
    }

    protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new DataGridAutomationPeer(this);
}
