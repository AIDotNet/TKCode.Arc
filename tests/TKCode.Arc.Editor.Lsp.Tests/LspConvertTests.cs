// The protocol <-> engine conversions. Written for this package.

using System.Text.Json;
using Shouldly;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Lsp.Protocol;
using TKCode.Arc.Editor.Platform.Markers;
using Xunit;

namespace TKCode.Arc.Editor.Lsp.Tests;

public sealed class LspConvertTests
{
    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static T Read<T>(string json, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo) => JsonSerializer.Deserialize(json, typeInfo)!;

    [Fact]
    public void Positions_Are_Zero_Based_In_The_Protocol()
    {
        LspConvert.ToPosition(new LspPosition(0, 0)).ShouldBe(new TextPosition(1, 1));
        LspConvert.ToPosition(new LspPosition(4, 7)).ShouldBe(new TextPosition(5, 8));
        LspConvert.FromPosition(new TextPosition(5, 8)).ShouldBe(new LspPosition(4, 7));
        LspConvert.ToRange(new LspRange(new LspPosition(1, 2), new LspPosition(3, 4))).ShouldBe(new TextRange(2, 3, 4, 5));
        LspConvert.FromRange(new TextRange(2, 3, 4, 5)).ShouldBe(new LspRange(new LspPosition(1, 2), new LspPosition(3, 4)));
    }

    [Fact]
    public void Uri_Keys_Ignore_Encoding_Differences()
    {
        LspConvert.UriKey("file:///c%3A/a%20b.ts").ShouldBe(LspConvert.UriKey("file:///C:/a b.ts"));
        LspConvert.FromUri(new Uri("file:///work/a b.ts")).ShouldBe("file:///work/a%20b.ts");
    }

    [Fact]
    public void Hover_Contents_Become_Markdown()
    {
        var plain = LspConvert.ToMarkdownStrings(Json("""{"kind":"plaintext","value":"a*b"}"""));
        plain.Single().Value.ShouldBe(@"a\*b");

        var markdown = LspConvert.ToMarkdownStrings(Json("""{"kind":"markdown","value":"**x**"}"""));
        markdown.Single().Value.ShouldBe("**x**");

        var marked = LspConvert.ToMarkdownStrings(Json("""["text",{"language":"ts","value":"let x"},""]"""));
        marked.Select(m => m.Value).ShouldBe(["text", "```ts\nlet x\n```"]);
    }

    [Fact]
    public void Documentation_Keeps_Plain_Text_As_Text()
    {
        var plain = LspConvert.ToDocumentation(Json("""{"kind":"plaintext","value":"doc"}"""))!.Value;
        plain.IsMarkdown.ShouldBeFalse();
        plain.Value.ShouldBe("doc");

        LspConvert.ToDocumentation(Json("\"s\""))!.Value.Value.ShouldBe("s");
        LspConvert.ToDocumentation(Json("""{"kind":"markdown","value":"_m_"}"""))!.Value.IsMarkdown.ShouldBeTrue();
        LspConvert.ToDocumentation(null).ShouldBeNull();
    }

    [Fact]
    public void A_Diagnostic_Becomes_A_Marker()
    {
        var diagnostic = Read("""
            {"range":{"start":{"line":2,"character":4},"end":{"line":2,"character":9}},"severity":2,"code":6133,
             "codeDescription":{"href":"https://example.com/6133"},"source":"ts","message":"unused","tags":[1,7],
             "relatedInformation":[{"location":{"uri":"file:///a.ts","range":{"start":{"line":0,"character":0},"end":{"line":0,"character":1}}},"message":"here"}]}
            """, LspJsonContext.Default.LspDiagnostic);

        var marker = LspConvert.ToMarkerData(diagnostic);

        marker.Severity.ShouldBe(MarkerSeverity.Warning);
        marker.Code.ShouldBe("6133");
        marker.CodeTarget.ShouldBe(new Uri("https://example.com/6133"));
        marker.Source.ShouldBe("ts");
        marker.Message.ShouldBe("unused");
        (marker.StartLineNumber, marker.StartColumn, marker.EndLineNumber, marker.EndColumn).ShouldBe((3, 5, 3, 10));
        marker.Tags.ShouldBe([MarkerTag.Unnecessary]);
        marker.RelatedInformation!.Single().Message.ShouldBe("here");
    }

