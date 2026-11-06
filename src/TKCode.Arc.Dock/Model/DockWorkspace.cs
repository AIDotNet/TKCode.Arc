using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Layout;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// The model of a dock layout: the main tree (<see cref="Root"/>), the panes auto-hidden on each edge and the floating
/// windows. The workspace keeps the tree tidy: a group whose last pane leaves is removed, a split left with one child is
/// replaced by that child (which takes over its share), a split inside a split of the same orientation is merged into
/// it, and the <see cref="DockDocumentArea"/> is never removed. Change the model on the UI thread only.
/// </summary>
public sealed partial class DockWorkspace : AvaloniaObject
{
    /// <summary>Share of a new group docked to an edge of the whole layout (a quarter).</summary>
    internal const double EdgeShare = 0.25;

    /// <summary>Share of a new group split off a group (half of it).</summary>
    internal const double SplitShare = 0.5;

    /// <summary>The main layout tree.</summary>
    public static readonly DirectProperty<DockWorkspace, DockNode?> RootProperty =
        AvaloniaProperty.RegisterDirect<DockWorkspace, DockNode?>(nameof(Root), o => o.Root, (o, v) => o.Root = v);

    private readonly Dictionary<string, DockPane> _known = new(StringComparer.Ordinal);
    private DockNode? _root;
    private int _batch;
    private bool _changed;
    private bool _tidyPending;
    private bool _tidying;
    private DockGroup? _recentDocumentGroup;

    public DockWorkspace()
    {
        LeftHidden = CreateHiddenList(DockSide.Left);
        RightHidden = CreateHiddenList(DockSide.Right);
        TopHidden = CreateHiddenList(DockSide.Top);
        BottomHidden = CreateHiddenList(DockSide.Bottom);
        Floats = new AvaloniaList<DockFloat> { ResetBehavior = ResetBehavior.Remove };
        Floats.Validate = ValidateFloat;
        Floats.CollectionChanged += OnFloatsChanged;
    }

    /// <inheritdoc cref="RootProperty"/>
    [Content]
    public DockNode? Root
    {
        get => _root;
        set
        {
            if (ReferenceEquals(value, _root))
            {
                return;
            }
            if (value is not null)
            {
                if (value.Parent is not null || value.Owner is not null)
                {
                    throw new InvalidOperationException("The dock node already has a parent; remove it first.");
                }
                ValidateIncoming(value, replacing: _root);
            }
            var old = _root;
            if (old is not null && ReferenceEquals(old.Owner, this))
            {
                old.Owner = null;
            }
            if (value is not null)
            {
                value.Owner = this;
            }
            SetAndRaise(RootProperty, ref _root, value);
            NotifyStructure(removal: old is not null);
        }
    }

    /// <summary>Panes auto-hidden on the left edge.</summary>
    public AvaloniaList<DockPane> LeftHidden { get; }

    /// <summary>Panes auto-hidden on the right edge.</summary>
    public AvaloniaList<DockPane> RightHidden { get; }

    /// <summary>Panes auto-hidden on the top edge.</summary>
    public AvaloniaList<DockPane> TopHidden { get; }

    /// <summary>Panes auto-hidden on the bottom edge.</summary>
    public AvaloniaList<DockPane> BottomHidden { get; }

    /// <summary>The floating windows.</summary>
    public AvaloniaList<DockFloat> Floats { get; }

    /// <summary>The auto-hidden panes of one edge.</summary>
    public AvaloniaList<DockPane> AutoHidden(DockSide side) => side switch
    {
        DockSide.Left => LeftHidden,
        DockSide.Right => RightHidden,
        DockSide.Top => TopHidden,
        _ => BottomHidden
    };

