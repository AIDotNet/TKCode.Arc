using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>How the crosshair of a <see cref="SmallMultiplesGrid"/> was moved.</summary>
internal enum SmallMultiplesCrosshairSource
{
    Pointer,
    Keyboard,
    Code,
}

/// <summary>
/// The trellis of a <see cref="SmallMultiples"/>: lays the panels out in four columns from a 640px container (two
/// below), flies them to new slots with a FLIP when the order changes, and owns the synced crosshair every panel draws.
/// It is the panels' single tab stop: arrows move the crosshair one period, Page up / Page down twelve, Home / End to
/// the first or latest, Escape clears it. Mouse hover drives the crosshair; on touch a tap or horizontal drag places it.
/// </summary>
internal sealed class SmallMultiplesGrid : Panel
{
    private const int PageStep = 12;

    private readonly List<SmallMultiplesPanel> _order = new();
    private readonly Dictionary<SmallMultiplesPanel, Point> _flipFrom = new();
    private readonly MotionValue<double> _crosshair;
    private bool _crosshairVisible;
    private int? _crosshairIndex;
    private int _columns = 4;
    private bool _touchScrubbing;

    public SmallMultiplesGrid(SmallMultiples owner)
    {
        Owner = owner;
        Focusable = true;
        IsTabStop = true;
        FocusAdorner = null;
        ClipToBounds = false;
        _crosshair = new MotionValue<double>(this, 0, MotionVectors.Double);
        _crosshair.Changed += _ => InvalidatePanels();
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Content);
    }

    public SmallMultiples Owner { get; }

    public LineChartsTextCache Text { get; } = new();

    /// <summary>The panels in display order.</summary>
    public IReadOnlyList<SmallMultiplesPanel> Ordered => _order;

    public bool IsPhone { get; private set; }

    public double PlotHeight { get; private set; } = 92;

    public int ColumnCount => _columns;

    /// <summary>The sum of every series per period (the ghost line) and its own domain.</summary>
    public double[] Totals { get; set; } = [];

    public (double Min, double Max) GhostDomain { get; set; } = (0, 1);

    public int PeriodCount { get; set; }

    /// <summary>The period under the crosshair; null when it is hidden.</summary>
    public int? CrosshairIndex => _crosshairIndex;

    /// <summary>The crosshair's drawn position in periods (fractional while it glides); NaN when hidden.</summary>
    public double CrosshairPosition => _crosshairVisible ? _crosshair.Value : double.NaN;

    /// <summary>Raised when <see cref="CrosshairIndex"/> changes, with how it was moved.</summary>
    public event Action<int?, SmallMultiplesCrosshairSource>? CrosshairChanged;

    public string FormatValue(double value) => Owner.FormatValueText(value);

    public string FormatTick(double value) => Owner.FormatTickText(value);

    public string CompareText(SmallMultiplesRow row) => Owner.CompareText(row);

    public string TrendWords(SmallMultiplesRow row) => Owner.TrendWords(row);

    // Panels ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Shows <paramref name="rows"/> in this order, reusing each key's panel. With <paramref name="flip"/> panels that
    /// change slot fly there (FLIP on the smooth spring); new panels appear in place.
    /// </summary>
    public void SetRows(IReadOnlyList<SmallMultiplesRow> rows, bool flip)
    {
        var existing = new Dictionary<string, SmallMultiplesPanel>();
        foreach (var panel in _order)
        {
            existing.TryAdd(panel.Row.Key, panel);
        }
        _flipFrom.Clear();
        if (flip)
        {
            foreach (var panel in _order)
            {
                _flipFrom[panel] = panel.Bounds.Position + panel.FlipOffset;
            }
        }
        var next = new List<SmallMultiplesPanel>(rows.Count);
        foreach (var row in rows)
        {
            if (existing.Remove(row.Key, out var panel))
            {
                panel.SetRow(row);
            }
            else
            {
                panel = new SmallMultiplesPanel(this, row);
                Children.Add(panel);
            }
            next.Add(panel);
        }
        foreach (var gone in existing.Values)
        {
            Children.Remove(gone);
            _flipFrom.Remove(gone);
        }
        _order.Clear();
        _order.AddRange(next);
        // Children follow the display order too, so screen readers and hit testing read panels in it.
        for (var i = 0; i < _order.Count; i++)
        {
            var at = Children.IndexOf(_order[i]);
            if (at != i)
            {
                Children.Move(at, i);
            }
        }
        if (!flip)
        {
            foreach (var panel in _order)
            {
                panel.StopFlip();
            }
        }
        InvalidateMeasure();
        InvalidateArrange();
    }

    public void InvalidatePanels()
    {
        foreach (var panel in _order)
        {
            panel.InvalidateVisual();
        }
    }

    /// <summary>Applies the container width: columns, plot height, the phone layout.</summary>
    public void SetContainerWidth(double width)
    {
        var columns = SmallMultiplesMath.Columns(width);
        var plot = SmallMultiplesMath.PlotHeight(width);
        var phone = width < SmallMultiplesMath.PhoneWidth;
        if (columns == _columns && plot == PlotHeight && phone == IsPhone)
        {
            return;
        }
        _columns = columns;
        PlotHeight = plot;
        IsPhone = phone;
        foreach (var panel in _order)
        {
            panel.StopFlip();
            panel.InvalidateMeasure();
            panel.InvalidateVisual();
        }
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 960;
        var height = SmallMultiplesPanel.HeightFor(IsPhone, PlotHeight);
        var cellWidth = width / Math.Max(1, _columns);
        foreach (var panel in _order)
        {
            panel.Measure(new Size(cellWidth, height));
        }
        var rows = (_order.Count + _columns - 1) / Math.Max(1, _columns);
        return new Size(width, rows * height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var height = SmallMultiplesPanel.HeightFor(IsPhone, PlotHeight);
        for (var i = 0; i < _order.Count; i++)
        {
            var column = i % _columns;
            var row = i / _columns;
            var left = Math.Round(finalSize.Width * column / _columns);
            var right = Math.Round(finalSize.Width * (column + 1) / _columns);
            var rect = new Rect(left, row * height, right - left, height);
            var panel = _order[i];
            panel.Arrange(rect);
            if (_flipFrom.Remove(panel, out var from))
            {
                panel.Flip(from - rect.Position - panel.FlipOffset);
            }
        }
        _flipFrom.Clear();
        return finalSize;
    }

    // Crosshair ------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Moves the crosshair to <paramref name="index"/> (null hides it). It appears in place, then glides between periods
    /// on a short spring (Reduced and Off land at once).
    /// </summary>
    public void SetCrosshair(int? index, SmallMultiplesCrosshairSource source)
    {
        if (index is { } i)
        {
            index = PeriodCount > 0 ? Math.Clamp(i, 0, PeriodCount - 1) : null;
        }
        if (index == _crosshairIndex)
        {
            return;
        }
        _crosshairIndex = index;
        if (index is { } target)
        {
            if (!_crosshairVisible)
            {
                _crosshairVisible = true;
                _crosshair.Jump(target);
            }
            else
            {
                _ = _crosshair.AnimateTo(target, MotionSpec.Spring(ArcMotionPresets.Quick));
            }
        }
        else
        {
            _crosshairVisible = false;
            _crosshair.Stop();
        }
        foreach (var panel in _order)
        {
            panel.UpdateAutomationName();
        }
        InvalidatePanels();
        CrosshairChanged?.Invoke(index, source);
    }

    private int? IndexAt(Point position)
    {
        if (PeriodCount <= 0)
        {
            return null;
        }
        foreach (var panel in _order)
        {
            var bounds = panel.Bounds;
            if (!bounds.Contains(position))
            {
                continue;
            }
            var plot = panel.PlotRect;
            var x = position.X - bounds.X - plot.X;
            var t = plot.Width > 0 ? Math.Clamp(x / plot.Width, 0, 1) : 1;
            return (int)Math.Round(t * (PeriodCount - 1));
        }
        return null;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetCurrentPoint(this);
        if (e.Pointer.Type == PointerType.Touch)
        {
            if (_touchScrubbing && IndexAt(point.Position) is { } touched)
            {
                SetCrosshair(touched, SmallMultiplesCrosshairSource.Pointer);
            }
            return;
        }
        if (IndexAt(point.Position) is { } index)
        {
            SetCrosshair(index, SmallMultiplesCrosshairSource.Pointer);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Pointer.Type == PointerType.Touch && IndexAt(e.GetPosition(this)) is { } index)
        {
            _touchScrubbing = true;
            SetCrosshair(index, SmallMultiplesCrosshairSource.Pointer);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _touchScrubbing = false;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _touchScrubbing = false;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        // Keyboard focus keeps its crosshair; the pointer leaving only clears what the pointer placed.
        if (!IsKeyboardFocusVisible)
        {
            SetCrosshair(null, SmallMultiplesCrosshairSource.Pointer);
        }
    }

    private bool IsKeyboardFocusVisible => IsFocused && Classes.Contains(":focus-visible");

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional && _crosshairIndex is null && PeriodCount > 0)
        {
            SetCrosshair(PeriodCount - 1, SmallMultiplesCrosshairSource.Keyboard);
        }
        Owner.OnGridFocusChanged();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (!IsPointerOver)
        {
            SetCrosshair(null, SmallMultiplesCrosshairSource.Keyboard);
        }
        Owner.OnGridFocusChanged();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || PeriodCount <= 0)
        {
            return;
        }
        if (e.Key == Key.Escape)
        {
            if (_crosshairIndex is not null)
            {
                SetCrosshair(null, SmallMultiplesCrosshairSource.Keyboard);
                e.Handled = true;
            }
            return;
        }
        var last = PeriodCount - 1;
        // Without a crosshair the first key starts from the latest period.
        var current = _crosshairIndex ?? last;
        var hidden = _crosshairIndex is null;
        int? next = e.Key switch
        {
            Key.Left => hidden ? last : current - 1,
            Key.Right => hidden ? last : current + 1,
            Key.PageUp => current - PageStep,
            Key.PageDown => current + PageStep,
            Key.Home => 0,
            Key.End => last,
            _ => null,
        };
        if (next is not { } index)
        {
            return;
        }
        SetCrosshair(Math.Clamp(index, 0, last), SmallMultiplesCrosshairSource.Keyboard);
        e.Handled = true;
    }
}
