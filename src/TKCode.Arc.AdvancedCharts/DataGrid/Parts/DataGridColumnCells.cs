using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Localization;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// One column header: a sort button (label, direction arrow and priority), a column menu button and a resize grip on the
/// right edge. Click sorts (Shift adds a sort key), Alt + arrows resize by 16px, dragging the grip resizes and a double
/// click on it fits the column to its content. Dragging the label (or Alt + Shift + arrows) moves the column.
/// </summary>
[TemplatePart(SortPart, typeof(AvaloniaButton))]
[TemplatePart(MenuPart, typeof(AvaloniaButton))]
[TemplatePart(ResizerPart, typeof(Border))]
[PseudoClasses(SortedPseudoClass, DescendingPseudoClass, NumericPseudoClass, InRangePseudoClass, PinEdgePseudoClass,
    ResizingPseudoClass, TouchPseudoClass, UnsortablePseudoClass)]
internal sealed class DataGridHeaderCell : TemplatedControl
{
    public const string SortPart = "PART_Sort";
    public const string MenuPart = "PART_Menu";
    public const string ResizerPart = "PART_Resizer";
    public const string SortedPseudoClass = ":sorted";
    public const string DescendingPseudoClass = ":descending";
    public const string NumericPseudoClass = ":numeric";
    public const string InRangePseudoClass = ":in-range";
    public const string PinEdgePseudoClass = ":pin-edge";
    public const string ResizingPseudoClass = ":resizing";
    public const string TouchPseudoClass = ":touch";
    public const string UnsortablePseudoClass = ":unsortable";

    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<DataGridHeaderCell, string>(nameof(Header), string.Empty);

    public static readonly StyledProperty<string?> SortOrderTextProperty =
        AvaloniaProperty.Register<DataGridHeaderCell, string?>(nameof(SortOrderText));

    public static readonly StyledProperty<double> SortAngleProperty =
        AvaloniaProperty.Register<DataGridHeaderCell, double>(nameof(SortAngle));

    public static readonly StyledProperty<HorizontalAlignment> LabelAlignmentProperty =
        AvaloniaProperty.Register<DataGridHeaderCell, HorizontalAlignment>(nameof(LabelAlignment));

    private readonly DataGrid _grid;
    private AvaloniaButton? _sort;
    private AvaloniaButton? _menu;
    private Border? _resizer;
    private bool _shiftOnPress;
    private IPointer? _dragPointer;
    private Point _dragStart;
    private bool _dragging;
    private int _dragTarget;
    private double _resizeStart;
    private double _resizeOrigin;
    private IPointer? _resizePointer;

    public DataGridHeaderCell(DataGrid grid)
    {
        _grid = grid;
    }

    public string Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    public string? SortOrderText { get => GetValue(SortOrderTextProperty); set => SetValue(SortOrderTextProperty, value); }

    public double SortAngle { get => GetValue(SortAngleProperty); set => SetValue(SortAngleProperty, value); }

    public HorizontalAlignment LabelAlignment { get => GetValue(LabelAlignmentProperty); set => SetValue(LabelAlignmentProperty, value); }

    public int VisibleIndex { get; private set; } = -1;

    public int Accessor { get; private set; } = -1;

    public DataGridColumn? Column => Accessor >= 0 && Accessor < _grid.Store.Accessors.Length ? _grid.ColumnOf(Accessor) : null;

    public void Bind(int visibleIndex, int accessor)
    {
        VisibleIndex = visibleIndex;
        Accessor = accessor;
        Update();
    }

