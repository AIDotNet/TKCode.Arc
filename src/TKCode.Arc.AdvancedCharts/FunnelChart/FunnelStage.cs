using Avalonia.Collections;

namespace TKCode.Arc;

/// <summary>Which way a <see cref="FunnelChart"/> runs.</summary>
public enum FunnelOrientation
{
    /// <summary>Left to right, and top to bottom when the chart is narrower than 420 pixels.</summary>
    Auto,

    /// <summary>Always left to right, with the stage labels above the band.</summary>
    Horizontal,

    /// <summary>Always top to bottom, with the stage labels to the right of the band.</summary>
    Vertical
}

/// <summary>One step of a <see cref="FunnelChart"/>.</summary>
public sealed class FunnelStage
{
    /// <summary>Creates an empty stage (for XAML).</summary>
    public FunnelStage()
    {
    }

    /// <summary>Creates a stage with one count.</summary>
    public FunnelStage(string key, string label, double value)
    {
        Key = key;
        Label = label;
        Value = value;
    }

    /// <summary>Creates a stage with one count per segment, in the order of <see cref="FunnelChart.Segments"/>.</summary>
    public FunnelStage(string key, string label, params double[] values)
    {
        Key = key;
        Label = label;
        Values = new AvaloniaList<double>(values);
    }

    /// <summary>Stable identity: a stage that survives a data change morphs into its new size.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Stage name shown above the band; wraps to two lines.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The count of a funnel without segments. Ignored when <see cref="Values"/> is set.</summary>
    public double Value { get; set; }

    /// <summary>One count per segment, in the order of <see cref="FunnelChart.Segments"/>; in XAML a comma separated list.</summary>
    public AvaloniaList<double>? Values { get; set; }
}

/// <summary>One part of the audience that a <see cref="FunnelChart"/> can split or isolate.</summary>
public sealed class FunnelSegment
{
    /// <summary>Creates an empty segment (for XAML).</summary>
    public FunnelSegment()
    {
    }

    /// <summary>Creates a segment.</summary>
    public FunnelSegment(string key, string label)
    {
        Key = key;
        Label = label;
    }

    /// <summary>Identity; set <see cref="FunnelChart.View"/> to it to isolate this segment.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Segment name shown in the tooltip of the split view.</summary>
    public string Label { get; set; } = string.Empty;
}

/// <summary>Arguments of <see cref="FunnelChart.ActiveIndexChanged"/>.</summary>
public sealed class FunnelActiveIndexChangedEventArgs : EventArgs
{
    internal FunnelActiveIndexChangedEventArgs(int? index, FunnelStage? stage, double count, double fromPrevious, double fromFirst)
    {
        Index = index;
        Stage = stage;
        Count = count;
        RetentionFromPrevious = fromPrevious;
        RetentionFromFirst = fromFirst;
    }

    /// <summary>The focused step, or null when the reading was cleared.</summary>
    public int? Index { get; }

    /// <summary>The focused stage, or null.</summary>
    public FunnelStage? Stage { get; }

    /// <summary>The step's count in the current view (0 when cleared).</summary>
    public double Count { get; }

    /// <summary>Share of the previous step that reached this one, from 0 to 1 (the first step is 1).</summary>
    public double RetentionFromPrevious { get; }

    /// <summary>Share of the first step that reached this one, from 0 to 1.</summary>
    public double RetentionFromFirst { get; }
}
