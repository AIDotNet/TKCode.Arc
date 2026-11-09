using Avalonia;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

public partial class DockLayout
{
    public const string IndicatorsPart = "PART_Indicators";
    public const string DraggingPseudoClass = ":dragging";

    /// <summary>How far the pointer may leave a tab strip, above or below, before reordering turns into docking (spec 9.1).</summary>
    internal const double StripLeaveDistance = 16;

    /// <summary>The share of a group, from each side, in which the pointer docks to that side when it is not on a button (spec 9.2).</summary>
    internal const double ZoneShare = 0.25;

    /// <summary>Without indicators: how close to the layout's edge the pointer docks to that edge of the whole layout (self-designed).</summary>
    internal const double EdgeBand = 16;

    /// <summary>
    /// Whether a drag shows the compass and the edge buttons. Off, panes still dock by where the pointer is in a group (a
    /// quarter from an edge splits that side, the middle joins as a tab) and close to the layout's edge; only the preview shows.
    /// </summary>
    public static readonly StyledProperty<bool> ShowDockIndicatorsProperty =
        AvaloniaProperty.Register<DockLayout, bool>(nameof(ShowDockIndicators), true);

    private DockIndicatorLayer? _indicators;
    private DockMainHost? _mainHost;
    private DockDragSession? _drag;

    /// <inheritdoc cref="ShowDockIndicatorsProperty"/>
    public bool ShowDockIndicators { get => GetValue(ShowDockIndicatorsProperty); set => SetValue(ShowDockIndicatorsProperty, value); }

    /// <summary>The layout's indicator layer (tests, hosts).</summary>
    internal DockIndicatorLayer? IndicatorLayer => _indicators;

    internal DockMainHost MainHost => _mainHost ??= new DockMainHost(this);

    /// <summary>The drag in progress (tests).</summary>
    internal DockDragSession? DragSession => _drag;

    private void ApplyDragTemplate(TemplateAppliedEventArgs e)
    {
        CancelDrag();
        _indicators?.Clear(animate: false);
        _indicators = e.NameScope.Find<DockIndicatorLayer>(IndicatorsPart);
    }

    #region Starting

    /// <summary>What a press on a group's tab strip drags: the pressed tab's pane, or (empty strip area) the whole group.</summary>
    internal DockDragRequest? ResolveStripPress(DockGroupView view, PointerPressedEventArgs e)
    {
        var group = view.Group;
        if (IsLocked || _drag is not null || !ReferenceEquals(group.Workspace, _workspace) || group.Panes.Count == 0)
        {
            return null;
        }
        var source = e.Source as Visual;
        var tab = source as DockTab ?? source?.FindAncestorOfType<DockTab>();
        if (tab?.CloseButton is { } close && source is not null && (ReferenceEquals(source, close) || close.IsVisualAncestorOf(source)))
        {
            return null;
        }
        var host = group.Float is { } dock ? HostOf(dock) : null;
        if (host is { IsWindow: true } && !DockFloatingWindow.CanTrackPosition)
        {
            // Where windows cannot be placed (Wayland), floating windows re-dock from the menu or by a double click.
            return null;
        }
        var onlyGroup = host is not null && ReferenceEquals(group.Float!.Root, group);
        if (tab is not null && ReferenceEquals(tab.Owner, view))
        {
            if (!tab.Pane.CanMove)
            {
                return null;
            }
            return new DockDragRequest([tab.Pane], tab.Pane, group, isGroupDrag: false)
            {
                Tab = tab,
                MovingHost = onlyGroup && group.Panes.Count == 1 ? host : null
            };
        }
        if (group.Panes.Any(p => !p.CanMove))
        {
            return null;
        }
        return new DockDragRequest(group.Panes.ToList(), group.ActivePane ?? group.Panes[0], group, isGroupDrag: true)
        {
            MovingHost = onlyGroup ? host : null
        };
    }

