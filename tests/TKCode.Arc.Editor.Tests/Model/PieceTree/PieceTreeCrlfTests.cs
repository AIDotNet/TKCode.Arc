// Ported from vscode's src/vs/editor/test/common/model/pieceTreeTextBuffer/pieceTreeTextBuffer.test.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
// Suites: 'CRLF', 'centralized lineStarts with CRLF', 'random is unsupervised'.

using TKCode.Arc.Editor.Model;
using static TKCode.Arc.Editor.Tests.PieceTreeTestHelpers;

namespace TKCode.Arc.Editor.Tests;

public sealed class PieceTreeCrlfTests
{
    [Fact(DisplayName = "delete CR in CRLF 1")]
    public void DeleteCrInCrlf1()
    {
        var pieceTree = CreateTextBuffer(new[] { string.Empty }, false);
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(0, "a\r\nb");
        pieceTable.Delete(0, 2);

        pieceTable.GetLineCount().ShouldBe(2);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "delete CR in CRLF 2")]
    public void DeleteCrInCrlf2()
    {
        var pieceTree = CreateTextBuffer(new[] { string.Empty }, false);
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(0, "a\r\nb");
        pieceTable.Delete(2, 2);

        pieceTable.GetLineCount().ShouldBe(2);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random bug 1")]
    public void RandomBug1()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty }, false);
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(0, "\n\n\r\r");
        str = str.Substring(0, 0) + "\n\n\r\r" + str.Substring(0);
        pieceTable.Insert(1, "\r\n\r\n");
        str = str.Substring(0, 1) + "\r\n\r\n" + str.Substring(1);
        pieceTable.Delete(5, 3);
        str = str.Substring(0, 5) + str.Substring(5 + 3);
        pieceTable.Delete(2, 3);
        str = str.Substring(0, 2) + str.Substring(2 + 3);

        var lines = PieceTreeStrings.SplitLines(str);
        pieceTable.GetLineCount().ShouldBe(lines.Length);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random bug 2")]
    public void RandomBug2()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty }, false);
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Insert(0, "\n\r\n\r");
        str = str.Substring(0, 0) + "\n\r\n\r" + str.Substring(0);
        pieceTable.Insert(2, "\n\r\r\r");
        str = str.Substring(0, 2) + "\n\r\r\r" + str.Substring(2);
        pieceTable.Delete(4, 1);
        str = str.Substring(0, 4) + str.Substring(4 + 1);

        var lines = PieceTreeStrings.SplitLines(str);
        pieceTable.GetLineCount().ShouldBe(lines.Length);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random bug 3")]
    public void RandomBug3()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty }, false);
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Insert(0, "\n\n\n\r");
        str = str.Substring(0, 0) + "\n\n\n\r" + str.Substring(0);
        pieceTable.Delete(2, 2);
        str = str.Substring(0, 2) + str.Substring(2 + 2);
        pieceTable.Delete(0, 2);
        str = str.Substring(0, 0) + str.Substring(0 + 2);
        pieceTable.Insert(0, "\r\r\r\r");
        str = str.Substring(0, 0) + "\r\r\r\r" + str.Substring(0);
        pieceTable.Insert(2, "\r\n\r\r");
        str = str.Substring(0, 2) + "\r\n\r\r" + str.Substring(2);
        pieceTable.Insert(3, "\r\r\r\n");
        str = str.Substring(0, 3) + "\r\r\r\n" + str.Substring(3);

        var lines = PieceTreeStrings.SplitLines(str);
        pieceTable.GetLineCount().ShouldBe(lines.Length);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random bug 4")]
    public void RandomBug4()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty }, false);
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Insert(0, "\n\n\n\n");
        str = str.Substring(0, 0) + "\n\n\n\n" + str.Substring(0);
        pieceTable.Delete(3, 1);
        str = str.Substring(0, 3) + str.Substring(3 + 1);
        pieceTable.Insert(1, "\r\r\r\r");
        str = str.Substring(0, 1) + "\r\r\r\r" + str.Substring(1);
        pieceTable.Insert(6, "\r\n\n\r");
        str = str.Substring(0, 6) + "\r\n\n\r" + str.Substring(6);
        pieceTable.Delete(5, 3);
        str = str.Substring(0, 5) + str.Substring(5 + 3);

        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random bug 5")]
    public void RandomBug5()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty }, false);
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Insert(0, "\n\n\n\n");
        str = str.Substring(0, 0) + "\n\n\n\n" + str.Substring(0);
        pieceTable.Delete(3, 1);
        str = str.Substring(0, 3) + str.Substring(3 + 1);
        pieceTable.Insert(0, "\n\r\r\n");
        str = str.Substring(0, 0) + "\n\r\r\n" + str.Substring(0);
        pieceTable.Insert(4, "\n\r\r\n");
        str = str.Substring(0, 4) + "\n\r\r\n" + str.Substring(4);
        pieceTable.Delete(4, 3);
        str = str.Substring(0, 4) + str.Substring(4 + 3);
        pieceTable.Insert(5, "\r\r\n\r");
        str = str.Substring(0, 5) + "\r\r\n\r" + str.Substring(5);
        pieceTable.Insert(12, "\n\n\n\r");
        str = str.Substring(0, 12) + "\n\n\n\r" + str.Substring(12);
        pieceTable.Insert(5, "\r\r\r\n");
        str = str.Substring(0, 5) + "\r\r\r\n" + str.Substring(5);
        pieceTable.Insert(20, "\n\n\r\n");
        str = str.Substring(0, 20) + "\n\n\r\n" + str.Substring(20);

        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random bug 6")]
    public void RandomBug6()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty }, false);
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Insert(0, "\n\r\r\n");
        str = str.Substring(0, 0) + "\n\r\r\n" + str.Substring(0);
        pieceTable.Insert(4, "\r\n\n\r");
        str = str.Substring(0, 4) + "\r\n\n\r" + str.Substring(4);
        pieceTable.Insert(3, "\r\n\n\n");
        str = str.Substring(0, 3) + "\r\n\n\n" + str.Substring(3);
        pieceTable.Delete(4, 8);
        str = str.Substring(0, 4) + str.Substring(4 + 8);
        pieceTable.Insert(4, "\r\n\n\r");
        str = str.Substring(0, 4) + "\r\n\n\r" + str.Substring(4);
        pieceTable.Insert(0, "\r\n\n\r");
        str = str.Substring(0, 0) + "\r\n\n\r" + str.Substring(0);
        pieceTable.Delete(4, 0);
        str = str.Substring(0, 4) + str.Substring(4 + 0);
        pieceTable.Delete(8, 4);
        str = str.Substring(0, 8) + str.Substring(8 + 4);

        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random bug 8")]
    public void RandomBug8()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty }, false);
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Insert(0, "\r\n\n\r");
        str = str.Substring(0, 0) + "\r\n\n\r" + str.Substring(0);
        pieceTable.Delete(1, 0);
        str = str.Substring(0, 1) + str.Substring(1 + 0);
        pieceTable.Insert(3, "\n\n\n\r");
        str = str.Substring(0, 3) + "\n\n\n\r" + str.Substring(3);
        pieceTable.Insert(7, "\n\n\r\n");
        str = str.Substring(0, 7) + "\n\n\r\n" + str.Substring(7);

        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random bug 7")]
    public void RandomBug7()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty }, false);
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Insert(0, "\r\r\n\n");
        str = str.Substring(0, 0) + "\r\r\n\n" + str.Substring(0);
        pieceTable.Insert(4, "\r\n\n\r");
        str = str.Substring(0, 4) + "\r\n\n\r" + str.Substring(4);
        pieceTable.Insert(7, "\n\r\r\r");
        str = str.Substring(0, 7) + "\n\r\r\r" + str.Substring(7);
        pieceTable.Insert(11, "\n\n\r\n");
        str = str.Substring(0, 11) + "\n\n\r\n" + str.Substring(11);
        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random bug 10")]
    public void RandomBug10()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty }, false);
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Insert(0, "qneW");
        str = str.Substring(0, 0) + "qneW" + str.Substring(0);
        pieceTable.Insert(0, "YhIl");
        str = str.Substring(0, 0) + "YhIl" + str.Substring(0);
        pieceTable.Insert(0, "qdsm");
        str = str.Substring(0, 0) + "qdsm" + str.Substring(0);
        pieceTable.Delete(7, 0);
        str = str.Substring(0, 7) + str.Substring(7 + 0);
        pieceTable.Insert(12, "iiPv");
        str = str.Substring(0, 12) + "iiPv" + str.Substring(12);
        pieceTable.Insert(9, "V\rSA");
        str = str.Substring(0, 9) + "V\rSA" + str.Substring(9);

        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random bug 9")]
    public void RandomBug9()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty }, false);
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Insert(0, "\n\n\n\n");
        str = str.Substring(0, 0) + "\n\n\n\n" + str.Substring(0);
        pieceTable.Insert(3, "\n\r\n\r");
        str = str.Substring(0, 3) + "\n\r\n\r" + str.Substring(3);
        pieceTable.Insert(2, "\n\r\n\n");
        str = str.Substring(0, 2) + "\n\r\n\n" + str.Substring(2);
        pieceTable.Insert(0, "\n\n\r\r");
        str = str.Substring(0, 0) + "\n\n\r\r" + str.Substring(0);
        pieceTable.Insert(3, "\r\r\r\r");
        str = str.Substring(0, 3) + "\r\r\r\r" + str.Substring(3);
        pieceTable.Insert(3, "\n\n\r\r");
        str = str.Substring(0, 3) + "\n\n\r\r" + str.Substring(3);

        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }
}

