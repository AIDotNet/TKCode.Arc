using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The placeholder shapes of a <see cref="Skeleton"/>, drawn directly: an optional round avatar and 1–6 text lines
/// (first 16px tall at 48%, last at 72%, the rest full width, 12px apart). Each block pulses a beat (90ms) after the
/// one above it: opacity 1 → 0.52 → 1 every 1.8s on the in-out curve, 11 times (about 20s), then rests (P21). The
/// loop runs only while attached, visible, on screen and at full motion; reduced motion keeps the blocks still.
/// </summary>
internal sealed class SkeletonBlocks : Control
{
    public static readonly StyledProperty<int> LinesProperty =
        AvaloniaProperty.Register<SkeletonBlocks, int>(nameof(Lines), 3);

    public static readonly StyledProperty<bool> IsAvatarVisibleProperty =
        AvaloniaProperty.Register<SkeletonBlocks, bool>(nameof(IsAvatarVisible));

    public static readonly StyledProperty<IBrush?> BlockBrushProperty =
        AvaloniaProperty.Register<SkeletonBlocks, IBrush?>(nameof(BlockBrush));

    public static readonly StyledProperty<double> BlockRadiusProperty =
        AvaloniaProperty.Register<SkeletonBlocks, double>(nameof(BlockRadius), 18);

    public static readonly StyledProperty<double> AvatarDiameterProperty =
        AvaloniaProperty.Register<SkeletonBlocks, double>(nameof(AvatarDiameter), 44);

    public int Lines { get => GetValue(LinesProperty); set => SetValue(LinesProperty, value); }
    public bool IsAvatarVisible { get => GetValue(IsAvatarVisibleProperty); set => SetValue(IsAvatarVisibleProperty, value); }
    public IBrush? BlockBrush { get => GetValue(BlockBrushProperty); set => SetValue(BlockBrushProperty, value); }
    public double BlockRadius { get => GetValue(BlockRadiusProperty); set => SetValue(BlockRadiusProperty, value); }
    public double AvatarDiameter { get => GetValue(AvatarDiameterProperty); set => SetValue(AvatarDiameterProperty, value); }

    internal const double PulsePeriod = 1.8;
    internal const double PulseStagger = 0.09;
    internal const int PulseIterations = 11;
    internal const double PulseLow = 0.52;
    private const double AvatarGap = 16;
    private const double LinesTop = 4;
    private const double FirstLineHeight = 16;
    private const double LineHeight = 12;
    private const double LineGap = 12;
    private static readonly Easing s_inOut = ArcEasings.InOut;

    private readonly LiveLoop _loop;
    private double _elapsed;

    static SkeletonBlocks()
    {
        AffectsMeasure<SkeletonBlocks>(LinesProperty, IsAvatarVisibleProperty, AvatarDiameterProperty);
        AffectsRender<SkeletonBlocks>(BlockBrushProperty, BlockRadiusProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<SkeletonBlocks>(false);
    }

    public SkeletonBlocks()
    {
        // P21: the Core loop pauses itself while detached, hidden, off screen or below full motion; it finishes
        // after the last pulse.
        _loop = new LiveLoop(this, Step);
        // Stopping redraws the blocks at rest (reduced motion keeps them still).
        _loop.RunningChanged += _ => InvalidateVisual();
        _loop.Start();
    }

    /// <summary>Clamped line count (Arc: 1–6).</summary>
    internal int LineCount => Math.Clamp(Lines, 1, 6);

    /// <summary>Seconds after which every block has finished its pulses.</summary>
    internal double TotalDuration => PulseIterations * PulsePeriod + PulseStagger * (LineCount + (IsAvatarVisible ? 1 : 0) - 1);

    internal bool IsPulsing => _loop.IsRunning;

    /// <summary>Opacity of block <paramref name="index"/> at <paramref name="elapsed"/> seconds (CSS keyframes, fill both).</summary>
    internal static double PulseOpacity(int index, double elapsed)
    {
        var local = elapsed - index * PulseStagger;
        if (local <= 0 || local >= PulseIterations * PulsePeriod)
        {
            return 1;
        }
        var phase = local % PulsePeriod / PulsePeriod;
        return phase < 0.5
            ? 1 - (1 - PulseLow) * s_inOut.Ease(phase * 2)
            : PulseLow + (1 - PulseLow) * s_inOut.Ease((phase - 0.5) * 2);
    }

    private bool Step(double dt)
    {
        _elapsed += dt;
        InvalidateVisual();
        return _elapsed < TotalDuration;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = LineCount;
        var lines = LinesTop + FirstLineHeight + (count - 1) * (LineGap + LineHeight);
        var height = Math.Max(IsAvatarVisible ? AvatarDiameter : 0, lines);
        var width = double.IsInfinity(availableSize.Width) ? 320 : availableSize.Width;
        return new Size(width, height);
    }

    public override void Render(DrawingContext context)
    {
        var brush = BlockBrush;
        if (brush is null)
        {
            return;
        }
        var pulsing = ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        var width = Bounds.Width;
        var index = 0;
        var x = 0.0;
        if (IsAvatarVisible)
        {
            var d = AvatarDiameter;
            using (context.PushOpacity(pulsing ? PulseOpacity(index, _elapsed) : 1))
            {
                context.DrawEllipse(brush, null, new Rect(0, 0, d, d));
            }
            index++;
            x = d + AvatarGap;
        }
        var column = Math.Max(0, width - x);
        var count = LineCount;
        var y = LinesTop;
        for (var i = 0; i < count; i++)
        {
            var height = i == 0 ? FirstLineHeight : LineHeight;
            var fraction = i == count - 1 ? 0.72 : i == 0 ? 0.48 : 1.0;
            var rect = new Rect(x, y, column * fraction, height);
            var radius = Math.Min(BlockRadius, height / 2);
            using (context.PushOpacity(pulsing ? PulseOpacity(index, _elapsed) : 1))
            {
                context.DrawRectangle(brush, null, new RoundedRect(rect, radius));
            }
            index++;
            y += height + LineGap;
        }
    }
}
