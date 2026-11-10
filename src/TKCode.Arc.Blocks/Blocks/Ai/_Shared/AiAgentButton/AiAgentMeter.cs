using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>A thin progress line of the agent blocks (a source's reading, a task's progress): a pill track filled from the left by <see cref="Value"/>.</summary>
internal sealed class AiAgentMeter : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<AiAgentMeter, double>(nameof(Value));

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<AiAgentMeter, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> TrackProperty =
        AvaloniaProperty.Register<AiAgentMeter, IBrush?>(nameof(Track));

    static AiAgentMeter()
    {
        AffectsRender<AiAgentMeter>(ValueProperty, FillProperty, TrackProperty);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<AiAgentMeter>(AccessibilityView.Raw);
    }

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    public IBrush? Track { get => GetValue(TrackProperty); set => SetValue(TrackProperty, value); }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        var radius = size.Height / 2;
        if (Track is { } track)
        {
            context.DrawRectangle(track, null, new Rect(size), radius, radius);
        }
        var value = Math.Clamp(Value, 0, 1);
        if (Fill is { } fill && value > 0)
        {
            context.DrawRectangle(fill, null, new Rect(0, 0, size.Width * value, size.Height), radius, radius);
        }
    }
}
