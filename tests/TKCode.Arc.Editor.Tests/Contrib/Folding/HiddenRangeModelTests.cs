// Ported from vscode (src/vs/editor/contrib/folding/test/browser/hiddenRangeModel.test.ts and syntaxFold.test.ts,
// commit b57d5c9e), MIT license. Both upstream tests (hasRanges; limit by nesting level). Below them, tests of
// adjustSelections and of the syntax provider's fallback written for this port.

using Shouldly;
using TKCode.Arc.Editor.Contrib.Folding;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class HiddenRangeModelTests
{
    private static (int, int) R(int startLineNumber, int endLineNumber) => (startLineNumber, endLineNumber);

    private static void AssertRanges(IReadOnlyList<TextRange> actual, (int, int)[] expectedRegions, string? message = null) =>
        actual.Select(r => (r.StartLineNumber, r.EndLineNumber)).ToList().ShouldBe(expectedRegions, false, message);

    [Fact]
    public void HasRanges()
    {
        string[] lines =
        [
            /* 1*/ "/**",
            /* 2*/ " * Comment",
            /* 3*/ " */",
            /* 4*/ "class A {",
            /* 5*/ "  void foo() {",
            /* 6*/ "    if (true) {",
            /* 7*/ "      //hello",
            /* 8*/ "    }",
            /* 9*/ "  }",
            /* 10*/ "}",
        ];

        using var textModel = TestTextModel.Create(string.Join('\n', lines));
        var foldingModel = new FoldingModel(textModel, new TestFoldingDecorationProvider(textModel));
        using var hiddenRangeModel = new HiddenRangeModel(foldingModel);
        hiddenRangeModel.HasRanges().ShouldBeFalse();

        var ranges = IndentRangeProvider.ComputeRanges(textModel, false);
        foldingModel.Update(ranges);

        foldingModel.ToggleCollapseState([foldingModel.GetRegionAtLine(1)!, foldingModel.GetRegionAtLine(6)!]);
        AssertRanges(hiddenRangeModel.HiddenRanges, [R(2, 3), R(7, 7)]);

        hiddenRangeModel.HasRanges().ShouldBeTrue();
        bool[] expected1 = [false, true, true, false, false, false, true, false, false, false];
        for (var line = 1; line <= 10; line++)
        {
            hiddenRangeModel.IsHidden(line).ShouldBe(expected1[line - 1], "line " + line);
        }

        foldingModel.ToggleCollapseState([foldingModel.GetRegionAtLine(4)!]);
        AssertRanges(hiddenRangeModel.HiddenRanges, [R(2, 3), R(5, 9)]);

        hiddenRangeModel.HasRanges().ShouldBeTrue();
        bool[] expected2 = [false, true, true, false, true, true, true, true, true, false];
        for (var line = 1; line <= 10; line++)
        {
            hiddenRangeModel.IsHidden(line).ShouldBe(expected2[line - 1], "line " + line);
        }

        foldingModel.ToggleCollapseState([foldingModel.GetRegionAtLine(1)!, foldingModel.GetRegionAtLine(6)!, foldingModel.GetRegionAtLine(4)!]);
        AssertRanges(hiddenRangeModel.HiddenRanges, []);
        hiddenRangeModel.HasRanges().ShouldBeFalse();
        for (var line = 1; line <= 10; line++)
        {
            hiddenRangeModel.IsHidden(line).ShouldBeFalse("line " + line);
        }
    }

    private sealed class TestFoldingRangeProvider(TextModel model, (int Start, int End)[] ranges) : IFoldingRangeProvider
    {
        public Task<IReadOnlyList<FoldingRange>?> ProvideFoldingRanges(TextModel m, FoldingContext context, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<FoldingRange>?>(ReferenceEquals(m, model) ? [.. ranges.Select(r => new FoldingRange(r.Start, r.End))] : null);
    }

    [Fact]
    public async Task Syntax_folding_limit_by_nesting_level()
    {
        string[] lines =
        [
            /* 1*/ "{",
            /* 2*/ "  A",
            /* 3*/ "  {",
            /* 4*/ "    {",
            /* 5*/ "      B",
            /* 6*/ "    }",
            /* 7*/ "    {",
            /* 8*/ "      A",
            /* 9*/ "      {",
            /* 10*/ "         A",
            /* 11*/ "      }",
            /* 12*/ "      {",
            /* 13*/ "        {",
            /* 14*/ "          {",
            /* 15*/ "             A",
            /* 16*/ "          }",
            /* 17*/ "        }",
            /* 18*/ "      }",
            /* 19*/ "    }",
            /* 20*/ "  }",
            /* 21*/ "}",
            /* 22*/ "{",
            /* 23*/ "  A",
            /* 24*/ "}",
        ];

        var r1 = (1, 20); // 0
        var r2 = (3, 19); // 1
        var r3 = (4, 5); // 2
        var r4 = (7, 18); // 2
        var r5 = (9, 10); // 3
        var r6 = (12, 17); // 4
        var r7 = (13, 16); // 5
        var r8 = (14, 15); // 6
        var r9 = (22, 23); // 0

        using var model = TestTextModel.Create(string.Join('\n', lines));
        (int, int)[] ranges = [r1, r2, r3, r4, r5, r6, r7, r8, r9];
        IFoldingRangeProvider[] providers = [new TestFoldingRangeProvider(model, ranges)];

        async Task AssertLimit(int maxEntries, (int, int)[] expectedRanges, string message)
        {
            int? reported = -1;
            var foldingRangesLimit = new FoldingLimitReporter(maxEntries, (_, limited) => reported = limited);
            using var syntaxRangeProvider = new SyntaxRangeProvider(model, providers, () => { }, foldingRangesLimit, null);
            var indentRanges = await syntaxRangeProvider.Compute(CancellationToken.None);
            var actual = new List<(int, int)>();
            if (indentRanges is not null)
            {
                for (var i = 0; i < indentRanges.Length; i++)
                {
                    actual.Add((indentRanges.GetStartLineNumber(i), indentRanges.GetEndLineNumber(i)));
                }

                reported.ShouldBe(9 <= maxEntries ? null : maxEntries, "limited");
            }

            actual.ShouldBe(expectedRanges, false, message);
        }

        await AssertLimit(1000, [r1, r2, r3, r4, r5, r6, r7, r8, r9], "1000");
        await AssertLimit(9, [r1, r2, r3, r4, r5, r6, r7, r8, r9], "9");
        await AssertLimit(8, [r1, r2, r3, r4, r5, r6, r7, r9], "8");
        await AssertLimit(7, [r1, r2, r3, r4, r5, r6, r9], "7");
        await AssertLimit(6, [r1, r2, r3, r4, r5, r9], "6");
        await AssertLimit(5, [r1, r2, r3, r4, r9], "5");
        await AssertLimit(4, [r1, r2, r3, r9], "4");
        await AssertLimit(3, [r1, r2, r9], "3");
        await AssertLimit(2, [r1, r9], "2");
        await AssertLimit(1, [r1], "1");
        await AssertLimit(0, [], "0");
    }

    // ---- written for the port

    [Fact]
    public void AdjustSelections_moves_selection_ends_out_of_hidden_lines()
    {
        using var textModel = TestTextModel.Create(string.Join('\n', ["class A {", "  void foo() {", "    x();", "  }", "}"]));
        var foldingModel = new FoldingModel(textModel, new TestFoldingDecorationProvider(textModel));
        using var hiddenRangeModel = new HiddenRangeModel(foldingModel);
        var changes = new List<IReadOnlyList<TextRange>>();
        hiddenRangeModel.DidChange += changes.Add;
        foldingModel.Update(IndentRangeProvider.ComputeRanges(textModel, false));
        foldingModel.ToggleCollapseState([foldingModel.GetRegionAtLine(2)!]);
        changes.Count.ShouldBe(1);
        AssertRanges(hiddenRangeModel.HiddenRanges, [R(3, 3)]);

        var selections = new List<TextSelection> { new(3, 3, 3, 3), new(1, 1, 1, 2) };
        hiddenRangeModel.AdjustSelections(selections).ShouldBeTrue();
        selections[0].ShouldBe(new TextSelection(2, 15, 2, 15));
        selections[1].ShouldBe(new TextSelection(1, 1, 1, 2));
        hiddenRangeModel.AdjustSelections(selections).ShouldBeFalse();
    }

    private sealed class NullProvider : IFoldingRangeProvider
    {
        public Task<IReadOnlyList<FoldingRange>?> ProvideFoldingRanges(TextModel model, FoldingContext context, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<FoldingRange>?>(null);
    }

    private sealed class FixedProvider(params FoldingRange[] ranges) : IFoldingRangeProvider
    {
        public Task<IReadOnlyList<FoldingRange>?> ProvideFoldingRanges(TextModel model, FoldingContext context, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<FoldingRange>?>(ranges);
    }

    [Fact]
    public async Task The_syntax_provider_falls_back_to_indentation_and_keeps_kinds()
    {
        using var textModel = TestTextModel.Create(string.Join('\n', ["a", "  b", "  c", "d"]));
        var limit = new FoldingLimitReporter(100);
        var languages = new LanguageConfigurationService();
        var indent = new IndentRangeProvider(textModel, languages, limit);
        using var fallback = new SyntaxRangeProvider(textModel, [new NullProvider()], () => { }, limit, indent);
        var regions = (await fallback.Compute(CancellationToken.None)).ShouldNotBeNull();
        regions.Length.ShouldBe(1);
        regions.HasTypes().ShouldBeFalse();

        // ranges outside the document or empty are dropped; the kinds become types
        using var syntax = new SyntaxRangeProvider(textModel,
        [
            new FixedProvider(new FoldingRange(1, 3) { Kind = FoldingRangeKind.Comment }, new FoldingRange(2, 9), new FoldingRange(3, 3)),
        ], () => { }, limit, null);
        regions = (await syntax.Compute(CancellationToken.None)).ShouldNotBeNull();
        regions.Length.ShouldBe(1);
        regions.GetType(0).ShouldBe("comment");
    }
}
