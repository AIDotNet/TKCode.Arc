using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The track of a <see cref="MemoryMeter"/>, drawn directly: a pill-shaped track and, clipped to it, a fill from the left
/// edge across <see cref="Ratio"/> of the width (Arc scales a full-width fill from its left edge inside the rounded,
/// clipped track: the fill's left end is rounded, its right end is cut straight). A new ratio only repaints.
/// </summary>
internal sealed class MemoryMeterBar : Control
{
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<MemoryMeterBar, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<MemoryMeterBar, IBrush?>(nameof(FillBrush));

    private double _ratio;

    static MemoryMeterBar()
    {
        AffectsRender<MemoryMeterBar>(TrackBrushProperty, FillBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<MemoryMeterBar>(false);
        FocusableProperty.OverrideDefaultValue<MemoryMeterBar>(false);
    }

    /// <summary>The track behind the fill (SurfaceMuted).</summary>
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    /// <summary>The fill (Accent, or Warning from the warning ratio on).</summary>
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    /// <summary>The share of the track the fill covers (0 to 1).</summary>
    public double Ratio
    {
        get => _ratio;
        set
        {
            var clamped = double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);
            if (clamped.Equals(_ratio))
            {
                return;
            }
            _ratio = clamped;
            InvalidateVisual();
        }
    }

    /// <summary>The width the fill covers right now.</summary>
    internal double FillWidth => Bounds.Width * _ratio;

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        var track = new RoundedRect(bounds, bounds.Height / 2);
        if (TrackBrush is { } trackBrush)
        {
            context.DrawRectangle(trackBrush, null, track);
        }
        if (_ratio <= 0 || FillBrush is not { } fillBrush)
        {
            return;
        }
        using (context.PushClip(track))
        {
            context.FillRectangle(fillBrush, new Rect(0, 0, bounds.Width * _ratio, bounds.Height));
        }
    }
}
