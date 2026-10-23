using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <c>AiSidePanel.ChangeApplied</c>, <c>ChangeUndone</c> and <c>ChangeDiscarded</c>.</summary>
public class AiPanelChangeEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiPanelChangeEventArgs(RoutedEvent routedEvent, object source, AiPanelChange change, AiPanelPage page)
        : base(routedEvent, source)
    {
        Change = change;
        Page = page;
    }

    /// <summary>The change; <see cref="AiPanelChange.Before"/> is the block that was replaced.</summary>
    public AiPanelChange Change { get; }

    /// <summary>The page as it is after the change (or without it, after an undo).</summary>
    public AiPanelPage Page { get; }
}

/// <summary>Arguments of <c>AiSidePanel.SelectionAsked</c>.</summary>
public class AiPanelSelectionEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiPanelSelectionEventArgs(RoutedEvent routedEvent, object source, AiPanelSelection selection)
        : base(routedEvent, source)
    {
        Selection = selection;
    }

    /// <summary>The selection that was added as context.</summary>
    public AiPanelSelection Selection { get; }
}
