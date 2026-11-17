using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiPanelDock"/>: the docked layout and its divider, the dock width spring, the page column following it, the
/// sheet below the breakpoint (geometry, look, scrim, inert page), closing by Escape, scrim and close request with the focus
/// on the reopen pill, reopening (events, focus), the reopen pill's place, inert panel and tab order, and motion levels.
/// </summary>
public class AiPanelDockTests
{
    /// <summary>A panel with a box and a button, like the side panel's.</summary>
    private sealed class PanelContent : StackPanel
    {
        public TextBox Box { get; } = new() { Name = "Box" };

        public Button Close { get; } = new() { Content = "Close", Name = "Close" };

        public PanelContent()
        {
            Children.Add(Close);
            Children.Add(Box);
        }
    }

    private sealed class PageContent : StackPanel
    {
        public Button Link { get; } = new() { Content = "Link", Name = "Link" };

        public PageContent()
        {
            Background = Brushes.Transparent;
            Children.Add(Link);
        }
    }

    private static AiPanelDock Dock(bool open = true, object? page = null) => new()
    {
        Page = page ?? new PageContent(),
        Panel = new PanelContent(),
        IsOpen = open,
        ReopenText = "Helper",
        PanelName = "Helper panel"
    };

    private static Rect BoundsIn(Visual visual, Visual root) =>
        new(visual.TranslatePoint(default, root) ?? default, visual.Bounds.Size);

    private const double Frame = 1.0 / 60;

    #region Docked

    [AvaloniaFact]
    public void Open_Docks_The_Panel_At_The_Right_Behind_A_One_Pixel_Divider()
    {
        using var rig = new AiPanelRig(Dock(), width: 1440, height: 900);
        var dock = (AiPanelDock)rig.Content;
        dock.IsSheetMode.ShouldBeFalse();
        dock.Classes.Contains(":sheet").ShouldBeFalse();
        BoundsIn(dock.PagePresenter!, dock).ShouldBe(new Rect(0, 0, 1080, 900));
        var host = dock.PanelHost!;
        BoundsIn(host, dock).ShouldBe(new Rect(1080, 0, 360, 900));
        host.BorderThickness.ShouldBe(new Thickness(1, 0, 0, 0));
        TestHelpers.ColorOf(host.BorderBrush).ShouldBe(ArcTokens.GetColor(dock, ArcDesignTokenKind.Border));
        TestHelpers.ColorOf(host.Background).ShouldBe(ArcTokens.GetColor(dock, ArcDesignTokenKind.Surface));
        host.IsHitTestVisible.ShouldBeTrue();
        KeyboardNavigation.GetTabNavigation(host).ShouldBe(KeyboardNavigationMode.Continue);
        // The reopen pill is not there while the panel is open.
        var reopen = dock.ReopenButton!;
        reopen.IsVisible.ShouldBeFalse();
        reopen.Focusable.ShouldBeFalse();
        // The panel is a named complementary landmark.
        AutomationProperties.GetName(host).ShouldBe("Helper panel");
        AutomationProperties.GetLandmarkType(host).ShouldBe(AutomationLandmarkType.Complementary);
    }

    [AvaloniaFact]
    public void Closing_At_Off_Gives_The_Page_The_Width_And_Shows_The_Reopen_Pill_14px_From_The_Corner()
    {
        using var rig = new AiPanelRig(Dock(), width: 1440, height: 900);
        var dock = (AiPanelDock)rig.Content;
        dock.Close();
        rig.Settle();
        dock.IsOpen.ShouldBeFalse();
        dock.DockWidth.ShouldBe(0);
        BoundsIn(dock.PagePresenter!, dock).Width.ShouldBe(1440);
        var host = dock.PanelHost!;
        host.IsVisible.ShouldBeFalse();
        host.IsHitTestVisible.ShouldBeFalse();
        KeyboardNavigation.GetTabNavigation(host).ShouldBe(KeyboardNavigationMode.None);
        var reopen = dock.ReopenButton!;
        reopen.IsVisible.ShouldBeTrue();
        reopen.Opacity.ShouldBe(1);
        var pill = BoundsIn(reopen, dock);
        pill.Right.ShouldBe(1440 - 14, 0.5);
        pill.Y.ShouldBe(14, 0.5);
        pill.Height.ShouldBe(32);
        AutomationProperties.GetName(reopen).ShouldBe("Helper");
        var peer = ControlAutomationPeer.CreatePeerForElement(reopen);
        peer.GetProvider<IExpandCollapseProvider>().ShouldNotBeNull().ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
    }

