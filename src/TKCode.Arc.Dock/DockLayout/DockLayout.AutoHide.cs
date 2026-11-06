using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

public partial class DockLayout
{
    private readonly DockAutoHideStrip?[] _strips = new DockAutoHideStrip?[4];
    private DockAutoHideFlyout? _flyout;
    private DockPane? _paneBeforeFlyout;
    private TopLevel? _outsideWatch;

    /// <summary>The auto-hide strip of an edge (tests).</summary>
    internal DockAutoHideStrip? StripOf(DockSide side) => _strips[(int)side];

    /// <summary>The auto-hide flyout layer (tests).</summary>
    internal DockAutoHideFlyout? Flyout => _flyout;

    /// <summary>
    /// Auto-hides (unpins) a docked tool pane: it leaves its group for the strip of the layout edge nearest the group's
    /// centre, where a click slides it out over the content. Documents, floating panes and panes with
    /// <see cref="DockPane.CanAutoHide"/> false stay where they are. Returns whether the pane moved.
    /// </summary>
    public bool AutoHide(DockPane pane)
    {
        ArgumentNullException.ThrowIfNull(pane);
        if (!CanAutoHideNow(pane) || pane.Group is not { } group)
        {
            return false;
        }
        var side = AutoHideSideOf(pane, group);
        var extent = DockedExtentOf(group, side) is var px and > 0 ? px : pane.EffectivePreferredSize;
        var wasActive = ReferenceEquals(ActivePane, pane);
        var hadFocus = pane.IsKeyboardFocusWithin || (group.View as DockGroupView)?.TabOf(pane)?.IsKeyboardFocusWithin == true;
        var next = wasActive ? NextActiveAfter(pane) : null;
        if (ReferenceEquals(_maximized, group) && group.Panes.Count == 1)
        {
            RestoreMaximized();
        }
        if (!_workspace.AutoHideTo(pane, side, extent))
        {
            return false;
        }
        if (wasActive)
        {
            SetCurrentValue(ActivePaneProperty, next is { State: DockPaneState.Docked } && ReferenceEquals(next.Workspace, _workspace) ? next : FallbackActivePane());
        }
        if (hadFocus)
        {
            if (ActivePane is not { } target || !FocusTab(target))
            {
                StripOf(side)?.ButtonOf(pane)?.Focus(NavigationMethod.Tab);
            }
        }
        Announce(DockText.Format(DockLangResourceKind.AnnounceAutoHidden, pane.ToString()));
        RaiseLayoutChanged(DockLayoutChangeKind.AutoHide, pane);
        return true;
    }

    /// <summary>
    /// Pins an auto-hidden pane back: into the group it left, at its old place, or, when that group has gone, into a new
    /// group along its strip's edge as wide or high as it was. Returns whether the pane moved.
    /// </summary>
    public bool Pin(DockPane pane)
    {
        ArgumentNullException.ThrowIfNull(pane);
        if (pane.State != DockPaneState.AutoHidden || !ReferenceEquals(pane.Workspace, _workspace) || pane.HiddenSide is not { } side)
        {
            return false;
        }
        var hadFocus = ReferenceEquals(_flyout?.Pane, pane) && _flyout!.Card.IsKeyboardFocusWithin;
        if (ReferenceEquals(_flyout?.Pane, pane))
        {
            UnwatchOutside();
            SetStripSelection(null);
            _flyout!.Hide(animate: false);
        }
        var extent = pane.AutoHideExtent > 0 ? pane.AutoHideExtent : pane.EffectivePreferredSize;
        var group = _workspace.PinBack(pane, EdgeShareOf(side, extent));
        if (group is null)
        {
            return false;
        }
        Select(pane);
        if (hadFocus)
        {
            Activate(pane);
        }
        Announce(group.Panes.Count > 1 && group.Panes.FirstOrDefault(p => !ReferenceEquals(p, pane)) is { } other
            ? DockText.Format(DockLangResourceKind.AnnounceJoined, pane.ToString(), other.ToString())
            : DockText.Format(side switch
            {
                DockSide.Left => DockLangResourceKind.AnnounceDockedEdgeLeft,
                DockSide.Right => DockLangResourceKind.AnnounceDockedEdgeRight,
                DockSide.Top => DockLangResourceKind.AnnounceDockedEdgeTop,
                _ => DockLangResourceKind.AnnounceDockedEdgeBottom
            }, pane.ToString()));
        RaiseLayoutChanged(DockLayoutChangeKind.Pin, pane);
        return true;
    }

