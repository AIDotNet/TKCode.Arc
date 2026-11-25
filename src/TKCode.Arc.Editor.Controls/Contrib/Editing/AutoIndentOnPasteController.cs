// Ported from vscode (src/vs/editor/contrib/indentation/browser/indentation.ts: AutoIndentOnPaste's update, commit
// b57d5c9e), MIT license. The trigger itself is the engine's AutoIndentOnPaste.Trigger.
// Differences from upstream: the options are the editor's EditingOptions (autoIndentOnPaste, off by default as
// upstream) and its cursor options (autoIndent); there is no model swap.

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Contrib;
using AutoIndentOnPasteImpl = TKCode.Arc.Editor.Contrib.Indentation.AutoIndentOnPaste;

namespace TKCode.Arc;

/// <summary>Re-indents pasted lines when <see cref="CodeEditorEditingOptions.AutoIndentOnPaste"/> is on (upstream's AutoIndentOnPaste).</summary>
internal sealed class AutoIndentOnPasteController : ICodeEditorContribution
{
    public const string Id = "editor.contrib.autoIndentOnPaste";

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new AutoIndentOnPasteController(editor), EditorContributionInstantiation.BeforeFirstInteraction);

    private readonly CodeEditor _editor;
    private bool _listening;

    private AutoIndentOnPasteController(CodeEditor editor)
    {
        _editor = editor;
        editor.DidChangeEditingOptions += OnOptionsChanged;
        editor.DidChangeViewModel += OnViewModelChanged;
        Update();
    }

    private void OnOptionsChanged(CodeEditorEditingOptions options) => Update();

    private void OnViewModelChanged(Editor.ViewModel.ViewModel viewModel) => Update();

    private void Update()
    {
        // clean up
        StopListening();

        // we are disabled
        if (!_editor.EditingOptions.AutoIndentOnPaste || ((IEditorActionHost)_editor).GetOptions().AutoIndent < EditorAutoIndentStrategy.Full)
        {
            return;
        }

        _listening = true;
        _editor.DidPaste += OnPaste;
    }

    private void OnPaste(TextRange range) =>
        AutoIndentOnPasteImpl.Trigger(_editor, range, _editor.EditingOptions.AutoIndentOnPasteWithinString);

    private void StopListening()
    {
        if (_listening)
        {
            _listening = false;
            _editor.DidPaste -= OnPaste;
        }
    }

    public void Dispose()
    {
        StopListening();
        _editor.DidChangeEditingOptions -= OnOptionsChanged;
        _editor.DidChangeViewModel -= OnViewModelChanged;
    }
}
