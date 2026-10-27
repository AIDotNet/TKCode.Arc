using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The candlestick chart of <see cref="MarketTerminal"/>, drawn in one pass and only for the visible candles: a readout line
/// (the crosshair candle's time, open, high, low, close, change, volume and averages), grid and price axis, candles,
/// volume bars, the two moving averages, the live price line with its tag, and the crosshair with its price and time tags.
/// The view is a window of candle indices; zoom (wheel, pinch, keys, buttons), pan (drag with momentum, keys) and range
/// changes move a target window, and the shown window and price scale approach their targets exponentially (time constant
/// from the smooth spring, so nothing overshoots and every gesture can reverse mid-way). Live ticks glide the last close the
/// same way. Frames run only while something moves.
/// Code-driven: the view is continuous state fed by gestures and ticks every frame, which a ControlTheme cannot express.
/// </summary>
internal sealed class MarketChart : Control
{
    public static readonly StyledProperty<IBrush?> UpBrushProperty = AvaloniaProperty.Register<MarketChart, IBrush?>(nameof(UpBrush));
    public static readonly StyledProperty<IBrush?> DownBrushProperty = AvaloniaProperty.Register<MarketChart, IBrush?>(nameof(DownBrush));
    public static readonly StyledProperty<IBrush?> GridBrushProperty = AvaloniaProperty.Register<MarketChart, IBrush?>(nameof(GridBrush));
    public static readonly StyledProperty<IBrush?> AxisBrushProperty = AvaloniaProperty.Register<MarketChart, IBrush?>(nameof(AxisBrush));
    public static readonly StyledProperty<IBrush?> LabelBrushProperty = AvaloniaProperty.Register<MarketChart, IBrush?>(nameof(LabelBrush));
    public static readonly StyledProperty<IBrush?> ValueBrushProperty = AvaloniaProperty.Register<MarketChart, IBrush?>(nameof(ValueBrush));
    public static readonly StyledProperty<IBrush?> StrongBrushProperty = AvaloniaProperty.Register<MarketChart, IBrush?>(nameof(StrongBrush));
    public static readonly StyledProperty<IBrush?> TagTextBrushProperty = AvaloniaProperty.Register<MarketChart, IBrush?>(nameof(TagTextBrush));
    public static readonly StyledProperty<IBrush?> Ma20BrushProperty = AvaloniaProperty.Register<MarketChart, IBrush?>(nameof(Ma20Brush));
    public static readonly StyledProperty<IBrush?> Ma50BrushProperty = AvaloniaProperty.Register<MarketChart, IBrush?>(nameof(Ma50Brush));
    public static readonly StyledProperty<IBrush?> FocusBrushProperty = AvaloniaProperty.Register<MarketChart, IBrush?>(nameof(FocusBrush));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<MarketChart>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<MarketChart>();

    public static readonly StyledProperty<bool> ShowMa20Property = AvaloniaProperty.Register<MarketChart, bool>(nameof(ShowMa20), true);
    public static readonly StyledProperty<bool> ShowMa50Property = AvaloniaProperty.Register<MarketChart, bool>(nameof(ShowMa50), true);

    /// <summary>Width of the price axis on the right.</summary>
    public static readonly StyledProperty<double> GutterProperty = AvaloniaProperty.Register<MarketChart, double>(nameof(Gutter), 70);

    /// <summary>Minimum spacing of candles in the default window.</summary>
    public static readonly StyledProperty<double> MinCandleSpacingProperty =
        AvaloniaProperty.Register<MarketChart, double>(nameof(MinCandleSpacing), 5);

    public const int MinVisible = 12;
    private const double AxisHeight = 24;
    private const double ReadoutLineHeight = 17;
    private const double ReadoutGap = 12;
    private const double VolumeShare = 0.18;
    private const double VolumeGap = 8;
    private const double TagHeight = 22;
    private const double TagWidth = 64;
    private const double ZoomStep = 1.25;
    private const double ApproachSeconds = 0.4 / 4.6;   // ~99% of the way in the smooth spring's 0.4s
    private const double MomentumSeconds = 0.31;        // 0.95 per 16ms frame
    private const double DragThreshold = 4;

    private readonly FinanceText _text = new();
    private readonly LiveLoop _loop;
    private readonly List<(double X, string Text)> _xLabels = new();
    private readonly (double Time, double X)[] _samples = new (double, double)[8];
    private MarketSeries? _series;
    private double _previousClose;
    private double _start, _end, _targetStart, _targetEnd;
    private double _min, _max, _targetMin, _targetMax;
    private double _shownClose;
    private double _velocity;
    private bool _hasScale;
    private int _cross = -1;
    private double _crossY = double.NaN;
    private bool _crossFromKeyboard;
    private bool _pinned;
    private bool _focusVisible;
    private IPointer? _dragPointer;
    private Point _pressPoint;
    private double _pressStart, _pressEnd;
    private bool _dragging;
    private bool _dragIsTouch;
    private int _sampleCount;
    private double _pinchStartWidth, _pinchAnchor;
    private double _now;
    private IPen? _gridPen, _upDash, _downDash, _crossPen, _upPen, _downPen, _ma20Pen, _ma50Pen, _focusPen;
    private IBrush? _upVolume, _downVolume, _upSoft, _downSoft;

