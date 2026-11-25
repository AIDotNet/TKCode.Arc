// Tests of sticky scroll's copy of the indentation folding ranges, ported from vscode
// (src/vs/editor/contrib/folding/test/browser/indentRangeProvider.test.ts, commit b57d5c9e), MIT license: the
// 'Indentation Folding' and 'Folding with regions' suites (all but 'Markers with stateful flags', whose subject, the
// lastIndex of JavaScript's global expressions, has no .NET counterpart). Own tests of sanitizeRanges
// (syntaxRangeProvider.ts) follow.

using System.Text.RegularExpressions;
using Shouldly;
using TKCode.Arc.Editor.Contrib.StickyScroll;
using TKCode.Arc.Editor.Languages;
using Xunit;

namespace TKCode.Arc.Editor.Tests.Contrib.StickyScroll;

public sealed class StickyIndentRangesTests
{
    private static readonly FoldingMarkers Markers = new(new Regex(@"^\s*#region\b"), new Regex(@"^\s*#endregion\b"));

    private static (int Start, int End, int Parent) R(int startLineNumber, int endLineNumber, int parentIndex) => (startLineNumber, endLineNumber, parentIndex);

    private static void AssertRanges(string[] lines, (int, int, int)[] expected, bool offside, FoldingMarkers? markers = null)
    {
        using var model = TestTextModel.Create(string.Join('\n', lines));
        var actual = StickyIndentRanges.ComputeRanges(model, offside, markers);
        var actualRanges = new List<(int, int, int)>();
        for (var i = 0; i < actual.Length; i++)
        {
            actualRanges.Add((actual.GetStartLineNumber(i), actual.GetEndLineNumber(i), actual.GetParentIndex(i)));
        }

        actualRanges.ShouldBe(expected);
    }

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
    public void Fold_Java() => AssertRanges(
        ["class A {", "  void foo() {", "    console.log();", "    console.log();", "  }", "", "  void bar() {", "    console.log();", "  }", "}", "interface B {", "  void bar();", "}"],
        [R(1, 9, -1), R(2, 4, 0), R(7, 8, 0), R(11, 12, -1)], false);

    [Fact]
    public void Fold_Javadoc() => AssertRanges(
        ["/**", " * Comment", " */", "class A {", "  void foo() {", "  }", "}"],
        [R(1, 3, -1), R(4, 6, -1)], false);

    [Fact]
    public void Fold_whitespace_Java() => AssertRanges(
        ["class A {", "", "  void foo() {", "     ", "     return 0;", "  }", "      ", "}"],
        [R(1, 7, -1), R(3, 5, 0)], false);

    [Fact]
    public void Fold_whitespace_Python() => AssertRanges(
        ["def a:", "  pass", "   ", "  def b:", "    pass", "  ", "      ", "def c: # since there was a deintent here"],
        [R(1, 5, -1), R(4, 5, 0)], true);

    [Fact]
    public void Fold_tabs() => AssertRanges(
        ["class A {", "\t\t", "\tvoid foo() {", "\t \t//hello", "\t    return 0;", "  \t}", "      ", "}"],
        [R(1, 7, -1), R(3, 5, 0)], false);

    [Fact]
    public void Inside_region_indented() => AssertRanges(
        ["class A {", "  #region", "  void foo() {", "     ", "     return 0;", "  }", "  #endregion", "}"],
        [R(1, 7, -1), R(2, 7, 0), R(3, 5, 1)], false, Markers);

    [Fact]
    public void Inside_region_not_indented() => AssertRanges(
        ["var x;", "#region", "void foo() {", "     ", "     return 0;", "  }", "#endregion", ""],
        [R(2, 7, -1), R(3, 6, 0)], false, Markers);

    [Fact]
    public void Empty_regions() => AssertRanges(
        ["var x;", "#region", "#endregion", "#region", "", "#endregion", "var y;"],
        [R(2, 3, -1), R(4, 6, -1)], false, Markers);

    [Fact]
    public void Nested_regions() => AssertRanges(
        ["var x;", "#region", "#region", "", "#endregion", "#endregion", "var y;"],
        [R(2, 6, -1), R(3, 5, 0)], false, Markers);

