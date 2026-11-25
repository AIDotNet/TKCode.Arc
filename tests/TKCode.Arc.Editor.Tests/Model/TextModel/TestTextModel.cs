// Ported from vscode (src/vs/editor/test/common/testTextModel.ts: createTextModel and withEditorModel, commit b57d5c9e),
// MIT license. Upstream builds the model through its service container; here the model needs only an undo-redo
// service, whose dialogs answer "all" and "yes".

using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc.Editor.Tests;

internal static class TestTextModel
{
    /// <summary>The language id upstream uses when none is given.</summary>
    internal const string PlaintextLanguageId = "plaintext";

    /// <summary>Upstream's createTextModel: the default creation options unless others are given.</summary>
    internal static TextModel Create(
        string text,
        string? languageId = null,
        TextModelCreationOptions? options = null,
        Uri? uri = null) =>
        new(text, languageId ?? PlaintextLanguageId, options ?? TextModel.DefaultCreationOptions, CreateUndoRedoService(), uri);

    /// <summary>Upstream's createTextModel with a text buffer factory.</summary>
    internal static TextModel Create(
        PieceTreeTextBufferFactory factory,
        string? languageId = null,
        TextModelCreationOptions? options = null,
        Uri? uri = null) =>
        new(factory, languageId ?? PlaintextLanguageId, options ?? TextModel.DefaultCreationOptions, CreateUndoRedoService(), uri);

    /// <summary>Upstream's withEditorModel: a model of the lines joined with \n, disposed after the callback.</summary>
    internal static void WithEditorModel(string[] text, Action<TextModel> callback)
    {
        using var model = Create(string.Join('\n', text));
        callback(model);
    }

    internal static UndoRedoService CreateUndoRedoService() => new(new DialogService(), new NotificationService());

    private sealed class DialogService : IUndoRedoDialogService
    {
        public ValueTask<UndoChoice> PromptAsync(UndoRedoPromptOptions options) => ValueTask.FromResult(UndoChoice.All);

        public ValueTask<bool> ConfirmAsync(UndoRedoConfirmOptions options) => ValueTask.FromResult(true);
    }

    private sealed class NotificationService : IUndoRedoNotificationService
    {
        public void Warn(string message)
        {
        }

        public void Error(Exception error)
        {
        }
    }
}
