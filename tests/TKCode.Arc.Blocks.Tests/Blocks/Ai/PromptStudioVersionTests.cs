using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// Prompt studio's versions and models: the compare view (diff, counts, hint, tab dots, leaving it), promote and undo
/// (optimistic, rollback on Cancel, swapped arguments), the header buttons and their menus, focus and announcements.
/// </summary>
public class PromptStudioVersionTests
{
    #region Compare view

    [AvaloniaFact]
    public void Compare_Shows_The_Word_Diff_Of_The_Draft_Against_The_Version()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        var v3 = studio.Versions[1];
        studio.Compare(v3);
        rig.Pump();
        studio.IsCompareMode.ShouldBeTrue();
        studio.CompareVersion.ShouldBe(v3);
        studio.Classes.Contains(PromptStudio.ComparePseudoClass).ShouldBeTrue();
        var diff = studio.Diff!;
        diff.IsVisible.ShouldBeTrue();
        diff.Before.ShouldBe(PromptStudioSamples.SystemV3);
        diff.After.ShouldBe(PromptStudioSamples.SystemV4);
        diff.Appearance.ShouldBe(AiDiffAppearance.Studio);
        diff.InsertedWords.ShouldBeGreaterThan(0);
        studio.AddedText!.Text.ShouldBe("+" + diff.InsertedWords);
        studio.RemovedText!.Text.ShouldBe("−" + diff.DeletedWords);
        // Both editors are inert while the diff shows.
        studio.SystemEditor!.IsEnabled.ShouldBeFalse();
        studio.UserEditor!.IsEnabled.ShouldBeFalse();
        studio.SystemEditor.Opacity.ShouldBe(0);
        // The corner and the hint line change.
        studio.TokenCount!.IsVisible.ShouldBeFalse();
        studio.CompareCorner!.IsVisible.ShouldBeTrue();
        var hint = (StackPanel)studio.Hint!.Content!;
        ((TextBlock)hint.Children[0]).Text.ShouldBe("v3 to v4");
        ((TextBlock)((Border)hint.Children[1]).Child!).Text.ShouldBe("+ added");
        ((TextBlock)((Border)hint.Children[2]).Child!).Text.ShouldBe("− removed");
        // Both parts changed between v3 and v4: both tabs have their dot.
        studio.Tabs!.SystemChanged.ShouldBeTrue();
        studio.Tabs.UserChanged.ShouldBeTrue();
        AutomationProperties.GetName(studio.Tabs.SystemTab!).ShouldBe("System, changed");
        studio.LastAnnouncement.ShouldBe("Comparing v3 with v4");
        AutomationProperties.GetName(diff).ShouldBe("Changes from v3 to v4, system prompt");
        diff.SummaryName.ShouldBe($"{diff.InsertedWords} words added, {diff.DeletedWords} {(diff.DeletedWords == 1 ? "word" : "words")} removed");
    }

    [AvaloniaFact]
    public void The_Diff_Follows_The_Shown_Part_And_The_Draft()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        studio.Compare(studio.Versions[1]);
        studio.ActivePart = PromptPart.User;
        rig.Pump();
        studio.Diff!.Before.ShouldBe(PromptStudioSamples.UserV3);
        studio.Diff.After.ShouldBe(PromptStudioSamples.UserV4);
        AutomationProperties.GetName(studio.Diff).ShouldBe("Changes from v3 to v4, user prompt");
        studio.Draft = new PromptTemplate(studio.Draft.System, PromptStudioSamples.UserV3);
        rig.Pump();
        studio.Diff.After.ShouldBe(PromptStudioSamples.UserV3);
        studio.Diff.HasChanges.ShouldBeFalse();
        studio.Tabs!.UserChanged.ShouldBeFalse();
        studio.AddedText!.Text.ShouldBe("+0");
    }

    [AvaloniaFact]
    public void Compare_Mode_Without_A_Version_Compares_With_The_One_Before_The_Open_One()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        studio.IsCompareMode = true;
        rig.Pump();
        studio.EffectiveCompareVersion!.Id.ShouldBe("v3");
        studio.Diff!.Before.ShouldBe(PromptStudioSamples.SystemV3);
        studio.OpenVersion = studio.Versions[3];
        rig.Pump();
        // v1 is the last: the one before it (v2).
        studio.EffectiveCompareVersion!.Id.ShouldBe("v2");
    }

    [AvaloniaFact]
    public void Escape_Leaves_The_Compare_View_And_The_Tab_Takes_The_Focus()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        studio.Compare(studio.Versions[1]);
        rig.Pump();
        studio.BackButton!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Escape);
        studio.IsCompareMode.ShouldBeFalse();
        studio.LastAnnouncement.ShouldBe("Back to editing");
        rig.FocusIsIn(studio.Tabs!.SystemTab!).ShouldBeTrue();
        studio.SystemEditor!.IsEnabled.ShouldBeTrue();
        studio.SystemEditor.Opacity.ShouldBe(1);
        studio.Diff!.IsVisible.ShouldBeFalse();
        studio.TokenCount!.IsVisible.ShouldBeTrue();
        studio.Tabs.SystemChanged.ShouldBeFalse();
        ((StackPanel)studio.Hint!.Content!).Children.OfType<PromptKbd>().ShouldHaveSingleItem();
    }

    [AvaloniaFact]
    public void Back_To_Editing_Leaves_The_Compare_View()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        studio.Compare(studio.Versions[2]);
        studio.ActivePart = PromptPart.User;
        rig.Pump();
        rig.Click(studio.BackButton!);
        studio.IsCompareMode.ShouldBeFalse();
        rig.FocusIsIn(studio.Tabs!.UserTab!).ShouldBeTrue();
        studio.UserEditor!.IsEnabled.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void At_Full_Motion_The_Diff_And_The_Corner_Crossfade()
    {
        var frames = new ManualFrameClock();
        using var rig = new PromptStudioRig(level: ArcMotionLevel.Full, frames: frames);
        var studio = rig.Studio;
        rig.Frame(20);
        studio.Compare(studio.Versions[1]);
        rig.Pump(1);
        rig.Frame(3);
        studio.Diff!.Opacity.ShouldBeInRange(0.01, 0.99);
        studio.SystemEditor!.Opacity.ShouldBeInRange(0.01, 0.99);
        rig.Frame(30);
        studio.Diff.Opacity.ShouldBe(1);
        studio.SystemEditor.Opacity.ShouldBe(0);
        studio.TokenCount!.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Version_Menus_Compare_Action_Opens_The_Compare_View()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        rig.Click(studio.VersionPill!);
        var menu = studio.VersionMenu!;
        menu.IsOpen.ShouldBeTrue();
        studio.VersionPill!.IsOpen.ShouldBeTrue();
        // With v4 open, the menu selects v3.
        menu.SelectedVersion!.Id.ShouldBe("v3");
        rig.Click(menu.CompareAction!);
        menu.IsOpen.ShouldBeFalse();
        studio.IsCompareMode.ShouldBeTrue();
        studio.CompareVersion!.Id.ShouldBe("v3");
        rig.FocusIsIn(studio.VersionPill).ShouldBeTrue();
    }

    #endregion

    #region Promote and undo

    [AvaloniaFact]
    public void Promote_Moves_Production_At_Once_And_The_Pill_Says_Latest()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        var v4 = studio.Versions[0];
        var v3 = studio.Versions[1];
        PromptPromotedEventArgs? promoted = null;
        studio.Promoted += (_, e) =>
        {
            promoted = e;
            // Optimistic: the studio already shows it.
            studio.ProductionVersion.ShouldBe(v3);
        };
        studio.Promote(v3);
        rig.Pump();
        studio.ProductionVersion.ShouldBe(v3);
        promoted!.Version.ShouldBe(v3);
        promoted.Previous.ShouldBe(v4);
        promoted.IsUndo.ShouldBeFalse();
        studio.VersionPill!.IsProduction.ShouldBeFalse();
        studio.VersionMenu!.ProductionVersion.ShouldBe(v3);
        studio.LastAnnouncement.ShouldBe("v3 is now in production");
        AutomationProperties.GetName(studio.VersionPill).ShouldBe("Version v4");
    }

    [AvaloniaFact]
    public void Cancel_Rolls_A_Promotion_Back()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        studio.Promoted += (_, e) => e.Cancel = true;
        studio.Promote(studio.Versions[2]);
        rig.Pump();
        studio.ProductionVersion.ShouldBeNull();
        studio.EffectiveProductionVersion.ShouldBe(studio.Versions[0]);
        studio.VersionPill!.IsProduction.ShouldBeTrue();
        // Nothing to undo after a rollback.
        var events = 0;
        studio.Promoted += (_, _) => events++;
        studio.UndoPromote();
        events.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Undo_Puts_The_Previous_Version_Back_With_The_Arguments_Swapped()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        var v4 = studio.Versions[0];
        var v3 = studio.Versions[1];
        studio.Promote(v3);
        var events = new List<PromptPromotedEventArgs>();
        studio.Promoted += (_, e) => events.Add(e);
        studio.UndoPromote();
        rig.Pump();
        studio.EffectiveProductionVersion.ShouldBe(v4);
        studio.ProductionVersion.ShouldBeNull();
        events.Count.ShouldBe(1);
        events[0].Version.ShouldBe(v4);
        events[0].Previous.ShouldBe(v3);
        events[0].IsUndo.ShouldBeTrue();
        studio.VersionPill!.IsProduction.ShouldBeTrue();
        studio.LastAnnouncement.ShouldBe("Production serves v4 again");
        // Once undone there is nothing more to undo.
        studio.UndoPromote();
        events.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void A_Cancelled_Undo_Keeps_The_Promotion()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        var v3 = studio.Versions[1];
        studio.Promote(v3);
        var refuse = true;
        studio.Promoted += (_, e) => e.Cancel = e.IsUndo && refuse;
        studio.UndoPromote();
        studio.ProductionVersion.ShouldBe(v3);
        // It can still be undone later.
        refuse = false;
        studio.UndoPromote();
        studio.EffectiveProductionVersion.ShouldBe(studio.Versions[0]);
    }

    [AvaloniaFact]
    public void The_Menu_Promotes_And_Undoes()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        var events = new List<PromptPromotedEventArgs>();
        studio.Promoted += (_, e) => events.Add(e);
        rig.Click(studio.VersionPill!);
        var menu = studio.VersionMenu!;
        menu.SelectedVersion!.Id.ShouldBe("v3");
        rig.Click(menu.PromoteAction!);
        // The menu stays open; production moved and Undo is offered.
        menu.IsOpen.ShouldBeTrue();
        studio.ProductionVersion!.Id.ShouldBe("v3");
        menu.IsUndoAvailable.ShouldBeTrue();
        menu.PromoteAction!.IsEnabled.ShouldBeFalse();
        studio.VersionPill!.IsProduction.ShouldBeFalse();
        rig.Click(menu.Status!.UndoButton!);
        studio.EffectiveProductionVersion!.Id.ShouldBe("v4");
        menu.IsUndoAvailable.ShouldBeFalse();
        events.Select(e => (e.Version.Id, e.Previous?.Id, e.IsUndo)).ShouldBe([("v3", "v4", false), ("v4", "v3", true)]);
    }

    [AvaloniaFact]
    public void A_Host_That_Says_No_Leaves_No_Undo_In_The_Menu()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        studio.Promoted += (_, e) => e.Cancel = true;
        rig.Click(studio.VersionPill!);
        rig.Click(studio.VersionMenu!.PromoteAction!);
        studio.EffectiveProductionVersion!.Id.ShouldBe("v4");
        studio.VersionMenu.IsUndoAvailable.ShouldBeFalse();
    }

    #endregion

    #region Model menu

    [AvaloniaFact]
    public void The_Model_Menu_Picks_A_Model_And_Its_Default_Effort()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        rig.Click(studio.ModelPill!);
        var menu = studio.ModelMenu!;
        menu.IsOpen.ShouldBeTrue();
        studio.ModelPill!.IsOpen.ShouldBeTrue();
        rig.Click(menu.Rows[2]);
        studio.SelectedModel!.Id.ShouldBe("atlas-small");
        studio.Effort.ShouldBe(ModelEffort.Medium);
        studio.ModelPill.Model!.Id.ShouldBe("atlas-small");
        studio.ModelPill.Effort.ShouldBe(ModelEffort.Medium);
        // Picking a model keeps the menu open.
        menu.IsOpen.ShouldBeTrue();
        studio.LastAnnouncement.ShouldBe("Atlas Small selected, Medium effort");
        var low = menu.Strip!.Cells.First(c => c.Level == ModelEffort.Low);
        rig.Click(low);
        studio.Effort.ShouldBe(ModelEffort.Low);
        studio.LastAnnouncement.ShouldBe("Low effort");
        rig.Key(PhysicalKey.Escape);
        menu.IsOpen.ShouldBeFalse();
        studio.ModelPill.IsOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Escape_In_A_Menu_Does_Not_Leave_The_Compare_View()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        studio.Compare(studio.Versions[1]);
        rig.Pump();
        rig.Click(studio.ModelPill!);
        studio.ModelMenu!.IsOpen.ShouldBeTrue();
        rig.Key(PhysicalKey.Escape);
        studio.ModelMenu.IsOpen.ShouldBeFalse();
        studio.IsCompareMode.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Menus_Get_Their_Words_From_The_Labels()
    {
        using var rig = new PromptStudioRig();
        var studio = rig.Studio;
        var versions = studio.VersionMenu!;
        versions.GroupName.ShouldBe("Pick a version to compare or promote");
        versions.CompareText!("v3").ShouldBe("Compare with v3");
        versions.DaysAgoText!(3).ShouldBe("3 days ago");
        versions.LastWeekText.ShouldBe("last week");
        versions.WeeksAgoText!(2).ShouldBe("2 weeks ago");
        versions.Now.ShouldBe(PromptStudioSamples.Now);
        var models = studio.ModelMenu!;
        models.ModelsName.ShouldBe("Model");
        models.EffortTitle.ShouldBe("Effort");
        models.EffortCellText!(ModelEffort.ExtraHigh).ShouldBe("XHigh");
        models.EffortName!(ModelEffort.ExtraHigh).ShouldBe("Extra high");
        AutomationProperties.GetName(studio.ModelPill!).ShouldBe("Model: Atlas Medium, High effort. Change model");
    }

    #endregion
}
