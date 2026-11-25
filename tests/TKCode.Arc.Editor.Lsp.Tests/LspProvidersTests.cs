// The server's features through the engine's language feature registries, as editor features call them. Written for
// this package.

using System.Text.Json;
using Shouldly;
using TKCode.Arc.Editor.Languages;
using Xunit;

namespace TKCode.Arc.Editor.Lsp.Tests;

public sealed class LspProvidersTests
{
    private const string Range01 = """{"start":{"line":0,"character":0},"end":{"line":0,"character":1}}""";

    private const string AllFeatures = """
        {"textDocumentSync":{"openClose":true,"change":2},
         "completionProvider":{"triggerCharacters":["."],"resolveProvider":true},
         "hoverProvider":true,
         "signatureHelpProvider":{"triggerCharacters":["("],"retriggerCharacters":[","]},
         "definitionProvider":true,"declarationProvider":{},"typeDefinitionProvider":true,"implementationProvider":true,
         "referencesProvider":true,"documentHighlightProvider":true,"documentSymbolProvider":true,
         "codeActionProvider":{"codeActionKinds":["quickfix"],"resolveProvider":true},
         "codeLensProvider":{"resolveProvider":true},
         "documentFormattingProvider":true,"documentRangeFormattingProvider":{"rangesSupport":true},
         "documentOnTypeFormattingProvider":{"firstTriggerCharacter":";","moreTriggerCharacter":["}"]},
         "renameProvider":{"prepareProvider":true},
         "foldingRangeProvider":true,"selectionRangeProvider":true,"linkedEditingRangeProvider":true,"colorProvider":true,
         "inlayHintProvider":{"resolveProvider":true},
         "semanticTokensProvider":{"legend":{"tokenTypes":["variable","function"],"tokenModifiers":["readonly"]},"range":true,"full":{"delta":true}},
         "executeCommandProvider":{"commands":["fix.all"]}}
        """;

    private static Task<LspTestSession> Start(string text, Action<FakeLanguageServer> configure, string capabilities = AllFeatures) =>
        LspTestSession.Start(capabilities, text, configure);

    [Fact]
    public async Task Only_Announced_Features_Are_Registered()
    {
        await using var session = await LspTestSession.Start("""{"hoverProvider":true,"definitionProvider":false,"referencesProvider":{}}""", "abc");

        var features = session.Features;
        features.HoverProvider.Has(session.Model).ShouldBeTrue();
        features.ReferenceProvider.Has(session.Model).ShouldBeTrue();
        features.DefinitionProvider.Has(session.Model).ShouldBeFalse();
        features.CompletionProvider.Has(session.Model).ShouldBeFalse();
        features.DocumentSemanticTokensProvider.Has(session.Model).ShouldBeFalse();
    }

    [Fact]
    public async Task Everything_Announced_Is_Registered_Once_Per_Registry_Set_And_Language()
    {
        await using var session = await Start("abc", _ => { });
        using var second = LspTestSession.CreateModel("def", new Uri("file:///work/second.ts"));
        using var attachment = session.Client.Attach(second, session.Features, session.Markers);

        var features = session.Features;
        features.CompletionProvider.All(session.Model).Count.ShouldBe(1);
        features.HoverProvider.All(second).Count.ShouldBe(1);
        foreach (var has in new[]
                 {
                     features.SignatureHelpProvider.Has(session.Model), features.DefinitionProvider.Has(session.Model),
                     features.DeclarationProvider.Has(session.Model), features.TypeDefinitionProvider.Has(session.Model),
                     features.ImplementationProvider.Has(session.Model), features.ReferenceProvider.Has(session.Model),
                     features.DocumentHighlightProvider.Has(session.Model), features.DocumentSymbolProvider.Has(session.Model),
                     features.CodeActionProvider.Has(session.Model), features.CodeLensProvider.Has(session.Model),
                     features.DocumentFormattingEditProvider.Has(session.Model), features.DocumentRangeFormattingEditProvider.Has(session.Model),
                     features.OnTypeFormattingEditProvider.Has(session.Model), features.RenameProvider.Has(session.Model),
                     features.FoldingRangeProvider.Has(session.Model), features.SelectionRangeProvider.Has(session.Model),
                     features.LinkedEditingRangeProvider.Has(session.Model), features.ColorProvider.Has(session.Model),
                     features.InlayHintsProvider.Has(session.Model), features.DocumentSemanticTokensProvider.Has(session.Model),
                     features.DocumentRangeSemanticTokensProvider.Has(session.Model),
                 })
        {
            has.ShouldBeTrue();
        }
        features.CompletionProvider.All(session.Model).Single().TriggerCharacters.ShouldBe(["."]);
        features.SignatureHelpProvider.All(session.Model).Single().SignatureHelpRetriggerCharacters.ShouldBe([","]);
        features.OnTypeFormattingEditProvider.All(session.Model).Single().AutoFormatTriggerCharacters.ShouldBe([";", "}"]);

        attachment.Dispose();
        features.CompletionProvider.Has(session.Model).ShouldBeTrue();
    }

