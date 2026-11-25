// Ported from vscode's src/vs/editor/test/common/model/textModelSearch.test.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
//
// createTextModel is PieceTreeTextBuffer.Create here; each case still runs once for LF and once for CRLF content.
// A few tests build their expected regex flags through JsRegex.Source / Multiline instead of a RegExp literal.

using Shouldly;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class TextModelSearchTests
{
    private const string UsualWordSeparators = @"`~!@#$%^&*()-=+[{]}\|;:'"",.<>/?";
    private static readonly string EmojiLaptop = char.ConvertFromUtf32(0x1F4BB);

    private static TextRange FullRange(PieceTreeTextBuffer model) =>
        new(1, 1, model.GetLineCount(), model.GetLineMaxColumn(model.GetLineCount()));

    private static void AssertModelFindMatches(
        PieceTreeTextBuffer model,
        SearchParams searchParams,
        IReadOnlyList<FindMatch> expectedMatches,
        bool captureMatches = false)
    {
        var actual = TextModelSearch.FindMatches(model, searchParams, FullRange(model), captureMatches, 1000);
        actual.ShouldBe(expectedMatches);

        // test findNextMatch
        var startPos = new TextPosition(1, 1);
        var match = TextModelSearch.FindNextMatch(model, searchParams, startPos, captureMatches);
        match.ShouldBe(expectedMatches[0], $"findNextMatch {startPos}");
        foreach (var expectedMatch in expectedMatches)
        {
            startPos = expectedMatch.Range.Start;
            match = TextModelSearch.FindNextMatch(model, searchParams, startPos, captureMatches);
            match.ShouldBe(expectedMatch, $"findNextMatch {startPos}");
        }

        // test findPreviousMatch
        startPos = new TextPosition(model.GetLineCount(), model.GetLineMaxColumn(model.GetLineCount()));
        match = TextModelSearch.FindPreviousMatch(model, searchParams, startPos, captureMatches);
        match.ShouldBe(expectedMatches[^1], $"findPrevMatch {startPos}");
        foreach (var expectedMatch in expectedMatches)
        {
            startPos = expectedMatch.Range.End;
            match = TextModelSearch.FindPreviousMatch(model, searchParams, startPos, captureMatches);
            match.ShouldBe(expectedMatch, $"findPrevMatch {startPos}");
        }
    }

    private static void AssertFindMatches(
        string text,
        string searchString,
        bool isRegex,
        bool matchCase,
        string? wordSeparators,
        (int StartLine, int StartColumn, int EndLine, int EndColumn)[] expected,
        bool captureMatches = false)
    {
        var expectedMatches = expected
            .Select(entry => new FindMatch(new TextRange(entry.StartLine, entry.StartColumn, entry.EndLine, entry.EndColumn), null))
            .ToArray();
        var searchParams = new SearchParams(searchString, isRegex, matchCase, wordSeparators);

        var model = PieceTreeTextBuffer.Create(text);
        AssertModelFindMatches(model, searchParams, expectedMatches, captureMatches);

        // upstream runs every case on an LF and on a CRLF model
        var model2 = PieceTreeTextBuffer.Create(text);
        model2.SetEOL("\r\n");
        AssertModelFindMatches(model2, searchParams, expectedMatches, captureMatches);
    }

    private static void AssertFindMatch(FindMatch? actual, TextRange expectedRange, string?[]? expectedMatches = null)
    {
        actual.ShouldBe(new FindMatch(expectedRange, expectedMatches));
    }

    private static readonly string[] RegularText =
    [
        "This is some foo - bar text which contains foo and bar - as in Barcelona.",
        "Now it begins a word fooBar and now it is caps Foo-isn't this great?",
        "And here's a dull line with nothing interesting in it",
        "It is also interesting if it's part of a word like amazingFooBar",
        "Again nothing interesting here",
    ];

    [Fact]
    public void Simple_Find()
    {
        AssertFindMatches(
            string.Join('\n', RegularText),
            "foo", false, false, null,
            [
                (1, 14, 1, 17),
                (1, 44, 1, 47),
                (2, 22, 2, 25),
                (2, 48, 2, 51),
                (4, 59, 4, 62),
            ]);
    }

    [Fact]
    public void Case_Sensitive_Find()
    {
        AssertFindMatches(
            string.Join('\n', RegularText),
            "foo", false, true, null,
            [
                (1, 14, 1, 17),
                (1, 44, 1, 47),
                (2, 22, 2, 25),
            ]);
    }

    [Fact]
    public void Whole_Words_Find()
    {
        AssertFindMatches(
            string.Join('\n', RegularText),
            "foo", false, false, UsualWordSeparators,
            [
                (1, 14, 1, 17),
                (1, 44, 1, 47),
                (2, 48, 2, 51),
            ]);
    }

    [Fact]
    public void Caret_Find()
    {
        AssertFindMatches(
            string.Join('\n', RegularText),
            "^", true, false, null,
            [
                (1, 1, 1, 1),
                (2, 1, 2, 1),
                (3, 1, 3, 1),
                (4, 1, 4, 1),
                (5, 1, 5, 1),
            ]);
    }

    [Fact]
    public void Dollar_Find()
    {
        AssertFindMatches(
            string.Join('\n', RegularText),
            "$", true, false, null,
            [
                (1, 74, 1, 74),
                (2, 69, 2, 69),
                (3, 54, 3, 54),
                (4, 65, 4, 65),
                (5, 31, 5, 31),
            ]);
    }

    [Fact]
    public void DotStar_Find()
    {
        AssertFindMatches(
            string.Join('\n', RegularText),
            ".*", true, false, null,
            [
                (1, 1, 1, 74),
                (2, 1, 2, 69),
                (3, 1, 3, 54),
                (4, 1, 4, 65),
                (5, 1, 5, 31),
            ]);
    }

    [Fact]
    public void Caret_Dollar_Find()
    {
        AssertFindMatches(
            string.Join(
                '\n',
                "This is some foo - bar text which contains foo and bar - as in Barcelona.",
                string.Empty,
                "And here's a dull line with nothing interesting in it",
                string.Empty,
                "Again nothing interesting here"),
            "^$", true, false, null,
            [
                (2, 1, 2, 1),
                (4, 1, 4, 1),
            ]);
    }

    [Fact]
    public void Multiline_Find_1()
    {
        AssertFindMatches(
            string.Join('\n', "Just some text text", "Just some text text", "some text again", "again some text"),
            "text\\n", true, false, null,
            [
                (1, 16, 2, 1),
                (2, 16, 3, 1),
            ]);
    }

    [Fact]
    public void Multiline_Find_2()
    {
        AssertFindMatches(
            string.Join('\n', "Just some text text", "Just some text text", "some text again", "again some text"),
            "text\\nJust", true, false, null,
            [
                (1, 16, 2, 5),
            ]);
    }

    [Fact]
    public void Multiline_Find_3()
    {
        AssertFindMatches(
            string.Join('\n', "Just some text text", "Just some text text", "some text again", "again some text"),
            "\\nagain", true, false, null,
            [
                (3, 16, 4, 6),
            ]);
    }

    [Fact]
    public void Multiline_Find_4()
    {
        AssertFindMatches(
            string.Join('\n', "Just some text text", "Just some text text", "some text again", "again some text"),
            ".*\\nJust.*\\n", true, false, null,
            [
                (1, 1, 3, 1),
            ]);
    }

    [Fact]
    public void Multiline_Find_With_Line_Beginning_Regex()
    {
        AssertFindMatches(
            string.Join('\n', "if", "else", string.Empty, "if", "else"),
            "^if\\nelse", true, false, null,
            [
                (1, 1, 2, 5),
                (4, 1, 5, 5),
            ]);
    }

    [Fact]
    public void Matching_Empty_Lines_Using_Boundary_Expression()
    {
        AssertFindMatches(
            string.Join('\n', "if", string.Empty, "else", "  ", "if", " ", "else"),
            "^\\s*$\\n", true, false, null,
            [
                (2, 1, 3, 1),
                (4, 1, 5, 1),
                (6, 1, 7, 1),
            ]);
    }

    [Fact]
    public void Matching_Lines_Starting_With_A_And_Ending_With_B()
    {
        AssertFindMatches(
            string.Join('\n', "a if b", "a", "ab", "eb"),
            "^a.*b$", true, false, null,
            [
                (1, 1, 1, 7),
                (3, 1, 3, 3),
            ]);
    }

    [Fact]
    public void Multiline_Find_With_Line_Ending_Regex()
    {
        AssertFindMatches(
            string.Join('\n', "if", "else", string.Empty, "if", "elseif", "else"),
            "if\\nelse$", true, false, null,
            [
                (1, 1, 2, 5),
                (5, 5, 6, 5),
            ]);
    }

    [Fact]
    public void Issue_4836_Caret_DotStar_Dollar()
    {
        AssertFindMatches(
            string.Join('\n', "Just some text text", string.Empty, "some text again", string.Empty, "again some text"),
            "^.*$", true, false, null,
            [
                (1, 1, 1, 20),
                (2, 1, 2, 1),
                (3, 1, 3, 16),
                (4, 1, 4, 1),
                (5, 1, 5, 16),
            ]);
    }

    [Fact]
    public void Multiline_Find_For_Non_Regex_String()
    {
        AssertFindMatches(
            string.Join('\n', "Just some text text", "some text text", "some text again", "again some text", "but not some"),
            "text\nsome", false, false, null,
            [
                (1, 16, 2, 5),
                (2, 11, 3, 5),
            ]);
    }

    [Fact]
    public void Issue_3623_Whole_Word_Does_Not_Work_For_Not_Latin_Characters()
    {
        AssertFindMatches(
            string.Join('\n', "я", "компилятор", "обфускация", ":я-я"),
            "я", false, false, UsualWordSeparators,
            [
                (1, 1, 1, 2),
                (4, 2, 4, 3),
                (4, 4, 4, 5),
            ]);
    }

    [Fact]
    public void Issue_27459_Whole_Words_Regression()
    {
        AssertFindMatches(
            string.Join(
                '\n',
                "this._register(this._textAreaInput.onKeyDown((e: IKeyboardEvent) => {",
                "\tthis._viewController.emitKeyDown(e);",
                "}));"),
            "((e: ", false, false, UsualWordSeparators,
            [
                (1, 45, 1, 50),
            ]);
    }

    [Fact]
    public void Issue_27594_Search_Results_Disappear()
    {
        AssertFindMatches(
            "this.server.listen(0);",
            "listen(", false, false, UsualWordSeparators,
            [
                (1, 13, 1, 20),
            ]);
    }

    [Fact]
    public void FindNextMatch_Without_Regex()
    {
        var model = PieceTreeTextBuffer.Create("line line one\nline two\nthree");
        var searchParams = new SearchParams("line", false, false, null);

        var actual = TextModelSearch.FindNextMatch(model, searchParams, new TextPosition(1, 1), false);
        AssertFindMatch(actual, new TextRange(1, 1, 1, 5));

        actual = TextModelSearch.FindNextMatch(model, searchParams, actual!.Range.End, false);
        AssertFindMatch(actual, new TextRange(1, 6, 1, 10));

        actual = TextModelSearch.FindNextMatch(model, searchParams, new TextPosition(1, 3), false);
        AssertFindMatch(actual, new TextRange(1, 6, 1, 10));

        actual = TextModelSearch.FindNextMatch(model, searchParams, actual!.Range.End, false);
        AssertFindMatch(actual, new TextRange(2, 1, 2, 5));

        actual = TextModelSearch.FindNextMatch(model, searchParams, actual!.Range.End, false);
        AssertFindMatch(actual, new TextRange(1, 1, 1, 5));
    }

    [Fact]
    public void FindNextMatch_With_Beginning_Boundary_Regex()
    {
        var model = PieceTreeTextBuffer.Create("line one\nline two\nthree");
        var searchParams = new SearchParams("^line", true, false, null);

        var actual = TextModelSearch.FindNextMatch(model, searchParams, new TextPosition(1, 1), false);
        AssertFindMatch(actual, new TextRange(1, 1, 1, 5));

        actual = TextModelSearch.FindNextMatch(model, searchParams, actual!.Range.End, false);
        AssertFindMatch(actual, new TextRange(2, 1, 2, 5));

        actual = TextModelSearch.FindNextMatch(model, searchParams, new TextPosition(1, 3), false);
        AssertFindMatch(actual, new TextRange(2, 1, 2, 5));

        actual = TextModelSearch.FindNextMatch(model, searchParams, actual!.Range.End, false);
        AssertFindMatch(actual, new TextRange(1, 1, 1, 5));
    }

    [Fact]
    public void FindNextMatch_With_Beginning_Boundary_Regex_And_Repetitive_Beginnings()
    {
        var model = PieceTreeTextBuffer.Create("line line one\nline two\nthree");
        var searchParams = new SearchParams("^line", true, false, null);

        var actual = TextModelSearch.FindNextMatch(model, searchParams, new TextPosition(1, 1), false);
        AssertFindMatch(actual, new TextRange(1, 1, 1, 5));

        actual = TextModelSearch.FindNextMatch(model, searchParams, actual!.Range.End, false);
        AssertFindMatch(actual, new TextRange(2, 1, 2, 5));

        actual = TextModelSearch.FindNextMatch(model, searchParams, new TextPosition(1, 3), false);
        AssertFindMatch(actual, new TextRange(2, 1, 2, 5));

        actual = TextModelSearch.FindNextMatch(model, searchParams, actual!.Range.End, false);
        AssertFindMatch(actual, new TextRange(1, 1, 1, 5));
    }

    [Fact]
    public void FindNextMatch_With_Beginning_Boundary_Multiline_Regex_And_Repetitive_Beginnings()
    {
        var model = PieceTreeTextBuffer.Create("line line one\nline two\nline three\nline four");
        var searchParams = new SearchParams("^line.*\\nline", true, false, null);

        var actual = TextModelSearch.FindNextMatch(model, searchParams, new TextPosition(1, 1), false);
        AssertFindMatch(actual, new TextRange(1, 1, 2, 5));

        actual = TextModelSearch.FindNextMatch(model, searchParams, actual!.Range.End, false);
        AssertFindMatch(actual, new TextRange(3, 1, 4, 5));

        actual = TextModelSearch.FindNextMatch(model, searchParams, new TextPosition(2, 1), false);
        AssertFindMatch(actual, new TextRange(2, 1, 3, 5));
    }

    [Fact]
    public void FindNextMatch_With_Ending_Boundary_Regex()
    {
        var model = PieceTreeTextBuffer.Create("one line line\ntwo line\nthree");
        var searchParams = new SearchParams("line$", true, false, null);

        var actual = TextModelSearch.FindNextMatch(model, searchParams, new TextPosition(1, 1), false);
        AssertFindMatch(actual, new TextRange(1, 10, 1, 14));

        actual = TextModelSearch.FindNextMatch(model, searchParams, new TextPosition(1, 4), false);
        AssertFindMatch(actual, new TextRange(1, 10, 1, 14));

        actual = TextModelSearch.FindNextMatch(model, searchParams, actual!.Range.End, false);
        AssertFindMatch(actual, new TextRange(2, 5, 2, 9));

        actual = TextModelSearch.FindNextMatch(model, searchParams, actual!.Range.End, false);
        AssertFindMatch(actual, new TextRange(1, 10, 1, 14));
    }

    [Fact]
    public void FindMatches_With_Capturing_Matches()
    {
        var model = PieceTreeTextBuffer.Create("one line line\ntwo line\nthree");
        var searchParams = new SearchParams("(l(in)e)", true, false, null);

        var actual = TextModelSearch.FindMatches(model, searchParams, FullRange(model), true, 100);
        actual.ShouldBe(
        [
            new FindMatch(new TextRange(1, 5, 1, 9), ["line", "line", "in"]),
            new FindMatch(new TextRange(1, 10, 1, 14), ["line", "line", "in"]),
            new FindMatch(new TextRange(2, 5, 2, 9), ["line", "line", "in"]),
        ]);
    }

    [Fact]
    public void FindMatches_Multiline_With_Capturing_Matches()
    {
        var model = PieceTreeTextBuffer.Create("one line line\ntwo line\nthree");
        var searchParams = new SearchParams("(l(in)e)\\n", true, false, null);

        var actual = TextModelSearch.FindMatches(model, searchParams, FullRange(model), true, 100);
        actual.ShouldBe(
        [
            new FindMatch(new TextRange(1, 10, 2, 1), ["line\n", "line", "in"]),
            new FindMatch(new TextRange(2, 5, 3, 1), ["line\n", "line", "in"]),
        ]);
    }

    [Fact]
    public void FindNextMatch_With_Capturing_Matches()
    {
        var model = PieceTreeTextBuffer.Create("one line line\ntwo line\nthree");
        var searchParams = new SearchParams("(l(in)e)", true, false, null);

        var actual = TextModelSearch.FindNextMatch(model, searchParams, new TextPosition(1, 1), true);
        AssertFindMatch(actual, new TextRange(1, 5, 1, 9), ["line", "line", "in"]);
    }

    [Fact]
    public void FindNextMatch_Multiline_With_Capturing_Matches()
    {
        var model = PieceTreeTextBuffer.Create("one line line\ntwo line\nthree");
        var searchParams = new SearchParams("(l(in)e)\\n", true, false, null);

        var actual = TextModelSearch.FindNextMatch(model, searchParams, new TextPosition(1, 1), true);
        AssertFindMatch(actual, new TextRange(1, 10, 2, 1), ["line\n", "line", "in"]);
    }

    [Fact]
    public void FindPreviousMatch_With_Capturing_Matches()
    {
        var model = PieceTreeTextBuffer.Create("one line line\ntwo line\nthree");
        var searchParams = new SearchParams("(l(in)e)", true, false, null);

        var actual = TextModelSearch.FindPreviousMatch(model, searchParams, new TextPosition(1, 1), true);
        AssertFindMatch(actual, new TextRange(2, 5, 2, 9), ["line", "line", "in"]);
    }

    [Fact]
    public void FindPreviousMatch_Multiline_With_Capturing_Matches()
    {
        var model = PieceTreeTextBuffer.Create("one line line\ntwo line\nthree");
        var searchParams = new SearchParams("(l(in)e)\\n", true, false, null);

        var actual = TextModelSearch.FindPreviousMatch(model, searchParams, new TextPosition(1, 1), true);
        AssertFindMatch(actual, new TextRange(2, 5, 3, 1), ["line\n", "line", "in"]);
    }

    [Fact]
    public void Newline_Matches_Crlf()
    {
        var model = PieceTreeTextBuffer.Create("a\r\nb\r\nc\r\nd\r\ne\r\nf\r\ng\r\nh\r\ni");
        model.GetEOL().ShouldBe("\r\n");

        var searchParams = new SearchParams("h\\n", true, false, null);
        var actual = TextModelSearch.FindNextMatch(model, searchParams, new TextPosition(1, 1), true);
        actual = TextModelSearch.FindMatches(model, searchParams, FullRange(model), true, 1000)[0];
        AssertFindMatch(actual, new TextRange(8, 1, 9, 1), ["h\n"]);

        searchParams = new SearchParams("g\\nh\\n", true, false, null);
        actual = TextModelSearch.FindNextMatch(model, searchParams, new TextPosition(1, 1), true);
        actual = TextModelSearch.FindMatches(model, searchParams, FullRange(model), true, 1000)[0];
        AssertFindMatch(actual, new TextRange(7, 1, 9, 1), ["g\nh\n"]);

        searchParams = new SearchParams("\\ni", true, false, null);
        actual = TextModelSearch.FindNextMatch(model, searchParams, new TextPosition(1, 1), true);
        actual = TextModelSearch.FindMatches(model, searchParams, FullRange(model), true, 1000)[0];
        AssertFindMatch(actual, new TextRange(8, 2, 9, 2), ["\ni"]);
    }

    [Fact]
    public void Carriage_Return_Can_Never_Be_Found()
    {
        var model = PieceTreeTextBuffer.Create("a\r\nb\r\nc\r\nd\r\ne\r\nf\r\ng\r\nh\r\ni");
        model.GetEOL().ShouldBe("\r\n");

        var searchParams = new SearchParams("\\r\\n", true, false, null);
        var actual = TextModelSearch.FindNextMatch(model, searchParams, new TextPosition(1, 1), true);
        actual.ShouldBeNull();
        TextModelSearch.FindMatches(model, searchParams, FullRange(model), true, 1000).ShouldBeEmpty();
    }

    private static void AssertParseSearchResult(
        string searchString,
        bool isRegex,
        bool matchCase,
        string? wordSeparators,
        string? expectedSource,
        bool expectedMultiline,
        string? expectedSimpleSearch)
    {
        var searchParams = new SearchParams(searchString, isRegex, matchCase, wordSeparators);
        var actual = searchParams.ParseSearchRequest();

        if (expectedSource is null)
        {
            actual.ShouldBeNull();
            return;
        }

        actual.ShouldNotBeNull();
        actual!.Regex.Source.ShouldBe(expectedSource);
        actual.Regex.Multiline.ShouldBe(expectedMultiline);
        actual.Regex.MatchCase.ShouldBe(matchCase);
        actual.SimpleSearch.ShouldBe(expectedSimpleSearch);
        if (wordSeparators is not null)
        {
            actual.WordSeparators.ShouldNotBeNull();
        }
        else
        {
            actual.WordSeparators.ShouldBeNull();
        }
    }

    [Fact]
    public void ParseSearchRequest_Invalid()
    {
        AssertParseSearchResult(string.Empty, true, true, UsualWordSeparators, null, false, null);
        AssertParseSearchResult("(", true, false, null, null, false, null);
    }

    [Fact]
    public void ParseSearchRequest_Non_Regex()
    {
        AssertParseSearchResult("foo", false, false, null, "foo", false, null);
        AssertParseSearchResult("foo", false, false, UsualWordSeparators, "foo", false, null);
        AssertParseSearchResult("foo", false, true, null, "foo", false, "foo");
        AssertParseSearchResult("foo", false, true, UsualWordSeparators, "foo", false, "foo");
        AssertParseSearchResult("foo\\n", false, false, null, "foo\\\\n", false, null);
        AssertParseSearchResult("foo\\\\n", false, false, null, "foo\\\\\\\\n", false, null);
        AssertParseSearchResult("foo\\r", false, false, null, "foo\\\\r", false, null);
        AssertParseSearchResult("foo\\\\r", false, false, null, "foo\\\\\\\\r", false, null);
    }

    [Fact]
    public void ParseSearchRequest_Regex()
    {
        AssertParseSearchResult("foo", true, false, null, "foo", false, null);
        AssertParseSearchResult("foo", true, false, UsualWordSeparators, "foo", false, null);
        AssertParseSearchResult("foo", true, true, null, "foo", false, null);
        AssertParseSearchResult("foo", true, true, UsualWordSeparators, "foo", false, null);
        AssertParseSearchResult("foo\\n", true, false, null, "foo\\n", true, null);
        AssertParseSearchResult("foo\\\\n", true, false, null, "foo\\\\n", false, null);
        AssertParseSearchResult("foo\\r", true, false, null, "foo\\r", true, null);
        AssertParseSearchResult("foo\\\\r", true, false, null, "foo\\\\r", false, null);
    }

    [Fact]
    public void Issue_53415_Backslash_W_Should_Match_Line_Break()
    {
        AssertFindMatches(
            string.Join('\n', "text", "180702-", "180703-180704"),
            "\\d{6}-\\W", true, false, null,
            [
                (2, 1, 3, 1),
            ]);

        AssertFindMatches(
            string.Join('\n', "Just some text", string.Empty, "Just"),
            "\\W", true, false, null,
            [
                (1, 5, 1, 6),
                (1, 10, 1, 11),
                (1, 15, 2, 1),
                (2, 1, 3, 1),
            ]);

        // Line break doesn't affect the result as we always use \n as line break when doing search
        AssertFindMatches(
            string.Join("\r\n", "Just some text", string.Empty, "Just"),
            "\\W", true, false, null,
            [
                (1, 5, 1, 6),
                (1, 10, 1, 11),
                (1, 15, 2, 1),
                (2, 1, 3, 1),
            ]);

        AssertFindMatches(
            string.Join('\n', "Just some text", "\tJust", "Just"),
            "\\W", true, false, null,
            [
                (1, 5, 1, 6),
                (1, 10, 1, 11),
                (1, 15, 2, 1),
                (2, 1, 2, 2),
                (2, 6, 3, 1),
            ]);

        // line break is seen as one non-word character
        AssertFindMatches(
            string.Join('\n', "Just  some text", string.Empty, "Just"),
            "\\W{2}", true, false, null,
            [
                (1, 5, 1, 7),
                (1, 16, 3, 1),
            ]);

        // even if it's \r\n
        AssertFindMatches(
            string.Join("\r\n", "Just  some text", string.Empty, "Just"),
            "\\W{2}", true, false, null,
            [
                (1, 5, 1, 7),
                (1, 16, 3, 1),
            ]);
    }

    [Fact]
    public void Simple_Find_Using_Unicode_Escape_Sequences()
    {
        AssertFindMatches(
            string.Join('\n', RegularText),
            "\\u{0066}\\u006f\\u006F", true, false, null,
            [
                (1, 14, 1, 17),
                (1, 44, 1, 47),
                (2, 22, 2, 25),
                (2, 48, 2, 51),
                (4, 59, 4, 62),
            ]);
    }

    [Fact]
    public void IsMultilineRegexSource_()
    {
        TextModelSearch.IsMultilineRegexSource("foo").ShouldBeFalse();
        TextModelSearch.IsMultilineRegexSource(string.Empty).ShouldBeFalse();
        TextModelSearch.IsMultilineRegexSource("foo\\sbar").ShouldBeFalse();
        TextModelSearch.IsMultilineRegexSource("\\\\notnewline").ShouldBeFalse();

        TextModelSearch.IsMultilineRegexSource("foo\\nbar").ShouldBeTrue();
        TextModelSearch.IsMultilineRegexSource("foo\\nbar\\s").ShouldBeTrue();
        TextModelSearch.IsMultilineRegexSource("foo\\r\\n").ShouldBeTrue();
        TextModelSearch.IsMultilineRegexSource("\\n").ShouldBeTrue();
        TextModelSearch.IsMultilineRegexSource("foo\\W").ShouldBeTrue();
        TextModelSearch.IsMultilineRegexSource("foo\n").ShouldBeTrue();
        TextModelSearch.IsMultilineRegexSource("foo\r\n").ShouldBeTrue();
    }

    [Fact]
    public void IsMultilineRegexSource_Correctly_Identifies_Multiline_Patterns()
    {
        var singleLinePatterns = new[]
        {
            "MARK:\\s*(?<label>.*)$",
            "^// Header$",
            "\\s*[-=]+\\s*",
        };

        var multiLinePatterns = new[]
        {
            "^\\/\\/ =+\\n^\\/\\/ (?<label>[^\\n]+?)\\n^\\/\\/ =+$",
            "header\\r\\nfooter",
            "start\\r|\\nend",
            "top\nmiddle\r\nbottom",
        };

        foreach (var pattern in singleLinePatterns)
        {
            TextModelSearch.IsMultilineRegexSource(pattern).ShouldBeFalse($"Pattern should not be multiline: {pattern}");
        }

        foreach (var pattern in multiLinePatterns)
        {
            TextModelSearch.IsMultilineRegexSource(pattern).ShouldBeTrue($"Pattern should be multiline: {pattern}");
        }
    }

    [Fact]
    public void Issue_74715_DigitStar_Finds_Empty_String_And_Stops_Searching()
    {
        var model = PieceTreeTextBuffer.Create("10.243.30.10");
        var searchParams = new SearchParams("\\d*", true, false, null);

        var actual = TextModelSearch.FindMatches(model, searchParams, FullRange(model), true, 100);
        actual.ShouldBe(
        [
            new FindMatch(new TextRange(1, 1, 1, 3), ["10"]),
            new FindMatch(new TextRange(1, 3, 1, 3), [""]),
            new FindMatch(new TextRange(1, 4, 1, 7), ["243"]),
            new FindMatch(new TextRange(1, 7, 1, 7), [""]),
            new FindMatch(new TextRange(1, 8, 1, 10), ["30"]),
            new FindMatch(new TextRange(1, 10, 1, 10), [""]),
            new FindMatch(new TextRange(1, 11, 1, 13), ["10"]),
        ]);
    }

    [Fact]
    public void Issue_100134_Zero_Length_Matches_Should_Step_Over_Surrogate_Pairs()
    {
        // 1[Laptop]1 - there should be no matches inside of the [Laptop] emoji
        AssertFindMatches(
            "1" + EmojiLaptop + "1",
            "()", true, false, null,
            [
                (1, 1, 1, 1),
                (1, 2, 1, 2),
                (1, 4, 1, 4),
                (1, 5, 1, 5),
            ]);

        // 1[Cat Face][ZWJ][Laptop]1 - there should be matches between emoji and ZWJ,
        // there should be no matches inside of [Cat Face] and [Laptop] emoji
        var text = "1" + char.ConvertFromUtf32(0x1F431) + ((char)0x200D) + EmojiLaptop + "1";
        AssertFindMatches(
            text,
            "()", true, false, null,
            [
                (1, 1, 1, 1),
                (1, 2, 1, 2),
                (1, 4, 1, 4),
                (1, 5, 1, 5),
                (1, 7, 1, 7),
                (1, 8, 1, 8),
            ]);
    }
}
