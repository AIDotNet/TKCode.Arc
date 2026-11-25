// Tests for WordOperations, ported from vscode (src/vs/editor/contrib/wordOperations/test/browser/wordOperations.test.ts,
// commit b57d5c9e), MIT license. Upstream runs the word commands on a test code editor; here they are driven through
// the operations on a text model (OperationsTestEditor, WordCommands), with the same cases and expected results.
//
// Not ported:
//  - 'cursorWordLeft - Recognize words' and 'cursorWordRight - Recognize words': they need Intl.Segmenter
//    (wordSegmenterLocales: 'ja'); WordCharacterClassifier has no word segmenter.
//  - 'issue #51275 - cursorWordStartLeft does not push undo/redo stack element': it types through the view model.
// Upstream's cursorWordAccessibilityLeft/Right helpers pick the Select command for inSelectionMode false and the other
// way round; the stops they record are the same either way, so these call the command the name says.

using Shouldly;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Languages;
using Xunit;
using static TKCode.Arc.Editor.Tests.WordCommands;
using static TKCode.Arc.Editor.Tests.WordTestUtils;

namespace TKCode.Arc.Editor.Tests;

public sealed class WordOperationsTests
{
    private static readonly string[] s_text =
    [
        "    \tMy First Line\t ",
        "\tMy Second Line",
        "    Third Line🐶",
        "",
        "1",
    ];

    private static void AssertStops(
        string expected,
        TextPosition initialPosition,
        Action<OperationsTestEditor> action,
        Func<OperationsTestEditor, TextPosition> record,
        Func<OperationsTestEditor, bool> stopCondition,
        EditorOptions? options = null)
    {
        var (text, _) = DeserializePipePositions(expected);
        var actualStops = TestRepeatedActionAndExtractPositions(text, initialPosition, action, record, stopCondition, options);
        var actual = SerializePipePositions(text, actualStops);
        actual.ShouldBe(expected);
    }

    private static void AssertStopsLeft(string expected, Action<OperationsTestEditor> action, TextPosition? initialPosition = null) =>
        AssertStops(expected, initialPosition ?? new TextPosition(1000, 1000), action, ed => ed.GetPosition(), ed => ed.GetPosition() == new TextPosition(1, 1));

    private static void AssertStopsRight(string expected, Action<OperationsTestEditor> action, TextPosition stop) =>
        AssertStops(expected, new TextPosition(1, 1), action, ed => ed.GetPosition(), ed => ed.GetPosition() == stop);

    private static void AssertDeleteStopsLeft(string expected, Action<OperationsTestEditor> action) =>
        AssertStops(expected, new TextPosition(1000, 10000), action, ed => ed.GetPosition(), ed => ed.GetValue().Length == 0);

    private static void AssertDeleteStopsRight(string expected, Action<OperationsTestEditor> action)
    {
        var (text, _) = DeserializePipePositions(expected);
        AssertStops(
            expected,
            new TextPosition(1, 1),
            action,
            ed => new TextPosition(1, text.Length - ed.GetValue().Length + 1),
            ed => ed.GetValue().Length == 0);
    }

    private static void WithEditor(string[] lines, Action<OperationsTestEditor> callback)
    {
        using var editor = OperationsTestEditor.Create(lines);
        callback(editor);
    }

    [Fact]
    public void CursorWordLeft_Simple()
    {
        AssertStopsLeft(
            string.Join('\n',
                "|    \t|My |First |Line\t ",
                "|\t|My |Second |Line",
                "|    |Third |Line🐶",
                "|",
                "|1"),
            ed => CursorWordLeft(ed));
    }

    [Fact]
    public void CursorWordLeft_With_Selection()
    {
        WithEditor(s_text, editor =>
        {
            editor.SetPosition(new TextPosition(5, 2));
            CursorWordLeft(editor, true);
            editor.GetSelection().ShouldBe(new TextSelection(5, 2, 5, 1));
        });
    }

