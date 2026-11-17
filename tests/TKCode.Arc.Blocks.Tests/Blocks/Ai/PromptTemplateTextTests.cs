namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// The text model of the prompt template editor: markers, names, normalization, the "{{" rows, word deletion and the
/// undo grouping. Sample text is our own (prompt studio spec, section 10: Northwind Kites).
/// </summary>
public class PromptTemplateTextTests
{
    private const string System = "Apply this rule: {{refund_rule}}\n- Address {{buyer_name}} once.\n- Mention ticket {{ticket_id}}.";

    [Fact]
    public void Markers_Become_Chips_With_The_Name_As_Id_And_Label()
    {
        var chips = PromptTemplateText.Parse(System);
        chips.Select(c => c.Id).ShouldBe(["refund_rule", "buyer_name", "ticket_id"]);
        chips[0].Kind.ShouldBe(PromptTemplateText.VariableKind);
        chips[0].Label.ShouldBe("refund_rule");
        System.Substring(chips[1].Start, chips[1].Length).ShouldBe("{{buyer_name}}");
        // Not markers: spaces inside, an empty or invalid name, a missing brace.
        PromptTemplateText.Parse("{{ buyer_name }} {{}} {{a-b}} {{x} {x}}").ShouldBeEmpty();
        // A third brace stays text before the marker; names may use any script and digits.
        var odd = PromptTemplateText.Parse("{{{agent_sign}}} {{客户}} {{v2}}");
        odd.Select(c => (c.Id, c.Start)).ShouldBe([("agent_sign", 1), ("客户", 17), ("v2", 24)]);
    }

    [Fact]
    public void Names_Are_Listed_Once_In_Order_Of_First_Use()
    {
        PromptTemplateText.Names("{{b}} {{a}} {{b}}").ShouldBe(["b", "a"]);
        PromptTemplateText.IsValidName("ticket_id").ShouldBeTrue();
        PromptTemplateText.IsValidName("Ticket2").ShouldBeTrue();
        PromptTemplateText.IsValidName("ticket id").ShouldBeFalse();
        PromptTemplateText.IsValidName("ticket-id").ShouldBeFalse();
        PromptTemplateText.IsValidName("").ShouldBeFalse();
    }

    [Fact]
    public void Normalizing_Removes_Blanks_Inside_The_Braces_And_Moves_The_Caret()
    {
        PromptTemplateText.Normalize("Hi {{ buyer_name }}, about {{\tticket_id}}.").ShouldBe("Hi {{buyer_name}}, about {{ticket_id}}.");
        const string clean = "Hi {{buyer_name}}";
        PromptTemplateText.Normalize(clean).ShouldBeSameAs(clean);
        // Not a name inside: left alone.
        PromptTemplateText.Normalize("{{ buyer name }}").ShouldBe("{{ buyer name }}");
        // The caret after the marker moves left by the removed blanks; one inside them lands on the name's edge.
        var text = "Hi {{ buyer_name }} ok";
        PromptTemplateText.Normalize(text, text.Length, out var end).ShouldBe("Hi {{buyer_name}} ok");
        end.ShouldBe("Hi {{buyer_name}} ok".Length);
        PromptTemplateText.Normalize(text, 6, out var inside);
        inside.ShouldBe(5);
        PromptTemplateText.Normalize(text, 2, out var before);
        before.ShouldBe(2);
    }

    [Fact]
    public void A_Query_Needs_Two_Braces()
    {
        PromptTemplateText.IsVariableQuery("Sign as {{", 9).ShouldBeTrue();
        PromptTemplateText.IsVariableQuery("Sign as {x", 8).ShouldBeFalse();
        PromptTemplateText.IsVariableQuery("{", 0).ShouldBeFalse();
    }

    [Fact]
    public void Rows_Filter_By_Substring_And_End_With_Add_For_A_New_Valid_Name()
    {
        string[] variables = ["buyer_name", "ticket_id", "refund_rule"];
        PromptTemplateText.Rows(variables, "").Select(r => r.Name).ShouldBe(variables);
        PromptTemplateText.Rows(variables, "").ShouldAllBe(r => !r.IsAdd);
        // Substring, any case; a new name gets the last row.
        PromptTemplateText.Rows(variables, "ET").ShouldBe([new PromptVariableRow("ticket_id", false), new PromptVariableRow("ET", true)]);
        PromptTemplateText.Rows(variables, "ru").ShouldBe([new PromptVariableRow("refund_rule", false), new PromptVariableRow("ru", true)]);
        // An existing name: no add row; no match: only the add row.
        PromptTemplateText.Rows(variables, "ticket_id").ShouldBe([new PromptVariableRow("ticket_id", false)]);
        PromptTemplateText.Rows(variables, "agent_sign").ShouldBe([new PromptVariableRow("agent_sign", true)]);
        // Not a name: nothing (the list closes).
        PromptTemplateText.Rows(variables, "ticket-").ShouldBeEmpty();
        PromptTemplateText.Rows(variables, "x}").ShouldBeEmpty();
        PromptTemplateText.Rows([], "").ShouldBeEmpty();
    }

