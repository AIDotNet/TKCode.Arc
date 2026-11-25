// Ported from vscode (src/vs/editor/test/common/model/editableTextModel.test.ts, commit b57d5c9e), MIT license.
// The applyEdits suite; the flag suites and the CRLF suite are in EditableTextModelFlagsTests.cs.
// The mirror model is the TestMirrorTextModel of EditableTextModelTestUtils.cs; the reverse edits that ApplyEdits
// returns are turned back into SingleEditOperation before they are applied again.

using Shouldly;
using TKCode.Arc.Editor.Model;
using Xunit;
using static TKCode.Arc.Editor.Tests.EditableTextModelTestUtils;

namespace TKCode.Arc.Editor.Tests;

/// <summary>Upstream suite 'EditorModel - EditableTextModel.applyEdits'.</summary>
public sealed class EditableTextModelApplyEditsTests
{
    private static SingleEditOperation EditOp(int startLineNumber, int startColumn, int endLineNumber, int endColumn, string[] text) =>
        new(new TextRange(startLineNumber, startColumn, endLineNumber, endColumn), string.Join('\n', text), false);

    [Fact]
    public void High_Low_Surrogates_1()
    {
        TestApplyEditsWithSyncedModels(
            [
                "📚some",
                "very nice",
                "text",
            ],
            [
                EditOp(1, 2, 1, 2, ["a"]),
            ],
            [
                "a📚some",
                "very nice",
                "text",
            ],
            inputEditsAreInvalid: true);
    }

    [Fact]
    public void High_Low_Surrogates_2()
    {
        TestApplyEditsWithSyncedModels(
            [
                "📚some",
                "very nice",
                "text",
            ],
            [
                EditOp(1, 2, 1, 3, ["a"]),
            ],
            [
                "asome",
                "very nice",
                "text",
            ],
            inputEditsAreInvalid: true);
    }

    [Fact]
    public void High_Low_Surrogates_3()
    {
        TestApplyEditsWithSyncedModels(
            [
                "📚some",
                "very nice",
                "text",
            ],
            [
                EditOp(1, 1, 1, 2, ["a"]),
            ],
            [
                "asome",
                "very nice",
                "text",
            ],
            inputEditsAreInvalid: true);
    }

    [Fact]
    public void High_Low_Surrogates_4()
    {
        TestApplyEditsWithSyncedModels(
            [
                "📚some",
                "very nice",
                "text",
            ],
            [
                EditOp(1, 1, 1, 3, ["a"]),
            ],
            [
                "asome",
                "very nice",
                "text",
            ],
            inputEditsAreInvalid: true);
    }

    [Fact]
    public void Bug_19872_Undo_Is_Funky()
    {
        TestApplyEditsWithSyncedModels(
            [
                "something",
                " A",
                "",
                " B",
                "something else",
            ],
            [
                EditOp(2, 1, 2, 2, [""]),
                EditOp(3, 1, 4, 2, [""]),
            ],
            [
                "something",
                "A",
                "B",
                "something else",
            ]);
    }

    [Fact]
    public void Bug_19872_Undo_Is_Funky_2()
    {
        TestApplyEditsWithSyncedModels(
            [
                "something",
                "A",
                "B",
                "something else",
            ],
            [
                EditOp(2, 1, 2, 1, [" "]),
                EditOp(3, 1, 3, 1, ["", " "]),
            ],
            [
                "something",
                " A",
                "",
                " B",
                "something else",
            ]);
    }

