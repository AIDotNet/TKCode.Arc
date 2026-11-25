// Ported from vscode (src/vs/platform/keybinding/test/common/keybindingsTestUtils.ts, commit b57d5c9e), MIT license.
// Differences from upstream: one overload for the number form and one for the array form of the keybinding.

using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc.Editor.Tests;

internal static class KeybindingsTestUtils
{
    public static ResolvedKeybinding? CreateUSLayoutResolvedKeybinding(int encodedKeybinding, OperatingSystemKind os)
    {
        if (encodedKeybinding == 0)
        {
            return null;
        }

        return Resolve(Keybindings.DecodeKeybinding(encodedKeybinding, os), os);
    }

    public static ResolvedKeybinding? CreateUSLayoutResolvedKeybinding(int[] encodedKeybinding, OperatingSystemKind os)
    {
        return Resolve(Keybindings.DecodeKeybinding(encodedKeybinding, os), os);
    }

    private static ResolvedKeybinding? Resolve(Keybinding? keybinding, OperatingSystemKind os)
    {
        if (keybinding is null)
        {
            return null;
        }

        var result = USLayoutResolvedKeybinding.ResolveKeybinding(keybinding, os);
        if (result.Count > 0)
        {
            return result[0];
        }

        return null;
    }
}
