using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The anchored popup of a <see cref="TeamSwitcher"/>: Core <see cref="ArcPopup"/> (placement under the trigger, flips,
/// floating shadow, light dismiss) with the switcher's own curves. ArcPopup enters in 160ms and leaves in 120ms from 97%;
/// the switcher's menu fades and scales in from 98% over 180ms and leaves in 120ms, with no travel, so the motions are
/// replaced after ArcPopup configures its own (on every open and motion level change). Reduced motion: fades only.
/// </summary>
internal sealed class TeamSwitcherPopup : ArcPopup
{
    internal static readonly TimeSpan EnterDuration = TimeSpan.FromMilliseconds(180);
    internal static readonly TimeSpan ExitDuration = TimeSpan.FromMilliseconds(120);
    internal const double FromScale = 0.98;

    public TeamSwitcherPopup()
    {
        WindowManagerAddShadowHint = false;
        ApplyMotions();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PlacementProperty || change.Property == EnterOffsetProperty ||
            change.Property == EnterScaleProperty || change.Property == IsInstantProperty ||
            change.Property == ArcMotion.LevelProperty || change.Property == PlacementTargetProperty ||
            (change.Property == IsOpenProperty && change.GetNewValue<bool>()))
        {
            ApplyMotions();
        }
    }

    private void ApplyMotions()
    {
        var level = PlacementTarget is { } target ? ArcMotion.GetEffectiveLevel(target) : ArcMotion.GetEffectiveLevel(this);
        var reduced = level == ArcMotionLevel.Reduced;
        var origin = new RelativePoint(0.5, 0, RelativeUnit.Relative);
        OpenMotion = new ArcPopupEnterMotion(origin, default, FromScale, reduced,
            reduced ? ArcMotionPresets.Instant : EnterDuration, ArcEasings.Enter);
        CloseMotion = new ArcPopupExitMotion(origin, FromScale, reduced, ExitDuration);
    }
}

/// <summary>
/// The switcher's trigger: a button (Arc's <c>aria-haspopup="dialog"</c>) that reports whether the menu is open, with a
/// SurfaceMuted fill under the pointer and a keyboard focus ring.
/// </summary>
internal sealed class TeamSwitcherTrigger : ToolCallDisclosureButton
{
    protected override Type StyleKeyOverride => typeof(TeamSwitcherTrigger);
}

/// <summary>
/// What the trigger shows: the workspace's tile, its name and the plan line, each in its own crossfade (Arc's label swap:
/// the old part fades out over 100ms, the new one fades in over 160ms rising 2px), so a switch changes the logo, the
/// name and the plan line in place and a part that stays the same does not move. While a workspace loads the plan line
/// is a shimmering "Switching…"; after a failure it is the error with an alert icon. The face never changes size.
/// </summary>
[TemplatePart(TilePart, typeof(ArcContentSwap))]
[TemplatePart(NamePart, typeof(ArcContentSwap))]
[TemplatePart(PlanPart, typeof(ArcContentSwap))]
internal sealed class TeamSwitcherFace : TemplatedControl
{
    internal const string TilePart = "PART_Tile";
    internal const string NamePart = "PART_Name";
    internal const string PlanPart = "PART_Plan";

    /// <summary>The tile's size.</summary>
    public static readonly StyledProperty<double> TileSizeProperty =
        AvaloniaProperty.Register<TeamSwitcherFace, double>(nameof(TileSize), 32);

    private ArcContentSwap? _tile;
    private ArcContentSwap? _name;
    private ArcContentSwap? _plan;
    private TeamSwitcherWorkspace? _shownWorkspace;
    private string? _shownName;
    private (TeamSwitcherFaceLine Kind, string Text)? _shownPlan;
    private TeamSwitcherWorkspace? _workspace;
    private TeamSwitcherFaceLine _line;
    private string _text = string.Empty;

    /// <inheritdoc cref="TileSizeProperty"/>
    public double TileSize { get => GetValue(TileSizeProperty); set => SetValue(TileSizeProperty, value); }

