// Ported from vscode (src/vs/platform/keybinding/common/usLayoutResolvedKeybinding.ts, commit b57d5c9e), MIT license.
// Differences from upstream: private and protected members drop the leading underscore; a key code with no string
// gives the empty string where upstream concatenates undefined.

namespace TKCode.Arc.Editor.Platform;

/// <summary>
/// A keybinding resolved for the US keyboard layout. Do not instantiate: use a keybinding service to get a
/// ResolvedKeybinding seeded with information about the current keyboard layout.
/// </summary>
internal sealed class USLayoutResolvedKeybinding : BaseResolvedKeybinding<KeyCodeChord>
{
    public USLayoutResolvedKeybinding(IReadOnlyList<KeyCodeChord> chords, OperatingSystemKind os)
        : base(os, chords)
    {
    }

    private string KeyCodeToUILabel(KeyCode keyCode)
    {
        if (Os == OperatingSystemKind.Macintosh)
        {
            switch (keyCode)
            {
                case KeyCode.LeftArrow:
                    return "←";
                case KeyCode.UpArrow:
                    return "↑";
                case KeyCode.RightArrow:
                    return "→";
                case KeyCode.DownArrow:
                    return "↓";
            }
        }

        return KeyCodeUtils.ToString(keyCode);
    }

    protected override string? GetLabel(KeyCodeChord chord)
    {
        if (chord.IsDuplicateModifierCase())
        {
            return string.Empty;
        }

        return KeyCodeToUILabel(chord.KeyCode);
    }

    protected override string? GetAriaLabel(KeyCodeChord chord)
    {
        if (chord.IsDuplicateModifierCase())
        {
            return string.Empty;
        }

        return KeyCodeUtils.ToString(chord.KeyCode);
    }

    protected override string? GetElectronAccelerator(KeyCodeChord chord) => KeyCodeUtils.ToElectronAccelerator(chord.KeyCode);

    protected override string? GetUserSettingsLabel(KeyCodeChord chord)
    {
        if (chord.IsDuplicateModifierCase())
        {
            return string.Empty;
        }

        var result = KeyCodeUtils.ToUserSettingsUS(chord.KeyCode);
        return result.ToLowerInvariant();
    }

    protected override bool IsWYSIWYG(KeyCodeChord keybinding) => true;

    protected override string? GetChordDispatch(KeyCodeChord chord) => GetDispatchStr(chord);

    public static string? GetDispatchStr(KeyCodeChord chord)
    {
        if (chord.IsModifierKey())
        {
            return null;
        }

        var result = string.Empty;

        if (chord.CtrlKey)
        {
            result += "ctrl+";
        }

        if (chord.ShiftKey)
        {
            result += "shift+";
        }

        if (chord.AltKey)
        {
            result += "alt+";
        }

        if (chord.MetaKey)
        {
            result += "meta+";
        }

        result += KeyCodeUtils.ToString(chord.KeyCode);

        return result;
    }

    protected override string? GetSingleModifierChordDispatch(KeyCodeChord keybinding)
    {
        if (keybinding.KeyCode == KeyCode.Ctrl && !keybinding.ShiftKey && !keybinding.AltKey && !keybinding.MetaKey)
        {
            return SingleModifierChord.Ctrl;
        }

        if (keybinding.KeyCode == KeyCode.Shift && !keybinding.CtrlKey && !keybinding.AltKey && !keybinding.MetaKey)
        {
            return SingleModifierChord.Shift;
        }

        if (keybinding.KeyCode == KeyCode.Alt && !keybinding.CtrlKey && !keybinding.ShiftKey && !keybinding.MetaKey)
        {
            return SingleModifierChord.Alt;
        }

        if (keybinding.KeyCode == KeyCode.Meta && !keybinding.CtrlKey && !keybinding.ShiftKey && !keybinding.AltKey)
        {
            return SingleModifierChord.Meta;
        }

        return null;
    }

