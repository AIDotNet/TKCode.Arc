using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// A floating window (or, where the platform has no windows, a floating card) and the dock tree inside it. Listed in
/// <see cref="DockWorkspace.Floats"/>; saved and restored with the layout.
/// </summary>
public sealed class DockFloat : AvaloniaObject
{
    /// <summary>The dock tree shown in the window: a group, or a split of groups.</summary>
    public static readonly DirectProperty<DockFloat, DockNode?> RootProperty =
        AvaloniaProperty.RegisterDirect<DockFloat, DockNode?>(nameof(Root), o => o.Root, (o, v) => o.Root = v);

    /// <summary>
    /// The window's place and size in DIPs, relative to the work area of the main window's screen (for floating cards,
    /// relative to the <see cref="DockLayout"/>).
    /// </summary>
    public static readonly StyledProperty<Rect> BoundsProperty =
        AvaloniaProperty.Register<DockFloat, Rect>(nameof(Bounds));

    /// <summary>Whether the window is maximized.</summary>
    public static readonly StyledProperty<bool> IsMaximizedProperty =
        AvaloniaProperty.Register<DockFloat, bool>(nameof(IsMaximized));

    private DockNode? _root;

    public DockFloat()
    {
    }

    /// <summary>Creates a floating window holding <paramref name="root"/> at <paramref name="bounds"/>.</summary>
    public DockFloat(DockNode root, Rect bounds)
    {
        Root = root;
        Bounds = bounds;
    }

    /// <inheritdoc cref="RootProperty"/>
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
                if (value is DockDocumentArea)
                {
                    throw new InvalidOperationException("The document area cannot float.");
                }
                Workspace?.ValidateIncoming(value);
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
            Workspace?.NotifyStructure(removal: old is not null);
        }
    }

    /// <inheritdoc cref="BoundsProperty"/>
    public Rect Bounds { get => GetValue(BoundsProperty); set => SetValue(BoundsProperty, value); }

    /// <inheritdoc cref="IsMaximizedProperty"/>
    public bool IsMaximized { get => GetValue(IsMaximizedProperty); set => SetValue(IsMaximizedProperty, value); }

    /// <summary>The workspace whose <see cref="DockWorkspace.Floats"/> list holds this window.</summary>
    public DockWorkspace? Workspace { get; internal set; }
}
