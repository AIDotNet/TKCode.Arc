// Ported from vscode (src/vs/editor/contrib/editorState/browser/editorState.ts: TextModelCancellationTokenSource,
// commit b57d5c9e), MIT license.
// Differences from upstream: a wrapper around a linked System.Threading.CancellationTokenSource; the editor variant
// (EditorStateCancellationTokenSource) is in the controls package, since it needs the editor.

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib;

/// <summary>A cancellation source that cancels when the model's content changes or the parent token is cancelled.</summary>
internal sealed class TextModelCancellationTokenSource : IDisposable
{
    private readonly CancellationTokenSource _source;
    private readonly TextModel _model;
    private bool _disposed;

    public TextModelCancellationTokenSource(TextModel model, CancellationToken parent = default)
    {
        _model = model;
        _source = CancellationTokenSource.CreateLinkedTokenSource(parent);
        _model.DidChangeContent += OnContentChanged;
    }

    public CancellationToken Token => _source.Token;

    public void Cancel()
    {
        if (!_disposed)
        {
            _source.Cancel();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _model.DidChangeContent -= OnContentChanged;
        _source.Dispose();
    }

    private void OnContentChanged(ModelContentChangedEvent e) => Cancel();
}
