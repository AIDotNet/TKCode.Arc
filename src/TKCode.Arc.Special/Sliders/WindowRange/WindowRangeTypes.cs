using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>A label under the chart of a <see cref="WindowRange"/>, such as the first day of a month.</summary>
public sealed class WindowRangeTick
{
    /// <summary>Creates an empty tick; set <see cref="Value"/> and <see cref="Label"/>.</summary>
    public WindowRangeTick()
    {
    }

    /// <summary>Creates a tick at <paramref name="value"/> (domain units) with <paramref name="label"/>.</summary>
    public WindowRangeTick(double value, string label)
    {
        Value = value;
        Label = label;
    }

    /// <summary>Where the label sits, in the same domain units as the window.</summary>
    public double Value { get; set; }

    /// <summary>The text under the chart.</summary>
    public string Label { get; set; } = "";
}

/// <summary>Arguments of <see cref="WindowRange.ValueChanged"/>.</summary>
public sealed class WindowRangeValueChangedEventArgs : RoutedEventArgs
{
    internal WindowRangeValueChangedEventArgs(RoutedEvent routedEvent, double oldStart, double oldEnd, double newStart,
        double newEnd) : base(routedEvent)
    {
        OldStart = oldStart;
        OldEnd = oldEnd;
        NewStart = newStart;
        NewEnd = newEnd;
    }

    /// <summary>The start of the window before the change.</summary>
    public double OldStart { get; }

    /// <summary>The end of the window before the change.</summary>
    public double OldEnd { get; }

    /// <summary>The start of the window now.</summary>
    public double NewStart { get; }

    /// <summary>The end of the window now.</summary>
    public double NewEnd { get; }
}

/// <summary>Arguments of <see cref="WindowRange.ValueCommitted"/>.</summary>
public sealed class WindowRangeValueCommittedEventArgs : RoutedEventArgs
{
    internal WindowRangeValueCommittedEventArgs(RoutedEvent routedEvent, double start, double end) : base(routedEvent)
    {
        Start = start;
        End = end;
    }

    /// <summary>The start of the window it settled on.</summary>
    public double Start { get; }

    /// <summary>The end of the window it settled on.</summary>
    public double End { get; }
}

/// <summary>The part of a <see cref="WindowRange"/> a pointer or a key acts on.</summary>
internal enum WindowRangePart
{
    None,
    Start,
    Window,
    End
}

/// <summary>What the window is doing (Arc's <c>data-mode</c>).</summary>
internal enum WindowRangeMode
{
    Idle,
    Pan,
    Glide,
    Start,
    End,
    Coast
}
