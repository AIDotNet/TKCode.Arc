using Avalonia;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// The central area that holds the documents: one <see cref="DockGroup"/>, or a split of document groups (side-by-side
/// editors). Documents stay inside it; tools enter it only with <see cref="DockPane.CanDockAsDocument"/>. A workspace has
/// at most one, and it is never removed, even when it is empty (it then shows <see cref="DockLayout.EmptyDocumentContent"/>).
/// </summary>
public sealed class DockDocumentArea : DockNode
{
    /// <summary>The document group or split of document groups; null when no document is open.</summary>
    public static readonly DirectProperty<DockDocumentArea, DockNode?> RootProperty =
        AvaloniaProperty.RegisterDirect<DockDocumentArea, DockNode?>(nameof(Root), o => o.Root, (o, v) => o.Root = v);

    private DockNode? _root;

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
                ValidateChild(value);
            }
            var old = _root;
            if (old is not null && ReferenceEquals(old.Parent, this))
            {
                old.SetParent(null);
            }
            value?.SetParent(this);
            SetAndRaise(RootProperty, ref _root, value);
            NotifyStructure(removal: old is not null);
        }
    }

    internal override IEnumerable<DockNode> ChildNodes => _root is null ? [] : [_root];
}