    public void Update()
    {
        if (Column is not { } column)
        {
            return;
        }
        Header = column.Header;
        LabelAlignment = column.IsNumeric ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        var sort = _grid.SortDescriptions;
        var index = -1;
        for (var i = 0; i < sort.Count; i++)
        {
            if (sort[i].Key == column.Key)
            {
                index = i;
                break;
            }
        }
        var descending = index >= 0 && sort[index].Direction == DataGridSortDirection.Descending;
        PseudoClasses.Set(SortedPseudoClass, index >= 0);
        PseudoClasses.Set(DescendingPseudoClass, descending);
        PseudoClasses.Set(NumericPseudoClass, column.IsNumeric);
        PseudoClasses.Set(UnsortablePseudoClass, !column.IsSortable);
        PseudoClasses.Set(PinEdgePseudoClass, VisibleIndex == _grid.PinCount - 1);
        PseudoClasses.Set(TouchPseudoClass, _grid.Viewport.TouchMode);
        SortAngle = descending ? 180 : 0;
        SortOrderText = index >= 0 && sort.Count > 1 ? (index + 1).ToString(_grid.EffectiveCulture) : null;
        if (_sort is not null)
        {
            _sort.IsEnabled = column.IsSortable;
            var name = column.Header;
            if (index >= 0)
            {
                name += ", " + DataGridText.Get(descending ? DataGridLangResourceKind.SortedDescending : DataGridLangResourceKind.SortedAscending);
                if (sort.Count > 1)
                {
                    name += ", " + DataGridText.Format(DataGridLangResourceKind.SortPriority, index + 1);
                }
            }
            name += ". " + (column.IsSortable ? DataGridText.Get(DataGridLangResourceKind.SortHint) + ". " : string.Empty) +
                    DataGridText.Get(DataGridLangResourceKind.ResizeHint);
            AutomationProperties.SetName(_sort, name);
        }
        if (_menu is not null)
        {
            AutomationProperties.SetName(_menu, DataGridText.Format(DataGridLangResourceKind.ColumnOptions, column.Header));
        }
        UpdateRange();
    }

