// Ported from vscode (src/vs/editor/test/common/modes/supports/characterPair.test.ts, commit b57d5c9e), MIT license.
// assert.deepStrictEqual on StandardAutoClosingPairConditional compares open, close and the token types it may close
// in here (AssertPairs).

using Shouldly;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Tokens;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class CharacterPairSupportTests
{
    private static readonly StandardTokenType[] s_tokenTypes =
        [StandardTokenType.Other, StandardTokenType.Comment, StandardTokenType.String, StandardTokenType.RegEx];

    private static void AssertPairs(IReadOnlyList<StandardAutoClosingPairConditional> actual, params StandardAutoClosingPairConditional[] expected)
    {
        actual.Count.ShouldBe(expected.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            actual[i].Open.ShouldBe(expected[i].Open);
            actual[i].Close.ShouldBe(expected[i].Close);
            actual[i].BeforeText.ShouldBe(expected[i].BeforeText);
            foreach (var tokenType in s_tokenTypes)
            {
                actual[i].IsOK(tokenType).ShouldBe(expected[i].IsOK(tokenType));
            }
        }
    }

    private static void AssertSurroundingPairs(IReadOnlyList<IAutoClosingPair> actual, params (string Open, string Close)[] expected)
    {
        actual.Select(p => (p.Open, p.Close)).ShouldBe(expected);
    }

    [Fact]
    public void Only_AutoClosingPairs()
    {
        var characaterPairSupport = new CharacterPairSupport(new LanguageConfiguration { AutoClosingPairs = [new AutoClosingPairConditional("a", "b")] });
        AssertPairs(characaterPairSupport.GetAutoClosingPairs(), new StandardAutoClosingPairConditional(new AutoClosingPairConditional("a", "b")));
        AssertSurroundingPairs(characaterPairSupport.GetSurroundingPairs(), ("a", "b"));
        characaterPairSupport.GetSurroundingPairs()[0].ShouldBeOfType<StandardAutoClosingPairConditional>();
    }

    [Fact]
    public void Only_Empty_AutoClosingPairs()
    {
        var characaterPairSupport = new CharacterPairSupport(new LanguageConfiguration { AutoClosingPairs = [] });
        characaterPairSupport.GetAutoClosingPairs().ShouldBeEmpty();
        characaterPairSupport.GetSurroundingPairs().ShouldBeEmpty();
    }

    [Fact]
    public void Only_Brackets()
    {
        var characaterPairSupport = new CharacterPairSupport(new LanguageConfiguration { Brackets = [("a", "b")] });
        AssertPairs(characaterPairSupport.GetAutoClosingPairs(), new StandardAutoClosingPairConditional(new AutoClosingPairConditional("a", "b")));
        AssertSurroundingPairs(characaterPairSupport.GetSurroundingPairs(), ("a", "b"));
        characaterPairSupport.GetSurroundingPairs()[0].ShouldBeOfType<StandardAutoClosingPairConditional>();
    }

    [Fact]
    public void Only_Empty_Brackets()
    {
        var characaterPairSupport = new CharacterPairSupport(new LanguageConfiguration { Brackets = [] });
        characaterPairSupport.GetAutoClosingPairs().ShouldBeEmpty();
        characaterPairSupport.GetSurroundingPairs().ShouldBeEmpty();
    }

    [Fact]
    public void Only_SurroundingPairs()
    {
        var characaterPairSupport = new CharacterPairSupport(new LanguageConfiguration { SurroundingPairs = [new AutoClosingPair("a", "b")] });
        characaterPairSupport.GetAutoClosingPairs().ShouldBeEmpty();
        AssertSurroundingPairs(characaterPairSupport.GetSurroundingPairs(), ("a", "b"));
    }

    [Fact]
    public void Only_Empty_SurroundingPairs()
    {
        var characaterPairSupport = new CharacterPairSupport(new LanguageConfiguration { SurroundingPairs = [] });
        characaterPairSupport.GetAutoClosingPairs().ShouldBeEmpty();
        characaterPairSupport.GetSurroundingPairs().ShouldBeEmpty();
    }

    [Fact]
    public void Brackets_Is_Ignored_When_Having_AutoClosingPairs()
    {
        var characaterPairSupport = new CharacterPairSupport(new LanguageConfiguration { AutoClosingPairs = [], Brackets = [("a", "b")] });
        characaterPairSupport.GetAutoClosingPairs().ShouldBeEmpty();
        characaterPairSupport.GetSurroundingPairs().ShouldBeEmpty();
    }

    private static bool TestShouldAutoClose(CharacterPairSupport characterPairSupport, TokenText[] line, int column)
    {
        var autoClosingPair = characterPairSupport.GetAutoClosingPairs()[0];
        return autoClosingPair.ShouldAutoClose(ModesTestUtils.CreateFakeScopedLineTokens(line), column);
    }

    [Fact]
    public void ShouldAutoClosePair_In_Empty_Line()
    {
        var sup = new CharacterPairSupport(new LanguageConfiguration { AutoClosingPairs = [new AutoClosingPairConditional("{", "}") { NotIn = ["string", "comment"] }] });
        TokenText[] tokenText = [];
        TestShouldAutoClose(sup, tokenText, 1).ShouldBeTrue();
    }

    [Fact]
    public void ShouldAutoClosePair_In_Not_Interesting_Line_1()
    {
        var sup = new CharacterPairSupport(new LanguageConfiguration { AutoClosingPairs = [new AutoClosingPairConditional("{", "}") { NotIn = ["string", "comment"] }] });
        TokenText[] tokenText =
        [
            new("do", StandardTokenType.Other),
        ];
        TestShouldAutoClose(sup, tokenText, 3).ShouldBeTrue();
    }

    [Fact]
    public void ShouldAutoClosePair_In_Not_Interesting_Line_2()
    {
        var sup = new CharacterPairSupport(new LanguageConfiguration { AutoClosingPairs = [new AutoClosingPairConditional("{", "}")] });
        TokenText[] tokenText =
        [
            new("do", StandardTokenType.String),
        ];
        TestShouldAutoClose(sup, tokenText, 3).ShouldBeTrue();
    }

    [Fact]
    public void ShouldAutoClosePair_In_Interesting_Line_1()
    {
        var sup = new CharacterPairSupport(new LanguageConfiguration { AutoClosingPairs = [new AutoClosingPairConditional("{", "}") { NotIn = ["string", "comment"] }] });
        TokenText[] tokenText =
        [
            new("\"a\"", StandardTokenType.String),
        ];
        TestShouldAutoClose(sup, tokenText, 1).ShouldBeFalse();
        TestShouldAutoClose(sup, tokenText, 2).ShouldBeFalse();
        TestShouldAutoClose(sup, tokenText, 3).ShouldBeFalse();
        TestShouldAutoClose(sup, tokenText, 4).ShouldBeFalse();
    }

    [Fact]
    public void ShouldAutoClosePair_In_Interesting_Line_2()
    {
        var sup = new CharacterPairSupport(new LanguageConfiguration { AutoClosingPairs = [new AutoClosingPairConditional("{", "}") { NotIn = ["string", "comment"] }] });
        TokenText[] tokenText =
        [
            new("x=", StandardTokenType.Other),
            new("\"a\"", StandardTokenType.String),
            new(";", StandardTokenType.Other),
        ];
        TestShouldAutoClose(sup, tokenText, 1).ShouldBeTrue();
        TestShouldAutoClose(sup, tokenText, 2).ShouldBeTrue();
        TestShouldAutoClose(sup, tokenText, 3).ShouldBeTrue();
        TestShouldAutoClose(sup, tokenText, 4).ShouldBeFalse();
        TestShouldAutoClose(sup, tokenText, 5).ShouldBeFalse();
        TestShouldAutoClose(sup, tokenText, 6).ShouldBeFalse();
        TestShouldAutoClose(sup, tokenText, 7).ShouldBeTrue();
    }

    [Fact]
    public void ShouldAutoClosePair_In_Interesting_Line_3()
    {
        var sup = new CharacterPairSupport(new LanguageConfiguration { AutoClosingPairs = [new AutoClosingPairConditional("{", "}") { NotIn = ["string", "comment"] }] });
        TokenText[] tokenText =
        [
            new(" ", StandardTokenType.Other),
            new("//a", StandardTokenType.Comment),
        ];
        TestShouldAutoClose(sup, tokenText, 1).ShouldBeTrue();
        TestShouldAutoClose(sup, tokenText, 2).ShouldBeTrue();
        TestShouldAutoClose(sup, tokenText, 3).ShouldBeFalse();
        TestShouldAutoClose(sup, tokenText, 4).ShouldBeFalse();
        TestShouldAutoClose(sup, tokenText, 5).ShouldBeFalse();
    }
}
