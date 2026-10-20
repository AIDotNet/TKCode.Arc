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
/// One header cell of a <see cref="SortableDataTable"/>, drawn directly: the label, the hint arrow that shows
/// on hover or keyboard focus, and the sort arrow that fades and scales in where a column becomes sorted and
/// flips on a snappy spring when the direction changes. Column index -1 is the Select all checkbox.
/// </summary>
internal sealed class SortableDataTableHeaderCell : Control
{
    private const double GlyphExtent = 16;
    private const double GlyphGap = 6;
    private static readonly Geometry s_arrow = ArcIcons.ArrowUp;

    private readonly SortableDataTable _table;
    private readonly SortableDataTableCheckbox? _checkbox;
    private FormattedText? _label;
    private string? _labelText;
    private int _labelVersion = -1;
    private double _labelMax = -1;
    private bool _labelSmall;
    private MotionValue<double>? _hover;
    private MotionValue<double>? _sorted;
    private MotionValue<double>? _arrow;
    private MotionValue<double>? _arrowScale;
    private MotionValue<double>? _rotation;
    private bool _active;
    private bool _descending;
    private long _hiddenAt = long.MinValue;
    private bool _pressed;
    private ImmutablePen? _glyphPen;
    private Color _glyphPenColor;
    private ImmutablePen? _hintPen;
    private Color _hintPenColor;

    public SortableDataTableHeaderCell(SortableDataTable table, int columnIndex)
    {
        _table = table;
        ColumnIndex = columnIndex;
        if (columnIndex < 0)
        {
            _checkbox = new SortableDataTableCheckbox(this);
        }
        ClipToBounds = false;
        FocusAdorner = null;
    }

    /// <summary>Index into the table's columns, or -1 for Select all.</summary>
    public int ColumnIndex { get; }

    public bool IsSelectAll => ColumnIndex < 0;

    public DataTableColumn? Column => ColumnIndex >= 0 ? _table.ColumnAt(ColumnIndex) : null;

    public bool IsSortableColumn => Column is { IsSortable: true };

    public bool IsActive => _active;

    public DataTableSortDirection Direction => !_active ? DataTableSortDirection.None
        : _descending ? DataTableSortDirection.Descending : DataTableSortDirection.Ascending;

    internal SortableDataTableCheckbox? Checkbox => _checkbox;

