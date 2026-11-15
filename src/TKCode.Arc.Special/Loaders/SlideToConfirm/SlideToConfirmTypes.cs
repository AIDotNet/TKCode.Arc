using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where a <see cref="SlideToConfirm"/> is in its slide and confirm cycle.</summary>
public enum SlideToConfirmState
{
    /// <summary>At rest, waiting for a slide (also while the thumb springs back after an early release).</summary>
    Idle,

    /// <summary>The thumb is held by the pointer or glides while Space or Enter is held.</summary>
    Dragging,

    /// <summary>The slide completed and the task returned by <see cref="SlideToConfirm.ConfirmAction"/> is running.</summary>
    Pending,

    /// <summary>The action succeeded: the check and <see cref="SlideToConfirm.ConfirmedLabel"/> show.</summary>
    Done,

    /// <summary>The action failed: <see cref="SlideToConfirm.ErrorLabel"/> replaces the label until the next slide.</summary>
    Error
}

/// <summary>Arguments of <see cref="SlideToConfirm.StateChanged"/>.</summary>
public sealed class SlideToConfirmStateChangedEventArgs : RoutedEventArgs
{
    public SlideToConfirmStateChangedEventArgs(RoutedEvent routedEvent, SlideToConfirmState oldState,
                                               SlideToConfirmState newState, Exception? error)
        : base(routedEvent)
    {
        OldState = oldState;
        NewState = newState;
        Error = error;
    }

    /// <summary>The state before the change.</summary>
    public SlideToConfirmState OldState { get; }

    /// <summary>The state after the change.</summary>
    public SlideToConfirmState NewState { get; }

    /// <summary>The exception thrown by the action when <see cref="NewState"/> is <see cref="SlideToConfirmState.Error"/>; null otherwise.</summary>
    public Exception? Error { get; }
}