    [Fact]
    public void CursorWordLeft_Issue_832()
    {
        AssertStopsLeft("|   |/* |Just |some   |more   |text |a|+= |3 |+|5-|3 |+ |7 |*/  ", ed => CursorWordLeft(ed));
    }

    [Fact]
    public void CursorWordLeft_Issue_48046_Word_Selection_Doesnt_Work_As_Usual()
    {
        AssertStopsLeft("|deep.|object.|property", ed => CursorWordLeft(ed), new TextPosition(1, 21));
    }

    [Fact]
    public void CursorWordLeft_Does_Not_Recognize_Words()
    {
        AssertStopsLeft("|/* |これはテストです |/*", ed => CursorWordLeft(ed, true));
    }

    [Fact]
    public void CursorWordLeft_Issue_169904_Cursors_Out_Of_Sync()
    {
        string[] text =
        [
            ".grid1 {",
            "  display: grid;",
            "  grid-template-columns:",
            "    [full-start] minmax(1em, 1fr)",
            "    [main-start] minmax(0, 40em) [main-end]",
            "    minmax(1em, 1fr) [full-end];",
            "}",
            ".grid2 {",
            "  display: grid;",
            "  grid-template-columns:",
            "    [full-start] minmax(1em, 1fr)",
            "    [main-start] minmax(0, 40em) [main-end] minmax(1em, 1fr) [full-end];",
            "}",
        ];
        WithEditor(text, editor =>
        {
            editor.SetSelections([
                new TextSelection(5, 44, 5, 44),
                new TextSelection(6, 32, 6, 32),
                new TextSelection(12, 44, 12, 44),
                new TextSelection(12, 72, 12, 72),
            ]);
            CursorWordLeft(editor, false);
            editor.GetSelections().ShouldBe([
                new TextSelection(5, 43, 5, 43),
                new TextSelection(6, 31, 6, 31),
                new TextSelection(12, 43, 12, 43),
                new TextSelection(12, 71, 12, 71),
            ]);
        });
    }

    [Fact]
    public void CursorWordLeftSelect_Issue_74369_CursorWordLeft_And_CursorWordLeftSelect_Do_Not_Behave_Consistently()
    {
        AssertStopsLeft("|this.|is.|a.|test", ed => CursorWordLeft(ed, true), new TextPosition(1, 15));
    }

    [Fact]
    public void CursorWordStartLeft()
    {
        // This is the behaviour observed in Visual Studio, please do not touch test
        AssertStopsLeft("|   |/* |Just |some   |more   |text |a|+= |3 |+|5|-|3 |+ |7 |*/  ", ed => WordCommands.CursorWordStartLeft(ed));
    }

    [Fact]
    public void CursorWordStartLeft_Issue_51119_Regression_Makes_VS_Compatibility_Impossible()
    {
        // This is the behaviour observed in Visual Studio, please do not touch test
        AssertStopsLeft("|this|.|is|.|a|.|test", ed => WordCommands.CursorWordStartLeft(ed));
    }

    [Fact]
    public void CursorWordEndLeft()
    {
        AssertStopsLeft("|   /*| Just| some|   more|   text| a|+=| 3| +|5|-|3| +| 7| */|  ", ed => WordCommands.CursorWordEndLeft(ed));
    }

    [Fact]
    public void CursorWordRight_Simple()
    {
        AssertStopsRight(
            string.Join('\n',
                "    \tMy| First| Line|\t |",
                "\tMy| Second| Line|",
                "    Third| Line🐶|",
                "|",
                "1|"),
            ed => CursorWordRight(ed),
            new TextPosition(5, 2));
    }

    [Fact]
    public void CursorWordRight_Selection()
    {
        WithEditor(s_text, editor =>
        {
            editor.SetPosition(new TextPosition(1, 1));
            CursorWordRight(editor, true);
            editor.GetSelection().ShouldBe(new TextSelection(1, 1, 1, 8));
        });
    }

