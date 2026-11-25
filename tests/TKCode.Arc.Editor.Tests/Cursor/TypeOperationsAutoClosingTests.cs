// Tests for auto-closing and surrounding on type, ported from vscode
// (src/vs/editor/test/browser/controller/cursor.test.ts, suite 'Editor Controller', commit b57d5c9e), MIT license:
// the tests from 'issue #61070' to 'issue #6841'. The rest of the auto-closing tests are in
// TypeOperationsAutoClosingOvertypeTests. Upstream types through the view model of a test code editor; here the
// typing goes through TypeOperations on OperationsTestEditor, which tracks the auto-closed characters as the cursors
// controller does.
//
// Adapted: model.bracketPairs.getBracketsInRange(...) (which only makes upstream build its bracket pair tree) and
// model.tokenization.forceTokenization have nothing to do here and are left out.

using Shouldly;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;
using static TKCode.Arc.Editor.Tests.TypingTestContext;

namespace TKCode.Arc.Editor.Tests;

public sealed class TypeOperationsAutoClosingTests : IDisposable
{
    private const string Keyboard = OperationsTestEditor.Keyboard;

    private static readonly string[] s_autoClosingText =
    [
        "var a = [];",
        "var b = `asd`;",
        "var c = 'asd';",
        "var d = \"asd\";",
        "var e = /*3*/\t3;",
        "var f = /** 3 */3;",
        "var g = (3+5);",
        "var h = { a: 'value' };",
    ];

    private readonly TypingTestContext _context = new();

    public void Dispose() => _context.Dispose();

    private static TextSelection S(int selectionStartLineNumber, int selectionStartColumn, int positionLineNumber, int positionColumn) =>
        new(selectionStartLineNumber, selectionStartColumn, positionLineNumber, positionColumn);

    /// <summary>Types <paramref name="chr"/> at every column and checks whether it auto-closes where the annotation says.</summary>
    private static async Task AssertAutoClosePositions(
        OperationsTestEditor editor,
        string[] autoClosePositions,
        string chr,
        string closed,
        string? alsoNotClosing = null)
    {
        var model = editor.Model;
        for (int i = 0, len = autoClosePositions.Length; i < len; i++)
        {
            var lineNumber = i + 1;
            var autoCloseColumns = ExtractAutoClosingSpecialColumns(model.GetLineMaxColumn(lineNumber), autoClosePositions[i]);

            for (var column = 1; column < autoCloseColumns.Length; column++)
            {
                if (autoCloseColumns[column] == AutoClosingColumnType.Special1)
                {
                    await AssertType(editor, lineNumber, column, chr, closed, $"auto closes @ ({lineNumber}, {column})");
                }
                else
                {
                    await AssertType(editor, lineNumber, column, chr, chr, $"does not auto close @ ({lineNumber}, {column})");
                }
                if (alsoNotClosing is not null)
                {
                    await AssertType(editor, lineNumber, column, alsoNotClosing, alsoNotClosing, $"does not auto close @ ({lineNumber}, {column})");
                }
            }
        }
    }

    [Fact]
    public async Task Issue_61070_Backtick_Should_Auto_Close_After_A_Word_Character()
    {
        using var editor = _context.UsingCursor(["const markup = highlight"], AutoClosingLanguageId);
        await AssertType(editor, 1, 25, "`", "``", "auto closes ` @ (1, 25)");
    }

    [Fact]
    public async Task Issue_132912_Quotes_Should_Not_Auto_Close_If_They_Are_Closing_A_String()
    {
        _context.SetupAutoClosingLanguageTokenization();
        using var editor = _context.CreateEditor("const t2 = `something ${t1}", AutoClosingLanguageId);
        await AssertType(editor, 1, 28, "`", "`", "does not auto close ` @ (1, 28)");
    }

