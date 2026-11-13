using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class OnScreenKeyboard
{
    /// <summary>
    /// Set on a text input (or any element that holds one): while it has focus, a keyboard of this kind opens under it and
    /// types into it. It closes when focus leaves, after Enter in a single-line text box, and on a <c>Hide</c> key; tapping
    /// the field opens it again. <c>None</c> (default) opens nothing unless <c>PopupLayout</c> is set.
    /// </summary>
    public static readonly AttachedProperty<OnScreenKeyboardPopupMode> PopupProperty =
        AvaloniaProperty.RegisterAttached<OnScreenKeyboard, Control, OnScreenKeyboardPopupMode>("Popup");

    /// <summary>Set on a text input: the layout of the keyboard that opens under it while it has focus (it overrides <c>Popup</c>).</summary>
    public static readonly AttachedProperty<OnScreenKeyboardLayout?> PopupLayoutProperty =
        AvaloniaProperty.RegisterAttached<OnScreenKeyboard, Control, OnScreenKeyboardLayout?>("PopupLayout");

    private static readonly ConditionalWeakTable<Control, PopupHost> s_popupHosts = new();

    /// <summary>Gets the kind of keyboard that opens under <paramref name="element"/> while it has focus.</summary>
    public static OnScreenKeyboardPopupMode GetPopup(Control element) => element.GetValue(PopupProperty);

    /// <summary>Sets the kind of keyboard that opens under <paramref name="element"/> while it has focus.</summary>
    public static void SetPopup(Control element, OnScreenKeyboardPopupMode value) => element.SetValue(PopupProperty, value);

    /// <summary>Gets the layout of the keyboard that opens under <paramref name="element"/> while it has focus.</summary>
    public static OnScreenKeyboardLayout? GetPopupLayout(Control element) => element.GetValue(PopupLayoutProperty);

    /// <summary>Sets the layout of the keyboard that opens under <paramref name="element"/> while it has focus.</summary>
    public static void SetPopupLayout(Control element, OnScreenKeyboardLayout? value) => element.SetValue(PopupLayoutProperty, value);

    /// <summary>The keyboard open under <paramref name="element"/>, or null (tests).</summary>
    internal static OnScreenKeyboard? GetOpenPopupKeyboard(Control element) =>
        s_popupHosts.TryGetValue(element, out var host) && host.IsOpen ? host.Keyboard : null;

    private static void RegisterPopupHandlers()
    {
        PopupProperty.Changed.AddClassHandler<Control>(static (element, _) => UpdatePopupHost(element));
        PopupLayoutProperty.Changed.AddClassHandler<Control>(static (element, _) => UpdatePopupHost(element));
    }

    private static void UpdatePopupHost(Control element)
    {
        var wanted = GetPopup(element) != OnScreenKeyboardPopupMode.None || GetPopupLayout(element) is not null;
        if (s_popupHosts.TryGetValue(element, out var host))
        {
            if (wanted)
            {
                host.Update();
            }
            else
            {
                host.Dispose();
                s_popupHosts.Remove(element);
            }
        }
        else if (wanted)
        {
            s_popupHosts.Add(element, new PopupHost(element));
        }
    }

    /// <summary>The keyboard popup of one element: created on first focus, reused afterwards.</summary>
    private sealed class PopupHost : IDisposable
    {
        private readonly Control _element;
        private ArcPopup? _popup;
        private OnScreenKeyboard? _keyboard;

        public PopupHost(Control element)
        {
            _element = element;
            element.GotFocus += OnGotFocus;
            element.LostFocus += OnLostFocus;
            element.DetachedFromVisualTree += OnDetached;
            // Handled presses too: a text box handles its own presses, and tapping the field reopens a hidden keyboard.
            element.AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
            if (element.IsKeyboardFocusWithin)
            {
                Open();
            }
        }

        public bool IsOpen => _popup is { IsOpen: true };

        public OnScreenKeyboard? Keyboard => _keyboard;

        public void Update()
        {
            if (_keyboard is not null)
            {
                Configure(_keyboard);
            }
        }

        private void Configure(OnScreenKeyboard keyboard)
        {
            keyboard.Preset = GetPopup(_element) switch
            {
                OnScreenKeyboardPopupMode.Numeric => OnScreenKeyboardPreset.Numeric,
                OnScreenKeyboardPopupMode.SignedNumeric => OnScreenKeyboardPreset.SignedNumeric,
                OnScreenKeyboardPopupMode.Digits => OnScreenKeyboardPreset.Digits,
                _ => OnScreenKeyboardPreset.Qwerty,
            };
            keyboard.Layout = GetPopupLayout(_element);
        }

        private void Open()
        {
            if (TopLevel.GetTopLevel(_element) is not { } topLevel)
            {
                return;
            }
            EnsurePopup();
            // The element itself, or the text box inside it that actually has focus.
            _keyboard!.Target = topLevel.FocusManager?.GetFocusedElement() is InputElement focused &&
                                (ReferenceEquals(focused, _element) || _element.IsVisualAncestorOf(focused))
                ? focused
                : _element;
            Configure(_keyboard);
            if (_popup!.Parent is null)
            {
                ((ISetLogicalParent)_popup).SetParent(_element);
            }
            _popup.IsOpen = true;
        }

        private void Close()
        {
            if (_popup is { IsOpen: true } popup)
            {
                popup.IsOpen = false;
            }
        }

        private void EnsurePopup()
        {
            if (_popup is not null)
            {
                return;
            }
            _keyboard = new OnScreenKeyboard();
            _keyboard.HideRequested += (_, _) => Close();
            _keyboard.ItemInvoked += OnItemInvoked;
            _popup = new ArcPopup
            {
                Child = _keyboard,
                PlacementTarget = _element,
                Placement = PlacementMode.Bottom,
                IsLightDismissEnabled = false,
                MarginToAnchor = 8,
                TakesFocusFromNativeControl = false,
                WindowManagerAddShadowHint = false,
            };
            _popup.Closed += (_, _) =>
            {
                if (_popup is { IsOpen: false, Parent: not null } closed)
                {
                    ((ISetLogicalParent)closed).SetParent(null);
                }
            };
        }

        private void OnItemInvoked(object? sender, OnScreenKeyboardItemInvokedEventArgs e)
        {
            if (e.Item.Kind != OnScreenKeyboardKeyKind.Enter || e.Target is TextBox { AcceptsReturn: true })
            {
                return;
            }
            // Enter in a single-line field finishes it: close once every handler has run (unless one took the key).
            Dispatcher.UIThread.Post(() =>
            {
                if (!e.Handled)
                {
                    Close();
                }
            });
        }

        private void OnGotFocus(object? sender, FocusChangedEventArgs e) => Open();

        private void OnLostFocus(object? sender, FocusChangedEventArgs e)
        {
            // Focus can move between parts of the element; close only once it has really left.
            Dispatcher.UIThread.Post(() =>
            {
                if (!_element.IsKeyboardFocusWithin)
                {
                    Close();
                }
            });
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!IsOpen && _element.IsKeyboardFocusWithin)
            {
                Open();
            }
        }

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e) => Close();

        public void Dispose()
        {
            _element.GotFocus -= OnGotFocus;
            _element.LostFocus -= OnLostFocus;
            _element.DetachedFromVisualTree -= OnDetached;
            _element.RemoveHandler(PointerReleasedEvent, OnPointerReleased);
            Close();
        }
    }
}
