// Ported from vscode's src/vs/editor/test/common/model/pieceTreeTextBuffer/pieceTreeTextBuffer.test.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
// Shared helpers: random input with a seeded System.Random, the consistency checkers and a createTextBuffer.

using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Tests;

internal static class PieceTreeTestHelpers
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ\r\n";

    internal static int RandomInt(Random random, int bound) => bound <= 0 ? 0 : random.Next(bound);

    internal static string RandomChar(Random random) => Alphabet[random.Next(Alphabet.Length)].ToString();

    internal static string RandomStr(Random random, int len)
    {
        var chars = new char[len];
        for (var i = 0; i < len; i++)
        {
            chars[i] = Alphabet[random.Next(Alphabet.Length)];
        }

        return new string(chars);
    }

    internal static string TrimLineFeed(string text)
    {
        if (text.Length == 0)
        {
            return text;
        }

        if (text.Length == 1)
        {
            if (text[text.Length - 1] is '\n' or '\r')
            {
                return string.Empty;
            }

            return text;
        }

        if (text[text.Length - 1] == '\n')
        {
            if (text[text.Length - 2] == '\r')
            {
                return text.Substring(0, text.Length - 2);
            }

            return text.Substring(0, text.Length - 1);
        }

        if (text[text.Length - 1] == '\r')
        {
            return text.Substring(0, text.Length - 1);
        }

        return text;
    }

    /// <summary>The upstream test asks the text buffer for its piece tree; here the buffer is the tree.</summary>
    internal static PieceTreeBase GetPieceTree(this PieceTreeBase tree) => tree;

    internal static PieceTreeBase CreateTextBuffer(string[] val, bool normalizeEol = true)
    {
        var bufferBuilder = new PieceTreeTextBufferBuilder();
        foreach (var chunk in val)
        {
            bufferBuilder.AcceptChunk(chunk);
        }

        var factory = bufferBuilder.Finish(normalizeEol);
        return factory.Create(DefaultEndOfLine.LF).PieceTree;
    }

    internal static void TestLinesContent(string str, PieceTreeBase pieceTable)
    {
        var lines = PieceTreeStrings.SplitLines(str);
        pieceTable.GetLineCount().ShouldBe(lines.Length);
        pieceTable.GetLinesRawContent().ShouldBe(str);
        for (var i = 0; i < lines.Length; i++)
        {
            // The view first: GetLineContent remembers the line it returned, and the view would then be that string.
            pieceTable.GetLineContentSpan(i + 1).ToString().ShouldBe(lines[i]);
            pieceTable.GetLineContent(i + 1).ShouldBe(lines[i]);
            var endColumn = lines[i].Length + (i == lines.Length - 1 ? 1 : 2);
            var value = pieceTable.GetValueInRange(new TextRange(i + 1, 1, i + 1, endColumn));
            TrimLineFeed(value).ShouldBe(lines[i]);
        }
    }

    internal static void TestLineStarts(string str, PieceTreeBase pieceTable)
    {
        var lineStarts = new List<int> { 0 };

        foreach (Match match in Regex.Matches(str, @"\r\n|\r|\n"))
        {
            lineStarts.Add(match.Index + match.Length);
        }

        for (var i = 0; i < lineStarts.Count; i++)
        {
            pieceTable.GetPositionAt(lineStarts[i]).ShouldBe(new TextPosition(i + 1, 1));
            pieceTable.GetOffsetAt(i + 1, 1).ShouldBe(lineStarts[i]);
        }

        for (var i = 1; i < lineStarts.Count; i++)
        {
            var pos = pieceTable.GetPositionAt(lineStarts[i] - 1);
            pieceTable.GetOffsetAt(pos.LineNumber, pos.Column).ShouldBe(lineStarts[i] - 1);
        }
    }

    internal static void AssertTreeInvariants(PieceTreeBase tree)
    {
        var sentinel = tree.Sentinel;
        sentinel.Color.ShouldBe(NodeColor.Black);
        sentinel.Parent.ShouldBeSameAs(sentinel);
        sentinel.Left.ShouldBeSameAs(sentinel);
        sentinel.Right.ShouldBeSameAs(sentinel);
        sentinel.SizeLeft.ShouldBe(0);
        sentinel.LfLeft.ShouldBe(0);
        AssertValidTree(tree);
    }

    private static int Depth(TreeNode n, TreeNode sentinel)
    {
        if (n == sentinel)
        {
            // The leafs are black
            return 1;
        }

        Depth(n.Left, sentinel).ShouldBe(Depth(n.Right, sentinel));
        return (n.Color == NodeColor.Black ? 1 : 0) + Depth(n.Left, sentinel);
    }

    private static (int Size, int LfCnt) AssertValidNode(TreeNode n, TreeNode sentinel)
    {
        if (n == sentinel)
        {
            return (0, 0);
        }

        var l = n.Left;
        var r = n.Right;

        if (n.Color == NodeColor.Red)
        {
            l.Color.ShouldBe(NodeColor.Black);
            r.Color.ShouldBe(NodeColor.Black);
        }

        var actualLeft = AssertValidNode(l, sentinel);
        actualLeft.LfCnt.ShouldBe(n.LfLeft);
        actualLeft.Size.ShouldBe(n.SizeLeft);
        var actualRight = AssertValidNode(r, sentinel);

        return (
            n.SizeLeft + n.Piece.Length + actualRight.Size,
            n.LfLeft + n.Piece.LineFeedCnt + actualRight.LfCnt);
    }

    private static void AssertValidTree(PieceTreeBase tree)
    {
        if (tree.Root == tree.Sentinel)
        {
            return;
        }

        tree.Root.Color.ShouldBe(NodeColor.Black);
        Depth(tree.Root.Left, tree.Sentinel).ShouldBe(Depth(tree.Root.Right, tree.Sentinel));
        AssertValidNode(tree.Root, tree.Sentinel);
    }

    /// <summary>A single vscode edit operation (range replaced by text), applied like PieceTreeTextBuffer.applyEdits.</summary>
    internal static void ApplyEdit(
        PieceTreeBase pieceTable,
        int startLineNumber,
        int startColumn,
        int endLineNumber,
        int endColumn,
        string text)
    {
        if (startLineNumber == endLineNumber && startColumn == endColumn && text.Length == 0)
        {
            // no-op
            return;
        }

        var startOffset = pieceTable.GetOffsetAt(startLineNumber, startColumn);
        var endOffset = pieceTable.GetOffsetAt(endLineNumber, endColumn);
        if (text.Length > 0)
        {
            // replacement
            pieceTable.Delete(startOffset, endOffset - startOffset);
            pieceTable.Insert(startOffset, text, true);
        }
        else
        {
            // deletion
            pieceTable.Delete(startOffset, endOffset - startOffset);
        }
    }

    internal static string GetValueInSnapshot(ITextSnapshot snapshot)
    {
        var ret = new System.Text.StringBuilder();
        var tmp = snapshot.Read();

        while (tmp is not null)
        {
            ret.Append(tmp);
            tmp = snapshot.Read();
        }

        return ret.ToString();
    }
}