    /// <summary>*NOTE*: Check return value for <see cref="KeyCode.Unknown"/>.</summary>
    private static KeyCode ScanCodeToKeyCode(ScanCode scanCode)
    {
        if ((int)scanCode < 0 || (int)scanCode >= KeyCodes.ImmutableCodeToKeyCode.Length)
        {
            return KeyCode.Unknown;
        }

        var immutableKeyCode = KeyCodes.ImmutableCodeToKeyCode[(int)scanCode];
        if (immutableKeyCode != KeyCode.DependsOnKbLayout)
        {
            return immutableKeyCode;
        }

        return scanCode switch
        {
            ScanCode.KeyA => KeyCode.KeyA,
            ScanCode.KeyB => KeyCode.KeyB,
            ScanCode.KeyC => KeyCode.KeyC,
            ScanCode.KeyD => KeyCode.KeyD,
            ScanCode.KeyE => KeyCode.KeyE,
            ScanCode.KeyF => KeyCode.KeyF,
            ScanCode.KeyG => KeyCode.KeyG,
            ScanCode.KeyH => KeyCode.KeyH,
            ScanCode.KeyI => KeyCode.KeyI,
            ScanCode.KeyJ => KeyCode.KeyJ,
            ScanCode.KeyK => KeyCode.KeyK,
            ScanCode.KeyL => KeyCode.KeyL,
            ScanCode.KeyM => KeyCode.KeyM,
            ScanCode.KeyN => KeyCode.KeyN,
            ScanCode.KeyO => KeyCode.KeyO,
            ScanCode.KeyP => KeyCode.KeyP,
            ScanCode.KeyQ => KeyCode.KeyQ,
            ScanCode.KeyR => KeyCode.KeyR,
            ScanCode.KeyS => KeyCode.KeyS,
            ScanCode.KeyT => KeyCode.KeyT,
            ScanCode.KeyU => KeyCode.KeyU,
            ScanCode.KeyV => KeyCode.KeyV,
            ScanCode.KeyW => KeyCode.KeyW,
            ScanCode.KeyX => KeyCode.KeyX,
            ScanCode.KeyY => KeyCode.KeyY,
            ScanCode.KeyZ => KeyCode.KeyZ,
            ScanCode.Digit1 => KeyCode.Digit1,
            ScanCode.Digit2 => KeyCode.Digit2,
            ScanCode.Digit3 => KeyCode.Digit3,
            ScanCode.Digit4 => KeyCode.Digit4,
            ScanCode.Digit5 => KeyCode.Digit5,
            ScanCode.Digit6 => KeyCode.Digit6,
            ScanCode.Digit7 => KeyCode.Digit7,
            ScanCode.Digit8 => KeyCode.Digit8,
            ScanCode.Digit9 => KeyCode.Digit9,
            ScanCode.Digit0 => KeyCode.Digit0,
            ScanCode.Minus => KeyCode.Minus,
            ScanCode.Equal => KeyCode.Equal,
            ScanCode.BracketLeft => KeyCode.BracketLeft,
            ScanCode.BracketRight => KeyCode.BracketRight,
            ScanCode.Backslash => KeyCode.Backslash,
            ScanCode.IntlHash => KeyCode.Unknown, // missing
            ScanCode.Semicolon => KeyCode.Semicolon,
            ScanCode.Quote => KeyCode.Quote,
            ScanCode.Backquote => KeyCode.Backquote,
            ScanCode.Comma => KeyCode.Comma,
            ScanCode.Period => KeyCode.Period,
            ScanCode.Slash => KeyCode.Slash,
            ScanCode.IntlBackslash => KeyCode.IntlBackslash,
            _ => KeyCode.Unknown,
        };
    }

    private static KeyCodeChord? ToKeyCodeChord(Chord? chord)
    {
        if (chord is null)
        {
            return null;
        }

        if (chord is KeyCodeChord keyCodeChord)
        {
            return keyCodeChord;
        }

        var scanCodeChord = (ScanCodeChord)chord;
        var keyCode = ScanCodeToKeyCode(scanCodeChord.ScanCode);
        if (keyCode == KeyCode.Unknown)
        {
            return null;
        }

        return new KeyCodeChord(chord.CtrlKey, chord.ShiftKey, chord.AltKey, chord.MetaKey, keyCode);
    }

    public static IReadOnlyList<USLayoutResolvedKeybinding> ResolveKeybinding(Keybinding keybinding, OperatingSystemKind os)
    {
        var converted = new KeyCodeChord?[keybinding.Chords.Count];
        for (var i = 0; i < converted.Length; i++)
        {
            converted[i] = ToKeyCodeChord(keybinding.Chords[i]);
        }

        var chords = ResolvedKeybindingItems.ToEmptyArrayIfContainsNull(converted);
        if (chords.Count > 0)
        {
            return [new USLayoutResolvedKeybinding(chords, os)];
        }

        return [];
    }
}
