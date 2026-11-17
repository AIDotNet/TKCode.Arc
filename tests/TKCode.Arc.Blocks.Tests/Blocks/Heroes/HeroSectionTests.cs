using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Heroes;

public class HeroSectionTests
{
    private static Window Show(Control control, double width = 1280, double height = 800, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width;
        window.Height = height;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static HeroRelayGraph Graph(HeroSection hero) => ((HeroRelayScene)hero.Scene!).Graph;

    private static void Press(Window window, Control control)
    {
        control.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Each_Variant_Shows_Its_Scene_And_Backdrop()
    {
        var hero = new HeroSection();
        Show(hero);
        hero.Scene.ShouldBeOfType<HeroDashboardScene>();
        hero.DashboardMesh!.IsVisible.ShouldBeTrue();
        hero.DashboardMesh.PointCount.ShouldBe(5);
        hero.EditorialMesh!.IsVisible.ShouldBeFalse();
        hero.Dots!.IsVisible.ShouldBeFalse();

        hero.Variant = HeroSectionVariant.Split;
        Dispatcher.UIThread.RunJobs();
        hero.Scene.ShouldBeOfType<HeroRelayScene>();
        hero.Dots.IsVisible.ShouldBeTrue();
        hero.DashboardMesh.IsVisible.ShouldBeFalse();

        hero.Variant = HeroSectionVariant.Minimal;
        Dispatcher.UIThread.RunJobs();
        hero.Scene.ShouldBeOfType<HeroEditorialScene>();
        hero.EditorialMesh.IsVisible.ShouldBeTrue();
        hero.EditorialMesh.PointCount.ShouldBe(6);
    }

    [AvaloniaFact]
    public void A_Design_Fills_The_Height_It_Is_Given()
    {
        var hero = new HeroSection { Height = 700 };
        Show(hero);
        hero.Bounds.Height.ShouldBe(700);
    }

    [AvaloniaFact]
    public void A_Design_In_A_Scroller_Takes_One_Viewport()
    {
        var hero = new HeroSection();
        Show(new ScrollViewer { Content = hero }, 1280, 800);
        hero.Bounds.Height.ShouldBe(800);
    }

    [AvaloniaFact]
    public void The_Dashboard_Design_Is_Never_Shorter_Than_600()
    {
        var hero = new HeroSection();
        Show(new ScrollViewer { Content = hero }, 1280, 400);
        hero.Bounds.Height.ShouldBe(600);
    }

    [AvaloniaFact]
    public void A_Title_Switches_To_Your_Own_Content_At_Its_Natural_Height()
    {
        var hero = new HeroSection
        {
            Title = "Ship it",
            Description = "Today.",
            InstallCommand = "npm i thing",
            PrimaryAction = new HeroAction { Label = "Go" },
            Announcement = new HeroAction { Label = "News" },
            Meta = new AvaloniaList<string>(["MIT", "v3"])
        };
        Show(new ScrollViewer { Content = hero });
        hero.Scene.ShouldBeOfType<HeroContentScene>();
        hero.Classes.Contains(HeroSection.CustomPseudoClass).ShouldBeTrue();
        hero.DashboardMesh!.IsVisible.ShouldBeFalse();
        hero.DesiredSize.Height.ShouldBeLessThan(800);
        hero.Scene!.ActionButtons.Count.ShouldBe(1);
        AutomationProperties.GetName(hero).ShouldBe("Ship it");
        hero.GetVisualDescendants().OfType<CopyButton>().Single().Value.ShouldBe("npm i thing");
    }

    [AvaloniaFact]
    public void Own_Content_Shows_Media_Only_In_The_Split_Layout()
    {
        var media = new Border { Height = 100 };
        var hero = new HeroSection { Title = "Ship it", Media = media };
        Show(hero);
        TopLevel.GetTopLevel(media).ShouldBeNull();
        hero.Variant = HeroSectionVariant.Split;
        Dispatcher.UIThread.RunJobs();
        TopLevel.GetTopLevel(media).ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void Default_Actions_Confirm_In_Place_And_Reset()
    {
        var hero = new HeroSection();
        var window = Show(hero);
        var primary = hero.Scene!.ActionButtons[0];
        primary.CurrentText.ShouldBe("Start free trial");
        Press(window, primary);
        primary.IsConfirmed.ShouldBeTrue();
        primary.CurrentText.ShouldBe("Trial started");
        AutomationProperties.GetName(primary).ShouldBe("Trial started");
        primary.ResetConfirmed();
        primary.CurrentText.ShouldBe("Start free trial");
    }

    [AvaloniaFact]
    public void Actions_Raise_The_Event_And_A_Link_Never_Confirms()
    {
        var hero = new HeroSection
        {
            PrimaryAction = new HeroAction { Label = "Pricing", Href = "https://example.com", ConfirmedLabel = "Unused" }
        };
        CtaAction? pressed = null;
        var isPrimary = false;
        hero.ActionInvoked += (_, e) =>
        {
            pressed = e.Action;
            isPrimary = e.IsPrimary;
            e.Handled = true;
        };
        var window = Show(hero);
        var primary = hero.Scene!.ActionButtons[0];
        Press(window, primary);
        pressed.ShouldBe(hero.PrimaryAction);
        isPrimary.ShouldBeTrue();
        primary.IsConfirmed.ShouldBeFalse();
    }

    [Fact]
    public void Status_Follows_The_Phase_And_The_Branch_Skips_Small_Payments()
    {
        var routed = HeroRelayRun.Events[0];
        var small = HeroRelayRun.Events[1];
        HeroRelayRun.StatusOf(HeroNodeId.Trigger, 0, routed).ShouldBe(HeroNodeStatus.Idle);
        HeroRelayRun.StatusOf(HeroNodeId.Trigger, 1, routed).ShouldBe(HeroNodeStatus.Running);
        HeroRelayRun.StatusOf(HeroNodeId.Trigger, 2, routed).ShouldBe(HeroNodeStatus.Done);
        HeroRelayRun.StatusOf(HeroNodeId.Notify, 7, routed).ShouldBe(HeroNodeStatus.Running);
        HeroRelayRun.StatusOf(HeroNodeId.Notify, 8, routed).ShouldBe(HeroNodeStatus.Done);
        HeroRelayRun.StatusOf(HeroNodeId.Notify, 5, small).ShouldBe(HeroNodeStatus.Idle);
        HeroRelayRun.StatusOf(HeroNodeId.Notify, 6, small).ShouldBe(HeroNodeStatus.Skipped);
        HeroRelayRun.StatusOf(HeroNodeId.Ticket, 8, small).ShouldBe(HeroNodeStatus.Skipped);
        HeroRelayRun.StatusOf(HeroNodeId.Store, 8, small).ShouldBe(HeroNodeStatus.Done);
    }

    [AvaloniaFact]
    public void Below_Full_Motion_Each_Event_Shows_Its_Finished_Run()
    {
        var hero = new HeroSection { Variant = HeroSectionVariant.Split };
        Show(hero);
        var graph = Graph(hero);
        graph.Phase.ShouldBe(HeroRelayRun.Done);
        graph.NodeOf(HeroNodeId.Trigger).Status.ShouldBe(HeroNodeStatus.Done);
        graph.Edges.Progress.ShouldAllBe(p => p == 1);
        graph.Loop.IsRunning.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Send_Test_Event_Runs_The_Next_Sample()
    {
        var hero = new HeroSection { Variant = HeroSectionVariant.Split };
        var window = Show(hero);
        var scene = (HeroRelayScene)hero.Scene!;
        var graph = scene.Graph;
        var count = graph.RunCount;
        Press(window, scene.SendButton);
        graph.EventIndex.ShouldBe(1);
        graph.RunCount.ShouldBe(count + 1);
        // The second sample is small: two actions are skipped and their wires stay dark.
        graph.NodeOf(HeroNodeId.Notify).Status.ShouldBe(HeroNodeStatus.Skipped);
        graph.NodeOf(HeroNodeId.Notify).Sub.ShouldBe("Skipped");
        graph.Edges.Progress[2].ShouldBe(0);
        graph.Edges.Progress[4].ShouldBe(1);
    }

    [AvaloniaFact]
    public void The_Run_Advances_Phase_By_Phase_And_Moves_To_The_Next_Event()
    {
        var hero = new HeroSection { Variant = HeroSectionVariant.Split };
        Show(hero, level: ArcMotionLevel.Full);
        var graph = Graph(hero);
        graph.Loop.Stop();
        graph.SendTest();
        graph.EventIndex.ShouldBe(1);
        graph.Phase.ShouldBe(1);
        graph.Advance(HeroRelayRun.Phases[1]);
        graph.Phase.ShouldBe(2);
        graph.Edges.Progress[0].ShouldBe(0, 0.001);
        graph.Advance(HeroRelayRun.Phases[2] / 2);
        graph.Edges.Progress[0].ShouldBeInRange(0.2, 0.8);
        graph.Advance(HeroRelayRun.Phases.Sum());
        graph.EventIndex.ShouldBe(2);
    }

    [AvaloniaTheory]
    [InlineData(1280, false)]
    [InlineData(420, true)]
    public void The_Graph_Uses_Its_Compact_Layout_In_A_Narrow_Column(double width, bool narrow)
    {
        var hero = new HeroSection { Variant = HeroSectionVariant.Split };
        Show(hero, width);
        Graph(hero).IsNarrow.ShouldBe(narrow);
        Graph(hero).NodeOf(HeroNodeId.Notify).Title.ShouldBe(narrow ? "Chat" : "Post to #sales");
    }

    [AvaloniaTheory]
    [InlineData(1400, false)]
    [InlineData(600, true)]
    public void The_Dashboard_Has_Two_Designs(double width, bool narrow)
    {
        var hero = new HeroSection();
        Show(hero, width);
        var stage = ((HeroDashboardScene)hero.Scene!).Stage;
        stage.IsNarrowDesign.ShouldBe(narrow);
        stage.Dashboard.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void The_Range_Control_Changes_The_Dashboard()
    {
        var hero = new HeroSection();
        Show(hero);
        var dashboard = ((HeroDashboardScene)hero.Scene!).Stage.Dashboard!;
        dashboard.SecondKpiValue.ShouldBe("116%");
        dashboard.ChartPointCount.ShouldBe(30);
        var insight = dashboard.InsightText;
        dashboard.Range.SelectedIndex = 2;
        Dispatcher.UIThread.RunJobs();
        dashboard.SecondKpiValue.ShouldBe("112%");
        dashboard.ChartPointCount.ShouldBe(12);
        dashboard.InsightText.ShouldNotBe(insight);
    }

    [AvaloniaFact]
    public void The_Entrance_Can_Be_Skipped_And_Rests_In_Place()
    {
        var hero = new HeroSection { IsEntranceAnimated = false };
        Show(hero, level: ArcMotionLevel.Full);
        var stage = ((HeroDashboardScene)hero.Scene!).Stage;
        stage.Opacity.ShouldBe(1);
        stage.TiltAngle.ShouldBe(0);
    }

    [AvaloniaFact]
    public void The_Entrance_Finishes_Under_Full_Motion_With_The_Window_In_Place()
    {
        var hero = new HeroSection();
        Show(hero, level: ArcMotionLevel.Full);
        var stage = ((HeroDashboardScene)hero.Scene!).Stage;
        TestHelpers.PumpUntil(() => stage.TiltAngle == 0 && stage.Opacity == 1, 8000).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Mesh_Only_Drifts_Under_Full_Motion()
    {
        var off = new HeroSection();
        Show(off);
        off.DashboardMesh!.Loop.IsRunning.ShouldBeFalse();
        off.DashboardMesh.Loop.IsActive.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Every_Design_Renders_In_Light_And_Dark_And_Survives_Removal()
    {
        foreach (var variant in Enum.GetValues<HeroSectionVariant>())
        {
            var reference = TestHelpers.ShowAndClose(() => new HeroSection { Variant = variant });
            reference.ShouldNotBeNull();
        }
    }
}
