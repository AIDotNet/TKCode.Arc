// Ported from vscode (src/vs/editor/contrib/comment/test/browser/lineCommentCommand.test.ts, commit b57d5c9e), MIT
// license: the suites after 'Editor Contrib - Line Comment Command', one class each.

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Contrib.Comment;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;
using Shouldly;
using Xunit;
using static TKCode.Arc.Editor.Tests.Contrib.LineCommentTestHelpers;

namespace TKCode.Arc.Editor.Tests.Contrib;

/// <summary>Upstream's suite 'ignoreEmptyLines false'.</summary>
public sealed class LineCommentIgnoreEmptyLinesFalseTests
{
    private static readonly TestHelper TestLineCommentCommand = CreateTestCommandHelper(
        new CommentRule { LineComment = "!@#", BlockComment = ("<!@#", "#@!>") },
        (service, sel) => new LineCommentCommand(service, sel, 4, LineCommentType.Toggle, true, false)
    );

    [Fact]
    public void DoesNotIgnoreWhitespaceLines()
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
                "!@# \tsome text",
                "!@# \t   ",
                "!@# ",
                "!@# \tsome more text"
            ],
            new TextSelection(4, 6, 1, 5)
        );
    }


    [Fact]
    public void RemovesItsOwn2()
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
    public void WorksInOnlyWhitespace2()
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
    public void CommentsSingleLine()
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
    public void DetectsIndentation2()
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
}

/// <summary>Upstream's suite 'Editor Contrib - Line Comment As Block Comment'.</summary>
public sealed class LineCommentAsBlockCommentTests
{
    private static readonly TestHelper TestLineCommentCommand = CreateTestCommandHelper(
        new CommentRule { LineComment = "", BlockComment = ("(", ")") },
        (service, sel) => new LineCommentCommand(service, sel, 4, LineCommentType.Toggle, true, true)
    );

    [Fact]
    public void FallBackToBlockCommentCommand()
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
                "( first )",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 3, 1, 3)
        );
    }


    [Fact]
    public void FallBackToBlockCommentCommandToggle()
    {
        TestLineCommentCommand(
            [
                "(first)",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 7, 1, 2),
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 6, 1, 1)
        );
    }


    [Fact]
    public void Bug9513ExpandSingleLineToUncommentAutoBlock()
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
                "( first )",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 3, 1, 3)
        );
    }


    [Fact]
    public void Bug9691AlwaysExpandSelectionToLineBoundaries()
    {
        TestLineCommentCommand(
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(3, 2, 1, 3),
            [
                "( first",
                "\tsecond line",
                "third line )",
                "fourth line",
                "fifth"
            ],
            new TextSelection(3, 2, 1, 5)
        );

        TestLineCommentCommand(
            [
                "(first",
                "\tsecond line",
                "third line)",
                "fourth line",
                "fifth"
            ],
            new TextSelection(3, 11, 1, 2),
            [
                "first",
                "\tsecond line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(3, 11, 1, 1)
        );
    }
}

/// <summary>Upstream's suite 'Editor Contrib - Line Comment As Block Comment 2'.</summary>
public sealed class LineCommentAsBlockComment2Tests
{
    private static readonly TestHelper TestLineCommentCommand = CreateTestCommandHelper(
        new CommentRule { LineComment = null, BlockComment = ("<!@#", "#@!>") },
        (service, sel) => new LineCommentCommand(service, sel, 4, LineCommentType.Toggle, true, true)
    );

    [Fact]
    public void NoSelectionUsesIndentation()
    {
        TestLineCommentCommand(
            [
                "\t\tfirst\t    ",
                "\t\tsecond line",
                "\tthird line",
                "fourth line",
                "\t\t<!@#fifth#@!>\t\t"
            ],
            new TextSelection(1, 1, 1, 1),
            [
                "\t\t<!@# first\t     #@!>",
                "\t\tsecond line",
                "\tthird line",
                "fourth line",
                "\t\t<!@#fifth#@!>\t\t"
            ],
            new TextSelection(1, 1, 1, 1)
        );

        TestLineCommentCommand(
            [
                "\t\t<!@#first\t    #@!>",
                "\t\tsecond line",
                "\tthird line",
                "fourth line",
                "\t\t<!@#fifth#@!>\t\t"
            ],
            new TextSelection(1, 1, 1, 1),
            [
                "\t\tfirst\t   ",
                "\t\tsecond line",
                "\tthird line",
                "fourth line",
                "\t\t<!@#fifth#@!>\t\t"
            ],
            new TextSelection(1, 1, 1, 1)
        );
    }


