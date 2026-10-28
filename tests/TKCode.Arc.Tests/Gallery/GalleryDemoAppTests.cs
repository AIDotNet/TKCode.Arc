using TKCode.Arc.Gallery.DemoApp;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>The Northstar dashboard demo at /demo: the home page's way in, the full-frame app, its pages and its session.</summary>
public class GalleryDemoAppTests
{
    private static GalleryWindow Open(double width = 1440, double height = 900)
    {
        DemoSession.SignIn(DemoSession.Emma);
        var window = new GalleryWindow { Width = width, Height = height };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void The_Home_Page_Live_Demo_Button_Opens_The_Overview_Without_The_Gallery_Chrome()
    {
        var window = Open();
        try
        {
            var raised = new List<string>();
            window.Shell.RouteChanged += route => raised.Add(route.Path);
            var button = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Live demo").FindAncestorOfType<Button>()!;
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();

            window.Shell.Route.ShouldBe(GalleryRoute.Demo("overview"));
            raised.ShouldBe(["/demo/overview"]);
            window.TopBar.IsEffectivelyVisible.ShouldBeFalse("the demo is an app of its own, as on uiarc.dev");
            window.Sidebar.IsVisible.ShouldBeFalse();
            window.GetVisualDescendants().OfType<DemoOverviewPage>().ShouldHaveSingleItem();

            window.Shell.Navigate(GalleryRoute.Home);
            Pump();
            window.TopBar.IsEffectivelyVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Sidebar_Highlight_Glides_To_The_Chosen_Page_Through_The_Page_Switch()
    {
        var window = Open();
        ArcMotion.SetLevel(window, ArcMotionLevel.Full);
        try
        {
            window.Shell.Navigate(GalleryRoute.Demo("overview"));
            Pump();
            var nav = window.GetVisualDescendants().OfType<DemoNav>().First(n => n.IsEffectivelyVisible);
            var highlight = nav.GetVisualDescendants().OfType<GlidingIndicator>().Single();
            var from = highlight.CurrentRect;

            window.Shell.Navigate(GalleryRoute.Demo("customers"));
            Pump();
            window.UpdateLayout();
            Pump();

            // uiarc.dev's highlight springs across (layoutId with spring.morph); the new page's layout passes used to
            // jump it straight onto the row.
            var row = nav.GetVisualDescendants().OfType<GalleryNavRow>().Single(r => r.IsSelected);
            var target = row.TranslatePoint(default, highlight.GetVisualParent()!)!.Value;
            highlight.CurrentRect.Y.ShouldBeLessThan(target.Y - 1, "still on its way down from the overview row");
            highlight.CurrentRect.Y.ShouldBeGreaterThanOrEqualTo(from.Y);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Collapsed_Rail_Opens_The_Page_Whose_Icon_Is_Clicked()
    {
        var window = Open();
        try
        {
            window.Shell.Navigate(GalleryRoute.Demo("overview"));
            Pump();
            window.GetVisualDescendants().OfType<DemoFrame>().Single().ToggleCollapsed();
            var nav = window.GetVisualDescendants().OfType<DemoNav>().First(n => n.IsEffectivelyVisible);
            // The width follows a 400ms transition on the wall clock; under a loaded test run it can take longer.
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (nav.Bounds.Width >= DemoNav.CollapsedWidth && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(50);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Pump();
                window.UpdateLayout();
            }
            nav.Bounds.Width.ShouldBeLessThan(DemoNav.CollapsedWidth);

            // The labels are hidden from the pointer in the rail; the row's own surface has to take the click, not
            // only the glyph's strokes.
            var row = nav.GetVisualDescendants().OfType<GalleryNavRow>().Single(r => AutomationProperties.GetName(r) == "Customers");
            var point = row.TranslatePoint(new Point(4, row.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Pump();

            window.Shell.Route.ShouldBe(GalleryRoute.Demo("customers"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Preferences_Accent_Swatch_Sets_The_Accent_Of_Every_Control()
    {
        var window = Open();
        try
        {
            window.Shell.Navigate(GalleryRoute.Demo("settings"));
            Pump();
            var frame = window.GetVisualDescendants().OfType<DemoFrame>().Single();
            frame.Navigate(new DemoIntent("settings", View: "preferences"));
            Pump();
            var swatches = window.GetVisualDescendants().OfType<WrapPanel>().Single(p => AutomationProperties.GetName(p) == "Accent color");
            var probe = new Border();
            ArcTokens.Bind(probe, Border.BackgroundProperty, ArcDesignTokenKind.Accent);
            swatches.Children.Add(probe);
            Pump();
            var before = ((ISolidColorBrush)probe.Background!).Color;

            var blue = swatches.GetVisualDescendants().OfType<ToggleButton>().Single(b => AutomationProperties.GetName(b) == "Blue");
            blue.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            for (var i = 0; i < 20 && window.Shell.Appearance.Accent != ArcAccentPreset.Blue; i++)
            {
                Thread.Sleep(20);
                Pump();
            }
            Pump();

            window.Shell.Appearance.Accent.ShouldBe(ArcAccentPreset.Blue);
            blue.IsChecked.ShouldBe(true);
            ((ISolidColorBrush)probe.Background!).Color.ShouldNotBe(before, "the theme's accent token follows the swatch");
        }
        finally
        {
            window.Shell.Appearance.SetAccentAsync(ArcAccentPreset.Neutral).GetAwaiter().GetResult();
            Pump();
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Top_Menu_Has_A_Demo_Item_That_Opens_The_Overview()
    {
        var window = Open();
        try
        {
            var item = window.TopBar.Header.Items.Last();
            item.Value.ShouldBe("demo");
            item.Label.ShouldBe("Demo");
            window.TopBar.Header.RaiseEvent(new SiteHeaderNavigateEventArgs(SiteHeader.NavigateEvent, window.TopBar.Header, SiteHeaderNavigateKind.Item, item.Label, null, item));
            Pump();
            window.Shell.Route.ShouldBe(GalleryRoute.Demo("overview"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Every_Page_Opens_From_The_Sidebar_And_Marks_The_Breadcrumb()
    {
        var window = Open();
        try
        {
            window.Shell.Navigate(GalleryRoute.Demo());
            Pump();
            var frame = window.GetVisualDescendants().OfType<DemoFrame>().Single();
            foreach (var page in DemoPages.App)
            {
                frame.Navigate(new DemoIntent(page.Slug));
                Pump();
                window.Shell.Route.Path.ShouldBe($"/demo/{page.Slug}");
                frame.Current!.Page.ShouldBe(page);
                frame.GetVisualDescendants().OfType<TextMorph>().First().Text.ShouldBe(page.Title);
                frame.Current.GetVisualDescendants().OfType<ScrollViewer>().First().Offset.Y.ShouldBe(0, $"{page.Slug} opens at the top");
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Search_Intent_Opens_The_View_It_Names()
    {
        var window = Open();
        try
        {
            window.Shell.Navigate(GalleryRoute.Demo());
            Pump();
            var frame = window.GetVisualDescendants().OfType<DemoFrame>().Single();
            frame.Navigate(new DemoIntent("projects", "board"));
            Pump();
            frame.Current!.CurrentView.ShouldBe("board");
            frame.Navigate(new DemoIntent("settings", "security"));
            Pump();
            frame.Current!.CurrentView.ShouldBe("security");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Signing_Out_Leads_To_The_Sign_In_Page_And_The_App_Waits_For_A_Sign_In()
    {
        var window = Open();
        try
        {
            window.Shell.Navigate(GalleryRoute.Demo("billing"));
            Pump();
            var app = window.GetVisualDescendants().OfType<DemoAppView>().Single();
            await app.SignOutAsync();
            Pump();
            window.Shell.Route.ShouldBe(GalleryRoute.Demo("login"));
            window.GetVisualDescendants().OfType<LoginSplit>().ShouldHaveSingleItem();

            // Signed out, an app page and /demo lead to the sign-in page.
            window.Shell.Navigate(GalleryRoute.Demo("team"));
            Pump();
            window.Shell.Route.ShouldBe(GalleryRoute.Demo("login"));

            DemoSession.SignIn(new DemoAccount("Marcus Johnson", "marcus@northwind.studio"));
            DemoSession.Account!.Email.ShouldBe("marcus@northstar.studio");
            window.Shell.Navigate(GalleryRoute.Demo("login"));
            Pump();
            window.Shell.Route.ShouldBe(GalleryRoute.Demo("overview"), "a signed-in visitor has nothing to sign in to");
        }
        finally
        {
            DemoSession.SignIn(DemoSession.Emma);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Sidebar_Becomes_A_Drawer_Below_1100_And_A_Rail_When_Collapsed()
    {
        var window = Open();
        try
        {
            window.Shell.Navigate(GalleryRoute.Demo());
            Pump();
            var frame = window.GetVisualDescendants().OfType<DemoFrame>().Single();
            frame.ToggleCollapsed();
            frame.IsSidebarCollapsed.ShouldBeTrue();
            frame.ToggleCollapsed();

            window.Width = 900;
            Pump();
            window.GetVisualDescendants().OfType<DemoNav>().Where(nav => nav.IsEffectivelyVisible).ShouldBeEmpty("the drawer is closed");
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void The_Greeting_Uses_A_First_Name_Only_When_It_Is_One()
    {
        new DemoAccount("Emma Collins", "emma@northstar.studio").FirstName.ShouldBe("Emma");
        new DemoAccount("Emma", "emma@northstar.studio").FirstName.ShouldBe(string.Empty);
        new DemoAccount("Emma", "e.collins@northstar.studio").FirstName.ShouldBe("Emma");
    }
}