    static MarketChart()
    {
        AffectsRender<MarketChart>(UpBrushProperty, DownBrushProperty, GridBrushProperty, AxisBrushProperty, LabelBrushProperty,
            ValueBrushProperty, StrongBrushProperty, TagTextBrushProperty, Ma20BrushProperty, Ma50BrushProperty, FocusBrushProperty,
            ShowMa20Property, ShowMa50Property, GutterProperty, FontSizeProperty, FontFamilyProperty);
        FocusableProperty.OverrideDefaultValue<MarketChart>(true);
        ClipToBoundsProperty.OverrideDefaultValue<MarketChart>(false);
        CursorProperty.OverrideDefaultValue<MarketChart>(new Cursor(StandardCursorType.Cross));
    }

    public MarketChart()
    {
        _loop = new LiveLoop(this, Step);
        GestureRecognizers.Add(new PinchGestureRecognizer());
        AddHandler(InputElement.PinchEvent, OnPinch);
        AddHandler(InputElement.PinchEndedEvent, (_, _) => _pinchStartWidth = 0);
    }

    public IBrush? UpBrush { get => GetValue(UpBrushProperty); set => SetValue(UpBrushProperty, value); }
    public IBrush? DownBrush { get => GetValue(DownBrushProperty); set => SetValue(DownBrushProperty, value); }
    public IBrush? GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public IBrush? AxisBrush { get => GetValue(AxisBrushProperty); set => SetValue(AxisBrushProperty, value); }
    public IBrush? LabelBrush { get => GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }
    public IBrush? ValueBrush { get => GetValue(ValueBrushProperty); set => SetValue(ValueBrushProperty, value); }
    public IBrush? StrongBrush { get => GetValue(StrongBrushProperty); set => SetValue(StrongBrushProperty, value); }
    public IBrush? TagTextBrush { get => GetValue(TagTextBrushProperty); set => SetValue(TagTextBrushProperty, value); }
    public IBrush? Ma20Brush { get => GetValue(Ma20BrushProperty); set => SetValue(Ma20BrushProperty, value); }
    public IBrush? Ma50Brush { get => GetValue(Ma50BrushProperty); set => SetValue(Ma50BrushProperty, value); }
    public IBrush? FocusBrush { get => GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public bool ShowMa20 { get => GetValue(ShowMa20Property); set => SetValue(ShowMa20Property, value); }
    public bool ShowMa50 { get => GetValue(ShowMa50Property); set => SetValue(ShowMa50Property, value); }
    public double Gutter { get => GetValue(GutterProperty); set => SetValue(GutterProperty, value); }
    public double MinCandleSpacing { get => GetValue(MinCandleSpacingProperty); set => SetValue(MinCandleSpacingProperty, value); }

    /// <summary>Formats the readout's time and the time tag; set by the block (date for daily candles, time for intraday).</summary>
    public MarketTerminalLabels Labels { get; set; } = new();

    /// <summary>Announces keyboard moves of the crosshair (the block's live region).</summary>
    public Action<string>? Announce { get; set; }

    /// <summary>The candle under the crosshair, or -1.</summary>
    public int CrosshairIndex => _cross;

    /// <summary>The window shown now and the one it is moving to, in candle indices.</summary>
    public (double Start, double End) View => (_start, _end);

    public (double Start, double End) TargetView => (_targetStart, _targetEnd);

    /// <summary>The price scale shown now (bottom and top of the price area).</summary>
    public (double Min, double Max) PriceScale => (_min, _max);

    /// <summary>The close drawn for the live candle (it glides to the real close).</summary>
    public double ShownClose => _shownClose;

    public MarketSeries? Series => _series;

    /// <summary>True while the view, scale or live price is still moving.</summary>
    public bool IsMoving => _loop.IsRunning;

    /// <summary>Shows <paramref name="series"/>; <paramref name="animate"/> eases into its default window from a wider one.</summary>
    public void SetSeries(MarketSeries series, double previousClose, bool animate)
    {
        var changed = !ReferenceEquals(series, _series);
        _series = series;
        _previousClose = previousClose;
        _shownClose = series.Last.Close;
        _velocity = 0;
        if (changed)
        {
            _cross = -1;
            _pinned = false;
        }
        var (start, end) = DefaultWindow();
        _targetStart = start;
        _targetEnd = end;
        if (animate && _hasScale && CanAnimate)
        {
            var width = end - start;
            _start = end - width * 1.18;
            _end = end + width * 0.02;
        }
        else
        {
            _start = start;
            _end = end;
            _hasScale = false;
        }
        Kick();
    }

    /// <summary>The live candle changed (a tick): glide to it.</summary>
    public void OnTick()
    {
        if (_series is null)
        {
            return;
        }
        if (!CanAnimate)
        {
            _shownClose = _series.Last.Close;
        }
        Kick();
    }

    public void ZoomIn() => ZoomAround(Anchor(), 1 / ZoomStep);

    public void ZoomOut() => ZoomAround(Anchor(), ZoomStep);

    /// <summary>Back to the range's default window.</summary>
    public void ResetView()
    {
        var (start, end) = DefaultWindow();
        _velocity = 0;
        SetTarget(start, end);
    }

    /// <summary>Clears the crosshair (Escape, a second tap).</summary>
    public void ClearCrosshair()
    {
        _cross = -1;
        _crossY = double.NaN;
        _crossFromKeyboard = false;
        _pinned = false;
        InvalidateVisual();
    }

    /// <summary>Puts the crosshair on candle <paramref name="index"/> (clamped), scrolling the view to keep it visible.</summary>
    public void MoveCrosshair(int index, bool announce)
    {
        if (_series is null || _series.Count == 0)
        {
            return;
        }
        _cross = Math.Clamp(index, 0, _series.Count - 1);
        _crossFromKeyboard = true;
        _crossY = double.NaN;
        var width = _targetEnd - _targetStart;
        if (_cross < _targetStart + 0.5)
        {
            SetTarget(_cross - 0.5, _cross - 0.5 + width);
        }
        else if (_cross + 1 > _targetEnd - 0.5)
        {
            SetTarget(_cross + 1.5 - width, _cross + 1.5);
        }
        InvalidateVisual();
        if (announce)
        {
            Announce?.Invoke(Labels.CandleAnnouncement(Describe(_cross)));
        }
    }

    private bool CanAnimate => ArcMotion.IsFull(this) && this.IsAttachedToVisualTree();

    private double PlotWidth => Math.Max(1, Bounds.Width - Gutter);

    private (double Start, double End) DefaultWindow()
    {
        var count = _series?.Count ?? 0;
        if (count == 0)
        {
            return (0, 1);
        }
        var fits = Bounds.Width > 0 ? Math.Floor(PlotWidth / Math.Max(1, MinCandleSpacing)) : count;
        var shown = Math.Max(Math.Min(count, MinVisible), Math.Min(count, fits));
        var end = count + 0.5;
        return (end - shown - 0.5, end);
    }

    private double Anchor()
    {
        if (_cross >= 0)
        {
            return _cross + 0.5;
        }
        return _series is null ? 0 : _series.Count;
    }

    private void ZoomAround(double anchor, double factor)
    {
        var width = _targetEnd - _targetStart;
        var next = ClampWidth(width * factor);
        var share = width <= 0 ? 1 : (anchor - _targetStart) / width;
        var start = anchor - share * next;
        SetTarget(start, start + next);
    }

    private double ClampWidth(double width)
    {
        var count = _series?.Count ?? 1;
        return Math.Clamp(width, Math.Min(MinVisible, count + 1), count + 8);
    }

    private (double Start, double End) Clamp(double start, double end)
    {
        var count = _series?.Count ?? 0;
        var width = ClampWidth(end - start);
        var lo = -1.0;
        var hi = count + Math.Max(2, width * 0.08);
        start = Math.Clamp(start, lo, Math.Max(lo, hi - width));
        return (start, start + width);
    }

    private void SetTarget(double start, double end, bool immediate = false)
    {
        (_targetStart, _targetEnd) = Clamp(start, end);
        if (immediate || !CanAnimate)
        {
            _start = _targetStart;
            _end = _targetEnd;
        }
        Kick();
    }

    private void Kick()
    {
        if (CanAnimate)
        {
            _loop.Start();
        }
        else
        {
            _start = _targetStart;
            _end = _targetEnd;
            _velocity = 0;
            if (_series is not null)
            {
                _shownClose = _series.Last.Close;
            }
            UpdateScale(1);
        }
        InvalidateVisual();
    }

    private bool Step(double dt)
    {
        _now += dt;
        if (_series is null)
        {
            return false;
        }
        var moving = false;
        if (Math.Abs(_velocity) > 0.05 && !_dragging)
        {
            var (start, end) = Clamp(_targetStart + _velocity * dt, _targetEnd + _velocity * dt);
            if (Math.Abs(start - (_targetStart + _velocity * dt)) > 1e-6)
            {
                _velocity = 0;
            }
            _targetStart = start;
            _targetEnd = end;
            _start += start - _start;
            _end += end - _end;
            _velocity *= Math.Exp(-dt / MomentumSeconds);
            moving = true;
        }
        else
        {
            _velocity = 0;
        }
        var k = 1 - Math.Exp(-dt / ApproachSeconds);
        moving |= Approach(ref _start, _targetStart, k, 0.002);
        moving |= Approach(ref _end, _targetEnd, k, 0.002);
        moving |= Approach(ref _shownClose, _series.Last.Close, k, 0.0005);
        moving |= UpdateScale(k);
        InvalidateVisual();
        return moving || _dragging;
    }

    private static bool Approach(ref double value, double target, double k, double epsilon)
    {
        if (Math.Abs(target - value) <= epsilon)
        {
            value = target;
            return false;
        }
        value += (target - value) * k;
        return true;
    }

    /// <summary>Fits the price scale to the candles of the shown window (8% room above and below).</summary>
    private bool UpdateScale(double k)
    {
        if (_series is null || _series.Count == 0)
        {
            return false;
        }
        var first = Math.Max(0, (int)Math.Floor(_start));
        var last = Math.Min(_series.Count - 1, (int)Math.Ceiling(_end) - 1);
        double lo = double.MaxValue, hi = double.MinValue;
        for (var i = first; i <= last; i++)
        {
            ref var c = ref _series[i];
            var low = i == _series.Count - 1 ? Math.Min(c.Low, _shownClose) : c.Low;
            var high = i == _series.Count - 1 ? Math.Max(c.High, _shownClose) : c.High;
            lo = Math.Min(lo, low);
            hi = Math.Max(hi, high);
        }
        if (lo > hi)
        {
            lo = hi = _series.Last.Close;
        }
        var pad = Math.Max((hi - lo) * 0.08, hi * 0.002);
        _targetMin = lo - pad;
        _targetMax = hi + pad;
        if (!_hasScale || k >= 1)
        {
            _min = _targetMin;
            _max = _targetMax;
            _hasScale = true;
            return false;
        }
        var span = Math.Max(_targetMax - _targetMin, 1e-6);
        var moving = Approach(ref _min, _targetMin, k, span * 0.0005);
        moving |= Approach(ref _max, _targetMax, k, span * 0.0005);
        return moving;
    }

    #region Input

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed == false && e.Pointer.Type == PointerType.Mouse)
        {
            return;
        }
        Focus(NavigationMethod.Pointer);
        _dragPointer = e.Pointer;
        _pressPoint = point.Position;
        _pressStart = _targetStart;
        _pressEnd = _targetEnd;
        _dragging = false;
        _dragIsTouch = e.Pointer.Type != PointerType.Mouse;
        _velocity = 0;
        _sampleCount = 0;
        AddSample(e.Timestamp, point.Position.X);
        if (!_dragIsTouch)
        {
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = e.GetPosition(this);
        if (_dragPointer is not null && ReferenceEquals(e.Pointer, _dragPointer))
        {
            var dx = position.X - _pressPoint.X;
            var dy = position.Y - _pressPoint.Y;
            if (!_dragging && Math.Max(Math.Abs(dx), Math.Abs(dy)) >= DragThreshold)
            {
                if (_dragIsTouch && Math.Abs(dy) > Math.Abs(dx))
                {
                    // A vertical swipe scrolls the page; only horizontal drags pan the chart.
                    _dragPointer = null;
                    return;
                }
                _dragging = true;
                e.Pointer.Capture(this);
                Cursor = new Cursor(StandardCursorType.SizeWestEast);
            }
            if (_dragging)
            {
                var perCandle = PlotWidth / Math.Max(1e-6, _pressEnd - _pressStart);
                var shift = -dx / perCandle;
                SetTarget(_pressStart + shift, _pressEnd + shift, immediate: true);
                AddSample(e.Timestamp, position.X);
                e.Handled = true;
            }
        }
        if (e.Pointer.Type == PointerType.Mouse && !_dragging)
        {
            SetCrosshairAt(position);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragPointer is null || !ReferenceEquals(e.Pointer, _dragPointer))
        {
            return;
        }
        var position = e.GetPosition(this);
        if (_dragging)
        {
            AddSample(e.Timestamp, position.X);
            var perCandle = PlotWidth / Math.Max(1e-6, _targetEnd - _targetStart);
            _velocity = CanAnimate ? -ReleaseVelocity() / perCandle : 0;
            Cursor = new Cursor(StandardCursorType.Cross);
            _dragging = false;
            Kick();
        }
        else if (_dragIsTouch)
        {
            // A tap pins the crosshair; a second tap clears it.
            if (_pinned)
            {
                ClearCrosshair();
            }
            else
            {
                SetCrosshairAt(position);
                _pinned = true;
            }
        }
        _dragPointer = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_dragging)
        {
            _dragging = false;
            Cursor = new Cursor(StandardCursorType.Cross);
            Kick();
        }
        _dragPointer = null;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (e.Pointer.Type == PointerType.Mouse && !_pinned && !_crossFromKeyboard && !_dragging)
        {
            _cross = -1;
            _crossY = double.NaN;
            InvalidateVisual();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_series is null)
        {
            return;
        }
        var position = e.GetPosition(this);
        if (Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y))
        {
            var perCandle = PlotWidth / Math.Max(1e-6, _targetEnd - _targetStart);
            var shift = -e.Delta.X * 40 / perCandle;
            SetTarget(_targetStart + shift, _targetEnd + shift);
        }
        else
        {
            ZoomAround(IndexAt(position.X, target: true), Math.Exp(-e.Delta.Y * 0.18));
        }
        e.Handled = true;
    }

    private void OnPinch(object? sender, PinchEventArgs e)
    {
        if (_series is null)
        {
            return;
        }
        if (_pinchStartWidth <= 0)
        {
            _pinchStartWidth = _targetEnd - _targetStart;
            _pinchAnchor = IndexAt(e.ScaleOrigin.X * Bounds.Width, target: true);
        }
        var width = ClampWidth(_pinchStartWidth / Math.Max(0.05, e.Scale));
        var share = (_pinchAnchor - _targetStart) / Math.Max(1e-6, _targetEnd - _targetStart);
        var start = _pinchAnchor - share * width;
        SetTarget(start, start + width, immediate: true);
        _dragging = false;
        _dragPointer = null;
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_series is null || _series.Count == 0)
        {
            return;
        }
        var current = _cross >= 0 ? _cross : _series.Count - 1;
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1;
        switch (e.Key)
        {
            case Key.Left:
                MoveCrosshair(_cross >= 0 ? current - step : current, announce: true);
                break;
            case Key.Right:
                MoveCrosshair(current + step, announce: true);
                break;
            case Key.Home:
                MoveCrosshair(0, announce: true);
                break;
            case Key.End:
                MoveCrosshair(_series.Count - 1, announce: true);
                break;
            case Key.OemPlus or Key.Add:
                ZoomIn();
                break;
            case Key.OemMinus or Key.Subtract:
                ZoomOut();
                break;
            case Key.D0 or Key.NumPad0:
                ResetView();
                break;
            case Key.Escape when _cross >= 0:
                ClearCrosshair();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusVisible = false;
        if (_crossFromKeyboard)
        {
            ClearCrosshair();
        }
        InvalidateVisual();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        // Pointer focus draws no ring; keyboard focus shows the shared focus ring.
        _focusVisible = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        InvalidateVisual();
    }

    private void SetCrosshairAt(Point position)
    {
        if (_series is null || position.X > PlotWidth || position.X < 0)
        {
            if (_cross >= 0 && !_pinned)
            {
                _cross = -1;
                InvalidateVisual();
            }
            return;
        }
        var index = (int)Math.Floor(IndexAt(position.X, target: false));
        _cross = Math.Clamp(index, 0, _series.Count - 1);
        _crossY = position.Y;
        _crossFromKeyboard = false;
        InvalidateVisual();
    }

    private double IndexAt(double x, bool target)
    {
        var (start, end) = target ? (_targetStart, _targetEnd) : (_start, _end);
        return start + x / PlotWidth * (end - start);
    }

    private void AddSample(ulong timestamp, double x)
    {
        var slot = _sampleCount % _samples.Length;
        _samples[slot] = (timestamp / 1000.0, x);
        _sampleCount++;
    }

    /// <summary>Pointer speed (px/s) over the last ~100ms of the drag.</summary>
    private double ReleaseVelocity()
    {
        var n = Math.Min(_sampleCount, _samples.Length);
        if (n < 2)
        {
            return 0;
        }
        var newest = _samples[(_sampleCount - 1) % _samples.Length];
        var oldest = newest;
        for (var i = 2; i <= n; i++)
        {
            var sample = _samples[(_sampleCount - i) % _samples.Length];
            if (newest.Time - sample.Time > 0.1)
            {
                break;
            }
            oldest = sample;
        }
        var dt = newest.Time - oldest.Time;
        return dt <= 0.008 ? 0 : (newest.X - oldest.X) / dt;
    }

    #endregion

    #region Rendering

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty)
        {
            _text.Clear();
            _text.FontFamily = FontFamily;
        }
        else if (change.Property.Name.EndsWith("Brush", StringComparison.Ordinal))
        {
            _text.Clear();
            _gridPen = null;
        }
        else if (change.Property == BoundsProperty)
        {
            var old = change.GetOldValue<Rect>();
            if (old.Width <= 0 && _series is not null)
            {
                // The first real width decides how many candles the default window can show.
                var (start, end) = DefaultWindow();
                _start = _targetStart = start;
                _end = _targetEnd = end;
                _hasScale = false;
                UpdateScale(1);
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _text.Clear();
    }

    private void EnsurePens()
    {
        if (_gridPen is not null)
        {
            return;
        }
        _gridPen = new ImmutablePen(Immutable(GridBrush), 1);
        _upDash = new ImmutablePen(Immutable(UpBrush), 1, new ImmutableDashStyle(new double[] { 2, 3 }, 0));
        _downDash = new ImmutablePen(Immutable(DownBrush), 1, new ImmutableDashStyle(new double[] { 2, 3 }, 0));
        _crossPen = new ImmutablePen(Immutable(AxisBrush), 1, new ImmutableDashStyle(new double[] { 3, 3 }, 0));
        _upPen = new ImmutablePen(Immutable(UpBrush), 1);
        _downPen = new ImmutablePen(Immutable(DownBrush), 1);
        _ma20Pen = new ImmutablePen(Immutable(Ma20Brush), 1.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        _ma50Pen = new ImmutablePen(Immutable(Ma50Brush), 1.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        _focusPen = new ImmutablePen(Immutable(FocusBrush), 2);
        _upVolume = WithOpacity(UpBrush, 0.35);
        _downVolume = WithOpacity(DownBrush, 0.35);
        _upSoft = WithOpacity(UpBrush, 0.5);
        _downSoft = WithOpacity(DownBrush, 0.5);
    }

    private static IImmutableBrush Immutable(IBrush? brush) => brush switch
    {
        ISolidColorBrush solid => new ImmutableSolidColorBrush(solid.Color, solid.Opacity),
        IImmutableBrush immutable => immutable,
        _ => new ImmutableSolidColorBrush(Colors.Gray),
    };

    private static IBrush WithOpacity(IBrush? brush, double opacity)
        => brush is ISolidColorBrush solid ? new ImmutableSolidColorBrush(solid.Color, solid.Opacity * opacity) : new ImmutableSolidColorBrush(Colors.Gray, opacity);

    /// <summary>Top of the plot: under the readout, which takes two lines when one does not fit.</summary>
    private double ReadoutLines(double width) => ReadoutWidth(0) + ReadoutWidth(1) + 16 > width ? 2 : 1;

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        context.FillRectangle(Brushes.Transparent, new Rect(bounds.Size));
        if (_series is null || _series.Count == 0 || bounds.Width <= Gutter + 10 || bounds.Height <= 80)
        {
            return;
        }
        EnsurePens();
        _text.FontFamily = FontFamily;
        var series = _series;
        var plotWidth = PlotWidth;
        var lines = ReadoutLines(bounds.Width);
        var top = lines * ReadoutLineHeight + ReadoutGap;
        var bottom = bounds.Height - AxisHeight;
        var volumeHeight = (bottom - top) * VolumeShare;
        var priceBottom = bottom - volumeHeight - VolumeGap;
        var priceHeight = Math.Max(10, priceBottom - top);
        var span = Math.Max(1e-6, _end - _start);
        var perCandle = plotWidth / span;
        var min = _min;
        var max = Math.Max(_max, min + 1e-6);
        double X(double index) => (index - _start) * perCandle;
        double Y(double price) => top + (max - price) / (max - min) * priceHeight;

        // Price grid and axis labels (a label under the live or crosshair tag is left out).
        var tick = NiceStep((max - min) / Math.Max(2, priceHeight / 64));
        var size = FontSize;
        var liveY = Y(_shownClose);
        var crossY = _cross >= 0 && !double.IsNaN(_crossY) ? _crossY : double.NaN;
        for (var p = Math.Ceiling(min / tick) * tick; p <= max; p += tick)
        {
            var y = Math.Round(Y(p)) + 0.5;
            if (y < top + 4 || y > priceBottom - 4)
            {
                continue;
            }
            context.DrawLine(_gridPen!, new Point(0, y), new Point(plotWidth, y));
            if (Math.Abs(y - liveY) < TagHeight - 4 || Math.Abs(y - crossY) < TagHeight - 4)
            {
                continue;
            }
            var label = _text.Get(FinanceFormat.Price(p), size, FontWeight.Normal, AxisBrush ?? Brushes.Gray);
            label.Draw(context, new Point(plotWidth + 12, y - label.Height / 2));
        }

        // Time grid and axis labels.
        var first = Math.Max(0, (int)Math.Floor(_start));
        var last = Math.Min(series.Count - 1, (int)Math.Ceiling(_end));
        CollectTimeLabels(series, first, last, X, plotWidth);
        foreach (var (x, text) in _xLabels)
        {
            var gx = Math.Round(x) + 0.5;
            context.DrawLine(_gridPen!, new Point(gx, top), new Point(gx, bottom));
            var label = _text.Get(text, size, FontWeight.Normal, AxisBrush ?? Brushes.Gray);
            var lx = Math.Clamp(x - label.WidthIncludingTrailingWhitespace / 2, 0, plotWidth - label.WidthIncludingTrailingWhitespace);
            label.Draw(context, new Point(lx, bottom + (AxisHeight - label.Height) / 2 + 2));
        }
        context.DrawLine(_gridPen!, new Point(plotWidth + 0.5, top), new Point(plotWidth + 0.5, bottom));

        // Volume.
        var bodyWidth = Math.Max(1, Math.Min(perCandle * 0.62, 14));
        using (context.PushClip(new Rect(0, priceBottom, plotWidth, bottom - priceBottom)))
        {
            double maxVolume = 1;
            for (var i = first; i <= last; i++)
            {
                maxVolume = Math.Max(maxVolume, series[i].Volume);
            }
            for (var i = first; i <= last; i++)
            {
                ref var c = ref series[i];
                var close = i == series.Count - 1 ? _shownClose : c.Close;
                var up = close >= c.Open;
                var h = Math.Max(1, c.Volume / maxVolume * volumeHeight);
                var cx = X(i + 0.5);
                context.FillRectangle(up ? _upVolume! : _downVolume!, new Rect(cx - bodyWidth / 2, bottom - h, bodyWidth, h));
            }
        }

        using (context.PushClip(new Rect(0, top - 2, plotWidth, priceBottom - top + 4)))
        {
            // Candles: colored by open to close; drawn softer when the close went against the previous close.
            for (var i = first; i <= last; i++)
            {
                ref var c = ref series[i];
                var isLive = i == series.Count - 1;
                var close = isLive ? _shownClose : c.Close;
                var high = isLive ? Math.Max(c.High, close) : c.High;
                var low = isLive ? Math.Min(c.Low, close) : c.Low;
                var up = close >= c.Open;
                var previous = i > 0 ? series[i - 1].Close : series.Candles[Math.Max(0, series.Warmup - 1)].Close;
                var agrees = up == close >= previous;
                var cx = Math.Round(X(i + 0.5)) + 0.5;
                context.DrawLine(up ? _upPen! : _downPen!, new Point(cx, Y(high)), new Point(cx, Y(low)));
                var yOpen = Y(c.Open);
                var yClose = Y(close);
                var bodyTop = Math.Min(yOpen, yClose);
                var bodyHeight = Math.Max(1, Math.Abs(yClose - yOpen));
                var fill = agrees ? Immutable(up ? UpBrush : DownBrush) : up ? _upSoft! : _downSoft!;
                context.FillRectangle(fill, new Rect(cx - bodyWidth / 2, bodyTop, bodyWidth, bodyHeight));
            }

            // Moving averages.
            if (ShowMa50)
            {
                DrawAverage(context, series, first, last, _ma50Pen!, series.Ma50, X, Y);
            }
            if (ShowMa20)
            {
                DrawAverage(context, series, first, last, _ma20Pen!, series.Ma20, X, Y);
            }
        }

        // Live price line and tag (colored by today's change).
        var liveUp = _shownClose >= _previousClose;
        var liveBrush = Immutable(liveUp ? UpBrush : DownBrush);
        var ly = Math.Round(Math.Clamp(Y(_shownClose), top, priceBottom)) + 0.5;
        context.DrawLine(liveUp ? _upDash! : _downDash!, new Point(0, ly), new Point(plotWidth, ly));
        DrawTag(context, FinanceFormat.Price(_shownClose), liveBrush, bounds.Width - TagWidth, ly, TagWidth);

        // Crosshair.
        if (_cross >= 0 && _cross < series.Count)
        {
            var cx = Math.Round(X(_cross + 0.5)) + 0.5;
            if (cx >= 0 && cx <= plotWidth)
            {
                context.DrawLine(_crossPen!, new Point(cx, top), new Point(cx, bottom));
                var crossPrice = !double.IsNaN(_crossY) && _crossY >= top && _crossY <= priceBottom
                    ? max - (_crossY - top) / priceHeight * (max - min)
                    : (_cross == series.Count - 1 ? _shownClose : series[_cross].Close);
                var cy = Math.Round(Y(crossPrice)) + 0.5;
                context.DrawLine(_crossPen!, new Point(0, cy), new Point(plotWidth, cy));
                DrawTag(context, FinanceFormat.Price(crossPrice), Immutable(StrongBrush), bounds.Width - TagWidth, cy, TagWidth);
                var time = Labels.CrosshairTime(series[_cross].Time, series.Range);
                var layout = _text.Get(time, size, FontWeight.Medium, TagTextBrush ?? Brushes.White);
                var w = layout.WidthIncludingTrailingWhitespace + 16;
                var tx = Math.Clamp(cx - w / 2, 0, plotWidth - w);
                DrawTag(context, time, Immutable(StrongBrush), tx, bottom + AxisHeight / 2, w);
            }
        }

        DrawReadout(context, series, bounds.Width);

        if (_focusVisible && IsFocused)
        {
            context.DrawRectangle(null, _focusPen!, new RoundedRect(new Rect(bounds.Size).Inflate(3), 12));
        }
        _text.Sweep();
    }

    private void DrawAverage(DrawingContext context, MarketSeries series, int first, int last, IPen pen, double[] values,
        Func<double, double> x, Func<double, double> y)
    {
        var from = Math.Max(0, first - 1);
        var to = Math.Min(series.Count - 1, last + 1);
        var hasPrevious = false;
        Point previous = default;
        for (var i = from; i <= to; i++)
        {
            var value = values[series.Warmup + i];
            if (double.IsNaN(value))
            {
                hasPrevious = false;
                continue;
            }
            if (i == series.Count - 1)
            {
                // The live average moves with the glide of the live close.
                value += (_shownClose - series.Last.Close) / (ReferenceEquals(values, series.Ma20) ? 20 : 50);
            }
            var point = new Point(x(i + 0.5), y(value));
            if (hasPrevious)
            {
                context.DrawLine(pen, previous, point);
            }
            previous = point;
            hasPrevious = true;
        }
    }

    private void DrawTag(DrawingContext context, string text, IBrush background, double x, double centerY, double width)
    {
        var rect = new Rect(x, centerY - TagHeight / 2, width, TagHeight);
        context.FillRectangle(background, rect, 6);
        var layout = _text.Get(text, FontSize, FontWeight.Medium, TagTextBrush ?? Brushes.White);
        layout.Draw(context, new Point(rect.X + (width - layout.WidthIncludingTrailingWhitespace) / 2, rect.Y + (TagHeight - layout.Height) / 2));
    }

    private void CollectTimeLabels(MarketSeries series, int first, int last, Func<double, double> x, double plotWidth)
    {
        _xLabels.Clear();
        double lastX = double.NegativeInfinity;
        for (var i = Math.Max(1, first); i <= last; i++)
        {
            var time = series[i].Time;
            var previous = series[i - 1].Time;
            var boundary = series.Range switch
            {
                MarketRange.OneDay => time.Hour != previous.Hour,
                MarketRange.FiveDays => time.Date != previous.Date,
                MarketRange.OneMonth => time.DayOfWeek < previous.DayOfWeek || (time - previous).TotalDays > 2,
                _ => time.Month != previous.Month,
            };
            if (!boundary)
            {
                continue;
            }
            var px = x(i);
            if (px < 8 || px > plotWidth - 8 || px - lastX < 64)
            {
                continue;
            }
            _xLabels.Add((px, Labels.AxisTime(time, series.Range)));
            lastX = px;
        }
    }

    private static double NiceStep(double raw)
    {
        if (raw <= 0 || double.IsNaN(raw))
        {
            return 1;
        }
        var power = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var unit = raw / power;
        var nice = unit < 1.5 ? 1 : unit < 2.25 ? 2 : unit < 3.5 ? 2.5 : unit < 7.5 ? 5 : 10;
        return nice * power;
    }

    /// <summary>The readout's two groups: the candle (time, OHLC, change) and the volume and averages.</summary>
    private double ReadoutWidth(int group)
    {
        if (_series is null)
        {
            return 0;
        }
        var index = _cross >= 0 ? _cross : _series.Count - 1;
        return DrawReadoutGroup(null, _series, index, group, 0, 0);
    }

    private void DrawReadout(DrawingContext context, MarketSeries series, double width)
    {
        var index = _cross >= 0 ? _cross : series.Count - 1;
        var lines = ReadoutLines(width);
        var first = DrawReadoutGroup(context, series, index, 0, 0, 0);
        if (lines == 1)
        {
            DrawReadoutGroup(context, series, index, 1, first + 16, 0);
        }
        else
        {
            DrawReadoutGroup(context, series, index, 1, 0, ReadoutLineHeight);
        }
    }

    /// <summary>Draws (or with a null context, measures) one readout group starting at x; returns its width.</summary>
    private double DrawReadoutGroup(DrawingContext? context, MarketSeries series, int index, int group, double x, double y)
    {
        ref var c = ref series[index];
        var isLive = index == series.Count - 1;
        var close = isLive ? _shownClose : c.Close;
        var size = FontSize;
        var label = LabelBrush ?? Brushes.Gray;
        var value = ValueBrush ?? Brushes.Gray;
        var start = x;

        void Piece(string text, IBrush brush, FontWeight weight, double gapAfter)
        {
            var layout = _text.Get(text, size, weight, brush);
            if (context is not null)
            {
                layout.Draw(context, new Point(x, y));
            }
            x += layout.WidthIncludingTrailingWhitespace + gapAfter;
        }

        if (group == 0)
        {
            Piece(Labels.ReadoutTime(c.Time, series.Range), StrongBrush ?? Brushes.Black, FontWeight.Medium, 16);
            Piece("O", label, FontWeight.Normal, 4);
            Piece(FinanceFormat.Price(c.Open), value, FontWeight.Normal, 12);
            Piece("H", label, FontWeight.Normal, 4);
            Piece(FinanceFormat.Price(isLive ? Math.Max(c.High, close) : c.High), value, FontWeight.Normal, 12);
            Piece("L", label, FontWeight.Normal, 4);
            Piece(FinanceFormat.Price(isLive ? Math.Min(c.Low, close) : c.Low), value, FontWeight.Normal, 12);
            Piece("C", label, FontWeight.Normal, 4);
            Piece(FinanceFormat.Price(close), value, FontWeight.Normal, 12);
            var basis = BasisOf(series, index);
            var change = basis > 0 ? (close - basis) / basis : 0;
            Piece(FinanceFormat.SignedPercent(change, 2), change >= 0 ? UpBrush ?? value : DownBrush ?? value, FontWeight.Medium, 0);
        }
        else
        {
            Piece(Labels.VolumeShort, label, FontWeight.Normal, 4);
            Piece(FinanceFormat.Volume(c.Volume), value, FontWeight.Normal, 12);
            if (ShowMa20 && !double.IsNaN(series.Ma20At(index)))
            {
                Piece(Labels.Ma20, Ma20Brush ?? value, FontWeight.Normal, 4);
                Piece(FinanceFormat.Price(series.Ma20At(index)), value, FontWeight.Normal, 12);
            }
            if (ShowMa50 && !double.IsNaN(series.Ma50At(index)))
            {
                Piece(Labels.Ma50, Ma50Brush ?? value, FontWeight.Normal, 4);
                Piece(FinanceFormat.Price(series.Ma50At(index)), value, FontWeight.Normal, 0);
            }
        }
        return x - start;
    }

    /// <summary>The close a candle's change is measured from: the previous close for daily candles, yesterday's close intraday.</summary>
    private double BasisOf(MarketSeries series, int index)
    {
        if (series.Range is MarketRange.OneDay or MarketRange.FiveDays)
        {
            if (series.Range == MarketRange.OneDay)
            {
                return _previousClose;
            }
            var day = series[index].Time.Date;
            for (var i = series.Warmup + index - 1; i >= 0; i--)
            {
                if (series.Candles[i].Time.Date != day)
                {
                    return series.Candles[i].Close;
                }
            }
            return series[index].Open;
        }
        return series.Warmup + index > 0 ? series.Candles[series.Warmup + index - 1].Close : series[index].Open;
    }

    /// <summary>What the crosshair candle is, for the screen reader announcement.</summary>
    public MarketCandleDescription Describe(int index)
    {
        var series = _series!;
        ref var c = ref series[index];
        var isLive = index == series.Count - 1;
        var close = isLive ? series.Last.Close : c.Close;
        var basis = BasisOf(series, index);
        return new MarketCandleDescription(Labels.ReadoutTime(c.Time, series.Range), c.Open, c.High, c.Low, close,
            basis > 0 ? (close - basis) / basis : 0, c.Volume);
    }

    #endregion

}

/// <summary>A candle as the screen reader hears it.</summary>
public sealed record MarketCandleDescription(string Time, double Open, double High, double Low, double Close, double Change, double Volume);
