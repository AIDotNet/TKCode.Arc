using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A status glyph that morphs when its shape changes (Arc's tone / type swap): the old glyph shrinks to 0.6 into a
/// subtle blur while the new one grows out of it on the snappy spring; opacity and blur tween so the blur never
/// overshoots. It can also show Arc's loading ring (a 3/4 ring turning once every 0.8s, only while visible and
/// under full motion). Drawn in <c>Render</c> by at most two layers.
/// </summary>
internal sealed class MessagesGlyphSwap : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<MessagesGlyphSwap, Geometry?>(nameof(Data));

    public static readonly StyledProperty<bool> IsSpinnerProperty =
        AvaloniaProperty.Register<MessagesGlyphSwap, bool>(nameof(IsSpinner));

    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<MessagesGlyphSwap, double>(nameof(GlyphSize), 18);

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<MessagesGlyphSwap, double>(nameof(StrokeThickness), 1.75);

    public static readonly StyledProperty<double> SpinnerSizeProperty =
        AvaloniaProperty.Register<MessagesGlyphSwap, double>(nameof(SpinnerSize), 15);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<MessagesGlyphSwap>();

    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public bool IsSpinner
    {
        get => GetValue(IsSpinnerProperty);
        set => SetValue(IsSpinnerProperty, value);
    }

    public double GlyphSize
    {
        get => GetValue(GlyphSizeProperty);
        set => SetValue(GlyphSizeProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public double SpinnerSize
    {
        get => GetValue(SpinnerSizeProperty);
        set => SetValue(SpinnerSizeProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    private MessagesGlyphLayer? _current;
    private MessagesGlyphLayer? _exiting;
    private bool _initialized;

    internal MessagesGlyphLayer? CurrentLayer => _current;

    static MessagesGlyphSwap()
    {
        AffectsMeasure<MessagesGlyphSwap>(GlyphSizeProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<MessagesGlyphSwap>(false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataProperty || change.Property == IsSpinnerProperty)
        {
            Swap();
        }
        else if (change.Property == GlyphSizeProperty || change.Property == StrokeThicknessProperty ||
                 change.Property == SpinnerSizeProperty)
        {
            _current?.Configure(GlyphSize, StrokeThickness, SpinnerSize);
        }
    }

    private MessagesGlyphLayer CreateLayer()
    {
        var layer = new MessagesGlyphLayer();
        layer.Configure(GlyphSize, StrokeThickness, SpinnerSize);
        layer.Show(Data, IsSpinner);
        VisualChildren.Add(layer);
        LogicalChildren.Add(layer);
        return layer;
    }

    private void RemoveLayer(MessagesGlyphLayer layer)
    {
        layer.StopMotion();
        VisualChildren.Remove(layer);
        LogicalChildren.Remove(layer);
    }

    private void Swap()
    {
        if (_current is null)
        {
            _current = CreateLayer();
            _initialized = true;
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!_initialized || !IsLoaded || !IsEffectivelyVisible || level == ArcMotionLevel.Off)
        {
            _current.Show(Data, IsSpinner);
            return;
        }
        if (_exiting is not null)
        {
            RemoveLayer(_exiting);
        }
        _exiting = _current;
        var outgoing = _exiting;
        RemoveWhenDone(outgoing, outgoing.Leave(level == ArcMotionLevel.Full));
        _current = CreateLayer();
        _current.Enter(level == ArcMotionLevel.Full);
    }

    /// <summary>Plays the small settle of a newly shown message (scale 0.6 → 1 out of a subtle blur).</summary>
    public void PlayEnter()
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (_current is null || level != ArcMotionLevel.Full || !IsEffectivelyVisible)
        {
            return;
        }
        _current.Enter(true);
    }

    private void RemoveWhenDone(MessagesGlyphLayer layer, Task<bool> task)
    {
        task.ThenOnUiThread(() =>
        {
            if (_exiting == layer)
            {
                RemoveLayer(layer);
                _exiting = null;
            }
        });
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = Math.Max(GlyphSize, SpinnerSize);
        foreach (var child in VisualChildren)
        {
            if (child is Control control)
            {
                control.Measure(new Size(size, size));
            }
        }
        return new Size(size, size);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in VisualChildren)
        {
            if (child is Control control)
            {
                control.Arrange(new Rect(finalSize));
            }
        }
        return finalSize;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_exiting is not null)
        {
            RemoveLayer(_exiting);
            _exiting = null;
        }
        _current?.Settle();
    }
}

/// <summary>One drawn glyph (or loading ring) of a <see cref="MessagesGlyphSwap"/>.</summary>
internal sealed class MessagesGlyphLayer : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<MessagesGlyphLayer>();

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>One turn of the loading ring (0.8s, linear).</summary>
    private const double SpinSeconds = 0.8;

    /// <summary>Spinner rotation in degrees (advanced by the loop; a field, so a frame allocates nothing).</summary>
    internal double Angle => _angle;

    private readonly ScaleTransform _scale = new();
    private Geometry? _data;
    private bool _spinner;
    private double _glyphSize = 18;
    private double _stroke = 1.75;
    private double _spinnerSize = 15;
    private Pen? _pen;
    private IBrush? _penBrush;
    private double _penThickness;
    private Pen? _ringPen;
    private IBrush? _ringPenBrush;
    private double _ringPenThickness;
    private StreamGeometry? _ring;
    private double _ringSize;
    private BlurEffect? _blur;
    private MotionValue<double>? _grow;
    private MotionValue<double>? _fade;
    private LiveLoop? _spin;
    private double _angle;

    static MessagesGlyphLayer()
    {
        AffectsRender<MessagesGlyphLayer>(ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<MessagesGlyphLayer>(false);
    }

    public MessagesGlyphLayer()
    {
        RenderTransform = _scale;
        RenderTransformOrigin = RelativePoint.Center;
    }

    internal bool IsSpinner => _spinner;

    internal bool IsSpinning => _spin is { IsRunning: true };

    public void Configure(double glyphSize, double stroke, double spinnerSize)
    {
        _glyphSize = glyphSize;
        _stroke = stroke;
        _spinnerSize = spinnerSize;
        _ring = null;
        InvalidateVisual();
    }

    public void Show(Geometry? data, bool spinner)
    {
        _data = data;
        _spinner = spinner;
        UpdateSpin();
        InvalidateVisual();
    }

    private (MotionValue<double> Grow, MotionValue<double> Fade) Values()
    {
        if (_grow is null || _fade is null)
        {
            _grow = new MotionValue<double>(this, 1, MotionVectors.Double);
            _grow.Changed += v =>
            {
                _scale.ScaleX = v;
                _scale.ScaleY = v;
            };
            _fade = new MotionValue<double>(this, 1, MotionVectors.Double);
            _fade.Changed += v =>
            {
                Opacity = Math.Clamp(v, 0, 1);
                if (_blur is null)
                {
                    return;
                }
                var radius = 2 * (1 - Math.Clamp(v, 0, 1));
                if (radius < 0.05)
                {
                    Effect = null;
                }
                else
                {
                    _blur.Radius = radius;
                    Effect = _blur;
                }
            };
        }
        return (_grow, _fade);
    }

    public void Enter(bool full)
    {
        var (grow, fade) = Values();
        if (full)
        {
            _blur ??= new BlurEffect();
            grow.Jump(0.6);
            fade.Jump(0);
            _ = grow.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
            _ = fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        else
        {
            _blur = null;
            Effect = null;
            grow.Jump(1);
            fade.Jump(0);
            _ = fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
    }

    public Task<bool> Leave(bool full)
    {
        var (grow, fade) = Values();
        if (full)
        {
            _blur ??= new BlurEffect();
            _ = grow.AnimateTo(0.6, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            return fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        _blur = null;
        Effect = null;
        return fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
    }

    public void Settle()
    {
        _grow?.Jump(1);
        _fade?.Jump(1);
        Effect = null;
    }

    public void StopMotion()
    {
        _grow?.Stop();
        _fade?.Stop();
        StopSpin();
    }

    /// <summary>
    /// The ring turns on a Core <see cref="LiveLoop"/> (an infinite Avalonia <c>Animation</c> cannot be started with
    /// <c>RunAsync</c>): it pauses while hidden, detached, off-screen or below full motion and keeps its phase.
    /// </summary>
    private void UpdateSpin()
    {
        if (!_spinner)
        {
            StopSpin();
            return;
        }
        _spin ??= new LiveLoop(this, StepSpin);
        _spin.Start();
    }

    private bool StepSpin(double seconds)
    {
        _angle = (_angle + seconds / SpinSeconds * 360) % 360;
        InvalidateVisual();
        return true;
    }

    private void StopSpin() => _spin?.Stop();

    public override void Render(DrawingContext context)
    {
        var brush = Foreground;
        if (brush is null)
        {
            return;
        }
        var bounds = Bounds.Size;
        if (_spinner)
        {
            RenderSpinner(context, brush, bounds);
            return;
        }
        if (_data is null)
        {
            return;
        }
        if (_pen is null || !ReferenceEquals(_penBrush, brush) || Math.Abs(_penThickness - _stroke) > double.Epsilon)
        {
            _pen = new Pen(brush, _stroke, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _penBrush = brush;
            _penThickness = _stroke;
        }
        var scale = _glyphSize / 24.0;
        var offsetX = (bounds.Width - _glyphSize) / 2;
        var offsetY = (bounds.Height - _glyphSize) / 2;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
        {
            context.DrawGeometry(null, _pen, _data);
        }
    }

    private void RenderSpinner(DrawingContext context, IBrush brush, Size bounds)
    {
        var size = _spinnerSize;
        var t = _stroke;
        if (_ring is null || Math.Abs(_ringSize - size) > double.Epsilon)
        {
            // Three quarters of a ring; the right quarter is open (border-right-color: transparent).
            var radius = (size - t) / 2;
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                var start = new Point(radius * Math.Cos(Math.PI / 4), radius * Math.Sin(Math.PI / 4));
                var end = new Point(radius * Math.Cos(-Math.PI / 4), radius * Math.Sin(-Math.PI / 4));
                ctx.BeginFigure(start, false);
                ctx.ArcTo(end, new Size(radius, radius), 0, true, SweepDirection.Clockwise);
                ctx.EndFigure(false);
            }
            _ring = geometry;
            _ringSize = size;
        }
        if (_ringPen is null || !ReferenceEquals(_ringPenBrush, brush) || Math.Abs(_ringPenThickness - t) > double.Epsilon)
        {
            _ringPen = new Pen(brush, t, lineCap: PenLineCap.Flat);
            _ringPenBrush = brush;
            _ringPenThickness = t;
        }
        var center = new Point(bounds.Width / 2, bounds.Height / 2);
        using (context.PushTransform(Matrix.CreateRotation(_angle * Math.PI / 180.0) * Matrix.CreateTranslation(center.X, center.Y)))
        {
            context.DrawGeometry(null, _ringPen, _ring);
        }
    }
}
