using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Layout;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// Places its <see cref="Children"/> side by side (<see cref="Orientation.Horizontal"/>) or one above the other
/// (<see cref="Orientation.Vertical"/>), with draggable dividers between them. Each child's <see cref="DockNode.Size"/>
/// is its share. A split left with one child is replaced by that child; a split inside a split of the same orientation
/// is merged into it.
/// </summary>
public sealed class DockSplit : DockNode
{
    /// <summary>Side by side (Horizontal, the default) or one above the other (Vertical).</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<DockSplit, Orientation>(nameof(Orientation));

    public DockSplit()
    {
        Children = new AvaloniaList<DockNode> { ResetBehavior = ResetBehavior.Remove };
        Children.Validate = ValidateChild;
        Children.CollectionChanged += OnChildrenChanged;
    }

    /// <summary>Creates a split with the given orientation and children.</summary>
    public DockSplit(Orientation orientation, params DockNode[] children) : this()
    {
        Orientation = orientation;
        Children.AddRange(children);
    }

    /// <inheritdoc cref="OrientationProperty"/>
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>The nodes of the split, from left to right or top to bottom.</summary>
    [Content]
    public AvaloniaList<DockNode> Children { get; }

    internal override IEnumerable<DockNode> ChildNodes => Children;

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var removal = false;
        if (e.OldItems is { } old)
        {
            foreach (DockNode node in old)
            {
                if (ReferenceEquals(node.Parent, this) && !Children.Contains(node))
                {
                    node.SetParent(null);
                }
                removal = true;
            }
        }
        if (e.NewItems is { } added)
        {
            foreach (DockNode node in added)
            {
                node.SetParent(this);
            }
        }
        NotifyStructure(removal);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OrientationProperty)
        {
            NotifyStructure(removal: true);
        }
    }
}
