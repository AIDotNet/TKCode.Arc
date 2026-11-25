// Ported from vscode (src/vs/base/common/keybindings.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
// - Upstream's `type Chord = KeyCodeChord | ScanCodeChord` is an abstract class Chord that both derive from, and the
//   Modifiers interface is IModifiers.
// - getHashCode() returns a string upstream, which C# cannot overload against object.GetHashCode(): it is
//   GetHashCodeString() here. Chord and Keybinding also override Equals(object) and GetHashCode() consistently with
//   upstream's equals, so they can be dictionary keys.
// - decodeKeybinding has one overload for the number form and one for the array form; the operating system is an
//   OperatingSystemKind. illegalArgument is an ArgumentException.
// - SingleModifierChord ('ctrl' | 'shift' | 'alt' | 'meta') stays a string; SingleModifierChord holds the four values.
// - ResolvedChord and ResolvedKeybinding are in ResolvedKeybinding.cs.

namespace TKCode.Arc.Editor;

/// <summary>Binary encoding of a keybinding number (upstream's BinaryKeybindingsMask).</summary>
/// <remarks>
/// <code>
///    1111 11
///    5432 1098 7654 3210
///    ---- CSAW KKKK KKKK
///  C = bit 11 = ctrlCmd flag
///  S = bit 10 = shift flag
///  A = bit 9 = alt flag
///  W = bit 8 = winCtrl flag
///  K = bits 0-7 = key code
/// </code>
/// </remarks>
internal static class BinaryKeybindingsMask
{
    public const int CtrlCmd = 1 << 11;
    public const int Shift = 1 << 10;
    public const int Alt = 1 << 9;
    public const int WinCtrl = 1 << 8;
    public const int KeyCode = 0x000000FF;
}

/// <summary>Decodes keybinding numbers (KeyMod | KeyCode, KeyChord) into <see cref="Keybinding"/>s.</summary>
internal static class Keybindings
{
    public static Keybinding? DecodeKeybinding(int keybinding, OperatingSystemKind os)
    {
        if (keybinding == 0)
        {
            return null;
        }

        var firstChord = keybinding & 0x0000FFFF;
        var secondChord = (int)(((uint)keybinding & 0xFFFF0000) >> 16);
        if (secondChord != 0)
        {
            return new Keybinding([
                CreateSimpleKeybinding(firstChord, os),
                CreateSimpleKeybinding(secondChord, os),
            ]);
        }

        return new Keybinding([CreateSimpleKeybinding(firstChord, os)]);
    }

    public static Keybinding? DecodeKeybinding(IReadOnlyList<int> keybinding, OperatingSystemKind os)
    {
        var chords = new List<Chord>(keybinding.Count);
        for (var i = 0; i < keybinding.Count; i++)
        {
            chords.Add(CreateSimpleKeybinding(keybinding[i], os));
        }

        return new Keybinding(chords);
    }

    public static KeyCodeChord CreateSimpleKeybinding(int keybinding, OperatingSystemKind os)
    {
        var ctrlCmd = (keybinding & BinaryKeybindingsMask.CtrlCmd) != 0;
        var winCtrl = (keybinding & BinaryKeybindingsMask.WinCtrl) != 0;

        var ctrlKey = os == OperatingSystemKind.Macintosh ? winCtrl : ctrlCmd;
        var shiftKey = (keybinding & BinaryKeybindingsMask.Shift) != 0;
        var altKey = (keybinding & BinaryKeybindingsMask.Alt) != 0;
        var metaKey = os == OperatingSystemKind.Macintosh ? ctrlCmd : winCtrl;
        var keyCode = (KeyCode)(keybinding & BinaryKeybindingsMask.KeyCode);

        return new KeyCodeChord(ctrlKey, shiftKey, altKey, metaKey, keyCode);
    }
}

/// <summary>The modifier flags of a chord (upstream's Modifiers).</summary>
internal interface IModifiers
{
    bool CtrlKey { get; }

    bool ShiftKey { get; }

    bool AltKey { get; }

    bool MetaKey { get; }
}

/// <summary>A combination of keys pressed simultaneously: a <see cref="KeyCodeChord"/> or a <see cref="ScanCodeChord"/>.</summary>
internal abstract class Chord : IModifiers, IEquatable<Chord>
{
    protected Chord(bool ctrlKey, bool shiftKey, bool altKey, bool metaKey)
    {
        CtrlKey = ctrlKey;
        ShiftKey = shiftKey;
        AltKey = altKey;
        MetaKey = metaKey;
    }

    public bool CtrlKey { get; }

    public bool ShiftKey { get; }

    public bool AltKey { get; }

    public bool MetaKey { get; }

