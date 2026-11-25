using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.Platform.Markers;

namespace TKCode.Arc.Editor.Lsp.Tests;

/// <summary>A started client and fake server with one attached model, as an editor would have them.</summary>
internal sealed class LspTestSession : IAsyncDisposable
{
    public const string DocumentUri = "file:///work/main.ts";

    private LspTestSession(FakeLanguageServer server, LanguageClient client, TextModel model, IDisposable? attachment)
    {
        Server = server;
        Client = client;
        Model = model;
        Attachment = attachment;
    }

    public FakeLanguageServer Server { get; }

    public LanguageClient Client { get; }

    public TextModel Model { get; }

    public LanguageFeatures Features { get; } = new();

    public MarkerService Markers { get; } = new();

    public IDisposable? Attachment { get; private set; }

    public static async Task<LspTestSession> Start(
        string capabilities,
        string text = "",
        Action<FakeLanguageServer>? configure = null,
        LanguageClientOptions? options = null,
        bool attach = true)
    {
        var server = new FakeLanguageServer(capabilities);
        configure?.Invoke(server);
        server.Start();
        var client = new LanguageClient(options ?? new LanguageClientOptions { RootUri = new Uri("file:///work/") });
        var model = CreateModel(text, new Uri(DocumentUri));
        var session = new LspTestSession(server, client, model, null);
        await client.StartAsync(server.ClientInput, server.ClientOutput);
        if (attach)
        {
            session.Attach();
        }
        return session;
    }

    public void Attach() => Attachment = Client.Attach(Model, Features, Markers);

    /// <summary>Waits until the server has the model's text (the client's sync caught up).</summary>
    public Task Synced() => TestWait.Until(() => Server.Document(DocumentUri) == Model.GetValue());

    public static TextModel CreateModel(string text, Uri uri, string languageId = "typescript") =>
        new(text, languageId, TextModel.DefaultCreationOptions, new UndoRedoService(new DialogService(), new NotificationService()), uri);

    public async ValueTask DisposeAsync()
    {
        await Client.StopAsync();
        await Server.DisposeAsync();
        Model.Dispose();
        Markers.Dispose();
    }

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