    [AvaloniaFact]
    public void A_Dock_Made_Closed_Starts_Closed_Without_Motion()
    {
        var clock = new ManualFrameClock();
        using var rig = new AiPanelRig(Dock(open: false), width: 1440, height: 900, level: ArcMotionLevel.Full, clock: clock);
        var dock = (AiPanelDock)rig.Content;
        dock.DockWidth.ShouldBe(0);
        dock.PanelHost!.IsVisible.ShouldBeFalse();
        dock.ReopenButton!.Opacity.ShouldBe(1);
    }

    [AvaloniaFact]
    public void The_Dock_Width_Closes_And_Opens_On_The_Settle_Spring_And_The_Panel_Slides_With_Its_Edge()
    {
        var clock = new ManualFrameClock();
        using var rig = new AiPanelRig(Dock(), width: 1440, height: 900, level: ArcMotionLevel.Full, clock: clock);
        var dock = (AiPanelDock)rig.Content;
        var host = dock.PanelHost!;
        dock.IsOpen = false;
        rig.Settle();
        // Inert at once, still on screen while it slides out.
        host.IsHitTestVisible.ShouldBeFalse();
        host.IsVisible.ShouldBeTrue();
        rig.FrameCount(6);
        var (x, _) = SpringSolver.Evaluate(ArcMotionPresets.Settle, 360, 0, 6 * Frame);
        dock.DockWidth.ShouldBe(x, 0.01);
        // Arc measured 203 px left after 100ms (ω ≈ 15.5); Settle is ω ≈ 15.9.
        dock.DockWidth.ShouldBeInRange(180, 215);
        BoundsIn(dock.PagePresenter!, dock).Width.ShouldBe(1440 - dock.DockWidth, 1);
        var bounds = BoundsIn(host, dock);
        bounds.X.ShouldBe(1440 - dock.DockWidth, 1);
        bounds.Width.ShouldBe(360);
        rig.FrameCount(54);
        dock.DockWidth.ShouldBe(0);
        host.IsVisible.ShouldBeFalse();

        dock.IsOpen = true;
        rig.Settle();
        host.IsVisible.ShouldBeTrue();
        host.IsHitTestVisible.ShouldBeTrue();
        rig.FrameCount(12);
        var (back, _) = SpringSolver.Evaluate(ArcMotionPresets.Settle, -360, 0, 12 * Frame);
        dock.DockWidth.ShouldBe(360 + back, 0.01);
        // Arc: 277 at 200ms.
        dock.DockWidth.ShouldBeInRange(270, 300);
        rig.FrameCount(50);
        dock.DockWidth.ShouldBe(360);
    }

    [AvaloniaFact]
    public void A_Reversal_Mid_Spring_Keeps_Going_From_Where_The_Dock_Is()
    {
        var clock = new ManualFrameClock();
        using var rig = new AiPanelRig(Dock(), width: 1440, height: 900, level: ArcMotionLevel.Full, clock: clock);
        var dock = (AiPanelDock)rig.Content;
        dock.IsOpen = false;
        rig.FrameCount(6);
        var mid = dock.DockWidth;
        dock.IsOpen = true;
        rig.FrameCount(1);
        // Still moving toward 0 for a moment (velocity kept), never a jump.
        Math.Abs(dock.DockWidth - mid).ShouldBeLessThan(30);
        rig.FrameCount(60);
        dock.DockWidth.ShouldBe(360);
    }

    [AvaloniaFact]
    public void The_Page_Article_Recenters_As_The_Dock_Moves()
    {
        var clock = new ManualFrameClock();
        var page = new AiPanelPageView { Page = AiPanelPartSamples.Page() };
        using var rig = new AiPanelRig(Dock(page: page), width: 1440, height: 900, level: ArcMotionLevel.Full, clock: clock);
        var dock = (AiPanelDock)rig.Content;
        BoundsIn(page.Article!, dock).X.ShouldBe(200, 0.5);
        dock.IsOpen = false;
        rig.FrameCount(6);
        BoundsIn(page.Article!, dock).X.ShouldBe((1440 - dock.DockWidth - 680) / 2, 1);
        rig.FrameCount(60);
        BoundsIn(page.Article!, dock).X.ShouldBe(380, 0.5);
    }