    public abstract bool Equals(Chord? other);

    /// <summary>A string that identifies the chord (upstream's getHashCode).</summary>
    public abstract string GetHashCodeString();

    /// <summary>Does this keybinding refer to the key code of a modifier and it also has the modifier flag?</summary>
    public abstract bool IsDuplicateModifierCase();

    public override bool Equals(object? obj) => obj is Chord other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(GetHashCodeString());

    public override string ToString() => GetHashCodeString();

    private protected string ModifierBits() =>
        string.Concat(CtrlKey ? "1" : "0", ShiftKey ? "1" : "0", AltKey ? "1" : "0", MetaKey ? "1" : "0");
}

/// <summary>A chord which uses the `keyCode` field of keyboard events.</summary>
internal sealed class KeyCodeChord : Chord
{
    public KeyCodeChord(bool ctrlKey, bool shiftKey, bool altKey, bool metaKey, KeyCode keyCode)
        : base(ctrlKey, shiftKey, altKey, metaKey)
    {
        KeyCode = keyCode;
    }

    public KeyCode KeyCode { get; }

    public override bool Equals(Chord? other)
    {
        return other is KeyCodeChord o
            && CtrlKey == o.CtrlKey
            && ShiftKey == o.ShiftKey
            && AltKey == o.AltKey
            && MetaKey == o.MetaKey
            && KeyCode == o.KeyCode;
    }

    public override string GetHashCodeString() => $"K{ModifierBits()}{(int)KeyCode}";

    public bool IsModifierKey()
    {
        return KeyCode == KeyCode.Unknown
            || KeyCode == KeyCode.Ctrl
            || KeyCode == KeyCode.Meta
            || KeyCode == KeyCode.Alt
            || KeyCode == KeyCode.Shift;
    }

    public Keybinding ToKeybinding() => new([this]);

    public override bool IsDuplicateModifierCase()
    {
        return (CtrlKey && KeyCode == KeyCode.Ctrl)
            || (ShiftKey && KeyCode == KeyCode.Shift)
            || (AltKey && KeyCode == KeyCode.Alt)
            || (MetaKey && KeyCode == KeyCode.Meta);
    }
}

/// <summary>A chord which uses the `code` field of keyboard events.</summary>
internal sealed class ScanCodeChord : Chord
{
    public ScanCodeChord(bool ctrlKey, bool shiftKey, bool altKey, bool metaKey, ScanCode scanCode)
        : base(ctrlKey, shiftKey, altKey, metaKey)
    {
        ScanCode = scanCode;
    }

    public ScanCode ScanCode { get; }

    public override bool Equals(Chord? other)
    {
        return other is ScanCodeChord o
            && CtrlKey == o.CtrlKey
            && ShiftKey == o.ShiftKey
            && AltKey == o.AltKey
            && MetaKey == o.MetaKey
            && ScanCode == o.ScanCode;
    }

    public override string GetHashCodeString() => $"S{ModifierBits()}{(int)ScanCode}";

    public override bool IsDuplicateModifierCase()
    {
        return (CtrlKey && (ScanCode == ScanCode.ControlLeft || ScanCode == ScanCode.ControlRight))
            || (ShiftKey && (ScanCode == ScanCode.ShiftLeft || ScanCode == ScanCode.ShiftRight))
            || (AltKey && (ScanCode == ScanCode.AltLeft || ScanCode == ScanCode.AltRight))
            || (MetaKey && (ScanCode == ScanCode.MetaLeft || ScanCode == ScanCode.MetaRight));
    }
}

/// <summary>A keybinding is a sequence of chords.</summary>
internal sealed class Keybinding : IEquatable<Keybinding>
{
    public Keybinding(IReadOnlyList<Chord> chords)
    {
        if (chords.Count == 0)
        {
            throw new ArgumentException("Illegal argument: chords", nameof(chords));
        }

        Chords = chords;
    }

    public IReadOnlyList<Chord> Chords { get; }

    /// <summary>A string that identifies the keybinding (upstream's getHashCode).</summary>
    public string GetHashCodeString()
    {
        var result = string.Empty;
        for (var i = 0; i < Chords.Count; i++)
        {
            if (i != 0)
            {
                result += ";";
            }

            result += Chords[i].GetHashCodeString();
        }

        return result;
    }

    public bool Equals(Keybinding? other)
    {
        if (other is null)
        {
            return false;
        }

        if (Chords.Count != other.Chords.Count)
        {
            return false;
        }

        for (var i = 0; i < Chords.Count; i++)
        {
            if (!Chords[i].Equals(other.Chords[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is Keybinding other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(GetHashCodeString());

    public override string ToString() => GetHashCodeString();
}
