using Avalonia.Input;

namespace TKCode.Arc;

/// <summary>Which labels a <see cref="KeycapKeyboard"/> prints on its modifier keys.</summary>
public enum KeycapPlatform
{
    /// <summary>Mac labels on macOS and iOS, Windows labels elsewhere.</summary>
    Auto,

    /// <summary>Ctrl, Win and Alt, in that order from the left.</summary>
    Windows,

    /// <summary>⌃ control, ⌥ option and ⌘ command, in that order from the left.</summary>
    Mac
}

/// <summary>Which real key presses a <see cref="KeycapKeyboard"/> lights up.</summary>
public enum KeycapTracking
{
    /// <summary>None: the keyboard only reacts to the pointer and to <see cref="KeycapKeyboard.Shortcut"/>.</summary>
    None,

    /// <summary>Presses while the keyboard has keyboard focus (default).</summary>
    WhenFocused,

    /// <summary>Every press in the window, wherever the focus is. Presses are watched, never taken.</summary>
    Window
}

/// <summary>One keycap: its physical key, label, place in key units and, for a modifier, what it holds.</summary>
internal sealed record KeycapKeyboardKey(PhysicalKey Physical, string Label, double X, double Y, double Width, double Height,
    KeyModifiers Modifier = KeyModifiers.None, bool IsWide = false)
{
    /// <summary>True for Ctrl, Shift, Alt and the Windows or Command key.</summary>
    public bool IsModifier => Modifier != KeyModifiers.None;
}

/// <summary>
/// The key layouts of a <see cref="KeycapKeyboard"/> (a compact US layout, 15 key units wide, with an optional function
/// row) and the parser for shortcut text such as "Ctrl+Shift+K" or "⌘K". Pure data and functions.
/// </summary>
internal static class KeycapKeyboardLayout
{
    /// <summary>Width of every row in key units.</summary>
    public const double Columns = 15;

    private static readonly Dictionary<(bool Mac, bool Function), KeycapKeyboardKey[]> s_layouts = [];

    /// <summary>The keys of a layout, top row first, left to right.</summary>
    public static KeycapKeyboardKey[] Keys(bool mac, bool functionRow)
    {
        lock (s_layouts)
        {
            if (!s_layouts.TryGetValue((mac, functionRow), out var keys))
            {
                keys = Build(mac, functionRow);
                s_layouts[(mac, functionRow)] = keys;
            }
            return keys;
        }
    }

    /// <summary>The number of rows of a layout, in key units of height.</summary>
    public static double Rows(bool functionRow) => functionRow ? 6 : 5;