    [Fact]
    public void Severity_Defaults_To_Error()
    {
        LspConvert.ToSeverity(null).ShouldBe(MarkerSeverity.Error);
        LspConvert.ToSeverity(3).ShouldBe(MarkerSeverity.Info);
        LspConvert.ToSeverity(4).ShouldBe(MarkerSeverity.Hint);
    }

    [Fact]
    public void Workspace_Edit_Document_Changes_Win_Over_Changes()
    {
        var edit = Read("""
            {"changes":{"file:///ignored.ts":[{"range":{"start":{"line":0,"character":0},"end":{"line":0,"character":0}},"newText":"x"}]},
             "documentChanges":[
               {"textDocument":{"uri":"file:///a.ts","version":3},"edits":[{"range":{"start":{"line":0,"character":1},"end":{"line":0,"character":2}},"newText":"y"}]},
               {"kind":"rename","oldUri":"file:///a.ts","newUri":"file:///b.ts","options":{"overwrite":true}},
               {"kind":"create","uri":"file:///c.ts"},
               {"kind":"delete","uri":"file:///d.ts"}]}
            """, LspJsonContext.Default.LspWorkspaceEdit);

        var converted = LspConvert.ToWorkspaceEdit(edit);

        converted.Edits.Count.ShouldBe(4);
        var text = converted.Edits[0].ShouldBeOfType<WorkspaceTextEdit>();
        text.Resource.ShouldBe(new Uri("file:///a.ts"));
        text.VersionId.ShouldBe(3);
        text.TextEdit.ShouldBe(new Languages.TextEdit(new TextRange(1, 2, 1, 3), "y"));
        var rename = converted.Edits[1].ShouldBeOfType<WorkspaceFileEdit>();
        (rename.OldResource, rename.NewResource).ShouldBe((new Uri("file:///a.ts"), new Uri("file:///b.ts")));
        rename.Options!.Overwrite.ShouldBeTrue();
        converted.Edits[2].ShouldBeOfType<WorkspaceFileEdit>().OldResource.ShouldBeNull();
        converted.Edits[3].ShouldBeOfType<WorkspaceFileEdit>().NewResource.ShouldBeNull();
    }

    [Fact]
    public void Workspace_Edit_Changes_Map()
    {
        var edit = Read("""
            {"changes":{"file:///a.ts":[{"range":{"start":{"line":1,"character":0},"end":{"line":1,"character":3}},"newText":"let"}]}}
            """, LspJsonContext.Default.LspWorkspaceEdit);

        var text = LspConvert.ToWorkspaceEdit(edit).Edits.Single().ShouldBeOfType<WorkspaceTextEdit>();

        text.TextEdit.Range.ShouldBe(new TextRange(2, 1, 2, 4));
        text.VersionId.ShouldBeNull();
    }

    [Fact]
    public void Definition_Results_Of_Every_Shape_Become_Links()
    {
        const string range = """{"start":{"line":0,"character":0},"end":{"line":0,"character":3}}""";
        LspConvert.ToLocationLinks(Json($$"""{"uri":"file:///a.ts","range":{{range}}}"""))!.Single().Uri.ShouldBe(new Uri("file:///a.ts"));
        LspConvert.ToLocationLinks(Json($$"""[{"uri":"file:///a.ts","range":{{range}}},{"uri":"file:///b.ts","range":{{range}}}]"""))!.Count.ShouldBe(2);
        var link = LspConvert.ToLocationLinks(Json($$"""[{"targetUri":"file:///c.ts","targetRange":{{range}},"targetSelectionRange":{{range}},"originSelectionRange":{{range}}}]"""))!.Single();
        link.Uri.ShouldBe(new Uri("file:///c.ts"));
        link.OriginSelectionRange.ShouldBe(new TextRange(1, 1, 1, 4));
        LspConvert.ToLocationLinks(Json("null")).ShouldBeNull();
        LspConvert.ToLocationLinks(Json("[]"))!.ShouldBeEmpty();
    }

