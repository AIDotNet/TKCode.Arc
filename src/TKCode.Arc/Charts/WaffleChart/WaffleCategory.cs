using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>One category of a <see cref="WaffleChart"/>: a share of the whole drawn as countable cells.</summary>
public sealed class WaffleCategory
{
    /// <summary>Creates an empty category (for XAML).</summary>
    public WaffleCategory()
    {
    }

    /// <summary>Creates a category.</summary>
    public WaffleCategory(string key, string label, double value, Color? color = null)
    {
        Key = key;
        Label = label;
        Value = value;
        Color = color;
    }

    /// <summary>Stable identity: cells keep following their category when the data changes, so a new dataset flows instead of repainting.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Name in the legend, the tooltip and the accessible summary.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Raw amount. Shares are computed from the total; negatives count as zero.</summary>
    public double Value { get; set; }

    /// <summary>Optional fixed color; defaults to Series1 for the accent category, Series2–4 for the next three, then neutral steps.</summary>
    public Color? Color { get; set; }

    /// <summary>The original data item when the category was read from <see cref="WaffleChart.ItemsSource"/>.</summary>
    public object? Source { get; internal set; }
}

/// <summary>Arguments of <see cref="WaffleChart.ActiveKeyChanged"/>.</summary>
public sealed class WaffleActiveKeyChangedEventArgs : EventArgs
{
    internal WaffleActiveKeyChangedEventArgs(string? key, WaffleCategory? category)
    {
        Key = key;
        Category = category;
    }

    /// <summary>The pinned category key, or null when released.</summary>
    public string? Key { get; }

    /// <summary>The pinned category, or null when released.</summary>
    public WaffleCategory? Category { get; }
}
