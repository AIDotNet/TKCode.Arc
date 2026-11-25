// A Language Server Protocol client for the editor engine: the lifecycle (initialize, initialized, shutdown, exit),
// document sync of attached text models, diagnostics as markers, and the server's features registered as language
// feature providers. Written for this package: vscode's src/vs has no language client, so the shapes follow the
// specification (3.17) and the engine's port of languages.ts.

using System.Globalization;
using System.Text.Json;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Lsp.JsonRpc;
using TKCode.Arc.Editor.Lsp.Protocol;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform.Markers;

namespace TKCode.Arc.Editor.Lsp;

/// <summary>
/// Connects the code editor to a language server over a pair of streams (usually the server process's standard output
/// and input): it starts the session, keeps the attached documents in sync, shows the server's diagnostics and provides
/// its completion, hover, navigation, rename, code actions, formatting, semantic tokens, inlay hints and the other
/// features it announces.
/// </summary>
/// <remarks>
/// Messages from the server (diagnostics, edits it applies) are handled on the synchronization context that was current
/// when <see cref="StartAsync"/> was called, so start the client on the editor's UI thread.
/// </remarks>
public sealed partial class LanguageClient : IDisposable, IAsyncDisposable
{
    private static readonly LspJsonContext s_json = LspJsonContext.Default;

    private readonly LanguageClientOptions _options;
    private readonly List<LspDocument> _documents = new();
    private readonly Dictionary<string, LspPublishedDiagnostics> _diagnostics = new(StringComparer.Ordinal);
    private readonly List<LspFeatureRegistration> _registrations = new();
    private JsonRpcConnection? _connection;
    private SynchronizationContext? _context;
    private LspServerCapabilities? _capabilities;
    private State _state;

    /// <summary>Creates a client; <see cref="StartAsync"/> connects it.</summary>
    public LanguageClient(LanguageClientOptions? options = null)
    {
        _options = options ?? new LanguageClientOptions();
    }

    /// <summary>Raised for the server's <c>window/showMessage</c>: a message to show the user.</summary>
    public event EventHandler<LanguageClientMessageEventArgs>? ShowMessage;

    /// <summary>Raised for the server's <c>window/logMessage</c> and for requests of this client that failed.</summary>
    public event EventHandler<LanguageClientMessageEventArgs>? LogMessage;

    /// <summary>Raised when the connection ends without <see cref="StopAsync"/> (the server exited or a stream failed).</summary>
    public event EventHandler? Disconnected;

    /// <summary>Whether the session is initialized and the connection open.</summary>
    public bool IsRunning => _state == State.Running;

    /// <summary>The name the server gave in its <c>initialize</c> answer, if any.</summary>
    public string? ServerName { get; private set; }

    /// <summary>The version the server gave in its <c>initialize</c> answer, if any.</summary>
    public string? ServerVersion { get; private set; }

    internal LanguageClientOptions Options => _options;

    internal JsonRpcConnection? Connection => _connection;

    /// <summary>What the server announced; null before the session is initialized.</summary>
    internal LspServerCapabilities? Capabilities => _capabilities;

    /// <summary>The owner of the markers this client sets.</summary>
    internal string MarkerOwner => _options.Name;

    internal IReadOnlyList<LspDocument> Documents => _documents;

