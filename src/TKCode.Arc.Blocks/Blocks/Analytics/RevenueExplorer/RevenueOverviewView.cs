using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>Which part of the overview strip a key or pointer belongs to.</summary>
internal enum RevenueOverviewPart
{
    /// <summary>The window between the handles.</summary>
    Window = 0,

    /// <summary>The left handle (the first month).</summary>
    Start = 1,

    /// <summary>The right handle (the last month).</summary>
    End = 2,

    /// <summary>The empty track.</summary>
    Track = 3
}

/// <summary>
/// One of the overview's three keyboard stops (the window and its two handles): a zero-size focusable that takes the keys
/// and names itself for screen readers; the strip itself draws the focus ring and handles the pointer.
/// </summary>
internal sealed class RevenueOverviewThumb : Control
{
    public RevenueOverviewThumb(RevenueOverviewPart part)
    {
        Part = part;
        Focusable = true;
        IsHitTestVisible = false;
    }

    /// <summary>The part this stop stands for.</summary>
    public RevenueOverviewPart Part { get; }

    /// <summary>Gives the peer its range: minimum, maximum, value and value text.</summary>
    public Func<RevenueOverviewPart, (double Min, double Max, double Value, string Text)>? Describe { get; set; }

    /// <summary>Raised on a stepping key: the part and the step (−1, +1, ×3 with Shift; int.MinValue / MaxValue for Home / End).</summary>
    public event Action<RevenueOverviewPart, int>? StepRequested;

    /// <summary>Raised when the stop gains or loses focus.</summary>
    public event Action? FocusVisualChanged;

    /// <summary>Applies a step as a screen reader's SetValue (absolute month).</summary>
    public event Action<RevenueOverviewPart, double>? ValueRequested;

    internal void Request(double value) => ValueRequested?.Invoke(Part, value);

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        FocusVisualChanged?.Invoke();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        FocusVisualChanged?.Invoke();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        var unit = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 3 : 1;
        int? step = e.Key switch
        {
            Key.Left or Key.Down => -unit,
            Key.Right or Key.Up => unit,
            Key.Home => int.MinValue,
            Key.End => int.MaxValue,
            _ => null
        };
        if (step is { } s)
        {
            StepRequested?.Invoke(Part, s);
            e.Handled = true;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RevenueOverviewThumbPeer(this);
}

/// <summary>A thumb's peer: a slider (the window's value is its first month, a handle's its own month).</summary>
internal sealed class RevenueOverviewThumbPeer(RevenueOverviewThumb owner) : ControlAutomationPeer(owner), IRangeValueProvider, IValueProvider
{
    private RevenueOverviewThumb Thumb => (RevenueOverviewThumb)Owner;

    private (double Min, double Max, double Value, string Text) State => Thumb.Describe?.Invoke(Thumb.Part) ?? (0, 0, 0, string.Empty);

    public bool IsReadOnly => false;

    public double Minimum => State.Min;

    public double Maximum => State.Max;

    double IRangeValueProvider.Value => State.Value;

    string? IValueProvider.Value => State.Text;

    public double SmallChange => 1;

    public double LargeChange => 3;

    public void SetValue(double value) => Thumb.Request(value);

    public void SetValue(string? value)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
}

/// <summary>
/// The overview strip under the chart: every month as small layers (new plus expansion above, churn below), year ticks, and
/// the selected window with a handle on each side. Dragging the window follows the pointer pixel by pixel while the range
/// is the nearest whole months; a handle moves one edge (never closer than the minimum); a click on the track centres the
/// window there. The three stops take the keys. The window and the chart share one <see cref="RevenueViewport"/>.
/// </summary>
/// <remarks>
/// Why code-driven: the window's edges are coordinates in one drawing that follow the pointer one to one and then snap on a
/// Gentle spring; hover and press mixes are tweens read in the same Render.
/// </remarks>
internal sealed class RevenueOverviewView : Control
{
    #region Geometry properties (from the token)