    [Fact]
    public void Alt_Backspace_Deletes_A_Chip_As_One_Word()
    {
        const string text = "Mention ticket {{ticket_id}} now";
        var chips = PromptTemplateText.Parse(text);
        var chip = chips.Single();
        // Right after the chip, or after the chip and a space (deleted with it, as on macOS): the whole chip.
        PromptTemplateText.WordDeleteStart(text, chips, chip.End).ShouldBe(chip.Start);
        PromptTemplateText.WordDeleteStart(text, chips, chip.End + 1).ShouldBe(chip.Start);
        // The word before a chip stops at its start.
        PromptTemplateText.WordDeleteStart(text, chips, chip.Start).ShouldBe(chip.Start - "ticket ".Length);
        // A word ends where a chip ends.
        const string glued = "x{{ticket_id}}now";
        var gluedChips = PromptTemplateText.Parse(glued);
        PromptTemplateText.WordDeleteStart(glued, gluedChips, glued.Length).ShouldBe(gluedChips[0].End);
        // Blanks, then the word before them.
        PromptTemplateText.WordDeleteStart("refund rule  ", [], 13).ShouldBe(7);
        // Symbols are a word of their own; a line break is never crossed (unless it is what is deleted).
        PromptTemplateText.WordDeleteStart("rule:: ", [], 7).ShouldBe(4);
        PromptTemplateText.WordDeleteStart("a\nb", [], 3).ShouldBe(2);
        PromptTemplateText.WordDeleteStart("a\n", [], 2).ShouldBe(1);
        PromptTemplateText.WordDeleteStart("", [], 0).ShouldBe(0);
    }

    [Fact]
    public void The_Caret_After_Undo_Follows_The_Restored_Text()
    {
        // Undoing typed text puts the caret where it was typed; undoing a deletion puts it after the restored text.
        PromptTemplateText.CaretAfterChange("Hi there", "Hi ").ShouldBe(3);
        PromptTemplateText.CaretAfterChange("Hi ", "Hi {{buyer_name}}").ShouldBe(17);
        PromptTemplateText.CaretAfterChange("ab", "ab").ShouldBe(2);
    }

    #region Undo grouping

    private sealed class Clock
    {
        public TimeSpan Now { get; set; }

        public void Advance(int milliseconds) => Now += TimeSpan.FromMilliseconds(milliseconds);
    }

    private static (PromptUndoHistory History, Clock Clock) NewHistory()
    {
        var clock = new Clock();
        return (new PromptUndoHistory { Clock = () => clock.Now }, clock);
    }

    /// <summary>Types <paramref name="typed"/> after <paramref name="text"/>, one key every <paramref name="pause"/> ms.</summary>
    private static string Type(PromptUndoHistory history, Clock clock, string text, string typed, int pause = 100)
    {
        foreach (var c in typed)
        {
            clock.Advance(pause);
            var next = text + c;
            history.Record(text, next, PromptEditKind.Typing);
            text = next;
        }
        return text;
    }

    [Fact]
    public void Typing_Joins_One_Step_Until_A_Space_Or_A_Pause()
    {
        var (history, clock) = NewHistory();
        var text = Type(history, clock, "", "hello world");
        history.UndoCount.ShouldBe(2);
        text = history.Undo(text)!;
        text.ShouldBe("hello");
        text = history.Undo(text)!;
        text.ShouldBe("");
        history.Undo(text).ShouldBeNull();
        // Redo brings both back.
        text = history.Redo(text)!;
        text.ShouldBe("hello");
        history.Redo(text).ShouldBe("hello world");

        // A pause longer than the delay starts a step.
        (history, clock) = NewHistory();
        text = Type(history, clock, "", "refund");
        text = Type(history, clock, text, "s", pause: 1200);
        history.Undo(text).ShouldBe("refund");
        // Within the delay it does not.
        (history, clock) = NewHistory();
        text = Type(history, clock, "", "refund");
        text = Type(history, clock, text, "s", pause: 900);
        history.Undo(text).ShouldBe("");
    }

    [Fact]
    public void The_Delay_Is_Configurable_And_Other_Edits_Are_Steps_Of_Their_Own()
    {
        var (history, clock) = NewHistory();
        history.GroupDelay = TimeSpan.FromMilliseconds(300);
        var text = Type(history, clock, "", "ab");
        text = Type(history, clock, text, "c", pause: 400);
        history.UndoCount.ShouldBe(2);
        // A paste, then typing: separate steps even without a pause.
        history.Record(text, text + " {{buyer_name}}", PromptEditKind.Other);
        text += " {{buyer_name}}";
        text = Type(history, clock, text, "!", pause: 10);
        history.UndoCount.ShouldBe(4);
        // Typing somewhere else starts a step too.
        clock.Advance(10);
        history.Record(text, "X" + text, PromptEditKind.Typing);
        history.UndoCount.ShouldBe(5);
    }

    [Fact]
    public void Consecutive_Deletions_Join_And_A_New_Edit_Clears_Redo()
    {
        var (history, clock) = NewHistory();
        var text = "refund rule";
        foreach (var _ in "rule")
        {
            clock.Advance(80);
            var next = text[..^1];
            history.Record(text, next, PromptEditKind.Deleting);
            text = next;
        }
        history.UndoCount.ShouldBe(1);
        text = history.Undo(text)!;
        text.ShouldBe("refund rule");
        history.CanRedo.ShouldBeTrue();
        clock.Advance(80);
        history.Record(text, text + "s", PromptEditKind.Typing);
        history.CanRedo.ShouldBeFalse();
        // Undo ends the group in progress.
        history.Break();
        clock.Advance(10);
        history.Record(text + "s", text + "ss", PromptEditKind.Typing);
        history.UndoCount.ShouldBe(2);
    }

    #endregion
}
