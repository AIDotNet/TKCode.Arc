// Tests for WordPartOperations, ported from vscode
// (src/vs/editor/contrib/wordPartOperations/test/browser/wordPartOperations.test.ts, commit b57d5c9e), MIT license.
// Upstream runs the word part commands on a test code editor; here they are driven through the operations on a text
// model (OperationsTestEditor, WordCommands), with the same cases and expected results.

using Shouldly;
using TKCode.Arc.Editor.Config;
using Xunit;
using static TKCode.Arc.Editor.Tests.WordTestUtils;

namespace TKCode.Arc.Editor.Tests;

public sealed class WordPartOperationsTests
{
    // default characters sans '$-%~' plus '·'
    private static readonly EditorOptions s_kebabOptions = EditorOptions.Default with { WordSeparators = "!\"#&'()*+,./:;<=>?@[\\]^`{|}·" };

    private static void AssertStops(
        string expected,
        TextPosition initialPosition,
        Action<OperationsTestEditor> action,
        Func<string, OperationsTestEditor, TextPosition> record,
        Func<OperationsTestEditor, bool> stopCondition,
        EditorOptions? options = null)
    {
        var (text, _) = DeserializePipePositions(expected);
        var actualStops = TestRepeatedActionAndExtractPositions(text, initialPosition, action, ed => record(text, ed), stopCondition, options);
        var actual = SerializePipePositions(text, actualStops);
        actual.ShouldBe(expected);
    }

    private static TextPosition Position(string text, OperationsTestEditor editor) => editor.GetPosition();

    private static TextPosition DeletedLength(string text, OperationsTestEditor editor) => new(1, text.Length - editor.GetValue().Length + 1);

    private static bool AtStart(OperationsTestEditor editor) => editor.GetPosition() == new TextPosition(1, 1);

    private static bool IsEmpty(OperationsTestEditor editor) => editor.GetValue().Length == 0;

    [Fact]
    public void CursorWordPartLeft_Basic()
    {
        AssertStops(
            string.Join('\n',
                "|start| |line|",
                "|this|Is|A|Camel|Case|Var|  |this_|is_|a_|snake_|case_|var| |THIS_|IS_|CAPS_|SNAKE| |this_|IS|Mixed|Use|",
                "|end| |line"),
            new TextPosition(1000, 1000),
            ed => WordCommands.CursorWordPartLeft(ed),
            Position,
            AtStart);
    }

    [Fact]
    public void CursorWordPartLeft_Issue_53899_Whitespace()
    {
        AssertStops(
            "|myvar| |=| |'|demonstration|     |of| |selection| |with| |space|'",
            new TextPosition(1000, 1000),
            ed => WordCommands.CursorWordPartLeft(ed),
            Position,
            AtStart);
    }

    [Fact]
    public void CursorWordPartLeft_Issue_53899_Underscores()
    {
        AssertStops(
            "|myvar| |=| |'|demonstration_____|of| |selection| |with| |space|'",
            new TextPosition(1000, 1000),
            ed => WordCommands.CursorWordPartLeft(ed),
            Position,
            AtStart);
    }

    [Fact]
    public void CursorWordPartRight_Basic()
    {
        AssertStops(
            string.Join('\n',
                "start| |line|",
                "|this|Is|A|Camel|Case|Var|  |this|_is|_a|_snake|_case|_var| |THIS|_IS|_CAPS|_SNAKE| |this|_IS|Mixed|Use|",
                "|end| |line|"),
            new TextPosition(1, 1),
            ed => WordCommands.CursorWordPartRight(ed),
            Position,
            ed => ed.GetPosition() == new TextPosition(3, 9));
    }

    [Fact]
    public void CursorWordPartRight_Issue_53899_Whitespace()
    {
        AssertStops(
            "myvar| |=| |'|demonstration|     |of| |selection| |with| |space|'|",
            new TextPosition(1, 1),
            ed => WordCommands.CursorWordPartRight(ed),
            Position,
            ed => ed.GetPosition() == new TextPosition(1, 52));
    }

    [Fact]
    public void CursorWordPartRight_Issue_53899_Underscores()
    {
        AssertStops(
            "myvar| |=| |'|demonstration|_____of| |selection| |with| |space|'|",
            new TextPosition(1, 1),
            ed => WordCommands.CursorWordPartRight(ed),
            Position,
            ed => ed.GetPosition() == new TextPosition(1, 52));
    }

