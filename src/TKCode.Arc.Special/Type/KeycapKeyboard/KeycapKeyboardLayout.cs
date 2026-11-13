using Avalonia.Input;

namespace TKCode.Arc;

/// <summary>Which labels a <see cref="KeycapKeyboard"/> prints on its modifier keys.</summary>
public enum KeycapPlatform
{
    /// <summary>Mac labels on macOS and iOS, Windows labels elsewhere.</summary>
    Auto,

    /// <summary>ctrl, win and alt, in that order from the left.</summary>
    Windows,

    /// <summary>ctrl, opt and cmd, in that order from the left.</summary>
    Mac
}

/// <summary>Which real key presses a <see cref="KeycapKeyboard"/> hears.</summary>
public enum KeycapTracking
{
    /// <summary>None: the keyboard only reacts to the pointer, to Tab and Enter on a key and to <see cref="KeycapKeyboard.Shortcut"/>.</summary>
    None,

    /// <summary>Presses while the keyboard or one of its keys has keyboard focus (default).</summary>
    WhenFocused,

    /// <summary>
    /// Every press in the window, wherever the focus is. Presses are watched, never taken: a press meant for a text field
    /// still lights its key but types nothing, and shortcuts with Ctrl, Alt or the Command key pass through untouched.
    /// </summary>
    Window
}

/// <summary>The built-in key layouts of a <see cref="KeycapKeyboard"/>.</summary>
public enum KeycapLayout
{
    /// <summary>A 60% board: numbers, letters, punctuation, Delete, Tab, Caps, Return, both Shifts and the modifiers (default).</summary>
    Compact,

    /// <summary>Three rows of letters with Shift and Delete, and a space bar.</summary>
    Letters
}

/// <summary>What a keycap does when pressed, and how its legend is printed.</summary>
internal enum KeycapKind
{
    /// <summary>Types its character; legend centered.</summary>
    Character,

    /// <summary>Types a space; no legend.</summary>
    Space,

    /// <summary>Deletes the last character; icon in the corner.</summary>
    Backspace,

    /// <summary>Starts a new line; icon in the corner.</summary>
    Enter,

    /// <summary>Shift: a click latches it for the next character; icon in the corner.</summary>
    Shift,

    /// <summary>Tab: lights only; word in the corner.</summary>
    Tab,

    /// <summary>Caps lock: lights only; word in the corner.</summary>
    Caps,

    /// <summary>Control, Option/Alt or Command/Windows: lights only; word in the corner.</summary>
    Modifier,

    /// <summary>Esc and F1 to F12 of the function row: lights only; word in the corner.</summary>
    Function
}

/// <summary>The glyph a keycap prints instead of a word.</summary>
internal enum KeycapIcon
{
    None,
    Backspace,
    Enter,
    Shift
}

/// <summary>
/// One keycap: its physical key, legend, place (row and left edge in key units), width in key units, what it types and
/// how it looks.
/// </summary>
internal sealed record KeycapKeyboardKey(
    PhysicalKey Physical,
    string Label,
    int Row,
    double X,
    double Width,
    KeycapKind Kind,
    KeyModifiers Modifier = KeyModifiers.None,
    string? Text = null,
    string? ShiftText = null,
    string? Name = null)
{
    /// <summary>True for Ctrl, Shift, Alt and the Windows or Command key.</summary>
    public bool IsModifier => Modifier != KeyModifiers.None;

    /// <summary>Whether the legend sits small in the bottom-left corner of the face (special keys) rather than centered.</summary>
    public bool IsCorner => Kind is not (KeycapKind.Character or KeycapKind.Space);

    /// <summary>The glyph printed instead of a word, if any.</summary>
    public KeycapIcon Icon => Kind switch
    {
        KeycapKind.Backspace => KeycapIcon.Backspace,
        KeycapKind.Enter => KeycapIcon.Enter,
        KeycapKind.Shift => KeycapIcon.Shift,
        _ => KeycapIcon.None
    };

    /// <summary>The key's name for assistive technology.</summary>
    public string AccessibleName => Name ?? Label;
}