    [AvaloniaFact]
    public void Reduced_Moves_The_Dock_At_Once_And_Fades_The_Pill()
    {
        var clock = new ManualFrameClock();
        using var rig = new AiPanelRig(Dock(), width: 1440, height: 900, level: ArcMotionLevel.Reduced, clock: clock);
        var dock = (AiPanelDock)rig.Content;
        dock.IsOpen = false;
        rig.FrameCount(1);
        dock.DockWidth.ShouldBe(0);
        var reopen = dock.ReopenButton!;
        reopen.IsVisible.ShouldBeTrue();
        reopen.Opacity.ShouldBeLessThan(1);
        rig.FrameCount(8);
        reopen.Opacity.ShouldBe(1);
    }

    #endregion

    #region Sheet

    [AvaloniaFact]
    public void Below_The_Breakpoint_The_Panel_Is_A_Bottom_Sheet_Over_A_Scrim_And_The_Page_Is_Inert()
    {
        using var rig = new AiPanelRig(Dock(), width: 390, height: 844);
        var dock = (AiPanelDock)rig.Content;
        dock.IsSheetMode.ShouldBeTrue();
        dock.Classes.Contains(":sheet").ShouldBeTrue();
        BoundsIn(dock.PagePresenter!, dock).ShouldBe(new Rect(0, 0, 390, 844));
        var host = dock.PanelHost!;
        var sheet = BoundsIn(host, dock);
        sheet.X.ShouldBe(0);
        sheet.Width.ShouldBe(390);
        sheet.Height.ShouldBe(844 * 0.74, 0.5);
        sheet.Bottom.ShouldBe(844, 0.5);
        host.CornerRadius.ShouldBe(new CornerRadius(20, 20, 0, 0));
        host.BorderThickness.ShouldBe(new Thickness(1, 1, 1, 0));
        TestHelpers.ColorOf(host.Background).ShouldBe(ArcTokens.GetColor(dock, ArcDesignTokenKind.SurfaceRaised));
        host.BoxShadow.ShouldBe(ArcTokens.Get<BoxShadows>(dock, ArcDesignTokenKind.ShadowFloating));
        ArcSurface.GetElevation(host).ShouldBe(ArcSurfaceElevation.Raised);
        var scrim = dock.Scrim!;
        scrim.IsVisible.ShouldBeTrue();
        scrim.Opacity.ShouldBe(0.72, 0.001);
        TestHelpers.ColorOf(scrim.Background).ShouldBe(ArcTokens.GetColor(dock, ArcDesignTokenKind.SurfaceMuted));
        // The page under the sheet takes no pointer and no Tab.
        dock.PagePresenter!.IsHitTestVisible.ShouldBeFalse();
        KeyboardNavigation.GetTabNavigation(dock.PagePresenter).ShouldBe(KeyboardNavigationMode.None);
    }

    [AvaloniaFact]
    public void The_Breakpoint_Is_The_Docks_Own_Width_640_Docks_639_Is_A_Sheet()
    {
        using var rig = new AiPanelRig(Dock(), width: 640, height: 700);
        var dock = (AiPanelDock)rig.Content;
        dock.IsSheetMode.ShouldBeFalse();
        BoundsIn(dock.PagePresenter!, dock).Width.ShouldBe(280);
        rig.Window.Width = 639;
        rig.Settle();
        dock.IsSheetMode.ShouldBeTrue();
        dock.DockWidth.ShouldBe(0);
        BoundsIn(dock.PagePresenter!, dock).Width.ShouldBe(639);
        rig.Window.Width = 900;
        rig.Settle();
        dock.IsSheetMode.ShouldBeFalse();
        dock.PagePresenter!.IsHitTestVisible.ShouldBeTrue();
        dock.Scrim!.IsVisible.ShouldBeFalse();
        BoundsIn(dock.PanelHost!, dock).ShouldBe(new Rect(540, 0, 360, 700));
    }

