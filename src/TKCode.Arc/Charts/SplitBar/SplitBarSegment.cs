using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One part of a <see cref="SplitBar"/>: a label, a value and an optional color. Changing <see cref="Value"/> springs the
/// bar to the new shares; the bar listens to its segments while it is on screen.
/// </summary>
public class SplitBarSegment : AvaloniaObject
{
    /// <summary>The name shown in the legend and read by screen readers.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<SplitBarSegment, string?>(nameof(Label));

    /// <summary>The amount; negative values count as zero.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<SplitBarSegment, double>(nameof(Value));

    /// <summary>The segment's color; null takes the next chart series color.</summary>
    public static readonly StyledProperty<IBrush?> BrushProperty =
        AvaloniaProperty.Register<SplitBarSegment, IBrush?>(nameof(Brush));

    /// <summary>Creates an empty segment (set <see cref="Label"/> and <see cref="Value"/>).</summary>
    public SplitBarSegment()
    {
    }

    /// <summary>Creates a segment with a label and a value.</summary>
    public SplitBarSegment(string label, double value, IBrush? brush = null)
    {
        Label = label;
        Value = value;
        Brush = brush;
    }

    /// <summary>The name shown in the legend and read by screen readers.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The amount; negative values count as zero.</summary>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>The segment's color; null takes the next chart series color.</summary>
    public IBrush? Brush { get => GetValue(BrushProperty); set => SetValue(BrushProperty, value); }
}

/// <summary>Arguments of <see cref="SplitBar.SegmentInvoked"/>.</summary>
public sealed class SplitBarSegmentEventArgs : RoutedEventArgs
{
    internal SplitBarSegmentEventArgs(RoutedEvent routedEvent, object source, SplitBarSegment segment, int index, double share)
        : base(routedEvent, source)
    {
        Segment = segment;
        Index = index;
        Share = share;
    }

    /// <summary>The segment that was clicked or chosen with Enter.</summary>
    public SplitBarSegment Segment { get; }

    /// <summary>Its position in the bar, from 0.</summary>
    public int Index { get; }

    /// <summary>Its share of the bar's total, from 0 to 1.</summary>
    public double Share { get; }
}
