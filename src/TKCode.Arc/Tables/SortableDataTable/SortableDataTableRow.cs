using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// One realized body row of a <see cref="SortableDataTable"/>. Cells are drawn as text (custom
/// <see cref="DataTableColumn.CellTemplate"/> content is hosted as children); the fill follows hover and
/// selection on Arc's 240ms background transition, and the row's vertical offset is a spring-driven render
/// transform so a re-sort slides rows from their old places (FLIP) without any layout.
/// Rows are recycled by the presenter; the row is the keyboard target of its checkbox.
/// </summary>
internal sealed class SortableDataTableRow : Control
{
    private readonly SortableDataTable _table;
    private readonly SortableDataTableCheckbox _checkbox;
    private readonly TranslateTransform _shift = new();
    private MotionValue<double>? _offset;
    private MotionValue<double>? _hover;
    private MotionValue<double>? _selected;
    private FormattedText?[] _texts = [];
    private string?[] _strings = [];
    private double[] _maxWidths = [];
    private int _textVersion = -1;
    private bool _textCompact;
    private Control?[] _cells = [];
    private bool _pressed;
    private bool _pressInSelect;
    private bool _hoverSelect;
    private ImmutableSolidColorBrush? _fillA;
    private ImmutableSolidColorBrush? _fillB;
    private Task<bool>? _settle;
    private int _dataVersion = -1;
    private bool _templatesPending = true;

    public SortableDataTableRow(SortableDataTable table)
    {
        _table = table;
        _checkbox = new SortableDataTableCheckbox(this);
        RenderTransform = _shift;
        Focusable = true;
        IsTabStop = false;
        ClipToBounds = false;
        FocusAdorner = null;
    }

    public int SourceIndex { get; private set; } = -1;
    public object? Key { get; private set; }
    public int Stamp { get; set; }
    public bool HasLayout { get; set; }
    public int DisplayIndex { get; set; } = -1;
    public object? Item { get; private set; }
    public bool IsSelected { get; private set; }
    public bool IsLast { get; set; }

    /// <summary>Kept realized only to finish a slide out of the viewport.</summary>
    public bool IsLeaving { get; set; }

    /// <summary>Arranged top in presenter coordinates.</summary>
    public double LayoutY { get; set; }

    public double Offset => _offset?.Value ?? 0;

    public double VisualY => LayoutY + Offset;

    public bool IsSettling => _offset is { IsAnimating: true };

