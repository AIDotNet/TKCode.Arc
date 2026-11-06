using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

public partial class DockLayout
{
    private MenuSurface? _menu;

    /// <summary>The tab menu, once opened (tests).</summary>
    internal MenuSurface? Menu => _menu;

    #region Panes

    /// <summary>
    /// Adds a pane to the layout. Without a <paramref name="target"/> a document joins the most recently active document
    /// group, and a tool joins a group of tools with the same <see cref="DockPane.PreferredSide"/> or gets a new group along
    /// that edge, <see cref="DockPane.PreferredSize"/> wide or high. A pane already in the layout moves to the target (when
    /// given) and becomes active.
    /// </summary>
    /// <exception cref="InvalidOperationException">The target does not accept the pane (documents stay in the document
    /// area; tools enter it only with <see cref="DockPane.CanDockAsDocument"/>), or another pane has the same id.</exception>
    public void Open(DockPane pane, DockTarget? target = null)
    {
        ArgumentNullException.ThrowIfNull(pane);
        if (ReferenceEquals(pane.Workspace, _workspace))
        {
            if (target is { } move)
            {
                Move(pane, move);
            }
            Select(pane);
            return;
        }
        if (pane.Container is not null)
        {
            throw new InvalidOperationException($"The pane '{pane.Id}' belongs to another layout.");
        }
        if (target is { } t)
        {
            if (!_workspace.Insert(pane, t))
            {
                throw new InvalidOperationException($"The pane '{pane.Id}' cannot be placed at that target.");
            }
        }
        else
        {
            _workspace.PlaceByPreference(pane, EdgeShareFor(pane));
        }
        Select(pane);
        RaiseLayoutChanged(DockLayoutChangeKind.Open, pane);
    }

    /// <summary>
    /// Closes a pane: raises the cancelable <see cref="PaneClosing"/>, takes the pane out of the layout and raises
    /// <see cref="PaneClosed"/>. Its group then selects the tab to its right (or left); an emptied group is removed and the
    /// neighbouring group's pane becomes active. Returns false when the pane cannot close, the layout is locked or a
    /// handler cancelled.
    /// </summary>
    public bool Close(DockPane pane)
    {
        ArgumentNullException.ThrowIfNull(pane);
        if (IsLocked || !pane.CanClose || !ReferenceEquals(pane.Workspace, _workspace))
        {
            return false;
        }
        var closing = new DockPaneClosingEventArgs(PaneClosingEvent, pane);
        RaiseEvent(closing);
        if (closing.Cancel)
        {
            return false;
        }
        if (CloseThroughSource(pane))
        {
            return true;
        }
        FinishClose(pane);
        return true;
    }

    /// <summary>Takes a pane out of the layout after its close was allowed, then announces it and raises <see cref="PaneClosed"/>.</summary>
    internal void FinishClose(DockPane pane)
    {
        var wasActive = ReferenceEquals(ActivePane, pane);
        var hadFocus = pane.IsKeyboardFocusWithin
                       || (pane.Group?.View as DockGroupView)?.TabOf(pane)?.IsKeyboardFocusWithin == true;
        var next = wasActive ? NextActiveAfter(pane) : null;
        _suppressAutoActive = true;
        try
        {
            _workspace.Remove(pane);
        }
        finally
        {
            _suppressAutoActive = false;
        }
        if (wasActive)
        {
            SetCurrentValue(ActivePaneProperty, next is not null && ReferenceEquals(next.Workspace, _workspace) ? next : FallbackActivePane());
        }
        if (hadFocus && ActivePane is { } focusTarget)
        {
            if (!FocusTab(focusTarget))
            {
                Focus();
            }
        }
        else if (hadFocus)
        {
            // Nothing left to focus: let go of the closed pane's tab (the window would remember it as its focused element).
            TopLevel.GetTopLevel(this)?.FocusManager?.Focus(null);
        }
        Announce(DockText.Format(DockLangResourceKind.AnnounceClosed, pane.ToString()));
        RaiseEvent(new DockPaneEventArgs(PaneClosedEvent, pane));
        RaiseLayoutChanged(DockLayoutChangeKind.Close, pane);
    }