    /// <summary>Every pane: the main tree in order, then the auto-hidden ones (left, right, top, bottom), then the floating ones.</summary>
    public IEnumerable<DockPane> AllPanes()
    {
        if (_root is not null)
        {
            foreach (var pane in _root.AllPanes())
            {
                yield return pane;
            }
        }
        foreach (var side in s_sides)
        {
            foreach (var pane in AutoHidden(side))
            {
                yield return pane;
            }
        }
        foreach (var dock in Floats)
        {
            if (dock.Root is { } root)
            {
                foreach (var pane in root.AllPanes())
                {
                    yield return pane;
                }
            }
        }
    }

    /// <summary>The pane with this <see cref="DockPane.Id"/>, or null.</summary>
    public DockPane? FindPane(string id)
    {
        foreach (var pane in AllPanes())
        {
            if (string.Equals(pane.Id, id, StringComparison.Ordinal))
            {
                return pane;
            }
        }
        return null;
    }

    /// <summary>Raised after the structure changed (after the tidy-up); views and the layout follow it.</summary>
    internal event EventHandler? Changed;

    /// <summary>The document area of the main tree, or null.</summary>
    internal DockDocumentArea? DocumentArea =>
        _root?.SelfAndDescendants().OfType<DockDocumentArea>().FirstOrDefault();

    /// <summary>Every group: the main tree in order, then the floating windows.</summary>
    internal IEnumerable<DockGroup> Groups()
    {
        if (_root is not null)
        {
            foreach (var group in _root.SelfAndDescendants().OfType<DockGroup>())
            {
                yield return group;
            }
        }
        foreach (var dock in Floats)
        {
            if (dock.Root is { } root)
            {
                foreach (var group in root.SelfAndDescendants().OfType<DockGroup>())
                {
                    yield return group;
                }
            }
        }
    }

    /// <summary>
    /// Every pane this workspace has held, by id, including closed ones: a restored layout can bring back a pane that was
    /// declared in XAML and closed since.
    /// </summary>
    internal IReadOnlyDictionary<string, DockPane> KnownPanes => _known;

    /// <summary>The document group that last had an active pane; documents opened without a target go there.</summary>
    internal DockGroup? RecentDocumentGroup
    {
        get => _recentDocumentGroup is { IsDocumentGroup: true } group && ReferenceEquals(group.Workspace, this) && group.Float is null
            ? group
            : null;
        set => _recentDocumentGroup = value;
    }

    private static readonly DockSide[] s_sides = [DockSide.Left, DockSide.Right, DockSide.Top, DockSide.Bottom];

    #region Validation and change tracking

    private AvaloniaList<DockPane> CreateHiddenList(DockSide side)
    {
        var list = new AvaloniaList<DockPane> { ResetBehavior = ResetBehavior.Remove };
        list.Validate = pane =>
        {
            if (pane.Container is not null)
            {
                throw new InvalidOperationException($"The pane '{pane.Id}' is already in a group or on an auto-hide strip; remove it first.");
            }
            if (!pane.IsInitializing)
            {
                ValidateIncoming(pane);
            }
        };
        list.CollectionChanged += (_, e) => OnHiddenChanged(side, e);
        return list;
    }

    private void OnHiddenChanged(DockSide side, NotifyCollectionChangedEventArgs e)
    {
        var list = AutoHidden(side);
        var removal = false;
        if (e.OldItems is { } old)
        {
            foreach (DockPane pane in old)
            {
                if (ReferenceEquals(pane.Container, this) && pane.HiddenSide == side && !list.Contains(pane))
                {
                    pane.SetContainer(null, null);
                }
                removal = true;
            }
        }
        if (e.NewItems is { } added)
        {
            foreach (DockPane pane in added)
            {
                pane.SetContainer(this, side);
            }
        }
        NotifyStructure(removal);
    }

    private void ValidateFloat(DockFloat dock)
    {
        if (dock.Workspace is not null)
        {
            throw new InvalidOperationException("The floating window already belongs to a workspace.");
        }
        if (dock.Root is { } root)
        {
            ValidateIncoming(root);
        }
    }

