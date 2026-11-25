// The client against an in-process server over pipes: lifecycle, document sync, diagnostics, server requests.
// Written for this package.

using System.Text.Json;
using Shouldly;
using TKCode.Arc.Editor.Lsp.Protocol;
using TKCode.Arc.Editor.Platform.Markers;
using Xunit;

namespace TKCode.Arc.Editor.Lsp.Tests;

public sealed class LanguageClientTests
{
    internal const string IncrementalSync = """{"textDocumentSync":{"openClose":true,"change":2}}""";

    private const string WithHover = """{"textDocumentSync":{"openClose":true,"change":2},"hoverProvider":true}""";

    [Fact]
    public async Task Initialize_Introduces_The_Client_And_Reads_The_Server()
    {
        await using var session = await LspTestSession.Start(IncrementalSync, attach: false, options: new LanguageClientOptions
        {
            ClientName = "test-client",
            ClientVersion = "9.9",
            RootUri = new Uri("file:///work/project/"),
            InitializationOptions = """{"flag":true}""",
            Locale = "de-DE",
        });

        var parameters = session.Server.InitializeParams;
        parameters.GetProperty("processId").GetInt32().ShouldBe(Environment.ProcessId);
        parameters.GetProperty("clientInfo").GetProperty("name").GetString().ShouldBe("test-client");
        parameters.GetProperty("clientInfo").GetProperty("version").GetString().ShouldBe("9.9");
        parameters.GetProperty("rootUri").GetString().ShouldBe("file:///work/project/");
        parameters.GetProperty("locale").GetString().ShouldBe("de-DE");
        parameters.GetProperty("initializationOptions").GetProperty("flag").GetBoolean().ShouldBeTrue();
        parameters.GetProperty("workspaceFolders")[0].GetProperty("name").GetString().ShouldBe("project");
        var capabilities = parameters.GetProperty("capabilities");
        capabilities.GetProperty("textDocument").GetProperty("completion").GetProperty("completionItem").GetProperty("snippetSupport").GetBoolean().ShouldBeTrue();
        capabilities.GetProperty("general").GetProperty("positionEncodings")[0].GetString().ShouldBe("utf-16");
        capabilities.GetProperty("workspace").GetProperty("applyEdit").GetBoolean().ShouldBeTrue();

        await TestWait.Until(() => session.Server.Received("initialized").Any());
        session.Client.IsRunning.ShouldBeTrue();
        session.Client.ServerName.ShouldBe("fake");
        session.Client.ServerVersion.ShouldBe("1.0");
        session.Client.Capabilities!.SyncKind.ShouldBe(LspTextDocumentSyncKind.Incremental);
    }

    [Fact]
    public async Task Attach_Opens_The_Document()
    {
        await using var session = await LspTestSession.Start(IncrementalSync, "let a = 1;\nlet b = 2;");

        await TestWait.Until(() => session.Server.Received("textDocument/didOpen").Any());
        var item = session.Server.Received("textDocument/didOpen").Single().GetProperty("textDocument");
        item.GetProperty("uri").GetString().ShouldBe(LspTestSession.DocumentUri);
        item.GetProperty("languageId").GetString().ShouldBe("typescript");
        item.GetProperty("version").GetInt32().ShouldBe(session.Model.GetVersionId());
        item.GetProperty("text").GetString().ShouldBe("let a = 1;\nlet b = 2;");
    }