    /// <summary>Whether <paramref name="pane"/> is a docked tool of the main layout that may auto-hide.</summary>
    private bool CanAutoHideNow(DockPane pane)
        => ReferenceEquals(pane.Workspace, _workspace) && !pane.IsDocument && pane.CanAutoHide
           && pane.State == DockPaneState.Docked && pane.Group is { Float: null };

    /// <summary>The edge nearest the centre of the pane's group (spec 5.4); its preferred side when the group is not laid out.</summary>
    private DockSide AutoHideSideOf(DockPane pane, DockGroup group)
    {
        if (_host is { Bounds: { Width: > 0, Height: > 0 } hostBounds } && group.View is Visual view
            && view.TranslatePoint(new Point(view.Bounds.Width / 2, view.Bounds.Height / 2), _host) is { } centre)
        {
            var distances = new (DockSide Side, double Distance)[]
            {
                (DockSide.Left, centre.X),
                (DockSide.Right, hostBounds.Width - centre.X),
                (DockSide.Bottom, hostBounds.Height - centre.Y),
                (DockSide.Top, centre.Y)
            };
            var best = distances.Min(d => d.Distance);
            if (pane.PreferredSide is { } preferred && distances.Any(d => d.Side == preferred && d.Distance <= best + 0.5))
            {
                return preferred;
            }
            return distances.First(d => d.Distance <= best + 0.5).Side;
        }
        return pane.PreferredSide ?? DockSide.Left;
    }

    /// <summary>The group's width (left, right) or height (top, bottom) on screen; 0 when it is not laid out.</summary>
    private static double DockedExtentOf(DockGroup group, DockSide side)
        => group.View is Control view ? side is DockSide.Left or DockSide.Right ? view.Bounds.Width : view.Bounds.Height : 0;

    /// <summary>The share of the layout a group of <paramref name="extent"/> px along <paramref name="side"/>'s axis takes.</summary>
    private double EdgeShareOf(DockSide side, double extent)
    {
        var length = side is DockSide.Left or DockSide.Right ? _host?.Bounds.Width ?? 0 : _host?.Bounds.Height ?? 0;
        return length > 0 ? Math.Clamp(extent / length, 0.1, 0.5) : DockWorkspace.EdgeShare;
    }

    private void AttachStrips()
    {
        foreach (var side in (ReadOnlySpan<DockSide>)[DockSide.Left, DockSide.Right, DockSide.Top, DockSide.Bottom])
        {
            if (_strips[(int)side] is { } strip)
            {
                strip.Side = side;
                strip.Attach(_listening ? this : null, _listening ? _workspace.AutoHidden(side) : null);
            }
        }
    }

    private void SetStripSelection(DockPane? pane)
    {
        foreach (var strip in _strips)
        {
            strip?.SetSelected(pane is not null && pane.HiddenSide == strip.Side ? pane : null);
        }
    }

    /// <summary>A strip button was clicked or pressed: slide its pane out, or back in when it is out.</summary>
    internal void ToggleAutoHidden(DockPane pane, NavigationMethod method)
    {
        if (_flyout is { IsOpen: true } flyout && ReferenceEquals(flyout.Pane, pane))
        {
            HideAutoHidden(animate: true, returnFocus: method);
            return;
        }
        Activate(pane);
    }

    /// <summary>Slides out an auto-hidden pane (it is, or is about to become, the active pane).</summary>
    private void ShowAutoHidden(DockPane pane)
    {
        if (_flyout is null || pane.HiddenSide is not { } side)
        {
            return;
        }
        if (!ReferenceEquals(_flyout.Pane, pane) || !_flyout.IsOpen)
        {
            _flyout.Show(pane, side, pane.AutoHideExtent > 0 ? pane.AutoHideExtent : pane.EffectivePreferredSize);
            _flyout.Card.CanPin = !IsLocked;
            SetStripSelection(pane);
            WatchOutside();
            // Build the card's template and the pane's now, so focus can move into the content straight away.
            _flyout.UpdateLayout();
        }
    }

    /// <summary>
    /// Slides the flyout back in. With <paramref name="returnFocus"/>, focus that was inside it goes to the pane's strip
    /// button; the active pane goes back to the one that was active before the flyout opened.
    /// </summary>
    private void HideAutoHidden(bool animate, NavigationMethod? returnFocus)
    {
        if (_flyout is not { IsOpen: true } flyout || flyout.Pane is not { } pane)
        {
            return;
        }
        var hadFocus = flyout.Card.IsKeyboardFocusWithin;
        UnwatchOutside();
        SetStripSelection(null);
        if (returnFocus is { } method && hadFocus)
        {
            if (pane.HiddenSide is { } side && StripOf(side)?.ButtonOf(pane) is { } button)
            {
                button.Focus(method);
            }
            else
            {
                Focus(method);
            }
        }
        flyout.Hide(animate);
        if (ReferenceEquals(ActivePane, pane))
        {
            var back = _paneBeforeFlyout;
            SetCurrentValue(ActivePaneProperty,
                back is { State: not DockPaneState.AutoHidden } && ReferenceEquals(back.Workspace, _workspace) ? back : FallbackActivePane());
        }
        _paneBeforeFlyout = null;
    }

