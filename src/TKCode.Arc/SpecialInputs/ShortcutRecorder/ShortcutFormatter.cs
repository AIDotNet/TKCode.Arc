using System.Diagnostics.CodeAnalysis;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The keyboard conventions a <see cref="ShortcutRecorder"/> and <see cref="ShortcutFormatter"/> follow.</summary>
public enum ShortcutPlatform
{
    /// <summary>Apple keyboards: ⌃ ⌥ ⇧ ⌘ glyphs, and "mod" (the command modifier) is Command (<see cref="KeyModifiers.Meta"/>).</summary>
    Mac,

    /// <summary>Windows and Linux keyboards: Ctrl Alt Shift Win names, and "mod" is Ctrl (<see cref="KeyModifiers.Control"/>).</summary>
    Other
}

/// <summary>One key cap of a shortcut, in display order.</summary>
/// <param name="Label">What the cap shows ("⌘", "Ctrl", "K", "↩").</param>
/// <param name="Id">The physical key id ("meta", "ctrl", "alt", "shift", "k", "enter", ...).</param>
/// <param name="Spoken">The name assistive technology reads ("Command", "Control", "K", "Return").</param>
public readonly record struct ShortcutKeyCap(string Label, string Id, string Spoken);

/// <summary>
/// A shortcut that is already in use, such as "Save" on ⌘S. A <see cref="ShortcutRecorder"/> asks before it takes one of
/// its <see cref="ShortcutRecorder.Bindings"/>.
/// </summary>
public sealed class ShortcutBinding
{
    /// <summary>Creates an empty binding (for XAML).</summary>
    public ShortcutBinding()
    {
    }

    /// <summary>Creates a binding of <paramref name="gesture"/> to the action named <paramref name="label"/>.</summary>
    public ShortcutBinding(KeyGesture gesture, string label)
    {
        Gesture = gesture;
        Label = label;
    }

    /// <summary>The key combination.</summary>
    public KeyGesture? Gesture { get; set; }

    /// <summary>The action it runs, shown in the conflict message ("Already used by <b>Save</b>").</summary>
    public string Label { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string ToString() => $"{Label}: {Gesture}";
}

/// <summary>Arguments of <see cref="ShortcutRecorder.ValueChanged"/>.</summary>
public sealed class ShortcutRecorderValueChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ShortcutRecorderValueChangedEventArgs(RoutedEvent routedEvent, KeyGesture? oldValue, KeyGesture? newValue, ShortcutBinding? replaced)
        : base(routedEvent)
    {
        OldValue = oldValue;
        NewValue = newValue;
        Replaced = replaced;
    }

    /// <summary>The previous shortcut (null: none).</summary>
    public KeyGesture? OldValue { get; }

    /// <summary>The new shortcut (null: cleared).</summary>
    public KeyGesture? NewValue { get; }

    /// <summary>
    /// The binding the new shortcut was taken from after "Use anyway", so the caller can unbind it (Arc's
    /// <c>details.replaced</c>). Null for every other change, including taking a reserved shortcut.
    /// </summary>
    public ShortcutBinding? Replaced { get; }
}

/// <summary>
/// Shortcut helpers of the shortcut recorder (Arc's <c>formatShortcut</c>, <c>shortcutTokens</c> and
/// <c>matchesShortcut</c>). Shortcuts are <see cref="KeyGesture"/>s with real modifiers: Arc's "mod" (⌘ on a Mac, Ctrl
/// elsewhere) is resolved when a shortcut is parsed (<see cref="Parse"/>) or recorded. Keys are read from the physical
/// key (<see cref="KeyEventArgs.PhysicalKey"/>), so ⌥K is "Alt+K" rather than "˚", and numpad digits equal top-row digits.
/// </summary>
public static class ShortcutFormatter
{
    /// <summary>The platform of this process: <see cref="ShortcutPlatform.Mac"/> on macOS and iOS.</summary>
    public static ShortcutPlatform CurrentPlatform =>
        OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() ? ShortcutPlatform.Mac : ShortcutPlatform.Other;

