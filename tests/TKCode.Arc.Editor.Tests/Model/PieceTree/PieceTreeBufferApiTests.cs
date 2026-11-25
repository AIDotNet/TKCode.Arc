// Ported from vscode's src/vs/editor/test/common/model/pieceTreeTextBuffer/pieceTreeTextBuffer.test.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
// Suites: 'buffer api', 'search offset cache', 'snapshot'.

using TKCode.Arc.Editor.Model;
using static TKCode.Arc.Editor.Tests.PieceTreeTestHelpers;

namespace TKCode.Arc.Editor.Tests;

public sealed class PieceTreeBufferApiTests
{
    [Fact(DisplayName = "equal")]
    public void Equal()
    {
        var a = CreateTextBuffer(new[] { "abc" });
        var b = CreateTextBuffer(new[] { "ab", "c" });
        var c = CreateTextBuffer(new[] { "abd" });
        var d = CreateTextBuffer(new[] { "abcd" });

        a.GetPieceTree().Equal(b.GetPieceTree()).ShouldBeTrue();
        a.GetPieceTree().Equal(c.GetPieceTree()).ShouldBeFalse();
        a.GetPieceTree().Equal(d.GetPieceTree()).ShouldBeFalse();
    }

    [Fact(DisplayName = "equal with more chunks")]
    public void EqualWithMoreChunks()
    {
        var a = CreateTextBuffer(new[] { "ab", "cd", "e" });
        var b = CreateTextBuffer(new[] { "ab", "c", "de" });
        a.GetPieceTree().Equal(b.GetPieceTree()).ShouldBeTrue();
    }

    [Fact(DisplayName = "equal 2, empty buffer")]
    public void Equal2EmptyBuffer()
    {
        var a = CreateTextBuffer(new[] { string.Empty });
        var b = CreateTextBuffer(new[] { string.Empty });

        a.GetPieceTree().Equal(b.GetPieceTree()).ShouldBeTrue();
    }

    [Fact(DisplayName = "equal 3, empty buffer")]
    public void Equal3EmptyBuffer()
    {
        var a = CreateTextBuffer(new[] { "a" });
        var b = CreateTextBuffer(new[] { string.Empty });

        a.GetPieceTree().Equal(b.GetPieceTree()).ShouldBeFalse();
    }

    [Fact(DisplayName = "getLineCharCode - issue #45735")]
    public void GetLineCharCodeIssue45735()
    {
        var pieceTree = CreateTextBuffer(new[] { "LINE1\nline2" });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.GetLineCharCode(1, 0).ShouldBe((int)'L');
        pieceTable.GetLineCharCode(1, 1).ShouldBe((int)'I');
        pieceTable.GetLineCharCode(1, 2).ShouldBe((int)'N');
        pieceTable.GetLineCharCode(1, 3).ShouldBe((int)'E');
        pieceTable.GetLineCharCode(1, 4).ShouldBe((int)'1');
        pieceTable.GetLineCharCode(1, 5).ShouldBe((int)'\n');
        pieceTable.GetLineCharCode(2, 0).ShouldBe((int)'l');
        pieceTable.GetLineCharCode(2, 1).ShouldBe((int)'i');
        pieceTable.GetLineCharCode(2, 2).ShouldBe((int)'n');
        pieceTable.GetLineCharCode(2, 3).ShouldBe((int)'e');
        pieceTable.GetLineCharCode(2, 4).ShouldBe((int)'2');
    }

    [Fact(DisplayName = "getLineCharCode - issue #47733")]
    public void GetLineCharCodeIssue47733()
    {
        var pieceTree = CreateTextBuffer(new[] { string.Empty, "LINE1\n", "line2" });
        var pieceTable = pieceTree.GetPieceTree();
        pieceTable.GetLineCharCode(1, 0).ShouldBe((int)'L');
        pieceTable.GetLineCharCode(1, 1).ShouldBe((int)'I');
        pieceTable.GetLineCharCode(1, 2).ShouldBe((int)'N');
        pieceTable.GetLineCharCode(1, 3).ShouldBe((int)'E');
        pieceTable.GetLineCharCode(1, 4).ShouldBe((int)'1');
        pieceTable.GetLineCharCode(1, 5).ShouldBe((int)'\n');
        pieceTable.GetLineCharCode(2, 0).ShouldBe((int)'l');
        pieceTable.GetLineCharCode(2, 1).ShouldBe((int)'i');
        pieceTable.GetLineCharCode(2, 2).ShouldBe((int)'n');
        pieceTable.GetLineCharCode(2, 3).ShouldBe((int)'e');
        pieceTable.GetLineCharCode(2, 4).ShouldBe((int)'2');
    }

