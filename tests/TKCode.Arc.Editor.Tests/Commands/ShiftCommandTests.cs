// Ported from vscode (src/vs/editor/test/browser/commands/shiftCommand.test.ts, commit b57d5c9e), MIT license.
// Differences from upstream: testCommand runs the command through CommandExecutor (TestCommand.Run); the
// DocBlockCommentMode language is registered on the language configuration service only, as there is no language
// service; the edit operations are compared as SingleEditOperation records.

using Shouldly;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Languages;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class ShiftCommandTests
{
    private const string DocBlockCommentLanguageId = "commentMode";

    /// <summary>Create single edit operation</summary>
    private static SingleEditOperation CreateSingleEditOp(string text, int positionLineNumber, int positionColumn, int selectionLineNumber, int selectionColumn)
    {
        return new SingleEditOperation(new TextRange(selectionLineNumber, selectionColumn, positionLineNumber, positionColumn), text, false);
    }

    private static void PrepareDocBlockCommentLanguage(LanguageConfigurationService languageConfigurationService)
    {
        languageConfigurationService.Register(DocBlockCommentLanguageId, new LanguageConfiguration
        {
            Brackets =
            [
                ("(", ")"),
                ("{", "}"),
                ("[", "]"),
            ],
            OnEnterRules = OnEnterRules.JavascriptOnEnterRules,
        });
    }

    private static void TestShiftCommand(string[] lines, string? languageId, bool useTabStops, TextSelection selection, string[] expectedLines, TextSelection expectedSelection, Action<LanguageConfigurationService>? prepare = null)
    {
        TestCommand.Run(lines, languageId, selection, (service, sel) => new ShiftCommand(sel, new ShiftCommandOpts(
            IsUnshift: false,
            TabSize: 4,
            IndentSize: 4,
            InsertSpaces: false,
            UseTabStops: useTabStops,
            AutoIndent: EditorAutoIndentStrategy.Full), service), expectedLines, expectedSelection, prepare);
    }

    private static void TestUnshiftCommand(string[] lines, string? languageId, bool useTabStops, TextSelection selection, string[] expectedLines, TextSelection expectedSelection, Action<LanguageConfigurationService>? prepare = null)
    {
        TestCommand.Run(lines, languageId, selection, (service, sel) => new ShiftCommand(sel, new ShiftCommandOpts(
            IsUnshift: true,
            TabSize: 4,
            IndentSize: 4,
            InsertSpaces: false,
            UseTabStops: useTabStops,
            AutoIndent: EditorAutoIndentStrategy.Full), service), expectedLines, expectedSelection, prepare);
    }

    // --------- shift

    [Fact]
    public void Bug_9503_Shifting_Without_Any_Selection()
    {
        TestShiftCommand(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            null,
            true,
            new TextSelection(1, 1, 1, 1),
            [
                "\tMy First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            new TextSelection(1, 2, 1, 2)
        );
    }

    [Fact]
    public void Shift_On_Single_Line_Selection_1()
    {
        TestShiftCommand(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            null,
            true,
            new TextSelection(1, 3, 1, 1),
            [
                "\tMy First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            new TextSelection(1, 4, 1, 1)
        );
    }

    [Fact]
    public void Shift_On_Single_Line_Selection_2()
    {
        TestShiftCommand(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            null,
            true,
            new TextSelection(1, 1, 1, 3),
            [
                "\tMy First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            new TextSelection(1, 1, 1, 4)
        );
    }

    [Fact]
    public void Simple_Shift()
    {
        TestShiftCommand(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            null,
            true,
            new TextSelection(1, 1, 2, 1),
            [
                "\tMy First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            new TextSelection(1, 1, 2, 1)
        );
    }

    [Fact]
    public void Shifting_On_Two_Separate_Lines()
    {
        TestShiftCommand(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            null,
            true,
            new TextSelection(1, 1, 2, 1),
            [
                "\tMy First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            new TextSelection(1, 1, 2, 1)
        );

        TestShiftCommand(
            [
                "\tMy First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            null,
            true,
            new TextSelection(2, 1, 3, 1),
            [
                "\tMy First Line",
                "\t\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            new TextSelection(2, 1, 3, 1)
        );
    }

    [Fact]
    public void Shifting_On_Two_Lines()
    {
        TestShiftCommand(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            null,
            true,
            new TextSelection(1, 2, 2, 2),
            [
                "\tMy First Line",
                "\t\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            new TextSelection(1, 3, 2, 2)
        );
    }

    [Fact]
    public void Shifting_On_Two_Lines_Again()
    {
        TestShiftCommand(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            null,
            true,
            new TextSelection(2, 2, 1, 2),
            [
                "\tMy First Line",
                "\t\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            new TextSelection(2, 2, 1, 3)
        );
    }

    [Fact]
    public void Shifting_At_End_Of_File()
    {
        TestShiftCommand(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            null,
            true,
            new TextSelection(4, 1, 5, 2),
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "\t123"
            ],
            new TextSelection(4, 1, 5, 3)
        );
    }

    [Fact]
    public void Issue_1120_TAB_Should_Not_Indent_Empty_Lines_In_A_Multi_Line_Selection()
    {
        TestShiftCommand(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            null,
            true,
            new TextSelection(1, 1, 5, 2),
            [
                "\tMy First Line",
                "\t\t\tMy Second Line",
                "\t\tThird Line",
                "",
                "\t123"
            ],
            new TextSelection(1, 1, 5, 3)
        );

        TestShiftCommand(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            null,
            true,
            new TextSelection(4, 1, 5, 1),
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "\t",
                "123"
            ],
            new TextSelection(4, 1, 5, 1)
        );
    }

    // --------- unshift

    [Fact]
    public void Unshift_On_Single_Line_Selection_1()
    {
        TestShiftCommand(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            null,
            true,
            new TextSelection(2, 3, 2, 1),
            [
                "My First Line",
                "\t\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            new TextSelection(2, 3, 2, 1)
        );
    }

    [Fact]
    public void Unshift_On_Single_Line_Selection_2()
    {
        TestShiftCommand(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            null,
            true,
            new TextSelection(2, 1, 2, 3),
            [
                "My First Line",
                "\t\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            new TextSelection(2, 1, 2, 3)
        );
    }

    [Fact]
    public void Simple_Unshift()
    {
        TestUnshiftCommand(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            null,
            true,
            new TextSelection(1, 1, 2, 1),
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            new TextSelection(1, 1, 2, 1)
        );
    }

    [Fact]
    public void Unshifting_On_Two_Lines_1()
    {
        TestUnshiftCommand(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            null,
            true,
            new TextSelection(1, 2, 2, 2),
            [
                "My First Line",
                "\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            new TextSelection(1, 2, 2, 2)
        );
    }

    [Fact]
    public void Unshifting_On_Two_Lines_2()
    {
        TestUnshiftCommand(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            null,
            true,
            new TextSelection(2, 3, 2, 1),
            [
                "My First Line",
                "\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            new TextSelection(2, 2, 2, 1)
        );
    }

    [Fact]
    public void Unshifting_At_The_End_Of_The_File()
    {
        TestUnshiftCommand(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            null,
            true,
            new TextSelection(4, 1, 5, 2),
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            new TextSelection(4, 1, 5, 2)
        );
    }

    [Fact]
    public void Unshift_Many_Times_Shift()
    {
        TestUnshiftCommand(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            null,
            true,
            new TextSelection(1, 1, 5, 4),
            [
                "My First Line",
                "\tMy Second Line",
                "Third Line",
                "",
                "123"
            ],
            new TextSelection(1, 1, 5, 4)
        );

        TestUnshiftCommand(
            [
                "My First Line",
                "\tMy Second Line",
                "Third Line",
                "",
                "123"
            ],
            null,
            true,
            new TextSelection(1, 1, 5, 4),
            [
                "My First Line",
                "My Second Line",
                "Third Line",
                "",
                "123"
            ],
            new TextSelection(1, 1, 5, 4)
        );

        TestShiftCommand(
            [
                "My First Line",
                "My Second Line",
                "Third Line",
                "",
                "123"
            ],
            null,
            true,
            new TextSelection(1, 1, 5, 4),
            [
                "\tMy First Line",
                "\tMy Second Line",
                "\tThird Line",
                "",
                "\t123"
            ],
            new TextSelection(1, 1, 5, 5)
        );
    }

    [Fact]
    public void Bug_9119_Unshift_From_First_Column_Doesnt_Work()
    {
        TestUnshiftCommand(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            null,
            true,
            new TextSelection(2, 1, 2, 1),
            [
                "My First Line",
                "\tMy Second Line",
                "    Third Line",
                "",
                "123"
            ],
            new TextSelection(2, 1, 2, 1)
        );
    }

    [Fact]
    public void Issue_348_Indenting_Around_Doc_Block_Comments()
    {
        TestShiftCommand(
            [
                "",
                "/**",
                " * a doc comment",
                " */",
                "function hello() {}"
            ],
            DocBlockCommentLanguageId,
            true,
            new TextSelection(1, 1, 5, 20),
            [
                "",
                "\t/**",
                "\t * a doc comment",
                "\t */",
                "\tfunction hello() {}"
            ],
            new TextSelection(1, 1, 5, 21),
            PrepareDocBlockCommentLanguage
        );

        TestUnshiftCommand(
            [
                "",
                "/**",
                " * a doc comment",
                " */",
                "function hello() {}"
            ],
            DocBlockCommentLanguageId,
            true,
            new TextSelection(1, 1, 5, 20),
            [
                "",
                "/**",
                " * a doc comment",
                " */",
                "function hello() {}"
            ],
            new TextSelection(1, 1, 5, 20),
            PrepareDocBlockCommentLanguage
        );

        TestUnshiftCommand(
            [
                "\t",
                "\t/**",
                "\t * a doc comment",
                "\t */",
                "\tfunction hello() {}"
            ],
            DocBlockCommentLanguageId,
            true,
            new TextSelection(1, 1, 5, 21),
            [
                "",
                "/**",
                " * a doc comment",
                " */",
                "function hello() {}"
            ],
            new TextSelection(1, 1, 5, 20),
            PrepareDocBlockCommentLanguage
        );
    }

    [Fact]
    public void Issue_1609_Wrong_Indentation_Of_Block_Comments()
    {
        TestShiftCommand(
            [
                "",
                "/**",
                " * test",
                " *",
                " * @type {number}",
                " */",
                "var foo = 0;"
            ],
            DocBlockCommentLanguageId,
            true,
            new TextSelection(1, 1, 7, 13),
            [
                "",
                "\t/**",
                "\t * test",
                "\t *",
                "\t * @type {number}",
                "\t */",
                "\tvar foo = 0;"
            ],
            new TextSelection(1, 1, 7, 14),
            PrepareDocBlockCommentLanguage
        );
    }

    [Fact]
    public void Issue_1620_A_Line_Indent_Doesnt_Handle_Leading_Whitespace_Properly()
    {
        TestCommand.Run(
            [
                "   Written | Numeric",
                "       one | 1",
                "       two | 2",
                "     three | 3",
                "      four | 4",
                "      five | 5",
                "       six | 6",
                "     seven | 7",
                "     eight | 8",
                "      nine | 9",
                "       ten | 10",
                "    eleven | 11",
                "",
            ],
            null,
            new TextSelection(1, 1, 13, 1),
            (service, sel) => new ShiftCommand(sel, new ShiftCommandOpts(
                IsUnshift: false,
                TabSize: 4,
                IndentSize: 4,
                InsertSpaces: true,
                UseTabStops: false,
                AutoIndent: EditorAutoIndentStrategy.Full),
            service),
            [
                "       Written | Numeric",
                "           one | 1",
                "           two | 2",
                "         three | 3",
                "          four | 4",
                "          five | 5",
                "           six | 6",
                "         seven | 7",
                "         eight | 8",
                "          nine | 9",
                "           ten | 10",
                "        eleven | 11",
                "",
            ],
            new TextSelection(1, 1, 13, 1)
        );
    }

    [Fact]
    public void Issue_1620_B_Line_Indent_Doesnt_Handle_Leading_Whitespace_Properly()
    {
        TestCommand.Run(
            [
                "       Written | Numeric",
                "           one | 1",
                "           two | 2",
                "         three | 3",
                "          four | 4",
                "          five | 5",
                "           six | 6",
                "         seven | 7",
                "         eight | 8",
                "          nine | 9",
                "           ten | 10",
                "        eleven | 11",
                "",
            ],
            null,
            new TextSelection(1, 1, 13, 1),
            (service, sel) => new ShiftCommand(sel, new ShiftCommandOpts(
                IsUnshift: true,
                TabSize: 4,
                IndentSize: 4,
                InsertSpaces: true,
                UseTabStops: false,
                AutoIndent: EditorAutoIndentStrategy.Full),
            service),
            [
                "   Written | Numeric",
                "       one | 1",
                "       two | 2",
                "     three | 3",
                "      four | 4",
                "      five | 5",
                "       six | 6",
                "     seven | 7",
                "     eight | 8",
                "      nine | 9",
                "       ten | 10",
                "    eleven | 11",
                "",
            ],
            new TextSelection(1, 1, 13, 1)
        );
    }

    [Fact]
    public void Issue_1620_C_Line_Indent_Doesnt_Handle_Leading_Whitespace_Properly()
    {
        TestCommand.Run(
            [
                "       Written | Numeric",
                "           one | 1",
                "           two | 2",
                "         three | 3",
                "          four | 4",
                "          five | 5",
                "           six | 6",
                "         seven | 7",
                "         eight | 8",
                "          nine | 9",
                "           ten | 10",
                "        eleven | 11",
                "",
            ],
            null,
            new TextSelection(1, 1, 13, 1),
            (service, sel) => new ShiftCommand(sel, new ShiftCommandOpts(
                IsUnshift: true,
                TabSize: 4,
                IndentSize: 4,
                InsertSpaces: false,
                UseTabStops: false,
                AutoIndent: EditorAutoIndentStrategy.Full),
            service),
            [
                "   Written | Numeric",
                "       one | 1",
                "       two | 2",
                "     three | 3",
                "      four | 4",
                "      five | 5",
                "       six | 6",
                "     seven | 7",
                "     eight | 8",
                "      nine | 9",
                "       ten | 10",
                "    eleven | 11",
                "",
            ],
            new TextSelection(1, 1, 13, 1)
        );
    }

    [Fact]
    public void Issue_1620_D_Line_Indent_Doesnt_Handle_Leading_Whitespace_Properly()
    {
        TestCommand.Run(
            [
                "\t   Written | Numeric",
                "\t       one | 1",
                "\t       two | 2",
                "\t     three | 3",
                "\t      four | 4",
                "\t      five | 5",
                "\t       six | 6",
                "\t     seven | 7",
                "\t     eight | 8",
                "\t      nine | 9",
                "\t       ten | 10",
                "\t    eleven | 11",
                "",
            ],
            null,
            new TextSelection(1, 1, 13, 1),
            (service, sel) => new ShiftCommand(sel, new ShiftCommandOpts(
                IsUnshift: true,
                TabSize: 4,
                IndentSize: 4,
                InsertSpaces: true,
                UseTabStops: false,
                AutoIndent: EditorAutoIndentStrategy.Full),
            service),
            [
                "   Written | Numeric",
                "       one | 1",
                "       two | 2",
                "     three | 3",
                "      four | 4",
                "      five | 5",
                "       six | 6",
                "     seven | 7",
                "     eight | 8",
                "      nine | 9",
                "       ten | 10",
                "    eleven | 11",
                "",
            ],
            new TextSelection(1, 1, 13, 1)
        );
    }

    [Fact]
    public void Issue_Microsoft_Monaco_Editor_443_Indentation_Of_A_Single_Row_Deletes_Selected_Text_In_Some_Cases()
    {
        TestCommand.Run(
            [
                "Hello world!",
                "another line"
            ],
            null,
            new TextSelection(1, 1, 1, 13),
            (service, sel) => new ShiftCommand(sel, new ShiftCommandOpts(
                IsUnshift: false,
                TabSize: 4,
                IndentSize: 4,
                InsertSpaces: false,
                UseTabStops: true,
                AutoIndent: EditorAutoIndentStrategy.Full),
            service),
            [
                "\tHello world!",
                "another line"
            ],
            new TextSelection(1, 1, 1, 14)
        );
    }


    [Fact]
    public void Bug_16815_Shift_Tab_Doesnt_Go_Back_To_Tabstop()
    {
        static string RepeatStr(string str, int cnt)
        {
            var r = "";
            for (var i = 0; i < cnt; i++)
            {
                r += str;
            }
            return r;
        }

        void TestOutdent(int tabSize, int indentSize, bool insertSpaces, string lineText, int expectedIndents)
        {
            var oneIndent = insertSpaces ? RepeatStr(" ", indentSize) : "\t";
            var expectedIndent = RepeatStr(oneIndent, expectedIndents);
            if (lineText.Length > 0)
            {
                AssertUnshiftCommand(tabSize, indentSize, insertSpaces, [lineText + "aaa"], [CreateSingleEditOp(expectedIndent, 1, 1, 1, lineText.Length + 1)]);
            }
            else
            {
                AssertUnshiftCommand(tabSize, indentSize, insertSpaces, [lineText + "aaa"], []);
            }
        }

        void TestIndent(int tabSize, int indentSize, bool insertSpaces, string lineText, int expectedIndents)
        {
            var oneIndent = insertSpaces ? RepeatStr(" ", indentSize) : "\t";
            var expectedIndent = RepeatStr(oneIndent, expectedIndents);
            AssertShiftCommand(tabSize, indentSize, insertSpaces, [lineText + "aaa"], [CreateSingleEditOp(expectedIndent, 1, 1, 1, lineText.Length + 1)]);
        }

        void TestIndentation(int tabSize, int indentSize, string lineText, int expectedOnOutdent, int expectedOnIndent)
        {
            TestOutdent(tabSize, indentSize, true, lineText, expectedOnOutdent);
            TestOutdent(tabSize, indentSize, false, lineText, expectedOnOutdent);

            TestIndent(tabSize, indentSize, true, lineText, expectedOnIndent);
            TestIndent(tabSize, indentSize, false, lineText, expectedOnIndent);
        }

        // insertSpaces: true
        // 0 => 0
        TestIndentation(4, 4, "", 0, 1);

        // 1 => 0
        TestIndentation(4, 4, "\t", 0, 2);
        TestIndentation(4, 4, " ", 0, 1);
        TestIndentation(4, 4, " \t", 0, 2);
        TestIndentation(4, 4, "  ", 0, 1);
        TestIndentation(4, 4, "  \t", 0, 2);
        TestIndentation(4, 4, "   ", 0, 1);
        TestIndentation(4, 4, "   \t", 0, 2);
        TestIndentation(4, 4, "    ", 0, 2);

        // 2 => 1
        TestIndentation(4, 4, "\t\t", 1, 3);
        TestIndentation(4, 4, "\t ", 1, 2);
        TestIndentation(4, 4, "\t \t", 1, 3);
        TestIndentation(4, 4, "\t  ", 1, 2);
        TestIndentation(4, 4, "\t  \t", 1, 3);
        TestIndentation(4, 4, "\t   ", 1, 2);
        TestIndentation(4, 4, "\t   \t", 1, 3);
        TestIndentation(4, 4, "\t    ", 1, 3);
        TestIndentation(4, 4, " \t\t", 1, 3);
        TestIndentation(4, 4, " \t ", 1, 2);
        TestIndentation(4, 4, " \t \t", 1, 3);
        TestIndentation(4, 4, " \t  ", 1, 2);
        TestIndentation(4, 4, " \t  \t", 1, 3);
        TestIndentation(4, 4, " \t   ", 1, 2);
        TestIndentation(4, 4, " \t   \t", 1, 3);
        TestIndentation(4, 4, " \t    ", 1, 3);
        TestIndentation(4, 4, "  \t\t", 1, 3);
        TestIndentation(4, 4, "  \t ", 1, 2);
        TestIndentation(4, 4, "  \t \t", 1, 3);
        TestIndentation(4, 4, "  \t  ", 1, 2);
        TestIndentation(4, 4, "  \t  \t", 1, 3);
        TestIndentation(4, 4, "  \t   ", 1, 2);
        TestIndentation(4, 4, "  \t   \t", 1, 3);
        TestIndentation(4, 4, "  \t    ", 1, 3);
        TestIndentation(4, 4, "   \t\t", 1, 3);
        TestIndentation(4, 4, "   \t ", 1, 2);
        TestIndentation(4, 4, "   \t \t", 1, 3);
        TestIndentation(4, 4, "   \t  ", 1, 2);
        TestIndentation(4, 4, "   \t  \t", 1, 3);
        TestIndentation(4, 4, "   \t   ", 1, 2);
        TestIndentation(4, 4, "   \t   \t", 1, 3);
        TestIndentation(4, 4, "   \t    ", 1, 3);
        TestIndentation(4, 4, "    \t", 1, 3);
        TestIndentation(4, 4, "     ", 1, 2);
        TestIndentation(4, 4, "     \t", 1, 3);
        TestIndentation(4, 4, "      ", 1, 2);
        TestIndentation(4, 4, "      \t", 1, 3);
        TestIndentation(4, 4, "       ", 1, 2);
        TestIndentation(4, 4, "       \t", 1, 3);
        TestIndentation(4, 4, "        ", 1, 3);

        // 3 => 2
        TestIndentation(4, 4, "         ", 2, 3);

        static void AssertUnshiftCommand(int tabSize, int indentSize, bool insertSpaces, string[] text, SingleEditOperation[] expected)
        {
            TestTextModel.WithEditorModel(text, model =>
            {
                using var testLanguageConfigurationService = new LanguageConfigurationService();
                var op = new ShiftCommand(new TextSelection(1, 1, text.Length + 1, 1), new ShiftCommandOpts(
                    IsUnshift: true,
                    TabSize: tabSize,
                    IndentSize: indentSize,
                    InsertSpaces: insertSpaces,
                    UseTabStops: true,
                    AutoIndent: EditorAutoIndentStrategy.Full), testLanguageConfigurationService);
                var actual = TestCommand.GetEditOperation(model, op);
                actual.ShouldBe(expected);
            });
        }

        static void AssertShiftCommand(int tabSize, int indentSize, bool insertSpaces, string[] text, SingleEditOperation[] expected)
        {
            TestTextModel.WithEditorModel(text, model =>
            {
                using var testLanguageConfigurationService = new LanguageConfigurationService();
                var op = new ShiftCommand(new TextSelection(1, 1, text.Length + 1, 1), new ShiftCommandOpts(
                    IsUnshift: false,
                    TabSize: tabSize,
                    IndentSize: indentSize,
                    InsertSpaces: insertSpaces,
                    UseTabStops: true,
                    AutoIndent: EditorAutoIndentStrategy.Full), testLanguageConfigurationService);
                var actual = TestCommand.GetEditOperation(model, op);
                actual.ShouldBe(expected);
            });
        }
    }
}
