// Ported from vscode (src/vs/platform/keybinding/common/baseResolvedKeybinding.ts, commit b57d5c9e), MIT license.
// Differences from upstream: protected members drop the leading underscore (_os -> Os, _getLabel -> GetLabel(T), ...);
// illegalArgument is an ArgumentException.

namespace TKCode.Arc.Editor.Platform;

internal abstract class BaseResolvedKeybinding<T> : ResolvedKeybinding
    where T : Chord
{
    protected BaseResolvedKeybinding(OperatingSystemKind os, IReadOnlyList<T> chords)
    {
        if (chords.Count == 0)
        {
            throw new ArgumentException("Illegal argument: chords", nameof(chords));
        }

        Os = os;
        Chords = chords;
    }

    protected OperatingSystemKind Os { get; }

    protected IReadOnlyList<T> Chords { get; }

    public override string? GetLabel() => KeybindingLabels.UILabelProvider.ToLabel(Os, Chords, GetLabel);

    public override string? GetAriaLabel() => KeybindingLabels.AriaLabelProvider.ToLabel(Os, Chords, GetAriaLabel);

    public override string? GetElectronAccelerator()
    {
        if (Chords.Count > 1)
        {
            // [Electron Accelerators] Electron cannot handle chords
            return null;
        }

        if (Chords[0].IsDuplicateModifierCase())
        {
            // [Electron Accelerators] Electron cannot handle modifier only keybindings
            // e.g. "shift shift"
            return null;
        }

        return KeybindingLabels.ElectronAcceleratorLabelProvider.ToLabel(Os, Chords, GetElectronAccelerator);
    }

    public override string? GetUserSettingsLabel() => KeybindingLabels.UserSettingsLabelProvider.ToLabel(Os, Chords, GetUserSettingsLabel);

    public override bool IsWYSIWYG()
    {
        foreach (var chord in Chords)
        {
            if (!IsWYSIWYG(chord))
            {
                return false;
            }
        }

        return true;
    }

    public override bool HasMultipleChords() => Chords.Count > 1;

    public override IReadOnlyList<ResolvedChord> GetChords()
    {
        var result = new ResolvedChord[Chords.Count];
        for (var i = 0; i < Chords.Count; i++)
        {
            result[i] = GetChord(Chords[i]);
        }

        return result;
    }

    public override IReadOnlyList<string?> GetDispatchChords()
    {
        var result = new string?[Chords.Count];
        for (var i = 0; i < Chords.Count; i++)
        {
            result[i] = GetChordDispatch(Chords[i]);
        }

        return result;
    }

    public override IReadOnlyList<string?> GetSingleModifierDispatchChords()
    {
        var result = new string?[Chords.Count];
        for (var i = 0; i < Chords.Count; i++)
        {
            result[i] = GetSingleModifierChordDispatch(Chords[i]);
        }

        return result;
    }

    private ResolvedChord GetChord(T keybinding)
    {
        return new ResolvedChord(
            keybinding.CtrlKey,
            keybinding.ShiftKey,
            keybinding.AltKey,
            keybinding.MetaKey,
            GetLabel(keybinding),
            GetAriaLabel(keybinding));
    }

    protected abstract string? GetLabel(T keybinding);

    protected abstract string? GetAriaLabel(T keybinding);

    protected abstract string? GetElectronAccelerator(T keybinding);

    protected abstract string? GetUserSettingsLabel(T keybinding);

    protected abstract bool IsWYSIWYG(T keybinding);

    protected abstract string? GetChordDispatch(T keybinding);

    protected abstract string? GetSingleModifierChordDispatch(T keybinding);
}
