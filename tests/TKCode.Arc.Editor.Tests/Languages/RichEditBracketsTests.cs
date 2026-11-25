// Ported from vscode (src/vs/editor/test/common/modes/supports/richEditBrackets.test.ts, commit b57d5c9e), MIT license.

using System.Text.RegularExpressions;
using Shouldly;
using TKCode.Arc.Editor.Languages;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class RichEditBracketsTests
{
    private static TextRange? FindPrevBracketInRange(Regex reversedBracketRegex, string lineText, int currentTokenStart, int currentTokenEnd)
    {
        return BracketsUtils.FindPrevBracketInRange(reversedBracketRegex, 1, lineText, currentTokenStart, currentTokenEnd);
    }

    private static TextRange? FindNextBracketInRange(Regex forwardBracketRegex, string lineText, int currentTokenStart, int currentTokenEnd)
    {
        return BracketsUtils.FindNextBracketInRange(forwardBracketRegex, 1, lineText, currentTokenStart, currentTokenEnd);
    }

    [Fact]
    public void FindPrevBracketInToken_One_Char_1()
    {
        var result = FindPrevBracketInRange(JsRegExp.Of(@"(\{)|(\})", "i"), "{", 0, 1);
        result!.Value.StartColumn.ShouldBe(1);
        result!.Value.EndColumn.ShouldBe(2);
    }

    [Fact]
    public void FindPrevBracketInToken_One_Char_2()
    {
        var result = FindPrevBracketInRange(JsRegExp.Of(@"(\{)|(\})", "i"), "{{", 0, 1);
        result!.Value.StartColumn.ShouldBe(1);
        result!.Value.EndColumn.ShouldBe(2);
    }

    [Fact]
    public void FindPrevBracketInToken_One_Char_3()
    {
        var result = FindPrevBracketInRange(JsRegExp.Of(@"(\{)|(\})", "i"), "{hello world!", 0, 13);
        result!.Value.StartColumn.ShouldBe(1);
        result!.Value.EndColumn.ShouldBe(2);
    }

    [Fact]
    public void FindPrevBracketInToken_More_Chars_1()
    {
        var result = FindPrevBracketInRange(JsRegExp.Of("(olleh)", "i"), "hello world!", 0, 12);
        result!.Value.StartColumn.ShouldBe(1);
        result!.Value.EndColumn.ShouldBe(6);
    }

    [Fact]
    public void FindPrevBracketInToken_More_Chars_2()
    {
        var result = FindPrevBracketInRange(JsRegExp.Of("(olleh)", "i"), "hello world!", 0, 5);
        result!.Value.StartColumn.ShouldBe(1);
        result!.Value.EndColumn.ShouldBe(6);
    }

    [Fact]
    public void FindPrevBracketInToken_More_Chars_3()
    {
        var result = FindPrevBracketInRange(JsRegExp.Of("(olleh)", "i"), " hello world!", 0, 6);
        result!.Value.StartColumn.ShouldBe(2);
        result!.Value.EndColumn.ShouldBe(7);
    }

    [Fact]
    public void FindNextBracketInToken_One_Char()
    {
        var result = FindNextBracketInRange(JsRegExp.Of(@"(\{)|(\})", "i"), "{", 0, 1);
        result!.Value.StartColumn.ShouldBe(1);
        result!.Value.EndColumn.ShouldBe(2);
    }

    [Fact]
    public void FindNextBracketInToken_More_Chars()
    {
        var result = FindNextBracketInRange(JsRegExp.Of("(world)", "i"), "hello world!", 0, 12);
        result!.Value.StartColumn.ShouldBe(7);
        result!.Value.EndColumn.ShouldBe(12);
    }

    [Fact]
    public void FindNextBracketInToken_With_Emoty_Result()
    {
        var result = FindNextBracketInRange(JsRegExp.Of(@"(\{)|(\})", "i"), "", 0, 0);
        result.ShouldBeNull();
    }

    [Fact]
    public void Issue_3894_Handlebars_Curly_Braces_Edit_Issues()
    {
        var result = FindPrevBracketInRange(JsRegExp.Of(@"(\-\-!<)|(>\-\-)|(\{\{)|(\}\})", "i"), "{{asd}}", 0, 2);
        result!.Value.StartColumn.ShouldBe(1);
        result!.Value.EndColumn.ShouldBe(3);
    }
}
