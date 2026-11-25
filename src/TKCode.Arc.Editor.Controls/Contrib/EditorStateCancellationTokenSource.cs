// Ported from vscode (src/vs/editor/contrib/editorState/browser/editorState.ts: CodeEditorStateFlag, EditorState and
// EditorStateCancellationTokenSource, commit b57d5c9e), MIT license.
// Differences from upstream: a linked System.Threading.CancellationTokenSource; the model flag is left out (an editor's
// model never changes); the value is the model's version id; the scroll state is the view layout's scroll position.

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

/// <summary>What an editor state checks (upstream's CodeEditorStateFlag).</summary>
[Flags]
internal enum CodeEditorStateFlag
{
    Value = 1,
    Selection = 2,
    Position = 4,
    Scroll = 8,
}

/// <summary>A snapshot of what an editor shows, to tell whether it changed (upstream's EditorState).</summary>
internal sealed class EditorState
{
    private readonly CodeEditorStateFlag _flags;
    private readonly int _modelVersionId;
    private readonly TextPosition? _position;
    private readonly TextSelection? _selection;
    private readonly (double Left, double Top) _scroll;

    public EditorState(CodeEditor editor, CodeEditorStateFlag flags)
    {
        _flags = flags;
        _modelVersionId = editor.Model.GetVersionId();
        _position = editor.GetPosition();
        _selection = editor.GetSelection();
        _scroll = Scroll(editor);
    }

    /// <summary>Whether the editor still shows what it did when the state was taken.</summary>
    public bool Validate(CodeEditor editor)
    {
        if (_flags.HasFlag(CodeEditorStateFlag.Value) && editor.Model.GetVersionId() != _modelVersionId)
        {
            return false;
        }

        if (_flags.HasFlag(CodeEditorStateFlag.Position) && editor.GetPosition() != _position)
        {
            return false;
        }

        if (_flags.HasFlag(CodeEditorStateFlag.Selection) && editor.GetSelection() != _selection)
        {
            return false;
        }

        if (_flags.HasFlag(CodeEditorStateFlag.Scroll) && Scroll(editor) != _scroll)
        {
            return false;
        }

        return true;
    }

    private static (double, double) Scroll(CodeEditor editor) =>
        editor.ViewModel is { } viewModel
            ? (viewModel.ViewLayout.GetCurrentScrollLeft(), viewModel.ViewLayout.GetCurrentScrollTop())
            : (0, 0);
}

/// <summary>
/// A cancellation source that cancels when the editor's text, cursor position or selection change, or the cursor leaves
/// a range (upstream's EditorStateCancellationTokenSource).
/// </summary>
internal sealed class EditorStateCancellationTokenSource : IDisposable
{
    private readonly CodeEditor _editor;
    private readonly CodeEditorStateFlag _flags;
    private readonly TextRange? _range;
    private readonly CancellationTokenSource _source;
    private bool _disposed;

    public EditorStateCancellationTokenSource(CodeEditor editor, CodeEditorStateFlag flags, TextRange? range = null, CancellationToken parent = default)
    {
        _editor = editor;
        _flags = flags;
        _range = range;
        _source = CancellationTokenSource.CreateLinkedTokenSource(parent);
        if (flags.HasFlag(CodeEditorStateFlag.Position) || flags.HasFlag(CodeEditorStateFlag.Selection))
        {
            editor.DidChangeCursorSelection += OnCursorChanged;
        }

        if (flags.HasFlag(CodeEditorStateFlag.Value))
        {
            editor.DidChangeModelContent += OnContentChanged;
        }

        if (flags.HasFlag(CodeEditorStateFlag.Scroll))
        {
            editor.DidScrollChange += OnScrollChanged;
        }
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
        _editor.DidChangeCursorSelection -= OnCursorChanged;
        _editor.DidChangeModelContent -= OnContentChanged;
        _editor.DidScrollChange -= OnScrollChanged;
        _source.Dispose();
    }

    private void OnCursorChanged(CursorStateChangedEvent e)
    {
        if (_range is { } range && _editor.GetPosition() is { } position && range.ContainsPosition(position))
        {
            return;
        }

        Cancel();
    }

    private void OnContentChanged(ModelContentChangedEvent e) => Cancel();

    private void OnScrollChanged(ScrollChangedEvent e) => Cancel();
}
