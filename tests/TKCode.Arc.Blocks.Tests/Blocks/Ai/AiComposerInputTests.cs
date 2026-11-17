using Avalonia;
using Avalonia.Automation;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The "/" and "@" lists, their keys, tokens, <c>SetPrompt</c>, <c>InsertToken</c> and <c>FocusInput</c>.</summary>
public class AiComposerInputTests
{
    #region Trigger rules

    [AvaloniaFact]
    public void Slash_Opens_The_Command_List_Only_At_The_Start_Of_A_Line()
    {
        using var rig = new AiComposerRig();
        rig.Focus();
        rig.Type("/");
        rig.ListOpen.ShouldBeTrue();
        rig.Suggest!.Pop.Kind.ShouldBe(AiSuggestionKind.Command);
        rig.Suggest.Pop.Rows.Select(r => r.Title).ShouldBe(["recap", "rewrite", "outline", "debug"]);

        rig.Composer.Text = string.Empty;
        rig.Pump();
        rig.Type("see /");
        rig.ListOpen.ShouldBeFalse();
        rig.Key(PhysicalKey.Enter, RawInputModifiers.Shift);
        rig.Type("/");
        rig.ListOpen.ShouldBeTrue();
        rig.Composer.Text.ShouldBe("see /\n/");
    }

    [AvaloniaFact]
    public void Slash_Is_Plain_Text_Without_Commands()
    {
        using var rig = new AiComposerRig(c => c.Commands.Clear());
        rig.Focus();
        rig.Type("/");
        rig.ListOpen.ShouldBeFalse();
        rig.Editor.TriggerState.ShouldBeNull();
    }

    [AvaloniaFact]
    public void At_Opens_At_A_Line_Start_Or_After_Whitespace_But_Not_After_A_Bracket_Or_Inside_A_Word()
    {
        using var rig = new AiComposerRig();
        rig.Focus();
        rig.Type("@");
        rig.ListOpen.ShouldBeTrue();
        rig.Suggest!.Pop.Kind.ShouldBe(AiSuggestionKind.Mention);

        foreach (var (typed, opens) in new[] { ("mail@", false), ("(@", false), ("see @", true), ("line\n@", true) })
        {
            rig.Composer.Text = string.Empty;
            rig.Pump();
            rig.TypeEach(typed);
            rig.ListOpen.ShouldBe(opens, typed);
        }
    }

