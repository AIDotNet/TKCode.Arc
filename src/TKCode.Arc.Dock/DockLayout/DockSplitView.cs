using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// The view of a <see cref="DockSplit"/>: a <see cref="ResizablePanels"/> in the split's orientation with one
/// <see cref="ResizablePanel"/> per child, each holding the child's (reused) view. Dragging a divider writes the new
/// shares back to the children's <see cref="DockNode.Size"/>; setting a size moves the divider.
/// </summary>
internal sealed class DockSplitView : Decorator, IDockView
{
    private readonly Dictionary<DockNode, ResizablePanel> _wrappers = new();
    private List<DockNode> _shown = [];
    private bool _writing;

    public DockSplitView(DockLayout layout, DockSplit split)
    {
        Layout = layout;
        Split = split;
        Panels = new ResizablePanels { Orientation = split.Orientation };
        Panels.Bind(ResizablePanels.GapProperty, Panels.GetResourceObservable(DockLayoutTokens.Key(DockLayoutTokenKind.Gap)));
        Child = Panels;
        split.Children.CollectionChanged += OnChildrenChanged;
        split.PropertyChanged += OnSplitPropertyChanged;
        Panels.LayoutChanged += OnPanelsLayoutChanged;
        Sync();
    }

    /// <inheritdoc />
    public void Release()
    {
        Split.Children.CollectionChanged -= OnChildrenChanged;
        Split.PropertyChanged -= OnSplitPropertyChanged;
        Panels.LayoutChanged -= OnPanelsLayoutChanged;
        foreach (var node in _wrappers.Keys)
        {
            node.SizeChanged -= OnNodeSizeChanged;
        }
    }

    public DockLayout Layout { get; }

    public DockSplit Split { get; }

    /// <summary>The panels that hold the children (tests).</summary>
    internal ResizablePanels Panels { get; }

    /// <summary>The orientation the panels were asked to take.</summary>
    internal Orientation Orientation => Split.Orientation;

    /// <summary>The wrapper of a child (tests).</summary>
    internal ResizablePanel? WrapperOf(DockNode node) => _wrappers.GetValueOrDefault(node);

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e) => Sync();

    private void OnSplitPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == DockSplit.OrientationProperty)
        {
            Panels.Orientation = Split.Orientation;
        }
    }

    /// <summary>Follows the split's children: one wrapper per child, holding its view (also puts a view back after a group was maximized).</summary>
    internal void Sync()
    {
        var nodes = Split.Children.ToList();
        var removed = false;
        foreach (var node in _shown)
        {
            if (!nodes.Contains(node) && _wrappers.Remove(node, out var old))
            {
                node.SizeChanged -= OnNodeSizeChanged;
                if (old.Content is Control view && ReferenceEquals(view, node.View))
                {
                    old.Content = null;
                }
                // The panels hand the leaving pane's room to its neighbours, keeping the others' sizes.
                Panels.Panels.Remove(old);
                removed = true;
            }
        }
        var added = false;
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (!_wrappers.TryGetValue(node, out var wrapper))
            {
                wrapper = new ResizablePanel { DefaultSize = node.Size, MinSize = node.MinSize };
                _wrappers[node] = wrapper;
                node.SizeChanged += OnNodeSizeChanged;
            }
            var view = Layout.ViewFor(node);
            if (!ReferenceEquals(wrapper.Content, view))
            {
                DockLayout.Adopt(view);
                wrapper.Content = view;
            }
            wrapper.MinSize = node.MinSize;
            wrapper.DefaultSize = node.Size;
            var at = Panels.Panels.IndexOf(wrapper);
            if (at < 0)
            {
                Panels.Panels.Insert(Math.Min(i, Panels.Panels.Count), wrapper);
                added = true;
            }
            else if (at != i)
            {
                Panels.Panels.Move(at, i);
            }
        }
        _shown = nodes;
        _writing = true;
        try
        {
            if (added || !removed || Panels.Sizes is not { } shares || shares.Count != nodes.Count)
            {
                // New groups take the shares the model gave them (a split takes half, an edge a quarter).
                Panels.Sizes = nodes.Select(n => Math.Max(0, n.Size)).ToArray();
            }
            else
            {
                // Only removals: keep what the panels made of the freed room.
                for (var i = 0; i < nodes.Count; i++)
                {
                    nodes[i].Size = shares[i];
                }
            }
        }
        finally
        {
            _writing = false;
        }
    }

    private void OnNodeSizeChanged(object? sender, EventArgs e)
    {
        if (_writing || sender is not DockNode node || !_wrappers.TryGetValue(node, out var wrapper))
        {
            return;
        }
        wrapper.MinSize = node.MinSize;
        _writing = true;
        try
        {
            Panels.Sizes = _shown.Select(n => Math.Max(0, n.Size)).ToArray();
        }
        finally
        {
            _writing = false;
        }
    }

    private void OnPanelsLayoutChanged(object? sender, ResizablePanelsLayoutChangedEventArgs e)
    {
        // The event bubbles: only this split's own panels count.
        if (!ReferenceEquals(e.Source, Panels) || e.Sizes.Count != _shown.Count)
        {
            return;
        }
        _writing = true;
        try
        {
            for (var i = 0; i < _shown.Count; i++)
            {
                _shown[i].Size = e.Sizes[i];
            }
        }
        finally
        {
            _writing = false;
        }
        Layout.OnResized();
    }
}
