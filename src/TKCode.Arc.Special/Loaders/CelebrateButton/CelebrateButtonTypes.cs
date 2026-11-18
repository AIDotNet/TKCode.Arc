using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where a <see cref="CelebrateButton"/> is in its press, work and celebrate cycle.</summary>
public enum CelebrateButtonStatus
{
    /// <summary>At rest, showing the button's content. A press runs the action.</summary>
    Idle,

    /// <summary>The task returned by <see cref="CelebrateButton.Action"/> is running: a spinner and <see cref="CelebrateButton.PendingLabel"/> show and presses are ignored.</summary>
    Pending,

    /// <summary>The action succeeded: confetti bursts from the button's edges and the check with <see cref="CelebrateButton.SuccessLabel"/> shows. Presses are ignored.</summary>
    Success,

    /// <summary>The action failed: <see cref="CelebrateButton.ErrorLabel"/> shows, without a burst. A press runs the action again.</summary>
    Error
}

/// <summary>Arguments of <see cref="CelebrateButton.StatusChanged"/>.</summary>
public sealed class CelebrateButtonStatusChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments of a status change.</summary>
    public CelebrateButtonStatusChangedEventArgs(RoutedEvent routedEvent, CelebrateButtonStatus oldStatus,
                                                 CelebrateButtonStatus newStatus, Exception? error)
        : base(routedEvent)
    {
        OldStatus = oldStatus;
        NewStatus = newStatus;
        Error = error;
    }

    /// <summary>The status before the change.</summary>
    public CelebrateButtonStatus OldStatus { get; }

    /// <summary>The status after the change.</summary>
    public CelebrateButtonStatus NewStatus { get; }

    /// <summary>The exception thrown by the action when <see cref="NewStatus"/> is <see cref="CelebrateButtonStatus.Error"/> because the action failed; null otherwise.</summary>
    public Exception? Error { get; }
}
