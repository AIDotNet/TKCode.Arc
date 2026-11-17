using Avalonia.Automation;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>Tool toggles, the model pill and menu, the counter.</summary>
public class AiComposerToolbarTests
{
    #region Tools

    [AvaloniaFact]
    public void Each_Tool_Is_A_Toggle_That_Follows_It_Both_Ways()
    {
        using var rig = new AiComposerRig();
        var composer = rig.Composer;
        var toggles = composer.ToolToggles;
        toggles.Select(t => t.Label).ShouldBe(["Web search", "Extended thinking"]);
        toggles[0].Icon.ShouldBe(ArcIcons.Globe);
        AutomationProperties.GetName(toggles[0]).ShouldBe("Web search");

        rig.Click(toggles[0]);
        composer.Tools[0].IsChecked.ShouldBeTrue();
        rig.Toggled.ShouldBe([("web-search", true)]);
        rig.Announcement.ShouldBe("Web search on");
        rig.Click(toggles[0]);
        composer.Tools[0].IsChecked.ShouldBeFalse();
        rig.Announcement.ShouldBe("Web search off");

        // Code changes reach the toggle without an event or an announcement.
        composer.Tools[1].IsChecked = true;
        toggles[1].IsChecked.ShouldBe(true);
        rig.Toggled.Count.ShouldBe(2);
        composer.Tools[1].Label = "Deep thinking";
        toggles[1].Label.ShouldBe("Deep thinking");
    }

    [AvaloniaFact]
    public void Off_Screen_The_Composer_Stops_Listening_To_Its_Tools_And_Catches_Up_When_Back()
    {
        using var rig = new AiComposerRig();
        var composer = rig.Composer;
        var column = (Avalonia.Controls.Panel)composer.Parent!;
        column.Children.Remove(composer);
        rig.Pump();
        composer.Tools[0].IsChecked = true;
        composer.ToolToggles[0].IsChecked.ShouldBe(false);
        column.Children.Add(composer);
        rig.Pump();
        composer.ToolToggles[0].IsChecked.ShouldBe(true);
        composer.Tools[0].IsChecked = false;
        composer.ToolToggles[0].IsChecked.ShouldBe(false);
    }

