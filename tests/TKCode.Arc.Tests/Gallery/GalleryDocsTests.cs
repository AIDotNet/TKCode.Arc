using AtomUI.Localization;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Gallery.Pages;
using TKCode.Arc.Gallery.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>Docs pages: generated docs data, API references, translations and fallbacks, sections and examples.</summary>
public class GalleryDocsTests
{
    private static GalleryEntry Entry(string className) => GalleryCatalog.AllEntries.First(e => e.ClassName == className);

    private static ILanguageManager Languages() => global::AtomUI.ApplicationExtensions.GetLanguageManager(Application.Current!)!;

    [AvaloniaFact]
    public void Every_Page_Belongs_To_A_Gallery_Entry_And_Has_An_English_Source()
    {
        GalleryDocs.ClassNames.ShouldNotBeEmpty();
        foreach (var className in GalleryDocs.ClassNames)
        {
            GalleryCatalog.AllEntries.ShouldContain(e => e.ClassName == className, className);
            GalleryDocs.Languages(className).ShouldContain(GalleryDocs.SourceLanguage, className);
        }
    }

    [AvaloniaFact]
    public void Api_Reference_Is_Generated_From_The_Control_And_Its_Arc_Base_Classes()
    {
        var doc = GalleryDocs.Get("Input", "en-US").ShouldNotBeNull();
        doc.Language.ShouldBe("en-US");
        doc.IsFallback.ShouldBeFalse();
        doc.Title.ShouldBe("Input");

        var markdown = doc.Markdown;
        markdown.ShouldNotContain("<!-- api");
        markdown.ShouldContain("### Input");
        markdown.ShouldContain("| Property | Type | Default | Description |");
        // Declared on FieldTextBox, with the XML summary of its property field.
        markdown.ShouldContain("| `Label` | `string?` | `null` | The visible label above the frame; also the accessible name. |");
        // Defaults come from the Avalonia property metadata.
        markdown.ShouldContain("| `Size` | `ControlSize` | `Medium` |");
        markdown.ShouldContain("| `IsInvalid` | `bool` | `false` |");
        // A direct property without a public setter.
        markdown.ShouldContain("| `EffectiveErrorMessage` | `string?` | – | *Read-only.* The error actually shown");
        markdown.ShouldContain("Pseudo-classes: `:invalid`");
        markdown.ShouldContain("Derives from `TextBox`");
    }

    [AvaloniaFact]
    public void Translations_Use_The_Translated_Page_And_Api_Descriptions()
    {
        var simplified = GalleryDocs.Get("Input", "zh-CN").ShouldNotBeNull();
        simplified.Language.ShouldBe("zh-CN");
        simplified.IsOutdated.ShouldBeFalse();
        simplified.Title.ShouldBe("输入框");
        simplified.Markdown.ShouldContain("## API 参考");
        simplified.Markdown.ShouldContain("| 属性 | 类型 | 默认值 | 说明 |");
        simplified.Markdown.ShouldContain("| `Label` | `string?` | `null` | 外框上方的可见标签，同时作为无障碍名称。 |");
        simplified.Markdown.ShouldContain("| `EffectiveErrorMessage` | `string?` | – | *只读。* 实际显示的错误");

        var traditional = GalleryDocs.Get("Input", "zh-TW").ShouldNotBeNull();
        traditional.Language.ShouldBe("zh-TW");
        traditional.Markdown.ShouldContain("| 屬性 | 型別 | 預設值 | 說明 |");
        traditional.Markdown.ShouldContain("外框上方的可見標籤，同時作為無障礙名稱。");
    }

    [AvaloniaFact]
    public void An_Untranslated_Language_Shows_The_English_Page_With_A_Notice()
    {
        var doc = GalleryDocs.Get("Input", "pt-BR").ShouldNotBeNull();
        doc.Language.ShouldBe("en-US");
        doc.RequestedLanguage.ShouldBe("pt-BR");
        doc.IsFallback.ShouldBeTrue();
        doc.Markdown.ShouldStartWith("> This page is not translated yet");
        doc.Markdown.ShouldContain("## When to use");

        GalleryDocs.Get("NoSuchComponent", "en-US").ShouldBeNull();
    }