    /// <summary>The pane that becomes active when <paramref name="pane"/> leaves: its right (or left) neighbour, else the neighbouring group's.</summary>
    private DockPane? NextActiveAfter(DockPane pane)
    {
        if (pane.Group is not { } group)
        {
            return null;
        }
        if (group.Panes.Count > 1)
        {
            var index = group.Panes.IndexOf(pane);
            return group.Panes[index + 1 < group.Panes.Count ? index + 1 : index - 1];
        }
        var groups = _workspace.Groups().Where(g => g.Float is null).ToList();
        var at = groups.IndexOf(group);
        var neighbour = at + 1 < groups.Count ? groups[at + 1] : at > 0 ? groups[at - 1] : null;
        return neighbour?.ActivePane;
    }

    /// <summary>Closes the closable panes of <paramref name="pane"/>'s group except it.</summary>
    internal void CloseOthers(DockPane pane)
    {
        if (pane.Group is not { } group)
        {
            return;
        }
        foreach (var other in group.Panes.Where(p => !ReferenceEquals(p, pane)).ToList())
        {
            Close(other);
        }
    }

    /// <summary>Closes every closable pane of a group.</summary>
    internal void CloseAll(DockGroup group)
    {
        foreach (var pane in group.Panes.ToList())
        {
            Close(pane);
        }
    }

    /// <summary>
    /// Docks a pane at <paramref name="target"/>, exactly as dropping it there would: join a group as a tab, split a group,
    /// dock to an edge of the whole layout or beside the document area. Documents stay in the document area and tools enter
    /// it only with <see cref="DockPane.CanDockAsDocument"/>; a move those rules refuse does nothing. Returns whether the
    /// pane moved.
    /// </summary>
    public bool Move(DockPane pane, DockTarget target)
    {
        ArgumentNullException.ThrowIfNull(pane);
        var reference = target.Group?.ActivePane;
        if (!_workspace.Move(pane, target))
        {
            return false;
        }
        Select(pane);
        Announce(MoveAnnouncement(pane, target, reference));
        RaiseLayoutChanged(DockLayoutChangeKind.Move, pane);
        return true;
    }

    private static string MoveAnnouncement(DockPane pane, DockTarget target, DockPane? reference)
    {
        var name = pane.ToString();
        var other = reference is null || ReferenceEquals(reference, pane) ? name : reference.ToString();
        if (target.Scope == DockScope.Workspace)
        {
            return DockText.Format(target.Position switch
            {
                DockPosition.Left => DockLangResourceKind.AnnounceDockedEdgeLeft,
                DockPosition.Right => DockLangResourceKind.AnnounceDockedEdgeRight,
                DockPosition.Top => DockLangResourceKind.AnnounceDockedEdgeTop,
                _ => DockLangResourceKind.AnnounceDockedEdgeBottom
            }, name);
        }
        return DockText.Format(target.Position switch
        {
            DockPosition.Left => DockLangResourceKind.AnnounceDockedLeft,
            DockPosition.Right => DockLangResourceKind.AnnounceDockedRight,
            DockPosition.Top => DockLangResourceKind.AnnounceDockedAbove,
            DockPosition.Bottom => DockLangResourceKind.AnnounceDockedBelow,
            _ => DockLangResourceKind.AnnounceJoined
        }, name, other);
    }

    /// <summary>Moves a tab one place left (<paramref name="delta"/> −1) or right (+1) in its group.</summary>
    internal bool MoveTab(DockPane pane, int delta)
    {
        if (IsLocked || !pane.CanMove || pane.Group is not { } group)
        {
            return false;
        }
        var index = group.Panes.IndexOf(pane);
        var to = index + delta;
        if (to < 0 || to >= group.Panes.Count || !_workspace.Move(pane, DockTarget.Into(group, to)))
        {
            return false;
        }
        Announce(DockText.Format(DockLangResourceKind.AnnounceReordered, pane.ToString(), to + 1, group.Panes.Count));
        RaiseLayoutChanged(DockLayoutChangeKind.Move, pane);
        return true;
    }

