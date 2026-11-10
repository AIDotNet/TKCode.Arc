using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The fill of an option in multiple mode. Drawn, not laid out: reaching across the gap to a pressed neighbour and
/// squaring the corners that meet it are two animated numbers, so a join never runs a layout pass.
/// </summary>
internal sealed class ToggleGroupFill : Control
{
    public static readonly StyledProperty<IBrush?> PressedFillProperty =
        ToggleGroup.PressedFillProperty.AddOwner<ToggleGroupFill>();

    public static readonly StyledProperty<CornerRadius> RadiusProperty =
        AvaloniaProperty.Register<ToggleGroupFill, CornerRadius>(nameof(Radius));

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ToggleGroupFill, double>(nameof(Gap));

    /// <summary>1 when the option before this one is pressed too: the corners on that side are square.</summary>
    public static readonly StyledProperty<double> JoinStartProperty =
        AvaloniaProperty.Register<ToggleGroupFill, double>(nameof(JoinStart));

    /// <summary>1 when the next option is pressed too: the fill crosses the gap and those corners are square.</summary>
    public static readonly StyledProperty<double> JoinEndProperty =
        AvaloniaProperty.Register<ToggleGroupFill, double>(nameof(JoinEnd));

    static ToggleGroupFill()
    {
        AffectsRender<ToggleGroupFill>(PressedFillProperty, RadiusProperty, GapProperty, JoinStartProperty, JoinEndProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ToggleGroupFill>(false);
    }

    public IBrush? PressedFill { get => GetValue(PressedFillProperty); set => SetValue(PressedFillProperty, value); }
    public CornerRadius Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }
    public double JoinStart { get => GetValue(JoinStartProperty); set => SetValue(JoinStartProperty, value); }
    public double JoinEnd { get => GetValue(JoinEndProperty); set => SetValue(JoinEndProperty, value); }

    public override void Render(DrawingContext context)
    {
        var brush = PressedFill;
        if (brush is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }
        var joinStart = Math.Clamp(JoinStart, 0, 1);
        var joinEnd = Math.Clamp(JoinEnd, 0, 1);
        var radius = Radius;
        var rect = new Rect(0, 0, Bounds.Width + Gap * joinEnd, Bounds.Height);
        context.DrawRectangle(brush, null, new RoundedRect(rect,
            radius.TopLeft * (1 - joinStart), radius.TopRight * (1 - joinEnd),
            radius.BottomRight * (1 - joinEnd), radius.BottomLeft * (1 - joinStart)));
    }
}
