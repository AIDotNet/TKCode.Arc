// Ported from vscode (src/vs/editor/test/common/services/unicodeTextModelHighlighter.test.ts, commit b57d5c9e), MIT
// license. The one upstream test; plus own tests of the options, the reasons and the character tables.

using Shouldly;
using TKCode.Arc.Editor.Services;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class UnicodeTextModelHighlighterTests
{
    private static readonly UnicodeHighlighterOptions s_options = new()
    {
        AllowedCodePoints = [],
        AllowedLocales = [],
        AmbiguousCharacters = true,
        InvisibleCharacters = true,
        IncludeComments = false,
        IncludeStrings = false,
        NonBasicAscii = false,
    };

    private static (UnicodeHighlightsResult Result, string[] Ranges) T(string text, UnicodeHighlighterOptions options)
    {
        using var m = TestTextModel.Create(text);
        var r = UnicodeTextModelHighlighter.ComputeUnicodeHighlights(new TextModelUnicodeTarget(m), options);
        return (r, r.Ranges.Select(static range => range.ToString()).ToArray());
    }

    [Fact]
    public void ComputeUnicodeHighlights_168068()
    {
        var (r, ranges) = T("\n\tFor å gi et eksempel\n", s_options);

        r.AmbiguousCharacterCount.ShouldBe(0);
        r.HasMore.ShouldBeFalse();
        r.InvisibleCharacterCount.ShouldBe(4);
        r.NonBasicAsciiCharacterCount.ShouldBe(0);
        ranges.ShouldBe(["[2,5 -> 2,6]", "[2,7 -> 2,8]", "[2,10 -> 2,11]", "[2,13 -> 2,14]"]);
    }

    // ---- own tests

    [Fact]
    public void Ambiguous_Characters_In_Ascii_Words_Are_Highlighted()
    {
        // Cyrillic 'а' (U+0430) in an otherwise Latin word, and a Greek question mark (U+037E) after it
        var (r, ranges) = T("var pаssword = 1;", s_options);

        r.AmbiguousCharacterCount.ShouldBe(2);
        ranges.ShouldBe(["[1,6 -> 1,7]", "[1,17 -> 1,18]"]);
    }

    [Fact]
    public void Words_Of_Non_Ascii_Letters_Are_Left_Alone()
    {
        // a Russian word: its 'а' and 'о' are confusable, but the word has non-confusable letters and no ASCII
        var (r, _) = T("привет мир", s_options);

        r.Ranges.ShouldBeEmpty();
    }

    [Fact]
    public void Zero_Width_And_Other_Invisible_Characters_Are_Found()
    {
        var (r, ranges) = T("a​b c d\te f", s_options);

        r.InvisibleCharacterCount.ShouldBe(2);
        ranges.ShouldBe(["[1,2 -> 1,3]", "[1,6 -> 1,7]"]);
    }

    [Fact]
    public void Allowed_Code_Points_And_Disabled_Kinds_Are_Not_Highlighted()
    {
        T("a​b", s_options with { AllowedCodePoints = [0x200b] }).Result.Ranges.ShouldBeEmpty();
        T("a​b", s_options with { InvisibleCharacters = false }).Result.Ranges.ShouldBeEmpty();
        T("pаss", s_options with { AmbiguousCharacters = false }).Result.Ranges.ShouldBeEmpty();
    }

    [Fact]
    public void Non_Basic_Ascii_Highlights_Every_Other_Character_Once_Per_Code_Point()
    {
        var (r, ranges) = T("é😀x", s_options with { NonBasicAscii = true });

        r.NonBasicAsciiCharacterCount.ShouldBe(2);
        ranges.ShouldBe(["[1,1 -> 1,2]", "[1,2 -> 1,4]"]);
    }

    [Fact]
    public void A_Range_Limits_The_Lines()
    {
        var (r, _) = T("​\n​\n​", s_options);
        r.Ranges.Count.ShouldBe(3);

        using var m = TestTextModel.Create("​\n​\n​");
        UnicodeTextModelHighlighter.ComputeUnicodeHighlights(new TextModelUnicodeTarget(m), s_options, new TextRange(2, 1, 3, 1))
            .Ranges.Select(static x => x.StartLineNumber).ShouldBe([2, 3]);
    }

    [Fact]
    public void Stops_After_A_Thousand_Ranges()
    {
        var (r, _) = T(new string('​', 1005), s_options);

        r.Ranges.Count.ShouldBe(1000);
        r.HasMore.ShouldBeTrue();
    }

    [Fact]
    public void Reasons_Say_What_A_Character_Looks_Like()
    {
        var ambiguous = UnicodeTextModelHighlighter.ComputeUnicodeHighlightReason("а", s_options).ShouldNotBeNull();
        ambiguous.Kind.ShouldBe(UnicodeHighlighterReasonKind.Ambiguous);
        ambiguous.ConfusableWith.ShouldBe("a");
        ambiguous.NotAmbiguousInLocales.ShouldContain("ru");

        UnicodeTextModelHighlighter.ComputeUnicodeHighlightReason("​", s_options).ShouldNotBeNull().Kind.ShouldBe(UnicodeHighlighterReasonKind.Invisible);
        UnicodeTextModelHighlighter.ComputeUnicodeHighlightReason("x", s_options).ShouldBeNull();
    }

    [Fact]
    public void Locales_Make_Their_Own_Letters_Unambiguous()
    {
        AmbiguousCharacters.GetInstance([]).IsAmbiguous(0x0430).ShouldBeTrue();
        AmbiguousCharacters.GetInstance(["ru"]).IsAmbiguous(0x0430).ShouldBeFalse();
        AmbiguousCharacters.GetLocales().ShouldContain("zh-hans");
        InvisibleCharacters.IsInvisibleCharacter(0x00a0).ShouldBeTrue();
        InvisibleCharacters.IsInvisibleCharacter('a').ShouldBeFalse();
    }
}
