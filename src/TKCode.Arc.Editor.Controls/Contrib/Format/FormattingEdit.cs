// Ported from vscode (src/vs/editor/contrib/format/browser/formattingEdit.ts, and the editor path of format.ts:
// formatDocumentWithProvider / formatDocumentRangesWithProvider with EditorStateCancellationTokenSource
// (contrib/editorState/browser/editorState.ts) and StableEditorScrollState (browser/stableEditorScroll.ts); commit
// b57d5c9e), MIT license.
// Differences:
//  - the edits come from the engine (TKCode.Arc.Editor.Contrib.Format.Formatting), which computes the minimal edits on
//    the model; this file applies them through the editor;
//  - the editor state that cancels a request is the model's content and the cursor position (upstream's
//    CodeEditorStateFlag.Value | Position), watched through the editor's events;
//  - StableEditorScrollState keeps only what restoreRelativeVerticalPositionOfCursor needs (the cursor's line).

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Format;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Services;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

/// <summary>Applies formatting edits to an editor (upstream's FormattingEdit).</summary>
internal static class FormattingEdit
{
    /// <summary>Applies <paramref name="edits"/> as one undo step (with <paramref name="addUndoStops"/>) and keeps the cursor where it was on screen.</summary>
    public static void Execute(CodeEditor editor, IReadOnlyList<TextEdit> edits, bool addUndoStops)
    {
        if (addUndoStops)
        {
            editor.PushUndoStop();
        }

        var cursorLine = editor.GetPosition()?.LineNumber;
        var cursorTop = cursorLine is { } before ? editor.GetTopForLineNumber(before) : 0;

        var singleEdits = HandleEolEdits(editor, edits);
        if (singleEdits.Count == 1 && IsFullModelReplaceEdit(editor, singleEdits[0]))
        {
            // We use replace semantics and hope that markers stay put...
            editor.ExecuteEdits("formatEditsCommand", [.. singleEdits.Select(static edit => EditOperation.Replace(edit.Range, edit.Text))]);
        }
        else
        {
            editor.ExecuteEdits("formatEditsCommand", [.. singleEdits.Select(static edit => EditOperation.ReplaceMove(edit.Range, edit.Text))]);
        }

        if (addUndoStops)
        {
            editor.PushUndoStop();
        }

        // StableEditorScrollState.restoreRelativeVerticalPositionOfCursor
        if (cursorLine is not null && editor.GetPosition() is { } after && editor.ViewModel is { } viewModel)
        {
            var offset = editor.GetTopForLineNumber(after.LineNumber) - cursorTop;
            var scrollTop = viewModel.ViewLayout.GetCurrentScrollTop();
            viewModel.ViewLayout.SetScrollPosition(new NewScrollPosition(ScrollTop: scrollTop + offset), ScrollType.Immediate);
        }
    }

    private static List<TextEdit> HandleEolEdits(CodeEditor editor, IReadOnlyList<TextEdit> edits)
    {
        EndOfLineSequence? newEol = null;
        var singleEdits = new List<TextEdit>();
        foreach (var edit in edits)
        {
            if (edit.Eol is { } eol)
            {
                newEol = eol;
            }

            if (EditorWorker.HasRange(edit))
            {
                singleEdits.Add(edit);
            }
        }

        if (newEol is { } value)
        {
            editor.Model.PushEOL(value);
        }

        return singleEdits;
    }

    private static bool IsFullModelReplaceEdit(CodeEditor editor, TextEdit edit) =>
        editor.Model.GetFullModelRange() == editor.Model.ValidateRange(edit.Range);
}

