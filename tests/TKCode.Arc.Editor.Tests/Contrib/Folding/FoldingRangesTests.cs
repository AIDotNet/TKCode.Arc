// Ported from vscode (src/vs/editor/contrib/folding/test/browser/foldingRanges.test.ts, commit b57d5c9e), MIT license.
// All 7 upstream tests. The markers are JavaScript literals translated by JsRegex, as the language configurations do.

using Shouldly;
using TKCode.Arc.Editor.Contrib.Folding;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class FoldingRangesTests
{
    private static readonly FoldingMarkers Markers = new(JsRegex.FromLiteral("^#region$"), JsRegex.FromLiteral("^#endregion$"));

    private static FoldRange FoldRange(int from, int to, bool collapsed = false, FoldSource source = FoldSource.Provider, string? type = null) =>
        new(from, to, type, collapsed, source);

    private static void AssertEqualRanges(FoldRange range1, FoldRange range2, string msg)
    {
        range1.StartLineNumber.ShouldBe(range2.StartLineNumber, msg + " start");
        range1.EndLineNumber.ShouldBe(range2.EndLineNumber, msg + " end");
        range1.Type.ShouldBe(range2.Type, msg + " type");
        range1.IsCollapsed.ShouldBe(range2.IsCollapsed, msg + " collapsed");
        range1.Source.ShouldBe(range2.Source, msg + " source");
    }

    [Fact]
    public void Test_max_folding_regions()
    {
        var lines = new List<string>();
        var nRegions = FoldingRegions.MaxFoldingRegions;
        var collector = new RangesCollector(new FoldingLimitReporter(FoldingRegions.MaxFoldingRegions));
        for (var i = 0; i < nRegions; i++)
        {
            var startLineNumber = lines.Count;
            lines.Add("#region");
            var endLineNumber = lines.Count;
            lines.Add("#endregion");
            collector.InsertFirst(startLineNumber, endLineNumber, 0);
        }

        using var model = TestTextModel.Create(string.Join('\n', lines));
        var actual = collector.ToIndentRanges(model);
        actual.Length.ShouldBe(nRegions, "len");
    }

    [Fact]
    public void FindRange()
    {
        string[] lines =
        [
            /* 1*/ "#region",
            /* 2*/ "#endregion",
            /* 3*/ "class A {",
            /* 4*/ "  void foo() {",
            /* 5*/ "    if (true) {",
            /* 6*/ "        return;",
            /* 7*/ "    }",
            /* 8*/ "",
            /* 9*/ "    if (true) {",
            /* 10*/ "      return;",
            /* 11*/ "    }",
            /* 12*/ "  }",
            /* 13*/ "}",
        ];

        using var textModel = TestTextModel.Create(string.Join('\n', lines));
        var actual = IndentRangeProvider.ComputeRanges(textModel, false, Markers);

        actual.FindRange(1).ShouldBe(0, "1");
        actual.FindRange(2).ShouldBe(0, "2");
        actual.FindRange(3).ShouldBe(1, "3");
        actual.FindRange(4).ShouldBe(2, "4");
        actual.FindRange(5).ShouldBe(3, "5");
        actual.FindRange(6).ShouldBe(3, "6");
        actual.FindRange(7).ShouldBe(2, "7");
        actual.FindRange(8).ShouldBe(2, "8");
        actual.FindRange(9).ShouldBe(4, "9");
        actual.FindRange(10).ShouldBe(4, "10");
        actual.FindRange(11).ShouldBe(2, "11");
        actual.FindRange(12).ShouldBe(1, "12");
        actual.FindRange(13).ShouldBe(-1, "13");
    }

    [Fact]
    public void SetCollapsed()
    {
        var lines = new List<string>();
        var nRegions = 500;
        for (var i = 0; i < nRegions; i++)
        {
            lines.Add("#region");
        }

        for (var i = 0; i < nRegions; i++)
        {
            lines.Add("#endregion");
        }

        using var model = TestTextModel.Create(string.Join('\n', lines));
        var actual = IndentRangeProvider.ComputeRanges(model, false, Markers);
        actual.Length.ShouldBe(nRegions, "len");
        for (var i = 0; i < nRegions; i++)
        {
            actual.SetCollapsed(i, i % 3 == 0);
        }

        for (var i = 0; i < nRegions; i++)
        {
            actual.IsCollapsed(i).ShouldBe(i % 3 == 0, "line" + i);
        }
    }

    [Fact]
    public void SanitizeAndMerge1()
    {
        FoldRange[] regionSet1 =
        [
            FoldRange(0, 100), // invalid, should be removed
            FoldRange(1, 100, false, FoldSource.Provider, "A"), // valid
            FoldRange(1, 100, false, FoldSource.Provider, "Z"), // invalid, duplicate start
            FoldRange(10, 10, false), // invalid, should be removed
            FoldRange(20, 80, false, FoldSource.Provider, "C1"), // valid inside 'B'
            FoldRange(22, 80, true, FoldSource.Provider, "D1"), // valid inside 'C1'
            FoldRange(90, 101), // invalid, should be removed
        ];
        FoldRange[] regionSet2 =
        [
            FoldRange(20, 80, true), // should merge with C1
            FoldRange(18, 80, true), // invalid, out of order
            FoldRange(21, 81, true, FoldSource.Provider, "Z"), // invalid, overlapping
            FoldRange(22, 80, true, FoldSource.Provider, "D2"), // should merge with D1
        ];
        var result = FoldingRegions.SanitizeAndMerge(regionSet1, regionSet2, 100);
        result.Count.ShouldBe(3, "result length1");
        AssertEqualRanges(result[0], FoldRange(1, 100, false, FoldSource.Provider, "A"), "A1");
        AssertEqualRanges(result[1], FoldRange(20, 80, true, FoldSource.Provider, "C1"), "C1");
        AssertEqualRanges(result[2], FoldRange(22, 80, true, FoldSource.Provider, "D1"), "D1");
    }

    [Fact]
    public void SanitizeAndMerge2()
    {
        FoldRange[] regionSet1 =
        [
            FoldRange(1, 100, false, FoldSource.Provider, "a1"), // valid
            FoldRange(2, 100, false, FoldSource.Provider, "a2"), // valid
            FoldRange(3, 19, false, FoldSource.Provider, "a3"), // valid
            FoldRange(20, 71, false, FoldSource.Provider, "a4"), // overlaps b3
            FoldRange(21, 29, false, FoldSource.Provider, "a5"), // valid
            FoldRange(81, 91, false, FoldSource.Provider, "a6"), // overlaps b4
        ];
        FoldRange[] regionSet2 =
        [
            FoldRange(30, 39, true, FoldSource.Provider, "b1"), // valid, will be recovered
            FoldRange(40, 49, true, FoldSource.UserDefined, "b2"), // valid
            FoldRange(50, 100, true, FoldSource.UserDefined, "b3"), // overlaps a4
            FoldRange(80, 90, true, FoldSource.UserDefined, "b4"), // overlaps a6
            FoldRange(92, 100, true, FoldSource.UserDefined, "b5"), // valid
        ];
        var result = FoldingRegions.SanitizeAndMerge(regionSet1, regionSet2, 100);
        result.Count.ShouldBe(9, "result length1");
        AssertEqualRanges(result[0], FoldRange(1, 100, false, FoldSource.Provider, "a1"), "P1");
        AssertEqualRanges(result[1], FoldRange(2, 100, false, FoldSource.Provider, "a2"), "P2");
        AssertEqualRanges(result[2], FoldRange(3, 19, false, FoldSource.Provider, "a3"), "P3");
        AssertEqualRanges(result[3], FoldRange(21, 29, false, FoldSource.Provider, "a5"), "P4");
        AssertEqualRanges(result[4], FoldRange(30, 39, true, FoldSource.Recovered, "b1"), "P5");
        AssertEqualRanges(result[5], FoldRange(40, 49, true, FoldSource.UserDefined, "b2"), "P6");
        AssertEqualRanges(result[6], FoldRange(50, 100, true, FoldSource.UserDefined, "b3"), "P7");
        AssertEqualRanges(result[7], FoldRange(80, 90, true, FoldSource.UserDefined, "b4"), "P8");
        AssertEqualRanges(result[8], FoldRange(92, 100, true, FoldSource.UserDefined, "b5"), "P9");
    }

    [Fact]
    public void SanitizeAndMerge3()
    {
        FoldRange[] regionSet1 =
        [
            FoldRange(1, 100, false, FoldSource.Provider, "a1"), // valid
            FoldRange(10, 29, false, FoldSource.Provider, "a2"), // matches manual hidden
            FoldRange(35, 39, true, FoldSource.Recovered, "a3"), // valid
        ];
        FoldRange[] regionSet2 =
        [
            FoldRange(10, 29, true, FoldSource.Recovered, "b1"), // matches a
            FoldRange(20, 28, true, FoldSource.Provider, "b2"), // should remain
            FoldRange(30, 39, true, FoldSource.Recovered, "b3"), // should remain
        ];
        var result = FoldingRegions.SanitizeAndMerge(regionSet1, regionSet2, 100);
        result.Count.ShouldBe(5, "result length3");
        AssertEqualRanges(result[0], FoldRange(1, 100, false, FoldSource.Provider, "a1"), "R1");
        AssertEqualRanges(result[1], FoldRange(10, 29, true, FoldSource.Provider, "a2"), "R2");
        AssertEqualRanges(result[2], FoldRange(20, 28, true, FoldSource.Recovered, "b2"), "R3");
        AssertEqualRanges(result[3], FoldRange(30, 39, true, FoldSource.Recovered, "b3"), "R3");
        AssertEqualRanges(result[4], FoldRange(35, 39, true, FoldSource.Recovered, "a3"), "R4");
    }

    [Fact]
    public void SanitizeAndMerge4()
    {
        FoldRange[] regionSet1 =
        [
            FoldRange(1, 100, false, FoldSource.Provider, "a1"), // valid
        ];
        FoldRange[] regionSet2 =
        [
            FoldRange(20, 28, true, FoldSource.Provider, "b1"), // hidden
            FoldRange(30, 38, true, FoldSource.Provider, "b2"), // hidden
        ];
        var result = FoldingRegions.SanitizeAndMerge(regionSet1, regionSet2, 100);
        result.Count.ShouldBe(3, "result length4");
        AssertEqualRanges(result[0], FoldRange(1, 100, false, FoldSource.Provider, "a1"), "R1");
        AssertEqualRanges(result[1], FoldRange(20, 28, true, FoldSource.Recovered, "b1"), "R2");
        AssertEqualRanges(result[2], FoldRange(30, 38, true, FoldSource.Recovered, "b2"), "R3");
    }
}
