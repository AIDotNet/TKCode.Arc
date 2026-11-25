// Ported from vscode (src/vs/editor/contrib/folding/test/browser/indentRangeProvider.test.ts and indentFold.test.ts,
// commit b57d5c9e), MIT license. All 26 upstream tests (9 indentation, 16 with regions, 1 limit by indent). The markers
// are JavaScript literals translated by JsRegex (flags included), as the language configurations do; the limit
// reporter's false is null.

using Shouldly;
using TKCode.Arc.Editor.Contrib.Folding;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class IndentRangeProviderTests
{
    private static readonly FoldingMarkers Markers = new(JsRegex.FromLiteral("^\\s*#region\\b"), JsRegex.FromLiteral("^\\s*#endregion\\b"));

    private static (int Start, int End, int Parent) R(int startLineNumber, int endLineNumber, int parentIndex, bool marker = false) =>
        (startLineNumber, endLineNumber, parentIndex);

    private static void AssertRanges(string[] lines, (int Start, int End, int Parent)[] expected, bool offside, FoldingMarkers? markers = null)
    {
        using var model = TestTextModel.Create(string.Join('\n', lines));
        var actual = IndentRangeProvider.ComputeRanges(model, offside, markers);

        var actualRanges = new List<(int, int, int)>();
        for (var i = 0; i < actual.Length; i++)
        {
            actualRanges.Add((actual.GetStartLineNumber(i), actual.GetEndLineNumber(i), actual.GetParentIndex(i)));
        }

        actualRanges.ShouldBe(expected);
    }

    // ---- suite('Indentation Folding')

    [Fact]
    public void Fold_one_level()
    {
        string[] range = ["A", "  A", "  A", "  A"];
        AssertRanges(range, [R(1, 4, -1)], true);
        AssertRanges(range, [R(1, 4, -1)], false);
    }

    [Fact]
    public void Fold_two_levels()
    {
        string[] range = ["A", "  A", "  A", "    A", "    A"];
        AssertRanges(range, [R(1, 5, -1), R(3, 5, 0)], true);
        AssertRanges(range, [R(1, 5, -1), R(3, 5, 0)], false);
    }

    [Fact]
    public void Fold_three_levels()
    {
        string[] range = ["A", "  A", "    A", "      A", "A"];
        AssertRanges(range, [R(1, 4, -1), R(2, 4, 0), R(3, 4, 1)], true);
        AssertRanges(range, [R(1, 4, -1), R(2, 4, 0), R(3, 4, 1)], false);
    }

    [Fact]
    public void Fold_decreasing_indent()
    {
        string[] range = ["    A", "  A", "A"];
        AssertRanges(range, [], true);
        AssertRanges(range, [], false);
    }

    [Fact]
    public void Fold_Java()
    {
        AssertRanges(
        [
            /* 1*/ "class A {",
            /* 2*/ "  void foo() {",
            /* 3*/ "    console.log();",
            /* 4*/ "    console.log();",
            /* 5*/ "  }",
            /* 6*/ "",
            /* 7*/ "  void bar() {",
            /* 8*/ "    console.log();",
            /* 9*/ "  }",
            /*10*/ "}",
            /*11*/ "interface B {",
            /*12*/ "  void bar();",
            /*13*/ "}",
        ], [R(1, 9, -1), R(2, 4, 0), R(7, 8, 0), R(11, 12, -1)], false);
    }

    [Fact]
    public void Fold_Javadoc()
    {
        AssertRanges(
        [
            /* 1*/ "/**",
            /* 2*/ " * Comment",
            /* 3*/ " */",
            /* 4*/ "class A {",
            /* 5*/ "  void foo() {",
            /* 6*/ "  }",
            /* 7*/ "}",
        ], [R(1, 3, -1), R(4, 6, -1)], false);
    }

    [Fact]
    public void Fold_Whitespace_Java()
    {
        AssertRanges(
        [
            /* 1*/ "class A {",
            /* 2*/ "",
            /* 3*/ "  void foo() {",
            /* 4*/ "     ",
            /* 5*/ "     return 0;",
            /* 6*/ "  }",
            /* 7*/ "      ",
            /* 8*/ "}",
        ], [R(1, 7, -1), R(3, 5, 0)], false);
    }

    [Fact]
    public void Fold_Whitespace_Python()
    {
        AssertRanges(
        [
            /* 1*/ "def a:",
            /* 2*/ "  pass",
            /* 3*/ "   ",
            /* 4*/ "  def b:",
            /* 5*/ "    pass",
            /* 6*/ "  ",
            /* 7*/ "      ",
            /* 8*/ "def c: # since there was a deintent here",
        ], [R(1, 5, -1), R(4, 5, 0)], true);
    }

    [Fact]
    public void Fold_Tabs()
    {
        AssertRanges(
        [
            /* 1*/ "class A {",
            /* 2*/ "\t\t",
            /* 3*/ "\tvoid foo() {",
            /* 4*/ "\t \t//hello",
            /* 5*/ "\t    return 0;",
            /* 6*/ "  \t}",
            /* 7*/ "      ",
            /* 8*/ "}",
        ], [R(1, 7, -1), R(3, 5, 0)], false);
    }

    // ---- suite('Folding with regions')

    [Fact]
    public void Inside_region_indented()
    {
        AssertRanges(
        [
            /* 1*/ "class A {",
            /* 2*/ "  #region",
            /* 3*/ "  void foo() {",
            /* 4*/ "     ",
            /* 5*/ "     return 0;",
            /* 6*/ "  }",
            /* 7*/ "  #endregion",
            /* 8*/ "}",
        ], [R(1, 7, -1), R(2, 7, 0, true), R(3, 5, 1)], false, Markers);
    }

    [Fact]
    public void Inside_region_not_indented()
    {
        AssertRanges(
        [
            /* 1*/ "var x;",
            /* 2*/ "#region",
            /* 3*/ "void foo() {",
            /* 4*/ "     ",
            /* 5*/ "     return 0;",
            /* 6*/ "  }",
            /* 7*/ "#endregion",
            /* 8*/ "",
        ], [R(2, 7, -1, true), R(3, 6, 0)], false, Markers);
    }

    [Fact]
    public void Empty_Regions()
    {
        AssertRanges(
        [
            /* 1*/ "var x;",
            /* 2*/ "#region",
            /* 3*/ "#endregion",
            /* 4*/ "#region",
            /* 5*/ "",
            /* 6*/ "#endregion",
            /* 7*/ "var y;",
        ], [R(2, 3, -1, true), R(4, 6, -1, true)], false, Markers);
    }

    [Fact]
    public void Nested_Regions()
    {
        AssertRanges(
        [
            /* 1*/ "var x;",
            /* 2*/ "#region",
            /* 3*/ "#region",
            /* 4*/ "",
            /* 5*/ "#endregion",
            /* 6*/ "#endregion",
            /* 7*/ "var y;",
        ], [R(2, 6, -1, true), R(3, 5, 0, true)], false, Markers);
    }

    [Fact]
    public void Nested_Regions_2()
    {
        AssertRanges(
        [
            /* 1*/ "class A {",
            /* 2*/ "  #region",
            /* 3*/ "",
            /* 4*/ "  #region",
            /* 5*/ "",
            /* 6*/ "  #endregion",
            /* 7*/ "  // comment",
            /* 8*/ "  #endregion",
            /* 9*/ "}",
        ], [R(1, 8, -1), R(2, 8, 0, true), R(4, 6, 1, true)], false, Markers);
    }

    [Fact]
    public void Incomplete_Regions()
    {
        AssertRanges(
        [
            /* 1*/ "class A {",
            /* 2*/ "#region",
            /* 3*/ "  // comment",
            /* 4*/ "}",
        ], [R(2, 3, -1)], false, Markers);
    }

    [Fact]
    public void Incomplete_Regions_2()
    {
        AssertRanges(
        [
            /* 1*/ "",
            /* 2*/ "#region",
            /* 3*/ "#region",
            /* 4*/ "#region",
            /* 5*/ "  // comment",
            /* 6*/ "#endregion",
            /* 7*/ "#endregion",
            /* 8*/ " // hello",
        ], [R(3, 7, -1, true), R(4, 6, 0, true)], false, Markers);
    }

    [Fact]
    public void Indented_region_before()
    {
        AssertRanges(
        [
            /* 1*/ "if (x)",
            /* 2*/ "  return;",
            /* 3*/ "",
            /* 4*/ "#region",
            /* 5*/ "  // comment",
            /* 6*/ "#endregion",
        ], [R(1, 3, -1), R(4, 6, -1, true)], false, Markers);
    }

    [Fact]
    public void Indented_region_before_2()
    {
        AssertRanges(
        [
            /* 1*/ "if (x)",
            /* 2*/ "  log();",
            /* 3*/ "",
            /* 4*/ "    #region",
            /* 5*/ "      // comment",
            /* 6*/ "    #endregion",
        ], [R(1, 6, -1), R(2, 6, 0), R(4, 6, 1, true)], false, Markers);
    }

    [Fact]
    public void Indented_region_in_between()
    {
        AssertRanges(
        [
            /* 1*/ "#region",
            /* 2*/ "  // comment",
            /* 3*/ "  if (x)",
            /* 4*/ "    return;",
            /* 5*/ "",
            /* 6*/ "#endregion",
        ], [R(1, 6, -1, true), R(3, 5, 0)], false, Markers);
    }

    [Fact]
    public void Indented_region_after()
    {
        AssertRanges(
        [
            /* 1*/ "#region",
            /* 2*/ "  // comment",
            /* 3*/ "",
            /* 4*/ "#endregion",
            /* 5*/ "  if (x)",
            /* 6*/ "    return;",
        ], [R(1, 4, -1, true), R(5, 6, -1)], false, Markers);
    }

    [Fact]
    public void With_off_side()
    {
        AssertRanges(
        [
            /* 1*/ "#region",
            /* 2*/ "  ",
            /* 3*/ "",
            /* 4*/ "#endregion",
            /* 5*/ "",
        ], [R(1, 4, -1, true)], true, Markers);
    }

    [Fact]
    public void Nested_with_off_side()
    {
        AssertRanges(
        [
            /* 1*/ "#region",
            /* 2*/ "  ",
            /* 3*/ "#region",
            /* 4*/ "",
            /* 5*/ "#endregion",
            /* 6*/ "",
            /* 7*/ "#endregion",
            /* 8*/ "",
        ], [R(1, 7, -1, true), R(3, 5, 0, true)], true, Markers);
    }

    [Fact]
    public void Issue_35981()
    {
        AssertRanges(
        [
            /* 1*/ "function thisFoldsToEndOfPage() {",
            /* 2*/ "  const variable = []",
            /* 3*/ "    // #region",
            /* 4*/ "    .reduce((a, b) => a,[]);",
            /* 5*/ "}",
            /* 6*/ "",
            /* 7*/ "function thisFoldsProperly() {",
            /* 8*/ "  const foo = \"bar\"",
            /* 9*/ "}",
        ], [R(1, 4, -1), R(2, 4, 0), R(7, 8, -1)], false, Markers);
    }

    [Fact]
    public void Misspelled_Markers()
    {
        AssertRanges(
        [
            /* 1*/ "#Region",
            /* 2*/ "#endregion",
            /* 3*/ "#regionsandmore",
            /* 4*/ "#endregion",
            /* 5*/ "#region",
            /* 6*/ "#end region",
            /* 7*/ "#region",
            /* 8*/ "#endregionff",
        ], [], true, Markers);
    }

    [Fact]
    public void Issue_79359()
    {
        AssertRanges(
        [
            /* 1*/ "#region",
            /* 2*/ "",
            /* 3*/ "class A",
            /* 4*/ "  foo",
            /* 5*/ "",
            /* 6*/ "class A",
            /* 7*/ "  foo",
            /* 8*/ "",
            /* 9*/ "#endregion",
        ], [R(1, 9, -1, true), R(3, 4, 0), R(6, 7, 0)], true, Markers);
    }

    [Fact]
    public void Markers_with_stateful_flags()
    {
        AssertRanges(["#region", "content", "#endregion"], [R(1, 3, -1, true)], false,
            new FoldingMarkers(JsRegex.FromLiteral("^\\s*#region\\b", "g"), JsRegex.FromLiteral("^\\s*#endregion\\b", "g")));

        AssertRanges(["#REGION", "content", "#endregion"], [R(1, 3, -1, true)], false,
            new FoldingMarkers(JsRegex.FromLiteral("^\\s*#region\\b", "gi"), JsRegex.FromLiteral("^\\s*#endregion\\b", "g")));

        AssertRanges(["#REGION", "content", "#ENDREGION"], [], false,
            new FoldingMarkers(JsRegex.FromLiteral("^\\s*#region\\b", "gi"), JsRegex.FromLiteral("^\\s*#endregion\\b", "g")));

        AssertRanges(["#REGION", "content", "#ENDREGION"], [], false,
            new FoldingMarkers(JsRegex.FromLiteral("^\\s*#region\\b", "g"), JsRegex.FromLiteral("^\\s*#endregion\\b", "gi")));
    }

    // ---- indentFold.test.ts: suite('Indentation Folding')

    [Fact]
    public void Limit_by_indent()
    {
        string[] lines =
        [
            /* 1*/ "A",
            /* 2*/ "  A",
            /* 3*/ "  A",
            /* 4*/ "    A",
            /* 5*/ "      A",
            /* 6*/ "    A",
            /* 7*/ "      A",
            /* 8*/ "      A",
            /* 9*/ "         A",
            /* 10*/ "      A",
            /* 11*/ "         A",
            /* 12*/ "  A",
            /* 13*/ "              A",
            /* 14*/ "                 A",
            /* 15*/ "A",
            /* 16*/ "  A",
        ];
        var r1 = (1, 14); // 0
        var r2 = (3, 11); // 1
        var r3 = (4, 5); // 2
        var r4 = (6, 11); // 2
        var r5 = (8, 9); // 3
        var r6 = (10, 11); // 3
        var r7 = (12, 14); // 1
        var r8 = (13, 14); // 4
        var r9 = (15, 16); // 0

        using var model = TestTextModel.Create(string.Join('\n', lines));

        void AssertLimit(int maxEntries, (int, int)[] expectedRanges, string message)
        {
            int? reported = -1;
            var indentRanges = IndentRangeProvider.ComputeRanges(model, true, null, new FoldingLimitReporter(maxEntries, (_, limited) => reported = limited));
            indentRanges.Length.ShouldBeLessThanOrEqualTo(maxEntries, "max " + message);
            var actual = new List<(int, int)>();
            for (var i = 0; i < indentRanges.Length; i++)
            {
                actual.Add((indentRanges.GetStartLineNumber(i), indentRanges.GetEndLineNumber(i)));
            }

            actual.ShouldBe(expectedRanges, false, message);
            reported.ShouldBe(9 <= maxEntries ? null : maxEntries, "limited");
        }

        AssertLimit(1000, [r1, r2, r3, r4, r5, r6, r7, r8, r9], "1000");
        AssertLimit(9, [r1, r2, r3, r4, r5, r6, r7, r8, r9], "9");
        AssertLimit(8, [r1, r2, r3, r4, r5, r6, r7, r9], "8");
        AssertLimit(7, [r1, r2, r3, r4, r5, r7, r9], "7");
        AssertLimit(6, [r1, r2, r3, r4, r7, r9], "6");
        AssertLimit(5, [r1, r2, r3, r7, r9], "5");
        AssertLimit(4, [r1, r2, r7, r9], "4");
        AssertLimit(3, [r1, r2, r9], "3");
        AssertLimit(2, [r1, r9], "2");
        AssertLimit(1, [r1], "1");
        AssertLimit(0, [], "0");
    }
}