    [Fact]
    public void CursorWordRight_Issue_832()
    {
        AssertStopsRight("   /*| Just| some|   more|   text| a|+=| 3| +5|-3| +| 7| */|  |", ed => CursorWordRight(ed), new TextPosition(1, 50));
    }

    [Fact]
    public void CursorWordRight_Issue_41199()
    {
        AssertStopsRight("console|.log|(err|)|", ed => CursorWordRight(ed), new TextPosition(1, 17));
    }

    [Fact]
    public void CursorWordRight_Does_Not_Recognize_Words()
    {
        AssertStopsRight("/*| これはテストです|/*|", ed => CursorWordRight(ed), new TextPosition(1, 14));
    }

    [Fact]
    public void MoveWordEndRight()
    {
        AssertStopsRight("   /*| Just| some|   more|   text| a|+=| 3| +5|-3| +| 7| */|  |", ed => WordCommands.MoveWordEndRight(ed), new TextPosition(1, 50));
    }

    [Fact]
    public void MoveWordStartRight()
    {
        // This is the behaviour observed in Visual Studio, please do not touch test
        AssertStopsRight("   |/* |Just |some   |more   |text |a|+= |3 |+|5|-|3 |+ |7 |*/  |", ed => WordCommands.MoveWordStartRight(ed), new TextPosition(1, 50));
    }

    [Fact]
    public void Issue_51119_CursorWordStartRight_Regression_Makes_VS_Compatibility_Impossible()
    {
        // This is the behaviour observed in Visual Studio, please do not touch test
        AssertStopsRight("this|.|is|.|a|.|test|", ed => WordCommands.MoveWordStartRight(ed), new TextPosition(1, 15));
    }

    [Fact]
    public void Issue_64810_CursorWordStartRight_Skips_First_Word_After_Newline()
    {
        // This is the behaviour observed in Visual Studio, please do not touch test
        AssertStopsRight(string.Join('\n', "Hello |World|", "|Hei |mailman|"), ed => WordCommands.MoveWordStartRight(ed), new TextPosition(2, 12));
    }

    [Fact]
    public void CursorWordAccessibilityLeft()
    {
        AssertStopsLeft("|   /* |Just |some   |more   |text |a+= |3 +|5-|3 + |7 */  ", ed => WordCommands.CursorWordAccessibilityLeft(ed));
    }

    [Fact]
    public void CursorWordAccessibilityRight()
    {
        AssertStopsRight("   /* |Just |some   |more   |text |a+= |3 +|5-|3 + |7 */  |", ed => WordCommands.CursorWordAccessibilityRight(ed), new TextPosition(1, 50));
    }

    [Fact]
    public void DeleteWordLeft_For_Non_Empty_Selection()
    {
        WithEditor(s_text, editor =>
        {
            editor.SetSelection(new TextSelection(3, 7, 3, 9));
            WordCommands.DeleteWordLeft(editor);
            editor.Model.GetLineContent(3).ShouldBe("    Thd Line🐶");
            editor.GetPosition().ShouldBe(new TextPosition(3, 7));
        });
    }

    [Fact]
    public void DeleteWordLeft_For_Cursor_At_Beginning_Of_Document()
    {
        WithEditor(s_text, editor =>
        {
            editor.SetPosition(new TextPosition(1, 1));
            WordCommands.DeleteWordLeft(editor);
            editor.Model.GetLineContent(1).ShouldBe("    \tMy First Line\t ");
            editor.GetPosition().ShouldBe(new TextPosition(1, 1));
        });
    }

    [Fact]
    public void DeleteWordLeft_For_Cursor_At_End_Of_Whitespace()
    {
        WithEditor(s_text, editor =>
        {
            editor.SetPosition(new TextPosition(3, 11));
            WordCommands.DeleteWordLeft(editor);
            editor.Model.GetLineContent(3).ShouldBe("    Line🐶");
            editor.GetPosition().ShouldBe(new TextPosition(3, 5));
        });
    }