    /// <summary>The workspace on screen.</summary>
    internal TeamSwitcherWorkspace? Workspace => _workspace;

    /// <summary>The name line when there is no workspace ("No workspace").</summary>
    internal string EmptyName { get; set; } = string.Empty;

    /// <summary>What the second line is showing.</summary>
    internal TeamSwitcherFaceLine Line => _line;

    /// <summary>The second line's text.</summary>
    internal string LineText => _text;

    /// <summary>The text control of the second line when it shows a plan or an error (tests).</summary>
    internal Control? PlanContent => _plan?.Content as Control;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _tile = e.NameScope.Find<ArcContentSwap>(TilePart);
        _name = e.NameScope.Find<ArcContentSwap>(NamePart);
        _plan = e.NameScope.Find<ArcContentSwap>(PlanPart);
        _shownWorkspace = null;
        _shownName = null;
        _shownPlan = null;
        Sync();
    }

    /// <summary>Shows <paramref name="workspace"/> with <paramref name="line"/> under its name.</summary>
    public void Show(TeamSwitcherWorkspace? workspace, TeamSwitcherFaceLine line, string text)
    {
        _workspace = workspace;
        _line = line;
        _text = text;
        Sync();
    }

    private void Sync()
    {
        if (_tile is null || _name is null || _plan is null)
        {
            return;
        }
        if (!ReferenceEquals(_shownWorkspace, _workspace) || _tile.Content is null)
        {
            _shownWorkspace = _workspace;
            _tile.Content = _workspace is null ? null : new TeamSwitcherTile { Workspace = _workspace, TileSize = TileSize };
        }
        var name = _workspace?.Name ?? EmptyName;
        if (_shownName != name)
        {
            _shownName = name;
            _name.Content = new TextBlock
            {
                Text = name,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        var plan = (_line, _text);
        if (_shownPlan != plan)
        {
            _shownPlan = plan;
            _plan.Content = _line switch
            {
                TeamSwitcherFaceLine.Switching => SwitchingLine(_text),
                TeamSwitcherFaceLine.Error => ErrorLine(_text),
                _ => new TextBlock
                {
                    Text = _text,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }
    }

    private static Control SwitchingLine(string text)
    {
        var shimmer = new ToolCallText { Text = text, IsShimmering = true, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(shimmer, ToolCallText.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(shimmer, ToolCallText.MutedForegroundProperty, ArcDesignTokenKind.TextMuted);
        return shimmer;
    }

    private static Control ErrorLine(string text)
    {
        var glyph = new ArcGlyph { Data = ArcIcons.CircleAlert, Size = 12, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Danger);
        var label = new TextBlock
        {
            Text = text,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(label, TextBlock.ForegroundProperty, ArcDesignTokenKind.Danger);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 4 };
        Grid.SetColumn(label, 1);
        grid.Children.Add(glyph);
        grid.Children.Add(label);
        return grid;
    }
}

/// <summary>What the second line of the trigger shows.</summary>
internal enum TeamSwitcherFaceLine
{
    /// <summary>The plan and member count.</summary>
    Plan,

    /// <summary>A shimmering "Switching…" while the workspace loads.</summary>
    Switching,

    /// <summary>Why the last switch failed, with an alert icon.</summary>
    Error
}

/// <summary>
/// One workspace in the menu: its tile, the name over the plan line (both truncate), your role and the Cmd/Ctrl+number
/// shortcut (both keep their width), and a check on the active one. Rows are 48px tall, never take focus (the search
/// field keeps it) and draw no highlight of their own: the menu's single gliding highlight sits behind them.
/// </summary>
[PseudoClasses(CurrentPseudoClass)]
internal sealed class TeamSwitcherRow : TemplatedControl
{
    internal const string CurrentPseudoClass = ":current";

    /// <summary>The workspace.</summary>
    public static readonly StyledProperty<TeamSwitcherWorkspace?> WorkspaceProperty =
        AvaloniaProperty.Register<TeamSwitcherRow, TeamSwitcherWorkspace?>(nameof(Workspace));

    /// <summary>The line under the name.</summary>
    public static readonly StyledProperty<string?> PlanLineProperty =
        AvaloniaProperty.Register<TeamSwitcherRow, string?>(nameof(PlanLine));

    /// <summary>Your role, or null.</summary>
    public static readonly StyledProperty<string?> RoleProperty =
        AvaloniaProperty.Register<TeamSwitcherRow, string?>(nameof(Role));

    /// <summary>The shortcut keycap's text, or null past the ninth workspace.</summary>
    public static readonly StyledProperty<string?> ShortcutProperty =
        AvaloniaProperty.Register<TeamSwitcherRow, string?>(nameof(Shortcut));

    /// <summary>Whether this is the active workspace (the check shows).</summary>
    public static readonly StyledProperty<bool> IsCurrentProperty =
        AvaloniaProperty.Register<TeamSwitcherRow, bool>(nameof(IsCurrent));

    /// <summary>Raised when the row is clicked or tapped.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<TeamSwitcherRow, RoutedEventArgs>(nameof(Click), RoutingStrategies.Bubble);

    private bool _pressed;

    static TeamSwitcherRow()
    {
        FocusableProperty.OverrideDefaultValue<TeamSwitcherRow>(false);
    }

    public TeamSwitcherRow(TeamSwitcherWorkspace workspace, int index)
    {
        Workspace = workspace;
        Index = index;
    }

    /// <summary>The row's place in the full list (its shortcut is this plus one).</summary>
    public int Index { get; }

    /// <inheritdoc cref="WorkspaceProperty"/>
    public TeamSwitcherWorkspace? Workspace { get => GetValue(WorkspaceProperty); set => SetValue(WorkspaceProperty, value); }

    /// <inheritdoc cref="PlanLineProperty"/>
    public string? PlanLine { get => GetValue(PlanLineProperty); set => SetValue(PlanLineProperty, value); }

    /// <inheritdoc cref="RoleProperty"/>
    public string? Role { get => GetValue(RoleProperty); set => SetValue(RoleProperty, value); }

    /// <inheritdoc cref="ShortcutProperty"/>
    public string? Shortcut { get => GetValue(ShortcutProperty); set => SetValue(ShortcutProperty, value); }

    /// <inheritdoc cref="IsCurrentProperty"/>
    public bool IsCurrent { get => GetValue(IsCurrentProperty); set => SetValue(IsCurrentProperty, value); }

    /// <inheritdoc cref="ClickEvent"/>
    public event EventHandler<RoutedEventArgs>? Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCurrentProperty)
        {
            PseudoClasses.Set(CurrentPseudoClass, IsCurrent);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pressed)
        {
            _pressed = false;
            if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
            {
                e.Handled = true;
                RaiseEvent(new RoutedEventArgs(ClickEvent));
            }
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TeamSwitcherItemPeer(this);
}

/// <summary>
/// An action at the foot of the list: Create workspace or Join with invite. Like a workspace row it never takes focus and
/// the gliding highlight sits behind it.
/// </summary>
internal sealed class TeamSwitcherActionRow : TemplatedControl
{
    /// <summary>The 24 × 24 stroked glyph at the start.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<TeamSwitcherActionRow, Geometry?>(nameof(Glyph));

    /// <summary>The action's words.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<TeamSwitcherActionRow, string?>(nameof(Text));

    /// <summary>Raised when the row is clicked or tapped.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<TeamSwitcherActionRow, RoutedEventArgs>(nameof(Click), RoutingStrategies.Bubble);

    private bool _pressed;

    static TeamSwitcherActionRow()
    {
        FocusableProperty.OverrideDefaultValue<TeamSwitcherActionRow>(false);
    }

    /// <inheritdoc cref="GlyphProperty"/>
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="ClickEvent"/>
    public event EventHandler<RoutedEventArgs>? Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pressed)
        {
            _pressed = false;
            if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
            {
                e.Handled = true;
                RaiseEvent(new RoutedEventArgs(ClickEvent));
            }
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TeamSwitcherItemPeer(this);
}

/// <summary>A row or action as a list item for screen readers (its name is set by the menu).</summary>
internal sealed class TeamSwitcherItemPeer(Control owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

    protected override bool IsContentElementCore() => true;
}

/// <summary>
/// Moves between the menu's views (the list, Create workspace, Join with invite) inside a panel whose height never
/// changes: the old view fades out over 100ms while the new one fades in over 160ms, both shifting 6px sideways in the
/// direction of travel (forward from the list slides left, back slides right). Reduced motion keeps a 120ms crossfade
/// with no shift; Off swaps at once. Code-driven (<see cref="MotionValue{T}"/>) because the same view enters from either
/// side depending on where the person is going, which a ControlTheme transition on a state cannot express.
/// </summary>
internal sealed class TeamSwitcherViewMotion
{
    private static readonly TimeSpan s_exit = TimeSpan.FromMilliseconds(100);
    private static readonly Easing s_linear = new LinearEasing();
    private readonly Control _owner;
    private readonly Dictionary<Control, ViewState> _states = new();

    public TeamSwitcherViewMotion(Control owner)
    {
        _owner = owner;
    }

    /// <summary>How far the views shift.</summary>
    public double Shift { get; set; } = 6;

    /// <summary>
    /// Shows <paramref name="incoming"/> in place of <paramref name="outgoing"/>. <paramref name="direction"/> is 1 going
    /// forward, -1 going back. <paramref name="hide"/> runs on the outgoing view once it is gone.
    /// </summary>
    public void Swap(Control? outgoing, Control incoming, int direction, Action<Control> hide)
    {
        var level = ArcMotion.GetEffectiveLevel(_owner);
        var animate = _owner.IsLoaded && level != ArcMotionLevel.Off;
        var full = level == ArcMotionLevel.Full;
        incoming.IsVisible = true;
        var enter = State(incoming);
        enter.Hide = null;
        enter.Side = full ? direction * Shift : 0;
        if (!animate)
        {
            enter.Presence.Jump(1);
            enter.Apply(1);
        }
        else
        {
            enter.Presence.Jump(0);
            enter.Apply(0);
            _ = enter.Presence.AnimateTo(1, full
                ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter)
                : MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
        }
        if (outgoing is null || ReferenceEquals(outgoing, incoming))
        {
            return;
        }
        var exit = State(outgoing);
        exit.Side = full ? -direction * Shift : 0;
        exit.Hide = hide;
        if (!animate)
        {
            exit.Presence.Jump(0);
            exit.Apply(0);
            return;
        }
        _ = exit.Presence.AnimateTo(0, full
            ? MotionSpec.Tween(s_exit, ArcEasings.Standard)
            : MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
    }

    /// <summary>Puts <paramref name="view"/> at rest, shown or hidden, without motion.</summary>
    public void Set(Control view, bool shown)
    {
        var state = State(view);
        state.Hide = null;
        state.Presence.Jump(shown ? 1 : 0);
        state.Apply(shown ? 1 : 0);
    }

    private ViewState State(Control view)
    {
        if (!_states.TryGetValue(view, out var state))
        {
            state = new ViewState(_owner, view);
            _states[view] = state;
        }
        return state;
    }

    private sealed class ViewState
    {
        private readonly Control _view;
        private readonly TranslateTransform _shift = new();

        public ViewState(Control owner, Control view)
        {
            _view = view;
            view.RenderTransform = _shift;
            Presence = new MotionValue<double>(owner, view.IsVisible ? 1 : 0, MotionVectors.Double);
            Presence.Changed += Apply;
        }

        public MotionValue<double> Presence { get; }

        /// <summary>Where the view sits when it is fully hidden (px, signed).</summary>
        public double Side { get; set; }

        /// <summary>What to do once the view has faded out.</summary>
        public Action<Control>? Hide { get; set; }

        public void Apply(double presence)
        {
            _view.Opacity = presence;
            _shift.X = (1 - presence) * Side;
            if (presence <= 0.0001 && Hide is { } hide)
            {
                Hide = null;
                hide(_view);
            }
        }
    }
}
