namespace TKCode.Arc;

/// <summary>One reading of a <see cref="RealtimeStream"/>.</summary>
public sealed class RealtimeStreamPoint
{
    /// <summary>Creates an empty reading (for XAML).</summary>
    public RealtimeStreamPoint()
    {
    }

    /// <summary>Creates a reading.</summary>
    public RealtimeStreamPoint(DateTimeOffset time, double value, string? anomaly = null)
    {
        Time = time;
        Value = value;
        Anomaly = anomaly;
    }

    /// <summary>When the reading was taken. Readings arrive in ascending time.</summary>
    public DateTimeOffset Time { get; set; }

    /// <summary>The measured value.</summary>
    public double Value { get; set; }

    /// <summary>Set when your system flagged this reading: the text of its marker. The chart does not detect anomalies itself.</summary>
    public string? Anomaly { get; set; }
}

/// <summary>What made a <see cref="RealtimeStream"/> hold still or carry on.</summary>
public enum RealtimeStreamHoldReason
{
    /// <summary><see cref="RealtimeStream.IsPaused"/> changed: the status button, Space or Enter, or your own code.</summary>
    Request,

    /// <summary>The pointer entered or left the plot, or a touch started or ended.</summary>
    Pointer,

    /// <summary>The keyboard moved into or out of the plot, or Escape was pressed.</summary>
    Keyboard
}

/// <summary>Arguments of <see cref="RealtimeStream.PausedChanged"/>.</summary>
public sealed class RealtimeStreamPausedChangedEventArgs : EventArgs
{
    internal RealtimeStreamPausedChangedEventArgs(bool isPaused, RealtimeStreamHoldReason reason)
    {
        IsPaused = isPaused;
        Reason = reason;
    }

    /// <summary>True when the view now holds still, for whatever reason; false when it follows the feed again.</summary>
    public bool IsPaused { get; }

    /// <summary>What caused this change.</summary>
    public RealtimeStreamHoldReason Reason { get; }
}