    private void OnFloatsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var removal = false;
        if (e.OldItems is { } old)
        {
            foreach (DockFloat dock in old)
            {
                if (ReferenceEquals(dock.Workspace, this) && !Floats.Contains(dock))
                {
                    dock.Workspace = null;
                }
                removal = true;
            }
        }
        if (e.NewItems is { } added)
        {
            foreach (DockFloat dock in added)
            {
                dock.Workspace = this;
            }
        }
        NotifyStructure(removal);
    }

    /// <summary>Throws when a pane, or the panes and document area of a subtree, cannot join this workspace.</summary>
    internal void ValidateIncoming(object incoming, DockNode? replacing = null)
    {
        var panes = incoming switch
        {
            DockPane pane => [pane],
            DockNode node => node.AllPanes().ToList(),
            _ => []
        };
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pane in panes)
        {
            if (string.IsNullOrEmpty(pane.Id))
            {
                throw new InvalidOperationException($"Every DockPane needs an Id (pane '{pane.Title}').");
            }
            if (!ids.Add(pane.Id))
            {
                throw new InvalidOperationException($"Two panes have the Id '{pane.Id}'; pane ids must be unique in a workspace.");
            }
        }
        if (ids.Count == 0 && incoming is not DockNode)
        {
            return;
        }
        var replaced = replacing?.AllPanes().ToHashSet();
        foreach (var existing in AllPanes())
        {
            if (ids.Contains(existing.Id) && !panes.Contains(existing) && replaced?.Contains(existing) != true)
            {
                throw new InvalidOperationException($"A pane with the Id '{existing.Id}' is already in the workspace; pane ids must be unique.");
            }
        }
        if (incoming is DockNode subtree && subtree.SelfAndDescendants().OfType<DockDocumentArea>().Any())
        {
            var area = DocumentArea;
            if (area is not null && (replacing is null || !replacing.SelfAndDescendants().Contains(area)))
            {
                throw new InvalidOperationException("A workspace has at most one DockDocumentArea.");
            }
        }
    }

    /// <summary>Groups several changes: the tidy-up and <see cref="Changed"/> run once, at the end.</summary>
    internal void BeginUpdate() => _batch++;

    internal void EndUpdate()
    {
        if (--_batch == 0)
        {
            Flush();
        }
    }

    internal void NotifyStructure(bool removal)
    {
        _changed = true;
        if (removal && !_tidying)
        {
            _tidyPending = true;
        }
        if (_batch == 0)
        {
            Flush();
        }
    }

    /// <summary>Runs the tidy-up on the next flush even when nothing was removed (after loading a layout).</summary>
    internal void RequestTidy()
    {
        _tidyPending = true;
        _changed = true;
    }

    private void Flush()
    {
        if (_tidyPending)
        {
            _tidyPending = false;
            _tidying = true;
            _batch++;
            try
            {
                Tidy();
            }
            finally
            {
                _batch--;
                _tidying = false;
            }
        }
        if (!_changed)
        {
            return;
        }
        _changed = false;
        foreach (var group in Groups())
        {
            group.RefreshPlacement();
        }
        foreach (var pane in AllPanes())
        {
            pane.RefreshState();
            if (pane.Id.Length > 0)
            {
                _known[pane.Id] = pane;
            }
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Tidy-up

    /// <summary>Applies the structure rules to the whole workspace.</summary>
    internal void Tidy()
    {
        if (_root is not null)
        {
            var replacement = TidyNode(_root);
            if (!ReferenceEquals(replacement, _root))
            {
                Root = null;
                Root = replacement;
            }
        }
        foreach (var dock in Floats.ToList())
        {
            if (dock.Root is null)
            {
                Floats.Remove(dock);
                continue;
            }
            var replacement = TidyNode(dock.Root);
            if (replacement is null)
            {
                dock.Root = null;
                Floats.Remove(dock);
            }
            else if (!ReferenceEquals(replacement, dock.Root))
            {
                dock.Root = null;
                dock.Root = replacement;
            }
        }
    }

    /// <summary>
    /// Tidies a subtree and returns what should stand in its place: the node itself, another node (already detached from
    /// this one), or null to remove it.
    /// </summary>
    private static DockNode? TidyNode(DockNode node)
    {
        switch (node)
        {
            case DockGroup group:
                return group.Panes.Count == 0 ? null : group;
            case DockDocumentArea area:
                if (area.Root is { } inner)
                {
                    var replacement = TidyNode(inner);
                    if (!ReferenceEquals(replacement, inner))
                    {
                        area.Root = null;
                        area.Root = replacement;
                    }
                }
                return area;
            case DockSplit split:
                return TidySplit(split);
            default:
                return node;
        }
    }

    private static DockNode? TidySplit(DockSplit split)
    {
        for (var i = 0; i < split.Children.Count; i++)
        {
            var child = split.Children[i];
            var replacement = TidyNode(child);
            if (replacement is null)
            {
                split.Children.RemoveAt(i);
                i--;
            }
            else if (!ReferenceEquals(replacement, child))
            {
                replacement.Size = child.Size;
                split.Children[i] = replacement;
            }
        }
        // A split inside a split of the same orientation: its children take its place, sharing its share.
        for (var i = 0; i < split.Children.Count; i++)
        {
            if (split.Children[i] is DockSplit inner && inner.Orientation == split.Orientation && inner.Children.Count > 0)
            {
                var share = inner.Size;
                var total = inner.Children.Sum(c => Math.Max(0, c.Size));
                var moved = inner.Children.ToList();
                inner.Children.Clear();
                split.Children.RemoveAt(i);
                for (var k = 0; k < moved.Count; k++)
                {
                    moved[k].Size = total > 0 ? share * Math.Max(0, moved[k].Size) / total : share / moved.Count;
                    split.Children.Insert(i + k, moved[k]);
                }
                i += moved.Count - 1;
            }
        }
        if (split.Children.Count == 0)
        {
            return null;
        }
        if (split.Children.Count == 1)
        {
            var only = split.Children[0];
            split.Children.RemoveAt(0);
            only.Size = split.Size;
            return only;
        }
        return split;
    }

    #endregion

    #region Operations

    /// <summary>Whether <paramref name="pane"/> may go to <paramref name="target"/> (documents stay in the document area; tools enter it only with <see cref="DockPane.CanDockAsDocument"/>).</summary>
    internal bool CanPlace(DockPane pane, DockTarget target)
    {
        bool intoDocuments;
        switch (target.Scope)
        {
            case DockScope.Group:
                if (target.Group is not { } group || !ReferenceEquals(group.Workspace, this))
                {
                    return false;
                }
                if (group.Float is not null)
                {
                    return true;
                }
                intoDocuments = group.IsInDocumentArea;
                if (target.Position != DockPosition.Center && ReferenceEquals(pane.Group, group) && group.Panes.Count == 1)
                {
                    // Splitting a group off itself with its only pane changes nothing.
                    return false;
                }
                break;
            case DockScope.Workspace:
                intoDocuments = false;
                break;
            case DockScope.DocumentArea:
                if (DocumentArea is null)
                {
                    return false;
                }
                intoDocuments = target.Position == DockPosition.Center;
                break;
            default:
                return false;
        }
        if (pane.IsDocument)
        {
            return intoDocuments;
        }
        return !intoDocuments || pane.CanDockAsDocument;
    }

    /// <summary>Moves a pane of this workspace to <paramref name="target"/>; false when the move is not allowed.</summary>
    internal bool Move(DockPane pane, DockTarget target)
    {
        if (!ReferenceEquals(pane.Workspace, this) || !CanPlace(pane, target))
        {
            return false;
        }
        BeginUpdate();
        try
        {
            Place(pane, target);
        }
        finally
        {
            EndUpdate();
        }
        return true;
    }

    /// <summary>Adds a pane that is not in the workspace at <paramref name="target"/>; false when not allowed.</summary>
    internal bool Insert(DockPane pane, DockTarget target)
    {
        if (pane.Container is not null || !CanPlace(pane, target))
        {
            return false;
        }
        BeginUpdate();
        try
        {
            Place(pane, target);
        }
        finally
        {
            EndUpdate();
        }
        return true;
    }

    /// <summary>Takes a pane out of its group or auto-hide strip (the group is removed on the next tidy-up when it empties).</summary>
    internal void Detach(DockPane pane)
    {
        switch (pane.Container)
        {
            case DockGroup group:
                group.Panes.Remove(pane);
                break;
            case DockWorkspace workspace when pane.HiddenSide is { } side:
                workspace.AutoHidden(side).Remove(pane);
                break;
        }
    }

    /// <summary>Removes a pane from the workspace (closing it); the structure is tidied afterwards.</summary>
    internal bool Remove(DockPane pane)
    {
        if (!ReferenceEquals(pane.Workspace, this))
        {
            return false;
        }
        Detach(pane);
        return true;
    }

    private void Place(DockPane pane, DockTarget target)
    {
        switch (target.Scope)
        {
            case DockScope.Group when target.Position == DockPosition.Center:
            {
                var group = target.Group!;
                if (ReferenceEquals(pane.Group, group))
                {
                    var from = group.Panes.IndexOf(pane);
                    var to = Math.Clamp(target.Index ?? group.Panes.Count - 1, 0, group.Panes.Count - 1);
                    if (from != to)
                    {
                        group.Panes.Move(from, to);
                    }
                }
                else
                {
                    Detach(pane);
                    group.Panes.Insert(Math.Clamp(target.Index ?? group.Panes.Count, 0, group.Panes.Count), pane);
                }
                group.ActivePane = pane;
                break;
            }
            case DockScope.Group:
            {
                Detach(pane);
                InsertBeside(target.Group!, new DockGroup(pane), target.Position, SplitShare);
                break;
            }
            case DockScope.Workspace:
            {
                Detach(pane);
                InsertAtEdge(new DockGroup(pane), target.Position, EdgeShare);
                break;
            }
            case DockScope.DocumentArea when target.Position == DockPosition.Center:
            {
                Detach(pane);
                AddToDocuments(pane);
                break;
            }
            case DockScope.DocumentArea:
            {
                Detach(pane);
                InsertBeside(DocumentArea!, new DockGroup(pane), target.Position, SplitShare);
                break;
            }
        }
    }

    /// <summary>Adds a pane to the most recent document group, making the document area or its first group when needed.</summary>
    internal DockGroup AddToDocuments(DockPane pane)
    {
        var group = RecentDocumentGroup ?? DocumentArea?.Root?.SelfAndDescendants().OfType<DockGroup>().FirstOrDefault();
        if (group is null)
        {
            var area = DocumentArea;
            if (area is null)
            {
                area = new DockDocumentArea();
                if (_root is null)
                {
                    Root = area;
                }
                else
                {
                    InsertAtEdge(area, DockPosition.Right, 1 - EdgeShare);
                }
            }
            group = new DockGroup();
            if (area.Root is null)
            {
                area.Root = group;
            }
            else
            {
                InsertBeside(area.Root, group, DockPosition.Right, SplitShare);
            }
        }
        group.Panes.Add(pane);
        group.ActivePane = pane;
        return group;
    }

    /// <summary>
    /// Places a pane that is not in the workspace by its preference: a document joins the documents, a tool joins a group
    /// of tools with the same <see cref="DockPane.PreferredSide"/> or gets a new group along that edge, taking
    /// <paramref name="edgeShare"/> of the layout.
    /// </summary>
    internal DockGroup PlaceByPreference(DockPane pane, double edgeShare = EdgeShare)
    {
        BeginUpdate();
        try
        {
            if (pane.PreferredSide is not { } side)
            {
                return AddToDocuments(pane);
            }
            var existing = _root?.SelfAndDescendants().OfType<DockGroup>()
                .FirstOrDefault(g => !g.IsInDocumentArea && g.Panes.Any(p => p.PreferredSide == side));
            if (existing is not null)
            {
                existing.Panes.Add(pane);
                existing.ActivePane = pane;
                return existing;
            }
            var group = new DockGroup(pane);
            InsertAtEdge(group, PositionOf(side), Math.Clamp(edgeShare, 0.05, 0.6));
            return group;
        }
        finally
        {
            EndUpdate();
        }
    }

    internal static DockPosition PositionOf(DockSide side) => side switch
    {
        DockSide.Left => DockPosition.Left,
        DockSide.Right => DockPosition.Right,
        DockSide.Top => DockPosition.Top,
        _ => DockPosition.Bottom
    };

    private static Orientation OrientationOf(DockPosition position) =>
        position is DockPosition.Left or DockPosition.Right ? Orientation.Horizontal : Orientation.Vertical;

    private static bool IsBefore(DockPosition position) => position is DockPosition.Left or DockPosition.Top;

    /// <summary>Puts <paramref name="added"/> beside <paramref name="node"/>, taking <paramref name="share"/> of its room.</summary>
    private void InsertBeside(DockNode node, DockNode added, DockPosition position, double share)
    {
        var orientation = OrientationOf(position);
        var before = IsBefore(position);
        if (node.Parent is DockSplit parent && parent.Orientation == orientation)
        {
            var index = parent.Children.IndexOf(node);
            added.Size = node.Size * share;
            node.Size *= 1 - share;
            parent.Children.Insert(before ? index : index + 1, added);
            return;
        }
        var split = new DockSplit { Orientation = orientation, Size = node.Size };
        ReplaceNode(node, split);
        added.Size = share * 100;
        node.Size = (1 - share) * 100;
        split.Children.Add(before ? added : node);
        split.Children.Add(before ? node : added);
    }

    /// <summary>Puts <paramref name="added"/> along one edge of the whole main tree, taking <paramref name="share"/> of it.</summary>
    private void InsertAtEdge(DockNode added, DockPosition position, double share)
    {
        if (_root is null)
        {
            added.Size = 1;
            Root = added;
            return;
        }
        var orientation = OrientationOf(position);
        var before = IsBefore(position);
        if (_root is DockSplit split && split.Orientation == orientation)
        {
            var total = split.Children.Sum(c => Math.Max(0, c.Size));
            added.Size = total > 0 ? total * share / (1 - share) : share * 100;
            split.Children.Insert(before ? 0 : split.Children.Count, added);
            return;
        }
        var old = _root;
        var size = old.Size;
        var wrapper = new DockSplit { Orientation = orientation, Size = size };
        Root = null;
        added.Size = share * 100;
        old.Size = (1 - share) * 100;
        wrapper.Children.Add(before ? added : old);
        wrapper.Children.Add(before ? old : added);
        Root = wrapper;
    }

    /// <summary>Puts <paramref name="replacement"/> where <paramref name="node"/> is; <paramref name="node"/> ends up detached.</summary>
    private void ReplaceNode(DockNode node, DockNode replacement)
    {
        switch (node.Parent)
        {
            case DockSplit split:
                split.Children[split.Children.IndexOf(node)] = replacement;
                return;
            case DockDocumentArea area:
                area.Root = null;
                area.Root = replacement;
                return;
        }
        if (ReferenceEquals(node.Owner, this))
        {
            Root = null;
            Root = replacement;
        }
        else if (node.Owner is DockFloat dock)
        {
            dock.Root = null;
            dock.Root = replacement;
        }
    }

    /// <summary>Empties the workspace: every group gives up its panes, and the tree, strips and floating windows are cleared.</summary>
    internal void Clear()
    {
        BeginUpdate();
        try
        {
            foreach (var group in Groups().ToList())
            {
                group.Panes.Clear();
            }
            foreach (var side in s_sides)
            {
                AutoHidden(side).Clear();
            }
            foreach (var dock in Floats)
            {
                dock.Root = null;
            }
            Floats.Clear();
            Root = null;
        }
        finally
        {
            EndUpdate();
        }
    }

    #endregion
}