    public void UpdateRange()
    {
        var range = _grid.Range;
        PseudoClasses.Set(InRangePseudoClass, _grid.HasCells && VisibleIndex >= range.Column0 && VisibleIndex <= range.Column1);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_sort is not null)
        {
            _sort.Click -= OnSortClick;
            _sort.RemoveHandler(PointerPressedEvent, OnSortPointerPressed);
            _sort.RemoveHandler(PointerMovedEvent, OnSortPointerMoved);
            _sort.KeyDown -= OnSortKeyDown;
        }
        if (_menu is not null)
        {
            _menu.Click -= OnMenuClick;
        }
        if (_resizer is not null)
        {
            _resizer.PointerPressed -= OnResizerPressed;
            _resizer.PointerMoved -= OnResizerMoved;
            _resizer.PointerReleased -= OnResizerReleased;
            _resizer.PointerCaptureLost -= OnResizerCaptureLost;
            _resizer.DoubleTapped -= OnResizerDoubleTapped;
        }
        _sort = e.NameScope.Find<AvaloniaButton>(SortPart);
        _menu = e.NameScope.Find<AvaloniaButton>(MenuPart);
        _resizer = e.NameScope.Find<Border>(ResizerPart);
        if (_sort is not null)
        {
            _sort.Click += OnSortClick;
            _sort.AddHandler(PointerPressedEvent, OnSortPointerPressed, RoutingStrategies.Tunnel);
            _sort.AddHandler(PointerMovedEvent, OnSortPointerMoved, RoutingStrategies.Tunnel);
            _sort.KeyDown += OnSortKeyDown;
        }
        if (_menu is not null)
        {
            _menu.Click += OnMenuClick;
        }
        if (_resizer is not null)
        {
            _resizer.PointerPressed += OnResizerPressed;
            _resizer.PointerMoved += OnResizerMoved;
            _resizer.PointerReleased += OnResizerReleased;
            _resizer.PointerCaptureLost += OnResizerCaptureLost;
            _resizer.DoubleTapped += OnResizerDoubleTapped;
        }
        Update();
    }

    private void OnSortPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _shiftOnPress = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        _dragPointer = null;
        if (Column is not null && e.Pointer.Type != PointerType.Touch && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
            _grid.VisibleAccessors.Length > 1)
        {
            _dragPointer = e.Pointer;
            _dragStart = e.GetPosition(_grid.Viewport);
            _dragging = false;
        }
    }

    private void OnSortPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragPointer is null || !ReferenceEquals(_dragPointer, e.Pointer))
        {
            return;
        }
        var position = e.GetPosition(_grid.Viewport);
        if (!_dragging && Math.Abs(position.X - _dragStart.X) > 6)
        {
            _dragging = true;
            Opacity = 0.55;
            e.Pointer.Capture(this);
        }
        if (_dragging)
        {
            e.Handled = true;
            UpdateDrag(position);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging && ReferenceEquals(_dragPointer, e.Pointer))
        {
            UpdateDrag(e.GetPosition(_grid.Viewport));
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndDrag(commit: true);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndDrag(commit: false);
    }

    private void UpdateDrag(Point position)
    {
        var target = _grid.Viewport.ColumnIndexAtX(position.X);
        if (target < 0)
        {
            return;
        }
        _dragTarget = _grid.ClampMoveTarget(VisibleIndex, target);
        _grid.Viewport.ShowColumnDrop(_dragTarget == VisibleIndex ? null : _grid.Viewport.ColumnEdgeX(_dragTarget, rightEdge: _dragTarget > VisibleIndex));
    }

    private void EndDrag(bool commit)
    {
        if (_dragPointer is null)
        {
            return;
        }
        var wasDragging = _dragging;
        _dragPointer = null;
        _dragging = false;
        Opacity = 1;
        _grid.Viewport.ShowColumnDrop(null);
        if (wasDragging && commit && Column is { } column && _dragTarget != VisibleIndex)
        {
            _grid.MoveColumn(column.Key, _dragTarget);
        }
    }

    /// <summary>Puts keyboard focus on the sort button, so a moved column can be moved again.</summary>
    public void FocusSort() => _sort?.Focus(NavigationMethod.Tab);

    private void OnSortClick(object? sender, RoutedEventArgs e)
    {
        if (Column is not { IsSortable: true } column)
        {
            return;
        }
        var shift = _shiftOnPress;
        _shiftOnPress = false;
        _grid.CycleSort(column.Key, shift);
    }

    private void OnSortKeyDown(object? sender, KeyEventArgs e)
    {
        if (Column is not { } column)
        {
            return;
        }
        if ((e.KeyModifiers & (KeyModifiers.Alt | KeyModifiers.Shift)) == (KeyModifiers.Alt | KeyModifiers.Shift) && e.Key is Key.Left or Key.Right)
        {
            e.Handled = true;
            var key = column.Key;
            var index = VisibleIndex;
            _grid.MoveColumnBy(key, e.Key == Key.Right ? 1 : -1);
            var moved = _grid.VisibleIndexOf(key);
            if (moved >= 0 && moved != index)
            {
                _grid.Viewport.Header.FocusColumn(moved);
            }
            return;
        }
        if ((e.KeyModifiers & KeyModifiers.Alt) != 0 && e.Key is Key.Left or Key.Right)
        {
            e.Handled = true;
            var step = _grid.ColumnResizeStep * (e.Key == Key.Right ? 1 : -1);
            _grid.ResizeColumn(column, _grid.Layout.Widths[Math.Clamp(VisibleIndex, 0, _grid.Layout.Count - 1)] + step);
            return;
        }
        if (e.Key is Key.Enter or Key.Space)
        {
            _shiftOnPress = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        }
        if (e.Key == Key.Down && (e.KeyModifiers & KeyModifiers.Alt) == 0)
        {
            e.Handled = true;
            _grid.Viewport.FocusGrid();
        }
    }

    private void OnMenuClick(object? sender, RoutedEventArgs e)
    {
        if (_menu is not null && Accessor >= 0)
        {
            _grid.OpenColumnMenu(_menu, Accessor);
        }
    }

    private void OnResizerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Column is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        e.Handled = true;
        _resizePointer = e.Pointer;
        _resizeOrigin = e.GetPosition(_grid).X;
        _resizeStart = _grid.Layout.Widths[Math.Clamp(VisibleIndex, 0, _grid.Layout.Count - 1)];
        e.Pointer.Capture(_resizer);
        PseudoClasses.Set(ResizingPseudoClass, true);
    }

    private void OnResizerMoved(object? sender, PointerEventArgs e)
    {
        if (_resizePointer is null || !ReferenceEquals(_resizePointer, e.Pointer) || Column is not { } column)
        {
            return;
        }
        _grid.ResizeColumn(column, _resizeStart + e.GetPosition(_grid).X - _resizeOrigin);
    }

    private void OnResizerReleased(object? sender, PointerReleasedEventArgs e) => EndResize();

    private void OnResizerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => EndResize();

    private void EndResize()
    {
        if (_resizePointer is null)
        {
            return;
        }
        var pointer = _resizePointer;
        _resizePointer = null;
        pointer.Capture(null);
        PseudoClasses.Set(ResizingPseudoClass, false);
    }

    private void OnResizerDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Column is { } column)
        {
            e.Handled = true;
            _grid.AutoFitColumn(column.Key);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new HeaderPeer(this);

    private sealed class HeaderPeer(DataGridHeaderCell owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.HeaderItem;

        protected override string? GetNameCore() => ((DataGridHeaderCell)Owner).Header;
    }
}

