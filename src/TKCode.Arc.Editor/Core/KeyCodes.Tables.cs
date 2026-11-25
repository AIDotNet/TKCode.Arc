// Ported from vscode (src/vs/base/common/keyCodes.ts: KeyCodeStrMap, the module-level maps, KeyMod, KeyChord and
// isModifierKey, commit b57d5c9e), MIT license. Differences from upstream:
// - The module-level maps are built once, by the static constructor of KeyCodes (the CLR runs it once, thread safely),
//   and are immutable afterwards: frozen dictionaries and immutable arrays. Upstream's are mutable module globals.
// - SCREAMING_CASE names are PascalCase: EVENT_KEY_CODE_MAP -> EventKeyCodeMap, NATIVE_WINDOWS_KEY_CODE_TO_KEY_CODE ->
//   NativeWindowsKeyCodeToKeyCode, SCAN_CODE_STR_TO_EVENT_KEY_CODE -> ScanCodeStrToEventKeyCode,
//   IMMUTABLE_CODE_TO_KEY_CODE -> ImmutableCodeToKeyCode, IMMUTABLE_KEY_CODE_TO_CODE -> ImmutableKeyCodeToCode.
// - EventKeyCodeMap is a dictionary of the event key codes that have an entry; upstream's is a sparse array whose other
//   indices read undefined.
// - KeyMod is a static class of int constants, not an enum, so that `KeyMod.CtrlCmd | (int)KeyCode.KeyA` gives the
//   encoded keybinding as upstream's `KeyMod.CtrlCmd | KeyCode.KeyA` does (C# does not combine two enum types).
// - KeyChord and isModifierKey are static methods of KeyCodes (`using static TKCode.Arc.Editor.KeyCodes;` reads as
//   upstream).
// - A key code with no string (upstream: undefined) reads as the empty string.

using System.Collections.Frozen;
using System.Collections.Immutable;

namespace TKCode.Arc.Editor;

/// <summary>The modifier bits of an encoded keybinding (upstream's const enum KeyMod).</summary>
internal static class KeyMod
{
    public const int CtrlCmd = 1 << 11;
    public const int Shift = 1 << 10;
    public const int Alt = 1 << 9;
    public const int WinCtrl = 1 << 8;
}

/// <summary>A two-way map between key codes and their names (upstream's KeyCodeStrMap); immutable once built.</summary>
internal sealed class KeyCodeStrMap
{
    private readonly string?[] _keyCodeToStr;
    private readonly FrozenDictionary<string, KeyCode> _strToKeyCode;

    private KeyCodeStrMap(string?[] keyCodeToStr, Dictionary<string, KeyCode> strToKeyCode)
    {
        _keyCodeToStr = keyCodeToStr;
        _strToKeyCode = strToKeyCode.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public string KeyCodeToStr(KeyCode keyCode)
    {
        var index = (int)keyCode;
        if (index < 0 || index >= _keyCodeToStr.Length)
        {
            return string.Empty;
        }

        return _keyCodeToStr[index] ?? string.Empty;
    }

    public KeyCode StrToKeyCode(string str)
    {
        return _strToKeyCode.TryGetValue(str.ToLowerInvariant(), out var keyCode) ? keyCode : KeyCode.Unknown;
    }

    /// <summary>Collects the definitions while the tables are built (upstream's define), then freezes them.</summary>
    internal sealed class Builder
    {
        private readonly string?[] _keyCodeToStr = new string?[(int)KeyCode.MaxValue + 1];
        private readonly Dictionary<string, KeyCode> _strToKeyCode = new(StringComparer.Ordinal);

        public void Define(KeyCode keyCode, string str)
        {
            _keyCodeToStr[(int)keyCode] = str;
            _strToKeyCode[str.ToLowerInvariant()] = keyCode;
        }

        public KeyCodeStrMap Build() => new(_keyCodeToStr, _strToKeyCode);
    }
}

/// <summary>The key code tables of upstream's keyCodes.ts, and KeyChord and isModifierKey.</summary>
internal static partial class KeyCodes
{
    internal static readonly KeyCodeStrMap UiMap;
    internal static readonly KeyCodeStrMap UserSettingsUSMap;
    internal static readonly KeyCodeStrMap UserSettingsGeneralMap;

