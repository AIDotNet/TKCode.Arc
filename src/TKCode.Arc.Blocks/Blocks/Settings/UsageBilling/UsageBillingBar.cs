using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The thin bars of <see cref="UsageBilling"/>, drawn in one pass: a rounded track, a fill up to <see cref="FillEnd"/> that
/// turns from <see cref="FillBrush"/> to <see cref="OverBrush"/> at <see cref="Split"/> (a meter's included amount), and an
/// optional tick at <see cref="Marker"/> (the limit's alert threshold). All positions are fractions of the width.
/// Movement is declared in the theme (spring transitions on the fractions under <c>:motion-full</c>), so the bar only
/// draws its current values.
/// </summary>
internal sealed class UsageBillingBar : Control
{
    /// <summary>Where the fill ends, 0–1.</summary>
    public static readonly StyledProperty<double> FillEndProperty =
        AvaloniaProperty.Register<UsageBillingBar, double>(nameof(FillEnd));

    /// <summary>Where the fill turns into the overage, 0–1; at or past <see cref="FillEnd"/> there is no overage.</summary>
    public static readonly StyledProperty<double> SplitProperty =
        AvaloniaProperty.Register<UsageBillingBar, double>(nameof(Split), 1);

    /// <summary>Where the tick sits, 0–1; NaN draws none.</summary>
    public static readonly StyledProperty<double> MarkerProperty =
        AvaloniaProperty.Register<UsageBillingBar, double>(nameof(Marker), double.NaN);

    /// <summary>Thickness of the track, centered in the bar's height (the rest is the tick's overhang).</summary>
    public static readonly StyledProperty<double> TrackThicknessProperty =
        AvaloniaProperty.Register<UsageBillingBar, double>(nameof(TrackThickness), 6);

    /// <summary>Gap between the fill and the overage.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<UsageBillingBar, double>(nameof(Gap), 2);

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<UsageBillingBar, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<UsageBillingBar, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<IBrush?> OverBrushProperty =
        AvaloniaProperty.Register<UsageBillingBar, IBrush?>(nameof(OverBrush));

    public static readonly StyledProperty<IBrush?> MarkerBrushProperty =
        AvaloniaProperty.Register<UsageBillingBar, IBrush?>(nameof(MarkerBrush));

    /// <summary>Paints the hairline gap on each side of the tick (the card surface).</summary>
    public static readonly StyledProperty<IBrush?> GapBrushProperty =
        AvaloniaProperty.Register<UsageBillingBar, IBrush?>(nameof(GapBrush));

    static UsageBillingBar()
    {
        AffectsRender<UsageBillingBar>(FillEndProperty, SplitProperty, MarkerProperty, TrackThicknessProperty, GapProperty,
            TrackBrushProperty, FillBrushProperty, OverBrushProperty, MarkerBrushProperty, GapBrushProperty);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<UsageBillingBar>(AutomationControlType.ProgressBar);
        FocusableProperty.OverrideDefaultValue<UsageBillingBar>(false);
    }

    public double FillEnd
    {
        get => GetValue(FillEndProperty);
        set => SetValue(FillEndProperty, value);
    }

    public double Split
    {
        get => GetValue(SplitProperty);
        set => SetValue(SplitProperty, value);
    }

    public double Marker
    {
        get => GetValue(MarkerProperty);
        set => SetValue(MarkerProperty, value);
    }

    public double TrackThickness
    {
        get => GetValue(TrackThicknessProperty);
        set => SetValue(TrackThicknessProperty, value);
    }

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public IBrush? FillBrush
    {
        get => GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    public IBrush? OverBrush
    {
        get => GetValue(OverBrushProperty);
        set => SetValue(OverBrushProperty, value);
    }

    public IBrush? MarkerBrush
    {
        get => GetValue(MarkerBrushProperty);
        set => SetValue(MarkerBrushProperty, value);
    }

    public IBrush? GapBrush
    {
        get => GetValue(GapBrushProperty);
        set => SetValue(GapBrushProperty, value);
    }

    /// <summary>Sets the fractions at once (each is clamped to 0–1; a NaN marker hides the tick).</summary>
    public void Show(double fillEnd, double split, double marker)
    {
        SetCurrentValue(FillEndProperty, Clamp(fillEnd));
        SetCurrentValue(SplitProperty, Clamp(split));
        SetCurrentValue(MarkerProperty, double.IsNaN(marker) ? double.NaN : Clamp(marker));
    }

    private static double Clamp(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }
        var thickness = Math.Min(TrackThickness, height);
        var top = (height - thickness) / 2;
        var radius = thickness / 2;
        var track = new Rect(0, top, width, thickness);
        context.DrawRectangle(TrackBrush, null, new RoundedRect(track, radius));

        var end = Clamp(FillEnd) * width;
        if (end > 0.01)
        {
            // One rounded fill shape; the included part and the overage are clipped out of it, so both outer ends stay
            // round and the seam between them is a clean gap.
            var fill = new RoundedRect(new Rect(0, top, Math.Max(end, thickness), thickness), radius);
            var split = Clamp(Split) * width;
            if (split >= end - 0.01)
            {
                context.DrawRectangle(FillBrush, null, fill);
            }
            else
            {
                var gap = Gap / 2;
                if (split - gap > 0)
                {
                    using (context.PushClip(new Rect(0, top, split - gap, thickness)))
                    {
                        context.DrawRectangle(FillBrush, null, fill);
                    }
                }
                using (context.PushClip(new Rect(split + gap, top, Math.Max(0, width - split - gap), thickness)))
                {
                    context.DrawRectangle(OverBrush, null, fill);
                }
            }
        }

        var marker = Marker;
        if (double.IsFinite(marker))
        {
            var x = Math.Round(Clamp(marker) * width);
            context.DrawRectangle(GapBrush, null, new Rect(x - 2, top, 4, thickness));
            context.DrawRectangle(MarkerBrush, null, new RoundedRect(new Rect(x - 1, 0, 2, height), 1));
        }
    }
}