    public static readonly StyledProperty<double> OverviewHeightProperty = AvaloniaProperty.Register<RevenueOverviewView, double>(nameof(OverviewHeight), 62);
    public static readonly StyledProperty<double> BrushHeightProperty = AvaloniaProperty.Register<RevenueOverviewView, double>(nameof(BrushHeight), 44);
    public static readonly StyledProperty<double> LeftInsetProperty = AvaloniaProperty.Register<RevenueOverviewView, double>(nameof(LeftInset), 48);
    public static readonly StyledProperty<double> HandleWidthProperty = AvaloniaProperty.Register<RevenueOverviewView, double>(nameof(HandleWidth), 16);
    public static readonly StyledProperty<double> GripWidthProperty = AvaloniaProperty.Register<RevenueOverviewView, double>(nameof(GripWidth), 6);
    public static readonly StyledProperty<double> GripHeightProperty = AvaloniaProperty.Register<RevenueOverviewView, double>(nameof(GripHeight), 20);
    public static readonly StyledProperty<double> WindowRadiusProperty = AvaloniaProperty.Register<RevenueOverviewView, double>(nameof(WindowRadius), 10);

    public double OverviewHeight { get => GetValue(OverviewHeightProperty); set => SetValue(OverviewHeightProperty, value); }
    public double BrushHeight { get => GetValue(BrushHeightProperty); set => SetValue(BrushHeightProperty, value); }
    public double LeftInset { get => GetValue(LeftInsetProperty); set => SetValue(LeftInsetProperty, value); }
    public double HandleWidth { get => GetValue(HandleWidthProperty); set => SetValue(HandleWidthProperty, value); }
    public double GripWidth { get => GetValue(GripWidthProperty); set => SetValue(GripWidthProperty, value); }
    public double GripHeight { get => GetValue(GripHeightProperty); set => SetValue(GripHeightProperty, value); }
    public double WindowRadius { get => GetValue(WindowRadiusProperty); set => SetValue(WindowRadiusProperty, value); }

    #endregion

    private const double DataTop = 4;
    private const double DataBottom = 40;
    private const double TickBottom = 48;
    private const double LabelBaseline = 58;

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(64);
    private readonly LiveLoop _loop;
    private readonly RevenueOverviewThumb[] _thumbs;

    private RevenueData _data = RevenueData.Empty;
    private RevenueViewport? _viewport;
    private int _start;
    private int _end;
    private int _minMonths = 3;
    private Func<DateOnly, string> _yearText = m => m.Year.ToString(AnalyticsFormat.DefaultCulture);
    private bool _geometryDirty = true;
    private double _geometryWidth;
    private StreamGeometry? _posArea;
    private StreamGeometry? _negArea;
    private StreamGeometry? _posLine;
    private StreamGeometry? _negLine;
    private Point[] _posPoints = [];
    private Point[] _negPoints = [];
    private Point[] _zeroPoints = [];

    private RevenueOverviewPart _hover = RevenueOverviewPart.Track;
    private RevenueOverviewPart _drag = RevenueOverviewPart.Track;
    private bool _pressed;
    private double _pressX;
    private double _pressStart;
    private double _pressEnd;
    private bool _moved;
    private int _paletteVersion = -1;
    private AnalyticsTween _windowHover = new(0);
    private AnalyticsTween _windowActive = new(0);
    private AnalyticsTween _gripHoverStart = new(0);
    private AnalyticsTween _gripHoverEnd = new(0);
    private AnalyticsTween _gripPressStart = new(0);
    private AnalyticsTween _gripPressEnd = new(0);

    static RevenueOverviewView()
    {
        AffectsMeasure<RevenueOverviewView>(OverviewHeightProperty);
        AffectsRender<RevenueOverviewView>(BrushHeightProperty, LeftInsetProperty, HandleWidthProperty, GripWidthProperty, GripHeightProperty,
            WindowRadiusProperty);
    }

