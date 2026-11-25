// Tests of the port of vscode's src/vs/editor/common/languageFeatureRegistry.ts (commit b57d5c9e, MIT license). Upstream
// has no test file of its own for the registry (it is exercised through the features' tests); these cover the ordering
// rules of _updateScores and _compareByScoreAndTime, exclusive selectors, groups, removal and the change event, and the
// LanguageFeatures instance that LanguageServices carries.

using Shouldly;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class LanguageFeatureRegistryTests
{
    private sealed record Provider(string Name);

    private static TextModel Model(string languageId = "farboo", string uri = "file:///testbed/file.fb") =>
        TestTextModel.Create("text", languageId, uri: new Uri(uri));

    [Fact]
    public void Providers_that_do_not_match_are_left_out()
    {
        var registry = new LanguageFeatureRegistry<Provider>();
        registry.Register("farboo", new Provider("a"));
        registry.Register("other", new Provider("b"));
        using var model = Model();

        registry.All(model).Select(p => p.Name).ShouldBe(["a"]);
        registry.Has(model).ShouldBeTrue();
        using var other = Model("none");
        registry.Has(other).ShouldBeFalse();
        registry.AllNoModel().Count.ShouldBe(2);
    }

    [Fact]
    public void Higher_scores_come_first_then_the_newest()
    {
        var registry = new LanguageFeatureRegistry<Provider>();
        registry.Register("*", new Provider("any"));
        registry.Register("farboo", new Provider("exact1"));
        registry.Register("farboo", new Provider("exact2"));
        using var model = Model();

        registry.Ordered(model).Select(p => p.Name).ShouldBe(["exact2", "exact1", "any"]);
        registry.OrderedGroups(model).Select(g => g.Select(p => p.Name).ToArray()).ShouldBe([["exact2", "exact1"], ["any"]]);
    }

    [Fact]
    public void Builtin_providers_come_after_others_of_the_same_score()
    {
        var registry = new LanguageFeatureRegistry<Provider>();
        registry.Register("farboo", new Provider("ext1"));
        registry.Register(new LanguageFilter { Language = "farboo", IsBuiltin = true }, new Provider("builtin"));
        registry.Register("farboo", new Provider("ext2"));
        using var model = Model();

        registry.Ordered(model).Select(p => p.Name).ShouldBe(["ext2", "ext1", "builtin"]);
    }

    [Fact]
    public void An_exclusive_selector_shadows_every_other_provider_unless_recursive()
    {
        var registry = new LanguageFeatureRegistry<Provider>();
        registry.Register("farboo", new Provider("a"));
        registry.Register(new LanguageFilter { Language = "farboo", Exclusive = true }, new Provider("exclusive"));
        registry.Register("*", new Provider("b"));
        using var model = Model();

        registry.Ordered(model).Select(p => p.Name).ShouldBe(["exclusive"]);
        registry.Ordered(model, recursive: true).Select(p => p.Name).ShouldBe(["a", "b"]);
    }

    [Fact]
    public void Disposing_a_registration_removes_it_once_and_raises_the_change_event()
    {
        var registry = new LanguageFeatureRegistry<Provider>();
        var counts = new List<int>();
        registry.DidChange += counts.Add;
        var a = registry.Register("farboo", new Provider("a"));
        registry.Register("farboo", new Provider("b"));
        using var model = Model();
        registry.Ordered(model).Count.ShouldBe(2);

        a.Dispose();
        a.Dispose();

        registry.Ordered(model).Select(p => p.Name).ShouldBe(["b"]);
        counts.ShouldBe([1, 2, 1]);
    }

    [Fact]
    public void The_scores_follow_the_model_language()
    {
        var registry = new LanguageFeatureRegistry<Provider>();
        registry.Register("farboo", new Provider("a"));
        using var model = Model();
        registry.Has(model).ShouldBeTrue();

        model.SetLanguage("other");
        registry.Has(model).ShouldBeFalse();
    }

    [Fact]
    public void Resources_and_language_ids()
    {
        var registry = new LanguageFeatureRegistry<Provider>();
        registry.Register(LanguageSelector.Of("javascript", new LanguageFilter { Language = "css" }), new Provider("a"));
        registry.Register(new LanguageFilter { Scheme = "untitled" }, new Provider("b"));

        registry.RegisteredLanguageIds.Order().ShouldBe(["css", "javascript"]);
        registry.HasForResource(new Uri("file:///a.css"), "css").ShouldBeTrue();
        registry.HasForResource(new Uri("file:///a.cs"), "csharp").ShouldBeFalse();
        registry.HasForResource(new Uri("untitled:Untitled-1"), "csharp").ShouldBeTrue();
    }

    [Fact]
    public void A_notebook_resolver_scores_against_the_notebook()
    {
        var notebook = new Uri("file:///book.ipynb");
        var registry = new LanguageFeatureRegistry<Provider>(_ => new NotebookInfo(notebook, "jupyter"));
        registry.Register(new LanguageFilter { NotebookType = "jupyter", Scheme = "file" }, new Provider("a"));
        using var model = Model("python", "vscode-notebook-cell:///book.ipynb#cell1");

        registry.All(model).Select(p => p.Name).ShouldBe(["a"]);
    }

    [Fact]
    public void Language_services_carry_one_set_of_registries()
    {
        using var services = new LanguageServices();
        var hover = new HoverProvider();
        using var registration = services.LanguageFeatures.HoverProvider.Register("plaintext", hover);
        using var model = Model("plaintext");

        services.LanguageFeatures.HoverProvider.Ordered(model).ShouldBe([hover]);
        services.LanguageFeatures.CompletionProvider.Has(model).ShouldBeFalse();
    }

    [Fact]
    public async Task Optional_provider_members_have_defaults()
    {
        ICompletionItemProvider provider = new CompletionProvider();
        provider.TriggerCharacters.ShouldBeNull();
        provider.SupportsResolveCompletionItem.ShouldBeFalse();
        var item = new CompletionItem { Label = "a", InsertText = "a", Range = new TextRange(1, 1, 1, 1) };
        (await provider.ResolveCompletionItem(item, CancellationToken.None)).ShouldBeNull();
        item.Label.Label.ShouldBe("a");
        item.Range.Insert.ShouldBe(item.Range.Replace);
    }

    private sealed class HoverProvider : IHoverProvider
    {
        public Task<Hover?> ProvideHover(TextModel model, TextPosition position, CancellationToken token, HoverContext? context = null) =>
            Task.FromResult<Hover?>(new Hover([new MarkdownString("hi")]));
    }

    private sealed class CompletionProvider : ICompletionItemProvider
    {
        public Task<CompletionList?> ProvideCompletionItems(TextModel model, TextPosition position, CompletionContext context, CancellationToken token) =>
            Task.FromResult<CompletionList?>(new CompletionList([]));
    }
}
