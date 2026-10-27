using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// One service's day bars (notes 3.2 and 4): <c>n</c> bottom-aligned rounded bars across the width, each as wide as
/// <see cref="UptimeEngine.BarWidth"/> says and placed by <see cref="UptimeEngine.BarX"/>, 34 / 22 / 12 high for
/// operational / degraded / outage. Pointing at the band (or moving the keyboard cursor) inspects the nearest day: every bar
/// dims to 38% over 120ms while a full-strength copy of that one is drawn on top. A day that changes status fades its colour
/// over 240ms and springs its height (k 170, c 27). The focused band is inspected from the newest day; ← → move by a day,
/// PageUp / PageDown by seven, Home / End to the ends, Escape clears, Enter and Space select the service.
/// </summary>
/// <remarks>
/// Why code-driven: ninety rounded bars are drawn in one pass (not ninety controls), their heights spring in lockstep
/// and the nearest bar follows the pointer per move.
/// </remarks>
internal sealed class UptimeStrip : Control
{
    private static readonly SpringSpec s_heightSpring = SpringSpec.FromPhysical(170, 27);

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsSpringArray _heights;
    private readonly LiveLoop _loop;
    private AnalyticsTween[] _fade = [];
    private ServiceStatus[] _status = [];
    private ServiceStatus[] _from = [];
    private AnalyticsTween _dim = new(0);
    private double _dimmedOpacity = 0.38;
    private double _degraded = 0.65;
    private double _outage = 0.35;
    private double _radius = 1.5;
    private double _stripHeight = UptimeEngine.StripHeight;
    private int _inspected = -1;
    private int _lit = -1;
    private bool _keyboard;
    private bool _pointer;
    private bool _hasData;

    public UptimeStrip()
    {
        Focusable = true;
        IsTabStop = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        FocusAdorner = null;
        ClipToBounds = false;
        _heights = new AnalyticsSpringArray(this, s_heightSpring);
        _heights.Changed += InvalidateVisual;
        _loop = new LiveLoop(this, Advance);
        ActualThemeVariantChanged += (_, _) => RefreshPalette();
        ResourcesChanged += (_, _) => RefreshPalette();
    }

    /// <summary>Raised when the inspected day changes: its index (-1 none) and whether the keyboard moved it.</summary>
    public event Action<int, bool>? Inspected;

    /// <summary>Raised on a click, Enter or Space: select this service.</summary>
    public event Action? Activated;

    /// <summary>The number of bars.</summary>
    public int Count => _status.Length;

    /// <summary>The inspected bar (-1 none).</summary>
    public int InspectedIndex => _inspected;

    /// <summary>True when the keyboard moved the inspection last.</summary>
    public bool IsKeyboardInspection => _keyboard;

    /// <summary>The bar heights as drawn now (tests).</summary>
    internal AnalyticsSpringArray Heights => _heights;

    /// <summary>The opacity the other bars have now (1 at rest, 0.38 while inspecting).</summary>
    internal double BarOpacity => 1 - (1 - _dimmedOpacity) * Math.Clamp(_dim.Value, 0, 1);

    /// <summary>The status each bar is heading to.</summary>
    internal IReadOnlyList<ServiceStatus> Statuses => _status;

    /// <summary>The band's height (34), set by the theme from the token.</summary>
    public static readonly StyledProperty<double> BandHeightProperty =
        AvaloniaProperty.Register<UptimeStrip, double>(nameof(BandHeight), UptimeEngine.StripHeight);

    /// <summary>A bar's corner radius (1.5).</summary>
    public static readonly StyledProperty<double> BarRadiusProperty = AvaloniaProperty.Register<UptimeStrip, double>(nameof(BarRadius), 1.5);

    /// <summary>The height of a degraded bar as a share of the band (0.65).</summary>
    public static readonly StyledProperty<double> DegradedFractionProperty =
        AvaloniaProperty.Register<UptimeStrip, double>(nameof(DegradedFraction), 0.65);

    /// <summary>The height of an outage bar as a share of the band (0.35).</summary>
    public static readonly StyledProperty<double> OutageFractionProperty =
        AvaloniaProperty.Register<UptimeStrip, double>(nameof(OutageFraction), 0.35);

    /// <summary>The opacity of the other bars while one is inspected (0.38).</summary>
    public static readonly StyledProperty<double> DimmedOpacityProperty =
        AvaloniaProperty.Register<UptimeStrip, double>(nameof(DimmedOpacity), 0.38);

