using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The 30 day uptime bar of a <see cref="ProviderStatus"/> row: thirty ticks drawn by one control (performance rule: no
/// element per tick). The pointer, a tap or a sideways drag picks a day; as one tab stop the arrow keys move a day back or
/// forward, Home and End jump to the oldest day and today, Escape hides the day. The owner shows the shared day tooltip
/// for <see cref="ActiveIndex"/> and reads keyboard moves to a live region. The shown day's tick stands
/// <c>TickLift</c> taller at both ends. A tick whose health changes recolors over 240ms on the standard curve (code-driven
/// on a <see cref="LiveLoop"/>, because thirty ticks with their own start times are one drawing a ControlTheme transition
/// cannot reach; Off lands at once).
/// </summary>
internal sealed class ProviderStatusUptimeBar : Control
{
    public static readonly StyledProperty<IBrush?> OperationalInkProperty =
        AvaloniaProperty.Register<ProviderStatusUptimeBar, IBrush?>(nameof(OperationalInk));

    public static readonly StyledProperty<IBrush?> DegradedInkProperty =
        AvaloniaProperty.Register<ProviderStatusUptimeBar, IBrush?>(nameof(DegradedInk));

    public static readonly StyledProperty<IBrush?> OutageInkProperty =
        AvaloniaProperty.Register<ProviderStatusUptimeBar, IBrush?>(nameof(OutageInk));

    public static readonly StyledProperty<IBrush?> EmptyInkProperty =
        AvaloniaProperty.Register<ProviderStatusUptimeBar, IBrush?>(nameof(EmptyInk));

    public static readonly StyledProperty<IBrush?> FocusInkProperty =
        AvaloniaProperty.Register<ProviderStatusUptimeBar, IBrush?>(nameof(FocusInk));

    public static readonly StyledProperty<double> TickHeightProperty =
        AvaloniaProperty.Register<ProviderStatusUptimeBar, double>(nameof(TickHeight), 24);

    public static readonly StyledProperty<double> TickGapProperty =
        AvaloniaProperty.Register<ProviderStatusUptimeBar, double>(nameof(TickGap), 2);

    public static readonly StyledProperty<CornerRadius> TickCornerRadiusProperty =
        AvaloniaProperty.Register<ProviderStatusUptimeBar, CornerRadius>(nameof(TickCornerRadius), new CornerRadius(2));

    public static readonly StyledProperty<double> TickLiftProperty =
        AvaloniaProperty.Register<ProviderStatusUptimeBar, double>(nameof(TickLift), 2);

    /// <summary>Seconds a tick takes to recolor.</summary>
    public const double ColorDuration = 0.24;

    private const int Count = ProviderStatusFormat.WindowDays;

    /// <summary>How far a touch moves before it counts as a drag (and which way).</summary>
    private const double DragSlop = 6;

    private readonly ProviderStatusDayCell[] _cells = new ProviderStatusDayCell[Count];
    private readonly Tick[] _ticks = new Tick[Count];
    private readonly Dictionary<uint, IImmutableSolidColorBrush> _brushes = new();
    private readonly LiveLoop _loop;
    private IPen? _focusPen;
    private double _now;
    private bool _hasDays;
    private int _active = -1;
    private int _keyboard = Count - 1;
    private Point? _touchStart;
    private bool _scrubbing;
    private bool _keyboardFocus;

    public ProviderStatusUptimeBar()
    {
        Focusable = true;
        ClipToBounds = false;
        for (var i = 0; i < Count; i++)
        {
            _cells[i] = new ProviderStatusDayCell(default, null, null, null);
        }
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
    }

    public IBrush? OperationalInk { get => GetValue(OperationalInkProperty); set => SetValue(OperationalInkProperty, value); }

    public IBrush? DegradedInk { get => GetValue(DegradedInkProperty); set => SetValue(DegradedInkProperty, value); }

    public IBrush? OutageInk { get => GetValue(OutageInkProperty); set => SetValue(OutageInkProperty, value); }

    public IBrush? EmptyInk { get => GetValue(EmptyInkProperty); set => SetValue(EmptyInkProperty, value); }

    public IBrush? FocusInk { get => GetValue(FocusInkProperty); set => SetValue(FocusInkProperty, value); }

    public double TickHeight { get => GetValue(TickHeightProperty); set => SetValue(TickHeightProperty, value); }

    public double TickGap { get => GetValue(TickGapProperty); set => SetValue(TickGapProperty, value); }

    public CornerRadius TickCornerRadius { get => GetValue(TickCornerRadiusProperty); set => SetValue(TickCornerRadiusProperty, value); }

    public double TickLift { get => GetValue(TickLiftProperty); set => SetValue(TickLiftProperty, value); }

    /// <summary>The day whose tooltip shows, or -1.</summary>
    public int ActiveIndex => _active;

    /// <summary>The day the arrow keys move from (today until moved).</summary>
    public int KeyboardIndex => _keyboard;

    /// <summary>The days drawn, oldest first.</summary>
    public IReadOnlyList<ProviderStatusDayCell> Cells => _cells;

    /// <summary>True while a tick recolors.</summary>
    internal bool IsRecoloring => AnyMoving();

    /// <summary>Raised when the shown day changes: the bar, the day (or -1) and whether a key moved it.</summary>
    public event Action<ProviderStatusUptimeBar, int, bool>? ActiveChanged;

    /// <summary>Shows a new window of days; ticks whose health changed recolor from what they show now.</summary>
    public void SetDays(ProviderStatusDayCell[] cells)
    {
        var animate = _hasDays && IsLoaded && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
        var moving = false;
        for (var i = 0; i < Count; i++)
        {
            var health = cells[i].Health;
            ref var tick = ref _ticks[i];
            if (animate && _cells[i].Health != health)
            {
                tick.From = ColorAt(i, _now);
                tick.Start = _now;
                tick.Moving = true;
                moving = true;
            }
            else if (!animate)
            {
                tick.Moving = false;
            }
            _cells[i] = cells[i];
        }
        _hasDays = true;
        if (moving)
        {
            _loop.Start();
        }
        InvalidateVisual();
    }

    /// <summary>Shows <paramref name="index"/> (or hides with -1) and tells the owner.</summary>
    public void SetActive(int index, bool fromKeyboard)
    {
        index = index < 0 ? -1 : Math.Min(index, Count - 1);
        if (index >= 0)
        {
            _keyboard = index;
        }
        if (_active == index && !fromKeyboard)
        {
            return;
        }
        _active = index;
        InvalidateVisual();
        ActiveChanged?.Invoke(this, index, fromKeyboard);
    }

    /// <summary>The tick of day <paramref name="index"/> in the bar's coordinates (at rest).</summary>
    public Rect TickRect(int index)
    {
        var gap = TickGap;
        var width = TickWidth(Bounds.Width, gap);
        return new Rect(index * (width + gap), TickLift, width, TickHeight);
    }

    /// <summary>The day under <paramref name="x"/>: the nearest tick, gaps included.</summary>
    public int IndexAt(double x)
    {
        var width = Bounds.Width;
        if (width <= 0)
        {
            return -1;
        }
        var step = width / Count;
        return Math.Clamp((int)Math.Floor(x / step), 0, Count - 1);
    }

    private static double TickWidth(double width, double gap) => Math.Max(1, (width - (Count - 1) * gap) / Count);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 180;
        return new Size(width, TickHeight + 2 * TickLift);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == TickHeightProperty || p == TickLiftProperty)
        {
            InvalidateMeasure();
            InvalidateVisual();
        }
        else if (p == TickGapProperty || p == TickCornerRadiusProperty || p == FocusInkProperty)
        {
            _focusPen = null;
            InvalidateVisual();
        }
        else if (p == OperationalInkProperty || p == DegradedInkProperty || p == OutageInkProperty || p == EmptyInkProperty)
        {
            // A theme or accent change lands at once.
            for (var i = 0; i < Count; i++)
            {
                _ticks[i].Moving = false;
            }
            _brushes.Clear();
            InvalidateVisual();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        var from = _active >= 0 ? _active : _keyboard;
        int? next = e.Key switch
        {
            Key.Left => Math.Max(0, from - 1),
            Key.Right => Math.Min(Count - 1, from + 1),
            Key.Home => 0,
            Key.End => Count - 1,
            _ => null
        };
        if (next is { } index)
        {
            _keyboardFocus = true;
            SetActive(index, fromKeyboard: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _active >= 0)
        {
            SetActive(-1, fromKeyboard: true);
            e.Handled = true;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _keyboardFocus = e.NavigationMethod == NavigationMethod.Tab || e.NavigationMethod == NavigationMethod.Directional;
        if (_keyboardFocus)
        {
            SetActive(_keyboard, fromKeyboard: true);
        }
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _keyboardFocus = false;
        if (!IsPointerOver)
        {
            SetActive(-1, fromKeyboard: false);
        }
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (e.Pointer.Type == PointerType.Touch)
        {
            if (_touchStart is { } start && !_scrubbing)
            {
                var dx = Math.Abs(point.X - start.X);
                var dy = Math.Abs(point.Y - start.Y);
                if (dx > DragSlop && dx > dy)
                {
                    // A sideways drag scrubs the days; a vertical one is left to the scroller.
                    _scrubbing = true;
                    e.Pointer.Capture(this);
                }
                else if (dy > DragSlop)
                {
                    _touchStart = null;
                }
            }
            if (_scrubbing)
            {
                SetActive(IndexAt(point.X), fromKeyboard: false);
                e.Handled = true;
            }
            return;
        }
        SetActive(IndexAt(point.X), fromKeyboard: false);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetPosition(this);
        if (e.Pointer.Type == PointerType.Touch)
        {
            _touchStart = point;
            _scrubbing = false;
        }
        // A tap or click shows the day; it never opens the row.
        SetActive(IndexAt(point.X), fromKeyboard: false);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_scrubbing)
        {
            e.Pointer.Capture(null);
        }
        _touchStart = null;
        _scrubbing = false;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        if (_keyboardFocus && IsFocused)
        {
            SetActive(_keyboard, fromKeyboard: false);
            return;
        }
        SetActive(-1, fromKeyboard: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        for (var i = 0; i < Count; i++)
        {
            _ticks[i].Moving = false;
        }
        _active = -1;
    }

    private bool Step(double dt)
    {
        _now += dt;
        InvalidateVisual();
        return AnyMoving();
    }

    private bool AnyMoving()
    {
        for (var i = 0; i < Count; i++)
        {
            if (_ticks[i].Moving && _now < _ticks[i].Start + ColorDuration)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The resting color of a day's health.</summary>
    internal Color RestColor(ProviderStatusHealth? health) => health switch
    {
        ProviderStatusHealth.Operational => ColorOf(OperationalInk, Color.Parse("#2E9E5B")),
        ProviderStatusHealth.Degraded => ColorOf(DegradedInk, Color.Parse("#D99A06")),
        ProviderStatusHealth.Outage => ColorOf(OutageInk, Color.Parse("#D9443B")),
        _ => ColorOf(EmptyInk, Color.Parse("#E4E4E4"))
    };

    /// <summary>The color tick <paramref name="index"/> shows now.</summary>
    internal Color ColorAt(int index) => ColorAt(index, _now);

    /// <summary>Advances the recolor clock without frames (tests).</summary>
    internal void Advance(double seconds)
    {
        _now += seconds;
        InvalidateVisual();
    }

    private Color ColorAt(int index, double now)
    {
        ref var tick = ref _ticks[index];
        var target = RestColor(_cells[index].Health);
        if (!tick.Moving)
        {
            return target;
        }
        var t = now - tick.Start;
        if (t >= ColorDuration)
        {
            return target;
        }
        if (t <= 0)
        {
            return tick.From;
        }
        return Oklch.MixOklab(tick.From, target, ArcEasings.Standard.Ease(t / ColorDuration));
    }

    private static Color ColorOf(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid ? solid.Color : fallback;

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width <= 0)
        {
            return;
        }
        // The whole bar takes the pointer, gaps included.
        context.FillRectangle(Brushes.Transparent, new Rect(bounds.Size));
        var gap = TickGap;
        var width = TickWidth(bounds.Width, gap);
        var lift = TickLift;
        var height = TickHeight;
        var radius = Math.Min(TickCornerRadius.TopLeft, width / 2);
        for (var i = 0; i < Count; i++)
        {
            var x = i * (width + gap);
            var rect = i == _active
                ? new Rect(x, 0, width, height + 2 * lift)
                : new Rect(x, lift, width, height);
            context.DrawRectangle(BrushOf(ColorAt(i, _now)), null, rect, radius, radius);
        }
        if (_keyboardFocus && IsFocused && FocusInk is { } focus)
        {
            var ring = new Rect(bounds.Size).Inflate(3);
            _focusPen ??= new ImmutablePen(focus.ToImmutable(), 2);
            context.DrawRectangle(null, _focusPen, ring, 6, 6);
        }
    }

    private IImmutableSolidColorBrush BrushOf(Color color)
    {
        var key = color.ToUInt32();
        if (!_brushes.TryGetValue(key, out var brush))
        {
            if (_brushes.Count >= 512)
            {
                _brushes.Clear();
            }
            brush = new ImmutableSolidColorBrush(color);
            _brushes[key] = brush;
        }
        return brush;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new BarPeer(this);

    private struct Tick
    {
        public Color From;
        public double Start;
        public bool Moving;
    }

    /// <summary>One focusable image named by its 30 day summary; the days are read through the owner's live region.</summary>
    private sealed class BarPeer(ProviderStatusUptimeBar owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        protected override bool IsKeyboardFocusableCore() => true;

        protected override string? GetNameCore() => AutomationProperties.GetName(Owner);
    }
}
