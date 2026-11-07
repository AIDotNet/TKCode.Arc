namespace TKCode.Arc;

/// <summary>Where a <see cref="StretchRefresh"/> is in its pull and refresh cycle.</summary>
public enum StretchRefreshState
{
    /// <summary>At rest: the sheet covers the well.</summary>
    Idle,

    /// <summary>Being pulled, not yet far enough to refresh on release.</summary>
    Pulling,

    /// <summary>Pulled past <see cref="StretchRefresh.Threshold"/>: letting go starts a refresh.</summary>
    Armed,

    /// <summary>The refresh work is running; the line runs back and forth.</summary>
    Refreshing,

    /// <summary>The work finished; the result shows until the sheet has settled again.</summary>
    Done,

    /// <summary>The work failed; the error label shows until the sheet has settled again.</summary>
    Failed
}

/// <summary>What started a refresh of a <see cref="StretchRefresh"/>.</summary>
public enum StretchRefreshTrigger
{
    /// <summary>A mouse, pen or touch pull released past the threshold.</summary>
    Pull,

    /// <summary>A wheel or trackpad pull at the top of the feed that went idle past the threshold.</summary>
    Wheel,

    /// <summary>The refresh button (pointer, Enter or Space).</summary>
    Button,

    /// <summary>A call to <see cref="StretchRefresh.RefreshAsync"/>.</summary>
    Code
}

/// <summary>Data of <see cref="StretchRefresh.RefreshStarted"/>.</summary>
public sealed class StretchRefreshStartedEventArgs : EventArgs
{
    /// <summary>Creates the event data.</summary>
    public StretchRefreshStartedEventArgs(StretchRefreshTrigger trigger)
    {
        Trigger = trigger;
    }

    /// <summary>What started the refresh.</summary>
    public StretchRefreshTrigger Trigger { get; }
}

/// <summary>Data of <see cref="StretchRefresh.RefreshCompleted"/>.</summary>
public sealed class StretchRefreshCompletedEventArgs : EventArgs
{
    /// <summary>Creates the event data.</summary>
    public StretchRefreshCompletedEventArgs(StretchRefreshTrigger trigger, string resultText, Exception? error)
    {
        Trigger = trigger;
        ResultText = resultText;
        Error = error;
    }

    /// <summary>What started the refresh.</summary>
    public StretchRefreshTrigger Trigger { get; }

    /// <summary>The text the indicator shows and the status region announces: the handler's result, or the done or error label.</summary>
    public string ResultText { get; }

    /// <summary>The exception the refresh handler threw, or null when it succeeded.</summary>
    public Exception? Error { get; }

    /// <summary>True when the refresh handler threw.</summary>
    public bool IsFailed => Error is not null;
}

/// <summary>
/// Every word a <see cref="StretchRefresh"/> shows or announces, in English by default. Replace the instance (or set its
/// properties before the control is shown) to translate or reword.
/// </summary>
public sealed class StretchRefreshLabels
{
    /// <summary>The hint under the line while pulling: "Pull to refresh".</summary>
    public string PullHint { get; set; } = "Pull to refresh";

    /// <summary>The hint once the pull is past the threshold: "Release to refresh".</summary>
    public string ReleaseHint { get; set; } = "Release to refresh";

    /// <summary>Shown and announced while the refresh runs: "Refreshing".</summary>
    public string Refreshing { get; set; } = "Refreshing";

    /// <summary>The result when the refresh handler returns no text: "Up to date".</summary>
    public string Done { get; set; } = "Up to date";

    /// <summary>The result when the refresh handler throws: "Couldn’t refresh".</summary>
    public string Error { get; set; } = "Couldn’t refresh";

    /// <summary>The accessible name of the refresh button: "Refresh".</summary>
    public string RefreshButton { get; set; } = "Refresh";
}
