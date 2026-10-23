using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The current scope of a <see cref="SemanticZoom"/> changed (the camera moved onto another node).</summary>
public sealed class SemanticZoomScopeChangedEventArgs : RoutedEventArgs
{
    public SemanticZoomScopeChangedEventArgs(RoutedEvent routedEvent, SemanticZoomNode? oldScope, SemanticZoomNode? newScope,
                                             IReadOnlyList<SemanticZoomNode> path, bool isUserInitiated)
        : base(routedEvent)
    {
        OldScope = oldScope;
        NewScope = newScope;
        Path = path;
        IsUserInitiated = isUserInitiated;
    }

    /// <summary>The scope before the change.</summary>
    public SemanticZoomNode? OldScope { get; }

    /// <summary>The scope now.</summary>
    public SemanticZoomNode? NewScope { get; }

    /// <summary>The chain from the organization down to <see cref="NewScope"/> (what the breadcrumbs show).</summary>
    public IReadOnlyList<SemanticZoomNode> Path { get; }

    /// <summary>True when a person moved the camera (pointer, wheel, keys, buttons); false for code and the tour.</summary>
    public bool IsUserInitiated { get; }
}

/// <summary>A node was invoked (clicked, or Enter on the keyboard cursor) and the camera flies to it.</summary>
public sealed class SemanticZoomNodeEventArgs : RoutedEventArgs
{
    public SemanticZoomNodeEventArgs(RoutedEvent routedEvent, SemanticZoomNode node)
        : base(routedEvent)
    {
        Node = node;
    }

    /// <summary>The node that was invoked.</summary>
    public SemanticZoomNode Node { get; }
}

/// <summary>A checklist item was ticked or unticked from the map.</summary>
public sealed class SemanticZoomChecklistEventArgs : RoutedEventArgs
{
    public SemanticZoomChecklistEventArgs(RoutedEvent routedEvent, SemanticZoomTask task, SemanticZoomChecklistItem item, bool isDone)
        : base(routedEvent)
    {
        Task = task;
        Item = item;
        IsDone = isDone;
    }

    /// <summary>The task the item belongs to.</summary>
    public SemanticZoomTask Task { get; }

    /// <summary>The item that changed.</summary>
    public SemanticZoomChecklistItem Item { get; }

    /// <summary>The item's new state.</summary>
    public bool IsDone { get; }
}

/// <summary>The tour stopped.</summary>
public sealed class SemanticZoomTourStoppedEventArgs : RoutedEventArgs
{
    public SemanticZoomTourStoppedEventArgs(RoutedEvent routedEvent, bool completed)
        : base(routedEvent)
    {
        Completed = completed;
    }

    /// <summary>True when the tour reached its last stop; false when it was interrupted.</summary>
    public bool Completed { get; }
}
