// Ported from vscode (src/vs/editor/test/common/modes/supports/electricCharacter.test.ts, commit b57d5c9e), MIT license.

using Shouldly;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Tokens;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class ElectricCharacterTests
{
    private const string FakeLanguageId = "test";

    private static ElectricAction? TestOnElectricCharacter(BracketElectricCharacterSupport electricCharacterSupport, TokenText[] line, string character, int offset)
    {
        return electricCharacterSupport.OnElectricCharacter(character, ModesTestUtils.CreateFakeScopedLineTokens(line), offset);
    }

    private static void TestDoesNothing(BracketElectricCharacterSupport electricCharacterSupport, TokenText[] line, string character, int offset)
    {
        var actual = TestOnElectricCharacter(electricCharacterSupport, line, character, offset);
        actual.ShouldBeNull();
    }

    private static void TestMatchBracket(BracketElectricCharacterSupport electricCharacterSupport, TokenText[] line, string character, int offset, string matchOpenBracket)
    {
        var actual = TestOnElectricCharacter(electricCharacterSupport, line, character, offset);
        actual.ShouldBe(new ElectricAction(matchOpenBracket));
    }

    [Fact]
    public void GetElectricCharacters_Uses_All_Sources_And_Dedups()
    {
        var sup = new BracketElectricCharacterSupport(
            new RichEditBrackets(FakeLanguageId, [
                ("{", "}"),
                ("(", ")"),
            ]));

        sup.GetElectricCharacters().ShouldBe(["}", ")"]);
    }

    [Fact]
    public void MatchOpenBracket()
    {
        var sup = new BracketElectricCharacterSupport(
            new RichEditBrackets(FakeLanguageId, [
                ("{", "}"),
                ("(", ")"),
            ]));

        TestDoesNothing(sup, [new("\t{", StandardTokenType.Other)], "\t", 1);
        TestDoesNothing(sup, [new("\t{", StandardTokenType.Other)], "\t", 2);
        TestDoesNothing(sup, [new("\t\t", StandardTokenType.Other)], "{", 3);

        TestDoesNothing(sup, [new("\t}", StandardTokenType.Other)], "\t", 1);
        TestDoesNothing(sup, [new("\t}", StandardTokenType.Other)], "\t", 2);
        TestMatchBracket(sup, [new("\t\t", StandardTokenType.Other)], "}", 3, "}");
    }
}
