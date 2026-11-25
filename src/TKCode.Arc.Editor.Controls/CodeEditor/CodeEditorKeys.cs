using Avalonia.Input;
using EditorKeyCode = TKCode.Arc.Editor.KeyCode;

namespace TKCode.Arc;

/// <summary>
/// Maps Avalonia's keys onto the engine's key codes (upstream's EVENT_KEY_CODE_MAP maps browser key codes the same way):
/// a US layout reading, which is what the keybinding resolver's dispatch strings are written in.
/// </summary>
internal static class CodeEditorKeys
{
    public static EditorKeyCode ToKeyCode(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            return EditorKeyCode.KeyA + (key - Key.A);
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            return EditorKeyCode.Digit0 + (key - Key.D0);
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            return EditorKeyCode.Numpad0 + (key - Key.NumPad0);
        }

        if (key is >= Key.F1 and <= Key.F24)
        {
            return EditorKeyCode.F1 + (key - Key.F1);
        }

        return key switch
        {
            Key.Back => EditorKeyCode.Backspace,
            Key.Tab => EditorKeyCode.Tab,
            Key.Enter => EditorKeyCode.Enter,
            Key.LeftShift or Key.RightShift => EditorKeyCode.Shift,
            Key.LeftCtrl or Key.RightCtrl => EditorKeyCode.Ctrl,
            Key.LeftAlt or Key.RightAlt => EditorKeyCode.Alt,
            Key.LWin or Key.RWin => EditorKeyCode.Meta,
            Key.Pause => EditorKeyCode.PauseBreak,
            Key.CapsLock => EditorKeyCode.CapsLock,
            Key.Escape => EditorKeyCode.Escape,
            Key.Space => EditorKeyCode.Space,
            Key.PageUp => EditorKeyCode.PageUp,
            Key.PageDown => EditorKeyCode.PageDown,
            Key.End => EditorKeyCode.End,
            Key.Home => EditorKeyCode.Home,
            Key.Left => EditorKeyCode.LeftArrow,
            Key.Up => EditorKeyCode.UpArrow,
            Key.Right => EditorKeyCode.RightArrow,
            Key.Down => EditorKeyCode.DownArrow,
            Key.Insert => EditorKeyCode.Insert,
            Key.Delete => EditorKeyCode.Delete,
            Key.Apps => EditorKeyCode.ContextMenu,
            Key.NumLock => EditorKeyCode.NumLock,
            Key.Scroll => EditorKeyCode.ScrollLock,
            Key.OemSemicolon => EditorKeyCode.Semicolon,
            Key.OemPlus => EditorKeyCode.Equal,
            Key.OemComma => EditorKeyCode.Comma,
            Key.OemMinus => EditorKeyCode.Minus,
            Key.OemPeriod => EditorKeyCode.Period,
            Key.OemQuestion => EditorKeyCode.Slash,
            Key.OemTilde => EditorKeyCode.Backquote,
            Key.OemOpenBrackets => EditorKeyCode.BracketLeft,
            Key.OemPipe => EditorKeyCode.Backslash,
            Key.OemCloseBrackets => EditorKeyCode.BracketRight,
            Key.OemQuotes => EditorKeyCode.Quote,
            Key.OemBackslash => EditorKeyCode.IntlBackslash,
            _ => EditorKeyCode.Unknown,
        };
    }

    /// <summary>Whether <paramref name="key"/> is a modifier on its own (it never resolves a keybinding).</summary>
    public static bool IsModifier(Key key) =>
        key is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin;
}
