using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The loss chart of a <see cref="TrainingRun"/>, drawn in one control: every other epoch as a faint band, three hairline
/// gridlines with the fixed loss axis, the raw train loss (faint), the smoothed train loss and the validation loss with a dot
/// per sample, and under the plot the step axis drawn as the epoch progress bar (one segment per epoch) with the epoch
/// names. The x axis spans the whole run and the y axis is fixed from the first samples, so nothing rescales mid-run.
/// <para>
/// The curves are cached geometry, rebuilt only when samples arrive or the size changes; on each frame the drawn part is
/// a clip up to the eased head step, so a frame allocates nothing. The head and the progress fill ease toward the latest
/// sample and step (exponential, 120ms) on a Core <see cref="LiveLoop"/> that stops when they arrive (code-driven because
/// the target moves with every sample, which a ControlTheme transition cannot express); a rollback or reset, reduced
/// motion and Off jump. Arrow keys, Home, End and Escape inspect samples; a mouse inspects on hover, touch scrubs once a
/// drag turns horizontal so vertical swipes still scroll.
/// </para>
/// </summary>
internal sealed class TrainingRunChart : Control
{
    /// <summary>The time constant of the curve's easing toward each new sample (seconds).</summary>
    internal const double EaseSeconds = 0.12;

    public static readonly StyledProperty<IBrush?> TrainBrushProperty =
        AvaloniaProperty.Register<TrainingRunChart, IBrush?>(nameof(TrainBrush));

    public static readonly StyledProperty<IBrush?> ValidationBrushProperty =
        AvaloniaProperty.Register<TrainingRunChart, IBrush?>(nameof(ValidationBrush));

    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<TrainingRunChart, IBrush?>(nameof(GridBrush));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<TrainingRunChart, IBrush?>(nameof(LabelBrush));

    public static readonly StyledProperty<IBrush?> BandBrushProperty =
        AvaloniaProperty.Register<TrainingRunChart, IBrush?>(nameof(BandBrush));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<TrainingRunChart, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> ProgressBrushProperty =
        AvaloniaProperty.Register<TrainingRunChart, IBrush?>(nameof(ProgressBrush));

    public static readonly StyledProperty<IBrush?> SurfaceBrushProperty =
        AvaloniaProperty.Register<TrainingRunChart, IBrush?>(nameof(SurfaceBrush));

    public static readonly StyledProperty<IBrush?> FocusBrushProperty =
        AvaloniaProperty.Register<TrainingRunChart, IBrush?>(nameof(FocusBrush));

    public static readonly StyledProperty<double> GutterProperty =
        AvaloniaProperty.Register<TrainingRunChart, double>(nameof(Gutter), 30);

    public static readonly StyledProperty<double> CurveStrokeWidthProperty =
        AvaloniaProperty.Register<TrainingRunChart, double>(nameof(CurveStrokeWidth), 2);

    public static readonly StyledProperty<double> RawStrokeWidthProperty =
        AvaloniaProperty.Register<TrainingRunChart, double>(nameof(RawStrokeWidth), 1);

    public static readonly StyledProperty<double> RawOpacityProperty =
        AvaloniaProperty.Register<TrainingRunChart, double>(nameof(RawOpacity), 0.3);

    public static readonly StyledProperty<double> HeadDotRadiusProperty =
        AvaloniaProperty.Register<TrainingRunChart, double>(nameof(HeadDotRadius), 3.5);

    public static readonly StyledProperty<double> SampleDotRadiusProperty =
        AvaloniaProperty.Register<TrainingRunChart, double>(nameof(SampleDotRadius), 2.5);

    public static readonly StyledProperty<double> BandOpacityProperty =
        AvaloniaProperty.Register<TrainingRunChart, double>(nameof(BandOpacity), 0.55);

    public static readonly StyledProperty<double> ProgressHeightProperty =
        AvaloniaProperty.Register<TrainingRunChart, double>(nameof(ProgressHeight), 4);

    public static readonly StyledProperty<double> EpochGapProperty =
        AvaloniaProperty.Register<TrainingRunChart, double>(nameof(EpochGap), 3);

    public static readonly StyledProperty<double> ProgressMarginProperty =
        AvaloniaProperty.Register<TrainingRunChart, double>(nameof(ProgressMargin), 8);