/// <summary>
/// One filter cell: a text input for text, number and date columns (marked invalid when an expression cannot be read),
/// or a button that opens the value checklist of a select column.
/// </summary>
[TemplatePart(InputPart, typeof(TextBox))]
[TemplatePart(SelectPart, typeof(AvaloniaButton))]
[PseudoClasses(SelectPseudoClass, ActivePseudoClass, InvalidPseudoClass, DisabledFilterPseudoClass, NumericPseudoClass, PinEdgePseudoClass)]
internal sealed class DataGridFilterCell : TemplatedControl
{
    public const string InputPart = "PART_Input";
    public const string SelectPart = "PART_Select";
    public const string SelectPseudoClass = ":select";
    public const string ActivePseudoClass = ":active";
    public const string InvalidPseudoClass = ":invalid";
    public const string DisabledFilterPseudoClass = ":no-filter";
    public const string NumericPseudoClass = ":numeric";
    public const string PinEdgePseudoClass = ":pin-edge";

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<DataGridFilterCell, string?>(nameof(Placeholder));

    public static readonly StyledProperty<string?> SelectTextProperty =
        AvaloniaProperty.Register<DataGridFilterCell, string?>(nameof(SelectText));

    private readonly DataGrid _grid;
    private TextBox? _input;
    private AvaloniaButton? _select;
    private bool _syncing;

    public DataGridFilterCell(DataGrid grid)
    {
        _grid = grid;
    }

    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    public string? SelectText { get => GetValue(SelectTextProperty); set => SetValue(SelectTextProperty, value); }

    public int VisibleIndex { get; private set; } = -1;

    public int Accessor { get; private set; } = -1;

    private DataGridColumn? Column => Accessor >= 0 && Accessor < _grid.Store.Accessors.Length ? _grid.ColumnOf(Accessor) : null;

    public void Bind(int visibleIndex, int accessor)
    {
        VisibleIndex = visibleIndex;
        Accessor = accessor;
        Update();
    }

