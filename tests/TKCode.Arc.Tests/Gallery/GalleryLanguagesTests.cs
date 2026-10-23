using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.Motion;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>The language a session starts in, the top bar's language menu, and the choice kept for the next session.</summary>
public class GalleryLanguagesTests
{
    private sealed class MemoryStore(string? stored = null) : IGalleryLanguageStore
    {
        public string? Stored { get; private set; } = stored;

        public string? Read() => Stored;

        public void Write(string languageTag) => Stored = languageTag;
    }

    private sealed class BrokenStore : IGalleryLanguageStore
    {
        public string Read() => throw new IOException("blocked");

        public void Write(string languageTag) => throw new IOException("blocked");
    }

    [Theory]
    [InlineData("en-US", "en-US")]
    [InlineData("en", "en-US")]
    [InlineData("en-GB", "en-US")]
    [InlineData("EN_au", "en-US")]
    [InlineData("zh", "zh-CN")]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("zh-SG", "zh-CN")]
    [InlineData("zh-Hans", "zh-CN")]
    [InlineData("zh-Hans-CN", "zh-CN")]
    [InlineData("zh-Hans-HK", "zh-CN")]
    [InlineData("zh_CN", "zh-CN")]
    [InlineData("zh-TW", "zh-TW")]
    [InlineData("zh-HK", "zh-TW")]
    [InlineData("zh-MO", "zh-TW")]
    [InlineData("zh-Hant", "zh-TW")]
    [InlineData("zh-Hant-TW", "zh-TW")]
    [InlineData("zh_TW", "zh-TW")]
    [InlineData("ZH-tw", "zh-TW")]
    [InlineData("ja-JP", null)]
    [InlineData("fr", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Match_Maps_A_System_Language_To_A_Gallery_Language(string? tag, string? expected)
        => GalleryLanguages.Match(tag).ShouldBe(expected);

    [Fact]
    public void Initial_Prefers_The_Kept_Choice_Then_The_First_Known_Preferred_Language()
    {
        var previous = GalleryLanguages.Store;
        try
        {
            GalleryLanguages.Store = null;
            GalleryLanguages.Initial(["zh-Hans-CN"]).Value.ShouldBe("zh-CN");
            GalleryLanguages.Initial(["zh-HK", "en-US"]).Value.ShouldBe("zh-TW");
            // A language the gallery does not have is passed over for the next one, and English is the last resort.
            GalleryLanguages.Initial(["ja-JP", "zh-TW"]).Value.ShouldBe("zh-TW");
            GalleryLanguages.Initial(["ja-JP", "fr-FR"]).Value.ShouldBe("en-US");
            GalleryLanguages.Initial([]).Value.ShouldBe("en-US");

            // A visitor's own choice wins over the system.
            GalleryLanguages.Store = new MemoryStore("en-US");
            GalleryLanguages.Initial(["zh-CN"]).Value.ShouldBe("en-US");
            // A kept value that is no gallery language is ignored.
            GalleryLanguages.Store = new MemoryStore("de-DE");
            GalleryLanguages.Initial(["zh-TW"]).Value.ShouldBe("zh-TW");
            // Blocked storage only means there is no kept choice.
            GalleryLanguages.Store = new BrokenStore();
            GalleryLanguages.Initial(["zh-CN"]).Value.ShouldBe("zh-CN");
        }
        finally
        {
            GalleryLanguages.Store = previous;
        }
    }

    [AvaloniaFact]
    public void Top_Bar_Menu_Switches_Keeps_And_Follows_The_Language()
    {
        var previous = GalleryLanguages.Store;
        var store = new MemoryStore();
        GalleryLanguages.Store = store;
        var window = new GalleryWindow { Width = 1280, Height = 860 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var bar = window.TopBar;
            var button = bar.LanguageButton;
            button.IsEffectivelyVisible.ShouldBeTrue();
            Name(button).ShouldBe("English");
            var rows = bar.LanguageMenu.Items.OfType<MenuEntry>().ToList();
            rows.Select(r => r.Header as string).ShouldBe(["English", "简体中文", "繁體中文"]);
            rows.Select(r => r.IsChecked).ShouldBe([true, false, false]);

            // The button opens the menu; a row switches the whole application and is kept for the next session.
            button.Focus();
            window.KeyPressQwerty(Avalonia.Input.PhysicalKey.ArrowDown, Avalonia.Input.RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            bar.LanguageMenu.IsOpen.ShouldBeTrue();
            rows[1].PerformClick();
            Dispatcher.UIThread.RunJobs();
            bar.LanguageMenu.IsOpen.ShouldBeFalse();
            GalleryText.CurrentLanguage.ShouldBe("zh-CN");
            store.Stored.ShouldBe("zh-CN");
            Name(button).ShouldBe("简体中文");
            rows.Select(r => r.IsChecked).ShouldBe([false, true, false]);
            Avalonia.Automation.AutomationProperties.GetName(button).ShouldBe("语言");

            // A change from elsewhere moves the mark but is not a visitor's choice, so it is not kept.
            GalleryText.SetLanguage("zh-TW");
            Dispatcher.UIThread.RunJobs();
            Name(button).ShouldBe("繁體中文");
            rows.Select(r => r.IsChecked).ShouldBe([false, false, true]);
            store.Stored.ShouldBe("zh-CN");
        }
        finally
        {
            GalleryLanguages.Store = previous;
            GalleryText.SetLanguage("en-US");
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Choice_That_Cannot_Be_Kept_Still_Switches()
    {
        var previous = GalleryLanguages.Store;
        GalleryLanguages.Store = new BrokenStore();
        try
        {
            GalleryLanguages.Choose("zh-TW");
            Dispatcher.UIThread.RunJobs();
            GalleryText.CurrentLanguage.ShouldBe("zh-TW");
        }
        finally
        {
            GalleryLanguages.Store = previous;
            GalleryText.SetLanguage("en-US");
        }
    }

    /// <summary>The language name the button shows.</summary>
    private static string Name(Button button)
        => button.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).Single(t => !string.IsNullOrEmpty(t))!;
}
