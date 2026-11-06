using System.Windows.Input;
using Avalonia;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using TKCode.Arc.Localization;

using AvaloniaWindow = Avalonia.Controls.Window;

namespace TKCode.Arc;

public partial class DockLayout
{
    public const string FloatsPart = "PART_Floats";

    /// <summary>
    /// How panes float: <see cref="DockFloatingMode.Auto"/> (the default) uses real windows in a desktop window of a classic
    /// desktop application and floating cards inside the layout anywhere else; <see cref="DockFloatingMode.Window"/> and
    /// <see cref="DockFloatingMode.Overlay"/> force one of them; <see cref="DockFloatingMode.Disabled"/> keeps every pane
    /// docked (dragging out of the layout puts the pane back, and <see cref="DockPane.CanFloat"/> counts as false).
    /// </summary>
    public static readonly StyledProperty<DockFloatingMode> FloatingModeProperty =
        AvaloniaProperty.Register<DockLayout, DockFloatingMode>(nameof(FloatingMode));

    private readonly Dictionary<DockFloat, IDockFloatHost> _floatHosts = new();
    private Canvas? _floats;
    private DockCommand? _floatActiveCommand;

    /// <inheritdoc cref="FloatingModeProperty"/>
    public DockFloatingMode FloatingMode { get => GetValue(FloatingModeProperty); set => SetValue(FloatingModeProperty, value); }

    /// <summary>
    /// Floats the active pane (when it can float, floating is not disabled and the layout is not locked); bind it to a menu
    /// or key.
    /// </summary>
    public ICommand FloatActiveCommand
    {
        get
        {
            if (_floatActiveCommand is null)
            {
                _floatActiveCommand = new DockCommand(
                    () =>
                    {
                        if (ActivePane is { } pane)
                        {
                            Float(pane);
                        }
                    },
                    () => ActivePane is { } pane && CanFloatPanes([pane]) && !IsAloneInFloat(pane));
                PropertyChanged += (_, e) =>
                {
                    if (e.Property == ActivePaneProperty || e.Property == IsLockedProperty || e.Property == FloatingModeProperty)
                    {
                        _floatActiveCommand.RaiseCanExecuteChanged();
                    }
                };
            }
            return _floatActiveCommand;
        }
    }

    /// <summary>The panel of floating cards (tests).</summary>
    internal Canvas? FloatsPanel => _floats;

    /// <summary>The host showing a float, or null (tests, drags).</summary>
    internal IDockFloatHost? HostOf(DockFloat dock) => _floatHosts.GetValueOrDefault(dock);

    /// <summary>The floating windows and cards shown now (tests).</summary>
    internal IReadOnlyCollection<IDockFloatHost> FloatHosts => _floatHosts.Values;

    /// <summary>Whether floats show as real windows now.</summary>
    internal bool UsesWindows
    {
        get
        {
            if (TopLevel.GetTopLevel(this) is not AvaloniaWindow)
            {
                return false;
            }
            return FloatingMode switch
            {
                DockFloatingMode.Window => true,
                DockFloatingMode.Auto => Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime,
                _ => false
            };
        }
    }

    #region Hosts

    private void ApplyFloatingTemplate(TemplateAppliedEventArgs e)
    {
        if (_floats is not null)
        {
            _floats.SizeChanged -= OnFloatsSizeChanged;
        }
        DismissFloatHosts();
        _floats = e.NameScope.Find<Canvas>(FloatsPart);
        if (_floats is not null)
        {
            _floats.SizeChanged += OnFloatsSizeChanged;
        }
        SyncFloats();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        OnAttachedExtras();
        SyncFloats();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CancelDrag();
        // Windows go with the layout; the floats stay in the model and come back when the layout is shown again.
        DismissFloatHosts();
        OnDetachedExtras();
    }

    private void DismissFloatHosts()
    {
        foreach (var host in _floatHosts.Values.ToList())
        {
            host.Dismiss();
        }
        _floatHosts.Clear();
    }