    public void Update()
    {
        if (Column is not { } column)
        {
            return;
        }
        var filter = _grid.FilterOf(column.Key);
        var isSelect = column.Type is DataGridColumnType.Select or DataGridColumnType.Boolean;
        PseudoClasses.Set(SelectPseudoClass, isSelect);
        PseudoClasses.Set(DisabledFilterPseudoClass, !column.IsFilterable);
        PseudoClasses.Set(NumericPseudoClass, column.IsNumeric);
        PseudoClasses.Set(PinEdgePseudoClass, VisibleIndex == _grid.PinCount - 1);
        PseudoClasses.Set(ActivePseudoClass, DataGridFilterExpression.IsActive(column.Type, filter, _grid.EffectiveCulture));
        var expression = filter?.Expression ?? string.Empty;
        PseudoClasses.Set(InvalidPseudoClass, !isSelect && DataGridFilterExpression.IsInvalid(column.Type, expression, _grid.EffectiveCulture));
        Placeholder = column.IsNumeric
            ? DataGridText.Get(DataGridLangResourceKind.FilterNumberPlaceholder)
            : column.Type == DataGridColumnType.Date
                ? DataGridText.Get(DataGridLangResourceKind.FilterDatePlaceholder)
                : DataGridText.Get(DataGridLangResourceKind.FilterContains);
        if (_input is not null && !_syncing && filter?.Values is null && _input.Text != expression)
        {
            _syncing = true;
            _input.Text = expression;
            _syncing = false;
        }
        if (_input is not null)
        {
            var hint = column.IsNumeric
                ? ". " + DataGridText.Get(DataGridLangResourceKind.FilterNumberHint)
                : column.Type == DataGridColumnType.Date
                    ? ". " + DataGridText.Get(DataGridLangResourceKind.FilterDateHint)
                    : string.Empty;
            AutomationProperties.SetName(_input, DataGridText.Format(DataGridLangResourceKind.FilterLabel, column.Header) + hint);
        }
        if (isSelect)
        {
            var options = _grid.FilterOptionsOf(Accessor);
            var values = filter?.Values;
            SelectText = values is null
                ? DataGridText.Get(DataGridLangResourceKind.FilterAll)
                : values.Count == 0
                    ? DataGridText.Get(DataGridLangResourceKind.FilterNone)
                    : values.Count == 1
                        ? values[0].Length == 0 ? DataGridText.Get(DataGridLangResourceKind.EmptyValue) : _grid.FilterLabelOf(column, values[0])
                        : DataGridText.Format(DataGridLangResourceKind.FilterSomeOf, values.Count, options.Count);
            if (_select is not null)
            {
                AutomationProperties.SetName(_select, DataGridText.Format(DataGridLangResourceKind.FilterSelectLabel, column.Header, SelectText));
            }
        }
    }

    public void FocusInput()
    {
        if (Column?.Type is DataGridColumnType.Select or DataGridColumnType.Boolean)
        {
            // Reached from the column menu's Filter: no keyboard ring after a click.
            _select?.Focus(DataGridFocusModality.For(this));
        }
        else
        {
            _input?.Focus(NavigationMethod.Tab);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_input is not null)
        {
            _input.TextChanged -= OnInputChanged;
            _input.KeyDown -= OnInputKeyDown;
        }
        if (_select is not null)
        {
            _select.Click -= OnSelectClick;
        }
        _input = e.NameScope.Find<TextBox>(InputPart);
        _select = e.NameScope.Find<AvaloniaButton>(SelectPart);
        if (_input is not null)
        {
            _input.TextChanged += OnInputChanged;
            _input.KeyDown += OnInputKeyDown;
        }
        if (_select is not null)
        {
            _select.Click += OnSelectClick;
        }
        Update();
    }

    private void OnInputChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncing || Column is not { } column || _input is null)
        {
            return;
        }
        _syncing = true;
        try
        {
            _grid.SetFilter(column.Key, string.IsNullOrEmpty(_input.Text) ? null : DataGridFilter.Text(_input.Text));
        }
        finally
        {
            _syncing = false;
        }
        Update();
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (_input is null || Column is not { } column)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                if (!string.IsNullOrEmpty(_input.Text))
                {
                    _input.Text = string.Empty;
                    _grid.SetFilter(column.Key, null);
                }
                else
                {
                    _grid.Viewport.FocusGrid();
                }
                break;
            case Key.Enter:
            case Key.Down:
                e.Handled = true;
                _grid.Viewport.FocusGrid();
                break;
        }
    }

    private void OnSelectClick(object? sender, RoutedEventArgs e)
    {
        if (_select is not null && Accessor >= 0)
        {
            _grid.OpenSelectFilter(_select, Accessor);
        }
    }
}

/// <summary>One cell of the totals row: the aggregate's label and a rolling number, or the row count under the first column.</summary>
[PseudoClasses(NumericPseudoClass, CountPseudoClass, PinEdgePseudoClass)]
internal sealed class DataGridTotalCell : TemplatedControl
{
    public const string NumericPseudoClass = ":numeric";
    public const string CountPseudoClass = ":row-count";
    public const string PinEdgePseudoClass = ":pin-edge";

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<DataGridTotalCell, string?>(nameof(Label));

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<DataGridTotalCell, double>(nameof(Value));

    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<DataGridTotalCell, int>(nameof(Decimals));