    [Fact]
    public void Completion_Items_Take_Their_Range_From_The_Edit_The_Defaults_Or_The_Word()
    {
        var word = new CompletionItemRanges(new TextRange(1, 1, 1, 3), new TextRange(1, 1, 1, 5));

        var plain = LspConvert.ToCompletionItem(Read("""{"label":"log","kind":2,"detail":"(x)","insertText":"log()"}""", LspJsonContext.Default.LspCompletionItem), null, word);
        plain.InsertText.ShouldBe("log()");
        plain.Kind.ShouldBe(CompletionItemKind.Method);
        plain.Range.ShouldBe(word);
        plain.InsertTextRules.ShouldBe(CompletionItemInsertTextRule.None);

        var withEdit = LspConvert.ToCompletionItem(Read("""
            {"label":"for","insertTextFormat":2,"textEdit":{"newText":"for ($1) {}","range":{"start":{"line":0,"character":0},"end":{"line":0,"character":2}}},"tags":[1]}
            """, LspJsonContext.Default.LspCompletionItem), null, word);
        withEdit.InsertText.ShouldBe("for ($1) {}");
        withEdit.InsertTextRules.ShouldBe(CompletionItemInsertTextRule.InsertAsSnippet);
        withEdit.Range.Insert.ShouldBe(new TextRange(1, 1, 1, 3));
        withEdit.Tags.ShouldBe([CompletionItemTag.Deprecated]);

        var insertReplace = LspConvert.ToCompletionItem(Read("""
            {"label":"x","textEdit":{"newText":"xy","insert":{"start":{"line":0,"character":0},"end":{"line":0,"character":1}},"replace":{"start":{"line":0,"character":0},"end":{"line":0,"character":4}}}}
            """, LspJsonContext.Default.LspCompletionItem), null, word);
        insertReplace.Range.ShouldBe(new CompletionItemRanges(new TextRange(1, 1, 1, 2), new TextRange(1, 1, 1, 5)));

        var defaults = Read("""{"editRange":{"start":{"line":0,"character":1},"end":{"line":0,"character":2}},"insertTextFormat":2,"commitCharacters":["."]}""",
            LspJsonContext.Default.LspCompletionItemDefaults);
        var fromDefaults = LspConvert.ToCompletionItem(Read("""{"label":"a","textEditText":"a$0","labelDetails":{"detail":"()","description":"mod"}}""", LspJsonContext.Default.LspCompletionItem), defaults, word);
        fromDefaults.InsertText.ShouldBe("a$0");
        fromDefaults.Range.ShouldBe(new CompletionItemRanges(new TextRange(1, 2, 1, 3), new TextRange(1, 2, 1, 3)));
        fromDefaults.InsertTextRules.ShouldBe(CompletionItemInsertTextRule.InsertAsSnippet);
        fromDefaults.CommitCharacters.ShouldBe(["."]);
        fromDefaults.Label.Detail.ShouldBe("()");
        fromDefaults.Label.Description.ShouldBe("mod");
    }

    [Fact]
    public void Completion_Kinds_Map_From_The_Protocol()
    {
        LspConvert.ToCompletionItemKind(1).ShouldBe(CompletionItemKind.Text);
        LspConvert.ToCompletionItemKind(7).ShouldBe(CompletionItemKind.Class);
        LspConvert.ToCompletionItemKind(15).ShouldBe(CompletionItemKind.Snippet);
        LspConvert.ToCompletionItemKind(25).ShouldBe(CompletionItemKind.TypeParameter);
        LspConvert.ToCompletionItemKind(null).ShouldBe(CompletionItemKind.Property);
    }

    [Fact]
    public void Document_Symbols_In_Both_Shapes()
    {
        const string range = """{"start":{"line":0,"character":0},"end":{"line":4,"character":1}}""";
        var tree = LspConvert.ToDocumentSymbols(Json($$"""
            [{"name":"C","kind":5,"range":{{range}},"selectionRange":{{range}},"children":[{"name":"m","detail":"()","kind":6,"range":{{range}},"selectionRange":{{range}},"deprecated":true}]}]
            """))!;
        tree.Single().Kind.ShouldBe(SymbolKind.Class);
        var child = tree.Single().Children!.Single();
        child.Kind.ShouldBe(SymbolKind.Method);
        child.Detail.ShouldBe("()");
        child.Tags.ShouldBe([SymbolTag.Deprecated]);

        var flat = LspConvert.ToDocumentSymbols(Json($$"""
            [{"name":"f","kind":12,"location":{"uri":"file:///a.ts","range":{{range}}},"containerName":"mod"}]
            """))!;
        flat.Single().Kind.ShouldBe(SymbolKind.Function);
        flat.Single().ContainerName.ShouldBe("mod");
        flat.Single().Range.ShouldBe(new TextRange(1, 1, 5, 2));
    }