    [Fact(DisplayName = "getNearestChunk")]
    public void GetNearestChunk()
    {
        var pieceTree = CreateTextBuffer(new[] { "012345678" });
        var pt = pieceTree.GetPieceTree();

        pt.Insert(3, "ABC");
        pt.GetLineContent(1).ShouldBe("012ABC345678");
        pt.GetNearestChunk(3).ShouldBe("ABC");
        pt.GetNearestChunk(6).ShouldBe("345678");

        pt.Delete(9, 1);
        pt.GetLineContent(1).ShouldBe("012ABC34578");
        pt.GetNearestChunk(6).ShouldBe("345");
        pt.GetNearestChunk(9).ShouldBe("78");
    }
}

public sealed class PieceTreeSearchOffsetCacheTests
{
    [Fact(DisplayName = "render white space exception")]
    public void RenderWhiteSpaceException()
    {
        var pieceTree = CreateTextBuffer(new[] { "class Name{\n\t\n\t\t\tget() {\n\n\t\t\t}\n\t\t}" });
        var pieceTable = pieceTree.GetPieceTree();
        var str = "class Name{\n\t\n\t\t\tget() {\n\n\t\t\t}\n\t\t}";

        pieceTable.Insert(12, "s");
        str = str.Substring(0, 12) + "s" + str.Substring(12);

        pieceTable.Insert(13, "e");
        str = str.Substring(0, 13) + "e" + str.Substring(13);

        pieceTable.Insert(14, "t");
        str = str.Substring(0, 14) + "t" + str.Substring(14);

        pieceTable.Insert(15, "()");
        str = str.Substring(0, 15) + "()" + str.Substring(15);

        pieceTable.Delete(16, 1);
        str = str.Substring(0, 16) + str.Substring(16 + 1);

        pieceTable.Insert(17, "()");
        str = str.Substring(0, 17) + "()" + str.Substring(17);

        pieceTable.Delete(18, 1);
        str = str.Substring(0, 18) + str.Substring(18 + 1);

        pieceTable.Insert(18, "}");
        str = str.Substring(0, 18) + "}" + str.Substring(18);

        pieceTable.Insert(12, "\n");
        str = str.Substring(0, 12) + "\n" + str.Substring(12);

        pieceTable.Delete(12, 1);
        str = str.Substring(0, 12) + str.Substring(12 + 1);

        pieceTable.Delete(18, 1);
        str = str.Substring(0, 18) + str.Substring(18 + 1);

        pieceTable.Insert(18, "}");
        str = str.Substring(0, 18) + "}" + str.Substring(18);

        pieceTable.Delete(17, 2);
        str = str.Substring(0, 17) + str.Substring(17 + 2);

        pieceTable.Delete(16, 1);
        str = str.Substring(0, 16) + str.Substring(16 + 1);

        pieceTable.Insert(16, ")");
        str = str.Substring(0, 16) + ")" + str.Substring(16);

        pieceTable.Delete(15, 2);
        str = str.Substring(0, 15) + str.Substring(15 + 2);

        var content = pieceTable.GetLinesRawContent();
        content.ShouldBe(str);
    }

