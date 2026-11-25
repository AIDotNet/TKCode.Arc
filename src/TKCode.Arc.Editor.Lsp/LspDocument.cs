// Document sync of one text model (textDocument/didOpen, didChange, didClose; specification 3.17) and the markers of
// its diagnostics.

using System.Text.Json;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Lsp.Protocol;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform.Markers;

namespace TKCode.Arc.Editor.Lsp;

/// <summary>A text model attached to a <see cref="LanguageClient"/>; disposing it closes the document on the server.</summary>
internal sealed class LspDocument : IDisposable
{
    private static readonly LspJsonContext s_json = LspJsonContext.Default;

    private readonly LanguageClient _client;
    private readonly string? _languageIdOverride;
    private LspFeatureRegistration? _registration;
    private LspPublishedDiagnostics? _diagnostics;
    private bool _subscribed;
    private bool _detached;

    public LspDocument(LanguageClient client, TextModel model, LanguageFeatures features, MarkerService markers, Uri uri, string? languageId)
    {
        _client = client;
        Model = model;
        Features = features;
        Markers = markers;
        Uri = LspConvert.FromUri(uri);
        UriKey = LspConvert.UriKey(Uri);
        _languageIdOverride = languageId;
        LanguageId = languageId ?? model.GetLanguageId();
    }

    public TextModel Model { get; }

    public LanguageFeatures Features { get; }

    public MarkerService Markers { get; }

    /// <summary>The URI the server knows the document by.</summary>
    public string Uri { get; }

    public string UriKey { get; }

    /// <summary>The server's language id of the document.</summary>
    public string LanguageId { get; private set; }

    /// <summary>Whether the server has the document (didOpen sent, or the server does not sync and the features apply).</summary>
    public bool IsOpen { get; private set; }

    /// <summary>The version the server last got.</summary>
    public int Version { get; private set; }

    public TextDocumentIdentifier Identifier => new(Uri);

    /// <summary>Opens the document: didOpen (when the server wants it), the model's events, the providers.</summary>
    public void Open()
    {
        if (IsOpen || _detached || Model.IsDisposed())
        {
            return;
        }
        if (!_subscribed)
        {
            Model.DidChangeContent += OnDidChangeContent;
            Model.DidChangeLanguage += OnDidChangeLanguage;
            Model.WillDispose += OnWillDispose;
            _subscribed = true;
        }
        SendOpen();
        _registration = _client.AcquireRegistration(Features, LanguageId);
    }

    public void Dispose()
    {
        Detach(sendClose: true);
        _client.RemoveDocument(this);
    }

    /// <summary>Stops syncing: unsubscribes, releases the providers, removes the markers and (if asked) sends didClose.</summary>
    public void Detach(bool sendClose)
    {
        if (_detached)
        {
            return;
        }
        _detached = true;
        if (_subscribed)
        {
            Model.DidChangeContent -= OnDidChangeContent;
            Model.DidChangeLanguage -= OnDidChangeLanguage;
            Model.WillDispose -= OnWillDispose;
            _subscribed = false;
        }
        if (_registration is not null)
        {
            _client.ReleaseRegistration(_registration);
            _registration = null;
        }
        if (_diagnostics is not null)
        {
            Markers.Remove(_client.MarkerOwner, [Model.Uri]);
            _diagnostics = null;
        }
        if (IsOpen && sendClose)
        {
            SendClose();
        }
        IsOpen = false;
    }

    /// <summary>Shows the published diagnostics as markers on the model.</summary>
    public void ApplyDiagnostics(LspPublishedDiagnostics diagnostics)
    {
        if (_detached)
        {
            return;
        }
        _diagnostics = diagnostics.Items.Count == 0 ? null : diagnostics;
        Markers.ChangeOne(_client.MarkerOwner, Model.Uri, diagnostics.Items.Select(static item => item.Marker).ToList());
    }

    /// <summary>The protocol values of the published diagnostics that touch a range (a code action's context).</summary>
    public IReadOnlyList<JsonElement> GetDiagnosticsIn(TextRange range)
    {
        if (_diagnostics is null)
        {
            return [];
        }
        var result = new List<JsonElement>();
        foreach (var item in _diagnostics.Items)
        {
            var marker = item.Marker;
            var markerRange = new TextRange(marker.StartLineNumber, marker.StartColumn, marker.EndLineNumber, marker.EndColumn);
            if (TextRange.AreIntersectingOrTouching(markerRange, range))
            {
                result.Add(item.Raw);
            }
        }
        return result;
    }

    private bool SyncsOpenClose => _client.Capabilities is { OpenClose: true };

    private void SendOpen()
    {
        IsOpen = true;
        Version = Model.GetVersionId();
        if (!SyncsOpenClose || _client.Connection is not { } connection)
        {
            return;
        }
        var item = new TextDocumentItem(Uri, LanguageId, Version, Model.GetValue());
        connection.SendNotification("textDocument/didOpen", new DidOpenTextDocumentParams(item), s_json.DidOpenTextDocumentParams);
    }

    private void SendClose()
    {
        if (!SyncsOpenClose || _client.Connection is not { } connection)
        {
            return;
        }
        connection.SendNotification("textDocument/didClose", new DidCloseTextDocumentParams(Identifier), s_json.DidCloseTextDocumentParams);
    }

    private void OnDidChangeContent(ModelContentChangedEvent e)
    {
        if (!IsOpen || _client.Connection is not { } connection || _client.Capabilities is not { } capabilities)
        {
            return;
        }
        Version = e.VersionId;
        TextDocumentContentChangeEvent[] changes;
        switch (capabilities.SyncKind)
        {
            case LspTextDocumentSyncKind.Incremental when !e.IsFlush && !e.IsEolChange:
                // The model's changes of one edit all refer to the text before it and come last-in-document first, so the
                // server can apply them in order (as vscode's language client sends them).
                changes = new TextDocumentContentChangeEvent[e.Changes.Count];
                for (var i = 0; i < e.Changes.Count; i++)
                {
                    var change = e.Changes[i];
                    changes[i] = new TextDocumentContentChangeEvent(change.Text)
                    {
                        Range = LspConvert.FromRange(change.Range),
                        RangeLength = change.RangeLength,
                    };
                }
                break;
            case LspTextDocumentSyncKind.Incremental:
            case LspTextDocumentSyncKind.Full:
                changes = [new TextDocumentContentChangeEvent(Model.GetValue())];
                break;
            default:
                return;
        }
        var parameters = new DidChangeTextDocumentParams(new VersionedTextDocumentIdentifier(Uri, e.VersionId), changes);
        connection.SendNotification("textDocument/didChange", parameters, s_json.DidChangeTextDocumentParams);
    }

    private void OnDidChangeLanguage(ModelLanguageChangedEvent e)
    {
        if (_languageIdOverride is not null || !IsOpen)
        {
            return;
        }
        // The server sees a language change as a close and an open (as vscode's language client); the providers move to
        // the new language.
        SendClose();
        if (_registration is not null)
        {
            _client.ReleaseRegistration(_registration);
            _registration = null;
        }
        IsOpen = false;
        LanguageId = e.NewLanguage;
        SendOpen();
        _registration = _client.AcquireRegistration(Features, LanguageId);
    }

    private void OnWillDispose() => Dispose();
}
