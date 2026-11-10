using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The running tool's icon: Lucide LoaderCircle turning once every 900ms, linear (Arc's CSS spin). It turns on a Core
/// <see cref="LiveLoop"/> only while it is mounted, visible, in view and at full motion; under reduced motion the glyph
/// holds still and breathes in opacity (<see cref="ArcBusyPulse"/>) so it still reads as busy, and Off shows it at rest.
/// The pen is cached, so a frame allocates nothing.
/// </summary>
internal sealed class AiToolSpinner : Control
{
    /// <summary>The glyph color (inherited text color).</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<AiToolSpinner>();

    /// <summary>Width and height in DIPs (14).</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<AiToolSpinner, double>(nameof(Size), 14);

    /// <summary>One turn.</summary>
    internal const double TurnSeconds = 0.9;

    private readonly LiveLoop _loop;
    private double _angle;
    private double _pulse;
    private bool _full = true;
    private Pen? _pen;

    static AiToolSpinner()
    {
        AffectsMeasure<AiToolSpinner>(SizeProperty);
        AffectsRender<AiToolSpinner>(ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<AiToolSpinner>(false);
    }

    public AiToolSpinner()
    {
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
        _loop.RunningChanged += running =>
        {
            if (running)
            {
                RefreshLevel();
            }
            InvalidateVisual();
        };
        // The motion level can come from the theme's resources: re-read it when they change.
        ResourcesChanged += (_, _) => RefreshLevel();
        _loop.Start();
    }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>The rotation in degrees.</summary>
    internal double Angle => _angle;

    /// <summary>Whether the glyph turns (frames arrive at full motion while mounted, visible and in view).</summary>
    internal bool IsSpinning => _loop.IsRunning && _full;

    /// <summary>Whether the resting glyph breathes in opacity instead (reduced motion).</summary>
    internal bool IsPulsing => _loop.IsRunning && !_full;

    /// <summary>The glyph's opacity now: 1 unless it breathes under reduced motion.</summary>
    internal double PulseOpacity => IsPulsing ? ArcBusyPulse.Opacity(_pulse) : 1;

    private bool Step(double seconds)
    {
        if (_full)
        {
            _angle = (_angle + seconds / TurnSeconds * 360) % 360;
        }
        else
        {
            _pulse = ArcBusyPulse.Advance(_pulse, seconds);
        }
        InvalidateVisual();
        return true;
    }

    // Read once per level change, not per frame (the level lookup walks up to the resource root).
    private void RefreshLevel()
    {
        var full = ArcMotion.IsFull(this);
        if (full != _full)
        {
            _full = full;
            InvalidateVisual();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty)
        {
            _pen = null;
        }
        else if (change.Property == ArcMotion.LevelProperty)
        {
            RefreshLevel();
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        if (Foreground is not { } brush)
        {
            return;
        }
        _pen ??= new Pen(brush, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var size = Math.Min(Bounds.Width, Bounds.Height);
        var scale = size / 24.0;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var matrix = Matrix.CreateTranslation(-12, -12) * Matrix.CreateRotation(_angle * Math.PI / 180.0)
                     * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(center.X, center.Y);
        var opacity = PulseOpacity;
        using (opacity < 1 ? context.PushOpacity(opacity) : default(DrawingContext.PushedState?))
        using (context.PushTransform(matrix))
        {
            context.DrawGeometry(null, _pen, ArcIcons.LoaderCircle);
        }
    }
}
