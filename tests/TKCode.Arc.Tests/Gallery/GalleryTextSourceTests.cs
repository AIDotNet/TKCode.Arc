using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Gallery.Pages;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>
/// The pages' text (<see cref="GalleryTexts"/>): every docs page and guide is a resource of TKCode.Arc.Gallery.Content, not
/// of the gallery assembly, and a source that fetches a page later, as the WebAssembly host's does, shows it when it
/// arrives, or says that it could not be loaded.
/// </summary>
public class GalleryTextSourceTests
{
    private sealed class PendingSource : IGalleryTextSource
    {
        public Dictionary<string, TaskCompletionSource<string?>> Asked { get; } = new();

        public int Requests { get; private set; }

        public ValueTask<string?> ReadAsync(string path)
        {
            Requests++;
            if (!Asked.TryGetValue(path, out var pending))
            {
                Asked[path] = pending = new TaskCompletionSource<string?>();
            }
            return new ValueTask<string?>(pending.Task);
        }
    }

    [AvaloniaFact]
    public void Every_Page_And_Guide_Is_A_Resource_Of_The_Content_Assembly()
    {
        var missing = new List<string>();
        foreach (var className in GalleryDocs.ClassNames)
        {
            missing.AddRange(GalleryDocs.Languages(className).Select(l => $"Docs/{GalleryDocs.FolderOf(className)}/{l}.md"));
        }
        foreach (var page in GalleryGuides.Pages)
        {
            missing.AddRange(GalleryGuides.Languages.Select(l => $"Guides/{page}/{l}.md"));
        }
        missing.RemoveAll(path => AssetLoader.Exists(new Uri($"avares://TKCode.Arc.Gallery.Content/{path}")));
        missing.ShouldBeEmpty();

        // The gallery assembly is part of every host's startup download; the pages are not.
        AssetLoader.GetAssets(new Uri("avares://TKCode.Arc.Gallery/"), null)
                   .Where(u => u.AbsolutePath.EndsWith(".md", StringComparison.Ordinal))
                   .ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void A_Page_That_Arrives_Later_Is_Shown_When_It_Does()
    {
        var language = GalleryText.CurrentLanguage;
        var expected = GalleryDocs.Get("Button", language).ShouldNotBeNull();
        // Read now, from the gallery's resources: it is put together when it is first asked for.
        var expectedCopy = expected.CopyMarkdown;
        expectedCopy.ShouldContain("<arc:Button");
        var path = $"Docs/Buttons/Button/{expected.Language}.md";
        var original = GalleryTexts.Source;
        var file = original.ReadAsync(path).Result.ShouldNotBeNull();
        var pending = new PendingSource();
        GalleryTexts.Source = pending;
        try
        {
            var view = new GalleryPageView(GalleryCatalog.AllEntries.Single(e => e.ClassName == "Button"));
            var window = TestHelpers.Show(view);
            try
            {
                view.Docs.ShouldNotBeNull().Text.ShouldBeNullOrEmpty("the page has not arrived");
                pending.Asked.Keys.ShouldHaveSingleItem().ShouldBe(path);

                pending.Asked[path].SetResult(file);
                Dispatcher.UIThread.RunJobs();
                view.Docs.Text.ShouldBe(expected.Markdown);

                // The page is shown; "Copy page" waits for the files of the page's demos, each asked for once.
                var copyPage = view.CopyPage.ShouldNotBeNull();
                copyPage.Value.ShouldBeNull();
                var files = GalleryDocs.DemoIds("Button", language).SelectMany(id => GalleryDemos.Get(id)!.Files).ToList();
                files.ShouldNotBeEmpty();
                pending.Asked.Keys.Where(k => k != path).ShouldBe(files.Select(f => f.ContentPath), ignoreOrder: true);
                foreach (var demoFile in files)
                {
                    pending.Asked[demoFile.ContentPath].SetResult(original.ReadAsync(demoFile.ContentPath).Result);
                }
                Dispatcher.UIThread.RunJobs();
                copyPage.Value.ShouldBe(expectedCopy);
                var requests = pending.Requests;
                requests.ShouldBe(1 + files.Count);

                // Leaving the visual tree and coming back (as navigation does) shows the page it has, without asking again.
                window.Content = null;
                Dispatcher.UIThread.RunJobs();
                window.Content = view;
                Dispatcher.UIThread.RunJobs();
                pending.Requests.ShouldBe(requests);
                view.Docs.Text.ShouldBe(expected.Markdown);
                copyPage.Value.ShouldBe(expectedCopy);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        }
        finally
        {
            GalleryTexts.Source = original;
        }
    }

    [AvaloniaFact]
    public void Every_Demo_File_Is_A_Resource_Of_The_Content_Assembly()
    {
        var files = GalleryDemos.All.SelectMany(d => d.Files).ToList();
        files.Count.ShouldBeGreaterThan(1000);
        files.Where(f => !AssetLoader.Exists(new Uri($"avares://TKCode.Arc.Gallery.Content/{f.ContentPath}")))
             .Select(f => f.Path)
             .ShouldBeEmpty();

        // Read as written: LF line endings and nothing after the last line, whatever the checkout's line endings are.
        var variants = GalleryDemos.Get("Button.Variants").ShouldNotBeNull().Files[0];
        variants.ContentPath.ShouldBe("Demos/Buttons/Button/Variants.axaml.txt");
        variants.Source.ShouldStartWith("<UserControl");
        variants.Source.ShouldNotContain("\r");
        variants.Source.ShouldEndWith("</UserControl>");
        variants.ReadAsync().Result.ShouldBe(variants.Source);
    }

    [AvaloniaFact]
    public void A_Demo_File_That_Arrives_Later_Fills_The_Code_Tab_And_A_Missing_One_Is_Asked_Again()
    {
        var demo = GalleryDemos.Get("DateRangePicker.Booking").ShouldNotBeNull();
        var original = GalleryTexts.Source;
        var sources = demo.Files.Select(f => f.Source).ToList();
        sources.Count.ShouldBe(2);
        var pending = new PendingSource();
        GalleryTexts.Source = pending;
        try
        {
            var view = new GalleryDemoView(demo.Id);
            var window = TestHelpers.Show(view);
            try
            {
                view.Realize();
                Dispatcher.UIThread.RunJobs();
                pending.Requests.ShouldBe(0, "the source is read when the Code tab is opened");

                view.ShowCode(true);
                Dispatcher.UIThread.RunJobs();
                view.Code.Select(c => c.FileName).ShouldBe(demo.Files.Select(f => f.Path));
                view.Code.ShouldAllBe(c => string.IsNullOrEmpty(c.Code));
                pending.Asked.Keys.ShouldBe(demo.Files.Select(f => f.ContentPath), ignoreOrder: true);

                // The first file arrives with CRLF line endings; the second cannot be read.
                pending.Asked[demo.Files[0].ContentPath].SetResult(sources[0].Replace("\n", "\r\n") + "\r\n");
                pending.Asked[demo.Files[1].ContentPath].SetResult(null);
                Dispatcher.UIThread.RunJobs();
                view.Code[0].Code.ShouldBe(sources[0]);
                view.Code[1].Code.ShouldBeNullOrEmpty();

                // Opening the tab again asks for the missing file only.
                pending.Asked.Remove(demo.Files[1].ContentPath);
                view.ShowCode(false);
                view.ShowCode(true);
                Dispatcher.UIThread.RunJobs();
                pending.Requests.ShouldBe(3);
                pending.Asked[demo.Files[1].ContentPath].SetResult(sources[1]);
                Dispatcher.UIThread.RunJobs();
                view.Code[1].Code.ShouldBe(sources[1]);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        }
        finally
        {
            GalleryTexts.Source = original;
        }
    }

    [AvaloniaFact]
    public void A_Page_That_Cannot_Be_Loaded_Says_So()
    {
        var original = GalleryTexts.Source;
        var pending = new PendingSource();
        GalleryTexts.Source = pending;
        try
        {
            var view = new GalleryPageView(GalleryCatalog.AllEntries.Single(e => e.ClassName == "Button"));
            var window = TestHelpers.Show(view);
            try
            {
                pending.Asked.Values.Single().SetResult(null);
                Dispatcher.UIThread.RunJobs();
                view.Docs.ShouldNotBeNull().Text.ShouldBe("> " + GalleryText.Get(GalleryShellLangResourceKind.PageLoadFailed));
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        }
        finally
        {
            GalleryTexts.Source = original;
        }
    }

    [AvaloniaFact]
    public void A_Guide_That_Arrives_Later_Is_Shown_When_It_Does()
    {
        var language = GalleryText.CurrentLanguage;
        var expected = GalleryGuides.Get("Theming", language).ShouldNotBeNull();
        var path = $"Guides/Theming/{expected.Language}.md";
        var original = GalleryTexts.Source;
        var file = original.ReadAsync(path).Result.ShouldNotBeNull();
        var pending = new PendingSource();
        GalleryTexts.Source = pending;
        try
        {
            var page = global::TKCode.Arc.Gallery.Pages.DocumentPage.Create("Theming");
            var window = TestHelpers.Show(page);
            try
            {
                var document = ((Border)((ScrollViewer)page).Content!).Child.ShouldBeOfType<Markdown>();
                document.Text.ShouldBeNullOrEmpty("the guide has not arrived");

                pending.Asked[path].SetResult(file);
                Dispatcher.UIThread.RunJobs();
                document.Text.ShouldBe(expected.Markdown);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        }
        finally
        {
            GalleryTexts.Source = original;
        }
    }
}
