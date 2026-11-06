using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// A node of a dock layout tree: a <see cref="DockSplit"/>, a <see cref="DockGroup"/> or the <see cref="DockDocumentArea"/>.
/// Its <see cref="Size"/> is its share of the parent split, kept in step with the split's divider positions.
/// </summary>
public abstract class DockNode : AvaloniaObject
{
    /// <summary>Default <see cref="MinSize"/> in px (self-designed).</summary>
    public const double DefaultMinSize = 120;

    /// <summary>
    /// The node's share of its parent split, relative to its siblings (shares need not add up to 100). Updated when a
    /// divider is dragged; setting it moves the divider.
    /// </summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<DockNode, double>(nameof(Size), 1);

    /// <summary>Smallest size in px along the parent split's direction (default 120).</summary>
    public static readonly StyledProperty<double> MinSizeProperty =
        AvaloniaProperty.Register<DockNode, double>(nameof(MinSize), DefaultMinSize);

    /// <inheritdoc cref="SizeProperty"/>
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc cref="MinSizeProperty"/>
    public double MinSize { get => GetValue(MinSizeProperty); set => SetValue(MinSizeProperty, value); }

    /// <summary>The split or document area that holds this node; null for a root.</summary>
    public DockNode? Parent { get; private set; }

    /// <summary>The workspace this node belongs to (through its root), or null while it is not part of one.</summary>
    public DockWorkspace? Workspace => RootOwner switch
    {
        DockWorkspace workspace => workspace,
        DockFloat dock => dock.Workspace,
        _ => null
    };

    /// <summary>The floating window that holds this node, or null.</summary>
    internal DockFloat? Float => RootOwner as DockFloat;

    /// <summary>The topmost node of this node's tree.</summary>
    internal DockNode Top
    {
        get
        {
            var node = this;
            while (node.Parent is { } parent)
            {
                node = parent;
            }
            return node;
        }
    }

    /// <summary>The workspace or floating window holding the tree as its root, or null.</summary>
    internal object? RootOwner => Top.Owner;

    /// <summary>Set on a root node: the <see cref="DockWorkspace"/> or <see cref="DockFloat"/> holding it.</summary>
    internal object? Owner { get; set; }

    /// <summary>The view a <see cref="DockLayout"/> made for this node (reused when the node moves).</summary>
    internal Control? View { get; set; }

    /// <summary>True when this node is inside the document area (or is it).</summary>
    internal bool IsInDocumentArea
    {
        get
        {
            for (var node = this; node is not null; node = node.Parent)
            {
                if (node is DockDocumentArea)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>The nodes directly below this one.</summary>
    internal abstract IEnumerable<DockNode> ChildNodes { get; }

    /// <summary>This node and every node below it, depth first.</summary>
    internal IEnumerable<DockNode> SelfAndDescendants()
    {
        yield return this;
        foreach (var child in ChildNodes)
        {
            foreach (var node in child.SelfAndDescendants())
            {
                yield return node;
            }
        }
    }

    /// <summary>Every pane in this subtree, in tree order.</summary>
    internal IEnumerable<DockPane> AllPanes()
    {
        foreach (var node in SelfAndDescendants())
        {
            if (node is DockGroup group)
            {
                foreach (var pane in group.Panes)
                {
                    yield return pane;
                }
            }
        }
    }

    internal void SetParent(DockNode? parent) => Parent = parent;

    /// <summary>Throws when <paramref name="child"/> cannot become a child of this node.</summary>
    internal void ValidateChild(DockNode child)
    {
        if (child.Parent is not null || child.Owner is not null)
        {
            throw new InvalidOperationException("The dock node already has a parent; remove it from its split, document area or workspace first.");
        }
        for (var node = (DockNode?)this; node is not null; node = node.Parent)
        {
            if (ReferenceEquals(node, child))
            {
                throw new InvalidOperationException("A dock node cannot contain itself.");
            }
        }
        if (child is DockDocumentArea && IsInDocumentArea)
        {
            throw new InvalidOperationException("A document area cannot be placed inside a document area.");
        }
        Workspace?.ValidateIncoming(child);
    }

    /// <summary>Tells the workspace the structure changed (<paramref name="removal"/>: something left, so tidy up).</summary>
    internal void NotifyStructure(bool removal) => Workspace?.NotifyStructure(removal);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty || change.Property == MinSizeProperty)
        {
            SizeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Raised when <see cref="Size"/> or <see cref="MinSize"/> changes (the split view follows).</summary>
    internal event EventHandler? SizeChanged;
}
