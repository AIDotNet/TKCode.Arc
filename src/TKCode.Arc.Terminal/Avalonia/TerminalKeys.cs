using Avalonia.Input;
using TKCode.Terminal.Core;

namespace TKCode.Terminal.Avalonia;

/// <summary>
/// Avalonia 按键 → 物理键（W3C code，06-input.md §3）。只映射物理位置；字符由 TextInput 提供，
/// 所以布局相关的符号键也按物理位置映射（例如 OemQuestion → Slash）。无法映射的键返回 false。
/// </summary>
public static class TerminalKeys
{
    public static bool TryMap(Key key, out TerminalKey terminal)
    {
        terminal = key switch
        {
            >= Key.A and <= Key.Z => (TerminalKey)((int)TerminalKey.A + (key - Key.A)),
            >= Key.D0 and <= Key.D9 => (TerminalKey)((int)TerminalKey.Digit0 + (key - Key.D0)),
            >= Key.NumPad0 and <= Key.NumPad9 => (TerminalKey)((int)TerminalKey.Numpad0 + (key - Key.NumPad0)),
            >= Key.F1 and <= Key.F24 => (TerminalKey)((int)TerminalKey.F1 + (key - Key.F1)),
            Key.OemTilde => TerminalKey.Backquote,
            Key.OemMinus => TerminalKey.Minus,
            Key.OemPlus => TerminalKey.Equal,
            Key.OemOpenBrackets => TerminalKey.BracketLeft,
            Key.OemCloseBrackets => TerminalKey.BracketRight,
            Key.OemBackslash => TerminalKey.Backslash,
            Key.OemSemicolon => TerminalKey.Semicolon,
            Key.OemQuotes => TerminalKey.Quote,
            Key.OemComma => TerminalKey.Comma,
            Key.OemPeriod => TerminalKey.Period,
            Key.OemQuestion => TerminalKey.Slash,
            Key.Back => TerminalKey.Backspace,
            Key.Return => TerminalKey.Enter,
            Key.Tab => TerminalKey.Tab,
            Key.Space => TerminalKey.Space,
            Key.Escape => TerminalKey.Escape,
            Key.Delete => TerminalKey.Delete,
            Key.Insert => TerminalKey.Insert,
            Key.Home => TerminalKey.Home,
            Key.End => TerminalKey.End,
            Key.PageUp => TerminalKey.PageUp,
            Key.PageDown => TerminalKey.PageDown,
            Key.Left => TerminalKey.ArrowLeft,
            Key.Right => TerminalKey.ArrowRight,
            Key.Up => TerminalKey.ArrowUp,
            Key.Down => TerminalKey.ArrowDown,
            Key.LeftShift => TerminalKey.ShiftLeft,
            Key.RightShift => TerminalKey.ShiftRight,
            Key.LeftCtrl => TerminalKey.ControlLeft,
            Key.RightCtrl => TerminalKey.ControlRight,
            Key.LeftAlt => TerminalKey.AltLeft,
            Key.RightAlt => TerminalKey.AltRight,
            Key.LWin => TerminalKey.MetaLeft,
            Key.RWin => TerminalKey.MetaRight,
            Key.CapsLock => TerminalKey.CapsLock,
            Key.NumLock => TerminalKey.NumLock,
            Key.Scroll => TerminalKey.ScrollLock,
            Key.Pause => TerminalKey.Pause,
            Key.PrintScreen => TerminalKey.PrintScreen,
            Key.Apps => TerminalKey.ContextMenu,
            Key.Multiply => TerminalKey.NumpadMultiply,
            Key.Add => TerminalKey.NumpadAdd,
            Key.Subtract => TerminalKey.NumpadSubtract,
            Key.Divide => TerminalKey.NumpadDivide,
            Key.Decimal => TerminalKey.NumpadDecimal,
            _ => TerminalKey.Unidentified,
        };
        return terminal != TerminalKey.Unidentified;
    }

    /// <summary>修饰键（Meta = macOS 的 Command，Windows 的 Win 键）。</summary>
    public static KeyMods Mods(KeyModifiers modifiers)
    {
        var mods = KeyMods.None;
        if (modifiers.HasFlag(KeyModifiers.Shift)) mods |= KeyMods.Shift;
        if (modifiers.HasFlag(KeyModifiers.Control)) mods |= KeyMods.Ctrl;
        if (modifiers.HasFlag(KeyModifiers.Alt)) mods |= KeyMods.Alt;
        if (modifiers.HasFlag(KeyModifiers.Meta)) mods |= KeyMods.Super;
        return mods;
    }

    /// <summary>
    /// 键是否应当由 TextInput 产生字符：无 Ctrl / Alt / Meta 修饰的可打印键。其余键（功能键、Ctrl 组合、Alt 组合）
    /// 直接经 <see cref="InputEncoder"/> 编码。
    /// </summary>
    public static bool ProducesText(Key key, KeyModifiers modifiers) =>
        !modifiers.HasFlag(KeyModifiers.Control) && !modifiers.HasFlag(KeyModifiers.Alt) && !modifiers.HasFlag(KeyModifiers.Meta) &&
        key is (>= Key.A and <= Key.Z) or (>= Key.D0 and <= Key.D9) or (>= Key.NumPad0 and <= Key.NumPad9) or Key.Space or
            Key.OemTilde or Key.OemMinus or Key.OemPlus or Key.OemOpenBrackets or Key.OemCloseBrackets or Key.OemBackslash or Key.OemSemicolon or
            Key.OemQuotes or Key.OemComma or Key.OemPeriod or Key.OemQuestion or Key.Multiply or Key.Add or Key.Subtract or Key.Divide or Key.Decimal;
}
