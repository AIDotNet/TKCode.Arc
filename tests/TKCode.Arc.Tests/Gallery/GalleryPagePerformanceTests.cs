using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Pages;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>
/// What a page costs to open: demos below the first screen are built when they are first seen, a page is not built again
/// for the row that is already open, and arrow keys held down build only the page they stop on.
/// </summary>
public class GalleryPagePerformanceTests
{
    private static GalleryEntry Entry(string className) =>
        GalleryCatalog.AllEntries.First(e => e.ClassName == className);

    private static GalleryWindow Open()
    {
        var window = new GalleryWindow { Width = 1440, Height = 900 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        window.Select(GalleryCatalog.Categories[0].Entries[0]);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static GalleryPageView Page(Visual root) => root.GetVisualDescendants().OfType<GalleryPageView>().Single();

    private static async Task Laid(GalleryPageView page)
    {
        var view = page.Docs.ShouldNotBeNull().View.ShouldNotBeNull();
        for (var i = 0; i < 300; i++)
        {
            view.ApplyLatest();
            Dispatcher.UIThread.RunJobs();
            if (view.Snapshot is { IsComplete: true } && page.SectionTitles.Count > 0)
            {
                return;
            }
            await Task.Delay(10);
        }
        throw new TimeoutException("The docs page was not laid out.");
    }

    [AvaloniaFact]
    public async Task Demos_Below_The_First_Screen_Are_Built_When_Seen()
    {
        var page = (GalleryPageView)GalleryPages.Create(Entry("TagInput"));
        var window = TestHelpers.Show(page);
        try
        {
            window.Width = 1440;
            window.Height = 900;
            Dispatcher.UIThread.RunJobs();
            await Laid(page);
            window.UpdateLayout();

            var demos = page.GetVisualDescendants().OfType<GalleryDemoView>().ToList();
            demos.Count.ShouldBeGreaterThan(5);
            var hero = demos.Single(d => d.IsHero);
            hero.IsRealized.ShouldBeTrue();
            hero.View.ShouldNotBeNull();
            var waiting = demos.Where(d => !d.IsRealized).ToList();
            waiting.ShouldNotBeEmpty("demos far below the first screen wait until they are seen");
            waiting.ShouldAllBe(d => d.View == null);

            // A jump from "On this page" builds every demo above where it lands first, so the page does not move under it.
            page.ScrollToSection(page.SectionTitles.Count - 1);
            Dispatcher.UIThread.RunJobs();
            waiting.ShouldAllBe(d => d.IsRealized && d.View != null);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Demo_Scrolled_Into_View_Is_Built()
    {
        var stack = new StackPanel();
        stack.Children.Add(new Border { Height = 2000 });
        var demo = new GalleryDemoView("Button.Sizes");
        stack.Children.Add(demo);
        var scroll = new ScrollViewer { Content = stack };
        var window = TestHelpers.Show(scroll);
        try
        {
            window.Width = 800;
            window.Height = 600;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            demo.IsRealized.ShouldBeFalse();

            scroll.Offset = new Vector(0, 1800);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            demo.IsRealized.ShouldBeTrue();
            TopLevel.GetTopLevel(demo.View.ShouldNotBeNull()).ShouldBe(window);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Selecting_The_Open_Row_Again_Keeps_The_Page()
    {
        var window = Open();
        try
        {
            var page = Page(window);
            window.Sidebar.Select(window.Sidebar.SelectedEntry!);
            Dispatcher.UIThread.RunJobs();
            Page(window).ShouldBeSameAs(page);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Held_Down_Build_Only_The_Page_They_Stop_On()
    {
        var window = Open();
        try
        {
            var first = Page(window);
            window.Sidebar.FocusSearch();
            for (var i = 0; i < 4; i++)
            {
                window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
            }
            var selected = window.Sidebar.SelectedEntry.ShouldNotBeNull();
            window.Sidebar.IsSelectionSettling.ShouldBeTrue();
            Page(window).ShouldBeSameAs(first, "no page is built while the selection is still moving");

            TestHelpers.PumpUntil(() => !window.Sidebar.IsSelectionSettling, 2000).ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            var opened = Page(window);
            opened.ShouldNotBeSameAs(first);
            window.Shell.Route.ShouldBe(GalleryRoute.To(selected));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Views_That_Wait_To_Be_Seen_Do_Not_Outlive_Their_Page()
    {
        // The window stays open: a viewport listener left behind would keep the views reachable from it.
        var host = new StackPanel();
        var window = TestHelpers.Show(new ScrollViewer { Content = host });
        try
        {
            window.Width = 800;
            window.Height = 600;
            var (shot, seenDemo, waitingDemo) = AddAndRemove(host, window);
            TestHelpers.Collect(shot, seenDemo, waitingDemo);
            shot.IsAlive.ShouldBeFalse("a screenshot that left the page");
            seenDemo.IsAlive.ShouldBeFalse("a demo that was built");
            waitingDemo.IsAlive.ShouldBeFalse("a demo still waiting to be seen");
        }
        finally
        {
            window.Close();
        }
    }

    // In a method of its own: a Debug build keeps a method's locals alive to its end.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (WeakReference Shot, WeakReference Seen, WeakReference Waiting) AddAndRemove(StackPanel host, Window window)
    {
        var shotView = new GalleryShotView("Docs/TextFields/Input/images/WorkspaceForm", "A workspace settings form");
        var seen = new GalleryDemoView("Button.Sizes");
        var waiting = new GalleryDemoView("Button.Disabled");
        host.Children.Add(shotView);
        host.Children.Add(seen);
        host.Children.Add(new Border { Height = 3000 });
        host.Children.Add(waiting);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        shotView.Bitmap.ShouldNotBeNull("the capture is in view");
        seen.IsRealized.ShouldBeTrue();
        waiting.IsRealized.ShouldBeFalse();
        host.Children.Clear();
        Dispatcher.UIThread.RunJobs();
        return (new WeakReference(shotView), new WeakReference(seen), new WeakReference(waiting));
    }
}
