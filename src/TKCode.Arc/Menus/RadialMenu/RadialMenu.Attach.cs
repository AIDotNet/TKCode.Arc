using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

// The attached way to give any control a radial menu: arc:RadialMenu.Menu, opened by the menu's Trigger gestures.
public partial class RadialMenu
{
    /// <summary>How long a press must be held still before <see cref="RadialMenuTrigger.Hold"/> opens the menu.</summary>
    internal static readonly TimeSpan HoldDelay = TimeSpan.FromMilliseconds(400);

    /// <summary>How far a held pointer may move before the hold no longer counts.</summary>
    internal const double HoldSlop = 8;

    /// <summary>
    /// Attaches a radial menu to any control: it opens with the gestures in the menu's <see cref="Trigger"/> (right
    /// press and press-and-hold by default) and with the ContextMenu key or Shift+F10. The menu takes the control as its
    /// logical parent, so its bindings see the control's DataContext. Attach a menu to one control at a time.
    /// </summary>
    public static readonly AttachedProperty<RadialMenu?> MenuProperty =
        AvaloniaProperty.RegisterAttached<RadialMenu, Control, RadialMenu?>("Menu");

    private static readonly AttachedProperty<RadialMenuAttachment?> AttachmentProperty =
        AvaloniaProperty.RegisterAttached<RadialMenu, Control, RadialMenuAttachment?>("Attachment");

    // The press an attachment already acted on (by timestamp and pointer, so no event or control is kept alive): an inner
    // control's menu wins over its ancestors' menus.
    private static (ulong Timestamp, int Pointer) s_claimedPress;

    /// <summary>Gets the radial menu attached to <paramref name="element"/>.</summary>
    public static RadialMenu? GetMenu(Control element) => element.GetValue(MenuProperty);

    /// <summary>Attaches <paramref name="value"/> to <paramref name="element"/> (null detaches).</summary>
    public static void SetMenu(Control element, RadialMenu? value) => element.SetValue(MenuProperty, value);

    private static void OnMenuChanged(Control target, AvaloniaPropertyChangedEventArgs e)
    {
        target.GetValue(AttachmentProperty)?.Dispose();
        target.ClearValue(AttachmentProperty);
        if (e.NewValue is RadialMenu menu)
        {
            target.SetValue(AttachmentProperty, new RadialMenuAttachment(target, menu));
        }
    }

    /// <summary>The control the menu is attached to (null when it is not attached).</summary>
    internal Control? HomeTarget => _homeTarget;

    private void AttachTo(Control target)
    {
        _homeTarget = target;
        if (Parent is null || (_parentedByMenu && _state == PopupState.Closed))
        {
            SetLogicalParent(target);
        }
    }

    private void DetachFrom(Control target)
    {
        if (!ReferenceEquals(_homeTarget, target))
        {
            return;
        }
        if (ReferenceEquals(_openTarget, target) || _openTarget is null)
        {
            CloseCore(returnFocus: false, animate: false);
        }
        _homeTarget = null;
        if (_parentedByMenu && _state == PopupState.Closed)
        {
            SetLogicalParent(null);
        }
    }

    /// <summary>The trigger wiring of <see cref="MenuProperty"/> on one control.</summary>
    private sealed class RadialMenuAttachment : IDisposable
    {
        private readonly Control _target;
        private readonly RadialMenu _menu;
        private IDisposable? _holdTimer;
        private IPointer? _holdPointer;
        private Point _holdStart;

        public RadialMenuAttachment(Control target, RadialMenu menu)
        {
            _target = target;
            _menu = menu;
            menu.AttachTo(target);
            target.AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
            target.AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
            target.AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
            target.AddHandler(PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Bubble, handledEventsToo: true);
            target.AddHandler(ContextRequestedEvent, OnContextRequested);
            target.AddHandler(KeyDownEvent, OnKeyDown);
            target.AddHandler(InputElement.TappedEvent, OnTapped, RoutingStrategies.Bubble, handledEventsToo: true);
            if (target is AvaloniaButton button)
            {
                button.Click += OnButtonClick;
            }
            target.DetachedFromVisualTree += OnDetached;
        }