    [Fact]
    public void CanRemove()
    {
        TestLineCommentCommand(
            [
                "\t\tfirst\t    ",
                "\t\tsecond line",
                "\tthird line",
                "fourth line",
                "\t\t<!@#fifth#@!>\t\t"
            ],
            new TextSelection(5, 1, 5, 1),
            [
                "\t\tfirst\t    ",
                "\t\tsecond line",
                "\tthird line",
                "fourth line",
                "\t\tfifth\t\t"
            ],
            new TextSelection(5, 1, 5, 1)
        );

        TestLineCommentCommand(
            [
                "\t\tfirst\t    ",
                "\t\tsecond line",
                "\tthird line",
                "fourth line",
                "\t\t<!@#fifth#@!>\t\t"
            ],
            new TextSelection(5, 3, 5, 3),
            [
                "\t\tfirst\t    ",
                "\t\tsecond line",
                "\tthird line",
                "fourth line",
                "\t\tfifth\t\t"
            ],
            new TextSelection(5, 3, 5, 3)
        );

        TestLineCommentCommand(
            [
                "\t\tfirst\t    ",
                "\t\tsecond line",
                "\tthird line",
                "fourth line",
                "\t\t<!@#fifth#@!>\t\t"
            ],
            new TextSelection(5, 4, 5, 4),
            [
                "\t\tfirst\t    ",
                "\t\tsecond line",
                "\tthird line",
                "fourth line",
                "\t\tfifth\t\t"
            ],
            new TextSelection(5, 3, 5, 3)
        );

        TestLineCommentCommand(
            [
                "\t\tfirst\t    ",
                "\t\tsecond line",
                "\tthird line",
                "fourth line",
                "\t\t<!@#fifth#@!>\t\t"
            ],
            new TextSelection(5, 16, 5, 3),
            [
                "\t\tfirst\t    ",
                "\t\tsecond line",
                "\tthird line",
                "fourth line",
                "\t\tfifth\t\t"
            ],
            new TextSelection(5, 8, 5, 3)
        );

        TestLineCommentCommand(
            [
                "\t\tfirst\t    ",
                "\t\tsecond line",
                "\tthird line",
                "fourth line",
                "\t\t<!@#fifth#@!>\t\t"
            ],
            new TextSelection(5, 12, 5, 7),
            [
                "\t\tfirst\t    ",
                "\t\tsecond line",
                "\tthird line",
                "fourth line",
                "\t\tfifth\t\t"
            ],
            new TextSelection(5, 8, 5, 3)
        );

        TestLineCommentCommand(
            [
                "\t\tfirst\t    ",
                "\t\tsecond line",
                "\tthird line",
                "fourth line",
                "\t\t<!@#fifth#@!>\t\t"
            ],
            new TextSelection(5, 18, 5, 18),
            [
                "\t\tfirst\t    ",
                "\t\tsecond line",
                "\tthird line",
                "fourth line",
                "\t\tfifth\t\t"
            ],
            new TextSelection(5, 10, 5, 10)
        );
    }


    [Fact]
    public void Issue993RemoveCommentDoesNotWorkConsistentlyInHTML()
    {
        TestLineCommentCommand(
            [
                "     asd qwe",
                "     asd qwe",
                ""
            ],
            new TextSelection(1, 1, 3, 1),
            [
                "     <!@# asd qwe",
                "     asd qwe #@!>",
                ""
            ],
            new TextSelection(1, 1, 3, 1)
        );

        TestLineCommentCommand(
            [
                "     <!@#asd qwe",
                "     asd qwe#@!>",
                ""
            ],
            new TextSelection(1, 1, 3, 1),
            [
                "     asd qwe",
                "     asd qwe",
                ""
            ],
            new TextSelection(1, 1, 3, 1)
        );
    }
}

