using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The model menu in a narrow window: Effort and More models open as pages inside the menu (motion off).</summary>
public class ModelMenuPagesTests
{
    private static ModelMenuHost Narrow(ModelMenu? menu = null) =>
        ModelMenuHost.Show(menu, width: 390, height: 800, anchorAt: HorizontalAlignment.Center);

    [AvaloniaFact]
    public void At_Or_Below_The_Breakpoint_The_Action_Rows_Open_Pages()
    {
        using var host = Narrow();
        host.Open();
        host.Menu.IsPaged.ShouldBeTrue();
        var effort = host.ActionRow(host.Main, ModelMenuRowKind.Effort);
        effort.HasSubmenu.ShouldBeFalse();
        ControlAutomationPeer.CreatePeerForElement(effort).GetProvider<IExpandCollapseProvider>().ShouldBeNull();
        // Hovering does not open anything.
        host.Move(effort);
        host.Menu.IsHoverPending.ShouldBeFalse();
        host.Click(effort);
        host.Menu.EffortPresence.ShouldBeNull();
        host.Main.Page.ShouldBe(ModelMenuPage.Effort);
        AutomationProperties.GetName(host.Main).ShouldBe("Effort");
        var rows = host.Main.Rows;
        var back = rows[0].ShouldBeOfType<ModelMenuActionRow>();
        back.IsBack.ShouldBeTrue();
        back.Label.ShouldBe("Effort");
        AutomationProperties.GetName(back).ShouldBe("Back to featured models, Effort");
        back.Bounds.Height.ShouldBe(40);
        host.InPanel<ModelMenuSeparator>(host.Main).Count().ShouldBe(1);
        rows.Skip(1).Cast<ModelMenuEffortRow>().Select(r => r.Level).ShouldBe(ModelMenuSamples.FiveLevels);
        // The page keeps the menu's width; its height is the back row, the separator and five levels.
        var surface = host.BoundsOf(host.Main);
        surface.Width.ShouldBe(304);
        surface.Height.ShouldBe(40 + 13 + 5 * 50 + 12 + 2, 0.5);
    }

