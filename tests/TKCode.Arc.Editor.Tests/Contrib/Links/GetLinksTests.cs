// Tests of the port of vscode's getLinks.ts (src/vs/editor/contrib/links/browser/getLinks.ts, commit b57d5c9e; MIT
// license): merging the links of several providers and resolving a link. Upstream has no tests for it.

using Shouldly;
using TKCode.Arc.Editor.Contrib.Links;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class GetLinksTests
{
    private sealed class FixedLinks(params Link[] links) : ILinkProvider
    {
        public int Disposed { get; private set; }

        public Func<Link, Link?>? Resolver { get; init; }

        public bool SupportsResolveLink => Resolver is not null;

        public Task<LinksList?> ProvideLinks(TextModel model, CancellationToken token) =>
            Task.FromResult<LinksList?>(new LinksList(links) { Dispose = () => Disposed++ });

        public Task<Link?> ResolveLink(Link link, CancellationToken token) => Task.FromResult(Resolver!(link));
    }

    private sealed class Throwing : ILinkProvider
    {
        public Task<LinksList?> ProvideLinks(TextModel model, CancellationToken token) => throw new InvalidOperationException("boom");
    }

    private static Link L(int startColumn, int endColumn, string url) =>
        new(new TextRange(1, startColumn, 1, endColumn)) { Url = new Uri(url) };

    [Fact]
    public async Task Links_Of_Several_Providers_Are_Merged_And_The_Better_Provider_Wins_Where_They_Touch()
    {
        using var model = TestTextModel.Create("0123456789012345678901234567890123456789");
        var registry = new LanguageFeatureRegistry<ILinkProvider>();
        var worse = new FixedLinks(L(1, 5, "http://a"), L(10, 15, "http://b"), L(30, 35, "http://c"));
        var better = new FixedLinks(L(12, 20, "http://better"));
        using var r1 = registry.Register("*", worse);
        using var r2 = registry.Register(LanguageIds.PlaintextLanguageId, better);

        using var list = await LinksComputer.GetLinks(registry, model, CancellationToken.None);

        list.Links.Select(static l => l.Url!.OriginalString).ShouldBe(["http://a", "http://better", "http://c"]);
        list.Dispose();
        worse.Disposed.ShouldBe(1);
        better.Disposed.ShouldBe(1);
    }

    [Fact]
    public async Task A_Failing_Provider_Is_Reported_And_The_Others_Still_Count()
    {
        using var model = TestTextModel.Create("abc");
        var registry = new LanguageFeatureRegistry<ILinkProvider>();
        using var r1 = registry.Register("*", new Throwing());
        using var r2 = registry.Register("*", new FixedLinks(L(1, 2, "http://ok")));
        var errors = new List<Exception>();

        using var list = await LinksComputer.GetLinks(registry, model, CancellationToken.None, errors.Add);

        list.Links.ShouldHaveSingleItem();
        errors.ShouldHaveSingleItem().Message.ShouldBe("boom");
    }

    [Fact]
    public async Task A_Cancelled_Request_Gives_No_Links()
    {
        using var model = TestTextModel.Create("abc");
        var registry = new LanguageFeatureRegistry<ILinkProvider>();
        using var r = registry.Register("*", new FixedLinks(L(1, 2, "http://x")));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        using var list = await LinksComputer.GetLinks(registry, model, cancellation.Token);
        list.Links.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_Link_Without_Target_Is_Resolved_By_Its_Provider_Or_Is_Missing()
    {
        var unresolved = new Link(new TextRange(1, 1, 1, 3));
        var resolving = new FixedLinks(unresolved) { Resolver = l => l with { Url = new Uri("https://resolved.example/") } };
        (await new ProviderLink(unresolved, resolving).Resolve(CancellationToken.None)).Uri.ShouldBe(new Uri("https://resolved.example/"));

        var notResolving = new FixedLinks(unresolved);
        await Should.ThrowAsync<LinkResolveException>(() => new ProviderLink(unresolved, notResolving).Resolve(CancellationToken.None));

        var resolvingToNothing = new FixedLinks(unresolved) { Resolver = static _ => null };
        (await Should.ThrowAsync<LinkResolveException>(() => new ProviderLink(unresolved, resolvingToNothing).Resolve(CancellationToken.None))).Message.ShouldBe("missing");

        var text = new Link(new TextRange(1, 1, 1, 3)) { UrlText = "not a uri" };
        (await new ProviderLink(text, notResolving).Resolve(CancellationToken.None)).ToString().ShouldBe("not a uri");
    }
}