    [Fact]
    public void DeleteWordLeft_For_Cursor_Just_Behind_A_Word()
    {
        WithEditor(s_text, editor =>
        {
            editor.SetPosition(new TextPosition(2, 11));
            WordCommands.DeleteWordLeft(editor);
            editor.Model.GetLineContent(2).ShouldBe("\tMy  Line");
            editor.GetPosition().ShouldBe(new TextPosition(2, 5));
        });
    }

    [Fact]
    public void DeleteWordLeft_For_Cursor_Inside_Of_A_Word()
    {
        WithEditor(s_text, editor =>
        {
            editor.SetPosition(new TextPosition(1, 12));
            WordCommands.DeleteWordLeft(editor);
            editor.Model.GetLineContent(1).ShouldBe("    \tMy st Line\t ");
            editor.GetPosition().ShouldBe(new TextPosition(1, 9));
        });
    }

    [Fact]
    public void DeleteWordRight_For_Non_Empty_Selection()
    {
        WithEditor(s_text, editor =>
        {
            editor.SetSelection(new TextSelection(3, 7, 3, 9));
            WordCommands.DeleteWordRight(editor);
            editor.Model.GetLineContent(3).ShouldBe("    Thd Line🐶");
            editor.GetPosition().ShouldBe(new TextPosition(3, 7));
        });
    }

    [Fact]
    public void DeleteWordRight_For_Cursor_At_End_Of_Document()
    {
        WithEditor(s_text, editor =>
        {
            editor.SetPosition(new TextPosition(5, 3));
            WordCommands.DeleteWordRight(editor);
            editor.Model.GetLineContent(5).ShouldBe("1");
            editor.GetPosition().ShouldBe(new TextPosition(5, 2));
        });
    }

    [Fact]
    public void DeleteWordRight_For_Cursor_At_Beggining_Of_Whitespace()
    {
        WithEditor(s_text, editor =>
        {
            editor.SetPosition(new TextPosition(3, 1));
            WordCommands.DeleteWordRight(editor);
            editor.Model.GetLineContent(3).ShouldBe("Third Line🐶");
            editor.GetPosition().ShouldBe(new TextPosition(3, 1));
        });
    }

    [Fact]
    public void DeleteWordRight_For_Cursor_Just_Before_A_Word()
    {
        WithEditor(s_text, editor =>
        {
            editor.SetPosition(new TextPosition(2, 5));
            WordCommands.DeleteWordRight(editor);
            editor.Model.GetLineContent(2).ShouldBe("\tMy  Line");
            editor.GetPosition().ShouldBe(new TextPosition(2, 5));
        });
    }

    [Fact]
    public void DeleteWordRight_For_Cursor_Inside_Of_A_Word()
    {
        WithEditor(s_text, editor =>
        {
            editor.SetPosition(new TextPosition(1, 11));
            WordCommands.DeleteWordRight(editor);
            editor.Model.GetLineContent(1).ShouldBe("    \tMy Fi Line\t ");
            editor.GetPosition().ShouldBe(new TextPosition(1, 11));
        });
    }

    [Fact]
    public void DeleteWordLeft_Issue_832()
    {
        AssertDeleteStopsLeft("|   |/* |Just |some |text |a|+= |3 |+|5 |*/|  ", ed => WordCommands.DeleteWordLeft(ed));
    }

    [Fact]
    public void DeleteWordStartLeft()
    {
        AssertDeleteStopsLeft("|   |/* |Just |some |text |a|+= |3 |+|5 |*/  ", ed => WordCommands.DeleteWordStartLeft(ed));
    }

    [Fact]
    public void DeleteWordEndLeft()
    {
        AssertDeleteStopsLeft("|   /*| Just| some| text| a|+=| 3| +|5| */|  ", ed => WordCommands.DeleteWordEndLeft(ed));
    }

