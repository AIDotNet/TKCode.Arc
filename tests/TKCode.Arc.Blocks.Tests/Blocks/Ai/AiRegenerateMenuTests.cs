using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiRegenerateMenu"/>: rows, words and glyphs, the menu above its button, the list of models beside it, pointer
/// and keyboard models (wrapping arrows, Home / End, typeahead, → ←, Escape, Tab), focus return with and without the ring,
/// choices and their events, outside presses, live models, motion at every level and automation.
/// </summary>
public class AiRegenerateMenuTests
{
    private sealed class Host
    {
        public Host(Action<AiRegenerateMenu>? configure = null)
        {
            Target = new Button { Content = "Again", Width = 36, Height = 36, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
            Menu = new AiRegenerateMenu
            {
                PlacementTarget = Target,
                MenuName = "Answer again",
                TryAgainText = "Try again",
                MoreConciseText = "More concise",
                MoreDetailedText = "More detailed",
                SwitchModelText = "Switch model",
                AnswerAgainWithText = "Answer again with",
                Models = AiAnswerActionsSamples.OtherModels()
            };
            configure?.Invoke(Menu);
            Menu.Chosen += (_, e) => Chosen.Add(e);
            Root = new Panel { Children = { Target, Menu } };
        }

        public Button Target { get; }
        public AiRegenerateMenu Menu { get; }
        public Panel Root { get; }
        public List<AiRegenerateChosenEventArgs> Chosen { get; } = new();
    }

    private static (AiAnswerActionsRig Rig, Host Host) Open(bool keyboard = false, Action<AiRegenerateMenu>? configure = null,
        ArcMotionLevel level = ArcMotionLevel.Off, ManualFrameClock? clock = null, double width = 680)
    {
        var host = new Host(configure);
        var rig = new AiAnswerActionsRig(host.Root, level, clock, width);
        if (keyboard)
        {
            host.Target.Focus(NavigationMethod.Tab);
        }
        host.Menu.Open(keyboard);
        rig.Settle();
        rig.Frames(700);
        return (rig, host);
    }

    private static string Texts(IEnumerable<AiRegenerateMenuRow> rows) => string.Join(" | ", rows.Select(r => r.Text));

    #region Rows and geometry

    [AvaloniaFact]
    public void Three_Ways_A_Hairline_And_Switch_Model()
    {
        var (rig, host) = Open();
        using var _ = rig;
        var menu = host.Menu;
        Texts(menu.Rows).ShouldBe("Try again | More concise | More detailed | Switch model");
        menu.Rows[0].Icon.ShouldBeSameAs(ArcIcons.RotateCcw);
        menu.Rows[1].Icon.ShouldBeSameAs(ArcIcons.Minimize2);
        menu.Rows[2].Icon.ShouldBeSameAs(ArcIcons.Maximize2);
        menu.Rows[3].Icon.ShouldNotBeNull();
        menu.Rows[3].Classes.ShouldContain(AiRegenerateMenuRow.SubmenuPseudoClass);
        TestHelpers.Descendant<ArcGlyph>(menu.Rows[3], "PART_Chevron").IsVisible.ShouldBeTrue();
        TestHelpers.Descendant<ArcGlyph>(menu.Rows[0], "PART_Chevron").IsVisible.ShouldBeFalse();
        var list = menu.MenuPanel!.List.Children;
        list[3].ShouldBeOfType<AiActionsHairline>().Margin.ShouldBe(new Thickness(6, 5));
        Texts(menu.SubmenuRows).ShouldBe("Lumen 4 | Lumen Mini 4 | Atlas 2.1");
        menu.SubmenuPanel!.List.Children[0].ShouldBeOfType<AiRegenerateMenuHeading>().Text.ShouldBe("Answer again with");
    }

