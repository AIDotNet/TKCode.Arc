// Ported from vscode (src/vs/platform/keybinding/common/resolvedKeybindingItem.ts, commit b57d5c9e), MIT license.
// Differences from upstream: the when clause is an ContextKeyExpression (ContextKeyExpression.cs); commandArgs is object?;
// toEmptyArrayIfContainsNull treats null and the empty string as upstream's falsy elements.

namespace TKCode.Arc.Editor.Platform;

/// <summary>A keybinding resolved for the keyboard layout, with its command and when clause.</summary>
internal sealed class ResolvedKeybindingItem
{
    public ResolvedKeybindingItem(
        ResolvedKeybinding? resolvedKeybinding,
        string? command,
        object? commandArgs,
        ContextKeyExpression? when,
        bool isDefault,
        string? extensionId,
        bool isBuiltinExtension,
        bool systemWide = false)
    {
        ResolvedKeybinding = resolvedKeybinding;
        Chords = resolvedKeybinding is not null
            ? ResolvedKeybindingItems.ToEmptyArrayIfContainsNull(resolvedKeybinding.GetDispatchChords())
            : [];
        if (resolvedKeybinding is not null && Chords.Count == 0)
        {
            // handle possible single modifier chord keybindings
            Chords = ResolvedKeybindingItems.ToEmptyArrayIfContainsNull(resolvedKeybinding.GetSingleModifierDispatchChords());
        }

        Bubble = command is { Length: > 0 } && command[0] == '^';
        Command = Bubble ? command![1..] : command;
        CommandArgs = commandArgs;
        When = when;
        IsDefault = isDefault;
        ExtensionId = extensionId;
        IsBuiltinExtension = isBuiltinExtension;
        SystemWide = systemWide;
    }

    public ResolvedKeybinding? ResolvedKeybinding { get; }

    /// <summary>The dispatch strings of the chords; empty when the keybinding is unbound or cannot be dispatched.</summary>
    public IReadOnlyList<string> Chords { get; }

    /// <summary>The command was written with a leading '^': the key event goes on to the next handler as well.</summary>
    public bool Bubble { get; }

    public string? Command { get; }

    public object? CommandArgs { get; }

    public ContextKeyExpression? When { get; }

    public bool IsDefault { get; }

    public string? ExtensionId { get; }

    public bool IsBuiltinExtension { get; }

    /// <summary>
    /// Whether this keybinding was declared as a system-wide (OS global) shortcut via `keybindings.json`. Only ever
    /// true for user keybindings; defaults/extension keybindings are always false.
    /// </summary>
    public bool SystemWide { get; }
}

internal static class ResolvedKeybindingItems
{
    /// <summary>The elements, or an empty list when one of them is null (or an empty string).</summary>
    public static IReadOnlyList<T> ToEmptyArrayIfContainsNull<T>(IReadOnlyList<T?> arr)
        where T : class
    {
        var result = new List<T>(arr.Count);
        for (var i = 0; i < arr.Count; i++)
        {
            var element = arr[i];
            if (element is null || element is string { Length: 0 })
            {
                return [];
            }

            result.Add(element);
        }

        return result;
    }
}