    public double BandHeight { get => GetValue(BandHeightProperty); set => SetValue(BandHeightProperty, value); }

    public double BarRadius { get => GetValue(BarRadiusProperty); set => SetValue(BarRadiusProperty, value); }

    public double DegradedFraction { get => GetValue(DegradedFractionProperty); set => SetValue(DegradedFractionProperty, value); }

    public double OutageFraction { get => GetValue(OutageFractionProperty); set => SetValue(OutageFractionProperty, value); }

    public double DimmedOpacity { get => GetValue(DimmedOpacityProperty); set => SetValue(DimmedOpacityProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BandHeightProperty)
        {
            _stripHeight = BandHeight;
            InvalidateMeasure();
        }
        else if (change.Property == BarRadiusProperty)
        {
            _radius = BarRadius;
        }
        else if (change.Property == DegradedFractionProperty)
        {
            _degraded = DegradedFraction;
        }
        else if (change.Property == OutageFractionProperty)
        {
            _outage = OutageFraction;
        }
        else if (change.Property == DimmedOpacityProperty)
        {
            _dimmedOpacity = DimmedOpacity;
        }
        else
        {
            return;
        }
        InvalidateVisual();
    }

    /// <summary>
    /// Shows the days. The first call places everything; later calls fade the colour of the bars whose status changed and
    /// spring their height when <paramref name="animate"/>.
    /// </summary>
    public void SetDays(IReadOnlyList<UptimeDay> days, bool animate)
    {
        var count = days.Count;
        animate &= _hasData && this.IsAttachedToVisualTree() && ArcMotion.IsFull(this);
        var next = new ServiceStatus[count];
        var heights = new double[count];
        for (var i = 0; i < count; i++)
        {
            next[i] = days[i].Status;
            heights[i] = UptimeEngine.BarHeight(next[i], _stripHeight, _degraded, _outage);
        }
        if (count != _status.Length)
        {
            _fade = new AnalyticsTween[count];
            _from = (ServiceStatus[])next.Clone();
            for (var i = 0; i < count; i++)
            {
                _fade[i] = new AnalyticsTween(1);
            }
            animate = false;
        }
        else
        {
            for (var i = 0; i < count; i++)
            {
                if (next[i] == _status[i])
                {
                    continue;
                }
                if (animate)
                {
                    // Start from the colour on screen so an interrupted fade does not jump.
                    _from[i] = _fade[i].Value >= 0.5 ? _status[i] : _from[i];
                    _fade[i].Jump(0);
                    _fade[i].Start(1, ArcMotionPresets.Standard, AnalyticsEasings.Standard);
                }
                else
                {
                    _from[i] = next[i];
                    _fade[i].Jump(1);
                }
            }
        }
        _status = next;
        if (animate)
        {
            _heights.SetTargets(heights);
        }
        else
        {
            _heights.Jump(heights);
        }
        if (_inspected >= count)
        {
            SetInspected(count - 1, _keyboard);
        }
        _hasData = true;
        InvalidateVisual();
        _loop.Start();
    }

    /// <summary>The bar's rectangle in the strip's coordinates (its final height).</summary>
    public Rect BarRect(int index)
    {
        var count = _status.Length;
        var w = UptimeEngine.BarWidth(Bounds.Width, count);
        var h = index >= 0 && index < count ? UptimeEngine.BarHeight(_status[index], _stripHeight, _degraded, _outage) : 0;
        return new Rect(UptimeEngine.BarX(index, Bounds.Width, w, count), _stripHeight - h, w, h);
    }

    /// <summary>The x of the bar's centre in the strip's coordinates.</summary>
    public double BarCenter(int index) => BarRect(index).Center.X;

    /// <summary>Inspects a day (as the keyboard would).</summary>
    public void Inspect(int index, bool byKeyboard)
    {
        _keyboard = byKeyboard;
        SetInspected(index < 0 ? -1 : Math.Clamp(index, 0, Math.Max(0, _status.Length - 1)), byKeyboard);
    }

    /// <summary>Ends the inspection.</summary>
    public void ClearInspection()
    {
        _keyboard = false;
        _pointer = false;
        SetInspected(-1, false);
    }

