using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The drawing surface of a <see cref="CursorTrailDraw"/>: the dotted grid, every item, the stroke being drawn, the
/// morph of a snapping shape, the laser trail and the cursor, all drawn by this one control in board units (1000 wide).
/// It also owns the pointer, keyboard and intro input and the one frame loop that eases the pen tip, runs the snap
/// springs, ages the laser and moves the keyboard cursor.
/// </summary>
/// <remarks>
/// Code-driven motion: the pen tip follows the pointer per frame in substeps, the snap morph moves every resampled
/// point on a spring and the laser trail ages per point; none of this is a property a ControlTheme transition can
/// animate. The loop (Core <see cref="LiveLoop"/>) runs only while something moves and pauses out of view.
/// </remarks>
internal sealed class CursorTrailDrawBoard : Control
{
    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<CursorTrailDrawBoard, IBrush?>(nameof(GridBrush));

    public static readonly StyledProperty<IBrush?> InkBrushProperty =
        AvaloniaProperty.Register<CursorTrailDrawBoard, IBrush?>(nameof(InkBrush));

    public static readonly StyledProperty<IBrush?> AccentInkBrushProperty =
        AvaloniaProperty.Register<CursorTrailDrawBoard, IBrush?>(nameof(AccentInkBrush));

    public static readonly StyledProperty<IBrush?> DangerInkBrushProperty =
        AvaloniaProperty.Register<CursorTrailDrawBoard, IBrush?>(nameof(DangerInkBrush));

    public static readonly StyledProperty<double> AspectProperty =
        AvaloniaProperty.Register<CursorTrailDrawBoard, double>(nameof(Aspect), 1.6);

    public static readonly StyledProperty<double> GridSpacingProperty =
        AvaloniaProperty.Register<CursorTrailDrawBoard, double>(nameof(GridSpacing), 22);

    public static readonly StyledProperty<double> GridDotRadiusProperty =
        AvaloniaProperty.Register<CursorTrailDrawBoard, double>(nameof(GridDotRadius), 0.9);

    public static readonly StyledProperty<double> CleanWidthProperty =
        AvaloniaProperty.Register<CursorTrailDrawBoard, double>(nameof(CleanWidth), 3.37);

    /// <summary>Pen and laser cursor geometry in px: ring radii, ring thickness, nib and dot radii, laser head width.</summary>
    public static readonly StyledProperty<Thickness> CursorRadiiProperty =
        AvaloniaProperty.Register<CursorTrailDrawBoard, Thickness>(nameof(CursorRadii), new Thickness(8.4, 2.4, 11, 4.5));

    public static readonly StyledProperty<double> CursorThicknessProperty =
        AvaloniaProperty.Register<CursorTrailDrawBoard, double>(nameof(CursorThickness), 1.24);

    public static readonly StyledProperty<double> LaserWidthProperty =
        AvaloniaProperty.Register<CursorTrailDrawBoard, double>(nameof(LaserWidth), 7);

    /// <summary>Board width in units; heights follow the control's shape.</summary>
    public const double UnitsWide = 1000;

    // Arc Cursor trail draw (design reference): tip time constant 21ms at smoothing 0.5, widths 1.96 + 2.95·p px at
    // size 4 (≈ size·(0.49 + 0.74·p)), mouse pressure 1 when slow falling to 0.35 above 2300 px/s, start taper 14.5 px,
    // end taper 18 px, snap spring stiffness 270 / damping 19.1, clean swap 680ms after release, keyboard 340 px/s.
    private const double SubStep = 0.004;
    private const double PressureTime = 0.035;
    private const double StartTaperPx = 14.5;
    private const double EndTaperPx = 18;
    private const double SampleSpacingPx = 0.75;
    private const double SnapSwap = 0.68;
    private const double KeySpeedPx = 340;
    private const double KeySlowPx = 110;
    private const double FadeSeconds = 0.15;
    private const int MorphSamples = 96;
    private const double MinimumSnapPx = 24;

    private readonly LiveLoop _loop;
    private readonly List<BoardItem> _items = new();
    private readonly List<Point> _live = new(1024);
    private readonly List<double> _liveWidths = new(1024);
    private readonly List<Point> _outline = new(2048);
    private readonly List<LaserPoint> _laser = new(256);
    private readonly List<Point> _laserPoints = new(256);
    private readonly List<double> _laserWidths = new(256);
    private readonly HashSet<Key> _held = new();
    private StreamGeometry? _gridGeometry;
    private Size _gridSize;
    private double _gridSpacing;
    private StreamGeometry? _liveGeometry;
    private bool _liveDirty;
    private StreamGeometry? _laserGeometry;
    private int _nextId;
    private double _clock;

    // Pointer / pen state (board units).
    private bool _down;
    private bool _downFromIntro;
    private PointerType _pointerType = PointerType.Mouse;
    private CursorTrailDrawTool _strokeTool;
    private string _strokeInk = string.Empty;
    private Point _target;
    private Point _previousTarget;
    private Point _tip;
    private double _pressure = 1;
    private double _pointerPressure = 0.5;
    private bool _usePointerPressure;

