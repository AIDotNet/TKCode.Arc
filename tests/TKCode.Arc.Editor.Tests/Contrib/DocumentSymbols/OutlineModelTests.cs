// Tests of the outline model, ported from vscode (src/vs/editor/contrib/documentSymbols/test/browser/outlineModel.test.ts,
// commit b57d5c9e), MIT license: all five cases. The service is one per editor here (it takes the language features);
// the markers are OutlineMarker values. Own tests of create, ids and the flattened list follow.

using Shouldly;
using TKCode.Arc.Editor.Contrib.DocumentSymbols;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform.Markers;
using Xunit;

namespace TKCode.Arc.Editor.Tests.Contrib.DocumentSymbols;

[Collection(UnexpectedErrorsCollection.Name)]
public sealed class OutlineModelTests
{
    private static TextRange R(int a, int b, int c, int d) => new(a, b, c, d);

    private static DocumentSymbol FakeSymbolInformation(TextRange range, string name = "foo") =>
        new(name, "fake", SymbolKind.Boolean, range, range);

    private static OutlineMarker FakeMarker(TextRange range) => new(range, MarkerSeverity.Error);

    [Fact]
    public async Task OutlineModel_create_cached()
    {
        var features = new LanguageFeatures();
        var service = new OutlineModelService(features);
        using var model = TestTextModel.Create("foo", uri: new Uri("file:///fome/path.foo"));
        var count = 0;
        using var reg = features.DocumentSymbolProvider.Register(new LanguageFilter { Pattern = "**/path.foo" }, new SymbolProvider(_ =>
        {
            count += 1;
            return Task.FromResult<IReadOnlyList<DocumentSymbol>?>([]);
        }));

        await service.GetOrCreate(model, CancellationToken.None);
        count.ShouldBe(1);

        // cached
        await service.GetOrCreate(model, CancellationToken.None);
        count.ShouldBe(1);

        // new version
        model.ApplyEdits([EditOperation.Insert(new TextPosition(1, 1), "XXX")]);
        await service.GetOrCreate(model, CancellationToken.None);
        count.ShouldBe(2);
    }

    [Fact]
    public void OutlineModel_create_cached_cancel()
    {
        var features = new LanguageFeatures();
        var service = new OutlineModelService(features);
        using var model = TestTextModel.Create("foo", uri: new Uri("file:///fome/path.foo"));
        var isCancelled = false;

        using var reg = features.DocumentSymbolProvider.Register(new LanguageFilter { Pattern = "**/path.foo" }, new SymbolProvider(token =>
        {
            var tcs = new TaskCompletionSource<IReadOnlyList<DocumentSymbol>?>();
            token.Register(() =>
            {
                isCancelled = true;
                tcs.TrySetResult(null);
            });
            return tcs.Task;
        }));

        isCancelled.ShouldBeFalse();
        using var s1 = new CancellationTokenSource();
        _ = service.GetOrCreate(model, s1.Token);
        using var s2 = new CancellationTokenSource();
        _ = service.GetOrCreate(model, s2.Token);

        s1.Cancel();
        isCancelled.ShouldBeFalse();

        s2.Cancel();
        isCancelled.ShouldBeTrue();
    }

    [Fact]
    public void OutlineElement_updateMarker()
    {
        var e0 = new OutlineElement("foo1", null, FakeSymbolInformation(R(1, 1, 1, 10)));
        var e1 = new OutlineElement("foo2", null, FakeSymbolInformation(R(2, 1, 5, 1)));
        var e2 = new OutlineElement("foo3", null, FakeSymbolInformation(R(6, 1, 10, 10)));

        var group = new OutlineGroup("group", null, null!, 1);
        group.Children.Set(e0.Id, e0);
        group.Children.Set(e1.Id, e1);
        group.Children.Set(e2.Id, e2);

        var data = new List<OutlineMarker> { FakeMarker(R(6, 1, 6, 7)), FakeMarker(R(1, 1, 1, 4)), FakeMarker(R(10, 2, 14, 1)) };
        data.Sort(static (a, b) => TextRange.CompareRangesUsingStarts(a.Range, b.Range)); // model does this

        group.UpdateMarker(data);
        data.Count.ShouldBe(0); // all 'stolen'
        e0.Marker!.Count.ShouldBe(1);
        e1.Marker.ShouldBeNull();
        e2.Marker!.Count.ShouldBe(2);

        group.UpdateMarker([]);
        e0.Marker.ShouldBeNull();
        e1.Marker.ShouldBeNull();
        e2.Marker.ShouldBeNull();
    }

    [Fact]
    public void OutlineElement_updateMarker_2()
    {
        var p = new OutlineElement("A", null, FakeSymbolInformation(R(1, 1, 11, 1)));
        var c1 = new OutlineElement("A/B", null, FakeSymbolInformation(R(2, 4, 5, 4)));
        var c2 = new OutlineElement("A/C", null, FakeSymbolInformation(R(6, 4, 9, 4)));

        var group = new OutlineGroup("group", null, null!, 1);
        group.Children.Set(p.Id, p);
        p.Children.Set(c1.Id, c1);
        p.Children.Set(c2.Id, c2);

        var data = new List<OutlineMarker> { FakeMarker(R(2, 4, 5, 4)) };

        group.UpdateMarker(data);
        p.Marker!.Count.ShouldBe(0);
        c1.Marker!.Count.ShouldBe(1);
        c2.Marker.ShouldBeNull();

        data = [FakeMarker(R(2, 4, 5, 4)), FakeMarker(R(2, 6, 2, 8)), FakeMarker(R(7, 6, 7, 8))];
        group.UpdateMarker(data);
        p.Marker!.Count.ShouldBe(0);
        c1.Marker!.Count.ShouldBe(2);
        c2.Marker!.Count.ShouldBe(1);

        data = [FakeMarker(R(1, 4, 1, 11)), FakeMarker(R(7, 6, 7, 8))];
        group.UpdateMarker(data);
        p.Marker!.Count.ShouldBe(1);
        c1.Marker.ShouldBeNull();
        c2.Marker!.Count.ShouldBe(1);
    }

