using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The model menu under the pointer: one highlight, hover-opened submenus, the safe path to a submenu (motion off).</summary>
public class ModelMenuPointerTests
{
    #region Highlight and hover

    [AvaloniaFact]
    public void The_Highlight_Follows_The_Pointer_And_Leaves_On_The_Separator_And_Outside()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        var rows = host.Main.Rows;
        host.Move(rows[1]);
        host.Main.Active.ShouldBe(rows[1]);
        rows[1].IsFocused.ShouldBeTrue();
        host.Move(rows[2]);
        host.Main.Active.ShouldBe(rows[2]);
        // The separator is not a row: the highlight goes, focus stays in the menu.
        host.Move(host.Center(host.InPanel<ModelMenuSeparator>(host.Main).Single()));
        host.Main.Active.ShouldBeNull();
        host.Main.IsKeyboardFocusWithin.ShouldBeTrue();
        host.Move(rows[0]);
        host.Main.Active.ShouldBe(rows[0]);
        host.Move(new Point(800, 600));
        host.Main.Active.ShouldBeNull();
        host.Menu.IsOpen.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Row_With_A_Submenu_Opens_It_After_The_Hover_Delay_And_Keeps_Its_Highlight()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        var effort = host.ActionRow(host.Main, ModelMenuRowKind.Effort);
        host.Move(effort);
        host.Menu.IsHoverPending.ShouldBeTrue();
        host.Menu.OpenSubmenu.ShouldBeNull();
        host.Menu.FlushHoverIntent().ShouldBeTrue();
        host.Pump();
        host.Menu.OpenSubmenu.ShouldBe(ModelMenuPage.Effort);
        effort.IsExpanded.ShouldBeTrue();
        host.Main.Active.ShouldBe(effort);
        // The pointer opened it: focus stays on the row, nothing is highlighted in the submenu yet.
        effort.IsFocused.ShouldBeTrue();
        host.Effort!.Active.ShouldBeNull();
        ModelMenu.HoverOpenDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
    }

    [AvaloniaFact]
    public void Leaving_The_Row_Before_The_Delay_Cancels_The_Submenu()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        host.Move(host.ActionRow(host.Main, ModelMenuRowKind.Effort));
        host.Menu.IsHoverPending.ShouldBeTrue();
        host.Move(host.Main.Rows[0]);
        host.Menu.IsHoverPending.ShouldBeFalse();
        host.Menu.FlushHoverIntent().ShouldBeFalse();
        host.Menu.OpenSubmenu.ShouldBeNull();
    }

    [AvaloniaFact]
    public void A_Click_Opens_The_Submenu_At_Once()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        host.Click(host.ActionRow(host.Main, ModelMenuRowKind.More));
        host.Menu.OpenSubmenu.ShouldBe(ModelMenuPage.More);
        host.Menu.IsHoverPending.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Submenu_Sits_One_Pixel_Beside_The_Menu_With_Its_First_Row_On_The_Opening_Row()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        var effort = host.ActionRow(host.Main, ModelMenuRowKind.Effort);
        host.Click(effort);
        var main = host.BoundsOf(host.Main);
        var sub = host.BoundsOf(host.Effort!);
        sub.Left.ShouldBe(main.Right + 1, 0.5);
        sub.Top.ShouldBe(host.BoundsOf(effort).Top - 7, 0.5);
        host.BoundsOf(host.Effort!.Rows[0]).Top.ShouldBe(host.BoundsOf(effort).Top, 0.5);
        host.Menu.EffortPresence!.Popup.OpensLeft.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Without_Room_On_The_Right_A_Submenu_Opens_On_The_Left()
    {
        using var host = ModelMenuHost.Show(anchorAt: HorizontalAlignment.Right);
        host.Open();
        host.Click(host.ActionRow(host.Main, ModelMenuRowKind.Effort));
        host.Menu.OpenSubmenu.ShouldBe(ModelMenuPage.Effort);
        host.Menu.EffortPresence!.Popup.OpensLeft.ShouldBeTrue();
        host.BoundsOf(host.Effort!).Right.ShouldBe(host.BoundsOf(host.Main).Left - 1, 0.5);
    }

    [AvaloniaFact]
    public void Near_The_Window_Bottom_A_Submenu_Slides_Up_Instead_Of_Flipping()
    {
        using var host = ModelMenuHost.Show(height: 560);
        host.Open();
        host.Click(host.ActionRow(host.Main, ModelMenuRowKind.Effort));
        var sub = host.BoundsOf(host.Effort!);
        sub.Bottom.ShouldBe(560, 0.5);
        sub.Height.ShouldBe(264, 0.5);
    }

    [AvaloniaFact]
    public void The_Pointer_In_A_Submenu_Highlights_Its_Rows_While_The_Opening_Row_Stays_Highlighted()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        var effort = host.ActionRow(host.Main, ModelMenuRowKind.Effort);
        host.Move(effort);
        host.Menu.FlushHoverIntent();
        host.Pump();
        var high = host.LevelRow(host.Effort!, ModelEffort.High);
        host.Move(high);
        host.Effort!.Active.ShouldBe(high);
        high.IsFocused.ShouldBeTrue();
        host.Main.Active.ShouldBe(effort);
        host.Menu.OpenSubmenu.ShouldBe(ModelMenuPage.Effort);
        // Back on the opening row: the submenu stays, its highlight leaves.
        host.Move(effort);
        host.Menu.OpenSubmenu.ShouldBe(ModelMenuPage.Effort);
        host.Effort.Active.ShouldBeNull();
    }

    #endregion

    #region Closing a submenu

    [AvaloniaFact]
    public void Settling_On_Another_Row_Closes_The_Submenu_At_Once()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        var effort = host.ActionRow(host.Main, ModelMenuRowKind.Effort);
        host.Move(effort);
        host.Menu.FlushHoverIntent();
        host.Pump();
        host.Move(host.Main.Rows[1]);
        host.Menu.OpenSubmenu.ShouldBeNull();
        host.Menu.EffortPresence!.Popup.IsOpen.ShouldBeFalse();
        effort.IsExpanded.ShouldBeFalse();
        host.Main.Active.ShouldBe(host.Main.Rows[1]);
    }

    [AvaloniaFact]
    public void Hovering_The_Other_Action_Row_Swaps_The_Submenus()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        host.Move(host.ActionRow(host.Main, ModelMenuRowKind.Effort));
        host.Menu.FlushHoverIntent();
        host.Pump();
        // Straight down to More models: away from the submenu's side, so the path does not hold.
        var more = host.ActionRow(host.Main, ModelMenuRowKind.More);
        host.Move(host.Center(more) - new Point(40, 0));
        host.Menu.OpenSubmenu.ShouldBeNull();
        host.Main.Active.ShouldBe(more);
        host.Menu.FlushHoverIntent().ShouldBeTrue();
        host.Pump();
        host.Menu.OpenSubmenu.ShouldBe(ModelMenuPage.More);
    }

    [AvaloniaFact]
    public void A_Diagonal_Path_Toward_The_Submenu_Crosses_Other_Rows_Without_Closing_It()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        var effort = host.ActionRow(host.Main, ModelMenuRowKind.Effort);
        var more = host.ActionRow(host.Main, ModelMenuRowKind.More);
        host.Move(effort);
        host.Menu.FlushHoverIntent();
        host.Pump();
        // Down and to the right, over More models.
        var over = new Point(host.BoundsOf(host.Main).Right - 14, host.Center(more).Y);
        host.Move(over);
        host.Menu.SafeZone.ShouldNotBeNull();
        host.Menu.OpenSubmenu.ShouldBe(ModelMenuPage.Effort);
        host.Main.Active.ShouldBe(effort);
        more.IsExpanded.ShouldBeFalse();
        host.Menu.IsHoverPending.ShouldBeFalse();
        // Into the submenu.
        var low = host.LevelRow(host.Effort!, ModelEffort.Low);
        host.Move(low);
        host.Effort!.Active.ShouldBe(low);
        host.Menu.OpenSubmenu.ShouldBe(ModelMenuPage.Effort);
        host.Menu.SafeZone.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Turning_Away_From_The_Submenu_Ends_The_Safe_Path()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        var more = host.ActionRow(host.Main, ModelMenuRowKind.More);
        host.Move(host.ActionRow(host.Main, ModelMenuRowKind.Effort));
        host.Menu.FlushHoverIntent();
        host.Pump();
        var over = new Point(host.BoundsOf(host.Main).Right - 14, host.Center(more).Y);
        host.Move(over);
        host.Menu.OpenSubmenu.ShouldBe(ModelMenuPage.Effort);
        // Back to the left on the same row: the pointer is no longer heading for the submenu.
        host.Move(over - new Point(3, 0));
        host.Menu.OpenSubmenu.ShouldBeNull();
        host.Main.Active.ShouldBe(more);
    }

    [AvaloniaFact]
    public void The_Safe_Path_Expires_After_300ms()
    {
        var now = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
        var menu = ModelMenuSamples.Menu();
        menu.Now = () => now;
        using var host = ModelMenuHost.Show(menu);
        host.Open();
        var more = host.ActionRow(host.Main, ModelMenuRowKind.More);
        host.Move(host.ActionRow(host.Main, ModelMenuRowKind.Effort));
        host.Menu.FlushHoverIntent();
        host.Pump();
        var over = new Point(host.BoundsOf(host.Main).Right - 14, host.Center(more).Y);
        host.Move(over);
        host.Menu.OpenSubmenu.ShouldBe(ModelMenuPage.Effort);
        now += TimeSpan.FromMilliseconds(320);
        host.Move(over + new Point(2, 0));
        host.Menu.OpenSubmenu.ShouldBeNull();
        host.Main.Active.ShouldBe(more);
    }

    [AvaloniaFact]
    public void Leaving_Both_Surfaces_Closes_The_Submenu_And_Clears_Both_Highlights()
    {
        using var host = ModelMenuHost.Show();
        host.Open();
        host.Move(host.ActionRow(host.Main, ModelMenuRowKind.Effort));
        host.Menu.FlushHoverIntent();
        host.Pump();
        host.Move(host.Effort!.Rows[0]);
        host.Move(new Point(host.BoundsOf(host.Effort).Right + 60, host.BoundsOf(host.Effort).Bottom + 60));
        host.Menu.OpenSubmenu.ShouldBeNull();
        host.Main.Active.ShouldBeNull();
        host.Menu.IsOpen.ShouldBeTrue();
        // Focus never stays in the closed submenu.
        host.Main.IsKeyboardFocusWithin.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Pointer_Resting_Outside_Leaves_A_Keyboard_Submenu_Alone()
    {
        using var host = ModelMenuHost.Show();
        host.Move(new Point(850, 650));
        host.Open(ModelMenuOpenFocus.Selected);
        host.Key(PhysicalKey.E);
        host.Key(PhysicalKey.ArrowRight);
        host.Move(new Point(860, 660));
        host.Menu.OpenSubmenu.ShouldBe(ModelMenuPage.Effort);
        host.Effort!.Active.ShouldNotBeNull();
    }

    #endregion

    #region Safe zone (pure)

    [Fact]
    public void Safe_Zone_Covers_The_Wedge_From_The_Exit_Point_To_The_Submenu()
    {
        var submenu = new Rect(305, 100, 264, 264);
        var zone = new SubmenuSafeZone(new Point(200, 220), submenu, SubmenuOnRight: true);
        // The exit point itself (the apex moved 5px back) and points on the way.
        zone.Contains(new Point(200, 220)).ShouldBeTrue();
        zone.Contains(new Point(260, 200)).ShouldBeTrue();
        zone.Contains(new Point(260, 300)).ShouldBeTrue();
        zone.Contains(new Point(300, 110)).ShouldBeTrue();
        // The submenu itself.
        zone.Contains(new Point(400, 360)).ShouldBeTrue();
        // Too steep: far above or below the wedge near the exit point.
        zone.Contains(new Point(205, 180)).ShouldBeFalse();
        zone.Contains(new Point(210, 280)).ShouldBeFalse();
        // Behind the exit point, or past the submenu.
        zone.Contains(new Point(190, 220)).ShouldBeFalse();
        zone.Contains(new Point(600, 220)).ShouldBeFalse();
        zone.Contains(new Point(400, 380)).ShouldBeFalse();
    }

    [Fact]
    public void Safe_Zone_Mirrors_For_A_Submenu_On_The_Left()
    {
        var submenu = new Rect(10, 100, 264, 264);
        var zone = new SubmenuSafeZone(new Point(380, 220), submenu, SubmenuOnRight: false);
        zone.Contains(new Point(380, 220)).ShouldBeTrue();
        zone.Contains(new Point(300, 160)).ShouldBeTrue();
        zone.Contains(new Point(100, 300)).ShouldBeTrue();
        zone.Contains(new Point(390, 220)).ShouldBeFalse();
        zone.Contains(new Point(375, 120)).ShouldBeFalse();
        zone.IsToward(-2, false).ShouldBeTrue();
        zone.IsToward(2, true).ShouldBeFalse();
        zone.IsToward(0, true).ShouldBeTrue();
        zone.IsToward(0, false).ShouldBeFalse();
        SubmenuSafeZone.Lifetime.ShouldBe(TimeSpan.FromMilliseconds(300));
    }

    [Fact]
    public void Polygon_Test_Handles_Edges_And_Concave_Shapes()
    {
        ReadOnlySpan<Point> square = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)];
        SubmenuSafeZone.IsInPolygon(new Point(5, 5), square).ShouldBeTrue();
        SubmenuSafeZone.IsInPolygon(new Point(15, 5), square).ShouldBeFalse();
        SubmenuSafeZone.IsInPolygon(new Point(-1, -1), square).ShouldBeFalse();
        ReadOnlySpan<Point> notch = [new(0, 0), new(10, 0), new(5, 5), new(10, 10), new(0, 10)];
        SubmenuSafeZone.IsInPolygon(new Point(8, 5), notch).ShouldBeFalse();
        SubmenuSafeZone.IsInPolygon(new Point(2, 5), notch).ShouldBeTrue();
    }

    #endregion

    #region Typeahead (pure)

    [Fact]
    public void Typeahead_Repeating_A_Letter_Cycles_Through_Its_Rows()
    {
        var now = new DateTime(2026, 1, 1);
        var typeahead = new ModelMenuTypeahead(() => now);
        string?[] names = ["Aurora Prime 3", "Aurora Swift 3", "Cobalt Atlas 2", "Effort", "More models"];
        typeahead.Next("a", names, 0).ShouldBe(1);
        typeahead.Next("a", names, 1).ShouldBe(0);
        typeahead.Next("A", names, 0).ShouldBe(1);
        typeahead.Buffer.ShouldBe("aaA");
    }

    [Fact]
    public void Typeahead_Builds_A_Prefix_And_Starts_Over_After_A_Pause()
    {
        var now = new DateTime(2026, 1, 1);
        var typeahead = new ModelMenuTypeahead(() => now);
        string?[] names = ["Aurora Prime 3", "Aurora Swift 3", "Cobalt Atlas 2", null, "More models"];
        typeahead.Next("a", names, -1).ShouldBe(0);
        typeahead.Next("u", names, 0).ShouldBe(0);
        typeahead.Next("r", names, 0).ShouldBe(0);
        typeahead.Next("o", names, 0).ShouldBe(0);
        typeahead.Next("r", names, 0).ShouldBe(0);
        typeahead.Next("a", names, 0).ShouldBe(0);
        typeahead.Next(" ", names, 0).ShouldBe(0);
        typeahead.Next("s", names, 0).ShouldBe(1);
        now += TimeSpan.FromSeconds(1.5);
        typeahead.Next("m", names, 1).ShouldBe(4);
        typeahead.Buffer.ShouldBe("m");
        // Nothing matches: no move. Rows without a name are skipped.
        now += TimeSpan.FromSeconds(1.5);
        typeahead.Next("x", names, 4).ShouldBeNull();
        typeahead.Next("q", [], 0).ShouldBeNull();
        typeahead.Reset();
        typeahead.Buffer.ShouldBe("");
    }

    #endregion
}
