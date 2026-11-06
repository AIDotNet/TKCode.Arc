using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>A side of a <see cref="DockLayout"/>: where a tool pane prefers to sit, or the edge of an auto-hide strip.</summary>
public enum DockSide
{
    /// <summary>The left edge.</summary>
    Left,

    /// <summary>The right edge.</summary>
    Right,

    /// <summary>The top edge.</summary>
    Top,

    /// <summary>The bottom edge.</summary>
    Bottom
}

/// <summary>Where a pane lands relative to a <see cref="DockTarget"/>: as a tab (<see cref="Center"/>) or in a new group on one side.</summary>
public enum DockPosition
{
    /// <summary>Join the target group as a tab.</summary>
    Center,

    /// <summary>A new group to the left of the target.</summary>
    Left,

    /// <summary>A new group to the right of the target.</summary>
    Right,

    /// <summary>A new group above the target.</summary>
    Top,

    /// <summary>A new group below the target.</summary>
    Bottom
}

/// <summary>What a <see cref="DockTarget"/> splits or joins.</summary>
public enum DockScope
{
    /// <summary>The target group: join it (<see cref="DockPosition.Center"/>) or split it.</summary>
    Group,

    /// <summary>The whole layout: dock to one of its edges.</summary>
    Workspace,

    /// <summary>The document area as a whole: split it from outside, or join its most recent group (<see cref="DockPosition.Center"/>).</summary>
    DocumentArea
}

/// <summary>Where a pane is: in a group of the main layout, auto-hidden on an edge strip, or in a floating window.</summary>
public enum DockPaneState
{
    /// <summary>In a group of the main layout (also the state of a pane that is not in a layout).</summary>
    Docked,

    /// <summary>On one of the four auto-hide strips (<see cref="DockWorkspace.AutoHidden"/>).</summary>
    AutoHidden,

    /// <summary>In a floating window or card (<see cref="DockWorkspace.Floats"/>).</summary>
    Floating
}

/// <summary>What changed in a <see cref="DockLayout"/> (<see cref="DockLayoutChangedEventArgs.Kind"/>).</summary>
public enum DockLayoutChangeKind
{
    /// <summary>A pane moved to another group, a new group or an edge, or changed its place among the tabs.</summary>
    Move,

    /// <summary>A pane was closed.</summary>
    Close,

    /// <summary>A pane was added.</summary>
    Open,

    /// <summary>A pane moved to a floating window.</summary>
    Float,

    /// <summary>A floating or auto-hidden pane went back to its docked place.</summary>
    Dock,

    /// <summary>A pane moved to an auto-hide strip.</summary>
    AutoHide,

    /// <summary>An auto-hidden pane was pinned back into a group.</summary>
    Pin,

    /// <summary>A divider was dragged or moved with the keyboard.</summary>
    Resize,

    /// <summary>Another pane became the active one.</summary>
    Activate,

    /// <summary>A saved layout was loaded.</summary>
    Load
}

/// <summary>
/// Where <see cref="DockLayout.Move"/> or <see cref="DockLayout.Open"/> puts a pane: a <see cref="Group"/> (or the whole
/// layout, or the document area, by <see cref="Scope"/>), a <see cref="Position"/> on it, and for a tab the
/// <see cref="Index"/> among the group's tabs. Use the factories <see cref="Into"/>, <see cref="Split"/>,
/// <see cref="Edge"/> and <see cref="DocumentArea"/>.
/// </summary>
/// <param name="Group">The target group (<see cref="DockScope.Group"/>); null for the other scopes.</param>
/// <param name="Position">Join as a tab (<see cref="DockPosition.Center"/>) or make a new group on one side.</param>
/// <param name="Scope">What is split or joined.</param>
/// <param name="Index">Where the tab goes among the group's tabs (<see cref="DockPosition.Center"/> only); null puts it last.</param>
public readonly record struct DockTarget(DockGroup? Group, DockPosition Position, DockScope Scope, int? Index = null)
{
    /// <summary>Join <paramref name="group"/> as a tab, at <paramref name="index"/> (last when null).</summary>
    public static DockTarget Into(DockGroup group, int? index = null) => new(group, DockPosition.Center, DockScope.Group, index);

    /// <summary>A new group beside <paramref name="group"/>, taking half of its room.</summary>
    public static DockTarget Split(DockGroup group, DockPosition position) => new(group, position, DockScope.Group);

    /// <summary>A new group along one edge of the whole layout, taking a quarter of it.</summary>
    public static DockTarget Edge(DockPosition position) => new(null, position, DockScope.Workspace);

    /// <summary>A new group beside the document area as a whole, or (<see cref="DockPosition.Center"/>) its most recent group.</summary>
    public static DockTarget DocumentArea(DockPosition position) => new(null, position, DockScope.DocumentArea);
}

/// <summary>Arguments of <see cref="DockLayout.PaneClosed"/> and <see cref="DockLayout.ActivePaneChanged"/>.</summary>
public class DockPaneEventArgs : RoutedEventArgs
{
    public DockPaneEventArgs(RoutedEvent routedEvent, DockPane? pane) : base(routedEvent) => Pane = pane;

    /// <summary>The pane (null when no pane is active any more).</summary>
    public DockPane? Pane { get; }
}

/// <summary>Arguments of <see cref="DockLayout.PaneClosing"/>: set <see cref="Cancel"/> to keep the pane open.</summary>
public sealed class DockPaneClosingEventArgs : DockPaneEventArgs
{
    public DockPaneClosingEventArgs(RoutedEvent routedEvent, DockPane pane) : base(routedEvent, pane)
    {
    }

    /// <summary>Set to true to keep the pane open.</summary>
    public bool Cancel { get; set; }
}

/// <summary>Arguments of <see cref="DockLayout.LayoutChanged"/>.</summary>
public sealed class DockLayoutChangedEventArgs : RoutedEventArgs
{
    public DockLayoutChangedEventArgs(RoutedEvent routedEvent, DockLayoutChangeKind kind, DockPane? pane) : base(routedEvent)
    {
        Kind = kind;
        Pane = pane;
    }

    /// <summary>What changed.</summary>
    public DockLayoutChangeKind Kind { get; }

    /// <summary>The pane the change is about; null for resizes and loads.</summary>
    public DockPane? Pane { get; }
}