    /// <summary>Moves focus into the flyout card when the shown pane's content has nothing focusable.</summary>
    private void FocusAutoHideCard(DockPane pane)
    {
        if (_flyout?.Pane is { } shown && ReferenceEquals(shown, pane))
        {
            _flyout.Card.Focus(NavigationMethod.Tab);
        }
    }

    #region Click outside, focus leaving

    private void WatchOutside()
    {
        var top = TopLevel.GetTopLevel(this);
        if (ReferenceEquals(top, _outsideWatch))
        {
            return;
        }
        UnwatchOutside();
        _outsideWatch = top;
        if (top is not null)
        {
            top.AddHandler(PointerPressedEvent, OnOutsidePointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            top.AddHandler(GotFocusEvent, OnOutsideGotFocus, RoutingStrategies.Bubble, handledEventsToo: true);
        }
    }

    private void UnwatchOutside()
    {
        if (_outsideWatch is { } top)
        {
            top.RemoveHandler(PointerPressedEvent, OnOutsidePointerPressed);
            top.RemoveHandler(GotFocusEvent, OnOutsideGotFocus);
        }
        _outsideWatch = null;
    }

    /// <summary>True for the flyout card and grip, the shown pane's strip button, and popups (a menu opened from the card).</summary>
    private bool IsInsideFlyout(Visual visual)
    {
        if (_flyout is null)
        {
            return false;
        }
        if (ReferenceEquals(visual, _flyout.Card) || _flyout.Card.IsVisualAncestorOf(visual)
            || ReferenceEquals(visual, _flyout.Grip))
        {
            return true;
        }
        if (_flyout.Pane is { HiddenSide: { } side } pane && StripOf(side)?.ButtonOf(pane) is { } button
            && (ReferenceEquals(button, visual) || button.IsVisualAncestorOf(visual)))
        {
            return true;
        }
        return TopLevel.GetTopLevel(visual) is PopupRoot || visual.FindAncestorOfType<OverlayPopupHost>(includeSelf: true) is not null;
    }

    private void OnOutsidePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_flyout is { IsOpen: true } && e.Source is Visual source && !IsInsideFlyout(source))
        {
            HideAutoHidden(animate: true, returnFocus: null);
        }
    }

    private void OnOutsideGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (_flyout is not { IsOpen: true } || e.Source is not Visual source || IsInsideFlyout(source))
        {
            return;
        }
        // The switcher hands focus back itself when it closes; let it choose.
        if (_switcher is not null && (ReferenceEquals(source, _switcher) || _switcher.IsVisualAncestorOf(source)))
        {
            return;
        }
        Dispatcher.UIThread.Post(() =>
        {
            if (_flyout is { IsOpen: true } && !_flyout.Card.IsKeyboardFocusWithin)
            {
                HideAutoHidden(animate: true, returnFocus: null);
            }
        }, DispatcherPriority.Input);
    }

    #endregion

    #region Menu rows

    /// <summary>Auto hide (docked tools) or Pin (auto-hidden panes), after the close commands.</summary>
    private void AddAutoHideRows(List<MenuEntry> rows, DockPane pane)
    {
        var locked = IsLocked;
        if (pane.State == DockPaneState.AutoHidden)
        {
            var pin = Row(DockText.Get(DockLangResourceKind.Pin), !locked, () => Pin(pane), DockGlyphs.Pin);
            pin.HasSeparatorBefore = true;
            rows.Add(pin);
            return;
        }
        if (!pane.IsDocument && pane.State == DockPaneState.Docked && pane.Group is { Float: null })
        {
            var autoHide = Row(DockText.Get(DockLangResourceKind.AutoHide), !locked && CanAutoHideNow(pane), () =>
            {
                if (AutoHide(pane))
                {
                    // The menu hands focus back to the tab, which has gone; put it on the new active pane's tab.
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (ActivePane is { } active)
                        {
                            FocusTab(active);
                        }
                    }, DispatcherPriority.Loaded);
                }
            }, DockGlyphs.PinOff);
            autoHide.HasSeparatorBefore = true;
            rows.Add(autoHide);
        }
    }

    #endregion
}
