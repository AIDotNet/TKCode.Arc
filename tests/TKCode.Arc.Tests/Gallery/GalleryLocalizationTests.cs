using AtomUI.Localization;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Gallery.Pages;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>The shell's own text: xlf coverage, formats, live bindings and the sidebar group titles.</summary>
public class GalleryLocalizationTests
{
    private static readonly string[] Languages = ["en-US", "zh-CN", "zh-TW"];

    private static void With(string language, Action action)
    {
        GalleryText.SetLanguage(language);
        Dispatcher.UIThread.RunJobs();
        try
        {
            action();
        }
        finally
        {
            GalleryText.SetLanguage("en-US");
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void Every_String_Is_Translated_And_Keeps_Its_Placeholders()
    {
        var english = Enum.GetValues<GalleryShellLangResourceKind>().ToDictionary(k => k, GalleryText.Get);
        foreach (var language in Languages.Skip(1))
        {
            With(language, () =>
            {
                foreach (var (kind, source) in english)
                {
                    var text = GalleryText.Get(kind);
                    text.ShouldNotBeNullOrWhiteSpace(kind.ToString());
                    // Proper nouns and loanwords may stay the same; everything else must have been translated.
                    if (text == source)
                    {
                        source.ShouldBeOneOf(["Markdown", "Copy Markdown", "AI"], $"{language}: {kind} was left in English");
                    }
                    foreach (var placeholder in new[] { "{0}", "{1}" })
                    {
                        text.Contains(placeholder).ShouldBe(source.Contains(placeholder), $"{language}: {kind}");
                    }
                }
            });
        }
    }

    [AvaloniaFact]
    public void Formats_Fill_Their_Placeholders_In_Every_Language()
    {
        GalleryText.Format(GalleryShellLangResourceKind.ImplementedCount, 8, 110).ShouldBe("8 of 110");
        With("zh-CN", () =>
        {
            GalleryText.Format(GalleryShellLangResourceKind.ImplementedCount, 8, 110).ShouldBe("8 / 110");
            GalleryText.Format(GalleryShellLangResourceKind.NoComponentsMatch, "x").ShouldBe("没有与“x”匹配的组件。");
        });
        With("zh-TW", () => GalleryText.Format(GalleryShellLangResourceKind.PlannedPhase, 3).ShouldBe("規劃中 · P3"));
    }

    [AvaloniaFact]
    public void Bound_Text_Follows_The_Language()
    {
        var window = TestHelpers.Show(new StackPanel());
        var plain = new TextBlock();
        var formatted = new TextBlock();
        ((StackPanel)((Avalonia.Controls.Primitives.VisualLayerManager)window.Content!).Child!).Children.AddRange([plain, formatted]);
        GalleryText.Bind(plain, TextBlock.TextProperty, GalleryShellLangResourceKind.OnThisPage);
        GalleryText.BindFormat(formatted, TextBlock.TextProperty, GalleryShellLangResourceKind.ImplementedCount, 8, 110);
        Dispatcher.UIThread.RunJobs();
        try
        {
            plain.Text.ShouldBe("On this page");
            formatted.Text.ShouldBe("8 of 110");

            GalleryText.SetLanguage("zh-CN");
            Dispatcher.UIThread.RunJobs();
            plain.Text.ShouldBe("本页内容");
            formatted.Text.ShouldBe("8 / 110");

            GalleryText.SetLanguage("zh-TW");
            Dispatcher.UIThread.RunJobs();
            plain.Text.ShouldBe("本頁內容");
        }
        finally
        {
            GalleryText.SetLanguage("en-US");
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Every_Catalog_Category_Has_A_Title_That_Matches_The_Catalog()
    {
        foreach (var category in GalleryCatalog.AllCategories)
        {
            GalleryText.TryGetCategory(category.Code, out var kind).ShouldBeTrue(category.Code);
            GalleryText.Get(kind).ShouldBe(category.Title, category.Code);
        }
        GalleryText.TryGetCategory("NoSuchCategory", out _).ShouldBeFalse();
        // zh-CN uses the catalog's own Chinese titles (the design master data) except the extras group.
        With("zh-CN", () =>
        {
            foreach (var category in GalleryCatalog.Categories)
            {
                GalleryText.TryGetCategory(category.Code, out var kind);
                GalleryText.Get(kind).ShouldBe(category.ChineseTitle, category.Code);
            }
        });
    }

    private static List<string> Texts(Visual root) =>
        root.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty)
            .Concat(root.GetVisualDescendants().OfType<Button>().Select(b => b.Content as string ?? string.Empty))
            .Where(t => t.Length > 0).ToList();

    /// <summary>StreamMark parses the docs page on a worker: pumps the page until every text is shown.</summary>
    private static async Task Shown(Visual root, params string[] texts)
    {
        for (var i = 0; i < 300; i++)
        {
            root.GetVisualDescendants().OfType<GalleryPageView>().FirstOrDefault()?.Docs?.View?.ApplyLatest();
            Dispatcher.UIThread.RunJobs();
            var shown = Texts(root);
            if (texts.All(shown.Contains))
            {
                return;
            }
            await Task.Delay(10);
        }
        throw new TimeoutException($"Not shown: {string.Join(", ", texts.Where(t => !Texts(root).Contains(t)))}");
    }

    private static GalleryWindow Open()
    {
        var window = new GalleryWindow { Width = 1280, Height = 860 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public async Task Shell_Text_Follows_The_Language()
    {
        var window = Open();
        try
        {
            window.Select(GalleryCatalog.AllEntries.First(e => e.ClassName == "Input"));
            Dispatcher.UIThread.RunJobs();
            var search = window.Sidebar.GetVisualDescendants().OfType<TextBox>().First();

            await Shown(window, "Preview", "Code", "On this page", "A single line field with clear labels and useful states.");
            var english = Texts(window);
            english.ShouldContain("Components");
            english.ShouldContain("Search");
            english.ShouldContain("Appearance");
            english.ShouldContain("Text fields");
            english.ShouldContain("Preview");
            english.ShouldContain("Code");
            english.ShouldContain("On this page");
            english.ShouldContain("A single line field with clear labels and useful states.");
            search.PlaceholderText.ShouldBe("Search components");

            GalleryText.SetLanguage("zh-CN");
            Dispatcher.UIThread.RunJobs();
            await Shown(window, "预览", "代码", "本页内容", "标签清晰、状态实用的单行输入框。");
            var chinese = Texts(window);
            chinese.ShouldContain("组件");
            chinese.ShouldContain("搜索");
            chinese.ShouldContain("外观");
            chinese.ShouldContain("文本输入");
            chinese.ShouldContain("全部");
            chinese.ShouldContain("规划中");
            chinese.ShouldContain("预览");
            chinese.ShouldContain("代码");
            chinese.ShouldContain("本页内容");
            chinese.ShouldContain("在 uiarc.dev 查看");
            // The sidebar counts Arc components only: ported blocks are IsArc too, but are not components.
            chinese.ShouldContain($"{GalleryCatalog.Categories.SelectMany(c => c.Entries).Count(e => GalleryPages.IsImplemented(e.ClassName))} / {GalleryCatalog.Count}");
            // The page summary comes from the translated docs page; component names stay in English.
            chinese.ShouldContain("标签清晰、状态实用的单行输入框。");
            chinese.ShouldNotContain("A single line field with clear labels and useful states.");
            chinese.ShouldContain("Input");
            search.PlaceholderText.ShouldBe("搜索组件");

            GalleryText.SetLanguage("zh-TW");
            Dispatcher.UIThread.RunJobs();
            await Shown(window, "文字輸入", "標籤清楚、狀態實用的單行輸入框。");
            Texts(window).ShouldContain("文字輸入");
            Texts(window).ShouldContain("標籤清楚、狀態實用的單行輸入框。");
            search.PlaceholderText.ShouldBe("搜尋元件");

            GalleryText.SetLanguage("en-US");
            Dispatcher.UIThread.RunJobs();
            await Shown(window, "Text fields", "A single line field with clear labels and useful states.");
            Texts(window).ShouldContain("Text fields");
            Texts(window).ShouldContain("A single line field with clear labels and useful states.");
            search.PlaceholderText.ShouldBe("Search components");
        }
        finally
        {
            GalleryText.SetLanguage("en-US");
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Empty_Search_Message_Follows_The_Language()
    {
        var window = Open();
        try
        {
            window.Sidebar.SearchText = "zzz";
            Dispatcher.UIThread.RunJobs();
            Texts(window.Sidebar).ShouldContain("No components match “zzz”.");

            GalleryText.SetLanguage("zh-CN");
            Dispatcher.UIThread.RunJobs();
            Texts(window.Sidebar).ShouldContain("没有与“zzz”匹配的组件。");

            window.Sidebar.SearchText = string.Empty;
            window.Sidebar.FilterIndex = 2;
            Dispatcher.UIThread.RunJobs();
            // Every Arc component has a page, so "Planned" lists only what is left of the extras.
            window.Sidebar.VisibleEntries.ShouldAllBe(e => !GalleryPages.IsImplemented(e.ClassName));
        }
        finally
        {
            GalleryText.SetLanguage("en-US");
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Planned_Page_Text_Follows_The_Language()
    {
        var entry = new GalleryEntry("NotYetBuilt", "Not yet built", "not-yet-built", 3, "A component that does not exist.");
        var page = GalleryPages.Create(entry);
        var window = TestHelpers.Show(page);
        try
        {
            var english = Texts(page);
            english.ShouldContain("Not yet built is not built yet.");
            english.ShouldContain("NotYetBuilt is scheduled for phase P3.");
            english.ShouldContain("Planned · P3");

            GalleryText.SetLanguage("zh-CN");
            Dispatcher.UIThread.RunJobs();
            var chinese = Texts(page);
            chinese.ShouldContain("Not yet built 尚未实现。");
            chinese.ShouldContain("NotYetBuilt 计划在 P3 阶段实现。");
            chinese.ShouldContain("规划中 · P3");
        }
        finally
        {
            GalleryText.SetLanguage("en-US");
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Appearance_Panel_Switches_And_Follows_The_Language()
    {
        var owner = new Window();
        var panel = new GalleryAppearancePanel(new GalleryTheme(), owner);
        var window = TestHelpers.Show(panel);
        try
        {
            var segmented = panel.GetVisualDescendants().OfType<GallerySegmented>().ToList();
            segmented.Count.ShouldBe(3);
            var language = segmented[^1];
            language.SelectedIndex.ShouldBe(0);
            Texts(panel).ShouldContain("Language");
            Texts(panel).ShouldContain("English");
            Texts(panel).ShouldContain("简体中文");
            Texts(panel).ShouldContain("繁體中文");

            // Choosing a language in the panel switches the whole application.
            var chinese = language.GetVisualDescendants().OfType<Button>().First(b => b.Content as string == "简体中文");
            chinese.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            GalleryText.CurrentLanguage.ShouldBe("zh-CN");
            language.SelectedIndex.ShouldBe(1);
            Texts(panel).ShouldContain("语言");
            Texts(panel).ShouldContain("浅色");
            // The accent swatches are named for assistive technology and in their tooltips.
            panel.GetVisualDescendants().OfType<Button>()
                 .Select(Avalonia.Automation.AutomationProperties.GetName).ShouldContain("珊瑚");

            // And a change from outside moves the selection.
            GalleryText.SetLanguage("zh-TW");
            Dispatcher.UIThread.RunJobs();
            language.SelectedIndex.ShouldBe(2);
            Texts(panel).ShouldContain("語言");
        }
        finally
        {
            GalleryText.SetLanguage("en-US");
            window.Close();
        }
    }
}
