using TKCode.Arc.Gallery.DemoApp;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.Motion;
using Avalonia.Controls;
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