/// <summary>A built board: its keys, row count, widest row in key units and the index of the accent key.</summary>
internal sealed record KeycapBoard(KeycapKeyboardKey[] Keys, int Rows, double Units, int AccentIndex)
{
    public static readonly KeycapBoard Empty = new([], 0, 0, -1);
}

/// <summary>
/// The key layouts of a <see cref="KeycapKeyboard"/> (a 60% US board, a letters-only board and custom rows) and the parser
/// for shortcut text such as "Ctrl+Shift+K" or "⌘K". Pure data and functions.
/// </summary>
internal static class KeycapKeyboardLayout
{
    /// <summary>Width of every row of the compact board in key units.</summary>
    public const double Columns = 15;

    private static readonly Dictionary<(KeycapLayout Layout, bool Mac, bool Function), KeycapBoard> s_boards = [];

    private const string DigitRow = "1234567890";

    /// <summary>A built-in board; cached, the arrays are shared and must not be changed.</summary>
    public static KeycapBoard Board(KeycapLayout layout, bool mac, bool functionRow)
    {
        if (layout == KeycapLayout.Letters)
        {
            mac = true;
            functionRow = false;
        }
        lock (s_boards)
        {
            if (!s_boards.TryGetValue((layout, mac, functionRow), out var board))
            {
                board = layout == KeycapLayout.Letters ? BuildLetters() : BuildCompact(mac, functionRow);
                s_boards[(layout, mac, functionRow)] = board;
            }
            return board;
        }
    }

    private sealed class Builder
    {
        private readonly List<KeycapKeyboardKey> _keys = new(72);
        private int _row = -1;
        private double _x;
        private double _widest;

        public void NextRow()
        {
            _row++;
            _x = 0;
        }

        public void Add(PhysicalKey physical, string label, double width, KeycapKind kind, KeyModifiers modifier = KeyModifiers.None,
                        string? text = null, string? shiftText = null, string? name = null)
        {
            _keys.Add(new KeycapKeyboardKey(physical, label, _row, _x, width, kind, modifier, text, shiftText, name));
            _x += width;
            _widest = Math.Max(_widest, _x);
        }

        public void Character(char c, double width = 1)
        {
            var (physical, shifted) = CharacterKey(c);
            var text = c.ToString();
            Add(physical, text, width, KeycapKind.Character, text: text, shiftText: shifted);
        }

        public KeycapBoard Build(int accentIndex) => new(_keys.ToArray(), _row + 1, _widest, accentIndex);

        public int IndexOf(KeycapKind kind) => _keys.FindIndex(k => k.Kind == kind);
    }

