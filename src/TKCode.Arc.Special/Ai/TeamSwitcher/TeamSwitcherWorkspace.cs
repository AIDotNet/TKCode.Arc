using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One workspace (organization, team, project or store) in a <see cref="TeamSwitcher"/>: its id and name, and optionally
/// its plan, member count, your role in it and a logo (an image, or a mark on a brand color). Without a logo its tile
/// shows the first letter of the name in a tinted square.
/// </summary>
public sealed class TeamSwitcherWorkspace : AvaloniaObject
{
    /// <summary>The workspace's id; the switcher's <see cref="TeamSwitcher.Value"/> and its de-duplication use it.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<TeamSwitcherWorkspace, string>(nameof(Id), string.Empty);

    /// <summary>The name people know it by.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<TeamSwitcherWorkspace, string>(nameof(Name), string.Empty);

    /// <summary>The plan it is on ("Pro", "Enterprise"), shown under the name.</summary>
    public static readonly StyledProperty<string?> PlanProperty =
        AvaloniaProperty.Register<TeamSwitcherWorkspace, string?>(nameof(Plan));

    /// <summary>How many people belong to it, shown after the plan; null hides the count.</summary>
    public static readonly StyledProperty<int?> MembersProperty =
        AvaloniaProperty.Register<TeamSwitcherWorkspace, int?>(nameof(Members));

    /// <summary>Your role in it ("Owner", "Admin", "Member"), shown at the end of its row.</summary>
    public static readonly StyledProperty<string?> RoleProperty =
        AvaloniaProperty.Register<TeamSwitcherWorkspace, string?>(nameof(Role));

    /// <summary>
    /// The brand mark drawn in the tile instead of the first letter: a filled geometry in a 24 × 24 box, scaled with the
    /// tile. Null shows the letter.
    /// </summary>
    public static readonly StyledProperty<Geometry?> LogoProperty =
        AvaloniaProperty.Register<TeamSwitcherWorkspace, Geometry?>(nameof(Logo));

    /// <summary>
    /// An app-supplied logo image (a bitmap or a vector <c>DrawingImage</c>) drawn in the tile instead of
    /// <see cref="Logo"/> or the letter: it fills the tile, cropped to a square from its center and clipped to the tile's
    /// corners. Null falls back to <see cref="Logo"/>, then to the letter.
    /// </summary>
    public static readonly StyledProperty<IImage?> LogoImageProperty =
        AvaloniaProperty.Register<TeamSwitcherWorkspace, IImage?>(nameof(LogoImage));

    /// <summary>The tile's brand color. Null tints the tile from the theme's accent and series colors, picked by the name.</summary>
    public static readonly StyledProperty<IBrush?> TileBackgroundProperty =
        AvaloniaProperty.Register<TeamSwitcherWorkspace, IBrush?>(nameof(TileBackground));

    /// <summary>
    /// The color of the letter or mark on <see cref="TileBackground"/>. Null picks white or near-black, whichever reads
    /// better on that color, in both themes.
    /// </summary>
    public static readonly StyledProperty<IBrush?> TileForegroundProperty =
        AvaloniaProperty.Register<TeamSwitcherWorkspace, IBrush?>(nameof(TileForeground));

    /// <summary>Creates an empty workspace (set at least <see cref="Id"/> and <see cref="Name"/>).</summary>
    public TeamSwitcherWorkspace()
    {
    }

    /// <summary>Creates a workspace with its id, name and the optional details.</summary>
    public TeamSwitcherWorkspace(string id, string name, string? plan = null, int? members = null, string? role = null)
    {
        Id = id;
        Name = name;
        Plan = plan;
        Members = members;
        Role = role;
    }

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="PlanProperty"/>
    public string? Plan { get => GetValue(PlanProperty); set => SetValue(PlanProperty, value); }

    /// <inheritdoc cref="MembersProperty"/>
    public int? Members { get => GetValue(MembersProperty); set => SetValue(MembersProperty, value); }

    /// <inheritdoc cref="RoleProperty"/>
    public string? Role { get => GetValue(RoleProperty); set => SetValue(RoleProperty, value); }

