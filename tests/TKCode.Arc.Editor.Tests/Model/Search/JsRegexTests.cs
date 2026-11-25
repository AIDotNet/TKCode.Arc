// Tests for the JavaScript-to-.NET mapping in JsRegex, which adapts vscode's
// src/vs/base/common/strings.ts (createRegExp) and src/vs/editor/common/model/textModelSearch.ts,
// commit b57d5c9e, MIT licensed.

using Shouldly;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

/// <summary>
/// Direct tests for the JavaScript-to-.NET regular expression mapping. Each test names one difference the mapping
/// handles; the file header of <see cref="JsRegex"/> lists the ones it knowingly does not.
/// </summary>
public sealed class JsRegexTests
{
    private static readonly string Emoji = char.ConvertFromUtf32(0x1F600);
    private static readonly string ArabicDigit = ((char)0x0663).ToString();
    private static readonly string Nbsp = ((char)0xA0).ToString();
    private static readonly string OghamSpace = ((char)0x1680).ToString();
    private static readonly string LineSeparator = ((char)0x2028).ToString();
    private static readonly string ParagraphSeparator = ((char)0x2029).ToString();
    private static readonly string Bom = ((char)0xFEFF).ToString();
    private static readonly string Nel = ((char)0x85).ToString();

    private static bool IsMatch(string pattern, string input, bool matchCase = true, bool multiline = false) =>
        JsRegex.Create(pattern, isRegex: true, matchCase, multiline).Regex.IsMatch(input);

    [Fact]
    public void Simple_Search_Is_Escaped()
    {
        var regex = JsRegex.Create("a.b", isRegex: false, matchCase: true, multiline: false);

        regex.Source.ShouldBe("a\\.b");
        regex.Regex.IsMatch("a.b").ShouldBeTrue();
        regex.Regex.IsMatch("axb").ShouldBeFalse();
    }

    [Fact]
    public void MatchCase_Controls_IgnoreCase()
    {
        JsRegex.Create("foo", isRegex: false, matchCase: false, multiline: false).Regex.IsMatch("FOO").ShouldBeTrue();
        JsRegex.Create("foo", isRegex: false, matchCase: true, multiline: false).Regex.IsMatch("FOO").ShouldBeFalse();
    }

    [Fact]
    public void Digit_And_Word_Classes_Are_Ascii()
    {
        IsMatch("\\d", "5").ShouldBeTrue();
        IsMatch("\\d", ArabicDigit).ShouldBeFalse();
        IsMatch("[\\d]", ArabicDigit).ShouldBeFalse();

        IsMatch("\\w", "a").ShouldBeTrue();
        IsMatch("\\w", "é").ShouldBeFalse();
        IsMatch("[\\w]", "é").ShouldBeFalse();

        // \b uses the ASCII word definition too, so é does not make a boundary vanish.
        IsMatch("\\bfoo", "éfoo").ShouldBeTrue();
        IsMatch("\\bfoo", "afoo").ShouldBeFalse();
    }

    [Fact]
    public void Whitespace_Uses_The_JavaScript_Set()
    {
        foreach (var whitespace in new[] { " ", "\t", "\n", "\r", Nbsp, OghamSpace, LineSeparator, ParagraphSeparator, Bom })
        {
            IsMatch("\\s", whitespace).ShouldBeTrue($"\\s should match U+{(int)whitespace[0]:X4}");
            IsMatch("[\\s]", whitespace).ShouldBeTrue($"[\\s] should match U+{(int)whitespace[0]:X4}");
        }

        // U+0085 is whitespace in .NET but not in JavaScript.
        IsMatch("\\s", Nel).ShouldBeFalse();
    }

