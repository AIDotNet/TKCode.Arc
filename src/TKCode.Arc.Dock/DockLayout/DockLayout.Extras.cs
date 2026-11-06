using System.Windows.Input;
using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// The parts added in M3 and M5 (auto-hide strips and flyout, pane switcher, maximize, item sources) hook into the layout
/// here: one call from each place of <c>DockLayout.cs</c> that needs them.
/// </summary>
[TemplatePart(LeftStripPart, typeof(DockAutoHideStrip))]
[TemplatePart(RightStripPart, typeof(DockAutoHideStrip))]
[TemplatePart(TopStripPart, typeof(DockAutoHideStrip))]
[TemplatePart(BottomStripPart, typeof(DockAutoHideStrip))]
[TemplatePart(FlyoutPart, typeof(DockAutoHideFlyout))]
[TemplatePart(SwitcherPart, typeof(DockPaneSwitcher))]
public partial class DockLayout
{
    public const string LeftStripPart = "PART_LeftStrip";
    public const string RightStripPart = "PART_RightStrip";
    public const string TopStripPart = "PART_TopStrip";
    public const string BottomStripPart = "PART_BottomStrip";
    public const string FlyoutPart = "PART_Flyout";
    public const string SwitcherPart = "PART_Switcher";
    public const string MaximizedPseudoClass = ":maximized";

    private DockCommand? _autoHideActiveCommand;
    private DockCommand? _showPaneSwitcherCommand;
    private DockCommand? _toggleMaximizeCommand;

    /// <summary>
    /// Auto-hides the active pane (a docked tool that can auto-hide, while the layout is not locked); bind it to a menu or key.
    /// </summary>
    public ICommand AutoHideActiveCommand => _autoHideActiveCommand ??= new DockCommand(
        () =>
        {
            if (ActivePane is { } pane)
            {
                AutoHide(pane);
            }
        },
        () => !IsLocked && ActivePane is { } pane && CanAutoHideNow(pane));

    /// <summary>Opens the pane switcher (the list of panes, most recently used first); bind it to a menu or key.</summary>
    public ICommand ShowPaneSwitcherCommand => _showPaneSwitcherCommand ??= new DockCommand(
        () => ShowPaneSwitcher(holding: false, direction: 1),
        () => _workspace.AllPanes().Any());

    /// <summary>Maximizes the active pane's group, or restores the layout while a group is maximized; bind it to a menu or key.</summary>
    public ICommand ToggleMaximizeCommand => _toggleMaximizeCommand ??= new DockCommand(
        () =>
        {
            if (MaximizedGroup is { } maximized)
            {
                ToggleMaximize(maximized);
            }
            else if (ActivePane?.Group is { } group)
            {
                ToggleMaximize(group);
            }
        },
        () => MaximizedGroup is not null || (ActivePane?.Group is { } group && CanMaximize(group)));

    /// <summary>
    /// Whether the layout listens to its workspace. It stops while it is out of the visual tree, so a workspace the host
    /// keeps (a view-model's) does not keep a closed layout and its views alive; it builds its views again when it is shown.
    /// </summary>
    private bool _listening = true;

    /// <summary>Called when the layout is attached (from <c>OnAttachedToVisualTree</c>, before the floats are shown).</summary>
    private void OnAttachedExtras()
    {
        if (!_listening)
        {
            _listening = true;
            _workspace.Changed += OnWorkspaceChanged;
            AttachStrips();
            UpdateRootView();
        }
    }

    /// <summary>Called when the layout is detached (from <c>OnDetachedFromVisualTree</c>, after the floats were dismissed).</summary>
    private void OnDetachedExtras()
    {
        HideAutoHidden(animate: false, returnFocus: null);
        UnwatchOutside();
        ClosePaneSwitcher(restoreFocus: false);
        _menu?.Close(returnFocus: false);
        _menu?.Items.Clear();
        if (_maximized is not null)
        {
            _maximized = null;
            PseudoClasses.Set(MaximizedPseudoClass, false);
        }
        if (!_listening)
        {
            return;
        }
        _listening = false;
        _workspace.Changed -= OnWorkspaceChanged;
        foreach (var strip in _strips)
        {
            strip?.Attach(null, null);
        }
        _host?.Children.Clear();
        ReleaseViews(all: true);
    }