    public static readonly StyledProperty<string?> PrefixProperty =
        AvaloniaProperty.Register<DataGridTotalCell, string?>(nameof(Prefix));

    public static readonly StyledProperty<string?> SuffixProperty =
        AvaloniaProperty.Register<DataGridTotalCell, string?>(nameof(Suffix));

    public static readonly StyledProperty<bool> HasValueProperty =
        AvaloniaProperty.Register<DataGridTotalCell, bool>(nameof(HasValue));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<DataGridTotalCell, string?>(nameof(Text));

    public static readonly StyledProperty<string> CultureNameProperty =
        AvaloniaProperty.Register<DataGridTotalCell, string>(nameof(CultureName), "en-US");

    private readonly DataGrid _grid;

    public DataGridTotalCell(DataGrid grid)
    {
        _grid = grid;
    }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public int Decimals { get => GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }

    public string? Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }

    public string? Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }

    public bool HasValue { get => GetValue(HasValueProperty); set => SetValue(HasValueProperty, value); }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public string CultureName { get => GetValue(CultureNameProperty); set => SetValue(CultureNameProperty, value); }

    public int VisibleIndex { get; private set; } = -1;

    public int Accessor { get; private set; } = -1;

    public void Bind(int visibleIndex, int accessor)
    {
        VisibleIndex = visibleIndex;
        Accessor = accessor;
        Update();
    }

    public void Update()
    {
        if (Accessor < 0 || Accessor >= _grid.Store.Accessors.Length)
        {
            return;
        }
        var column = _grid.ColumnOf(Accessor);
        var kind = column.EffectiveAggregate;
        var totals = _grid.Totals;
        var value = VisibleIndex >= 0 && VisibleIndex < totals.Length ? totals[VisibleIndex] : null;
        var culture = _grid.EffectiveCulture;
        CultureName = string.IsNullOrEmpty(culture.Name) ? "en-US" : culture.Name;
        PseudoClasses.Set(NumericPseudoClass, column.IsNumeric || kind == DataGridAggregate.Count);
        PseudoClasses.Set(PinEdgePseudoClass, VisibleIndex == _grid.PinCount - 1);
        var rowCount = VisibleIndex == 0 && kind == DataGridAggregate.None;
        PseudoClasses.Set(CountPseudoClass, rowCount);
        if (rowCount)
        {
            HasValue = false;
            Label = null;
            Text = _grid.IsShowingSkeleton ? DataGridText.Get(DataGridLangResourceKind.Loading) : _grid.RowCountText();
            AutomationProperties.SetName(this, Text);
            return;
        }
        Text = null;
        if (value is not { } number || kind == DataGridAggregate.None)
        {
            HasValue = false;
            Label = null;
            AutomationProperties.SetName(this, string.Empty);
            return;
        }
        HasValue = true;
        Label = DataGridText.Get(kind switch
        {
            DataGridAggregate.Sum => DataGridLangResourceKind.AggregateSum,
            DataGridAggregate.Average => DataGridLangResourceKind.AggregateAverage,
            DataGridAggregate.Min => DataGridLangResourceKind.AggregateMin,
            DataGridAggregate.Max => DataGridLangResourceKind.AggregateMax,
            _ => DataGridLangResourceKind.AggregateCount
        });
        if (kind == DataGridAggregate.Count || !column.IsNumeric)
        {
            Value = (double)number;
            Decimals = 0;
            Prefix = null;
            Suffix = null;
        }
        else
        {
            var compact = DataGridValueFormat.FormatCompact(number, culture);
            Value = (double)compact.Value;
            Decimals = compact.Decimals;
            Prefix = column.Type == DataGridColumnType.Currency ? column.CurrencySymbol : null;
            Suffix = compact.Unit + (column.Type == DataGridColumnType.Percent ? culture.NumberFormat.PercentSymbol : string.Empty);
        }
        var spoken = DataGridText.Get(kind switch
        {
            DataGridAggregate.Sum => DataGridLangResourceKind.SpokenSum,
            DataGridAggregate.Average => DataGridLangResourceKind.SpokenAverage,
            DataGridAggregate.Min => DataGridLangResourceKind.SpokenMin,
            DataGridAggregate.Max => DataGridLangResourceKind.SpokenMax,
            _ => DataGridLangResourceKind.SpokenCount
        });
        AutomationProperties.SetName(this, DataGridText.Format(DataGridLangResourceKind.TotalCellLabel, column.Header, spoken,
            _grid.FormatAggregate(number, Accessor)));
    }
}

