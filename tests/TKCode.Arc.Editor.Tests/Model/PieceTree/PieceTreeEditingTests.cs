// Ported from vscode's src/vs/editor/test/common/model/pieceTreeTextBuffer/pieceTreeTextBuffer.test.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
// Suites: 'inserts and deletes', 'prefix sum for line feed', 'offset 2 position', 'get text in range'.

using TKCode.Arc.Editor.Model;
using static TKCode.Arc.Editor.Tests.PieceTreeTestHelpers;

namespace TKCode.Arc.Editor.Tests;

public sealed class PieceTreeEditingTests
{
    [Fact(DisplayName = "basic insert/delete")]
    public void BasicInsertDelete()
    {
        var pieceTree = CreateTextBuffer(new[] { "This is a document with some text." });
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Insert(34, "This is some more text to insert at offset 34.");
        pieceTable.GetLinesRawContent().ShouldBe(
            "This is a document with some text.This is some more text to insert at offset 34.");
        pieceTable.Delete(42, 5);
        pieceTable.GetLinesRawContent().ShouldBe(
            "This is a document with some text.This is more text to insert at offset 34.");
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "more inserts")]
    public void MoreInserts()
    {
        var pieceTree = CreateTextBuffer(new[] { string.Empty });
        var pt = pieceTree.GetPieceTree();
        pt.Insert(0, "AAA");
        pt.GetLinesRawContent().ShouldBe("AAA");
        pt.Insert(0, "BBB");
        pt.GetLinesRawContent().ShouldBe("BBBAAA");
        pt.Insert(6, "CCC");
        pt.GetLinesRawContent().ShouldBe("BBBAAACCC");
        pt.Insert(5, "DDD");
        pt.GetLinesRawContent().ShouldBe("BBBAADDDACCC");
        AssertTreeInvariants(pt);
    }

    [Fact(DisplayName = "more deletes")]
    public void MoreDeletes()
    {
        var pieceTree = CreateTextBuffer(new[] { "012345678" });
        var pt = pieceTree.GetPieceTree();

        pt.Delete(8, 1);
        pt.GetLinesRawContent().ShouldBe("01234567");
        pt.Delete(0, 1);
        pt.GetLinesRawContent().ShouldBe("1234567");
        pt.Delete(5, 1);
        pt.GetLinesRawContent().ShouldBe("123457");
        pt.Delete(5, 1);
        pt.GetLinesRawContent().ShouldBe("12345");
        pt.Delete(0, 5);
        pt.GetLinesRawContent().ShouldBe(string.Empty);
        AssertTreeInvariants(pt);
    }

    [Fact(DisplayName = "random test 1")]
    public void RandomTest1()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty });
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Insert(0, "ceLPHmFzvCtFeHkCBej ");
        str = str.Substring(0, 0) + "ceLPHmFzvCtFeHkCBej " + str.Substring(0);
        pieceTable.GetLinesRawContent().ShouldBe(str);
        pieceTable.Insert(8, "gDCEfNYiBUNkSwtvB K ");
        str = str.Substring(0, 8) + "gDCEfNYiBUNkSwtvB K " + str.Substring(8);
        pieceTable.GetLinesRawContent().ShouldBe(str);
        pieceTable.Insert(38, "cyNcHxjNPPoehBJldLS ");
        str = str.Substring(0, 38) + "cyNcHxjNPPoehBJldLS " + str.Substring(38);
        pieceTable.GetLinesRawContent().ShouldBe(str);
        pieceTable.Insert(59, "ejMx\nOTgWlbpeDExjOk ");
        str = str.Substring(0, 59) + "ejMx\nOTgWlbpeDExjOk " + str.Substring(59);

        pieceTable.GetLinesRawContent().ShouldBe(str);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random test 2")]
    public void RandomTest2()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(0, "VgPG ");
        str = str.Substring(0, 0) + "VgPG " + str.Substring(0);
        pieceTable.Insert(2, "DdWF ");
        str = str.Substring(0, 2) + "DdWF " + str.Substring(2);
        pieceTable.Insert(0, "hUJc ");
        str = str.Substring(0, 0) + "hUJc " + str.Substring(0);
        pieceTable.Insert(8, "lQEq ");
        str = str.Substring(0, 8) + "lQEq " + str.Substring(8);
        pieceTable.Insert(10, "Gbtp ");
        str = str.Substring(0, 10) + "Gbtp " + str.Substring(10);

        pieceTable.GetLinesRawContent().ShouldBe(str);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random test 3")]
    public void RandomTest3()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(0, "gYSz");
        str = str.Substring(0, 0) + "gYSz" + str.Substring(0);
        pieceTable.Insert(1, "mDQe");
        str = str.Substring(0, 1) + "mDQe" + str.Substring(1);
        pieceTable.Insert(1, "DTMQ");
        str = str.Substring(0, 1) + "DTMQ" + str.Substring(1);
        pieceTable.Insert(2, "GGZB");
        str = str.Substring(0, 2) + "GGZB" + str.Substring(2);
        pieceTable.Insert(12, "wXpq");
        str = str.Substring(0, 12) + "wXpq" + str.Substring(12);
        pieceTable.GetLinesRawContent().ShouldBe(str);
    }

    [Fact(DisplayName = "random delete 1")]
    public void RandomDelete1()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty });
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Insert(0, "vfb");
        str = str.Substring(0, 0) + "vfb" + str.Substring(0);
        pieceTable.GetLinesRawContent().ShouldBe(str);
        pieceTable.Insert(0, "zRq");
        str = str.Substring(0, 0) + "zRq" + str.Substring(0);
        pieceTable.GetLinesRawContent().ShouldBe(str);

        pieceTable.Delete(5, 1);
        str = str.Substring(0, 5) + str.Substring(5 + 1);
        pieceTable.GetLinesRawContent().ShouldBe(str);

        pieceTable.Insert(1, "UNw");
        str = str.Substring(0, 1) + "UNw" + str.Substring(1);
        pieceTable.GetLinesRawContent().ShouldBe(str);

        pieceTable.Delete(4, 3);
        str = str.Substring(0, 4) + str.Substring(4 + 3);
        pieceTable.GetLinesRawContent().ShouldBe(str);

        pieceTable.Delete(1, 4);
        str = str.Substring(0, 1) + str.Substring(1 + 4);
        pieceTable.GetLinesRawContent().ShouldBe(str);

        pieceTable.Delete(0, 1);
        str = str.Substring(0, 0) + str.Substring(0 + 1);
        pieceTable.GetLinesRawContent().ShouldBe(str);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random delete 2")]
    public void RandomDelete2()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty });
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Insert(0, "IDT");
        str = str.Substring(0, 0) + "IDT" + str.Substring(0);
        pieceTable.Insert(3, "wwA");
        str = str.Substring(0, 3) + "wwA" + str.Substring(3);
        pieceTable.Insert(3, "Gnr");
        str = str.Substring(0, 3) + "Gnr" + str.Substring(3);
        pieceTable.Delete(6, 3);
        str = str.Substring(0, 6) + str.Substring(6 + 3);
        pieceTable.Insert(4, "eHp");
        str = str.Substring(0, 4) + "eHp" + str.Substring(4);
        pieceTable.Insert(1, "UAi");
        str = str.Substring(0, 1) + "UAi" + str.Substring(1);
        pieceTable.Insert(2, "FrR");
        str = str.Substring(0, 2) + "FrR" + str.Substring(2);
        pieceTable.Delete(6, 7);
        str = str.Substring(0, 6) + str.Substring(6 + 7);
        pieceTable.Delete(3, 5);
        str = str.Substring(0, 3) + str.Substring(3 + 5);
        pieceTable.GetLinesRawContent().ShouldBe(str);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random delete 3")]
    public void RandomDelete3()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(0, "PqM");
        str = str.Substring(0, 0) + "PqM" + str.Substring(0);
        pieceTable.Delete(1, 2);
        str = str.Substring(0, 1) + str.Substring(1 + 2);
        pieceTable.Insert(1, "zLc");
        str = str.Substring(0, 1) + "zLc" + str.Substring(1);
        pieceTable.Insert(0, "MEX");
        str = str.Substring(0, 0) + "MEX" + str.Substring(0);
        pieceTable.Insert(0, "jZh");
        str = str.Substring(0, 0) + "jZh" + str.Substring(0);
        pieceTable.Insert(8, "GwQ");
        str = str.Substring(0, 8) + "GwQ" + str.Substring(8);
        pieceTable.Delete(5, 6);
        str = str.Substring(0, 5) + str.Substring(5 + 6);
        pieceTable.Insert(4, "ktw");
        str = str.Substring(0, 4) + "ktw" + str.Substring(4);
        pieceTable.Insert(5, "GVu");
        str = str.Substring(0, 5) + "GVu" + str.Substring(5);
        pieceTable.Insert(9, "jdm");
        str = str.Substring(0, 9) + "jdm" + str.Substring(9);
        pieceTable.Insert(15, "na\n");
        str = str.Substring(0, 15) + "na\n" + str.Substring(15);
        pieceTable.Delete(5, 8);
        str = str.Substring(0, 5) + str.Substring(5 + 8);
        pieceTable.Delete(3, 4);
        str = str.Substring(0, 3) + str.Substring(3 + 4);
        pieceTable.GetLinesRawContent().ShouldBe(str);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = @"random insert/delete \r bug 1")]
    public void RandomInsertDeleteCrBug1()
    {
        var str = "a";
        var pieceTree = CreateTextBuffer(new[] { "a" });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Delete(0, 1);
        str = str.Substring(0, 0) + str.Substring(0 + 1);
        pieceTable.Insert(0, "\r\r\n\n");
        str = str.Substring(0, 0) + "\r\r\n\n" + str.Substring(0);
        pieceTable.Delete(3, 1);
        str = str.Substring(0, 3) + str.Substring(3 + 1);
        pieceTable.Insert(2, "\n\n\ra");
        str = str.Substring(0, 2) + "\n\n\ra" + str.Substring(2);
        pieceTable.Delete(4, 3);
        str = str.Substring(0, 4) + str.Substring(4 + 3);
        pieceTable.Insert(2, "\na\r\r");
        str = str.Substring(0, 2) + "\na\r\r" + str.Substring(2);
        pieceTable.Insert(6, "\ra\n\n");
        str = str.Substring(0, 6) + "\ra\n\n" + str.Substring(6);
        pieceTable.Insert(0, "aa\n\n");
        str = str.Substring(0, 0) + "aa\n\n" + str.Substring(0);
        pieceTable.Insert(5, "\n\na\r");
        str = str.Substring(0, 5) + "\n\na\r" + str.Substring(5);

        pieceTable.GetLinesRawContent().ShouldBe(str);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = @"random insert/delete \r bug 2")]
    public void RandomInsertDeleteCrBug2()
    {
        var str = "a";
        var pieceTree = CreateTextBuffer(new[] { "a" });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(1, "\naa\r");
        str = str.Substring(0, 1) + "\naa\r" + str.Substring(1);
        pieceTable.Delete(0, 4);
        str = str.Substring(0, 0) + str.Substring(0 + 4);
        pieceTable.Insert(1, "\r\r\na");
        str = str.Substring(0, 1) + "\r\r\na" + str.Substring(1);
        pieceTable.Insert(2, "\n\r\ra");
        str = str.Substring(0, 2) + "\n\r\ra" + str.Substring(2);
        pieceTable.Delete(4, 1);
        str = str.Substring(0, 4) + str.Substring(4 + 1);
        pieceTable.Insert(8, "\r\n\r\r");
        str = str.Substring(0, 8) + "\r\n\r\r" + str.Substring(8);
        pieceTable.Insert(7, "\n\n\na");
        str = str.Substring(0, 7) + "\n\n\na" + str.Substring(7);
        pieceTable.Insert(13, "a\n\na");
        str = str.Substring(0, 13) + "a\n\na" + str.Substring(13);
        pieceTable.Delete(17, 3);
        str = str.Substring(0, 17) + str.Substring(17 + 3);
        pieceTable.Insert(2, "a\ra\n");
        str = str.Substring(0, 2) + "a\ra\n" + str.Substring(2);

        pieceTable.GetLinesRawContent().ShouldBe(str);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = @"random insert/delete \r bug 3")]
    public void RandomInsertDeleteCrBug3()
    {
        var str = "a";
        var pieceTree = CreateTextBuffer(new[] { "a" });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(0, "\r\na\r");
        str = str.Substring(0, 0) + "\r\na\r" + str.Substring(0);
        pieceTable.Delete(2, 3);
        str = str.Substring(0, 2) + str.Substring(2 + 3);
        pieceTable.Insert(2, "a\r\n\r");
        str = str.Substring(0, 2) + "a\r\n\r" + str.Substring(2);
        pieceTable.Delete(4, 2);
        str = str.Substring(0, 4) + str.Substring(4 + 2);
        pieceTable.Insert(4, "a\n\r\n");
        str = str.Substring(0, 4) + "a\n\r\n" + str.Substring(4);
        pieceTable.Insert(1, "aa\n\r");
        str = str.Substring(0, 1) + "aa\n\r" + str.Substring(1);
        pieceTable.Insert(7, "\na\r\n");
        str = str.Substring(0, 7) + "\na\r\n" + str.Substring(7);
        pieceTable.Insert(5, "\n\na\r");
        str = str.Substring(0, 5) + "\n\na\r" + str.Substring(5);
        pieceTable.Insert(10, "\r\r\n\r");
        str = str.Substring(0, 10) + "\r\r\n\r" + str.Substring(10);
        pieceTable.GetLinesRawContent().ShouldBe(str);
        pieceTable.Delete(21, 3);
        str = str.Substring(0, 21) + str.Substring(21 + 3);

        pieceTable.GetLinesRawContent().ShouldBe(str);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = @"random insert/delete \r bug 4s")]
    public void RandomInsertDeleteCrBug4s()
    {
        var str = "a";
        var pieceTree = CreateTextBuffer(new[] { "a" });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Delete(0, 1);
        str = str.Substring(0, 0) + str.Substring(0 + 1);
        pieceTable.Insert(0, "\naaa");
        str = str.Substring(0, 0) + "\naaa" + str.Substring(0);
        pieceTable.Insert(2, "\n\naa");
        str = str.Substring(0, 2) + "\n\naa" + str.Substring(2);
        pieceTable.Delete(1, 4);
        str = str.Substring(0, 1) + str.Substring(1 + 4);
        pieceTable.Delete(3, 1);
        str = str.Substring(0, 3) + str.Substring(3 + 1);
        pieceTable.Delete(1, 2);
        str = str.Substring(0, 1) + str.Substring(1 + 2);
        pieceTable.Delete(0, 1);
        str = str.Substring(0, 0) + str.Substring(0 + 1);
        pieceTable.Insert(0, "a\n\n\r");
        str = str.Substring(0, 0) + "a\n\n\r" + str.Substring(0);
        pieceTable.Insert(2, "aa\r\n");
        str = str.Substring(0, 2) + "aa\r\n" + str.Substring(2);
        pieceTable.Insert(3, "a\naa");
        str = str.Substring(0, 3) + "a\naa" + str.Substring(3);

        pieceTable.GetLinesRawContent().ShouldBe(str);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = @"random insert/delete \r bug 5")]
    public void RandomInsertDeleteCrBug5()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(0, "\n\n\n\r");
        str = str.Substring(0, 0) + "\n\n\n\r" + str.Substring(0);
        pieceTable.Insert(1, "\n\n\n\r");
        str = str.Substring(0, 1) + "\n\n\n\r" + str.Substring(1);
        pieceTable.Insert(2, "\n\r\r\r");
        str = str.Substring(0, 2) + "\n\r\r\r" + str.Substring(2);
        pieceTable.Insert(8, "\n\r\n\r");
        str = str.Substring(0, 8) + "\n\r\n\r" + str.Substring(8);
        pieceTable.Delete(5, 2);
        str = str.Substring(0, 5) + str.Substring(5 + 2);
        pieceTable.Insert(4, "\n\r\r\r");
        str = str.Substring(0, 4) + "\n\r\r\r" + str.Substring(4);
        pieceTable.Insert(8, "\n\n\n\r");
        str = str.Substring(0, 8) + "\n\n\n\r" + str.Substring(8);
        pieceTable.Delete(0, 7);
        str = str.Substring(0, 0) + str.Substring(0 + 7);
        pieceTable.Insert(1, "\r\n\r\r");
        str = str.Substring(0, 1) + "\r\n\r\r" + str.Substring(1);
        pieceTable.Insert(15, "\n\r\r\r");
        str = str.Substring(0, 15) + "\n\r\r\r" + str.Substring(15);

        pieceTable.GetLinesRawContent().ShouldBe(str);
        AssertTreeInvariants(pieceTable);
    }
}