    /// <summary>The share of the layout a tool placed along its edge takes: its preferred size over the layout's width or height.</summary>
    private double EdgeShareFor(DockPane pane)
    {
        var horizontal = pane.PreferredSide is DockSide.Left or DockSide.Right;
        var extent = horizontal ? Bounds.Width - Padding.Left - Padding.Right : Bounds.Height - Padding.Top - Padding.Bottom;
        if (extent <= 0)
        {
            return DockWorkspace.EdgeShare;
        }
        return Math.Clamp(pane.EffectivePreferredSize / extent, 0.1, 0.5);
    }

    #endregion

    #region Save and restore

    /// <summary>
    /// Writes the layout as JSON (format <c>tkcode-arc-dock</c>, version 1): the tree with each node's share, the
    /// auto-hidden and floating panes, and the active pane. Panes are stored by <see cref="DockPane.Id"/> only.
    /// </summary>
    public string SaveLayout()
    {
        var open = _workspace.AllPanes().Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var closed = _workspace.KnownPanes.Keys.Where(id => !open.Contains(id));
        return DockLayoutSerializer.Write(_workspace, ActivePane?.Id, closed);
    }

    /// <summary>
    /// Replaces the layout with a saved one (<see cref="SaveLayout"/>). Panes are found by id among the panes this layout
    /// has had, then through <see cref="PaneResolver"/>; unknown ids are skipped. Panes the saved layout does not mention go
    /// back by their <see cref="DockPane.PreferredSide"/> (documents to the document area). Returns false, leaving the
    /// layout as it was, when the text is not a layout or comes from a newer version.
    /// </summary>
    /// <remarks>Floating windows are pulled onto a screen when they open (any screen that shows enough of them counts).</remarks>
    public bool LoadLayout(string json) => LoadLayoutCore(json, workArea: null);

    internal bool LoadLayoutCore(string json, Rect? workArea)
    {
        if (!DockLayoutSerializer.TryRead(json, out var data))
        {
            return false;
        }
        DockPane? active;
        _suppressAutoActive = true;
        try
        {
            active = DockLayoutLoader.Apply(_workspace, data, PaneResolver,
                workArea is { } area ? bounds => DockLayoutSerializer.ClampToWorkArea(bounds, area) : null, EdgeShareFor);
        }
        finally
        {
            _suppressAutoActive = false;
        }
        SetCurrentValue(ActivePaneProperty, active ?? FallbackActivePane());
        RaiseLayoutChanged(DockLayoutChangeKind.Load, null);
        return true;
    }

    #endregion

    #region Menus

    private MenuSurface EnsureMenu()
    {
        return _menu ??= new MenuSurface { Variant = MenuSurfaceVariant.Context, ClosesOnViewportChange = true };
    }

    /// <summary>
    /// Opens a pane's menu: at <paramref name="at"/> (pointer, in <paramref name="anchor"/> coordinates) or under the anchor
    /// with the first row highlighted (keyboard).
    /// </summary>
    internal void ShowPaneMenu(DockPane pane, Control anchor, Point? at, MenuOpenFocus? focus = null)
    {
        var menu = EnsureMenu();
        menu.Close(returnFocus: false);
        menu.Items.Clear();
        foreach (var entry in BuildPaneMenu(pane))
        {
            menu.Items.Add(entry);
        }
        if (at is { } point)
        {
            menu.OpenAt(anchor, point, focus ?? MenuOpenFocus.Surface);
        }
        else
        {
            menu.Open(anchor, focus ?? MenuOpenFocus.First);
        }
    }

