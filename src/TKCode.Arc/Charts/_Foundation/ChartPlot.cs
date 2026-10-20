using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The self-drawn surface of a chart (template part <c>PART_Plot</c>). It measures to the owner's plot height,
/// draws through the owner in one <see cref="Render"/> pass (StreamGeometry, cached per data change), and routes
/// pointer and keyboard input to the owner. It is the keyboard stop for reading values (an adjustable control
/// whose value text reads the active point), so drawn shapes stay out of the accessibility tree.
/// </summary>
internal sealed class ChartPlot : Control, Avalonia.Rendering.ICustomHitTest
{
    /// <summary>The whole surface takes the pointer, not only the drawn shapes.</summary>
    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    private ChartBase? _owner;
    private readonly ChartAnnouncer _announcer = new();

    public ChartPlot()
    {
        ClipToBounds = false;
        VisualChildren.Add(_announcer);
        LogicalChildren.Add(_announcer);
    }

    /// <summary>Sends text to the polite live region (Arc's <c>aria-live="polite"</c>).</summary>
    internal void Announce(string text) => AutomationProperties.SetName(_announcer, text);

    internal ChartBase? Owner
    {
        get => _owner;
        set
        {
            _owner = value;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <summary>Which part of a multi-part chart this surface draws (BrushChart's overview strip is 1).</summary>
    internal int Role { get; set; }

    internal bool IsFocusVisible => IsFocused && Classes.Contains(":focus-visible");

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_owner is null)
        {
            return default;
        }
        _owner.EnsureData();
        var width = _owner.MeasurePlotWidth(this, double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width);
        _announcer.Measure(new Size(1, 1));
        return new Size(width, _owner.MeasurePlotHeight(this, width));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _announcer.Arrange(new Rect(0, 0, 1, 1));
        return finalSize;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _owner?.OnPlotResized(this, e.NewSize);
    }

    public override void Render(DrawingContext context) => _owner?.RenderPlotSurface(this, context, Bounds.Size);

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _owner?.OnPlotPointerMoved(this, e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _owner?.OnPlotPointerPressed(this, e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _owner?.OnPlotPointerReleased(this, e);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _owner?.OnPlotPointerExited(this, e);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _owner?.OnPlotPointerCaptureLost(this, e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        _owner?.OnPlotKeyDown(this, e);
        if (!e.Handled)
        {
            base.OnKeyDown(e);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _owner?.OnPlotFocusChanged(this, true, e.NavigationMethod == NavigationMethod.Tab || e.NavigationMethod == NavigationMethod.Directional);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _owner?.OnPlotFocusChanged(this, false, false);
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ChartPlotAutomationPeer(this);

    /// <summary>Raises the value-changed automation event after the active point moves.</summary>
    internal void NotifyValueChanged(double oldValue, double newValue)
    {
        if (ControlAutomationPeer.FromElement(this) is { } peer)
        {
            peer.RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, oldValue, newValue);
        }
    }
}

/// <summary>An invisible polite live region: its name is the latest announcement.</summary>
internal sealed class ChartAnnouncer : Control
{
    public ChartAnnouncer()
    {
        IsHitTestVisible = false;
        Focusable = false;
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ChartAnnouncerAutomationPeer(this);
}

internal sealed class ChartAnnouncerAutomationPeer : ControlAutomationPeer
{
    public ChartAnnouncerAutomationPeer(ChartAnnouncer owner) : base(owner)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => false;
}

/// <summary>
/// Adjustable (slider-like) peer for a chart plot: the name says what is explored, the value text reads the
/// active point and every visible series (Avalonia's range pattern has no value text, so the value pattern
/// carries it).
/// </summary>
internal sealed class ChartPlotAutomationPeer : ControlAutomationPeer, IValueProvider, IRangeValueProvider
{
    public ChartPlotAutomationPeer(ChartPlot owner) : base(owner)
    {
    }

    private ChartPlot Plot => (ChartPlot)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore()
        => Plot.Owner?.GetPlotControlType(Plot) ?? AutomationControlType.Group;

    protected override string? GetNameCore()
        => AutomationProperties.GetName(Plot) is { Length: > 0 } name ? name : Plot.Owner?.GetPlotAutomationName(Plot);

    protected override string? GetHelpTextCore() => Plot.Owner?.Summary;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    bool IValueProvider.IsReadOnly => true;

    string? IValueProvider.Value => Plot.Owner?.GetPlotValueText(Plot);

    void IValueProvider.SetValue(string? value)
    {
    }

    bool IRangeValueProvider.IsReadOnly => true;

    double IRangeValueProvider.Minimum => Plot.Owner?.GetPlotRange(Plot).Minimum ?? 0;

    double IRangeValueProvider.Maximum => Plot.Owner?.GetPlotRange(Plot).Maximum ?? 0;

    double IRangeValueProvider.Value => Plot.Owner?.GetPlotRange(Plot).Value ?? 0;

    double IRangeValueProvider.LargeChange => 1;

    double IRangeValueProvider.SmallChange => 1;

    void IRangeValueProvider.SetValue(double value)
    {
    }
}