    [Fact]
    public async Task AutoClosingPairs_Open_Parens_Default()
    {
        using var editor = _context.UsingCursor(s_autoClosingText, AutoClosingLanguageId);

        string[] autoClosePositions =
        [
            "var| a| |=| [|]|;|",
            "var| b| |=| |`asd|`|;|",
            "var| c| |=| |'asd|'|;|",
            "var| d| |=| |\"asd|\"|;|",
            "var| e| |=| /*3*/|\t3|;|",
            "var| f| |=| /**| 3| */3|;|",
            "var| g| |=| (3+5|)|;|",
            "var| h| |=| {| a|:| |'value|'| |}|;|",
        ];
        await AssertAutoClosePositions(editor, autoClosePositions, "(", "()");
    }

    [Fact]
    public async Task AutoClosingPairs_Open_Parens_Whitespace()
    {
        using var editor = _context.UsingCursor(
            s_autoClosingText,
            AutoClosingLanguageId,
            editorOpts: EditorOptions.Default with { AutoClosingBrackets = EditorAutoClosingStrategy.BeforeWhitespace });

        string[] autoClosePositions =
        [
            "var| a| =| [|];|",
            "var| b| =| `asd`;|",
            "var| c| =| 'asd';|",
            "var| d| =| \"asd\";|",
            "var| e| =| /*3*/|\t3;|",
            "var| f| =| /**| 3| */3;|",
            "var| g| =| (3+5|);|",
            "var| h| =| {| a:| 'value'| |};|",
        ];
        await AssertAutoClosePositions(editor, autoClosePositions, "(", "()");
    }

    [Fact]
    public async Task AutoClosingPairs_Open_Parens_Disabled_Enabled_Open_Quotes_Enabled_Disabled()
    {
        using (var editor = _context.UsingCursor(
            ["var a = [];"],
            AutoClosingLanguageId,
            editorOpts: EditorOptions.Default with
            {
                AutoClosingBrackets = EditorAutoClosingStrategy.BeforeWhitespace,
                AutoClosingQuotes = EditorAutoClosingStrategy.Never,
            }))
        {
            await AssertAutoClosePositions(editor, ["var| a| =| [|];|"], "(", "()", alsoNotClosing: "'");
        }

        using (var editor = _context.UsingCursor(
            ["var b = [];"],
            AutoClosingLanguageId,
            editorOpts: EditorOptions.Default with
            {
                AutoClosingBrackets = EditorAutoClosingStrategy.Never,
                AutoClosingQuotes = EditorAutoClosingStrategy.BeforeWhitespace,
            }))
        {
            await AssertAutoClosePositions(editor, ["var b =| [|];|"], "'", "''", alsoNotClosing: "(");
        }
    }

    [Fact]
    public async Task AutoClosingPairs_Configurable_Open_Parens()
    {
        _context.SetAutoClosingLanguageEnabledSet("abc");
        using var editor = _context.UsingCursor(
            s_autoClosingText,
            AutoClosingLanguageId,
            editorOpts: EditorOptions.Default with { AutoClosingBrackets = EditorAutoClosingStrategy.LanguageDefined });

        string[] autoClosePositions =
        [
            "v|ar |a = [|];|",
            "v|ar |b = `|asd`;|",
            "v|ar |c = '|asd';|",
            "v|ar d = \"|asd\";|",
            "v|ar e = /*3*/\t3;|",
            "v|ar f = /** 3| */3;|",
            "v|ar g = (3+5|);|",
            "v|ar h = { |a: 'v|alue' |};|",
        ];
        await AssertAutoClosePositions(editor, autoClosePositions, "(", "()");
    }

