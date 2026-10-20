namespace TKCode.Arc;

/// <summary>One line of a <see cref="SlopeChart"/>: an item's value in the first and the second period.</summary>
public sealed class SlopeItem
{
    /// <summary>Creates an empty item (for XAML).</summary>
    public SlopeItem()
    {
    }

    /// <summary>Creates an item.</summary>
    public SlopeItem(string key, string label, double start, double end)
    {
        Key = key;
        Label = label;
        Start = start;
        End = end;
    }

    /// <summary>Stable identity: an item keeps its line across datasets, so switching morphs it.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Name beside the start value, in the tooltip and in the summary.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Value in the first period.</summary>
    public double Start { get; set; }

    /// <summary>Value in the second period.</summary>
    public double End { get; set; }

    /// <summary>The original data item when read from <see cref="SlopeChart.ItemsSource"/>.</summary>
    public object? Source { get; internal set; }
}

/// <summary>Arguments of <see cref="SlopeChart.ActiveKeyChanged"/>.</summary>
public sealed class SlopeActiveKeyChangedEventArgs : EventArgs
{
    internal SlopeActiveKeyChangedEventArgs(string? key, SlopeItem? item)
    {
        Key = key;
        Item = item;
    }

    /// <summary>The item in focus, or null when the reading was cleared.</summary>
    public string? Key { get; }

    /// <summary>The item in focus, or null.</summary>
    public SlopeItem? Item { get; }
}
