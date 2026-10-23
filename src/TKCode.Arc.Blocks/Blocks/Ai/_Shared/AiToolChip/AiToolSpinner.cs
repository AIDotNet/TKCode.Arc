using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The running tool's icon: Lucide LoaderCircle turning once every 900ms, linear (Arc's CSS spin). It turns on a Core
/// <see cref="LiveLoop"/> only while it is mounted, visible, in view and at full motion; reduced motion and Off show the
/// glyph at rest. The pen is cached, so a frame allocates nothing.
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
    private Pen? _pen;

    static AiToolSpinner()
    {
        AffectsMeasure<AiToolSpinner>(SizeProperty);
        AffectsRender<AiToolSpinner>(ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<AiToolSpinner>(false);
    }

    public AiToolSpinner()
    {
        _loop = new LiveLoop(this, Step);
        _loop.Start();
    }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>The rotation in degrees.</summary>
    internal double Angle => _angle;

    /// <summary>Whether frames arrive (mounted, visible, in view, full motion).</summary>
    internal bool IsSpinning => _loop.IsRunning;

    private bool Step(double seconds)
    {
        _angle = (_angle + seconds / TurnSeconds * 360) % 360;
        InvalidateVisual();
        return true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty)
        {
            _pen = null;
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
        using (context.PushTransform(matrix))
        {
            context.DrawGeometry(null, _pen, ArcIcons.LoaderCircle);
        }
    }
}
