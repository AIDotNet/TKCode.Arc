using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A grid of square tiles that bends toward the pointer like spacetime around a mass (Arc Gravity well, design
/// reference). Tiles near the well lean in, shrink and dim on their own springs; a click, a tap, Space or Enter sends a
/// shockwave rolling outward through the grid. Each item becomes one tile: photos, avatars, icons or any small content.
/// </summary>
/// <remarks>
/// The tiles keep their grid slots and only move with their render transform, opacity and stacking order, so reading
/// order never changes. The grid is a focusable group: Tab places the well on the current tile, the arrow keys, Home and
/// End move it, and Space or Enter sends a shockwave. Under reduced motion the grid stays still and a shockwave only
/// flashes the tiles' rings.
/// </remarks>
public class GravityWell : ItemsControl
{
    /// <summary>A fixed column count; 0 (the default) fits as many columns as <see cref="MinTileSize"/> allows.</summary>
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<GravityWell, int>(nameof(Columns));

    /// <summary>The smallest tile width in px when <see cref="Columns"/> is 0.</summary>
    public static readonly StyledProperty<double> MinTileSizeProperty =
        AvaloniaProperty.Register<GravityWell, double>(nameof(MinTileSize), 56);

    /// <summary>The gap between tiles in px.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<GravityWell, double>(nameof(Gap), 8);

    /// <summary>The reach of the well in px.</summary>
    public static readonly StyledProperty<double> RadiusProperty =
        AvaloniaProperty.Register<GravityWell, double>(nameof(Radius), 150);

    /// <summary>The pull strength, from 0 to about 1.5.</summary>
    public static readonly StyledProperty<double> StrengthProperty =
        AvaloniaProperty.Register<GravityWell, double>(nameof(Strength), 1);

    /// <summary>The corner radius of every tile; the content is clipped to it and the shockwave ring follows it.</summary>
    public static readonly StyledProperty<CornerRadius> TileCornerRadiusProperty =
        AvaloniaProperty.Register<GravityWell, CornerRadius>(nameof(TileCornerRadius));

    /// <summary>The accessible name of the grid.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<GravityWell, string>(nameof(Label), "Gravity well");

    /// <summary>Raised when a click, a tap or a key sends a shockwave, with its origin in the grid.</summary>
    public static readonly RoutedEvent<GravityWellPulseEventArgs> PulsedEvent =
        RoutedEvent.Register<GravityWell, GravityWellPulseEventArgs>(nameof(Pulsed), RoutingStrategies.Bubble);

    private static readonly FuncTemplate<Panel?> DefaultPanel = new(() => new GravityWellPanel());

    private readonly GravityWellSimulation _simulation = new();
    private readonly LiveLoop _loop;
    private GravityWellPanel? _panel;
    private double[] _centersX = [];
    private double[] _centersY = [];
    private int _current;

    static GravityWell()
    {
        ItemsPanelProperty.OverrideDefaultValue<GravityWell>(DefaultPanel);
        FocusableProperty.OverrideDefaultValue<GravityWell>(true);
    }

    public GravityWell()
    {
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
        AddHandler(PointerMovedEvent, OnPointerMovedAnywhere, handledEventsToo: true);
        AddHandler(PointerPressedEvent, OnPointerPressedAnywhere, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnPointerReleasedAnywhere, handledEventsToo: true);
        AddHandler(PointerExitedEvent, (_, _) => Release(), handledEventsToo: true);
    }

    /// <inheritdoc cref="ColumnsProperty"/>
    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <inheritdoc cref="MinTileSizeProperty"/>
    public double MinTileSize
    {
        get => GetValue(MinTileSizeProperty);
        set => SetValue(MinTileSizeProperty, value);
    }

    /// <inheritdoc cref="GapProperty"/>
    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    /// <inheritdoc cref="RadiusProperty"/>
    public double Radius
    {
        get => GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    /// <inheritdoc cref="StrengthProperty"/>
    public double Strength
    {
        get => GetValue(StrengthProperty);
        set => SetValue(StrengthProperty, value);
    }

    /// <inheritdoc cref="TileCornerRadiusProperty"/>
    public CornerRadius TileCornerRadius
    {
        get => GetValue(TileCornerRadiusProperty);
        set => SetValue(TileCornerRadiusProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="PulsedEvent"/>
    public event EventHandler<GravityWellPulseEventArgs>? Pulsed
    {
        add => AddHandler(PulsedEvent, value);
        remove => RemoveHandler(PulsedEvent, value);
    }

    /// <summary>The tile the keyboard well sits on (an index into the items).</summary>
    public int CurrentIndex => _current;

    internal GravityWellSimulation Simulation => _simulation;

    internal GravityWellPanel? Panel => _panel;

    /// <summary>
    /// Sends a shockwave from <paramref name="origin"/> (in the control's coordinates), as a click there would, and raises
    /// <see cref="Pulsed"/>.
    /// </summary>
    public void Pulse(Point origin)
    {
        var point = _panel is null ? origin : this.TranslatePoint(origin, _panel) ?? origin;
        PulseAt(point);
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new GravityWellTile();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is GravityWellTile tile)
        {
            tile.CornerRadius = TileCornerRadius;
        }
    }

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<GravityWellTile>(item, out recycleKey);

    protected override AutomationPeer OnCreateAutomationPeer() => new GravityWellAutomationPeer(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ColumnsProperty || property == MinTileSizeProperty || property == GapProperty)
        {
            _panel?.InvalidateMeasure();
        }
        else if (property == RadiusProperty || property == StrengthProperty)
        {
            _simulation.Radius = Radius;
            _simulation.Strength = Strength;
            Wake();
        }
        else if (property == TileCornerRadiusProperty && _panel is not null)
        {
            foreach (var child in _panel.Children)
            {
                if (child is GravityWellTile tile)
                {
                    tile.CornerRadius = TileCornerRadius;
                }
            }
        }
        else if (property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        else if (property == ArcMotion.LevelProperty)
        {
            _simulation.CanMove = ArcMotion.IsFull(this);
            if (!_simulation.CanMove)
            {
                _simulation.Rest();
                ApplyPoses();
            }
        }
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _simulation.Radius = Radius;
        _simulation.Strength = Strength;
        if (AutomationProperties.GetName(this) is not { Length: > 0 })
        {
            AutomationProperties.SetName(this, Label);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        // The well shows where keyboard focus is; a click that focuses the grid already aims it at the pointer.
        if (e.Source == this && e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional)
        {
            AimAtTile(_current);
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (!IsKeyboardFocusWithin)
        {
            Release();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _centersX.Length == 0)
        {
            return;
        }
        var count = _centersX.Length;
        var columns = Math.Max(1, _panel?.ColumnCount ?? 1);
        int? step = e.Key switch
        {
            Key.Left => -1,
            Key.Right => 1,
            Key.Up => -columns,
            Key.Down => columns,
            Key.Home => -count,
            Key.End => count,
            _ => null
        };
        if (step is { } delta)
        {
            e.Handled = true;
            AimAtTile(Math.Clamp(_current + delta, 0, count - 1));
            return;
        }
        if (e.Key is Key.Space or Key.Enter)
        {
            e.Handled = true;
            if (_simulation.IsAbsent)
            {
                AimAtTile(_current);
            }
            var index = Math.Clamp(_current, 0, count - 1);
            PulseAt(new Point(_centersX[index], _centersY[index]));
        }
    }

    internal void AttachPanel(GravityWellPanel panel) => _panel = panel;

    internal void DetachPanel(GravityWellPanel panel)
    {
        if (ReferenceEquals(_panel, panel))
        {
            _panel = null;
        }
    }

    /// <summary>The panel arranged its tiles: read their resting centres (in the panel's coordinates).</summary>
    internal void OnTilesArranged(GravityWellPanel panel)
    {
        var children = panel.Children;
        var count = children.Count;
        if (_centersX.Length != count)
        {
            _centersX = new double[count];
            _centersY = new double[count];
        }
        for (var i = 0; i < count; i++)
        {
            var bounds = children[i].Bounds;
            _centersX[i] = bounds.X + bounds.Width / 2;
            _centersY[i] = bounds.Y + bounds.Height / 2;
        }
        _simulation.SetTiles(_centersX, _centersY);
        _current = Math.Clamp(_current, 0, Math.Max(0, count - 1));
        ApplyPoses();
        Wake();
    }

    private void AimAtTile(int index)
    {
        if (_centersX.Length == 0)
        {
            return;
        }
        _current = Math.Clamp(index, 0, _centersX.Length - 1);
        _simulation.Aim(_centersX[_current], _centersY[_current], _simulation.IsAbsent);
        Wake();
    }

    private void PulseAt(Point point)
    {
        _simulation.Pulse(point.X, point.Y);
        Wake();
        RaiseEvent(new GravityWellPulseEventArgs(PulsedEvent, point));
    }

    private void Release()
    {
        _simulation.Release();
        Wake();
    }

    private void Wake()
    {
        _simulation.CanMove = ArcMotion.IsFull(this);
        _loop.Start();
    }

    private bool Step(double dt)
    {
        var moving = _simulation.Step(dt);
        ApplyPoses();
        return moving;
    }

    private void ApplyPoses()
    {
        if (_panel is null)
        {
            return;
        }
        var children = _panel.Children;
        var count = Math.Min(children.Count, _simulation.Count);
        for (var i = 0; i < count; i++)
        {
            if (children[i] is GravityWellTile tile)
            {
                tile.Pose(_simulation.OffsetX(i), _simulation.OffsetY(i), _simulation.Scale(i), _simulation.Opacity(i),
                    _simulation.ZIndex(i), _simulation.Ring(i));
            }
        }
    }

    private void OnPointerMovedAnywhere(object? sender, PointerEventArgs e)
    {
        if (_panel is null)
        {
            return;
        }
        // A finger pulls the well only while it is down; a vertical drag still scrolls the page.
        if (e.Pointer.Type != PointerType.Mouse && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var point = e.GetPosition(_panel);
        _simulation.Aim(point.X, point.Y, _simulation.IsAbsent);
        Wake();
    }

    private void OnPointerPressedAnywhere(object? sender, PointerPressedEventArgs e)
    {
        if (_panel is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var point = e.GetPosition(_panel);
        _simulation.Aim(point.X, point.Y, _simulation.IsAbsent);
        PulseAt(point);
    }

    private void OnPointerReleasedAnywhere(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Mouse)
        {
            Release();
        }
    }

    private sealed class GravityWellAutomationPeer(GravityWell owner) : ItemsControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetLocalizedControlTypeCore() => "gravity well";
    }
}

/// <summary>The origin of a <see cref="GravityWell"/> shockwave.</summary>
public sealed class GravityWellPulseEventArgs : RoutedEventArgs
{
    public GravityWellPulseEventArgs(RoutedEvent routedEvent, Point origin) : base(routedEvent) => Origin = origin;

    /// <summary>Where the shockwave started, in px from the top left of the grid.</summary>
    public Point Origin { get; }
}