    private static KeycapKeyboardKey[] Build(bool mac, bool functionRow)
    {
        var keys = new List<KeycapKeyboardKey>(80);
        var y = 0.0;
        double x;

        void Add(PhysicalKey key, string label, double width, KeyModifiers modifier = KeyModifiers.None)
        {
            keys.Add(new KeycapKeyboardKey(key, label, x, y, width, 1, modifier, width > 1.01));
            x += width;
        }

        if (functionRow)
        {
            x = 0;
            Add(PhysicalKey.Escape, mac ? "esc" : "Esc", 1.5);
            PhysicalKey[] f =
            [
                PhysicalKey.F1, PhysicalKey.F2, PhysicalKey.F3, PhysicalKey.F4, PhysicalKey.F5, PhysicalKey.F6,
                PhysicalKey.F7, PhysicalKey.F8, PhysicalKey.F9, PhysicalKey.F10, PhysicalKey.F11, PhysicalKey.F12
            ];
            for (var i = 0; i < f.Length; i++)
            {
                Add(f[i], "F" + (i + 1), 13.5 / 12);
            }
            y += 1;
        }

        x = 0;
        Add(PhysicalKey.Backquote, "`", 1);
        PhysicalKey[] digits =
        [
            PhysicalKey.Digit1, PhysicalKey.Digit2, PhysicalKey.Digit3, PhysicalKey.Digit4, PhysicalKey.Digit5,
            PhysicalKey.Digit6, PhysicalKey.Digit7, PhysicalKey.Digit8, PhysicalKey.Digit9, PhysicalKey.Digit0
        ];
        for (var i = 0; i < digits.Length; i++)
        {
            Add(digits[i], ((i + 1) % 10).ToString(System.Globalization.CultureInfo.InvariantCulture), 1);
        }
        Add(PhysicalKey.Minus, "-", 1);
        Add(PhysicalKey.Equal, "=", 1);
        Add(PhysicalKey.Backspace, mac ? "delete" : "Backspace", 2);
        y += 1;

        x = 0;
        Add(PhysicalKey.Tab, mac ? "tab" : "Tab", 1.5);
        Letters("QWERTYUIOP");
        Add(PhysicalKey.BracketLeft, "[", 1);
        Add(PhysicalKey.BracketRight, "]", 1);
        Add(PhysicalKey.Backslash, "\\", 1.5);
        y += 1;

        x = 0;
        Add(PhysicalKey.CapsLock, mac ? "caps lock" : "Caps", 1.75);
        Letters("ASDFGHJKL");
        Add(PhysicalKey.Semicolon, ";", 1);
        Add(PhysicalKey.Quote, "'", 1);
        Add(PhysicalKey.Enter, mac ? "return" : "Enter", 2.25);
        y += 1;

        x = 0;
        Add(PhysicalKey.ShiftLeft, mac ? "⇧ shift" : "Shift", 2.25, KeyModifiers.Shift);
        Letters("ZXCVBNM");
        Add(PhysicalKey.Comma, ",", 1);
        Add(PhysicalKey.Period, ".", 1);
        Add(PhysicalKey.Slash, "/", 1);
        Add(PhysicalKey.ShiftRight, mac ? "⇧ shift" : "Shift", 2.75, KeyModifiers.Shift);
        y += 1;

        x = 0;
        if (mac)
        {
            Add(PhysicalKey.ControlLeft, "⌃", 1.25, KeyModifiers.Control);
            Add(PhysicalKey.AltLeft, "⌥", 1.25, KeyModifiers.Alt);
            Add(PhysicalKey.MetaLeft, "⌘", 1.25, KeyModifiers.Meta);
            Add(PhysicalKey.Space, string.Empty, 5.75);
            Add(PhysicalKey.MetaRight, "⌘", 1.25, KeyModifiers.Meta);
            Add(PhysicalKey.AltRight, "⌥", 1.25, KeyModifiers.Alt);
        }
        else
        {
            Add(PhysicalKey.ControlLeft, "Ctrl", 1.25, KeyModifiers.Control);
            Add(PhysicalKey.MetaLeft, "Win", 1.25, KeyModifiers.Meta);
            Add(PhysicalKey.AltLeft, "Alt", 1.25, KeyModifiers.Alt);
            Add(PhysicalKey.Space, string.Empty, 5.75);
            Add(PhysicalKey.AltRight, "Alt", 1.25, KeyModifiers.Alt);
            Add(PhysicalKey.ControlRight, "Ctrl", 1.25, KeyModifiers.Control);
        }
        Add(PhysicalKey.ArrowLeft, "←", 1);
        // Up and down share one key's place, half a row each.
        keys.Add(new KeycapKeyboardKey(PhysicalKey.ArrowUp, "↑", x, y, 1, 0.5));
        keys.Add(new KeycapKeyboardKey(PhysicalKey.ArrowDown, "↓", x, y + 0.5, 1, 0.5));
        x += 1;
        Add(PhysicalKey.ArrowRight, "→", 1);
        return keys.ToArray();

        void Letters(string letters)
        {
            foreach (var letter in letters)
            {
                Add(PhysicalKey.A + (letter - 'A'), letter.ToString(), 1);
            }
        }
    }