    [Fact]
    public async Task AutoClosingPairs_Auto_Pairing_Can_Be_Disabled()
    {
        using var editor = _context.UsingCursor(
            s_autoClosingText,
            AutoClosingLanguageId,
            editorOpts: EditorOptions.Default with
            {
                AutoClosingBrackets = EditorAutoClosingStrategy.Never,
                AutoClosingQuotes = EditorAutoClosingStrategy.Never,
            });

        // no column is annotated: nothing auto closes
        var model = editor.Model;
        for (int i = 0, len = s_autoClosingText.Length; i < len; i++)
        {
            var lineNumber = i + 1;
            var autoCloseColumns = ExtractAutoClosingSpecialColumns(model.GetLineMaxColumn(lineNumber), s_autoClosingText[i]);

            for (var column = 1; column < autoCloseColumns.Length; column++)
            {
                if (autoCloseColumns[column] == AutoClosingColumnType.Special1)
                {
                    await AssertType(editor, lineNumber, column, "(", "()", $"auto closes @ ({lineNumber}, {column})");
                    await AssertType(editor, lineNumber, column, "\"", "\"\"", $"auto closes @ ({lineNumber}, {column})");
                }
                else
                {
                    await AssertType(editor, lineNumber, column, "(", "(", $"does not auto close @ ({lineNumber}, {column})");
                    await AssertType(editor, lineNumber, column, "\"", "\"", $"does not auto close @ ({lineNumber}, {column})");
                }
            }
        }
    }

    [Fact]
    public void AutoClosingPairs_Auto_Wrapping_Is_Configurable()
    {
        using (var editor = _context.UsingCursor(["var a = asd"], AutoClosingLanguageId))
        {
            editor.SetSelectionsValidated(S(1, 1, 1, 4), S(1, 9, 1, 12));

            // type a `
            editor.Type("`", Keyboard);

            editor.Model.GetValue().ShouldBe("`var` a = `asd`");

            // type a (
            editor.Type("(", Keyboard);

            editor.Model.GetValue().ShouldBe("`(var)` a = `(asd)`");
        }

        using (var editor = _context.UsingCursor(["var a = asd"], AutoClosingLanguageId, editorOpts: EditorOptions.Default with { AutoSurround = EditorAutoSurroundStrategy.Never }))
        {
            editor.SetSelectionsValidated(S(1, 1, 1, 4));

            // type a `
            editor.Type("`", Keyboard);

            editor.Model.GetValue().ShouldBe("` a = asd");
        }

        using (var editor = _context.UsingCursor(["var a = asd"], AutoClosingLanguageId, editorOpts: EditorOptions.Default with { AutoSurround = EditorAutoSurroundStrategy.Quotes }))
        {
            editor.SetSelectionsValidated(S(1, 1, 1, 4));

            // type a `
            editor.Type("`", Keyboard);
            editor.Model.GetValue().ShouldBe("`var` a = asd");

            // type a (
            editor.Type("(", Keyboard);
            editor.Model.GetValue().ShouldBe("`(` a = asd");
        }

        using (var editor = _context.UsingCursor(["var a = asd"], AutoClosingLanguageId, editorOpts: EditorOptions.Default with { AutoSurround = EditorAutoSurroundStrategy.Brackets }))
        {
            editor.SetSelectionsValidated(S(1, 1, 1, 4));

            // type a (
            editor.Type("(", Keyboard);
            editor.Model.GetValue().ShouldBe("(var) a = asd");

            // type a `
            editor.Type("`", Keyboard);
            editor.Model.GetValue().ShouldBe("(`) a = asd");
        }
    }