    /// <inheritdoc cref="LogoProperty"/>
    public Geometry? Logo { get => GetValue(LogoProperty); set => SetValue(LogoProperty, value); }

    /// <inheritdoc cref="LogoImageProperty"/>
    public IImage? LogoImage { get => GetValue(LogoImageProperty); set => SetValue(LogoImageProperty, value); }

    /// <inheritdoc cref="TileBackgroundProperty"/>
    public IBrush? TileBackground { get => GetValue(TileBackgroundProperty); set => SetValue(TileBackgroundProperty, value); }

    /// <inheritdoc cref="TileForegroundProperty"/>
    public IBrush? TileForeground { get => GetValue(TileForegroundProperty); set => SetValue(TileForegroundProperty, value); }
}

/// <summary>Arguments of <see cref="TeamSwitcher.ValueChanged"/>: the workspace that is now active.</summary>
public sealed class TeamSwitcherValueChangedEventArgs : RoutedEventArgs
{
    internal TeamSwitcherValueChangedEventArgs(RoutedEvent routedEvent, TeamSwitcherWorkspace workspace, TeamSwitcherWorkspace? previous)
        : base(routedEvent)
    {
        Workspace = workspace;
        Previous = previous;
    }

    /// <summary>The id of the workspace that is now active.</summary>
    public string Id => Workspace.Id;

    /// <summary>The workspace that is now active.</summary>
    public TeamSwitcherWorkspace Workspace { get; }

    /// <summary>The workspace that was active before, if any.</summary>
    public TeamSwitcherWorkspace? Previous { get; }
}

/// <summary>Arguments of <see cref="TeamSwitcher.SwitchFailed"/>: the workspace that could not load and why.</summary>
public sealed class TeamSwitcherSwitchFailedEventArgs : RoutedEventArgs
{
    internal TeamSwitcherSwitchFailedEventArgs(RoutedEvent routedEvent, TeamSwitcherWorkspace workspace, Exception error)
        : base(routedEvent)
    {
        Workspace = workspace;
        Error = error;
    }

    /// <summary>The workspace the switch was going to.</summary>
    public TeamSwitcherWorkspace Workspace { get; }

    /// <summary>What the switch delegate threw; its message is shown on the trigger.</summary>
    public Exception Error { get; }
}

/// <summary>How a workspace joined the list of a <see cref="TeamSwitcher"/>.</summary>
public enum TeamSwitcherAddKind
{
    /// <summary>Made with Create workspace.</summary>
    Created,

    /// <summary>Joined with an invite link or code.</summary>
    Joined
}

/// <summary>
/// Arguments of <see cref="TeamSwitcher.WorkspaceAdded"/>: a workspace someone created or joined from the menu. Add it to
/// your own list as well; the switcher de-duplicates by id.
/// </summary>
public sealed class TeamSwitcherWorkspaceAddedEventArgs : RoutedEventArgs
{
    internal TeamSwitcherWorkspaceAddedEventArgs(RoutedEvent routedEvent, TeamSwitcherWorkspace workspace, TeamSwitcherAddKind kind,
        bool isSimulated)
        : base(routedEvent)
    {
        Workspace = workspace;
        Kind = kind;
        IsSimulated = isSimulated;
    }

    /// <summary>The new workspace.</summary>
    public TeamSwitcherWorkspace Workspace { get; }

    /// <summary>Whether it was created or joined.</summary>
    public TeamSwitcherAddKind Kind { get; }

    /// <summary>True when the switcher's built-in sample made it (no delegate was set, nothing was sent).</summary>
    public bool IsSimulated { get; }
}

/// <summary>Arguments of <see cref="TeamSwitcher.OpenChanged"/>.</summary>
public sealed class TeamSwitcherOpenChangedEventArgs : RoutedEventArgs
{
    internal TeamSwitcherOpenChangedEventArgs(RoutedEvent routedEvent, bool isOpen)
        : base(routedEvent)
    {
        IsOpen = isOpen;
    }

    /// <summary>True when the menu opened, false when it closed.</summary>
    public bool IsOpen { get; }
}
