using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

// Opening and closing. The ring mounts on the overlay layer of its target (the window's, or the nearest ArcOverlayHost)
// inside a RadialMenuHost instead of a Popup: it is centred on a point, shifted into view near the edges, and a gesture
// captures the pointer to it, which needs the ring in the same top level as the press.
//
// Enter and exit are code-driven (MotionValue): the ring is mounted on the frame it opens, before it is loaded, and
// ArcMotion.Tracks deliberately gives no motion pseudo-classes before Loaded; and the exit must finish before the ring
// leaves the layer and Closed is raised, which a ControlTheme transition cannot report. Highlight and press motion stay
// in the ControlTheme.
public partial class RadialMenu
{
    private const double EnterScale = 0.86;
    private const double ExitScale = 0.94;

    private static readonly Easing s_linear = new LinearEasing();

    private enum PopupState
    {
        Closed,
        Opening,
        Open,
        Closing
    }

    private enum OpenSource
    {
        Program,
        Pointer,
        Keyboard
    }

    private RadialMenuHost? _host;
    private Panel? _layer;
    private Control? _openTarget;
    private PopupState _state;
    private int _generation;
    private Point _requestedCenter;
    private bool _parentedByMenu;
    private Control? _homeTarget;
    private IInputElement? _focusOrigin;
    private MotionValue<double>? _opacity;
    private MotionValue<double>? _scale;
    private ScaleTransform? _scaleTransform;
    private bool _syncingIsOpen;
    private Control? _trackedTarget;
    private Point? _lastPointer;

    /// <summary>The host the floating ring is mounted in (tests).</summary>
    internal RadialMenuHost? Host => _host;

    /// <summary>The control the menu was last opened on.</summary>
    internal Control? OpenTarget => _openTarget;

    /// <summary>True while the menu is mounted (open, opening or playing its exit).</summary>
    internal bool IsMounted => _state != PopupState.Closed;

    /// <summary>
    /// Opens the menu on <see cref="PlacementTarget"/> (or the control it is attached to), centred as
    /// <see cref="Placement"/> says. Does nothing without a target or for a menu placed in a layout.
    /// </summary>
    public void Open()
    {
        var target = PlacementTarget ?? _homeTarget;
        if (target is null)
        {
            SyncIsOpen(_state is PopupState.Opening or PopupState.Open);
            return;
        }
        OpenCore(target, PlacementPoint(target), OpenSource.Program, null);
    }

    /// <summary>Opens the menu on <paramref name="target"/>, centred as <see cref="Placement"/> says.</summary>
    public void Open(Control target)
    {
        ArgumentNullException.ThrowIfNull(target);
        OpenCore(target, PlacementPoint(target), OpenSource.Program, null);
    }

    /// <summary>
    /// Opens the menu centred on <paramref name="position"/> in <paramref name="target"/>'s coordinates (shifted into
    /// view near the edges). Calling it again while open moves the menu to the new point.
    /// </summary>
    public void OpenAt(Control target, Point position)
    {
        ArgumentNullException.ThrowIfNull(target);
        OpenCore(target, position, OpenSource.Program, null);
    }

    /// <summary>Closes the menu (with its exit motion) and gives focus back to where it was.</summary>
    public void Close() => CloseCore(returnFocus: true);

    /// <summary>Opens at a pointer press that is still held: the start of a press-drag-release gesture.</summary>
    internal void OpenForGesture(Control target, Point position, IPointer pointer) => OpenCore(target, position, OpenSource.Pointer, pointer);

