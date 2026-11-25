// Ported from vscode (src/vs/editor/contrib/indentation/test/browser/indentation.test.ts, commit b57d5c9e), MIT
// license: the suites 'Change Indentation to Spaces' and 'Change Indentation to Tabs'.
// Differences from upstream: see IndentationTestBase.

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Contrib.Indentation;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Tokens;
using Xunit;
using static TKCode.Arc.Editor.Tests.Contrib.EditingAssert;
using static TKCode.Arc.Editor.Tests.Contrib.IndentationCommandTestHelpers;

namespace TKCode.Arc.Editor.Tests.Contrib;

/// <summary>The command helpers of indentation.test.ts.</summary>
internal static class IndentationCommandTestHelpers
{
    public static void TestIndentationToSpacesCommand(string[] lines, TextSelection selection, int tabSize, string[] expectedLines, TextSelection expectedSelection) =>
        TestCommand.Run(lines, null, selection, sel => new IndentationToSpacesCommand(sel, tabSize), expectedLines, expectedSelection);

    public static void TestIndentationToTabsCommand(string[] lines, TextSelection selection, int tabSize, string[] expectedLines, TextSelection expectedSelection) =>
        TestCommand.Run(lines, null, selection, sel => new IndentationToTabsCommand(sel, tabSize), expectedLines, expectedSelection);
}

/// <summary>Upstream's suite 'Change Indentation to Spaces - TypeScript/Javascript'.</summary>
public sealed class ChangeIndentationToSpacesTests
{
    [Fact]
    public void SingleTabsOnlyAtStartOfLine()
    {
        TestIndentationToSpacesCommand(
            [
                "first",
                "second line",
                "third line",
                "\tfourth line",
                "\tfifth"
            ],
            new TextSelection(2, 3, 2, 3),
            4,
            [
                "first",
                "second line",
                "third line",
                "    fourth line",
                "    fifth"
            ],
            new TextSelection(2, 3, 2, 3)
        );
    }


    [Fact]
    public void MultipleTabsAtStartOfLine()
    {
        TestIndentationToSpacesCommand(
            [
                "\t\tfirst",
                "\tsecond line",
                "\t\t\t third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 5, 1, 5),
            3,
            [
                "      first",
                "   second line",
                "          third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 9, 1, 9)
        );
    }


    [Fact]
    public void MultipleTabs()
    {
        TestIndentationToSpacesCommand(
            [
                "\t\tfirst\t",
                "\tsecond  \t line \t",
                "\t\t\t third line",
                " \tfourth line",
                "fifth"
            ],
            new TextSelection(1, 5, 1, 5),
            2,
            [
                "    first\t",
                "  second  \t line \t",
                "       third line",
                "   fourth line",
                "fifth"
            ],
            new TextSelection(1, 7, 1, 7)
        );
    }


    [Fact]
    public void EmptyLines()
    {
        TestIndentationToSpacesCommand(
            [
                "\t\t\t",
                "\t",
                "\t\t"
            ],
            new TextSelection(1, 4, 1, 4),
            2,
            [
                "      ",
                "  ",
                "    "
            ],
            new TextSelection(1, 4, 1, 4)
        );
    }
}

/// <summary>Upstream's suite 'Change Indentation to Tabs -  TypeScript/Javascript'.</summary>
public sealed class ChangeIndentationToTabsTests
{
    [Fact]
    public void SpacesOnlyAtStartOfLine()
    {
        TestIndentationToTabsCommand(
            [
                "    first",
                "second line",
                "    third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 3, 2, 3),
            4,
            [
                "\tfirst",
                "second line",
                "\tthird line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 3, 2, 3)
        );
    }


    [Fact]
    public void MultipleSpacesAtStartOfLine()
    {
        TestIndentationToTabsCommand(
            [
                "first",
                "   second line",
                "          third line",
                "fourth line",
                "     fifth"
            ],
            new TextSelection(1, 5, 1, 5),
            3,
            [
                "first",
                "\tsecond line",
                "\t\t\t third line",
                "fourth line",
                "\t  fifth"
            ],
            new TextSelection(1, 5, 1, 5)
        );
    }


    [Fact]
    public void MultipleSpaces()
    {
        TestIndentationToTabsCommand(
            [
                "      first   ",
                "  second     line \t",
                "       third line",
                "   fourth line",
                "fifth"
            ],
            new TextSelection(1, 8, 1, 8),
            2,
            [
                "\t\t\tfirst   ",
                "\tsecond     line \t",
                "\t\t\t third line",
                "\t fourth line",
                "fifth"
            ],
            new TextSelection(1, 5, 1, 5)
        );
    }


    [Fact]
    public void Issue45996()
    {
        TestIndentationToSpacesCommand(
            [
                "\tabc",
            ],
            new TextSelection(1, 3, 1, 3),
            4,
            [
                "    abc",
            ],
            new TextSelection(1, 6, 1, 6)
        );
    }
}
