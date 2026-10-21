using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Motion;
using TKCode.Arc.Gallery.Pages;
using TKCode.Arc.Gallery.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>The gallery shell: sidebar search / filter / selection, page chrome, "On this page" and keyboard shortcuts.</summary>
public class GalleryShellTests
{
    private static GalleryEntry Entry(string className) =>
        GalleryCatalog.Categories.SelectMany(c => c.Entries).First(e => e.ClassName == className);

    private static GalleryWindow Open(double width = 1280, double height = 860)
    {
        var window = new GalleryWindow { Width = width, Height = height };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static T Find<T>(GalleryWindow window) where T : Visual => window.GetVisualDescendants().OfType<T>().First();

    [AvaloniaFact]
    public void Opens_On_The_First_Component()
    {
        var window = Open();
        try
        {
            window.Sidebar.SelectedEntry.ShouldBe(GalleryCatalog.Categories[0].Entries[0]);
            Find<GalleryPageView>(window).ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Search_Narrows_The_Sidebar_By_Title_Class_And_Slug()
    {
        var window = Open();
        try
        {
            window.Sidebar.VisibleEntries.Count.ShouldBe(GalleryCatalog.AllEntries.Count());

            window.Sidebar.SearchText = "button";
            var buttons = window.Sidebar.VisibleEntries;
            buttons.ShouldNotBeEmpty();
            buttons.ShouldAllBe(e => e.Title.Contains("button", StringComparison.OrdinalIgnoreCase) ||
                                     e.ClassName.Contains("button", StringComparison.OrdinalIgnoreCase) ||
                                     e.Slug.Contains("button", StringComparison.OrdinalIgnoreCase));

            window.Sidebar.SearchText = "no such component";
            window.Sidebar.VisibleEntries.ShouldBeEmpty();

            window.Sidebar.SearchText = string.Empty;
            window.Sidebar.VisibleEntries.Count.ShouldBe(GalleryCatalog.AllEntries.Count());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ready_And_Planned_Filters_Split_The_Catalog()
    {
        var window = Open();
        try
        {
            window.Sidebar.FilterIndex = 1;
            var ready = window.Sidebar.VisibleEntries;
            ready.ShouldNotBeEmpty();
            ready.ShouldAllBe(e => GalleryPages.IsImplemented(e.ClassName));

            window.Sidebar.FilterIndex = 2;
            var planned = window.Sidebar.VisibleEntries;
            planned.ShouldAllBe(e => !GalleryPages.IsImplemented(e.ClassName));

            (ready.Count + planned.Count).ShouldBe(GalleryCatalog.AllEntries.Count());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Selecting_An_Entry_Opens_Its_Page()
    {
        var window = Open();
        try
        {
            window.Select(Entry("Switch"));
            Dispatcher.UIThread.RunJobs();

            window.Sidebar.SelectedEntry.ShouldBe(Entry("Switch"));
            var page = Find<GalleryPageView>(window);
            page.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ShouldContain("Switch");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Extras_Are_Listed_After_Arc_Without_An_Arc_Link()
    {
        var window = Open();
        try
        {
            var markdown = GalleryCatalog.Extras.SelectMany(c => c.Entries).First(e => e.ClassName == "Markdown");
            window.Sidebar.VisibleEntries.ShouldContain(markdown);
            window.Select(markdown);
            Dispatcher.UIThread.RunJobs();
            var page = Find<GalleryPageView>(window);
            var texts = page.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            texts.ShouldContain("Ready");
            texts.ShouldNotContain("View on uiarc.dev");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Every_Arc_Component_Has_A_Page()
    {
        GalleryCatalog.Categories.SelectMany(c => c.Entries).Where(e => !GalleryPages.IsImplemented(e.ClassName))
                      .Select(e => e.ClassName).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Planned_Component_Gets_A_Placeholder_Page()
    {
        // Every Arc component has a page now; the placeholder still serves entries added before their page.
        var planned = new GalleryEntry("NotYetBuilt", "Not yet built", "not-yet-built", 9, "A component without a page.");
        GalleryPages.IsImplemented(planned.ClassName).ShouldBeFalse();
        var page = GalleryPages.Create(planned);
        var window = TestHelpers.Show(page);
        try
        {
            window.Width = 1280;
            window.Height = 860;
            Dispatcher.UIThread.RunJobs();
            var texts = page.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            texts.ShouldContain($"Planned · P{planned.Phase}");
            texts.ShouldContain($"{planned.ClassName} is scheduled for phase P{planned.Phase}.");
            // One placeholder section is not enough to need an "On this page" list.
            page.GetVisualDescendants().OfType<GalleryToc>().ShouldAllBe(t => !t.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task On_This_Page_Lists_The_Sections_And_Hides_When_Narrow()
    {
        var wide = Open(1600);
        try
        {
            wide.Select(Entry("ActionButton"));
            Dispatcher.UIThread.RunJobs();
            var page = Find<GalleryPageView>(wide);
            // StreamMark parses the docs page on a worker; the list appears once the document is laid out.
            for (var i = 0; i < 300 && page.SectionTitles.Count == 0; i++)
            {
                page.Docs!.View?.ApplyLatest();
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }
            var toc = Find<GalleryToc>(wide);
            toc.IsVisible.ShouldBeTrue();
            // "Preview" and the seven sections, plus the two links under the list.
            toc.GetVisualDescendants().OfType<GalleryNavRow>().Count().ShouldBe(1 + 7 + 2);
        }
        finally
        {
            wide.Close();
        }

        var narrow = Open(900);
        try
        {
            narrow.Select(Entry("ActionButton"));
            Dispatcher.UIThread.RunJobs();
            var page = Find<GalleryPageView>(narrow);
            for (var i = 0; i < 300 && page.SectionTitles.Count == 0; i++)
            {
                page.Docs!.View?.ApplyLatest();
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }
            page.GetVisualDescendants().OfType<GalleryToc>().ShouldAllBe(t => !t.IsVisible);
        }
        finally
        {
            narrow.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_The_Selection_Through_Visible_Items()
    {
        var window = Open();
        try
        {
            var first = window.Sidebar.SelectedEntry!;
            window.Sidebar.FocusSearch();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            window.Sidebar.SelectedEntry.ShouldNotBe(first);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            window.Sidebar.SelectedEntry.ShouldBe(first);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_In_Search_Opens_The_First_Match_And_Escape_Clears()
    {
        var window = Open();
        try
        {
            window.Sidebar.FocusSearch();
            window.Sidebar.SearchText = "switch";
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            window.Sidebar.SelectedEntry!.ClassName.ShouldBe(window.Sidebar.VisibleEntries[0].ClassName);

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            window.Sidebar.SearchText.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ctrl_K_Focuses_The_Search_Field()
    {
        var window = Open();
        try
        {
            window.Sidebar.IsSearchFocused.ShouldBeFalse();
            window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            window.Sidebar.IsSearchFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