    /// <summary>The rows of a pane's menu (spec section 11): close commands, then Move to and Dock to.</summary>
    internal List<MenuEntry> BuildPaneMenu(DockPane pane)
    {
        var locked = IsLocked;
        var group = pane.Group;
        var rows = new List<MenuEntry>
        {
            Row(DockText.Get(DockLangResourceKind.Close), !locked && pane.CanClose, () => Close(pane), ArcIcons.X)
        };
        if (group is { IsDocumentGroup: true })
        {
            rows.Add(Row(DockText.Get(DockLangResourceKind.CloseOthers),
                !locked && group.Panes.Any(p => !ReferenceEquals(p, pane) && p.CanClose), () => CloseOthers(pane)));
            rows.Add(Row(DockText.Get(DockLangResourceKind.CloseAll), !locked && group.Panes.Any(p => p.CanClose), () => CloseAll(group)));
        }
        AddFloatingRows(rows, pane);
        AddAutoHideRows(rows, pane);
        var canMove = !locked && pane.CanMove && group is not null;
        if (group is not null)
        {
            var moveTo = Submenu(DockText.Get(DockLangResourceKind.MoveTo),
            [
                MoveRow(DockLangResourceKind.NewGroupLeft, pane, DockTarget.Split(group, DockPosition.Left), canMove),
                MoveRow(DockLangResourceKind.NewGroupRight, pane, DockTarget.Split(group, DockPosition.Right), canMove),
                MoveRow(DockLangResourceKind.NewGroupAbove, pane, DockTarget.Split(group, DockPosition.Top), canMove),
                MoveRow(DockLangResourceKind.NewGroupBelow, pane, DockTarget.Split(group, DockPosition.Bottom), canMove)
            ]);
            moveTo.HasSeparatorBefore = true;
            rows.Add(moveTo);
        }
        if (!pane.IsDocument)
        {
            var dockTo = Submenu(DockText.Get(DockLangResourceKind.DockTo),
            [
                MoveRow(DockLangResourceKind.SideLeft, pane, DockTarget.Edge(DockPosition.Left), canMove),
                MoveRow(DockLangResourceKind.SideRight, pane, DockTarget.Edge(DockPosition.Right), canMove),
                MoveRow(DockLangResourceKind.SideTop, pane, DockTarget.Edge(DockPosition.Top), canMove),
                MoveRow(DockLangResourceKind.SideBottom, pane, DockTarget.Edge(DockPosition.Bottom), canMove)
            ]);
            dockTo.HasSeparatorBefore = group is null;
            rows.Add(dockTo);
        }
        AddMaximizeRow(rows, pane);
        return rows;
    }

    private MenuEntry MoveRow(DockLangResourceKind text, DockPane pane, DockTarget target, bool canMove)
        => Row(DockText.Get(text), canMove && _workspace.CanPlace(pane, target), () =>
        {
            if (Move(pane, target))
            {
                // The menu hands focus back to the old tab, which has gone; put it on the pane's new tab.
                Dispatcher.UIThread.Post(() => FocusTab(pane), DispatcherPriority.Loaded);
            }
        });

    internal static MenuEntry Row(string header, bool enabled, Action action, Avalonia.Media.Geometry? icon = null)
    {
        var row = new MenuEntry { Header = header, IsEnabled = enabled, Icon = icon };
        row.Click += (_, _) => action();
        return row;
    }

    private static MenuEntry Submenu(string header, MenuEntry[] rows)
        => new() { Header = header, ItemsSource = rows, IsEnabled = rows.Any(r => r.IsEnabled) };

    /// <summary>Opens the list of a group's tabs (the strip's overflow button): choosing one selects it.</summary>
    internal void ShowTabList(DockGroupView view, Control anchor)
    {
        var menu = EnsureMenu();
        menu.Close(returnFocus: false);
        menu.Items.Clear();
        foreach (var pane in view.Group.Panes)
        {
            var row = new MenuEntry
            {
                Header = pane.ToString(),
                ToggleType = MenuEntryToggleType.Radio,
                GroupName = "dock-tabs",
                IsChecked = ReferenceEquals(pane, view.Group.ActivePane),
                Icon = pane.Icon
            };
            var target = pane;
            row.Click += (_, _) =>
            {
                Select(target);
                Dispatcher.UIThread.Post(() => FocusTab(target, NavigationMethod.Pointer), DispatcherPriority.Loaded);
            };
            menu.Items.Add(row);
        }
        menu.Open(anchor, MenuOpenFocus.Surface);
    }

    #endregion
}