        private RadialMenuTrigger Trigger => _menu.Trigger;

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (s_claimedPress == (e.Timestamp, e.Pointer.Id) || _menu.IsMounted && _menu._state != PopupState.Closing)
            {
                return;
            }
            var point = e.GetCurrentPoint(_target);
            var properties = point.Properties;
            if (properties.IsRightButtonPressed && e.Pointer.Type == PointerType.Mouse && Trigger.HasFlag(RadialMenuTrigger.RightPress))
            {
                s_claimedPress = (e.Timestamp, e.Pointer.Id);
                CancelHold();
                e.Handled = true;
                _menu.OpenForGesture(_target, point.Position, e.Pointer);
                return;
            }
            if (properties.IsLeftButtonPressed && Trigger.HasFlag(RadialMenuTrigger.Hold))
            {
                s_claimedPress = (e.Timestamp, e.Pointer.Id);
                CancelHold();
                _holdPointer = e.Pointer;
                _holdStart = point.Position;
                _holdTimer = DispatcherTimer.RunOnce(OnHoldElapsed, HoldDelay);
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            var position = e.GetPosition(_target);
            _menu.TrackPointer(_target, position);
            if (_holdPointer is not null && ReferenceEquals(e.Pointer, _holdPointer) &&
                RadialMenuLayout.Distance(_holdStart, position) > HoldSlop)
            {
                CancelHold();
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (ReferenceEquals(e.Pointer, _holdPointer))
            {
                CancelHold();
            }
        }

        private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            // Another control taking the pointer (a drag starting) ends the hold; the menu taking it is the hold succeeding.
            if (ReferenceEquals(e.Pointer, _holdPointer) && !ReferenceEquals(e.Pointer.Captured, _menu))
            {
                CancelHold();
            }
        }

        /// <summary>Called by the hold timer: opens at the press point and carries on as a gesture.</summary>
        internal void OnHoldElapsed()
        {
            _holdTimer = null;
            if (_holdPointer is not { } pointer)
            {
                return;
            }
            _holdPointer = null;
            if (!_target.IsEffectivelyVisible || !_target.IsEffectivelyEnabled)
            {
                return;
            }
            _menu.OpenForGesture(_target, _holdStart, pointer);
        }

        private void CancelHold()
        {
            _holdTimer?.Dispose();
            _holdTimer = null;
            _holdPointer = null;
        }

        private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }
            if (_menu.IsMounted && _menu._state != PopupState.Closing)
            {
                e.Handled = true;
                return;
            }
            if (!e.TryGetPosition(_target, out _))
            {
                // The ContextMenu key or Shift+F10.
                e.Handled = true;
                _menu.OpenFromKeyboard(_target);
                return;
            }
            // A right click or a touch hold: the press already opened the menu when the trigger asks for it.
            if (Trigger.HasFlag(RadialMenuTrigger.RightPress) || Trigger.HasFlag(RadialMenuTrigger.Hold))
            {
                e.Handled = true;
            }
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            // The ContextMenu key and Shift+F10 on the focused control (Avalonia raises ContextRequested on key up; this
            // opens on key down like ContextMenu, and the request that follows finds the menu open).
            if (e.Handled || !ReferenceEquals(e.Source, _target) ||
                !(e.Key == Key.Apps || (e.Key == Key.F10 && e.KeyModifiers == KeyModifiers.Shift)))
            {
                return;
            }
            e.Handled = true;
            if (!_menu.IsMounted || _menu._state == PopupState.Closing)
            {
                _menu.OpenFromKeyboard(_target);
            }
        }

        private void OnTapped(object? sender, TappedEventArgs e)
        {
            if (_target is AvaloniaButton || !Trigger.HasFlag(RadialMenuTrigger.Click) || _menu.IsMounted && _menu._state != PopupState.Closing)
            {
                return;
            }
            _menu.OpenFromClick(_target, e.GetPosition(_target), fromKeyboard: false);
        }

        private void OnButtonClick(object? sender, RoutedEventArgs e)
        {
            if (!Trigger.HasFlag(RadialMenuTrigger.Click) || _menu.IsMounted && _menu._state != PopupState.Closing)
            {
                return;
            }
            var fromPointer = _target.IsPointerOver;
            _menu.OpenFromClick(_target, null, fromKeyboard: !fromPointer);
        }

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e) => CancelHold();

        public void Dispose()
        {
            CancelHold();
            _target.RemoveHandler(PointerPressedEvent, OnPointerPressed);
            _target.RemoveHandler(PointerMovedEvent, OnPointerMoved);
            _target.RemoveHandler(PointerReleasedEvent, OnPointerReleased);
            _target.RemoveHandler(PointerCaptureLostEvent, OnPointerCaptureLost);
            _target.RemoveHandler(ContextRequestedEvent, OnContextRequested);
            _target.RemoveHandler(KeyDownEvent, OnKeyDown);
            _target.RemoveHandler(InputElement.TappedEvent, OnTapped);
            if (_target is AvaloniaButton button)
            {
                button.Click -= OnButtonClick;
            }
            _target.DetachedFromVisualTree -= OnDetached;
            _menu.DetachFrom(_target);
        }
    }

    /// <summary>Fires a pending hold on <paramref name="target"/> at once (tests: headless timers only run in pushed frames).</summary>
    internal static void ElapseHold(Control target) => target.GetValue(AttachmentProperty)?.OnHoldElapsed();
}
