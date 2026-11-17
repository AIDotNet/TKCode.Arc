using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// Prompt studio: the draft and the versions, the effective variables, test cases and values, models and effort, the
/// wiring between fields and chips, the tabs, the token estimate, the header, the card's breakpoints, words and names.
/// Sample data is our own (spec section 10: Northwind Kites).
/// </summary>
public class PromptStudioTests
{
    #region Draft and versions

    [AvaloniaFact]
    public void The_Draft_Starts_As_The_Open_Version_And_The_Editors_Show_It()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        studio.OpenVersion.ShouldBeNull();
        studio.EffectiveOpenVersion!.Id.ShouldBe("v4");
        studio.Draft.ShouldBe(new PromptTemplate(PromptStudioSamples.SystemV4, PromptStudioSamples.UserV4));
        studio.SystemEditor!.Text.ShouldBe(PromptStudioSamples.SystemV4);
        studio.UserEditor!.Text.ShouldBe(PromptStudioSamples.UserV4);
        studio.VersionPill!.Label.ShouldBe("v4");
        studio.VersionPill.IsProduction.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Opening_Another_Version_Loads_Its_Text_And_The_Pill_Says_Latest()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        var versions = studio.Versions;
        studio.ProductionVersion = versions[0];
        studio.OpenVersion = versions[1];
        rig.Pump();
        studio.Draft.System.ShouldBe(PromptStudioSamples.SystemV3);
        studio.SystemEditor!.Text.ShouldBe(PromptStudioSamples.SystemV3);
        studio.VersionPill!.Label.ShouldBe("v3");
        studio.VersionPill.IsProduction.ShouldBeFalse();
        studio.VersionMenu!.OpenVersion.ShouldBe(versions[1]);
        studio.VersionMenu.ProductionVersion.ShouldBe(versions[0]);
    }

    [AvaloniaFact]
    public void Production_Follows_The_Open_Version_Until_It_Is_Set()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        studio.OpenVersion = studio.Versions[2];
        rig.Pump();
        studio.EffectiveProductionVersion.ShouldBe(studio.Versions[2]);
        studio.VersionPill!.IsProduction.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Draft_The_Host_Set_Stays_When_The_Versions_Arrive()
    {
        var studio = new PromptStudio { Draft = new PromptTemplate("Hello {{who}}", "Hi") };
        foreach (var version in PromptStudioSamples.Versions())
        {
            studio.Versions.Add(version);
        }
        var window = TestHelpers.Show(studio);
        try
        {
            studio.Draft.ShouldBe(new PromptTemplate("Hello {{who}}", "Hi"));
            studio.EffectiveVariables.ShouldBe(["who"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Versions_That_Arrive_Later_Fill_An_Untouched_Draft()
    {
        var studio = new PromptStudio();
        var window = TestHelpers.Show(studio);
        try
        {
            studio.Draft.System.ShouldBe(string.Empty);
            foreach (var version in PromptStudioSamples.Versions())
            {
                studio.Versions.Add(version);
            }
            studio.Draft.System.ShouldBe(PromptStudioSamples.SystemV4);
            studio.SystemEditor!.Text.ShouldBe(PromptStudioSamples.SystemV4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Editing_Updates_The_Draft_And_Raises_TemplateChanged()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        var changes = new List<PromptTemplate>();
        studio.TemplateChanged += (_, e) => changes.Add(e.Template);
        var editor = studio.SystemEditor!;
        editor.Focus();
        editor.Select(0, 0);
        rig.Type("Hi. ");
        studio.Draft.System.ShouldBe("Hi. " + PromptStudioSamples.SystemV4);
        studio.Draft.User.ShouldBe(PromptStudioSamples.UserV4);
        changes.Count.ShouldBe(4);
        changes[^1].ShouldBe(studio.Draft);
    }

    [AvaloniaFact]
    public void Setting_The_Draft_Shows_It_Without_TemplateChanged()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        var changes = 0;
        studio.TemplateChanged += (_, _) => changes++;
        studio.Draft = new PromptTemplate("Use {{ tone }} words.", "Go");
        rig.Pump();
        // The editor tidies the marker; the draft takes the tidy form.
        studio.SystemEditor!.Text.ShouldBe("Use {{tone}} words.");
        studio.Draft.System.ShouldBe("Use {{tone}} words.");
        studio.UserEditor!.Text.ShouldBe("Go");
        changes.ShouldBe(0);
    }

    #endregion

    #region Variables and values

    [AvaloniaFact]
    public void Effective_Variables_Are_The_List_Then_New_Names_In_Order_Of_Appearance()
    {
        using var rig = new PromptStudioRig(studio =>
        {
            studio.Variables.Clear();
            studio.Variables.Add("ticket_id");
            studio.Variables.Add("tone");
        });
        var studio = rig.Studio;
        studio.EffectiveVariables.ShouldBe(["ticket_id", "tone", "refund_rule", "buyer_name", "agent_sign"]);
        studio.Fields!.Fields.Select(f => f.VariableName).ShouldBe(studio.EffectiveVariables);
        // The tones follow the effective order (the fifth wraps to the first series color).
        studio.Fields.Fields[4].Tone.ShouldBe(1);
        studio.SystemEditor!.ToneOf("agent_sign").ShouldBe(1);
        studio.SystemEditor.ToneOf("refund_rule").ShouldBe(3);

        studio.Draft = new PromptTemplate("{{refund_rule}}", "{{order}}");
        rig.Pump();
        studio.EffectiveVariables.ShouldBe(["ticket_id", "tone", "refund_rule", "order"]);
        studio.Fields.Fields.Count.ShouldBe(4);
    }

    [AvaloniaFact]
    public void The_Effective_List_Changes_In_Place_So_Fields_Keep_Their_Focus()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        studio.Fields!.FocusField("ticket_id").ShouldBeTrue();
        var field = studio.Fields.FieldOf("ticket_id");
        studio.Draft = new PromptTemplate(studio.Draft.System + " {{tracking_url}}", studio.Draft.User);
        rig.Pump();
        studio.EffectiveVariables[^1].ShouldBe("tracking_url");
        studio.Fields.FieldOf("ticket_id").ShouldBeSameAs(field);
        field!.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_First_Test_Case_Loads_Its_Values()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        studio.SelectedTestCase!.Id.ShouldBe("spar");
        studio.Values["buyer_name"].ShouldBe("Mira Okafor");
        studio.Values.ContainsKey("agent_sign").ShouldBeFalse();
        studio.Cases!.SelectedCase.ShouldBe(studio.SelectedTestCase);
        studio.Fields!.FieldOf("buyer_name")!.Text.ShouldBe("Mira Okafor");
    }

    [AvaloniaFact]
    public void Choosing_A_Test_Case_Resets_The_Values_And_Drops_Edits()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        studio.Values["buyer_name"] = "Someone";
        studio.Values["agent_sign"] = "Kai";
        rig.Pump();
        rig.Click(studio.Cases!.Options[1]);
        studio.SelectedTestCase!.Id.ShouldBe("late");
        studio.Values["buyer_name"].ShouldBe("Tomas Lindqvist");
        studio.Values.ContainsKey("agent_sign").ShouldBeFalse();
        studio.Fields!.FieldOf("agent_sign")!.Text.ShouldBe(string.Empty);
        studio.LastAnnouncement.ShouldBe("Late parcel test case loaded");
    }

    [AvaloniaFact]
    public void Empty_Values_Make_Dashed_Chips()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        studio.MissingVariableNames.ShouldBe(["agent_sign"]);
        studio.SystemEditor!.Renderer.Missing.ShouldBe(["agent_sign"]);
        studio.Values["agent_sign"] = "Kai";
        studio.Values["ticket_id"] = string.Empty;
        rig.Pump();
        studio.MissingVariableNames.ShouldBe(["ticket_id"]);
        studio.SystemEditor.Renderer.Missing.ShouldBe(["ticket_id"]);
        studio.UserEditor!.Renderer.Missing.ShouldBe(["ticket_id"]);
    }

    [AvaloniaFact]
    public void Typing_In_A_Field_Writes_The_Value_And_Clears_The_Dash()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        studio.Fields!.FocusField("agent_sign").ShouldBeTrue();
        rig.Type("Kai");
        studio.Values["agent_sign"].ShouldBe("Kai");
        studio.MissingVariableNames.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void A_Focused_Field_Highlights_Its_Chips_And_Each_Key_Flashes_Them()
    {
        var frames = new ManualFrameClock();
        using var rig = new PromptStudioRig(level: ArcMotionLevel.Full, frames: frames);
        var studio = rig.Studio;
        studio.Fields!.FocusField("ticket_id").ShouldBeTrue();
        rig.Pump();
        studio.SystemEditor!.ActiveVariable.ShouldBe("ticket_id");
        studio.UserEditor!.ActiveVariable.ShouldBe("ticket_id");
        rig.Type("9");
        studio.SystemEditor.Renderer.FlashAmount("ticket_id").ShouldBeGreaterThan(0.5);
        studio.SystemEditor.Renderer.FlashAmount("buyer_name").ShouldBe(0);
        studio.Fields.FocusField("buyer_name");
        rig.Pump();
        studio.SystemEditor.ActiveVariable.ShouldBe("buyer_name");
    }

    [AvaloniaFact]
    public void Clicking_A_Chip_Focuses_Its_Field_With_The_Caret_At_The_End()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        studio.SystemEditor!.RaiseEvent(new PromptChipEventArgs(PromptTemplateEditor.ChipInvokedEvent, "refund_rule"));
        rig.Pump();
        var field = studio.Fields!.FieldOf("refund_rule")!;
        field.IsFocused.ShouldBeTrue();
        field.CaretIndex.ShouldBe(field.Text!.Length);
    }

    [AvaloniaFact]
    public void A_Name_Typed_In_The_Editor_Becomes_A_Variable_With_A_Field()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        var added = new List<string>();
        studio.VariableAdded += (_, e) => added.Add(e.Name);
        var editor = studio.SystemEditor!;
        editor.Focus();
        var end = editor.Text.Length;
        editor.Select(end, end);
        rig.Pump();
        rig.Type(" {{tracking_url}}");
        rig.Pump();
        studio.EffectiveVariables[^1].ShouldBe("tracking_url");
        studio.Fields!.FieldOf("tracking_url").ShouldNotBeNull();
        added.ShouldBe(["tracking_url"]);
        studio.LastAnnouncement.ShouldBe("Variable tracking_url added");
        studio.Variables.ShouldNotContain("tracking_url");
    }

    #endregion

    #region Models

    [AvaloniaFact]
    public void The_Selected_Model_Brings_Its_Default_Effort()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        studio.SelectedModel!.Id.ShouldBe("atlas-medium");
        studio.Effort.ShouldBe(ModelEffort.High);
        studio.Effort = ModelEffort.Max;
        studio.SelectedModel = studio.Models[2];
        studio.Effort.ShouldBe(ModelEffort.Medium);
        studio.SelectedModel = studio.Models[3];
        studio.Effort.ShouldBe(ModelEffort.Low);
        rig.Pump();
        studio.ModelPill!.Model.ShouldBe(studio.Models[3]);
        studio.ModelPill.Effort.ShouldBe(ModelEffort.Low);
        studio.ModelMenu!.SelectedModel.ShouldBe(studio.Models[3]);
    }

    [AvaloniaFact]
    public void Without_A_Selection_The_First_Model_Is_Used()
    {
        var studio = new PromptStudio();
        foreach (var model in PromptStudioSamples.Models())
        {
            studio.Models.Add(model);
        }
        studio.SelectedModel!.Id.ShouldBe("atlas-large");
        studio.Effort.ShouldBe(ModelEffort.High);
    }

    [AvaloniaFact]
    public void An_Effort_Set_Before_The_Models_Is_Kept()
    {
        var studio = new PromptStudio { Effort = ModelEffort.Low };
        foreach (var model in PromptStudioSamples.Models())
        {
            studio.Models.Add(model);
        }
        studio.Effort.ShouldBe(ModelEffort.Low);
    }

    [AvaloniaFact]
    public void A_Model_Without_Effort_Shows_None()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        studio.SelectedModel = studio.Models[4];
        rig.Pump();
        studio.ModelPill!.Effort.ShouldBeNull();
        studio.ModelMenu!.Effort.ShouldBeNull();
    }

    #endregion

    #region Tabs and editor

    [AvaloniaFact]
    public void The_Tabs_Show_One_Editor_And_The_Other_Is_Inert()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        var system = studio.SystemEditor!;
        var user = studio.UserEditor!;
        system.Opacity.ShouldBe(1);
        system.IsEnabled.ShouldBeTrue();
        user.Opacity.ShouldBe(0);
        user.IsEnabled.ShouldBeFalse();
        user.IsHitTestVisible.ShouldBeFalse();
        rig.Click(studio.Tabs!.UserTab!);
        studio.ActivePart.ShouldBe(PromptPart.User);
        user.Opacity.ShouldBe(1);
        user.IsEnabled.ShouldBeTrue();
        system.IsEnabled.ShouldBeFalse();
        studio.Tabs.UserTab!.IsSelected.ShouldBeTrue();
        KeyboardNavigation.GetIsTabStop(studio.Tabs.UserTab).ShouldBeTrue();
        KeyboardNavigation.GetIsTabStop(studio.Tabs.SystemTab!).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Editor_Slot_Is_As_High_As_The_Higher_Part()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        var stack = (Control)studio.SystemEditor!.Parent!;
        var height = stack.Bounds.Height;
        height.ShouldBe(studio.SystemEditor.Bounds.Height, 0.5);
        studio.ActivePart = PromptPart.User;
        rig.Pump();
        stack.Bounds.Height.ShouldBe(height, 0.5);
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_Between_The_Tabs_And_Activate_Them()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        studio.Tabs!.SystemTab!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.ArrowRight);
        studio.ActivePart.ShouldBe(PromptPart.User);
        rig.FocusIsIn(studio.Tabs.UserTab!).ShouldBeTrue();
        rig.Key(PhysicalKey.ArrowRight);
        studio.ActivePart.ShouldBe(PromptPart.System);
        rig.Key(PhysicalKey.End);
        studio.ActivePart.ShouldBe(PromptPart.User);
        rig.Key(PhysicalKey.Home);
        studio.ActivePart.ShouldBe(PromptPart.System);
    }

    [AvaloniaFact]
    public void The_Tab_Highlight_Sits_Under_The_Active_Tab()
    {
        using var rig = new PromptStudioRig();
        var tabs = rig.Studio.Tabs!;
        var highlight = tabs.Highlight!;
        highlight.HasTarget.ShouldBeTrue();
        highlight.CurrentRect.Width.ShouldBe(tabs.SystemTab!.Bounds.Width, 0.5);
        rig.Studio.ActivePart = PromptPart.User;
        rig.Pump();
        highlight.CurrentRect.X.ShouldBe(tabs.UserTab!.Bounds.X, 0.5);
        highlight.CurrentRect.Width.ShouldBe(tabs.UserTab.Bounds.Width, 0.5);
    }

    [AvaloniaFact]
    public void At_Full_Motion_The_Panes_Crossfade_And_The_Highlight_Glides()
    {
        var frames = new ManualFrameClock();
        using var rig = new PromptStudioRig(level: ArcMotionLevel.Full, frames: frames);
        var studio = rig.Studio;
        rig.Frame(30);
        var from = studio.Tabs!.Highlight!.CurrentRect.X;
        studio.ActivePart = PromptPart.User;
        rig.Pump(1);
        rig.Frame(3);
        studio.SystemEditor!.Opacity.ShouldBeInRange(0.01, 0.99);
        studio.UserEditor!.Opacity.ShouldBeInRange(0.01, 0.99);
        studio.Tabs.Highlight.CurrentRect.X.ShouldBeInRange(from + 0.5, studio.Tabs.UserTab!.Bounds.X - 0.5);
        rig.Frame(40);
        studio.SystemEditor.Opacity.ShouldBe(0);
        studio.UserEditor.Opacity.ShouldBe(1);
        studio.Tabs.Highlight.CurrentRect.X.ShouldBe(studio.Tabs.UserTab.Bounds.X, 0.5);
    }

    [AvaloniaFact]
    public void The_Token_Estimate_Counts_The_Shown_Part_With_Its_Values()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        var rendered = PromptStudioText.Render(PromptStudioSamples.SystemV4, studio.Values);
        studio.TokenCount!.Count.ShouldBe((rendered.Length + 3) / 4);
        studio.ActivePart = PromptPart.User;
        studio.TokenCount.Count.ShouldBe((PromptStudioText.Render(PromptStudioSamples.UserV4, studio.Values).Length + 3) / 4);
        studio.TokenEstimator = text => text.Split(' ').Length;
        studio.TokenCount.Count.ShouldBe(PromptStudioText.Render(PromptStudioSamples.UserV4, studio.Values).Split(' ').Length);
        studio.Values["buyer_name"] = "A";
        studio.TokenCount.Count.ShouldBe(PromptStudioText.Render(PromptStudioSamples.UserV4, studio.Values).Split(' ').Length);
    }

    [AvaloniaFact]
    public void Rendering_Fills_Values_And_Empties_Missing_Ones()
    {
        var values = new Dictionary<string, string> { ["a"] = "one", ["b"] = string.Empty };
        PromptStudioText.Render("x {{a}} {{b}} {{c}} {{ a }}", values).ShouldBe("x one   {{ a }}");
        PromptStudioText.EstimateTokens(string.Empty).ShouldBe(0);
        PromptStudioText.EstimateTokens("abcde").ShouldBe(2);
    }

    #endregion

    #region Header and layout

    [AvaloniaFact]
    public void The_Header_Shows_The_Title_And_Both_Buttons()
    {
        using var rig = new PromptStudioRig(width: 1000);
        var studio = rig.Studio;
        var title = TestHelpers.Descendant<TextBlock>(studio, PromptStudio.TitlePart);
        title.Text.ShouldBe("Refund reply");
        title.FontSize.ShouldBe(16);
        var header = TestHelpers.Descendant<Border>(studio, PromptStudio.HeaderPart);
        var row = studio.HeaderRow!;
        var titleGroup = (Control)row.Children[0];
        // One row when the title group and the model button fit with 12 between them (the headless font is wide).
        var fits = titleGroup.DesiredSize.Width + 12 + studio.ModelPill!.DesiredSize.Width <= row.Bounds.Width + 0.01;
        row.IsWrapped.ShouldBe(!fits);
        header.Bounds.Height.ShouldBe(fits ? 61 : 101, 0.6);
        var model = rig.BoundsOf(studio.ModelPill);
        if (fits)
        {
            model.Right.ShouldBe(rig.BoundsOf(header).Right - 16, 0.6);
        }
    }

    [AvaloniaFact]
    public void The_Header_Row_Wraps_Only_When_The_Two_Groups_Do_Not_Fit()
    {
        var title = new Border { Width = 200, Height = 32 };
        var model = new Border { Width = 160, Height = 32 };
        var row = new PromptStudioHeaderPanel { ColumnGap = 12, RowGap = 8, Children = { title, model } };
        var host = new Border { Width = 372, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left, Child = row };
        var window = TestHelpers.Show(host);
        try
        {
            window.UpdateLayout();
            row.IsWrapped.ShouldBeFalse();
            row.Bounds.Height.ShouldBe(32);
            model.Bounds.X.ShouldBe(212);
            host.Width = 371;
            window.UpdateLayout();
            row.IsWrapped.ShouldBeTrue();
            row.Bounds.Height.ShouldBe(72);
            model.Bounds.Position.ShouldBe(new Point(0, 40));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Card_Is_Surface_With_A_Border_And_At_Most_760_Wide()
    {
        using var rig = new PromptStudioRig(width: 1000);
        var studio = rig.Studio;
        studio.Bounds.Width.ShouldBe(760, 0.5);
        var card = TestHelpers.Descendant<Border>(studio, PromptStudio.CardPart);
        TestHelpers.ColorOf(card.Background).ShouldBe(ArcTokens.GetColor(studio, ArcDesignTokenKind.Surface));
        TestHelpers.ColorOf(card.BorderBrush).ShouldBe(ArcTokens.GetColor(studio, ArcDesignTokenKind.Border));
        card.CornerRadius.TopLeft.ShouldBe(34);
    }

    [AvaloniaTheory]
    [InlineData(680, false, false, false, false)]
    [InlineData(560, false, false, true, false)]
    [InlineData(520, true, false, true, false)]
    [InlineData(480, true, true, true, false)]
    [InlineData(340, true, true, false, true)]
    public void The_Breakpoints_Follow_The_Card_Width(double card, bool compact, bool narrow, bool twoColumns, bool oneColumn)
    {
        using var rig = new PromptStudioRig(width: card + 40);
        var studio = rig.Studio;
        studio.Bounds.Width.ShouldBe(card, 0.5);
        studio.Classes.Contains(PromptStudio.CompactPseudoClass).ShouldBe(compact);
        studio.Classes.Contains(PromptStudio.NarrowPseudoClass).ShouldBe(narrow);
        studio.Classes.Contains(PromptStudio.TwoColumnPseudoClass).ShouldBe(twoColumns);
        studio.Classes.Contains(PromptStudio.OneColumnPseudoClass).ShouldBe(oneColumn);
        studio.Fields!.ColumnCount.ShouldBe(oneColumn ? 1 : twoColumns ? 2 : 4);
        var vars = TestHelpers.Descendant<Border>(studio, PromptStudio.VarsPart);
        vars.Padding.Left.ShouldBe(compact ? 12 : 16);
        var header = TestHelpers.Descendant<Border>(studio, PromptStudio.HeaderPart);
        header.Padding.Left.ShouldBe(compact ? 24 : 28);
        studio.Output!.Padding.Left.ShouldBe(compact ? 12 : 16);
        var run = studio.RunButton!;
        var cases = studio.Cases!;
        if (narrow)
        {
            // Stacked: the cases on one row, Run under them, both the full width.
            rig.BoundsOf(run).Top.ShouldBeGreaterThan(rig.BoundsOf(cases).Bottom);
            run.Bounds.Width.ShouldBe(cases.Bounds.Width, 0.5);
            cases.Classes.Contains(PromptTestCases.StretchedPseudoClass).ShouldBeTrue();
        }
        else
        {
            rig.BoundsOf(run).Top.ShouldBe(rig.BoundsOf(cases).Top, 0.5);
            rig.BoundsOf(run).Right.ShouldBe(rig.BoundsOf(vars).Right - (compact ? 12 : 16), 0.5);
        }
    }

    [AvaloniaFact]
    public void On_A_Narrow_Card_The_Model_Button_Wraps_Under_The_Title()
    {
        using var rig = new PromptStudioRig(width: 380);
        var studio = rig.Studio;
        studio.HeaderRow!.IsWrapped.ShouldBeTrue();
        var title = rig.BoundsOf(studio.VersionPill!);
        var model = rig.BoundsOf(studio.ModelPill!);
        model.Top.ShouldBe(title.Bottom + 8, 0.6);
        var header = TestHelpers.Descendant<Border>(studio, PromptStudio.HeaderPart);
        header.Bounds.Height.ShouldBe(101, 0.6);
    }

    #endregion

    #region Keyboard and motion

    [AvaloniaFact]
    public void Tab_Goes_Through_The_Card_In_Reading_Order()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        studio.VersionPill!.Focus(NavigationMethod.Tab);
        var stops = new List<string>();
        for (var i = 0; i < 9; i++)
        {
            rig.Key(PhysicalKey.Tab);
            stops.Add(rig.Focused switch
            {
                PromptModelPill => "model",
                PromptPartTab tab => "tab:" + tab.Part,
                PromptVariableField field => "field:" + field.VariableName,
                PromptTestCaseOption option => "case:" + option.Case?.Id,
                PromptRunButton => "run",
                Visual visual when studio.SystemEditor!.IsVisualAncestorOf(visual) => "system editor",
                Visual visual when studio.UserEditor!.IsVisualAncestorOf(visual) => "user editor",
                var other => other?.GetType().Name ?? "none"
            });
        }
        stops.ShouldBe(["model", "tab:System", "system editor", "field:buyer_name", "field:ticket_id", "field:refund_rule",
            "field:agent_sign", "case:spar", "run"]);
    }

    [AvaloniaFact]
    public void At_Reduced_Motion_The_Panes_Fade_Briefly_And_The_Highlight_Jumps()
    {
        var frames = new ManualFrameClock();
        using var rig = new PromptStudioRig(level: ArcMotionLevel.Reduced, frames: frames);
        var studio = rig.Studio;
        rig.Frame(20);
        studio.ActivePart = PromptPart.User;
        rig.Pump(1);
        rig.Frame(3);
        // 120 ms linear opacity, no movement: the highlight is already under the user tab.
        studio.UserEditor!.Opacity.ShouldBeInRange(0.2, 0.6);
        studio.Tabs!.Highlight!.CurrentRect.X.ShouldBe(studio.Tabs.UserTab!.Bounds.X, 0.5);
        rig.Frame(10);
        studio.UserEditor.Opacity.ShouldBe(1);
        studio.SystemEditor!.Opacity.ShouldBe(0);
    }

    #endregion

    #region Words and names

    [AvaloniaFact]
    public void Screen_Readers_Get_Names_From_The_Labels()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        AutomationProperties.GetName(studio).ShouldBe("Refund reply prompt");
        AutomationProperties.GetName(studio.Tabs!).ShouldBe("Prompt part");
        AutomationProperties.GetName(studio.Tabs!.SystemTab!).ShouldBe("System");
        studio.SystemEditor!.AccessibleName.ShouldBe("System prompt");
        studio.UserEditor!.AccessibleName.ShouldBe("User prompt");
        AutomationProperties.GetHelpText(studio.SystemEditor).ShouldBe("Type {{ to add a variable");
        AutomationProperties.GetName(studio.RunButton!).ShouldBe("Run prompt");
        AutomationProperties.GetHelpText(studio.RunButton!).ShouldBe($"Run ({PromptKbd.ModifierName}+Enter)");
        AutomationProperties.GetName(studio.Fields!).ShouldBe("Variables");
        AutomationProperties.GetName(studio.Cases!).ShouldBe("Test case");
        studio.VersionMenu!.DialogName.ShouldBe("Versions");
        studio.ModelMenu!.DialogName.ShouldBe("Model and effort");
        AutomationProperties.GetName(studio.VersionPill!).ShouldBe("Version v4, in production");
    }

    [AvaloniaFact]
    public void New_Labels_Reword_Everything()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        studio.Labels = new PromptStudioLabels
        {
            SystemTab = "Système",
            Run = "Lancer",
            EmptyValue = "Vide",
            CardName = title => $"Invite {title}",
            Idle = label => $"Lancez {label}.",
            RunsAreSimulated = string.Empty
        };
        rig.Pump();
        studio.Tabs!.SystemTab!.Text.ShouldBe("Système");
        studio.RunButton!.RunText.ShouldBe("Lancer");
        studio.Fields!.EmptyText.ShouldBe("Vide");
        AutomationProperties.GetName(studio).ShouldBe("Invite Refund reply");
        studio.Output!.IdleText.ShouldBe("Lancez Broken spar.");
    }

    [AvaloniaFact]
    public void The_Hint_Line_Shows_The_Key_Cap()
    {
        using var rig = new PromptStudioRig();
        var hint = (StackPanel)rig.Studio.Hint!.Content!;
        hint.Children.OfType<TextBlock>().Select(t => t.Text).ShouldBe(["Type", "to add a variable"]);
        hint.Children.OfType<PromptKbd>().Single().Text.ShouldBe("{{");
    }

    #endregion
}
