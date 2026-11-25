// Ported from vscode (src/vs/base/common/keybindings.ts: ResolvedChord, SingleModifierChord and ResolvedKeybinding,
// commit b57d5c9e), MIT license. Differences from upstream: SingleModifierChord is a string with the four values as
// constants; the abstract members return IReadOnlyList instead of arrays.

namespace TKCode.Arc.Editor;

/// <summary>One chord of a resolved keybinding, with its labels.</summary>
internal sealed class ResolvedChord : IModifiers
{
    public ResolvedChord(bool ctrlKey, bool shiftKey, bool altKey, bool metaKey, string? keyLabel, string? keyAriaLabel)
    {
        CtrlKey = ctrlKey;
        ShiftKey = shiftKey;
        AltKey = altKey;
        MetaKey = metaKey;
        KeyLabel = keyLabel;
        KeyAriaLabel = keyAriaLabel;
    }

    public bool CtrlKey { get; }

    public bool ShiftKey { get; }

    public bool AltKey { get; }

    public bool MetaKey { get; }

    public string? KeyLabel { get; }

    public string? KeyAriaLabel { get; }
}

/// <summary>The dispatch strings of a chord that is a single modifier key (upstream's SingleModifierChord type).</summary>
internal static class SingleModifierChord
{
    public const string Ctrl = "ctrl";
    public const string Shift = "shift";
    public const string Alt = "alt";
    public const string Meta = "meta";
}

/// <summary>A resolved keybinding. Consists of one or multiple chords.</summary>
internal abstract class ResolvedKeybinding
{
    /// <summary>This prints the binding in a format suitable for displaying in the UI.</summary>
    public abstract string? GetLabel();

    /// <summary>This prints the binding in a format suitable for ARIA.</summary>
    public abstract string? GetAriaLabel();

    /// <summary>
    /// This prints the binding in a format suitable for electron's accelerators.
    /// See https://github.com/electron/electron/blob/master/docs/api/accelerator.md
    /// </summary>
    public abstract string? GetElectronAccelerator();

    /// <summary>This prints the binding in a format suitable for user settings.</summary>
    public abstract string? GetUserSettingsLabel();

    /// <summary>Is the user settings label reflecting the label?</summary>
    public abstract bool IsWYSIWYG();

    /// <summary>Does the keybinding consist of more than one chord?</summary>
    public abstract bool HasMultipleChords();

    /// <summary>Returns the chords that comprise of the keybinding.</summary>
    public abstract IReadOnlyList<ResolvedChord> GetChords();

    /// <summary>
    /// Returns the chords as strings useful for dispatching. Returns null for modifier only chords.
    /// Keybinding "Shift" gives null; keybinding ("D" with shift == true) gives "shift+D".
    /// </summary>
    public abstract IReadOnlyList<string?> GetDispatchChords();

    /// <summary>
    /// Returns the modifier only chords as strings useful for dispatching (one of <see cref="SingleModifierChord"/>).
    /// Returns null for chords that contain more than one modifier or a regular key.
    /// Keybinding "Shift" gives "shift"; keybinding ("D" with shift == true) gives null.
    /// </summary>
    public abstract IReadOnlyList<string?> GetSingleModifierDispatchChords();
}