    public RevenueOverviewView()
    {
        _thumbs = [new(RevenueOverviewPart.Window), new(RevenueOverviewPart.Start), new(RevenueOverviewPart.End)];
        foreach (var thumb in _thumbs)
        {
            thumb.StepRequested += OnStep;
            thumb.FocusVisualChanged += InvalidateVisual;
            thumb.ValueRequested += OnValueRequested;
            thumb.Describe = Describe;
            VisualChildren.Add(thumb);
            LogicalChildren.Add(thumb);
        }
        // Function as well as decoration: the loop also lands hover and press mixes under reduced motion.
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        ActualThemeVariantChanged += (_, _) => InvalidatePalette();
        ResourcesChanged += (_, _) => InvalidatePalette();
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    /// <summary>Raised when the person asks for a new whole-month range (drag, keys, click); the block applies it.</summary>
    public event Action<int, int, RevenueRangeSource>? RangeRequested;

    /// <summary>Raised when a drag or click starts or ends.</summary>
    public event Action<bool>? DraggingChanged;

    /// <summary>The window's keyboard stops (tests): window, start handle, end handle.</summary>
    internal IReadOnlyList<Control> Thumbs => _thumbs;

    /// <summary>Texts for the stops: names are set by the block through <see cref="AutomationProperties"/> on each thumb.</summary>
    public Func<int, int, string> RangeText { get; set; } = (a, b) => $"{a} – {b}";

    /// <summary>Months the window keeps at least (3).</summary>
    public int MinMonths
    {
        get => _minMonths;
        set => _minMonths = Math.Max(2, value);
    }

    /// <summary>Year label text.</summary>
    public Func<DateOnly, string> YearText
    {
        get => _yearText;
        set
        {
            _yearText = value;
            InvalidateVisual();
        }
    }

    /// <summary>The shared window (set once by the block).</summary>
    public RevenueViewport? Viewport
    {
        get => _viewport;
        set
        {
            if (_viewport is not null)
            {
                _viewport.Changed -= InvalidateVisual;
            }
            _viewport = value;
            if (value is not null)
            {
                value.Changed += InvalidateVisual;
            }
            InvalidateVisual();
        }
    }

    /// <summary>True while the pointer holds the window or a handle.</summary>
    public bool IsDragging => _pressed && _moved;

    /// <summary>Sets the months drawn.</summary>
    public void SetData(RevenueData data)
    {
        _data = data;
        _geometryDirty = true;
        _posPoints = new Point[data.Count];
        _negPoints = new Point[data.Count];
        _zeroPoints = new Point[data.Count];
        InvalidateVisual();
    }

    /// <summary>Tells the strip the whole-month range in effect.</summary>
    public void SetRange(int start, int end)
    {
        _start = start;
        _end = end;
        InvalidateVisual();
    }

    private ArcMotionLevel Level => this.IsAttachedToVisualTree() ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;

    private double Step => _data.Count > 1 ? Math.Max(0, Bounds.Width - LeftInset) / (_data.Count - 1) : 0;

    private double XOf(double month) => LeftInset + month * Step;

    private double WindowLeft => XOf(_viewport?.WindowStart ?? _start);

    private double WindowRight => XOf(_viewport?.WindowEnd ?? _end);

    private (double Min, double Max, double Value, string Text) Describe(RevenueOverviewPart part)
    {
        var last = Math.Max(0, _data.Count - 1);
        var minSpan = Math.Max(1, _minMonths - 1);
        return part switch
        {
            RevenueOverviewPart.Window => (0, Math.Max(0, last - (_end - _start)), _start, RangeText(_start, _end)),
            RevenueOverviewPart.Start => (0, Math.Max(0, _end - minSpan), _start, RangeText(_start, _start)),
            _ => (Math.Min(last, _start + minSpan), last, _end, RangeText(_end, _end))
        };
    }

    #region Keys

    private void OnStep(RevenueOverviewPart part, int step)
    {
        var (s, e) = _data.Step((int)part, _start, _end, step, _minMonths);
        if (s != _start || e != _end)
        {
            RangeRequested?.Invoke(s, e, RevenueRangeSource.Keyboard);
        }
    }

    private void OnValueRequested(RevenueOverviewPart part, double value)
    {
        var target = (int)Math.Round(value);
        var step = part switch
        {
            RevenueOverviewPart.Window => target - _start,
            RevenueOverviewPart.Start => target - _start,
            _ => target - _end
        };
        OnStep(part, step);
    }

    #endregion

    #region Pointer

    private RevenueOverviewPart HitPart(Point p)
    {
        if (_data.Count < 2 || p.Y < 0 || p.Y > BrushHeight)
        {
            return RevenueOverviewPart.Track;
        }
        var left = WindowLeft;
        var right = WindowRight;
        var half = HandleWidth / 2;
        var dStart = Math.Abs(p.X - left);
        var dEnd = Math.Abs(p.X - right);
        if (Math.Min(dStart, dEnd) <= half)
        {
            return dStart <= dEnd ? RevenueOverviewPart.Start : RevenueOverviewPart.End;
        }
        return p.X > left && p.X < right ? RevenueOverviewPart.Window : RevenueOverviewPart.Track;
    }

    private void SetHover(RevenueOverviewPart part)
    {
        if (part == _hover)
        {
            return;
        }
        _hover = part;
        var level = Level;
        Mix(ref _windowHover, part == RevenueOverviewPart.Window ? 1 : 0, level);
        Mix(ref _gripHoverStart, part == RevenueOverviewPart.Start ? 1 : 0, level);
        Mix(ref _gripHoverEnd, part == RevenueOverviewPart.End ? 1 : 0, level);
        Cursor = new Cursor(part is RevenueOverviewPart.Start or RevenueOverviewPart.End ? StandardCursorType.SizeWestEast : StandardCursorType.Hand);
        _loop.Start();
    }

    private static void Mix(ref AnalyticsTween tween, double target, ArcMotionLevel level)
    {
        if (level == ArcMotionLevel.Full)
        {
            tween.Start(target, ArcMotionPresets.Fast, AnalyticsEasings.Standard);
        }
        else
        {
            tween.Jump(target);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        if (_pressed)
        {
            Drag(p);
            return;
        }
        SetHover(HitPart(p));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!_pressed)
        {
            SetHover(RevenueOverviewPart.Track);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || _data.Count < 2)
        {
            return;
        }
        var p = e.GetPosition(this);
        var part = HitPart(p);
        _drag = part;
        _pressed = true;
        _moved = false;
        _pressX = p.X;
        _pressStart = _viewport?.WindowStart ?? _start;
        _pressEnd = _viewport?.WindowEnd ?? _end;
        e.Pointer.Capture(this);
        e.Handled = true;
        var level = Level;
        if (part == RevenueOverviewPart.Window)
        {
            Mix(ref _windowActive, 1, level);
        }
        else if (part == RevenueOverviewPart.Start)
        {
            Mix(ref _gripPressStart, 1, level);
        }
        else if (part == RevenueOverviewPart.End)
        {
            Mix(ref _gripPressEnd, 1, level);
        }
        if (part != RevenueOverviewPart.Track)
        {
            Cursor = new Cursor(part == RevenueOverviewPart.Window ? StandardCursorType.DragMove : StandardCursorType.SizeWestEast);
            // Keys continue where the pointer worked: the stop that matches the part takes focus.
            _thumbs[(int)part].Focus(NavigationMethod.Pointer);
        }
        _loop.Start();
        InvalidateVisual();
    }

    private void Drag(Point p)
    {
        if (_drag == RevenueOverviewPart.Track || _viewport is null || Step <= 0)
        {
            return;
        }
        var delta = (p.X - _pressX) / Step;
        if (!_moved && Math.Abs(p.X - _pressX) < 1)
        {
            return;
        }
        if (!_moved)
        {
            _moved = true;
            DraggingChanged?.Invoke(true);
        }
        var last = _data.Count - 1;
        var minSpan = Math.Max(1, _minMonths - 1);
        double s = _pressStart;
        double e = _pressEnd;
        switch (_drag)
        {
            case RevenueOverviewPart.Window:
            {
                var span = _pressEnd - _pressStart;
                s = Math.Clamp(_pressStart + delta, 0, last - span);
                e = s + span;
                break;
            }
            case RevenueOverviewPart.Start:
                s = Math.Clamp(_pressStart + delta, 0, Math.Max(0, _pressEnd - minSpan));
                break;
            default:
                e = Math.Clamp(_pressEnd + delta, Math.Min(last, _pressStart + minSpan), last);
                break;
        }
        // The window follows the pointer; the range is the nearest whole months, reported when it changes.
        _viewport.Drag(s, e);
        var rs = (int)Math.Round(s, MidpointRounding.AwayFromZero);
        var re = (int)Math.Round(e, MidpointRounding.AwayFromZero);
        (rs, re) = _data.ClampRange(rs, re, _minMonths);
        if (rs != _start || re != _end)
        {
            RangeRequested?.Invoke(rs, re, RevenueRangeSource.Drag);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        _pressed = false;
        e.Pointer.Capture(null);
        var level = Level;
        Mix(ref _windowActive, 0, level);
        Mix(ref _gripPressStart, 0, level);
        Mix(ref _gripPressEnd, 0, level);
        if (_drag == RevenueOverviewPart.Track)
        {
            var month = (e.GetPosition(this).X - LeftInset) / Math.Max(1e-6, Step);
            var target = _data.CenterOn(month, _start, _end);
            var span = _end - _start;
            if (target != _start)
            {
                RangeRequested?.Invoke(target, target + span, RevenueRangeSource.Click);
            }
        }
        else if (_moved)
        {
            _viewport?.Release(_start, _end);
            DraggingChanged?.Invoke(false);
        }
        _moved = false;
        _drag = RevenueOverviewPart.Track;
        _hover = RevenueOverviewPart.Track;
        SetHover(HitPart(e.GetPosition(this)));
        _loop.Start();
        InvalidateVisual();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_pressed)
        {
            _pressed = false;
            if (_moved)
            {
                _viewport?.Release(_start, _end);
                DraggingChanged?.Invoke(false);
            }
            _moved = false;
            _drag = RevenueOverviewPart.Track;
            _windowActive.Jump(0);
            _gripPressStart.Jump(0);
            _gripPressEnd.Jump(0);
            InvalidateVisual();
        }
    }

    private bool Advance(double dt)
    {
        var moving = _windowHover.Advance(dt);
        moving |= _windowActive.Advance(dt);
        moving |= _gripHoverStart.Advance(dt);
        moving |= _gripHoverEnd.Advance(dt);
        moving |= _gripPressStart.Advance(dt);
        moving |= _gripPressEnd.Advance(dt);
        InvalidateVisual();
        return moving;
    }

    #endregion

    #region Layout

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width;
        foreach (var thumb in _thumbs)
        {
            thumb.Measure(default);
        }
        return new Size(width, OverviewHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var thumb in _thumbs)
        {
            thumb.Arrange(new Rect(0, 0, 0, 0));
        }
        return finalSize;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _geometryDirty = true;
    }