    [Fact]
    public async Task A_Document_Attached_Before_Start_Opens_After_Initialized()
    {
        var server = new FakeLanguageServer(IncrementalSync).Start();
        var client = new LanguageClient();
        using var model = LspTestSession.CreateModel("x", new Uri(LspTestSession.DocumentUri));
        var features = new Languages.LanguageFeatures();
        using var markers = new MarkerService();
        using var attachment = client.Attach(model, features, markers);

        await client.StartAsync(server.ClientInput, server.ClientOutput);

        await TestWait.Until(() => server.Document(LspTestSession.DocumentUri) == "x");
        var methods = server.Messages.Select(m => m.Method).ToList();
        methods.IndexOf("initialized").ShouldBeLessThan(methods.IndexOf("textDocument/didOpen"));
        await client.StopAsync();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task Incremental_Changes_Rebuild_The_Text_On_The_Server()
    {
        await using var session = await LspTestSession.Start(IncrementalSync, "function f() {\n  return 1;\n}\n");
        var model = session.Model;

        model.PushEditOperations(null, [new SingleEditOperation(new TextRange(2, 10, 2, 11), "42")], null);
        model.PushStackElement();
        // Several edits in one operation (as multiple cursors make), across lines, and a deletion.
        model.PushEditOperations(null,
        [
            new SingleEditOperation(new TextRange(1, 10, 1, 11), "g"),
            new SingleEditOperation(new TextRange(2, 3, 2, 3), "// x\n  "),
            new SingleEditOperation(new TextRange(3, 1, 4, 1), "}"),
        ], null);
        model.PushStackElement();
        model.PushEditOperations(null, [new SingleEditOperation(new TextRange(1, 1, 1, 1), "é😀 ")], null);
        await session.Synced();

        await model.Undo();
        await session.Synced();
        await model.Undo();
        await session.Synced();

        var changes = session.Server.Received("textDocument/didChange").ToList();
        changes.Count.ShouldBe(5);
        changes[^1].GetProperty("textDocument").GetProperty("version").GetInt32().ShouldBe(model.GetVersionId());
        changes[0].GetProperty("contentChanges")[0].GetProperty("range").GetProperty("start").GetProperty("line").GetInt32().ShouldBe(1);
        changes[0].GetProperty("contentChanges")[0].GetProperty("rangeLength").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Full_Sync_Sends_The_Whole_Text()
    {
        await using var session = await LspTestSession.Start("""{"textDocumentSync":1}""", "abc");

        session.Model.PushEditOperations(null, [new SingleEditOperation(new TextRange(1, 2, 1, 3), "X")], null);
        await session.Synced();

        var change = session.Server.Received("textDocument/didChange").Single().GetProperty("contentChanges").EnumerateArray().Single();
        change.TryGetProperty("range", out _).ShouldBeFalse();
        change.GetProperty("text").GetString().ShouldBe("aXc");
    }

    [Fact]
    public async Task Setting_The_Value_Sends_The_Whole_Text_Even_When_Incremental()
    {
        await using var session = await LspTestSession.Start(IncrementalSync, "old");

        session.Model.SetValue("new\ntext");
        await session.Synced();

        session.Server.Received("textDocument/didChange").Single().GetProperty("contentChanges")[0].TryGetProperty("range", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task No_Sync_Sends_Nothing()
    {
        await using var session = await LspTestSession.Start("""{"hoverProvider":true}""", "abc");

        session.Model.PushEditOperations(null, [new SingleEditOperation(new TextRange(1, 1, 1, 1), "x")], null);
        await session.Server.Request("workspace/configuration", """{"items":[]}""");

        session.Server.Messages.Select(m => m.Method).ShouldNotContain("textDocument/didOpen");
        session.Server.Messages.Select(m => m.Method).ShouldNotContain("textDocument/didChange");
    }

    [Fact]
    public async Task Disposing_The_Attachment_Closes_The_Document()
    {
        await using var session = await LspTestSession.Start(IncrementalSync, "abc");
        await session.Synced();

        session.Attachment!.Dispose();

        await TestWait.Until(() => session.Server.Received("textDocument/didClose").Any());
        session.Client.Documents.ShouldBeEmpty();
        session.Model.PushEditOperations(null, [new SingleEditOperation(new TextRange(1, 1, 1, 1), "x")], null);
        await session.Server.Request("workspace/configuration", """{"items":[]}""");
        session.Server.Received("textDocument/didChange").ShouldBeEmpty();
    }

    [Fact]
    public async Task Disposing_The_Model_Closes_The_Document()
    {
        var server = new FakeLanguageServer(IncrementalSync).Start();
        var client = new LanguageClient();
        await client.StartAsync(server.ClientInput, server.ClientOutput);
        var model = LspTestSession.CreateModel("x", new Uri("file:///other.ts"));
        using var markers = new MarkerService();
        client.Attach(model, new Languages.LanguageFeatures(), markers);

        model.Dispose();

        await TestWait.Until(() => server.Received("textDocument/didClose").Any());
        client.Documents.ShouldBeEmpty();
        await client.StopAsync();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task A_Language_Change_Reopens_The_Document()
    {
        await using var session = await LspTestSession.Start(IncrementalSync, "abc");
        await session.Synced();

        session.Model.SetLanguage("javascript");

        await TestWait.Until(() => session.Server.Received("textDocument/didOpen").Count() == 2);
        session.Server.Received("textDocument/didClose").Count().ShouldBe(1);
        session.Server.Received("textDocument/didOpen").Last().GetProperty("textDocument").GetProperty("languageId").GetString().ShouldBe("javascript");
    }

    [Fact]
    public async Task Published_Diagnostics_Become_Markers_On_The_Model()
    {
        await using var session = await LspTestSession.Start(IncrementalSync, "let unused = 1;\nfoo();");

        session.Server.Notify("textDocument/publishDiagnostics", """
            {"uri":"file:///work/main.ts","diagnostics":[
              {"range":{"start":{"line":0,"character":4},"end":{"line":0,"character":10}},"severity":4,"message":"unused","tags":[1],"source":"ts","code":6133},
              {"range":{"start":{"line":1,"character":0},"end":{"line":1,"character":3}},"severity":1,"message":"Cannot find name 'foo'."}]}
            """);
        await TestWait.Until(() => session.Markers.Read(new MarkerReadOptions { Resource = session.Model.Uri }).Count == 2);

        var markers = session.Markers.Read(new MarkerReadOptions { Resource = session.Model.Uri });
        var error = markers.Single(m => m.Severity == MarkerSeverity.Error);
        error.Owner.ShouldBe("lsp");
        error.Message.ShouldBe("Cannot find name 'foo'.");
        error.Range.ShouldBe(new TextRange(2, 1, 2, 4));
        var hint = markers.Single(m => m.Severity == MarkerSeverity.Hint);
        hint.HasTag(MarkerTag.Unnecessary).ShouldBeTrue();
        hint.Code.ShouldBe("6133");

        session.Server.Notify("textDocument/publishDiagnostics", """{"uri":"file:///work/main.ts","diagnostics":[]}""");
        await TestWait.Until(() => session.Markers.Read(new MarkerReadOptions { Resource = session.Model.Uri }).Count == 0);
    }

    [Fact]
    public async Task Diagnostics_Published_Before_Attach_Show_When_The_Document_Opens()
    {
        await using var session = await LspTestSession.Start(IncrementalSync, "abc", attach: false);
        session.Server.Notify("textDocument/publishDiagnostics", """
            {"uri":"file:///work/main.ts","diagnostics":[{"range":{"start":{"line":0,"character":0},"end":{"line":0,"character":1}},"message":"early"}]}
            """);
        await TestWait.Until(() => session.Client.GetPublishedDiagnostics(LspConvert.UriKey(LspTestSession.DocumentUri)) is not null);

        session.Attach();

        session.Markers.Read(new MarkerReadOptions { Resource = session.Model.Uri }).Single().Message.ShouldBe("early");
    }

    [Fact]
    public async Task Diagnostics_For_Another_Document_Do_Not_Touch_This_One()
    {
        await using var session = await LspTestSession.Start(IncrementalSync, "abc");
        session.Server.Notify("textDocument/publishDiagnostics", """
            {"uri":"file:///work/other.ts","diagnostics":[{"range":{"start":{"line":0,"character":0},"end":{"line":0,"character":1}},"message":"elsewhere"}]}
            """);
        await TestWait.Until(() => session.Client.GetPublishedDiagnostics(LspConvert.UriKey("file:///work/other.ts")) is not null);

        session.Markers.Read().ShouldBeEmpty();
    }

    [Fact]
    public async Task The_Server_Applies_A_Workspace_Edit()
    {
        await using var session = await LspTestSession.Start(IncrementalSync, "let a = 1;\nlet b = a;");
        await session.Synced();

        var result = await session.Server.Request("workspace/applyEdit", """
            {"label":"rename","edit":{"changes":{"file:///work/main.ts":[
              {"range":{"start":{"line":0,"character":4},"end":{"line":0,"character":5}},"newText":"x"},
              {"range":{"start":{"line":1,"character":8},"end":{"line":1,"character":9}},"newText":"x"}]}}}
            """);

        result.GetProperty("applied").GetBoolean().ShouldBeTrue();
        session.Model.GetValue().ShouldBe("let x = 1;\nlet b = x;");
        await session.Synced();
        await session.Model.Undo();
        session.Model.GetValue().ShouldBe("let a = 1;\nlet b = a;");
    }

    [Fact]
    public async Task A_Workspace_Edit_For_A_Closed_Document_Or_A_File_Fails()
    {
        await using var session = await LspTestSession.Start(IncrementalSync, "abc");
        await session.Synced();

        var closed = await session.Server.Request("workspace/applyEdit", """
            {"edit":{"changes":{"file:///work/closed.ts":[{"range":{"start":{"line":0,"character":0},"end":{"line":0,"character":0}},"newText":"x"}]}}}
            """);
        var file = await session.Server.Request("workspace/applyEdit", """{"edit":{"documentChanges":[{"kind":"create","uri":"file:///work/new.ts"}]}}""");
        var stale = await session.Server.Request("workspace/applyEdit", """
            {"edit":{"documentChanges":[{"textDocument":{"uri":"file:///work/main.ts","version":999},"edits":[{"range":{"start":{"line":0,"character":0},"end":{"line":0,"character":0}},"newText":"x"}]}]}}
            """);

        closed.GetProperty("applied").GetBoolean().ShouldBeFalse();
        file.GetProperty("applied").GetBoolean().ShouldBeFalse();
        stale.GetProperty("applied").GetBoolean().ShouldBeFalse();
        stale.GetProperty("failureReason").GetString()!.ShouldContain("999");
        session.Model.GetValue().ShouldBe("abc");
    }

    [Fact]
    public async Task Window_Messages_Reach_The_Host()
    {
        var chosen = new List<string>();
        await using var session = await LspTestSession.Start(IncrementalSync, options: new LanguageClientOptions
        {
            ShowMessageRequest = (message, actions, _) =>
            {
                chosen.Add(message.Message);
                return Task.FromResult<string?>(actions[1]);
            },
        });
        var shown = new List<LanguageClientMessageEventArgs>();
        var logged = new List<LanguageClientMessageEventArgs>();
        session.Client.ShowMessage += (_, e) => shown.Add(e);
        session.Client.LogMessage += (_, e) => logged.Add(e);

        session.Server.Notify("window/showMessage", """{"type":2,"message":"careful"}""");
        session.Server.Notify("window/logMessage", """{"type":4,"message":"started"}""");
        var answer = await session.Server.Request("window/showMessageRequest", """{"type":3,"message":"Pick","actions":[{"title":"A"},{"title":"B"}]}""");

        answer.GetProperty("title").GetString().ShouldBe("B");
        chosen.ShouldBe(["Pick"]);
        await TestWait.Until(() => shown.Count == 1 && logged.Count == 1);
        shown[0].Type.ShouldBe(LanguageClientMessageType.Warning);
        shown[0].Message.ShouldBe("careful");
        logged[0].Type.ShouldBe(LanguageClientMessageType.Log);
    }

    [Fact]
    public async Task Configuration_And_Registration_Requests_Are_Answered()
    {
        await using var session = await LspTestSession.Start(IncrementalSync);

        var configuration = await session.Server.Request("workspace/configuration", """{"items":[{"section":"a"},{"section":"b"}]}""");
        var registration = await session.Server.Request("client/registerCapability", """{"registrations":[]}""");
        var folders = await session.Server.Request("workspace/workspaceFolders", "null");

        configuration.GetArrayLength().ShouldBe(2);
        configuration[0].ValueKind.ShouldBe(JsonValueKind.Null);
        registration.ValueKind.ShouldBe(JsonValueKind.Null);
        folders[0].GetProperty("uri").GetString().ShouldBe("file:///work/");
    }

    [Fact]
    public async Task Stop_Shuts_Down_And_Removes_Markers_And_Providers()
    {
        await using var session = await LspTestSession.Start(WithHover, "abc");
        session.Server.Notify("textDocument/publishDiagnostics", """
            {"uri":"file:///work/main.ts","diagnostics":[{"range":{"start":{"line":0,"character":0},"end":{"line":0,"character":1}},"message":"m"}]}
            """);
        await TestWait.Until(() => session.Markers.Read().Count == 1);
        session.Features.HoverProvider.Has(session.Model).ShouldBeTrue();

        await session.Client.StopAsync();

        session.Server.ShutdownReceived.ShouldBeTrue();
        await TestWait.Until(() => session.Server.ExitReceived);
        session.Client.IsRunning.ShouldBeFalse();
        session.Markers.Read().ShouldBeEmpty();
        session.Features.HoverProvider.Has(session.Model).ShouldBeFalse();
        session.Server.Received("textDocument/didClose").ShouldBeEmpty();
    }

    [Fact]
    public async Task A_Server_That_Goes_Away_Disconnects_The_Client()
    {
        await using var session = await LspTestSession.Start(WithHover, "abc");
        var disconnected = new TaskCompletionSource();
        session.Client.Disconnected += (_, _) => disconnected.TrySetResult();

        await session.Server.DisposeAsync();

        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        session.Client.IsRunning.ShouldBeFalse();
        session.Features.HoverProvider.Has(session.Model).ShouldBeFalse();
    }

    [Fact]
    public async Task Start_Twice_Throws()
    {
        await using var session = await LspTestSession.Start(IncrementalSync);

        await Should.ThrowAsync<InvalidOperationException>(() => session.Client.StartAsync(session.Server.ClientInput, session.Server.ClientOutput));
    }

    [Fact]
    public async Task Server_Messages_Run_On_The_Context_The_Client_Started_On()
    {
        using var context = new SingleThreadContext();
        var server = new FakeLanguageServer(IncrementalSync).Start();
        var client = new LanguageClient();
        using var model = LspTestSession.CreateModel("abc", new Uri(LspTestSession.DocumentUri));
        using var markers = new MarkerService();
        var threads = new List<int>();
        markers.MarkerChanged += _ => threads.Add(Environment.CurrentManagedThreadId);

        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        Task start;
        try
        {
            start = client.StartAsync(server.ClientInput, server.ClientOutput);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
        await start;
        await context.Run(() => client.Attach(model, new Languages.LanguageFeatures(), markers));
        server.Notify("textDocument/publishDiagnostics", """
            {"uri":"file:///work/main.ts","diagnostics":[{"range":{"start":{"line":0,"character":0},"end":{"line":0,"character":1}},"message":"m"}]}
            """);

        await TestWait.Until(() => threads.Count == 1);
        threads.Single().ShouldBe(context.ThreadId);
        await client.StopAsync();
        await server.DisposeAsync();
    }

    // A synchronization context with one thread of its own, as a UI dispatcher.
    private sealed class SingleThreadContext : SynchronizationContext, IDisposable
    {
        private readonly System.Collections.Concurrent.BlockingCollection<(SendOrPostCallback, object?)> _queue = new();
        private readonly Thread _thread;

        public SingleThreadContext()
        {
            _thread = new Thread(() =>
            {
                SetSynchronizationContext(this);
                foreach (var (callback, state) in _queue.GetConsumingEnumerable())
                {
                    callback(state);
                }
            }) { IsBackground = true };
            _thread.Start();
        }

        public int ThreadId => _thread.ManagedThreadId;

        public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

        public Task Run(Action action)
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Post(_ =>
            {
                action();
                done.SetResult();
            }, null);
            return done.Task;
        }

        public void Dispose() => _queue.CompleteAdding();
    }
}