/// <summary>Upstream's suite 'Editor Contrib - Line Comment in mixed modes'.</summary>
public sealed class LineCommentInMixedModesTests
{
    private const string OUTER_LANGUAGE_ID = "outerMode";
    private const string INNER_LANGUAGE_ID = "innerMode";

    // OuterMode and InnerMode, and testCommand with them: lines starting with two spaces are in the inner language.
    private static void TestLineCommentCommand(string[] lines, TextSelection selection, string[] expectedLines, TextSelection expectedSelection)
    {
        using var languageConfigurationService = new LanguageConfigurationService();
        languageConfigurationService.Register(OUTER_LANGUAGE_ID, new LanguageConfiguration { Comments = new CommentRule { LineComment = "//", BlockComment = ("/*", "*/") } });
        languageConfigurationService.Register(INNER_LANGUAGE_ID, new LanguageConfiguration { Comments = new CommentRule { LineComment = null, BlockComment = ("{/*", "*/}") } });

        var codec = new LanguageIdCodec();
        codec.Register(OUTER_LANGUAGE_ID);
        codec.Register(INNER_LANGUAGE_ID);
        using var model = new TextModel(string.Join('\n', lines), OUTER_LANGUAGE_ID, TextModel.DefaultCreationOptions, TestTextModel.CreateUndoRedoService(), null, codec);
        using var tokenization = TestTokenizationSupports.RegisterByLine(model, (_, line) =>
        {
            var languageId = line.StartsWith("  ", StringComparison.Ordinal) ? INNER_LANGUAGE_ID : OUTER_LANGUAGE_ID;
            var encodedLanguageId = (uint)codec.EncodeLanguageId(languageId);
            return [0, ((uint)ColorId.DefaultForeground << MetadataConsts.ForegroundOffset) | (encodedLanguageId << MetadataConsts.LanguageIdOffset)];
        });
        model.Tokenization.ForceTokenization(model.GetLineCount());

        var command = new LineCommentCommand(languageConfigurationService, selection, 4, LineCommentType.Toggle, true, true);
        var selections = CommandExecutor.ExecuteCommands(model, [selection], [command]) ?? [selection];

        model.GetLinesContent().ShouldBe(expectedLines);
        selections[0].ToString().ShouldBe(expectedSelection.ToString());
    }

    [Fact]
    public void Issue24047Part1CommentingCodeInJSXFiles()
    {
        TestLineCommentCommand(
            [
                "import React from 'react';",
                "const Loader = () => (",
                "  <div>",
                "    Loading...",
                "  </div>",
                ");",
                "export default Loader;"
            ],
            new TextSelection(1, 1, 7, 22),
            [
                "// import React from 'react';",
                "// const Loader = () => (",
                "//   <div>",
                "//     Loading...",
                "//   </div>",
                "// );",
                "// export default Loader;"
            ],
            new TextSelection(1, 4, 7, 25)
        );
    }


    [Fact]
    public void Issue24047Part2CommentingCodeInJSXFiles()
    {
        TestLineCommentCommand(
            [
                "import React from 'react';",
                "const Loader = () => (",
                "  <div>",
                "    Loading...",
                "  </div>",
                ");",
                "export default Loader;"
            ],
            new TextSelection(3, 4, 3, 4),
            [
                "import React from 'react';",
                "const Loader = () => (",
                "  {/* <div> */}",
                "    Loading...",
                "  </div>",
                ");",
                "export default Loader;"
            ],
            new TextSelection(3, 8, 3, 8)
        );
    }


    [Fact]
    public void Issue36173CommentingCodeInJSXTagBody()
    {
        TestLineCommentCommand(
            [
                "<div>",
                "  {123}",
                "</div>",
            ],
            new TextSelection(2, 4, 2, 4),
            [
                "<div>",
                "  {/* {123} */}",
                "</div>",
            ],
            new TextSelection(2, 8, 2, 8)
        );
    }
}