    [Fact]
    public async Task AutoClosingPairs_Quote()
    {
        using var editor = _context.UsingCursor(s_autoClosingText, AutoClosingLanguageId);
        var model = editor.Model;

        string[] autoClosePositions =
        [
            "var a |=| [|]|;|",
            "var b |=| `asd`|;|",
            "var c |=| 'asd'|;|",
            "var d |=| \"asd\"|;|",
            "var e |=| /*3*/|\t3;|",
            "var f |=| /**| 3 */3;|",
            "var g |=| (3+5)|;|",
            "var h |=| {| a:| 'value'| |}|;|",
        ];
        for (int i = 0, len = autoClosePositions.Length; i < len; i++)
        {
            var lineNumber = i + 1;
            var autoCloseColumns = ExtractAutoClosingSpecialColumns(model.GetLineMaxColumn(lineNumber), autoClosePositions[i]);

            for (var column = 1; column < autoCloseColumns.Length; column++)
            {
                if (autoCloseColumns[column] == AutoClosingColumnType.Special1)
                {
                    await AssertType(editor, lineNumber, column, "'", "''", $"auto closes @ ({lineNumber}, {column})");
                }
                else if (autoCloseColumns[column] == AutoClosingColumnType.Special2)
                {
                    await AssertType(editor, lineNumber, column, "'", "", $"over types @ ({lineNumber}, {column})");
                }
                else
                {
                    await AssertType(editor, lineNumber, column, "'", "'", $"does not auto close @ ({lineNumber}, {column})");
                }
            }
        }
    }

    [Fact]
    public void AutoClosingPairs_Multi_Character_Autoclose()
    {
        using var editor = _context.UsingCursor([""], AutoClosingLanguageId);
        var model = editor.Model;

        model.SetValue("begi");
        editor.SetSelectionsValidated(S(1, 5, 1, 5));
        editor.Type("n", Keyboard);
        model.GetLineContent(1).ShouldBe("beginend");

        model.SetValue("/*");
        editor.SetSelectionsValidated(S(1, 3, 1, 3));
        editor.Type("*", Keyboard);
        model.GetLineContent(1).ShouldBe("/** */");
    }

    [Fact]
    public async Task Issue_338693_Undo_After_Multi_Character_Autoclose_First_Removes_Only_The_Auto_Closed_Characters()
    {
        using var editor = _context.UsingCursor(["begi begi"], AutoClosingLanguageId);
        var model = editor.Model;

        var states = new List<string>();
        void RecordState() => states.Add(model.GetLineContent(1) + " " + string.Join(", ", editor.GetSelections().Select(s => s.ToString())));

        editor.SetSelectionsValidated(S(1, 5, 1, 5), S(1, 10, 1, 10));
        editor.Type("n", Keyboard);
        RecordState();
        await editor.Undo();
        RecordState();
        await editor.Undo();
        RecordState();

        states.ShouldBe(
        [
            "beginend beginend [1,6 -> 1,6], [1,15 -> 1,15]",
            "begin begin [1,6 -> 1,6], [1,12 -> 1,12]",
            "begi begi [1,5 -> 1,5], [1,10 -> 1,10]",
        ]);
    }

    [Fact]
    public void Issue_338693_Multi_Character_Autoclose_Does_Not_Insert_An_Orphan_Close_When_A_Listener_Reentrantly_Removes_The_Typed_Character()
    {
        using var editor = _context.UsingCursor(["begi"], AutoClosingLanguageId);
        var model = editor.Model;

        var isFirst = true;
        model.DidChangeContent += _ =>
        {
            if (isFirst)
            {
                isFirst = false;
                model.ApplyEdits([new SingleEditOperation(new TextRange(1, 5, 1, 6), "")]);
            }
        };

        editor.SetSelectionsValidated(S(1, 5, 1, 5));
        editor.Type("n", Keyboard);
        model.GetLineContent(1).ShouldBe("begi");
    }

    [Fact]
    public void Issue_338693_Multi_Character_Autoclose_Does_Not_Use_Stale_Selections_After_A_Reentrant_Edit()
    {
        using var editor = _context.UsingCursor(["begi"], AutoClosingLanguageId);
        var model = editor.Model;

        var isFirst = true;
        model.DidChangeContent += _ =>
        {
            if (isFirst)
            {
                isFirst = false;
                model.ApplyEdits([new SingleEditOperation(new TextRange(1, 1, 1, 1), "begin\n")]);
            }
        };

        editor.SetSelectionsValidated(S(1, 5, 1, 5));
        editor.Type("n", Keyboard);
        model.GetValue().ShouldBe("begin\nbegin");
    }

