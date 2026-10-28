using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Pages;
using TKCode.Arc.Gallery.Shell;
using AtomUI.Theme.Algorithms;
using AtomUI.Theme.Configuration;
using System.Collections.ObjectModel;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>Renders the gallery shell and every implemented page in light and dark (catches theme/template errors).</summary>
public class GallerySmokeTests
{
    [AvaloniaFact]
    public void Catalog_Lists_All_112_Components()
    {
        GalleryCatalog.Count.ShouldBe(112);
        GalleryCatalog.Categories.Length.ShouldBe(22);
        GalleryCatalog.Categories.ShouldAllBe(c => c.IsArc && c.Entries.All(e => e.IsArc && e.ArcUrl != null));
        // Extras (TKCode components with no Arc original) are listed separately and not counted.
        GalleryCatalog.Extras.ShouldAllBe(c => !c.IsArc && c.Entries.All(e => !e.IsArc && e.ArcUrl == null));
        GalleryCatalog.Special.ShouldAllBe(c => !c.IsArc && c.Entries.All(e => !e.IsArc && e.ArcUrl == null));
        // Blocks and Special components are listed under their own sections and not counted either.
        GalleryCatalog.AllEntries.Count().ShouldBe(112 + GalleryCatalog.Extras.Sum(c => c.Entries.Length) + GalleryCatalog.Docs.Entries.Length
            + GalleryCatalog.Blocks.AllEntries.Count() + GalleryCatalog.Special.Sum(c => c.Entries.Length) + GalleryCatalog.Mobile.Entries.Length);
        GalleryCatalog.AllEntries.Select(e => e.ClassName).ShouldBeUnique();
        // The sidebar footer counts the whole Components section, extras included: 112 + 5 = 117.
        GalleryCatalog.ComponentCount.ShouldBe(117);
        GalleryCatalog.ComponentEntries.Count().ShouldBe(117);
    }

    [AvaloniaFact]
    public void Breadcrumb_Demo_Opens_Folders_After_The_Path_Is_Cut_Back_To_Its_Root()
    {
        var window = TestHelpers.Show(new TKCode.Arc.Gallery.Demos.BreadcrumbClientSidePathDemo());
        try
        {
            var buttons = window.GetVisualDescendants().OfType<Button>().ToList();
            var open = buttons.First(b => Equals(b.Content, "Open next folder"));
            var up = buttons.First(b => Equals(b.Content, "Up one level"));
            var path = window.GetVisualDescendants().OfType<Breadcrumb>().Select(b => b.ItemsSource).OfType<ObservableCollection<string>>().First();
            void Press(Button button) => ControlAutomationPeer.CreatePeerForElement(button).GetProvider<IInvokeProvider>()!.Invoke();

            Press(up);
            path.ShouldBe(["Home"]);
            Press(open);
            path.Count.ShouldBe(2);
            Press(open);
            Press(open);
            Press(open);
            Press(open);
            path.Count.ShouldBe(6);
            path.Skip(1).ShouldAllBe(p => p.Length > 0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Every_Implemented_Page_Renders_In_Light_And_Dark()
    {
        var window = new GalleryWindow();
        window.Show();
        try
        {
            foreach (var dark in new[] { false, true })
            {
                await TestHelpers.ApplyAsync(dark ? new ThemeConfigBuilder().WithAlgorithms(ThemeAlgorithm.Dark).Build() : null);
                foreach (var entry in GalleryCatalog.AllEntries.Where(e => GalleryPages.IsImplemented(e.ClassName)))
                {
                    var page = GalleryPages.Create(entry);
                    var host = new Avalonia.Controls.Window { Content = page, Width = 1000, Height = 800 };
                    host.Show();
                    Dispatcher.UIThread.RunJobs();
                    page.Bounds.Width.ShouldBeGreaterThan(0, entry.ClassName);
                    host.Close();
                }
            }
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }
}