    [Fact]
    public void Nested_regions_2() => AssertRanges(
        ["class A {", "  #region", "", "  #region", "", "  #endregion", "  // comment", "  #endregion", "}"],
        [R(1, 8, -1), R(2, 8, 0), R(4, 6, 1)], false, Markers);

    [Fact]
    public void Incomplete_regions() => AssertRanges(
        ["class A {", "#region", "  // comment", "}"],
        [R(2, 3, -1)], false, Markers);

    [Fact]
    public void Incomplete_regions_2() => AssertRanges(
        ["", "#region", "#region", "#region", "  // comment", "#endregion", "#endregion", " // hello"],
        [R(3, 7, -1), R(4, 6, 0)], false, Markers);

    [Fact]
    public void Indented_region_before() => AssertRanges(
        ["if (x)", "  return;", "", "#region", "  // comment", "#endregion"],
        [R(1, 3, -1), R(4, 6, -1)], false, Markers);

    [Fact]
    public void Indented_region_before_2() => AssertRanges(
        ["if (x)", "  log();", "", "    #region", "      // comment", "    #endregion"],
        [R(1, 6, -1), R(2, 6, 0), R(4, 6, 1)], false, Markers);

    [Fact]
    public void Indented_region_in_between() => AssertRanges(
        ["#region", "  // comment", "  if (x)", "    return;", "", "#endregion"],
        [R(1, 6, -1), R(3, 5, 0)], false, Markers);

    [Fact]
    public void Indented_region_after() => AssertRanges(
        ["#region", "  // comment", "", "#endregion", "  if (x)", "    return;"],
        [R(1, 4, -1), R(5, 6, -1)], false, Markers);

    [Fact]
    public void With_off_side() => AssertRanges(
        ["#region", "  ", "", "#endregion", ""],
        [R(1, 4, -1)], true, Markers);

    [Fact]
    public void Nested_with_off_side() => AssertRanges(
        ["#region", "  ", "#region", "", "#endregion", "", "#endregion", ""],
        [R(1, 7, -1), R(3, 5, 0)], true, Markers);

    [Fact]
    public void Issue_35981() => AssertRanges(
        ["function thisFoldsToEndOfPage() {", "  const variable = []", "    // #region", "    .reduce((a, b) => a,[]);", "}", "", "function thisFoldsProperly() {", "  const foo = \"bar\"", "}"],
        [R(1, 4, -1), R(2, 4, 0), R(7, 8, -1)], false, Markers);

    [Fact]
    public void Misspelled_markers() => AssertRanges(
        ["#Region", "#endregion", "#regionsandmore", "#endregion", "#region", "#end region", "#region", "#endregionff"],
        [], true, Markers);

    [Fact]
    public void Issue_79359() => AssertRanges(
        ["#region", "", "class A", "  foo", "", "class A", "  foo", "", "#endregion"],
        [R(1, 9, -1), R(3, 4, 0), R(6, 7, 0)], true, Markers);

    // ---- own tests

    [Fact]
    public void Markers_with_different_options_are_matched_separately()
    {
        var markers = new FoldingMarkers(new Regex(@"^\s*#region\b", RegexOptions.IgnoreCase), new Regex(@"^\s*#endregion\b"));
        AssertRanges(["#REGION", "content", "#endregion"], [R(1, 3, -1)], false, markers);
        AssertRanges(["#REGION", "content", "#ENDREGION"], [], false, markers);
    }

    [Fact]
    public void Sanitize_ranges_sorts_by_start_and_rank_and_drops_ranges_starting_on_the_same_line()
    {
        var regions = StickySyntaxRanges.SanitizeRanges(
        [
            new(5, 8, 0, null),
            new(1, 10, 0, null),
            new(2, 4, 1, null),
            new(3, 6, 0, null),
            new(2, 3, 0, FoldingRangeKind.Comment),
        ]);

        var actual = Enumerable.Range(0, regions.Length).Select(i => (regions.GetStartLineNumber(i), regions.GetEndLineNumber(i), regions.GetParentIndex(i))).ToList();
        actual.ShouldBe([(1, 10, -1), (2, 3, 0), (3, 6, 0), (5, 8, 0)]);
        regions.Types![1].ShouldBe("comment");
    }
}
