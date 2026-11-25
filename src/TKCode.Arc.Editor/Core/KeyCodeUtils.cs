// Ported from vscode (src/vs/base/common/keyCodes.ts: ScanCodeUtils and KeyCodeUtils, commit b57d5c9e), MIT license.
// Differences from upstream: both read the immutable tables of KeyCodes (KeyCodes.Tables.cs); a key code with no
// string (upstream: undefined) reads as the empty string.

namespace TKCode.Arc.Editor;

/// <summary>Scan code names (keyboardEvent.code) to <see cref="ScanCode"/> and back.</summary>
internal static class ScanCodeUtils
{
    public static ScanCode LowerCaseToEnum(string scanCode) => KeyCodes.ScanCodeLowerCaseStrToInt(scanCode);

    public static ScanCode ToEnum(string scanCode) => KeyCodes.ScanCodeStrToInt(scanCode);

    public static string ToString(ScanCode scanCode)
    {
        var str = KeyCodes.ScanCodeIntToStr(scanCode);
        return string.IsNullOrEmpty(str) ? "None" : str;
    }
}

/// <summary>Key code names: for the UI and for user settings (keybindings.json).</summary>
internal static class KeyCodeUtils
{
    public static string ToString(KeyCode keyCode) => KeyCodes.UiMap.KeyCodeToStr(keyCode);

    public static KeyCode FromString(string key) => KeyCodes.UiMap.StrToKeyCode(key);

    public static string ToUserSettingsUS(KeyCode keyCode) => KeyCodes.UserSettingsUSMap.KeyCodeToStr(keyCode);

    public static string ToUserSettingsGeneral(KeyCode keyCode) => KeyCodes.UserSettingsGeneralMap.KeyCodeToStr(keyCode);

    public static KeyCode FromUserSettings(string key)
    {
        var keyCode = KeyCodes.UserSettingsUSMap.StrToKeyCode(key);
        return keyCode != KeyCode.Unknown ? keyCode : KeyCodes.UserSettingsGeneralMap.StrToKeyCode(key);
    }

    public static string? ToElectronAccelerator(KeyCode keyCode)
    {
        if (keyCode >= KeyCode.Numpad0 && keyCode <= KeyCode.NumpadDivide)
        {
            // [Electron Accelerators] Electron is able to parse numpad keys, but unfortunately it
            // renders them just as regular keys in menus. For example, num0 is rendered as "0",
            // numdiv is rendered as "/", numsub is rendered as "-".
            //
            // This can lead to incredible confusion, as it makes numpad based keybindings indistinguishable
            // from keybindings based on regular keys.
            //
            // We therefore need to fall back to custom rendering for numpad keys.
            return null;
        }

        switch (keyCode)
        {
            case KeyCode.UpArrow:
                return "Up";
            case KeyCode.DownArrow:
                return "Down";
            case KeyCode.LeftArrow:
                return "Left";
            case KeyCode.RightArrow:
                return "Right";
            case KeyCode.Delete:
                return "Delete";
        }

        return KeyCodes.UiMap.KeyCodeToStr(keyCode);
    }
}
