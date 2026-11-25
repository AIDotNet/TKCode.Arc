// Tests for CommandExecutor and the replace commands, ported from vscode (src/vs/editor/common/cursor/cursor.ts and
// commands/replaceCommand.ts, commit b57d5c9e, MIT license). Upstream tests them through the cursor tests; these check
// the executor's own rules directly.

using Shouldly;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

[Collection(UnexpectedErrorsCollection.Name)]
public sealed class CommandExecutorTests
{
    [Fact]
    public void Replace_Command_Puts_The_Cursor_After_The_Text()
    {
        TestCommand.Run(
            ["hello world"],
            null,
            new TextSelection(1, 7, 1, 12),
            sel => new ReplaceCommand(sel.Range, "there"),
            ["hello there"],
            new TextSelection(1, 12, 1, 12));
    }

    [Fact]
    public void Replace_Command_That_Selects_Text()
    {
        TestCommand.Run(
            ["abc", "def"],
            null,
            new TextSelection(1, 2, 2, 2),
            sel => new ReplaceCommandThatSelectsText(sel.Range, "X\nY"),
            ["aX", "Yef"],
            new TextSelection(1, 2, 2, 2));
    }

    [Fact]
    public void Replace_Command_Without_Changing_Position()
    {
        TestCommand.Run(
            ["abc"],
            null,
            new TextSelection(1, 2, 1, 2),
            sel => new ReplaceCommandWithoutChangingPosition(sel.Range, "XYZ"),
            ["aXYZbc"],
            new TextSelection(1, 2, 1, 2));
    }

    [Fact]
    public void Replace_Command_With_Offset_Cursor_State()
    {
        TestCommand.Run(
            ["()"],
            null,
            new TextSelection(1, 2, 1, 2),
            sel => new ReplaceCommandWithOffsetCursorState(sel.Range, "ab", 0, -1),
            ["(ab)"],
            new TextSelection(1, 3, 1, 3));
    }

    [Fact]
    public void Replace_Overtype_Command_Types_Over_The_Next_Characters_On_The_Line()
    {
        TestCommand.Run(
            ["abcdef", "gh"],
            null,
            new TextSelection(1, 3, 1, 3),
            sel => new ReplaceOvertypeCommand(sel.Range, "XY"),
            ["abXYef", "gh"],
            new TextSelection(1, 5, 1, 5));

        // not past the end of the line
        TestCommand.Run(
            ["abc", "gh"],
            null,
            new TextSelection(1, 3, 1, 3),
            sel => new ReplaceOvertypeCommand(sel.Range, "XYZ"),
            ["abXYZ", "gh"],
            new TextSelection(1, 6, 1, 6));
    }

    [Fact]
    public void Surround_Selection_Wraps_It_And_Keeps_It_Selected()
    {
        TestCommand.Run(
            ["say hello now"],
            null,
            new TextSelection(1, 5, 1, 10),
            sel => new SurroundSelectionCommand(sel, "\"", "\""),
            ["say \"hello\" now"],
            new TextSelection(1, 6, 1, 11));

        TestCommand.Run(
            ["a", "b"],
            null,
            new TextSelection(1, 1, 2, 2),
            sel => new SurroundSelectionCommand(sel, "(", ")"),
            ["(a", "b)"],
            new TextSelection(1, 2, 2, 2));
    }

    [Fact]
    public void Composition_Surround_Selects_The_Composed_Text()
    {
        TestCommand.Run(
            ["x"],
            null,
            new TextSelection(1, 2, 1, 2),
            _ => new CompositionSurroundSelectionCommand(new TextPosition(1, 2), "ab", "'"),
            ["xab'"],
            new TextSelection(1, 2, 1, 4));
    }

    [Fact]
    public void Column_Selection_Paste_Pads_The_Block_And_Appends_Missing_Lines()
    {
        // "a", "bbb" pasted at column 3: each line at visible column 2, padded to the block width of 3
        TestCommand.Run(
            ["12345", "1", "12345"],
            null,
            new TextSelection(1, 3, 1, 3),
            sel => new ColumnSelectionPasteCommand(sel, ["a", "bbb", "cc", "d"], 4, false),
            ["12a  345", "1 bbb", "12cc 345", "  d  "],
            new TextSelection(4, 6, 4, 6));
    }

