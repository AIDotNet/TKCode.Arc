// The messages a language server sends the client: diagnostics, window messages, workspace edits, configuration,
// registrations and refresh requests (specification 3.17).

using System.Text.Json;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Lsp.JsonRpc;
using TKCode.Arc.Editor.Lsp.Protocol;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform.Markers;

namespace TKCode.Arc.Editor.Lsp;

/// <summary>A diagnostic the server published: the marker it shows and the protocol value code actions send back.</summary>
internal sealed record LspPublishedDiagnostic(MarkerData Marker, JsonElement Raw);

/// <summary>The diagnostics a server last published for one document.</summary>
internal sealed record LspPublishedDiagnostics(string Uri, int? Version, IReadOnlyList<LspPublishedDiagnostic> Items);

public sealed partial class LanguageClient
{
    private void RegisterHandlers(JsonRpcConnection connection)
    {
        connection.OnNotification("textDocument/publishDiagnostics", OnPublishDiagnostics);
        connection.OnNotification("window/showMessage", parameters => OnMessage(parameters, show: true));
        connection.OnNotification("window/logMessage", parameters => OnMessage(parameters, show: false));
        connection.OnRequest("window/showMessageRequest", OnShowMessageRequest);
        connection.OnRequest("workspace/applyEdit", OnApplyEdit);
        connection.OnRequest("workspace/configuration", OnConfiguration);
        connection.OnRequest("workspace/workspaceFolders", OnWorkspaceFolders);
        connection.OnRequest("client/registerCapability", static (_, _) => Task.FromResult<JsonValueWriter?>(null));
        connection.OnRequest("client/unregisterCapability", static (_, _) => Task.FromResult<JsonValueWriter?>(null));
        connection.OnRequest("window/workDoneProgress/create", static (_, _) => Task.FromResult<JsonValueWriter?>(null));
        connection.OnRequest("workspace/semanticTokens/refresh", (_, _) => Refresh(static registration => registration.Providers.RefreshSemanticTokens()));
        connection.OnRequest("workspace/inlayHint/refresh", (_, _) => Refresh(static registration => registration.Providers.RefreshInlayHints()));
        connection.OnRequest("workspace/codeLens/refresh", (_, _) => Refresh(static registration => registration.Providers.RefreshCodeLenses()));
        connection.OnRequest("workspace/foldingRange/refresh", (_, _) => Refresh(static registration => registration.Providers.RefreshFoldingRanges()));
    }

