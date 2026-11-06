using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One dockable pane of a <see cref="DockLayout"/>: a tool (with a <see cref="PreferredSide"/>) or a document (without
/// one). The pane is its own content host: when it moves between groups it is the same instance under a new parent, so
/// its content keeps its state (scroll position, input, selection). Only the active pane of a group is in the visual tree.
/// </summary>
[PseudoClasses(ActivePseudoClass, FloatingPseudoClass, AutoHiddenPseudoClass)]
public partial class DockPane : ContentControl
{
    public const string ActivePseudoClass = ":active";
    public const string FloatingPseudoClass = ":floating";
    public const string AutoHiddenPseudoClass = ":auto-hidden";

    /// <summary>Default <see cref="PreferredSize"/> in px (self-designed).</summary>
    public const double DefaultPreferredSize = 280;

    /// <summary>Stable id, unique within a workspace; saved layouts refer to panes by it. Required.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<DockPane, string>(nameof(Id), string.Empty);

    /// <summary>The text of the pane's tab, menus and accessible name.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<DockPane, string>(nameof(Title), string.Empty);

    /// <summary>A 24×24 Lucide stroke path shown before the title in the tab (for example <c>{x:Static arc:ArcIcons.Folder}</c>); none by default.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<DockPane, Geometry?>(nameof(Icon));

    /// <summary>Whether the user can close the pane (tab close button, menu, keyboard). Default true.</summary>
    public static readonly StyledProperty<bool> CanCloseProperty =
        AvaloniaProperty.Register<DockPane, bool>(nameof(CanClose), true);

    /// <summary>Whether the pane can move to a floating window. Default true.</summary>
    public static readonly StyledProperty<bool> CanFloatProperty =
        AvaloniaProperty.Register<DockPane, bool>(nameof(CanFloat), true);

    /// <summary>Whether a tool pane can be auto-hidden to an edge strip (documents never are). Default true.</summary>
    public static readonly StyledProperty<bool> CanAutoHideProperty =
        AvaloniaProperty.Register<DockPane, bool>(nameof(CanAutoHide), true);

    /// <summary>Whether the user can move the pane (drag, Move to and Dock to commands, keyboard reordering). Default true.</summary>
    public static readonly StyledProperty<bool> CanMoveProperty =
        AvaloniaProperty.Register<DockPane, bool>(nameof(CanMove), true);

    /// <summary>Whether a tool pane may be placed inside the document area. Default false.</summary>
    public static readonly StyledProperty<bool> CanDockAsDocumentProperty =
        AvaloniaProperty.Register<DockPane, bool>(nameof(CanDockAsDocument));

    /// <summary>Shows the document tab's modified dot (unsaved changes).</summary>
    public static readonly StyledProperty<bool> IsModifiedProperty =
        AvaloniaProperty.Register<DockPane, bool>(nameof(IsModified));

    /// <summary>
    /// The edge a tool pane belongs to (null for a document): used when it is first placed, when it is opened without a
    /// target and when a restored layout does not mention it.
    /// </summary>
    public static readonly StyledProperty<DockSide?> PreferredSideProperty =
        AvaloniaProperty.Register<DockPane, DockSide?>(nameof(PreferredSide));

    /// <summary>The width (left and right) or height (top and bottom) in px a tool pane asks for when it is placed on its own along an edge. Default 280.</summary>
    public static readonly StyledProperty<double> PreferredSizeProperty =
        AvaloniaProperty.Register<DockPane, double>(nameof(PreferredSize), DefaultPreferredSize);

    /// <summary>The group the pane is in; null while it is auto-hidden or not in a layout.</summary>
    public static readonly DirectProperty<DockPane, DockGroup?> GroupProperty =
        AvaloniaProperty.RegisterDirect<DockPane, DockGroup?>(nameof(Group), o => o.Group);

    /// <summary>Where the pane is: docked, auto-hidden or floating.</summary>
    public static readonly DirectProperty<DockPane, DockPaneState> StateProperty =
        AvaloniaProperty.RegisterDirect<DockPane, DockPaneState>(nameof(State), o => o.State);

    /// <summary>False while the pane is auto-hidden.</summary>
    public static readonly DirectProperty<DockPane, bool> IsPinnedProperty =
        AvaloniaProperty.RegisterDirect<DockPane, bool>(nameof(IsPinned), o => o.IsPinned);

    private DockGroup? _group;
    private DockPaneState _state;
    private bool _isPinned = true;

    public DockPane()
    {
    }

    /// <summary>Creates a pane with an id, a title and optional content.</summary>
    public DockPane(string id, string title, object? content = null)
    {
        Id = id;
        Title = title;
        Content = content;
    }

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <inheritdoc cref="CanCloseProperty"/>
    public bool CanClose { get => GetValue(CanCloseProperty); set => SetValue(CanCloseProperty, value); }

    /// <inheritdoc cref="CanFloatProperty"/>
    public bool CanFloat { get => GetValue(CanFloatProperty); set => SetValue(CanFloatProperty, value); }

    /// <inheritdoc cref="CanAutoHideProperty"/>
    public bool CanAutoHide { get => GetValue(CanAutoHideProperty); set => SetValue(CanAutoHideProperty, value); }

    /// <inheritdoc cref="CanMoveProperty"/>
    public bool CanMove { get => GetValue(CanMoveProperty); set => SetValue(CanMoveProperty, value); }

    /// <inheritdoc cref="CanDockAsDocumentProperty"/>
    public bool CanDockAsDocument { get => GetValue(CanDockAsDocumentProperty); set => SetValue(CanDockAsDocumentProperty, value); }

    /// <inheritdoc cref="IsModifiedProperty"/>
    public bool IsModified { get => GetValue(IsModifiedProperty); set => SetValue(IsModifiedProperty, value); }

    /// <inheritdoc cref="PreferredSideProperty"/>
    public DockSide? PreferredSide { get => GetValue(PreferredSideProperty); set => SetValue(PreferredSideProperty, value); }

    /// <inheritdoc cref="PreferredSizeProperty"/>
    public double PreferredSize { get => GetValue(PreferredSizeProperty); set => SetValue(PreferredSizeProperty, value); }

    /// <inheritdoc cref="GroupProperty"/>
    public DockGroup? Group
    {
        get => _group;
        private set => SetAndRaise(GroupProperty, ref _group, value);
    }

    /// <inheritdoc cref="StateProperty"/>
    public DockPaneState State
    {
        get => _state;
        private set => SetAndRaise(StateProperty, ref _state, value);
    }

    /// <inheritdoc cref="IsPinnedProperty"/>
    public bool IsPinned
    {
        get => _isPinned;
        private set => SetAndRaise(IsPinnedProperty, ref _isPinned, value);
    }

    /// <summary>True for a document (a pane without a <see cref="PreferredSide"/>).</summary>
    internal bool IsDocument => PreferredSide is null;

    /// <summary>The group, or the workspace whose auto-hide list holds the pane; null when the pane is not in a layout.</summary>
    internal object? Container { get; private set; }

    /// <summary>The auto-hide strip the pane is on, or null.</summary>
    internal DockSide? HiddenSide { get; private set; }

    /// <summary>The workspace the pane belongs to, or null.</summary>
    internal DockWorkspace? Workspace => Container switch
    {
        DockGroup group => group.Workspace,
        DockWorkspace workspace => workspace,
        _ => null
    };

    internal void SetContainer(object? container, DockSide? hiddenSide)
    {
        Container = container;
        HiddenSide = hiddenSide;
        Group = container as DockGroup;
        RefreshState();
    }

    /// <summary>Recomputes <see cref="State"/> and <see cref="IsPinned"/> from where the pane is now.</summary>
    internal void RefreshState()
    {
        State = HiddenSide is not null ? DockPaneState.AutoHidden
            : _group?.Float is not null ? DockPaneState.Floating
            : DockPaneState.Docked;
        IsPinned = State != DockPaneState.AutoHidden;
        PseudoClasses.Set(FloatingPseudoClass, State == DockPaneState.Floating);
        PseudoClasses.Set(AutoHiddenPseudoClass, State == DockPaneState.AutoHidden);
    }

    internal void SetIsActive(bool active) => PseudoClasses.Set(ActivePseudoClass, active);

    /// <summary>The preferred size, or the theme's default when it is not a positive number.</summary>
    internal double EffectivePreferredSize => PreferredSize is > 0 and < double.PositiveInfinity ? PreferredSize : DefaultPreferredSize;

    public override string ToString() => string.IsNullOrEmpty(Title) ? Id : Title;
}
