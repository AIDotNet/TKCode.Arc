// Ported from vscode (src/vs/editor/test/common/model/bracketPairColorizer/length.test.ts, smallImmutableSet.test.ts,
// concat23Trees.test.ts and combineTextEditInfos.test.ts, commit b57d5c9e), MIT license.
// Differences: combineTextEditInfos' random edits come from System.Random with the same seeds (upstream's own seeded
// generator draws other numbers); the test checks the same invariant for each.

using Shouldly;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Model.BracketPairs;
using Xunit;
using static TKCode.Arc.Editor.Model.BracketPairs.LengthHelpers;

namespace TKCode.Arc.Editor.Tests;

public sealed class BracketPairColorizerLengthTests
{
    private static string ToStr(Length length) => LengthToObj(length).ToString();

    [Fact]
    public void Basic()
    {
        var l1 = ToLength(100, 10);
        LengthToObj(l1).LineCount.ShouldBe(100);
        LengthToObj(l1).ColumnCount.ShouldBe(10);

        ToStr(LengthAdd(l1, ToLength(100, 10))).ShouldBe("200,10");
        ToStr(LengthAdd(l1, ToLength(0, 10))).ShouldBe("100,20");
    }

    [Fact]
    public void LengthDiffNonNeg()
    {
        ToStr(LengthDiffNonNegative(ToLength(100, 10), ToLength(100, 20))).ShouldBe("0,10");
        ToStr(LengthDiffNonNegative(ToLength(100, 10), ToLength(101, 20))).ShouldBe("1,20");
        ToStr(LengthDiffNonNegative(ToLength(101, 30), ToLength(101, 20))).ShouldBe("0,0");
        ToStr(LengthDiffNonNegative(ToLength(102, 10), ToLength(101, 20))).ShouldBe("0,0");
    }
}

public sealed class BracketPairColorizerImmutableSetTests
{
    [Fact]
    public void Basic()
    {
        var keyProvider = new DenseKeyProvider<string>();

        var empty = SmallImmutableSet<string>.GetEmpty();
        var items1 = empty.Add("item1", keyProvider);
        var items12 = items1.Add("item2", keyProvider);
        var items2 = empty.Add("item2", keyProvider);
        var items21 = items2.Add("item1", keyProvider);

        var items3 = empty.Add("item3", keyProvider);

        items12.Intersects(items1).ShouldBeTrue();
        items12.Has("item1", keyProvider).ShouldBeTrue();

        items12.Intersects(items3).ShouldBeFalse();
        items12.Has("item3", keyProvider).ShouldBeFalse();

        items21.Equals(items12).ShouldBeTrue();
        items21.Equals(items2).ShouldBeFalse();
    }

    [Fact]
    public void Many_Elements()
    {
        var keyProvider = new DenseKeyProvider<string>();

        var set = SmallImmutableSet<string>.GetEmpty();

        for (var i = 0; i < 100; i++)
        {
            keyProvider.GetKey($"item{i}");
            if (i % 2 == 0)
            {
                set = set.Add($"item{i}", keyProvider);
            }
        }

        for (var i = 0; i < 100; i++)
        {
            set.Has($"item{i}", keyProvider).ShouldBe(i % 2 == 0);
        }
    }
}

public sealed class BracketPairColorizerMergeItemsTests
{
    [Fact]
    public void Clone()
    {
        var tree = ListAstNode.Create([new TextAstNode(ToLength(1, 1)), new TextAstNode(ToLength(1, 1))]);

        AreEqual(tree, tree.DeepClone()).ShouldBeTrue();
    }

    private static bool AreEqual(AstNode node1, AstNode node2)
    {
        if (node1.Length != node2.Length)
        {
            return false;
        }

        if (node1.Children.Count != node2.Children.Count)
        {
            return false;
        }

        for (var i = 0; i < node1.Children.Count; i++)
        {
            if (!AreEqual(node1.Children[i], node2.Children[i]))
            {
                return false;
            }
        }

        if (!node1.MissingOpeningBracketIds.Equals(node2.MissingOpeningBracketIds))
        {
            return false;
        }

        return node1.Kind == node2.Kind;
    }

    private static void TestMerge(List<AstNode> lists)
    {
        var node = (Concat23Trees.Concat(lists.Select(l => l.DeepClone()).ToList()) ?? ListAstNode.Create([])).FlattenLists();
        // This trivial merge does not maintain the (2,3) tree invariant.
        var referenceNode = ListAstNode.Create(lists).FlattenLists();

        AreEqual(node, referenceNode).ShouldBeTrue("merge23Trees failed");
    }

    [Fact]
    public void Empty_List() => TestMerge([]);

    [Fact]
    public void Same_Height_Lists()
    {
        var textNode = new TextAstNode(ToLength(1, 1));
        var tree = ListAstNode.Create([textNode.DeepClone(), textNode.DeepClone()]);
        TestMerge([tree.DeepClone(), tree.DeepClone(), tree.DeepClone(), tree.DeepClone(), tree.DeepClone()]);
    }

