// Ported from vscode (src/vs/base/test/common/strings.test.ts, commit b57d5c9e), MIT license.
// Only the tests for the grapheme machinery are ported; the tests from Grapheme_Iteration_Steps_Over_Clusters on are
// not upstream's (upstream covers these functions through the cursor tests, which come with the cursor).

using Shouldly;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class StringsGraphemeTests
{
    [Fact]
    public void Issue_115221_IsEmojiImprecise_Misses_Star()
    {
        var codePoint = Strings.GetNextCodePoint("⭐", "⭐".Length, 0);
        Strings.IsEmojiImprecise(codePoint).ShouldBeTrue();
    }

    [Fact]
    public void IsFullWidthCharacter()
    {
        // Fullwidth ASCII (FF01-FF5E)
        Strings.IsFullWidthCharacter('Ａ').ShouldBeTrue("Ａ U+FF21 fullwidth A");
        Strings.IsFullWidthCharacter('？').ShouldBeTrue("？ U+FF1F fullwidth question mark");
        Strings.IsFullWidthCharacter('＃').ShouldBeTrue("＃ U+FF03 fullwidth number sign");
        Strings.IsFullWidthCharacter('＝').ShouldBeTrue("＝ U+FF1D fullwidth equals sign");

        // Hiragana (3040-309F)
        Strings.IsFullWidthCharacter('あ').ShouldBeTrue("あ U+3042 hiragana");

        // Fullwidth symbols (FFE0-FFE6)
        Strings.IsFullWidthCharacter('￥').ShouldBeTrue("￥ U+FFE5 fullwidth yen sign");

        // Regular ASCII should not be full width
        Strings.IsFullWidthCharacter('A').ShouldBeFalse("A regular ASCII");
        Strings.IsFullWidthCharacter('?').ShouldBeFalse("? regular ASCII");
    }

    [Fact]
    public void GetGraphemeBreakType()
    {
        Strings.GetGraphemeBreakType(0xBC1).ShouldBe(GraphemeBreakType.SpacingMark);
    }

    // Not upstream's from here on.

    [Fact]
    public void Grapheme_Break_Types_Of_Common_Code_Points()
    {
        Strings.GetGraphemeBreakType('\r').ShouldBe(GraphemeBreakType.CR);
        Strings.GetGraphemeBreakType('\n').ShouldBe(GraphemeBreakType.LF);
        Strings.GetGraphemeBreakType('\t').ShouldBe(GraphemeBreakType.Control);
        Strings.GetGraphemeBreakType('a').ShouldBe(GraphemeBreakType.Other);
        Strings.GetGraphemeBreakType(0x0301).ShouldBe(GraphemeBreakType.Extend);
        Strings.GetGraphemeBreakType(0x200D).ShouldBe(GraphemeBreakType.ZWJ);
        Strings.GetGraphemeBreakType(0x1F1E9).ShouldBe(GraphemeBreakType.RegionalIndicator);
        Strings.GetGraphemeBreakType(0x1F468).ShouldBe(GraphemeBreakType.ExtendedPictographic);
        Strings.GetGraphemeBreakType(0x1100).ShouldBe(GraphemeBreakType.L);
        Strings.GetGraphemeBreakType(0xAC00).ShouldBe(GraphemeBreakType.LV);
    }

    [Fact]
    public void Grapheme_Iteration_Steps_Over_Clusters()
    {
        const string combining = "áb";
        Strings.NextCharLength(combining, 0).ShouldBe(2);
        Strings.NextCharLength(combining, 2).ShouldBe(1);
        Strings.PrevCharLength(combining, 2).ShouldBe(2);
        Strings.PrevCharLength(combining, 3).ShouldBe(1);

        Strings.NextCharLength("\r\nx", 0).ShouldBe(2);
        Strings.PrevCharLength("x\r\n", 3).ShouldBe(2);

        // man ZWJ woman ZWJ girl is one cluster of eight code units.
        const string family = "\U0001F468‍\U0001F469‍\U0001F467!";
        Strings.NextCharLength(family, 0).ShouldBe(8);
        Strings.PrevCharLength(family, 8).ShouldBe(8);

        var iterator = new GraphemeIterator("a\U0001F4DAb");
        var lengths = new List<int>();
        while (!iterator.Eol())
        {
            lengths.Add(iterator.NextGraphemeLength());
        }
        lengths.ShouldBe([1, 2, 1]);
    }

    [Fact]
    public void Code_Point_Iteration_Combines_Surrogate_Pairs()
    {
        const string text = "a\U0001F4DA";
        var iterator = new CodePointIterator(text);
        iterator.NextCodePoint().ShouldBe('a');
        iterator.NextCodePoint().ShouldBe(0x1F4DA);
        iterator.Eol().ShouldBeTrue();
        iterator.PrevCodePoint().ShouldBe(0x1F4DA);
        iterator.Offset.ShouldBe(1);

        // A lone surrogate is a code point of its own.
        Strings.GetNextCodePoint("\uD83Dx", 2, 0).ShouldBe(0xD83D);
        Strings.GetPrevCodePoint("x\uDCDA", 2).ShouldBe(0xDCDA);
    }

    [Fact]
    public void GetCharContainingOffset_Widens_To_The_Cluster()
    {
        const string text = "a\U0001F4DAb";
        Strings.GetCharContainingOffset(text, 0).ShouldBe((0, 1));
        Strings.GetCharContainingOffset(text, 1).ShouldBe((1, 3));
        Strings.GetCharContainingOffset(text, 2).ShouldBe((1, 3));
        Strings.GetCharContainingOffset(text, 3).ShouldBe((3, 4));
    }

    [Fact]
    public void GetLeftDeleteOffset_Deletes_Emoji_Components()
    {
        Strings.GetLeftDeleteOffset(0, "abc").ShouldBe(0);
        Strings.GetLeftDeleteOffset(2, "abc").ShouldBe(1);

        // thumbs up + skin tone modifier goes at once
        Strings.GetLeftDeleteOffset(5, "a\U0001F44D\U0001F3FD").ShouldBe(1);

        // man ZWJ woman: the woman and the ZWJ go, the man stays
        Strings.GetLeftDeleteOffset(5, "\U0001F468‍\U0001F469").ShouldBe(2);

        // a combining mark is one code point, not the whole cluster
        Strings.GetLeftDeleteOffset(2, "x́").ShouldBe(1);

        // a surrogate pair that is not an emoji is one code point
        Strings.GetLeftDeleteOffset(3, "a\U00010300").ShouldBe(1);
    }
}
