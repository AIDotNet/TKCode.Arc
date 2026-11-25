// The "chunk based search" suite of vscode's
// src/vs/editor/test/common/model/pieceTreeTextBuffer/pieceTreeTextBuffer.test.ts, commit b57d5c9e, MIT licensed.
// These four tests were skipped when the piece tree was ported; they run against the tree itself here.

using Shouldly;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class PieceTreeChunkSearchTests
{
    private static SearchData SearchData(string source, string? wordSeparators, string? simpleSearch, bool matchCase = false) =>
        new(JsRegex.Create(source, isRegex: true, matchCase, multiline: false), wordSeparators is null ? null : new WordCharacterClassifier(wordSeparators), simpleSearch);

    [Fact]
    public void Issue_45892_Buffer_Is_Empty_But_We_Still_Try_To_Search()
    {
        var pieceTree = PieceTreeTestHelpers.CreateTextBuffer([string.Empty]);
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Delete(0, 1);

        var ret = pieceTable.FindMatchesLineByLine(
            new TextRange(1, 1, 1, 1),
            SearchData("abc", ",./", "abc"),
            captureMatches: true,
            limitResultCount: 1000);

        ret.Count.ShouldBe(0);
    }

    [Fact]
    public void Issue_45770_FindInNode_Should_Not_Cross_Node_Boundary()
    {
        var pieceTree = PieceTreeTestHelpers.CreateTextBuffer(
        [
            string.Join(
                '\n',
                "balabalababalabalababalabalaba",
                "balabalababalabalababalabalaba",
                string.Empty,
                "* [ ] task1",
                "* [x] task2 balabalaba",
                "* [ ] task 3"),
        ]);
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Delete(0, 62);
        pieceTable.Delete(16, 1);

        pieceTable.Insert(16, " ", true);
        var ret = pieceTable.FindMatchesLineByLine(
            new TextRange(1, 1, 4, 13),
            SearchData("\\[", ",./", "["),
            captureMatches: true,
            limitResultCount: 1000);

        ret.Count.ShouldBe(3);
        ret[0].Range.ShouldBe(new TextRange(2, 3, 2, 4));
        ret[1].Range.ShouldBe(new TextRange(3, 3, 3, 4));
        ret[2].Range.ShouldBe(new TextRange(4, 3, 4, 4));
    }

    [Fact]
    public void Search_Searching_From_The_Middle()
    {
        var pieceTree = PieceTreeTestHelpers.CreateTextBuffer(["def\ndbcabc"]);
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Delete(4, 1);
        var ret = pieceTable.FindMatchesLineByLine(
            new TextRange(2, 3, 2, 6),
            SearchData("a", null, "a"),
            captureMatches: true,
            limitResultCount: 1000);
        ret.Count.ShouldBe(1);
        ret[0].Range.ShouldBe(new TextRange(2, 3, 2, 4));

        pieceTable.Delete(4, 1);
        ret = pieceTable.FindMatchesLineByLine(
            new TextRange(2, 2, 2, 5),
            SearchData("a", null, "a"),
            captureMatches: true,
            limitResultCount: 1000);
        ret.Count.ShouldBe(1);
        ret[0].Range.ShouldBe(new TextRange(2, 2, 2, 3));
    }

    [Fact]
    public void Issue_348942_Search_Should_Not_Read_Buffer_Text_Past_The_End_Of_The_Search_Range()
    {
        var pieceTree = PieceTreeTestHelpers.CreateTextBuffer([string.Empty]);
        var pieceTable = pieceTree.GetPieceTree();
        var searchData = SearchData("foo", null, "foo");

        pieceTable.Insert(0, "abc foo", true);
        var beforeRangeEnd = pieceTable.FindMatchesLineByLine(
            new TextRange(1, 1, 1, 7), searchData, captureMatches: false, limitResultCount: 1000);

        // The deleted 'o' stays in the change buffer right after the remaining text
        pieceTable.Delete(6, 1);
        var afterDeletion = pieceTable.FindMatchesLineByLine(
            new TextRange(1, 1, 1, 7), searchData, captureMatches: false, limitResultCount: 1000);

        beforeRangeEnd.ShouldBeEmpty();
        afterDeletion.ShouldBeEmpty();
    }
}
