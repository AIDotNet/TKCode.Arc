using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One loop of an illustration's story: actions at times in seconds, built once. The clock passes over them in order;
/// at the end of the loop it wraps and the next loop starts.
/// </summary>
internal sealed class IllustrationScript
{
    private readonly List<(double At, Action Act)> _beats = new();
    private int _next;

    /// <summary>Length of one loop in seconds (at least the last beat).</summary>
    public double Length { get; set; }

    /// <summary>Called when a loop ends, before the next one starts.</summary>
    public Action? Looped { get; set; }

    public void Clear()
    {
        _beats.Clear();
        _next = 0;
        Length = 0;
        Looped = null;
    }

    public void At(double seconds, Action act)
    {
        var index = _beats.FindLastIndex(b => b.At <= seconds) + 1;
        _beats.Insert(index, (seconds, act));
        Length = Math.Max(Length, seconds);
    }

    public void Rewind() => _next = 0;

    /// <summary>Moves <paramref name="time"/> on by <paramref name="seconds"/> and runs the beats it passes.</summary>
    public void Advance(ref double time, double seconds)
    {
        var end = time + seconds;
        while (true)
        {
            while (_next < _beats.Count && _beats[_next].At <= end)
            {
                _beats[_next++].Act();
            }
            if (Length <= 0 || end < Length)
            {
                break;
            }
            // Wrap: the loop is over; carry the remainder into the next loop.
            end -= Length;
            _next = 0;
            Looped?.Invoke();
            if (end <= 0)
            {
                end = 0;
                while (_next < _beats.Count && _beats[_next].At <= 0)
                {
                    _beats[_next++].Act();
                }
                break;
            }
        }
        time = end;
    }
}

/// <summary>
/// Draws an <see cref="Illustration"/>'s scene, scaled uniformly to the width it is given (never above 1) and centred.
/// Hovering reports the scene point under the pointer so the owner can light up a node and its connections.
/// </summary>
internal sealed class IllustrationCanvas : Control
{
    private readonly IllustrationMotionLayer _motion;
    private Illustration? _owner;

    public IllustrationCanvas()
    {
        _motion = new IllustrationMotionLayer(this);
        VisualChildren.Add(_motion);
    }

    /// <summary>Redraws the travelling parts only (packets, busy rings, meters).</summary>
    public void InvalidateMotion() => _motion.InvalidateVisual();

    public Illustration? Owner
    {
        get => _owner;
        set
        {
            _owner = value;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <summary>The current scale from scene to control coordinates.</summary>
    public double Scale { get; private set; } = 1;

    private Vector _offset;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_owner is null)
        {
            return default;
        }
        var scene = _owner.SceneSize;
        var scale = double.IsInfinity(availableSize.Width) ? 1 : Math.Min(1, availableSize.Width / scene.Width);
        var size = new Size(scene.Width * scale, scene.Height * scale);
        _motion.Measure(size);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_owner is not null)
        {
            var scene = _owner.SceneSize;
            Scale = Math.Max(0.01, Math.Min(1, Math.Min(finalSize.Width / scene.Width, finalSize.Height / scene.Height)));
            _offset = new Vector((finalSize.Width - scene.Width * Scale) / 2, (finalSize.Height - scene.Height * Scale) / 2);
        }
        _motion.Arrange(new Rect(finalSize));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (_owner is null)
        {
            return;
        }
        using (context.PushTransform(SceneTransform))
        {
            _owner.RenderStatic(context, Scale);
        }
    }

    public Matrix SceneTransform => Matrix.CreateScale(Scale, Scale) * Matrix.CreateTranslation(_offset.X, _offset.Y);

    /// <summary>
    /// The parts that move every frame, in their own visual: a frame where only packets travel re-records this layer
    /// alone, not the cards, text and lines underneath.
    /// </summary>
    private sealed class IllustrationMotionLayer : Control
    {
        private readonly IllustrationCanvas _canvas;

        public IllustrationMotionLayer(IllustrationCanvas canvas)
        {
            _canvas = canvas;
            IsHitTestVisible = false;
        }

        public override void Render(DrawingContext context)
        {
            if (_canvas._owner is not { } owner)
            {
                return;
            }
            using (context.PushTransform(_canvas.SceneTransform))
            {
                owner.RenderMotion(context, _canvas.Scale);
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            _owner?.SetHover(ToScene(e.GetPosition(this)));
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _owner?.SetHover(null);
    }

    /// <summary>A point in control coordinates mapped into scene coordinates.</summary>
    public Point ToScene(Point point) => new((point.X - _offset.X) / Scale, (point.Y - _offset.Y) / Scale);
}

/// <summary>The dotted backdrop of an illustration frame (drawn only when its size changes).</summary>
internal sealed class IllustrationDots : Control
{
    public static readonly StyledProperty<IBrush?> DotBrushProperty =
        AvaloniaProperty.Register<IllustrationDots, IBrush?>(nameof(DotBrush));

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<IllustrationDots, double>(nameof(Spacing), 16);

    public IBrush? DotBrush { get => GetValue(DotBrushProperty); set => SetValue(DotBrushProperty, value); }

    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    static IllustrationDots()
    {
        AffectsRender<IllustrationDots>(DotBrushProperty, SpacingProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<IllustrationDots>(false);
    }

    public override void Render(DrawingContext context)
    {
        if (DotBrush is not { } brush || Spacing < 4)
        {
            return;
        }
        var size = Bounds.Size;
        var spacing = Spacing;
        var startX = (size.Width % spacing) / 2;
        var startY = (size.Height % spacing) / 2;
        for (var y = startY; y <= size.Height; y += spacing)
        {
            for (var x = startX; x <= size.Width; x += spacing)
            {
                context.DrawEllipse(brush, null, new Point(x, y), 0.9, 0.9);
            }
        }
    }
}

/// <summary>The round play / pause button of an illustration.</summary>
internal sealed class IllustrationPlayButton : Avalonia.Controls.Button
{
    protected override Type StyleKeyOverride => typeof(IllustrationPlayButton);
}