    private void SetInspected(int index, bool byKeyboard)
    {
        if (_status.Length == 0)
        {
            index = -1;
        }
        if (index == _inspected)
        {
            return;
        }
        var wasNone = _inspected < 0;
        _inspected = index;
        if (index >= 0)
        {
            _lit = index;
        }
        var target = index >= 0 ? 1 : 0;
        if (ArcMotion.IsFull(this) && this.IsAttachedToVisualTree())
        {
            if (Math.Abs(_dim.Target - target) > 0.5 || wasNone || index < 0)
            {
                _dim.Start(target, ArcMotionPresets.Fast, AnalyticsEasings.Standard);
            }
        }
        else
        {
            _dim.Jump(target);
        }
        InvalidateVisual();
        _loop.Start();
        Inspected?.Invoke(index, byKeyboard);
    }

    private bool Advance(double dt)
    {
        var moving = _heights.IsMoving;
        moving |= _dim.Advance(dt);
        for (var i = 0; i < _fade.Length; i++)
        {
            if (_fade[i].IsRunning)
            {
                moving |= _fade[i].Advance(dt);
            }
        }
        InvalidateVisual();
        return moving;
    }

    private void RefreshPalette()
    {
        if (_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        InvalidateVisual();
    }

    // Input --------------------------------------------------------------------------------------------------------------

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Touch && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pointer = true;
        _keyboard = false;
        var p = e.GetPosition(this);
        var w = UptimeEngine.BarWidth(Bounds.Width, _status.Length);
        SetInspected(UptimeEngine.IndexAt(p.X, Bounds.Width, w, _status.Length), false);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!_pointer)
        {
            return;
        }
        _pointer = false;
        if (!IsFocused || !AnalyticsFocus.IsVisible(this))
        {
            SetInspected(-1, false);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            Activated?.Invoke();
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional && _inspected < 0 && _status.Length > 0)
        {
            Inspect(_status.Length - 1, true);
        }
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (!_pointer)
        {
            ClearInspection();
        }
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        if (e.Key == Key.Escape)
        {
            if (_inspected >= 0)
            {
                e.Handled = true;
                ClearInspection();
            }
            return;
        }
        if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            Activated?.Invoke();
            return;
        }
        if (AnalyticsKeyboard.Step(e.Key, _inspected, _status.Length, page: 7, vertical: false) is { } next)
        {
            e.Handled = true;
            _pointer = false;
            Inspect(next, true);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    // Render -------------------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, _stripHeight);

    public override void Render(DrawingContext context)
    {
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        var size = Bounds.Size;
        // The whole band takes the pointer, also between the bars.
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        var n = _status.Length;
        if (n == 0 || size.Width <= 0)
        {
            return;
        }
        var p = _palette;
        var w = UptimeEngine.BarWidth(size.Width, n);
        var heights = _heights.Values;
        using (context.PushOpacity(BarOpacity))
        {
            for (var i = 0; i < n; i++)
            {
                DrawBar(context, i, w, heights, size.Width);
            }
        }
        if (_lit >= 0 && _lit < n && _dim.Value > 0.001)
        {
            // The inspected bar again at full strength (Arc draws a lit copy on top).
            using (context.PushOpacity(Math.Clamp(_dim.Value * 4, 0, 1)))
            {
                DrawBar(context, _lit, w, heights, size.Width);
            }
        }
        if (AnalyticsFocus.IsVisible(this))
        {
            AnalyticsFocus.Draw(context, p, new Rect(size), 6);
        }
    }

    private void DrawBar(DrawingContext context, int i, double w, ReadOnlySpan<double> heights, double width)
    {
        var p = _palette;
        var h = i < heights.Length ? Math.Clamp(heights[i], 1, _stripHeight) : 0;
        var color = UptimeColors.Bar(p, _status[i]);
        var t = _fade[i].Value;
        if (_fade[i].IsRunning && t < 1)
        {
            color = AnalyticsColors.Lerp(UptimeColors.Bar(p, _from[i]), color, Math.Clamp(t, 0, 1));
        }
        var x = UptimeEngine.BarX(i, width, w, _status.Length);
        context.DrawRectangle(p.Brush(color), null, new RoundedRect(new Rect(x, _stripHeight - h, w, h), _radius));
    }

    private sealed class GroupPeer(UptimeStrip owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsControlElementCore() => true;

        protected override bool IsContentElementCore() => true;
    }
}