    [AvaloniaFact]
    public void At_Is_Plain_Text_Without_Context_Items()
    {
        using var rig = new AiComposerRig(c => c.ContextItems.Clear());
        rig.Focus();
        rig.Type("@");
        rig.ListOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Commands_Filter_By_Prefix_And_No_Match_Closes_The_List()
    {
        using var rig = new AiComposerRig();
        rig.Focus();
        rig.TypeEach("/RE");
        rig.Suggest!.Pop.Rows.Select(r => r.Title).ShouldBe(["recap", "rewrite"]);
        rig.TypeEach("x");
        rig.ListOpen.ShouldBeFalse();
        rig.Composer.Text.ShouldBe("/REx");
        rig.Key(PhysicalKey.Backspace);
        rig.ListOpen.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Mentions_Match_Label_Or_Hint_Under_Files_And_Docs_And_A_Space_Ends_The_Query()
    {
        using var rig = new AiComposerRig();
        rig.Focus();
        rig.Type("@");
        rig.Suggest!.Pop.Rows.Select(r => r.Group).Distinct().ShouldBe(["Files", "Docs"]);
        rig.TypeEach("wiki");
        rig.Suggest.Pop.Rows.Single().Title.ShouldBe("Sprint 15 review");
        rig.Composer.Text = string.Empty;
        rig.Pump();
        rig.TypeEach("@sprint 1");
        rig.ListOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void MentionGroups_Set_The_Order_Of_The_Headings()
    {
        using var rig = new AiComposerRig(c => c.MentionGroups.AddRange(["Docs", "Files"]));
        rig.Focus();
        rig.Type("@");
        rig.Suggest!.Pop.Rows.Select(r => r.Group).Distinct().ShouldBe(["Docs", "Files"]);
        rig.Suggest.Pop.Rows[0].Title.ShouldBe("Sprint 15 review");
    }

    #endregion

    #region Keys and insertion

    [AvaloniaFact]
    public void Up_And_Down_Wrap_And_Enter_Inserts_A_Token_And_A_Space_Without_Sending()
    {
        using var rig = new AiComposerRig();
        rig.Focus();
        rig.Type("/");
        rig.Key(PhysicalKey.ArrowUp);
        rig.Suggest!.Pop.ActiveRow!.Title.ShouldBe("debug");
        rig.Key(PhysicalKey.ArrowDown);
        rig.Key(PhysicalKey.ArrowDown);
        rig.Suggest.Pop.ActiveRow!.Title.ShouldBe("rewrite");
        rig.EditorFocused.ShouldBeTrue();
        rig.Key(PhysicalKey.Enter);
        rig.Composer.Text.ShouldBe("/rewrite ");
        rig.Composer.Value.Tokens.Single().ShouldBe(new AiPromptToken(AiComposerTokenKind.Command, "rewrite", "rewrite", 0, 8));
        rig.ListOpen.ShouldBeFalse();
        rig.Sent.ShouldBeEmpty();
        rig.Editor.CaretIndex.ShouldBe(9);
    }

    [AvaloniaFact]
    public void Tab_Inserts_The_Active_Mention()
    {
        using var rig = new AiComposerRig();
        rig.Focus();
        rig.TypeEach("Compare @usa");
        rig.Key(PhysicalKey.Tab);
        rig.Composer.Text.ShouldBe("Compare @usage-oct.csv ");
        var token = rig.Composer.Value.Tokens.Single();
        token.Kind.ShouldBe(AiComposerTokenKind.Mention);
        token.Id.ShouldBe("file-usage-oct");
        token.Start.ShouldBe(8);
        token.Text.ShouldBe("@usage-oct.csv");
        rig.EditorFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Escape_Closes_The_List_Keeps_The_Text_And_Does_Not_Stop_A_Reply()
    {
        using var rig = new AiComposerRig(c => c.IsGenerating = true);
        rig.Focus();
        rig.TypeEach("/re");
        rig.ListOpen.ShouldBeTrue();
        rig.Key(PhysicalKey.Escape);
        rig.ListOpen.ShouldBeFalse();
        rig.Composer.Text.ShouldBe("/re");
        rig.Stops.ShouldBe(0);
        // The dismissed query stays closed while the caret stays in it.
        rig.TypeEach("w");
        rig.ListOpen.ShouldBeFalse();
        rig.Key(PhysicalKey.Escape);
        rig.Stops.ShouldBe(1);
    }

    [AvaloniaFact]
    public void A_Click_On_A_Row_Inserts_It_And_The_Text_Area_Keeps_The_Focus()
    {
        using var rig = new AiComposerRig();
        rig.Focus();
        rig.Type("@");
        var row = rig.Find<AiSuggestionRowView>(r => r.Row?.Title == "Export timeouts");
        row.ShouldNotBeNull();
        rig.Click(row);
        rig.Composer.Text.ShouldBe("@Export timeouts ");
        rig.EditorFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_List_Is_Announced_And_Its_Active_Row_Is_The_Text_Areas_Item_Status()
    {
        using var rig = new AiComposerRig();
        rig.Focus();
        rig.Type("/");
        rig.Announcement.ShouldBe("4 suggestions");
        rig.Editor.SuggestionsExpanded.ShouldBeTrue();
        AutomationProperties.GetItemStatus(rig.Editor).ShouldBe("/recap, Boil a doc or file down to its main points");
        rig.Key(PhysicalKey.ArrowDown);
        AutomationProperties.GetItemStatus(rig.Editor).ShouldBe("/rewrite, Rewrite in a clearer or friendlier tone");
        rig.TypeEach("re");
        rig.Announcement.ShouldBe("2 suggestions");
        rig.Suggest!.Pop.ListName.ShouldBe("Commands");
        rig.Key(PhysicalKey.Escape);
        rig.Editor.SuggestionsExpanded.ShouldBeFalse();
        AutomationProperties.GetItemStatus(rig.Editor).ShouldBeNull();
    }

    [AvaloniaFact]
    public void The_List_Hangs_Above_The_Line_At_The_Trigger()
    {
        using var rig = new AiComposerRig();
        rig.Focus();
        rig.TypeEach("Read @");
        var pop = rig.Suggest!.Pop;
        var surface = rig.Composer.Surface!;
        var popOrigin = pop.TranslatePoint(default, rig.Window)!.Value;
        var surfaceOrigin = surface.TranslatePoint(default, rig.Window)!.Value;
        var trigger = rig.Editor.TriggerState!.TriggerRect;
        var triggerInSurface = rig.Editor.TranslatePoint(trigger.TopLeft, surface)!.Value;
        (popOrigin.X - surfaceOrigin.X).ShouldBe(triggerInSurface.X - 12, 1);
        // The list's bottom edge sits 6px above the line.
        (popOrigin.Y + pop.Bounds.Height).ShouldBe(surfaceOrigin.Y + triggerInSurface.Y - 6, 1);
    }

    #endregion

    #region SetPrompt, InsertToken, FocusInput

    [AvaloniaFact]
    public void SetPrompt_Turns_Known_Commands_And_Mentions_Into_Tokens_And_Focuses_The_End()
    {
        using var rig = new AiComposerRig();
        rig.Composer.SetPrompt("/recap @sprint 15 review and list what slipped, then /debug it\n/outline @nobody");
        rig.Pump();
        rig.Composer.Text.ShouldBe("/recap @Sprint 15 review and list what slipped, then /debug it\n/outline @nobody");
        var tokens = rig.Composer.Value.Tokens;
        tokens.Select(t => (t.Kind, t.Id)).ShouldBe([
            (AiComposerTokenKind.Command, "recap"),
            (AiComposerTokenKind.Mention, "doc-sprint-15"),
            (AiComposerTokenKind.Command, "outline")
        ]);
        tokens[1].Start.ShouldBe(7);
        tokens[1].End.ShouldBe(7 + "@Sprint 15 review".Length);
        rig.EditorFocused.ShouldBeTrue();
        rig.Editor.CaretIndex.ShouldBe(rig.Composer.Text.Length);
        rig.Editor.Tokens.Count.ShouldBe(3);
    }

    [AvaloniaFact]
    public void SetPrompt_Fills_The_Value_Before_The_Template()
    {
        var composer = AiComposerSamples.Create();
        composer.SetPrompt(AiComposerSamples.Suggestions[2].Prompt);
        composer.Text.ShouldBe("/debug @export-worker.ts large CSV exports stall at the last chunk");
        composer.Value.Tokens.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public void InsertToken_Goes_At_The_Caret_Or_Replaces_The_Open_Query()
    {
        using var rig = new AiComposerRig();
        rig.Focus();
        rig.Type("Look at");
        rig.Composer.InsertToken(AiComposerTokenKind.Mention, "doc-export-timeouts", "Export timeouts");
        rig.Pump();
        rig.Composer.Text.ShouldBe("Look at @Export timeouts ");
        rig.TypeEach("@exp");
        rig.Composer.InsertToken(AiComposerTokenKind.Mention, "file-export-worker", "export-worker.ts");
        rig.Pump();
        rig.Composer.Text.ShouldBe("Look at @Export timeouts @export-worker.ts ");
        rig.Composer.Value.Tokens.Select(t => t.Id).ShouldBe(["doc-export-timeouts", "file-export-worker"]);
    }

    [AvaloniaFact]
    public void FocusInput_Puts_The_Focus_In_The_Text_Area()
    {
        using var rig = new AiComposerRig();
        rig.EditorFocused.ShouldBeFalse();
        rig.Composer.FocusInput();
        rig.Pump();
        rig.EditorFocused.ShouldBeTrue();
        rig.Composer.Classes.Contains(":focus-within").ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Press_On_The_Surface_Padding_Focuses_The_Text_But_Not_On_The_Toolbar()
    {
        using var rig = new AiComposerRig();
        var surface = rig.Composer.Surface!;
        var corner = surface.TranslatePoint(new Avalonia.Point(surface.Bounds.Width / 2, 4), rig.Window)!.Value;
        rig.Click(corner);
        rig.EditorFocused.ShouldBeTrue();

        rig.Send.Focus();
        rig.Pump();
        var toolbarGap = rig.Composer.ToolToggles[^1];
        var blank = toolbarGap.TranslatePoint(new Avalonia.Point(toolbarGap.Bounds.Width + 60, 18), rig.Window)!.Value;
        rig.Click(blank);
        rig.EditorFocused.ShouldBeFalse();
    }

    #endregion
}