    [Fact]
    public void Signature_Help_Parameters_By_Label_Or_Offsets_Round_Trip()
    {
        var help = LspConvert.ToSignatureHelp(Read("""
            {"signatures":[{"label":"f(a, b)","documentation":"does f","parameters":[{"label":"a"},{"label":[5,6],"documentation":{"kind":"markdown","value":"*b*"}}]}],"activeSignature":0,"activeParameter":1}
            """, LspJsonContext.Default.LspSignatureHelp));

        help.ActiveParameter.ShouldBe(1);
        var signature = help.Signatures.Single();
        signature.Parameters[0].Label.ShouldBe("a");
        signature.Parameters[1].LabelOffsets.ShouldBe((5, 6));
        signature.Parameters[1].Documentation!.Value.IsMarkdown.ShouldBeTrue();

        var back = LspConvert.FromSignatureHelp(help);
        back.Signatures[0].Parameters![1].Label.ValueKind.ShouldBe(JsonValueKind.Array);
        back.Signatures[0].Parameters![0].Label.GetString().ShouldBe("a");
        back.ActiveParameter.ShouldBe(1);
    }

    [Fact]
    public void Code_Actions_And_Bare_Commands()
    {
        var command = LspConvert.ToCodeAction(Json("""{"title":"Run","command":"run.it","arguments":[1]}"""))!;
        command.Title.ShouldBe("Run");
        command.Command!.Id.ShouldBe("run.it");
        command.Command.Arguments!.Count.ShouldBe(1);

        var action = LspConvert.ToCodeAction(Json("""
            {"title":"Fix","kind":"quickfix","isPreferred":true,"disabled":{"reason":"no"},
             "edit":{"changes":{"file:///a.ts":[{"range":{"start":{"line":0,"character":0},"end":{"line":0,"character":0}},"newText":"x"}]}},
             "command":{"title":"after","command":"after.fix"}}
            """))!;
        action.Kind.ShouldBe("quickfix");
        action.IsPreferred.ShouldBeTrue();
        action.Disabled.ShouldBe("no");
        action.Edit!.Edits.Count.ShouldBe(1);
        action.Command!.Id.ShouldBe("after.fix");
    }

    [Fact]
    public void Inlay_Hints_With_A_String_Or_Parts()
    {
        var simple = LspConvert.ToInlayHint(Read("""{"position":{"line":1,"character":5},"label":": number","kind":1,"paddingLeft":true}""", LspJsonContext.Default.LspInlayHint));
        simple.Position.ShouldBe(new TextPosition(2, 6));
        simple.Label.ShouldBe(": number");
        simple.Kind.ShouldBe(InlayHintKind.Type);
        simple.PaddingLeft.ShouldBeTrue();

        var parts = LspConvert.ToInlayHint(Read("""
            {"position":{"line":0,"character":0},"label":[{"value":"x:","tooltip":"param"},{"value":"T","location":{"uri":"file:///t.ts","range":{"start":{"line":0,"character":0},"end":{"line":0,"character":1}}}}],"kind":2}
            """, LspJsonContext.Default.LspInlayHint));
        parts.Label.ShouldBeNull();
        parts.LabelParts!.Select(p => p.Label).ShouldBe(["x:", "T"]);
        parts.LabelParts![1].Location!.Uri.ShouldBe(new Uri("file:///t.ts"));
        parts.Kind.ShouldBe(InlayHintKind.Parameter);
    }

    [Fact]
    public void Folding_And_Selection_Ranges()
    {
        var folds = LspConvert.ToFoldingRanges(Read("""[{"startLine":0,"endLine":3,"kind":"comment"},{"startLine":5,"endLine":4}]""", LspJsonContext.Default.LspFoldingRangeArray));
        folds.Single().ShouldBe(new FoldingRange(1, 4) { Kind = FoldingRangeKind.Comment });

        var selections = LspConvert.ToSelectionRanges(Read("""
            [{"range":{"start":{"line":0,"character":2},"end":{"line":0,"character":3}},"parent":{"range":{"start":{"line":0,"character":0},"end":{"line":0,"character":9}}}}]
            """, LspJsonContext.Default.LspSelectionRangeArray));
        selections.Single().Select(s => s.Range).ShouldBe([new TextRange(1, 3, 1, 4), new TextRange(1, 1, 1, 10)]);
    }

    [Fact]
    public void Line_Endings_Follow_The_Model()
    {
        LspConvert.NormalizeEol("a\r\nb\nc\rd", "\n").ShouldBe("a\nb\nc\nd");
        LspConvert.NormalizeEol("a\nb", "\r\n").ShouldBe("a\r\nb");
    }
}