public sealed class PieceTreePrefixSumTests
{
    [Fact(DisplayName = "basic")]
    public void Basic()
    {
        var pieceTree = CreateTextBuffer(new[] { "1\n2\n3\n4" });
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.GetLineCount().ShouldBe(4);
        pieceTable.GetPositionAt(0).ShouldBe(new TextPosition(1, 1));
        pieceTable.GetPositionAt(1).ShouldBe(new TextPosition(1, 2));
        pieceTable.GetPositionAt(2).ShouldBe(new TextPosition(2, 1));
        pieceTable.GetPositionAt(3).ShouldBe(new TextPosition(2, 2));
        pieceTable.GetPositionAt(4).ShouldBe(new TextPosition(3, 1));
        pieceTable.GetPositionAt(5).ShouldBe(new TextPosition(3, 2));
        pieceTable.GetPositionAt(6).ShouldBe(new TextPosition(4, 1));

        pieceTable.GetOffsetAt(1, 1).ShouldBe(0);
        pieceTable.GetOffsetAt(1, 2).ShouldBe(1);
        pieceTable.GetOffsetAt(2, 1).ShouldBe(2);
        pieceTable.GetOffsetAt(2, 2).ShouldBe(3);
        pieceTable.GetOffsetAt(3, 1).ShouldBe(4);
        pieceTable.GetOffsetAt(3, 2).ShouldBe(5);
        pieceTable.GetOffsetAt(4, 1).ShouldBe(6);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "append")]
    public void Append()
    {
        var pieceTree = CreateTextBuffer(new[] { "a\nb\nc\nde" });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(8, "fh\ni\njk");

        pieceTable.GetLineCount().ShouldBe(6);
        pieceTable.GetPositionAt(9).ShouldBe(new TextPosition(4, 4));
        pieceTable.GetOffsetAt(1, 1).ShouldBe(0);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "insert")]
    public void Insert()
    {
        var pieceTree = CreateTextBuffer(new[] { "a\nb\nc\nde" });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(7, "fh\ni\njk");

        pieceTable.GetLineCount().ShouldBe(6);
        pieceTable.GetPositionAt(6).ShouldBe(new TextPosition(4, 1));
        pieceTable.GetPositionAt(7).ShouldBe(new TextPosition(4, 2));
        pieceTable.GetPositionAt(8).ShouldBe(new TextPosition(4, 3));
        pieceTable.GetPositionAt(9).ShouldBe(new TextPosition(4, 4));
        pieceTable.GetPositionAt(12).ShouldBe(new TextPosition(6, 1));
        pieceTable.GetPositionAt(13).ShouldBe(new TextPosition(6, 2));
        pieceTable.GetPositionAt(14).ShouldBe(new TextPosition(6, 3));

        pieceTable.GetOffsetAt(4, 1).ShouldBe(6);
        pieceTable.GetOffsetAt(4, 2).ShouldBe(7);
        pieceTable.GetOffsetAt(4, 3).ShouldBe(8);
        pieceTable.GetOffsetAt(4, 4).ShouldBe(9);
        pieceTable.GetOffsetAt(6, 1).ShouldBe(12);
        pieceTable.GetOffsetAt(6, 2).ShouldBe(13);
        pieceTable.GetOffsetAt(6, 3).ShouldBe(14);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "delete")]
    public void Delete()
    {
        var pieceTree = CreateTextBuffer(new[] { "a\nb\nc\ndefh\ni\njk" });
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Delete(7, 2);

        pieceTable.GetLinesRawContent().ShouldBe("a\nb\nc\ndh\ni\njk");
        pieceTable.GetLineCount().ShouldBe(6);
        pieceTable.GetPositionAt(6).ShouldBe(new TextPosition(4, 1));
        pieceTable.GetPositionAt(7).ShouldBe(new TextPosition(4, 2));
        pieceTable.GetPositionAt(8).ShouldBe(new TextPosition(4, 3));
        pieceTable.GetPositionAt(9).ShouldBe(new TextPosition(5, 1));
        pieceTable.GetPositionAt(11).ShouldBe(new TextPosition(6, 1));
        pieceTable.GetPositionAt(12).ShouldBe(new TextPosition(6, 2));
        pieceTable.GetPositionAt(13).ShouldBe(new TextPosition(6, 3));

        pieceTable.GetOffsetAt(4, 1).ShouldBe(6);
        pieceTable.GetOffsetAt(4, 2).ShouldBe(7);
        pieceTable.GetOffsetAt(4, 3).ShouldBe(8);
        pieceTable.GetOffsetAt(5, 1).ShouldBe(9);
        pieceTable.GetOffsetAt(6, 1).ShouldBe(11);
        pieceTable.GetOffsetAt(6, 2).ShouldBe(12);
        pieceTable.GetOffsetAt(6, 3).ShouldBe(13);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "add+delete 1")]
    public void AddDelete1()
    {
        var pieceTree = CreateTextBuffer(new[] { "a\nb\nc\nde" });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(8, "fh\ni\njk");
        pieceTable.Delete(7, 2);

        pieceTable.GetLinesRawContent().ShouldBe("a\nb\nc\ndh\ni\njk");
        pieceTable.GetLineCount().ShouldBe(6);
        pieceTable.GetPositionAt(6).ShouldBe(new TextPosition(4, 1));
        pieceTable.GetPositionAt(7).ShouldBe(new TextPosition(4, 2));
        pieceTable.GetPositionAt(8).ShouldBe(new TextPosition(4, 3));
        pieceTable.GetPositionAt(9).ShouldBe(new TextPosition(5, 1));
        pieceTable.GetPositionAt(11).ShouldBe(new TextPosition(6, 1));
        pieceTable.GetPositionAt(12).ShouldBe(new TextPosition(6, 2));
        pieceTable.GetPositionAt(13).ShouldBe(new TextPosition(6, 3));

        pieceTable.GetOffsetAt(4, 1).ShouldBe(6);
        pieceTable.GetOffsetAt(4, 2).ShouldBe(7);
        pieceTable.GetOffsetAt(4, 3).ShouldBe(8);
        pieceTable.GetOffsetAt(5, 1).ShouldBe(9);
        pieceTable.GetOffsetAt(6, 1).ShouldBe(11);
        pieceTable.GetOffsetAt(6, 2).ShouldBe(12);
        pieceTable.GetOffsetAt(6, 3).ShouldBe(13);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "insert random bug 1: prefixSumComputer.removeValues(start, cnt) cnt is 1 based.")]
    public void InsertRandomBug1()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(0, " ZX \n Z\nZ\n YZ\nY\nZXX ");
        str = str.Substring(0, 0) + " ZX \n Z\nZ\n YZ\nY\nZXX " + str.Substring(0);
        pieceTable.Insert(14, "X ZZ\nYZZYZXXY Y XY\n ");
        str = str.Substring(0, 14) + "X ZZ\nYZZYZXXY Y XY\n " + str.Substring(14);

        pieceTable.GetLinesRawContent().ShouldBe(str);
        TestLineStarts(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "insert random bug 2: prefixSumComputer initialize does not do deep copy of UInt32Array.")]
    public void InsertRandomBug2()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(0, "ZYZ\nYY XY\nX \nZ Y \nZ ");
        str = str.Substring(0, 0) + "ZYZ\nYY XY\nX \nZ Y \nZ " + str.Substring(0);
        pieceTable.Insert(3, "XXY \n\nY Y YYY  ZYXY ");
        str = str.Substring(0, 3) + "XXY \n\nY Y YYY  ZYXY " + str.Substring(3);

        pieceTable.GetLinesRawContent().ShouldBe(str);
        TestLineStarts(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "delete random bug 1: I forgot to update the lineFeedCnt when deletion is on one single piece.")]
    public void DeleteRandomBug1()
    {
        var pieceTree = CreateTextBuffer(new[] { string.Empty });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(0, "ba\na\nca\nba\ncbab\ncaa ");
        pieceTable.Insert(13, "cca\naabb\ncac\nccc\nab ");
        pieceTable.Delete(5, 8);
        pieceTable.Delete(30, 2);
        pieceTable.Insert(24, "cbbacccbac\nbaaab\n\nc ");
        pieceTable.Delete(29, 3);
        pieceTable.Delete(23, 9);
        pieceTable.Delete(21, 5);
        pieceTable.Delete(30, 3);
        pieceTable.Insert(3, "cb\nac\nc\n\nacc\nbb\nb\nc ");
        pieceTable.Delete(19, 5);
        pieceTable.Insert(18, "\nbb\n\nacbc\ncbb\nc\nbb\n ");
        pieceTable.Insert(65, "cbccbac\nbc\n\nccabba\n ");
        pieceTable.Insert(77, "a\ncacb\n\nac\n\n\n\n\nabab ");
        pieceTable.Delete(30, 9);
        pieceTable.Insert(45, "b\n\nc\nba\n\nbbbba\n\naa\n ");
        pieceTable.Insert(82, "ab\nbb\ncabacab\ncbc\na ");
        pieceTable.Delete(123, 9);
        pieceTable.Delete(71, 2);
        pieceTable.Insert(33, "acaa\nacb\n\naa\n\nc\n\n\n\n ");

        var str = pieceTable.GetLinesRawContent();
        TestLineStarts(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "delete random bug rb tree 1")]
    public void DeleteRandomBugRbTree1()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { str });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(0, "YXXZ\n\nYY\n");
        str = str.Substring(0, 0) + "YXXZ\n\nYY\n" + str.Substring(0);
        pieceTable.Delete(0, 5);
        str = str.Substring(0, 0) + str.Substring(0 + 5);
        pieceTable.Insert(0, "ZXYY\nX\nZ\n");
        str = str.Substring(0, 0) + "ZXYY\nX\nZ\n" + str.Substring(0);
        pieceTable.Insert(10, "\nXY\nYXYXY");
        str = str.Substring(0, 10) + "\nXY\nYXYXY" + str.Substring(10);
        TestLineStarts(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "delete random bug rb tree 2")]
    public void DeleteRandomBugRbTree2()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { str });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(0, "YXXZ\n\nYY\n");
        str = str.Substring(0, 0) + "YXXZ\n\nYY\n" + str.Substring(0);
        pieceTable.Insert(0, "ZXYY\nX\nZ\n");
        str = str.Substring(0, 0) + "ZXYY\nX\nZ\n" + str.Substring(0);
        pieceTable.Insert(10, "\nXY\nYXYXY");
        str = str.Substring(0, 10) + "\nXY\nYXYXY" + str.Substring(10);
        pieceTable.Insert(8, "YZXY\nZ\nYX");
        str = str.Substring(0, 8) + "YZXY\nZ\nYX" + str.Substring(8);
        pieceTable.Insert(12, "XX\nXXYXYZ");
        str = str.Substring(0, 12) + "XX\nXXYXYZ" + str.Substring(12);
        pieceTable.Delete(0, 4);
        str = str.Substring(0, 0) + str.Substring(0 + 4);

        TestLineStarts(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "delete random bug rb tree 3")]
    public void DeleteRandomBugRbTree3()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { str });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(0, "YXXZ\n\nYY\n");
        str = str.Substring(0, 0) + "YXXZ\n\nYY\n" + str.Substring(0);
        pieceTable.Delete(7, 2);
        str = str.Substring(0, 7) + str.Substring(7 + 2);
        pieceTable.Delete(6, 1);
        str = str.Substring(0, 6) + str.Substring(6 + 1);
        pieceTable.Delete(0, 5);
        str = str.Substring(0, 0) + str.Substring(0 + 5);
        pieceTable.Insert(0, "ZXYY\nX\nZ\n");
        str = str.Substring(0, 0) + "ZXYY\nX\nZ\n" + str.Substring(0);
        pieceTable.Insert(10, "\nXY\nYXYXY");
        str = str.Substring(0, 10) + "\nXY\nYXYXY" + str.Substring(10);
        pieceTable.Insert(8, "YZXY\nZ\nYX");
        str = str.Substring(0, 8) + "YZXY\nZ\nYX" + str.Substring(8);
        pieceTable.Insert(12, "XX\nXXYXYZ");
        str = str.Substring(0, 12) + "XX\nXXYXYZ" + str.Substring(12);
        pieceTable.Delete(0, 4);
        str = str.Substring(0, 0) + str.Substring(0 + 4);
        pieceTable.Delete(30, 3);
        str = str.Substring(0, 30) + str.Substring(30 + 3);

        TestLineStarts(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }
}