/// <summary>The select-all checkbox in the header gutter: checks every filtered row, or clears them; mixed when some are checked.</summary>
internal sealed class DataGridSelectAllBox : Control
{
    private readonly DataGrid _grid;
    private bool? _state = false;

    public DataGridSelectAllBox(DataGrid grid)
    {
        _grid = grid;
        Focusable = true;
        FocusAdorner = null;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public bool? State => _state;

    public void Update()
    {
        IsVisible = _grid.IsRowCheckEnabled && !_grid.HasDataSource;
        _state = _grid.SelectAllState;
        AutomationProperties.SetName(this, DataGridText.Get(_state == true ? DataGridLangResourceKind.ClearRowSelection : DataGridLangResourceKind.SelectAllRows));
        InvalidateVisual();
    }

    public void Toggle()
    {
        _grid.ToggleAllChecked();
        Update();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            e.Handled = true;
            Toggle();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Space or Key.Enter)
        {
            e.Handled = true;
            Toggle();
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var palette = _grid.Palette;
        var size = Bounds.Size;
        context.FillRectangle(palette.SurfaceBrush, new Rect(size));
        context.DrawLine(palette.BorderPen, new Point(size.Width - 0.5, 0), new Point(size.Width - 0.5, size.Height));
        context.DrawLine(palette.BorderPen, new Point(0, size.Height - 0.5), new Point(size.Width, size.Height - 0.5));
        var box = new Rect(Math.Round((size.Width - 16) / 2), Math.Round((size.Height - 16) / 2), 16, 16);
        DataGridDrawing.Checkbox(context, box, _state, palette);
        if (IsFocused && palette.FocusPen is { } focus && Classes.Contains(":focus-visible"))
        {
            context.DrawRectangle(null, focus, box.Inflate(3), 6, 6);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    private sealed class Peer(DataGridSelectAllBox owner) : ControlAutomationPeer(owner), IToggleProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.CheckBox;

        public ToggleState ToggleState => ((DataGridSelectAllBox)Owner).State switch
        {
            true => ToggleState.On,
            false => ToggleState.Off,
            _ => ToggleState.Indeterminate
        };

        public void Toggle() => ((DataGridSelectAllBox)Owner).Toggle();
    }
}

/// <summary>The clear-all-filters button in the filter row's gutter, shown while any filter or the search is active.</summary>
internal sealed class DataGridClearFiltersButton : Decorator
{
    private readonly DataGrid _grid;
    private readonly Button _button;

    public DataGridClearFiltersButton(DataGrid grid)
    {
        _grid = grid;
        _button = new Button
        {
            Variant = ButtonVariant.Ghost,
            Size = ControlSize.Small,
            Width = 28,
            Height = 28,
            MinHeight = 28,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Content = new ArcGlyph { Data = ArcIcons.FilterX, Size = 15, StrokeThickness = 1.75 }
        };
        _button.Click += (_, _) => _grid.ClearAllFilters();
        Child = _button;
    }

    public void Update()
    {
        _button.IsVisible = _grid.IsFiltering;
        AutomationProperties.SetName(_button, DataGridText.Get(DataGridLangResourceKind.ClearAllFilters));
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var palette = _grid.Palette;
        context.FillRectangle(palette.SurfaceBrush, new Rect(Bounds.Size));
        context.DrawLine(palette.BorderPen, new Point(Bounds.Width - 0.5, 0), new Point(Bounds.Width - 0.5, Bounds.Height));
        context.DrawLine(palette.GridPen, new Point(0, Bounds.Height - 0.5), new Point(Bounds.Width, Bounds.Height - 0.5));
    }
}