    public static readonly StyledProperty<CornerRadius> FocusCornerRadiusProperty =
        AvaloniaProperty.Register<TrainingRunChart, CornerRadius>(nameof(FocusCornerRadius), new CornerRadius(8));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<TrainingRunChart>();

    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<TrainingRunChart>();

    private static readonly double[] s_gridFractions = [1, 0.5, 0];

    private readonly List<TrainingRunLossPoint> _points = [];
    private readonly List<double> _trainX = [];
    private readonly List<double> _trainRaw = [];
    private readonly List<double> _trainSmooth = [];
    private readonly List<double> _valX = [];
    private readonly List<double> _valY = [];
    private readonly LiveLoop _loop;
    private readonly TextLayout?[] _axisLabels = new TextLayout?[3];
    private readonly List<TextLayout> _epochLabels = [];
    private StreamGeometry? _rawGeometry;
    private StreamGeometry? _smoothGeometry;
    private StreamGeometry? _valGeometry;
    private Rect _geometryPlot;
    private bool _geometryDirty = true;
    private bool _labelsDirty = true;
    private IPen? _smoothPen;
    private IPen? _rawPen;
    private IPen? _valPen;
    private IPen? _gridPen;
    private IPen? _inspectPen;
    private IPen? _focusPen;
    private IPen? _dotRingPen;
    private double? _yTop;
    private int _totalSteps = 1;
    private int _epochs = 1;
    private double _head;
    private double _headTarget;
    private double _progress;
    private double _progressTarget;
    private bool _live;
    private int? _inspected;
    private bool _pointerInspects;
    private Point? _touchStart;
    private bool _scrubbing;
    private Func<int, string> _epochLabel = n => "Epoch " + n;