    /// <summary>Arc's "mod": Command (<see cref="KeyModifiers.Meta"/>) on a Mac, Ctrl elsewhere.</summary>
    public static KeyModifiers CommandModifier(ShortcutPlatform platform) =>
        platform == ShortcutPlatform.Mac ? KeyModifiers.Meta : KeyModifiers.Control;

    /// <summary>"⌘⇧K" on a Mac, "Ctrl+Shift+K" elsewhere, plus the spoken form ("Command Shift K").</summary>
    public static (string Text, string Spoken) Format(KeyGesture gesture, ShortcutPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        var caps = Tokens(gesture, platform);
        return (string.Join(platform == ShortcutPlatform.Mac ? string.Empty : "+", caps.Select(c => c.Label)),
                string.Join(" ", caps.Select(c => c.Spoken)));
    }

    /// <summary>The key caps of <paramref name="gesture"/> in platform order: ⌃ ⌥ ⇧ ⌘ on a Mac, Ctrl Alt Shift Win elsewhere, then the key.</summary>
    public static IReadOnlyList<ShortcutKeyCap> Tokens(KeyGesture gesture, ShortcutPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        return Tokens(gesture.KeyModifiers, gesture.Key, platform);
    }

    /// <summary>
    /// True when the key event is <paramref name="gesture"/>: the physical key and the exact modifiers. Use it to run
    /// the action a recorded shortcut is bound to. <paramref name="platform"/> is accepted for parity with Arc's
    /// <c>matchesShortcut</c>; recorded gestures already carry real modifiers.
    /// </summary>
    public static bool Matches(KeyEventArgs e, KeyGesture gesture, ShortcutPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(gesture);
        return FromKeyEvent(e) is { } pressed && AreEquivalent(pressed, gesture);
    }

    /// <summary>
    /// Reads a key-down as a shortcut, the way the recorder does: the physical key with the modifiers held. Null while
    /// only modifiers are down and for keys that never make a shortcut (Caps Lock, media keys, ...).
    /// </summary>
    public static KeyGesture? FromKeyEvent(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var key = KeyOf(e);
        return key is { } k ? new KeyGesture(k, e.KeyModifiers & AllModifiers) : null;
    }

    /// <summary>
    /// Parses Arc's shortcut syntax ("mod+shift+k", "alt+f4", "ctrl+/"): "mod" is <see cref="CommandModifier"/> of
    /// <paramref name="platform"/>; also accepts ctrl / control, alt / option, shift, meta / cmd / command / win, and the
    /// key names space, enter / return, escape / esc, backspace, delete / del, tab, up, down, left, right, home, end,
    /// pageup, pagedown, f1–f24, letters, digits and , . / ; ' [ ] \ - = `.
    /// </summary>
    /// <exception cref="FormatException">The text is not a shortcut with exactly one key.</exception>
    public static KeyGesture Parse(string shortcut, ShortcutPlatform platform)
    {
        if (TryParse(shortcut, platform, out var gesture))
        {
            return gesture;
        }
        throw new FormatException($"'{shortcut}' is not a shortcut such as \"mod+shift+k\".");
    }

    /// <summary>Like <see cref="Parse"/>, without throwing.</summary>
    public static bool TryParse(string? shortcut, ShortcutPlatform platform, [NotNullWhen(true)] out KeyGesture? gesture)
    {
        gesture = null;
        if (string.IsNullOrWhiteSpace(shortcut))
        {
            return false;
        }
        var modifiers = KeyModifiers.None;
        Key? key = null;
        foreach (var raw in shortcut.Split('+'))
        {
            var part = raw.Trim().ToLowerInvariant();
            if (part.Length == 0)
            {
                continue;
            }
            switch (part)
            {
                case "mod":
                    modifiers |= CommandModifier(platform);
                    continue;
                case "ctrl" or "control":
                    modifiers |= KeyModifiers.Control;
                    continue;
                case "alt" or "option" or "opt":
                    modifiers |= KeyModifiers.Alt;
                    continue;
                case "shift":
                    modifiers |= KeyModifiers.Shift;
                    continue;
                case "meta" or "cmd" or "command" or "win" or "super":
                    modifiers |= KeyModifiers.Meta;
                    continue;
            }
            var parsed = KeyFromId(part);
            if (parsed is null)
            {
                return false;
            }
            key = parsed;
        }
        if (key is not { } k)
        {
            return false;
        }
        gesture = new KeyGesture(k, modifiers);
        return true;
    }