    [Fact]
    public async Task Issue_338693_Undo_After_Multi_Character_Autoclose_With_Auto_Indentation_First_Removes_Only_The_Auto_Closed_Characters()
    {
        const string languageId = "autoClosingWithIndentRulesLanguage";
        _context.Add(_context.LanguageConfigurationService.Register(languageId, new LanguageConfiguration
        {
            AutoClosingPairs = [new AutoClosingPairConditional("begin", "end")],
            IndentationRules = new IndentationRule(
                DecreaseIndentPattern: JsRegExp.Of(@"^\s*begin\b"),
                IncreaseIndentPattern: JsRegExp.Of(@"^\s*procedure\b")),
        }));
        using var editor = _context.UsingCursor(["\tfoo", "\tbegi"], languageId, editorOpts: EditorOptions.Default with { AutoIndent = EditorAutoIndentStrategy.Full });
        var model = editor.Model;

        var states = new List<string>();
        void RecordState() => states.Add(model.GetLineContent(2) + " " + string.Join(", ", editor.GetSelections().Select(s => s.ToString())));

        editor.SetSelectionsValidated(S(2, 6, 2, 6));
        editor.Type("n", Keyboard);
        RecordState();
        await editor.Undo();
        RecordState();
        await editor.Undo();
        RecordState();

        states.ShouldBe(
        [
            "beginend [2,6 -> 2,6]",
            "begin [2,6 -> 2,6]",
            "\tbegi [2,6 -> 2,6]",
        ]);
    }

    [Fact]
    public void AutoClosingPairs_BeforeText()
    {
        const string languageId = "autoClosingBeforeText";
        _context.Add(_context.LanguageConfigurationService.Register(languageId, new LanguageConfiguration
        {
            AutoClosingPairs = [new AutoClosingPairConditional("begin", "end") { BeforeText = JsRegExp.Of(@"(?:^|\s)begin$", "g") }],
        }));

        using var editor = _context.UsingCursor(["begi", "begi"], languageId);
        var model = editor.Model;
        var results = new List<string>();

        editor.SetSelectionsValidated(S(1, 5, 1, 5), S(2, 5, 2, 5));
        editor.Type("n", Keyboard);
        results.Add(model.GetValue());

        model.SetValue("prefixbegi");
        editor.SetSelectionsValidated(S(1, 11, 1, 11));
        editor.Type("n", Keyboard);
        results.Add(model.GetValue());

        model.SetValue("prefix begi");
        editor.SetSelectionsValidated(S(1, 12, 1, 12));
        editor.Type("n", Keyboard);
        results.Add(model.GetValue());

        results.ShouldBe(["beginend\nbeginend", "prefixbegin", "prefix beginend"]);
    }

    [Fact]
    public void AutoClosingPairs_BeforeText_Falls_Back_To_A_Shorter_Pair()
    {
        const string languageId = "autoClosingBeforeTextFallback";
        _context.Add(_context.LanguageConfigurationService.Register(languageId, new LanguageConfiguration
        {
            AutoClosingPairs =
            [
                new AutoClosingPairConditional("begin", "end") { BeforeText = JsRegExp.Of(@"(?:^|\s)begin$") },
                new AutoClosingPairConditional("n", "]"),
            ],
        }));

        using var editor = _context.UsingCursor(["prefixbegi"], languageId);
        editor.SetSelectionsValidated(S(1, 11, 1, 11));
        editor.Type("n", Keyboard);

        editor.Model.GetValue().ShouldBe("prefixbegin]");
    }

