using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// A column header of a <see cref="TreeTable"/>: a plain label, or a sort button whose arrow fades in and turns over on
/// a snappy spring. Cycles ascending, descending, off.
/// </summary>
internal sealed class TreeTableHeaderCell : Control
{
    private const double ArrowExtent = 14;
    private const double ButtonInset = 8;
    private const double Gap = 4;

    private readonly TreeTable _owner;
    private MotionValue<double>? _arrowShown;
    private MotionValue<double>? _arrowTurn;
    private FormattedText? _label;
    private double _labelWidth = -1;
    private int _paletteVersion = -1;
    private bool _hover;
    private bool _pressed;

    public TreeTableHeaderCell(TreeTable owner, TreeTableColumn column, bool tree)
    {
        _owner = owner;
        Column = column;
        IsTree = tree;
        IsSortable = false;
        FocusAdorner = null;
        ClipToBounds = false;
    }

    public TreeTableColumn Column { get; }

    public bool IsTree { get; }

    public bool IsSortable { get; private set; }

    public DataTableSortDirection Direction { get; private set; }

    /// <summary>The arrow's current opacity (tests).</summary>
    internal double ArrowOpacity => _arrowShown?.Value ?? (Direction == DataTableSortDirection.None ? 0 : 1);

    internal double ArrowAngle => _arrowTurn?.Value ?? (Direction == DataTableSortDirection.Descending ? 180 : 0);