    /// <summary>What a press on a floating host's title bar drags: every pane of the float, moving the host itself.</summary>
    internal DockDragRequest? ResolveHostPress(IDockFloatHost host)
    {
        if (IsLocked || _drag is not null || host.Float?.Root is not { } root)
        {
            return null;
        }
        var panes = root.AllPanes().ToList();
        if (panes.Count == 0 || panes.Any(p => !p.CanMove))
        {
            return null;
        }
        var group = root as DockGroup ?? root.SelfAndDescendants().OfType<DockGroup>().First(g => g.Panes.Count > 0);
        return new DockDragRequest(panes, group.ActivePane ?? panes[0], group, isGroupDrag: true) { MovingHost = host };
    }

    /// <summary>Starts a drag the gesture recognized; false when it may not start (locked, another drag).</summary>
    internal bool BeginDrag(DockDragRequest request, DockDragGesture gesture, PointerEventArgs e, Point start)
    {
        if (_drag is not null || IsLocked || TopLevel.GetTopLevel(gesture.Target) is not { } top)
        {
            return false;
        }
        if (MaximizedGroup is not null)
        {
            // A drag needs the whole layout to aim at: restore it first (before the gesture captures the pointer, so the
            // group view moving back into its split does not cost the capture).
            RestoreMaximized();
            top.UpdateLayout();
        }
        var session = new DockDragSession(request, gesture, top, start) { StartScreen = top.PointToScreen(start) };
        _drag = session;
        top.AddHandler(KeyDownEvent, OnDragKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        if (request.MovingHost is { } host)
        {
            session.Phase = DockDragPhase.Move;
            BeginHostMove(session, host);
            EnterDock(session);
        }
        else if (request is { Tab: { } tab, IsGroupDrag: false } && tab.Owner.Strip is { } strip && request.SourceGroup.Panes.Count > 1)
        {
            StartReorder(session, tab, strip);
        }
        else
        {
            EnterDock(session);
        }
        return true;
    }

    private void EnterDock(DockDragSession session)
    {
        if (session.Phase != DockDragPhase.Move)
        {
            session.Phase = DockDragPhase.Dock;
        }
        PseudoClasses.Set(DraggingPseudoClass, true);
        if (!session.IsGroupDrag && session.Request.Tab is { } tab)
        {
            tab.SetDragging(true);
        }
    }

    #endregion

    #region Moving

    /// <summary>A pointer move of the drag in progress.</summary>
    internal void MoveDrag(PointerEventArgs e)
    {
        if (_drag is not { } session)
        {
            return;
        }
        var point = e.GetPosition(session.Top);
        session.Last = point;
        if (session.Phase == DockDragPhase.Reorder)
        {
            if (UpdateReorder(session, point))
            {
                return;
            }
            LeaveReorder(session);
            EnterDock(session);
        }
        var screen = session.Top.PointToScreen(point);
        if (session.Phase == DockDragPhase.Move && session.MovingHost is { } host)
        {
            // The event's position is relative to where the window was when it came: its screen point stays valid after the move.
            MoveHost(session, host, point, screen);
        }
        var hit = HitTest(session, point, screen);
        ApplyHit(session, hit);
    }

    private IEnumerable<IDockHost> HostsInOrder(DockDragSession session)
    {
        foreach (var host in _floatHosts.Values.Where(h => h.IsWindow && DockFloatingWindow.CanTrackPosition)
                     .OrderByDescending(h => h.ActivationStamp))
        {
            if (!ReferenceEquals(host, session.MovingHost))
            {
                yield return host;
            }
        }
        foreach (var host in _floatHosts.Values.Where(h => !h.IsWindow).OrderByDescending(h => h.ActivationStamp))
        {
            if (!ReferenceEquals(host, session.MovingHost))
            {
                yield return host;
            }
        }
        yield return MainHost;
    }

    /// <summary>The pointer in <paramref name="visual"/>'s coordinates: directly within the drag's window, by screen position across windows.</summary>
    private static Point? ToVisual(DockDragSession session, Visual visual, Point point, PixelPoint screen)
    {
        if (!visual.IsAttachedToVisualTree())
        {
            return null;
        }
        if (ReferenceEquals(TopLevel.GetTopLevel(visual), session.Top))
        {
            return session.Top.TranslatePoint(point, visual);
        }
        return visual.PointToClient(screen);
    }

    private DockHit HitTest(DockDragSession session, Point point, PixelPoint screen)
    {
        foreach (var host in HostsInOrder(session))
        {
            if (host.Layer is not { } layer || !host.Region.IsEffectivelyVisible)
            {
                continue;
            }
            if (ToVisual(session, host.Region, point, screen) is not { } inRegion || !new Rect(host.Region.Bounds.Size).Contains(inRegion))
            {
                continue;
            }
            if (ToVisual(session, layer, point, screen) is not { } inLayer)
            {
                continue;
            }
            if (!ReferenceEquals(host, session.HoverHost))
            {
                SwitchHost(session, host);
            }
            return HitInHost(session, host, layer, inLayer);
        }
        if (session.HoverHost is not null)
        {
            SwitchHost(session, null);
        }
        return default;
    }

    private void SwitchHost(DockDragSession session, IDockHost? host)
    {
        session.HoverHost?.Layer?.Clear();
        session.HoverHost = host;
        session.CompassGroup = null;
        if (host is DockMainHost && ShowDockIndicators && _indicators is { } layer && AreaRect(layer) is { } area)
        {
            layer.ShowEdges(area, slot => IsDroppable(session, EdgeTarget(slot)));
        }
    }

    private DockHit HitInHost(DockDragSession session, IDockHost host, DockIndicatorLayer layer, Point p)
    {
        var indicators = ShowDockIndicators;
        var isMain = host is DockMainHost;
        if (indicators)
        {
            if (layer.HitTest(p) is { } slot && (DockCompassButton.IsEdgeSlot(slot) || session.CompassGroup is not null))
            {
                return SlotHit(session, host, layer, slot, session.CompassGroup, p);
            }
        }
        else if (isMain && EdgeZone(layer, p) is { } edge)
        {
            return Evaluate(session, host, DockTarget.Edge(edge), EdgePreview(layer, edge), null, null, p);
        }
        if (GroupAt(host, layer, p) is not { } found)
        {
            layer.ShowCompass(null, false, static _ => false);
            session.CompassGroup = null;
            return new DockHit(host, null, false, null, null, null, p);
        }
        var (group, view, rect) = found;
        if (indicators)
        {
            if (!ReferenceEquals(session.CompassGroup, group))
            {
                session.CompassGroup = group;
                var ring = isMain && group.IsInDocumentArea && _workspace.DocumentArea?.View is { } areaView && areaView.IsAttachedToVisualTree();
                layer.ShowCompass(rect, ring, slot => IsDroppable(session, SlotTarget(slot, group)));
            }
            if (layer.HitTest(p) is { } slot && !DockCompassButton.IsEdgeSlot(slot))
            {
                return SlotHit(session, host, layer, slot, group, p);
            }
        }
        if (view.Strip is { } strip && StripRect(strip, layer) is { } stripRect && stripRect.Contains(p))
        {
            var (index, line) = Insertion(session, view, strip, layer, p);
            return Evaluate(session, host, DockTarget.Into(group, index), rect, line, null, p);
        }
        if (session.Phase == DockDragPhase.Move)
        {
            // A floating window or card being moved docks only on a button or a tab strip: anywhere else it just moves.
            return new DockHit(host, null, false, null, null, null, p);
        }
        var position = Zone(p, rect);
        var target = position == DockPosition.Center ? DockTarget.Into(group) : DockTarget.Split(group, position);
        return Evaluate(session, host, target, SplitPreview(rect, position), null, null, p);
    }

    private DockHit SlotHit(DockDragSession session, IDockHost host, DockIndicatorLayer layer, DockCompassSlot slot, DockGroup? group, Point p)
    {
        if (!layer.IsSlotEnabled(slot))
        {
            // A button that refuses the drag: dropping on it leaves everything where it was.
            return new DockHit(host, null, true, null, null, slot, p);
        }
        var position = DockCompassButton.PositionOf(slot);
        if (DockCompassButton.IsEdgeSlot(slot))
        {
            return Evaluate(session, host, DockTarget.Edge(position), EdgePreview(layer, position), null, slot, p);
        }
        if (DockCompassButton.IsAreaSlot(slot))
        {
            var area = _workspace.DocumentArea?.View is { } areaView ? RectIn(areaView, layer) : null;
            return Evaluate(session, host, DockTarget.DocumentArea(position), area is { } a ? SplitPreview(a, position) : null, null, slot, p);
        }
        if (group?.View is not DockGroupView view || RectIn(view, layer) is not { } rect)
        {
            return new DockHit(host, null, false, null, null, null, p);
        }
        return Evaluate(session, host, SlotTarget(slot, group), SplitPreview(rect, position), null, slot, p);
    }

    private DockHit Evaluate(DockDragSession session, IDockHost host, DockTarget target, Rect? preview, Rect? line, DockCompassSlot? slot, Point p)
    {
        if (IsNoOp(session, target))
        {
            return new DockHit(host, null, true, null, null, slot, p);
        }
        if (!CanDrop(session, target))
        {
            return new DockHit(host, null, false, null, null, slot, p);
        }
        return new DockHit(host, target, false, preview, line, slot, p);
    }

    /// <summary>A target that leaves the drag where it is: the dragged group itself, or splitting a one-pane group off itself.</summary>
    private static bool IsNoOp(DockDragSession session, DockTarget target)
    {
        if (target.Scope != DockScope.Group || !ReferenceEquals(target.Group, session.SourceGroup))
        {
            return false;
        }
        return session.IsGroupDrag || (target.Position != DockPosition.Center && session.SourceGroup.Panes.Count == 1);
    }

    /// <summary>Whether every dragged pane may go to <paramref name="target"/> (spec 9.2 rules).</summary>
    private bool CanDrop(DockDragSession session, DockTarget target)
    {
        if (target.Scope == DockScope.Group && target.Group is { } group && session.MovingHost?.Float is { } moving
            && ReferenceEquals(group.Float, moving))
        {
            return false;
        }
        foreach (var pane in session.Panes)
        {
            if (!_workspace.CanPlace(pane, target))
            {
                return false;
            }
        }
        return true;
    }

    private bool IsDroppable(DockDragSession session, DockTarget target) => !IsNoOp(session, target) && CanDrop(session, target);

    private static DockTarget EdgeTarget(DockCompassSlot slot) => DockTarget.Edge(DockCompassButton.PositionOf(slot));

    private static DockTarget SlotTarget(DockCompassSlot slot, DockGroup group)
    {
        var position = DockCompassButton.PositionOf(slot);
        if (DockCompassButton.IsAreaSlot(slot))
        {
            return DockTarget.DocumentArea(position);
        }
        if (DockCompassButton.IsEdgeSlot(slot))
        {
            return DockTarget.Edge(position);
        }
        return position == DockPosition.Center ? DockTarget.Into(group) : DockTarget.Split(group, position);
    }

    private static (DockGroup Group, DockGroupView View, Rect Rect)? GroupAt(IDockHost host, DockIndicatorLayer layer, Point p)
    {
        foreach (var group in host.Groups)
        {
            if (group.View is DockGroupView view && view.IsEffectivelyVisible && RectIn(view, layer) is { } rect && rect.Contains(p))
            {
                return (group, view, rect);
            }
        }
        return null;
    }

    /// <summary>A visual's bounds in the layer's coordinates, or null when they are not in one tree.</summary>
    private static Rect? RectIn(Visual visual, Visual layer)
    {
        if (!visual.IsAttachedToVisualTree() || visual.TranslatePoint(default, layer) is not { } origin)
        {
            return null;
        }
        return new Rect(origin, visual.Bounds.Size);
    }

    /// <summary>The whole layout's tree (inside the padding) in the layer's coordinates.</summary>
    private Rect? AreaRect(DockIndicatorLayer layer) => _host is null ? null : RectIn(_host, layer);

    private static Rect? StripRect(DockTabStrip strip, DockIndicatorLayer layer) => RectIn(strip, layer);

    /// <summary>Where the dragged tab would go among a strip's tabs, and the insertion line there (spec 9.2).</summary>
    private static (int Index, Rect Line) Insertion(DockDragSession session, DockGroupView view, DockTabStrip strip, DockIndicatorLayer layer, Point p)
    {
        var tabs = strip.Tabs.Where(t => session.IsGroupDrag || !ReferenceEquals(t.Pane, session.Lead) || !ReferenceEquals(view.Group, session.SourceGroup))
            .ToList();
        var x = layer.TranslatePoint(p, strip)?.X ?? 0;
        var index = 0;
        foreach (var tab in tabs)
        {
            if (tab.Bounds.Center.X < x)
            {
                index++;
            }
        }
        var width = layer.TryFindResource(DockIndicatorTokens.Key(DockIndicatorTokenKind.InsertionLineWidth), out var w) && w is double d ? d : 2;
        var gap = strip.TryFindResource(DockTabTokens.Key(DockTabTokenKind.TabGap), out var g) && g is double gd ? gd : 2;
        double lineX;
        if (tabs.Count == 0)
        {
            lineX = 0;
        }
        else if (index == 0)
        {
            lineX = tabs[0].Bounds.X - gap / 2;
        }
        else
        {
            lineX = tabs[index - 1].Bounds.Right + gap / 2;
        }
        lineX = Math.Clamp(lineX, width / 2, Math.Max(width / 2, strip.Bounds.Width - width / 2));
        var tabHeight = tabs.Count > 0 ? tabs[0].Bounds.Height : strip.Bounds.Height;
        var top = Math.Max(0, (strip.Bounds.Height - tabHeight) / 2);
        var origin = strip.TranslatePoint(new Point(lineX - width / 2, top), layer) ?? default;
        return (index, new Rect(origin, new Size(width, tabHeight)));
    }

    /// <summary>The side of a group the pointer docks to when it is not on a button: a quarter from an edge, else the middle.</summary>
    internal static DockPosition Zone(Point p, Rect rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return DockPosition.Center;
        }
        var fx = (p.X - rect.X) / rect.Width;
        var fy = (p.Y - rect.Y) / rect.Height;
        var left = fx;
        var right = 1 - fx;
        var top = fy;
        var bottom = 1 - fy;
        var nearest = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
        if (nearest >= ZoneShare)
        {
            return DockPosition.Center;
        }
        if (nearest == left)
        {
            return DockPosition.Left;
        }
        if (nearest == right)
        {
            return DockPosition.Right;
        }
        return nearest == top ? DockPosition.Top : DockPosition.Bottom;
    }