    [Fact]
    public void AutoClosingPairs_BeforeText_Falls_Back_To_A_Shorter_Pair_After_Composition()
    {
        const string languageId = "autoClosingBeforeTextCompositionFallback";
        _context.Add(_context.LanguageConfigurationService.Register(languageId, new LanguageConfiguration
        {
            AutoClosingPairs =
            [
                new AutoClosingPairConditional("begin", "end") { BeforeText = JsRegExp.Of(@"(?:^|\s)begin$") },
                new AutoClosingPairConditional("n", "]"),
            ],
        }));

        using var editor = _context.UsingCursor(["prefixbegi"], languageId);
        editor.SetSelectionsValidated(S(1, 11, 1, 11));
        editor.StartComposition();
        editor.Type("n", Keyboard);
        editor.CompositionType("n", 1, 0, 0, Keyboard);
        editor.EndComposition(Keyboard);

        editor.Model.GetValue().ShouldBe("prefixbegin]");
    }

    [Fact]
    public void AutoClosingPairs_Doc_Comments_Can_Be_Turned_Off()
    {
        using var editor = _context.UsingCursor([""], AutoClosingLanguageId, editorOpts: EditorOptions.Default with { AutoClosingComments = EditorAutoClosingStrategy.Never });
        var model = editor.Model;

        model.SetValue("/*");
        editor.SetSelectionsValidated(S(1, 3, 1, 3));
        editor.Type("*", Keyboard);
        model.GetLineContent(1).ShouldBe("/**");
    }

    [Fact]
    public void Issue_72177_Multi_Character_Autoclose_With_Conflicting_Patterns()
    {
        const string languageId = "autoClosingModeMultiChar";
        _context.Add(_context.LanguageConfigurationService.Register(languageId, new LanguageConfiguration
        {
            AutoClosingPairs =
            [
                new AutoClosingPairConditional("(", ")"),
                new AutoClosingPairConditional("(*", "*)"),
                new AutoClosingPairConditional("<@", "@>"),
                new AutoClosingPairConditional("<@@", "@@>"),
            ],
        }));

        using var editor = _context.UsingCursor([""], languageId);
        var model = editor.Model;

        editor.Type("(", Keyboard);
        model.GetLineContent(1).ShouldBe("()");
        editor.Type("*", Keyboard);
        model.GetLineContent(1).ShouldBe("(**)", "doesn't add entire close when already closed substring is there");

        model.SetValue("(");
        editor.SetSelectionsValidated(S(1, 2, 1, 2));
        editor.Type("*", Keyboard);
        model.GetLineContent(1).ShouldBe("(**)", "does add entire close if not already there");

        model.SetValue("");
        editor.Type("<@", Keyboard);
        model.GetLineContent(1).ShouldBe("<@@>");
        editor.Type("@", Keyboard);
        model.GetLineContent(1).ShouldBe("<@@@@>", "autocloses when before multi-character closing brace");
        editor.Type("(", Keyboard);
        model.GetLineContent(1).ShouldBe("<@@()@@>", "autocloses when before multi-character closing brace");
    }

    [Fact]
    public async Task Issue_55314_Do_Not_Auto_Close_When_Ending_With_Open()
    {
        const string languageId = "myElectricMode";
        _context.Add(_context.LanguageConfigurationService.Register(languageId, new LanguageConfiguration
        {
            AutoClosingPairs =
            [
                new AutoClosingPairConditional("{", "}"),
                new AutoClosingPairConditional("[", "]"),
                new AutoClosingPairConditional("(", ")"),
                new AutoClosingPairConditional("'", "'") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("\"", "\"") { NotIn = ["string"] },
                new AutoClosingPairConditional("B\"", "\"") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("`", "`") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("/**", " */") { NotIn = ["string"] },
            ],
        }));

        using var editor = _context.UsingCursor(["little goat", "little LAMB", "little sheep", "Big LAMB"], languageId);
        await AssertType(editor, 1, 4, "\"", "\"", "does not double quote when ending with open");
        await AssertType(editor, 2, 4, "\"", "\"", "does not double quote when ending with open");
        await AssertType(editor, 3, 4, "\"", "\"", "does not double quote when ending with open");
        await AssertType(editor, 4, 2, "\"", "\"", "does not double quote when ending with open");
        await AssertType(editor, 4, 3, "\"", "\"", "does not double quote when ending with open");
    }

