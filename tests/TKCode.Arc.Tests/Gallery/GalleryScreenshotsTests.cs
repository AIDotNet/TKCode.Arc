using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>
/// Screenshots in docs pages (<c>![caption](images/Name.png)</c>, captured by tools/docshots): every picture has a light
/// and a dark capture, every capture is shown, translations show the same pictures, and the view follows the theme.
/// </summary>
public class GalleryScreenshotsTests
{
    [AvaloniaFact]
    public void Every_Screenshot_Has_A_Light_And_A_Dark_Capture()
    {
        var missing = new List<string>();
        foreach (var className in GalleryDocs.ClassNames)
        {
            foreach (var shot in GalleryDocs.Screenshots(className, GalleryDocs.SourceLanguage))
            {
                var dark = new Uri(shot.Light.AbsoluteUri.Replace(".webp", ".dark.webp", StringComparison.Ordinal));
                missing.AddRange(new[] { shot.Light, dark }.Where(u => !AssetLoader.Exists(u)).Select(u => $"{className}: {u}"));
            }
        }
        // Run: dotnet tools/docshots/bin/Debug/net10.0/DocShots.dll shoot <the page's en-US.md>
        missing.ShouldBeEmpty(string.Join("\n", missing));
    }

    [AvaloniaFact]
    public void Every_Capture_Is_Shown_By_Its_Page()
    {
        var shown = new HashSet<string>(StringComparer.Ordinal);
        foreach (var className in GalleryDocs.ClassNames)
        {
            foreach (var shot in GalleryDocs.Screenshots(className, GalleryDocs.SourceLanguage))
            {
                shown.Add(shot.Light.AbsolutePath);
                shown.Add(shot.Light.AbsolutePath.Replace(".webp", ".dark.webp", StringComparison.Ordinal));
            }
        }
        var captures = AssetLoader.GetAssets(new Uri("avares://TKCode.Arc.Gallery.Shots/Docs/"), null)
                                  .Where(u => u.AbsolutePath.EndsWith(".webp", StringComparison.Ordinal))
                                  .ToList();
        captures.Count.ShouldBe(shown.Count, "every screenshot has exactly a light and a dark capture");
        captures.Where(u => !shown.Contains(u.AbsolutePath)).ShouldBeEmpty("captures no page shows (DocShots shoot removes them)");
    }

    [AvaloniaFact]
    public void Translations_Show_The_Same_Screenshots()
    {
        foreach (var className in GalleryDocs.ClassNames)
        {
            var english = GalleryDocs.Screenshots(className, GalleryDocs.SourceLanguage).Select(s => s.Name).ToList();
            foreach (var language in GalleryDocs.Languages(className).Where(l => l != GalleryDocs.SourceLanguage))
            {
                GalleryDocs.Screenshots(className, language).Select(s => s.Name).ShouldBe(english, $"{className} ({language})");
            }
        }
    }

    [AvaloniaFact]
    public void A_Screenshot_Line_Becomes_A_Picture_That_Follows_The_Theme()
    {
        var doc = GalleryDocs.Get("Input", "en-US").ShouldNotBeNull();
        doc.Markdown.ShouldContain("<shot src=\"Docs/TextFields/Input/images/WorkspaceForm\" alt=\"");
        doc.Markdown.ShouldNotContain("](images/WorkspaceForm.png)");
        // Copied Markdown keeps the plain image line.
        doc.CopyMarkdown.ShouldContain("](images/WorkspaceForm.png)");
        var shot = GalleryDocs.Screenshots("Input", "zh-CN").ShouldHaveSingleItem();
        shot.Name.ShouldBe("WorkspaceForm");
        shot.Light.ShouldBe(new Uri("avares://TKCode.Arc.Gallery.Shots/Docs/TextFields/Input/images/WorkspaceForm.webp"));

        var view = new GalleryShotView("Docs/TextFields/Input/images/WorkspaceForm", "A workspace settings form");
        var window = TestHelpers.Show(view);
        try
        {
            var light = view.Bitmap.ShouldNotBeNull();
            view.IsDarkShown.ShouldBeFalse();
            // Captures are twice the logical size: the picture shows at half its pixel width.
            view.Bounds.Width.ShouldBeGreaterThan(0);

            window.RequestedThemeVariant = ThemeVariant.Dark;
            Dispatcher.UIThread.RunJobs();
            view.IsDarkShown.ShouldBeTrue();
            view.Bitmap.ShouldNotBeNull().ShouldNotBeSameAs(light);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
        view.Bitmap.ShouldBeNull("a detached view releases its capture");
    }
}