    [Fact]
    public void OutlineElement_updateMarker_multiple_groups()
    {
        var model = new TestOutlineModel();
        var g1 = new OutlineGroup("g1", model, null!, 1);
        model.Children.Set("g1", g1);
        g1.Children.Set("c1", new OutlineElement("c1", g1, FakeSymbolInformation(R(1, 1, 11, 1))));

        var g2 = new OutlineGroup("g2", model, null!, 1);
        model.Children.Set("g2", g2);
        var c2 = new OutlineElement("c2", g2, FakeSymbolInformation(R(1, 1, 7, 1)));
        g2.Children.Set("c2", c2);
        c2.Children.Set("c2.1", new OutlineElement("c2.1", c2, FakeSymbolInformation(R(1, 3, 2, 19))));
        c2.Children.Set("c2.2", new OutlineElement("c2.2", c2, FakeSymbolInformation(R(4, 1, 6, 10))));

        model.ReadyForTesting();

        var data = new List<OutlineMarker> { FakeMarker(R(1, 1, 2, 8)), FakeMarker(R(6, 1, 6, 98)) };
        model.UpdateMarker(data);

        g1.Children.Get("c1")!.Marker!.Count.ShouldBe(2);
        c2.Children.Get("c2.1")!.Marker!.Count.ShouldBe(1);
        c2.Children.Get("c2.2")!.Marker!.Count.ShouldBe(1);
    }

    // ---- own tests

    [Fact]
    public async Task Create_adopts_the_elements_of_a_single_provider_and_keeps_groups_for_several()
    {
        var features = new LanguageFeatures();
        using var model = TestTextModel.Create("a\nb\nc", "sym");
        DocumentSymbol[] symbols =
        [
            new("b", string.Empty, SymbolKind.Class, R(2, 1, 3, 2), R(2, 1, 2, 2)) { Children = [new("inner", string.Empty, SymbolKind.Method, R(3, 1, 3, 2), R(3, 1, 3, 2))] },
            new("a", string.Empty, SymbolKind.Function, R(1, 1, 1, 2), R(1, 1, 1, 2)),
            new("a", string.Empty, SymbolKind.Function, R(1, 1, 1, 2), R(1, 1, 1, 2)),
        ];
        features.DocumentSymbolProvider.Register("sym", new SymbolProvider(_ => Task.FromResult<IReadOnlyList<DocumentSymbol>?>(symbols)));
        features.DocumentSymbolProvider.Register("sym", new SymbolProvider(_ => Task.FromResult<IReadOnlyList<DocumentSymbol>?>([])));

        var outline = await OutlineModel.Create(features.DocumentSymbolProvider, model, default);
        // the empty provider's group is dropped, the other's elements are adopted by the root
        outline.Children.Select(static c => c.Key).ShouldBe(["root/provider_1/b", "root/provider_1/a", "root/provider_1/a_1_1"]);
        outline.Children.Values.All(c => c.Parent == outline).ShouldBeTrue();
        outline.GetItemEnclosingPosition(new TextPosition(3, 1))!.Symbol.Name.ShouldBe("inner");
        outline.GetItemById("root/provider_1/b/inner").ShouldBeOfType<OutlineElement>();
        TreeElement.Size(outline).ShouldBe(5);

        outline.GetTopLevelSymbols().Select(static s => s.Name).ShouldBe(["a", "a", "b"]);
        var flat = outline.AsListOfDocumentSymbols();
        flat.Select(static s => (s.Name, s.ContainerName)).ShouldBe([("a", string.Empty), ("a", string.Empty), ("b", string.Empty), ("inner", "b")]);
        flat.All(static s => s.Children is null).ShouldBeTrue();

        features.DocumentSymbolProvider.Register("sym", new SymbolProvider(_ => Task.FromResult<IReadOnlyList<DocumentSymbol>?>([symbols[1]])));
        var two = await OutlineModel.Create(features.DocumentSymbolProvider, model, default);
        two.Children.Values.ShouldAllBe(static c => c is OutlineGroup);
        two.Children.Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_provider_that_throws_leaves_an_empty_outline()
    {
        var features = new LanguageFeatures();
        using var model = TestTextModel.Create("a", "sym");
        features.DocumentSymbolProvider.Register("sym", new SymbolProvider(_ => throw new InvalidOperationException("broken")));
        var errors = new List<Exception>();
        void OnError(Exception e) => errors.Add(e);
        UnexpectedErrors.Occurred += OnError;
        try
        {
            var outline = await OutlineModel.Create(features.DocumentSymbolProvider, model, default);
            outline.Children.Count.ShouldBe(0);
        }
        finally
        {
            UnexpectedErrors.Occurred -= OnError;
        }

        errors.Count.ShouldBe(1);
    }

    private sealed class TestOutlineModel() : OutlineModel(null)
    {
        public void ReadyForTesting()
        {
            var groups = new OutlineChildren<OutlineGroup>();
            foreach (var (key, value) in Children)
            {
                groups.Set(key, (OutlineGroup)value);
            }

            Groups = groups;
        }
    }

    private sealed class SymbolProvider(Func<CancellationToken, Task<IReadOnlyList<DocumentSymbol>?>> provide) : IDocumentSymbolProvider
    {
        public Task<IReadOnlyList<DocumentSymbol>?> ProvideDocumentSymbols(TextModel model, CancellationToken token) => provide(token);
    }
}