    private void InvalidatePalette()
    {
        _paletteVersion = -1;
        _text.Clear();
        InvalidateVisual();
    }

    #endregion

    #region Render

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        if (!_palette.IsReady || _paletteVersion < 0)
        {
            _palette.Refresh(this);
            _paletteVersion = _palette.Version;
        }
        if (_data.Count < 2 || size.Width <= LeftInset)
        {
            return;
        }
        if (_geometryDirty || Math.Abs(_geometryWidth - size.Width) > 0.01)
        {
            BuildGeometry(size.Width);
        }
        var body = new Typeface(_palette.BodyFont);
        DrawData(context);
        DrawYears(context, body, size);
        DrawWindow(context, size);
        DrawFocus(context);
    }

    private void BuildGeometry(double width)
    {
        _geometryDirty = false;
        _geometryWidth = width;
        var n = _data.Count;
        double max = 0;
        double min = 0;
        for (var i = 0; i < n; i++)
        {
            max = Math.Max(max, _data.New[i] + _data.Expansion[i]);
            min = Math.Min(min, _data.Churn[i]);
        }
        if (max - min <= 0)
        {
            max = min + 1;
        }
        var scale = new AnalyticsLinearScale(min, max, DataBottom, DataTop);
        var zero = scale.Map(0);
        for (var i = 0; i < n; i++)
        {
            var x = XOf(i);
            _posPoints[i] = new Point(x, scale.Map(_data.New[i] + _data.Expansion[i]));
            _negPoints[i] = new Point(x, scale.Map(_data.Churn[i]));
            _zeroPoints[i] = new Point(x, zero);
        }
        _posArea = AnalyticsGeometry.Band(_posPoints, _zeroPoints, AnalyticsCurve.Monotone);
        _negArea = AnalyticsGeometry.Band(_zeroPoints, _negPoints, AnalyticsCurve.Monotone);
        _posLine = AnalyticsGeometry.Line(_posPoints, AnalyticsCurve.Monotone);
        _negLine = AnalyticsGeometry.Line(_negPoints, AnalyticsCurve.Monotone);
    }