    /// <summary>Nodes this layout made a view for; a node that left the workspace gives its view up.</summary>
    private readonly HashSet<DockNode> _viewNodes = [];

    /// <summary>
    /// Releases the views of nodes that left the workspace (<paramref name="all"/>: of every node), so a removed group the host
    /// still holds (a pane's last place, say) keeps no view and no layout alive.
    /// </summary>
    private void ReleaseViews(bool all)
    {
        if (_viewNodes.Count == 0)
        {
            return;
        }
        foreach (var node in _viewNodes.ToList())
        {
            if (!all && ReferenceEquals(node.Workspace, _workspace))
            {
                continue;
            }
            _viewNodes.Remove(node);
            if (node.View is IDockView view && ReferenceEquals(view.Layout, this))
            {
                view.Release();
                node.View = null;
            }
        }
    }

    private void InitializeExtras()
    {
        AddHandler(KeyDownEvent, OnTunnelKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnTunnelKeyUp, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnBubbleKeyDown, RoutingStrategies.Bubble);
    }

    private void ApplyExtraParts(INameScope scope)
    {
        foreach (var strip in _strips)
        {
            strip?.Attach(null, null);
        }
        _flyout?.Attach(null);
        _strips[(int)DockSide.Left] = scope.Find<DockAutoHideStrip>(LeftStripPart);
        _strips[(int)DockSide.Right] = scope.Find<DockAutoHideStrip>(RightStripPart);
        _strips[(int)DockSide.Top] = scope.Find<DockAutoHideStrip>(TopStripPart);
        _strips[(int)DockSide.Bottom] = scope.Find<DockAutoHideStrip>(BottomStripPart);
        _flyout = scope.Find<DockAutoHideFlyout>(FlyoutPart);
        _flyout?.Attach(this);
        if (_switcher is not null)
        {
            _switcher.Layout = null;
        }
        _switcher = scope.Find<DockPaneSwitcher>(SwitcherPart);
        if (_switcher is not null)
        {
            _switcher.Layout = this;
        }
        AttachStrips();
    }

    private void OnWorkspaceReplaced(DockWorkspace old)
    {
        if (!_listening)
        {
            // Out of the visual tree: start listening when shown.
            _workspace.Changed -= OnWorkspaceChanged;
        }
        HideAutoHidden(animate: false, returnFocus: null);
        ClosePaneSwitcher(restoreFocus: false);
        if (_maximized is not null)
        {
            _maximized = null;
            PseudoClasses.Set(MaximizedPseudoClass, false);
        }
        _recent.Clear();
        ReleaseViews(all: false);
        AttachStrips();
        MoveSourcePanes(old);
    }

    private void OnWorkspaceChangedExtras()
    {
        if (_flyout?.Pane is { } shown && (shown.State != DockPaneState.AutoHidden || !ReferenceEquals(shown.Workspace, _workspace)))
        {
            HideAutoHidden(animate: false, returnFocus: null);
        }
        if (_maximized is not null && MaximizedGroup is null)
        {
            RestoreMaximized();
        }
        _recent.RemoveAll(p => !ReferenceEquals(p.Workspace, _workspace));
        ReleaseViews(all: false);
        RaiseExtraCommandsChanged();
    }

