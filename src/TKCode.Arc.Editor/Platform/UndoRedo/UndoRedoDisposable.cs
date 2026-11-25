// Ported from vscode (src/vs/base/common/lifecycle.ts Disposable.None, commit b57d5c9e), MIT license.

namespace TKCode.Arc.Editor.Platform;

/// <summary>A no-op <see cref="IDisposable"/>, the equivalent of vscode's <c>Disposable.None</c>.</summary>
internal sealed class UndoRedoDisposable : IDisposable
{
    public static readonly UndoRedoDisposable None = new();

    private UndoRedoDisposable()
    {
    }

    public void Dispose()
    {
    }
}