    private void DrawData(DrawingContext context)
    {
        if (_posArea is null || _negArea is null || _posLine is null || _negLine is null)
        {
            return;
        }
        context.DrawGeometry(_palette.Brush(_palette.TextMuted, 0.14), null, _posArea);
        context.DrawGeometry(_palette.Brush(_palette.TextMuted, 0.08), null, _negArea);
        var pen = _palette.Pen(_palette.TextMuted, 1);
        context.DrawGeometry(null, pen, _posLine);
        context.DrawGeometry(null, pen, _negLine);
    }

    private void DrawYears(DrawingContext context, Typeface body, Size size)
    {
        var tick = _palette.Pen(_palette.Border, 1, cap: PenLineCap.Flat);
        var brush = _palette.Brush(_palette.TextMuted);
        for (var i = 0; i < _data.Count; i++)
        {
            if (_data.Months[i].Month != 1)
            {
                continue;
            }
            var x = Math.Round(XOf(i), MidpointRounding.AwayFromZero) + 0.5;
            context.DrawLine(tick, new Point(x, 0), new Point(x, TickBottom));
            _text.Get(_yearText(_data.Months[i]), body, 11).DrawAtBaseline(context, brush, x + 5, LabelBaseline);
        }
    }

    private Color MixColor(Color from, Color to, double t) => AnalyticsColors.Lerp(from, to, Math.Clamp(t, 0, 1));

