// Ported from vscode (src/vs/editor/contrib/rename/browser/rename.ts: RenameController, RenameAction and the
// acceptRenameInput / cancelRenameInput commands, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - edits are applied through CodeEditor.ApplyWorkspaceEdit (the model's edits as one undo step, other documents
//    through the host); a rejection or failure shows as a message at the position (upstream: a notification);
//  - no new symbol name providers, no preview (acceptRenameInputWithPreview and the rename suggestion commands are not
//    ported), no progress, logging or telemetry; RenameAction's (uri, position) arguments are not ported.

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Rename;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc;

/// <summary>Renames the symbol at the cursor: F2 opens a box over it, Enter applies the providers' edits (upstream's RenameController).</summary>
internal sealed class RenameController : ICodeEditorContribution
{
    public const string Id = "editor.contrib.renameController";

    public static readonly EditorContributionDescription Description = new(Id, editor => new RenameController(editor), EditorContributionInstantiation.Lazy);

    /// <summary>Rename Symbol (F2).</summary>
    public static readonly EditorAction RenameAction = EditorAction.Create(
        "editor.action.rename", "Rename Symbol",
        ContextKeyExpr.And(EditorContextKeys.Writable, EditorContextKeys.HasRenameProvider),
        static (editor, _) => Get(editor)?.Run() ?? Task.CompletedTask,
        new EditorKeybinding((int)KeyCode.F2) { When = EditorContextKeys.EditorTextFocus });

    /// <summary>Enter in the rename box.</summary>
    public static readonly EditorCommand AcceptRenameInputCommand = EditorCommand.BindToContribution<RenameController>(
        Id, "acceptRenameInput", CodeEditorContextKeys.RenameInputVisible, static (controller, _) => controller.AcceptRenameInput(),
        new EditorKeybinding((int)KeyCode.Enter) { Weight = KeybindingWeight.EditorContrib + 99, When = EditorContextKeys.Focus });

    /// <summary>Escape in the rename box.</summary>
    public static readonly EditorCommand CancelRenameInputCommand = EditorCommand.BindToContribution<RenameController>(
        Id, "cancelRenameInput", CodeEditorContextKeys.RenameInputVisible, static (controller, _) => controller.CancelRenameInput(),
        new EditorKeybinding((int)KeyCode.Escape)
        {
            Secondary = [KeyMod.Shift | (int)KeyCode.Escape],
            Weight = KeybindingWeight.EditorContrib + 99,
            When = EditorContextKeys.Focus,
        });

    private readonly CodeEditor _editor;
    private readonly RenameWidget _renameWidget;
    private CancellationTokenSource _cts = new();

    private RenameController(CodeEditor editor)
    {
        _editor = editor;
        _renameWidget = new RenameWidget(editor);
    }

    public static RenameController? Get(CodeEditor editor) => editor.GetContribution<RenameController>(Id);

    /// <summary>The rename box (for tests).</summary>
    internal RenameWidget Widget => _renameWidget;

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _renameWidget.Dispose();
    }

    /// <summary>Resolves the location, asks for the new name, then applies the rename's edits (upstream's run).</summary>
    public async Task Run()
    {
        // a new rename cancels the one before (the parent of the resolve and rename tokens)
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();

        if (_editor.GetPosition() is not { } position)
        {
            return;
        }

        var model = _editor.Model;
        var skeleton = new RenameSkeleton(model, position, _editor.LanguageFeatures.RenameProvider, _editor.LanguageConfigurationService);
        if (!skeleton.HasProvider())
        {
            return;
        }

        // part 1 - resolve the rename location
        RenameLocationResult? loc;
        using (var cts1 = new EditorStateCancellationTokenSource(_editor, CodeEditorStateFlag.Position | CodeEditorStateFlag.Value, null, _cts.Token))
        {
            try
            {
                loc = await skeleton.ResolveRenameLocation(cts1.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception error)
            {
                MessageController.Get(_editor)?.ShowMessage(
                    string.IsNullOrEmpty(error.Message) ? "An unknown error occurred while resolving rename location" : error.Message, position);
                return;
            }

            if (loc is null)
            {
                return;
            }

            if (loc.RejectReason is { } rejectReason)
            {
                MessageController.Get(_editor)?.ShowMessage(rejectReason, position);
                return;
            }

            if (cts1.Token.IsCancellationRequested)
            {
                return;
            }
        }

        // part 2 - the new name
        using var cts2 = new EditorStateCancellationTokenSource(_editor, CodeEditorStateFlag.Position | CodeEditorStateFlag.Value, loc.Range, _cts.Token);
        var inputFieldResult = await _renameWidget.GetInput(loc.Range, loc.Text, cts2.Token).ConfigureAwait(true);
        if (inputFieldResult.NewName is not { } newName)
        {
            if (inputFieldResult.FocusEditor)
            {
                _editor.Focus();
            }

            return;
        }

        _editor.Focus();

        // part 3 - the edits
        RenameEdits renameResult;
        try
        {
            renameResult = await skeleton.ProvideRenameEdits(newName, cts2.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            MessageController.Get(_editor)?.ShowMessage("Rename failed to compute edits", position);
            return;
        }

        if (cts2.Token.IsCancellationRequested)
        {
            return;
        }

        if (renameResult.RejectReason is { Length: > 0 } reason)
        {
            MessageController.Get(_editor)?.ShowMessage(reason, position);
            return;
        }

        if (_editor.GetSelection() is { } selection)
        {
            _editor.SetSelection(new TextSelection(selection.Position, selection.Position));
        }

        var (applied, applyError) = await _editor.ApplyWorkspaceEdit(renameResult.Edit, $"Renaming '{loc.Text}' to '{newName}'").ConfigureAwait(true);
        if (!applied)
        {
            MessageController.Get(_editor)?.ShowMessage(applyError ?? "Rename failed to apply edits", position);
        }
    }

    public void AcceptRenameInput() => _renameWidget.AcceptInput();

    public void CancelRenameInput() => _renameWidget.CancelInput(focusEditor: true);
}
