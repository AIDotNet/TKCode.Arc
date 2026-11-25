// Tests for the core commands (src/TKCode.Arc.Editor/Commands/CoreCommands*.cs, ported from vscode's
// src/vs/editor/browser/coreCommands.ts, commit b57d5c9e, MIT license) that the ported cursor tests do not run: the
// scroll commands, RevealLine, SelectAll, SetSelection, CancelSelection, RemoveSecondaryCursors, the line start / end,
// page and sticky end commands, the paged column selection, LineBreakInsert, DeleteRight, Undo / Redo and the command
// table. Upstream has no tests for these commands; the expected values follow upstream's bodies on a test code editor
// (a 100 pixel high editor with 19 pixel lines: a page is 3 lines, see CursorConfiguration.PageSize).

using Shouldly;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class CoreCommandsTests
{
    private const double LineHeight = 19;

    private static readonly string FiftyLines = string.Join('\n', Enumerable.Range(1, 50).Select(i => "line " + i));

    private static TextSelection S(int a, int b, int c, int d) => new(a, b, c, d);

    private static TextPosition P(int lineNumber, int column) => new(lineNumber, column);

    private static void AssertCursor(TestViewModel viewModel, params TextSelection[] expected) =>
        viewModel.GetSelections().Select(s => s.ToString()).ShouldBe(expected.Select(s => s.ToString()));

    private static double ScrollTop(TestViewModel viewModel) => viewModel.ViewLayout.GetCurrentScrollTop();

    private static void Scroll(TestViewModel viewModel, CoreNavigationCommands.EditorScrollCommandOptions args) =>
        CoreNavigationCommands.EditorScroll.RunCoreEditorCommand(viewModel, args);

    private sealed class RevealRecorder : ViewEventHandler
    {
        public List<ViewRevealRangeRequestEvent> Events { get; } = new();

        public override bool OnRevealRangeRequest(ViewRevealRangeRequestEvent e)
        {
            Events.Add(e);
            return false;
        }
    }

    private static RevealRecorder Record(TestViewModel viewModel)
    {
        var recorder = new RevealRecorder();
        viewModel.AddViewEventHandler(recorder);
        return recorder;
    }

    // --------- scrolling

    [Fact]
    public void ScrollLineDown_And_ScrollLineUp_Scroll_By_One_Line()
    {
        using var editor = TestCodeEditor.Create(FiftyLines);
        var viewModel = editor.ViewModel;

        CoreNavigationCommands.ScrollLineDown.RunCoreEditorCommand(viewModel, new());
        ScrollTop(viewModel).ShouldBe(LineHeight);
        CoreNavigationCommands.ScrollLineDown.RunCoreEditorCommand(viewModel, new());
        ScrollTop(viewModel).ShouldBe(2 * LineHeight);
        CoreNavigationCommands.ScrollLineUp.RunCoreEditorCommand(viewModel, new());
        ScrollTop(viewModel).ShouldBe(LineHeight);
        CoreNavigationCommands.ScrollLineUp.RunCoreEditorCommand(viewModel, new());
        CoreNavigationCommands.ScrollLineUp.RunCoreEditorCommand(viewModel, new());
        ScrollTop(viewModel).ShouldBe(0);

        // the cursor does not move
        AssertCursor(viewModel, S(1, 1, 1, 1));
    }

    [Fact]
    public void ScrollPageDown_And_ScrollPageUp_Scroll_By_The_Page_Size()
    {
        using var editor = TestCodeEditor.Create(FiftyLines);
        var viewModel = editor.ViewModel;
        viewModel.CursorConfig.PageSize.ShouldBe(3);

        CoreNavigationCommands.ScrollPageDown.RunCoreEditorCommand(viewModel, new());
        ScrollTop(viewModel).ShouldBe(3 * LineHeight);
        CoreNavigationCommands.ScrollPageDown.RunCoreEditorCommand(viewModel, new());
        ScrollTop(viewModel).ShouldBe(6 * LineHeight);
        CoreNavigationCommands.ScrollPageUp.RunCoreEditorCommand(viewModel, new());
        ScrollTop(viewModel).ShouldBe(3 * LineHeight);
    }

    [Fact]
    public void ScrollEditorBottom_And_ScrollEditorTop()
    {
        using var editor = TestCodeEditor.Create(FiftyLines);
        var viewModel = editor.ViewModel;

        // the top of line lineCount - pageSize
        CoreNavigationCommands.ScrollEditorBottom.RunCoreEditorCommand(viewModel, new());
        ScrollTop(viewModel).ShouldBe(viewModel.ViewLayout.GetVerticalOffsetForLineNumber(50 - 3));
        ScrollTop(viewModel).ShouldBe(46 * LineHeight);

        CoreNavigationCommands.ScrollEditorTop.RunCoreEditorCommand(viewModel, new());
        ScrollTop(viewModel).ShouldBe(0);
    }

    [Fact]
    public void EditorScroll_By_HalfPage_Rounds_Half_The_Page_Size()
    {
        using var editor = TestCodeEditor.Create(FiftyLines);
        var viewModel = editor.ViewModel;

        // Math.round(3 / 2) * 2 = 4 lines
        Scroll(viewModel, new() { To = "down", By = "halfPage", Value = 2 });
        ScrollTop(viewModel).ShouldBe(4 * LineHeight);
    }

    [Fact]
    public void EditorScroll_By_Model_Lines_Puts_The_Line_At_The_Top()
    {
        using var editor = TestCodeEditor.Create(FiftyLines);
        var viewModel = editor.ViewModel;

        Scroll(viewModel, new() { To = "down", By = "line", Value = 3 });
        ScrollTop(viewModel).ShouldBe(3 * LineHeight);

        Scroll(viewModel, new() { To = "up", By = "line", Value = 10 });
        ScrollTop(viewModel).ShouldBe(0);
    }

    [Fact]
    public void EditorScroll_Defaults_To_One_Wrapped_Line()
    {
        using var editor = TestCodeEditor.Create(FiftyLines);
        var viewModel = editor.ViewModel;

        Scroll(viewModel, new() { To = "down" });
        ScrollTop(viewModel).ShouldBe(LineHeight);

        // value 0 is 1 (args.value || 1), and a fraction is rounded down
        Scroll(viewModel, new() { To = "down", Value = 0 });
        ScrollTop(viewModel).ShouldBe(2 * LineHeight);
        Scroll(viewModel, new() { To = "down", Value = 2.9 });
        ScrollTop(viewModel).ShouldBe(4 * LineHeight);
    }

    [Fact]
    public void EditorScroll_Ignores_Illegal_Arguments()
    {
        using var editor = TestCodeEditor.Create(FiftyLines);
        var viewModel = editor.ViewModel;

        Scroll(viewModel, new());
        Scroll(viewModel, new() { To = "sideways" });
        // a vertical unit with a horizontal direction, and the other way round
        Scroll(viewModel, new() { To = "left", By = "line" });
        Scroll(viewModel, new() { To = "down", By = "column" });
        ScrollTop(viewModel).ShouldBe(0);
        viewModel.ViewLayout.GetCurrentScrollLeft().ShouldBe(0);
    }

    [Fact]
    public void EditorScroll_With_RevealCursor_Moves_The_Cursor_Into_The_New_Viewport()
    {
        using var editor = TestCodeEditor.Create(FiftyLines);
        var viewModel = editor.ViewModel;

        Scroll(viewModel, new() { To = "down", By = "line", Value = 10, RevealCursor = true });
        ScrollTop(viewModel).ShouldBe(10 * LineHeight);
        // lines 11 to 15 are completely visible at that scroll top; the cursor padding (sticky scroll is on, so half the
        // visible lines, 2) leaves 13 to 15, and the cursor goes to the first of them
        AssertCursor(viewModel, S(13, 1, 13, 1));

        Scroll(viewModel, new() { To = "up", By = "line", Value = 10, RevealCursor = true, Select = true });
        ScrollTop(viewModel).ShouldBe(0);
        // lines 1 to 5 are visible, 3 to 4 once padded: the cursor goes to the line before the range's end line
        // (findPositionInViewportIfOutside keeps off the last line) and selects from where it was
        AssertCursor(viewModel, S(13, 1, 3, 1));
    }

    [Fact]
    public void EditorScroll_Without_RevealCursor_Leaves_The_Cursor()
    {
        using var editor = TestCodeEditor.Create(FiftyLines);
        var viewModel = editor.ViewModel;

        Scroll(viewModel, new() { To = "down", By = "page", Value = 2 });
        ScrollTop(viewModel).ShouldBe(6 * LineHeight);
        AssertCursor(viewModel, S(1, 1, 1, 1));
    }

    [Fact]
    public void ScrollRight_And_ScrollLeft_Scroll_By_Two_Columns()
    {
        using var editor = TestCodeEditor.Create(FiftyLines);
        var viewModel = editor.ViewModel;
        viewModel.ViewLayout.SetMaxLineWidth(1000);

        CoreNavigationCommands.ScrollRight.RunCoreEditorCommand(viewModel, new());
        viewModel.ViewLayout.GetCurrentScrollLeft().ShouldBe(20);
        CoreNavigationCommands.ScrollRight.RunCoreEditorCommand(viewModel, new());
        viewModel.ViewLayout.GetCurrentScrollLeft().ShouldBe(40);
        CoreNavigationCommands.ScrollLeft.RunCoreEditorCommand(viewModel, new());
        viewModel.ViewLayout.GetCurrentScrollLeft().ShouldBe(20);

        CoreNavigationCommands.EditorScroll.ComputeDesiredScrollLeft(
            viewModel,
            EditorScroll_.Parse(new() { To = "left", By = "column", Value = 3 })!).ShouldBe(20 - 30);
    }

    [Fact]
    public void EditorScroll_Parse()
    {
        EditorScroll_.Parse(new() { To = "up" }).ShouldBe(new EditorScroll_.ParsedArguments(EditorScroll_.Direction.Up, EditorScroll_.Unit.WrappedLine, 1, false, false));
        EditorScroll_.Parse(new() { To = "right", By = "column", Value = 4.5, RevealCursor = true, Select = true })
            .ShouldBe(new EditorScroll_.ParsedArguments(EditorScroll_.Direction.Right, EditorScroll_.Unit.Column, 4, true, true));
        EditorScroll_.Parse(new() { To = "down", By = "nonsense" })!.Unit.ShouldBe(EditorScroll_.Unit.WrappedLine);
        EditorScroll_.Parse(new() { To = "Down" }).ShouldBeNull();
        EditorScroll_.Parse(new()).ShouldBeNull();
    }

    [Fact]
    public void IsEditorScrollArgs_And_IsRevealLineArgs()
    {
        EditorScroll_.IsEditorScrollArgs(new EditorScroll_.RawArguments { To = "up" }).ShouldBeTrue();
        EditorScroll_.IsEditorScrollArgs(new EditorScroll_.RawArguments()).ShouldBeFalse();
        EditorScroll_.IsEditorScrollArgs(new Dictionary<string, object?> { ["to"] = "up", ["by"] = "line", ["value"] = 2, ["revealCursor"] = true }).ShouldBeTrue();
        EditorScroll_.IsEditorScrollArgs(new Dictionary<string, object?> { ["to"] = 1 }).ShouldBeFalse();
        EditorScroll_.IsEditorScrollArgs(new Dictionary<string, object?> { ["to"] = "up", ["value"] = "2" }).ShouldBeFalse();
        EditorScroll_.IsEditorScrollArgs(new Dictionary<string, object?> { ["to"] = "up", ["revealCursor"] = "yes" }).ShouldBeFalse();
        EditorScroll_.IsEditorScrollArgs("up").ShouldBeFalse();

        RevealLine_.IsRevealLineArgs(new RevealLine_.RawArguments { LineNumber = 3 }).ShouldBeTrue();
        RevealLine_.IsRevealLineArgs(new RevealLine_.RawArguments { LineNumber = "3" }).ShouldBeTrue();
        RevealLine_.IsRevealLineArgs(new RevealLine_.RawArguments()).ShouldBeFalse();
        RevealLine_.IsRevealLineArgs(new Dictionary<string, object?> { ["lineNumber"] = 3.0, ["at"] = "top" }).ShouldBeTrue();
        RevealLine_.IsRevealLineArgs(new Dictionary<string, object?> { ["lineNumber"] = true }).ShouldBeFalse();
        RevealLine_.IsRevealLineArgs(new Dictionary<string, object?> { ["lineNumber"] = 3, ["at"] = 1 }).ShouldBeFalse();
        RevealLine_.IsRevealLineArgs(new Dictionary<string, object?>()).ShouldBeFalse();
    }

    // --------- RevealLine

    [Fact]
    public void RevealLine_Reveals_The_Zero_Based_Line_At_The_Requested_Position()
    {
        using var editor = TestCodeEditor.Create(FiftyLines);
        var viewModel = editor.ViewModel;
        var recorder = Record(viewModel);

        CoreNavigationCommands.RevealLine.RunCoreEditorCommand(viewModel, new() { LineNumber = 4, At = "center", Source = "test" });
        var e = recorder.Events.ShouldHaveSingleItem();
        e.Range.ShouldBe(new TextRange(5, 1, 5, 7));
        e.VerticalType.ShouldBe(VerticalRevealType.Center);
        e.RevealHorizontal.ShouldBeFalse();
        e.ScrollType.ShouldBe(ScrollType.Smooth);
        e.Source.ShouldBe("test");

        CoreNavigationCommands.RevealLine.RunCoreEditorCommand(viewModel, new() { LineNumber = 0, At = "top" });
        recorder.Events[^1].VerticalType.ShouldBe(VerticalRevealType.Top);
        CoreNavigationCommands.RevealLine.RunCoreEditorCommand(viewModel, new() { LineNumber = 0, At = "bottom" });
        recorder.Events[^1].VerticalType.ShouldBe(VerticalRevealType.Bottom);
        CoreNavigationCommands.RevealLine.RunCoreEditorCommand(viewModel, new() { LineNumber = 0, At = "elsewhere" });
        recorder.Events[^1].VerticalType.ShouldBe(VerticalRevealType.Simple);
        CoreNavigationCommands.RevealLine.RunCoreEditorCommand(viewModel, new() { LineNumber = 0 });
        recorder.Events[^1].VerticalType.ShouldBe(VerticalRevealType.Simple);

        // revealing does not move the cursor
        AssertCursor(viewModel, S(1, 1, 1, 1));
    }

    [Fact]
    public void RevealLine_Clamps_The_Line_Number_And_Reads_Strings_As_ParseInt()
    {
        using var editor = TestCodeEditor.Create(FiftyLines);
        var viewModel = editor.ViewModel;
        var recorder = Record(viewModel);

        void Reveal(object? lineNumber) => CoreNavigationCommands.RevealLine.RunCoreEditorCommand(viewModel, new() { LineNumber = lineNumber });
        int LastLine() => recorder.Events[^1].Range!.Value.StartLineNumber;

        Reveal(-10);
        LastLine().ShouldBe(1);
        Reveal(1000);
        LastLine().ShouldBe(50);
        Reveal(null);
        LastLine().ShouldBe(1);
        Reveal("7");
        LastLine().ShouldBe(8);
        Reveal("  12abc");
        LastLine().ShouldBe(13);
        Reveal("0x10");
        LastLine().ShouldBe(17);
        Reveal("");
        LastLine().ShouldBe(1);
        Reveal(2.7);
        LastLine().ShouldBe(3);
        Reveal(double.NaN);
        LastLine().ShouldBe(1);

        var count = recorder.Events.Count;
        Reveal("abc");
        recorder.Events.Count.ShouldBe(count);
    }

    [Fact]
    public void ParseInt_Reads_Like_JavaScript()
    {
        CoreCommandsArgs.ParseInt("42").ShouldBe(42);
        CoreCommandsArgs.ParseInt(" \t-3x").ShouldBe(-3);
        CoreCommandsArgs.ParseInt("+5").ShouldBe(5);
        CoreCommandsArgs.ParseInt("0xff").ShouldBe(255);
        CoreCommandsArgs.ParseInt("1.9").ShouldBe(1);
        double.IsNaN(CoreCommandsArgs.ParseInt("x1")).ShouldBeTrue();
        double.IsNaN(CoreCommandsArgs.ParseInt("")).ShouldBeTrue();
        double.IsNaN(CoreCommandsArgs.ParseInt("-")).ShouldBeTrue();
    }

    // --------- selection commands

    [Fact]
    public void SelectAll_Selects_The_Whole_Model_From_The_Keyboard()
    {
        using var editor = TestCodeEditor.Create(["abc", "de", "fghi"]);
        var viewModel = editor.ViewModel;
        editor.SetSelections([S(1, 2, 1, 2), S(3, 1, 3, 2)]);

        CoreNavigationCommands.SelectAll.RunCoreEditorCommand(viewModel, new() { Source = "mouse" });
        AssertCursor(viewModel, S(1, 1, 3, 5));
        viewModel.CursorStateChangedEvents[^1].Source.ShouldBe("keyboard");
    }

    [Fact]
    public void SetSelection_Sets_One_Selection_And_Needs_One()
    {
        using var editor = TestCodeEditor.Create(["abc", "de", "fghi"]);
        var viewModel = editor.ViewModel;
        editor.SetSelections([S(1, 2, 1, 2), S(3, 1, 3, 2)]);

        CoreNavigationCommands.SetSelection.RunCoreEditorCommand(viewModel, new());
        AssertCursor(viewModel, S(1, 2, 1, 2), S(3, 1, 3, 2));

        CoreNavigationCommands.SetSelection.RunCoreEditorCommand(viewModel, new() { Selection = S(3, 3, 1, 2) });
        AssertCursor(viewModel, S(3, 3, 1, 2));
    }

    [Fact]
    public void CancelSelection_Keeps_Only_The_Primary_Cursor_Collapsed()
    {
        using var editor = TestCodeEditor.Create(["abc", "de", "fghi"]);
        var viewModel = editor.ViewModel;
        var recorder = Record(viewModel);
        editor.SetSelections([S(1, 1, 1, 3), S(3, 1, 3, 2)]);

        CoreNavigationCommands.CancelSelection.RunCoreEditorCommand(viewModel, new());
        AssertCursor(viewModel, S(1, 3, 1, 3));
        recorder.Events.ShouldNotBeEmpty();
    }

    [Fact]
    public void RemoveSecondaryCursors_Keeps_The_Primary_Selection()
    {
        using var editor = TestCodeEditor.Create(["abc", "de", "fghi"]);
        var viewModel = editor.ViewModel;
        editor.SetSelections([S(1, 1, 1, 3), S(3, 1, 3, 2)]);

        CoreNavigationCommands.RemoveSecondaryCursors.RunCoreEditorCommand(viewModel, new());
        AssertCursor(viewModel, S(1, 1, 1, 3));
    }

    [Fact]
    public void MoveTo_Does_Nothing_Without_A_Position_And_Reveals_Unless_Told_Not_To()
    {
        using var editor = TestCodeEditor.Create(["abc", "de", "fghi"]);
        var viewModel = editor.ViewModel;
        var recorder = Record(viewModel);

        CoreNavigationCommands.MoveTo.RunCoreEditorCommand(viewModel, new());
        AssertCursor(viewModel, S(1, 1, 1, 1));

        CoreNavigationCommands.MoveTo.RunCoreEditorCommand(viewModel, new() { Position = P(2, 2), RevealType = NavigationCommandRevealType.None });
        AssertCursor(viewModel, S(2, 2, 2, 2));
        recorder.Events.ShouldBeEmpty();

        CoreNavigationCommands.MoveToSelect.RunCoreEditorCommand(viewModel, new() { Position = P(3, 3) });
        AssertCursor(viewModel, S(2, 2, 3, 3));
        recorder.Events.ShouldHaveSingleItem().MinimalReveal.ShouldBeTrue();
    }

    // --------- line and page moves

    [Fact]
    public void CursorLineStart_And_CursorLineEnd_Go_To_The_Model_Line_Ends()
    {
        using var editor = TestCodeEditor.Create(["    abc", "de"]);
        var viewModel = editor.ViewModel;
        editor.SetSelections([S(1, 6, 1, 6), S(2, 2, 2, 2)]);

        // unlike CursorHome, CursorLineStart ignores the indentation
        CoreNavigationCommands.CursorLineStart.RunCoreEditorCommand(viewModel, new());
        AssertCursor(viewModel, S(1, 1, 1, 1), S(2, 1, 2, 1));

        CoreNavigationCommands.CursorLineEndSelect.RunCoreEditorCommand(viewModel, new());
        AssertCursor(viewModel, S(1, 1, 1, 8), S(2, 1, 2, 3));

        CoreNavigationCommands.CursorLineEnd.RunCoreEditorCommand(viewModel, new());
        AssertCursor(viewModel, S(1, 8, 1, 8), S(2, 3, 2, 3));

        CoreNavigationCommands.CursorLineStartSelect.RunCoreEditorCommand(viewModel, new());
        AssertCursor(viewModel, S(1, 8, 1, 1), S(2, 3, 2, 1));
    }

    [Fact]
    public void CursorPageDown_And_CursorPageUp_Move_By_The_Page_Size_Or_The_Given_One()
    {
        using var editor = TestCodeEditor.Create(FiftyLines);
        var viewModel = editor.ViewModel;

        CoreNavigationCommands.CursorPageDown.RunCoreEditorCommand(viewModel, new());
        AssertCursor(viewModel, S(4, 1, 4, 1));

        CoreNavigationCommands.CursorPageDownSelect.RunCoreEditorCommand(viewModel, new() { PageSize = 10 });
        AssertCursor(viewModel, S(4, 1, 14, 1));

        // moving up without selecting starts from the selection's start line (4)
        CoreNavigationCommands.CursorPageUp.RunCoreEditorCommand(viewModel, new() { PageSize = 0 });
        AssertCursor(viewModel, S(1, 1, 1, 1));

        CoreNavigationCommands.CursorPageDown.RunCoreEditorCommand(viewModel, new() { PageSize = 20 });
        AssertCursor(viewModel, S(21, 1, 21, 1));

        // page size 0 is the configured one (dynamicArgs.pageSize || cursorConfig.pageSize)
        CoreNavigationCommands.CursorPageUpSelect.RunCoreEditorCommand(viewModel, new() { PageSize = 0 });
        AssertCursor(viewModel, S(21, 1, 18, 1));
    }

    [Fact]
    public void CursorEnd_Sticky_Keeps_To_The_End_Of_Longer_Lines()
    {
        using var editor = TestCodeEditor.Create(["ab", "abcdef", "a"]);
        var viewModel = editor.ViewModel;

        CoreNavigationCommands.CursorEnd.RunCoreEditorCommand(viewModel, new() { Sticky = true });
        AssertCursor(viewModel, S(1, 3, 1, 3));
        CoreNavigationCommands.CursorDown.RunCoreEditorCommand(viewModel, new());
        AssertCursor(viewModel, S(2, 7, 2, 7));

        editor.SetPosition(P(1, 1));
        CoreNavigationCommands.CursorEnd.RunCoreEditorCommand(viewModel, new());
        CoreNavigationCommands.CursorDown.RunCoreEditorCommand(viewModel, new());
        AssertCursor(viewModel, S(2, 3, 2, 3));
    }

    [Fact]
    public void CursorMove_With_NoHistory_Uses_The_Programmatic_Source()
    {
        using var editor = TestCodeEditor.Create(["abc", "def"]);
        var viewModel = editor.ViewModel;

        CoreNavigationCommands.CursorMove.RunCoreEditorCommand(viewModel, new() { To = "down", Source = "keyboard" });
        viewModel.CursorStateChangedEvents[^1].Source.ShouldBe("keyboard");

        CoreNavigationCommands.CursorMove.RunCoreEditorCommand(viewModel, new() { To = "up", NoHistory = true, Source = "keyboard" });
        viewModel.CursorStateChangedEvents[^1].Source.ShouldBe("api");
        AssertCursor(viewModel, S(1, 1, 1, 1));
    }

    [Fact]
    public void CursorColumnSelectPageDown_And_PageUp()
    {
        using var editor = TestCodeEditor.Create(FiftyLines);
        var viewModel = editor.ViewModel;
        editor.SetPosition(P(2, 3));

        CoreNavigationCommands.CursorColumnSelectPageDown.RunCoreEditorCommand(viewModel, new());
        AssertCursor(viewModel, S(2, 3, 2, 3), S(3, 3, 3, 3), S(4, 3, 4, 3), S(5, 3, 5, 3));
        viewModel.GetCursorColumnSelectData().ShouldBe(new ColumnSelectData(true, 2, 2, 5, 2));

        CoreNavigationCommands.CursorColumnSelectPageUp.RunCoreEditorCommand(viewModel, new());
        AssertCursor(viewModel, S(2, 3, 2, 3));
    }

    [Fact]
    public void ColumnSelect_Needs_A_Position_A_View_Position_And_A_Mouse_Column()
    {
        using var editor = TestCodeEditor.Create(["abcdef", "abcdef", "abcdef"]);
        var viewModel = editor.ViewModel;

        CoreNavigationCommands.ColumnSelect.RunCoreEditorCommand(viewModel, new() { Position = P(2, 3), ViewPosition = P(2, 3) });
        AssertCursor(viewModel, S(1, 1, 1, 1));

        CoreNavigationCommands.ColumnSelect.RunCoreEditorCommand(viewModel, new() { Position = P(1, 2), ViewPosition = P(1, 2), MouseColumn = 2 });
        CoreNavigationCommands.ColumnSelect.RunCoreEditorCommand(viewModel, new() { Position = P(3, 4), ViewPosition = P(3, 4), MouseColumn = 4, DoColumnSelect = true });
        AssertCursor(viewModel, S(1, 2, 1, 4), S(2, 2, 2, 4), S(3, 2, 3, 4));
    }

    // --------- editing commands

    private sealed class NoViewEditor : ICoreCommandsCodeEditor
    {
        public int Calls { get; private set; }

        public IViewModel? GetViewModel() => null;

        public TextModel? GetModel() => null;

        public bool IsReadOnly => false;

        public bool PushUndoStop()
        {
            Calls++;
            return false;
        }

        public void ExecuteCommands(string? source, IReadOnlyList<ICommand?> commands) => Calls++;
    }

    [Fact]
    public async Task Commands_Do_Nothing_In_An_Editor_Without_A_View_Or_Model()
    {
        var editor = new NoViewEditor();
        CoreEditingCommands.LineBreakInsert.RunEditorCommand(editor, null);
        CoreEditingCommands.DeleteLeft.RunEditorCommand(editor, null);
        CoreNavigationCommands.CursorDown.RunEditorCommand(editor, null);
        await CoreEditingCommands.Undo.RunEditorCommand(editor, null);
        await CoreEditingCommands.Redo.RunEditorCommand(editor, null);
        editor.Calls.ShouldBe(0);
    }

    [Fact]
    public void LineBreakInsert_Breaks_The_Line_And_Keeps_The_Cursor()
    {
        using var editor = TestCodeEditor.Create(["abcdef", "ghi"]);
        var viewModel = editor.ViewModel;
        editor.SetSelections([S(1, 4, 1, 4), S(2, 2, 2, 2)]);

        CoreEditingCommands.LineBreakInsert.RunEditorCommand(editor, null);
        editor.GetValue().ShouldBe("abc\ndef\ng\nhi");
        AssertCursor(viewModel, S(1, 4, 1, 4), S(3, 2, 3, 2));
    }

    [Fact]
    public void LineBreakInsert_Replaces_The_Selection()
    {
        using var editor = TestCodeEditor.Create(["abcdef"]);
        var viewModel = editor.ViewModel;
        editor.SetSelection(S(1, 2, 1, 5));

        CoreEditingCommands.LineBreakInsert.RunEditorCommand(editor, null);
        editor.GetValue().ShouldBe("a\nef");
        AssertCursor(viewModel, S(1, 2, 1, 2));
    }

    [Fact]
    public async Task LineBreakInsert_Is_One_Undo_Step()
    {
        using var editor = TestCodeEditor.Create(["abcdef"]);
        editor.SetPosition(P(1, 4));
        editor.Type("x");

        CoreEditingCommands.LineBreakInsert.RunEditorCommand(editor, null);
        editor.GetValue().ShouldBe("abcx\ndef");

        await CoreEditingCommands.Undo.RunEditorCommand(editor, null);
        editor.GetValue().ShouldBe("abcxdef");
        await CoreEditingCommands.Undo.RunEditorCommand(editor, null);
        editor.GetValue().ShouldBe("abcdef");
        await CoreEditingCommands.Redo.RunEditorCommand(editor, null);
        editor.GetValue().ShouldBe("abcxdef");
    }

    [Fact]
    public void DeleteRight_Deletes_The_Next_Character_And_Joins_Lines()
    {
        using var editor = TestCodeEditor.Create(["abc", "def"]);
        var viewModel = editor.ViewModel;
        editor.SetPosition(P(1, 3));

        CoreEditingCommands.DeleteRight.RunEditorCommand(editor, null);
        editor.GetValue().ShouldBe("ab\ndef");
        CoreEditingCommands.DeleteRight.RunEditorCommand(editor, null);
        editor.GetValue().ShouldBe("abdef");
        AssertCursor(viewModel, S(1, 3, 1, 3));
        viewModel.GetPrevEditOperationType().ShouldBe(EditOperationType.DeletingRight);
    }

    [Fact]
    public async Task Editing_Commands_Do_Not_Edit_A_Read_Only_Editor()
    {
        using var editor = TestCodeEditor.Create(["abc"], new TestCodeEditorOptions { EditorOptions = EditorOptions.Default with { ReadOnly = true } });
        editor.SetPosition(P(1, 2));

        CoreEditingCommands.LineBreakInsert.RunEditorCommand(editor, null);
        CoreEditingCommands.Tab.RunEditorCommand(editor, null);
        CoreEditingCommands.DeleteLeft.RunEditorCommand(editor, null);
        CoreEditingCommands.DeleteRight.RunEditorCommand(editor, null);
        await CoreEditingCommands.Undo.RunEditorCommand(editor, null);
        editor.GetValue().ShouldBe("abc");
        editor.PushUndoStop().ShouldBeFalse();
    }

    // --------- the command table

    [Fact]
    public void The_Table_Lists_Every_Command_Once()
    {
        string[] ids =
        [
            CoreNavigationCommands.MoveTo.Id, CoreNavigationCommands.MoveToSelect.Id, CoreNavigationCommands.ColumnSelect.Id,
            CoreNavigationCommands.CursorColumnSelectLeft.Id, CoreNavigationCommands.CursorColumnSelectRight.Id,
            CoreNavigationCommands.CursorColumnSelectUp.Id, CoreNavigationCommands.CursorColumnSelectPageUp.Id,
            CoreNavigationCommands.CursorColumnSelectDown.Id, CoreNavigationCommands.CursorColumnSelectPageDown.Id,
            CoreNavigationCommands.CursorMove.Id, CoreNavigationCommands.CursorLeft.Id, CoreNavigationCommands.CursorLeftSelect.Id,
            CoreNavigationCommands.CursorRight.Id, CoreNavigationCommands.CursorRightSelect.Id, CoreNavigationCommands.CursorUp.Id,
            CoreNavigationCommands.CursorUpSelect.Id, CoreNavigationCommands.CursorPageUp.Id, CoreNavigationCommands.CursorPageUpSelect.Id,
            CoreNavigationCommands.CursorDown.Id, CoreNavigationCommands.CursorDownSelect.Id, CoreNavigationCommands.CursorPageDown.Id,
            CoreNavigationCommands.CursorPageDownSelect.Id, CoreNavigationCommands.CreateCursor.Id, CoreNavigationCommands.LastCursorMoveToSelect.Id,
            CoreNavigationCommands.CursorHome.Id, CoreNavigationCommands.CursorHomeSelect.Id, CoreNavigationCommands.CursorLineStart.Id,
            CoreNavigationCommands.CursorLineStartSelect.Id, CoreNavigationCommands.CursorEnd.Id, CoreNavigationCommands.CursorEndSelect.Id,
            CoreNavigationCommands.CursorLineEnd.Id, CoreNavigationCommands.CursorLineEndSelect.Id, CoreNavigationCommands.CursorTop.Id,
            CoreNavigationCommands.CursorTopSelect.Id, CoreNavigationCommands.CursorBottom.Id, CoreNavigationCommands.CursorBottomSelect.Id,
            CoreNavigationCommands.EditorScroll.Id, CoreNavigationCommands.ScrollLineUp.Id, CoreNavigationCommands.ScrollPageUp.Id,
            CoreNavigationCommands.ScrollEditorTop.Id, CoreNavigationCommands.ScrollLineDown.Id, CoreNavigationCommands.ScrollPageDown.Id,
            CoreNavigationCommands.ScrollEditorBottom.Id, CoreNavigationCommands.ScrollLeft.Id, CoreNavigationCommands.ScrollRight.Id,
            CoreNavigationCommands.WordSelect.Id, CoreNavigationCommands.WordSelectDrag.Id, CoreNavigationCommands.LastCursorWordSelect.Id,
            CoreNavigationCommands.LineSelect.Id, CoreNavigationCommands.LineSelectDrag.Id, CoreNavigationCommands.LastCursorLineSelect.Id,
            CoreNavigationCommands.LastCursorLineSelectDrag.Id, CoreNavigationCommands.CancelSelection.Id,
            CoreNavigationCommands.RemoveSecondaryCursors.Id, CoreNavigationCommands.RevealLine.Id, CoreNavigationCommands.SelectAll.Id,
            CoreNavigationCommands.SetSelection.Id,
            CoreEditingCommands.LineBreakInsert.Id, CoreEditingCommands.Outdent.Id, CoreEditingCommands.Tab.Id,
            CoreEditingCommands.DeleteLeft.Id, CoreEditingCommands.DeleteRight.Id, CoreEditingCommands.Undo.Id, CoreEditingCommands.Redo.Id,
        ];

        CoreCommandsTable.Commands.Select(c => c.Id).ShouldBe(ids);
        ids.Distinct().Count().ShouldBe(ids.Length);
        CoreCommandsTable.Get("nope").ShouldBeNull();
    }

    [Fact]
    public void The_Table_Keeps_Upstreams_Keybindings()
    {
        var cursorLeft = CoreCommandsTable.Get("cursorLeft")!.Keybindings.ShouldHaveSingleItem();
        cursorLeft.Primary.ShouldBe(15); // KeyCode.LeftArrow
        cursorLeft.When.ShouldBe("textInputFocus");
        cursorLeft.Weight.ShouldBe(0);
        cursorLeft.Mac!.Secondary.ShouldBe([256 | 32]); // WinCtrl | KeyB

        var columnLeft = CoreCommandsTable.Get("cursorColumnSelectLeft")!.Keybindings;
        columnLeft.Length.ShouldBe(2);
        columnLeft[0].Primary.ShouldBe(2048 | 1024 | 512 | 15);
        columnLeft[0].Linux!.Primary.ShouldBe(0);
        columnLeft[1].ShouldBe(columnLeft[1] with { Weight = 1, When = "editorColumnSelection && textInputFocus", Primary = 1024 | 15 });

        var cancelSelection = CoreCommandsTable.Get("cancelSelection")!;
        cancelSelection.Precondition.ShouldBe("editorHasSelection");
        cancelSelection.Keybindings[0].When.ShouldBe("editorHasSelection && textInputFocus");
        cancelSelection.Keybindings[0].Secondary.ShouldBe([1024 | 9]);
        CoreCommandsTable.Get("removeSecondaryCursors")!.Keybindings[0].Weight.ShouldBe(1);

        var tab = CoreCommandsTable.Get("tab")!;
        tab.Precondition.ShouldBe("!editorReadonly");
        tab.Keybindings[0].When.ShouldBe("editorTextFocus && !editorReadonly && !editorTabMovesFocus");
        tab.Keybindings[0].Primary.ShouldBe(2);

        CoreCommandsTable.Get("cursorEnd")!.Keybindings[0].Args.ShouldBe("{\"sticky\":false}");
        CoreCommandsTable.Get("_moveTo")!.Keybindings.ShouldBeEmpty();
        CoreCommandsTable.Get("redo")!.Keybindings[0].Mac!.Primary.ShouldBe(2048 | 1024 | 56);
    }
}
