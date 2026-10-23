using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A table field of the document extraction form (spec 3.4): a header line 30 high (the group dot, the label 12 / 500
/// TextSecondary, the row count 12 TextMuted, and on the right the confidence of the cells), then the grid in a 1px Border
/// of radius 10 on Surface that scrolls sideways below its minimum width of 304: column headers 28 high (12 TextMuted,
/// padding 0 9, a BorderSubtle line under them), data rows 32 high (33 with the line above from the second row) of editable
/// cells, or the empty text (12 TextMuted, padding 10 9). The whole field sits in a box of radius 12 with padding 4 4 6 8.
/// </summary>
/// <remarks>
/// <para>
/// Columns: the first takes what the others leave; numbers are right-aligned with tabular figures. Widths by type: Integer
/// and Number 40, Money 78 (80 for the last column), Date 92, Currency 56; text columns after the first share the rest;
/// <see cref="IsNarrow"/> takes 2 off every fixed column (the invoice's 1fr / 40 / 78 / 80 become 126 / 38 / 76 / 78 at
/// the 358px form).
/// </para>
/// <para>
/// <see cref="Rows"/> is the owner's list: it adds a row (every cell already an <see cref="ExtractionValue"/>) when its
/// first value arrives and updates the values as events come in; the grid follows. A row added while the table is shown
/// opens on the smooth spring with a 160ms fade (reduced motion and Off: at once). Cells behave like the field row's box
/// (landing, low confidence, invalid, the person's typing written to the value and raised as <see cref="ValueEdited"/>);
/// a low-confidence cell carries the model's note as its tooltip; the owner's "Looks right" for a cell goes through
/// <see cref="ConfirmCell"/>.
/// </para>
/// <para>
/// <see cref="LinkedPath"/> lights the table: the table's key or anything in it tints the whole field at 6%, a row path
/// (<c>key.row</c>) the row at 12%, a cell path (<c>key.row.column</c>) the cell at 12%. Pointer movement over the field
/// raises <see cref="PathEntered"/> / <see cref="PathExited"/> with the cell's path, or the table's key between cells
/// (the same routed events as <see cref="ExtractionFieldRow"/>).
/// </para>
/// <para>
/// Automation: the grid is a table (named by the label), the header line a header of header items, each data row a data
/// item, each cell's box an edit named by <see cref="CellAutomationName"/> (column, row number from 1). Avalonia has no
/// cell role, so the box itself stands for the cell.
/// </para>
/// </remarks>
[TemplatePart(DotPart, typeof(Ellipse))]
[TemplatePart(SizePart, typeof(SpringSizeDecorator))]
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(BodyPart, typeof(StackPanel))]
[TemplatePart(HeaderRowPart, typeof(ExtractionTableRowPanel))]
[TemplatePart(RowsPart, typeof(StackPanel))]
[PseudoClasses(NarrowPseudoClass, RunningPseudoClass, EmptyPseudoClass, LinkedPseudoClass)]
internal sealed class ExtractionTableGrid : TemplatedControl
{
    public const string DotPart = "PART_Dot";
    public const string SizePart = "PART_Size";
    public const string ScrollPart = "PART_Scroll";
    public const string BodyPart = "PART_Body";
    public const string HeaderRowPart = "PART_HeaderRow";
    public const string RowsPart = "PART_Rows";

    public const string NarrowPseudoClass = ":narrow";
    public const string RunningPseudoClass = ":running";
    public const string EmptyPseudoClass = ":empty";
    public const string LinkedPseudoClass = ":linked";

    /// <summary>Below this width the grid scrolls sideways.</summary>
    public const double MinTableWidth = 304;

    /// <summary>The field box's padding left and right (8 + 4) plus the grid's border (1 + 1).</summary>
    private const double HorizontalChrome = 8 + 4 + 2;

    #region Properties

    /// <summary>The table field's label ("Line items").</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ExtractionTableGrid, string?>(nameof(Label));

    /// <summary>The group's tone, 1 to 4 (<see cref="PromptTone"/>): the dot and the tints.</summary>
    public static readonly StyledProperty<int> ToneProperty =
        AvaloniaProperty.Register<ExtractionTableGrid, int>(nameof(Tone), 1);

