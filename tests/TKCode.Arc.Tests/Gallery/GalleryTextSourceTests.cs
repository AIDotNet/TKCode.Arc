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

                // Leaving the visual tree and coming back (as navigation does) shows the page it has, without asking again.
                var layers = (Avalonia.Controls.Primitives.VisualLayerManager)window.Content!;
                layers.Child = null;
                Dispatcher.UIThread.RunJobs();
                layers.Child = view;
                Dispatcher.UIThread.RunJobs();
                pending.Requests.ShouldBe(1);
                view.Docs.Text.ShouldBe(expected.Markdown);
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
