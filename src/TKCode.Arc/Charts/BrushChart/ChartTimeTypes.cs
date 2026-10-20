using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>An event marked on a time chart (BrushChart): a hairline with a dot in the second series color and a label when there is room.</summary>
/// <param name="Date">When it happened.</param>
/// <param name="Label">Short label printed beside the marker, such as "v2 launch".</param>
/// <param name="Description">Longer note for the tooltip and screen readers.</param>
public sealed record ChartAnnotation(DateTimeOffset Date, string Label, string? Description = null);

/// <summary>A window of time, such as the BrushChart's visible range.</summary>
/// <param name="Start">Inclusive start.</param>
/// <param name="End">Inclusive end.</param>
public readonly record struct ChartTimeRange(DateTimeOffset Start, DateTimeOffset End)
{
    /// <summary>The length of the window.</summary>
    public TimeSpan Span => End - Start;
}

/// <summary>Raised while a BrushChart window is dragged, resized, stepped or reset.</summary>
public sealed class ChartTimeRangeChangedEventArgs : RoutedEventArgs
{
    public ChartTimeRangeChangedEventArgs(RoutedEvent routedEvent, ChartTimeRange range) : base(routedEvent) => Range = range;

    /// <summary>The new window.</summary>
    public ChartTimeRange Range { get; }
}