    /// <summary>The columns (fixed for the grid's life; a new list rebuilds it).</summary>
    public static readonly StyledProperty<IReadOnlyList<ExtractionColumn>?> ColumnsProperty =
        AvaloniaProperty.Register<ExtractionTableGrid, IReadOnlyList<ExtractionColumn>?>(nameof(Columns));

    /// <summary>The rows, each a list of the cells' values in column order (the owner's; observed).</summary>
    public static readonly StyledProperty<AvaloniaList<AvaloniaList<ExtractionValue>>?> RowsProperty =
        AvaloniaProperty.Register<ExtractionTableGrid, AvaloniaList<AvaloniaList<ExtractionValue>>?>(nameof(Rows));

    /// <summary>The confidence of the table on the header line (the owner keeps it as the mean of the cells); null shows none.</summary>
    public static readonly StyledProperty<ExtractionValue?> SummaryValueProperty =
        AvaloniaProperty.Register<ExtractionTableGrid, ExtractionValue?>(nameof(SummaryValue));

    /// <summary>What screen readers hear for the header's confidence.</summary>
    public static readonly StyledProperty<string?> SummaryAutomationTextProperty =
        AvaloniaProperty.Register<ExtractionTableGrid, string?>(nameof(SummaryAutomationText));

    /// <summary>The word of an edited summary ("Edited").</summary>
    public static readonly StyledProperty<string?> EditedTextProperty =
        AvaloniaProperty.Register<ExtractionTableGrid, string?>(nameof(EditedText));

    /// <summary>Whether Accept all has reached the table (the header's confidence shows the check).</summary>
    public static readonly StyledProperty<bool> IsAcceptedProperty =
        AvaloniaProperty.Register<ExtractionTableGrid, bool>(nameof(IsAccepted));

    /// <summary>The row count next to the label once there are rows ("4 rows").</summary>
    public static readonly StyledProperty<string?> RowsTextProperty =
        AvaloniaProperty.Register<ExtractionTableGrid, string?>(nameof(RowsText));

    /// <summary>The caption next to the label before there are rows ("Table").</summary>
    public static readonly StyledProperty<string?> TableTextProperty =
        AvaloniaProperty.Register<ExtractionTableGrid, string?>(nameof(TableText));

    /// <summary>The text of a table without rows ("No rows yet").</summary>
    public static readonly StyledProperty<string?> EmptyTextProperty =
        AvaloniaProperty.Register<ExtractionTableGrid, string?>(nameof(EmptyText));

    /// <summary>The text of a table without rows while a run goes ("Rows appear here as they are found").</summary>
    public static readonly StyledProperty<string?> PendingTextProperty =
        AvaloniaProperty.Register<ExtractionTableGrid, string?>(nameof(PendingText));

    /// <summary>Whether a run is going (the empty text becomes <see cref="PendingText"/>).</summary>
    public static readonly StyledProperty<bool> IsRunningProperty =
        AvaloniaProperty.Register<ExtractionTableGrid, bool>(nameof(IsRunning));

    /// <summary>The narrow layout (fixed columns 2 narrower, the confidence column 54).</summary>
    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<ExtractionTableGrid, bool>(nameof(IsNarrow));

    /// <summary>The path that is lit: the table's key, a row (<c>key.row</c>) or a cell (<c>key.row.column</c>); null for none.</summary>
    public static readonly StyledProperty<string?> LinkedPathProperty =
        AvaloniaProperty.Register<ExtractionTableGrid, string?>(nameof(LinkedPath));

    /// <summary>The table field's key (the first part of its paths); null takes it from the first value.</summary>
    public static readonly StyledProperty<string?> FieldKeyProperty =
        AvaloniaProperty.Register<ExtractionTableGrid, string?>(nameof(FieldKey));

    /// <summary>A cell's accessible name from its column's label and its row number (from 1).</summary>
    public static readonly StyledProperty<Func<string, int, string>?> CellAutomationNameProperty =
        AvaloniaProperty.Register<ExtractionTableGrid, Func<string, int, string>?>(nameof(CellAutomationName));