    public TrainingRunChart()
    {
        Focusable = true;
        ClipToBounds = false;
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Group);
        _loop = new LiveLoop(this, Advance);
        _loop.RunningChanged += running =>
        {
            // A loop that is wanted but held (reduced motion, off screen) lands at once instead of resting half way.
            if (!running && _loop.IsActive)
            {
                JumpToTargets();
                _loop.Stop();
            }
        };
    }

    public IBrush? TrainBrush { get => GetValue(TrainBrushProperty); set => SetValue(TrainBrushProperty, value); }

    public IBrush? ValidationBrush { get => GetValue(ValidationBrushProperty); set => SetValue(ValidationBrushProperty, value); }

    public IBrush? GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }

    public IBrush? LabelBrush { get => GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }

    public IBrush? BandBrush { get => GetValue(BandBrushProperty); set => SetValue(BandBrushProperty, value); }

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public IBrush? ProgressBrush { get => GetValue(ProgressBrushProperty); set => SetValue(ProgressBrushProperty, value); }

    public IBrush? SurfaceBrush { get => GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }

    public IBrush? FocusBrush { get => GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }

    public double Gutter { get => GetValue(GutterProperty); set => SetValue(GutterProperty, value); }

    public double CurveStrokeWidth { get => GetValue(CurveStrokeWidthProperty); set => SetValue(CurveStrokeWidthProperty, value); }

    public double RawStrokeWidth { get => GetValue(RawStrokeWidthProperty); set => SetValue(RawStrokeWidthProperty, value); }

    public double RawOpacity { get => GetValue(RawOpacityProperty); set => SetValue(RawOpacityProperty, value); }

    public double HeadDotRadius { get => GetValue(HeadDotRadiusProperty); set => SetValue(HeadDotRadiusProperty, value); }

    public double SampleDotRadius { get => GetValue(SampleDotRadiusProperty); set => SetValue(SampleDotRadiusProperty, value); }

    public double BandOpacity { get => GetValue(BandOpacityProperty); set => SetValue(BandOpacityProperty, value); }

    public double ProgressHeight { get => GetValue(ProgressHeightProperty); set => SetValue(ProgressHeightProperty, value); }

    public double EpochGap { get => GetValue(EpochGapProperty); set => SetValue(EpochGapProperty, value); }

    public double ProgressMargin { get => GetValue(ProgressMarginProperty); set => SetValue(ProgressMarginProperty, value); }

    public CornerRadius FocusCornerRadius { get => GetValue(FocusCornerRadiusProperty); set => SetValue(FocusCornerRadiusProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>Raised when the inspected sample changes (keys, hover, scrub, or new data clamping it).</summary>
    public event Action? InspectedChanged;

    /// <summary>The samples on the chart, oldest first.</summary>
    public IReadOnlyList<TrainingRunLossPoint> Points => _points;

    /// <summary>The index of the inspected sample, or null.</summary>
    public int? InspectedIndex => _inspected;

    /// <summary>The step the curves are drawn up to now (eases toward <see cref="HeadTarget"/>).</summary>
    public double Head => _head;

    /// <summary>The step of the latest sample.</summary>
    public double HeadTarget => _headTarget;

    /// <summary>The step the progress bar is filled to now.</summary>
    public double Progress => _progress;

    /// <summary>The top of the fixed loss axis, or null before the first sample.</summary>
    public double? AxisTop => _yTop;

    /// <summary>True while the easing loop runs.</summary>
    public bool IsEasing => _loop.IsRunning;

    /// <summary>The epoch names under the axis.</summary>
    public Func<int, string> EpochLabel
    {
        get => _epochLabel;
        set
        {
            _epochLabel = value;
            _labelsDirty = true;
            InvalidateVisual();
        }
    }

    /// <summary>The smoothed train loss at sample <paramref name="index"/> (of <see cref="Points"/>), or null.</summary>
    public double? SmoothedAt(int index)
    {
        if (index < 0 || index >= _points.Count || _points[index].Train is null)
        {
            return null;
        }
        var position = BinarySearch(_trainX, _points[index].Step);
        return position >= 0 && position < _trainSmooth.Count ? _trainSmooth[position] : null;
    }

    /// <summary>The validation loss at or before sample <paramref name="index"/>, or null.</summary>
    public double? ValidationAt(int index)
    {
        for (var i = Math.Min(index, _points.Count - 1); i >= 0; i--)
        {
            if (_points[i].Validation is { } value)
            {
                return value;
            }
        }
        return null;
    }

    /// <summary>The train loss at or before sample <paramref name="index"/> (raw), or null.</summary>
    public double? TrainAt(int index)
    {
        for (var i = Math.Min(index, _points.Count - 1); i >= 0; i--)
        {
            if (_points[i].Train is { } value)
            {
                return value;
            }
        }
        return null;
    }

    /// <summary>The run's shape: the steps of the whole run, the epochs, the steps done, and whether it is live.</summary>
    public void SetRun(int totalSteps, int epochs, int step, bool live)
    {
        totalSteps = Math.Max(1, totalSteps);
        epochs = Math.Max(1, epochs);
        if (totalSteps != _totalSteps || epochs != _epochs)
        {
            _totalSteps = totalSteps;
            _epochs = epochs;
            _geometryDirty = true;
            _labelsDirty = true;
        }
        _live = live;
        var target = Math.Clamp(step, 0, totalSteps);
        if (target < _progressTarget)
        {
            _progress = target;
        }
        _progressTarget = target;
        Ease();
    }

    /// <summary>Replaces the samples (call it after every change). A rollback or reset jumps instead of easing.</summary>
    public void SetSamples(IReadOnlyList<TrainingRunLossPoint> points)
    {
        _points.Clear();
        _trainX.Clear();
        _trainRaw.Clear();
        _valX.Clear();
        _valY.Clear();
        var latest = 0.0;
        foreach (var point in points)
        {
            _points.Add(point);
            latest = Math.Max(latest, point.Step);
            if (point.Train is { } train && double.IsFinite(train))
            {
                _trainX.Add(point.Step);
                _trainRaw.Add(train);
            }
            if (point.Validation is { } validation && double.IsFinite(validation))
            {
                _valX.Add(point.Step);
                _valY.Add(validation);
            }
        }
        TrainingRunFormat.Smooth(_trainRaw, _trainSmooth);
        if (_points.Count == 0)
        {
            _yTop = null;
            _labelsDirty = true;
        }
        else if (_yTop is null)
        {
            // The axis is fixed from the first samples (up to five) and kept for the rest of the run.
            var largest = 0.0;
            for (var i = 0; i < Math.Min(5, _trainRaw.Count); i++)
            {
                largest = Math.Max(largest, _trainRaw[i]);
            }
            for (var i = 0; i < Math.Min(5, _valY.Count); i++)
            {
                largest = Math.Max(largest, _valY[i]);
            }
            if (largest > 0)
            {
                _yTop = TrainingRunFormat.AxisTop(largest);
                _labelsDirty = true;
            }
        }
        if (latest < _head)
        {
            _head = latest;
        }
        _headTarget = latest;
        if (_inspected is { } inspected && inspected >= _points.Count)
        {
            SetInspected(_points.Count > 0 ? _points.Count - 1 : null, _pointerInspects);
        }
        _geometryDirty = true;
        Ease();
        InvalidateVisual();
    }

    /// <summary>Starts easing toward the targets, or jumps when motion is not full or the chart is not on screen.</summary>
    private void Ease()
    {
        if (_head == _headTarget && _progress == _progressTarget)
        {
            _loop.Stop();
            InvalidateVisual();
            return;
        }
        if (!this.IsAttachedToVisualTree() || !ArcMotion.IsFull(this))
        {
            JumpToTargets();
            _loop.Stop();
            return;
        }
        _loop.Start();
        if (!_loop.IsRunning && !_loop.CanRun)
        {
            JumpToTargets();
            _loop.Stop();
        }
    }

    private void JumpToTargets()
    {
        _head = _headTarget;
        _progress = _progressTarget;
        InvalidateVisual();
    }

    private bool Advance(double dt)
    {
        var k = 1 - Math.Exp(-Math.Max(0, dt) / EaseSeconds);
        _head = Approach(_head, _headTarget, k, 0.25);
        _progress = Approach(_progress, _progressTarget, k, 0.25);
        InvalidateVisual();
        return _head != _headTarget || _progress != _progressTarget;
    }

    private static double Approach(double value, double target, double k, double snap)
    {
        var next = value + (target - value) * k;
        return Math.Abs(target - next) < snap ? target : next;
    }

    /// <summary>Inspects sample <paramref name="index"/> (null clears).</summary>
    public void SetInspected(int? index, bool fromPointer = false)
    {
        if (index is { } i)
        {
            index = _points.Count == 0 ? null : Math.Clamp(i, 0, _points.Count - 1);
        }
        _pointerInspects = fromPointer && index is not null;
        if (_inspected == index)
        {
            return;
        }
        _inspected = index;
        InvalidateVisual();
        InspectedChanged?.Invoke();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == TrainBrushProperty || p == ValidationBrushProperty || p == GridBrushProperty || p == FocusBrushProperty
            || p == SurfaceBrushProperty || p == CurveStrokeWidthProperty || p == RawStrokeWidthProperty)
        {
            _smoothPen = _rawPen = _valPen = _gridPen = _inspectPen = _focusPen = _dotRingPen = null;
            InvalidateVisual();
        }
        else if (p == LabelBrushProperty || p == FontFamilyProperty || p == FontSizeProperty)
        {
            _labelsDirty = true;
            InvalidateVisual();
        }
        else if (p == GutterProperty || p == ProgressHeightProperty || p == ProgressMarginProperty || p == EpochGapProperty)
        {
            _geometryDirty = true;
            _labelsDirty = true;
            InvalidateVisual();
        }
        else if (p == BoundsProperty)
        {
            _geometryDirty = true;
            _labelsDirty = true;
        }
        else if (p == RawOpacityProperty || p == BandOpacityProperty || p == HeadDotRadiusProperty
                 || p == SampleDotRadiusProperty || p == BandBrushProperty || p == TrackBrushProperty || p == ProgressBrushProperty
                 || p == FocusCornerRadiusProperty)
        {
            InvalidateVisual();
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (!_pointerInspects)
        {
            SetInspected(null);
        }
        InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        JumpToTargets();
        DisposeLabels();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _points.Count == 0)
        {
            return;
        }
        var last = _points.Count - 1;
        var jump = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1;
        int target;
        switch (e.Key)
        {
            case Key.Left:
                target = _inspected is { } before ? before - jump : last;
                break;
            case Key.Right:
                target = _inspected is { } after ? after + jump : last;
                break;
            case Key.Home:
                target = 0;
                break;
            case Key.End:
                target = last;
                break;
            case Key.Escape:
                if (_inspected is not null)
                {
                    SetInspected(null);
                    e.Handled = true;
                }
                return;
            default:
                return;
        }
        SetInspected(Math.Clamp(target, 0, last));
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            _touchStart = e.GetPosition(this);
            _scrubbing = false;
            return;
        }
        Focus(NavigationMethod.Pointer);
        InspectAt(e.GetPosition(this).X);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = e.GetPosition(this);
        if (e.Pointer.Type != PointerType.Touch)
        {
            InspectAt(position.X);
            return;
        }
        if (_scrubbing)
        {
            InspectAt(position.X);
            e.Handled = true;
            return;
        }
        if (_touchStart is { } start)
        {
            var dx = Math.Abs(position.X - start.X);
            var dy = Math.Abs(position.Y - start.Y);
            if (dx > 6 && dx > dy)
            {
                // A horizontal drag scrubs; a vertical one is left alone so the page scrolls.
                _scrubbing = true;
                e.Pointer.Capture(this);
                InspectAt(position.X);
                e.Handled = true;
            }
            else if (dy > 6)
            {
                _touchStart = null;
            }
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            EndTouch();
            if (ReferenceEquals(e.Pointer.Captured, this))
            {
                e.Pointer.Capture(null);
            }
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndTouch();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_pointerInspects && e.Pointer.Type != PointerType.Touch)
        {
            SetInspected(null);
        }
    }

    private void EndTouch()
    {
        if (_scrubbing && _pointerInspects)
        {
            SetInspected(null);
        }
        _touchStart = null;
        _scrubbing = false;
    }

    /// <summary>Inspects the sample nearest to <paramref name="x"/> that is drawn already.</summary>
    private void InspectAt(double x)
    {
        if (_points.Count == 0)
        {
            return;
        }
        var plot = PlotRect(Bounds.Size);
        if (plot.Width <= 0)
        {
            return;
        }
        var step = (x - plot.X) / plot.Width * _totalSteps;
        step = Math.Min(step, _head);
        var best = 0;
        var distance = double.MaxValue;
        // Samples are oldest first, so a binary search finds the neighbours.
        var lo = 0;
        var hi = _points.Count - 1;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (_points[mid].Step < step)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }
        for (var i = Math.Max(0, lo - 1); i <= Math.Min(_points.Count - 1, lo); i++)
        {
            var d = Math.Abs(_points[i].Step - step);
            if (d < distance)
            {
                distance = d;
                best = i;
            }
        }
        SetInspected(best, fromPointer: true);
    }

    /// <summary>The plot inside the chart: the y gutter on the left, the progress bar and the epoch labels below.</summary>
    internal Rect PlotRect(Size size)
    {
        var labelHeight = Math.Ceiling(FontSize * 1.3);
        var bottom = ProgressMargin + ProgressHeight + 6 + labelHeight;
        var top = Math.Ceiling(FontSize * 0.6);
        var gutter = Gutter;
        return new Rect(gutter, top, Math.Max(0, size.Width - gutter), Math.Max(0, size.Height - bottom - top));
    }

    private double X(Rect plot, double step) => plot.X + step / _totalSteps * plot.Width;

    private double Y(Rect plot, double value)
    {
        var top = _yTop ?? 1;
        var t = Math.Clamp(value / top, 0, 1);
        return plot.Bottom - t * plot.Height;
    }

    private void EnsureGeometry(Rect plot)
    {
        if (!_geometryDirty && plot == _geometryPlot)
        {
            return;
        }
        _geometryDirty = false;
        _geometryPlot = plot;
        _rawGeometry = Polyline(plot, _trainX, _trainRaw);
        _smoothGeometry = Polyline(plot, _trainX, _trainSmooth);
        _valGeometry = Polyline(plot, _valX, _valY);
    }

    private StreamGeometry? Polyline(Rect plot, List<double> xs, List<double> ys)
    {
        if (xs.Count == 0)
        {
            return null;
        }
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(X(plot, xs[0]), Y(plot, ys[0])), false);
            for (var i = 1; i < xs.Count; i++)
            {
                ctx.LineTo(new Point(X(plot, xs[i]), Y(plot, ys[i])));
            }
            if (xs.Count == 1)
            {
                ctx.LineTo(new Point(X(plot, xs[0]) + 0.01, Y(plot, ys[0])));
            }
            ctx.EndFigure(false);
        }
        return geometry;
    }

    private void EnsurePens()
    {
        var width = CurveStrokeWidth;
        _smoothPen ??= new ImmutablePen(Immutable(TrainBrush), width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        _rawPen ??= new ImmutablePen(Immutable(TrainBrush), RawStrokeWidth, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        _valPen ??= new ImmutablePen(Immutable(ValidationBrush), width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        _gridPen ??= new ImmutablePen(Immutable(GridBrush), 1);
        _inspectPen ??= new ImmutablePen(Immutable(GridBrush), 1, new ImmutableDashStyle([3, 3], 0));
        _focusPen ??= new ImmutablePen(Immutable(FocusBrush), 2);
        _dotRingPen ??= new ImmutablePen(Immutable(SurfaceBrush), 1.5);
    }

    private static IImmutableBrush? Immutable(IBrush? brush) => brush switch
    {
        null => null,
        IImmutableBrush immutable => immutable,
        ISolidColorBrush solid => new ImmutableSolidColorBrush(solid.Color, solid.Opacity),
        _ => brush.ToImmutable(),
    };

    private void EnsureLabels(Rect plot)
    {
        if (!_labelsDirty)
        {
            return;
        }
        _labelsDirty = false;
        DisposeLabels();
        var typeface = new Typeface(FontFamily);
        var brush = LabelBrush;
        if (_yTop is { } top)
        {
            for (var i = 0; i < s_gridFractions.Length; i++)
            {
                _axisLabels[i] = new TextLayout(TrainingRunFormat.AxisLabel(top * s_gridFractions[i]), typeface, FontSize, brush);
            }
        }
        for (var e = 0; e < _epochs; e++)
        {
            _epochLabels.Add(new TextLayout(_epochLabel(e + 1), typeface, FontSize, brush, maxWidth: Math.Max(1, plot.Width / _epochs),
                textTrimming: TextTrimming.CharacterEllipsis, maxLines: 1));
        }
    }

    private void DisposeLabels()
    {
        for (var i = 0; i < _axisLabels.Length; i++)
        {
            _axisLabels[i]?.Dispose();
            _axisLabels[i] = null;
        }
        foreach (var label in _epochLabels)
        {
            label.Dispose();
        }
        _epochLabels.Clear();
        _labelsDirty = true;
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        var plot = PlotRect(size);
        if (plot.Width <= 0 || plot.Height <= 0)
        {
            return;
        }
        EnsurePens();
        EnsureGeometry(plot);
        EnsureLabels(plot);
        var perEpoch = (double)_totalSteps / _epochs;

        // Every other epoch as a faint band.
        if (BandBrush is { } band && _epochs > 1)
        {
            using (context.PushOpacity(BandOpacity))
            {
                for (var e = 1; e < _epochs; e += 2)
                {
                    var x0 = X(plot, e * perEpoch);
                    var x1 = X(plot, Math.Min(_totalSteps, (e + 1) * perEpoch));
                    context.FillRectangle(band, new Rect(x0, plot.Y, x1 - x0, plot.Height));
                }
            }
        }

        // Gridlines with the fixed loss axis.
        for (var i = 0; i < s_gridFractions.Length; i++)
        {
            var y = Math.Round(plot.Bottom - s_gridFractions[i] * plot.Height) + 0.5;
            context.DrawLine(_gridPen!, new Point(plot.X, y), new Point(plot.Right, y));
            if (_axisLabels[i] is { } label)
            {
                label.Draw(context, new Point(plot.X - 8 - label.WidthIncludingTrailingWhitespace, y - label.Height / 2));
            }
        }

        // The curves up to the eased head: a clip over cached geometry.
        var headX = X(plot, _head);
        if (_head > 0 && (_rawGeometry is not null || _valGeometry is not null))
        {
            using (context.PushClip(new Rect(0, 0, Math.Max(0, headX), size.Height)))
            {
                if (_rawGeometry is not null)
                {
                    using (context.PushOpacity(RawOpacity))
                    {
                        context.DrawGeometry(null, _rawPen, _rawGeometry);
                    }
                }
                if (_valGeometry is not null)
                {
                    context.DrawGeometry(null, _valPen, _valGeometry);
                }
                if (_smoothGeometry is not null)
                {
                    context.DrawGeometry(null, _smoothPen, _smoothGeometry);
                }
            }
            var sampleRadius = SampleDotRadius;
            for (var i = 0; i < _valX.Count && _valX[i] <= _head + 1e-6; i++)
            {
                context.DrawEllipse(ValidationBrush, _dotRingPen, new Point(X(plot, _valX[i]), Y(plot, _valY[i])), sampleRadius, sampleRadius);
            }
            if (_live && _trainSmooth.Count > 0 && _inspected is null)
            {
                var r = HeadDotRadius;
                context.DrawEllipse(TrainBrush, _dotRingPen, new Point(headX, Y(plot, SmoothAtStep(_head))), r, r);
            }
        }

        if (_inspected is { } index && index < _points.Count)
        {
            var point = _points[index];
            var x = Math.Round(X(plot, point.Step)) + 0.5;
            context.DrawLine(_inspectPen!, new Point(x, plot.Y), new Point(x, plot.Bottom));
            var r = HeadDotRadius;
            if (SmoothedAt(index) is { } smooth)
            {
                context.DrawEllipse(TrainBrush, _dotRingPen, new Point(X(plot, point.Step), Y(plot, smooth)), r, r);
            }
            if (point.Validation is { } validation)
            {
                context.DrawEllipse(ValidationBrush, _dotRingPen, new Point(X(plot, point.Step), Y(plot, validation)), r, r);
            }
        }

        // The step axis as the epoch progress bar: one rounded segment per epoch.
        var barY = plot.Bottom + ProgressMargin;
        var barHeight = ProgressHeight;
        var gap = _epochs > 1 ? EpochGap : 0;
        var segment = (plot.Width - gap * (_epochs - 1)) / _epochs;
        var radius = barHeight / 2;
        for (var e = 0; e < _epochs; e++)
        {
            var x0 = plot.X + e * (segment + gap);
            var bar = new Rect(x0, barY, Math.Max(0, segment), barHeight);
            if (TrackBrush is { } track)
            {
                context.DrawRectangle(track, null, bar, radius, radius);
            }
            var fill = Math.Clamp((_progress - e * perEpoch) / perEpoch, 0, 1);
            if (fill > 0 && ProgressBrush is { } progress)
            {
                context.DrawRectangle(progress, null, new Rect(x0, barY, Math.Max(barHeight, segment * fill), barHeight), radius, radius);
            }
            if (e < _epochLabels.Count)
            {
                _epochLabels[e].Draw(context, new Point(x0, barY + barHeight + 6));
            }
        }

        if (IsFocused && Classes.Contains(":focus-visible"))
        {
            var corner = FocusCornerRadius.TopLeft;
            context.DrawRectangle(null, _focusPen, new Rect(size).Inflate(3), corner, corner);
        }
    }

    /// <summary>The smoothed train loss at any step (linear between samples).</summary>
    private double SmoothAtStep(double step)
    {
        if (_trainX.Count == 0)
        {
            return 0;
        }
        if (step <= _trainX[0])
        {
            return _trainSmooth[0];
        }
        var last = _trainX.Count - 1;
        if (step >= _trainX[last])
        {
            return _trainSmooth[last];
        }
        var lo = 0;
        var hi = last;
        while (hi - lo > 1)
        {
            var mid = (lo + hi) / 2;
            if (_trainX[mid] <= step)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }
        var span = _trainX[hi] - _trainX[lo];
        var t = span <= 0 ? 1 : (step - _trainX[lo]) / span;
        return _trainSmooth[lo] + (_trainSmooth[hi] - _trainSmooth[lo]) * t;
    }

    private static int BinarySearch(List<double> xs, double value)
    {
        var lo = 0;
        var hi = xs.Count - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (xs[mid] < value)
            {
                lo = mid + 1;
            }
            else if (xs[mid] > value)
            {
                hi = mid - 1;
            }
            else
            {
                return mid;
            }
        }
        return -1;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);
}
