using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.Motion;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>The home page, the top bar's menu and GitHub button, and the shell's route.</summary>
public class GalleryHomeTests
{
    private static GalleryWindow Open(double width = 1280, double height = 860)
    {
        var window = new GalleryWindow { Width = width, Height = height };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static GalleryEntry Changelog => GalleryCatalog.Docs.Entries.First(e => e.ClassName == "DocsChangelog");

    [AvaloniaFact]
    public void The_Gallery_Opens_On_The_Home_Page_Without_A_Sidebar()
    {
        var window = Open();
        try
        {
            window.Shell.Route.ShouldBe(GalleryRoute.Home);
            window.Sidebar.IsVisible.ShouldBeFalse();
            window.TopBar.MarkedNavIndex.ShouldBe(-1, "no menu item is marked on the home page");
            window.GetVisualDescendants().OfType<GalleryHomeView>().ShouldHaveSingleItem();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Page_Opens_With_The_Sidebar_Raises_The_Route_And_Marks_Its_Menu_Item()
    {
        var window = Open();
        try
        {
            var raised = new List<GalleryRoute>();
            window.Shell.RouteChanged += raised.Add;

            window.Shell.Navigate(window.Shell.RouteFor(GalleryRouteKind.Components));
            Dispatcher.UIThread.RunJobs();
            window.Sidebar.IsVisible.ShouldBeTrue();
            window.Sidebar.SelectedEntry.ShouldBe(GalleryCatalog.Categories[0].Entries[0]);
            window.TopBar.MarkedNavIndex.ShouldBe(0);
            raised.ShouldHaveSingleItem().Path.ShouldBe("/components/button");

            window.Shell.Navigate(GalleryRoute.To(Changelog));
            window.TopBar.MarkedNavIndex.ShouldBe(3);
            window.Shell.Navigate(window.Shell.RouteFor(GalleryRouteKind.Docs));
            window.TopBar.MarkedNavIndex.ShouldBe(2);
            window.Shell.Navigate(window.Shell.RouteFor(GalleryRouteKind.Blocks));
            window.TopBar.MarkedNavIndex.ShouldBe(1);
            raised.Select(r => r.Kind).ShouldBe([GalleryRouteKind.Components, GalleryRouteKind.Docs, GalleryRouteKind.Docs, GalleryRouteKind.Blocks]);
            raised.Select(r => r.Entry!.ClassName).ShouldBe([window.Shell.RouteFor(GalleryRouteKind.Components).Entry!.ClassName, "DocsChangelog", "DocsIntroduction", GalleryCatalog.Blocks.AllEntries.First().ClassName]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Sidebar_And_The_Brand_Move_The_Route()
    {
        var window = Open();
        try
        {
            var raised = new List<GalleryRoute>();
            window.Shell.RouteChanged += raised.Add;
            var entry = GalleryCatalog.Categories[1].Entries[0];

            window.Sidebar.Select(entry);
            window.Shell.Route.ShouldBe(GalleryRoute.To(entry));
            window.Shell.Navigate(GalleryRoute.Home);
            window.Sidebar.IsVisible.ShouldBeFalse();
            window.Shell.Route.ShouldBe(GalleryRoute.Home);
            window.TopBar.MarkedNavIndex.ShouldBe(-1);
            raised.Select(r => r.Path).ShouldBe([GalleryRoute.To(entry).Path, "/"]);

            // The components item returns to the page that was open last.
            window.Shell.RouteFor(GalleryRouteKind.Components).ShouldBe(GalleryRoute.To(entry));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Searching_From_The_Home_Page_Opens_The_Components_First()
    {
        var window = Open();
        try
        {
            window.Shell.FocusSearch();
            Dispatcher.UIThread.RunJobs();
            window.Shell.Route.Kind.ShouldBe(GalleryRouteKind.Components);
            window.Sidebar.IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Search_Shortcut_Works_On_The_Home_Page_With_Nothing_Focused()
    {
        // As in the browser host: the shell is the window's content and there is no GalleryWindow to catch the shortcut.
        var shell = new GalleryShell();
        var window = TestHelpers.Show(shell);
        try
        {
            Dispatcher.UIThread.RunJobs();
            shell.Route.ShouldBe(GalleryRoute.Home);
            window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            shell.Route.Kind.ShouldBe(GalleryRouteKind.Components);
            shell.Sidebar.IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Live_Examples_Are_Built_On_Demand_And_Open_Their_Pages()
    {
        var window = Open();
        try
        {
            var home = window.GetVisualDescendants().OfType<GalleryHomeView>().Single();
            home.Cards.Count.ShouldBe(4);
            foreach (var card in home.Cards)
            {
                card.EnsureCreated();
                card.IsCreated.ShouldBeTrue();
                card.Entry.ShouldNotBeNull();
            }
            home.Cards.Select(c => c.Entry.ClassName).ShouldBe(["ButtonGroup", "Gauge", "ChatThread", "CommandPalette"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Github_Button_Shows_The_Star_Count_When_It_Is_Known_And_Nothing_When_It_Is_Not()
    {
        var original = GalleryGitHub.StarsProvider;
        try
        {
            GalleryGitHub.StarsProvider = () => Task.FromResult<int?>(1234);
            var known = new GalleryGitHubButton();
            var window = TestHelpers.Show(known);
            try
            {
                Dispatcher.UIThread.RunJobs();
                known.CountText.ShouldBe("1.2k");
            }
            finally
            {
                window.Close();
            }

            GalleryGitHub.StarsProvider = () => Task.FromResult<int?>(null);
            var unknown = new GalleryGitHubButton();
            window = TestHelpers.Show(unknown);
            try
            {
                Dispatcher.UIThread.RunJobs();
                unknown.CountText.ShouldBeNull();
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            GalleryGitHub.StarsProvider = original;
        }
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(872, "872")]
    [InlineData(1000, "1k")]
    [InlineData(1234, "1.2k")]
    [InlineData(15300, "15.3k")]
    public void Star_Counts_Are_Short(int stars, string text) => GalleryGitHub.Format(stars).ShouldBe(text);
}