    /// <summary>The caption next to the label as shown (<see cref="RowsText"/> or <see cref="TableText"/>; read-only).</summary>
    public static readonly DirectProperty<ExtractionTableGrid, string?> CountTextProperty =
        AvaloniaProperty.RegisterDirect<ExtractionTableGrid, string?>(nameof(CountText), o => o.CountText);

    /// <summary>The text under the column headers while there are no rows (read-only).</summary>
    public static readonly DirectProperty<ExtractionTableGrid, string?> ShownEmptyTextProperty =
        AvaloniaProperty.RegisterDirect<ExtractionTableGrid, string?>(nameof(ShownEmptyText), o => o.ShownEmptyText);

    #endregion

    #region Events (the field row's)

    /// <inheritdoc cref="ExtractionFieldRow.PathEnteredEvent"/>
    public static readonly RoutedEvent<ExtractionPathEventArgs> PathEnteredEvent = ExtractionFieldRow.PathEnteredEvent;

    /// <inheritdoc cref="ExtractionFieldRow.PathExitedEvent"/>
    public static readonly RoutedEvent<ExtractionPathEventArgs> PathExitedEvent = ExtractionFieldRow.PathExitedEvent;

    /// <inheritdoc cref="ExtractionFieldRow.PathFocusedEvent"/>
    public static readonly RoutedEvent<ExtractionPathEventArgs> PathFocusedEvent = ExtractionFieldRow.PathFocusedEvent;

    /// <inheritdoc cref="ExtractionFieldRow.ValueEditedEvent"/>
    public static readonly RoutedEvent<ExtractionValueChangedEventArgs> ValueEditedEvent = ExtractionFieldRow.ValueEditedEvent;

    /// <inheritdoc cref="ExtractionFieldRow.LooksRightRequestedEvent"/>
    public static readonly RoutedEvent<ExtractionPathEventArgs> LooksRightRequestedEvent = ExtractionFieldRow.LooksRightRequestedEvent;

    /// <summary>Raised when the pointer enters a cell (its path) or the field between cells (the table's key).</summary>
    public event EventHandler<ExtractionPathEventArgs>? PathEntered { add => AddHandler(PathEnteredEvent, value); remove => RemoveHandler(PathEnteredEvent, value); }

    /// <summary>Raised when the pointer leaves what it entered.</summary>
    public event EventHandler<ExtractionPathEventArgs>? PathExited { add => AddHandler(PathExitedEvent, value); remove => RemoveHandler(PathExitedEvent, value); }

    /// <summary>Raised when a cell's box gets the focus.</summary>
    public event EventHandler<ExtractionPathEventArgs>? PathFocused { add => AddHandler(PathFocusedEvent, value); remove => RemoveHandler(PathFocusedEvent, value); }

    /// <summary>Raised after every edit the person makes in a cell.</summary>
    public event EventHandler<ExtractionValueChangedEventArgs>? ValueEdited { add => AddHandler(ValueEditedEvent, value); remove => RemoveHandler(ValueEditedEvent, value); }

    /// <summary>Raised by <see cref="ConfirmCell"/>.</summary>
    public event EventHandler<ExtractionPathEventArgs>? LooksRightRequested { add => AddHandler(LooksRightRequestedEvent, value); remove => RemoveHandler(LooksRightRequestedEvent, value); }

    #endregion

    private readonly List<ExtractionTableRow> _rows = new();
    private readonly Dictionary<string, ExtractionTableCell> _cellsByPath = new(StringComparer.Ordinal);
    private ExtractionPalette? _palette;
    private Ellipse? _dot;
    private SpringSizeDecorator? _size;
    private ScrollViewer? _scroll;
    private StackPanel? _body;
    private ExtractionTableRowPanel? _headerRow;
    private StackPanel? _rowsPanel;
    private AvaloniaList<AvaloniaList<ExtractionValue>>? _observedRows;
    private readonly List<AvaloniaList<ExtractionValue>> _observedCells = new();
    private double[] _widths = [];
    private bool[] _numeric = [];
    private string? _countText;
    private string? _shownEmptyText;
    private string? _hoverPath;
    private bool _attached;
    private ExtractionTableRow? _litRow;
    private ExtractionTableCell? _litCell;

    static ExtractionTableGrid()
    {
        FocusableProperty.OverrideDefaultValue<ExtractionTableGrid>(false);
    }

