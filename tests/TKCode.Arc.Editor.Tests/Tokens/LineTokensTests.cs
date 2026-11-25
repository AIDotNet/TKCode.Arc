// Ported from vscode (src/vs/editor/test/common/core/lineTokens.test.ts, commit b57d5c9e), MIT license.

using Shouldly;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Tokens;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class LineTokensTests
{
    private readonly record struct LineToken(int StartIndex, int Foreground);

    private static LineTokens CreateLineTokens(string text, LineToken[] tokens)
    {
        var binTokens = new uint[tokens.Length << 1];

        for (int i = 0, len = tokens.Length; i < len; i++)
        {
            binTokens[i << 1] = (uint)(i + 1 < len ? tokens[i + 1].StartIndex : text.Length);
            binTokens[(i << 1) + 1] = (uint)tokens[i].Foreground << MetadataConsts.ForegroundOffset;
        }

        return new LineTokens(binTokens, text, new LanguageIdCodec());
    }

    private static LineTokens CreateTestLineTokens()
    {
        return CreateLineTokens(
            "Hello world, this is a lovely day",
            [
                new(0, 1), // Hello_
                new(6, 2), // world,_
                new(13, 3), // this_
                new(18, 4), // is_
                new(21, 5), // a_
                new(23, 6), // lovely_
                new(30, 7), // day
            ]);
    }

    private static string RenderLineTokens(LineTokens tokens)
    {
        var result = "";
        var str = tokens.GetLineContent();
        var lastOffset = 0;
        for (var i = 0; i < tokens.GetCount(); i++)
        {
            result += str.Substring(lastOffset, tokens.GetEndOffset(i) - lastOffset);
            result += $"({tokens.GetMetadata(i)})";
            lastOffset = tokens.GetEndOffset(i);
        }
        return result;
    }

    [Fact]
    public void WithInserted_1()
    {
        var lineTokens = CreateTestLineTokens();
        RenderLineTokens(lineTokens).ShouldBe("Hello (32768)world, (65536)this (98304)is (131072)a (163840)lovely (196608)day(229376)");

        var lineTokens2 = lineTokens.WithInserted([
            new InsertedToken(0, "1", 0),
            new InsertedToken(6, "2", 0),
            new InsertedToken(9, "3", 0),
        ]);

        RenderLineTokens(lineTokens2).ShouldBe("1(0)Hello (32768)2(0)wor(65536)3(0)ld, (65536)this (98304)is (131072)a (163840)lovely (196608)day(229376)");
    }

    [Fact]
    public void WithInserted_Tokens_At_The_Same_Position()
    {
        var lineTokens = CreateTestLineTokens();
        RenderLineTokens(lineTokens).ShouldBe("Hello (32768)world, (65536)this (98304)is (131072)a (163840)lovely (196608)day(229376)");

        var lineTokens2 = lineTokens.WithInserted([
            new InsertedToken(0, "1", 0),
            new InsertedToken(0, "2", 0),
            new InsertedToken(0, "3", 0),
        ]);

        RenderLineTokens(lineTokens2).ShouldBe("1(0)2(0)3(0)Hello (32768)world, (65536)this (98304)is (131072)a (163840)lovely (196608)day(229376)");
    }

    [Fact]
    public void WithInserted_Tokens_At_The_End()
    {
        var lineTokens = CreateTestLineTokens();
        RenderLineTokens(lineTokens).ShouldBe("Hello (32768)world, (65536)this (98304)is (131072)a (163840)lovely (196608)day(229376)");

        var lineTokens2 = lineTokens.WithInserted([
            new InsertedToken("Hello world, this is a lovely day".Length - 1, "1", 0),
            new InsertedToken("Hello world, this is a lovely day".Length, "2", 0),
        ]);

        RenderLineTokens(lineTokens2).ShouldBe("Hello (32768)world, (65536)this (98304)is (131072)a (163840)lovely (196608)da(229376)1(0)y(229376)2(0)");
    }

    [Fact]
    public void Basics()
    {
        var lineTokens = CreateTestLineTokens();

        lineTokens.GetLineContent().ShouldBe("Hello world, this is a lovely day");
        lineTokens.GetLineContent().Length.ShouldBe(33);
        lineTokens.GetCount().ShouldBe(7);

        lineTokens.GetStartOffset(0).ShouldBe(0);
        lineTokens.GetEndOffset(0).ShouldBe(6);
        lineTokens.GetStartOffset(1).ShouldBe(6);
        lineTokens.GetEndOffset(1).ShouldBe(13);
        lineTokens.GetStartOffset(2).ShouldBe(13);
        lineTokens.GetEndOffset(2).ShouldBe(18);
        lineTokens.GetStartOffset(3).ShouldBe(18);
        lineTokens.GetEndOffset(3).ShouldBe(21);
        lineTokens.GetStartOffset(4).ShouldBe(21);
        lineTokens.GetEndOffset(4).ShouldBe(23);
        lineTokens.GetStartOffset(5).ShouldBe(23);
        lineTokens.GetEndOffset(5).ShouldBe(30);
        lineTokens.GetStartOffset(6).ShouldBe(30);
        lineTokens.GetEndOffset(6).ShouldBe(33);
    }

    [Fact]
    public void FindToken()
    {
        var lineTokens = CreateTestLineTokens();

        int[] expected =
        [
            0, 0, 0, 0, 0, 0, // 0..5
            1, 1, 1, 1, 1, 1, 1, // 6..12
            2, 2, 2, 2, 2, // 13..17
            3, 3, 3, // 18..20
            4, 4, // 21..22
            5, 5, 5, 5, 5, 5, 5, // 23..29
            6, 6, 6, 6, 6, // 30..34
        ];
        for (var offset = 0; offset < expected.Length; offset++)
        {
            lineTokens.FindTokenIndexAtOffset(offset).ShouldBe(expected[offset], $"offset {offset}");
        }
    }

    private readonly record struct TestViewLineToken(int EndIndex, int Foreground);

    private static void AssertViewLineTokens(IViewLineTokens actualTokens, TestViewLineToken[] expected)
    {
        var actual = new List<TestViewLineToken>();
        for (int i = 0, len = actualTokens.GetCount(); i < len; i++)
        {
            actual.Add(new TestViewLineToken(actualTokens.GetEndOffset(i), (int)actualTokens.GetForeground(i)));
        }
        actual.ShouldBe(expected);
    }

    [Fact]
    public void Inflate()
    {
        var lineTokens = CreateTestLineTokens();
        AssertViewLineTokens(lineTokens.Inflate(), [
            new(6, 1),
            new(13, 2),
            new(18, 3),
            new(21, 4),
            new(23, 5),
            new(30, 6),
            new(33, 7),
        ]);
    }

    [Fact]
    public void SliceAndInflate()
    {
        var lineTokens = CreateTestLineTokens();
        AssertViewLineTokens(lineTokens.SliceAndInflate(0, 33, 0), [
            new(6, 1),
            new(13, 2),
            new(18, 3),
            new(21, 4),
            new(23, 5),
            new(30, 6),
            new(33, 7),
        ]);

        AssertViewLineTokens(lineTokens.SliceAndInflate(0, 32, 0), [
            new(6, 1),
            new(13, 2),
            new(18, 3),
            new(21, 4),
            new(23, 5),
            new(30, 6),
            new(32, 7),
        ]);

        AssertViewLineTokens(lineTokens.SliceAndInflate(0, 30, 0), [
            new(6, 1),
            new(13, 2),
            new(18, 3),
            new(21, 4),
            new(23, 5),
            new(30, 6),
        ]);

        AssertViewLineTokens(lineTokens.SliceAndInflate(0, 30, 1), [
            new(7, 1),
            new(14, 2),
            new(19, 3),
            new(22, 4),
            new(24, 5),
            new(31, 6),
        ]);

        AssertViewLineTokens(lineTokens.SliceAndInflate(6, 18, 0), [
            new(7, 2),
            new(12, 3),
        ]);

        AssertViewLineTokens(lineTokens.SliceAndInflate(7, 18, 0), [
            new(6, 2),
            new(11, 3),
        ]);

        AssertViewLineTokens(lineTokens.SliceAndInflate(6, 17, 0), [
            new(7, 2),
            new(11, 3),
        ]);

        AssertViewLineTokens(lineTokens.SliceAndInflate(6, 19, 0), [
            new(7, 2),
            new(12, 3),
            new(13, 4),
        ]);
    }
}