    /// <summary>Makes the floating windows or cards match <see cref="DockWorkspace.Floats"/> (both ways: a float added in code opens a host).</summary>
    internal void SyncFloats()
    {
        var top = TopLevel.GetTopLevel(this);
        if (_floats is null || top is null || !this.IsAttachedToVisualTree())
        {
            DismissFloatHosts();
            return;
        }
        var windows = UsesWindows;
        foreach (var (dock, host) in _floatHosts.ToList())
        {
            if (!_workspace.Floats.Contains(dock) || host.IsWindow != windows)
            {
                _floatHosts.Remove(dock);
                host.Dismiss();
                if (_workspace.Floats.Contains(dock))
                {
                    // Switching between windows and cards: keep the float where it shows on screen.
                    dock.Bounds = ConvertBounds(dock.Bounds, toWindows: windows);
                }
            }
        }
        foreach (var dock in _workspace.Floats.ToList())
        {
            if (_floatHosts.TryGetValue(dock, out var existing))
            {
                existing.SyncContent();
                if (existing is DockFloatingCard shown)
                {
                    shown.Place(_floats.Bounds.Size);
                }
                continue;
            }
            if (dock.Root is null)
            {
                continue;
            }
            if (windows && top is AvaloniaWindow owner)
            {
                ClampToScreens(dock, owner);
                var window = new DockFloatingWindow(this, dock);
                _floatHosts[dock] = window;
                window.ShowOver(owner);
            }
            else
            {
                var card = new DockFloatingCard(this, dock);
                _floatHosts[dock] = card;
                _floats.Children.Add(card);
                card.Place(_floats.Bounds.Size);
                card.BringToFront();
            }
        }
        _floatActiveCommand?.RaiseCanExecuteChanged();
    }

    /// <summary>Bounds of a card (floats panel coordinates) as a window's (desktop DIPs), or the other way round.</summary>
    private Rect ConvertBounds(Rect bounds, bool toWindows)
    {
        if (_floats is null || !_floats.IsAttachedToVisualTree())
        {
            return bounds;
        }
        var scale = DesktopScale();
        if (toWindows)
        {
            var screen = _floats.PointToScreen(bounds.Position);
            return new Rect(screen.X / scale, screen.Y / scale, bounds.Width, bounds.Height);
        }
        var local = _floats.PointToClient(new PixelPoint((int)Math.Round(bounds.X * scale), (int)Math.Round(bounds.Y * scale)));
        return new Rect(local, bounds.Size);
    }

    /// <summary>A floating window closed by its owner or the application: the float stays in the model.</summary>
    internal void ForgetFloatHost(IDockFloatHost host)
    {
        if (host.Float is { } dock && _floatHosts.TryGetValue(dock, out var known) && ReferenceEquals(known, host))
        {
            _floatHosts.Remove(dock);
        }
    }