    public ExtractionTableGrid()
    {
        ExtractionPalette.BindProbes(this);
    }

    #region CLR properties

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="ToneProperty"/>
    public int Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <inheritdoc cref="ColumnsProperty"/>
    public IReadOnlyList<ExtractionColumn>? Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    /// <inheritdoc cref="RowsProperty"/>
    public AvaloniaList<AvaloniaList<ExtractionValue>>? Rows { get => GetValue(RowsProperty); set => SetValue(RowsProperty, value); }

    /// <inheritdoc cref="SummaryValueProperty"/>
    public ExtractionValue? SummaryValue { get => GetValue(SummaryValueProperty); set => SetValue(SummaryValueProperty, value); }

    /// <inheritdoc cref="SummaryAutomationTextProperty"/>
    public string? SummaryAutomationText { get => GetValue(SummaryAutomationTextProperty); set => SetValue(SummaryAutomationTextProperty, value); }

    /// <inheritdoc cref="EditedTextProperty"/>
    public string? EditedText { get => GetValue(EditedTextProperty); set => SetValue(EditedTextProperty, value); }

    /// <inheritdoc cref="IsAcceptedProperty"/>
    public bool IsAccepted { get => GetValue(IsAcceptedProperty); set => SetValue(IsAcceptedProperty, value); }

    /// <inheritdoc cref="RowsTextProperty"/>
    public string? RowsText { get => GetValue(RowsTextProperty); set => SetValue(RowsTextProperty, value); }

    /// <inheritdoc cref="TableTextProperty"/>
    public string? TableText { get => GetValue(TableTextProperty); set => SetValue(TableTextProperty, value); }

    /// <inheritdoc cref="EmptyTextProperty"/>
    public string? EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    /// <inheritdoc cref="PendingTextProperty"/>
    public string? PendingText { get => GetValue(PendingTextProperty); set => SetValue(PendingTextProperty, value); }

    /// <inheritdoc cref="IsRunningProperty"/>
    public bool IsRunning { get => GetValue(IsRunningProperty); set => SetValue(IsRunningProperty, value); }

    /// <inheritdoc cref="IsNarrowProperty"/>
    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    /// <inheritdoc cref="LinkedPathProperty"/>
    public string? LinkedPath { get => GetValue(LinkedPathProperty); set => SetValue(LinkedPathProperty, value); }

    /// <inheritdoc cref="FieldKeyProperty"/>
    public string? FieldKey { get => GetValue(FieldKeyProperty); set => SetValue(FieldKeyProperty, value); }

    /// <inheritdoc cref="CellAutomationNameProperty"/>
    public Func<string, int, string>? CellAutomationName { get => GetValue(CellAutomationNameProperty); set => SetValue(CellAutomationNameProperty, value); }

    /// <inheritdoc cref="CountTextProperty"/>
    public string? CountText { get => _countText; private set => SetAndRaise(CountTextProperty, ref _countText, value); }

    /// <inheritdoc cref="ShownEmptyTextProperty"/>
    public string? ShownEmptyText { get => _shownEmptyText; private set => SetAndRaise(ShownEmptyTextProperty, ref _shownEmptyText, value); }

    #endregion

    /// <summary>The data rows shown, in order.</summary>
    internal IReadOnlyList<ExtractionTableRow> RowViews => _rows;

    /// <summary>The height spring around the grid (template part).</summary>
    internal SpringSizeDecorator? SizeDecorator => _size;

    /// <summary>The sideways scroller (template part).</summary>
    internal ScrollViewer? Scroller => _scroll;

    /// <summary>The column headers' line (template part).</summary>
    internal ExtractionTableRowPanel? HeaderRow => _headerRow;

    /// <summary>The colors in use.</summary>
    internal ExtractionPalette? Palette => _palette;

    /// <summary>The table's key: <see cref="FieldKey"/>, else the first part of the first value's path.</summary>
    public string? Key
    {
        get
        {
            if (!string.IsNullOrEmpty(FieldKey))
            {
                return FieldKey;
            }
            foreach (var row in _rows)
            {
                foreach (var cell in row.Cells)
                {
                    if (cell.Value?.Path is { } path)
                    {
                        var dot = path.IndexOf('.');
                        return dot < 0 ? path : path[..dot];
                    }
                }
            }
            return null;
        }
    }