    private static DockPosition? EdgeZone(DockIndicatorLayer layer, Point p)
    {
        var size = layer.Bounds.Size;
        if (p.X < EdgeBand)
        {
            return DockPosition.Left;
        }
        if (p.X > size.Width - EdgeBand)
        {
            return DockPosition.Right;
        }
        if (p.Y < EdgeBand)
        {
            return DockPosition.Top;
        }
        if (p.Y > size.Height - EdgeBand)
        {
            return DockPosition.Bottom;
        }
        return null;
    }

    private static Rect SplitPreview(Rect rect, DockPosition position) => position switch
    {
        DockPosition.Left => new Rect(rect.X, rect.Y, rect.Width / 2, rect.Height),
        DockPosition.Right => new Rect(rect.X + rect.Width / 2, rect.Y, rect.Width / 2, rect.Height),
        DockPosition.Top => new Rect(rect.X, rect.Y, rect.Width, rect.Height / 2),
        DockPosition.Bottom => new Rect(rect.X, rect.Y + rect.Height / 2, rect.Width, rect.Height / 2),
        _ => rect
    };

    private Rect? EdgePreview(DockIndicatorLayer layer, DockPosition position)
    {
        if (AreaRect(layer) is not { } area)
        {
            return null;
        }
        var share = DockWorkspace.EdgeShare;
        return position switch
        {
            DockPosition.Left => new Rect(area.X, area.Y, area.Width * share, area.Height),
            DockPosition.Right => new Rect(area.Right - area.Width * share, area.Y, area.Width * share, area.Height),
            DockPosition.Top => new Rect(area.X, area.Y, area.Width, area.Height * share),
            _ => new Rect(area.X, area.Bottom - area.Height * share, area.Width, area.Height * share)
        };
    }