    private static KeycapBoard BuildCompact(bool mac, bool functionRow)
    {
        var b = new Builder();
        if (functionRow)
        {
            b.NextRow();
            b.Add(PhysicalKey.Escape, "esc", 1.5, KeycapKind.Function, name: "Escape");
            for (var i = 0; i < 12; i++)
            {
                var n = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                b.Add(PhysicalKey.F1 + i, "f" + n, 13.5 / 12, KeycapKind.Function, name: "F" + n);
            }
        }

        b.NextRow();
        b.Character('`');
        foreach (var c in DigitRow)
        {
            b.Character(c);
        }
        b.Character('-');
        b.Character('=');
        b.Add(PhysicalKey.Backspace, string.Empty, 2, KeycapKind.Backspace, name: "Delete");

        b.NextRow();
        b.Add(PhysicalKey.Tab, "tab", 1.5, KeycapKind.Tab, name: "Tab");
        foreach (var c in "qwertyuiop[]")
        {
            b.Character(c);
        }
        b.Character('\\', 1.5);

        b.NextRow();
        b.Add(PhysicalKey.CapsLock, "caps", 1.75, KeycapKind.Caps, name: "Caps lock");
        foreach (var c in "asdfghjkl;'")
        {
            b.Character(c);
        }
        b.Add(PhysicalKey.Enter, string.Empty, 2.25, KeycapKind.Enter, name: "Return");

        b.NextRow();
        b.Add(PhysicalKey.ShiftLeft, string.Empty, 2.25, KeycapKind.Shift, KeyModifiers.Shift, name: "Shift");
        foreach (var c in "zxcvbnm,./")
        {
            b.Character(c);
        }
        b.Add(PhysicalKey.ShiftRight, string.Empty, 2.75, KeycapKind.Shift, KeyModifiers.Shift, name: "Shift");

        b.NextRow();
        if (mac)
        {
            b.Add(PhysicalKey.ControlLeft, "ctrl", 1.25, KeycapKind.Modifier, KeyModifiers.Control, name: "Control");
            b.Add(PhysicalKey.AltLeft, "opt", 1.25, KeycapKind.Modifier, KeyModifiers.Alt, name: "Option");
            b.Add(PhysicalKey.MetaLeft, "cmd", 1.5, KeycapKind.Modifier, KeyModifiers.Meta, name: "Command");
            b.Add(PhysicalKey.Space, string.Empty, 7, KeycapKind.Space, text: " ", shiftText: " ", name: "Space");
            b.Add(PhysicalKey.MetaRight, "cmd", 1.5, KeycapKind.Modifier, KeyModifiers.Meta, name: "Command");
            b.Add(PhysicalKey.AltRight, "opt", 1.25, KeycapKind.Modifier, KeyModifiers.Alt, name: "Option");
            b.Add(PhysicalKey.ControlRight, "ctrl", 1.25, KeycapKind.Modifier, KeyModifiers.Control, name: "Control");
        }
        else
        {
            b.Add(PhysicalKey.ControlLeft, "ctrl", 1.25, KeycapKind.Modifier, KeyModifiers.Control, name: "Control");
            b.Add(PhysicalKey.MetaLeft, "win", 1.25, KeycapKind.Modifier, KeyModifiers.Meta, name: "Windows");
            b.Add(PhysicalKey.AltLeft, "alt", 1.5, KeycapKind.Modifier, KeyModifiers.Alt, name: "Alt");
            b.Add(PhysicalKey.Space, string.Empty, 7, KeycapKind.Space, text: " ", shiftText: " ", name: "Space");
            b.Add(PhysicalKey.AltRight, "alt", 1.5, KeycapKind.Modifier, KeyModifiers.Alt, name: "Alt");
            b.Add(PhysicalKey.MetaRight, "win", 1.25, KeycapKind.Modifier, KeyModifiers.Meta, name: "Windows");
            b.Add(PhysicalKey.ControlRight, "ctrl", 1.25, KeycapKind.Modifier, KeyModifiers.Control, name: "Control");
        }
        return b.Build(b.IndexOf(KeycapKind.Enter));
    }

    private static KeycapBoard BuildLetters()
    {
        var b = new Builder();
        b.NextRow();
        foreach (var c in "qwertyuiop")
        {
            b.Character(c);
        }
        b.NextRow();
        foreach (var c in "asdfghjkl")
        {
            b.Character(c);
        }
        b.NextRow();
        b.Add(PhysicalKey.ShiftLeft, string.Empty, 1.5, KeycapKind.Shift, KeyModifiers.Shift, name: "Shift");
        foreach (var c in "zxcvbnm")
        {
            b.Character(c);
        }
        b.Add(PhysicalKey.Backspace, string.Empty, 1.5, KeycapKind.Backspace, name: "Delete");
        b.NextRow();
        b.Add(PhysicalKey.Space, string.Empty, 6, KeycapKind.Space, text: " ", shiftText: " ", name: "Space");
        return b.Build(b.IndexOf(KeycapKind.Space));
    }

    /// <summary>
    /// A board from custom rows of key tokens: single characters, or "space", "backspace", "enter", "shift", "tab" and
    /// "caps" (names are case-insensitive; "delete" and "return" are read as backspace and enter). Unknown words are skipped.
    /// The accent key is Return if there is one, else the space bar.
    /// </summary>
    public static KeycapBoard Custom(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var b = new Builder();
        foreach (var row in rows)
        {
            b.NextRow();
            foreach (var token in row)
            {
                AddToken(b, token);
            }
        }
        var accent = b.IndexOf(KeycapKind.Enter);
        if (accent < 0)
        {
            accent = b.IndexOf(KeycapKind.Space);
        }
        return b.Build(accent);
    }

