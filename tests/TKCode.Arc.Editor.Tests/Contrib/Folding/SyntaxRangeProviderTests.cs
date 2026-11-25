// Tests of SyntaxRangeProvider (src/vs/editor/contrib/folding/browser/syntaxRangeProvider.ts, commit b57d5c9e)
// written for this port: sanitizing, kinds, provider errors and the fallback, change events. Upstream's one test
// (syntaxFold.test.ts, limit by nesting level) is ported in HiddenRangeModelTests.cs.

using Shouldly;
using TKCode.Arc.Editor.Contrib.Folding;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

[Collection(UnexpectedErrorsCollection.Name)]
public sealed class SyntaxRangeProviderTests
{
    private sealed class TestFoldingRangeProvider(TextModel model, IReadOnlyList<FoldingRange> ranges) : IFoldingRangeProvider
    {
        public event Action<IFoldingRangeProvider>? DidChange;

        public Task<IReadOnlyList<FoldingRange>?> ProvideFoldingRanges(TextModel m, FoldingContext context, CancellationToken token) =>
            Task.FromResult(m == model ? ranges : null);

        public void Fire() => DidChange?.Invoke(this);
    }

    private sealed class ThrowingProvider : IFoldingRangeProvider
    {
        public Task<IReadOnlyList<FoldingRange>?> ProvideFoldingRanges(TextModel model, FoldingContext context, CancellationToken token) =>
            throw new InvalidOperationException("boom");
    }

    private sealed class FixedRangeProvider(FoldingRegions regions) : IRangeProvider
    {
        public bool Disposed { get; private set; }

        public string Id => "fixed";

        public Task<FoldingRegions?> Compute(CancellationToken cancellationToken) => Task.FromResult<FoldingRegions?>(regions);

        public void Dispose() => Disposed = true;
    }

    private static FoldingRange R(int start, int end) => new(start, end);

    private static List<(int, int)> ToList(FoldingRegions? regions)
    {
        var actual = new List<(int, int)>();
        if (regions is not null)
        {
            for (var i = 0; i < regions.Length; i++)
            {
                actual.Add((regions.GetStartLineNumber(i), regions.GetEndLineNumber(i)));
            }
        }

        return actual;
    }

    // ---- written for this port

    [Fact]
    public async Task Invalid_and_overlapping_ranges_are_dropped_and_ranges_sorted()
    {
        using var model = TestTextModel.Create(string.Join('\n', Enumerable.Range(1, 10).Select(static i => "line " + i)));
        IFoldingRangeProvider[] providers =
        [
            new TestFoldingRangeProvider(model, [R(5, 8), R(1, 4), R(0, 3), R(6, 6), R(7, 11), R(3, 6), R(6, 7)]),
        ];
        using var provider = new SyntaxRangeProvider(model, providers, () => { }, new FoldingLimitReporter(100), null);
        var regions = await provider.Compute(CancellationToken.None);
        // (0,3), (6,6) and (7,11) are invalid; (3,6) overlaps (1,4) without nesting and is kept as upstream does (it
        // starts a new top), then (5,8) overlaps (3,6) as well
        ToList(regions).ShouldBe([(1, 4), (3, 6), (5, 8), (6, 7)]);
    }

    [Fact]
    public async Task Kinds_become_region_types()
    {
        using var model = TestTextModel.Create("/*\n*/\n#region\nx\n#endregion");
        IFoldingRangeProvider[] providers =
        [
            new TestFoldingRangeProvider(model, [R(1, 2) with { Kind = FoldingRangeKind.Comment }, R(3, 5) with { Kind = FoldingRangeKind.Region }]),
        ];
        using var provider = new SyntaxRangeProvider(model, providers, () => { }, new FoldingLimitReporter(100), null);
        var regions = (await provider.Compute(CancellationToken.None))!;
        regions.GetType(0).ShouldBe("comment");
        regions.GetType(1).ShouldBe("region");
    }

    [Fact]
    public async Task No_result_uses_the_fallback_provider()
    {
        using var model = TestTextModel.Create("a\n b\nc");
        using var other = TestTextModel.Create("x");
        var fallback = new FixedRangeProvider(new FoldingRegions([1], [2]));
        IFoldingRangeProvider[] providers = [new TestFoldingRangeProvider(other, [R(1, 2)]), new ThrowingProvider()];
        var provider = new SyntaxRangeProvider(model, providers, () => { }, new FoldingLimitReporter(100), fallback);
        var errors = new List<Exception>();
        Action<Exception> report = errors.Add;
        UnexpectedErrors.Occurred += report;
        try
        {
            ToList(await provider.Compute(CancellationToken.None)).ShouldBe([(1, 2)]);
        }
        finally
        {
            UnexpectedErrors.Occurred -= report;
        }

        errors.Count.ShouldBe(1);
        provider.Dispose();
        fallback.Disposed.ShouldBeTrue();
    }

    [Fact]
    public void Provider_changes_are_forwarded_until_disposed()
    {
        using var model = TestTextModel.Create("a");
        var testProvider = new TestFoldingRangeProvider(model, []);
        var count = 0;
        var provider = new SyntaxRangeProvider(model, [testProvider], () => count++, new FoldingLimitReporter(100), null);
        testProvider.Fire();
        count.ShouldBe(1);
        provider.Dispose();
        testProvider.Fire();
        count.ShouldBe(1);
    }
}