    public void Update(bool sortable, DataTableSortDirection direction, bool animate)
    {
        var changed = sortable != IsSortable || direction != Direction;
        IsSortable = sortable;
        // The hover plate of a sort button reaches 8px past the label, so the label itself lines up with the row names.
        Margin = sortable ? new Thickness(-ButtonInset, 0) : default;
        Focusable = sortable;
        IsTabStop = sortable;
        Cursor = new Cursor(StandardCursorType.Arrow);
        var previous = Direction;
        Direction = direction;
        var shown = direction == DataTableSortDirection.None ? 0.0 : 1.0;
        var turn = direction == DataTableSortDirection.Descending ? 180.0 : 0.0;
        if (!animate || !ArcMotion.IsFull(this) || !this.IsAttachedToVisualTree())
        {
            _arrowShown?.Jump(shown);
            _arrowTurn?.Jump(turn);
        }
        else
        {
            _arrowShown ??= Create(previous == DataTableSortDirection.None ? 0 : 1);
            _arrowTurn ??= Create(previous == DataTableSortDirection.Descending ? 180 : 0);
            _ = _arrowShown.AnimateTo(shown, MotionSpec.Spring(ArcSpring.Snappy));
            _ = _arrowTurn.AnimateTo(turn, MotionSpec.Spring(ArcSpring.Snappy));
        }
        AutomationProperties.SetName(this, Column.Header);
        AutomationProperties.SetItemStatus(this, direction switch
        {
            DataTableSortDirection.Ascending => "ascending",
            DataTableSortDirection.Descending => "descending",
            _ => sortable ? "none" : string.Empty
        });
        if (changed)
        {
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private MotionValue<double> Create(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private FormattedText Label(double maxWidth)
    {
        var palette = _owner.Palette;
        if (_label is null || _paletteVersion != palette.Version || Math.Abs(_labelWidth - maxWidth) > 0.5)
        {
            _paletteVersion = palette.Version;
            _labelWidth = maxWidth;
            _label = new FormattedText(Column.Header, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, palette.Regular,
                palette.SmallFontSize, IsSortable && Direction != DataTableSortDirection.None ? palette.Foreground : palette.TextMuted)
            {
                MaxTextWidth = Math.Max(1, maxWidth),
                MaxLineCount = 1,
                Trimming = TextTrimming.CharacterEllipsis
            };
        }
        return _label;
    }

    private double Extra => IsSortable ? ButtonInset * 2 + Gap + ArrowExtent : 0;

    protected override Size MeasureOverride(Size availableSize)
    {
        var palette = _owner.Palette;
        var natural = new FormattedText(Column.Header, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, palette.Regular,
            palette.SmallFontSize, palette.TextMuted).Width;
        var width = natural + Extra;
        if (!double.IsInfinity(availableSize.Width))
        {
            width = Math.Min(width, availableSize.Width);
        }
        return new Size(width, 28);
    }

    public override void Render(DrawingContext context)
    {
        var palette = _owner.Palette;
        var bounds = new Rect(Bounds.Size);
        context.DrawRectangle(Brushes.Transparent, null, bounds);
        if (IsSortable && (_hover || _pressed) && palette.Foreground is { } fill)
        {
            using (context.PushOpacity(0.06))
            {
                context.DrawRectangle(fill, null, bounds, 10, 10);
            }
        }
        var inset = IsSortable ? ButtonInset : 0;
        var label = Label(Math.Max(1, bounds.Width - Extra));
        var labelOrigin = new Point(inset, (bounds.Height - label.Height) / 2);
        var arrowX = inset + Math.Min(label.Width, bounds.Width - Extra) + Gap;
        if (Column.IsEndAligned && IsSortable)
        {
            // Arc flips the row: the arrow comes first on an end-aligned column.
            labelOrigin = new Point(inset + ArrowExtent + Gap, labelOrigin.Y);
            arrowX = inset;
        }
        context.DrawText(label, labelOrigin);
        if (IsSortable)
        {
            var shown = Math.Clamp(ArrowOpacity, 0, 1);
            if (shown > 0.001 && palette.Foreground is { } foreground)
            {
                var center = new Point(arrowX + ArrowExtent / 2, bounds.Height / 2 + (1 - shown) * 2);
                var scale = ArrowExtent / 24;
                var matrix = Matrix.CreateTranslation(-12, -12) * Matrix.CreateRotation(ArrowAngle * Math.PI / 180) *
                             Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(center.X, center.Y);
                var pen = new Pen(foreground, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                using (context.PushOpacity(shown))
                using (context.PushTransform(matrix))
                {
                    context.DrawGeometry(null, pen, ArcIcons.ArrowUp);
                }
            }
            if (Classes.Contains(":focus-visible") && palette.FocusRing is { } ring)
            {
                context.DrawRectangle(null, ring, bounds.Inflate(2), 12, 12);
            }
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            _hover = true;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = false;
        _pressed = false;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (IsSortable && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
            e.Handled = true;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var wasPressed = _pressed;
        _pressed = false;
        InvalidateVisual();
        if (wasPressed && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            _owner.CycleSort(Column);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (IsSortable && e.Key is Key.Enter or Key.Space && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            _owner.CycleSort(Column);
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

    /// <summary>Activates the header as a click does (tests and automation).</summary>
    internal void Activate()
    {
        if (IsSortable)
        {
            _owner.CycleSort(Column);
        }
    }
}

/// <summary>
/// The header row: an optional select-all checkbox cell, then one cell per shown column; the tree column also carries
/// the expand-all toggle. Shares its column tracks with the rows.
/// </summary>
internal sealed class TreeTableHeader : Grid
{
    private readonly TreeTable _owner;
    private TreeTableGlyphButton? _expandAll;
    private Checkbox? _selectAll;
    private bool _syncingCheck;
    private bool _lastEveryOpen;
    private bool _hasExpandAll;

    public TreeTableHeader(TreeTable owner)
    {
        _owner = owner;
        ClipToBounds = false;
    }

    internal IReadOnlyList<TreeTableHeaderCell> Cells { get; private set; } = [];

    internal Checkbox? SelectAllCheckbox => _selectAll;

    internal TreeTableGlyphButton? ExpandAllButton => _expandAll;

    /// <summary>Builds the cells for the shown columns; called when the columns, their width or selectability change.</summary>
    public void Build(IReadOnlyList<TreeTableColumn> shown, bool selectable)
    {
        Children.Clear();
        ColumnDefinitions.Clear();
        _expandAll = null;
        _selectAll = null;
        _hasExpandAll = false;
        var cells = new List<TreeTableHeaderCell>();
        var column = 0;
        if (selectable)
        {
            ColumnDefinitions.Add(new ColumnDefinition(_owner.CheckColumnWidth, GridUnitType.Pixel));
            _selectAll = new Checkbox
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                IsTabStop = false,
                Focusable = false
            };
            _selectAll.CheckedStateChanged += OnSelectAllChanged;
            SetColumn(_selectAll, 0);
            Children.Add(_selectAll);
            column = 1;
        }
        for (var i = 0; i < shown.Count; i++)
        {
            var definition = shown[i];
            var width = definition.Width;
            ColumnDefinitions.Add(new ColumnDefinition(width) { MinWidth = 0 });
            var isTree = i == 0;
            var cell = new TreeTableHeaderCell(_owner, definition, isTree);
            var content = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 2,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = definition.IsEndAligned ? Avalonia.Layout.HorizontalAlignment.Right : Avalonia.Layout.HorizontalAlignment.Left,
                Margin = new Thickness(isTree ? 8 : 12, 0, 12, 0)
            };
            if (isTree)
            {
                _expandAll = new TreeTableGlyphButton(_owner, 28, 10, isTabStop: true) { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                _expandAll.SetGlyph(ArcIcons.ChevronsUpDown, animate: false);
                _expandAll.Clicked += _owner.ToggleEverything;
                AutomationProperties.SetName(_expandAll, "Expand all");
                content.Children.Add(_expandAll);
                _hasExpandAll = true;
            }
            cell.Margin = new Thickness(isTree ? 0 : 0, 0, 0, 0);
            content.Children.Add(cell);
            cells.Add(cell);
            SetColumn(content, column + i);
            Children.Add(content);
        }
        Cells = cells;
    }

    /// <summary>Shows or hides the expand-all toggle (it is a spacer-less gap when nothing can expand).</summary>
    public void Sync(bool anyExpandable, bool everyOpen, bool animate, int selectionState)
    {
        if (_expandAll is { } toggle)
        {
            toggle.IsVisible = anyExpandable;
            if (anyExpandable)
            {
                toggle.SetGlyph(everyOpen ? ArcIcons.ChevronsDownUp : ArcIcons.ChevronsUpDown, animate, everyOpen ? 1 : -1);
                var name = everyOpen ? "Collapse all" : "Expand all";
                AutomationProperties.SetName(toggle, name);
                ToolTip.SetTip(toggle, name);
            }
            _lastEveryOpen = everyOpen;
        }
        if (_selectAll is { } check)
        {
            _syncingCheck = true;
            check.CheckedState = selectionState switch
            {
                2 => CheckboxState.Checked,
                1 => CheckboxState.Indeterminate,
                _ => CheckboxState.Unchecked
            };
            AutomationProperties.SetName(check, selectionState == 2 ? "Clear selection" : "Select all rows");
            _syncingCheck = false;
        }
    }

    private void OnSelectAllChanged(object? sender, CheckboxStateChangedEventArgs e)
    {
        if (!_syncingCheck)
        {
            _owner.ToggleSelectAll();
        }
    }

    internal bool HasExpandAll => _hasExpandAll;

    internal bool LastEveryOpen => _lastEveryOpen;
}