    [Fact]
    public void Insert_Empty_Text()
    {
        TestApplyEditsWithSyncedModels(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ],
            [
                EditOp(1, 1, 1, 1, [""]),
            ],
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ]);
    }

    [Fact]
    public void Last_Op_Is_No_Op()
    {
        TestApplyEditsWithSyncedModels(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ],
            [
                EditOp(1, 1, 1, 2, [""]),
                EditOp(4, 1, 4, 1, [""]),
            ],
            [
                "y First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ]);
    }

    [Fact]
    public void Insert_Text_Without_Newline_1()
    {
        TestApplyEditsWithSyncedModels(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ],
            [
                EditOp(1, 1, 1, 1, ["foo "]),
            ],
            [
                "foo My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ]);
    }

    [Fact]
    public void Insert_Text_Without_Newline_2()
    {
        TestApplyEditsWithSyncedModels(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ],
            [
                EditOp(1, 3, 1, 3, [" foo"]),
            ],
            [
                "My foo First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ]);
    }

    [Fact]
    public void Insert_One_Newline()
    {
        TestApplyEditsWithSyncedModels(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ],
            [
                EditOp(1, 4, 1, 4, ["", ""]),
            ],
            [
                "My ",
                "First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ]);
    }

    [Fact]
    public void Insert_Text_With_One_Newline()
    {
        TestApplyEditsWithSyncedModels(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ],
            [
                EditOp(1, 3, 1, 3, [" new line", "No longer"]),
            ],
            [
                "My new line",
                "No longer First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ]);
    }

    [Fact]
    public void Insert_Text_With_Two_Newlines()
    {
        TestApplyEditsWithSyncedModels(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ],
            [
                EditOp(1, 3, 1, 3, [" new line", "One more line in the middle", "No longer"]),
            ],
            [
                "My new line",
                "One more line in the middle",
                "No longer First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ]);
    }

    [Fact]
    public void Insert_Text_With_Many_Newlines()
    {
        TestApplyEditsWithSyncedModels(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ],
            [
                EditOp(1, 3, 1, 3, ["", "", "", "", ""]),
            ],
            [
                "My",
                "",
                "",
                "",
                " First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ]);
    }

    [Fact]
    public void Insert_Multiple_Newlines()
    {
        TestApplyEditsWithSyncedModels(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ],
            [
                EditOp(1, 3, 1, 3, ["", "", "", "", ""]),
                EditOp(3, 15, 3, 15, ["a", "b"]),
            ],
            [
                "My",
                "",
                "",
                "",
                " First Line",
                "\t\tMy Second Line",
                "    Third Linea",
                "b",
                "",
                "1",
            ]);
    }

    [Fact]
    public void Delete_Empty_Text()
    {
        TestApplyEditsWithSyncedModels(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ],
            [
                EditOp(1, 1, 1, 1, [""]),
            ],
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ]);
    }

    [Fact]
    public void Delete_Text_From_One_Line()
    {
        TestApplyEditsWithSyncedModels(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ],
            [
                EditOp(1, 1, 1, 2, [""]),
            ],
            [
                "y First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ]);
    }

    [Fact]
    public void Delete_Text_From_One_Line_2()
    {
        TestApplyEditsWithSyncedModels(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ],
            [
                EditOp(1, 1, 1, 3, ["a"]),
            ],
            [
                "a First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ]);
    }

    [Fact]
    public void Delete_All_Text_From_A_Line()
    {
        TestApplyEditsWithSyncedModels(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ],
            [
                EditOp(1, 1, 1, 14, [""]),
            ],
            [
                "",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ]);
    }

    [Fact]
    public void Delete_Text_From_Two_Lines()
    {
        TestApplyEditsWithSyncedModels(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ],
            [
                EditOp(1, 4, 2, 6, [""]),
            ],
            [
                "My Second Line",
                "    Third Line",
                "",
                "1",
            ]);
    }

    [Fact]
    public void Delete_Text_From_Many_Lines()
    {
        TestApplyEditsWithSyncedModels(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ],
            [
                EditOp(1, 4, 3, 5, [""]),
            ],
            [
                "My Third Line",
                "",
                "1",
            ]);
    }

    [Fact]
    public void Delete_Everything()
    {
        TestApplyEditsWithSyncedModels(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "1",
            ],
            [
                EditOp(1, 1, 5, 2, [""]),
            ],
            [
                "",
            ]);
    }

    [Fact]
    public void Two_Unrelated_Edits()
    {
        TestApplyEditsWithSyncedModels(
            [
                "My First Line",
                "\t\tMy Second Line",
                "    Third Line",
                "",
                "123",
            ],
            [
                EditOp(2, 1, 2, 3, ["\t"]),
                EditOp(3, 1, 3, 5, [""]),
            ],
            [
                "My First Line",
                "\tMy Second Line",
                "Third Line",
                "",
                "123",
            ]);
    }

    [Fact]
    public void Two_Edits_On_One_Line()
    {
        TestApplyEditsWithSyncedModels(
            [
                "\t\tfirst\t    ",
                "\t\tsecond line",
                "\tthird line",
                "fourth line",
                "\t\t<!@#fifth#@!>\t\t",
            ],
            [
                EditOp(5, 3, 5, 7, [""]),
                EditOp(5, 12, 5, 16, [""]),
            ],
            [
                "\t\tfirst\t    ",
                "\t\tsecond line",
                "\tthird line",
                "fourth line",
                "\t\tfifth\t\t",
            ]);
    }

    [Fact]
    public void Many_Edits()
    {
        TestApplyEditsWithSyncedModels(
            [
                "{\"x\" : 1}",
            ],
            [
                EditOp(1, 2, 1, 2, ["\n  "]),
                EditOp(1, 5, 1, 6, [""]),
                EditOp(1, 9, 1, 9, ["\n"]),
            ],
            [
                "{",
                "  \"x\": 1",
                "}",
            ]);
    }

    [Fact]
    public void Many_Edits_Reversed()
    {
        TestApplyEditsWithSyncedModels(
            [
                "{",
                "  \"x\": 1",
                "}",
            ],
            [
                EditOp(1, 2, 2, 3, [""]),
                EditOp(2, 6, 2, 6, [" "]),
                EditOp(2, 9, 3, 1, [""]),
            ],
            [
                "{\"x\" : 1}",
            ]);
    }

    [Fact]
    public void Replacing_Newlines_1()
    {
        TestApplyEditsWithSyncedModels(
            [
                "{",
                "\"a\": true,",
                "",
                "\"b\": true",
                "}",
            ],
            [
                EditOp(1, 2, 2, 1, ["", "\t"]),
                EditOp(2, 11, 4, 1, ["", "\t"]),
            ],
            [
                "{",
                "\t\"a\": true,",
                "\t\"b\": true",
                "}",
            ]);
    }

    [Fact]
    public void Replacing_Newlines_2()
    {
        TestApplyEditsWithSyncedModels(
            [
                "some text",
                "some more text",
                "now comes an empty line",
                "",
                "after empty line",
                "and the last line",
            ],
            [
                EditOp(1, 5, 3, 1, [" text", "some more text", "some more text"]),
                EditOp(3, 2, 4, 1, ["o more lines", "asd", "asd", "asd"]),
                EditOp(5, 1, 5, 6, ["zzzzzzzz"]),
                EditOp(5, 11, 6, 16, ["1", "2", "3", "4"]),
            ],
            [
                "some text",
                "some more text",
                "some more textno more lines",
                "asd",
                "asd",
                "asd",
                "zzzzzzzz empt1",
                "2",
                "3",
                "4ne",
            ]);
    }

    [Fact]
    public void Advanced_1()
    {
        TestApplyEditsWithSyncedModels(
            [
                " {       \"d\": [",
                "             null",
                "        ] /*comment*/",
                "        ,\"e\": /*comment*/ [null] }",
            ],
            [
                EditOp(1, 1, 1, 2, [""]),
                EditOp(1, 3, 1, 10, ["", "  "]),
                EditOp(1, 16, 2, 14, ["", "    "]),
                EditOp(2, 18, 3, 9, ["", "  "]),
                EditOp(3, 22, 4, 9, [""]),
                EditOp(4, 10, 4, 10, ["", "  "]),
                EditOp(4, 28, 4, 28, ["", "    "]),
                EditOp(4, 32, 4, 32, ["", "  "]),
                EditOp(4, 33, 4, 34, ["", ""]),
            ],
            [
                "{",
                "  \"d\": [",
                "    null",
                "  ] /*comment*/,",
                "  \"e\": /*comment*/ [",
                "    null",
                "  ]",
                "}",
            ]);
    }

    [Fact]
    public void Advanced_Simplified()
    {
        TestApplyEditsWithSyncedModels(
            [
                "   abc",
                " ,def",
            ],
            [
                EditOp(1, 1, 1, 4, [""]),
                EditOp(1, 7, 2, 2, [""]),
                EditOp(2, 3, 2, 3, ["", ""]),
            ],
            [
                "abc,",
                "def",
            ]);
    }

    [Fact]
    public void Issue_144()
    {
        TestApplyEditsWithSyncedModels(
            [
                "package caddy",
                "",
                "func main() {",
                "\tfmt.Println(\"Hello World! :)\")",
                "}",
                "",
            ],
            [
                EditOp(1, 1, 6, 1, [
                    "package caddy",
                    "",
                    "import \"fmt\"",
                    "",
                    "func main() {",
                    "\tfmt.Println(\"Hello World! :)\")",
                    "}",
                    "",
                ]),
            ],
            [
                "package caddy",
                "",
                "import \"fmt\"",
                "",
                "func main() {",
                "\tfmt.Println(\"Hello World! :)\")",
                "}",
                "",
            ]);
    }

    [Fact]
    public void Issue_2586_Replacing_Selected_End_Of_Line_With_Newline_Locks_Up_The_Document()
    {
        TestApplyEditsWithSyncedModels(
            [
                "something",
                "interesting",
            ],
            [
                EditOp(1, 10, 2, 1, ["", ""]),
            ],
            [
                "something",
                "interesting",
            ]);
    }

    [Fact]
    public void Issue_3980()
    {
        TestApplyEditsWithSyncedModels(
            [
                "class A {",
                "    someProperty = false;",
                "    someMethod() {",
                "    this.someMethod();",
                "    }",
                "}",
            ],
            [
                EditOp(1, 8, 1, 9, ["", ""]),
                EditOp(3, 17, 3, 18, ["", ""]),
                EditOp(3, 18, 3, 18, ["    "]),
                EditOp(4, 5, 4, 5, ["    "]),
            ],
            [
                "class A",
                "{",
                "    someProperty = false;",
                "    someMethod()",
                "    {",
                "        this.someMethod();",
                "    }",
                "}",
            ]);
    }

    private static void TestApplyEditsFails(string[] original, SingleEditOperation[] edits)
    {
        using var model = TestTextModel.Create(string.Join('\n', original));

        var hasThrown = false;
        try
        {
            model.ApplyEdits(edits);
        }
        catch (Exception)
        {
            hasThrown = true;
        }

        hasThrown.ShouldBeTrue("expected model.applyEdits to fail.");
    }

    [Fact]
    public void Touching_Edits_Two_Inserts_At_The_Same_Position()
    {
        TestApplyEditsWithSyncedModels(
            [
                "hello world",
            ],
            [
                EditOp(1, 1, 1, 1, ["a"]),
                EditOp(1, 1, 1, 1, ["b"]),
            ],
            [
                "abhello world",
            ]);
    }

    [Fact]
    public void Touching_Edits_Insert_And_Replace_Touching()
    {
        TestApplyEditsWithSyncedModels(
            [
                "hello world",
            ],
            [
                EditOp(1, 1, 1, 1, ["b"]),
                EditOp(1, 1, 1, 3, ["ab"]),
            ],
            [
                "babllo world",
            ]);
    }

    [Fact]
    public void Overlapping_Edits_Two_Overlapping_Replaces()
    {
        TestApplyEditsFails(
            [
                "hello world",
            ],
            [
                EditOp(1, 1, 1, 2, ["b"]),
                EditOp(1, 1, 1, 3, ["ab"]),
            ]);
    }

    [Fact]
    public void Overlapping_Edits_Two_Overlapping_Deletes()
    {
        TestApplyEditsFails(
            [
                "hello world",
            ],
            [
                EditOp(1, 1, 1, 2, [""]),
                EditOp(1, 1, 1, 3, [""]),
            ]);
    }

    [Fact]
    public void Touching_Edits_Two_Touching_Replaces()
    {
        TestApplyEditsWithSyncedModels(
            [
                "hello world",
            ],
            [
                EditOp(1, 1, 1, 2, ["H"]),
                EditOp(1, 2, 1, 3, ["E"]),
            ],
            [
                "HEllo world",
            ]);
    }

    [Fact]
    public void Touching_Edits_Two_Touching_Deletes()
    {
        TestApplyEditsWithSyncedModels(
            [
                "hello world",
            ],
            [
                EditOp(1, 1, 1, 2, [""]),
                EditOp(1, 2, 1, 3, [""]),
            ],
            [
                "llo world",
            ]);
    }

    [Fact]
    public void Touching_Edits_Insert_And_Replace()
    {
        TestApplyEditsWithSyncedModels(
            [
                "hello world",
            ],
            [
                EditOp(1, 1, 1, 1, ["H"]),
                EditOp(1, 1, 1, 3, ["e"]),
            ],
            [
                "Hello world",
            ]);
    }

    [Fact]
    public void Touching_Edits_Replace_And_Insert()
    {
        TestApplyEditsWithSyncedModels(
            [
                "hello world",
            ],
            [
                EditOp(1, 1, 1, 3, ["H"]),
                EditOp(1, 3, 1, 3, ["e"]),
            ],
            [
                "Hello world",
            ]);
    }

    [Fact]
    public void Change_While_Emitting_Events_1()
    {
        Action<ModelContentChangedEvent>? listener = null;
        TextModel? listenedModel = null;
        AssertSyncedModels("Hello", (model, assertMirrorModels) =>
        {
            model.ApplyEdits([
                new SingleEditOperation(new TextRange(1, 6, 1, 6), " world!"),
                // forceMoveMarkers: false
            ]);

            assertMirrorModels();
        }, model =>
        {
            var isFirstTime = true;
            listenedModel = model;
            listener = _ =>
            {
                if (!isFirstTime)
                {
                    return;
                }

                isFirstTime = false;

                model.ApplyEdits([
                    new SingleEditOperation(new TextRange(1, 13, 1, 13), " How are you?"),
                    // forceMoveMarkers: false
                ]);
            };
            model.DidChangeContent += listener;
        });
        listenedModel!.DidChangeContent -= listener;
    }

    [Fact]
    public void Change_While_Emitting_Events_2()
    {
        Action<ModelContentChangedEvent>? listener = null;
        TextModel? listenedModel = null;
        AssertSyncedModels("Hello", (model, assertMirrorModels) =>
        {
            model.ApplyEdits([
                new SingleEditOperation(new TextRange(1, 6, 1, 6), " world!"),
                // forceMoveMarkers: false
            ]);

            assertMirrorModels();
        }, model =>
        {
            var isFirstTime = true;
            listenedModel = model;
            listener = (ModelContentChangedEvent e) =>
            {
                if (!isFirstTime)
                {
                    return;
                }

                isFirstTime = false;

                model.ApplyEdits([
                    new SingleEditOperation(new TextRange(1, 13, 1, 13), " How are you?"),
                    // forceMoveMarkers: false
                ]);
            };
            model.DidChangeContent += listener;
        });
        listenedModel!.DidChangeContent -= listener;
    }

    [Fact]
    public void Issue_1580_Changes_In_Line_Endings_Are_Not_Correctly_Reflected_In_The_Extension_Host_Leading_To_Invalid_Offsets_Sent_To_External_Refactoring_Tools()
    {
        var model = TestTextModel.Create("Hello\nWorld!");
        model.GetEOL().ShouldBe("\n");

        var mirrorModel2 = new TestMirrorTextModel(model.GetLinesContent(), model.GetEOL(), model.GetVersionId());

        void OnDidChangeContent(ModelContentChangedEvent e) => mirrorModel2.OnEvents(e);

        model.DidChangeContent += OnDidChangeContent;

        void AssertMirrorModels()
        {
            mirrorModel2.GetText().ShouldBe(model.GetValue(), "mirror model 2 text OK");
            mirrorModel2.Version.ShouldBe(model.GetVersionId(), "mirror model 2 version OK");
        }

        model.SetEOL(EndOfLineSequence.CRLF);
        AssertMirrorModels();

        model.DidChangeContent -= OnDidChangeContent;
        model.Dispose();
        mirrorModel2.Dispose();
    }

    [Fact]
    public void Issue_47733_Undo_Mangles_Unicode_Characters()
    {
        using var model = TestTextModel.Create("'👁'");

        model.ApplyEdits([
            new SingleEditOperation(new TextRange(1, 1, 1, 1), "\""),
            new SingleEditOperation(new TextRange(1, 2, 1, 2), "\""),
        ]);

        model.GetValue(EndOfLinePreference.LF).ShouldBe("\"'\"👁'");

        model.ValidateRange(new TextRange(1, 3, 1, 4)).ShouldBe(new TextRange(1, 3, 1, 4));

        model.ApplyEdits([
            new SingleEditOperation(new TextRange(1, 1, 1, 2), null),
            new SingleEditOperation(new TextRange(1, 3, 1, 4), null),
        ]);

        model.GetValue(EndOfLinePreference.LF).ShouldBe("'👁'");
    }

    [Fact]
    public void Issue_48741_Broken_Undo_Stack_With_Move_Lines_Up_With_Multiple_Cursors()
    {
        using var model = TestTextModel.Create(string.Join('\n', [
            "line1",
            "line2",
            "line3",
            "",
        ]));

        var undoEdits = model.ApplyEdits([
            new SingleEditOperation(new TextRange(4, 1, 4, 1), "line3"),
            new SingleEditOperation(new TextRange(3, 1, 3, 6), null),
            new SingleEditOperation(new TextRange(2, 1, 3, 1), null),
            new SingleEditOperation(new TextRange(3, 6, 3, 6), "\nline2"),
        ], computeUndoEdits: true)!;

        model.ApplyEdits(ToSingleEditOperations(undoEdits));

        model.GetValue().ShouldBe("line1\nline2\nline3\n");
    }
}