    /// <summary>Opens on <paramref name="target"/>'s centre with the first item highlighted (ContextMenu key, Shift+F10).</summary>
    internal void OpenFromKeyboard(Control target) =>
        OpenCore(target, new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), OpenSource.Keyboard, null);

    /// <summary>Opens from a click on <paramref name="target"/>: at the click point with pointer placement, else on its centre.</summary>
    internal void OpenFromClick(Control target, Point? position, bool fromKeyboard)
    {
        var point = Placement == RadialMenuPlacement.Pointer && position is { } p ? p : PlacementPoint(target);
        OpenCore(target, point, fromKeyboard ? OpenSource.Keyboard : OpenSource.Pointer, null);
    }

    private Point PlacementPoint(Control target)
    {
        if (Placement == RadialMenuPlacement.Pointer && ReferenceEquals(_trackedTarget, target) && _lastPointer is { } pointer)
        {
            return pointer;
        }
        return new Point(target.Bounds.Width / 2, target.Bounds.Height / 2);
    }

    /// <summary>Remembers where the pointer last was over <paramref name="target"/> (pointer placement).</summary>
    internal void TrackPointer(Control target, Point position)
    {
        _trackedTarget = target;
        _lastPointer = position;
    }

    private Control? _pointerSource;

    private void UpdatePointerTracking()
    {
        var target = Placement == RadialMenuPlacement.Pointer ? PlacementTarget : null;
        if (ReferenceEquals(target, _pointerSource))
        {
            return;
        }
        _pointerSource?.RemoveHandler(PointerMovedEvent, OnPlacementTargetPointerMoved);
        _pointerSource = target;
        target?.AddHandler(PointerMovedEvent, OnPlacementTargetPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnPlacementTargetPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is Control target)
        {
            TrackPointer(target, e.GetPosition(target));
        }
    }

    private void OnIsOpenChanged(bool open)
    {
        if (_syncingIsOpen)
        {
            return;
        }
        if (open && _state is PopupState.Closed or PopupState.Closing)
        {
            Open();
        }
        else if (!open && _state is PopupState.Opening or PopupState.Open)
        {
            CloseCore(returnFocus: true);
        }
    }

    private void SyncIsOpen(bool open)
    {
        if (IsOpen == open)
        {
            return;
        }
        _syncingIsOpen = true;
        try
        {
            SetCurrentValue(IsOpenProperty, open);
        }
        finally
        {
            _syncingIsOpen = false;
        }
    }

    private void OpenCore(Control target, Point position, OpenSource source, IPointer? gesturePointer)
    {
        var topLevel = TopLevel.GetTopLevel(target);
        var layer = topLevel is null || IsInline ? null : ArcOverlayLayer.Find(target);
        if (layer is null)
        {
            // Not in a window, or placed in a layout: nothing to float.
            SyncIsOpen(_state is PopupState.Opening or PopupState.Open);
            return;
        }
        var mounted = _state != PopupState.Closed;
        if (mounted && !ReferenceEquals(layer, _layer))
        {
            Unmount(raise: true);
            mounted = false;
        }
        var wasClosing = _state == PopupState.Closing;
        _generation++;
        EndGesture();
        ClearPress();
        if (!mounted || wasClosing)
        {
            _focusOrigin = topLevel!.FocusManager?.GetFocusedElement();
        }
        AdoptTarget(target);
        _host ??= new RadialMenuHost(this);
        if (!mounted)
        {
            _layer = layer;
            SizeHost();
            layer.Children.Add(_host);
            layer.SizeChanged += OnLayerSizeChanged;
        }
        _host.IsHitTestVisible = true;
        IsHitTestVisible = true;
        _host.ZIndex = int.MaxValue;
        _requestedCenter = target.TranslatePoint(position, layer) ?? position;
        PlaceHost();
        // Lay out now, so the first frame, hit-testing and the gesture all see the ring at its place.
        var size = new Size(_host.Width, _host.Height);
        _host.Measure(size);
        _host.Arrange(new Rect(size));
        _state = PopupState.Opening;
        PseudoClasses.Set(OpenPseudoClass, true);
        SyncIsOpen(true);
        _ = RunEnter(fresh: !wasClosing);
        if (source == OpenSource.Keyboard)
        {
            HighlightFirstForKeyboard();
        }
        else
        {
            _keyboardModality = false;
            Focus(NavigationMethod.Pointer);
        }
        if (gesturePointer is not null)
        {
            BeginGesture(gesturePointer, _requestedCenter - Bounds.Position);
        }
        if (!mounted)
        {
            RaiseEvent(new RoutedEventArgs(OpenedEvent, this));
        }
    }

    /// <summary>
    /// Makes <paramref name="target"/> the open target: the menu's logical parent while it is open (so its DataContext,
    /// resources and styles follow the target) and the source of its motion level.
    /// </summary>
    private void AdoptTarget(Control target)
    {
        if (!ReferenceEquals(_openTarget, target))
        {
            if (_openTarget is not null)
            {
                _openTarget.DetachedFromVisualTree -= OnTargetDetached;
            }
            _openTarget = target;
            target.DetachedFromVisualTree += OnTargetDetached;
        }
        if (!ReferenceEquals(Parent, target) && (Parent is null || _parentedByMenu))
        {
            SetLogicalParent(target);
        }
        else if (_parentedByMenu && !((ILogical)this).IsAttachedToLogicalTree && ((ILogical)target).IsAttachedToLogicalTree)
        {
            // Attached to the control before the control joined a window: the logical attach that applies styles and
            // resources never reached the menu (it is not one of the control's logical children). Parent it again.
            SetLogicalParent(null);
            SetLogicalParent(target);
        }
        if (!ReferenceEquals(Parent, target))
        {
            // Parented elsewhere (an app's own logical tree): still animate at the target's motion level.
            SetCurrentValue(ArcMotion.LevelProperty, ArcMotion.GetEffectiveLevel(target));
        }
    }

    private void SetLogicalParent(StyledElement? parent)
    {
        if (ReferenceEquals(Parent, parent))
        {
            return;
        }
        if (_resourceSource is not null)
        {
            _resourceSource.ResourcesChanged -= OnParentResourcesChanged;
            _resourceSource = null;
        }
        if (Parent is not null)
        {
            ((ISetLogicalParent)this).SetParent(null);
        }
        if (parent is not null)
        {
            ((ISetLogicalParent)this).SetParent(parent);
            // The menu is not in the parent's logical children, so resource changes (a new accent, a theme applied)
            // that travel down the logical tree would pass it by: forward them while it hangs off the parent.
            _resourceSource = parent;
            parent.ResourcesChanged += OnParentResourcesChanged;
        }
        _parentedByMenu = parent is not null;
    }

    private StyledElement? _resourceSource;

    private void OnParentResourcesChanged(object? sender, ResourcesChangedEventArgs e) =>
        ((IResourceHost)this).NotifyHostedResourcesChanged(e);

    private void OnTargetDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_state != PopupState.Closed)
        {
            // The target left the window (a page navigated away): remove at once.
            EndGesture();
            ClearPress();
            SyncIsOpen(false);
            Unmount(raise: true);
        }
    }

    internal void CloseCore(bool returnFocus, bool animate = true)
    {
        if (_state is PopupState.Closed or PopupState.Closing)
        {
            SyncIsOpen(false);
            return;
        }
        EndGesture();
        ClearPress();
        _generation++;
        _state = PopupState.Closing;
        PseudoClasses.Set(OpenPseudoClass, false);
        // A leaving ring lets clicks through, so the target can reopen it mid-exit.
        if (_host is not null)
        {
            _host.IsHitTestVisible = false;
        }
        IsHitTestVisible = false;
        if (returnFocus && IsKeyboardFocusWithin)
        {
            ReturnFocus();
        }
        SyncIsOpen(false);
        _ = RunExit(animate);
    }

    /// <summary>A press on the host outside the ring: closes, and a right press on the attached control reopens there.</summary>
    internal void OnOutsidePressed(PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(null).Properties;
        if (properties.IsRightButtonPressed && Trigger.HasFlag(RadialMenuTrigger.RightPress) &&
            _homeTarget is { } home && IsAttached(home) && home.IsEffectivelyVisible)
        {
            var point = e.GetPosition(home);
            if (new Rect(home.Bounds.Size).Contains(point))
            {
                OpenForGesture(home, point, e.Pointer);
                return;
            }
        }
        _keyboardModality = false;
        CloseCore(returnFocus: true);
    }

    private static bool IsAttached(Visual visual) => Avalonia.VisualTree.VisualExtensions.IsAttachedToVisualTree(visual);

    private void ReturnFocus()
    {
        var method = _keyboardModality ? NavigationMethod.Tab : NavigationMethod.Pointer;
        if (_focusOrigin is Visual origin && IsAttached(origin) && _focusOrigin is InputElement { IsEffectivelyEnabled: true } element &&
            !ReferenceEquals(element, this) && !this.IsVisualAncestorOf(origin))
        {
            element.Focus(method);
            return;
        }
        if (_openTarget is { Focusable: true } target)
        {
            target.Focus(method);
        }
    }

    private void Unmount(bool raise)
    {
        if (_layer is { } layer)
        {
            layer.SizeChanged -= OnLayerSizeChanged;
            if (_host is { } host)
            {
                layer.Children.Remove(host);
            }
        }
        if (_openTarget is not null)
        {
            _openTarget.DetachedFromVisualTree -= OnTargetDetached;
        }
        _openTarget = null;
        _layer = null;
        _focusOrigin = null;
        _generation++;
        _state = PopupState.Closed;
        PseudoClasses.Set(OpenPseudoClass, false);
        if (_parentedByMenu)
        {
            // An attached menu goes back to its control (bindings keep working); any other menu is let go.
            SetLogicalParent(_homeTarget);
        }
        if (raise)
        {
            RaiseEvent(new RoutedEventArgs(ClosedEvent, this));
        }
    }

    #region Placement

    private void OnLayerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        SizeHost();
        PlaceHost();
    }

    private Size LayerSize()
    {
        var size = _layer?.Bounds.Size ?? default;
        if ((size.Width <= 0 || size.Height <= 0) && _layer is ArcOverlayLayer { Host: { } host })
        {
            size = host.Bounds.Size;
        }
        if ((size.Width <= 0 || size.Height <= 0) && _openTarget is not null && TopLevel.GetTopLevel(_openTarget) is { } topLevel)
        {
            size = topLevel.ClientSize;
        }
        return size;
    }

    private void SizeHost()
    {
        if (_host is null)
        {
            return;
        }
        var size = LayerSize();
        _host.Width = size.Width;
        _host.Height = size.Height;
        Canvas.SetLeft(_host, 0);
        Canvas.SetTop(_host, 0);
    }

    /// <summary>Centres the ring on the requested point, shifted so the whole ring stays inside the viewport.</summary>
    private void PlaceHost()
    {
        if (_host is null)
        {
            return;
        }
        var size = LayerSize();
        var margin = ViewportMargin;
        var bounds = new Rect(margin, margin, Math.Max(0, size.Width - 2 * margin), Math.Max(0, size.Height - 2 * margin));
        var extent = RadialMenuLayout.Extent(EffectiveInnerRadius, EffectiveOuterRadius, PlatePadding, StartAngle, SweepAngle);
        _host.Center = _requestedCenter + RadialMenuLayout.Shift(_requestedCenter, extent, bounds);
    }

    /// <summary>The ring's centre in overlay coordinates (tests).</summary>
    internal Point? HostCenter => _host?.Center;

    /// <summary>The point the ring was asked to open at, in overlay coordinates (tests).</summary>
    internal Point RequestedCenter => _requestedCenter;

    #endregion

    #region Motion

    private void EnsureMotion()
    {
        if (_opacity is not null)
        {
            return;
        }
        _scaleTransform = new ScaleTransform(1, 1);
        RenderTransform = _scaleTransform;
        RenderTransformOrigin = RelativePoint.Center;
        _opacity = new MotionValue<double>(this, 1, MotionVectors.Double);
        _opacity.Changed += value => Opacity = Math.Clamp(value, 0, 1);
        _scale = new MotionValue<double>(this, 1, MotionVectors.Double);
        _scale.Changed += value =>
        {
            _scaleTransform!.ScaleX = value;
            _scaleTransform.ScaleY = value;
        };
    }

    private async Task RunEnter(bool fresh)
    {
        var generation = _generation;
        EnsureMotion();
        var level = ArcMotion.GetEffectiveLevel(this);
        if (fresh)
        {
            _opacity!.Jump(0);
            _scale!.Jump(level == ArcMotionLevel.Full ? EnterScale : 1);
        }
        if (level == ArcMotionLevel.Off)
        {
            _opacity!.Jump(1);
            _scale!.Jump(1);
            CompleteEnter(generation);
            return;
        }
        Task<bool> fade;
        Task<bool> grow;
        if (level == ArcMotionLevel.Reduced)
        {
            _scale!.Jump(1);
            fade = _opacity!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
            grow = Task.FromResult(true);
        }
        else
        {
            fade = _opacity!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
            grow = _scale!.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
        }
        await fade;
        await grow;
        CompleteEnter(generation);
    }

    private void CompleteEnter(int generation)
    {
        if (generation == _generation && _state == PopupState.Opening)
        {
            _state = PopupState.Open;
        }
    }

    private async Task RunExit(bool animate)
    {
        var generation = _generation;
        EnsureMotion();
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!animate || level == ArcMotionLevel.Off || !IsAttached(this))
        {
            FinishExit(generation);
            return;
        }
        Task<bool> fade;
        Task<bool> shrink;
        if (level == ArcMotionLevel.Reduced)
        {
            fade = _opacity!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
            shrink = Task.FromResult(true);
        }
        else
        {
            fade = _opacity!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            shrink = _scale!.AnimateTo(ExitScale, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
        await fade;
        await shrink;
        FinishExit(generation);
    }

    private void FinishExit(int generation)
    {
        if (generation != _generation || _state != PopupState.Closing)
        {
            return;
        }
        Unmount(raise: true);
        _opacity?.Jump(1);
        _scale?.Jump(1);
    }

    #endregion
}