    private static void AddToken(Builder b, string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return;
        }
        if (token.Length == 1 || (token.Length == 2 && char.IsSurrogatePair(token[0], token[1])))
        {
            if (token.Length == 1)
            {
                b.Character(token[0]);
            }
            else
            {
                b.Add(PhysicalKey.None, token, 1, KeycapKind.Character, text: token, shiftText: token);
            }
            return;
        }
        switch (token.ToLowerInvariant())
        {
            case "space":
                b.Add(PhysicalKey.Space, string.Empty, 5, KeycapKind.Space, text: " ", shiftText: " ", name: "Space");
                break;
            case "backspace" or "delete":
                b.Add(PhysicalKey.Backspace, string.Empty, 1.5, KeycapKind.Backspace, name: "Delete");
                break;
            case "enter" or "return":
                b.Add(PhysicalKey.Enter, string.Empty, 1.75, KeycapKind.Enter, name: "Return");
                break;
            case "shift":
                b.Add(PhysicalKey.ShiftLeft, string.Empty, 1.5, KeycapKind.Shift, KeyModifiers.Shift, name: "Shift");
                break;
            case "tab":
                b.Add(PhysicalKey.Tab, "tab", 1.5, KeycapKind.Tab, name: "Tab");
                break;
            case "caps" or "capslock":
                b.Add(PhysicalKey.CapsLock, "caps", 1.75, KeycapKind.Caps, name: "Caps lock");
                break;
        }
    }

    /// <summary>Splits a row written as tokens separated by white space ("k e y backspace").</summary>
    public static string[] Tokens(string? row)
        => string.IsNullOrWhiteSpace(row)
            ? []
            : row.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>The physical key of a character on a US layout, and the character typed with Shift.</summary>
    public static (PhysicalKey Key, string Shifted) CharacterKey(char c)
    {
        if (c is >= 'a' and <= 'z')
        {
            return (PhysicalKey.A + (c - 'a'), char.ToUpperInvariant(c).ToString());
        }
        if (c is >= 'A' and <= 'Z')
        {
            return (PhysicalKey.A + (c - 'A'), c.ToString());
        }
        return c switch
        {
            '1' => (PhysicalKey.Digit1, "!"),
            '2' => (PhysicalKey.Digit2, "@"),
            '3' => (PhysicalKey.Digit3, "#"),
            '4' => (PhysicalKey.Digit4, "$"),
            '5' => (PhysicalKey.Digit5, "%"),
            '6' => (PhysicalKey.Digit6, "^"),
            '7' => (PhysicalKey.Digit7, "&"),
            '8' => (PhysicalKey.Digit8, "*"),
            '9' => (PhysicalKey.Digit9, "("),
            '0' => (PhysicalKey.Digit0, ")"),
            '`' => (PhysicalKey.Backquote, "~"),
            '-' => (PhysicalKey.Minus, "_"),
            '=' => (PhysicalKey.Equal, "+"),
            '[' => (PhysicalKey.BracketLeft, "{"),
            ']' => (PhysicalKey.BracketRight, "}"),
            '\\' => (PhysicalKey.Backslash, "|"),
            ';' => (PhysicalKey.Semicolon, ":"),
            '\'' => (PhysicalKey.Quote, "\""),
            ',' => (PhysicalKey.Comma, "<"),
            '.' => (PhysicalKey.Period, ">"),
            '/' => (PhysicalKey.Slash, "?"),
            _ => (PhysicalKey.None, c.ToString())
        };
    }

    /// <summary>The index of a physical key on a board, or −1.</summary>
    public static int IndexOf(KeycapKeyboardKey[] keys, PhysicalKey key)
    {
        if (key == PhysicalKey.None)
        {
            return -1;
        }
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

    /// <summary>A shortcut written as text in the platform's way: "Ctrl+Shift+K" or "Control+Command+K".</summary>
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

    /// <summary>The name of a key in a shortcut description: upper-case letters, otherwise its accessible name.</summary>
    public static string NameOf(KeycapKeyboardKey key)
        => key.Kind == KeycapKind.Character && key.Label.Length == 1 && char.IsLetter(key.Label[0])
            ? key.Label.ToUpperInvariant()
            : key.AccessibleName;
}
