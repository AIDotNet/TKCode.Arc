using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>What a slider part exposes to assistive technology (the peer reads it).</summary>
internal interface IUsageSlider
{
    double Minimum { get; }

    double Maximum { get; }

    double Value { get; }

    double SmallChange { get; }

    double LargeChange { get; }

    void SetValue(double value);
}

/// <summary>A range-value peer for the plot's day inspector and the budget handle.</summary>
internal sealed class UsageSliderPeer(Control owner, IUsageSlider slider) : ControlAutomationPeer(owner), Avalonia.Automation.Provider.IRangeValueProvider
{
    public bool IsReadOnly => false;

    public double Maximum => slider.Maximum;

    public double Minimum => slider.Minimum;

    public double Value => slider.Value;

    public double SmallChange => slider.SmallChange;

    public double LargeChange => slider.LargeChange;

    public void SetValue(double value) => slider.SetValue(value);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}

internal sealed partial class UsageForecastPlot : IUsageSlider
{
    double IUsageSlider.Minimum => 1;

    double IUsageSlider.Maximum => Math.Max(1, Series.Days);

    double IUsageSlider.Value => KeyDay;

    double IUsageSlider.SmallChange => 1;

    double IUsageSlider.LargeChange => 7;

    void IUsageSlider.SetValue(double value) => InspectRequested?.Invoke((int)Math.Clamp(Math.Round(value), 1, Math.Max(1, Series.Days)), true);
}

/// <summary>
/// The 32px band around the budget line (a slider): the whole band takes the pointer (an ns-resize cursor), a drag moves the
/// budget relative to where it started and snaps to the step, beyond the range the line follows squeezed and springs back
/// on release; the arrow keys step it, Page keys move five steps and Home / End jump to the ends. It draws only its focus
/// ring (a square one around the whole band); the line and the capsule belong to the plot.
/// </summary>
internal sealed class UsageBudgetHandle : Control, IUsageSlider
{
    public static readonly StyledProperty<double> BandHeightProperty =
        AvaloniaProperty.Register<UsageBudgetHandle, double>(nameof(BandHeight), 32);

    private readonly AnalyticsPalette _palette = new();
    private readonly TranslateTransform _transform = new();
    private bool _dragging;
    private double _startValue;
    private double _startY;
    private double _lastValue;

    static UsageBudgetHandle()
    {
        FocusableProperty.OverrideDefaultValue<UsageBudgetHandle>(true);
        AffectsMeasure<UsageBudgetHandle>(BandHeightProperty);
    }

    public UsageBudgetHandle()
    {
        Cursor = new Avalonia.Input.Cursor(StandardCursorType.SizeNorthSouth);
        RenderTransform = _transform;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        ActualThemeVariantChanged += (_, _) => _palette.Refresh(this);
    }

    /// <summary>The band's height (32).</summary>
    public double BandHeight { get => GetValue(BandHeightProperty); set => SetValue(BandHeightProperty, value); }

    /// <summary>The plot this handle edits.</summary>
    public UsageForecastPlot? Plot { get; set; }

    /// <summary>True while a drag is running.</summary>
    public bool IsDragging => _dragging;

    /// <summary>Moves the band so it is centred on <paramref name="centerY"/> (whole pixels).</summary>
    public void Place(double centerY) => _transform.Y = centerY - BandHeight / 2;

    internal double Offset => _transform.Y;

    protected override Size MeasureOverride(Size availableSize) => new(0, BandHeight);

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (!AnalyticsFocus.IsVisible(this))
        {
            return;
        }
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        AnalyticsFocus.Draw(context, _palette, new Rect(Bounds.Size));
    }

    private (double Min, double Max, double Step) Range
    {
        get
        {
            var d = Plot?.Data;
            return d is null ? (0, 1, 1) : (d.BudgetMin, Math.Max(d.BudgetMin, d.BudgetMax), d.BudgetStep);
        }
    }

    private double Snapped(double raw)
    {
        var (min, max, step) = Range;
        return Math.Clamp(UsageForecastEngine.Snap(Math.Clamp(raw, min, max), min, step), min, max);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        Plot?.SetHandleHover(true, _dragging);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!_dragging)
        {
            Plot?.SetHandleHover(false, false);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Plot is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _dragging = true;
        _startValue = Plot.Budget;
        _lastValue = _startValue;
        _startY = e.GetPosition(Plot).Y;
        e.Pointer.Capture(this);
        Focus();
        Plot.SetHandleHover(true, true);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging || Plot is null)
        {
            return;
        }
        var ppu = Math.Max(1e-9, Plot.PixelsPerUnit);
        // Relative to where the press happened: pressing does not make the line jump to the pointer.
        var raw = _startValue - (e.GetPosition(Plot).Y - _startY) / ppu;
        var (min, max, _) = Range;
        var overshoot = raw > max ? (raw - max) * ppu : raw < min ? -(min - raw) * ppu : 0;
        _lastValue = Snapped(raw);
        Plot.DragTo(_lastValue, overshoot);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndDrag(e.Pointer);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndDrag(null);
    }

    private void EndDrag(IPointer? pointer)
    {
        if (!_dragging)
        {
            return;
        }
        _dragging = false;
        pointer?.Capture(null);
        Plot?.SetHandleHover(IsPointerOver, false);
        Plot?.CommitBudget(_lastValue);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || Plot is null)
        {
            return;
        }
        var (min, max, step) = Range;
        var current = Plot.Budget;
        double? next = e.Key switch
        {
            Key.Up or Key.Right => current + step,
            Key.Down or Key.Left => current - step,
            Key.PageUp => current + 5 * step,
            Key.PageDown => current - 5 * step,
            Key.Home => min,
            Key.End => max,
            _ => null
        };
        if (next is { } value)
        {
            var snapped = Snapped(value);
            if (Math.Abs(snapped - current) > 1e-9)
            {
                Plot.CommitBudget(snapped);
            }
            e.Handled = true;
        }
    }

    double IUsageSlider.Minimum => Range.Min;

    double IUsageSlider.Maximum => Range.Max;

    double IUsageSlider.Value => Plot?.Budget ?? 0;

    double IUsageSlider.SmallChange => Range.Step;

    double IUsageSlider.LargeChange => Range.Step * 5;

    void IUsageSlider.SetValue(double value) => Plot?.CommitBudget(Snapped(value));

    protected override AutomationPeer OnCreateAutomationPeer() => new UsageSliderPeer(this, this);
}