    // Cursor.
    private Point _cursor = new(500, 312.5);
    private bool _hovering;
    private bool _keyboard;
    private bool _keyboardPlaced;
    private double _ringOpacity;
    private double _nibOpacity;

    // Intro.
    private CursorTrailDrawIntro? _intro;
    private double _introTime;
    private int _introPart = -1;
    private CursorTrailDrawTool _introTool;

    static CursorTrailDrawBoard()
    {
        FocusableProperty.OverrideDefaultValue<CursorTrailDrawBoard>(true);
        ClipToBoundsProperty.OverrideDefaultValue<CursorTrailDrawBoard>(true);
        AffectsRender<CursorTrailDrawBoard>(GridBrushProperty, InkBrushProperty, AccentInkBrushProperty, DangerInkBrushProperty,
            GridSpacingProperty, GridDotRadiusProperty, CursorRadiiProperty, CursorThicknessProperty);
        AffectsMeasure<CursorTrailDrawBoard>(AspectProperty);
    }

    public CursorTrailDrawBoard()
    {
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Off };
        Cursor = new Cursor(StandardCursorType.None);
    }

    public IBrush? GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public IBrush? InkBrush { get => GetValue(InkBrushProperty); set => SetValue(InkBrushProperty, value); }
    public IBrush? AccentInkBrush { get => GetValue(AccentInkBrushProperty); set => SetValue(AccentInkBrushProperty, value); }
    public IBrush? DangerInkBrush { get => GetValue(DangerInkBrushProperty); set => SetValue(DangerInkBrushProperty, value); }
    public double Aspect { get => GetValue(AspectProperty); set => SetValue(AspectProperty, value); }
    public double GridSpacing { get => GetValue(GridSpacingProperty); set => SetValue(GridSpacingProperty, value); }
    public double GridDotRadius { get => GetValue(GridDotRadiusProperty); set => SetValue(GridDotRadiusProperty, value); }
    public double CleanWidth { get => GetValue(CleanWidthProperty); set => SetValue(CleanWidthProperty, value); }
    public Thickness CursorRadii { get => GetValue(CursorRadiiProperty); set => SetValue(CursorRadiiProperty, value); }
    public double CursorThickness { get => GetValue(CursorThicknessProperty); set => SetValue(CursorThicknessProperty, value); }
    public double LaserWidth { get => GetValue(LaserWidthProperty); set => SetValue(LaserWidthProperty, value); }

    internal CursorTrailDraw? Owner { get; set; }

    /// <summary>px per board unit.</summary>
    internal double Scale => Bounds.Width > 0 ? Bounds.Width / UnitsWide : 1;

    /// <summary>Board height in units.</summary>
    internal double UnitsHigh => Bounds.Width > 0 ? Bounds.Height / Scale : UnitsWide / Math.Max(0.1, Aspect);

    internal IReadOnlyList<BoardItem> Items => _items;

    internal bool IsDrawing => _down;

    internal bool IsIntroPlaying => _intro is not null;

    internal double IntroTime => _introTime;

    internal double Clock => _clock;

    internal bool IsKeyboardMode => _keyboard;

    internal Point CursorPosition => _cursor;

    internal int LaserPointCount => _laser.Count;

    internal double RingOpacity => _ringOpacity;

    internal double NibOpacity => _nibOpacity;

    internal IReadOnlyList<Point> LivePoints => _live;

    internal IReadOnlyList<double> LiveWidths => _liveWidths;

    private bool Animates => Owner is { IsPaused: false } && ArcMotion.IsFull(this);

    private double Speed => Math.Max(0.05, Owner?.Speed ?? 1);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 720 : availableSize.Width;
        return new Size(width, width / Math.Max(0.1, Aspect));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new BoardAutomationPeer(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            var oldSize = change.GetOldValue<Rect>().Size;
            var newSize = change.GetNewValue<Rect>().Size;
            if (oldSize.Width > 0 && newSize.Width > 0 && Math.Abs(oldSize.Width - newSize.Width) > 0.01)
            {
                // Items keep their board units; strokes drawn later use the new px-to-unit scale.
                foreach (var item in _items)
                {
                    item.Invalidate();
                }
            }
        }
    }

    // --- items ------------------------------------------------------------------------------------------------------

    internal void SetItems(IEnumerable<CursorTrailDrawItem> items)
    {
        CancelStroke();
        _items.Clear();
        foreach (var item in items)
        {
            _items.Add(BoardItem.FromPublic(item));
            _nextId = Math.Max(_nextId, ParseId(item.Id) + 1);
        }
        InvalidateVisual();
    }

    internal void RemoveLast(bool animate)
    {
        var item = _items.LastOrDefault(i => !i.Removing);
        if (item is null)
        {
            return;
        }
        if (animate && Animates)
        {
            item.Removing = true;
            item.Fade = 1;
            _loop.Start();
        }
        else
        {
            _items.Remove(item);
        }
        InvalidateVisual();
    }

    internal void RemoveAll(bool animate)
    {
        CancelStroke();
        if (animate && Animates)
        {
            foreach (var item in _items)
            {
                item.Removing = true;
                item.Fade = 1;
            }
            _loop.Start();
        }
        else
        {
            _items.Clear();
        }
        InvalidateVisual();
    }

    /// <summary>Runs the snap spring back so the last snapped item looks as drawn again.</summary>
    internal BoardItem? KeepAsDrawn(BoardItem item)
    {
        if (item.Clean is null || item.Removing)
        {
            return null;
        }
        if (Animates && item.Samples.Count > 1)
        {
            var from = item.Morph is { } running ? running.Current() : item.Clean.Value.Sample(MorphSamples, item.Samples[0]);
            item.Morph = Morph.Create(from, CursorTrailDrawInkGeometry.Resample(item.Samples, MorphSamples),
                Fill(MorphSamples, item.Width), CursorTrailDrawInkGeometry.ResampleWidths(item.Samples, item.Widths, MorphSamples),
                toClean: false);
            _loop.Start();
        }
        else
        {
            item.Morph = null;
        }
        item.Clean = null;
        item.Shape = CursorTrailDrawShape.Stroke;
        item.Invalidate();
        InvalidateVisual();
        return item;
    }

    // --- input --------------------------------------------------------------------------------------------------

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        UpdateHover(e);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!_down)
        {
            _hovering = false;
            UpdateCursorTargets();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed || Owner is null)
        {
            return;
        }
        e.Handled = true;
        Owner.StopIntro();
        Focus(NavigationMethod.Pointer);
        _keyboard = false;
        e.Pointer.Capture(this);
        _pointerType = e.Pointer.Type;
        _hovering = _pointerType != PointerType.Touch;
        var units = ToUnits(point.Position);
        _cursor = units;
        BeginStroke(units, point.Properties.Pressure, _pointerType == PointerType.Pen, Owner.Tool, fromIntro: false);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetCurrentPoint(this);
        if (_keyboard && !_down)
        {
            _keyboard = false;
        }
        _pointerType = e.Pointer.Type;
        if (_down && !_downFromIntro)
        {
            MoveStroke(ToUnits(point.Position), point.Properties.Pressure);
        }
        UpdateHover(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_down && !_downFromIntro)
        {
            e.Handled = true;
            MoveStroke(ToUnits(e.GetPosition(this)), _pointerPressure);
            EndStroke();
            e.Pointer.Capture(null);
        }
        if (e.Pointer.Type == PointerType.Touch)
        {
            _hovering = false;
            UpdateCursorTargets();
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_down && !_downFromIntro)
        {
            EndStroke();
        }
    }

    private void UpdateHover(PointerEventArgs e)
    {
        var position = e.GetPosition(this);
        var inside = new Rect(Bounds.Size).Contains(position);
        _hovering = e.Pointer.Type != PointerType.Touch && (inside || _down);
        if (!_keyboard && _intro is null)
        {
            _cursor = ToUnits(position);
        }
        UpdateCursorTargets();
        InvalidateVisual();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional)
        {
            EnterKeyboardMode();
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _held.Clear();
        if (_down && !_downFromIntro && _keyboardPen)
        {
            EndStroke();
        }
        _keyboard = false;
        UpdateCursorTargets();
    }

    private bool _keyboardPen;

    private void EnterKeyboardMode()
    {
        _keyboard = true;
        if (!_keyboardPlaced)
        {
            _keyboardPlaced = true;
            _cursor = new Point(500, Math.Min(312.5, UnitsHigh / 2));
        }
        UpdateCursorTargets();
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (e.Handled || Owner is null || e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Left or Key.Right or Key.Up or Key.Down:
                e.Handled = true;
                Owner.StopIntro();
                if (!_keyboard)
                {
                    EnterKeyboardMode();
                }
                _held.Add(e.Key);
                _loop.Start();
                break;
            case Key.Space or Key.Enter:
                e.Handled = true;
                Owner.StopIntro();
                if (!_keyboard)
                {
                    EnterKeyboardMode();
                }
                if (_down && _keyboardPen)
                {
                    EndStroke();
                    _keyboardPen = false;
                    Owner.Announce(Owner.Texts.PenUp);
                }
                else if (!_down)
                {
                    _keyboardPen = true;
                    _pointerType = PointerType.Mouse;
                    BeginStroke(_cursor, 0.5, false, Owner.Tool, fromIntro: false);
                    Owner.Announce(Owner.Texts.PenDown);
                }
                break;
            case Key.Escape:
                if (_down && !_downFromIntro)
                {
                    e.Handled = true;
                    CancelStroke();
                    Owner.Announce(Owner.Texts.Cancelled);
                }
                break;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        _shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key is not (Key.LeftShift or Key.RightShift);
        if (_held.Remove(e.Key))
        {
            e.Handled = true;
        }
    }

    // --- strokes ------------------------------------------------------------------------------------------------

    private void BeginStroke(Point units, double pressure, bool usePressure, CursorTrailDrawTool tool, bool fromIntro, string? ink = null)
    {
        if (Owner is null)
        {
            return;
        }
        _down = true;
        _downFromIntro = fromIntro;
        _strokeTool = tool;
        _strokeInk = ink ?? Owner.CurrentInkId;
        _usePointerPressure = usePressure;
        _pointerPressure = usePressure ? pressure : 0.5;
        _target = _previousTarget = _tip = units;
        _pressure = usePressure ? pressure : 0.85;
        _live.Clear();
        _liveWidths.Clear();
        if (tool == CursorTrailDrawTool.Pen)
        {
            _live.Add(units);
            _liveWidths.Add(WidthUnits(_pressure));
            _liveDirty = true;
        }
        else
        {
            AddLaserPoint(units);
        }
        Owner.OnStrokeStarted();
        UpdateCursorTargets();
        _loop.Start();
        InvalidateVisual();
    }

    private void MoveStroke(Point units, double pressure)
    {
        _target = units;
        _cursor = units;
        if (_usePointerPressure)
        {
            _pointerPressure = pressure;
        }
        _loop.Start();
    }

    private void EndStroke()
    {
        if (!_down || Owner is null)
        {
            return;
        }
        _down = false;
        var fromIntro = _downFromIntro;
        _downFromIntro = false;
        UpdateCursorTargets();
        if (_strokeTool == CursorTrailDrawTool.Laser)
        {
            AddLaserPoint(_target);
            _loop.Start();
            return;
        }
        // The stroke completes to the release point at once.
        Advance(_target);
        if (CursorTrailDrawInkGeometry.Distance(_live[^1], _target) > 1e-6)
        {
            _live.Add(_target);
            _liveWidths.Add(WidthUnits(_pressure));
        }
        Land(fromIntro);
        _live.Clear();
        _liveWidths.Clear();
        _liveGeometry = null;
        InvalidateVisual();
    }

    internal void CancelStroke()
    {
        if (!_down)
        {
            return;
        }
        _down = false;
        _downFromIntro = false;
        _keyboardPen = false;
        _live.Clear();
        _liveWidths.Clear();
        _liveGeometry = null;
        UpdateCursorTargets();
        InvalidateVisual();
    }

    private void Land(bool fromIntro)
    {
        if (Owner is null || _live.Count == 0)
        {
            return;
        }
        var scale = Scale;
        var item = new BoardItem
        {
            Id = "s" + _nextId++.ToString(System.Globalization.CultureInfo.InvariantCulture),
            InkId = _strokeInk,
            Shape = CursorTrailDrawShape.Stroke,
            Samples = new List<Point>(_live),
            Widths = new List<double>(_liveWidths),
            Width = CleanWidth / scale,
        };
        var clean = Owner.IsSnapEnabled ? CursorTrailDrawRecognizer.Recognize(item.Samples, MinimumSnapPx / scale) : null;
        if (clean is { } shape)
        {
            item.Clean = shape;
            item.Shape = shape.Shape;
            if (Animates && item.Samples.Count > 1)
            {
                item.Morph = Morph.Create(CursorTrailDrawInkGeometry.Resample(item.Samples, MorphSamples), shape.Sample(MorphSamples, item.Samples[0]),
                    CursorTrailDrawInkGeometry.ResampleWidths(item.Samples, item.Widths, MorphSamples), Fill(MorphSamples, item.Width),
                    toClean: true);
                _loop.Start();
            }
        }
        _items.Add(item);
        Owner.OnLanded(item, fromIntro);
    }

    private static double[] Fill(int n, double value)
    {
        var values = new double[n];
        Array.Fill(values, value);
        return values;
    }

    /// <summary>Pen width in units for a pressure (Arc: size·(0.49 + 0.74·p) px).</summary>
    private double WidthUnits(double pressure)
    {
        var size = Owner?.PenSize ?? 4;
        return Math.Max(0.4, size * (0.49 + 0.74 * Math.Clamp(pressure, 0, 1))) / Scale;
    }

    /// <summary>Effective pressure of a mouse or finger at a tip speed in px/s: full when slow, 0.35 above 2300 px/s.</summary>
    internal static double PressureForSpeed(double pxPerSecond)
    {
        var t = Math.Clamp((pxPerSecond - 200) / 2100, 0, 1);
        return 1 - 0.65 * t * t * (3 - 2 * t);
    }

    /// <summary>Time constant of the pen tip for a smoothing from 0 (raw) to 1 (very smooth); 21ms at 0.5.</summary>
    internal static double TipTime(double smoothing) => 0.06 * Math.Pow(Math.Clamp(smoothing, 0, 1), 1.5);

    /// <summary>Moves the tip straight to <paramref name="units"/> (no smoothing), sampling on the way.</summary>
    private void Advance(Point units)
    {
        _previousTarget = units;
        _tip = units;
    }

    private void StepPen(double dt)
    {
        if (dt <= 0)
        {
            return;
        }
        var tau = TipTime(Owner?.Smoothing ?? 0.5);
        var scale = Scale;
        var steps = Math.Max(1, (int)Math.Ceiling(dt / SubStep));
        var h = dt / steps;
        var from = _previousTarget;
        for (var k = 1; k <= steps; k++)
        {
            var target = from + (_target - from) * ((double)k / steps);
            var before = _tip;
            _tip = tau <= 1e-4 ? target : _tip + (target - _tip) * (1 - Math.Exp(-h / tau));
            var moved = CursorTrailDrawInkGeometry.Distance(before, _tip) * scale;
            var pressureTarget = _usePointerPressure ? _pointerPressure : PressureForSpeed(moved / h);
            _pressure += (pressureTarget - _pressure) * (1 - Math.Exp(-h / PressureTime));
            if (_strokeTool == CursorTrailDrawTool.Pen && _live.Count > 0
                && CursorTrailDrawInkGeometry.Distance(_live[^1], _tip) * scale >= SampleSpacingPx)
            {
                _live.Add(_tip);
                _liveWidths.Add(WidthUnits(_pressure));
                _liveDirty = true;
            }
        }
        _previousTarget = _target;
        if (_strokeTool == CursorTrailDrawTool.Laser)
        {
            AddLaserPoint(_target);
        }
    }

    private void AddLaserPoint(Point units)
    {
        if (!Animates)
        {
            return;
        }
        if (_laser.Count > 0 && CursorTrailDrawInkGeometry.Distance(_laser[^1].At, units) * Scale < 0.5)
        {
            return;
        }
        _laser.Add(new LaserPoint(units, _clock));
    }

    // --- intro ------------------------------------------------------------------------------------------------------

    internal void StartIntro()
    {
        if (Owner is null)
        {
            return;
        }
        _intro = CursorTrailDrawIntro.For(UnitsHigh);
        _introTime = 0;
        _introPart = -1;
        _introTool = CursorTrailDrawTool.Pen;
        _cursor = _intro.Parts[0].Path[0];
        _loop.Start();
        UpdateCursorTargets();
    }

    /// <summary>Stops the intro at once: its stroke in progress is dropped, finished items stay.</summary>
    internal void StopIntro()
    {
        if (_intro is null)
        {
            return;
        }
        _intro = null;
        _introPart = -1;
        if (_down && _downFromIntro)
        {
            if (_strokeTool == CursorTrailDrawTool.Laser)
            {
                _down = false;
                _downFromIntro = false;
            }
            else
            {
                CancelStroke();
            }
        }
        UpdateCursorTargets();
        InvalidateVisual();
    }

    /// <summary>Adds the intro's finished sketch at once (reduced motion, paused).</summary>
    internal void PlaceIntroSketch()
    {
        if (Owner is null)
        {
            return;
        }
        var intro = CursorTrailDrawIntro.For(UnitsHigh);
        var scale = Scale;
        foreach (var part in intro.Strokes)
        {
            var clean = CursorTrailDrawRecognizer.Recognize(part.Path);
            var item = new BoardItem
            {
                Id = "s" + _nextId++.ToString(System.Globalization.CultureInfo.InvariantCulture),
                InkId = Owner.IntroInkId(part.Ink),
                Samples = part.Path.ToList(),
                Widths = Enumerable.Repeat(WidthUnits(0.75), part.Path.Length).ToList(),
                Width = CleanWidth / scale,
                Clean = clean,
                Shape = clean?.Shape ?? CursorTrailDrawShape.Stroke,
            };
            _items.Add(item);
        }
        Owner.OnIntroPlaced();
        InvalidateVisual();
    }

    private void StepIntro(double dt)
    {
        if (_intro is null || Owner is null)
        {
            return;
        }
        _introTime += dt * Speed;
        var (position, part) = _intro.Sample(_introTime);
        _cursor = position;
        if (part != _introPart)
        {
            // Leaving a drawing part lifts the pen; entering one puts it down.
            if (_introPart >= 0 && _intro.Parts[_introPart].Down && _down && _downFromIntro)
            {
                MoveStroke(_intro.Parts[_introPart].Path[^1], 0.5);
                EndStroke();
            }
            _introPart = part;
            if (part >= 0)
            {
                var p = _intro.Parts[part];
                _introTool = p.Tool;
                if (p.Down)
                {
                    BeginStroke(position, 0.5, false, p.Tool, fromIntro: true, Owner.IntroInkId(p.Ink));
                }
            }
            UpdateCursorTargets();
        }
        else if (_down && _downFromIntro)
        {
            MoveStroke(position, 0.5);
        }
        if (_introTime > _intro.End + 0.05)
        {
            _intro = null;
            _introPart = -1;
            UpdateCursorTargets();
            Owner.OnIntroFinished();
        }
    }

    // --- frame loop --------------------------------------------------------------------------------------------

    /// <summary>Advances everything by <paramref name="dt"/> seconds; returns whether anything still moves.</summary>
    internal bool Step(double dt)
    {
        dt = Math.Clamp(dt, 0, 0.1);
        _clock += dt;
        StepIntro(dt);
        StepKeyboard(dt);
        if (_down)
        {
            StepPen(dt);
        }
        var moving = _down || _intro is not null || _held.Count > 0;
        // Laser: points older than the fade drop off the tail.
        var fade = (Owner?.LaserFade.TotalSeconds ?? 0.7) / Speed;
        var drop = 0;
        while (drop < _laser.Count && _clock - _laser[drop].Time > fade)
        {
            drop++;
        }
        if (drop > 0)
        {
            _laser.RemoveRange(0, drop);
        }
        if (_laser.Count > 0)
        {
            moving = true;
        }
        // Snap and keep springs.
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (item.Morph is { } morph)
            {
                morph.Time += dt * Speed;
                if (morph.Time >= SnapSwap)
                {
                    item.Morph = null;
                    item.Invalidate();
                }
                else
                {
                    moving = true;
                }
            }
            if (item.Removing)
            {
                item.Fade -= dt / FadeSeconds;
                if (item.Fade <= 0)
                {
                    _items.RemoveAt(i);
                    i--;
                    continue;
                }
                moving = true;
            }
        }
        if (StepCursorFade(dt))
        {
            moving = true;
        }
        InvalidateVisual();
        return moving;
    }

    private void StepKeyboard(double dt)
    {
        if (_held.Count == 0)
        {
            return;
        }
        var x = (_held.Contains(Key.Right) ? 1 : 0) - (_held.Contains(Key.Left) ? 1 : 0);
        var y = (_held.Contains(Key.Down) ? 1 : 0) - (_held.Contains(Key.Up) ? 1 : 0);
        if (x == 0 && y == 0)
        {
            return;
        }
        var speed = (_shift ? KeySlowPx : KeySpeedPx) / Scale;
        var length = Math.Sqrt(x * x + y * y);
        var next = new Point(
            Math.Clamp(_cursor.X + x / length * speed * dt, 0, UnitsWide),
            Math.Clamp(_cursor.Y + y / length * speed * dt, 0, UnitsHigh));
        _cursor = next;
        if (_down && !_downFromIntro)
        {
            MoveStroke(next, 0.5);
        }
    }

    private bool _shift;

    private double _ringTarget;
    private double _nibTarget;

    private void UpdateCursorTargets()
    {
        var tool = _intro is not null ? _introTool : (_down ? _strokeTool : Owner?.Tool ?? CursorTrailDrawTool.Pen);
        var virtualCursor = _intro is not null || _keyboard;
        var shown = virtualCursor || (_hovering && _pointerType != PointerType.Touch);
        if (!shown)
        {
            _ringTarget = 0;
            _nibTarget = 0;
        }
        else if (tool == CursorTrailDrawTool.Laser)
        {
            _ringTarget = _down ? 0.5 : 0.32;
            _nibTarget = 0.92;
        }
        else
        {
            _ringTarget = virtualCursor ? (_down && !_downFromIntro ? 0.5 : 0.32) : 0;
            _nibTarget = 0.55;
        }
        if (!Animates)
        {
            _ringOpacity = _ringTarget;
            _nibOpacity = _nibTarget;
        }
        if (Math.Abs(_ringOpacity - _ringTarget) > 1e-3 || Math.Abs(_nibOpacity - _nibTarget) > 1e-3)
        {
            _loop.Start();
        }
        InvalidateVisual();
    }

    private bool StepCursorFade(double dt)
    {
        var rate = dt / FadeSeconds;
        _ringOpacity = Approach(_ringOpacity, _ringTarget, rate);
        _nibOpacity = Approach(_nibOpacity, _nibTarget, rate);
        return Math.Abs(_ringOpacity - _ringTarget) > 1e-3 || Math.Abs(_nibOpacity - _nibTarget) > 1e-3;
    }

    private static double Approach(double value, double target, double rate)
    {
        // An ease-out approach that finishes in about the fade time.
        var next = value + (target - value) * Math.Min(1, rate * 3);
        return Math.Abs(next - target) < 0.004 ? target : next;
    }

    /// <summary>Re-reads the tool and motion level for the cursor; the owner calls it when its state changes.</summary>
    internal void Refresh()
    {
        UpdateCursorTargets();
        if (!Animates)
        {
            _laser.Clear();
            foreach (var item in _items)
            {
                if (item.Morph is not null)
                {
                    item.Morph = null;
                    item.Invalidate();
                }
            }
            _items.RemoveAll(i => i.Removing);
        }
        _loop.Refresh();
        InvalidateVisual();
    }

    // --- render -------------------------------------------------------------------------------------------------

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        DrawGrid(context, size);
        var scale = Scale;
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            foreach (var item in _items)
            {
                DrawItem(context, item);
            }
            DrawLive(context);
            DrawLaser(context);
            DrawCursor(context, scale);
        }
    }

    private void DrawGrid(DrawingContext context, Size size)
    {
        if (GridBrush is null)
        {
            return;
        }
        var spacing = Math.Max(4, GridSpacing);
        if (_gridGeometry is null || _gridSize != size || Math.Abs(_gridSpacing - spacing) > 0.001)
        {
            _gridSize = size;
            _gridSpacing = spacing;
            var geometry = new StreamGeometry();
            var r = Math.Max(0.2, GridDotRadius);
            using (var c = geometry.Open())
            {
                for (var y = spacing / 2; y < size.Height; y += spacing)
                {
                    for (var x = spacing / 2; x < size.Width; x += spacing)
                    {
                        c.BeginFigure(new Point(x + r, y), true);
                        c.ArcTo(new Point(x - r, y), new Size(r, r), 0, false, SweepDirection.Clockwise);
                        c.ArcTo(new Point(x + r, y), new Size(r, r), 0, false, SweepDirection.Clockwise);
                        c.EndFigure(true);
                    }
                }
            }
            _gridGeometry = geometry;
        }
        context.DrawGeometry(GridBrush, null, _gridGeometry);
    }

    private void DrawItem(DrawingContext context, BoardItem item)
    {
        var brush = Owner?.BrushFor(item.InkId) ?? InkBrush;
        if (brush is null)
        {
            return;
        }
        if (item.Removing)
        {
            using (context.PushOpacity(Math.Clamp(1 - ArcEasings.Exit.Ease(Math.Clamp(1 - item.Fade, 0, 1)), 0, 1)))
            {
                DrawItemBody(context, item, brush);
            }
            return;
        }
        DrawItemBody(context, item, brush);
    }

    private void DrawItemBody(DrawingContext context, BoardItem item, IBrush brush)
    {
        if (item.Morph is { } morph)
        {
            var points = morph.Current();
            var widths = morph.CurrentWidths();
            CursorTrailDrawInkGeometry.Build(points, widths, points.Length, StartTaperPx / Scale,
                morph.ToClean ? 0 : EndTaperPx / Scale, _outline);
            if (CursorTrailDrawInkGeometry.ToGeometry(_outline) is { } g)
            {
                context.DrawGeometry(brush, null, g);
            }
            return;
        }
        if (item.Clean is { } clean)
        {
            item.CleanGeometry ??= CursorTrailDrawInkGeometry.Centerline(clean.Centerline(), clean.IsClosed);
            context.DrawGeometry(null, item.PenFor(brush), item.CleanGeometry);
            return;
        }
        if (item.InkGeometry is null)
        {
            var scale = Scale;
            CursorTrailDrawInkGeometry.Build(item.Samples, item.Widths, item.Samples.Count, StartTaperPx / scale, EndTaperPx / scale, _outline);
            item.InkGeometry = CursorTrailDrawInkGeometry.ToGeometry(_outline);
        }
        if (item.InkGeometry is not null)
        {
            context.DrawGeometry(brush, null, item.InkGeometry);
        }
    }

    private void DrawLive(DrawingContext context)
    {
        if (!_down || _strokeTool != CursorTrailDrawTool.Pen || _live.Count == 0)
        {
            return;
        }
        if (_liveDirty || _liveGeometry is null)
        {
            _liveDirty = false;
            CursorTrailDrawInkGeometry.Build(_live, _liveWidths, _live.Count, StartTaperPx / Scale, 0, _outline);
            _liveGeometry = CursorTrailDrawInkGeometry.ToGeometry(_outline);
        }
        var brush = Owner?.BrushFor(_strokeInk) ?? InkBrush;
        if (_liveGeometry is not null && brush is not null)
        {
            context.DrawGeometry(brush, null, _liveGeometry);
        }
    }

    private void DrawLaser(DrawingContext context)
    {
        if (_laser.Count < 2)
        {
            return;
        }
        var brush = Owner?.LaserBrush ?? AccentInkBrush;
        if (brush is null)
        {
            return;
        }
        var fade = (Owner?.LaserFade.TotalSeconds ?? 0.7) / Speed;
        var head = LaserWidth / Scale;
        _laserPoints.Clear();
        _laserWidths.Clear();
        foreach (var point in _laser)
        {
            var age = Math.Clamp((_clock - point.Time) / fade, 0, 1);
            _laserPoints.Add(point.At);
            _laserWidths.Add(head * (1 - age));
        }
        CursorTrailDrawInkGeometry.Build(_laserPoints, _laserWidths, _laserPoints.Count, 0, 0, _outline);
        _laserGeometry = CursorTrailDrawInkGeometry.ToGeometry(_outline);
        if (_laserGeometry is not null)
        {
            using (context.PushOpacity(0.9))
            {
                context.DrawGeometry(brush, null, _laserGeometry);
            }
        }
    }

    private void DrawCursor(DrawingContext context, double scale)
    {
        if (_ringOpacity <= 0.003 && _nibOpacity <= 0.003)
        {
            return;
        }
        var tool = _intro is not null ? _introTool : (_down ? _strokeTool : Owner?.Tool ?? CursorTrailDrawTool.Pen);
        var laser = tool == CursorTrailDrawTool.Laser;
        var brush = laser
            ? Owner?.LaserBrush ?? AccentInkBrush
            : Owner?.BrushFor(_intro is not null && _down ? _strokeInk : Owner.CurrentInkId) ?? InkBrush;
        if (brush is null)
        {
            return;
        }
        var radii = CursorRadii;
        var ring = (laser ? radii.Right : radii.Left) / scale;
        var nib = (laser ? radii.Bottom : radii.Top) / scale;
        if (_ringOpacity > 0.003)
        {
            using (context.PushOpacity(_ringOpacity))
            {
                context.DrawEllipse(null, CursorPen(brush, CursorThickness / scale), _cursor, ring, ring);
            }
        }
        if (_nibOpacity > 0.003)
        {
            using (context.PushOpacity(_nibOpacity))
            {
                context.DrawEllipse(brush, null, _cursor, nib, nib);
            }
        }
    }

    private IPen? _cursorPen;
    private IBrush? _cursorPenBrush;
    private double _cursorPenWidth;

    private IPen CursorPen(IBrush brush, double width)
    {
        if (_cursorPen is null || !ReferenceEquals(_cursorPenBrush, brush) || Math.Abs(_cursorPenWidth - width) > 1e-6)
        {
            _cursorPenBrush = brush;
            _cursorPenWidth = width;
            _cursorPen = new ImmutablePen(brush.ToImmutable(), width);
        }
        return _cursorPen;
    }

    private Point ToUnits(Point px) => new(px.X / Scale, px.Y / Scale);

    private static int ParseId(string id) =>
        id.Length > 1 && id[0] == 's' && int.TryParse(id.AsSpan(1), System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : -1;

    private readonly record struct LaserPoint(Point At, double Time);

    /// <summary>A snapping (or un-snapping) shape: every resampled point travels on one spring.</summary>
    internal sealed class Morph
    {
        private static readonly SpringSpec s_spring = SpringSpec.FromPhysical(270, 19.1);
        private Point[] _from = [];
        private Point[] _to = [];
        private double[] _fromWidths = [];
        private double[] _toWidths = [];
        private Point[] _points = [];
        private double[] _widths = [];

        public double Time { get; set; }

        public bool ToClean { get; private set; }

        public static Morph Create(Point[] from, Point[] to, double[] fromWidths, double[] toWidths, bool toClean) => new()
        {
            _from = from,
            _to = to,
            _fromWidths = fromWidths,
            _toWidths = toWidths,
            _points = new Point[from.Length],
            _widths = new double[from.Length],
            ToClean = toClean,
        };

        /// <summary>Spring progress from 0 to 1 (overshooting about 10%).</summary>
        public double Progress => 1 + SpringSolver.Evaluate(s_spring, -1, 0, Time).Displacement;

        public Point[] Current()
        {
            var s = Progress;
            for (var i = 0; i < _points.Length; i++)
            {
                _points[i] = new Point(_from[i].X + (_to[i].X - _from[i].X) * s, _from[i].Y + (_to[i].Y - _from[i].Y) * s);
            }
            return _points;
        }

        public double[] CurrentWidths()
        {
            var s = Math.Clamp(Progress, 0, 1);
            for (var i = 0; i < _widths.Length; i++)
            {
                _widths[i] = _fromWidths[i] + (_toWidths[i] - _fromWidths[i]) * s;
            }
            return _widths;
        }
    }

    private sealed class BoardAutomationPeer(CursorTrailDrawBoard owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;

        protected override string GetLocalizedControlTypeCore() => "whiteboard";

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>One item on the board with what it needs to draw: samples and widths, the clean shape, caches.</summary>
internal sealed class BoardItem
{
    private IPen? _pen;
    private IBrush? _penBrush;

    public string Id { get; init; } = string.Empty;
    public CursorTrailDrawShape Shape { get; set; }
    public string InkId { get; init; } = string.Empty;
    public double Width { get; init; }
    public List<Point> Samples { get; init; } = new();
    public List<double> Widths { get; init; } = new();
    public CursorTrailDrawCleanShape? Clean { get; set; }
    public CursorTrailDrawBoard.Morph? Morph { get; set; }
    public bool Removing { get; set; }
    public double Fade { get; set; } = 1;
    public Geometry? InkGeometry { get; set; }
    public Geometry? CleanGeometry { get; set; }

    public void Invalidate()
    {
        InkGeometry = null;
        CleanGeometry = null;
        _pen = null;
    }

    public IPen PenFor(IBrush brush)
    {
        if (_pen is null || !ReferenceEquals(_penBrush, brush))
        {
            _penBrush = brush;
            _pen = new ImmutablePen(brush.ToImmutable(), Width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        }
        return _pen;
    }

    /// <summary>The public view: clean shapes give their centerline, strokes their samples.</summary>
    public CursorTrailDrawItem ToPublic()
    {
        IReadOnlyList<Point> points = Clean is { } clean ? clean.Centerline(48) : Samples.ToArray();
        var width = Clean is not null ? Width : (Widths.Count > 0 ? Widths.Average() : Width);
        return new CursorTrailDrawItem(Id, Shape, InkId, width, points);
    }

    public static BoardItem FromPublic(CursorTrailDrawItem item)
    {
        var points = item.Points.ToList();
        CursorTrailDrawCleanShape? clean = null;
        if (item.Shape != CursorTrailDrawShape.Stroke && points.Count > 0)
        {
            clean = CursorTrailDrawRecognizer.FromPoints(item.Shape, points);
        }
        return new BoardItem
        {
            Id = item.Id,
            Shape = item.Shape,
            InkId = item.InkId,
            Width = item.Width,
            Samples = points,
            Widths = Enumerable.Repeat(item.Width, points.Count).ToList(),
            Clean = clean,
        };
    }
}
