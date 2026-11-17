using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The model menu: opening, rows, keyboard model, picking, focus return and accessibility (motion off).</summary>
public class ModelMenuTests
{
    #region Open and close

    [AvaloniaFact]
    public void An_Anchor_That_Had_Focus_Before_The_Menu_Opened_Shows_Its_Ring_After_A_Keyboard_Pick_And_After_Escape()
    {
        // Hiding the surface detaches the focused row; the focus manager would then put focus back on the anchor
        // without a keyboard ring, so the menu returns focus first.
        foreach (var level in new[] { ArcMotionLevel.Off, ArcMotionLevel.Full })
        {
            using var host = ModelMenuHost.Show(level: level);
            host.Anchor.Focus(NavigationMethod.Tab);
            host.Pump();
            host.Open(ModelMenuOpenFocus.Selected);
            host.Key(PhysicalKey.ArrowDown);
            host.Key(PhysicalKey.Enter);
            host.Pump(40);
            host.Anchor.IsFocused.ShouldBeTrue();
            host.Anchor.Classes.ShouldContain(":focus-visible");

            host.Open(ModelMenuOpenFocus.Selected);
            host.Key(PhysicalKey.Escape);
            host.Pump(40);
            host.Anchor.IsFocused.ShouldBeTrue();
            host.Anchor.Classes.ShouldContain(":focus-visible");
        }
    }

    [AvaloniaFact]
    public void Opens_Below_The_Anchor_With_Focus_On_The_Surface()
    {
        using var host = ModelMenuHost.Show();
        var opened = 0;
        host.Menu.Opened += (_, _) => opened++;
        host.Open();
        host.Menu.IsOpen.ShouldBeTrue();
        host.Menu.PlacementTarget.ShouldBe(host.Anchor);
        opened.ShouldBe(1);
        host.Menu.IsPaged.ShouldBeFalse();
        var panel = host.Main;
        var surface = host.BoundsOf(panel);
        var anchor = host.BoundsOf(host.Anchor);
        surface.Width.ShouldBe(304);
        surface.Top.ShouldBe(anchor.Bottom + 8, 0.5);
        // Centered on the anchor, then kept 12px inside the window.
        surface.Left.ShouldBe(12, 0.5);
        panel.CornerRadius.ShouldBe(new CornerRadius(26));
        panel.BorderThickness.ShouldBe(new Thickness(1));
        // A pointer open: focus on the menu, no highlighted row.
        panel.Active.ShouldBeNull();
        panel.IsKeyboardFocusWithin.ShouldBeTrue();
        // Main list: 4 featured (54) + separator (13) + 2 action rows (40) + padding (12) + border (2).
        surface.Height.ShouldBe(4 * 54 + 13 + 2 * 40 + 12 + 2, 0.5);
    }

    [AvaloniaFact]
    public void A_Centered_Anchor_Gets_A_Centered_Menu()
    {
        using var host = ModelMenuHost.Show(anchorAt: HorizontalAlignment.Center);
        host.Open();
        var surface = host.BoundsOf(host.Main);
        var anchor = host.BoundsOf(host.Anchor);
        (surface.Left + surface.Width / 2).ShouldBe(anchor.Left + anchor.Width / 2, 0.5);
    }

    [AvaloniaFact]
    public void Close_Raises_Closed_And_IsOpen_Opens_Again_Against_The_Same_Anchor()
    {
        using var host = ModelMenuHost.Show();
        var closed = 0;
        host.Menu.Closed += (_, _) => closed++;
        host.Open();
        host.Menu.Close();
        host.Pump();
        host.Menu.IsOpen.ShouldBeFalse();
        host.Menu.MainPresence!.Popup.IsOpen.ShouldBeFalse();
        closed.ShouldBe(1);
        // Two-way IsOpen.
        host.Menu.IsOpen = true;
        host.Pump();
        host.Menu.MainPresence.Popup.IsOpen.ShouldBeTrue();
        host.Menu.IsOpen = false;
        host.Pump();
        host.Menu.MainPresence.Popup.IsOpen.ShouldBeFalse();
        closed.ShouldBe(2);
    }

    [AvaloniaFact]
    public void Toggle_Opens_And_Closes()
    {
        using var host = ModelMenuHost.Show();
        host.Menu.Toggle(host.Anchor);
        host.Pump();
        host.Menu.IsOpen.ShouldBeTrue();
        host.Menu.Toggle(host.Anchor);
        host.Pump();
        host.Menu.IsOpen.ShouldBeFalse();
        host.Anchor.IsFocused.ShouldBeTrue();
        host.Anchor.Classes.ShouldNotContain(":focus-visible");
    }

