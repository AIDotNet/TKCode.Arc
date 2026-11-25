// Ported from vscode (src/vs/editor/contrib/folding/test/browser/foldingModel.test.ts, commit b57d5c9e), MIT license.
// All 22 upstream tests, with upstream's TestDecorationProvider. Below them, tests of the memento (getMemento /
// applyMemento) and of setCollapseStateForType written for this port.

using System.Text.RegularExpressions;
using Shouldly;
using TKCode.Arc.Editor.Contrib.Folding;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;
using static TKCode.Arc.Editor.Contrib.Folding.FoldingModelFunctions;

namespace TKCode.Arc.Editor.Tests;

/// <summary>Upstream's TestDecorationProvider (foldingModel.test.ts): three options, told apart by reference.</summary>
internal sealed class TestFoldingDecorationProvider(TextModel model) : IFoldingDecorationProvider
{
    private static readonly ModelDecorationOptions CollapsedDecoration = new("test")
    {
        Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
        LinesDecorationsClassName = "folding",
    };

    private static readonly ModelDecorationOptions ExpandedDecoration = new("test")
    {
        Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
        LinesDecorationsClassName = "folding",
    };

    private static readonly ModelDecorationOptions HiddenDecoration = new("test")
    {
        Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
        LinesDecorationsClassName = "folding",
    };

    public ModelDecorationOptions GetDecorationOption(bool isCollapsed, bool isHidden, bool isManual)
    {
        if (isHidden)
        {
            return HiddenDecoration;
        }

        if (isCollapsed)
        {
            return CollapsedDecoration;
        }

        return ExpandedDecoration;
    }

    public T? ChangeDecorations<T>(Func<TextModel.ModelDecorationsChangeAccessor, T> callback) => model.ChangeDecorations(callback);

    public void RemoveDecorations(IReadOnlyList<string> decorationIds) =>
        model.ChangeDecorations(accessor => accessor.DeltaDecorations(decorationIds, []));

    public List<(int Line, string Type)> GetDecorations()
    {
        var res = new List<(int, string)>();
        foreach (var decoration in model.GetAllDecorations())
        {
            if (ReferenceEquals(decoration.Options, HiddenDecoration))
            {
                res.Add((decoration.Range.StartLineNumber, "hidden"));
            }
            else if (ReferenceEquals(decoration.Options, CollapsedDecoration))
            {
                res.Add((decoration.Range.StartLineNumber, "collapsed"));
            }
            else if (ReferenceEquals(decoration.Options, ExpandedDecoration))
            {
                res.Add((decoration.Range.StartLineNumber, "expanded"));
            }
        }

        return res;
    }
}

public sealed class FoldingModelTests : IDisposable
{
    private static readonly FoldingMarkers RegionMarkers = new(JsRegex.FromLiteral("^\\/\\/#region$"), JsRegex.FromLiteral("^\\/\\/#endregion$"));

    private readonly List<IDisposable> _store = new();

    public void Dispose()
    {
        foreach (var d in _store)
        {
            d.Dispose();
        }
    }

    private TextModel CreateModel(string[] lines)
    {
        var model = TestTextModel.Create(string.Join('\n', lines));
        _store.Add(model);
        return model;
    }

    private static (int Start, int End, bool Collapsed) R(int startLineNumber, int endLineNumber, bool isCollapsed = false) =>
        (startLineNumber, endLineNumber, isCollapsed);

    private static (int Line, string Type) D(int line, string type) => (line, type);

    private static void AssertRegion(FoldingRegion? actual, (int Start, int End, bool Collapsed)? expected, string? message = null)
    {
        (actual is not null).ShouldBe(expected is not null, message);
        if (actual is not null && expected is { } e)
        {
            actual.StartLineNumber.ShouldBe(e.Start, message);
            actual.EndLineNumber.ShouldBe(e.End, message);
            actual.IsCollapsed.ShouldBe(e.Collapsed, message);
        }
    }

    private static void AssertFoldedRanges(FoldingModel foldingModel, (int, int, bool)[] expectedRegions, string? message = null)
    {
        var actualRanges = new List<(int, int, bool)>();
        var actual = foldingModel.Regions;
        for (var i = 0; i < actual.Length; i++)
        {
            if (actual.IsCollapsed(i))
            {
                actualRanges.Add(R(actual.GetStartLineNumber(i), actual.GetEndLineNumber(i)));
            }
        }

        actualRanges.ShouldBe(expectedRegions, false, message);
    }

    private static void AssertRanges(FoldingModel foldingModel, (int, int, bool)[] expectedRegions, string? message = null)
    {
        var actualRanges = new List<(int, int, bool)>();
        var actual = foldingModel.Regions;
        for (var i = 0; i < actual.Length; i++)
        {
            actualRanges.Add(R(actual.GetStartLineNumber(i), actual.GetEndLineNumber(i), actual.IsCollapsed(i)));
        }

        actualRanges.ShouldBe(expectedRegions, false, message);
    }

