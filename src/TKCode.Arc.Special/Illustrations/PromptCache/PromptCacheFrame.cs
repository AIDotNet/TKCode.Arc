using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The cache panel's frame in a <see cref="PromptCache"/>: a rounded 1px outline, solid while the cache holds something
/// and dashed while it is empty or expired (a plain <see cref="Border"/> cannot dash). The color is a styled brush so the
/// theme can move it between Border and BorderStrong with a transition.
/// </summary>
internal sealed class PromptCacheFrame : Decorator
{
    /// <summary>Defines the <see cref="BorderBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        Border.BorderBrushProperty.AddOwner<PromptCacheFrame>();

    /// <summary>Defines the <see cref="CornerRadius"/> property.</summary>
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner<PromptCacheFrame>();

    /// <summary>Defines the <see cref="IsDashed"/> property.</summary>
    public static readonly StyledProperty<bool> IsDashedProperty =
        AvaloniaProperty.Register<PromptCacheFrame, bool>(nameof(IsDashed));

    private static readonly ImmutableDashStyle s_dash = new([3, 3], 0);

    static PromptCacheFrame()
    {
        AffectsRender<PromptCacheFrame>(BorderBrushProperty, CornerRadiusProperty, IsDashedProperty);
    }

    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>Dashes the outline (an empty or expired cache).</summary>
    public bool IsDashed { get => GetValue(IsDashedProperty); set => SetValue(IsDashedProperty, value); }

    public override void Render(DrawingContext context)
    {
        if (BorderBrush is not { } brush || Bounds.Width <= 1 || Bounds.Height <= 1)
        {
            return;
        }
        var pen = new ImmutablePen(brush.ToImmutable(), 1, IsDashed ? s_dash : null);
        var r = CornerRadius.TopLeft;
        context.DrawRectangle(null, pen, new Rect(Bounds.Size).Deflate(0.5), Math.Max(0, r - 0.5), Math.Max(0, r - 0.5));
    }
}