    [AvaloniaFact]
    public void Opening_Without_An_Anchor_Waits_For_One()
    {
        using var host = ModelMenuHost.Show();
        host.Menu.IsOpen = true;
        host.Pump();
        host.Menu.MainPresence.ShouldBeNull();
        host.Menu.PlacementTarget = host.Anchor;
        host.Pump();
        host.Menu.MainPresence!.Popup.IsOpen.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Menu_Fits_A_Narrow_Window()
    {
        using var host = ModelMenuHost.Show(width: 320, anchorAt: HorizontalAlignment.Center);
        host.Open();
        host.Menu.IsPaged.ShouldBeTrue();
        var surface = host.BoundsOf(host.Main);
        surface.Width.ShouldBe(296);
        surface.Left.ShouldBe(12, 0.5);
    }

    [AvaloniaFact]
    public void Near_The_Window_Bottom_The_Menu_Opens_Above_And_Stays_Attached_When_A_Page_Grows()
    {
        using var host = ModelMenuHost.Show(width: 390, height: 800, anchorAt: HorizontalAlignment.Center, anchorV: VerticalAlignment.Bottom);
        host.Open(ModelMenuOpenFocus.Selected);
        var anchor = host.BoundsOf(host.Anchor);
        host.BoundsOf(host.Main).Bottom.ShouldBe(anchor.Top - 8, 0.5);
        host.Key(PhysicalKey.End);
        host.Key(PhysicalKey.ArrowRight);
        host.Pump();
        host.BoundsOf(host.Main).Height.ShouldBe(422, 0.5);
        host.BoundsOf(host.Main).Bottom.ShouldBe(anchor.Top - 8, 0.5);
    }

    [AvaloniaFact]
    public void A_Window_Resize_While_Open_Refits_The_Menu_And_Closes_A_Submenu()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        host.Click(host.ActionRow(host.Main, ModelMenuRowKind.Effort));
        host.Menu.OpenSubmenu.ShouldBe(ModelMenuPage.Effort);
        host.Window.Width = 300;
        host.Pump();
        host.BoundsOf(host.Main).Width.ShouldBe(276);
        host.Menu.OpenSubmenu.ShouldBeNull();
        host.Menu.IsOpen.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Events_Bubble_And_The_Anchor_Receives_Its_Own_Presses()
    {
        using var host = ModelMenuHost.Show();
        var seen = new List<string>();
        var parent = (Panel)host.Menu.Parent!;
        parent.AddHandler(ModelMenu.OpenedEvent, (_, _) => seen.Add("opened"));
        parent.AddHandler(ModelMenu.ModelChosenEvent, (_, e) => seen.Add($"model {e.Model.Id}"));
        parent.AddHandler(ModelMenu.EffortChosenEvent, (_, e) => seen.Add($"effort {e.Effort}"));
        parent.AddHandler(ModelMenu.ClosedEvent, (_, _) => seen.Add("closed"));
        host.Open();
        host.Menu.MainPresence!.Popup.OverlayInputPassThroughElement.ShouldBe(host.Anchor);
        host.Menu.Effort = ModelEffort.Max;
        host.Pump();
        host.Click(host.Main.Rows[2]);
        seen.ShouldBe(["opened", "model cobalt-atlas-2", "effort High", "closed"]);
    }

    [AvaloniaFact]
    public void Opening_From_Another_Anchor_Moves_The_Menu()
    {
        using var host = ModelMenuHost.Show();
        var other = new Button { Content = "Other", Width = 120, Height = 40, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 200, 300, 0) };
        ((Panel)host.Menu.Parent!).Children.Add(other);
        host.Pump();
        host.Open();
        host.Menu.Open(other, ModelMenuOpenFocus.First);
        host.Pump();
        host.Menu.PlacementTarget.ShouldBe(other);
        host.BoundsOf(host.Main).Top.ShouldBe(host.BoundsOf(other).Bottom + 8, 0.5);
        host.Main.Active.ShouldBe(host.Main.Rows[0]);
    }

