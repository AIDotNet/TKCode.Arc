// Ported from vscode (src/vs/editor/contrib/comment/test/browser/lineCommentCommand.test.ts, commit b57d5c9e), MIT
// license. Each upstream suite is a class; this file has 'Editor Contrib - Line Comment Command', the others are in
// LineCommentCommandTests.Suites.cs. Differences from upstream: languages are ids (no language service); the
// tokenization support of the mixed modes suite is registered on the model (TestTokenizationSupports.RegisterByLine).

using Shouldly;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Contrib.Comment;
using TKCode.Arc.Editor.Languages;
using Xunit;
using static TKCode.Arc.Editor.Tests.Contrib.EditingAssert;
using static TKCode.Arc.Editor.Tests.Contrib.LineCommentTestHelpers;

namespace TKCode.Arc.Editor.Tests.Contrib;

/// <summary>The helpers of lineCommentCommand.test.ts.</summary>
internal static class LineCommentTestHelpers
{
    public delegate void TestHelper(string[] lines, TextSelection selection, string[] expectedLines, TextSelection expectedSelection);

    public static TestHelper CreateTestCommandHelper(CommentRule commentsConfig, Func<LanguageConfigurationService, TextSelection, ICommand> commandFactory) =>
        (lines, selection, expectedLines, expectedSelection) =>
        {
            const string languageId = "commentMode";
            TestCommand.Run(lines, languageId, selection, commandFactory, expectedLines, expectedSelection,
                service => service.Register(languageId, new LanguageConfiguration { Comments = commentsConfig }));
        };

    public static ILineCommentSimpleModel CreateSimpleModel(string[] lines) => new SimpleModel(lines);

    public static List<LinePreflightData> CreateBasicLinePreflightData(string[] commentTokens) =>
        commentTokens.Select(commentString => new LinePreflightData
        {
            Ignore = false,
            CommentStr = commentString,
            CommentStrOffset = 0,
            CommentStrLength = commentString.Length,
        }).ToList();

    private sealed class SimpleModel(string[] lines) : ILineCommentSimpleModel
    {
        public string GetLineContent(int lineNumber) => lines[lineNumber - 1];
    }
}

public sealed class LineCommentCommandTests
{
    private static readonly TestHelper TestLineCommentCommand = CreateTestCommandHelper(
        new CommentRule { LineComment = "!@#", BlockComment = ("<!@#", "#@!>") },
        (service, sel) => new LineCommentCommand(service, sel, 4, LineCommentType.Toggle, true, true)
    );

    private static readonly TestHelper TestAddLineCommentCommand = CreateTestCommandHelper(
        new CommentRule { LineComment = "!@#", BlockComment = ("<!@#", "#@!>") },
        (service, sel) => new LineCommentCommand(service, sel, 4, LineCommentType.ForceAdd, true, true)
    );

    private static readonly TestHelper TestLineCommentCommandTokenFirstColumn = CreateTestCommandHelper(
        new CommentRule { LineComment = new LineCommentConfig("!@#") { NoIndent = true }, BlockComment = ("<!@#", "#@!>") },
        (service, sel) => new LineCommentCommand(service, sel, 4, LineCommentType.Toggle, true, true)
    );

    [Fact]
    public void CommentSingleLine()
    {
        TestLineCommentCommand(
            [
                "some text",
                "\tsome more text"
            ],
            new TextSelection(1, 1, 1, 1),
            [
                "!@# some text",
                "\tsome more text"
            ],
            new TextSelection(1, 5, 1, 5)
        );
    }


    [Fact]
    public void CaseInsensitive()
    {
        var TestLineCommentCommand = CreateTestCommandHelper(
            new CommentRule { LineComment = "rem" },
            (service, sel) => new LineCommentCommand(service, sel, 4, LineCommentType.Toggle, true, true)
        );

        TestLineCommentCommand(
            [
                "REM some text"
            ],
            new TextSelection(1, 1, 1, 1),
            [
                "some text"
            ],
            new TextSelection(1, 1, 1, 1)
        );
    }


    [Fact]
    public void CommentWithTokenColumnFixed()
    {
        TestLineCommentCommandTokenFirstColumn(
            [
                "some text",
                "\tsome more text"
            ],
            new TextSelection(2, 1, 2, 1),
            [
                "some text",
                "!@# \tsome more text"
            ],
            new TextSelection(2, 5, 2, 5)
        );
    }



