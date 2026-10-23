using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where a <see cref="ReleaseReadiness"/> gate stands.</summary>
public enum ReleaseReadinessState
{
    /// <summary>Some required checks are still open; the release button is disabled.</summary>
    Collecting,

    /// <summary>Every required check is done; the release can start.</summary>
    Ready,

    /// <summary>The release action is running; the button shows a spinner and the checks are locked.</summary>
    Releasing,

    /// <summary>The release started; the checks stay locked until a reset.</summary>
    Released,

    /// <summary>The last attempt failed; the error shows under the footer and the release can be tried again.</summary>
    Failed,
}

/// <summary>What a release attempt was started with: passed to the release action, the command and the events.</summary>
public sealed class ReleaseRequest
{
    /// <summary>Creates a request.</summary>
    /// <param name="notify">Whether the person asked to notify the channel.</param>
    /// <param name="checks">Every check at the time of the request, in order.</param>
    /// <param name="attempt">The attempt number since the last reset (1 for the first).</param>
    public ReleaseRequest(bool notify, IReadOnlyList<ReleaseCheck> checks, int attempt)
    {
        Notify = notify;
        Checks = checks;
        Attempt = attempt;
        SkippedChecks = checks.Where(c => !c.IsRequired && !c.IsDone).ToArray();
    }

    /// <summary>Whether the notify switch was on.</summary>
    public bool Notify { get; }

    /// <summary>Every check at the time of the request, in order.</summary>
    public IReadOnlyList<ReleaseCheck> Checks { get; }

    /// <summary>The optional checks that were not done.</summary>
    public IReadOnlyList<ReleaseCheck> SkippedChecks { get; }

    /// <summary>The attempt number since the last reset: 1 for the first, 2 for the first retry and so on.</summary>
    public int Attempt { get; }
}

/// <summary>Arguments of <c>ReleaseReadiness.CheckChanged</c>: a check was ticked or cleared.</summary>
public class ReleaseCheckChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The block.</param>
    /// <param name="check">The check that changed.</param>
    /// <param name="oldValue">Its previous <see cref="ReleaseCheck.IsDone"/>.</param>
    /// <param name="newValue">Its new <see cref="ReleaseCheck.IsDone"/>.</param>
    /// <param name="doneCount">Required checks done after the change.</param>
    /// <param name="totalCount">Required checks in all.</param>
    public ReleaseCheckChangedEventArgs(RoutedEvent routedEvent, object source, ReleaseCheck check, bool oldValue, bool newValue,
        int doneCount, int totalCount)
        : base(routedEvent, source)
    {
        Check = check;
        OldValue = oldValue;
        NewValue = newValue;
        DoneCount = doneCount;
        TotalCount = totalCount;
    }

    /// <summary>The check that changed.</summary>
    public ReleaseCheck Check { get; }

    /// <summary>Its previous value.</summary>
    public bool OldValue { get; }

    /// <summary>Its new value.</summary>
    public bool NewValue { get; }

    /// <summary>Required checks done after the change.</summary>
    public int DoneCount { get; }

    /// <summary>Required checks in all.</summary>
    public int TotalCount { get; }
}

/// <summary>Arguments of <c>ReleaseReadiness.ReadyChanged</c>: every required check became done, or one was cleared again.</summary>
public class ReleaseReadyChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The block.</param>
    /// <param name="isReady">Whether every required check is done now.</param>
    public ReleaseReadyChangedEventArgs(RoutedEvent routedEvent, object source, bool isReady)
        : base(routedEvent, source)
    {
        IsReady = isReady;
    }

    /// <summary>Whether every required check is done now.</summary>
    public bool IsReady { get; }
}

/// <summary>Arguments of <c>ReleaseReadiness.StateChanged</c>: the gate moved to another <see cref="ReleaseReadinessState"/>.</summary>
public class ReleaseStateChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The block.</param>
    /// <param name="oldState">The state before.</param>
    /// <param name="newState">The state now.</param>
    public ReleaseStateChangedEventArgs(RoutedEvent routedEvent, object source, ReleaseReadinessState oldState, ReleaseReadinessState newState)
        : base(routedEvent, source)
    {
        OldState = oldState;
        NewState = newState;
    }

    /// <summary>The state before.</summary>
    public ReleaseReadinessState OldState { get; }

    /// <summary>The state now.</summary>
    public ReleaseReadinessState NewState { get; }
}

/// <summary>Arguments of <c>ReleaseReadiness.ReleaseStarting</c>: a release is about to run; set <see cref="Cancel"/> to stop it.</summary>
public class ReleaseStartingEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The block.</param>
    /// <param name="request">What the release would start with.</param>
    public ReleaseStartingEventArgs(RoutedEvent routedEvent, object source, ReleaseRequest request)
        : base(routedEvent, source)
    {
        Request = request;
    }

    /// <summary>What the release would start with.</summary>
    public ReleaseRequest Request { get; }

    /// <summary>Set to true to keep the release from starting; the gate stays ready.</summary>
    public bool Cancel { get; set; }
}

/// <summary>Arguments of <c>ReleaseReadiness.Released</c>: the release action finished without an error.</summary>
public class ReleasedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The block.</param>
    /// <param name="request">What the release ran with.</param>
    public ReleasedEventArgs(RoutedEvent routedEvent, object source, ReleaseRequest request)
        : base(routedEvent, source)
    {
        Request = request;
    }

    /// <summary>What the release ran with.</summary>
    public ReleaseRequest Request { get; }

    /// <summary>Whether the notify switch was on.</summary>
    public bool Notify => Request.Notify;
}

/// <summary>Arguments of <c>ReleaseReadiness.ReleaseFailed</c>: the release action or command threw.</summary>
public class ReleaseFailedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The block.</param>
    /// <param name="request">What the release ran with.</param>
    /// <param name="exception">What it threw.</param>
    /// <param name="errorText">The text shown under the footer.</param>
    public ReleaseFailedEventArgs(RoutedEvent routedEvent, object source, ReleaseRequest request, Exception exception, string errorText)
        : base(routedEvent, source)
    {
        Request = request;
        Exception = exception;
        ErrorText = errorText;
    }

    /// <summary>What the release ran with.</summary>
    public ReleaseRequest Request { get; }

    /// <summary>What the action or command threw.</summary>
    public Exception Exception { get; }

    /// <summary>The text shown under the footer.</summary>
    public string ErrorText { get; }
}

/// <summary>Arguments of <c>ReleaseReadiness.ResetPerformed</c>: the checks went back to their first values.</summary>
public class ReleaseResetEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The block.</param>
    /// <param name="previousState">The state before the reset.</param>
    public ReleaseResetEventArgs(RoutedEvent routedEvent, object source, ReleaseReadinessState previousState)
        : base(routedEvent, source)
    {
        PreviousState = previousState;
    }

    /// <summary>The state before the reset.</summary>
    public ReleaseReadinessState PreviousState { get; }
}
