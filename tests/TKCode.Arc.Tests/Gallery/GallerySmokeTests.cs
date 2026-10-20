using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Pages;
using TKCode.Arc.Gallery.Shell;
using AtomUI.Theme.Algorithms;
using AtomUI.Theme.Configuration;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>Renders the gallery shell and every implemented page in light and dark (catches theme/template errors).</summary>
public class GallerySmokeTests
{
    [AvaloniaFact]
    public void Catalog_Lists_All_110_Components()
    {
        GalleryCatalog.Count.ShouldBe(110);
        GalleryCatalog.Categories.Length.ShouldBe(22);
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
                foreach (var entry in GalleryCatalog.Categories.SelectMany(c => c.Entries).Where(e => GalleryPages.IsImplemented(e.ClassName)))
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