    private void OnPublishDiagnostics(JsonElement parameters)
    {
        var published = parameters.Deserialize(s_json.PublishDiagnosticsParams);
        if (published is null)
        {
            return;
        }
        var items = new List<LspPublishedDiagnostic>(published.Diagnostics.Length);
        if (parameters.TryGetProperty("diagnostics", out var raw) && raw.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var element in raw.EnumerateArray())
            {
                if (index < published.Diagnostics.Length)
                {
                    items.Add(new LspPublishedDiagnostic(LspConvert.ToMarkerData(published.Diagnostics[index]), element.Clone()));
                }
                index++;
            }
        }
        var diagnostics = new LspPublishedDiagnostics(published.Uri, published.Version, items);
        var key = LspConvert.UriKey(published.Uri);
        if (items.Count == 0)
        {
            _diagnostics.Remove(key);
        }
        else
        {
            _diagnostics[key] = diagnostics;
        }
        if (FindDocument(published.Uri) is { IsOpen: true } document)
        {
            document.ApplyDiagnostics(diagnostics);
        }
    }

    private void OnMessage(JsonElement parameters, bool show)
    {
        var message = parameters.Deserialize(s_json.ShowMessageParams);
        if (message is null)
        {
            return;
        }
        var args = new LanguageClientMessageEventArgs(ToMessageType(message.Type), message.Message);
        (show ? ShowMessage : LogMessage)?.Invoke(this, args);
    }

    private async Task<JsonValueWriter?> OnShowMessageRequest(JsonElement parameters, CancellationToken cancellationToken)
    {
        var request = parameters.Deserialize(s_json.ShowMessageRequestParams);
        if (request is null || _options.ShowMessageRequest is not { } handler)
        {
            return null;
        }
        var titles = request.Actions?.Select(static action => action.Title).ToList() ?? [];
        var chosen = await handler(new LanguageClientMessageEventArgs(ToMessageType(request.Type), request.Message), titles, cancellationToken);
        if (chosen is null)
        {
            return null;
        }
        var item = new MessageActionItem(chosen);
        return writer => JsonSerializer.Serialize(writer, item, s_json.MessageActionItem);
    }

    private Task<JsonValueWriter?> OnApplyEdit(JsonElement parameters, CancellationToken cancellationToken)
    {
        var request = parameters.Deserialize(s_json.ApplyWorkspaceEditParams);
        var result = request is null
            ? new ApplyWorkspaceEditResult(false) { FailureReason = "The edit could not be read." }
            : ApplyWorkspaceEdit(LspConvert.ToWorkspaceEdit(request.Edit));
        return Task.FromResult<JsonValueWriter?>(writer => JsonSerializer.Serialize(writer, result, s_json.ApplyWorkspaceEditResult));
    }

    /// <summary>
    /// Applies a workspace edit to the attached documents, all or nothing: a file operation, a document that is not
    /// attached or a version that does not match fails the whole edit (the client announces failureHandling "abort"
    /// and no resource operations). Each document's edits are one undo step.
    /// </summary>
    internal ApplyWorkspaceEditResult ApplyWorkspaceEdit(WorkspaceEdit edit)
    {
        var byDocument = new List<(LspDocument Document, List<SingleEditOperation> Operations)>();
        for (var i = 0; i < edit.Edits.Count; i++)
        {
            switch (edit.Edits[i])
            {
                case WorkspaceTextEdit textEdit:
                    var document = FindDocument(LspConvert.FromUri(textEdit.Resource));
                    if (document is not { IsOpen: true })
                    {
                        return new ApplyWorkspaceEditResult(false) { FailureReason = $"The document {textEdit.Resource} is not open.", FailedChange = i };
                    }
                    if (textEdit.VersionId is { } version && version != document.Model.GetVersionId())
                    {
                        return new ApplyWorkspaceEditResult(false) { FailureReason = $"The document {textEdit.Resource} has changed (version {document.Model.GetVersionId()}, the edit is for {version}).", FailedChange = i };
                    }
                    var entry = byDocument.FirstOrDefault(pair => pair.Document == document);
                    if (entry.Document is null)
                    {
                        entry = (document, new List<SingleEditOperation>());
                        byDocument.Add(entry);
                    }
                    var model = document.Model;
                    entry.Operations.Add(new SingleEditOperation(model.ValidateRange(textEdit.TextEdit.Range), LspConvert.NormalizeEol(textEdit.TextEdit.Text, model.GetEOL())));
                    break;
                default:
                    return new ApplyWorkspaceEditResult(false) { FailureReason = "File operations are not supported.", FailedChange = i };
            }
        }
        foreach (var (document, operations) in byDocument)
        {
            var model = document.Model;
            model.PushStackElement();
            model.PushEditOperations(null, operations, null);
            model.PushStackElement();
        }
        return new ApplyWorkspaceEditResult(true);
    }

    private static Task<JsonValueWriter?> OnConfiguration(JsonElement parameters, CancellationToken cancellationToken)
    {
        // No settings: one null per item, which servers read as "use your defaults".
        var count = parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array
            ? items.GetArrayLength()
            : 0;
        return Task.FromResult<JsonValueWriter?>(writer =>
        {
            writer.WriteStartArray();
            for (var i = 0; i < count; i++)
            {
                writer.WriteNullValue();
            }
            writer.WriteEndArray();
        });
    }

    private Task<JsonValueWriter?> OnWorkspaceFolders(JsonElement parameters, CancellationToken cancellationToken)
    {
        if (_options.RootUri is not { } root)
        {
            return Task.FromResult<JsonValueWriter?>(null);
        }
        var folder = new WorkspaceFolder(LspConvert.FromUri(root), WorkspaceName(root));
        return Task.FromResult<JsonValueWriter?>(writer =>
        {
            writer.WriteStartArray();
            writer.WriteStartObject();
            writer.WriteString("uri", folder.Uri);
            writer.WriteString("name", folder.Name);
            writer.WriteEndObject();
            writer.WriteEndArray();
        });
    }

    private Task<JsonValueWriter?> Refresh(Action<LspFeatureRegistration> refresh)
    {
        foreach (var registration in _registrations.ToList())
        {
            refresh(registration);
        }
        return Task.FromResult<JsonValueWriter?>(null);
    }

    private static LanguageClientMessageType ToMessageType(int type) =>
        type is >= 1 and <= 5 ? (LanguageClientMessageType)type : LanguageClientMessageType.Info;
}