    [Fact]
    public async Task Issue_27937_Trying_To_Add_An_Item_To_The_Front_Of_A_List_Is_Cumbersome()
    {
        using var editor = _context.UsingCursor(["var arr = [\"b\", \"c\"];"], AutoClosingLanguageId);
        await AssertType(editor, 1, 12, "\"", "\"", "does not over type and will not auto close");
    }

    [Fact]
    public void Issue_6841_Auto_Closing_Brackets_Should_Balance_Brackets()
    {
        const string languageId = "balancedAutoClosingLanguage";
        _context.Add(_context.LanguageConfigurationService.Register(languageId, new LanguageConfiguration
        {
            Brackets = [("{", "}"), ("[", "]"), ("(", ")")],
            AutoClosingPairs =
            [
                new AutoClosingPairConditional("{", "}"),
                new AutoClosingPairConditional("[", "]"),
                new AutoClosingPairConditional("(", ")"),
            ],
        }));

        using (var editor = _context.UsingCursor([""], languageId))
        {
            var model = editor.Model;
            (string Text, TextSelection Selection, string Type, string Expected)[] testCases =
            [
                ("\n}", S(1, 1, 1, 1), "{", "{\n}"),
                ("", S(1, 1, 1, 1), "{", "{}"),
                ("{}", S(1, 2, 1, 2), "{", "{{}}"),
                ("{\n", S(2, 1, 2, 1), "{", "{\n{}"),
                ("\n]", S(1, 1, 1, 1), "{", "{}\n]"),
                (")\n", S(2, 1, 2, 1), "(", ")\n()"),
                ("function foo() {\n\n}", S(2, 1, 2, 1), "{", "function foo() {\n{}\n}"),
                ("someFunction);", S(1, 13, 1, 13), "(", "someFunction();"),
                ("someFunction;", S(1, 13, 1, 13), "(", "someFunction();"),
                ("someFunctionsomeParam);", S(1, 13, 1, 13), "(", "someFunction(someParam);"),
            ];
            var actual = testCases.Select(testCase =>
            {
                model.SetValue(testCase.Text);
                editor.SetSelectionsValidated(testCase.Selection);
                editor.Type(testCase.Type, Keyboard);
                return model.GetValue();
            }).ToList();
            actual.ShouldBe(testCases.Select(testCase => testCase.Expected).ToList());
        }

        using (var editor = _context.UsingCursor(["", "}", ""], languageId))
        {
            editor.SetSelectionsValidated(S(1, 1, 1, 1), S(3, 1, 3, 1));
            editor.Type("{", Keyboard);
            editor.Model.GetValue().ShouldBe("{\n}\n{");
        }

        using (var editor = _context.UsingCursor(["", ")"], languageId, editorOpts: EditorOptions.Default with { AutoClosingBrackets = EditorAutoClosingStrategy.Always }))
        {
            editor.Type("(", Keyboard);
            editor.Model.GetValue().ShouldBe("()\n)");
        }

        _context.Add(_context.LanguageConfigurationService.Register(AutoClosingLanguageId, new LanguageConfiguration
        {
            Brackets = [("{", "}")],
        }));
        _context.SetupAutoClosingLanguageTokenization();
        using (var editor = _context.UsingCursor(["", "\"}\""], AutoClosingLanguageId))
        {
            editor.Type("{", Keyboard);
            editor.Model.GetValue().ShouldBe("{}\n\"}\"");
        }

        using (var editor = _context.UsingCursor(["", "\"}\"", "// }"], AutoClosingLanguageId))
        {
            editor.Type("{", Keyboard);
            editor.Model.GetValue().ShouldBe("{}\n\"}\"\n// }");
        }
    }
}
