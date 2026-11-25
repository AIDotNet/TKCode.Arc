// Ported from vscode (src/vs/base/common/keybindingLabels.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
// - ModifierLabels is a record and KeyLabelProvider a Func; ModifierLabelProvider.ModifierLabels is indexed by
//   OperatingSystemKind through LabelsFor.
// - The engine has no localization yet: the nls.localize strings of the UI and ARIA providers are their English
//   defaults, kept in KeybindingLabelStrings so they can be localized later.

namespace TKCode.Arc.Editor;

internal sealed record ModifierLabels(string CtrlKey, string ShiftKey, string AltKey, string MetaKey, string Separator);

internal sealed class ModifierLabelProvider
{
    private readonly ModifierLabels _mac;
    private readonly ModifierLabels _windows;
    private readonly ModifierLabels _linux;

    public ModifierLabelProvider(ModifierLabels mac, ModifierLabels windows, ModifierLabels? linux = null)
    {
        _mac = mac;
        _windows = windows;
        _linux = linux ?? windows;
    }

    public ModifierLabels LabelsFor(OperatingSystemKind os) => os switch
    {
        OperatingSystemKind.Macintosh => _mac,
        OperatingSystemKind.Windows => _windows,
        _ => _linux,
    };

    public string? ToLabel<T>(OperatingSystemKind os, IReadOnlyList<T> chords, Func<T, string?> keyLabelProvider)
        where T : IModifiers
    {
        if (chords.Count == 0)
        {
            return null;
        }

        var result = new string[chords.Count];
        for (var i = 0; i < chords.Count; i++)
        {
            var chord = chords[i];
            var keyLabel = keyLabelProvider(chord);
            if (keyLabel is null)
            {
                // this keybinding cannot be expressed...
                return null;
            }

            result[i] = SimpleAsString(chord, keyLabel, LabelsFor(os));
        }

        return string.Join(' ', result);
    }

    private static string SimpleAsString(IModifiers modifiers, string key, ModifierLabels labels)
    {
        var result = new List<string>(5);

        // translate modifier keys: Ctrl-Shift-Alt-Meta
        if (modifiers.CtrlKey)
        {
            result.Add(labels.CtrlKey);
        }

        if (modifiers.ShiftKey)
        {
            result.Add(labels.ShiftKey);
        }

        if (modifiers.AltKey)
        {
            result.Add(labels.AltKey);
        }

        if (modifiers.MetaKey)
        {
            result.Add(labels.MetaKey);
        }

        // the actual key
        if (key.Length != 0)
        {
            result.Add(key);
        }

        return string.Join(labels.Separator, result);
    }
}

/// <summary>The localizable strings of the label providers, in English.</summary>
internal static class KeybindingLabelStrings
{
    // short forms
    public const string CtrlKey = "Ctrl";
    public const string ShiftKey = "Shift";
    public const string AltKey = "Alt";
    public const string WindowsKey = "Windows";
    public const string SuperKey = "Super";

    // long forms
    public const string CtrlKeyLong = "Control";
    public const string ShiftKeyLong = "Shift";
    public const string OptKeyLong = "Option";
    public const string CmdKeyLong = "Command";
    public const string AltKeyLong = "Alt";
    public const string WindowsKeyLong = "Windows";
    public const string SuperKeyLong = "Super";
}

internal static class KeybindingLabels
{
    /// <summary>A label provider that prints modifiers in a suitable format for displaying in the UI.</summary>
    public static readonly ModifierLabelProvider UILabelProvider = new(
        new ModifierLabels("⌃", "⇧", "⌥", "⌘", string.Empty),
        new ModifierLabels(KeybindingLabelStrings.CtrlKey, KeybindingLabelStrings.ShiftKey, KeybindingLabelStrings.AltKey, KeybindingLabelStrings.WindowsKey, "+"),
        new ModifierLabels(KeybindingLabelStrings.CtrlKey, KeybindingLabelStrings.ShiftKey, KeybindingLabelStrings.AltKey, KeybindingLabelStrings.SuperKey, "+"));

    /// <summary>A label provider that prints modifiers in a suitable format for ARIA.</summary>
    public static readonly ModifierLabelProvider AriaLabelProvider = new(
        new ModifierLabels(KeybindingLabelStrings.CtrlKeyLong, KeybindingLabelStrings.ShiftKeyLong, KeybindingLabelStrings.OptKeyLong, KeybindingLabelStrings.CmdKeyLong, "+"),
        new ModifierLabels(KeybindingLabelStrings.CtrlKeyLong, KeybindingLabelStrings.ShiftKeyLong, KeybindingLabelStrings.AltKeyLong, KeybindingLabelStrings.WindowsKeyLong, "+"),
        new ModifierLabels(KeybindingLabelStrings.CtrlKeyLong, KeybindingLabelStrings.ShiftKeyLong, KeybindingLabelStrings.AltKeyLong, KeybindingLabelStrings.SuperKeyLong, "+"));

    /// <summary>
    /// A label provider that prints modifiers in a suitable format for Electron Accelerators.
    /// See https://github.com/electron/electron/blob/master/docs/api/accelerator.md
    /// </summary>
    public static readonly ModifierLabelProvider ElectronAcceleratorLabelProvider = new(
        new ModifierLabels("Ctrl", "Shift", "Alt", "Cmd", "+"),
        new ModifierLabels("Ctrl", "Shift", "Alt", "Super", "+"));

    /// <summary>A label provider that prints modifiers in a suitable format for user settings.</summary>
    public static readonly ModifierLabelProvider UserSettingsLabelProvider = new(
        new ModifierLabels("ctrl", "shift", "alt", "cmd", "+"),
        new ModifierLabels("ctrl", "shift", "alt", "win", "+"),
        new ModifierLabels("ctrl", "shift", "alt", "meta", "+"));
}