    [AvaloniaFact]
    public void Changing_The_Tools_Rebuilds_The_Toggles()
    {
        using var rig = new AiComposerRig();
        rig.Composer.Tools.RemoveAt(0);
        rig.Composer.ToolToggles.Select(t => t.Label).ShouldBe(["Extended thinking"]);
        rig.Composer.Tools.Add(new AiComposerTool { Id = "canvas", Label = "Canvas", Icon = ArcIcons.PenLine });
        rig.Pump();
        rig.Composer.ToolsPanel!.Children.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public void Space_Toggles_A_Tool_From_The_Keyboard()
    {
        using var rig = new AiComposerRig();
        rig.Composer.ToolToggles[1].Focus(NavigationMethod.Tab);
        rig.Pump();
        rig.Key(PhysicalKey.Space);
        rig.Composer.Tools[1].IsChecked.ShouldBeTrue();
        rig.Toggled.ShouldBe([("extended-thinking", true)]);
    }

    #endregion

    #region Model

    [AvaloniaFact]
    public void The_First_Model_Is_Selected_Once_And_Written_Back_With_Its_Default_Effort()
    {
        using var rig = new AiComposerRig();
        var composer = rig.Composer;
        composer.SelectedModel.ShouldBe(composer.Models[0]);
        composer.Effort.ShouldBe(ModelEffort.Medium);
        var pill = composer.ModelPillControl!;
        pill.Appearance.ShouldBe(ModelPillAppearance.Toolbar);
        pill.Model.ShouldBe(composer.Models[0]);
        pill.Effort.ShouldBe(ModelEffort.Medium);
        AutomationProperties.GetName(pill).ShouldBe("Model: Aurora Prime 3, medium effort");
        rig.ModelChanges.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void A_Declared_Model_And_Effort_Are_Kept()
    {
        using var rig = new AiComposerRig(c =>
        {
            c.SelectedModel = c.Models[1];
            c.Effort = ModelEffort.Max;
        });
        rig.Composer.SelectedModel.ShouldBe(rig.Composer.Models[1]);
        rig.Composer.Effort.ShouldBe(ModelEffort.Max);
        rig.Composer.ModelMenu.SelectedModel.ShouldBe(rig.Composer.Models[1]);
    }

    [AvaloniaFact]
    public void Choosing_A_Model_Raises_The_Change_Snaps_The_Effort_And_Returns_The_Focus_To_The_Pill()
    {
        using var rig = new AiComposerRig(c => c.Effort = ModelEffort.Max);
        var composer = rig.Composer;
        var pill = composer.ModelPillControl!;
        // Declaring the effort reported it; only the choice below counts here.
        rig.EffortChanges.Clear();
        pill.Focus(NavigationMethod.Tab);
        rig.Pump();
        rig.Key(PhysicalKey.Enter);
        composer.ModelMenu.IsOpen.ShouldBeTrue();
        pill.IsOpen.ShouldBeTrue();
        // Selected row first (Aurora Prime 3); down twice to Cobalt Atlas 2 (three levels).
        rig.Key(PhysicalKey.ArrowDown);
        rig.Key(PhysicalKey.ArrowDown);
        rig.Key(PhysicalKey.Enter);
        composer.ModelMenu.IsOpen.ShouldBeFalse();
        composer.SelectedModel!.Id.ShouldBe("cobalt-atlas-2");
        composer.Effort.ShouldBe(ModelEffort.High);
        rig.ModelChanges.Single().NewModel!.Id.ShouldBe("cobalt-atlas-2");
        var snap = rig.EffortChanges.Single();
        snap.IsSnap.ShouldBeTrue();
        snap.NewEffort.ShouldBe(ModelEffort.High);
        rig.Announcement.ShouldBe("Model: Cobalt Atlas 2");
        rig.Focused.ShouldBe(pill);
    }

    [AvaloniaFact]
    public void An_Effort_Picked_In_The_Menu_Is_Raised_And_Announced_And_The_Menu_Stays()
    {
        using var rig = new AiComposerRig();
        var composer = rig.Composer;
        composer.ModelPillControl!.Focus(NavigationMethod.Tab);
        rig.Pump();
        rig.Key(PhysicalKey.Enter);
        // Five models, then the effort row.
        for (var i = 0; i < 5; i++)
        {
            rig.Key(PhysicalKey.ArrowDown);
        }
        rig.Key(PhysicalKey.ArrowRight);
        composer.Effort.ShouldBe(ModelEffort.High);
        composer.ModelMenu.IsOpen.ShouldBeTrue();
        var change = rig.EffortChanges.Single();
        change.IsSnap.ShouldBeFalse();
        change.OldEffort.ShouldBe(ModelEffort.Medium);
        rig.Announcement.ShouldBe("Effort: High");
        rig.Key(PhysicalKey.Escape);
        composer.ModelMenu.IsOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Code_Sets_The_Model_And_Effort_With_Events_And_An_Unsupported_Level_Snaps()
    {
        using var rig = new AiComposerRig();
        var composer = rig.Composer;
        composer.SelectedModel = composer.Models[3];
        rig.ModelChanges.Single().OldModel.ShouldBe(composer.Models[0]);
        composer.ModelPillControl!.Model.ShouldBe(composer.Models[3]);
        composer.Effort = ModelEffort.Max;
        composer.Effort.ShouldBe(ModelEffort.High);
        rig.EffortChanges.Last().NewEffort.ShouldBe(ModelEffort.High);
        composer.ModelMenu.Effort.ShouldBe(ModelEffort.High);
    }

    [AvaloniaFact]
    public void A_Null_Effort_Follows_The_Models_Default_And_A_Model_Without_Levels_Has_None()
    {
        using var rig = new AiComposerRig();
        var composer = rig.Composer;
        composer.Effort = ModelEffort.Low;
        composer.Effort = null;
        composer.Effort.ShouldBe(ModelEffort.Medium);
        composer.SelectedModel = composer.Models[4];
        composer.Effort.ShouldBeNull();
        composer.Effort = ModelEffort.High;
        composer.Effort.ShouldBeNull();
        composer.Text = "hi";
        composer.TrySend();
        rig.Sent.Single().Effort.ShouldBeNull();
        rig.Sent.Single().Model!.Id.ShouldBe("granite-70b");
    }

    [AvaloniaFact]
    public void Without_Models_The_Pill_Hides()
    {
        using var rig = new AiComposerRig();
        rig.Composer.Models.Clear();
        rig.Pump();
        rig.Composer.ModelPillControl!.IsVisible.ShouldBeFalse();
        rig.Composer.Models.AddRange(ModelMenuSamples.ComposerModels());
        rig.Pump();
        rig.Composer.ModelPillControl.IsVisible.ShouldBeTrue();
    }

    #endregion

    #region Counter

    [AvaloniaFact]
    public void The_Counter_Shows_From_80_Percent_Warns_From_95_And_Turns_Red_Past_The_Limit()
    {
        using var rig = new AiComposerRig(c => c.MaxLength = 100);
        var counter = rig.Composer.Counter!;
        counter.Max.ShouldBe(100);
        rig.Composer.Text = new string('a', 79);
        counter.IsShown.ShouldBeFalse();
        rig.Composer.Text = new string('a', 80);
        counter.IsShown.ShouldBeTrue();
        counter.Text.ShouldBe("20 left");
        AutomationProperties.GetName(counter).ShouldBe("20 of 100 characters left");
        rig.Composer.Text = new string('a', 96);
        counter.IsNear.ShouldBeTrue();
        rig.Composer.Text = new string('a', 104);
        counter.IsOver.ShouldBeTrue();
        counter.Text.ShouldBe("4 over");
        AutomationProperties.GetName(counter).ShouldBe("4 characters over the limit");
        rig.Composer.Classes.Contains(":over-limit").ShouldBeTrue();
    }

    [AvaloniaFact]
    public void No_Limit_Means_No_Counter_And_Custom_Thresholds_Apply()
    {
        using var rig = new AiComposerRig(c => c.MaxLength = null);
        rig.Composer.Text = new string('a', 5000);
        rig.Composer.Counter!.IsShown.ShouldBeFalse();
        rig.Composer.SendState.ShouldBe(AiSendState.Ready);
        rig.Composer.MaxLength = 1000;
        rig.Composer.CounterThreshold = 0.5;
        rig.Composer.Text = new string('a', 500);
        rig.Composer.Counter.IsShown.ShouldBeTrue();
        rig.Composer.Counter.Text.ShouldBe("500 left");
    }

    #endregion
}