    [Fact]
    public void DeleteWordLeft_Issue_24947()
    {
        WithEditor(["{", "}"], editor =>
        {
            editor.SetPosition(new TextPosition(2, 1));
            WordCommands.DeleteWordLeft(editor);
            editor.Model.GetLineContent(1).ShouldBe("{}");
        });

        WithEditor(["{", "}"], editor =>
        {
            editor.SetPosition(new TextPosition(2, 1));
            WordCommands.DeleteWordStartLeft(editor);
            editor.Model.GetLineContent(1).ShouldBe("{}");
        });

        WithEditor(["{", "}"], editor =>
        {
            editor.SetPosition(new TextPosition(2, 1));
            WordCommands.DeleteWordEndLeft(editor);
            editor.Model.GetLineContent(1).ShouldBe("{}");
        });
    }

    [Fact]
    public void DeleteWordRight_Issue_832()
    {
        AssertDeleteStopsRight("   |/*| |Just| |some| |text| |a|+=| |3| |+|5|-|3| |*/|  |", ed => WordCommands.DeleteWordRight(ed));
    }

    [Fact]
    public void DeleteWordRight_Issue_3882()
    {
        WithEditor(["public void Add( int x,", "                 int y )"], editor =>
        {
            editor.SetPosition(new TextPosition(1, 24));
            WordCommands.DeleteWordRight(editor);
            editor.Model.GetLineContent(1).ShouldBe("public void Add( int x,int y )", "001");
        });
    }

    [Fact]
    public void DeleteWordStartRight_Issue_3882()
    {
        WithEditor(["public void Add( int x,", "                 int y )"], editor =>
        {
            editor.SetPosition(new TextPosition(1, 24));
            WordCommands.DeleteWordStartRight(editor);
            editor.Model.GetLineContent(1).ShouldBe("public void Add( int x,int y )", "001");
        });
    }

    [Fact]
    public void DeleteWordEndRight_Issue_3882()
    {
        WithEditor(["public void Add( int x,", "                 int y )"], editor =>
        {
            editor.SetPosition(new TextPosition(1, 24));
            WordCommands.DeleteWordEndRight(editor);
            editor.Model.GetLineContent(1).ShouldBe("public void Add( int x,int y )", "001");
        });
    }

    [Fact]
    public void DeleteWordStartRight()
    {
        AssertDeleteStopsRight("   |/* |Just |some |text |a|+= |3 |+|5|-|3 |*/  |", ed => WordCommands.DeleteWordStartRight(ed));
    }

    [Fact]
    public void DeleteWordEndRight()
    {
        AssertDeleteStopsRight("   /*| Just| some| text| a|+=| 3| +|5|-|3| */|  |", ed => WordCommands.DeleteWordEndRight(ed));
    }

    [Fact]
    public void DeleteWordRight_Issue_3882_1_Ctrl_Delete_Removing_Entire_Line_When_Used_At_The_End_Of_Line()
    {
        WithEditor(["A line with text.", "   And another one"], editor =>
        {
            editor.SetPosition(new TextPosition(1, 18));
            WordCommands.DeleteWordRight(editor);
            editor.Model.GetLineContent(1).ShouldBe("A line with text.And another one", "001");
        });
    }

    [Fact]
    public void DeleteWordLeft_Issue_3882_2_Ctrl_Delete_Removing_Entire_Line_When_Used_At_The_End_Of_Line()
    {
        WithEditor(["A line with text.", "   And another one"], editor =>
        {
            editor.SetPosition(new TextPosition(2, 1));
            WordCommands.DeleteWordLeft(editor);
            editor.Model.GetLineContent(1).ShouldBe("A line with text.   And another one", "001");
        });
    }