    [Fact]
    public void AnalyzeLines()
    {
        using var languageConfigurationService = new LanguageConfigurationService();
        PreflightData r;

        r = LineCommentCommand.AnalyzeLines(LineCommentType.Toggle, true, CreateSimpleModel([
            "\t\t",
            "    ",
            "    c",
            "\t\td"
        ]), CreateBasicLinePreflightData(["//", "rem", "!@#", "!@#"]), 1, true, false, languageConfigurationService, "plaintext");
        if (!r.Supported)
        {
            throw new InvalidOperationException("unexpected");
        }

        AssertDeepStrictEqual(r.ShouldRemoveComments, false);

        // Does not change `commentStr`
        AssertDeepStrictEqual(r.Lines[0].CommentStr, "//");
        AssertDeepStrictEqual(r.Lines[1].CommentStr, "rem");
        AssertDeepStrictEqual(r.Lines[2].CommentStr, "!@#");
        AssertDeepStrictEqual(r.Lines[3].CommentStr, "!@#");

        // Fills in `isWhitespace`
        AssertDeepStrictEqual(r.Lines[0].Ignore, true);
        AssertDeepStrictEqual(r.Lines[1].Ignore, true);
        AssertDeepStrictEqual(r.Lines[2].Ignore, false);
        AssertDeepStrictEqual(r.Lines[3].Ignore, false);

        // Fills in `commentStrOffset`
        AssertDeepStrictEqual(r.Lines[0].CommentStrOffset, 2);
        AssertDeepStrictEqual(r.Lines[1].CommentStrOffset, 4);
        AssertDeepStrictEqual(r.Lines[2].CommentStrOffset, 4);
        AssertDeepStrictEqual(r.Lines[3].CommentStrOffset, 2);


        r = LineCommentCommand.AnalyzeLines(LineCommentType.Toggle, true, CreateSimpleModel([
            "\t\t",
            "    rem ",
            "    !@# c",
            "\t\t!@#d"
        ]), CreateBasicLinePreflightData(["//", "rem", "!@#", "!@#"]), 1, true, false, languageConfigurationService, "plaintext");
        if (!r.Supported)
        {
            throw new InvalidOperationException("unexpected");
        }

        AssertDeepStrictEqual(r.ShouldRemoveComments, true);

        // Does not change `commentStr`
        AssertDeepStrictEqual(r.Lines[0].CommentStr, "//");
        AssertDeepStrictEqual(r.Lines[1].CommentStr, "rem");
        AssertDeepStrictEqual(r.Lines[2].CommentStr, "!@#");
        AssertDeepStrictEqual(r.Lines[3].CommentStr, "!@#");

        // Fills in `isWhitespace`
        AssertDeepStrictEqual(r.Lines[0].Ignore, true);
        AssertDeepStrictEqual(r.Lines[1].Ignore, false);
        AssertDeepStrictEqual(r.Lines[2].Ignore, false);
        AssertDeepStrictEqual(r.Lines[3].Ignore, false);

        // Fills in `commentStrOffset`
        AssertDeepStrictEqual(r.Lines[0].CommentStrOffset, 2);
        AssertDeepStrictEqual(r.Lines[1].CommentStrOffset, 4);
        AssertDeepStrictEqual(r.Lines[2].CommentStrOffset, 4);
        AssertDeepStrictEqual(r.Lines[3].CommentStrOffset, 2);

        // Fills in `commentStrLength`
        AssertDeepStrictEqual(r.Lines[0].CommentStrLength, 2);
        AssertDeepStrictEqual(r.Lines[1].CommentStrLength, 4);
        AssertDeepStrictEqual(r.Lines[2].CommentStrLength, 4);
        AssertDeepStrictEqual(r.Lines[3].CommentStrLength, 3);

    }