    [Fact]
    public async Task A_Model_That_Is_Not_Attached_Gets_Nothing()
    {
        await using var session = await Start("abc", server => server.On("textDocument/hover", _ => """{"contents":"x"}"""));
        using var other = LspTestSession.CreateModel("abc", new Uri("file:///work/other.ts"));

        var provider = session.Features.HoverProvider.Ordered(other).Single();

        (await provider.ProvideHover(other, new TextPosition(1, 1), CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task Completion_With_Resolve()
    {
        await using var session = await Start("console.lo", server => server
            .On("textDocument/completion", parameters =>
            {
                parameters.GetProperty("context").GetProperty("triggerKind").GetInt32().ShouldBe(2);
                parameters.GetProperty("context").GetProperty("triggerCharacter").GetString().ShouldBe(".");
                parameters.GetProperty("position").GetProperty("character").GetInt32().ShouldBe(10);
                return """{"isIncomplete":true,"itemDefaults":{"data":{"id":7}},"items":[{"label":"log","kind":2},{"label":"error","kind":3,"data":{"id":8}}]}""";
            })
            .On("completionItem/resolve", item => $$$"""{"label":"log","kind":2,"documentation":{"kind":"markdown","value":"Logs {{{item.GetProperty("data").GetProperty("id").GetInt32()}}}"}}"""));
        await session.Synced();
        var provider = session.Features.CompletionProvider.Ordered(session.Model).Single();

        var list = await provider.ProvideCompletionItems(session.Model, new TextPosition(1, 11),
            new CompletionContext(CompletionTriggerKind.TriggerCharacter) { TriggerCharacter = "." }, CancellationToken.None);

        list!.Incomplete.ShouldBeTrue();
        list.Suggestions.Select(s => s.Label.Label).ShouldBe(["log", "error"]);
        var log = list.Suggestions[0];
        log.Kind.ShouldBe(CompletionItemKind.Method);
        log.InsertText.ShouldBe("log");
        // Without an edit range the item replaces the word before the cursor.
        log.Range.Insert.ShouldBe(new TextRange(1, 9, 1, 11));
        provider.SupportsResolveCompletionItem.ShouldBeTrue();

        var resolved = await provider.ResolveCompletionItem(log, CancellationToken.None);

        resolved!.Documentation!.Value.Content.ShouldBe("Logs 7");
        resolved.Range.ShouldBe(log.Range);
    }

    [Fact]
    public async Task Hover()
    {
        await using var session = await Start("let x", server => server.On("textDocument/hover", _ =>
            $$$"""{"contents":{"kind":"markdown","value":"```ts\nlet x: number\n```"},"range":{{{Range01}}}}"""));
        var provider = session.Features.HoverProvider.Ordered(session.Model).Single();

        var hover = await provider.ProvideHover(session.Model, new TextPosition(1, 5), CancellationToken.None);

        hover!.Contents.Single().Value.ShouldStartWith("```ts");
        hover.Range.ShouldBe(new TextRange(1, 1, 1, 2));
    }

    [Fact]
    public async Task A_Failing_Request_Gives_Nothing_And_Is_Logged()
    {
        await using var session = await Start("let x", server => server.On("textDocument/hover", _ => throw new Exception("boom")));
        var logged = new List<string>();
        session.Client.LogMessage += (_, e) => logged.Add(e.Message);
        var provider = session.Features.HoverProvider.Ordered(session.Model).Single();

        (await provider.ProvideHover(session.Model, new TextPosition(1, 1), CancellationToken.None)).ShouldBeNull();
        logged.ShouldContain(message => message.Contains("textDocument/hover") && message.Contains("boom"));
    }

    [Fact]
    public async Task Signature_Help_Sends_Its_Context()
    {
        await using var session = await Start("f(1, ", server => server.On("textDocument/signatureHelp", parameters =>
        {
            var context = parameters.GetProperty("context");
            context.GetProperty("isRetrigger").GetBoolean().ShouldBeTrue();
            context.GetProperty("activeSignatureHelp").GetProperty("signatures")[0].GetProperty("label").GetString().ShouldBe("f(a)");
            return """{"signatures":[{"label":"f(a, b)","parameters":[{"label":"a"},{"label":"b"}]}],"activeSignature":0,"activeParameter":1}""";
        }));
        var provider = session.Features.SignatureHelpProvider.Ordered(session.Model).Single();
        var previous = new SignatureHelp([new SignatureInformation("f(a)", [new ParameterInformation { Label = "a" }])], 0, 0);

        var result = await provider.ProvideSignatureHelp(session.Model, new TextPosition(1, 6), CancellationToken.None,
            new SignatureHelpContext(SignatureHelpTriggerKind.TriggerCharacter, true) { TriggerCharacter = ",", ActiveSignatureHelp = previous });

        result!.Value.ActiveParameter.ShouldBe(1);
        result.Value.Signatures.Single().Parameters.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Definitions_References_And_Highlights()
    {
        await using var session = await Start("abc", server => server
            .On("textDocument/definition", _ => $$$"""{"uri":"file:///work/lib.ts","range":{{{Range01}}}}""")
            .On("textDocument/implementation", _ => $$$"""[{"targetUri":"file:///work/impl.ts","targetRange":{{{Range01}}},"targetSelectionRange":{{{Range01}}}}]""")
            .On("textDocument/typeDefinition", _ => "null")
            .On("textDocument/declaration", _ => "[]")
            .On("textDocument/references", parameters =>
            {
                parameters.GetProperty("context").GetProperty("includeDeclaration").GetBoolean().ShouldBeTrue();
                return $$$"""[{"uri":"file:///work/main.ts","range":{{{Range01}}}},{"uri":"file:///work/b.ts","range":{{{Range01}}}}]""";
            })
            .On("textDocument/documentHighlight", _ => $$$"""[{"range":{{{Range01}}},"kind":3},{"range":{{{Range01}}}}]"""));
        var model = session.Model;
        var position = new TextPosition(1, 1);
        var token = CancellationToken.None;

        (await session.Features.DefinitionProvider.Ordered(model).Single().ProvideDefinition(model, position, token))!.Single().Uri.ShouldBe(new Uri("file:///work/lib.ts"));
        (await session.Features.ImplementationProvider.Ordered(model).Single().ProvideImplementation(model, position, token))!.Single().Uri.ShouldBe(new Uri("file:///work/impl.ts"));
        (await session.Features.TypeDefinitionProvider.Ordered(model).Single().ProvideTypeDefinition(model, position, token)).ShouldBeNull();
        (await session.Features.DeclarationProvider.Ordered(model).Single().ProvideDeclaration(model, position, token))!.ShouldBeEmpty();
        (await session.Features.ReferenceProvider.Ordered(model).Single().ProvideReferences(model, position, new ReferenceContext(true), token))!.Count.ShouldBe(2);
        var highlights = await session.Features.DocumentHighlightProvider.Ordered(model).Single().ProvideDocumentHighlights(model, position, token);
        highlights!.Select(h => h.Kind).ShouldBe([DocumentHighlightKind.Write, DocumentHighlightKind.Text]);
    }

    [Fact]
    public async Task Rename_With_Prepare()
    {
        await using var session = await Start("let value = 1;\nvalue++;", server => server
            .On("textDocument/prepareRename", parameters => parameters.GetProperty("position").GetProperty("line").GetInt32() == 0
                ? """{"range":{"start":{"line":0,"character":4},"end":{"line":0,"character":9}},"placeholder":"value"}"""
                : "null")
            .On("textDocument/rename", parameters => $$$"""
                {"changes":{"file:///work/main.ts":[
                  {"range":{"start":{"line":0,"character":4},"end":{"line":0,"character":9}},"newText":"{{{parameters.GetProperty("newName").GetString()}}}"},
                  {"range":{"start":{"line":1,"character":0},"end":{"line":1,"character":5}},"newText":"{{{parameters.GetProperty("newName").GetString()}}}"}]}}
                """));
        var provider = session.Features.RenameProvider.Ordered(session.Model).Single();
        provider.SupportsResolveRenameLocation.ShouldBeTrue();

        var location = await provider.ResolveRenameLocation(session.Model, new TextPosition(1, 6), CancellationToken.None);
        var rejected = await provider.ResolveRenameLocation(session.Model, new TextPosition(2, 1), CancellationToken.None);
        var edits = await provider.ProvideRenameEdits(session.Model, new TextPosition(1, 6), "count", CancellationToken.None);

        location!.Range.ShouldBe(new TextRange(1, 5, 1, 10));
        location.Text.ShouldBe("value");
        location.RejectReason.ShouldBeNull();
        rejected!.RejectReason.ShouldNotBeNull();
        edits!.Edit.Edits.Count.ShouldBe(2);
        edits.Edit.Edits.Cast<WorkspaceTextEdit>().Select(e => e.TextEdit.Text).ShouldBe(["count", "count"]);
    }

    [Fact]
    public async Task Rename_Default_Behavior_Uses_The_Word()
    {
        await using var session = await Start("let value = 1;", server => server.On("textDocument/prepareRename", _ => """{"defaultBehavior":true}"""));
        var provider = session.Features.RenameProvider.Ordered(session.Model).Single();

        var location = await provider.ResolveRenameLocation(session.Model, new TextPosition(1, 7), CancellationToken.None);

        location!.Text.ShouldBe("value");
        location.Range.ShouldBe(new TextRange(1, 5, 1, 10));
    }

    [Fact]
    public async Task Code_Actions_Send_The_Diagnostics_And_Resolve()
    {
        await using var session = await Start("foo();\nbar();", server => server
            .On("textDocument/codeAction", parameters =>
            {
                var context = parameters.GetProperty("context");
                context.GetProperty("diagnostics").GetArrayLength().ShouldBe(1);
                context.GetProperty("diagnostics")[0].GetProperty("data").GetString().ShouldBe("d1");
                context.GetProperty("only")[0].GetString().ShouldBe("quickfix");
                return """[{"title":"Import foo","kind":"quickfix","data":"a1"},{"title":"Run","command":"fix.all"}]""";
            })
            .On("codeAction/resolve", action =>
            {
                action.GetProperty("data").GetString().ShouldBe("a1");
                return """{"title":"Import foo","kind":"quickfix","edit":{"changes":{"file:///work/main.ts":[{"range":{"start":{"line":0,"character":0},"end":{"line":0,"character":0}},"newText":"import foo;\n"}]}}}""";
            }));
        session.Server.Notify("textDocument/publishDiagnostics", """
            {"uri":"file:///work/main.ts","diagnostics":[
              {"range":{"start":{"line":0,"character":0},"end":{"line":0,"character":3}},"message":"foo?","data":"d1"},
              {"range":{"start":{"line":1,"character":0},"end":{"line":1,"character":3}},"message":"bar?","data":"d2"}]}
            """);
        await TestWait.Until(() => session.Markers.Read().Count == 2);
        var provider = session.Features.CodeActionProvider.Ordered(session.Model).Single();

        var list = await provider.ProvideCodeActions(session.Model, new TextRange(1, 1, 1, 2),
            new CodeActionContext(CodeActionTriggerType.Invoke) { Only = "quickfix" }, CancellationToken.None);

        list!.Actions.Select(a => a.Title).ShouldBe(["Import foo", "Run"]);
        list.Actions[1].Command!.Id.ShouldBe("fix.all");
        var resolved = await provider.ResolveCodeAction(list.Actions[0], CancellationToken.None);
        resolved!.Edit!.Edits.Count.ShouldBe(1);
        (await provider.ResolveCodeAction(list.Actions[1], CancellationToken.None)).ShouldBeNull();
        (await session.Client.ExecuteCommandAsync(list.Actions[1].Command!)).ShouldBeFalse();
    }

    [Fact]
    public async Task Execute_Command_Runs_On_The_Server()
    {
        await using var session = await Start("abc", server => server.On("workspace/executeCommand", parameters =>
        {
            parameters.GetProperty("command").GetString().ShouldBe("fix.all");
            parameters.GetProperty("arguments")[0].GetInt32().ShouldBe(3);
            return "null";
        }));
        var command = LspConvert.ToCommand(new Protocol.LspCommand("Fix", "fix.all") { Arguments = [JsonDocument.Parse("3").RootElement.Clone()] })!;

        (await session.Client.ExecuteCommandAsync(command)).ShouldBeTrue();
        (await session.Client.ExecuteCommandAsync(command with { Id = "unknown" })).ShouldBeFalse();
    }

    [Fact]
    public async Task Formatting_Document_Range_Ranges_And_On_Type()
    {
        const string edit = """[{"range":{"start":{"line":0,"character":0},"end":{"line":0,"character":2}},"newText":"  "}]""";
        await using var session = await Start("\tx;", server => server
            .On("textDocument/formatting", parameters =>
            {
                parameters.GetProperty("options").GetProperty("tabSize").GetInt32().ShouldBe(2);
                parameters.GetProperty("options").GetProperty("insertSpaces").GetBoolean().ShouldBeTrue();
                return edit;
            })
            .On("textDocument/rangeFormatting", _ => edit)
            .On("textDocument/rangesFormatting", parameters =>
            {
                parameters.GetProperty("ranges").GetArrayLength().ShouldBe(2);
                return edit;
            })
            .On("textDocument/onTypeFormatting", parameters =>
            {
                parameters.GetProperty("ch").GetString().ShouldBe(";");
                return "[]";
            }));
        var model = session.Model;
        var options = new FormattingOptions(2, true);
        var token = CancellationToken.None;

        var document = await session.Features.DocumentFormattingEditProvider.Ordered(model).Single().ProvideDocumentFormattingEdits(model, options, token);
        var rangeProvider = session.Features.DocumentRangeFormattingEditProvider.Ordered(model).Single();
        var range = await rangeProvider.ProvideDocumentRangeFormattingEdits(model, new TextRange(1, 1, 1, 3), options, token);
        rangeProvider.SupportsRangesFormatting.ShouldBeTrue();
        var ranges = await rangeProvider.ProvideDocumentRangesFormattingEdits(model, [new TextRange(1, 1, 1, 2), new TextRange(1, 2, 1, 3)], options, token);
        var onType = await session.Features.OnTypeFormattingEditProvider.Ordered(model).Single().ProvideOnTypeFormattingEdits(model, new TextPosition(1, 4), ";", options, token);

        document!.Single().ShouldBe(new Languages.TextEdit(new TextRange(1, 1, 1, 3), "  "));
        range!.Count.ShouldBe(1);
        ranges!.Count.ShouldBe(1);
        onType!.ShouldBeEmpty();
    }

    [Fact]
    public async Task Symbols_Folding_Selection_And_Linked_Editing()
    {
        await using var session = await Start("<div></div>", server => server
            .On("textDocument/documentSymbol", _ => $$$"""[{"name":"div","kind":8,"range":{{{Range01}}},"selectionRange":{{{Range01}}}}]""")
            .On("textDocument/foldingRange", _ => """[{"startLine":0,"endLine":2,"kind":"region"}]""")
            .On("textDocument/selectionRange", parameters =>
            {
                parameters.GetProperty("positions").GetArrayLength().ShouldBe(2);
                return $$$"""[{"range":{{{Range01}}}},{"range":{{{Range01}}},"parent":{"range":{{{Range01}}}}}]""";
            })
            .On("textDocument/linkedEditingRange", _ => """
                {"ranges":[{"start":{"line":0,"character":1},"end":{"line":0,"character":4}},{"start":{"line":0,"character":7},"end":{"line":0,"character":10}}],"wordPattern":"[a-z]+"}
                """));
        var model = session.Model;
        var token = CancellationToken.None;

        var symbols = await session.Features.DocumentSymbolProvider.Ordered(model).Single().ProvideDocumentSymbols(model, token);
        var folds = await session.Features.FoldingRangeProvider.Ordered(model).Single().ProvideFoldingRanges(model, new FoldingContext(), token);
        var selections = await session.Features.SelectionRangeProvider.Ordered(model).Single().ProvideSelectionRanges(model, [new TextPosition(1, 1), new TextPosition(1, 2)], token);
        var linked = await session.Features.LinkedEditingRangeProvider.Ordered(model).Single().ProvideLinkedEditingRanges(model, new TextPosition(1, 3), token);

        symbols!.Single().Kind.ShouldBe(SymbolKind.Field);
        folds!.Single().ShouldBe(new FoldingRange(1, 3) { Kind = FoldingRangeKind.Region });
        selections!.Select(chain => chain.Count).ShouldBe([1, 2]);
        linked!.Ranges.ShouldBe([new TextRange(1, 2, 1, 5), new TextRange(1, 8, 1, 11)]);
        linked.WordPattern!.IsMatch("div").ShouldBeTrue();
    }

    [Fact]
    public async Task Semantic_Tokens_Full_Then_Delta_And_Range()
    {
        await using var session = await Start("let a = b;", server => server
            .On("textDocument/semanticTokens/full", _ => """{"resultId":"1","data":[0,4,1,0,0]}""")
            .On("textDocument/semanticTokens/full/delta", parameters =>
            {
                parameters.GetProperty("previousResultId").GetString().ShouldBe("1");
                return """{"resultId":"2","edits":[{"start":5,"deleteCount":0,"data":[0,4,1,1,1]}]}""";
            })
            .On("textDocument/semanticTokens/range", _ => """{"data":[0,8,1,0,0]}"""));
        var provider = session.Features.DocumentSemanticTokensProvider.Ordered(session.Model).Single();

        provider.GetLegend().TokenTypes.ShouldBe(["variable", "function"]);
        provider.GetLegend().TokenModifiers.ShouldBe(["readonly"]);
        var full = (await provider.ProvideDocumentSemanticTokens(session.Model, null, CancellationToken.None))!.Value;
        var delta = (await provider.ProvideDocumentSemanticTokens(session.Model, full.Tokens!.ResultId, CancellationToken.None))!.Value;
        var range = await session.Features.DocumentRangeSemanticTokensProvider.Ordered(session.Model).Single()
            .ProvideDocumentRangeSemanticTokens(session.Model, new TextRange(1, 1, 1, 11), CancellationToken.None);

        full.Tokens.Data.ShouldBe([0u, 4u, 1u, 0u, 0u]);
        delta.Edits!.ResultId.ShouldBe("2");
        delta.Edits.Edits.Single().Data.ShouldBe([0u, 4u, 1u, 1u, 1u]);
        range!.Data.ShouldBe([0u, 8u, 1u, 0u, 0u]);
    }

    [Fact]
    public async Task Refresh_Requests_Fire_The_Change_Events()
    {
        await using var session = await Start("abc", _ => { });
        var fired = new List<string>();
        session.Features.DocumentSemanticTokensProvider.Ordered(session.Model).Single().DidChange += () => fired.Add("semantic");
        session.Features.InlayHintsProvider.Ordered(session.Model).Single().DidChangeInlayHints += () => fired.Add("inlay");
        session.Features.CodeLensProvider.Ordered(session.Model).Single().DidChange += _ => fired.Add("lens");
        session.Features.FoldingRangeProvider.Ordered(session.Model).Single().DidChange += _ => fired.Add("folding");

        await session.Server.Request("workspace/semanticTokens/refresh", "null");
        await session.Server.Request("workspace/inlayHint/refresh", "null");
        await session.Server.Request("workspace/codeLens/refresh", "null");
        await session.Server.Request("workspace/foldingRange/refresh", "null");

        fired.ShouldBe(["semantic", "inlay", "lens", "folding"]);
    }

    [Fact]
    public async Task Code_Lens_And_Inlay_Hints_Resolve()
    {
        await using var session = await Start("f(1)", server => server
            .On("textDocument/codeLens", _ => $$$"""[{"range":{{{Range01}}},"data":"lens"},{"range":{{{Range01}}},"command":{"title":"2 refs","command":"refs"}}]""")
            .On("codeLens/resolve", lens => $$$"""{"range":{{{Range01}}},"command":{"title":"resolved {{{lens.GetProperty("data").GetString()}}}","command":"x"}}""")
            .On("textDocument/inlayHint", parameters =>
            {
                parameters.GetProperty("range").GetProperty("end").GetProperty("character").GetInt32().ShouldBe(4);
                return """[{"position":{"line":0,"character":2},"label":"n:","kind":2,"data":"h"}]""";
            })
            .On("inlayHint/resolve", _ => """{"position":{"line":0,"character":2},"label":"n:","kind":2,"tooltip":"the count"}"""));
        var model = session.Model;
        var lensProvider = session.Features.CodeLensProvider.Ordered(model).Single();
        var hintProvider = session.Features.InlayHintsProvider.Ordered(model).Single();

        var lenses = await lensProvider.ProvideCodeLenses(model, CancellationToken.None);
        var resolvedLens = await lensProvider.ResolveCodeLens(model, lenses!.Lenses[0], CancellationToken.None);
        var hints = await hintProvider.ProvideInlayHints(model, new TextRange(1, 1, 1, 5), CancellationToken.None);
        var resolvedHint = await hintProvider.ResolveInlayHint(hints!.Hints[0], CancellationToken.None);

        lenses.Lenses[1].Command!.Title.ShouldBe("2 refs");
        resolvedLens!.Command!.Title.ShouldBe("resolved lens");
        hints.Hints.Single().Kind.ShouldBe(InlayHintKind.Parameter);
        hints.Hints.Single().Position.ShouldBe(new TextPosition(1, 3));
        resolvedHint!.Tooltip!.Value.Content.ShouldBe("the count");
    }

    [Fact]
    public async Task Document_Colors_And_Presentations()
    {
        await using var session = await Start("#ff0000", server => server
            .On("textDocument/documentColor", _ => """[{"range":{"start":{"line":0,"character":0},"end":{"line":0,"character":7}},"color":{"red":1,"green":0,"blue":0,"alpha":1}}]""")
            .On("textDocument/colorPresentation", parameters =>
            {
                parameters.GetProperty("color").GetProperty("red").GetDouble().ShouldBe(1);
                return """[{"label":"#ff0000"},{"label":"rgb(255, 0, 0)","textEdit":{"range":{"start":{"line":0,"character":0},"end":{"line":0,"character":7}},"newText":"rgb(255, 0, 0)"}}]""";
            }));
        var provider = session.Features.ColorProvider.Ordered(session.Model).Single();

        var colors = (await provider.ProvideDocumentColors(session.Model, CancellationToken.None))!;
        var presentations = await provider.ProvideColorPresentations(session.Model, colors.Single(), CancellationToken.None);

        colors.Single().Color.ShouldBe(new LanguageColor(1, 0, 0, 1));
        colors.Single().Range.ShouldBe(new TextRange(1, 1, 1, 8));
        presentations!.Select(p => p.Label).ShouldBe(["#ff0000", "rgb(255, 0, 0)"]);
        presentations![1].TextEdit!.Text.ShouldBe("rgb(255, 0, 0)");
    }

    [Fact]
    public async Task Cancelling_A_Provider_Call_Cancels_The_Request()
    {
        var release = new ManualResetEventSlim();
        await using var session = await Start("abc", server => server.On("textDocument/hover", _ =>
        {
            release.Wait(TimeSpan.FromSeconds(5));
            return "null";
        }));
        var provider = session.Features.HoverProvider.Ordered(session.Model).Single();
        using var cts = new CancellationTokenSource();

        var hover = provider.ProvideHover(session.Model, new TextPosition(1, 1), cts.Token);
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => hover);
        release.Set();
        await TestWait.Until(() => session.Server.Received("$/cancelRequest").Any() || session.Server.Messages.Count(m => m.Method == "textDocument/hover") == 1);
    }
}
