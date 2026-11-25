// Ported from vscode (src/vs/editor/contrib/comment/test/browser/blockCommentCommand.test.ts, commit b57d5c9e), MIT
// license.

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Contrib.Comment;
using TKCode.Arc.Editor.Languages;
using Xunit;

namespace TKCode.Arc.Editor.Tests.Contrib;

public sealed class BlockCommentCommandTests
{
    // _testCommentCommand
    private static void TestCommentCommand(string[] lines, TextSelection selection, Func<LanguageConfigurationService, TextSelection, ICommand> commandFactory, string[] expectedLines, TextSelection expectedSelection)
    {
        const string languageId = "commentMode";
        TestCommand.Run(lines, languageId, selection, commandFactory, expectedLines, expectedSelection,
            service => service.Register(languageId, new LanguageConfiguration { Comments = new CommentRule { LineComment = "!@#", BlockComment = ("<0", "0>") } }));
    }

    private static void TestBlockCommentCommand(string[] lines, TextSelection selection, string[] expectedLines, TextSelection expectedSelection) =>
        TestCommentCommand(lines, selection, (service, sel) => new BlockCommentCommand(sel, true, service), expectedLines, expectedSelection);

    [Fact]
    public void EmptySelectionWrapsItself()
    {
        TestBlockCommentCommand(
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 3, 1, 3),
            [
                "fi<0  0>rst",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 6, 1, 6)
        );
    }


    [Fact]
    public void InvisibleSelectionIgnored()
    {
        TestBlockCommentCommand(
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 1, 1, 1),
            [
                "<0 first",
                " 0>\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 4, 2, 1)
        );
    }


    [Fact]
    public void Bug9511()
    {
        TestBlockCommentCommand(
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 6, 1, 1),
            [
                "<0 first 0>",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 4, 1, 9)
        );

        TestBlockCommentCommand(
            [
                "<0first0>",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 8, 1, 3),
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 1, 1, 6)
        );
    }


    [Fact]
    public void OneLineSelection()
    {
        TestBlockCommentCommand(
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 6, 1, 3),
            [
                "fi<0 rst 0>",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 6, 1, 9)
        );
    }


    [Fact]
    public void OneLineSelectionToggle()
    {
        TestBlockCommentCommand(
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 6, 1, 3),
            [
                "fi<0 rst 0>",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 6, 1, 9)
        );

        TestBlockCommentCommand(
            [
                "fi<0rst0>",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 8, 1, 5),
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 3, 1, 6)
        );

        TestBlockCommentCommand(
            [
                "<0 first 0>",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 10, 1, 1),
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 1, 1, 6)
        );

        TestBlockCommentCommand(
            [
                "<0 first0>",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 9, 1, 1),
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 1, 1, 6)
        );

        TestBlockCommentCommand(
            [
                "<0first 0>",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 9, 1, 1),
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 1, 1, 6)
        );

        TestBlockCommentCommand(
            [
                "fi<0rst0>",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 8, 1, 5),
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 3, 1, 6)
        );
    }


    [Fact]
    public void MultiLineSelection()
    {
        TestBlockCommentCommand(
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 4, 1, 1),
            [
                "<0 first",
                "\tse 0>cond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 4, 2, 4)
        );
    }


    [Fact]
    public void MultiLineSelectionToggle()
    {
        TestBlockCommentCommand(
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 4, 1, 1),
            [
                "<0 first",
                "\tse 0>cond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 4, 2, 4)
        );

        TestBlockCommentCommand(
            [
                "<0first",
                "\tse0>cond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 4, 1, 3),
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 1, 2, 4)
        );

        TestBlockCommentCommand(
            [
                "<0 first",
                "\tse0>cond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 4, 1, 3),
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 1, 2, 4)
        );

        TestBlockCommentCommand(
            [
                "<0first",
                "\tse 0>cond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 4, 1, 3),
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 1, 2, 4)
        );

        TestBlockCommentCommand(
            [
                "<0 first",
                "\tse 0>cond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 4, 1, 3),
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 1, 2, 4)
        );
    }


    [Fact]
    public void FuzzyRemoves()
    {
        TestBlockCommentCommand(
            [
                "asd <0 qwe",
                "asd 0> qwe"
            ],
            new TextSelection(2, 5, 1, 7),
            [
                "asd qwe",
                "asd qwe"
            ],
            new TextSelection(1, 5, 2, 4)
        );

        TestBlockCommentCommand(
            [
                "asd <0 qwe",
                "asd 0> qwe"
            ],
            new TextSelection(2, 5, 1, 6),
            [
                "asd qwe",
                "asd qwe"
            ],
            new TextSelection(1, 5, 2, 4)
        );

        TestBlockCommentCommand(
            [
                "asd <0 qwe",
                "asd 0> qwe"
            ],
            new TextSelection(2, 5, 1, 5),
            [
                "asd qwe",
                "asd qwe"
            ],
            new TextSelection(1, 5, 2, 4)
        );

        TestBlockCommentCommand(
            [
                "asd <0 qwe",
                "asd 0> qwe"
            ],
            new TextSelection(2, 5, 1, 11),
            [
                "asd qwe",
                "asd qwe"
            ],
            new TextSelection(1, 5, 2, 4)
        );

        TestBlockCommentCommand(
            [
                "asd <0 qwe",
                "asd 0> qwe"
            ],
            new TextSelection(2, 1, 1, 11),
            [
                "asd qwe",
                "asd qwe"
            ],
            new TextSelection(1, 5, 2, 4)
        );

        TestBlockCommentCommand(
            [
                "asd <0 qwe",
                "asd 0> qwe"
            ],
            new TextSelection(2, 7, 1, 11),
            [
                "asd qwe",
                "asd qwe"
            ],
            new TextSelection(1, 5, 2, 4)
        );
    }


    [Fact]
    public void Bug30358()
    {
        TestBlockCommentCommand(
            [
                "<0 start 0> middle end",
            ],
            new TextSelection(1, 20, 1, 23),
            [
                "<0 start 0> middle <0 end 0>"
            ],
            new TextSelection(1, 23, 1, 26)
        );

        TestBlockCommentCommand(
            [
                "<0 start 0> middle <0 end 0>"
            ],
            new TextSelection(1, 13, 1, 19),
            [
                "<0 start 0> <0 middle 0> <0 end 0>"
            ],
            new TextSelection(1, 16, 1, 22)
        );
    }


    [Fact]
    public void Issue34618()
    {
        TestBlockCommentCommand(
            [
                "<0  0> middle end",
            ],
            new TextSelection(1, 4, 1, 4),
            [
                " middle end"
            ],
            new TextSelection(1, 1, 1, 1)
        );
    }


    [Fact]
    public void InsertSpaceFalse()
    {
        void testLineCommentCommand(string[] lines, TextSelection selection, string[] expectedLines, TextSelection expectedSelection) =>
            TestCommentCommand(lines, selection, (service, sel) => new BlockCommentCommand(sel, false, service), expectedLines, expectedSelection);

        testLineCommentCommand(
            [
                "some text"
            ],
            new TextSelection(1, 1, 1, 5),
            [
                "<0some0> text"
            ],
            new TextSelection(1, 3, 1, 7)
        );
    }


    [Fact]
    public void InsertSpaceFalseDoesNotRemoveSpace()
    {
        void testLineCommentCommand(string[] lines, TextSelection selection, string[] expectedLines, TextSelection expectedSelection) =>
            TestCommentCommand(lines, selection, (service, sel) => new BlockCommentCommand(sel, false, service), expectedLines, expectedSelection);

        testLineCommentCommand(
            [
                "<0 some 0> text"
            ],
            new TextSelection(1, 4, 1, 8),
            [
                " some  text"
            ],
            new TextSelection(1, 1, 1, 7)
        );
    }
}