    private static readonly string?[] s_scanCodeIntToStr;
    private static readonly FrozenDictionary<string, ScanCode> s_scanCodeStrToInt;
    private static readonly FrozenDictionary<string, ScanCode> s_scanCodeLowerCaseStrToInt;

    static KeyCodes()
    {
        var uiMap = new KeyCodeStrMap.Builder();
        var userSettingsUSMap = new KeyCodeStrMap.Builder();
        var userSettingsGeneralMap = new KeyCodeStrMap.Builder();
        var eventKeyCodeMap = new Dictionary<int, KeyCode>();
        var nativeWindowsKeyCodeToKeyCode = new Dictionary<string, KeyCode>(StringComparer.Ordinal);
        var scanCodeStrToEventKeyCode = new Dictionary<string, int>(StringComparer.Ordinal);
        var scanCodeIntToStr = new string?[(int)ScanCode.MaxValue + 1];
        var scanCodeStrToInt = new Dictionary<string, ScanCode>(StringComparer.Ordinal);
        var scanCodeLowerCaseStrToInt = new Dictionary<string, ScanCode>(StringComparer.Ordinal);

        // -1 if a ScanCode => KeyCode mapping depends on kb layout.
        var immutableCodeToKeyCode = new KeyCode[(int)ScanCode.MaxValue + 1];
        Array.Fill(immutableCodeToKeyCode, KeyCode.DependsOnKbLayout);

        // -1 if a KeyCode => ScanCode mapping depends on kb layout.
        var immutableKeyCodeToCode = new ScanCode[(int)KeyCode.MaxValue + 1];
        Array.Fill(immutableKeyCodeToCode, ScanCode.DependsOnKbLayout);

        var seenKeyCode = new bool[(int)KeyCode.MaxValue + 1];
        var seenScanCode = new bool[(int)ScanCode.MaxValue + 1];
        foreach (var mapping in CreateMappings())
        {
            var (immutable, scanCode, scanCodeStr, keyCode, keyCodeStr, eventKeyCode, vkey, usUserSettingsLabel, generalUserSettingsLabel) = mapping;
            if (!seenScanCode[(int)scanCode])
            {
                seenScanCode[(int)scanCode] = true;
                scanCodeIntToStr[(int)scanCode] = scanCodeStr;
                scanCodeStrToInt[scanCodeStr] = scanCode;
                scanCodeLowerCaseStrToInt[scanCodeStr.ToLowerInvariant()] = scanCode;
                if (immutable != 0)
                {
                    immutableCodeToKeyCode[(int)scanCode] = keyCode;
                    if (keyCode != KeyCode.Unknown && keyCode != KeyCode.Enter && !IsModifierKey(keyCode))
                    {
                        immutableKeyCodeToCode[(int)keyCode] = scanCode;
                    }
                }
            }

            if (!seenKeyCode[(int)keyCode])
            {
                seenKeyCode[(int)keyCode] = true;
                if (keyCodeStr.Length == 0)
                {
                    throw new InvalidOperationException($"String representation missing for key code {(int)keyCode} around scan code {scanCodeStr}");
                }

                uiMap.Define(keyCode, keyCodeStr);
                userSettingsUSMap.Define(keyCode, usUserSettingsLabel.Length > 0 ? usUserSettingsLabel : keyCodeStr);
                userSettingsGeneralMap.Define(
                    keyCode,
                    generalUserSettingsLabel.Length > 0 ? generalUserSettingsLabel : (usUserSettingsLabel.Length > 0 ? usUserSettingsLabel : keyCodeStr));
            }

            if (eventKeyCode != 0)
            {
                eventKeyCodeMap[eventKeyCode] = keyCode;
            }

            if (scanCodeStr.Length > 0)
            {
                scanCodeStrToEventKeyCode[scanCodeStr] = eventKeyCode;
            }

            if (vkey.Length > 0)
            {
                nativeWindowsKeyCodeToKeyCode[vkey] = keyCode;
            }
        }

        // Manually added due to the exclusion above (due to duplication with NumpadEnter)
        immutableKeyCodeToCode[(int)KeyCode.Enter] = ScanCode.Enter;

        UiMap = uiMap.Build();
        UserSettingsUSMap = userSettingsUSMap.Build();
        UserSettingsGeneralMap = userSettingsGeneralMap.Build();
        EventKeyCodeMap = eventKeyCodeMap.ToFrozenDictionary();
        NativeWindowsKeyCodeToKeyCode = nativeWindowsKeyCodeToKeyCode.ToFrozenDictionary(StringComparer.Ordinal);
        ScanCodeStrToEventKeyCode = scanCodeStrToEventKeyCode.ToFrozenDictionary(StringComparer.Ordinal);
        ImmutableCodeToKeyCode = ImmutableArray.Create(immutableCodeToKeyCode);
        ImmutableKeyCodeToCode = ImmutableArray.Create(immutableKeyCodeToCode);
        s_scanCodeIntToStr = scanCodeIntToStr;
        s_scanCodeStrToInt = scanCodeStrToInt.ToFrozenDictionary(StringComparer.Ordinal);
        s_scanCodeLowerCaseStrToInt = scanCodeLowerCaseStrToInt.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>keyboardEvent.keyCode to KeyCode, for the event key codes that have one.</summary>
    public static FrozenDictionary<int, KeyCode> EventKeyCodeMap { get; }

    /// <summary>Windows virtual key names (VK_*) to KeyCode.</summary>
    public static FrozenDictionary<string, KeyCode> NativeWindowsKeyCodeToKeyCode { get; }

    /// <summary>Scan code names (keyboardEvent.code) to keyboardEvent.keyCode.</summary>
    public static FrozenDictionary<string, int> ScanCodeStrToEventKeyCode { get; }

    /// <summary>Indexed by ScanCode: its KeyCode, or <see cref="KeyCode.DependsOnKbLayout"/> if that depends on the keyboard layout.</summary>
    public static ImmutableArray<KeyCode> ImmutableCodeToKeyCode { get; }

    /// <summary>Indexed by KeyCode: its ScanCode, or <see cref="ScanCode.DependsOnKbLayout"/> if that depends on the keyboard layout.</summary>
    public static ImmutableArray<ScanCode> ImmutableKeyCodeToCode { get; }

    /// <summary>Encodes a two-chord keybinding: the second chord goes into the upper 16 bits.</summary>
    public static int KeyChord(int firstPart, int secondPart)
    {
        var chordPart = (secondPart & 0x0000FFFF) << 16;
        return firstPart | chordPart;
    }

    public static bool IsModifierKey(KeyCode keyCode)
    {
        return keyCode == KeyCode.Ctrl
            || keyCode == KeyCode.Shift
            || keyCode == KeyCode.Alt
            || keyCode == KeyCode.Meta;
    }

    internal static string? ScanCodeIntToStr(ScanCode scanCode)
    {
        var index = (int)scanCode;
        return index >= 0 && index < s_scanCodeIntToStr.Length ? s_scanCodeIntToStr[index] : null;
    }

    internal static ScanCode ScanCodeStrToInt(string code) =>
        s_scanCodeStrToInt.TryGetValue(code, out var scanCode) ? scanCode : ScanCode.None;

    internal static ScanCode ScanCodeLowerCaseStrToInt(string code) =>
        s_scanCodeLowerCaseStrToInt.TryGetValue(code, out var scanCode) ? scanCode : ScanCode.None;
}