    [Fact(DisplayName = "Line breaks replacement is not necessary when EOL is normalized")]
    public void LineBreaksReplacementNotNecessary1()
    {
        var pieceTree = CreateTextBuffer(new[] { "abc" });
        var pieceTable = pieceTree.GetPieceTree();
        var str = "abc";

        pieceTable.Insert(3, "def\nabc");
        str = str + "def\nabc";

        TestLineStarts(str, pieceTable);
        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "Line breaks replacement is not necessary when EOL is normalized 2")]
    public void LineBreaksReplacementNotNecessary2()
    {
        var pieceTree = CreateTextBuffer(new[] { "abc\n" });
        var pieceTable = pieceTree.GetPieceTree();
        var str = "abc\n";

        pieceTable.Insert(4, "def\nabc");
        str = str + "def\nabc";

        TestLineStarts(str, pieceTable);
        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "Line breaks replacement is not necessary when EOL is normalized 3")]
    public void LineBreaksReplacementNotNecessary3()
    {
        var pieceTree = CreateTextBuffer(new[] { "abc\n" });
        var pieceTable = pieceTree.GetPieceTree();
        var str = "abc\n";

        pieceTable.Insert(2, "def\nabc");
        str = str.Substring(0, 2) + "def\nabc" + str.Substring(2);

        TestLineStarts(str, pieceTable);
        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }

    [Fact(DisplayName = "Line breaks replacement is not necessary when EOL is normalized 4")]
    public void LineBreaksReplacementNotNecessary4()
    {
        var pieceTree = CreateTextBuffer(new[] { "abc\n" });
        var pieceTable = pieceTree.GetPieceTree();
        var str = "abc\n";

        pieceTable.Insert(3, "def\nabc");
        str = str.Substring(0, 3) + "def\nabc" + str.Substring(3);

        TestLineStarts(str, pieceTable);
        TestLinesContent(str, pieceTable);
        AssertTreeInvariants(pieceTable);
    }
}

public sealed class PieceTreeSnapshotTests
{
    [Fact(DisplayName = "bug #45564, piece tree pieces should be immutable")]
    public void Bug45564PiecesShouldBeImmutable()
    {
        var pieceTable = CreateTextBuffer(new[] { "\n" });
        ApplyEdit(pieceTable, 2, 1, 2, 1, "!");
        var snapshot = pieceTable.CreateSnapshot(string.Empty);
        var snapshot1 = pieceTable.CreateSnapshot(string.Empty);
        string.Join("\n", pieceTable.GetLinesContent()).ShouldBe(GetValueInSnapshot(snapshot));

        ApplyEdit(pieceTable, 2, 1, 2, 2, string.Empty);
        ApplyEdit(pieceTable, 2, 1, 2, 1, "!");

        string.Join("\n", pieceTable.GetLinesContent()).ShouldBe(GetValueInSnapshot(snapshot1));
    }

    [Fact(DisplayName = "immutable snapshot 1")]
    public void ImmutableSnapshot1()
    {
        var pieceTable = CreateTextBuffer(new[] { "abc\ndef" });
        var snapshot = pieceTable.CreateSnapshot(string.Empty);
        ApplyEdit(pieceTable, 2, 1, 2, 4, string.Empty);

        ApplyEdit(pieceTable, 1, 1, 2, 1, "abc\ndef");

        string.Join("\n", pieceTable.GetLinesContent()).ShouldBe(GetValueInSnapshot(snapshot));
    }

    [Fact(DisplayName = "immutable snapshot 2")]
    public void ImmutableSnapshot2()
    {
        var pieceTable = CreateTextBuffer(new[] { "abc\ndef" });
        var snapshot = pieceTable.CreateSnapshot(string.Empty);
        ApplyEdit(pieceTable, 2, 1, 2, 1, "!");

        ApplyEdit(pieceTable, 2, 1, 2, 2, string.Empty);

        string.Join("\n", pieceTable.GetLinesContent()).ShouldBe(GetValueInSnapshot(snapshot));
    }

    [Fact(DisplayName = "immutable snapshot 3")]
    public void ImmutableSnapshot3()
    {
        var pieceTable = CreateTextBuffer(new[] { "abc\ndef" });
        ApplyEdit(pieceTable, 2, 4, 2, 4, "!");
        var snapshot = pieceTable.CreateSnapshot(string.Empty);
        ApplyEdit(pieceTable, 2, 5, 2, 5, "!");

        string.Join("\n", pieceTable.GetLinesContent()).ShouldNotBe(GetValueInSnapshot(snapshot));
    }
}
