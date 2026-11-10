using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Special.Inputs;

/// <summary>
/// The emoji picker: the Unicode table (groups, names, tones, versions), search ranking, the sections with Recent, the
/// grid's keyboard model and drawing only the rows in view, picking (tone, recent list, event, command), categories and
/// the skin tone row.
/// </summary>
public class EmojiPickerTests
{
    private static (EmojiPicker Picker, Window Window) Show(Action<EmojiPicker>? configure = null)
    {
        var picker = new EmojiPicker();
        configure?.Invoke(picker);
        var window = TestHelpers.Show(picker);
        window.Width = 500;
        window.Height = 640;
        Dispatcher.UIThread.RunJobs();
        return (picker, window);
    }

    // ---- The table ----

    [Fact]
    public void Table_Holds_The_Unicode_Groups_In_Order_With_Names_And_Tones()
    {
        var all = EmojiPickerData.All;
        all.Count.ShouldBeGreaterThan(1800);
        all[0].Emoji.ShouldBe("😀");
        all[0].Name.ShouldBe("grinning face");
        all[0].Category.ShouldBe(EmojiCategory.Smileys);
        all.Select(e => e.Category).Distinct().ShouldBe(
        [
            EmojiCategory.Smileys, EmojiCategory.People, EmojiCategory.Nature, EmojiCategory.Food, EmojiCategory.Travel,
            EmojiCategory.Activities, EmojiCategory.Objects, EmojiCategory.Symbols, EmojiCategory.Flags
        ]);
        all.ShouldAllBe(e => e.Name.Length > 0);
        // Skin tone forms are folded into their emoji, not listed on their own.
        all.ShouldNotContain(e => e.Emoji.Contains("\U0001F3FD"));

        var wave = EmojiPickerData.Find("👋🏽").ShouldNotBeNull();
        wave.Emoji.ShouldBe("👋");
        wave.Display(EmojiSkinTone.Medium, 99).ShouldBe("👋🏽");
        wave.Display(EmojiSkinTone.Default, 99).ShouldBe("👋");
        EmojiPickerData.Find("❤").ShouldNotBeNull().Name.ShouldBe("red heart");
        EmojiPickerData.Find("😀")!.Display(EmojiSkinTone.Dark, 99).ShouldBe("😀");
        // Tones newer than the version limit are not applied.
        var handshake = EmojiPickerData.Find("🤝").ShouldNotBeNull();
        handshake.ToneVersion.ShouldBe(14.0);
        handshake.Display(EmojiSkinTone.Light, 13).ShouldBe("🤝");
    }

    [Fact]
    public void Search_Matches_Word_Starts_Best_First()
    {
        var all = EmojiPickerData.All;
        var hearts = EmojiPickerData.Search("red hea", all, 99);
        hearts[0].Name.ShouldBe("red heart");
        EmojiPickerData.Search("hea", all, 99).ShouldContain(e => e.Name == "smiling face with heart-eyes");
        // A keyword match (CLDR "lol") comes after name matches.
        EmojiPickerData.Search("lol", all, 99).ShouldNotBeEmpty();
        EmojiPickerData.Search("qqzzxx", all, 99).ShouldBeEmpty();
        EmojiPickerData.Search("   ", all, 99).ShouldBeEmpty();
        // A word that starts the name beats one later in it.
        var cats = EmojiPickerData.Search("cat", all, 99);
        cats[0].Name.ShouldStartWith("cat");
        // The version limit leaves out newer emoji.
        EmojiPickerData.Search("melting", all, 99).ShouldContain(e => e.Name == "melting face");
        EmojiPickerData.Search("melting", all, 13).ShouldNotContain(e => e.Name == "melting face");
    }

    // ---- The picker ----