    [Fact]
    public void CursorWordPartRight_Issue_53899_Second_Case()
    {
        AssertStops(
            string.Join('\n',
                ";| |--| |1|",
                "|;|        |--| |2|",
                "|;|    |#|3|",
                "|;|   |#|4|"),
            new TextPosition(1, 1),
            ed => WordCommands.CursorWordPartRight(ed),
            Position,
            ed => ed.GetPosition() == new TextPosition(4, 7));
    }

    [Fact]
    public void Issue_93239_CursorWordPartRight()
    {
        AssertStops(
            "foo|_bar|",
            new TextPosition(1, 1),
            ed => WordCommands.CursorWordPartRight(ed),
            Position,
            ed => ed.GetPosition() == new TextPosition(1, 8));
    }

    [Fact]
    public void Issue_93239_CursorWordPartLeft()
    {
        AssertStops(
            "|foo_|bar",
            new TextPosition(1, 8),
            ed => WordCommands.CursorWordPartLeft(ed),
            Position,
            AtStart);
    }

    [Fact]
    public void DeleteWordPartLeft_Basic()
    {
        AssertStops(
            "|   |/*| |Just| |some| |text| |a|+=| |3| |+|5|-|3| |*/|  |this|Is|A|Camel|Case|Var|  |this_|is_|a_|snake_|case_|var| |THIS_|IS_|CAPS_|SNAKE| |this_|IS|Mixed|Use",
            new TextPosition(1, 1000),
            ed => WordCommands.DeleteWordPartLeft(ed),
            Position,
            IsEmpty);
    }

    [Fact]
    public void DeleteWordPartRight_Basic()
    {
        AssertStops(
            "   |/*| |Just| |some| |text| |a|+=| |3| |+|5|-|3| |*/|  |this|Is|A|Camel|Case|Var|  |this|_is|_a|_snake|_case|_var| |THIS|_IS|_CAPS|_SNAKE| |this|_IS|Mixed|Use|",
            new TextPosition(1, 1),
            ed => WordCommands.DeleteWordPartRight(ed),
            DeletedLength,
            IsEmpty);
    }

    [Fact]
    public void Issue_158667_CursorWordPartLeft_Stops_At_Dash_Even_When_Dash_Is_Not_In_Word_Separators()
    {
        AssertStops(
            "|this-|is-|a-|kebab-|case-|var| |THIS-|IS-|CAPS-|KEBAB| |this-|IS|Mixed|Use",
            new TextPosition(1000, 1000),
            ed => WordCommands.CursorWordPartLeft(ed),
            Position,
            AtStart,
            s_kebabOptions);
    }

    [Fact]
    public void Issue_158667_CursorWordPartRight_Stops_At_Dash_Even_When_Dash_Is_Not_In_Word_Separators()
    {
        AssertStops(
            "this|-is|-a|-kebab|-case|-var| |THIS|-IS|-CAPS|-KEBAB| |this|-IS|Mixed|Use|",
            new TextPosition(1, 1),
            ed => WordCommands.CursorWordPartRight(ed),
            Position,
            ed => ed.GetPosition() == new TextPosition(1, 60),
            s_kebabOptions);
    }

    [Fact]
    public void Issue_158667_DeleteWordPartLeft_Stops_At_Dash_Even_When_Dash_Is_Not_In_Word_Separators()
    {
        AssertStops(
            "|this-|is-|a-|kebab-|case-|var| |THIS-|IS-|CAPS-|KEBAB| |this-|IS|Mixed|Use",
            new TextPosition(1000, 1000),
            ed => WordCommands.DeleteWordPartLeft(ed),
            Position,
            IsEmpty,
            s_kebabOptions);
    }

    [Fact]
    public void Issue_158667_DeleteWordPartRight_Stops_At_Dash_Even_When_Dash_Is_Not_In_Word_Separators()
    {
        AssertStops(
            "this|-is|-a|-kebab|-case|-var| |THIS|-IS|-CAPS|-KEBAB| |this|-IS|Mixed|Use|",
            new TextPosition(1, 1),
            ed => WordCommands.DeleteWordPartRight(ed),
            DeletedLength,
            IsEmpty,
            s_kebabOptions);
    }
}