    // ---- internals shared with the recorder ------------------------------------------------------------------------

    internal const KeyModifiers AllModifiers = KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta;

    /// <summary>One spelling per combination: numpad digits read as top-row digits.</summary>
    internal static bool AreEquivalent(KeyGesture? a, KeyGesture? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }
        return Canonical(a.Key) == Canonical(b.Key) && (a.KeyModifiers & AllModifiers) == (b.KeyModifiers & AllModifiers);
    }

    internal static Key Canonical(Key key) => key is >= Key.NumPad0 and <= Key.NumPad9 ? Key.D0 + (key - Key.NumPad0) : key;

    /// <summary>The modifier a modifier key holds, or None for other keys.</summary>
    internal static KeyModifiers ModifierOf(KeyEventArgs e)
    {
        switch (e.PhysicalKey)
        {
            case PhysicalKey.ShiftLeft or PhysicalKey.ShiftRight:
                return KeyModifiers.Shift;
            case PhysicalKey.ControlLeft or PhysicalKey.ControlRight:
                return KeyModifiers.Control;
            case PhysicalKey.AltLeft or PhysicalKey.AltRight:
                return KeyModifiers.Alt;
            case PhysicalKey.MetaLeft or PhysicalKey.MetaRight:
                return KeyModifiers.Meta;
        }
        return e.Key switch
        {
            Key.LeftShift or Key.RightShift => KeyModifiers.Shift,
            Key.LeftCtrl or Key.RightCtrl => KeyModifiers.Control,
            Key.LeftAlt or Key.RightAlt => KeyModifiers.Alt,
            Key.LWin or Key.RWin => KeyModifiers.Meta,
            _ => KeyModifiers.None
        };
    }

    /// <summary>The shortcut key of an event (Arc's <c>keyId</c>): physical first, then the logical key; null for modifiers and keys that never make a shortcut.</summary>
    internal static Key? KeyOf(KeyEventArgs e)
    {
        if (ModifierOf(e) != KeyModifiers.None)
        {
            return null;
        }
        if (KeyFromPhysical(e.PhysicalKey) is { } physical)
        {
            return physical;
        }
        // Keys without a physical code (or with one Arc has no name for) fall back to the logical key when it is one
        // of the supported keys; Caps Lock, dead keys, IME and media keys never make a shortcut.
        var logical = Canonical(e.Key);
        return IdOf(logical) is not null ? logical : null;
    }

    internal static Key? KeyFromPhysical(PhysicalKey key)
    {
        if (key is >= PhysicalKey.A and <= PhysicalKey.Z)
        {
            return Key.A + (key - PhysicalKey.A);
        }
        if (key is >= PhysicalKey.Digit0 and <= PhysicalKey.Digit9)
        {
            return Key.D0 + (key - PhysicalKey.Digit0);
        }
        if (key is >= PhysicalKey.NumPad0 and <= PhysicalKey.NumPad9)
        {
            return Key.D0 + (key - PhysicalKey.NumPad0);
        }
        if (key is >= PhysicalKey.F1 and <= PhysicalKey.F24)
        {
            return Key.F1 + (key - PhysicalKey.F1);
        }
        return key switch
        {
            PhysicalKey.Comma => Key.OemComma,
            PhysicalKey.Period => Key.OemPeriod,
            PhysicalKey.Slash => Key.OemQuestion,
            PhysicalKey.Semicolon => Key.OemSemicolon,
            PhysicalKey.Quote => Key.OemQuotes,
            PhysicalKey.BracketLeft => Key.OemOpenBrackets,
            PhysicalKey.BracketRight => Key.OemCloseBrackets,
            PhysicalKey.Backslash => Key.OemPipe,
            PhysicalKey.Minus => Key.OemMinus,
            PhysicalKey.Equal => Key.OemPlus,
            PhysicalKey.Backquote => Key.OemTilde,
            PhysicalKey.Space => Key.Space,
            PhysicalKey.Enter or PhysicalKey.NumPadEnter => Key.Enter,
            PhysicalKey.Escape => Key.Escape,
            PhysicalKey.Backspace => Key.Back,
            PhysicalKey.Delete => Key.Delete,
            PhysicalKey.Tab => Key.Tab,
            PhysicalKey.ArrowUp => Key.Up,
            PhysicalKey.ArrowDown => Key.Down,
            PhysicalKey.ArrowLeft => Key.Left,
            PhysicalKey.ArrowRight => Key.Right,
            PhysicalKey.Home => Key.Home,
            PhysicalKey.End => Key.End,
            PhysicalKey.PageUp => Key.PageUp,
            PhysicalKey.PageDown => Key.PageDown,
            _ => null
        };
    }

    /// <summary>Arc's key id for a key ("k", "1", "f5", "enter", ","), or null for keys Arc never records.</summary>
    internal static string? IdOf(Key key)
    {
        key = Canonical(key);
        if (key is >= Key.A and <= Key.Z)
        {
            return ((char)('a' + (key - Key.A))).ToString();
        }
        if (key is >= Key.D0 and <= Key.D9)
        {
            return ((char)('0' + (key - Key.D0))).ToString();
        }
        if (key is >= Key.F1 and <= Key.F24)
        {
            return "f" + (key - Key.F1 + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        return key switch
        {
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemQuestion => "/",
            Key.OemSemicolon => ";",
            Key.OemQuotes => "'",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",
            Key.OemPipe => "\\",
            Key.OemMinus => "-",
            Key.OemPlus => "=",
            Key.OemTilde => "`",
            Key.Space => "space",
            Key.Enter => "enter",
            Key.Escape => "escape",
            Key.Back => "backspace",
            Key.Delete => "delete",
            Key.Tab => "tab",
            Key.Up => "up",
            Key.Down => "down",
            Key.Left => "left",
            Key.Right => "right",
            Key.Home => "home",
            Key.End => "end",
            Key.PageUp => "pageup",
            Key.PageDown => "pagedown",
            _ => null
        };
    }

    private static Key? KeyFromId(string id)
    {
        if (id.Length == 1)
        {
            var c = id[0];
            if (c is >= 'a' and <= 'z')
            {
                return Key.A + (c - 'a');
            }
            if (c is >= '0' and <= '9')
            {
                return Key.D0 + (c - '0');
            }
        }
        if (id.Length is 2 or 3 && id[0] == 'f'
            && int.TryParse(id.AsSpan(1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var f)
            && f is >= 1 and <= 24)
        {
            return Key.F1 + (f - 1);
        }
        return id switch
        {
            "," => Key.OemComma,
            "." => Key.OemPeriod,
            "/" => Key.OemQuestion,
            ";" => Key.OemSemicolon,
            "'" => Key.OemQuotes,
            "[" => Key.OemOpenBrackets,
            "]" => Key.OemCloseBrackets,
            "\\" => Key.OemPipe,
            "-" => Key.OemMinus,
            "=" => Key.OemPlus,
            "`" => Key.OemTilde,
            "space" => Key.Space,
            "enter" or "return" => Key.Enter,
            "escape" or "esc" => Key.Escape,
            "backspace" => Key.Back,
            "delete" or "del" => Key.Delete,
            "tab" => Key.Tab,
            "up" or "arrowup" => Key.Up,
            "down" or "arrowdown" => Key.Down,
            "left" or "arrowleft" => Key.Left,
            "right" or "arrowright" => Key.Right,
            "home" => Key.Home,
            "end" => Key.End,
            "pageup" or "pgup" => Key.PageUp,
            "pagedown" or "pgdn" => Key.PageDown,
            _ => null
        };
    }

    /// <summary>Caps for modifiers plus an optional key (<see cref="Key.None"/>: modifiers only, the live caps while recording).</summary>
    internal static IReadOnlyList<ShortcutKeyCap> Tokens(KeyModifiers modifiers, Key key, ShortcutPlatform platform)
    {
        var caps = new List<ShortcutKeyCap>(5);
        if (platform == ShortcutPlatform.Mac)
        {
            if (modifiers.HasFlag(KeyModifiers.Control))
            {
                caps.Add(new ShortcutKeyCap("⌃", "ctrl", "Control"));
            }
            if (modifiers.HasFlag(KeyModifiers.Alt))
            {
                caps.Add(new ShortcutKeyCap("⌥", "alt", "Option"));
            }
            if (modifiers.HasFlag(KeyModifiers.Shift))
            {
                caps.Add(new ShortcutKeyCap("⇧", "shift", "Shift"));
            }
            if (modifiers.HasFlag(KeyModifiers.Meta))
            {
                caps.Add(new ShortcutKeyCap("⌘", "meta", "Command"));
            }
        }
        else
        {
            if (modifiers.HasFlag(KeyModifiers.Control))
            {
                caps.Add(new ShortcutKeyCap("Ctrl", "ctrl", "Control"));
            }
            if (modifiers.HasFlag(KeyModifiers.Alt))
            {
                caps.Add(new ShortcutKeyCap("Alt", "alt", "Alt"));
            }
            if (modifiers.HasFlag(KeyModifiers.Shift))
            {
                caps.Add(new ShortcutKeyCap("Shift", "shift", "Shift"));
            }
            if (modifiers.HasFlag(KeyModifiers.Meta))
            {
                caps.Add(new ShortcutKeyCap("Win", "meta", "Windows"));
            }
        }
        if (key != Key.None)
        {
            caps.Add(KeyCap(key, platform));
        }
        return caps;
    }

    private static ShortcutKeyCap KeyCap(Key key, ShortcutPlatform platform)
    {
        var id = IdOf(key);
        if (id is null)
        {
            // A key Arc never records but a caller may bind (Insert, media keys): its name.
            var name = key.ToString();
            return new ShortcutKeyCap(name, name.ToLowerInvariant(), name);
        }
        if (platform == ShortcutPlatform.Mac)
        {
            switch (id)
            {
                case "enter":
                    return new ShortcutKeyCap("↩", id, "Return");
                case "backspace":
                    return new ShortcutKeyCap("⌫", id, "Delete");
                case "delete":
                    return new ShortcutKeyCap("⌦", id, "Forward delete");
                case "tab":
                    return new ShortcutKeyCap("⇥", id, "Tab");
                case "escape":
                    return new ShortcutKeyCap("esc", id, "Escape");
            }
        }
        var named = id switch
        {
            "space" => ("Space", "Space"),
            "enter" => ("Enter", "Enter"),
            "escape" => ("Esc", "Escape"),
            "backspace" => ("Backspace", "Backspace"),
            "delete" => ("Del", "Delete"),
            "tab" => ("Tab", "Tab"),
            "up" => ("↑", "Up arrow"),
            "down" => ("↓", "Down arrow"),
            "left" => ("←", "Left arrow"),
            "right" => ("→", "Right arrow"),
            "home" => ("Home", "Home"),
            "end" => ("End", "End"),
            "pageup" => ("PgUp", "Page up"),
            "pagedown" => ("PgDn", "Page down"),
            "," => (",", "Comma"),
            "." => (".", "Period"),
            "/" => ("/", "Slash"),
            ";" => (";", "Semicolon"),
            "'" => ("'", "Quote"),
            "[" => ("[", "Left bracket"),
            "]" => ("]", "Right bracket"),
            "\\" => ("\\", "Backslash"),
            "-" => ("-", "Minus"),
            "=" => ("=", "Equals"),
            "`" => ("`", "Backtick"),
            _ => (id.ToUpperInvariant(), id.ToUpperInvariant())
        };
        return new ShortcutKeyCap(named.Item1, id, named.Item2);
    }
}