    /// <summary>
    /// Connects to a server and initializes the session. <paramref name="input"/> is what the server writes (its
    /// standard output), <paramref name="output"/> what it reads (its standard input). Documents attached before the
    /// call are opened once the session is initialized.
    /// </summary>
    /// <exception cref="InvalidOperationException">The client was started before.</exception>
    public async Task StartAsync(Stream input, Stream output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        if (_state != State.New)
        {
            throw new InvalidOperationException("The language client has already been started.");
        }
        JsonElement? initializationOptions = null;
        if (!string.IsNullOrWhiteSpace(_options.InitializationOptions))
        {
            using var document = JsonDocument.Parse(_options.InitializationOptions);
            initializationOptions = document.RootElement.Clone();
        }

        _state = State.Starting;
        _context = SynchronizationContext.Current;
        var connection = new JsonRpcConnection(input, output, _context);
        _connection = connection;
        RegisterHandlers(connection);
        connection.Trace += message => Log(LanguageClientMessageType.Log, message);
        connection.Closed += OnConnectionClosed;
        connection.Start();

        var root = _options.RootUri;
        var parameters = new InitializeParams
        {
            ProcessId = Environment.ProcessId,
            ClientInfo = new ClientInfo(_options.ClientName) { Version = _options.ClientVersion },
            Locale = _options.Locale ?? CultureInfo.CurrentUICulture.Name,
            RootUri = root is null ? null : LspConvert.FromUri(root),
            RootPath = root is { IsFile: true } ? root.LocalPath : null,
            Capabilities = LspClientCapabilities.Value,
            InitializationOptions = initializationOptions,
            Trace = "off",
            WorkspaceFolders = root is null ? null : [new WorkspaceFolder(LspConvert.FromUri(root), WorkspaceName(root))],
        };
        JsonElement result;
        try
        {
            result = await connection.SendRequest("initialize", parameters, s_json.InitializeParams, cancellationToken);
        }
        catch
        {
            _state = State.Stopped;
            connection.Dispose();
            throw;
        }
        var initializeResult = LspConvert.IsNull(result) ? null : result.Deserialize(s_json.InitializeResult);
        _capabilities = new LspServerCapabilities(initializeResult?.Capabilities ?? default);
        ServerName = initializeResult?.ServerInfo?.Name;
        ServerVersion = initializeResult?.ServerInfo?.Version;
        if (_state != State.Starting)
        {
            // The connection closed while the server initialized.
            throw new IOException("The language server closed the connection during initialization.");
        }
        connection.SendNotification("initialized", new InitializedParams(), s_json.InitializedParams);
        _state = State.Running;

        foreach (var document in _documents.ToList())
        {
            OpenDocument(document);
        }
    }

