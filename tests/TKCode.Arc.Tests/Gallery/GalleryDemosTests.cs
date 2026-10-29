using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Gallery.Pages;
using TKCode.Arc.Gallery.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Localization;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>Live demos in docs pages: the generated registry, the demo block and the docs page that shows them.</summary>
public class GalleryDemosTests
{
    private static GalleryEntry Entry(string className) => GalleryCatalog.AllEntries.First(e => e.ClassName == className);

    private static ArcLanguageManager Languages() => ArcLanguageManager.Current!;

    /// <summary>Opens <paramref name="className"/>'s page and waits until StreamMark has parsed and laid out its document.</summary>
    private static async Task<(GalleryPageView Page, Window Window)> OpenDocsPage(string className, double width = 1440)
    {
        var page = (GalleryPageView)GalleryPages.Create(Entry(className));
        var window = TestHelpers.Show(page);
        window.Width = width;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        await Laid(page);
        return (page, window);
    }

    /// <summary>StreamMark parses on a worker: waits until the page shows the complete document and has listed its sections.</summary>
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

    private static List<GalleryDemoView> Demos(Visual root) => root.GetVisualDescendants().OfType<GalleryDemoView>().ToList();

    [AvaloniaFact]
    public void Every_Demo_Renders_And_Has_Its_Files()
    {
        GalleryDemos.All.ShouldNotBeEmpty();
        foreach (var demo in GalleryDemos.All)
        {
            var view = demo.Files[0];
            view.Path.ShouldStartWith("Demos/", customMessage: demo.Id);
            view.Path.ShouldEndWith($"/{demo.Component}/{demo.Name}.axaml", customMessage: demo.Id);
            view.Language.ShouldBe("axaml", demo.Id);
            view.Source.ShouldStartWith("<UserControl", customMessage: demo.Id);
            demo.Files.Skip(1).ShouldAllBe(f => f.Path == view.Path + ".cs" && f.Language == "cs", demo.Id);

            var control = demo.Create();
            // The compiled XAML ran: a generated view class loads it as well as a hand-written code-behind does.
            control.ShouldBeAssignableTo<UserControl>(demo.Id).Content.ShouldNotBeNull(demo.Id);
            var window = TestHelpers.Show(control);
            try
            {
                window.Width = 800;
                window.Height = 600;
                Dispatcher.UIThread.RunJobs();
                // A built-in TextBox has no template of its own (nothing supplies one): a demo uses Arc's fields.
                control.GetVisualDescendants().OfType<TextBox>().Where(t => t.IsEffectivelyVisible)
                       .ShouldAllBe(t => t.GetVisualChildren().Any(), demo.Id);
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void A_Demo_Shows_Its_Axaml_And_Its_Code_Behind_When_It_Has_One()
    {
        var variants = GalleryDemos.Get("Button.Variants").ShouldNotBeNull();
        variants.Files.Select(f => f.Path).ShouldBe(["Demos/Buttons/Button/Variants.axaml"]);
        variants.Files[0].Source.ShouldContain("<arc:Button Content=\"Save changes\" Variant=\"Primary\" />");

        var booking = GalleryDemos.Get("DateRangePicker.Booking").ShouldNotBeNull();
        booking.Files.Select(f => f.Path).ShouldBe(["Demos/Pickers/DateRangePicker/Booking.axaml", "Demos/Pickers/DateRangePicker/Booking.axaml.cs"]);
        booking.Files[1].Source.ShouldContain("new DateRangePreset(\"This weekend\", WeekendFrom)");
        var picker = ((UserControl)booking.Create()).Content.ShouldBeOfType<DateRangePicker>();
        picker.Label.ShouldBe("Stay");
        picker.MinDate.ShouldBe(DateOnly.FromDateTime(DateTime.Today));
    }

    [AvaloniaFact]
    public void Every_Translation_Shows_The_Same_Demos_As_The_English_Page()
    {
        var withDemos = GalleryDocs.ClassNames.Where(GalleryDocs.HasDemos).ToList();
        withDemos.ShouldContain("Button");
        foreach (var className in withDemos)
        {
            var english = GalleryDocs.DemoIds(className, GalleryDocs.SourceLanguage);
            english.ShouldAllBe(id => GalleryDemos.Get(id) != null, className);
            foreach (var language in GalleryDocs.Languages(className))
            {
                GalleryDocs.DemoIds(className, language).ShouldBe(english, $"{className} ({language}): demos");
            }
        }
        // A component's page is implemented by its demos alone.
        GalleryPages.IsImplemented("Button").ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Shown_And_Copied_Markdown_Turn_Demo_Tags_Into_Blocks_And_Sources()
    {
        var doc = GalleryDocs.Get("Button", "en-US").ShouldNotBeNull();
        doc.Markdown.ShouldNotContain("<Demo ", Case.Sensitive);
        // The demo above the first section is the page's preview.
        doc.Markdown.ShouldStartWith("<demo name=\"Button.Variants\" hero=\"true\"></demo>");
        doc.Markdown.ShouldContain("<demo name=\"Button.Sizes\"></demo>");

        doc.CopyMarkdown.ShouldNotContain("<demo");
        doc.CopyMarkdown.ShouldContain("```xml\n<UserControl");
        doc.CopyMarkdown.ShouldContain("x:Class=\"TKCode.Arc.Gallery.Demos.ButtonSizesDemo\"");
        doc.CopyMarkdown.ShouldContain("```cs\nusing Avalonia.Controls;");
    }

    [AvaloniaFact]
    public async Task Docs_Page_Shows_Live_Demos_And_Lists_Its_Sections()
    {
        var (page, window) = await OpenDocsPage("Button");
        try
        {
            page.IsDocsPage.ShouldBeTrue();
            var demos = Demos(page);
            demos.Select(d => d.DemoId).ShouldBe(["Button.Variants", "Button.Sizes", "Button.LoadingMorph", "Button.Disabled"]);
            demos.Select(d => d.IsHero).ShouldBe([true, false, false, false]);
            demos.ShouldAllBe(d => d.Demo != null);
            demos[0].GetVisualDescendants().OfType<Button>().Select(b => b.Content).ShouldContain("Save changes");

            page.SectionTitles.ShouldBe(["Preview", "When to use", "When not to use", "Usage", "API reference", "Accessibility", "Motion", "Related"]);
            var toc = page.GetVisualDescendants().OfType<GalleryToc>().Single();
            toc.IsVisible.ShouldBeTrue();

            var copyPage = page.GetVisualDescendants().OfType<CopyButton>().Single(c => c.Label == "Copy page");
            copyPage.Value.ShouldBe(GalleryDocs.Get("Button", "en-US")!.CopyMarkdown);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Code_Tab_Shows_The_Demo_Source_And_Keeps_The_Live_Control()
    {
        var (page, window) = await OpenDocsPage("DateRangePicker");
        try
        {
            var demo = Demos(page).Single(d => d.DemoId == "DateRangePicker.Booking");
            var picker = demo.GetVisualDescendants().OfType<DateRangePicker>().Single();
            demo.Code.ShouldBeEmpty();

            demo.ShowCode(true);
            Dispatcher.UIThread.RunJobs();
            demo.IsShowingCode.ShouldBeTrue();
            var code = demo.Code;
            code.Select(c => c.FileName).ShouldBe(["Demos/Pickers/DateRangePicker/Booking.axaml", "Demos/Pickers/DateRangePicker/Booking.axaml.cs"]);
            code.Select(c => c.Language).ShouldBe(["axaml", "cs"]);
            code.Select(c => c.Code).ShouldBe(demo.Demo!.Files.Select(f => f.Source));
            code.ShouldAllBe(c => c.IsEffectivelyVisible);
            picker.IsEffectivelyVisible.ShouldBeFalse();

            demo.ShowCode(false);
            Dispatcher.UIThread.RunJobs();
            code.ShouldAllBe(c => !c.IsEffectivelyVisible);
            demo.GetVisualDescendants().OfType<DateRangePicker>().Single().ShouldBeSameAs(picker);
            picker.IsEffectivelyVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Clicking_A_Demos_Tabs_Switches_Between_Preview_And_Code()
    {
        var (page, window) = await OpenDocsPage("Input");
        try
        {
            var demo = Demos(page)[0];
            var tabs = demo.GetVisualDescendants().OfType<Tabs>().Single();
            tabs.SelectedIndex.ShouldBe(0);

            Click(window, (TabItem)tabs.ContainerFromIndex(1)!);
            demo.IsShowingCode.ShouldBeTrue();
            tabs.SelectedIndex.ShouldBe(1);
            demo.Code.ShouldNotBeEmpty();
            demo.Code.ShouldAllBe(c => c.IsEffectivelyVisible);

            Click(window, (TabItem)tabs.ContainerFromIndex(0)!);
            demo.IsShowingCode.ShouldBeFalse();
            tabs.SelectedIndex.ShouldBe(0);

            // Switched from code (not by a click), the selection follows.
            demo.ShowCode(true);
            tabs.SelectedIndex.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Switching_A_Demos_Tabs_Does_Not_Scroll_The_Page()
    {
        var (page, window) = await OpenDocsPage("Input");
        try
        {
            var scroll = page.GetVisualDescendants().OfType<ScrollViewer>().First();
            scroll.Offset.Y.ShouldBe(0);
            var tabs = Demos(page)[0].GetVisualDescendants().OfType<Tabs>().Single();

            Click(window, (TabItem)tabs.ContainerFromIndex(1)!);
            scroll.Offset.Y.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    private static void Click(Window window, Control target)
    {
        var point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task Docs_Page_Follows_The_Language()
    {
        var (page, window) = await OpenDocsPage("Switch");
        var languages = Languages();
        try
        {
            page.SectionTitles[0].ShouldBe("Preview");
            languages.Set(LanguageTags.ZhCN);
            Dispatcher.UIThread.RunJobs();
            page.Docs!.Text.ShouldNotBeNull().ShouldContain("## 何时使用");
            for (var i = 0; i < 300 && !page.SectionTitles.Contains("何时使用"); i++)
            {
                page.Docs.View!.ApplyLatest();
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }
            page.SectionTitles[0].ShouldBe("预览");
            page.SectionTitles.ShouldContain("何时使用");
            Demos(page).Select(d => d.DemoId).ShouldBe(["Switch.SettingsList", "Switch.Disabled"]);
        }
        finally
        {
            languages.Set(LanguageTags.EnUS);
            window.Close();
        }
    }
}