    /// <summary>The cell of a path (its box is the cell's focus target and link anchor); null when there is none.</summary>
    public Control? FindCell(string path) => _cellsByPath.GetValueOrDefault(path);

    /// <summary>The value box of a cell path, or null.</summary>
    internal ExtractionInput? FindInput(string path) => _cellsByPath.GetValueOrDefault(path)?.Input;

    /// <summary>The data row at <paramref name="index"/> (the link anchor of a row path), or null.</summary>
    public Control? FindRow(int index) => index >= 0 && index < _rows.Count ? _rows[index] : null;

    /// <summary>Focuses the box of a cell path with the caret after the text; false when there is no such cell.</summary>
    public bool FocusCell(string path, NavigationMethod method = NavigationMethod.Pointer) =>
        _cellsByPath.TryGetValue(path, out var cell) && cell.Input.FocusWithCaretAtEnd(method);

    /// <summary>
    /// The person confirmed a cell's value (the owner's "Looks right" from the cell's context): raises
    /// <see cref="LooksRightRequested"/> with the path and changes nothing else. False when the path is not a cell of this table.
    /// </summary>
    public bool ConfirmCell(string path)
    {
        if (!_cellsByPath.ContainsKey(path))
        {
            return false;
        }
        RaiseEvent(new ExtractionPathEventArgs(LooksRightRequestedEvent, this, path));
        return true;
    }

    /// <summary>The fixed widths of the columns (NaN = an equal share of the rest) and whether each is numeric.</summary>
    internal static (double[] Widths, bool[] Numeric) Layout(IReadOnlyList<ExtractionColumn>? columns, bool narrow)
    {
        if (columns is null || columns.Count == 0)
        {
            return ([], []);
        }
        var widths = new double[columns.Count];
        var numeric = new bool[columns.Count];
        var less = narrow ? 2 : 0;
        for (var i = 0; i < columns.Count; i++)
        {
            var type = columns[i].Type;
            numeric[i] = type is ExtractionFieldType.Integer or ExtractionFieldType.Number or ExtractionFieldType.Money;
            widths[i] = i == 0 ? double.NaN : type switch
            {
                ExtractionFieldType.Integer or ExtractionFieldType.Number => 40 - less,
                ExtractionFieldType.Money => (i == columns.Count - 1 ? 80 : 78) - less,
                ExtractionFieldType.Date => 92 - less,
                ExtractionFieldType.Currency => 56 - less,
                _ => double.NaN
            };
        }
        return (widths, numeric);
    }

