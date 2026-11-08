using Avalonia.Collections;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>One competitor of a <see cref="RaceBarChart"/>: a row that keeps its identity from period to period.</summary>
public sealed class RaceBarItem
{
    /// <summary>Creates an empty item (for XAML).</summary>
    public RaceBarItem()
    {
    }

    /// <summary>Creates an item.</summary>
    public RaceBarItem(string id, string label, string? group = null)
    {
        Id = id;
        Label = label;
        Group = group;
    }

    /// <summary>Stable identity, matched against <see cref="RaceBarValue.Id"/> in every frame.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The name drawn beside or inside the bar.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Id of the <see cref="RaceBarGroup"/> whose hue the bar takes, or null.</summary>
    public string? Group { get; set; }
}

/// <summary>A category of a <see cref="RaceBarChart"/>: the first four take series hues and appear in the legend.</summary>
public sealed class RaceBarGroup
{
    /// <summary>Creates an empty group (for XAML).</summary>
    public RaceBarGroup()
    {
    }

    /// <summary>Creates a group.</summary>
    public RaceBarGroup(string id, string label)
    {
        Id = id;
        Label = label;
    }

    /// <summary>Stable identity, matched against <see cref="RaceBarItem.Group"/>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The name shown in the legend.</summary>
    public string Label { get; set; } = string.Empty;
}

/// <summary>The value of one item in one <see cref="RaceBarFrame"/>.</summary>
public sealed class RaceBarValue
{
    /// <summary>Creates an empty value (for XAML).</summary>
    public RaceBarValue()
    {
    }

    /// <summary>Creates a value.</summary>
    public RaceBarValue(string id, double value)
    {
        Id = id;
        Value = value;
    }

    /// <summary>Id of the <see cref="RaceBarItem"/> this value belongs to.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The value in this period. Negative values draw an empty bar; values that are not finite count as zero.</summary>
    public double Value { get; set; }
}

/// <summary>One period of a <see cref="RaceBarChart"/>: its name and the value of each item. A missing id counts as zero.</summary>
public sealed class RaceBarFrame
{
    /// <summary>Creates an empty frame (for XAML).</summary>
    public RaceBarFrame()
    {
    }

    /// <summary>Creates a frame from values by item id.</summary>
    public RaceBarFrame(string period, IEnumerable<KeyValuePair<string, double>> values)
    {
        Period = period;
        foreach (var (id, value) in values)
        {
            Values.Add(new RaceBarValue(id, value));
        }
    }

    /// <summary>The period's name: the large label, the slider's value text and the announcement.</summary>
    public string Period { get; set; } = string.Empty;

    /// <summary>The values of this period.</summary>
    [Content]
    public AvaloniaList<RaceBarValue> Values { get; } = new();
}

/// <summary>Arguments of <see cref="RaceBarChart.FrameChanged"/>.</summary>
public sealed class RaceBarFrameChangedEventArgs : EventArgs
{
    internal RaceBarFrameChangedEventArgs(int previousIndex, int index, string period, RaceBarItem? leader)
    {
        PreviousIndex = previousIndex;
        Index = index;
        Period = period;
        Leader = leader;
    }

    /// <summary>The period shown before this change.</summary>
    public int PreviousIndex { get; }

    /// <summary>The period now shown.</summary>
    public int Index { get; }

    /// <summary>The name of the period now shown.</summary>
    public string Period { get; }

    /// <summary>The item ranked first in that period, or null without items.</summary>
    public RaceBarItem? Leader { get; }
}

/// <summary>Arguments of <see cref="RaceBarChart.PlayingChanged"/>.</summary>
public sealed class RaceBarPlayingChangedEventArgs : EventArgs
{
    internal RaceBarPlayingChangedEventArgs(bool isPlaying, int index)
    {
        IsPlaying = isPlaying;
        Index = index;
    }

    /// <summary>Whether the timeline is now playing.</summary>
    public bool IsPlaying { get; }

    /// <summary>The period shown when playback started or stopped.</summary>
    public int Index { get; }
}