    [AvaloniaFact]
    public void A_Press_On_The_Scrim_Closes_The_Sheet_And_Gives_The_Page_Back()
    {
        using var rig = new AiPanelRig(Dock(), width: 390, height: 844);
        var dock = (AiPanelDock)rig.Content;
        var requests = 0;
        dock.CloseRequested += (_, _) => requests++;
        rig.Click(new Point(195, 100));
        requests.ShouldBe(1);
        dock.IsOpen.ShouldBeFalse();
        dock.PagePresenter!.IsHitTestVisible.ShouldBeTrue();
        dock.PanelHost!.IsVisible.ShouldBeFalse();
        dock.ReopenButton!.IsFocused.ShouldBeTrue();
        BoundsIn(dock.ReopenButton, dock).Right.ShouldBe(390 - 14, 0.5);
    }

    [AvaloniaFact]
    public void The_Sheet_Slides_Down_On_The_Spring_While_The_Scrim_Fades_In_200ms()
    {
        var clock = new ManualFrameClock();
        using var rig = new AiPanelRig(Dock(), width: 390, height: 844, level: ArcMotionLevel.Full, clock: clock);
        var dock = (AiPanelDock)rig.Content;
        var closed = 844 * 0.74 + 25;
        dock.IsOpen = false;
        rig.FrameCount(6);
        var (x, _) = SpringSolver.Evaluate(ArcMotionPresets.Settle, -closed, 0, 6 * Frame);
        dock.SheetOffset.ShouldBe(closed + x, 0.01);
        dock.PanelHost!.RenderTransform.ShouldBeOfType<TranslateTransform>().Y.ShouldBe(dock.SheetOffset);
        dock.ScrimProgress.ShouldBe(1 - new Avalonia.Animation.Easings.SplineEasing(0, 0, 0.58, 1).Ease(0.5), 0.001);
        rig.FrameCount(6);
        dock.ScrimProgress.ShouldBe(0, 1e-6);
        rig.FrameCount(50);
        dock.SheetOffset.ShouldBe(closed, 0.01);
        dock.PanelHost.IsVisible.ShouldBeFalse();

        dock.IsOpen = true;
        rig.Settle();
        dock.PanelHost.IsVisible.ShouldBeTrue();
        rig.FrameCount(60);
        dock.SheetOffset.ShouldBe(0);
        dock.ScrimProgress.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Reduced_Shows_The_Sheet_In_Place_With_A_Fade()
    {
        var clock = new ManualFrameClock();
        using var rig = new AiPanelRig(Dock(open: false), width: 390, height: 844, level: ArcMotionLevel.Reduced, clock: clock);
        var dock = (AiPanelDock)rig.Content;
        dock.IsOpen = true;
        rig.FrameCount(1);
        dock.SheetOffset.ShouldBe(0);
        dock.PanelHost!.Opacity.ShouldBeGreaterThan(0);
        dock.PanelHost.Opacity.ShouldBeLessThan(0.5);
        rig.FrameCount(10);
        dock.PanelHost.Opacity.ShouldBe(1);
        dock.IsOpen = false;
        rig.FrameCount(3);
        // Fading where it is, then out of sight.
        dock.SheetOffset.ShouldBe(0);
        dock.PanelHost.Opacity.ShouldBeLessThan(1);
        rig.FrameCount(10);
        dock.PanelHost.IsVisible.ShouldBeFalse();
    }

    #endregion

    #region Keyboard and focus

    [AvaloniaFact]
    public void Escape_In_The_Panel_Closes_It_And_Focuses_The_Reopen_Pill_With_Its_Ring()
    {
        using var rig = new AiPanelRig(Dock(), width: 1440, height: 900);
        var dock = (AiPanelDock)rig.Content;
        var content = (PanelContent)dock.Panel!;
        content.Box.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Escape);
        dock.IsOpen.ShouldBeFalse();
        var reopen = dock.ReopenButton!;
        reopen.IsFocused.ShouldBeTrue();
        reopen.Classes.Contains(":focus-visible").ShouldBeTrue();
        TestHelpers.Descendant<Border>(reopen, "PART_FocusRing").IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Escape_Handled_Inside_The_Panel_Does_Not_Close_It()
    {
        using var rig = new AiPanelRig(Dock(), width: 1440, height: 900);
        var dock = (AiPanelDock)rig.Content;
        var content = (PanelContent)dock.Panel!;
        content.Box.KeyDown += (_, e) => e.Handled = e.Key == Key.Escape;
        content.Box.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Escape);
        dock.IsOpen.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Hosts_Close_Button_Raises_CloseRequested_And_The_Dock_Closes()
    {
        using var rig = new AiPanelRig(Dock(), width: 1440, height: 900);
        var dock = (AiPanelDock)rig.Content;
        var content = (PanelContent)dock.Panel!;
        var heard = 0;
        dock.CloseRequested += (_, _) => heard++;
        content.Close.Click += (_, _) => content.Close.RaiseEvent(new RoutedEventArgs(AiPanelDock.CloseRequestedEvent, content.Close));
        rig.Click(content.Close);
        dock.IsOpen.ShouldBeFalse();
        // The owner hears it too.
        heard.ShouldBe(1);
        dock.ReopenButton!.IsFocused.ShouldBeTrue();
        // A pointer close moves the focus without the ring.
        dock.ReopenButton.Classes.Contains(":focus-visible").ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Programmatic_Close_Moves_The_Focus_Only_When_It_Was_In_The_Panel()
    {
        using var rig = new AiPanelRig(Dock(), width: 1440, height: 900);
        var dock = (AiPanelDock)rig.Content;
        var page = (PageContent)dock.Page!;
        page.Link.Focus(NavigationMethod.Tab);
        dock.Close();
        rig.Settle();
        page.Link.IsFocused.ShouldBeTrue();
        dock.Open();
        rig.Settle();
        ((PanelContent)dock.Panel!).Box.Focus();
        dock.IsOpen = false;
        rig.Settle();
        dock.ReopenButton!.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Reopen_Pill_Opens_The_Panel_And_Asks_The_Owner_To_Focus_The_Box()
    {
        using var rig = new AiPanelRig(Dock(open: false), width: 1440, height: 900);
        var dock = (AiPanelDock)rig.Content;
        var content = (PanelContent)dock.Panel!;
        var order = new List<string>();
        dock.ReopenRequested += (_, _) => order.Add("reopen");
        dock.FocusPanelRequested += (_, e) =>
        {
            order.Add("focus");
            content.Box.Focus(NavigationMethod.Tab);
            e.Handled = true;
        };
        rig.Click(dock.ReopenButton!);
        dock.IsOpen.ShouldBeTrue();
        order.ShouldBe(["reopen", "focus"]);
        content.Box.IsFocused.ShouldBeTrue();
        dock.ReopenButton!.Focusable.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Unhandled_The_First_Focusable_Element_Of_The_Panel_Gets_The_Focus()
    {
        using var rig = new AiPanelRig(Dock(open: false), width: 1440, height: 900);
        var dock = (AiPanelDock)rig.Content;
        dock.ReopenButton!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        dock.IsOpen.ShouldBeTrue();
        ((PanelContent)dock.Panel!).Close.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Tab_Skips_A_Closed_Panel_And_Reaches_The_Reopen_Pill()
    {
        using var rig = new AiPanelRig(Dock(open: false), width: 1440, height: 900);
        var dock = (AiPanelDock)rig.Content;
        var page = (PageContent)dock.Page!;
        page.Link.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Tab);
        dock.ReopenButton!.IsFocused.ShouldBeTrue();
        rig.Key(PhysicalKey.Tab);
        var content = (PanelContent)dock.Panel!;
        content.Close.IsFocused.ShouldBeFalse();
        content.Box.IsFocused.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Tab_From_The_Page_Enters_An_Open_Panel_But_Not_Under_A_Sheet()
    {
        using var rig = new AiPanelRig(Dock(), width: 1440, height: 900);
        var dock = (AiPanelDock)rig.Content;
        var page = (PageContent)dock.Page!;
        page.Link.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Tab);
        ((PanelContent)dock.Panel!).Close.IsFocused.ShouldBeTrue();
        rig.Window.Width = 390;
        rig.Settle();
        dock.IsSheetMode.ShouldBeTrue();
        // The page under the open sheet is out of the tab order.
        rig.Key(PhysicalKey.Tab);
        rig.Key(PhysicalKey.Tab);
        page.Link.IsFocused.ShouldBeFalse();
    }

    #endregion
}