    [AvaloniaFact]
    public void Without_Models_There_Is_No_Switch_Model()
    {
        var (rig, host) = Open(configure: m => m.Models = null);
        using var _ = rig;
        Texts(host.Menu.Rows).ShouldBe("Try again | More concise | More detailed");
        host.Menu.MenuPanel!.List.Children.OfType<AiActionsHairline>().ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void The_Menu_Sits_6px_Above_Its_Button_Left_Edges_Aligned()
    {
        var (rig, host) = Open();
        using var _ = rig;
        var panel = host.Menu.MenuPanel!;
        var target = rig.BoundsOf(host.Target);
        var surface = rig.BoundsOf(panel);
        surface.X.ShouldBe(target.X, 0.5);
        (target.Y - surface.Bottom).ShouldBe(6, 0.5);
        panel.MinWidth.ShouldBe(216);
        surface.Width.ShouldBeGreaterThanOrEqualTo(216);
        panel.Padding.ShouldBe(new Thickness(5));
        panel.BorderThickness.ShouldBe(new Thickness(1));
        panel.CornerRadius.ShouldBe(ArcTokens.Get<CornerRadius>(panel, ArcDesignTokenKind.RadiusPanel));
        TestHelpers.ColorOf(panel.Background).ShouldBe(ArcTokens.GetColor(panel, ArcDesignTokenKind.SurfaceRaised));
        foreach (var row in host.Menu.Rows)
        {
            row.Bounds.Height.ShouldBe(36);
            row.CornerRadius.ShouldBe(new CornerRadius(20));
            row.Padding.ShouldBe(new Thickness(10, 0, 12, 0));
        }
        // 5px padding and 1px border, then the rows.
        (rig.BoundsOf(host.Menu.Rows[0]).X - surface.X).ShouldBe(6, 0.5);
        var lead = TestHelpers.Descendant<Panel>(host.Menu.Rows[0], "PART_Lead");
        lead.Bounds.Size.ShouldBe(new Size(16, 16));
        TestHelpers.Descendant<ArcGlyph>(host.Menu.Rows[0], "PART_Icon").StrokeThickness.ShouldBe(1.75);
        (rig.BoundsOf(TestHelpers.Descendant<TextBlock>(host.Menu.Rows[0], "PART_Text")).X - rig.BoundsOf(lead).Right).ShouldBe(10, 0.5);
    }

    [AvaloniaFact]
    public void The_List_Opens_Against_The_Menus_Right_Edge_5px_Above_Its_Row()
    {
        var (rig, host) = Open(keyboard: true);
        using var _ = rig;
        rig.Key(PhysicalKey.End);
        rig.Key(PhysicalKey.ArrowRight);
        rig.Frames(700);
        host.Menu.IsSubmenuOpen.ShouldBeTrue();
        var menu = rig.BoundsOf(host.Menu.MenuPanel!);
        var sub = rig.BoundsOf(host.Menu.SubmenuPanel!);
        var row = rig.BoundsOf(host.Menu.Rows[3]);
        sub.X.ShouldBe(menu.Right, 0.5);
        sub.Y.ShouldBe(row.Y - 5, 0.5);
        sub.Width.ShouldBeGreaterThanOrEqualTo(216);
        var heading = (AiRegenerateMenuHeading)host.Menu.SubmenuPanel!.List.Children[0];
        heading.Padding.ShouldBe(new Thickness(10, 6, 10, 4));
        var mark = TestHelpers.Descendant<ModelMark>(host.Menu.SubmenuRows[0], "PART_Mark");
        mark.IsVisible.ShouldBeTrue();
        mark.Size.ShouldBe(15);
        mark.Mark.ShouldBeSameAs(AiAnswerActionsSamples.StarMark);
        TestHelpers.Descendant<ArcGlyph>(host.Menu.SubmenuRows[0], "PART_Icon").IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Narrow_Window_Keeps_The_List_Inside()
    {
        var (rig, host) = Open(keyboard: true, width: 300);
        using var _ = rig;
        rig.Key(PhysicalKey.End);
        rig.Key(PhysicalKey.ArrowRight);
        rig.Frames(700);
        rig.BoundsOf(host.Menu.SubmenuPanel!).Right.ShouldBeLessThanOrEqualTo(rig.Window.Bounds.Width + 0.5);
    }

    #endregion

    #region Pointer

    [AvaloniaFact]
    public void A_Pointer_Open_Highlights_Nothing_And_Follows_The_Pointer()
    {
        var (rig, host) = Open();
        using var _ = rig;
        var menu = host.Menu;
        menu.HighlightedRow.ShouldBeNull();
        menu.MenuPanel!.IsFocused.ShouldBeTrue();
        rig.Hover(menu.Rows[1]);
        menu.HighlightedRow.ShouldBeSameAs(menu.Rows[1]);
        menu.Rows[1].Classes.ShouldContain(AiRegenerateMenuRow.HighlightedPseudoClass);
        TestHelpers.ColorOf(menu.Rows[1].Background).ShouldBe(ArcTokens.GetColor(menu.MenuPanel, ArcDesignTokenKind.SurfaceMuted));
        rig.MoveTo(new Point(2, 2));
        menu.HighlightedRow.ShouldBeNull();
        menu.IsOpen.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Hovering_Switch_Model_Opens_The_List_And_Another_Row_Closes_It()
    {
        var (rig, host) = Open();
        using var _ = rig;
        var menu = host.Menu;
        rig.Hover(menu.Rows[3]);
        menu.IsSubmenuOpen.ShouldBeTrue();
        menu.SubmenuHighlightedRow.ShouldBeNull();
        menu.Rows[3].IsFocused.ShouldBeTrue();
        rig.Frames(700);
        rig.Hover(menu.SubmenuRows[2]);
        menu.SubmenuHighlightedRow.ShouldBeSameAs(menu.SubmenuRows[2]);
        // Switch model stays highlighted while its list is open.
        menu.HighlightedRow.ShouldBeSameAs(menu.Rows[3]);
        rig.Hover(menu.Rows[0]);
        menu.IsSubmenuOpen.ShouldBeFalse();
        menu.HighlightedRow.ShouldBeSameAs(menu.Rows[0]);
    }

    [AvaloniaFact]
    public void A_Click_Chooses_After_Closing_And_Focus_Goes_Back_Without_A_Ring()
    {
        var (rig, host) = Open();
        using var _ = rig;
        var order = new List<string>();
        host.Menu.Chosen += (_, _) => order.Add(host.Menu.IsOpen ? "open" : "closed");
        rig.Click(host.Menu.Rows[2]);
        host.Chosen.Single().Mode.ShouldBe(AiAnswerMode.Detailed);
        order.ShouldBe(["closed"]);
        host.Target.IsFocused.ShouldBeTrue();
        host.Target.Classes.ShouldNotContain(":focus-visible");
    }

    [AvaloniaFact]
    public void A_Model_Click_Chooses_Switch_Model_With_It()
    {
        var (rig, host) = Open();
        using var _ = rig;
        rig.Click(host.Menu.Rows[3]);
        host.Menu.IsSubmenuOpen.ShouldBeTrue();
        host.Menu.IsOpen.ShouldBeTrue();
        rig.Frames(700);
        rig.Click(host.Menu.SubmenuRows[2]);
        host.Chosen.Single().Mode.ShouldBe(AiAnswerMode.SwitchModel);
        host.Chosen.Single().Model!.Id.ShouldBe("atlas");
        host.Menu.IsOpen.ShouldBeFalse();
        host.Menu.IsSubmenuOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Press_Outside_Closes_Everything_And_The_Pressed_Control_Keeps_Focus()
    {
        var (rig, host) = Open(keyboard: true);
        using var _ = rig;
        rig.Key(PhysicalKey.End);
        rig.Key(PhysicalKey.ArrowRight);
        // The left end of the button below (the list of models covers its middle).
        rig.Click(new Point(rig.BoundsOf(rig.After).X + 5, rig.CenterOf(rig.After).Y));
        host.Menu.IsOpen.ShouldBeFalse();
        host.Menu.IsSubmenuOpen.ShouldBeFalse();
        host.Chosen.ShouldBeEmpty();
        // What was pressed keeps the focus it took.
        rig.After.IsFocused.ShouldBeTrue();
    }

    #endregion

    #region Keyboard

    [AvaloniaFact]
    public void A_Keyboard_Open_Highlights_The_First_Row_And_Arrows_Wrap()
    {
        var (rig, host) = Open(keyboard: true);
        using var _ = rig;
        var menu = host.Menu;
        menu.HighlightedRow.ShouldBeSameAs(menu.Rows[0]);
        menu.Rows[0].IsFocused.ShouldBeTrue();
        rig.Key(PhysicalKey.ArrowUp);
        menu.HighlightedRow.ShouldBeSameAs(menu.Rows[3]);
        rig.Key(PhysicalKey.ArrowDown);
        menu.HighlightedRow.ShouldBeSameAs(menu.Rows[0]);
        rig.Key(PhysicalKey.ArrowDown);
        menu.HighlightedRow.ShouldBeSameAs(menu.Rows[1]);
        menu.Rows[1].IsFocused.ShouldBeTrue();
        rig.Key(PhysicalKey.End);
        menu.HighlightedRow.ShouldBeSameAs(menu.Rows[3]);
        rig.Key(PhysicalKey.Home);
        menu.HighlightedRow.ShouldBeSameAs(menu.Rows[0]);
        // Tab stays in the menu.
        rig.Key(PhysicalKey.Tab);
        menu.IsOpen.ShouldBeTrue();
        menu.Rows[0].IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Letters_Jump_To_The_Next_Row_That_Starts_With_Them()
    {
        var now = new DateTime(2026, 10, 23, 9, 0, 0, DateTimeKind.Utc);
        var (rig, host) = Open(keyboard: true, configure: m => m.Now = () => now);
        using var _ = rig;
        var menu = host.Menu;
        rig.Key(PhysicalKey.M);
        menu.HighlightedRow.ShouldBeSameAs(menu.Rows[1]);
        now += TimeSpan.FromSeconds(1);
        rig.Key(PhysicalKey.M);
        menu.HighlightedRow.ShouldBeSameAs(menu.Rows[2]);
        now += TimeSpan.FromSeconds(1);
        rig.Key(PhysicalKey.S);
        menu.HighlightedRow.ShouldBeSameAs(menu.Rows[3]);
        now += TimeSpan.FromSeconds(1);
        // "mo" typed quickly stays on the first match of the prefix.
        rig.Key(PhysicalKey.M);
        now += TimeSpan.FromMilliseconds(100);
        rig.Key(PhysicalKey.O);
        menu.HighlightedRow.ShouldBeSameAs(menu.Rows[1]);
        rig.Key(PhysicalKey.X);
        menu.HighlightedRow.ShouldBeSameAs(menu.Rows[1]);
    }

    [AvaloniaFact]
    public void Right_Opens_The_List_On_Its_First_Model_And_Left_Comes_Back()
    {
        var (rig, host) = Open(keyboard: true);
        using var _ = rig;
        var menu = host.Menu;
        // → does nothing on a row without a list.
        rig.Key(PhysicalKey.ArrowRight);
        menu.IsSubmenuOpen.ShouldBeFalse();
        rig.Key(PhysicalKey.End);
        rig.Key(PhysicalKey.ArrowRight);
        menu.IsSubmenuOpen.ShouldBeTrue();
        menu.SubmenuHighlightedRow.ShouldBeSameAs(menu.SubmenuRows[0]);
        menu.SubmenuRows[0].IsFocused.ShouldBeTrue();
        menu.HighlightedRow.ShouldBeSameAs(menu.Rows[3]);
        rig.Key(PhysicalKey.ArrowUp);
        menu.SubmenuHighlightedRow.ShouldBeSameAs(menu.SubmenuRows[2]);
        rig.Key(PhysicalKey.A);
        menu.SubmenuHighlightedRow.ShouldBeSameAs(menu.SubmenuRows[2]);
        rig.Key(PhysicalKey.ArrowLeft);
        menu.IsSubmenuOpen.ShouldBeFalse();
        menu.Rows[3].IsFocused.ShouldBeTrue();
        menu.HighlightedRow.ShouldBeSameAs(menu.Rows[3]);
        // Enter on Switch model opens the list the same way.
        rig.Key(PhysicalKey.Enter);
        menu.IsSubmenuOpen.ShouldBeTrue();
        menu.SubmenuRows[0].IsFocused.ShouldBeTrue();
        host.Chosen.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Escape_In_The_List_Closes_Everything_And_The_Button_Gets_Its_Ring()
    {
        var (rig, host) = Open(keyboard: true);
        using var _ = rig;
        rig.Key(PhysicalKey.End);
        rig.Key(PhysicalKey.ArrowRight);
        rig.Key(PhysicalKey.Escape);
        host.Menu.IsOpen.ShouldBeFalse();
        host.Menu.IsSubmenuOpen.ShouldBeFalse();
        host.Target.IsFocused.ShouldBeTrue();
        host.Target.Classes.ShouldContain(":focus-visible");
        host.Chosen.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Enter_And_Space_Choose_And_The_Button_Gets_Its_Ring()
    {
        var (rig, host) = Open(keyboard: true);
        using var _ = rig;
        rig.Key(PhysicalKey.Enter);
        host.Chosen.Single().Mode.ShouldBe(AiAnswerMode.Retry);
        host.Target.Classes.ShouldContain(":focus-visible");
        host.Menu.Open(keyboard: true);
        rig.Settle();
        rig.Key(PhysicalKey.ArrowDown);
        rig.Key(PhysicalKey.Space);
        host.Chosen.Count.ShouldBe(2);
        host.Chosen[1].Mode.ShouldBe(AiAnswerMode.Concise);
    }

    #endregion

    #region Data, motion, automation

    [AvaloniaFact]
    public void Models_Change_While_Open()
    {
        var models = AiAnswerActionsSamples.OtherModels();
        var (rig, host) = Open(configure: m => m.Models = models);
        using var _ = rig;
        models.RemoveAt(0);
        rig.Settle();
        Texts(host.Menu.SubmenuRows).ShouldBe("Lumen Mini 4 | Atlas 2.1");
        models.Clear();
        rig.Settle();
        Texts(host.Menu.Rows).ShouldBe("Try again | More concise | More detailed");
    }

    [AvaloniaFact]
    public void A_Model_Without_A_Mark_Takes_Its_Providers()
    {
        var provider = new AiModelProvider { Id = "lumen", Name = "Lumen", Mark = AiAnswerActionsSamples.RingsMark };
        var plain = new AiModel { Id = "plain", Name = "Plain", ProviderId = "lumen" };
        var (rig, host) = Open(configure: m =>
        {
            m.Models = new AvaloniaList<AiModel> { plain };
            m.Providers = new AvaloniaList<AiModelProvider> { provider };
        });
        using var _ = rig;
        host.Menu.SubmenuRows[0].Mark.ShouldBeSameAs(AiAnswerActionsSamples.RingsMark);
    }

    [AvaloniaFact]
    public void Full_Motion_Rises_5px_From_097_On_The_Css_Spring_And_Fades_In_160ms()
    {
        var clock = new ManualFrameClock();
        var host = new Host();
        using var rig = new AiAnswerActionsRig(host.Root, ArcMotionLevel.Full, clock);
        host.Menu.Open();
        rig.Settle();
        var presence = host.Menu.MenuPresence!;
        presence.CurrentOpacity.ShouldBeLessThan(0.2);
        presence.CurrentPose.X.ShouldBeGreaterThan(4);
        presence.CurrentPose.Y.ShouldBeLessThan(0.98);
        rig.Frames(170);
        presence.CurrentOpacity.ShouldBe(1, 0.001);
        presence.CurrentPose.X.ShouldBeGreaterThan(0);
        rig.Frames(450);
        presence.CurrentPose.ShouldBe(new Point(0, 1));

        host.Menu.Close();
        rig.Settle();
        host.Menu.MenuPopup!.IsOpen.ShouldBeTrue();
        presence.IsClosing.ShouldBeTrue();
        rig.Frames(150);
        rig.Drain(() => !host.Menu.MenuPopup.IsOpen).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Reduced_Motion_Only_Fades_And_Off_Is_Instant()
    {
        var clock = new ManualFrameClock();
        var host = new Host();
        using (var rig = new AiAnswerActionsRig(host.Root, ArcMotionLevel.Reduced, clock))
        {
            host.Menu.Open();
            rig.Settle();
            host.Menu.MenuPresence!.CurrentPose.ShouldBe(new Point(0, 1));
            host.Menu.MenuPresence.CurrentOpacity.ShouldBeLessThan(0.2);
            rig.Frames(130);
            host.Menu.MenuPresence.CurrentOpacity.ShouldBe(1, 0.001);
        }
        var off = new Host();
        using var offRig = new AiAnswerActionsRig(off.Root);
        off.Menu.Open();
        offRig.Settle();
        off.Menu.MenuPresence!.CurrentOpacity.ShouldBe(1);
        off.Menu.Close();
        offRig.Settle();
        off.Menu.MenuPopup!.IsOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Menus_And_Rows_Are_Named_For_Automation()
    {
        var (rig, host) = Open(keyboard: true);
        using var _ = rig;
        var menu = host.Menu;
        var panelPeer = ControlAutomationPeer.CreatePeerForElement(menu.MenuPanel!);
        panelPeer.GetAutomationControlType().ShouldBe(AutomationControlType.Menu);
        panelPeer.GetName().ShouldBe("Answer again");
        var rowPeer = ControlAutomationPeer.CreatePeerForElement(menu.Rows[0]);
        rowPeer.GetAutomationControlType().ShouldBe(AutomationControlType.MenuItem);
        rowPeer.GetName().ShouldBe("Try again");
        rowPeer.GetProvider<IExpandCollapseProvider>().ShouldBeNull();
        var switchPeer = ControlAutomationPeer.CreatePeerForElement(menu.Rows[3]).GetProvider<IExpandCollapseProvider>().ShouldNotBeNull();
        switchPeer.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
        switchPeer.Expand();
        rig.Settle();
        menu.IsSubmenuOpen.ShouldBeTrue();
        switchPeer.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
        ControlAutomationPeer.CreatePeerForElement(menu.SubmenuPanel!).GetName().ShouldBe("Answer again with");
        ControlAutomationPeer.CreatePeerForElement(menu.SubmenuRows[1]).GetProvider<IInvokeProvider>().ShouldNotBeNull().Invoke();
        rig.Settle();
        host.Chosen.Single().Model!.Name.ShouldBe("Lumen Mini 4");
    }

    #endregion
}
