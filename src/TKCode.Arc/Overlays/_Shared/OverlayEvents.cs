using System.ComponentModel;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>An overlay (dialog, drawer, sheet, popover) is about to close. Set <see cref="CancelEventArgs.Cancel"/> to keep it open.</summary>
public sealed class OverlayClosingEventArgs : CancelEventArgs
{
    public OverlayClosingEventArgs(DismissReason reason, object? result)
    {
        Reason = reason;
        Result = result;
    }

    /// <summary>What asked to close (Escape, a press outside, the close button, a gesture, an action, code).</summary>
    public DismissReason Reason { get; }

    /// <summary>The result the overlay closes with (set by <c>arc:Dialog.Result</c> buttons or <c>Close(result)</c>).</summary>
    public object? Result { get; }
}

/// <summary>An overlay closed (its exit animation may still be playing).</summary>
public sealed class OverlayClosedEventArgs : EventArgs
{
    public OverlayClosedEventArgs(DismissReason reason, object? result)
    {
        Reason = reason;
        Result = result;
    }

    public DismissReason Reason { get; }

    public object? Result { get; }
}

/// <summary>Overlays that buttons inside them can close with a result (<c>arc:Dialog.Result</c>).</summary>
internal interface IOverlayResultTarget
{
    void CloseWithResult(object? result);
}
