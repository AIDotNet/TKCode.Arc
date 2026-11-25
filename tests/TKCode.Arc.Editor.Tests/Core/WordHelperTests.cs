// Ported from vscode (src/vs/editor/test/common/model/model.test.ts, commit b57d5c9e), MIT license.
// Upstream has no wordHelper.test.ts; its word tests go through TextModel.getWordAtPosition, which for a line with
// one token calls getWordAtText(column, wordDefinition, lineContent, 0). The first two tests are those, run on the
// line directly (the embedded-language test needs tokenization and comes with it); issue #61296's CSS word pattern is
// written in .NET syntax (`[\w\-?]`, as .NET rejects `[\w-?]`). The tests after them are not upstream's.

using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class WordHelperTests
{
    private static WordAtPosition? GetWordAtPosition(string line, int column, Regex? wordDefinition = null) =>
        WordHelper.GetWordAtText(column, WordHelper.EnsureValidWordDefinition(wordDefinition), line, 0);

    [Fact]
    public void Get_Word_At_Position()
    {
        const string text = "This text has some  words. ";

        GetWordAtPosition(text, 1).ShouldBe(new WordAtPosition("This", 1, 5));
        GetWordAtPosition(text, 2).ShouldBe(new WordAtPosition("This", 1, 5));
        GetWordAtPosition(text, 4).ShouldBe(new WordAtPosition("This", 1, 5));
        GetWordAtPosition(text, 5).ShouldBe(new WordAtPosition("This", 1, 5));
        GetWordAtPosition(text, 6).ShouldBe(new WordAtPosition("text", 6, 10));
        GetWordAtPosition(text, 19).ShouldBe(new WordAtPosition("some", 15, 19));
        GetWordAtPosition(text, 20).ShouldBeNull();
        GetWordAtPosition(text, 21).ShouldBe(new WordAtPosition("words", 21, 26));
        GetWordAtPosition(text, 26).ShouldBe(new WordAtPosition("words", 21, 26));
        GetWordAtPosition(text, 27).ShouldBeNull();
        GetWordAtPosition(text, 28).ShouldBeNull();
    }

    [Fact]
    public void Issue_61296_VS_Code_Freezes_When_Editing_CSS_File_With_Emoji()
    {
        var wordPattern = new Regex(@"(#?-?\d*\.\d\w*%?)|(::?[\w-]*(?=[^,{;]*[,{]))|(([@#.!])?[\w\-?]+%?|[@#!.])", RegexOptions.ECMAScript);
        const string text = ".🐷-a-b";

        GetWordAtPosition(text, 1, wordPattern).ShouldBe(new WordAtPosition(".", 1, 2));
        GetWordAtPosition(text, 2, wordPattern).ShouldBe(new WordAtPosition(".", 1, 2));
        GetWordAtPosition(text, 3, wordPattern).ShouldBeNull();
        GetWordAtPosition(text, 4, wordPattern).ShouldBe(new WordAtPosition("-a-b", 4, 8));
        GetWordAtPosition(text, 5, wordPattern).ShouldBe(new WordAtPosition("-a-b", 4, 8));
        GetWordAtPosition(text, 6, wordPattern).ShouldBe(new WordAtPosition("-a-b", 4, 8));
        GetWordAtPosition(text, 7, wordPattern).ShouldBe(new WordAtPosition("-a-b", 4, 8));
        GetWordAtPosition(text, 8, wordPattern).ShouldBe(new WordAtPosition("-a-b", 4, 8));
    }

    // Not upstream's from here on.

    [Fact]
    public void Default_Word_Definition_Reads_Numbers_And_Stops_At_Separators()
    {
        const string text = "x = -12.5e3 + foo_bar.baz";

        GetWordAtPosition(text, 1).ShouldBe(new WordAtPosition("x", 1, 2));
        GetWordAtPosition(text, 6).ShouldBe(new WordAtPosition("-12.5e3", 5, 12));
        GetWordAtPosition(text, 16).ShouldBe(new WordAtPosition("foo_bar", 15, 22));
        GetWordAtPosition(text, 23).ShouldBe(new WordAtPosition("baz", 23, 26));
    }

    [Fact]
    public void Default_Word_Definition_Uses_JavaScript_Whitespace()
    {
        // U+00A0 and U+3000 separate words as \s does in JavaScript; U+0085 does not (it is not JavaScript whitespace).
        GetWordAtPosition("ab cd", 2).ShouldBe(new WordAtPosition("ab", 1, 3));
        GetWordAtPosition("ab　cd", 5).ShouldBe(new WordAtPosition("cd", 4, 6));
        GetWordAtPosition("ab\u0085cd", 2).ShouldBe(new WordAtPosition("ab\u0085cd", 1, 6));

        // Non-ASCII letters and astral characters are word characters.
        GetWordAtPosition("é漢\U0001F4DA x", 1).ShouldBe(new WordAtPosition("é漢\U0001F4DA", 1, 5));
    }

    [Fact]
    public void CreateWordRegExp_Keeps_Allowed_Separators_In_Words()
    {
        var withHyphen = WordHelper.CreateWordRegExp("-");
        GetWordAtPosition("a foo-bar b", 4, withHyphen).ShouldBe(new WordAtPosition("foo-bar", 3, 10));
        GetWordAtPosition("a foo-bar b", 4).ShouldBe(new WordAtPosition("foo", 3, 6));
    }

    [Fact]
    public void EnsureValidWordDefinition_Falls_Back_To_The_Default()
    {
        WordHelper.EnsureValidWordDefinition(null).ShouldBeSameAs(WordHelper.DefaultWordRegExp);
        var custom = new Regex("[a-z]+");
        WordHelper.EnsureValidWordDefinition(custom).ShouldBeSameAs(custom);
    }

    [Fact]
    public void Long_Lines_Search_A_Window_Around_The_Column()
    {
        var text = new string('.', 3000) + "needle" + new string('.', 3000);

        GetWordAtPosition(text, 3001).ShouldBe(new WordAtPosition("needle", 3001, 3007));
        GetWordAtPosition(text, 3007).ShouldBe(new WordAtPosition("needle", 3001, 3007));
        GetWordAtPosition(text, 100).ShouldBeNull();

        var config = new GetWordAtTextConfig(MaxLen: 20, WindowSize: 3, TimeBudget: 150);
        WordHelper.GetWordAtText(3004, WordHelper.DefaultWordRegExp, text, 0, config).ShouldBe(new WordAtPosition("needle", 3001, 3007));
    }

    [Fact]
    public void Text_Offset_Shifts_The_Columns()
    {
        // The text is the part of a line that starts 10 code units in.
        WordHelper.GetWordAtText(13, WordHelper.DefaultWordRegExp, "ab cd", 10).ShouldBe(new WordAtPosition("ab", 11, 13));
        WordHelper.GetWordAtText(15, WordHelper.DefaultWordRegExp, "ab cd", 10).ShouldBe(new WordAtPosition("cd", 14, 16));
    }

    [Fact]
    public void An_Empty_Match_Does_Not_Hang()
    {
        var emptyOrWord = new Regex("[a-z]*");
        GetWordAtPosition("  ab", 4, emptyOrWord).ShouldBe(new WordAtPosition("ab", 3, 5));
    }
}