    #region Template and lifetime

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _rowsPanel?.Children.Clear();
        _headerRow?.Children.Clear();
        _dot = e.NameScope.Find<Ellipse>(DotPart);
        _size = e.NameScope.Find<SpringSizeDecorator>(SizePart);
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        _body = e.NameScope.Find<StackPanel>(BodyPart);
        _headerRow = e.NameScope.Find<ExtractionTableRowPanel>(HeaderRowPart);
        _rowsPanel = e.NameScope.Find<StackPanel>(RowsPart);
        if (_body is not null)
        {
            AutomationProperties.SetControlTypeOverride(_body, AutomationControlType.Table);
            AutomationProperties.SetName(_body, Label);
        }
        BuildColumns();
        RebuildRows(animate: false);
        UpdatePalette();
        UpdateTexts();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        ObserveRows(Rows);
        SyncRows();
        UpdatePalette();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        ObserveRows(null);
        _hoverPath = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ColumnsProperty || change.Property == IsNarrowProperty)
        {
            if (change.Property == IsNarrowProperty)
            {
                PseudoClasses.Set(NarrowPseudoClass, change.GetNewValue<bool>());
            }
            var rebuild = change.Property == ColumnsProperty;
            BuildColumns();
            if (rebuild)
            {
                RebuildRows(animate: false);
            }
        }
        else if (change.Property == RowsProperty)
        {
            if (_attached)
            {
                ObserveRows(change.GetNewValue<AvaloniaList<AvaloniaList<ExtractionValue>>?>());
            }
            RebuildRows(animate: false);
        }
        else if (change.Property == IsRunningProperty)
        {
            PseudoClasses.Set(RunningPseudoClass, change.GetNewValue<bool>());
            UpdateTexts();
        }
        else if (change.Property == RowsTextProperty || change.Property == TableTextProperty ||
                 change.Property == EmptyTextProperty || change.Property == PendingTextProperty)
        {
            UpdateTexts();
        }
        else if (change.Property == LinkedPathProperty || change.Property == FieldKeyProperty || change.Property == ToneProperty)
        {
            UpdateTints();
        }
        else if (change.Property == CellAutomationNameProperty)
        {
            UpdateCellNames();
        }
        else if (change.Property == LabelProperty)
        {
            if (_body is not null)
            {
                AutomationProperties.SetName(_body, change.GetNewValue<string?>());
            }
        }
        else if (ExtractionPalette.IsProbe(change.Property))
        {
            UpdatePalette();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The grid's content is as wide as the frame, but never narrower than the minimum: below it the frame scrolls. The
        // width is given here because the scroller measures its content with an unbounded width.
        if (_body is not null)
        {
            var inner = double.IsInfinity(availableSize.Width) ? MinTableWidth : availableSize.Width - HorizontalChrome;
            var width = Math.Max(MinTableWidth, inner);
            if (Math.Abs(_body.Width - width) > 0.01 || double.IsNaN(_body.Width))
            {
                _body.Width = width;
            }
        }
        return base.MeasureOverride(availableSize);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    #endregion

    #region Rows

    private void ObserveRows(AvaloniaList<AvaloniaList<ExtractionValue>>? rows)
    {
        if (!ReferenceEquals(_observedRows, rows))
        {
            if (_observedRows is not null)
            {
                _observedRows.CollectionChanged -= OnRowsChanged;
            }
            _observedRows = rows;
            if (_observedRows is not null)
            {
                _observedRows.CollectionChanged += OnRowsChanged;
            }
        }
        ObserveCells(rows);
    }

    /// <summary>Each row's list too: a cell replaced in a row rebuilds that row.</summary>
    private void ObserveCells(AvaloniaList<AvaloniaList<ExtractionValue>>? rows)
    {
        foreach (var list in _observedCells)
        {
            list.CollectionChanged -= OnCellsChanged;
        }
        _observedCells.Clear();
        if (rows is null)
        {
            return;
        }
        foreach (var list in rows)
        {
            list.CollectionChanged += OnCellsChanged;
            _observedCells.Add(list);
        }
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ObserveCells(_observedRows);
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { } added && e.NewStartingIndex >= 0 && _rowsPanel is not null &&
            e.NewStartingIndex == _rows.Count)
        {
            // Rows arrive at the end, one by one: each opens on its own.
            var index = e.NewStartingIndex;
            foreach (var item in added)
            {
                if (item is AvaloniaList<ExtractionValue> values)
                {
                    AddRow(index++, values, animate: true);
                }
            }
            AfterRowsChanged();
            return;
        }
        RebuildRows(animate: false);
    }

    private void OnCellsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is not AvaloniaList<ExtractionValue> list)
        {
            return;
        }
        for (var i = 0; i < _rows.Count; i++)
        {
            if (ReferenceEquals(_rows[i].Values, list))
            {
                FillRow(_rows[i], i);
                break;
            }
        }
        AfterRowsChanged();
    }

    /// <summary>Makes the row views match <see cref="Rows"/> from scratch (no motion).</summary>
    private void RebuildRows(bool animate)
    {
        if (_rowsPanel is null)
        {
            return;
        }
        _rowsPanel.Children.Clear();
        _rows.Clear();
        _cellsByPath.Clear();
        _litRow = null;
        _litCell = null;
        if (Rows is { } rows)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                AddRow(i, rows[i], animate);
            }
        }
        AfterRowsChanged();
    }

    /// <summary>
    /// Back in a window: the rows that still match <see cref="Rows"/> keep their cells (and anything the person typed); the
    /// lists were not observed while away, so a changed list rebuilds.
    /// </summary>
    private void SyncRows()
    {
        var rows = Rows;
        var count = rows?.Count ?? 0;
        var same = count == _rows.Count;
        for (var i = 0; same && i < count; i++)
        {
            same = ReferenceEquals(_rows[i].Values, rows![i]);
        }
        if (!same)
        {
            RebuildRows(animate: false);
            return;
        }
        for (var i = 0; i < _rows.Count; i++)
        {
            FillRow(_rows[i], i);
        }
        AfterRowsChanged();
    }

    private void AddRow(int index, AvaloniaList<ExtractionValue> values, bool animate)
    {
        if (_rowsPanel is null)
        {
            return;
        }
        var row = new ExtractionTableRow { Values = values };
        row.Panel.Widths = _widths;
        row.SetSeparated(index > 0);
        FillRow(row, index);
        _rows.Insert(index, row);
        _rowsPanel.Children.Insert(index, row);
        var level = ArcMotion.GetEffectiveLevel(this);
        if (animate && IsLoaded && level == ArcMotionLevel.Full)
        {
            // The height opens on the grid's spring (the SpringSizeDecorator); the row itself fades in.
            FadeIn(row);
        }
    }

    /// <summary>One cell per column for the row's values (cells are kept when the value at a column stays the same).</summary>
    private void FillRow(ExtractionTableRow row, int index)
    {
        var columns = Columns ?? [];
        var values = row.Values;
        foreach (var old in row.Cells)
        {
            if (old.Value?.Path is { } path && _cellsByPath.TryGetValue(path, out var mapped) && ReferenceEquals(mapped, old))
            {
                _cellsByPath.Remove(path);
            }
        }
        for (var c = 0; c < columns.Count; c++)
        {
            var value = values is not null && c < values.Count ? values[c] : null;
            ExtractionTableCell cell;
            if (c < row.Cells.Count && ReferenceEquals(row.Cells[c].Column, columns[c]))
            {
                cell = row.Cells[c];
            }
            else
            {
                cell = new ExtractionTableCell(columns[c], c < _numeric.Length && _numeric[c]);
                if (c < row.Cells.Count)
                {
                    row.Cells[c] = cell;
                    row.Panel.Children[c] = cell;
                }
                else
                {
                    row.Cells.Add(cell);
                    row.Panel.Children.Add(cell);
                }
            }
            cell.Input.Palette = _palette;
            cell.Value = value;
            if (value?.Path is { } path)
            {
                _cellsByPath[path] = cell;
            }
            AutomationProperties.SetName(cell.Input, CellName(columns[c], index));
        }
        while (row.Cells.Count > columns.Count)
        {
            row.Cells.RemoveAt(row.Cells.Count - 1);
            row.Panel.Children.RemoveAt(row.Panel.Children.Count - 1);
        }
    }

    private string CellName(ExtractionColumn column, int index) =>
        CellAutomationName is { } name ? name(column.Label, index + 1) : column.Label;

    private void UpdateCellNames()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            foreach (var cell in _rows[i].Cells)
            {
                AutomationProperties.SetName(cell.Input, CellName(cell.Column, i));
            }
        }
    }

    private void AfterRowsChanged()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].SetSeparated(i > 0);
        }
        PseudoClasses.Set(EmptyPseudoClass, _rows.Count == 0);
        UpdateTexts();
        UpdateTints();
    }

    /// <summary>A new row fades in, 160ms on the standard curve, while the grid's height opens on the smooth spring.</summary>
    private static async void FadeIn(ExtractionTableRow row)
    {
        // Code-driven: the row is created when its first value arrives, so there is no resting state for a ControlTheme
        // transition to start from. The row is in the window already (it was just inserted).
        row.Opacity = 0;
        var fade = new MotionValue<double>(row, 0, MotionVectors.Double);
        fade.Changed += v => row.Opacity = v;
        await fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        row.Opacity = 1;
        fade.Dispose();
    }

    #endregion

    #region Columns, texts and colors

    private void BuildColumns()
    {
        (_widths, _numeric) = Layout(Columns, IsNarrow);
        if (_headerRow is not null)
        {
            _headerRow.Children.Clear();
            if (Columns is { } columns)
            {
                for (var i = 0; i < columns.Count; i++)
                {
                    _headerRow.Children.Add(new ExtractionTableHeaderCell(columns[i].Label, _numeric[i]));
                }
            }
            _headerRow.Widths = _widths;
        }
        foreach (var row in _rows)
        {
            row.Panel.Widths = _widths;
        }
    }

    private void UpdateTexts()
    {
        CountText = _rows.Count > 0 ? RowsText : TableText;
        ShownEmptyText = _rows.Count > 0 ? null : IsRunning ? PendingText : EmptyText;
    }

    private void UpdatePalette()
    {
        var palette = ExtractionPalette.Build(this);
        if (palette is null || ReferenceEquals(palette, _palette))
        {
            return;
        }
        _palette = palette;
        foreach (var row in _rows)
        {
            foreach (var cell in row.Cells)
            {
                cell.Input.Palette = palette;
            }
        }
        UpdateTints();
    }

    /// <summary>The tints of <see cref="LinkedPath"/>: the field at 6%, a row or a cell at 12% (the theme fades them).</summary>
    private void UpdateTints()
    {
        if (_palette is not { } palette)
        {
            return;
        }
        if (_dot is not null)
        {
            _dot.Fill = palette.Dot(Tone);
        }
        var path = LinkedPath;
        var key = Key;
        var inTable = path is not null && key is not null &&
                      (string.Equals(path, key, StringComparison.Ordinal) || path.StartsWith(key + ".", StringComparison.Ordinal));
        PseudoClasses.Set(LinkedPseudoClass, inTable);
        SetValue(BackgroundProperty, inTable ? palette.TableTint(Tone) : Brushes.Transparent);

        ExtractionTableRow? litRow = null;
        ExtractionTableCell? litCell = null;
        if (inTable && path!.Length > key!.Length)
        {
            var rest = path[(key.Length + 1)..];
            var dot = rest.IndexOf('.');
            if (dot < 0)
            {
                if (int.TryParse(rest, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index) &&
                    index >= 0 && index < _rows.Count)
                {
                    litRow = _rows[index];
                }
            }
            else
            {
                litCell = _cellsByPath.GetValueOrDefault(path);
            }
        }
        if (!ReferenceEquals(litRow, _litRow) && _litRow is not null)
        {
            _litRow.SetValue(BackgroundProperty, Brushes.Transparent);
        }
        if (!ReferenceEquals(litCell, _litCell) && _litCell is not null)
        {
            _litCell.SetValue(BackgroundProperty, Brushes.Transparent);
        }
        _litRow = litRow;
        _litCell = litCell;
        litRow?.SetValue(BackgroundProperty, palette.CellTint(Tone));
        litCell?.SetValue(BackgroundProperty, palette.CellTint(Tone));
    }

    #endregion

    #region Pointer

    // The entry itself is followed by a move over the element under the pointer (bubbling here): the move tells which cell.
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            SetHover(CellPathAt(e) ?? Key);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHover(null);
    }

    /// <summary>The path of the cell under the pointer, from the element it is over.</summary>
    private static string? CellPathAt(PointerEventArgs e)
    {
        for (var element = e.Source as Visual; element is not null; element = element.GetVisualParent())
        {
            if (element is ExtractionTableCell cell)
            {
                return cell.Value?.Path;
            }
            if (element is ExtractionTableGrid)
            {
                break;
            }
        }
        return null;
    }

    /// <summary>Raises the exit of the old path and the entry of the new one when the pointer moved to something else.</summary>
    private void SetHover(string? path)
    {
        if (string.Equals(path, _hoverPath, StringComparison.Ordinal))
        {
            return;
        }
        var old = _hoverPath;
        _hoverPath = path;
        if (old is not null)
        {
            RaiseEvent(new ExtractionPathEventArgs(PathExitedEvent, this, old));
        }
        if (path is not null)
        {
            RaiseEvent(new ExtractionPathEventArgs(PathEnteredEvent, this, path));
        }
    }

    /// <summary>The path the pointer is over now (a cell, the table's key or null).</summary>
    internal string? HoverPath => _hoverPath;

    #endregion

    /// <summary>The field as a group (the grid inside is the table).</summary>
    private sealed class GroupPeer(ExtractionTableGrid owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => ((ExtractionTableGrid)Owner).Label;
    }
}
