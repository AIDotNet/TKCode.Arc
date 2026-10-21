using System.Text;
using AtomUI.Localization;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Gallery.Pages;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>
/// The sidebar's Docs section (<see cref="GalleryGuides"/>): every page in every gallery language, translations that
/// match their English page, examples that load, screenshots in both themes, and a page that follows the language.
/// </summary>
public class GalleryGuidesTests
{
    private static ILanguageManager Languages() => global::AtomUI.ApplicationExtensions.GetLanguageManager(Application.Current!)!;

    [AvaloniaFact]
    public void Every_Docs_Entry_Has_A_Page_In_Every_Language()
    {
        GalleryCatalog.Docs.AllEntries.Select(e => e.ClassName).ShouldBe(GalleryGuides.Pages.Select(p => $"Docs{p}"));
        foreach (var page in GalleryGuides.Pages)
        {
            foreach (var language in GalleryGuides.Languages)
            {
                var guide = GalleryGuides.Get(page, language).ShouldNotBeNull(page);
                guide.Language.ShouldBe(language, $"{page} is not written in {language}");
                guide.Markdown.ShouldStartWith("# ", customMessage: $"{page} ({language})");
            }
        }
    }

    [AvaloniaFact]
    public void Translations_Match_Their_English_Page()
    {
        foreach (var page in GalleryGuides.Pages)
        {
            var englishText = GalleryGuides.Read(page, GalleryDocs.SourceLanguage).ShouldNotBeNull();
            var english = GalleryGuides.Split(englishText).Body;
            foreach (var language in GalleryGuides.Languages.Skip(1))
            {
                var where = $"{page} ({language})";
                var (front, body) = GalleryGuides.Split(GalleryGuides.Read(page, language).ShouldNotBeNull(where));
                // Same rule as the component pages: a translation records the hash of the en-US file it was made from.
                front.GetValueOrDefault("source-hash").ShouldBe(HashOf(englishText), $"{where} was translated from an older en-US.md (tools/docs/dochash.py --stamp)");
                Sections(body).Count.ShouldBe(Sections(english).Count, where + ": sections");
                DocSnippets.Blocks(body).Select(b => b.Language).ShouldBe(DocSnippets.Blocks(english).Select(b => b.Language), where + ": code blocks");
                GalleryDocs.ScreenshotLines(body).Select(s => s.Name).ShouldBe(GalleryDocs.ScreenshotLines(english).Select(s => s.Name), where + ": screenshots");
            }
        }
    }

    [AvaloniaFact]
    public void Xaml_Examples_Load()
    {
        // The runtime XAML loader resolves namespaces from loaded assemblies; nothing else in the tests touches Lucide.
        _ = typeof(TKCode.Arc.Icons.Lucide.LucideIcon).Assembly;
        var failures = new List<string>();
        foreach (var page in GalleryGuides.Pages)
        {
            foreach (var language in GalleryGuides.Languages)
            {
                var body = GalleryGuides.Split(GalleryGuides.Read(page, language)!).Body;
                foreach (var block in DocSnippets.Blocks(body).Where(b => b.IsXaml))
                {
                    if (DocSnippets.ValidateXaml(block.Code) is { } reason)
                    {
                        failures.Add($"{page} ({language}) line {block.Line}: {reason}");
                    }
                }
            }
        }
        failures.ShouldBeEmpty(string.Join("\n", failures));
    }

    [AvaloniaFact]
    public void Screenshots_Have_Both_Captures_And_Every_Capture_Is_Shown()
    {
        var shown = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in GalleryGuides.Pages)
        {
            var body = GalleryGuides.Split(GalleryGuides.Read(page, GalleryDocs.SourceLanguage)!).Body;
            foreach (var (name, _) in GalleryDocs.ScreenshotLines(body))
            {
                foreach (var dark in new[] { false, true })
                {
                    var uri = GalleryDocs.ScreenshotUri($"Guides/{page}/images/{name}", dark);
                    AssetLoader.Exists(uri).ShouldBeTrue($"{uri} is missing (DocShots shoot)");
                    shown.Add(uri.AbsolutePath);
                }
            }
        }
        shown.ShouldNotBeEmpty();
        AssetLoader.GetAssets(new Uri("avares://TKCode.Arc.Gallery.Shots/Guides/"), null)
                   .Where(u => u.AbsolutePath.EndsWith(".png", StringComparison.Ordinal))
                   .Where(u => !shown.Contains(u.AbsolutePath))
                   .ShouldBeEmpty("captures no Docs page shows");
    }

    [AvaloniaFact]
    public void A_Docs_Page_Shows_Its_Screenshots_And_Follows_The_Language()
    {
        var entry = GalleryCatalog.Docs.AllEntries.First(e => e.ClassName == "DocsTheming");
        var page = GalleryPages.Create(entry);
        var window = TestHelpers.Show(page);
        var languages = Languages();
        try
        {
            var markdown = page.GetVisualDescendants().OfType<Markdown>().ShouldHaveSingleItem();
            markdown.Text.ShouldNotBeNull().ShouldStartWith("# Theming");
            markdown.Text.ShouldContain("<shot src=\"Guides/Theming/images/AccentPresets\" alt=\"");
            markdown.Text.ShouldNotContain("](images/");

            languages.ChangeLanguage(LanguageTags.ZhCN);
            Dispatcher.UIThread.RunJobs();
            markdown.Text.ShouldStartWith(GalleryGuides.Get("Theming", "zh-CN")!.Markdown[..10]);
            markdown.Text.ShouldNotStartWith("# Theming");
        }
        finally
        {
            languages.ChangeLanguage(LanguageTags.EnUS);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Untranslated_Language_Shows_The_English_Page_With_A_Notice()
    {
        var guide = GalleryGuides.Get("Installation", "pt-BR").ShouldNotBeNull();
        guide.IsFallback.ShouldBeTrue();
        guide.Language.ShouldBe("en-US");
        guide.Markdown.ShouldStartWith("> This page is not translated yet");
        GalleryGuides.Get("NoSuchPage", "en-US").ShouldBeNull();
    }

    private static List<string> Sections(string markdown)
    {
        var sections = new List<string>();
        var inside = false;
        foreach (var line in markdown.Split('\n'))
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inside = !inside;
            }
            else if (!inside && line.StartsWith("## ", StringComparison.Ordinal))
            {
                sections.Add(line);
            }
        }
        return sections;
    }

    /// <summary>The source-hash of a page: FNV-1a 64-bit of the normalized UTF-8 text, first 12 hex digits (DocPage.HashOf).</summary>
    private static string HashOf(string text)
    {
        var hash = 14695981039346656037UL;
        foreach (var b in Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd()))
        {
            hash ^= b;
            hash *= 1099511628211UL;
        }
        return hash.ToString("x16")[..12];
    }
}