public sealed class PieceTreeCentralizedLineStartsCrlfTests
{
    [Fact(DisplayName = "delete CR in CRLF 1")]
    public void DeleteCrInCrlf1()
    {
        var pieceTree = CreateTextBuffer(new[] { "a\r\nb" }, false);
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Delete(2, 2);
        pieceTable.GetLineCount().ShouldBe(2);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "delete CR in CRLF 2")]
    public void DeleteCrInCrlf2()
    {
        var pieceTree = CreateTextBuffer(new[] { "a\r\nb" });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Delete(0, 2);

        pieceTable.GetLineCount().ShouldBe(2);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random bug 1")]
    public void RandomBug1()
    {
        var str = "\n\n\r\r";
        var pieceTree = CreateTextBuffer(new[] { "\n\n\r\r" }, false);
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Insert(1, "\r\n\r\n");
        str = str.Substring(0, 1) + "\r\n\r\n" + str.Substring(1);
        pieceTable.Delete(5, 3);
        str = str.Substring(0, 5) + str.Substring(5 + 3);
        pieceTable.Delete(2, 3);
        str = str.Substring(0, 2) + str.Substring(2 + 3);

        var lines = PieceTreeStrings.SplitLines(str);
        pieceTable.GetLineCount().ShouldBe(lines.Length);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random bug 2")]
    public void RandomBug2()
    {
        var str = "\n\r\n\r";
        var pieceTree = CreateTextBuffer(new[] { "\n\r\n\r" }, false);
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Insert(2, "\n\r\r\r");
        str = str.Substring(0, 2) + "\n\r\r\r" + str.Substring(2);
        pieceTable.Delete(4, 1);
        str = str.Substring(0, 4) + str.Substring(4 + 1);

        var lines = PieceTreeStrings.SplitLines(str);
        pieceTable.GetLineCount().ShouldBe(lines.Length);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random bug 3")]
    public void RandomBug3()
    {
        var str = "\n\n\n\r";
        var pieceTree = CreateTextBuffer(new[] { "\n\n\n\r" }, false);
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Delete(2, 2);
        str = str.Substring(0, 2) + str.Substring(2 + 2);
        pieceTable.Delete(0, 2);
        str = str.Substring(0, 0) + str.Substring(0 + 2);
        pieceTable.Insert(0, "\r\r\r\r");
        str = str.Substring(0, 0) + "\r\r\r\r" + str.Substring(0);
        pieceTable.Insert(2, "\r\n\r\r");
        str = str.Substring(0, 2) + "\r\n\r\r" + str.Substring(2);
        pieceTable.Insert(3, "\r\r\r\n");
        str = str.Substring(0, 3) + "\r\r\r\n" + str.Substring(3);

        var lines = PieceTreeStrings.SplitLines(str);
        pieceTable.GetLineCount().ShouldBe(lines.Length);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random bug 4")]
    public void RandomBug4()
    {
        var str = "\n\n\n\n";
        var pieceTree = CreateTextBuffer(new[] { "\n\n\n\n" }, false);
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Delete(3, 1);
        str = str.Substring(0, 3) + str.Substring(3 + 1);
        pieceTable.Insert(1, "\r\r\r\r");
        str = str.Substring(0, 1) + "\r\r\r\r" + str.Substring(1);
        pieceTable.Insert(6, "\r\n\n\r");
        str = str.Substring(0, 6) + "\r\n\n\r" + str.Substring(6);
        pieceTable.Delete(5, 3);
        str = str.Substring(0, 5) + str.Substring(5 + 3);

        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random bug 5")]
    public void RandomBug5()
    {
        var str = "\n\n\n\n";
        var pieceTree = CreateTextBuffer(new[] { "\n\n\n\n" }, false);
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Delete(3, 1);
        str = str.Substring(0, 3) + str.Substring(3 + 1);
        pieceTable.Insert(0, "\n\r\r\n");
        str = str.Substring(0, 0) + "\n\r\r\n" + str.Substring(0);
        pieceTable.Insert(4, "\n\r\r\n");
        str = str.Substring(0, 4) + "\n\r\r\n" + str.Substring(4);
        pieceTable.Delete(4, 3);
        str = str.Substring(0, 4) + str.Substring(4 + 3);
        pieceTable.Insert(5, "\r\r\n\r");
        str = str.Substring(0, 5) + "\r\r\n\r" + str.Substring(5);
        pieceTable.Insert(12, "\n\n\n\r");
        str = str.Substring(0, 12) + "\n\n\n\r" + str.Substring(12);
        pieceTable.Insert(5, "\r\r\r\n");
        str = str.Substring(0, 5) + "\r\r\r\n" + str.Substring(5);
        pieceTable.Insert(20, "\n\n\r\n");
        str = str.Substring(0, 20) + "\n\n\r\n" + str.Substring(20);

        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random bug 6")]
    public void RandomBug6()
    {
        var str = "\n\r\r\n";
        var pieceTree = CreateTextBuffer(new[] { "\n\r\r\n" }, false);
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Insert(4, "\r\n\n\r");
        str = str.Substring(0, 4) + "\r\n\n\r" + str.Substring(4);
        pieceTable.Insert(3, "\r\n\n\n");
        str = str.Substring(0, 3) + "\r\n\n\n" + str.Substring(3);
        pieceTable.Delete(4, 8);
        str = str.Substring(0, 4) + str.Substring(4 + 8);
        pieceTable.Insert(4, "\r\n\n\r");
        str = str.Substring(0, 4) + "\r\n\n\r" + str.Substring(4);
        pieceTable.Insert(0, "\r\n\n\r");
        str = str.Substring(0, 0) + "\r\n\n\r" + str.Substring(0);
        pieceTable.Delete(4, 0);
        str = str.Substring(0, 4) + str.Substring(4 + 0);
        pieceTable.Delete(8, 4);
        str = str.Substring(0, 8) + str.Substring(8 + 4);

        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random bug 7")]
    public void RandomBug7()
    {
        var str = "\r\n\n\r";
        var pieceTree = CreateTextBuffer(new[] { "\r\n\n\r" }, false);
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Delete(1, 0);
        str = str.Substring(0, 1) + str.Substring(1 + 0);
        pieceTable.Insert(3, "\n\n\n\r");
        str = str.Substring(0, 3) + "\n\n\n\r" + str.Substring(3);
        pieceTable.Insert(7, "\n\n\r\n");
        str = str.Substring(0, 7) + "\n\n\r\n" + str.Substring(7);

        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random bug 8")]
    public void RandomBug8()
    {
        var str = "\r\r\n\n";
        var pieceTree = CreateTextBuffer(new[] { "\r\r\n\n" }, false);
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Insert(4, "\r\n\n\r");
        str = str.Substring(0, 4) + "\r\n\n\r" + str.Substring(4);
        pieceTable.Insert(7, "\n\r\r\r");
        str = str.Substring(0, 7) + "\n\r\r\r" + str.Substring(7);
        pieceTable.Insert(11, "\n\n\r\n");
        str = str.Substring(0, 11) + "\n\n\r\n" + str.Substring(11);
        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random bug 9")]
    public void RandomBug9()
    {
        var str = "qneW";
        var pieceTree = CreateTextBuffer(new[] { "qneW" }, false);
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Insert(0, "YhIl");
        str = str.Substring(0, 0) + "YhIl" + str.Substring(0);
        pieceTable.Insert(0, "qdsm");
        str = str.Substring(0, 0) + "qdsm" + str.Substring(0);
        pieceTable.Delete(7, 0);
        str = str.Substring(0, 7) + str.Substring(7 + 0);
        pieceTable.Insert(12, "iiPv");
        str = str.Substring(0, 12) + "iiPv" + str.Substring(12);
        pieceTable.Insert(9, "V\rSA");
        str = str.Substring(0, 9) + "V\rSA" + str.Substring(9);

        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random bug 10")]
    public void RandomBug10()
    {
        var str = "\n\n\n\n";
        var pieceTree = CreateTextBuffer(new[] { "\n\n\n\n" }, false);
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Insert(3, "\n\r\n\r");
        str = str.Substring(0, 3) + "\n\r\n\r" + str.Substring(3);
        pieceTable.Insert(2, "\n\r\n\n");
        str = str.Substring(0, 2) + "\n\r\n\n" + str.Substring(2);
        pieceTable.Insert(0, "\n\n\r\r");
        str = str.Substring(0, 0) + "\n\n\r\r" + str.Substring(0);
        pieceTable.Insert(3, "\r\r\r\r");
        str = str.Substring(0, 3) + "\r\r\r\r" + str.Substring(3);
        pieceTable.Insert(3, "\n\n\r\r");
        str = str.Substring(0, 3) + "\n\n\r\r" + str.Substring(3);

        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random chunk bug 1")]
    public void RandomChunkBug1()
    {
        var pieceTree = CreateTextBuffer(new[] { "\n\r\r\n\n\n\r\n\r" }, false);
        var pieceTable = pieceTree.GetPieceTree();

        var str = "\n\r\r\n\n\n\r\n\r";
        pieceTable.Delete(0, 2);
        str = str.Substring(0, 0) + str.Substring(0 + 2);
        pieceTable.Insert(1, "\r\r\n\n");
        str = str.Substring(0, 1) + "\r\r\n\n" + str.Substring(1);
        pieceTable.Insert(7, "\r\r\r\r");
        str = str.Substring(0, 7) + "\r\r\r\r" + str.Substring(7);

        pieceTable.GetLinesRawContent().ShouldBe(str);
        TestLineStarts(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random chunk bug 2")]
    public void RandomChunkBug2()
    {
        var pieceTree = CreateTextBuffer(new[] { "\n\r\n\n\n\r\n\r\n\r\r\n\n\n\r\r\n\r\n" }, false);
        var pieceTable = pieceTree.GetPieceTree();
        var str = "\n\r\n\n\n\r\n\r\n\r\r\n\n\n\r\r\n\r\n";
        pieceTable.Insert(16, "\r\n\r\r");
        str = str.Substring(0, 16) + "\r\n\r\r" + str.Substring(16);
        pieceTable.Insert(13, "\n\n\r\r");
        str = str.Substring(0, 13) + "\n\n\r\r" + str.Substring(13);
        pieceTable.Insert(19, "\n\n\r\n");
        str = str.Substring(0, 19) + "\n\n\r\n" + str.Substring(19);
        pieceTable.Delete(5, 0);
        str = str.Substring(0, 5) + str.Substring(5 + 0);
        pieceTable.Delete(11, 2);
        str = str.Substring(0, 11) + str.Substring(11 + 2);

        pieceTable.GetLinesRawContent().ShouldBe(str);
        TestLineStarts(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random chunk bug 3")]
    public void RandomChunkBug3()
    {
        var pieceTree = CreateTextBuffer(new[] { "\r\n\n\n\n\n\n\r\n" }, false);
        var pieceTable = pieceTree.GetPieceTree();
        var str = "\r\n\n\n\n\n\n\r\n";
        pieceTable.Insert(4, "\n\n\r\n\r\r\n\n\r");
        str = str.Substring(0, 4) + "\n\n\r\n\r\r\n\n\r" + str.Substring(4);
        pieceTable.Delete(4, 4);
        str = str.Substring(0, 4) + str.Substring(4 + 4);
        pieceTable.Insert(11, "\r\n\r\n\n\r\r\n\n");
        str = str.Substring(0, 11) + "\r\n\r\n\n\r\r\n\n" + str.Substring(11);
        pieceTable.Delete(1, 2);
        str = str.Substring(0, 1) + str.Substring(1 + 2);

        pieceTable.GetLinesRawContent().ShouldBe(str);
        TestLineStarts(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random chunk bug 4")]
    public void RandomChunkBug4()
    {
        var pieceTree = CreateTextBuffer(new[] { "\n\r\n\r" }, false);
        var pieceTable = pieceTree.GetPieceTree();
        var str = "\n\r\n\r";
        pieceTable.Insert(4, "\n\n\r\n");
        str = str.Substring(0, 4) + "\n\n\r\n" + str.Substring(4);
        pieceTable.Insert(3, "\r\n\n\n");
        str = str.Substring(0, 3) + "\r\n\n\n" + str.Substring(3);

        pieceTable.GetLinesRawContent().ShouldBe(str);
        TestLineStarts(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }
}

public sealed class PieceTreeRandomUnsupervisedTests
{
    [Fact(DisplayName = "splitting large change buffer")]
    public void SplittingLargeChangeBuffer()
    {
        var pieceTree = CreateTextBuffer(new[] { string.Empty }, false);
        var pieceTable = pieceTree.GetPieceTree();
        var str = string.Empty;

        pieceTable.Insert(0, "WUZ\nXVZY\n");
        str = str.Substring(0, 0) + "WUZ\nXVZY\n" + str.Substring(0);
        pieceTable.Insert(8, "\r\r\nZXUWVW");
        str = str.Substring(0, 8) + "\r\r\nZXUWVW" + str.Substring(8);
        pieceTable.Delete(10, 7);
        str = str.Substring(0, 10) + str.Substring(10 + 7);
        pieceTable.Delete(10, 1);
        str = str.Substring(0, 10) + str.Substring(10 + 1);
        pieceTable.Insert(4, "VX\r\r\nWZVZ");
        str = str.Substring(0, 4) + "VX\r\r\nWZVZ" + str.Substring(4);
        pieceTable.Delete(11, 3);
        str = str.Substring(0, 11) + str.Substring(11 + 3);
        pieceTable.Delete(12, 4);
        str = str.Substring(0, 12) + str.Substring(12 + 4);
        pieceTable.Delete(8, 0);
        str = str.Substring(0, 8) + str.Substring(8 + 0);
        pieceTable.Delete(10, 2);
        str = str.Substring(0, 10) + str.Substring(10 + 2);
        pieceTable.Insert(0, "VZXXZYZX\r");
        str = str.Substring(0, 0) + "VZXXZYZX\r" + str.Substring(0);

        pieceTable.GetLinesRawContent().ShouldBe(str);

        TestLineStarts(str, pieceTable);
        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random insert delete")]
    public void RandomInsertDelete()
    {
        var random = new Random(1001);
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { str }, false);
        var pieceTable = pieceTree.GetPieceTree();
        for (var i = 0; i < 1000; i++)
        {
            if (random.NextDouble() < 0.6)
            {
                // insert
                var text = RandomStr(random, 100);
                var pos = RandomInt(random, str.Length + 1);
                pieceTable.Insert(pos, text);
                str = str.Substring(0, pos) + text + str.Substring(pos);
            }
            else
            {
                // delete
                var pos = RandomInt(random, str.Length);
                var length = Math.Min(str.Length - pos, (int)Math.Floor(random.NextDouble() * 10));
                pieceTable.Delete(pos, length);
                str = str.Substring(0, pos) + str.Substring(pos + length);
            }
        }

        pieceTable.GetLinesRawContent().ShouldBe(str);

        TestLineStarts(str, pieceTable);
        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random chunks")]
    public void RandomChunks()
    {
        var random = new Random(2002);
        var chunks = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            chunks.Add(RandomStr(random, 1000));
        }

        var pieceTree = CreateTextBuffer(chunks.ToArray(), false);
        var pieceTable = pieceTree.GetPieceTree();
        var str = string.Join(string.Empty, chunks);

        for (var i = 0; i < 1000; i++)
        {
            if (random.NextDouble() < 0.6)
            {
                // insert
                var text = RandomStr(random, 100);
                var pos = RandomInt(random, str.Length + 1);
                pieceTable.Insert(pos, text);
                str = str.Substring(0, pos) + text + str.Substring(pos);
            }
            else
            {
                // delete
                var pos = RandomInt(random, str.Length);
                var length = Math.Min(str.Length - pos, (int)Math.Floor(random.NextDouble() * 10));
                pieceTable.Delete(pos, length);
                str = str.Substring(0, pos) + str.Substring(pos + length);
            }
        }

        pieceTable.GetLinesRawContent().ShouldBe(str);
        TestLineStarts(str, pieceTable);
        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random chunks 2")]
    public void RandomChunks2()
    {
        var random = new Random(3003);
        var chunks = new List<string> { RandomStr(random, 1000) };

        var pieceTree = CreateTextBuffer(chunks.ToArray(), false);
        var pieceTable = pieceTree.GetPieceTree();
        var str = string.Join(string.Empty, chunks);

        for (var i = 0; i < 50; i++)
        {
            if (random.NextDouble() < 0.6)
            {
                // insert
                var text = RandomStr(random, 30);
                var pos = RandomInt(random, str.Length + 1);
                pieceTable.Insert(pos, text);
                str = str.Substring(0, pos) + text + str.Substring(pos);
            }
            else
            {
                // delete
                var pos = RandomInt(random, str.Length);
                var length = Math.Min(str.Length - pos, (int)Math.Floor(random.NextDouble() * 10));
                pieceTable.Delete(pos, length);
                str = str.Substring(0, pos) + str.Substring(pos + length);
            }

            TestLinesContent(str, pieceTable);
        }

        pieceTable.GetLinesRawContent().ShouldBe(str);
        TestLineStarts(str, pieceTable);
        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }
}