    /// <summary>The index of a physical key in a layout, or −1.</summary>
    public static int IndexOf(KeycapKeyboardKey[] keys, PhysicalKey key)
    {
        for (var i = 0; i < keys.Length; i++)
        {
            if (keys[i].Physical == key)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>The left-hand key that holds a modifier.</summary>
    public static PhysicalKey KeyOf(KeyModifiers modifier) => modifier switch
    {
        KeyModifiers.Control => PhysicalKey.ControlLeft,
        KeyModifiers.Shift => PhysicalKey.ShiftLeft,
        KeyModifiers.Alt => PhysicalKey.AltLeft,
        KeyModifiers.Meta => PhysicalKey.MetaLeft,
        _ => PhysicalKey.None
    };

    /// <summary>
    /// Reads shortcut text: modifiers and one key joined by "+" ("Ctrl+Shift+K", "Cmd+,", "Alt+F4"), or Mac symbols run
    /// together ("⇧⌘P"). Modifier names: Ctrl, Control, ⌃, Shift, ⇧, Alt, Option, Opt, ⌥, Cmd, Command, Meta, Win, Super,
    /// ⌘. The key is a letter, digit, symbol, F1 to F12, Esc, Tab, Enter, Space, Backspace, an arrow or a key name.
    /// </summary>
    /// <returns>False when the text names no key (a modifier alone is allowed, with <paramref name="key"/> None).</returns>
    public static bool TryParse(string? text, out KeyModifiers modifiers, out PhysicalKey key)
    {
        modifiers = KeyModifiers.None;
        key = PhysicalKey.None;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        var span = text.Trim();
        // Mac symbols in front of the key, with or without "+".
        var start = 0;
        while (start < span.Length)
        {
            var symbol = span[start] switch
            {
                '⌃' => KeyModifiers.Control,
                '⇧' => KeyModifiers.Shift,
                '⌥' => KeyModifiers.Alt,
                '⌘' => KeyModifiers.Meta,
                _ => KeyModifiers.None
            };
            if (symbol == KeyModifiers.None)
            {
                break;
            }
            modifiers |= symbol;
            start++;
            if (start < span.Length && span[start] == '+')
            {
                start++;
            }
        }
        var rest = span[start..];
        var parts = new List<string>();
        var i = 0;
        while (i < rest.Length)
        {
            var plus = rest.IndexOf('+', i);
            if (plus < 0)
            {
                parts.Add(rest[i..]);
                break;
            }
            if (plus == i)
            {
                // "Ctrl++": the key itself is a plus.
                parts.Add("+");
                i = plus + 1;
                if (i < rest.Length && rest[i] == '+')
                {
                    i++;
                }
                continue;
            }
            parts.Add(rest[i..plus]);
            i = plus + 1;
        }
        foreach (var raw in parts)
        {
            var part = raw.Trim();
            if (part.Length == 0)
            {
                continue;
            }
            var modifier = ModifierOf(part);
            if (modifier != KeyModifiers.None)
            {
                modifiers |= modifier;
                continue;
            }
            var found = KeyOf(part);
            if (found == PhysicalKey.None)
            {
                return false;
            }
            key = found;
        }
        return key != PhysicalKey.None || modifiers != KeyModifiers.None;
    }

    private static KeyModifiers ModifierOf(string part) => part.ToLowerInvariant() switch
    {
        "ctrl" or "control" or "⌃" => KeyModifiers.Control,
        "shift" or "⇧" => KeyModifiers.Shift,
        "alt" or "option" or "opt" or "⌥" => KeyModifiers.Alt,
        "cmd" or "command" or "meta" or "win" or "super" or "⌘" => KeyModifiers.Meta,
        _ => KeyModifiers.None
    };

    private static PhysicalKey KeyOf(string part)
    {
        if (part.Length == 1)
        {
            var c = char.ToUpperInvariant(part[0]);
            if (c is >= 'A' and <= 'Z')
            {
                return PhysicalKey.A + (c - 'A');
            }
            if (c is >= '1' and <= '9')
            {
                return PhysicalKey.Digit1 + (c - '1');
            }
            return c switch
            {
                '0' => PhysicalKey.Digit0,
                '`' or '~' => PhysicalKey.Backquote,
                '-' or '_' => PhysicalKey.Minus,
                '=' or '+' => PhysicalKey.Equal,
                '[' or '{' => PhysicalKey.BracketLeft,
                ']' or '}' => PhysicalKey.BracketRight,
                '\\' or '|' => PhysicalKey.Backslash,
                ';' or ':' => PhysicalKey.Semicolon,
                '\'' or '"' => PhysicalKey.Quote,
                ',' or '<' => PhysicalKey.Comma,
                '.' or '>' => PhysicalKey.Period,
                '/' or '?' => PhysicalKey.Slash,
                '←' => PhysicalKey.ArrowLeft,
                '→' => PhysicalKey.ArrowRight,
                '↑' => PhysicalKey.ArrowUp,
                '↓' => PhysicalKey.ArrowDown,
                ' ' => PhysicalKey.Space,
                _ => PhysicalKey.None
            };
        }
        var lower = part.ToLowerInvariant();
        if (lower.Length is 2 or 3 && lower[0] == 'f' && int.TryParse(lower.AsSpan(1), out var n) && n is >= 1 and <= 12)
        {
            return PhysicalKey.F1 + (n - 1);
        }
        return lower switch
        {
            "esc" or "escape" => PhysicalKey.Escape,
            "tab" => PhysicalKey.Tab,
            "enter" or "return" => PhysicalKey.Enter,
            "space" or "spacebar" => PhysicalKey.Space,
            "backspace" or "delete" or "del" => PhysicalKey.Backspace,
            "caps" or "capslock" => PhysicalKey.CapsLock,
            "left" => PhysicalKey.ArrowLeft,
            "right" => PhysicalKey.ArrowRight,
            "up" => PhysicalKey.ArrowUp,
            "down" => PhysicalKey.ArrowDown,
            "plus" => PhysicalKey.Equal,
            "minus" => PhysicalKey.Minus,
            "comma" => PhysicalKey.Comma,
            "period" => PhysicalKey.Period,
            "slash" => PhysicalKey.Slash,
            _ => Enum.TryParse<PhysicalKey>(part, true, out var named) ? named : PhysicalKey.None
        };
    }

    /// <summary>A modifier set written as text in the platform's way: "Ctrl+Shift" or "⌃⇧".</summary>
    public static string Describe(KeyModifiers modifiers, PhysicalKey key, bool mac, KeycapKeyboardKey[] keys)
    {
        var parts = new List<string>(5);
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add(mac ? "Control" : "Ctrl");
        }
        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add(mac ? "Option" : "Alt");
        }
        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Shift");
        }
        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            parts.Add(mac ? "Command" : "Win");
        }
        if (key != PhysicalKey.None)
        {
            var index = IndexOf(keys, key);
            parts.Add(index >= 0 ? NameOf(keys[index]) : key.ToString());
        }
        return string.Join("+", parts);
    }

    /// <summary>The name of a key for assistive technology: its label, or a word for a key whose label is blank or a symbol.</summary>
    public static string NameOf(KeycapKeyboardKey key) => key.Physical switch
    {
        PhysicalKey.Space => "Space",
        PhysicalKey.ArrowLeft => "Left",
        PhysicalKey.ArrowRight => "Right",
        PhysicalKey.ArrowUp => "Up",
        PhysicalKey.ArrowDown => "Down",
        PhysicalKey.ControlLeft or PhysicalKey.ControlRight => "Control",
        PhysicalKey.AltLeft or PhysicalKey.AltRight => key.Label == "⌥" ? "Option" : "Alt",
        PhysicalKey.MetaLeft or PhysicalKey.MetaRight => key.Label == "⌘" ? "Command" : "Windows",
        PhysicalKey.ShiftLeft or PhysicalKey.ShiftRight => "Shift",
        _ => key.Label
    };
}
