// Ported from vscode (src/vs/editor/contrib/gotoSymbol/test/browser/referencesModel.test.ts, commit b57d5c9e), MIT
// license. The one upstream test (nearestReference); URI.file(path) is a file:// Uri. Below it, tests of the grouping,
// navigation, previews and the goToSymbol requests written for this port.

using Shouldly;
using TKCode.Arc.Editor.Contrib.GotoSymbol;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class ReferencesModelTests
{
    private static Uri File(string path) => new("file://" + path);

    private static LocationLink Link(string path, int line, int column = 1, int endColumn = 1) =>
        new(File(path), new TextRange(line, column, line, endColumn));

    [Fact]
    public void NearestReference()
    {
        var model = new ReferencesModel(
        [
            new LocationLink(File("/out/obj/can"), new TextRange(1, 1, 1, 1)),
            new LocationLink(File("/out/obj/can2"), new TextRange(1, 1, 1, 1)),
            new LocationLink(File("/src/can"), new TextRange(1, 1, 1, 1)),
        ], "FOO");

        model.NearestReference(File("/src/can"), new TextPosition(1, 1))!.Uri.AbsolutePath.ShouldBe("/src/can");
        model.NearestReference(File("/src/someOtherFileInSrc"), new TextPosition(1, 1))!.Uri.AbsolutePath.ShouldBe("/src/can");
        model.NearestReference(File("/out/someOtherFile"), new TextPosition(1, 1))!.Uri.AbsolutePath.ShouldBe("/out/obj/can");
        model.NearestReference(File("/out/obj/can2222"), new TextPosition(1, 1))!.Uri.AbsolutePath.ShouldBe("/out/obj/can2");
    }

    // ---- written for this port

    [Fact]
    public void Links_are_grouped_by_document_sorted_and_deduplicated()
    {
        var first = Link("/b", 5);
        using var model = new ReferencesModel([first, Link("/a", 3), Link("/b", 2), Link("/a", 1), Link("/b", 5)], "T");

        model.Groups.Select(g => g.Uri.AbsolutePath).ShouldBe(["/a", "/b"]);
        model.Groups[0].Children.Select(r => r.Range.StartLineNumber).ShouldBe([1, 3]);
        model.Groups[1].Children.Select(r => r.Range.StartLineNumber).ShouldBe([2, 5]);
        model.References.Count.ShouldBe(4);
        model.FirstReference()!.Link.ShouldBeSameAs(first);
        model.AriaMessage.ShouldBe("Found 4 symbols in 2 files");
        model.References.Select(r => r.Id).Distinct().Count().ShouldBe(4);
        GoToSymbol.SortedAndDeduped([Link("/b", 1), Link("/a", 1), Link("/a", 1)]).Select(l => l.Uri.AbsolutePath).ShouldBe(["/a", "/b"]);
    }

    [Fact]
    public void Next_and_previous_cycle_within_a_file_and_across_files()
    {
        using var model = new ReferencesModel([Link("/a", 1), Link("/a", 2), Link("/b", 3)], "T");
        var a1 = model.References[0];
        var a2 = model.References[1];
        var b3 = model.References[2];

        model.NextOrPreviousReference(a1, next: true).ShouldBeSameAs(a2);
        model.NextOrPreviousReference(a2, next: true).ShouldBeSameAs(b3);
        model.NextOrPreviousReference(b3, next: true).ShouldBeSameAs(a1);
        model.NextOrPreviousReference(a1, next: false).ShouldBeSameAs(b3);
        model.NextOrPreviousReference(b3, next: false).ShouldBeSameAs(a2);

        using var single = new ReferencesModel([Link("/a", 1), Link("/a", 2)], "T");
        single.NextOrPreviousReference(single.References[1], next: true).ShouldBeSameAs(single.References[0]);
        single.NextOrPreviousReference(single.References[0], next: false).ShouldBeSameAs(single.References[1]);
    }

    [Fact]
    public void ReferenceAt_and_ranges_that_move()
    {
        using var model = new ReferencesModel([new LocationLink(File("/a"), new TextRange(2, 1, 2, 10)) { TargetSelectionRange = new TextRange(2, 5, 2, 8) }], "T");
        var reference = model.References[0];
        reference.Range.ShouldBe(new TextRange(2, 5, 2, 8));
        model.ReferenceAt(File("/a"), new TextPosition(2, 6)).ShouldBeSameAs(reference);
        model.ReferenceAt(File("/a"), new TextPosition(2, 2)).ShouldBeNull();
        model.ReferenceAt(File("/b"), new TextPosition(2, 6)).ShouldBeNull();

        OneReference? moved = null;
        model.DidChangeReferenceRange += r => moved = r;
        reference.Range = new TextRange(3, 5, 3, 8);
        moved.ShouldBeSameAs(reference);
        reference.Range.StartLineNumber.ShouldBe(3);
        model.Clone().References[0].Range.ShouldBe(new TextRange(2, 5, 2, 8));
    }

    [Fact]
    public async Task Previews_show_the_word_before_the_reference_and_the_rest_of_its_line()
    {
        var uri = File("/a.ts");
        using var model = new ReferencesModel([new LocationLink(uri, new TextRange(1, 17, 1, 20))], "T");
        using var text = TestTextModel.Create("    const value = foo(bar);   \nnext", uri: uri);
        await model.Groups[0].Resolve(_ => Task.FromResult<(TextModel, bool)?>((text, false)));

        var preview = model.Groups[0].GetPreview(model.References[0])!.Preview(model.References[0].Range)!;
        preview.Value.ShouldBe("const value = foo(bar);");
        preview.Value[preview.HighlightStart..preview.HighlightEnd].ShouldBe("= f");
        model.References[0].AriaMessage.ShouldBe("const value = foo(bar); in a.ts on line 1 at column 17");
        model.Groups[0].AriaMessage.ShouldStartWith("1 symbol in a.ts");
    }

    [Fact]
    public async Task GoToSymbol_merges_the_providers_and_compacts_references()
    {
        const string lang = "gotoLang";
        using var text = TestTextModel.Create("a\nb", lang, uri: File("/m"));
        var registry = new LanguageFeatureRegistry<IDefinitionProvider>();
        using var r1 = registry.Register(lang, new Definitions([Link("/x", 1)]));
        using var r2 = registry.Register(lang, new Definitions([Link("/y", 2), new LocationLink(new Uri("walkThroughSnippet:/s"), new TextRange(1, 1, 1, 1))]));
        using var r3 = registry.Register(lang, new ThrowingDefinitions());

        var links = await GoToSymbol.GetDefinitionsAtPosition(registry, text, new TextPosition(1, 1), false, CancellationToken.None);
        links.Select(l => l.Uri.AbsolutePath).ShouldBe(["/y", "/x"]);

        var references = new LanguageFeatureRegistry<IReferenceProvider>();
        using var r4 = references.Register(lang, new References());
        (await GoToSymbol.GetReferencesAtPosition(references, text, new TextPosition(1, 1), compact: false, false, CancellationToken.None)).Count.ShouldBe(2);
        var compact = await GoToSymbol.GetReferencesAtPosition(references, text, new TextPosition(1, 1), compact: true, false, CancellationToken.None);
        compact.Single().Range.StartLineNumber.ShouldBe(2);
    }

    private sealed class Definitions(IReadOnlyList<LocationLink> links) : IDefinitionProvider
    {
        public Task<IReadOnlyList<LocationLink>?> ProvideDefinition(TextModel model, TextPosition position, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<LocationLink>?>(links);
    }

    private sealed class ThrowingDefinitions : IDefinitionProvider
    {
        public Task<IReadOnlyList<LocationLink>?> ProvideDefinition(TextModel model, TextPosition position, CancellationToken token) =>
            throw new InvalidOperationException("provider failed");
    }

    private sealed class References : IReferenceProvider
    {
        public Task<IReadOnlyList<Location>?> ProvideReferences(TextModel model, TextPosition position, ReferenceContext context, CancellationToken token)
        {
            var declaration = new Location(model.Uri, new TextRange(1, 1, 1, 2));
            var use = new Location(model.Uri, new TextRange(2, 1, 2, 2));
            return Task.FromResult<IReadOnlyList<Location>?>(context.IncludeDeclaration ? [declaration, use] : [use]);
        }
    }
}
