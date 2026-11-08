using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// An 8px legend dot of <see cref="SeatManager"/> (radius 2) drawn in the same look as the meter's cells of its kind, so the
/// legend always matches the meter (the accent tints are computed once, by the meter).
/// </summary>
internal sealed class SeatManagerLegendDot : Control
{
    private SeatManagerMeter? _meter;

    public SeatManagerLegendDot()
    {
        Width = 8;
        Height = 8;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
    }

    public SeatManagerCellKind Kind { get; set; }

    public SeatManagerMeter? Meter
    {
        get => _meter;
        set
        {
            if (_meter is not null)
            {
                _meter.PaletteChanged -= InvalidateVisual;
            }
            _meter = value;
            if (_meter is not null)
            {
                _meter.PaletteChanged += InvalidateVisual;
            }
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        if (_meter is null)
        {
            return;
        }
        var look = _meter.LookOf(Kind);
        var rect = new Rect(Bounds.Size);
        using (context.PushOpacity(look.Opacity))
        {
            context.DrawRectangle(new ImmutableSolidColorBrush(look.Fill), null, new RoundedRect(rect, 2));
            if (look.Stroke.A > 0)
            {
                context.DrawRectangle(null, new ImmutablePen(new ImmutableSolidColorBrush(look.Stroke), 1),
                    new RoundedRect(rect.Deflate(0.5), 1.5));
            }
        }
    }
}

/// <summary>
/// The text action of the full-team notice ("Add a seat"): 13/20 medium in the accent, no fill, a keyboard focus ring
/// only. Its theme lives in the block's theme file.
/// </summary>
internal sealed class SeatManagerTextButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(SeatManagerTextButton);
}