    [Fact]
    public void NonWhitespace_Is_The_JavaScript_Complement()
    {
        foreach (var whitespace in new[] { " ", Nbsp, LineSeparator, Bom })
        {
            IsMatch("\\S", whitespace).ShouldBeFalse();
            IsMatch("[\\S]", whitespace).ShouldBeFalse();
        }

        IsMatch("\\S", "a").ShouldBeTrue();
        IsMatch("[\\S]", "a").ShouldBeTrue();
        IsMatch("\\S", Nel).ShouldBeTrue();
        IsMatch("[\\S]", Nel).ShouldBeTrue();
    }

    [Fact]
    public void Dot_Excludes_Every_Line_Terminator()
    {
        IsMatch(".", "\n").ShouldBeFalse();
        IsMatch(".", "\r").ShouldBeFalse();
        IsMatch(".", LineSeparator).ShouldBeFalse();
        IsMatch(".", ParagraphSeparator).ShouldBeFalse();

        // U+0085 is not a line terminator for JavaScript's dot.
        IsMatch(".", Nel).ShouldBeTrue();
        IsMatch(".", "a").ShouldBeTrue();
    }

    [Fact]
    public void Dot_Matches_One_Code_Point()
    {
        var match = JsRegex.Create(".", isRegex: true, matchCase: true, multiline: false).Regex.Match(Emoji);
        match.Success.ShouldBeTrue();
        match.Length.ShouldBe(2);

        // A surrogate pair is indivisible, so two dots cannot split one emoji.
        IsMatch("..", Emoji).ShouldBeFalse();
    }

    [Fact]
    public void Dollar_Without_Multiline_Only_Matches_The_End()
    {
        IsMatch("foo$", "foo").ShouldBeTrue();
        IsMatch("foo$", "foo\n").ShouldBeFalse();

        IsMatch("^foo$", "a\nfoo\nb", multiline: true).ShouldBeTrue();
        IsMatch("^foo$", "a\nfoo\nb", multiline: false).ShouldBeFalse();
    }

    [Fact]
    public void Code_Point_Escapes_Work()
    {
        IsMatch("\\u{0066}", "f").ShouldBeTrue();

        var match = JsRegex.Create("\\u{1F600}", isRegex: true, matchCase: true, multiline: false).Regex.Match(Emoji);
        match.Success.ShouldBeTrue();
        match.Length.ShouldBe(2);

        // The quantifier applies to the whole code point, not to the low surrogate.
        var twice = JsRegex.Create("\\u{1F600}{2}", isRegex: true, matchCase: true, multiline: false)
            .Regex.Match(Emoji + Emoji);
        twice.Success.ShouldBeTrue();
        twice.Length.ShouldBe(4);
    }

    [Fact]
    public void Named_Groups_And_Backreferences_Work()
    {
        IsMatch("(?<word>foo)-\\k<word>", "foo-foo").ShouldBeTrue();
    }

    [Fact]
    public void Empty_Character_Classes_Are_Rewritten()
    {
        var never = JsRegex.Create("[]", isRegex: true, matchCase: true, multiline: false).Regex;
        never.Match("a").Success.ShouldBeFalse();
        never.Match(string.Empty).Success.ShouldBeFalse();

        var any = JsRegex.Create("[^]", isRegex: true, matchCase: true, multiline: false).Regex;
        any.Match("\n").Success.ShouldBeTrue();
        any.Match(Emoji).Length.ShouldBe(2);
    }

    [Fact]
    public void Invalid_Pattern_Throws()
    {
        Should.Throw<ArgumentException>(() => JsRegex.Create("(", isRegex: true, matchCase: false, multiline: false));
        Should.Throw<ArgumentException>(() => JsRegex.Create(string.Empty, isRegex: true, matchCase: false, multiline: false));
        Should.Throw<ArgumentException>(() => JsRegex.Create("\\u{110000}", isRegex: true, matchCase: false, multiline: false));
    }

    [Fact]
    public void Every_Regex_Has_A_Finite_Match_Timeout()
    {
        JsRegex.Create("a", isRegex: true, matchCase: false, multiline: false)
            .Regex.MatchTimeout.ShouldBe(JsRegex.MatchTimeout);
    }
}
