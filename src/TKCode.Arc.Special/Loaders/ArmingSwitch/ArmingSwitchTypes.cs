using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where an <see cref="ArmingSwitch"/> is in its lift, arm, hold and run ritual.</summary>
public enum ArmingSwitchStatus
{
    /// <summary>The safety cover is down and the switch is safe.</summary>
    Safe,

    /// <summary>The cover is lifted; the switch is still safe.</summary>
    Open,

    /// <summary>The switch is thrown up and the lamp is lit: holding the lamp runs the action.</summary>
    Armed,

    /// <summary>The hold completed and the task from <see cref="ArmingSwitch.ConfirmAction"/> is running.</summary>
    Running,

    /// <summary>The action succeeded. The lamp stays green until the cover is lifted again.</summary>
    Done,

    /// <summary>The action failed. The switch stays armed, so holding the lamp again retries.</summary>
    Failed
}

/// <summary>Arguments of <see cref="ArmingSwitch.StatusChanged"/>.</summary>
public sealed class ArmingSwitchStatusChangedEventArgs : RoutedEventArgs
{
    public ArmingSwitchStatusChangedEventArgs(RoutedEvent routedEvent, ArmingSwitchStatus oldStatus,
                                              ArmingSwitchStatus newStatus, Exception? error)
        : base(routedEvent)
    {
        OldStatus = oldStatus;
        NewStatus = newStatus;
        Error = error;
    }

    /// <summary>The status before the change.</summary>
    public ArmingSwitchStatus OldStatus { get; }

    /// <summary>The status after the change.</summary>
    public ArmingSwitchStatus NewStatus { get; }

    /// <summary>The exception thrown by the action when <see cref="NewStatus"/> is <see cref="ArmingSwitchStatus.Failed"/> because of one; null otherwise.</summary>
    public Exception? Error { get; }
}