    [AvaloniaFact]
    public void Leaving_The_Window_Closes_The_Menu()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        ((Panel)host.Menu.Parent!).Children.Remove(host.Menu);
        host.Pump();
        host.Menu.IsOpen.ShouldBeFalse();
        host.Menu.MainPresence!.Popup.IsOpen.ShouldBeFalse();
    }

    #endregion

    #region Rows

    [AvaloniaFact]
    public void Main_List_Shows_Featured_Models_Then_Effort_And_More_Models()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        var rows = host.Main.Rows;
        rows.Count.ShouldBe(6);
        var featured = rows.Take(4).Cast<ModelMenuModelRow>().ToList();
        featured.Select(r => r.ModelName).ShouldBe(["Aurora Prime 3", "Aurora Swift 3", "Cobalt Atlas 2", "Halcyon Wide 1.5"]);
        featured.ShouldAllBe(r => !r.IsCompact && r.Bounds.Height == 54);
        featured[0].Blurb.ShouldBe("Deep work: refactors, research, long tasks");
        featured.Select(r => r.IsChecked).ShouldBe([true, false, false, false]);
        // A featured model without a mark of its own shows its provider's.
        featured[2].Mark.ShouldBe(host.Menu.Providers[1].Mark);
        featured[2].MarkColor.ShouldBe(host.Menu.Providers[1].MarkColor);
        var effort = (ModelMenuActionRow)rows[4];
        effort.Kind.ShouldBe(ModelMenuRowKind.Effort);
        effort.Label.ShouldBe("Effort");
        effort.Value.ShouldBe("Medium");
        effort.Bounds.Height.ShouldBe(40);
        effort.HasSubmenu.ShouldBeTrue();
        var more = (ModelMenuActionRow)rows[5];
        more.Label.ShouldBe("More models");
        more.Value.ShouldBeNull();
        host.InPanel<ModelMenuSeparator>(host.Main).Count().ShouldBe(1);
        // Rows are 290 wide inside the 6px list padding.
        host.BoundsOf(rows[0]).Width.ShouldBe(290);
        (host.BoundsOf(rows[0]).Left - host.BoundsOf(host.Main).Left).ShouldBe(7);
    }

    [AvaloniaFact]
    public void A_Model_Without_Effort_Hides_The_Effort_Row_And_More_Models_Names_A_Selected_Other_Model()
    {
        using var host = ModelMenuHost.Show(ModelMenuSamples.Menu(m => m.SelectedModelId = "cobalt-atlas-lite"));
        host.Open();
        var rows = host.Main.Rows;
        rows.Count.ShouldBe(5);
        rows.OfType<ModelMenuModelRow>().ShouldAllBe(r => !r.IsChecked);
        rows.OfType<ModelMenuActionRow>().ShouldHaveSingleItem().Value.ShouldBe("Cobalt Atlas Lite");
        AutomationProperties.GetName(rows[4]).ShouldBe("More models, Cobalt Atlas Lite");
    }

    [AvaloniaFact]
    public void Effort_Submenu_Lists_The_Levels_With_The_Default_Note_And_A_Check_On_The_Current_Level()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        host.Click(host.ActionRow(host.Main, ModelMenuRowKind.Effort));
        host.Menu.OpenSubmenu.ShouldBe(ModelMenuPage.Effort);
        var panel = host.Effort!;
        host.BoundsOf(panel).Width.ShouldBe(264);
        AutomationProperties.GetName(panel).ShouldBe("Effort");
        var levels = panel.Rows.Cast<ModelMenuEffortRow>().ToList();
        levels.Select(r => r.LevelName).ShouldBe(["Low", "Medium", "High", "Extra high", "Max"]);
        levels.Select(r => r.Description).ShouldBe(levels.Select(r => ModelEffortNames.Description(r.Level)));
        levels.Select(r => r.IsDefault).ShouldBe([false, true, false, false, false]);
        levels.Select(r => r.IsChecked).ShouldBe([false, true, false, false, false]);
        levels.ShouldAllBe(r => r.Bounds.Height == 50);
        AutomationProperties.GetName(levels[1]).ShouldBe("Medium, Default");
        // 5 levels: 6 + 5 × 50 + 6 + 2.
        host.BoundsOf(panel).Height.ShouldBe(264, 0.5);
    }

    [AvaloniaFact]
    public void More_Models_Submenu_Groups_By_Provider_In_Compact_Rows()
    {
        using var host = ModelMenuHost.Show(ModelMenuSamples.Menu(m => m.SelectedModelId = "fjord-reason-2"));
        host.Open();
        host.Click(host.ActionRow(host.Main, ModelMenuRowKind.More));
        var panel = host.More!;
        host.BoundsOf(panel).Width.ShouldBe(272);
        // The list stops growing at 420 and scrolls.
        host.BoundsOf(panel).Height.ShouldBe(422, 0.5);
        panel.Scroll!.Extent.Height.ShouldBeGreaterThan(420);
        var headers = host.InPanel<ModelMenuGroupHeader>(panel).ToList();
        headers.Select(h => h.Text).ShouldBe(["Aurora Labs", "Cobalt Research", "Halcyon", "Basalt AI", "Driftwood", "Ember Systems", "Fjord", "Granite Open"]);
        headers[0].Padding.ShouldBe(new Thickness(10, 6, 10, 4));
        headers[1].Padding.ShouldBe(new Thickness(10, 10, 10, 4));
        var rows = panel.Rows.Cast<ModelMenuModelRow>().ToList();
        rows.Count.ShouldBe(9);
        rows.ShouldAllBe(r => r.IsCompact && r.Bounds.Height == 38 && r.Blurb == null);
        rows.Select(r => r.TagText).ShouldBe(["Strongest", "Fast", "Fast", "Fast", null, "Code", null, "New", "Open weights"]);
        rows.Single(r => r.IsChecked).Model.Id.ShouldBe("fjord-reason-2");
        // Each provider is a named group.
        var groups = host.InPanel<StackPanel>(panel).Where(g => g.GetType().Name == "ModelMenuGroup").ToList();
        groups.Select(AutomationProperties.GetName).ShouldBe(headers.Select(h => h.Text));
    }

    [AvaloniaFact]
    public void An_Empty_Catalog_Opens_A_Menu_With_A_Short_Note()
    {
        using var host = ModelMenuHost.Show(new ModelMenu());
        host.Open();
        host.Main.Rows.ShouldBeEmpty();
        host.InPanel<ModelMenuGroupHeader>(host.Main).ShouldHaveSingleItem().Text.ShouldBe("No models available");
        host.InPanel<ModelMenuSeparator>(host.Main).ShouldBeEmpty();
        // Keys do nothing harmful.
        host.Key(PhysicalKey.ArrowDown);
        host.Key(PhysicalKey.Enter);
        host.Key(PhysicalKey.A);
        host.Menu.IsOpen.ShouldBeTrue();
        host.Key(PhysicalKey.Escape);
        host.Menu.IsOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Single_Model_Without_Effort_Is_The_Whole_Menu()
    {
        var menu = new ModelMenu();
        menu.Models.Add(ModelMenuSamples.Model("solo", "Solo", "fjord", "The only one", null, null, null));
        using var host = ModelMenuHost.Show(menu);
        host.Open(ModelMenuOpenFocus.Selected);
        host.Main.Rows.ShouldHaveSingleItem();
        host.InPanel<ModelMenuSeparator>(host.Main).ShouldBeEmpty();
        host.Main.Active.ShouldBe(host.Main.Rows[0]);
        host.Key(PhysicalKey.ArrowDown);
        host.Main.Active.ShouldBe(host.Main.Rows[0]);
        host.Key(PhysicalKey.Enter);
        host.Menu.IsOpen.ShouldBeFalse();
        menu.SelectedModelId.ShouldBe("solo");
    }

    [AvaloniaFact]
    public void Labels_Replace_Every_Word()
    {
        var menu = ModelMenuSamples.Menu(m => m.Labels = new ModelMenuLabels
        {
            Menu = "Modelle",
            Effort = "Aufwand",
            MoreModels = "Weitere Modelle",
            EffortName = level => level.ToString().ToUpperInvariant()
        });
        using var host = ModelMenuHost.Show(menu);
        host.Open();
        AutomationProperties.GetName(host.Main).ShouldBe("Modelle");
        host.ActionRow(host.Main, ModelMenuRowKind.Effort).Label.ShouldBe("Aufwand");
        host.ActionRow(host.Main, ModelMenuRowKind.Effort).Value.ShouldBe("MEDIUM");
        host.ActionRow(host.Main, ModelMenuRowKind.More).Label.ShouldBe("Weitere Modelle");
        // Changing labels while open rebuilds the rows.
        menu.Labels = new ModelMenuLabels();
        host.Pump();
        host.ActionRow(host.Main, ModelMenuRowKind.Effort).Label.ShouldBe("Effort");
    }

    #endregion

    #region Keyboard

    [AvaloniaFact]
    public void Arrow_Keys_Walk_Every_Row_And_Wrap_Home_And_End_Jump()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        var rows = host.Main.Rows;
        host.Key(PhysicalKey.ArrowDown);
        host.Main.Active.ShouldBe(rows[0]);
        rows[0].IsFocused.ShouldBeTrue();
        host.Key(PhysicalKey.ArrowUp);
        host.Main.Active.ShouldBe(rows[5]);
        host.Key(PhysicalKey.ArrowDown);
        host.Main.Active.ShouldBe(rows[0]);
        for (var i = 0; i < 4; i++)
        {
            host.Key(PhysicalKey.ArrowDown);
        }
        host.Main.Active.ShouldBe(rows[4]);
        rows[4].IsFocused.ShouldBeTrue();
        host.Key(PhysicalKey.Home);
        host.Main.Active.ShouldBe(rows[0]);
        host.Key(PhysicalKey.End);
        host.Main.Active.ShouldBe(rows[5]);
        host.Key(PhysicalKey.ArrowUp);
        host.Main.Active.ShouldBe(rows[4]);
    }

    [AvaloniaFact]
    public void Arrow_Up_From_The_Surface_Starts_At_The_Last_Row()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        host.Key(PhysicalKey.ArrowUp);
        host.Main.Active.ShouldBe(host.Main.Rows[^1]);
    }

    [AvaloniaFact]
    public void Open_Focus_Chooses_Where_The_Keyboard_Starts()
    {
        using var host = ModelMenuHost.Show(ModelMenuSamples.Menu(m => m.SelectedModelId = "cobalt-atlas-2"));
        host.Open(ModelMenuOpenFocus.Selected);
        host.Main.Active.ShouldBe(host.ModelRow(host.Main, "cobalt-atlas-2"));
        host.Main.Active!.IsFocused.ShouldBeTrue();
        host.Menu.Close(returnFocus: false);
        host.Pump();
        host.Open(ModelMenuOpenFocus.Last);
        host.Main.Active.ShouldBe(host.Main.Rows[^1]);
        host.Menu.Close(returnFocus: false);
        host.Pump();
        host.Open(ModelMenuOpenFocus.First);
        host.Main.Active.ShouldBe(host.Main.Rows[0]);
        host.Menu.Close(returnFocus: false);
        host.Pump();
        host.Open();
        host.Main.Active.ShouldBeNull();

        // A selected model that is only in "More models": the first row.
        host.Menu.Close(returnFocus: false);
        host.Pump();
        host.Menu.SelectedModelId = "granite-70b";
        host.Open(ModelMenuOpenFocus.Selected);
        host.Main.Active.ShouldBe(host.Main.Rows[0]);
    }

    [AvaloniaFact]
    public void Open_Again_With_A_Key_Moves_The_Keyboard_Position_In()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        host.Main.Active.ShouldBeNull();
        host.Open(ModelMenuOpenFocus.Selected);
        host.Main.Active.ShouldBe(host.Main.Rows[0]);
    }

    [AvaloniaFact]
    public void A_Letter_Jumps_To_The_Next_Row_Starting_With_It_And_Repeats_Cycle()
    {
        var now = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
        var menu = ModelMenuSamples.Menu();
        menu.Now = () => now;
        using var host = ModelMenuHost.Show(menu);
        host.Open(ModelMenuOpenFocus.Selected);
        var rows = host.Main.Rows;
        host.Key(PhysicalKey.A);
        host.Main.Active.ShouldBe(rows[1]);
        host.Key(PhysicalKey.A);
        host.Main.Active.ShouldBe(rows[0]);
        now += TimeSpan.FromSeconds(2);
        // Action rows are found by name too.
        host.Key(PhysicalKey.E);
        host.Main.Active.ShouldBe(rows[4]);
        now += TimeSpan.FromSeconds(2);
        host.Key(PhysicalKey.M);
        host.Main.Active.ShouldBe(rows[5]);
        now += TimeSpan.FromSeconds(2);
        // A prefix: "co" finds Cobalt Atlas 2.
        host.Key(PhysicalKey.C);
        host.Key(PhysicalKey.O);
        host.Main.Active.ShouldBe(rows[2]);
        now += TimeSpan.FromSeconds(2);
        // No match leaves the highlight where it is.
        host.Key(PhysicalKey.Z);
        host.Main.Active.ShouldBe(rows[2]);
        host.Menu.IsOpen.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Tab_Does_Nothing_Inside_The_Menu()
    {
        using var host = ModelMenuHost.Show();
        host.Open(ModelMenuOpenFocus.Selected);
        var row = host.Main.Active;
        host.Key(PhysicalKey.Tab);
        host.Key(PhysicalKey.Tab, RawInputModifiers.Shift);
        host.Menu.IsOpen.ShouldBeTrue();
        host.Main.Active.ShouldBe(row);
        row!.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Right_Opens_The_Submenu_On_The_Selected_Level_And_Left_Goes_Back_To_Its_Row()
    {
        using var host = ModelMenuHost.Show();
        host.Open(ModelMenuOpenFocus.Selected);
        host.Key(PhysicalKey.E);
        var effortRow = host.ActionRow(host.Main, ModelMenuRowKind.Effort);
        host.Main.Active.ShouldBe(effortRow);
        host.Key(PhysicalKey.ArrowRight);
        host.Menu.OpenSubmenu.ShouldBe(ModelMenuPage.Effort);
        effortRow.IsExpanded.ShouldBeTrue();
        var medium = host.LevelRow(host.Effort!, ModelEffort.Medium);
        host.Effort!.Active.ShouldBe(medium);
        medium.IsFocused.ShouldBeTrue();
        // The menu's highlight steps back while the keyboard is in the submenu (Arc).
        host.Main.Active.ShouldBeNull();
        host.Key(PhysicalKey.ArrowDown);
        host.Effort.Active.ShouldBe(host.LevelRow(host.Effort, ModelEffort.High));
        host.Key(PhysicalKey.ArrowLeft);
        host.Menu.OpenSubmenu.ShouldBeNull();
        host.Menu.EffortPresence!.Popup.IsOpen.ShouldBeFalse();
        effortRow.IsExpanded.ShouldBeFalse();
        host.Main.Active.ShouldBe(effortRow);
        effortRow.IsFocused.ShouldBeTrue();
        // Right on a model row does nothing; Enter on the Effort row opens it too.
        host.Key(PhysicalKey.Home);
        host.Key(PhysicalKey.ArrowRight);
        host.Menu.OpenSubmenu.ShouldBeNull();
        host.Key(PhysicalKey.E);
        host.Key(PhysicalKey.Enter);
        host.Menu.OpenSubmenu.ShouldBe(ModelMenuPage.Effort);
        host.Effort.Active.ShouldBe(medium);
    }

    [AvaloniaFact]
    public void Right_Into_More_Models_Starts_On_The_Selected_Model_Or_The_First()
    {
        using var host = ModelMenuHost.Show();
        host.Open(ModelMenuOpenFocus.Selected);
        host.Key(PhysicalKey.End);
        host.Key(PhysicalKey.ArrowRight);
        host.More!.Active.ShouldBe(host.More.Rows[0]);
        host.Key(PhysicalKey.Escape);
        host.Menu.SelectedModelId = "driftwood-coder-7";
        host.Open(ModelMenuOpenFocus.Selected);
        host.Key(PhysicalKey.End);
        host.Key(PhysicalKey.ArrowRight);
        host.More.Active.ShouldBe(host.ModelRow(host.More, "driftwood-coder-7"));
        host.More.Active!.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Moving_Off_The_Row_Of_An_Open_Submenu_Closes_It()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        host.Click(host.ActionRow(host.Main, ModelMenuRowKind.Effort));
        host.Menu.OpenSubmenu.ShouldBe(ModelMenuPage.Effort);
        // The pointer opened it, focus is still on the row: a key moves on and the submenu closes.
        host.Key(PhysicalKey.ArrowDown);
        host.Menu.OpenSubmenu.ShouldBeNull();
        host.Main.Active.ShouldBe(host.ActionRow(host.Main, ModelMenuRowKind.More));
    }

    [AvaloniaFact]
    public void Escape_Closes_The_Whole_Tree_And_The_Anchor_Shows_Its_Focus_Ring()
    {
        using var host = ModelMenuHost.Show();
        var closed = 0;
        host.Menu.Closed += (_, _) => closed++;
        host.Open(ModelMenuOpenFocus.Selected);
        host.Key(PhysicalKey.End);
        host.Key(PhysicalKey.ArrowRight);
        host.Menu.OpenSubmenu.ShouldBe(ModelMenuPage.More);
        host.Key(PhysicalKey.Escape);
        host.Menu.IsOpen.ShouldBeFalse();
        host.Menu.OpenSubmenu.ShouldBeNull();
        host.Menu.MorePresence!.Popup.IsOpen.ShouldBeFalse();
        host.Menu.MainPresence!.Popup.IsOpen.ShouldBeFalse();
        closed.ShouldBe(1);
        host.Anchor.IsFocused.ShouldBeTrue();
        host.Anchor.Classes.ShouldContain(":focus-visible");
    }

    [AvaloniaFact]
    public void Escape_Opened_By_Pointer_Still_Returns_A_Visible_Focus()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        host.Key(PhysicalKey.Escape);
        host.Anchor.IsFocused.ShouldBeTrue();
        host.Anchor.Classes.ShouldContain(":focus-visible");
    }

    #endregion

    #region Picking

    [AvaloniaFact]
    public void Enter_Picks_A_Model_In_Order_ModelChosen_Then_Selection_Then_Snap_Then_Close()
    {
        using var host = ModelMenuHost.Show();
        host.Open(ModelMenuOpenFocus.Selected);
        host.Menu.Effort = ModelEffort.Max;
        var log = new List<string>();
        host.Menu.ModelChosen += (_, e) => log.Add($"model {e.Model.Id} from {e.PreviousModel?.Id} change {e.IsChange} selected {host.Menu.SelectedModelId}");
        host.Menu.PropertyChanged += (_, e) =>
        {
            if (e.Property == ModelMenu.SelectedModelProperty)
            {
                log.Add("selection");
            }
            else if (e.Property == ModelMenu.IsOpenProperty)
            {
                log.Add($"open {e.NewValue}");
            }
        };
        host.Menu.EffortChosen += (_, e) => log.Add($"effort {e.Effort} from {e.PreviousEffort} snap {e.IsSnap}");
        host.Menu.Closed += (_, _) => log.Add("closed");
        host.Key(PhysicalKey.C);
        host.Key(PhysicalKey.Enter);
        // With motion off the popup is gone at once, so Closed arrives while IsOpen is still being changed.
        log.ShouldBe([
            "model cobalt-atlas-2 from aurora-prime-3 change True selected aurora-prime-3",
            "selection",
            "effort High from Max snap True",
            "closed",
            "open False"
        ]);
        host.Menu.SelectedModelId.ShouldBe("cobalt-atlas-2");
        host.Menu.Effort.ShouldBe(ModelEffort.High);
        host.Anchor.IsFocused.ShouldBeTrue();
        host.Anchor.Classes.ShouldContain(":focus-visible");
    }

    [AvaloniaFact]
    public void Picking_The_Selected_Model_Or_Level_Reports_It_Without_A_Change_And_Closes()
    {
        using var host = ModelMenuHost.Show();
        host.Open(ModelMenuOpenFocus.Selected);
        ModelMenuModelEventArgs? model = null;
        ModelMenuEffortEventArgs? effort = null;
        host.Menu.ModelChosen += (_, e) => model = e;
        host.Menu.EffortChosen += (_, e) => effort = e;
        host.Key(PhysicalKey.Space);
        model!.IsChange.ShouldBeFalse();
        model.Model.Id.ShouldBe("aurora-prime-3");
        effort.ShouldBeNull();
        host.Menu.IsOpen.ShouldBeFalse();

        host.Open(ModelMenuOpenFocus.Selected);
        host.Key(PhysicalKey.E);
        host.Key(PhysicalKey.ArrowRight);
        host.Key(PhysicalKey.Enter);
        effort!.Effort.ShouldBe(ModelEffort.Medium);
        effort.PreviousEffort.ShouldBe(ModelEffort.Medium);
        effort.IsSnap.ShouldBeFalse();
        host.Menu.IsOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Picking_A_Level_Sets_The_Effort_Then_Reports_It()
    {
        using var host = ModelMenuHost.Show();
        host.Open(ModelMenuOpenFocus.Selected);
        ModelEffort? seen = null;
        ModelMenuEffortEventArgs? effort = null;
        host.Menu.EffortChosen += (_, e) =>
        {
            effort = e;
            seen = host.Menu.Effort;
        };
        host.Key(PhysicalKey.E);
        host.Key(PhysicalKey.ArrowRight);
        host.Key(PhysicalKey.ArrowDown);
        host.Key(PhysicalKey.ArrowDown);
        host.Key(PhysicalKey.Enter);
        effort!.Effort.ShouldBe(ModelEffort.ExtraHigh);
        effort.PreviousEffort.ShouldBe(ModelEffort.Medium);
        effort.IsSnap.ShouldBeFalse();
        seen.ShouldBe(ModelEffort.ExtraHigh);
        host.Menu.Effort.ShouldBe(ModelEffort.ExtraHigh);
        host.Menu.IsOpen.ShouldBeFalse();
        host.Anchor.Classes.ShouldContain(":focus-visible");
        // The next open shows the new level.
        host.Open();
        host.ActionRow(host.Main, ModelMenuRowKind.Effort).Value.ShouldBe("Extra high");
    }

    [AvaloniaFact]
    public void A_Click_Picks_And_Gives_Focus_Back_Without_A_Ring()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        ModelMenuModelEventArgs? model = null;
        host.Menu.ModelChosen += (_, e) => model = e;
        host.Click(host.ModelRow(host.Main, "halcyon-wide-15"));
        model!.Model.Id.ShouldBe("halcyon-wide-15");
        host.Menu.SelectedModelId.ShouldBe("halcyon-wide-15");
        host.Menu.IsOpen.ShouldBeFalse();
        host.Anchor.IsFocused.ShouldBeTrue();
        host.Anchor.Classes.ShouldNotContain(":focus-visible");
    }

    [AvaloniaFact]
    public void A_Click_In_More_Models_Picks_From_The_Submenu()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        host.Click(host.ActionRow(host.Main, ModelMenuRowKind.More));
        host.Click(host.ModelRow(host.More!, "basalt-4"));
        host.Menu.SelectedModelId.ShouldBe("basalt-4");
        host.Menu.IsOpen.ShouldBeFalse();
        host.Menu.MorePresence!.Popup.IsOpen.ShouldBeFalse();
        host.Open();
        host.ActionRow(host.Main, ModelMenuRowKind.More).Value.ShouldBe("Basalt 4");
    }

    [AvaloniaFact]
    public void A_Press_Outside_Closes_Without_Taking_Focus_And_A_Press_On_The_Anchor_Passes_Through()
    {
        using var host = ModelMenuHost.Show();
        var anchorPresses = 0;
        host.Anchor.AddHandler(InputElement.PointerPressedEvent, (_, _) => anchorPresses++, RoutingStrategies.Tunnel, handledEventsToo: true);
        host.Open();
        host.Click(host.Center(host.Anchor));
        anchorPresses.ShouldBe(1);
        host.Menu.IsOpen.ShouldBeTrue();
        host.Click(new Point(860, 680));
        host.Menu.IsOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Press_Inside_A_Submenu_Does_Not_Close_The_Menu()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        host.Click(host.ActionRow(host.Main, ModelMenuRowKind.More));
        // A provider heading is not a row: pressing it changes nothing.
        host.Click(host.InPanel<ModelMenuGroupHeader>(host.More!).First());
        host.Menu.IsOpen.ShouldBeTrue();
        host.Menu.OpenSubmenu.ShouldBe(ModelMenuPage.More);
    }

    #endregion

    #region Accessibility

    [AvaloniaFact]
    public void Automation_Exposes_A_Menu_Of_Radio_Items_And_Expandable_Items()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        var menuPeer = ControlAutomationPeer.CreatePeerForElement(host.Main);
        menuPeer.GetAutomationControlType().ShouldBe(AutomationControlType.Menu);
        menuPeer.GetName().ShouldBe("Models");

        var selected = ControlAutomationPeer.CreatePeerForElement(host.Main.Rows[0]);
        selected.GetAutomationControlType().ShouldBe(AutomationControlType.MenuItem);
        selected.GetName().ShouldBe("Aurora Prime 3");
        selected.GetProvider<IToggleProvider>()!.ToggleState.ShouldBe(ToggleState.On);
        ControlAutomationPeer.CreatePeerForElement(host.Main.Rows[1]).GetProvider<IToggleProvider>()!.ToggleState.ShouldBe(ToggleState.Off);
        selected.GetProvider<IExpandCollapseProvider>().ShouldBeNull();

        var effort = ControlAutomationPeer.CreatePeerForElement(host.Main.Rows[4]);
        effort.GetName().ShouldBe("Effort, Medium");
        effort.GetProvider<IToggleProvider>().ShouldBeNull();
        var expand = effort.GetProvider<IExpandCollapseProvider>()!;
        expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
        expand.Expand();
        host.Pump();
        host.Menu.OpenSubmenu.ShouldBe(ModelMenuPage.Effort);
        expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
        ControlAutomationPeer.CreatePeerForElement(host.Effort!).GetName().ShouldBe("Effort");
        expand.Collapse();
        host.Pump();
        host.Menu.OpenSubmenu.ShouldBeNull();

        var separator = ControlAutomationPeer.CreatePeerForElement(host.InPanel<ModelMenuSeparator>(host.Main).Single());
        separator.GetAutomationControlType().ShouldBe(AutomationControlType.Separator);
        var group = host.Main.Rows[0].GetVisualParent()!;
        ControlAutomationPeer.CreatePeerForElement((Control)group).GetAutomationControlType().ShouldBe(AutomationControlType.Group);
        AutomationProperties.GetName(group).ShouldBe("Featured models");

        // Invoking a model row through automation picks it.
        ControlAutomationPeer.CreatePeerForElement(host.Main.Rows[3]).GetProvider<IInvokeProvider>()!.Invoke();
        host.Pump();
        host.Menu.SelectedModelId.ShouldBe("halcyon-wide-15");
    }

    #endregion
}