    [Fact]
    public void A_Tracked_Selection_Follows_The_Edit()
    {
        TestCommand.Run(
            ["one two three"],
            null,
            new TextSelection(1, 5, 1, 8),
            sel => new ReplaceCommandThatPreservesSelection(new TextRange(1, 1, 1, 4), "1", sel),
            ["1 two three"],
            new TextSelection(1, 3, 1, 6));
    }

    [Fact]
    public void Cursors_Whose_Edits_Overlap_Lose_And_Are_Removed()
    {
        using var model = TestTextModel.Create("abcdef");
        TextSelection[] selections = [new(1, 1, 1, 3), new(1, 2, 1, 5), new(1, 6, 1, 7)];
        ICommand[] commands = selections.Select(s => (ICommand)new ReplaceCommand(s.Range, "_")).ToArray();

        var after = CommandExecutor.ExecuteCommands(model, selections, commands)!;

        // the second cursor overlaps the first and has the higher index: it loses, its edit is dropped
        model.GetValue().ShouldBe("_cde_");
        after.Count.ShouldBe(2);
        after[0].ShouldBe(new TextSelection(1, 2, 1, 2));
        after[1].ShouldBe(new TextSelection(1, 6, 1, 6));
    }

    [Fact]
    public void Null_Commands_Keep_Their_Selection_And_No_Command_Means_No_Edit()
    {
        using var model = TestTextModel.Create("abc");
        CommandExecutor.ExecuteCommands(model, [new TextSelection(1, 1, 1, 1)], [null]).ShouldBeNull();

        var after = CommandExecutor.ExecuteCommands(
            model,
            [new TextSelection(1, 1, 1, 1), new TextSelection(1, 3, 1, 3)],
            [new ReplaceCommand(new TextRange(1, 1, 1, 1), "X"), null])!;

        model.GetValue().ShouldBe("Xabc");
        after[0].ShouldBe(new TextSelection(1, 2, 1, 2));
        after[1].ShouldBe(new TextSelection(1, 3, 1, 3)); // the selection as it was, not tracked
    }

    [Fact]
    public void A_Throwing_Command_Is_Ignored_And_Reported()
    {
        using var model = TestTextModel.Create("abc");
        var reported = new List<Exception>();
        Action<Exception> report = reported.Add;
        UnexpectedErrors.Occurred += report;
        try
        {
            var after = CommandExecutor.ExecuteCommands(
                model,
                [new TextSelection(1, 1, 1, 1), new TextSelection(1, 4, 1, 4)],
                [new ThrowingCommand(), new ReplaceCommand(new TextRange(1, 4, 1, 4), "!")])!;

            model.GetValue().ShouldBe("abc!");
            after[1].ShouldBe(new TextSelection(1, 5, 1, 5));
            reported.ShouldContain(e => e.Message == "command failed");
        }
        finally
        {
            UnexpectedErrors.Occurred -= report;
        }
    }

    [Fact]
    public async Task Undo_Restores_The_Text_And_The_Tracked_Ranges_Are_Removed()
    {
        using var model = TestTextModel.Create("one two");
        CommandExecutor.ExecuteCommands(
            model,
            [new TextSelection(1, 5, 1, 8)],
            [new ReplaceCommandThatPreservesSelection(new TextRange(1, 1, 1, 4), "1", new TextSelection(1, 5, 1, 8))]);

        model.GetValue().ShouldBe("1 two");
        model.GetAllDecorations().ShouldBeEmpty(); // the tracked range of the selection is gone

        await model.Undo();
        model.GetValue().ShouldBe("one two");
    }

    private sealed class ThrowingCommand : ICommand
    {
        public void GetEditOperations(TextModel model, IEditOperationBuilder builder)
        {
            builder.AddEditOperation(new TextRange(1, 1, 1, 1), "ignored");
            throw new InvalidOperationException("command failed");
        }

        public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper) => throw new NotSupportedException();
    }
}