    [Fact]
    public void NormalizeInsertionPoint()
    {

        void runTest(object[] mixedArr, int tabSize, int[] expected, string testName)
        {
            var model = CreateSimpleModel(mixedArr.Where((item, idx) => idx % 2 == 0).Cast<string>().ToArray());
            var offsets = mixedArr.Where((item, idx) => idx % 2 == 1).Select(offset => new LinePreflightData
            {
                CommentStrOffset = (int)offset,
                Ignore = false,
            }).ToList();
            LineCommentCommand.NormalizeInsertionPoint(model, offsets, 1, tabSize);
            var actual = offsets.Select(item => item.CommentStrOffset).ToArray();
            actual.ShouldBe(expected, testName);
        }

        // Bug 16696:[comment] comments not aligned in this case
        runTest(new object[] {
            "  XX", 2,
            "    YY", 4
        }, 4, [0, 0], "Bug 16696");

        runTest(new object[] {
            "\t\t\tXX", 3,
            "    \tYY", 5,
            "        ZZ", 8,
            "\t\tTT", 2
        }, 4, [2, 5, 8, 2], "Test1");

        runTest(new object[] {
            "\t\t\t   XX", 6,
            "    \t\t\t\tYY", 8,
            "        ZZ", 8,
            "\t\t    TT", 6
        }, 4, [2, 5, 8, 2], "Test2");

        runTest(new object[] {
            "\t\t", 2,
            "\t\t\t", 3,
            "\t\t\t\t", 4,
            "\t\t\t", 3
        }, 4, [2, 2, 2, 2], "Test3");

        runTest(new object[] {
            "\t\t", 2,
            "\t\t\t", 3,
            "\t\t\t\t", 4,
            "\t\t\t", 3,
            "    ", 4
        }, 2, [2, 2, 2, 2, 4], "Test4");

        runTest(new object[] {
            "\t\t", 2,
            "\t\t\t", 3,
            "\t\t\t\t", 4,
            "\t\t\t", 3,
            "    ", 4
        }, 4, [1, 1, 1, 1, 4], "Test5");

        runTest(new object[] {
            " \t", 2,
            "  \t", 3,
            "   \t", 4,
            "    ", 4,
            "\t", 1
        }, 4, [2, 3, 4, 4, 1], "Test6");

        runTest(new object[] {
            " \t\t", 3,
            "  \t\t", 4,
            "   \t\t", 5,
            "    \t", 5,
            "\t", 1
        }, 4, [2, 3, 4, 4, 1], "Test7");

        runTest(new object[] {
            "\t", 1,
            "    ", 4
        }, 4, [1, 4], "Test8:4");
        runTest(new object[] {
            "\t", 1,
            "   ", 3
        }, 4, [0, 0], "Test8:3");
        runTest(new object[] {
            "\t", 1,
            "  ", 2
        }, 4, [0, 0], "Test8:2");
        runTest(new object[] {
            "\t", 1,
            " ", 1
        }, 4, [0, 0], "Test8:1");
        runTest(new object[] {
            "\t", 1,
            "", 0
        }, 4, [0, 0], "Test8:0");
    }

    [Fact]
    public void DetectsIndentation()
    {
        TestLineCommentCommand(
            [
                "\tsome text",
                "\tsome more text"
            ],
            new TextSelection(2, 2, 1, 1),
            [
                "\t!@# some text",
                "\t!@# some more text"
            ],
            new TextSelection(2, 2, 1, 1)
        );
    }


    [Fact]
    public void DetectsMixedIndentation()
    {
        TestLineCommentCommand(
            [
                "\tsome text",
                "    some more text"
            ],
            new TextSelection(2, 2, 1, 1),
            [
                "\t!@# some text",
                "    !@# some more text"
            ],
            new TextSelection(2, 2, 1, 1)
        );
    }


    [Fact]
    public void IgnoresWhitespaceLines()
    {
        TestLineCommentCommand(
            [
                "\tsome text",
                "\t   ",
                "",
                "\tsome more text"
            ],
            new TextSelection(4, 2, 1, 1),
            [
                "\t!@# some text",
                "\t   ",
                "",
                "\t!@# some more text"
            ],
            new TextSelection(4, 2, 1, 1)
        );
    }


    [Fact]
    public void RemovesItsOwn()
    {
        TestLineCommentCommand(
            [
                "\t!@# some text",
                "\t   ",
                "\t\t!@# some more text"
            ],
            new TextSelection(3, 2, 1, 1),
            [
                "\tsome text",
                "\t   ",
                "\t\tsome more text"
            ],
            new TextSelection(3, 2, 1, 1)
        );
    }


    [Fact]
    public void WorksInOnlyWhitespace()
    {
        TestLineCommentCommand(
            [
                "\t    ",
                "\t",
                "\t\tsome more text"
            ],
            new TextSelection(3, 1, 1, 1),
            [
                "\t!@#     ",
                "\t!@# ",
                "\t\tsome more text"
            ],
            new TextSelection(3, 1, 1, 1)
        );
    }


    [Fact]
    public void Bug9697WhitespaceBeforeCommentToken()
    {
        TestLineCommentCommand(
            [
                "\t !@#first",
                "\tsecond line"
            ],
            new TextSelection(1, 1, 1, 1),
            [
                "\t first",
                "\tsecond line"
            ],
            new TextSelection(1, 1, 1, 1)
        );
    }


    [Fact]
    public void Bug10162LineCommentBeforeCaret()
    {
        TestLineCommentCommand(
            [
                "first!@#",
                "\tsecond line"
            ],
            new TextSelection(1, 1, 1, 1),
            [
                "!@# first!@#",
                "\tsecond line"
            ],
            new TextSelection(1, 5, 1, 5)
        );
    }


    [Fact]
    public void CommentSingleLineLeadingWhitespace()
    {
        TestLineCommentCommand(
            [
                "first!@#",
                "\tsecond line"
            ],
            new TextSelection(2, 3, 2, 1),
            [
                "first!@#",
                "\t!@# second line"
            ],
            new TextSelection(2, 7, 2, 1)
        );
    }