    private static void AssertDecorations(FoldingModel foldingModel, (int, string)[] expectedDecoration, string? message = null)
    {
        var decorationProvider = (TestFoldingDecorationProvider)foldingModel.DecorationProvider;
        decorationProvider.GetDecorations().ShouldBe(expectedDecoration, false, message);
    }

    private static void AssertRegions(IEnumerable<FoldingRegion> actual, (int, int, bool)[] expectedRegions, string? message = null) =>
        actual.Select(r => R(r.StartLineNumber, r.EndLineNumber, r.IsCollapsed)).ToList().ShouldBe(expectedRegions, false, message);

    private static FoldingModel NewFoldingModel(TextModel textModel) => new(textModel, new TestFoldingDecorationProvider(textModel));

    private static readonly string[] ClassLines =
    [
        /* 1*/ "/**",
        /* 2*/ " * Comment",
        /* 3*/ " */",
        /* 4*/ "class A {",
        /* 5*/ "  void foo() {",
        /* 6*/ "    // comment {",
        /* 7*/ "  }",
        /* 8*/ "}",
    ];

    private static readonly string[] RegionLines =
    [
        /* 1*/ "//#region",
        /* 2*/ "//#endregion",
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

    [Fact]
    public void GetRegionAtLine()
    {
        var textModel = CreateModel(ClassLines);
        var foldingModel = NewFoldingModel(textModel);

        var ranges = IndentRangeProvider.ComputeRanges(textModel, false);
        foldingModel.Update(ranges);

        var r1 = R(1, 3, false);
        var r2 = R(4, 7, false);
        var r3 = R(5, 6, false);

        AssertRanges(foldingModel, [r1, r2, r3]);

        AssertRegion(foldingModel.GetRegionAtLine(1), r1, "1");
        AssertRegion(foldingModel.GetRegionAtLine(2), r1, "2");
        AssertRegion(foldingModel.GetRegionAtLine(3), r1, "3");
        AssertRegion(foldingModel.GetRegionAtLine(4), r2, "4");
        AssertRegion(foldingModel.GetRegionAtLine(5), r3, "5");
        AssertRegion(foldingModel.GetRegionAtLine(6), r3, "5");
        AssertRegion(foldingModel.GetRegionAtLine(7), r2, "6");
        AssertRegion(foldingModel.GetRegionAtLine(8), null, "7");
    }

    [Fact]
    public void Collapse()
    {
        var textModel = CreateModel(ClassLines);
        var foldingModel = NewFoldingModel(textModel);

        var ranges = IndentRangeProvider.ComputeRanges(textModel, false);
        foldingModel.Update(ranges);

        var r1 = R(1, 3, false);
        var r2 = R(4, 7, false);
        var r3 = R(5, 6, false);

        AssertRanges(foldingModel, [r1, r2, r3]);

        foldingModel.ToggleCollapseState([foldingModel.GetRegionAtLine(1)!]);
        foldingModel.Update(ranges);

        AssertRanges(foldingModel, [R(1, 3, true), r2, r3]);

        foldingModel.ToggleCollapseState([foldingModel.GetRegionAtLine(5)!]);
        foldingModel.Update(ranges);

        AssertRanges(foldingModel, [R(1, 3, true), r2, R(5, 6, true)]);

        foldingModel.ToggleCollapseState([foldingModel.GetRegionAtLine(7)!]);
        foldingModel.Update(ranges);

        AssertRanges(foldingModel, [R(1, 3, true), R(4, 7, true), R(5, 6, true)]);
    }

    [Fact]
    public void Update()
    {
        var textModel = CreateModel(ClassLines);
        var foldingModel = NewFoldingModel(textModel);

        var ranges = IndentRangeProvider.ComputeRanges(textModel, false);
        foldingModel.Update(ranges);

        var r1 = R(1, 3, false);
        var r2 = R(4, 7, false);
        var r3 = R(5, 6, false);

        AssertRanges(foldingModel, [r1, r2, r3]);
        foldingModel.ToggleCollapseState([foldingModel.GetRegionAtLine(2)!, foldingModel.GetRegionAtLine(5)!]);

        textModel.ApplyEdits([EditOperation.Insert(new TextPosition(4, 1), "//hello\n")]);

        foldingModel.Update(IndentRangeProvider.ComputeRanges(textModel, false));

        AssertRanges(foldingModel, [R(1, 3, true), R(5, 8, false), R(6, 7, true)]);
    }

    [Fact]
    public void Delete()
    {
        var textModel = CreateModel(
        [
            /* 1*/ "function foo() {",
            /* 2*/ "  switch (x) {",
            /* 3*/ "    case 1:",
            /* 4*/ "      //hello1",
            /* 5*/ "      break;",
            /* 6*/ "    case 2:",
            /* 7*/ "      //hello2",
            /* 8*/ "      break;",
            /* 9*/ "    case 3:",
            /* 10*/ "      //hello3",
            /* 11*/ "      break;",
            /* 12*/ "  }",
            /* 13*/ "}",
        ]);
        var foldingModel = NewFoldingModel(textModel);

        var ranges = IndentRangeProvider.ComputeRanges(textModel, false);
        foldingModel.Update(ranges);

        var r1 = R(1, 12, false);
        var r2 = R(2, 11, false);
        var r3 = R(3, 5, false);
        var r4 = R(6, 8, false);
        var r5 = R(9, 11, false);

        AssertRanges(foldingModel, [r1, r2, r3, r4, r5]);
        foldingModel.ToggleCollapseState([foldingModel.GetRegionAtLine(6)!]);

        textModel.ApplyEdits([EditOperation.Delete(new TextRange(6, 11, 9, 0))]);

        foldingModel.Update(IndentRangeProvider.ComputeRanges(textModel, true), SelectedLines.ToSelectedLines([new TextSelection(7, 1, 7, 1)]));

        AssertRanges(foldingModel, [R(1, 9, false), R(2, 8, false), R(3, 5, false), R(6, 8, false)]);
    }

    [Fact]
    public void GetRegionsInside()
    {
        var textModel = CreateModel(ClassLines);
        var foldingModel = NewFoldingModel(textModel);

        var ranges = IndentRangeProvider.ComputeRanges(textModel, false);
        foldingModel.Update(ranges);

        var r1 = R(1, 3, false);
        var r2 = R(4, 7, false);
        var r3 = R(5, 6, false);

        AssertRanges(foldingModel, [r1, r2, r3]);
        var region1 = foldingModel.GetRegionAtLine(r1.Start);
        var region2 = foldingModel.GetRegionAtLine(r2.Start);
        var region3 = foldingModel.GetRegionAtLine(r3.Start);

        AssertRegions(foldingModel.GetRegionsInside(null), [r1, r2, r3], "1");
        AssertRegions(foldingModel.GetRegionsInside(region1), [], "2");
        AssertRegions(foldingModel.GetRegionsInside(region2), [r3], "3");
        AssertRegions(foldingModel.GetRegionsInside(region3), [], "4");
    }

    [Fact]
    public void GetRegionsInsideWithLevel()
    {
        var textModel = CreateModel(
        [
            /* 1*/ "//#region",
            /* 2*/ "//#endregion",
            /* 3*/ "class A {",
            /* 4*/ "  void foo() {",
            /* 5*/ "    if (true) {",
            /* 6*/ "        return;",
            /* 7*/ "    }",
            /* 8*/ "    if (true) {",
            /* 9*/ "      return;",
            /* 10*/ "    }",
            /* 11*/ "  }",
            /* 12*/ "}",
        ]);

        var foldingModel = NewFoldingModel(textModel);

        var ranges = IndentRangeProvider.ComputeRanges(textModel, false, RegionMarkers);
        foldingModel.Update(ranges);

        var r1 = R(1, 2, false);
        var r2 = R(3, 11, false);
        var r3 = R(4, 10, false);
        var r4 = R(5, 6, false);
        var r5 = R(8, 9, false);

        var region1 = foldingModel.GetRegionAtLine(r1.Start);
        var region2 = foldingModel.GetRegionAtLine(r2.Start);
        var region3 = foldingModel.GetRegionAtLine(r3.Start);

        AssertRanges(foldingModel, [r1, r2, r3, r4, r5]);

        AssertRegions(foldingModel.GetRegionsInside(null, (r, level) => level == 1), [r1, r2], "1");
        AssertRegions(foldingModel.GetRegionsInside(null, (r, level) => level == 2), [r3], "2");
        AssertRegions(foldingModel.GetRegionsInside(null, (r, level) => level == 3), [r4, r5], "3");

        AssertRegions(foldingModel.GetRegionsInside(region2, (r, level) => level == 1), [r3], "4");
        AssertRegions(foldingModel.GetRegionsInside(region2, (r, level) => level == 2), [r4, r5], "5");
        AssertRegions(foldingModel.GetRegionsInside(region3, (r, level) => level == 1), [r4, r5], "6");

        AssertRegions(foldingModel.GetRegionsInside(region2, (r, level) => r.HidesLine(9)), [r3, r5], "7");

        AssertRegions(foldingModel.GetRegionsInside(region1, (r, level) => level == 1), [], "8");
    }

    [Fact]
    public void GetRegionAtLine2()
    {
        var textModel = CreateModel(
        [
            /* 1*/ "//#region",
            /* 2*/ "class A {",
            /* 3*/ "  void foo() {",
            /* 4*/ "    if (true) {",
            /* 5*/ "      //hello",
            /* 6*/ "    }",
            /* 7*/ "",
            /* 8*/ "  }",
            /* 9*/ "}",
            /* 10*/ "//#endregion",
            /* 11*/ "",
        ]);
        var foldingModel = NewFoldingModel(textModel);

        var ranges = IndentRangeProvider.ComputeRanges(textModel, false, RegionMarkers);
        foldingModel.Update(ranges);

        var r1 = R(1, 10, false);
        var r2 = R(2, 8, false);
        var r3 = R(3, 7, false);
        var r4 = R(4, 5, false);

        AssertRanges(foldingModel, [r1, r2, r3, r4]);

        AssertRegions(foldingModel.GetAllRegionsAtLine(1), [r1], "1");
        AssertRegions(foldingModel.GetAllRegionsAtLine(2), [r2, r1], "2");
        AssertRegions(foldingModel.GetAllRegionsAtLine(3), [r3, r2, r1], "3");
        AssertRegions(foldingModel.GetAllRegionsAtLine(4), [r4, r3, r2, r1], "4");
        AssertRegions(foldingModel.GetAllRegionsAtLine(5), [r4, r3, r2, r1], "5");
        AssertRegions(foldingModel.GetAllRegionsAtLine(6), [r3, r2, r1], "6");
        AssertRegions(foldingModel.GetAllRegionsAtLine(7), [r3, r2, r1], "7");
        AssertRegions(foldingModel.GetAllRegionsAtLine(8), [r2, r1], "8");
        AssertRegions(foldingModel.GetAllRegionsAtLine(9), [r1], "9");
        AssertRegions(foldingModel.GetAllRegionsAtLine(10), [r1], "10");
        AssertRegions(foldingModel.GetAllRegionsAtLine(11), [], "10");
    }

    [Fact]
    public void SetCollapseStateRecursivly()
    {
        var textModel = CreateModel(RegionLines);
        var foldingModel = NewFoldingModel(textModel);

        var ranges = IndentRangeProvider.ComputeRanges(textModel, false, RegionMarkers);
        foldingModel.Update(ranges);

        var r1 = R(1, 2, false);
        var r2 = R(3, 12, false);
        var r3 = R(4, 11, false);
        var r4 = R(5, 6, false);
        var r5 = R(9, 10, false);
        AssertRanges(foldingModel, [r1, r2, r3, r4, r5]);

        SetCollapseStateLevelsDown(foldingModel, true, int.MaxValue, [4]);
        AssertFoldedRanges(foldingModel, [r3, r4, r5], "1");

        SetCollapseStateLevelsDown(foldingModel, false, int.MaxValue, [8]);
        AssertFoldedRanges(foldingModel, [], "2");

        SetCollapseStateLevelsDown(foldingModel, true, int.MaxValue, [12]);
        AssertFoldedRanges(foldingModel, [r2, r3, r4, r5], "1");

        SetCollapseStateLevelsDown(foldingModel, false, int.MaxValue, [7]);
        AssertFoldedRanges(foldingModel, [r2], "1");

        SetCollapseStateLevelsDown(foldingModel, false);
        AssertFoldedRanges(foldingModel, [], "1");

        SetCollapseStateLevelsDown(foldingModel, true);
        AssertFoldedRanges(foldingModel, [r1, r2, r3, r4, r5], "1");
    }

    [Fact]
    public void SetCollapseStateAtLevel()
    {
        var textModel = CreateModel(
        [
            /* 1*/ "//#region",
            /* 2*/ "//#endregion",
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
            /* 13*/ "  //#region",
            /* 14*/ "  const bar = 9;",
            /* 15*/ "  //#endregion",
            /* 16*/ "}",
        ]);
        var foldingModel = NewFoldingModel(textModel);

        var ranges = IndentRangeProvider.ComputeRanges(textModel, false, new FoldingMarkers(JsRegex.FromLiteral("^\\s*\\/\\/#region$"), JsRegex.FromLiteral("^\\s*\\/\\/#endregion$")));
        foldingModel.Update(ranges);

        var r1 = R(1, 2, false);
        var r2 = R(3, 15, false);
        var r3 = R(4, 11, false);
        var r4 = R(5, 6, false);
        var r5 = R(9, 10, false);
        var r6 = R(13, 15, false);
        AssertRanges(foldingModel, [r1, r2, r3, r4, r5, r6]);

        FoldingModelFunctions.SetCollapseStateAtLevel(foldingModel, 1, true, []);
        AssertFoldedRanges(foldingModel, [r1, r2], "1");

        FoldingModelFunctions.SetCollapseStateAtLevel(foldingModel, 1, false, [5]);
        AssertFoldedRanges(foldingModel, [r2], "2");

        FoldingModelFunctions.SetCollapseStateAtLevel(foldingModel, 1, false, [1]);
        AssertFoldedRanges(foldingModel, [], "3");

        FoldingModelFunctions.SetCollapseStateAtLevel(foldingModel, 2, true, []);
        AssertFoldedRanges(foldingModel, [r3, r6], "4");

        FoldingModelFunctions.SetCollapseStateAtLevel(foldingModel, 2, false, [5, 6]);
        AssertFoldedRanges(foldingModel, [r3], "5");

        FoldingModelFunctions.SetCollapseStateAtLevel(foldingModel, 3, true, [4, 9]);
        AssertFoldedRanges(foldingModel, [r3, r4], "6");

        FoldingModelFunctions.SetCollapseStateAtLevel(foldingModel, 3, false, [4, 9]);
        AssertFoldedRanges(foldingModel, [r3], "7");
    }

    [Fact]
    public void SetCollapseStateLevelsDown_()
    {
        var textModel = CreateModel(RegionLines);
        var foldingModel = NewFoldingModel(textModel);

        var ranges = IndentRangeProvider.ComputeRanges(textModel, false, RegionMarkers);
        foldingModel.Update(ranges);

        var r1 = R(1, 2, false);
        var r2 = R(3, 12, false);
        var r3 = R(4, 11, false);
        var r4 = R(5, 6, false);
        var r5 = R(9, 10, false);
        AssertRanges(foldingModel, [r1, r2, r3, r4, r5]);

        SetCollapseStateLevelsDown(foldingModel, true, 1, [4]);
        AssertFoldedRanges(foldingModel, [r3], "1");

        SetCollapseStateLevelsDown(foldingModel, true, 2, [4]);
        AssertFoldedRanges(foldingModel, [r3, r4, r5], "2");

        SetCollapseStateLevelsDown(foldingModel, false, 2, [3]);
        AssertFoldedRanges(foldingModel, [r4, r5], "3");

        SetCollapseStateLevelsDown(foldingModel, false, 2, [2]);
        AssertFoldedRanges(foldingModel, [r4, r5], "4");

        SetCollapseStateLevelsDown(foldingModel, true, 4, [2]);
        AssertFoldedRanges(foldingModel, [r1, r4, r5], "5");

        SetCollapseStateLevelsDown(foldingModel, false, 4, [2, 3]);
        AssertFoldedRanges(foldingModel, [], "6");
    }

    [Fact]
    public void SetCollapseStateLevelsUp_()
    {
        var textModel = CreateModel(RegionLines);
        var foldingModel = NewFoldingModel(textModel);

        var ranges = IndentRangeProvider.ComputeRanges(textModel, false, RegionMarkers);
        foldingModel.Update(ranges);

        var r1 = R(1, 2, false);
        var r2 = R(3, 12, false);
        var r3 = R(4, 11, false);
        var r4 = R(5, 6, false);
        var r5 = R(9, 10, false);
        AssertRanges(foldingModel, [r1, r2, r3, r4, r5]);

        SetCollapseStateLevelsUp(foldingModel, true, 1, [4]);
        AssertFoldedRanges(foldingModel, [r3], "1");

        SetCollapseStateLevelsUp(foldingModel, true, 2, [4]);
        AssertFoldedRanges(foldingModel, [r2, r3], "2");

        SetCollapseStateLevelsUp(foldingModel, false, 4, [1, 3, 4]);
        AssertFoldedRanges(foldingModel, [], "3");

        SetCollapseStateLevelsUp(foldingModel, true, 2, [10]);
        AssertFoldedRanges(foldingModel, [r3, r5], "4");
    }

    [Fact]
    public void SetCollapseStateUp_()
    {
        var textModel = CreateModel(RegionLines);
        var foldingModel = NewFoldingModel(textModel);

        var ranges = IndentRangeProvider.ComputeRanges(textModel, false, RegionMarkers);
        foldingModel.Update(ranges);

        var r1 = R(1, 2, false);
        var r2 = R(3, 12, false);
        var r3 = R(4, 11, false);
        var r4 = R(5, 6, false);
        var r5 = R(9, 10, false);
        AssertRanges(foldingModel, [r1, r2, r3, r4, r5]);

        SetCollapseStateUp(foldingModel, true, [5]);
        AssertFoldedRanges(foldingModel, [r4], "1");

        SetCollapseStateUp(foldingModel, true, [5]);
        AssertFoldedRanges(foldingModel, [r3, r4], "2");

        SetCollapseStateUp(foldingModel, true, [4]);
        AssertFoldedRanges(foldingModel, [r2, r3, r4], "2");
    }

    [Fact]
    public void SetCollapseStateForMatchingLines_()
    {
        var textModel = CreateModel(
        [
            /* 1*/ "/**",
            /* 2*/ " * the class",
            /* 3*/ " */",
            /* 4*/ "class A {",
            /* 5*/ "  /**",
            /* 6*/ "   * the foo",
            /* 7*/ "   */",
            /* 8*/ "  void foo() {",
            /* 9*/ "    /*",
            /* 10*/ "     * the comment",
            /* 11*/ "     */",
            /* 12*/ "  }",
            /* 13*/ "}",
        ]);
        var foldingModel = NewFoldingModel(textModel);

        var ranges = IndentRangeProvider.ComputeRanges(textModel, false, RegionMarkers);
        foldingModel.Update(ranges);

        var r1 = R(1, 3, false);
        var r2 = R(4, 12, false);
        var r3 = R(5, 7, false);
        var r4 = R(8, 11, false);
        var r5 = R(9, 11, false);
        AssertRanges(foldingModel, [r1, r2, r3, r4, r5]);

        var regExp = new Regex("^\\s*" + Regex.Escape("/*"));
        SetCollapseStateForMatchingLines(foldingModel, regExp, true);
        AssertFoldedRanges(foldingModel, [r1, r3, r5], "1");
    }

    [Fact]
    public void SetCollapseStateForRest_()
    {
        var textModel = CreateModel(RegionLines);
        var foldingModel = NewFoldingModel(textModel);

        var ranges = IndentRangeProvider.ComputeRanges(textModel, false, RegionMarkers);
        foldingModel.Update(ranges);

        var r1 = R(1, 2, false);
        var r2 = R(3, 12, false);
        var r3 = R(4, 11, false);
        var r4 = R(5, 6, false);
        var r5 = R(9, 10, false);
        AssertRanges(foldingModel, [r1, r2, r3, r4, r5]);

        SetCollapseStateForRest(foldingModel, true, [5]);
        AssertFoldedRanges(foldingModel, [r1, r5], "1");

        SetCollapseStateForRest(foldingModel, false, [5]);
        AssertFoldedRanges(foldingModel, [], "2");

        SetCollapseStateForRest(foldingModel, true, [1]);
        AssertFoldedRanges(foldingModel, [r2, r3, r4, r5], "3");

        SetCollapseStateForRest(foldingModel, true, [3]);
        AssertFoldedRanges(foldingModel, [r1, r2, r3, r4, r5], "3");
    }

    [Fact]
    public void Folding_decoration()
    {
        var textModel = CreateModel(
        [
            /* 1*/ "class A {",
            /* 2*/ "  void foo() {",
            /* 3*/ "    if (true) {",
            /* 4*/ "      hoo();",
            /* 5*/ "    }",
            /* 6*/ "  }",
            /* 7*/ "}",
        ]);
        var foldingModel = NewFoldingModel(textModel);

        var ranges = IndentRangeProvider.ComputeRanges(textModel, false);
        foldingModel.Update(ranges);

        var r1 = R(1, 6, false);
        var r2 = R(2, 5, false);
        var r3 = R(3, 4, false);

        AssertRanges(foldingModel, [r1, r2, r3]);
        AssertDecorations(foldingModel, [D(1, "expanded"), D(2, "expanded"), D(3, "expanded")]);

        foldingModel.ToggleCollapseState([foldingModel.GetRegionAtLine(2)!]);

        AssertRanges(foldingModel, [r1, R(2, 5, true), r3]);
        AssertDecorations(foldingModel, [D(1, "expanded"), D(2, "collapsed"), D(3, "hidden")]);

        foldingModel.Update(ranges);

        AssertRanges(foldingModel, [r1, R(2, 5, true), r3]);
        AssertDecorations(foldingModel, [D(1, "expanded"), D(2, "collapsed"), D(3, "hidden")]);

        foldingModel.ToggleCollapseState([foldingModel.GetRegionAtLine(1)!]);

        AssertRanges(foldingModel, [R(1, 6, true), R(2, 5, true), r3]);
        AssertDecorations(foldingModel, [D(1, "collapsed"), D(2, "hidden"), D(3, "hidden")]);

        foldingModel.Update(ranges);

        AssertRanges(foldingModel, [R(1, 6, true), R(2, 5, true), r3]);
        AssertDecorations(foldingModel, [D(1, "collapsed"), D(2, "hidden"), D(3, "hidden")]);

        foldingModel.ToggleCollapseState([foldingModel.GetRegionAtLine(1)!, foldingModel.GetRegionAtLine(3)!]);

        AssertRanges(foldingModel, [r1, R(2, 5, true), R(3, 4, true)]);
        AssertDecorations(foldingModel, [D(1, "expanded"), D(2, "collapsed"), D(3, "hidden")]);

        foldingModel.Update(ranges);

        AssertRanges(foldingModel, [r1, R(2, 5, true), R(3, 4, true)]);
        AssertDecorations(foldingModel, [D(1, "expanded"), D(2, "collapsed"), D(3, "hidden")]);
    }

    [Fact]
    public void Fold_jumping()
    {
        var textModel = CreateModel(
        [
            /* 1*/ "class A {",
            /* 2*/ "  void foo() {",
            /* 3*/ "    if (1) {",
            /* 4*/ "      a();",
            /* 5*/ "    } else if (2) {",
            /* 6*/ "      if (true) {",
            /* 7*/ "        b();",
            /* 8*/ "      }",
            /* 9*/ "    } else {",
            /* 10*/ "      c();",
            /* 11*/ "    }",
            /* 12*/ "  }",
            /* 13*/ "}",
        ]);
        var foldingModel = NewFoldingModel(textModel);

        var ranges = IndentRangeProvider.ComputeRanges(textModel, false);
        foldingModel.Update(ranges);

        AssertRanges(foldingModel, [R(1, 12), R(2, 11), R(3, 4), R(5, 8), R(6, 7), R(9, 10)]);

        // Test jump to parent.
        GetParentFoldLine(7, foldingModel).ShouldBe(6);
        GetParentFoldLine(6, foldingModel).ShouldBe(5);
        GetParentFoldLine(5, foldingModel).ShouldBe(2);
        GetParentFoldLine(2, foldingModel).ShouldBe(1);
        GetParentFoldLine(1, foldingModel).ShouldBeNull();

        // Test jump to previous.
        GetPreviousFoldLine(10, foldingModel).ShouldBe(9);
        GetPreviousFoldLine(9, foldingModel).ShouldBe(5);
        GetPreviousFoldLine(5, foldingModel).ShouldBe(3);
        GetPreviousFoldLine(3, foldingModel).ShouldBeNull();
        // Test when not on a folding region start line.
        GetPreviousFoldLine(4, foldingModel).ShouldBe(3);
        GetPreviousFoldLine(7, foldingModel).ShouldBe(6);
        GetPreviousFoldLine(8, foldingModel).ShouldBe(6);

        // Test jump to next.
        GetNextFoldLine(3, foldingModel).ShouldBe(5);
        GetNextFoldLine(5, foldingModel).ShouldBe(9);
        GetNextFoldLine(9, foldingModel).ShouldBeNull();
        // Test when not on a folding region start line.
        GetNextFoldLine(4, foldingModel).ShouldBe(5);
        GetNextFoldLine(7, foldingModel).ShouldBe(9);
        GetNextFoldLine(8, foldingModel).ShouldBe(9);
    }

    [Fact]
    public void Fold_jumping_issue_129503()
    {
        var textModel = CreateModel(["", "if True:", "  print(1)", "if True:", "  print(1)", ""]);
        var foldingModel = NewFoldingModel(textModel);

        var ranges = IndentRangeProvider.ComputeRanges(textModel, false);
        foldingModel.Update(ranges);

        AssertRanges(foldingModel, [R(2, 3, false), R(4, 6, false)]);

        // Test jump to next.
        GetNextFoldLine(1, foldingModel).ShouldBe(2);
        GetNextFoldLine(2, foldingModel).ShouldBe(4);
        GetNextFoldLine(3, foldingModel).ShouldBe(4);
        GetNextFoldLine(4, foldingModel).ShouldBeNull();
        GetNextFoldLine(5, foldingModel).ShouldBeNull();
        GetNextFoldLine(6, foldingModel).ShouldBeNull();

        // Test jump to previous.
        GetPreviousFoldLine(1, foldingModel).ShouldBeNull();
        GetPreviousFoldLine(2, foldingModel).ShouldBeNull();
        GetPreviousFoldLine(3, foldingModel).ShouldBe(2);
        GetPreviousFoldLine(4, foldingModel).ShouldBe(2);
        GetPreviousFoldLine(5, foldingModel).ShouldBe(4);
        GetPreviousFoldLine(6, foldingModel).ShouldBe(4);
    }

    private static readonly string[] SevenLines =
    [
        /* 1*/ "class A {",
        /* 2*/ "  void foo() {",
        /* 3*/ "    if (true) {",
        /* 4*/ "      return;",
        /* 5*/ "    }",
        /* 6*/ "  }",
        /* 7*/ "}",
    ];

    [Fact]
    public void RemoveManualRanges_cursor_on_manual_range_removes_innermost_only()
    {
        var textModel = CreateModel(SevenLines);
        var foldingModel = NewFoldingModel(textModel);

        // Set up ranges: outer provider range + two nested manual ranges
        foldingModel.Update(FoldingRegions.FromFoldRanges(
        [
            new FoldRange(1, 6, null, false, FoldSource.Provider),
            new FoldRange(2, 5, null, false, FoldSource.UserDefined),
            new FoldRange(3, 4, null, false, FoldSource.UserDefined),
        ]));
        AssertRanges(foldingModel, [R(1, 6), R(2, 5), R(3, 4)]);

        // Cursor on line 4 (inside innermost manual range 3-4): should remove only 3-4
        foldingModel.RemoveManualRanges([new TextRange(4, 1, 4, 1)]);
        AssertRanges(foldingModel, [R(1, 6), R(2, 5)]);

        // Cursor on line 3 (inside remaining manual range 2-5): should remove only 2-5
        foldingModel.RemoveManualRanges([new TextRange(3, 1, 3, 1)]);
        AssertRanges(foldingModel, [R(1, 6)]);
    }

    [Fact]
    public void RemoveManualRanges_cursor_skips_provider_ranges_to_remove_nearest_manual_range()
    {
        var textModel = CreateModel(["class A {", "  void foo() {", "    return;", "  }", "}"]);
        var foldingModel = NewFoldingModel(textModel);
        foldingModel.Update(FoldingRegions.FromFoldRanges(
        [
            new FoldRange(1, 5, null, false, FoldSource.UserDefined),
            new FoldRange(2, 4, null, false, FoldSource.Provider),
        ]));

        foldingModel.RemoveManualRanges([new TextRange(3, 1, 3, 1)]);
        AssertRanges(foldingModel, [R(2, 4)]);
    }

    [Fact]
    public void RemoveManualRanges_cursor_not_on_manual_range_removes_all_manual_ranges()
    {
        var textModel = CreateModel(["// header", "class A {", "  void foo() {", "  }", "}", "// footer"]);
        var foldingModel = NewFoldingModel(textModel);

        // Provider range at 2-4, manual range at 3-4
        foldingModel.Update(FoldingRegions.FromFoldRanges(
        [
            new FoldRange(2, 4, null, false, FoldSource.Provider),
            new FoldRange(3, 4, null, false, FoldSource.UserDefined),
        ]));
        AssertRanges(foldingModel, [R(2, 4), R(3, 4)]);

        // A single-line selection outside manual ranges should preserve them
        foldingModel.RemoveManualRanges([new TextRange(6, 1, 6, 2)]);
        AssertRanges(foldingModel, [R(2, 4), R(3, 4)]);

        // Cursor on line 6 (not inside any manual range): should remove all manual ranges
        foldingModel.RemoveManualRanges([new TextRange(6, 1, 6, 1)]);
        AssertRanges(foldingModel, [R(2, 4)]);
    }

    [Fact]
    public void RemoveManualRanges_single_line_selection_removes_all_intersecting_manual_ranges()
    {
        var textModel = CreateModel(SevenLines);
        var foldingModel = NewFoldingModel(textModel);
        foldingModel.Update(FoldingRegions.FromFoldRanges(
        [
            new FoldRange(1, 6, null, false, FoldSource.Provider),
            new FoldRange(2, 5, null, false, FoldSource.UserDefined),
            new FoldRange(3, 4, null, false, FoldSource.UserDefined),
        ]));

        foldingModel.RemoveManualRanges([new TextRange(4, 1, 4, 2)]);
        AssertRanges(foldingModel, [R(1, 6)]);
    }

    [Fact]
    public void RemoveManualRanges_selection_range_removes_intersecting_manual_ranges()
    {
        var textModel = CreateModel(["class A {", "  void foo() {", "  }", "  void bar() {", "  }", "}"]);
        var foldingModel = NewFoldingModel(textModel);

        foldingModel.Update(FoldingRegions.FromFoldRanges(
        [
            new FoldRange(1, 5, null, false, FoldSource.Provider),
            new FoldRange(2, 3, null, false, FoldSource.UserDefined),
            new FoldRange(4, 5, null, false, FoldSource.UserDefined),
        ]));
        AssertRanges(foldingModel, [R(1, 5), R(2, 3), R(4, 5)]);

        // Selection spanning lines 2-3: removes only the first manual range
        foldingModel.RemoveManualRanges([new TextRange(2, 1, 3, 1)]);
        AssertRanges(foldingModel, [R(1, 5), R(4, 5)]);
    }

    // ---- written for the port

    [Fact]
    public void A_memento_restores_the_collapsed_regions_and_skips_changed_lines()
    {
        var textModel = CreateModel(SevenLines);
        var foldingModel = NewFoldingModel(textModel);
        foldingModel.Update(IndentRangeProvider.ComputeRanges(textModel, false));
        foldingModel.ToggleCollapseState([foldingModel.GetRegionAtLine(3)!]);

        var memento = foldingModel.GetMemento().ShouldNotBeNull();
        memento.Single().ShouldSatisfyAllConditions(
            m => m.StartLineNumber.ShouldBe(3),
            m => m.EndLineNumber.ShouldBe(4),
            m => m.IsCollapsed.ShouldBe(true),
            m => m.Checksum.ShouldNotBeNull());

        // a fresh model of the same text takes the state
        var other = NewFoldingModel(textModel);
        other.Update(IndentRangeProvider.ComputeRanges(textModel, false));
        other.ApplyMemento(memento);
        AssertFoldedRanges(other, [R(3, 4)]);

        // the lines changed: the checksum differs, nothing is restored
        textModel.ApplyEdits([EditOperation.Replace(new TextRange(4, 1, 4, 13), "      break;")]);
        var third = NewFoldingModel(textModel);
        third.Update(IndentRangeProvider.ComputeRanges(textModel, false));
        third.ApplyMemento(memento);
        AssertFoldedRanges(third, []);

        // nothing folded: no memento
        NewFoldingModel(textModel).GetMemento().ShouldBeNull();
    }

    [Fact]
    public void SetCollapseStateForType_folds_the_regions_of_a_kind()
    {
        var textModel = CreateModel(SevenLines);
        var foldingModel = NewFoldingModel(textModel);
        foldingModel.Update(FoldingRegions.FromFoldRanges(
        [
            new FoldRange(1, 6, null, false, FoldSource.Provider),
            new FoldRange(2, 5, "comment", false, FoldSource.Provider),
            new FoldRange(3, 4, "region", false, FoldSource.Provider),
        ]));
        foldingModel.Regions.HasTypes().ShouldBeTrue();

        SetCollapseStateForType(foldingModel, "region", true);
        AssertFoldedRanges(foldingModel, [R(3, 4)]);
        SetCollapseStateForType(foldingModel, "comment", true);
        AssertFoldedRanges(foldingModel, [R(2, 5), R(3, 4)]);
        SetCollapseStateForType(foldingModel, "region", false);
        AssertFoldedRanges(foldingModel, [R(2, 5)]);
    }

    [Fact]
    public void Disposing_removes_the_decorations()
    {
        var textModel = CreateModel(SevenLines);
        var foldingModel = NewFoldingModel(textModel);
        foldingModel.Update(IndentRangeProvider.ComputeRanges(textModel, false));
        textModel.GetAllDecorations().Count.ShouldBe(3);
        foldingModel.Dispose();
        textModel.GetAllDecorations().ShouldBeEmpty();
    }
}