    /// <summary>Binds the row to a source item. Recycled rows snap all state; the same item keeps its motion.</summary>
    public void Configure(object key, int sourceIndex, int displayIndex, object item, bool selected)
    {
        var newItem = !ReferenceEquals(item, Item);
        var stale = newItem || _dataVersion != _table.DataVersion;
        _dataVersion = _table.DataVersion;
        Key = key;
        SourceIndex = sourceIndex;
        DisplayIndex = displayIndex;
        if (newItem)
        {
            Item = item;
            if (_templatesPending)
            {
                _templatesPending = false;
                SyncTemplates();
            }
            IsSelected = selected;
            _checkbox.Reset(selected, false);
            _selected?.Jump(selected ? 1 : 0);
            _hover?.Jump(0);
            _hoverSelect = false;
            for (var i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] is { } cell)
                {
                    cell.DataContext = item;
                }
            }
        }
        else if (selected != IsSelected)
        {
            SetSelected(selected, true);
        }
        if (stale)
        {
            _textVersion = -1;
            UpdateAutomation();
            InvalidateVisual();
            if (_table.Layout.Compact)
            {
                InvalidateArrange();
            }
        }
    }

    /// <summary>Drops cached cell text (values or columns changed).</summary>
    public void InvalidateTexts()
    {
        _textVersion = -1;
        UpdateAutomation();
        InvalidateVisual();
    }

    public void Detach()
    {
        Item = null;
        Key = null;
        SourceIndex = -1;
        DisplayIndex = -1;
        IsLeaving = false;
        HasLayout = false;
        _settle = null;
        _offset?.Jump(0);
        for (var i = 0; i < _cells.Length; i++)
        {
            if (_cells[i] is { } cell)
            {
                cell.DataContext = null;
            }
        }
    }

    public void SetSelected(bool selected, bool animate)
    {
        if (selected == IsSelected)
        {
            return;
        }
        IsSelected = selected;
        _checkbox.Set(selected, false, animate);
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!animate || level == ArcMotionLevel.Off)
        {
            _selected?.Jump(selected ? 1 : 0);
        }
        else
        {
            _selected ??= Create(selected ? 0 : 1);
            _ = _selected.AnimateTo(selected ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
        }
        UpdateAutomation();
        InvalidateVisual();
    }

    /// <summary>FLIP: starts at <paramref name="from"/> (relative to the layout slot) and springs home.</summary>
    public void SlideFrom(double from, bool animate)
    {
        _offset ??= CreateOffset();
        if (!animate || Math.Abs(from) < 0.5)
        {
            _offset.Jump(0);
            _settle = null;
            return;
        }
        _offset.Jump(from);
        _settle = _offset.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
    }

    /// <summary>Keeps the current visual position while the layout slot moves by <paramref name="slotDelta"/>.</summary>
    public void Rebase(double slotDelta)
    {
        if (_offset is null || Math.Abs(slotDelta) < 0.01)
        {
            return;
        }
        var velocity = _offset.Velocity;
        var current = _offset.Value - slotDelta;
        _offset.Jump(current);
        _settle = _offset.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth), velocity);
    }

    public Task<bool>? Settle => _settle;

    private MotionValue<double> CreateOffset()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += v => _shift.Y = v;
        return value;
    }

    private MotionValue<double> Create(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    public void UpdateAutomation()
    {
        var name = _table.RowName(this);
        AutomationProperties.SetName(this, _table.IsSelectable ? $"Select {name}" : name);
        AutomationProperties.SetItemStatus(this, IsSelected ? "selected" : string.Empty);
    }

    #region Templated cells

    /// <summary>Creates hosted controls for columns with a <see cref="DataTableColumn.CellTemplate"/>.</summary>
    public void SyncTemplates()
    {
        if (Item is null)
        {
            // Built with the first item, so templates that read their data at build time start right.
            _templatesPending = true;
            return;
        }
        var count = _table.ColumnCount;
        if (_cells.Length == count)
        {
            var same = true;
            for (var i = 0; i < count && same; i++)
            {
                same = (_cells[i] is not null) == (_table.ColumnAt(i)?.CellTemplate is not null);
            }
            if (same)
            {
                return;
            }
        }
        foreach (var old in _cells)
        {
            if (old is not null)
            {
                VisualChildren.Remove(old);
                LogicalChildren.Remove(old);
            }
        }
        _cells = new Control?[count];
        for (var i = 0; i < count; i++)
        {
            if (_table.ColumnAt(i)?.CellTemplate is { } template && template.Build(Item) is { } cell)
            {
                cell.DataContext = Item;
                _cells[i] = cell;
                LogicalChildren.Add(cell);
                VisualChildren.Add(cell);
            }
        }
        _textVersion = -1;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var layout = _table.Layout;
        for (var i = 0; i < _cells.Length; i++)
        {
            if (_cells[i] is { } cell)
            {
                var width = layout.Compact ? double.PositiveInfinity : Math.Max(0, layout.ColumnWidth(i) - _table.LeadingPadding(i) - _table.CellInlinePadding);
                cell.Measure(new Size(width, layout.RowHeight));
            }
        }
        return new Size(layout.Width, layout.RowHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var layout = _table.Layout;
        if (layout.Compact && Item is not null)
        {
            LayoutCompact(finalSize);
        }
        for (var i = 0; i < _cells.Length; i++)
        {
            if (_cells[i] is { } cell)
            {
                cell.Arrange(CellContentRect(i, cell.DesiredSize, finalSize));
            }
        }
        return finalSize;
    }

    private Rect CellContentRect(int column, Size desired, Size size)
    {
        var layout = _table.Layout;
        if (layout.Compact)
        {
            var slot = _compactSlots.Length > column ? _compactSlots[column] : default;
            var slotHeight = Math.Min(desired.Height, size.Height);
            return slot.Visible
                ? new Rect(slot.X, slot.Y + (slot.Height - slotHeight) / 2, slot.Width, slotHeight)
                : default;
        }
        var x = layout.ColumnX(column) + _table.LeadingPadding(column);
        var width = Math.Max(0, layout.ColumnWidth(column) - _table.LeadingPadding(column) - _table.CellInlinePadding);
        var height = Math.Min(desired.Height, size.Height);
        return new Rect(x, (size.Height - height) / 2, width, height);
    }

    #endregion

    #region Input

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        AnimateHover(true);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Touch || !_table.IsSelectable)
        {
            return;
        }
        var inSelect = InSelectCell(e.GetPosition(this));
        if (inSelect != _hoverSelect)
        {
            _hoverSelect = inSelect;
            _checkbox.SetHover(inSelect);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        AnimateHover(false);
        if (_hoverSelect)
        {
            _hoverSelect = false;
            _checkbox.SetHover(false);
        }
        if (_pressed)
        {
            _pressed = false;
            _checkbox.SetPressed(false);
        }
    }

    private bool InSelectCell(Point point)
    {
        var layout = _table.Layout;
        return _table.IsSelectable && (layout.Compact ? point.X < 44 : point.X < layout.SelectWidth);
    }

    private void AnimateHover(bool hover)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off)
        {
            _hover?.Jump(hover ? 1 : 0);
            InvalidateVisual();
            return;
        }
        _hover ??= Create(0);
        _ = _hover.AnimateTo(hover ? 1 : 0, level == ArcMotionLevel.Reduced
            ? MotionSpec.Instant
            : MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!_table.IsSelectable || e.Handled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressed = true;
        _pressInSelect = InSelectCell(e.GetPosition(this));
        if (_pressInSelect)
        {
            _checkbox.SetPressed(true);
        }
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        _pressed = false;
        if (_pressInSelect)
        {
            _checkbox.SetPressed(false);
        }
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            _table.OnRowClicked(this, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled)
        {
            e.Handled = _table.NavigateFromRow(this, e.Key, e.KeyModifiers);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _table.OnRowFocused(this);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    #endregion

    #region Rendering

    private struct CompactSlot
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;
        public bool Visible;
    }

    private CompactSlot[] _compactSlots = [];

    private void EnsureTexts()
    {
        var palette = _table.Palette;
        var layout = _table.Layout;
        var count = _table.ColumnCount;
        if (_texts.Length != count)
        {
            _texts = new FormattedText?[count];
            _strings = new string?[count];
            _maxWidths = new double[count];
            _textVersion = -1;
        }
        if (_textVersion == palette.Version && _textCompact == layout.Compact && Item is not null)
        {
            return;
        }
        _textVersion = palette.Version;
        _textCompact = layout.Compact;
        _compactSize = default;
        for (var i = 0; i < count; i++)
        {
            if (Item is null || i < _cells.Length && _cells[i] is not null)
            {
                _texts[i] = null;
                continue;
            }
            var text = _table.CellText(SourceIndex, i);
            var primary = i == 0;
            var small = layout.Compact && !primary && !_table.IsCompactLeadNumeric(i);
            var typeface = layout.Compact && primary ? palette.Medium : palette.Regular;
            var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface,
                small ? palette.SmallFontSize : palette.FontSize, null)
            {
                MaxLineCount = 1,
                Trimming = TextTrimming.CharacterEllipsis
            };
            if (_table.IsNumericColumn(i))
            {
                formatted.SetFontFeatures(SortableDataTablePalette.TabularNumbers);
            }
            _strings[i] = text;
            _texts[i] = formatted;
            _maxWidths[i] = -1;
        }
    }

    private FormattedText? TextAt(int column, double maxWidth)
    {
        var text = _texts[column];
        if (text is null)
        {
            return null;
        }
        maxWidth = Math.Max(1, maxWidth);
        if (Math.Abs(_maxWidths[column] - maxWidth) > 0.5)
        {
            _maxWidths[column] = maxWidth;
            text.MaxTextWidth = maxWidth;
        }
        return text;
    }

    public override void Render(DrawingContext context)
    {
        if (Item is null)
        {
            return;
        }
        var palette = _table.Palette;
        var layout = _table.Layout;
        var size = Bounds.Size;
        EnsureTexts();
        var hover = _hover?.Value ?? 0;
        var selected = _selected?.Value ?? (IsSelected ? 1 : 0);
        var sorted = layout.Compact ? -1 : _table.SortedColumnIndex;
        var fill = Brush(ref _fillA, palette.RowFill(0, hover, selected));
        if (sorted < 0)
        {
            context.FillRectangle(fill, new Rect(size));
        }
        else
        {
            var x = layout.ColumnX(sorted);
            var width = layout.ColumnWidth(sorted);
            context.FillRectangle(fill, new Rect(0, 0, x, size.Height));
            context.FillRectangle(fill, new Rect(x + width, 0, Math.Max(0, size.Width - x - width), size.Height));
            context.FillRectangle(Brush(ref _fillB, palette.RowFill(1, hover, selected)), new Rect(x, 0, width, size.Height));
        }
        if (!IsLast)
        {
            context.DrawLine(palette.DividerPen, new Point(0, size.Height - 0.5), new Point(size.Width, size.Height - 0.5));
        }
        var focusVisible = IsFocused && Classes.Contains(":focus-visible");
        if (layout.Compact)
        {
            RenderCompact(context, palette, size);
        }
        else
        {
            for (var i = 0; i < _texts.Length; i++)
            {
                var lead = _table.LeadingPadding(i);
                var inner = layout.ColumnWidth(i) - lead - _table.CellInlinePadding;
                if (TextAt(i, inner) is not { } text)
                {
                    continue;
                }
                text.SetForegroundBrush(palette.ForegroundBrush);
                var x = _table.IsNumericColumn(i)
                    ? layout.ColumnX(i) + layout.ColumnWidth(i) - _table.CellInlinePadding - Math.Min(text.WidthIncludingTrailingWhitespace, inner)
                    : layout.ColumnX(i) + lead;
                context.DrawText(text, new Point(x, Math.Round((size.Height - text.Height) / 2)));
            }
        }
        if (_table.IsSelectable)
        {
            var boxX = layout.Compact ? 4 + (36 - SortableDataTableCheckbox.Extent) / 2 : _table.CellInlinePadding;
            var boxY = Math.Round((size.Height - SortableDataTableCheckbox.Extent) / 2);
            _checkbox.Render(context, new Point(boxX, boxY), palette, focusVisible);
        }
        else if (focusVisible && palette.FocusPen is { } focus)
        {
            context.DrawRectangle(null, focus, new Rect(size).Deflate(2), 4, 4);
        }
    }

    /// <summary>
    /// Phone layout: the first column with its first numeric column on one line, the remaining details below
    /// (12px secondary text; the sorted value at full contrast). Computed at arrange so hosted cells land in place.
    /// </summary>
    private void LayoutCompact(Size size)
    {
        EnsureTexts();
        var palette = _table.Palette;
        var count = _texts.Length;
        if (_compactSlots.Length != count)
        {
            _compactSlots = new CompactSlot[count];
        }
        _compactSize = size;
        var left = _table.IsSelectable ? 44.0 : 16.0;
        var right = size.Width - 16;
        var lead = _table.CompactLeadNumericIndex;
        var line1 = palette.FontSize * 1.43;
        var line2 = palette.SmallFontSize * 1.45;
        var hasSecond = false;
        for (var i = 1; i < count; i++)
        {
            if (i != lead)
            {
                hasSecond = true;
                break;
            }
        }
        var top = Math.Round((size.Height - (hasSecond ? line1 + 2 + line2 : line1)) / 2);
        var leadWidth = 0.0;
        for (var i = 0; i < count; i++)
        {
            _compactSlots[i] = default;
        }
        if (lead > 0 && lead < count)
        {
            var room = (right - left) / 2;
            var width = Width(lead, room);
            _compactSlots[lead] = new CompactSlot { X = right - width, Y = top, Width = width, Height = line1, Visible = true };
            leadWidth = width + 8;
        }
        if (count > 0)
        {
            var primaryWidth = Math.Max(1, right - left - leadWidth);
            _compactSlots[0] = new CompactSlot { X = left, Y = top, Width = primaryWidth, Height = line1, Visible = true };
            TextAt(0, primaryWidth);
        }
        var x = left;
        var y = top + line1 + 2;
        for (var i = 1; i < count; i++)
        {
            if (i == lead)
            {
                continue;
            }
            var room = right - x;
            if (room < 8)
            {
                break;
            }
            var width = Width(i, room);
            _compactSlots[i] = new CompactSlot { X = x, Y = y, Width = width, Height = line2, Visible = true };
            x += width + 8;
        }

        double Width(int column, double room)
        {
            if (column < _cells.Length && _cells[column] is { } cell)
            {
                return Math.Min(cell.DesiredSize.Width, room);
            }
            return TextAt(column, room) is { } text ? Math.Min(text.WidthIncludingTrailingWhitespace, room) : 0;
        }
    }

    private Size _compactSize;

    private void RenderCompact(DrawingContext context, SortableDataTablePalette palette, Size size)
    {
        if (_compactSize != size || _compactSlots.Length != _texts.Length)
        {
            LayoutCompact(size);
        }
        var sorted = _table.SortedColumnIndex;
        for (var i = 0; i < _texts.Length; i++)
        {
            var slot = _compactSlots[i];
            if (!slot.Visible || _texts[i] is not { } text)
            {
                continue;
            }
            var emphasized = i == 0 || _table.IsCompactLeadNumeric(i) || i == sorted;
            text.SetForegroundBrush(emphasized ? palette.ForegroundBrush : palette.TextSecondaryBrush);
            context.DrawText(text, new Point(slot.X, slot.Y + (slot.Height - text.Height) / 2));
        }
    }

    private static ImmutableSolidColorBrush Brush(ref ImmutableSolidColorBrush? cache, Color color)
    {
        if (cache is null || cache.Color != color)
        {
            cache = new ImmutableSolidColorBrush(color);
        }
        return cache;
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    private sealed class RowPeer : ControlAutomationPeer, IToggleProvider
    {
        public RowPeer(SortableDataTableRow owner) : base(owner)
        {
        }

        private SortableDataTableRow Row => (SortableDataTableRow)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() =>
            Row._table.IsSelectable ? AutomationControlType.CheckBox : AutomationControlType.DataItem;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public ToggleState ToggleState => Row.IsSelected ? ToggleState.On : ToggleState.Off;

        public void Toggle() => Row._table.OnRowClicked(Row, false);
    }
}
