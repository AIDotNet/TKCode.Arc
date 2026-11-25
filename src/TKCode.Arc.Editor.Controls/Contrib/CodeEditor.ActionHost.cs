// What the language actions (code actions, rename, go to definition, peek, code lens) need from outside the editor
// (upstream: the services ICodeEditorService.openCodeEditor, IBulkEditService.apply, ITextModelService.
// createModelReference and ICommandService.executeCommand; commit b57d5c9e, MIT license). An editor has one model and
// no workspace, so it does what concerns its own model itself (edits of its model are one undo step, a location in its
// model is selected and revealed) and hands the rest to callbacks its host sets on ActionHost: edits of other documents,
// opening other documents, the text of other documents for previews, and commands the editor does not know.

using Avalonia.Threading;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib;
using TKCode.Arc.Editor.Contrib.CodeLens;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

/// <summary>A request to show another document (upstream's openCodeEditor input).</summary>
/// <param name="Resource">The document.</param>
/// <param name="Selection">What to select and reveal, or null.</param>
/// <param name="SideBySide">Whether the user asked to open it next to the current editor.</param>
internal sealed record CodeEditorOpenRequest(Uri Resource, TextRange? Selection, bool SideBySide);

/// <summary>The text of a document the host resolved, and its language (null: the editor's language).</summary>
internal sealed record CodeEditorResolvedText(string Text, string? LanguageId = null);

/// <summary>The callbacks through which an editor reaches documents and commands outside it.</summary>
internal sealed class CodeEditorActionHost
{
    /// <summary>
    /// Applies the part of a workspace edit that is not the editor's model (other documents, file operations); returns
    /// whether it was applied. Null: such edits are not applied.
    /// </summary>
    public Func<WorkspaceEdit, Task<bool>>? ApplyWorkspaceEdit { get; set; }

    /// <summary>Shows another document (go to definition into another file); returns whether it did. Null: nothing happens.</summary>
    public Func<CodeEditorOpenRequest, Task<bool>>? OpenCodeEditor { get; set; }

    /// <summary>The text of another document, for peek previews and reference lists; null when unknown.</summary>
    public Func<Uri, CancellationToken, Task<CodeEditorResolvedText?>>? ResolveText { get; set; }

    /// <summary>Runs a command of a code lens or code action that is not registered in the editor.</summary>
    public Func<LanguageCommand, Task>? ExecuteCommand { get; set; }

    /// <summary>The last code lenses of models (upstream's ICodeLensCache); a host may give several editors one cache.</summary>
    public CodeLensCache CodeLensCache { get; set; } = new();
}

public sealed partial class CodeEditor
{
    /// <summary>The host's callbacks for documents and commands outside the editor.</summary>
    internal CodeEditorActionHost ActionHost { get; } = new();

    /// <summary>
    /// Applies a workspace edit (upstream's IBulkEditService.apply): the edits of the editor's model as one undo step, the
    /// rest through <see cref="CodeEditorActionHost.ApplyWorkspaceEdit"/>. Returns whether everything was applied; null
    /// error when it was.
    /// </summary>
    internal async Task<(bool Applied, string? Error)> ApplyWorkspaceEdit(WorkspaceEdit edit, string? label = null)
    {
        var split = WorkspaceEdits.Split(edit, Model);
        if (split.Error is { } error)
        {
            return (false, error);
        }

        if (split.ModelEdits.Count > 0)
        {
            if (IsReadOnly)
            {
                return (false, "Cannot edit in read-only editor");
            }

            PushUndoStop();
            var applied = ExecuteEdits(label ?? "workspaceEdit", split.ToEditOperations());
            PushUndoStop();
            if (!applied)
            {
                return (false, "Cannot edit in read-only editor");
            }
        }

        if (split.Other is { } other)
        {
            if (ActionHost.ApplyWorkspaceEdit is not { } apply || !await apply(other).ConfigureAwait(true))
            {
                return (false, "Some edits were not applied: they change other documents.");
            }
        }

        return (true, null);
    }

    /// <summary>
    /// Shows a location (upstream's openCodeEditor with a selection): in the editor's model it collapses the selection to
    /// the start of the range and reveals it, elsewhere it asks the host. Returns whether the location was shown here.
    /// </summary>
    internal async Task<bool> OpenLocation(Uri resource, TextRange range, bool sideBySide = false, bool focus = true)
    {
        if (!sideBySide && WorkspaceEdits.IsSameResource(resource, Model.Uri))
        {
            var start = Model.ValidatePosition(range.Start);
            SetSelection(new TextSelection(start, start), "api");
            RevealRange(Model.ValidateRange(range), VerticalRevealType.CenterIfOutsideViewport, ScrollType.Immediate);
            if (focus)
            {
                Focus();
            }

            return true;
        }

        if (ActionHost.OpenCodeEditor is { } open)
        {
            await open(new CodeEditorOpenRequest(resource, range, sideBySide)).ConfigureAwait(true);
        }

        return false;
    }

    /// <summary>Runs a provider's command: through the editor's commands when it has one of that id, else through the host.</summary>
    internal async Task RunLanguageCommand(LanguageCommand command)
    {
        if (CommandsRegistry.GetCommand(command.Id) is not null)
        {
            await CommandService.ExecuteCommand(command.Id, [.. command.Arguments ?? []]).ConfigureAwait(true);
            return;
        }

        if (ActionHost.ExecuteCommand is { } execute)
        {
            await execute(command).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// The model of a document for previews: the editor's own (not owned by the caller) or a new model of the text the
    /// host resolves (owned by the caller, who disposes it); null when the text is unknown.
    /// </summary>
    internal async Task<(TextModel Model, bool Owned)?> ResolveTextModel(Uri resource, CancellationToken token = default)
    {
        if (WorkspaceEdits.IsSameResource(resource, Model.Uri))
        {
            return (Model, false);
        }

        if (ActionHost.ResolveText is not { } resolve || await resolve(resource, token).ConfigureAwait(true) is not { } resolved)
        {
            return null;
        }

        Dispatcher.UIThread.VerifyAccess();
        var languageId = resolved.LanguageId ?? Model.GetLanguageId();
        LanguageIdCodec.Register(languageId);
        var model = new TextModel(
            resolved.Text, languageId, TextModel.DefaultCreationOptions, new UndoRedoService(new NoDialogs(), new NoNotifications()),
            resource, LanguageIdCodec, TokenizationRegistry, IdleScheduler.None);
        return (model, true);
    }
}