    private void OnFloatsSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        foreach (var host in _floatHosts.Values)
        {
            if (host is DockFloatingCard card)
            {
                card.Place(e.NewSize);
            }
        }
    }

    /// <summary>Pulls a restored window's bounds onto a screen when no screen shows enough of it (a monitor was unplugged).</summary>
    private static void ClampToScreens(DockFloat dock, AvaloniaWindow owner)
    {
        if (owner.Screens is not { } screens || dock.Bounds.Width <= 0 || dock.Bounds.Height <= 0)
        {
            return;
        }
        var scale = owner.DesktopScaling > 0 ? owner.DesktopScaling : 1;
        Rect WorkArea(Avalonia.Platform.Screen screen) => new(screen.WorkingArea.X / scale, screen.WorkingArea.Y / scale,
            screen.WorkingArea.Width / scale, screen.WorkingArea.Height / scale);
        foreach (var screen in screens.All)
        {
            var area = WorkArea(screen);
            if (area.Width > 0 && DockLayoutSerializer.ClampToWorkArea(dock.Bounds, area) == dock.Bounds)
            {
                return;
            }
        }
        if ((screens.ScreenFromWindow(owner) ?? screens.Primary) is { } main)
        {
            dock.Bounds = DockLayoutSerializer.ClampToWorkArea(dock.Bounds, WorkArea(main));
        }
    }

    /// <summary>Brings the host of a floating pane to the front (activating the pane).</summary>
    private void BringFloatToFront(DockPane pane)
    {
        if (pane.Group?.Float is { } dock && HostOf(dock) is { } host)
        {
            host.BringToFront();
        }
    }

    internal void OnFloatHostGotFocus(FocusChangedEventArgs e) => OnAnyGotFocus(this, e);

    /// <summary>
    /// Runs the pending layout of the other windows before a pane is shown in <paramref name="target"/>'s window. A pane
    /// moving between windows can still be queued in the old window's layout manager, which then throws when it reaches a
    /// control that now belongs to another window ("wrong LayoutManager").
    /// </summary>
    internal void FlushForeignLayouts(Visual target)
    {
        if (!_floatHosts.Values.Any(h => h.IsWindow))
        {
            return;
        }
        var top = TopLevel.GetTopLevel(target);
        if (TopLevel.GetTopLevel(this) is { } main && !ReferenceEquals(main, top))
        {
            main.UpdateLayout();
        }
        foreach (var host in _floatHosts.Values)
        {
            if (host is DockFloatingWindow window && !ReferenceEquals(window, top) && window.IsVisible)
            {
                window.UpdateLayout();
            }
        }
    }

    /// <summary>Ctrl+F4 / ⌘W inside a floating window closes its active pane, as in the layout.</summary>
    internal void OnFloatHostKeyDown(KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }
        var closeKey = (e.Key == Key.F4 && e.KeyModifiers == KeyModifiers.Control) || (e.Key == Key.W && e.KeyModifiers == KeyModifiers.Meta);
        if (closeKey && ActivePane is { State: DockPaneState.Floating } pane && IsInsidePane(e.Source, pane))
        {
            e.Handled = true;
            Close(pane);
        }
    }

    internal void OnFloatResized() => RaiseLayoutChanged(DockLayoutChangeKind.Resize, null);

    #endregion

    #region Float and dock

    /// <summary>Whether every pane may float now.</summary>
    internal bool CanFloatPanes(IReadOnlyList<DockPane> panes)
    {
        if (IsLocked || FloatingMode == DockFloatingMode.Disabled || panes.Count == 0)
        {
            return false;
        }
        foreach (var pane in panes)
        {
            if (!pane.CanFloat || !ReferenceEquals(pane.Workspace, _workspace))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsAloneInFloat(DockPane pane)
        => pane.Group is { Float: { } dock } && dock.Root?.AllPanes().Skip(1).Any() != true;

    /// <summary>
    /// Moves a pane to a floating window (a floating card where there are no windows; <see cref="FloatingMode"/>) that opens
    /// near where the pane was. Does nothing when the pane cannot float (<see cref="DockPane.CanFloat"/>, a locked layout,
    /// floating disabled) or already floats alone. <see cref="Dock"/> brings it back to the same place.
    /// </summary>
    public void Float(DockPane pane)
    {
        ArgumentNullException.ThrowIfNull(pane);
        if (!CanFloatPanes([pane]))
        {
            return;
        }
        if (IsAloneInFloat(pane))
        {
            BringFloatToFront(pane);
            return;
        }
        FloatCore([pane], pane, FloatBoundsNear(pane));
    }

    /// <summary>
    /// Docks a floating or auto-hidden pane back where it was before (its old group, or beside the old neighbour with the old
    /// size when that group is gone); without a remembered place, by its <see cref="DockPane.PreferredSide"/> (documents go to
    /// the document area). Does nothing for a docked pane or in a locked layout.
    /// </summary>
    public void Dock(DockPane pane)
    {
        ArgumentNullException.ThrowIfNull(pane);
        if (IsLocked || !ReferenceEquals(pane.Workspace, _workspace) || pane.State == DockPaneState.Docked)
        {
            return;
        }
        if (pane.State == DockPaneState.AutoHidden)
        {
            // An auto-hidden pane's docked place is the one it was unpinned from.
            Pin(pane);
            return;
        }
        DockCore(pane);
        Select(pane);
        RaiseLayoutChanged(DockLayoutChangeKind.Dock, pane);
    }

    /// <summary>Docks every pane of a floating window or card (its Dock button, a double click on its title bar).</summary>
    internal void DockFloat(DockFloat dock)
    {
        if (IsLocked || dock.Root is null)
        {
            return;
        }
        var panes = dock.Root.AllPanes().ToList();
        var lead = dock.Root.SelfAndDescendants().OfType<DockGroup>().FirstOrDefault()?.ActivePane;
        foreach (var pane in panes)
        {
            DockCore(pane);
        }
        if (lead is not null)
        {
            Select(lead);
        }
        RaiseLayoutChanged(DockLayoutChangeKind.Dock, lead);
    }

    /// <summary>
    /// Closes a floating window or card as its close button does: its panes that can close close (each may be cancelled in
    /// <see cref="PaneClosing"/>), the others dock back; the host goes once its float is empty.
    /// </summary>
    internal void CloseFloat(DockFloat dock)
    {
        if (IsLocked || dock.Root is null || !ReferenceEquals(dock.Workspace, _workspace))
        {
            return;
        }
        foreach (var pane in dock.Root.AllPanes().ToList())
        {
            if (pane.CanClose)
            {
                Close(pane);
            }
            else
            {
                DockCore(pane);
                RaiseLayoutChanged(DockLayoutChangeKind.Dock, pane);
            }
        }
    }

    private void DockCore(DockPane pane)
    {
        var placed = pane.LastDockedPlacement is { } placement && _workspace.Restore(pane, placement);
        if (!placed)
        {
            _workspace.BeginUpdate();
            try
            {
                _workspace.Detach(pane);
                _workspace.PlaceByPreference(pane, EdgeShareFor(pane));
            }
            finally
            {
                _workspace.EndUpdate();
            }
        }
        var other = pane.Group?.Panes.FirstOrDefault(p => !ReferenceEquals(p, pane));
        if (other is not null)
        {
            Announce(DockText.Format(DockLangResourceKind.AnnounceJoined, pane.ToString(), other.ToString()));
        }
        else if (pane.PreferredSide is { } side)
        {
            Announce(DockText.Format(side switch
            {
                DockSide.Left => DockLangResourceKind.AnnounceDockedEdgeLeft,
                DockSide.Right => DockLangResourceKind.AnnounceDockedEdgeRight,
                DockSide.Top => DockLangResourceKind.AnnounceDockedEdgeTop,
                _ => DockLangResourceKind.AnnounceDockedEdgeBottom
            }, pane.ToString()));
        }
    }

    /// <summary>Floats panes at <paramref name="bounds"/>, remembering where the docked ones were.</summary>
    private void FloatCore(IReadOnlyList<DockPane> panes, DockPane lead, Rect bounds)
    {
        foreach (var pane in panes)
        {
            if (pane.State == DockPaneState.Docked && _workspace.PlacementOf(pane) is { } placement)
            {
                pane.LastDockedPlacement = placement;
            }
        }
        var dock = _workspace.FloatPanes(panes, bounds, lead);
        Select(lead);
        HostOf(dock)?.BringToFront();
        Announce(DockText.Format(DockLangResourceKind.AnnounceFloating, lead.ToString()));
        RaiseLayoutChanged(DockLayoutChangeKind.Float, lead);
    }

    /// <summary>A dock drag dropped outside every target: the panes float where the pointer is.</summary>
    private void FloatFromDrag(DockDragSession session)
    {
        var size = FloatSize(session.SourceGroup.View as Control);
        var offset = new Vector(FloatToken(DockFloatingWindowTokenKind.OpenOffsetX, 24), FloatToken(DockFloatingWindowTokenKind.OpenOffsetY, 16));
        Rect bounds;
        if (UsesWindows)
        {
            var screen = session.Top.PointToScreen(session.Last);
            var scale = DesktopScale();
            bounds = new Rect(new Point(screen.X / scale, screen.Y / scale) - offset, size);
        }
        else
        {
            var at = _floats is not null ? session.Top.TranslatePoint(session.Last, _floats) ?? default : default;
            bounds = new Rect(at - offset, size);
        }
        FloatCore(session.Panes, session.Lead, bounds);
    }

    /// <summary>Where <see cref="Float"/> opens a pane: a little right of and below its group, as large as the group.</summary>
    private Rect FloatBoundsNear(DockPane pane)
    {
        var view = pane.Group?.View as Control;
        var size = FloatSize(view);
        var step = new Vector(FloatToken(DockFloatingWindowTokenKind.OpenOffsetX, 24), FloatToken(DockFloatingWindowTokenKind.OpenOffsetX, 24));
        if (view is not null && view.IsAttachedToVisualTree())
        {
            if (UsesWindows)
            {
                var screen = view.PointToScreen(default);
                var scale = DesktopScale();
                return new Rect(new Point(screen.X / scale, screen.Y / scale) + step, size);
            }
            if (_floats is not null && view.TranslatePoint(default, _floats) is { } at)
            {
                return new Rect(at + step, size);
            }
        }
        if (UsesWindows && TopLevel.GetTopLevel(this) is AvaloniaWindow window)
        {
            var scale = DesktopScale();
            var center = window.PointToScreen(new Point(window.Bounds.Width / 2, window.Bounds.Height / 2));
            return new Rect(center.X / scale - size.Width / 2, center.Y / scale - size.Height / 2, size.Width, size.Height);
        }
        var area = _floats?.Bounds.Size ?? Bounds.Size;
        return new Rect((area.Width - size.Width) / 2, (area.Height - size.Height) / 2, size.Width, size.Height);
    }

    private Size FloatSize(Control? view)
    {
        var minWidth = FloatToken(DockFloatingWindowTokenKind.MinWidth, 320);
        var minHeight = FloatToken(DockFloatingWindowTokenKind.MinHeight, 240);
        var size = view?.Bounds.Size ?? default;
        return new Size(Math.Max(minWidth, size.Width), Math.Max(minHeight, size.Height));
    }

    private double FloatToken(DockFloatingWindowTokenKind kind, double fallback)
        => this.TryFindResource(DockFloatingWindowTokens.Key(kind), out var value) && value is double d ? d : fallback;

    private double DesktopScale() => TopLevel.GetTopLevel(this) is WindowBase { DesktopScaling: > 0 } window ? window.DesktopScaling : 1;

    #endregion

    #region Moving a host with a drag

    private void BeginHostMove(DockDragSession session, IDockFloatHost host)
    {
        host.BringToFront();
        if (host is DockFloatingWindow window)
        {
            session.HostStartPixel = window.Position;
        }
        else if (host is DockFloatingCard card)
        {
            session.HostStartPoint = card.Position;
        }
    }

    private void MoveHost(DockDragSession session, IDockFloatHost host, Point point, PixelPoint screen)
    {
        if (host is DockFloatingWindow window)
        {
            var delta = screen - session.StartScreen;
            window.MoveTo(session.HostStartPixel + delta);
        }
        else if (host is DockFloatingCard card && _floats is not null)
        {
            card.MoveTo(session.HostStartPoint + (point - session.Start), _floats.Bounds.Size);
        }
    }

    private void RestoreHost(DockDragSession session, IDockFloatHost host)
    {
        if (host is DockFloatingWindow window)
        {
            window.MoveTo(session.HostStartPixel);
        }
        else if (host is DockFloatingCard card && _floats is not null)
        {
            card.MoveTo(session.HostStartPoint, _floats.Bounds.Size);
        }
    }

    #endregion

    #region Menu

    /// <summary>The Float and Dock rows of a pane's menu (spec 11).</summary>
    private void AddFloatingRows(List<MenuEntry> rows, DockPane pane)
    {
        var added = new List<MenuEntry>();
        if (FloatingMode != DockFloatingMode.Disabled && !IsAloneInFloat(pane))
        {
            added.Add(Row(DockText.Get(DockLangResourceKind.Float), CanFloatPanes([pane]), () => Float(pane), ArcIcons.ExternalLink));
        }
        if (pane.State == DockPaneState.Floating)
        {
            added.Add(Row(DockText.Get(DockLangResourceKind.Dock), !IsLocked, () => Dock(pane)));
        }
        if (added.Count > 0)
        {
            added[0].HasSeparatorBefore = true;
            rows.AddRange(added);
        }
    }

    #endregion
}
