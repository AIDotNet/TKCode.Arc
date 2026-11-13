using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The built-in layouts of <see cref="OnScreenKeyboard"/>.</summary>
public enum OnScreenKeyboardPreset
{
    /// <summary>Letters in the QWERTY arrangement, with a numbers and symbols page and a second symbols page.</summary>
    Qwerty,

    /// <summary>A number pad: digits, the culture's decimal separator, Backspace, Left, Right and Enter.</summary>
    Numeric,

    /// <summary>The number pad with a minus key, for values that can be negative.</summary>
    SignedNumeric,

    /// <summary>A PIN pad: digits, Backspace and Enter only.</summary>
    Digits,
}

/// <summary>Which keyboard <c>OnScreenKeyboard.Popup</c> opens under a text input that gets focus.</summary>
public enum OnScreenKeyboardPopupMode
{
    /// <summary>No keyboard opens (unless <c>OnScreenKeyboard.PopupLayout</c> is set).</summary>
    None,

    /// <summary>The QWERTY keyboard.</summary>
    Qwerty,

    /// <summary>The number pad.</summary>
    Numeric,

    /// <summary>The number pad with a minus key.</summary>
    SignedNumeric,

    /// <summary>The PIN pad.</summary>
    Digits,
}

/// <summary>What a key of an <see cref="OnScreenKeyboardLayout"/> does.</summary>
public enum OnScreenKeyboardKeyKind
{
    /// <summary>Types <see cref="OnScreenKeyboardKey.Text"/> (or <see cref="OnScreenKeyboardKey.ShiftText"/> while shifted).</summary>
    Text,

    /// <summary>Types a space.</summary>
    Space,

    /// <summary>Deletes the character before the caret, or the selection (sends <c>Back</c>). Repeats while held.</summary>
    Backspace,

    /// <summary>Sends <c>Enter</c>: a new line in a multi-line text box, otherwise the default button or your own handler.</summary>
    Enter,

    /// <summary>Sends <c>Tab</c>: moves focus to the next field (Shift+Tab with Shift on), or types a tab where it is accepted.</summary>
    Tab,

    /// <summary>Shift: one tap shifts the next key, a double tap locks capitals, another tap turns it off.</summary>
    Shift,

    /// <summary>Moves the caret left (with Shift on, extends the selection). Repeats while held.</summary>
    Left,

    /// <summary>Moves the caret right (with Shift on, extends the selection). Repeats while held.</summary>
    Right,

    /// <summary>Moves the caret up a line. Repeats while held.</summary>
    Up,

    /// <summary>Moves the caret down a line. Repeats while held.</summary>
    Down,

    /// <summary>Switches to the page named by <see cref="OnScreenKeyboardKey.TargetPage"/>.</summary>
    Page,

    /// <summary>Raises <see cref="OnScreenKeyboard.HideRequested"/>; a keyboard opened by <c>OnScreenKeyboard.Popup</c> closes.</summary>
    Hide,

    /// <summary>Sends <see cref="OnScreenKeyboardKey.KeyCode"/> as a key press.</summary>
    Key,

    /// <summary>Does nothing by itself: handle <see cref="OnScreenKeyboard.ItemInvoked"/> to give it a meaning (Clear, Done, ...).</summary>
    Custom,

    /// <summary>An empty gap of <see cref="OnScreenKeyboardKey.Width"/> units; it draws nothing and cannot be pressed.</summary>
    Spacer,
}

/// <summary>The Shift state of an <see cref="OnScreenKeyboard"/>.</summary>
public enum OnScreenKeyboardShiftState
{
    /// <summary>Lower case.</summary>
    Off,

    /// <summary>Shift for one key: the next key that types or sends something is shifted, then Shift turns off.</summary>
    On,

    /// <summary>Caps lock: letters stay in upper case until Shift is tapped again.</summary>
    Locked,
}

/// <summary>Arguments of <see cref="OnScreenKeyboard.ItemInvoked"/>: a key was pressed (or repeated while held).</summary>
public sealed class OnScreenKeyboardItemInvokedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for a press of <paramref name="item"/>.</summary>
    public OnScreenKeyboardItemInvokedEventArgs(RoutedEvent routedEvent, OnScreenKeyboardKey item, string? text, InputElement? target, bool isRepeat)
        : base(routedEvent)
    {
        Item = item;
        Text = text;
        Target = target;
        IsRepeat = isRepeat;
    }

    /// <summary>The key that was pressed.</summary>
    public OnScreenKeyboardKey Item { get; }

    /// <summary>The text the key is about to type, with Shift applied (a space for the space bar); null for keys that type no text.</summary>
    public string? Text { get; }

    /// <summary>The element that is about to receive the text or key press; null when nothing has focus and no target is set.</summary>
    public InputElement? Target { get; }

    /// <summary>True when the key repeats because it is held down.</summary>
    public bool IsRepeat { get; }
}