    [AvaloniaFact]
    public void Sections_Follow_The_Groups_With_Recent_First()
    {
        var (picker, window) = Show();
        try
        {
            var grid = picker.Grid!;
            grid.Sections.Select(s => s.Category).First().ShouldBe(EmojiCategory.Smileys);
            grid.Sections.Count.ShouldBe(9);
            picker.Tabs!.Categories.Count.ShouldBe(9);
            grid.Columns.ShouldBe(9);

            picker.Recent.Add("🎉");
            picker.Recent.Add("👋🏽");
            Dispatcher.UIThread.RunJobs();
            grid.Sections[0].Category.ShouldBe(EmojiCategory.Recent);
            grid.Sections[0].Title.ShouldBe("Recently used");
            grid.Sections[0].Items.Select(e => e.Emoji).ShouldBe(["🎉", "👋"]);
            picker.Tabs.Categories[0].ShouldBe(EmojiCategory.Recent);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Grid_Draws_Only_The_Rows_In_View()
    {
        var (picker, window) = Show();
        try
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var grid = picker.Grid!;
            grid.Bounds.Height.ShouldBeGreaterThan(5000);
            grid.LastDrawnRows.ShouldBeGreaterThan(0);
            grid.LastDrawnRows.ShouldBeLessThan(20);
            grid.GlyphCacheCount.ShouldBeLessThan(200);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Moves_Through_The_Grid_And_Enter_Picks()
    {
        string? commanded = null;
        var (picker, window) = Show(p => p.Command = new Relay(o => commanded = o as string));
        try
        {
            EmojiSelectedEventArgs? picked = null;
            picker.EmojiSelected += (_, e) => picked = e;
            var grid = picker.Grid!;
            grid.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            grid.Active.ShouldBe((0, 0));
            AutomationProperties.GetName(grid).ShouldBe("grinning face");

            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            grid.Active.ShouldBe((0, 1 + grid.Columns));
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            grid.Active.ShouldBe((0, 2 * grid.Columns - 1));
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.Control);
            grid.Active.ShouldBe((0, 0));
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            grid.Active.ShouldBe((0, 0));
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.Control);
            grid.Active.Section.ShouldBe(grid.Sections.Count - 1);
            // Down across a section title keeps the column where it can.
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.Control);
            for (var i = 0; i < 40; i++)
            {
                window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            }
            grid.Active.Section.ShouldBeGreaterThan(0);

            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.Control);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            picked.ShouldNotBeNull().Emoji.ShouldBe("😀");
            picked.Name.ShouldBe("grinning face");
            picked.Category.ShouldBe(EmojiCategory.Smileys);
            commanded.ShouldBe("😀");
            picker.Recent.ShouldBe(["😀"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Search_Filters_Reports_And_Enter_Picks_The_Best_Match_In_The_Tone()
    {
        var (picker, window) = Show(p => p.SkinTone = EmojiSkinTone.Medium);
        try
        {
            EmojiSelectedEventArgs? picked = null;
            picker.EmojiSelected += (_, e) => picked = e;
            picker.Search!.Focus();
            picker.SearchText = "waving hand";
            Dispatcher.UIThread.RunJobs();
            picker.Classes.Contains(":searching").ShouldBeTrue();
            picker.Grid!.Sections.Count.ShouldBe(1);
            picker.Grid.Sections[0].Title.ShouldBe("Search results");
            AutomationProperties.GetHelpText(picker.Search).ShouldEndWith("found");

            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            picked.ShouldNotBeNull().Emoji.ShouldBe("👋🏽");
            picked.BaseEmoji.ShouldBe("👋");
            picker.Recent.ShouldBe(["👋"]);

            picker.SearchText = "qqzzxx";
            Dispatcher.UIThread.RunJobs();
            picker.Classes.Contains(":no-results").ShouldBeTrue();
            AutomationProperties.GetHelpText(picker.Search).ShouldBe("No emoji found");
            TestHelpers.Descendant<TextBlock>(picker, EmojiPicker.EmptyPart).IsVisible.ShouldBeTrue();

            // Down moves from the search into the grid.
            picker.SearchText = "cat";
            Dispatcher.UIThread.RunJobs();
            picker.Search.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            picker.Grid.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Category_Scrolls_The_Grid_And_Activates_Its_First_Emoji()
    {
        var (picker, window) = Show();
        try
        {
            picker.ScrollToCategory(EmojiCategory.Food);
            Dispatcher.UIThread.RunJobs();
            var grid = picker.Grid!;
            var index = grid.Sections.Select(s => s.Category).ToList().IndexOf(EmojiCategory.Food);
            grid.Active.ShouldBe((index, 0));
            var scroll = TestHelpers.Descendant<ScrollViewer>(picker, EmojiPicker.ScrollPart);
            scroll.Offset.Y.ShouldBe(grid.SectionTop(index), 1);
            picker.Tabs!.ActiveCategory.ShouldBe(EmojiCategory.Food);

            // The strip's keys move between categories and jump.
            picker.Tabs.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            picker.Tabs.ActiveCategory.ShouldBe(EmojiCategory.Travel);
            AutomationProperties.GetName(picker.Tabs).ShouldBe("Travel and places");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Skin_Tone_Row_Opens_Moves_And_Chooses()
    {
        var (picker, window) = Show();
        try
        {
            var tones = picker.Tones!;
            AutomationProperties.GetName(tones).ShouldBe("Skin tone: Default");
            tones.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            tones.IsExpanded.ShouldBeTrue();
            picker.Classes.Contains(":tones-open").ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            tones.IsExpanded.ShouldBeFalse();
            picker.SkinTone.ShouldBe(EmojiSkinTone.MediumLight);
            picker.Grid!.Display(EmojiPickerData.Find("👍")!).ShouldBe("👍🏼");

            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            tones.IsExpanded.ShouldBeFalse();
            picker.SkinTone.ShouldBe(EmojiSkinTone.MediumLight);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Recent_Keeps_Its_Limit_And_Moves_A_Repeat_To_The_Front()
    {
        var (picker, window) = Show(p => p.MaxRecent = 3);
        try
        {
            var grid = picker.Grid!;
            foreach (var query in new[] { "grinning face", "red heart", "thumbs up", "fire", "red heart" })
            {
                picker.SearchText = query;
                Dispatcher.UIThread.RunJobs();
                grid.PickActive().ShouldBeTrue();
            }
            picker.Recent.Count.ShouldBe(3);
            picker.Recent[0].ShouldBe(EmojiPickerData.Search("red heart", EmojiPickerData.All, 99)[0].Emoji);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class Relay : System.Windows.Input.ICommand
    {
        private readonly Action<object?> _run;

        public Relay(Action<object?> run) => _run = run;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _run(parameter);
    }
}