    private void DrawWindow(DrawingContext context, Size size)
    {
        var left = WindowLeft;
        var right = WindowRight;
        var h = BrushHeight;
        var dim = _palette.Brush(_palette.Surface, 0.72);
        if (left > LeftInset)
        {
            context.FillRectangle(dim, new Rect(LeftInset, 0, left - LeftInset, h));
        }
        if (right < size.Width)
        {
            context.FillRectangle(dim, new Rect(right, 0, size.Width - right, h));
        }
        var rect = new Rect(left, 0, Math.Max(0, right - left), h);
        // Border: BorderStrong → TextMuted on hover → Accent while held (160ms mixes).
        var border = MixColor(MixColor(_palette.BorderStrong, _palette.TextMuted, _windowHover.Value), _palette.Accent, _windowActive.Value);
        context.DrawRectangle(_palette.Brush(_palette.Accent, 0.05), _palette.Pen(border, 1, cap: PenLineCap.Flat),
            new RoundedRect(rect.Deflate(0.5), WindowRadius));
        DrawGrip(context, left, _gripHoverStart.Value, _gripPressStart.Value);
        DrawGrip(context, right, _gripHoverEnd.Value, _gripPressEnd.Value);
    }

    private void DrawGrip(DrawingContext context, double edge, double hover, double press)
    {
        var rect = new Rect(edge - GripWidth / 2, (BrushHeight - GripHeight) / 2, GripWidth, GripHeight);
        var border = MixColor(MixColor(_palette.BorderStrong, _palette.Foreground, hover), _palette.Accent, press);
        var center = rect.Center;
        // scaleY(1.15) while pressed (580ms spring curve).
        var scaleY = 1 + 0.15 * press;
        using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(1, scaleY) * Matrix.CreateTranslation(center.X, center.Y)))
        {
            context.DrawRectangle(_palette.Brush(_palette.Surface), _palette.Pen(border, 1, cap: PenLineCap.Flat),
                new RoundedRect(rect.Deflate(0.5), 3), _palette.ShadowResting);
        }
    }

    private void DrawFocus(DrawingContext context)
    {
        var left = WindowLeft;
        var right = WindowRight;
        for (var i = 0; i < _thumbs.Length; i++)
        {
            if (!AnalyticsFocus.IsVisible(_thumbs[i]))
            {
                continue;
            }
            var part = (RevenueOverviewPart)i;
            if (part == RevenueOverviewPart.Window)
            {
                AnalyticsFocus.Draw(context, _palette, new Rect(left, 0, Math.Max(0, right - left), BrushHeight), WindowRadius);
            }
            else
            {
                var edge = part == RevenueOverviewPart.Start ? left : right;
                AnalyticsFocus.Draw(context, _palette, new Rect(edge - HandleWidth / 2, 0, HandleWidth, BrushHeight));
            }
        }
    }

    #endregion
}