public sealed class PieceTreeOffsetToPositionTests
{
    [Fact(DisplayName = "random tests bug 1")]
    public void RandomTestsBug1()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(0, "huuyYzUfKOENwGgZLqn ");
        str = str.Substring(0, 0) + "huuyYzUfKOENwGgZLqn " + str.Substring(0);
        pieceTable.Delete(18, 2);
        str = str.Substring(0, 18) + str.Substring(18 + 2);
        pieceTable.Delete(3, 1);
        str = str.Substring(0, 3) + str.Substring(3 + 1);
        pieceTable.Delete(12, 4);
        str = str.Substring(0, 12) + str.Substring(12 + 4);
        pieceTable.Insert(3, "hMbnVEdTSdhLlPevXKF ");
        str = str.Substring(0, 3) + "hMbnVEdTSdhLlPevXKF " + str.Substring(3);
        pieceTable.Delete(22, 8);
        str = str.Substring(0, 22) + str.Substring(22 + 8);
        pieceTable.Insert(4, "S umSnYrqOmOAV\nEbZJ ");
        str = str.Substring(0, 4) + "S umSnYrqOmOAV\nEbZJ " + str.Substring(4);

        TestLineStarts(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }
}

public sealed class PieceTreeGetTextInRangeTests
{
    [Fact(DisplayName = "getContentInRange")]
    public void GetContentInRange()
    {
        var pieceTree = CreateTextBuffer(new[] { "a\nb\nc\nde" });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(8, "fh\ni\njk");
        pieceTable.Delete(7, 2);
        // 'a\nb\nc\ndh\ni\njk'

        pieceTable.GetValueInRange(new TextRange(1, 1, 1, 3)).ShouldBe("a\n");
        pieceTable.GetValueInRange(new TextRange(2, 1, 2, 3)).ShouldBe("b\n");
        pieceTable.GetValueInRange(new TextRange(3, 1, 3, 3)).ShouldBe("c\n");
        pieceTable.GetValueInRange(new TextRange(4, 1, 4, 4)).ShouldBe("dh\n");
        pieceTable.GetValueInRange(new TextRange(5, 1, 5, 3)).ShouldBe("i\n");
        pieceTable.GetValueInRange(new TextRange(6, 1, 6, 3)).ShouldBe("jk");
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random test value in range")]
    public void RandomTestValueInRange()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { str });
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Insert(0, "ZXXY");
        str = str.Substring(0, 0) + "ZXXY" + str.Substring(0);
        pieceTable.Insert(1, "XZZY");
        str = str.Substring(0, 1) + "XZZY" + str.Substring(1);
        pieceTable.Insert(5, "\nX\n\n");
        str = str.Substring(0, 5) + "\nX\n\n" + str.Substring(5);
        pieceTable.Insert(3, "\nXX\n");
        str = str.Substring(0, 3) + "\nXX\n" + str.Substring(3);
        pieceTable.Insert(12, "YYYX");
        str = str.Substring(0, 12) + "YYYX" + str.Substring(12);

        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random test value in range exception")]
    public void RandomTestValueInRangeException()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { str });
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Insert(0, "XZ\nZ");
        str = str.Substring(0, 0) + "XZ\nZ" + str.Substring(0);
        pieceTable.Delete(0, 3);
        str = str.Substring(0, 0) + str.Substring(0 + 3);
        pieceTable.Delete(0, 1);
        str = str.Substring(0, 0) + str.Substring(0 + 1);
        pieceTable.Insert(0, "ZYX\n");
        str = str.Substring(0, 0) + "ZYX\n" + str.Substring(0);
        pieceTable.Delete(0, 4);
        str = str.Substring(0, 0) + str.Substring(0 + 4);

        pieceTable.GetValueInRange(new TextRange(1, 1, 1, 1));
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random tests bug 1")]
    public void RandomTestsBug1()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(0, "huuyYzUfKOENwGgZLqn ");
        str = str.Substring(0, 0) + "huuyYzUfKOENwGgZLqn " + str.Substring(0);
        pieceTable.Delete(18, 2);
        str = str.Substring(0, 18) + str.Substring(18 + 2);
        pieceTable.Delete(3, 1);
        str = str.Substring(0, 3) + str.Substring(3 + 1);
        pieceTable.Delete(12, 4);
        str = str.Substring(0, 12) + str.Substring(12 + 4);
        pieceTable.Insert(3, "hMbnVEdTSdhLlPevXKF ");
        str = str.Substring(0, 3) + "hMbnVEdTSdhLlPevXKF " + str.Substring(3);
        pieceTable.Delete(22, 8);
        str = str.Substring(0, 22) + str.Substring(22 + 8);
        pieceTable.Insert(4, "S umSnYrqOmOAV\nEbZJ ");
        str = str.Substring(0, 4) + "S umSnYrqOmOAV\nEbZJ " + str.Substring(4);
        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random tests bug 2")]
    public void RandomTestsBug2()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(0, "xfouRDZwdAHjVXJAMV\n ");
        str = str.Substring(0, 0) + "xfouRDZwdAHjVXJAMV\n " + str.Substring(0);
        pieceTable.Insert(16, "dBGndxpFZBEAIKykYYx ");
        str = str.Substring(0, 16) + "dBGndxpFZBEAIKykYYx " + str.Substring(16);
        pieceTable.Delete(7, 6);
        str = str.Substring(0, 7) + str.Substring(7 + 6);
        pieceTable.Delete(9, 7);
        str = str.Substring(0, 9) + str.Substring(9 + 7);
        pieceTable.Delete(17, 6);
        str = str.Substring(0, 17) + str.Substring(17 + 6);
        pieceTable.Delete(0, 4);
        str = str.Substring(0, 0) + str.Substring(0 + 4);
        pieceTable.Insert(9, "qvEFXCNvVkWgvykahYt ");
        str = str.Substring(0, 9) + "qvEFXCNvVkWgvykahYt " + str.Substring(9);
        pieceTable.Delete(4, 6);
        str = str.Substring(0, 4) + str.Substring(4 + 6);
        pieceTable.Insert(11, "OcSChUYT\nzPEBOpsGmR ");
        str = str.Substring(0, 11) + "OcSChUYT\nzPEBOpsGmR " + str.Substring(11);
        pieceTable.Insert(15, "KJCozaXTvkE\nxnqAeTz ");
        str = str.Substring(0, 15) + "KJCozaXTvkE\nxnqAeTz " + str.Substring(15);

        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "get line content")]
    public void GetLineContent()
    {
        var pieceTree = CreateTextBuffer(new[] { "1" });
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.GetLineRawContent(1).ShouldBe("1");
        pieceTable.Insert(1, "2");
        pieceTable.GetLineRawContent(1).ShouldBe("12");
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "get line content basic")]
    public void GetLineContentBasic()
    {
        var pieceTree = CreateTextBuffer(new[] { "1\n2\n3\n4" });
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.GetLineRawContent(1).ShouldBe("1\n");
        pieceTable.GetLineRawContent(2).ShouldBe("2\n");
        pieceTable.GetLineRawContent(3).ShouldBe("3\n");
        pieceTable.GetLineRawContent(4).ShouldBe("4");
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "get line content after inserts/deletes")]
    public void GetLineContentAfterInsertsDeletes()
    {
        var pieceTree = CreateTextBuffer(new[] { "a\nb\nc\nde" });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(8, "fh\ni\njk");
        pieceTable.Delete(7, 2);
        // 'a\nb\nc\ndh\ni\njk'

        pieceTable.GetLineRawContent(1).ShouldBe("a\n");
        pieceTable.GetLineRawContent(2).ShouldBe("b\n");
        pieceTable.GetLineRawContent(3).ShouldBe("c\n");
        pieceTable.GetLineRawContent(4).ShouldBe("dh\n");
        pieceTable.GetLineRawContent(5).ShouldBe("i\n");
        pieceTable.GetLineRawContent(6).ShouldBe("jk");
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random 1")]
    public void Random1()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty });
        var pieceTable = pieceTree.GetPieceTree();

        pieceTable.Insert(0, "J eNnDzQpnlWyjmUu\ny ");
        str = str.Substring(0, 0) + "J eNnDzQpnlWyjmUu\ny " + str.Substring(0);
        pieceTable.Insert(0, "QPEeRAQmRwlJqtZSWhQ ");
        str = str.Substring(0, 0) + "QPEeRAQmRwlJqtZSWhQ " + str.Substring(0);
        pieceTable.Delete(5, 1);
        str = str.Substring(0, 5) + str.Substring(5 + 1);

        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "random 2")]
    public void Random2()
    {
        var str = string.Empty;
        var pieceTree = CreateTextBuffer(new[] { string.Empty });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.Insert(0, "DZoQ tglPCRHMltejRI ");
        str = str.Substring(0, 0) + "DZoQ tglPCRHMltejRI " + str.Substring(0);
        pieceTable.Insert(10, "JRXiyYqJ qqdcmbfkKX ");
        str = str.Substring(0, 10) + "JRXiyYqJ qqdcmbfkKX " + str.Substring(10);
        pieceTable.Delete(16, 3);
        str = str.Substring(0, 16) + str.Substring(16 + 3);
        pieceTable.Delete(25, 1);
        str = str.Substring(0, 25) + str.Substring(25 + 1);
        pieceTable.Insert(18, "vH\nNlvfqQJPm\nSFkhMc ");
        str = str.Substring(0, 18) + "vH\nNlvfqQJPm\nSFkhMc " + str.Substring(18);

        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }
}