    /// <summary>
    /// Ends the session: closes the attached documents' features and markers, asks the server to shut down (waiting up
    /// to <see cref="LanguageClientOptions.ShutdownTimeout"/>), tells it to exit and closes the connection.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_state is State.New or State.Stopped || _connection is not { } connection)
        {
            _state = State.Stopped;
            return;
        }
        var wasRunning = _state == State.Running;
        _state = State.Stopping;
        foreach (var document in _documents.ToList())
        {
            document.Detach(sendClose: false);
        }
        _documents.Clear();
        if (wasRunning && !connection.IsClosed)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.ShutdownTimeout);
            try
            {
                await connection.SendRequest("shutdown", null, timeout.Token);
            }
            catch (Exception e) when (e is OperationCanceledException or JsonRpcException or IOException)
            {
                Log(LanguageClientMessageType.Warning, "The language server did not answer shutdown: " + e.Message);
            }
            connection.SendNotification("exit", null);
        }
        _state = State.Stopped;
        connection.Dispose();
        try
        {
            await connection.WriteCompletion.WaitAsync(_options.ShutdownTimeout, cancellationToken);
        }
        catch (Exception e) when (e is TimeoutException or OperationCanceledException)
        {
        }
    }

    /// <summary>Stops the client without waiting for the server (see <see cref="StopAsync"/> for an orderly end).</summary>
    public void Dispose()
    {
        foreach (var document in _documents.ToList())
        {
            document.Detach(sendClose: false);
        }
        _documents.Clear();
        if (_state == State.Running && _connection is { IsClosed: false } connection)
        {
            connection.SendNotification("exit", null);
        }
        _state = State.Stopped;
        _connection?.Dispose();
    }

    /// <summary>Stops the client, as <see cref="StopAsync"/>.</summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        Dispose();
    }

    /// <summary>
    /// Attaches a text model: it is opened on the server (now, or when the session starts), its edits are sent as they
    /// happen, the server's diagnostics for it go to <paramref name="markers"/> and the server's features are registered
    /// in <paramref name="features"/> for the model's language. Disposing the result closes the document.
    /// </summary>
    /// <param name="model">The document.</param>
    /// <param name="features">The registries the providers go to (an editor's <c>LanguageFeatures</c>).</param>
    /// <param name="markers">Where diagnostics go (an editor's <c>MarkerService</c>); they are set on the model's URI.</param>
    /// <param name="documentUri">The URI the server knows the document by; null uses the model's URI.</param>
    /// <param name="languageId">The server's language id; null uses the model's language (and follows its changes).</param>
    internal IDisposable Attach(TextModel model, LanguageFeatures features, MarkerService markers, Uri? documentUri = null, string? languageId = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(features);
        ArgumentNullException.ThrowIfNull(markers);
        if (_state is State.Stopping or State.Stopped)
        {
            throw new InvalidOperationException("The language client has been stopped.");
        }
        if (_documents.Any(document => document.Model == model))
        {
            throw new InvalidOperationException("The model is already attached to this client.");
        }
        var lspDocument = new LspDocument(this, model, features, markers, documentUri ?? model.Uri, languageId);
        _documents.Add(lspDocument);
        if (_state == State.Running)
        {
            OpenDocument(lspDocument);
        }
        return lspDocument;
    }

    /// <summary>The attached document of a model; null when the model is not attached (or the session is not running).</summary>
    internal LspDocument? GetDocument(TextModel model)
    {
        if (_state != State.Running)
        {
            return null;
        }
        foreach (var document in _documents)
        {
            if (document.Model == model && document.IsOpen)
            {
                return document;
            }
        }
        return null;
    }

    /// <summary>The attached document the server knows by <paramref name="uri"/>.</summary>
    internal LspDocument? FindDocument(string uri)
    {
        var key = LspConvert.UriKey(uri);
        foreach (var document in _documents)
        {
            if (document.UriKey == key)
            {
                return document;
            }
        }
        return null;
    }

    /// <summary>Runs a command on the server (<c>workspace/executeCommand</c>); false when the server does not offer it.</summary>
    internal async Task<bool> ExecuteCommandAsync(LanguageCommand command, CancellationToken cancellationToken = default)
    {
        if (_state != State.Running || _connection is not { } connection || _capabilities is not { } capabilities
            || !capabilities.ExecuteCommands.Contains(command.Id))
        {
            return false;
        }
        var lspCommand = LspConvert.FromCommand(command);
        var parameters = new ExecuteCommandParams(lspCommand.Command) { Arguments = lspCommand.Arguments };
        try
        {
            await connection.SendRequest("workspace/executeCommand", parameters, s_json.ExecuteCommandParams, cancellationToken);
            return true;
        }
        catch (JsonRpcException e)
        {
            Log(LanguageClientMessageType.Error, $"workspace/executeCommand '{command.Id}' failed: {e.Message}");
            return false;
        }
    }

    internal void Log(LanguageClientMessageType type, string message) =>
        LogMessage?.Invoke(this, new LanguageClientMessageEventArgs(type, message));

    internal void RemoveDocument(LspDocument document) => _documents.Remove(document);

    /// <summary>The diagnostics the server last published for a document, if any.</summary>
    internal LspPublishedDiagnostics? GetPublishedDiagnostics(string uriKey) =>
        _diagnostics.TryGetValue(uriKey, out var diagnostics) ? diagnostics : null;

    /// <summary>The provider registration for a registry set and language, shared by the documents that use it.</summary>
    internal LspFeatureRegistration AcquireRegistration(LanguageFeatures features, string languageId)
    {
        foreach (var registration in _registrations)
        {
            if (registration.Features == features && registration.LanguageId == languageId)
            {
                registration.AddReference();
                return registration;
            }
        }
        var created = new LspFeatureRegistration(this, features, languageId, _capabilities!);
        _registrations.Add(created);
        created.AddReference();
        return created;
    }

    internal void ReleaseRegistration(LspFeatureRegistration registration)
    {
        if (registration.Release())
        {
            _registrations.Remove(registration);
        }
    }

    private void OpenDocument(LspDocument document)
    {
        document.Open();
        if (_diagnostics.TryGetValue(document.UriKey, out var diagnostics))
        {
            document.ApplyDiagnostics(diagnostics);
        }
    }

    private void OnConnectionClosed(Exception? error)
    {
        var context = _context;
        if (context is null)
        {
            HandleClosed(error);
        }
        else
        {
            context.Post(_ => HandleClosed(error), null);
        }
    }

    private void HandleClosed(Exception? error)
    {
        if (_state is State.Stopping or State.Stopped)
        {
            return;
        }
        _state = State.Stopped;
        foreach (var document in _documents.ToList())
        {
            document.Detach(sendClose: false);
        }
        _documents.Clear();
        if (error is not null)
        {
            Log(LanguageClientMessageType.Error, "The connection to the language server failed: " + error.Message);
        }
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    private static string WorkspaceName(Uri root)
    {
        var path = root.IsAbsoluteUri ? root.AbsolutePath.TrimEnd('/') : root.OriginalString.TrimEnd('/');
        var slash = path.LastIndexOf('/');
        var name = Uri.UnescapeDataString(slash >= 0 ? path[(slash + 1)..] : path);
        return name.Length == 0 ? "workspace" : name;
    }

    private enum State
    {
        New,
        Starting,
        Running,
        Stopping,
        Stopped,
    }
}