    [AvaloniaFact]
    public void The_Breakpoint_Is_The_Window_Width_When_The_Menu_Opens()
    {
        using var host = ModelMenuHost.Show(width: 640, anchorAt: HorizontalAlignment.Center);
        host.Open();
        host.Menu.IsPaged.ShouldBeTrue();
        host.Menu.Close();
        host.Window.Width = 641;
        host.Pump();
        host.Open();
        host.Menu.IsPaged.ShouldBeFalse();
        host.ActionRow(host.Main, ModelMenuRowKind.Effort).HasSubmenu.ShouldBeTrue();
        host.Menu.PagedBelowWidth = 700;
        host.Menu.Close();
        host.Open();
        host.Menu.IsPaged.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Keys_Walk_A_Page_Through_Its_Back_Row_And_The_Highlight_Shows_At_Once()
    {
        using var host = Narrow();
        host.Open(ModelMenuOpenFocus.Selected);
        host.Key(PhysicalKey.E);
        host.Key(PhysicalKey.ArrowRight);
        host.Main.Page.ShouldBe(ModelMenuPage.Effort);
        var medium = host.LevelRow(host.Main, ModelEffort.Medium);
        host.Main.Active.ShouldBe(medium);
        medium.IsFocused.ShouldBeTrue();
        // Unlike Arc, the highlight is on the focused row right away.
        host.Main.Highlight!.CurrentOpacity.ShouldBe(1);
        host.Main.Highlight.CurrentRect.ShouldBe(host.Main.RectOf(medium)!.Value);
        host.Key(PhysicalKey.ArrowUp);
        host.Key(PhysicalKey.ArrowUp);
        var back = host.Main.Rows[0];
        host.Main.Active.ShouldBe(back);
        host.Key(PhysicalKey.ArrowUp);
        host.Main.Active.ShouldBe(host.LevelRow(host.Main, ModelEffort.Max));
        host.Key(PhysicalKey.Home);
        host.Key(PhysicalKey.Enter);
        host.Main.Page.ShouldBe(ModelMenuPage.Main);
        AutomationProperties.GetName(host.Main).ShouldBe("Models");
        var effort = host.ActionRow(host.Main, ModelMenuRowKind.Effort);
        host.Main.Active.ShouldBe(effort);
        effort.IsFocused.ShouldBeTrue();
        host.Menu.IsOpen.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Left_On_A_Page_Returns_To_The_Row_That_Opened_It()
    {
        using var host = Narrow();
        host.Open(ModelMenuOpenFocus.Selected);
        host.Key(PhysicalKey.End);
        host.Key(PhysicalKey.ArrowRight);
        host.Main.Page.ShouldBe(ModelMenuPage.More);
        // The selected model is featured: the first model, past the back row.
        host.Main.Active.ShouldBe(host.Main.Rows[1]);
        host.Main.Rows[1].ShouldBeOfType<ModelMenuModelRow>().Model.Id.ShouldBe("aurora-summit-29");
        host.InPanel<ModelMenuGroupHeader>(host.Main).First().IsFirst.ShouldBeTrue();
        host.Key(PhysicalKey.ArrowLeft);
        host.Main.Page.ShouldBe(ModelMenuPage.Main);
        host.Main.Active.ShouldBe(host.ActionRow(host.Main, ModelMenuRowKind.More));
        // Left on the main page does nothing.
        host.Key(PhysicalKey.ArrowLeft);
        host.Main.Page.ShouldBe(ModelMenuPage.Main);
        host.Menu.IsOpen.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_More_Page_Starts_On_The_Selected_Model_And_Scrolls_To_It()
    {
        using var host = Narrow(ModelMenuSamples.Menu(m => m.SelectedModelId = "granite-70b"));
        host.Open(ModelMenuOpenFocus.Selected);
        host.Key(PhysicalKey.End);
        host.Key(PhysicalKey.Enter);
        var granite = host.ModelRow(host.Main, "granite-70b");
        host.Main.Active.ShouldBe(granite);
        granite.IsChecked.ShouldBeTrue();
        host.Pump();
        host.Main.Scroll!.Offset.Y.ShouldBeGreaterThan(0);
        // Picking from the page closes the menu.
        host.Key(PhysicalKey.ArrowUp);
        host.Key(PhysicalKey.Enter);
        host.Menu.SelectedModelId.ShouldBe("fjord-reason-2");
        host.Menu.IsOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Back_Row_Scrolls_With_The_Page()
    {
        using var host = Narrow();
        host.Open(ModelMenuOpenFocus.Selected);
        host.Key(PhysicalKey.End);
        host.Key(PhysicalKey.ArrowRight);
        var back = host.Main.Rows[0];
        var top = host.BoundsOf(back).Top;
        host.Key(PhysicalKey.End);
        host.Pump();
        host.BoundsOf(back).Top.ShouldBeLessThan(top);
    }

    [AvaloniaFact]
    public void A_Click_On_The_Back_Row_Goes_Back()
    {
        using var host = Narrow();
        host.Open();
        host.Click(host.ActionRow(host.Main, ModelMenuRowKind.More));
        host.Main.Page.ShouldBe(ModelMenuPage.More);
        host.Click(host.Main.Rows[0]);
        host.Main.Page.ShouldBe(ModelMenuPage.Main);
        host.Menu.IsOpen.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Reopening_Starts_On_The_Main_Page()
    {
        using var host = Narrow();
        host.Open(ModelMenuOpenFocus.Selected);
        host.Key(PhysicalKey.E);
        host.Key(PhysicalKey.ArrowRight);
        host.Key(PhysicalKey.Escape);
        host.Menu.IsOpen.ShouldBeFalse();
        host.Open();
        host.Main.Page.ShouldBe(ModelMenuPage.Main);
        host.Main.Scroll!.Offset.Y.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Without_Room_On_Either_Side_A_Wide_Window_Uses_A_Page_Too()
    {
        using var host = ModelMenuHost.Show(width: 760, anchorAt: HorizontalAlignment.Center);
        host.Open(ModelMenuOpenFocus.Selected);
        host.Menu.IsPaged.ShouldBeFalse();
        host.Key(PhysicalKey.E);
        host.Key(PhysicalKey.ArrowRight);
        host.Menu.OpenSubmenu.ShouldBeNull();
        host.Main.Page.ShouldBe(ModelMenuPage.Effort);
        host.Main.Rows[0].ShouldBeOfType<ModelMenuActionRow>().IsBack.ShouldBeTrue();
        host.Main.Active.ShouldBe(host.LevelRow(host.Main, ModelEffort.Medium));
        host.Key(PhysicalKey.ArrowLeft);
        host.Main.Page.ShouldBe(ModelMenuPage.Main);
    }

    [AvaloniaFact]
    public void Without_Room_On_Either_Side_A_Hover_Does_Not_Change_The_Page()
    {
        using var host = ModelMenuHost.Show(width: 760, anchorAt: HorizontalAlignment.Center);
        host.Open();
        var effort = host.ActionRow(host.Main, ModelMenuRowKind.Effort);
        host.Move(effort);
        host.Menu.FlushHoverIntent().ShouldBeTrue();
        host.Pump();
        host.Main.Page.ShouldBe(ModelMenuPage.Main);
        host.Menu.OpenSubmenu.ShouldBeNull();
        host.Main.Active.ShouldBe(effort);
        host.Click(effort);
        host.Main.Page.ShouldBe(ModelMenuPage.Effort);
    }
}