/// <summary>Formatting through an editor (upstream's format.ts, editor path).</summary>
internal static class EditorFormatting
{
    /// <summary>
    /// Formats the editor's document with the formatter <paramref name="selector"/> picks (Monaco: the first), as one
    /// undo step; an edit or a cursor move while the formatter runs cancels it (upstream's
    /// formatDocumentWithSelectedProvider).
    /// </summary>
    public static async Task<bool> FormatDocumentWithSelectedProvider(
        CodeEditor editor,
        FormattingMode mode,
        CancellationToken token,
        FormattingEditProviderSelector<IDocumentFormattingEditProvider>? selector = null)
    {
        var model = editor.Model;
        var features = editor.LanguageFeatures;
        var provider = Formatting.GetRealAndSyntheticDocumentFormattersOrdered(features.DocumentFormattingEditProvider, features.DocumentRangeFormattingEditProvider, model);
        var selected = await FormattingConflicts.Select(provider, model, mode, FormattingKind.File, selector).ConfigureAwait(true);
        if (selected is null)
        {
            return false;
        }

        using var cts = new EditorStateCancellationTokenSource(editor, token);
        var edits = await Formatting.ComputeDocumentFormattingEdits(selected, model, cts.Token).ConfigureAwait(true);
        if (cts.Token.IsCancellationRequested || edits is null || edits.Count == 0)
        {
            return false;
        }

        FormattingEdit.Execute(editor, edits, mode != FormattingMode.Silent);
        if (mode != FormattingMode.Silent && editor.GetPosition() is { } position)
        {
            editor.RevealRange(new TextRange(position), VerticalRevealType.CenterIfOutsideViewport, ScrollType.Immediate);
        }

        return true;
    }

    /// <summary>Formats ranges of the editor's document with its range formatter, as one undo step (upstream's formatDocumentRangesWithSelectedProvider).</summary>
    public static async Task<bool> FormatDocumentRangesWithSelectedProvider(
        CodeEditor editor,
        IReadOnlyList<TextRange> ranges,
        FormattingMode mode,
        CancellationToken token,
        FormattingEditProviderSelector<IDocumentRangeFormattingEditProvider>? selector = null)
    {
        var model = editor.Model;
        var provider = editor.LanguageFeatures.DocumentRangeFormattingEditProvider.Ordered(model);
        var selected = await FormattingConflicts.Select(provider, model, mode, FormattingKind.Selection, selector).ConfigureAwait(true);
        if (selected is null)
        {
            return false;
        }

        using var cts = new EditorStateCancellationTokenSource(editor, token);
        var edits = await Formatting.ComputeDocumentRangesFormattingEdits(selected, model, ranges, cts.Token).ConfigureAwait(true);
        if (cts.Token.IsCancellationRequested || edits is null || edits.Count == 0)
        {
            return false;
        }

        FormattingEdit.Execute(editor, edits, true);
        if (editor.GetPosition() is { } position)
        {
            editor.RevealRange(new TextRange(position), VerticalRevealType.CenterIfOutsideViewport, ScrollType.Immediate);
        }

        return true;
    }

    /// <summary>Cancels when the editor's text or cursor position changes (upstream's EditorStateCancellationTokenSource with Value | Position).</summary>
    private sealed class EditorStateCancellationTokenSource : IDisposable
    {
        private readonly CodeEditor _editor;
        private readonly CancellationTokenSource _source;
        private readonly TextPosition? _position;

        public EditorStateCancellationTokenSource(CodeEditor editor, CancellationToken parent)
        {
            _editor = editor;
            _source = CancellationTokenSource.CreateLinkedTokenSource(parent);
            _position = editor.GetPosition();
            editor.DidChangeModelContent += OnContent;
            editor.DidChangeCursorSelection += OnCursor;
        }

        public CancellationToken Token => _source.Token;

        public void Dispose()
        {
            _editor.DidChangeModelContent -= OnContent;
            _editor.DidChangeCursorSelection -= OnCursor;
            _source.Dispose();
        }

        private void OnContent(Editor.Model.ModelContentChangedEvent e) => _source.Cancel();

        private void OnCursor(CursorStateChangedEvent e)
        {
            if (_editor.GetPosition() != _position)
            {
                _source.Cancel();
            }
        }
    }
}
