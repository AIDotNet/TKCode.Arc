// Tests for what the search does differently from vscode's textModelSearch.ts and pieceTreeBase.ts: it searches views
// of the buffer and of its lines where upstream takes substrings (a slice in JavaScript, a copy in .NET), and it
// has a faster expression for basic ASCII text. The results must be the ones the string search gives.

using System.Text;
using Shouldly;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class SearchWithoutCopyTests
{
    private const string UsualWordSeparators = @"`~!@#$%^&*()-=+[{]}\|;:'"",.<>/?";
    private static readonly string Emoji = char.ConvertFromUtf32(0x1F600);

    public static TheoryData<string, string, string?> SearcherCases()
    {
        var data = new TheoryData<string, string, string?>();
        var texts = new[]
        {
            string.Empty,
            "foo bar foo.bar foobar",
            "  leading and trailing  ",
            "a" + Emoji + "b" + Emoji + Emoji + "c",
            "line one\nline two\r\nline three",
        };
        var patterns = new[] { "foo", "o+", "a*", "\\b", "^", "$", ".", "(?<=o)\\s", "[^a-z]*", "(f)(o)?" };
        foreach (var text in texts)
        {
            foreach (var pattern in patterns)
            {
                data.Add(text, pattern, null);
                data.Add(text, pattern, UsualWordSeparators);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SearcherCases))]
    public void A_View_Search_Finds_What_The_String_Search_Finds(string text, string pattern, string? wordSeparators)
    {
        var regex = JsRegex.Create(pattern, isRegex: true, matchCase: false, multiline: false).Regex;
        var classifier = wordSeparators is null ? null : new WordCharacterClassifier(wordSeparators);

        var fromString = new List<(int Index, int Length)>();
        var stringSearcher = new Searcher(classifier, regex);
        stringSearcher.Reset(0);
        while (stringSearcher.Next(text) is { } match)
        {
            fromString.Add((match.Index, match.Length));
        }

        var fromView = new List<(int Index, int Length)>();
        var viewSearcher = new Searcher(classifier, regex);
        viewSearcher.Reset(0);
        while (viewSearcher.Next(text.AsSpan(), out var index, out var length))
        {
            fromView.Add((index, length));
        }

        fromView.ShouldBe(fromString);
    }

    [Fact]
    public void Captures_Of_An_Expression_Without_Groups_Are_The_Match()
    {
        var searcher = new Searcher(null, JsRegex.Create("fo+", isRegex: true, matchCase: false, multiline: false).Regex);
        searcher.HasCaptureGroups.ShouldBeFalse();
        searcher.NeedsString(captureMatches: true).ShouldBeFalse();

        searcher.Reset(0);
        searcher.Next("a FOO b".AsSpan(), null, captureMatches: true, out var index, out var length, out var matches)
            .ShouldBeTrue();
        index.ShouldBe(2);
        length.ShouldBe(3);
        matches.ShouldBe(["FOO"]);
    }

    [Fact]
    public void Captures_Of_An_Expression_With_Groups_Need_The_String()
    {
        var searcher = new Searcher(null, JsRegex.Create("(f)(x)?(o+)", isRegex: true, matchCase: true, multiline: false).Regex);
        searcher.HasCaptureGroups.ShouldBeTrue();
        searcher.NeedsString(captureMatches: true).ShouldBeTrue();
        searcher.NeedsString(captureMatches: false).ShouldBeFalse();

        const string text = "a foo b";
        searcher.Reset(0);
        searcher.Next(text, text, captureMatches: true, out _, out _, out var matches).ShouldBeTrue();
        matches.ShouldBe(["foo", "f", null, "oo"]);
    }

    [Fact]
    public void Piece_Tree_Search_Returns_Captures_With_And_Without_Groups()
    {
        var pieceTable = PieceTreeTestHelpers.CreateTextBuffer(["one foo\ntwo foo\nthree"]).GetPieceTree();
        pieceTable.Insert(0, "zero foo\n", true);
        var range = new TextRange(1, 1, 4, 6);

        var plain = pieceTable.FindMatchesLineByLine(
            range,
            new SearchData(JsRegex.Create("foo", isRegex: true, matchCase: true, multiline: false), null, "foo"),
            captureMatches: true,
            limitResultCount: 1000);
        plain.ShouldBe(
        [
            new FindMatch(new TextRange(1, 6, 1, 9), ["foo"]),
            new FindMatch(new TextRange(2, 5, 2, 8), ["foo"]),
            new FindMatch(new TextRange(3, 5, 3, 8), ["foo"]),
        ]);

        var grouped = pieceTable.FindMatchesLineByLine(
            range,
            new SearchData(JsRegex.Create("(f)(o+)", isRegex: true, matchCase: true, multiline: false), null, null),
            captureMatches: true,
            limitResultCount: 1000);
        grouped.ShouldBe(
        [
            new FindMatch(new TextRange(1, 6, 1, 9), ["foo", "f", "oo"]),
            new FindMatch(new TextRange(2, 5, 2, 8), ["foo", "f", "oo"]),
            new FindMatch(new TextRange(3, 5, 3, 8), ["foo", "f", "oo"]),
        ]);
    }

    [Fact]
    public void A_Line_View_Is_The_Line_Content()
    {
        // not normalized: \r\n, \r and \n all end a line, and each is trimmed from the view
        var pieceTable = PieceTreeTestHelpers.CreateTextBuffer(["ab\r\ncd\ref\n\ngh"], normalizeEol: false).GetPieceTree();
        // a line that an edit split over three pieces
        pieceTable.Insert(1, "XY", false);

        string[] expected = ["aXYb", "cd", "ef", string.Empty, "gh"];
        pieceTable.GetLineCount().ShouldBe(expected.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            pieceTable.GetLineContentSpan(i + 1).ToString().ShouldBe(expected[i]);
            pieceTable.GetLineContent(i + 1).ShouldBe(expected[i]);
            // GetLineContent remembers its last line; the view of that line is the same text
            pieceTable.GetLineContentSpan(i + 1).ToString().ShouldBe(expected[i]);
        }
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("a", "a")]
    [InlineData("a\n", "a")]
    [InlineData("a\r", "a")]
    [InlineData("a\r\n", "a")]
    [InlineData("a\n\n", "a\n")]
    [InlineData("a\n\r", "a\n")]
    [InlineData("\n", "")]
    [InlineData("\r\n", "")]
    public void TrimTrailingLineBreak_Removes_One_Line_Break(string value, string expected)
    {
        PieceTreeStrings.TrimTrailingLineBreak(value).ShouldBe(expected);
        PieceTreeStrings.TrimTrailingLineBreak(value.AsSpan()).ToString().ShouldBe(expected);
    }

    [Fact]
    public void Basic_Ascii_Text_Gets_A_Plain_Class_For_The_Dot()
    {
        var withDot = JsRegex.Create("a.c", isRegex: true, matchCase: true, multiline: false);
        withDot.GetRegex(mightContainNonBasicAscii: true).ShouldBeSameAs(withDot.Regex);
        withDot.GetRegex(mightContainNonBasicAscii: false).ShouldNotBeSameAs(withDot.Regex);
        withDot.GetRegex(mightContainNonBasicAscii: false).MatchTimeout.ShouldBe(JsRegex.MatchTimeout);

        // nothing to gain without a dot (an escaped one or one in a class is not the dot)
        foreach (var pattern in new[] { "abc", "a\\.c", "a[.]c" })
        {
            var withoutDot = JsRegex.Create(pattern, isRegex: true, matchCase: true, multiline: false);
            withoutDot.GetRegex(mightContainNonBasicAscii: false).ShouldBeSameAs(withoutDot.Regex);
        }

        // on basic ASCII text both find the same; only the general one keeps a surrogate pair whole
        const string ascii = "abc a\tc a\nc a\rc ac";
        var plain = withDot.GetRegex(mightContainNonBasicAscii: false);
        plain.Matches(ascii).Select(m => (m.Index, m.Length))
            .ShouldBe(withDot.Regex.Matches(ascii).Select(m => (m.Index, m.Length)));
        withDot.Regex.Match("a" + Emoji + "c").Success.ShouldBeTrue();
    }

    [Fact]
    public void Find_Uses_The_Right_Dot_For_The_Buffer()
    {
        var searchParams = new SearchParams("a.c", isRegex: true, matchCase: true, wordSeparators: null);

        var ascii = PieceTreeTextBuffer.Create("abc\na-c\nac");
        ascii.MightContainNonBasicASCII().ShouldBeFalse();
        TextModelSearch.FindMatches(ascii, searchParams, new TextRange(1, 1, 3, 3), false, 1000)
            .Select(m => m.Range)
            .ShouldBe([new TextRange(1, 1, 1, 4), new TextRange(2, 1, 2, 4)]);

        var astral = PieceTreeTextBuffer.Create("abc\na" + Emoji + "c\nac");
        astral.MightContainNonBasicASCII().ShouldBeTrue();
        TextModelSearch.FindMatches(astral, searchParams, new TextRange(1, 1, 3, 3), false, 1000)
            .Select(m => m.Range)
            .ShouldBe([new TextRange(1, 1, 1, 4), new TextRange(2, 1, 2, 5)]);
    }

    [Fact]
    public void Searching_Does_Not_Copy_The_Buffer_Or_Its_Lines()
    {
        // About 2 MB of text in 64 KB chunks, then 1000 edits whose text ends up in the change buffer.
        var text = new StringBuilder();
        for (var i = 0; i < 20_000; i++)
        {
            text.Append("the quick brown fox jumps over the lazy dog ").Append(i).Append('\n');
        }

        var content = text.ToString();
        var builder = new PieceTreeTextBufferBuilder();
        for (var offset = 0; offset < content.Length; offset += PieceTreeBase.AverageBufferSize)
        {
            builder.AcceptChunk(content.Substring(offset, Math.Min(PieceTreeBase.AverageBufferSize, content.Length - offset)));
        }

        var buffer = new PieceTreeTextBuffer(builder.Finish().Create(DefaultEndOfLine.LF));
        var pieceTable = buffer.GetPieceTree();
        var random = new Random(1);
        for (var i = 0; i < 1000; i++)
        {
            var line = random.Next(1, pieceTable.GetLineCount());
            pieceTable.Insert(pieceTable.GetOffsetAt(line, 1), "inserted " + i + "\nsecond line\nthird line\n", true);
        }

        var range = new TextRange(1, 1, buffer.GetLineCount(), buffer.GetLineMaxColumn(buffer.GetLineCount()));
        var documentBytes = (long)buffer.GetLength() * sizeof(char);
        var plainText = new SearchParams("not in the text", isRegex: false, matchCase: true, wordSeparators: null);
        var plainData = plainText.ParseSearchRequest().ShouldNotBeNull();
        var regex = new SearchParams("not [a-z]+ the text", isRegex: true, matchCase: true, wordSeparators: null);

        // Searching copies used to cost many times the document: the node search copied the buffer up to the end of
        // every piece it visited, and the line searches made a string of every line.
        Allocated(() => buffer.FindMatchesLineByLine(range, plainData, false, int.MaxValue).ShouldBeEmpty())
            .ShouldBeLessThan(documentBytes / 4);
        Allocated(() => TextModelSearch.FindMatches(buffer, plainText, range, false, int.MaxValue).ShouldBeEmpty())
            .ShouldBeLessThan(documentBytes / 4);
        Allocated(() => TextModelSearch.FindMatches(buffer, regex, range, false, int.MaxValue).ShouldBeEmpty())
            .ShouldBeLessThan(documentBytes / 4);
        Allocated(() => TextModelSearch.FindNextMatch(buffer, regex, new TextPosition(1, 1), false).ShouldBeNull())
            .ShouldBeLessThan(documentBytes / 4);
        Allocated(() => TextModelSearch.FindPreviousMatch(buffer, regex, new TextPosition(1, 1), false).ShouldBeNull())
            .ShouldBeLessThan(documentBytes / 4);
    }

    private static long Allocated(Action search)
    {
        // once to get what the first call sets up out of the way
        search();
        var before = GC.GetAllocatedBytesForCurrentThread();
        search();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