    [Fact]
    public void DeleteWordLeft_Issue_91855_Matching_Quote_Bracket_Paren_Doesnt_Get_Deleted_When_Hitting_Ctrl_Backspace()
    {
        const string languageId = "myTestMode";

        using var languageConfigurationService = new LanguageConfigurationService();
        using var registration = languageConfigurationService.Register(languageId, new LanguageConfiguration
        {
            AutoClosingPairs = [new AutoClosingPairConditional("\"", "\"")],
        });

        using var editor = OperationsTestEditor.Create(
            "a \"\"",
            EditorOptions.Default with { AutoClosingDelete = EditorAutoClosingEditStrategy.Always },
            languageId,
            languageConfigurationService);

        editor.SetPosition(new TextPosition(1, 4));
        WordCommands.DeleteWordLeft(editor);
        editor.Model.GetLineContent(1).ShouldBe("a ");
    }

    [Fact]
    public void DeleteInsideWord_Empty_Line()
    {
        WithEditor(["Line1", "", "Line2"], editor =>
        {
            editor.SetPosition(new TextPosition(2, 1));
            WordCommands.DeleteInsideWord(editor);
            editor.GetValue().ShouldBe("Line1\nLine2");
        });
    }

    [Fact]
    public void DeleteInsideWord_In_Whitespace_1()
    {
        WithEditor(["Just  some text."], editor =>
        {
            editor.SetPosition(new TextPosition(1, 6));
            WordCommands.DeleteInsideWord(editor);
            editor.GetValue().ShouldBe("Justsome text.");
        });
    }

    [Fact]
    public void DeleteInsideWord_In_Whitespace_2()
    {
        WithEditor(["Just     some text."], editor =>
        {
            editor.SetPosition(new TextPosition(1, 6));
            WordCommands.DeleteInsideWord(editor);
            editor.GetValue().ShouldBe("Justsome text.");
        });
    }

    [Fact]
    public void DeleteInsideWord_In_Whitespace_3()
    {
        WithEditor(["Just     \"some text."], editor =>
        {
            editor.SetPosition(new TextPosition(1, 6));
            AssertDeleteInsideWord(editor, "Just\"some text.", "\"some text.", "some text.", "text.", ".", "", "");
        });
    }

    [Fact]
    public void DeleteInsideWord_In_Non_Words()
    {
        WithEditor(["x=3+4+5+6"], editor =>
        {
            editor.SetPosition(new TextPosition(1, 7));
            AssertDeleteInsideWord(editor, "x=3+45+6", "x=3++6", "x=36", "x=", "x", "", "");
        });
    }

    [Fact]
    public void DeleteInsideWord_In_Words_1()
    {
        WithEditor(["This is interesting"], editor =>
        {
            editor.SetPosition(new TextPosition(1, 7));
            AssertDeleteInsideWord(editor, "This interesting", "This", "", "");
        });
    }

    [Fact]
    public void DeleteInsideWord_In_Words_2()
    {
        WithEditor(["This  is  interesting"], editor =>
        {
            editor.SetPosition(new TextPosition(1, 7));
            AssertDeleteInsideWord(editor, "This  interesting", "This", "", "");
        });
    }

    [Fact]
    public void DeleteInsideWord_OnlyWord_Does_Not_Delete_Whitespace_Before_Last_Word()
    {
        WithEditor(["hello world"], editor =>
        {
            editor.SetPosition(new TextPosition(1, 9));
            WordCommands.DeleteInsideWord(editor, onlyWord: true);
            editor.GetValue().ShouldBe("hello ");
        });
    }

    [Fact]
    public void DeleteInsideWord_OnlyWord_Deletes_Just_The_Word_Leaves_Double_Spaces()
    {
        WithEditor(["This is interesting"], editor =>
        {
            editor.SetPosition(new TextPosition(1, 7));
            WordCommands.DeleteInsideWord(editor, onlyWord: true);
            editor.GetValue().ShouldBe("This  interesting");
        });
    }

    private static void AssertDeleteInsideWord(OperationsTestEditor editor, params string[] expectedValues)
    {
        foreach (var expected in expectedValues)
        {
            WordCommands.DeleteInsideWord(editor);
            editor.GetValue().ShouldBe(expected);
        }
    }
}