    [Fact]
    public void Different_Height_Lists_1()
    {
        var textNode = new TextAstNode(ToLength(1, 1));
        var tree1 = ListAstNode.Create([textNode.DeepClone(), textNode.DeepClone()]);
        var tree2 = ListAstNode.Create([tree1.DeepClone(), tree1.DeepClone()]);

        TestMerge([tree1, tree2]);
    }

    [Fact]
    public void Different_Height_Lists_2()
    {
        var textNode = new TextAstNode(ToLength(1, 1));
        var tree1 = ListAstNode.Create([textNode.DeepClone(), textNode.DeepClone()]);
        var tree2 = ListAstNode.Create([tree1.DeepClone(), tree1.DeepClone()]);

        TestMerge([tree2, tree1]);
    }

    [Fact]
    public void Different_Height_Lists_3()
    {
        var textNode = new TextAstNode(ToLength(1, 1));
        var tree1 = ListAstNode.Create([textNode.DeepClone(), textNode.DeepClone()]);
        var tree2 = ListAstNode.Create([tree1.DeepClone(), tree1.DeepClone()]);

        TestMerge([tree2, tree1, tree1, tree2, tree2]);
    }
}

public sealed class CombineTextEditInfosTests
{
    public static TheoryData<int> Seeds => [.. Enumerable.Range(0, 50)];

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Test(int seed)
    {
        var rng = new Random(seed);

        const string str = "abcde\nfghij\nklmno\npqrst\n";
        using var textModelS0 = TestTextModel.Create(str);

        var edits1 = GetRandomEditInfos(textModelS0, rng.Next(1, 4), rng);
        using var textModelS1 = TestTextModel.Create(textModelS0.GetValue());
        textModelS1.ApplyEdits(edits1.Select(ToEdit).ToArray());

        var edits2 = GetRandomEditInfos(textModelS1, rng.Next(1, 4), rng);
        using var textModelS2 = TestTextModel.Create(textModelS1.GetValue());
        textModelS2.ApplyEdits(edits2.Select(ToEdit).ToArray());

        var combinedEdits = TextEditInfoCombiner.CombineTextEditInfos(edits1, edits2);
        foreach (var edit in combinedEdits)
        {
            var start = LengthToPosition(edit.StartOffset);
            var end = LengthToPosition(LengthAdd(edit.StartOffset, edit.NewLength));
            var range = new TextRange(start.LineNumber, start.Column, end.LineNumber, end.Column);
            var value = textModelS2.GetValueInRange(range);
            value.All(c => c is 'L' or 'C' or '\n').ShouldBeTrue("Invalid edit: " + value);
            var oldStart = LengthToPosition(edit.StartOffset);
            var oldEnd = LengthToPosition(edit.EndOffset);
            textModelS2.ApplyEdits([new SingleEditOperation(range, textModelS0.GetValueInRange(new TextRange(oldStart.LineNumber, oldStart.Column, oldEnd.LineNumber, oldEnd.Column)))]);
        }

        textModelS2.GetValue().ShouldBe(textModelS0.GetValue());
    }

    internal static List<TextEditInfo> GetRandomEditInfos(TextModel textModel, int count, Random rng, bool disjoint = false)
    {
        var edits = new List<TextEditInfo>();
        var i = 0;
        for (var j = 0; j < count; j++)
        {
            edits.Add(GetRandomEdit(textModel, i, rng));
            i = textModel.GetOffsetAt(LengthToPosition(edits[j].EndOffset)) + (disjoint ? 1 : 0);
        }

        return edits;
    }

    private static TextEditInfo GetRandomEdit(TextModel textModel, int rangeOffsetStart, Random rng)
    {
        var textModelLength = textModel.GetValueLength();
        var offsetStart = rng.Next(Math.Min(rangeOffsetStart, textModelLength), textModelLength);
        var offsetEnd = rng.Next(offsetStart, textModelLength);

        var lineCount = rng.Next(0, 3);
        var columnCount = rng.Next(0, 5);

        return new TextEditInfo(
            PositionToLength(textModel.GetPositionAt(offsetStart)),
            PositionToLength(textModel.GetPositionAt(offsetEnd)),
            ToLength(lineCount, columnCount));
    }

    internal static SingleEditOperation ToEdit(TextEditInfo editInfo)
    {
        var l = LengthToObj(editInfo.NewLength);
        var text = string.Empty;

        for (var i = 0; i < l.LineCount; i++)
        {
            text += "LLL\n";
        }

        for (var i = 0; i < l.ColumnCount; i++)
        {
            text += "C";
        }

        var start = LengthToPosition(editInfo.StartOffset);
        var end = LengthToPosition(editInfo.EndOffset);
        return new SingleEditOperation(new TextRange(start.LineNumber, start.Column, end.LineNumber, end.Column), text);
    }
}
