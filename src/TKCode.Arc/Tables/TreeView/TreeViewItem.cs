using Avalonia;
using Avalonia.Collections;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A node written inline in a <see cref="TreeView"/> (Arc's <c>TreeNode</c>): a label, optional icon and child
/// nodes. A node with children is a folder. Not a control: the tree draws its rows itself (virtualized), so
/// nodes stay cheap however many there are.
/// <code>
/// &lt;arc:TreeView&gt;
///   &lt;arc:TreeViewItem Header="src" IsExpanded="True"&gt;
///     &lt;arc:TreeViewItem Header="app.tsx" /&gt;
///   &lt;/arc:TreeViewItem&gt;
/// &lt;/arc:TreeView&gt;
/// </code>
/// </summary>
public class TreeViewItem : StyledElement
{
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<TreeViewItem, string?>(nameof(Header));

    /// <summary>Optional 24x24 stroked glyph (for example an <see cref="ArcIcons"/> member) replacing the folder / file icon.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<TreeViewItem, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<TreeViewItem, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    private readonly AvaloniaList<TreeViewItem> _items = new();

    public TreeViewItem()
    {
        _items.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is not null)
            {
                foreach (TreeViewItem item in e.OldItems)
                {
                    LogicalChildren.Remove(item);
                }
            }
            if (e.NewItems is not null)
            {
                foreach (TreeViewItem item in e.NewItems)
                {
                    LogicalChildren.Add(item);
                }
            }
            NotifyOwner(structure: true);
        };
    }

    /// <summary>Label of the row.</summary>
    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Whether the folder is open (two-way; the tree keeps it in sync).</summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>Child nodes.</summary>
    [Content]
    public AvaloniaList<TreeViewItem> Items => _items;

    /// <summary>Any value the application wants to carry (for example a path or id).</summary>
    public object? Value { get; set; }

    public override string ToString() => Header ?? string.Empty;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
        {
            NotifyOwner(structure: true, expansion: true);
        }
        else if (change.Property == HeaderProperty || change.Property == IconProperty)
        {
            NotifyOwner(structure: false);
        }
    }

    private void NotifyOwner(bool structure, bool expansion = false)
    {
        ILogical? current = this;
        while (current is not null)
        {
            if (current is TreeView tree)
            {
                tree.OnInlineItemChanged(this, structure, expansion);
                return;
            }
            current = current.LogicalParent;
        }
    }
}
