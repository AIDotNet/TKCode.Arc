using System.ComponentModel;

namespace TKCode.Arc.Primitives;

/// <summary>How a <see cref="ModalPresence"/> panel enters and leaves (P11).</summary>
public enum ModalEntrance
{
    /// <summary>Dialog: rises 8px from scale 0.96 on the smooth spring with a fast fade; leaves to 4px / 0.98, fast.</summary>
    Rise,

    /// <summary>Drawer: slides in from <see cref="ModalPresence.Side"/> on the smooth spring; leaves with a standard tween (or a velocity-keeping spring after a fling).</summary>
    Slide,

    /// <summary>Opacity only.</summary>
    Fade,

    /// <summary>The owner moves the panel itself (<see cref="ModalPresence.CustomMotion"/>) and drives <see cref="ModalPresence.ScrimOpacity"/>.</summary>
    Custom
}

/// <summary>Edge a sliding modal panel is anchored to.</summary>
public enum ModalSide
{
    Left,
    Top,
    Right,
    Bottom
}

/// <summary>Lifecycle of a <see cref="ModalPresence"/>.</summary>
public enum ModalState
{
    /// <summary>Not mounted.</summary>
    Closed,

    /// <summary>Mounted and entering.</summary>
    Opening,

    /// <summary>Entered.</summary>
    Open,

    /// <summary>Leaving; the layer lets clicks through so the trigger can reopen it mid-exit.</summary>
    Closing
}

/// <summary>Why an overlay closes.</summary>
public enum DismissReason
{
    /// <summary>Code or a binding closed it.</summary>
    Programmatic,

    /// <summary>The built-in close button.</summary>
    CloseButton,

    /// <summary>The Escape key.</summary>
    Escape,

    /// <summary>A press outside the surface (scrim or page).</summary>
    PressOutside,

    /// <summary>A drag or fling (drawer, sheet).</summary>
    Gesture,

    /// <summary>An action inside the surface (a button with a result).</summary>
    Action
}

/// <summary>A dismissal request (Escape, scrim press, close button, gesture). Set <see cref="CancelEventArgs.Cancel"/> to keep the modal open.</summary>
public sealed class ModalDismissingEventArgs : CancelEventArgs
{
    public ModalDismissingEventArgs(DismissReason reason) => Reason = reason;

    public DismissReason Reason { get; }
}

/// <summary>Raised when a modal finished leaving and was removed from the overlay layer.</summary>
public sealed class ModalClosedEventArgs : EventArgs
{
    public ModalClosedEventArgs(DismissReason reason) => Reason = reason;

    public DismissReason Reason { get; }
}