    /// <summary>Applies the sort state; <paramref name="animate"/> is false on the first layout and on rebuilds.</summary>
    public void SetSort(bool active, bool descending, bool animate)
    {
        var full = animate && ArcMotion.IsFull(this);
        var level = ArcMotion.GetEffectiveLevel(this);
        var wasActive = _active;
        if (active)
        {
            // Turn only while the arrow is visible; a freshly shown arrow starts pointing the right way.
            var visible = wasActive || Environment.TickCount64 - _hiddenAt < 160;
            var target = descending ? 180.0 : 0.0;
            if (visible && full)
            {
                _rotation ??= Create(_descending ? 180 : 0);
                _ = _rotation.AnimateTo(target, MotionSpec.Spring(ArcSpring.Snappy));
            }
            else
            {
                (_rotation ??= Create(target)).Jump(target);
            }
        }
        else if (wasActive)
        {
            _hiddenAt = Environment.TickCount64;
        }
        _active = active;
        _descending = descending;
        if (wasActive != active)
        {
            _arrow ??= Create(wasActive ? 1 : 0);
            _arrowScale ??= Create(wasActive ? 1 : 0.6);
            _sorted ??= Create(wasActive ? 1 : 0);
            if (!animate || level == ArcMotionLevel.Off)
            {
                _arrow.Jump(active ? 1 : 0);
                _arrowScale.Jump(active ? 1 : 0.6);
                _sorted.Jump(active ? 1 : 0);
            }
            else if (active)
            {
                _ = _arrow.AnimateTo(1, level == ArcMotionLevel.Reduced
                    ? MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard)
                    : MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
                if (full)
                {
                    _ = _arrowScale.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
                }
                else
                {
                    _arrowScale.Jump(1);
                }
                _ = _sorted.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
            }
            else
            {
                var leave = MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);
                _ = _arrow.AnimateTo(0, leave);
                if (full)
                {
                    _ = _arrowScale.AnimateTo(0.6, leave);
                }
                else
                {
                    _arrowScale.Jump(0.6);
                }
                _ = _sorted.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
            }
        }
        UpdateAutomation();
        InvalidateVisual();
    }

    public void UpdateAutomation()
    {
        if (IsSelectAll)
        {
            AutomationProperties.SetName(this, _table.SelectAllLabel);
            return;
        }
        var column = Column;
        if (column is null)
        {
            return;
        }
        var label = column.Header;
        if (column.IsSortable)
        {
            var state = !_active ? string.Empty : _descending ? ", currently descending" : ", currently ascending";
            AutomationProperties.SetName(this, $"Sort by {label}{state}");
            AutomationProperties.SetItemStatus(this, !_active ? "none" : _descending ? "descending" : "ascending");
        }
        else
        {
            AutomationProperties.SetName(this, label);
        }
    }

    private MotionValue<double> Create(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

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

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        AnimateHover(false);
        if (_pressed)
        {
            _pressed = false;
            _checkbox?.SetPressed(false);
        }
    }

    private void AnimateHover(bool hover)
    {
        if (_checkbox is not null)
        {
            _checkbox.SetHover(hover);
            return;
        }
        if (!IsSortableColumn)
        {
            return;
        }
        _hover ??= Create(0);
        var spec = ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full
            ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)
            : MotionSpec.Instant;
        _ = _hover.AnimateTo(hover ? 1 : 0, spec);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!Focusable || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressed = true;
        e.Handled = true;
        _checkbox?.SetPressed(true);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        _pressed = false;
        _checkbox?.SetPressed(false);
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            Focus(NavigationMethod.Pointer);
            Activate();
        }
    }

    public void Activate()
    {
        if (IsSelectAll)
        {
            _table.ToggleAll();
        }
        else if (Column is { IsSortable: true } column)
        {
            _table.CycleSort(column);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            Activate();
            return;
        }
        e.Handled = _table.NavigateFromHeader(this, e.Key);
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

    #endregion

    #region Rendering

    public double MeasureNaturalWidth()
    {
        var palette = _table.Palette;
        if (IsSelectAll)
        {
            return _table.SelectColumnWidth;
        }
        var column = Column;
        if (column is null)
        {
            return 0;
        }
        var label = new FormattedText(column.Header, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, palette.Medium,
            palette.FontSize, null);
        var width = label.WidthIncludingTrailingWhitespace + _table.CellInlinePadding * 2;
        if (column.IsSortable)
        {
            width += GlyphGap + GlyphExtent;
        }
        return Math.Ceiling(width);
    }

    private FormattedText EnsureLabel(string text, double maxWidth, bool small)
    {
        var palette = _table.Palette;
        if (_label is null || _labelText != text || _labelVersion != palette.Version || small != _labelSmall)
        {
            _labelText = text;
            _labelVersion = palette.Version;
            _labelSmall = small;
            _label = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, palette.Medium,
                palette.FontSize, null)
            {
                MaxLineCount = 1,
                Trimming = TextTrimming.CharacterEllipsis
            };
            _labelMax = -1;
        }
        if (Math.Abs(_labelMax - maxWidth) > 0.5)
        {
            _labelMax = maxWidth;
            _label.MaxTextWidth = Math.Max(1, maxWidth);
        }
        return _label;
    }

    public override void Render(DrawingContext context)
    {
        var palette = _table.Palette;
        var size = Bounds.Size;
        var compact = _table.IsCompact;
        var focusVisible = IsFocused && Classes.Contains(":focus-visible");
        var sorted = _sorted?.Value ?? (_active ? 1 : 0);
        if (_checkbox is not null)
        {
            if (!compact)
            {
                context.FillRectangle(palette.SurfaceBrush, new Rect(size));
            }
            var y = Math.Round((size.Height - SortableDataTableCheckbox.Extent) / 2);
            var x = compact ? Math.Round((size.Width - SortableDataTableCheckbox.Extent) / 2) : _table.CellInlinePadding;
            _checkbox.Render(context, new Point(x, y), palette, focusVisible);
            return;
        }
        var column = Column;
        if (column is null)
        {
            return;
        }
        var hover = _hover?.Value ?? 0;
        var numeric = _table.IsNumericColumn(ColumnIndex) && !compact;
        if (compact)
        {
            // Phone layout: a pill per sortable column; the sorted one sits on surface-muted.
            if (sorted > 0.001)
            {
                var tint = Cached(ref _fillBrush, TablesBDrawing.Lerp(Color.FromArgb(0, palette.ColumnTint.R, palette.ColumnTint.G, palette.ColumnTint.B), palette.ColumnTint, sorted));
                context.DrawRectangle(tint, null, new Rect(size), _table.ControlRadius, _table.ControlRadius);
            }
        }
        else
        {
            var fill = sorted > 0.999 ? palette.ColumnTintBrush : sorted < 0.001 ? palette.SurfaceBrush
                : Cached(ref _fillBrush, TablesBDrawing.Lerp(palette.Surface, palette.ColumnTint, sorted));
            context.FillRectangle(fill, new Rect(size));
        }
        var sortable = column.IsSortable;
        var padLeft = compact ? 10 : _table.LeadingPadding(ColumnIndex);
        var padRight = compact ? 10 : _table.CellInlinePadding;
        var glyphSpace = sortable ? GlyphGap + GlyphExtent : 0;
        var available = Math.Max(1, size.Width - padLeft - padRight - glyphSpace);
        var label = EnsureLabel(column.Header, available, false);
        var labelColor = TablesBDrawing.Lerp(palette.TextMuted, palette.Foreground, Math.Max(sorted, hover));
        label.SetForegroundBrush(Cached(ref _labelBrush, labelColor));
        var labelWidth = Math.Min(label.WidthIncludingTrailingWhitespace, available);
        var innerWidth = labelWidth + glyphSpace;
        var innerX = numeric ? size.Width - padRight - innerWidth : padLeft;
        var midY = size.Height / 2;
        var labelX = numeric ? innerX + glyphSpace : innerX;
        context.DrawText(label, new Point(labelX, midY - label.Height / 2));
        if (sortable)
        {
            var glyphX = numeric ? innerX : innerX + labelWidth + GlyphGap;
            var glyphRect = new Rect(glyphX, midY - GlyphExtent / 2, GlyphExtent, GlyphExtent);
            // The hint shows on hover or keyboard focus while the column is not sorted.
            var hint = Math.Max(hover, focusVisible ? 1 : 0) * 0.6 * (1 - sorted);
            if (hint > 0.001)
            {
                DrawGlyph(context, glyphRect, 0, 1, hint, ref _hintPen, ref _hintPenColor, palette.TextMuted);
            }
            var arrow = Math.Clamp(_arrow?.Value ?? (_active ? 1 : 0), 0, 1);
            if (arrow > 0.001)
            {
                DrawGlyph(context, glyphRect, _rotation?.Value ?? (_descending ? 180 : 0), _arrowScale?.Value ?? 1, arrow,
                    ref _glyphPen, ref _glyphPenColor, palette.Foreground);
            }
        }
        if (focusVisible && palette.FocusPen is { } focus)
        {
            var ring = compact
                ? new Rect(size).Inflate(2)
                : new Rect(innerX - 8, midY - 14, innerWidth + 16, 28);
            context.DrawRectangle(null, focus, ring, compact ? _table.ControlRadius + 2 : 10, compact ? _table.ControlRadius + 2 : 10);
        }
    }

    private ImmutableSolidColorBrush? _fillBrush;
    private ImmutableSolidColorBrush? _labelBrush;

    private static ImmutableSolidColorBrush Cached(ref ImmutableSolidColorBrush? cache, Color color)
    {
        if (cache is null || cache.Color != color)
        {
            cache = new ImmutableSolidColorBrush(color);
        }
        return cache;
    }

    private static void DrawGlyph(DrawingContext context, Rect rect, double rotation, double scale, double opacity,
                                  ref ImmutablePen? pen, ref Color penColor, Color color)
    {
        if (pen is null || penColor != color)
        {
            penColor = color;
            // Lucide stroke 1.75 at 16px: 2.625 viewBox units.
            pen = new ImmutablePen(new ImmutableSolidColorBrush(color), 1.75 * 24 / GlyphExtent, lineCap: PenLineCap.Round,
                lineJoin: PenLineJoin.Round);
        }
        var center = rect.Center;
        var factor = GlyphExtent / 24 * scale;
        var matrix = Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(factor, factor) *
                     Matrix.CreateRotation(rotation * Math.PI / 180) * Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushOpacity(opacity))
        using (context.PushTransform(matrix))
        {
            context.DrawGeometry(null, pen, s_arrow);
        }
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() =>
        IsSelectAll ? new SelectAllPeer(this) : new HeaderPeer(this);

    private sealed class HeaderPeer : ControlAutomationPeer, IInvokeProvider
    {
        public HeaderPeer(SortableDataTableHeaderCell owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.HeaderItem;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public void Invoke() => ((SortableDataTableHeaderCell)Owner).Activate();
    }

    private sealed class SelectAllPeer : ControlAutomationPeer, IToggleProvider
    {
        public SelectAllPeer(SortableDataTableHeaderCell owner) : base(owner)
        {
        }

        private SortableDataTableHeaderCell Cell => (SortableDataTableHeaderCell)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.CheckBox;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public ToggleState ToggleState => Cell.Checkbox is not { } box ? ToggleState.Off
            : box.IsMixed ? ToggleState.Indeterminate : box.IsChecked ? ToggleState.On : ToggleState.Off;

        public void Toggle() => Cell.Activate();
    }
}