    private static void ApplyHit(DockDragSession session, DockHit hit)
    {
        session.Hit = hit;
        if (hit.Host?.Layer is { } layer)
        {
            layer.SetHot(hit.Target is not null ? hit.Slot : null);
            layer.ShowPreview(hit.Preview);
            layer.ShowInsertionLine(hit.Line);
            layer.ShowGhost(session.MovingHost is null ? hit.LayerPoint : null, session.Panes, session.Lead);
        }
        var view = hit.Target is { Scope: DockScope.Group, Group: { } group } ? group.View as DockGroupView : null;
        if (!ReferenceEquals(view, session.DropView))
        {
            session.DropView?.SetDropTarget(false);
            session.DropView = view;
            view?.SetDropTarget(true);
        }
    }

    private void OnDragKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _drag is not { } session)
        {
            return;
        }
        e.Handled = true;
        session.Gesture.Abandon();
        EndDrag(null, drop: false);
    }

    /// <summary>Cancels the drag in progress, if any (Escape, template change, workspace change).</summary>
    internal void CancelDrag()
    {
        if (_drag is { } session)
        {
            session.Gesture.Abandon();
            EndDrag(null, drop: false);
        }
    }

    #endregion

    #region Reorder

    private static void StartReorder(DockDragSession session, DockTab tab, DockTabStrip strip)
    {
        session.Phase = DockDragPhase.Reorder;
        session.Strip = strip;
        session.Tabs.Clear();
        session.Tabs.AddRange(strip.Tabs);
        session.Slots = session.Tabs.Select(t => t.Bounds.X).ToArray();
        session.Widths = session.Tabs.Select(t => t.Bounds.Width).ToArray();
        session.From = session.Tabs.IndexOf(tab);
        session.Index = session.From;
        session.PointerStartX = session.Top.TranslatePoint(session.Start, strip)?.X ?? 0;
        // The selected pill travels with the tab and covers the tabs it passes (tabs have no background of their own).
        strip.Pill.ZIndex = 1;
        tab.ZIndex = 2;
    }

    /// <summary>Moves the tab along its strip; false once the pointer has left the strip (the drag then docks).</summary>
    private static bool UpdateReorder(DockDragSession session, Point point)
    {
        if (session.Strip is not { } strip || session.Request.Tab is not { } tab || session.From < 0)
        {
            return false;
        }
        var inStrip = session.Top.TranslatePoint(point, strip);
        var inGroup = session.Top.TranslatePoint(point, tab.Owner);
        if (inStrip is not { } sp || inGroup is not { } gp
            || sp.Y < -StripLeaveDistance || sp.Y > strip.Bounds.Height + StripLeaveDistance
            || gp.X < 0 || gp.X > tab.Owner.Bounds.Width)
        {
            return false;
        }
        var from = session.From;
        var last = session.Slots.Length - 1;
        var min = session.Slots[0] - session.Slots[from];
        var max = session.Slots[last] + session.Widths[last] - (session.Slots[from] + session.Widths[from]);
        var raw = sp.X - session.PointerStartX;
        var dx = Math.Clamp(raw, min, max);
        session.Offset = dx;
        DockTabShift.Of(tab).Jump(dx);
        // A neighbour makes room once the dragged tab's leading edge passes its middle; the place follows the pointer, not
        // the clamped tab, so a wide tab still passes a narrow last one.
        var left = session.Slots[from] + raw;
        var right = left + session.Widths[from];
        var index = 0;
        for (var i = 0; i < session.Slots.Length; i++)
        {
            var middle = session.Slots[i] + session.Widths[i] / 2;
            if ((i < from && middle < left) || (i > from && middle < right))
            {
                index++;
            }
        }
        session.Index = index;
        var gap = TabGapOf(strip);
        var room = session.Widths[from] + gap;
        for (var i = 0; i < session.Tabs.Count; i++)
        {
            if (i == from)
            {
                continue;
            }
            var other = i < from ? i : i - 1;
            var shift = i > from && other < index ? -room : i < from && other >= index ? room : 0;
            var motion = DockTabShift.Of(session.Tabs[i]);
            // A tab resting on its target needs no spring (each one would cost a task and a scheduler round trip per move).
            if (motion.IsAnimating || Math.Abs(motion.Value - shift) > 0.01)
            {
                motion.SpringTo(shift);
            }
        }
        strip.Pill.MoveTo(new Rect(session.Slots[from] + dx, tab.Bounds.Y, session.Widths[from], tab.Bounds.Height), animate: false);
        return true;
    }

    private static double TabGapOf(Control strip)
        => strip.TryFindResource(DockTabTokens.Key(DockTabTokenKind.TabGap), out var value) && value is double gap ? gap : 2;

    /// <summary>The pointer left the strip: the tabs spring back and the drag becomes a dock drag.</summary>
    private static void LeaveReorder(DockDragSession session)
    {
        foreach (var tab in session.Tabs)
        {
            if (ReferenceEquals(tab, session.Request.Tab))
            {
                DockTabShift.Of(tab).Jump(0);
                tab.ZIndex = 0;
            }
            else
            {
                DockTabShift.Of(tab).SpringTo(0);
            }
        }
        if (session.Strip is { } strip)
        {
            strip.Pill.ZIndex = 0;
            strip.InvalidateArrange();
        }
        session.Strip = null;
    }

    /// <summary>Drops a reordered tab at its new place: the model changes and the tab springs from where it is into its slot.</summary>
    private void CommitReorder(DockDragSession session)
    {
        if (session.Request.Tab is not { } tab || session.Strip is not { } strip)
        {
            return;
        }
        var from = session.From;
        var index = session.Index;
        tab.ZIndex = 0;
        strip.Pill.ZIndex = 0;
        if (index == from)
        {
            DockTabShift.Of(tab).SpringTo(0);
            foreach (var other in session.Tabs)
            {
                DockTabShift.Of(other).SpringTo(0);
            }
            strip.InvalidateArrange();
            return;
        }
        var visual = session.Slots[from] + session.Offset;
        var gap = TabGapOf(strip);
        var slot = session.Slots[0];
        var k = 0;
        for (var i = 0; i < session.Tabs.Count && k < index; i++)
        {
            if (i != from)
            {
                slot += session.Widths[i] + gap;
                k++;
            }
        }
        foreach (var other in session.Tabs)
        {
            DockTabShift.Of(other).Jump(0);
        }
        var pane = tab.Pane;
        var group = session.SourceGroup;
        if (_workspace.Move(pane, DockTarget.Into(group, index)))
        {
            Select(pane);
            Announce(DockText.Format(DockLangResourceKind.AnnounceReordered, pane.ToString(), group.Panes.IndexOf(pane) + 1, group.Panes.Count));
            RaiseLayoutChanged(DockLayoutChangeKind.Move, pane);
            var shift = DockTabShift.Of(tab);
            shift.Jump(visual - slot);
            shift.SpringTo(0);
        }
    }

    #endregion

    #region Ending

    /// <summary>Ends the drag: drops (release) or puts everything back (Escape, lost capture).</summary>
    internal void EndDrag(PointerEventArgs? e, bool drop)
    {
        if (_drag is not { } session)
        {
            return;
        }
        if (e is not null)
        {
            session.Last = e.GetPosition(session.Top);
        }
        _drag = null;
        session.Top.RemoveHandler(KeyDownEvent, OnDragKeyDown);
        Cleanup(session);
        if (!drop)
        {
            if (session.Phase == DockDragPhase.Reorder)
            {
                ResetReorder(session);
            }
            else if (session.Phase == DockDragPhase.Move && session.MovingHost is { } moving)
            {
                RestoreHost(session, moving);
            }
            return;
        }
        switch (session.Phase)
        {
            case DockDragPhase.Reorder:
                CommitReorder(session);
                break;
            case DockDragPhase.Dock:
                if (session.Hit.Target is { } target)
                {
                    Drop(session, target);
                }
                else if (!session.Hit.NoOp && CanFloatPanes(session.Panes))
                {
                    FloatFromDrag(session);
                }
                break;
            case DockDragPhase.Move:
                if (session.Hit.Target is { } docked)
                {
                    Drop(session, docked);
                }
                else if (session.MovingHost is { } host)
                {
                    host.BringToFront();
                    RaiseLayoutChanged(DockLayoutChangeKind.Move, session.Lead);
                }
                break;
        }
    }

    private static void ResetReorder(DockDragSession session)
    {
        foreach (var tab in session.Tabs)
        {
            DockTabShift.Of(tab).SpringTo(0);
            tab.ZIndex = 0;
        }
        if (session.Strip is { } strip)
        {
            strip.Pill.ZIndex = 0;
            strip.InvalidateArrange();
        }
    }

    private void Cleanup(DockDragSession session)
    {
        PseudoClasses.Set(DraggingPseudoClass, false);
        session.Request.Tab?.SetDragging(false);
        session.DropView?.SetDropTarget(false);
        session.DropView = null;
        _indicators?.Clear(animate: false);
        foreach (var host in _floatHosts.Values)
        {
            host.Layer?.Clear(animate: false);
        }
    }

    /// <summary>Docks the dragged pane or panes at <paramref name="target"/>.</summary>
    private void Drop(DockDragSession session, DockTarget target)
    {
        if (!session.IsGroupDrag && session.Panes.Count == 1)
        {
            if (target is { Scope: DockScope.Group, Position: DockPosition.Center } && ReferenceEquals(target.Group, session.SourceGroup))
            {
                var group = session.SourceGroup;
                var pane = session.Lead;
                if (target.Index is { } index && index != group.Panes.IndexOf(pane) && _workspace.Move(pane, target))
                {
                    Select(pane);
                    Announce(DockText.Format(DockLangResourceKind.AnnounceReordered, pane.ToString(), group.Panes.IndexOf(pane) + 1, group.Panes.Count));
                    RaiseLayoutChanged(DockLayoutChangeKind.Move, pane);
                }
                return;
            }
            Move(session.Lead, target);
            return;
        }
        MoveAll(session.Panes, session.Lead, target);
    }

    /// <summary>Docks several panes (a group, a floating window) at one target, keeping their order.</summary>
    private void MoveAll(IReadOnlyList<DockPane> panes, DockPane lead, DockTarget target)
    {
        var reference = target.Group?.ActivePane;
        _workspace.BeginUpdate();
        try
        {
            if (target is { Scope: DockScope.Group, Position: DockPosition.Center, Group: { } group })
            {
                var index = target.Index ?? group.Panes.Count;
                for (var i = 0; i < panes.Count; i++)
                {
                    _workspace.Move(panes[i], DockTarget.Into(group, index + i));
                }
            }
            else if (panes.Count > 0 && _workspace.Move(panes[0], target) && panes[0].Group is { } first)
            {
                for (var i = 1; i < panes.Count; i++)
                {
                    _workspace.Move(panes[i], DockTarget.Into(first));
                }
            }
        }
        finally
        {
            _workspace.EndUpdate();
        }
        if (lead.Group is { } landed)
        {
            landed.ActivePane = lead;
        }
        Select(lead);
        Announce(MoveAnnouncement(lead, target, reference));
        RaiseLayoutChanged(DockLayoutChangeKind.Move, lead);
    }

    #endregion
}