    private void OnActivePaneChangedExtras(DockPane? old, DockPane? pane)
    {
        if (pane is not null)
        {
            _recent.Remove(pane);
            _recent.Insert(0, pane);
        }
        if (pane is { State: DockPaneState.AutoHidden })
        {
            if (old is not null && old.State != DockPaneState.AutoHidden)
            {
                _paneBeforeFlyout = old;
            }
            ShowAutoHidden(pane);
        }
        else if (_flyout is { IsOpen: true })
        {
            HideAutoHidden(animate: true, returnFocus: null);
        }
        if (_maximized is not null && pane is { State: DockPaneState.Docked, Group: { } group } && !ReferenceEquals(group, _maximized))
        {
            RestoreMaximized();
        }
        RaiseExtraCommandsChanged();
    }

    private void OnLockedChangedExtras()
    {
        foreach (var strip in _strips)
        {
            strip?.Refresh();
        }
        if (_flyout is not null)
        {
            _flyout.Card.CanPin = !IsLocked;
            _flyout.Grip.IsVisible = _flyout.IsOpen && !IsLocked;
        }
        RaiseExtraCommandsChanged();
    }

    private void RaiseExtraCommandsChanged()
    {
        _autoHideActiveCommand?.RaiseCanExecuteChanged();
        _showPaneSwitcherCommand?.RaiseCanExecuteChanged();
        _toggleMaximizeCommand?.RaiseCanExecuteChanged();
    }

    #region Keyboard

    private void OnTunnelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }
        var ctrl = (e.KeyModifiers & KeyModifiers.Control) != 0;
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        if (_switcher is { IsOpen: true } switcher)
        {
            switch (e.Key)
            {
                case Key.Tab:
                    e.Handled = true;
                    switcher.Move(shift ? -1 : 1);
                    return;
                case Key.Down:
                case Key.Right:
                    e.Handled = true;
                    switcher.Move(1);
                    return;
                case Key.Up:
                case Key.Left:
                    e.Handled = true;
                    switcher.Move(-1);
                    return;
                case Key.Enter:
                case Key.Space:
                    e.Handled = true;
                    CommitPaneSwitcher();
                    return;
                case Key.Escape:
                    e.Handled = true;
                    ClosePaneSwitcher(restoreFocus: true);
                    return;
            }
            return;
        }
        if (e.Key == Key.Tab && ctrl)
        {
            if (ShowPaneSwitcher(holding: true, direction: shift ? -1 : 1))
            {
                e.Handled = true;
            }
        }
    }

    private void OnTunnelKeyUp(object? sender, KeyEventArgs e)
    {
        if (_switcher is { IsOpen: true } && _switcherHolding && e.Key is Key.LeftCtrl or Key.RightCtrl)
        {
            e.Handled = true;
            CommitPaneSwitcher();
        }
    }

    private void OnBubbleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.Shift)
        {
            var pane = PaneOf(e.Source);
            if (pane is { State: DockPaneState.AutoHidden } && ReferenceEquals(_flyout?.Pane, pane) && _flyout.IsOpen)
            {
                e.Handled = true;
                HideAutoHidden(animate: true, returnFocus: NavigationMethod.Tab);
            }
            else if (pane is not null && !IsLocked && CanAutoHideNow(pane))
            {
                e.Handled = true;
                AutoHide(pane);
            }
            return;
        }
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && _flyout is { IsOpen: true } flyout
            && e.Source is Visual source && IsInsideFlyout(source))
        {
            e.Handled = true;
            HideAutoHidden(animate: true, returnFocus: NavigationMethod.Tab);
        }
    }

    /// <summary>The pane a key event came from: a pane's content, its tab, or the flyout card showing it.</summary>
    private DockPane? PaneOf(object? source)
    {
        if (source is not Visual visual)
        {
            return null;
        }
        if (visual is DockTab tab)
        {
            return tab.Pane;
        }
        if (_flyout?.Pane is { } shown && (ReferenceEquals(visual, _flyout.Card) || _flyout.Card.IsVisualAncestorOf(visual)))
        {
            return shown;
        }
        var pane = visual as DockPane ?? visual.FindAncestorOfType<DockPane>();
        return pane is not null && ReferenceEquals(pane.Workspace, _workspace) ? pane : null;
    }

    #endregion
}