    [Fact]
    public void IgnoresInvisibleSelection()
    {
        TestLineCommentCommand(
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 1, 1, 1),
            [
                "!@# first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 1, 1, 5)
        );
    }


    [Fact]
    public void MultipleLines()
    {
        TestLineCommentCommand(
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 4, 1, 1),
            [
                "!@# first",
                "!@# \tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 8, 1, 5)
        );
    }


    [Fact]
    public void MultipleModesOnMultipleLines()
    {
        TestLineCommentCommand(
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(4, 4, 3, 1),
            [
                "first",
                "\tsecond line",
                "!@# third line",
                "!@# fourth line",
                "fifth"
            ],
            new TextSelection(4, 8, 3, 5)
        );
    }


    [Fact]
    public void ToggleSingleLine()
    {
        TestLineCommentCommand(
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 1, 1, 1),
            [
                "!@# first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 5, 1, 5)
        );

        TestLineCommentCommand(
            [
                "!@# first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 4, 1, 4),
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 1, 1, 1)
        );
    }


    [Fact]
    public void ToggleMultipleLines()
    {
        TestLineCommentCommand(
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 4, 1, 1),
            [
                "!@# first",
                "!@# \tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 8, 1, 5)
        );

        TestLineCommentCommand(
            [
                "!@# first",
                "!@# \tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 7, 1, 4),
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 3, 1, 1)
        );
    }


    [Fact]
    public void Issue5964CtrlToCreateCommentWhenCursorIsAtTheBeginningOfTheLinePutsTheCursorInAStrangePosition()
    {
        TestLineCommentCommand(
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 1, 1, 1),
            [
                "!@# first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 5, 1, 5)
        );
    }


    [Fact]
    public void Issue35673CommentHotkeysThrowsTheCursorBeforeTheComment()
    {
        TestLineCommentCommand(
            [
                "first",
                "",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 1, 2, 1),
            [
                "first",
                "!@# ",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 5, 2, 5)
        );

        TestLineCommentCommand(
            [
                "first",
                "\t",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 2, 2, 2),
            [
                "first",
                "\t!@# ",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 6, 2, 6)
        );
    }


    [Fact]
    public void Issue2837AddLineCommentFaultWhenBlankLinesInvolved()
    {
        TestAddLineCommentCommand(
            [
                "    if displayName == \"\":",
                "        displayName = groupName",
                "    description = getAttr(attributes, \"description\")",
                "    mailAddress = getAttr(attributes, \"mail\")",
                "",
                "    print \"||Group name|%s|\" % displayName",
                "    print \"||Description|%s|\" % description",
                "    print \"||Email address|[mailto:%s]|\" % mailAddress`",
            ],
            new TextSelection(1, 1, 8, 56),
            [
                "    !@# if displayName == \"\":",
                "    !@#     displayName = groupName",
                "    !@# description = getAttr(attributes, \"description\")",
                "    !@# mailAddress = getAttr(attributes, \"mail\")",
                "",
                "    !@# print \"||Group name|%s|\" % displayName",
                "    !@# print \"||Description|%s|\" % description",
                "    !@# print \"||Email address|[mailto:%s]|\" % mailAddress`",
            ],
            new TextSelection(1, 1, 8, 60)
        );
    }


    [Fact]
    public void Issue47004ToggleCommentsShouldnTMoveCursor()
    {
        TestAddLineCommentCommand(
            [
                "    A line",
                "    Another line"
            ],
            new TextSelection(2, 7, 1, 1),
            [
                "    !@# A line",
                "    !@# Another line"
            ],
            new TextSelection(2, 11, 1, 1)
        );
    }


    [Fact]
    public void InsertSpaceFalse()
    {
        var TestLineCommentCommand = CreateTestCommandHelper(
            new CommentRule { LineComment = "!@#" },
            (service, sel) => new LineCommentCommand(service, sel, 4, LineCommentType.Toggle, false, true)
        );

        TestLineCommentCommand(
            [
                "some text"
            ],
            new TextSelection(1, 1, 1, 1),
            [
                "!@#some text"
            ],
            new TextSelection(1, 4, 1, 4)
        );
    }


    [Fact]
    public void InsertSpaceFalseDoesNotRemoveSpace()
    {
        var TestLineCommentCommand = CreateTestCommandHelper(
            new CommentRule { LineComment = "!@#" },
            (service, sel) => new LineCommentCommand(service, sel, 4, LineCommentType.Toggle, false, true)
        );

        TestLineCommentCommand(
            [
                "!@#    some text"
            ],
            new TextSelection(1, 1, 1, 1),
            [
                "    some text"
            ],
            new TextSelection(1, 1, 1, 1)
        );
    }
}
