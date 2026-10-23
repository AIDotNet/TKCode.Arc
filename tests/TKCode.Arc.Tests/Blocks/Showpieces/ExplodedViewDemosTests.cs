using AtomUI.Theme.Algorithms;
using AtomUI.Theme.Configuration;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Docs;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary>The Exploded view's Gallery demos load and render in light and dark, and its catalog entry and page line up.</summary>
public class ExplodedViewDemosTests
{
    private static readonly string[] s_demos = ["Preview", "PageScroll", "SliderProgress", "CustomLayers", "Narrow", "ReducedMotion"];

    [AvaloniaFact]
    public async Task Every_Demo_Renders_In_Light_And_Dark()
    {
        var demos = GalleryDemos.All.Where(d => d.Component == "ExplodedView").ToList();
        demos.Select(d => d.Name).ShouldBe(s_demos, ignoreOrder: true);
        try
        {
            foreach (var dark in new[] { false, true })
            {
                await TestHelpers.ApplyAsync(dark ? new ThemeConfigBuilder().WithAlgorithms(ThemeAlgorithm.Dark).Build() : null);
                foreach (var demo in demos)
                {
                    var control = demo.Create();
                    var window = TestHelpers.Show(control);
                    try
                    {
                        window.Width = 900;
                        window.Height = 1000;
                        for (var i = 0; i < 3; i++)
                        {
                            Dispatcher.UIThread.RunJobs();
                            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                        }
                        var view = control.GetVisualDescendants().OfType<ExplodedView>().ShouldHaveSingleItem(demo.Id);
                        view.Layers.Count.ShouldBeGreaterThanOrEqualTo(3, demo.Id);
                        view.Stage.ShouldNotBeNull(demo.Id).Hosts.Count.ShouldBe(view.Layers.Count, demo.Id);
                        view.Bounds.Width.ShouldBeGreaterThan(0, demo.Id);
                        // The frame and the plate take the theme's colors.
                        var plate = view.Stage!.Hosts[0].GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Plate");
                        var fill = TestHelpers.ColorOf(plate.Background);
                        (dark ? fill.R < 64 : fill.R > 200).ShouldBeTrue($"{demo.Id} plate {fill} in {(dark ? "dark" : "light")}");
                    }
                    finally
                    {
                        window.Close();
                    }
                }
            }
        }
        finally
        {
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public void The_Catalog_Lists_The_Block_Under_Showpieces_With_Its_Page_Summary()
    {
        BlockCatalog.Blocks.ShouldContain(b => b.ClassName == "ExplodedView");
        var entry = BlockCatalog.Blocks.First(b => b.ClassName == "ExplodedView");
        entry.Group.ShouldBe("Showpieces");
        entry.Slug.ShouldBe("exploded-view");
        entry.IsArcPort.ShouldBeFalse();
        var page = GalleryDocs.Get("ExplodedView", GalleryDocs.SourceLanguage).ShouldNotBeNull();
        page.Summary.ShouldBe(entry.Description);
        foreach (var demo in s_demos)
        {
            page.Markdown.ShouldContain($"name=\"ExplodedView.{demo}\"");
        }
    }

    [AvaloniaFact]
    public void The_Sample_Screen_Uses_Arc_Parts()
    {
        var view = new ExplodedView();
        global::TKCode.Arc.Gallery.Demos.ExplodedViewDemoData.Fill(view);
        var window = TestHelpers.Show(view);
        try
        {
            window.Width = 800;
            window.Height = 900;
            Dispatcher.UIThread.RunJobs();
            view.Layers.Select(l => l.Name).ShouldBe(["Base", "Grid", "Data", "Controls", "Overlay"]);
            view.Layers.Select(l => l.Kind).ShouldBe([ExplodedLayerKind.Plate, ExplodedLayerKind.Grid, ExplodedLayerKind.Sheet,
                ExplodedLayerKind.Sheet, ExplodedLayerKind.Sheet]);
            view.GetVisualDescendants().OfType<SegmentedControl>().ShouldHaveSingleItem();
            view.GetVisualDescendants().OfType<Avatar>().Count().ShouldBe(3);
            view.GetVisualDescendants().OfType<ArcGlyph>().Count().ShouldBeGreaterThanOrEqualTo(7);
            view.GetVisualDescendants().OfType<Border>().Count(b => b.BoxShadow.Count > 0).ShouldBeGreaterThanOrEqualTo(1);
        }
        finally
        {
            window.Close();
        }
    }
}
