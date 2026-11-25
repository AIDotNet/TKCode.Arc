using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Lsp;
using TKCode.Arc.Editor.Lsp.Tests;
using TKCode.Arc.Editor.Platform.Markers;

namespace TKCode.Arc.Tests.Editor;

/// <summary>A CodeEditor connected to a language server (the in-process fake over pipes): sync, diagnostics, providers.</summary>
public class CodeEditorLspTests
{
    private const string Capabilities = """{"textDocumentSync":{"openClose":true,"change":2},"hoverProvider":true,"completionProvider":{"triggerCharacters":["."]}}""";

    private static async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The condition did not hold in time.");
            }
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
    }

    [AvaloniaFact]
    public async Task Diagnostics_show_as_squiggles_and_edits_reach_the_server()
    {
        var editor = new CodeEditor { Text = "let a = b;", LanguageId = "typescript", Width = 400, Height = 200 };
        var window = TestHelpers.Show(editor);
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();

        await using var server = new FakeLanguageServer(Capabilities).Start();
        await using var client = new LanguageClient();
        await client.StartAsync(server.ClientInput, server.ClientOutput);
        const string uri = "file:///work/main.ts";
        using var attachment = editor.AttachLanguageClient(client, new Uri(uri));
        await Until(() => server.Document(uri) == "let a = b;");

        editor.LanguageFeatures.HoverProvider.Has(editor.Model).ShouldBeTrue();
        editor.LanguageFeatures.CompletionProvider.Has(editor.Model).ShouldBeTrue();

        server.Notify("textDocument/publishDiagnostics", """
            {"uri":"file:///work/main.ts","diagnostics":[{"range":{"start":{"line":0,"character":8},"end":{"line":0,"character":9}},"severity":1,"message":"Cannot find name 'b'."}]}
            """);
        await Until(() => editor.MarkerDecorationsService.GetLiveMarkers(editor.Model.Uri).Count == 1);
        var (range, marker) = editor.MarkerDecorationsService.GetLiveMarkers(editor.Model.Uri).Single();
        marker.Severity.ShouldBe(MarkerSeverity.Error);
        range.ShouldBe(new TextRange(1, 9, 1, 10));

        editor.ExecuteEdits("test", [EditOperation.Insert(new TextPosition(1, 1), "const x = 1;\n")]).ShouldBeTrue();
        await Until(() => server.Document(uri) == "const x = 1;\nlet a = b;");
        // The squiggle follows the edit until the server publishes again.
        editor.MarkerDecorationsService.GetLiveMarkers(editor.Model.Uri).Single().Range.ShouldBe(new TextRange(2, 9, 2, 10));

        await client.StopAsync();
        editor.MarkerService.Read().ShouldBeEmpty();
        editor.LanguageFeatures.HoverProvider.Has(editor.Model).ShouldBeFalse();
    }
}
