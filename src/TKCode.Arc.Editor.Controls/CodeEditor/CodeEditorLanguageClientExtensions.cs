using TKCode.Arc.Editor.Lsp;

namespace TKCode.Arc;

/// <summary>Connects a <see cref="CodeEditor"/> to a language server through a <see cref="LanguageClient"/>.</summary>
public static class CodeEditorLanguageClientExtensions
{
    /// <summary>
    /// Attaches the editor's document to a started <paramref name="client"/>: the server is told about the document and
    /// its edits, its diagnostics show as squiggles, and its features (completion, hover, signature help, navigation,
    /// rename, code actions, code lenses, formatting, folding, semantic tokens, inlay hints and the rest the server
    /// offers) work in the editor. Dispose the result to detach; the server is then told the document closed.
    /// </summary>
    /// <param name="editor">The editor.</param>
    /// <param name="client">A language client that has been started with <see cref="LanguageClient.StartAsync"/>.</param>
    /// <param name="documentUri">The URI the server knows the document by, usually a <c>file://</c> URI in its workspace.</param>
    /// <param name="languageId">The server's language id; null uses the editor's <see cref="CodeEditor.LanguageId"/> and follows its changes.</param>
    /// <returns>A handle that detaches the document.</returns>
    public static IDisposable AttachLanguageClient(this CodeEditor editor, LanguageClient client, Uri documentUri, string? languageId = null)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(documentUri);
        return client.Attach(editor.Model, editor.LanguageFeatures, editor.MarkerService, documentUri, languageId);
    }
}