    [AvaloniaFact]
    public void A_Component_Without_A_Docs_Page_Shows_Its_Body_And_No_Docs()
    {
        // Every real component has a page, so use an entry that is only planned.
        var planned = new GalleryEntry("NotYetBuilt", "Not yet built", "not-yet-built", 3, "A component that does not exist.");
        GalleryDocs.Has(planned.ClassName).ShouldBeFalse();
        var page = (GalleryPageView)GalleryPages.Create(planned);
        var window = TestHelpers.Show(page);
        try
        {
            page.IsDocsPage.ShouldBeFalse();
            page.GetVisualDescendants().OfType<Markdown>().ShouldBeEmpty();
            page.GetVisualDescendants().OfType<Tabs>().ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Every_Arc_Component_Has_A_Page_In_Every_Language_That_Is_Up_To_Date()
    {
        // The rule for a new component: write Docs/<Category>/<Class>/en-US.md and its translations (design/engineering/gallery-app.md, 6).
        var entries = GalleryCatalog.Categories.SelectMany(c => c.Entries)
            .Concat(GalleryCatalog.Extras.Where(c => c.Code is "Content" or "AdvancedCharts").SelectMany(c => c.Entries))
            .Concat(GalleryCatalog.Special.SelectMany(c => c.Entries));
        foreach (var entry in entries)
        {
            GalleryDocs.Has(entry.ClassName).ShouldBeTrue($"{entry.ClassName} has no docs page");
            foreach (var language in new[] { "en-US", "zh-CN", "zh-TW" })
            {
                var doc = GalleryDocs.Get(entry.ClassName, language).ShouldNotBeNull();
                doc.Language.ShouldBe(language, $"{entry.ClassName} is not translated into {language}");
                doc.IsOutdated.ShouldBeFalse($"{entry.ClassName} ({language}) was translated from an older en-US.md");
                doc.Summary.ShouldNotBeNullOrWhiteSpace();
            }
        }
    }

    [AvaloniaFact]
    public void Every_Page_Has_The_Standard_Sections_And_Its_Own_Api_Reference()
    {
        foreach (var className in GalleryDocs.ClassNames)
        {
            var english = GalleryDocs.Get(className, GalleryDocs.SourceLanguage).ShouldNotBeNull().Markdown;
            english.ShouldContain($"### {className}", customMessage: $"{className}: the page has no <!-- api: {className} --> line");
            Headings(english).ShouldBe(
                ["## When to use", "## When not to use", "## Usage", "## API reference", "## Accessibility", "## Motion", "## Related"],
                $"{className}: the sections of a page, in order");
        }
    }

    [AvaloniaFact]
    public void Translations_Have_The_Same_Sections_And_Code_Blocks_As_The_English_Page()
    {
        foreach (var className in GalleryDocs.ClassNames)
        {
            var english = GalleryDocs.Get(className, GalleryDocs.SourceLanguage)!.Markdown;
            foreach (var language in GalleryDocs.Languages(className).Where(l => l != GalleryDocs.SourceLanguage))
            {
                var translated = GalleryDocs.Get(className, language)!;
                translated.Language.ShouldBe(language);
                var where = $"{className} ({language})";
                Headings(translated.Markdown).Count.ShouldBe(Headings(english).Count, where + ": sections");
                DocSnippets.Blocks(translated.Markdown).Select(b => b.Language)
                           .ShouldBe(DocSnippets.Blocks(english).Select(b => b.Language), where + ": code blocks");
            }
        }
    }

    [AvaloniaFact]
    public void Xaml_Examples_In_Every_Page_Load()
    {
        var failures = new List<string>();
        var checkedBlocks = 0;
        foreach (var className in GalleryDocs.ClassNames)
        {
            foreach (var language in GalleryDocs.Languages(className))
            {
                foreach (var block in DocSnippets.Blocks(GalleryDocs.Get(className, language)!.Markdown).Where(b => b.IsXaml))
                {
                    checkedBlocks++;
                    if (DocSnippets.ValidateXaml(block.Code) is { } reason)
                    {
                        failures.Add($"{className} ({language}) line {block.Line}: {reason}");
                    }
                }
            }
        }
        checkedBlocks.ShouldBeGreaterThan(0);
        failures.ShouldBeEmpty(string.Join("\n", failures));
    }

    [AvaloniaFact]
    public void The_Snippet_Check_Rejects_Unknown_Types_And_Properties()
    {
        DocSnippets.ValidateXaml("<arc:Input Label=\"Email\" />").ShouldBeNull();
        DocSnippets.ValidateXaml("<arc:NoSuchControl />").ShouldNotBeNull();
        DocSnippets.ValidateXaml("<arc:Input NoSuchProperty=\"1\" />").ShouldNotBeNull();
        DocSnippets.ValidateXaml("<arc:Input Size=\"Gigantic\" />").ShouldNotBeNull();
        DocSnippets.ValidateXaml("<UserControl xmlns=\"https://github.com/avaloniaui\" xmlns:arc=\"https://github.com/AIDotNet/TKCode.Arc\"><arc:Input /></UserControl>").ShouldBeNull();
    }

    private static List<string> Headings(string markdown)
        => OutsideFences(markdown).Where(l => l.StartsWith("## ", StringComparison.Ordinal)).ToList();

    private static IEnumerable<string> OutsideFences(string markdown)
    {
        var inside = false;
        foreach (var line in markdown.Split('\n'))
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inside = !inside;
            }
            else if (!inside)
            {
                yield return line;
            }
        }
    }
}
