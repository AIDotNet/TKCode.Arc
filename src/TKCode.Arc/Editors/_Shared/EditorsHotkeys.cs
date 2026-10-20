using Avalonia;
using Avalonia.Input;
using Avalonia.Input.Platform;

namespace TKCode.Arc;

/// <summary>Shortcut helpers shared by the editors: Arc's Mod key (Cmd or Ctrl) and the platform's text navigation gestures.</summary>
internal static class EditorsHotkeys
{
    /// <summary>
    /// Arc's "Mod": Meta or Control (both are accepted, as Arc does). Control with Alt is AltGr on Windows keyboards,
    /// which types characters, so it is not a shortcut.
    /// </summary>
    public static bool IsMod(KeyModifiers modifiers) =>
        (modifiers & KeyModifiers.Meta) != 0 || ((modifiers & KeyModifiers.Control) != 0 && (modifiers & KeyModifiers.Alt) == 0);

    public static PlatformHotkeyConfiguration? Keymap => Application.Current?.PlatformSettings?.HotkeyConfiguration;

    /// <summary>The modifier that moves by words: Alt on macOS, Control elsewhere.</summary>
    public static KeyModifiers WordModifiers => Keymap?.WholeWordTextActionModifiers ?? KeyModifiers.Control;

    public static bool Matches(List<KeyGesture>? gestures, KeyEventArgs e)
    {
        if (gestures is null)
        {
            return false;
        }
        foreach (var gesture in gestures)
        {
            if (gesture.Matches(e))
            {
                return true;
            }
        }
        return false;
    }
}
