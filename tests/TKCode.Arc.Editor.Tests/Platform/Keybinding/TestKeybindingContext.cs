// Ported from vscode (src/vs/platform/keybinding/test/common/keybindingResolver.test.ts: createContext, commit
// b57d5c9e), MIT license. A context backed by a dictionary.

using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc.Editor.Tests;

internal sealed class TestKeybindingContext(IReadOnlyDictionary<string, object?> values) : IContext
{
    public static TestKeybindingContext Empty { get; } = new(new Dictionary<string, object?>());

    public object? GetValue(string key) => values.TryGetValue(key, out var value) ? value : null;
}
