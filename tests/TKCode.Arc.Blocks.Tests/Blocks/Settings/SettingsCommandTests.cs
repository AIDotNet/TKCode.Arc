using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Blocks.Settings;

/// <summary>
/// The settings command palette: search and ranking, the keyboard model (arrows, Enter, Escape), changes in place with
/// their events and notice, two-way values, and the no-results message.
/// </summary>
public class SettingsCommandTests
{
    private static SettingsCommand Palette(out SettingsCommandChoice theme, out SettingsCommandToggle compact,
                                           out SettingsCommandToggle receipts, out SettingsCommandAction email)
    {
        theme = new SettingsCommandChoice
        {
            Group = "Appearance",
            Header = "Theme",
            Description = "Match your system, or pick light or dark.",
            Keywords = "dark light scheme",
            Options = { "Light", "Dark", "System" }
        };
        compact = new SettingsCommandToggle { Group = "Appearance", Header = "Compact mode", Description = "Tighter rows.", Keywords = "density" };
        receipts = new SettingsCommandToggle { Group = "Privacy", Header = "Read receipts", IsEnabled = false };
        email = new SettingsCommandAction { Group = "Account", Header = "Change email", Description = "noor@larkspur.co", IsNavigation = true };
        var palette = new SettingsCommand { Width = 560 };
        palette.Items.Add(theme);
        palette.Items.Add(compact);
        palette.Items.Add(receipts);
        palette.Items.Add(new SettingsCommandToggle { Group = "Notifications", Header = "Mentions", Description = "When someone mentions you." });
        palette.Items.Add(new SettingsCommandChoice { Group = "Notifications", Header = "Email digests", Options = { "Off", "Daily", "Weekly" } });
        palette.Items.Add(email);
        return palette;
    }

    private static Window Show(SettingsCommand palette)
    {
        var window = TestHelpers.Show(palette);
        window.Width = 700;
        window.Height = 700;
        Settle();
        palette.FocusSearch();
        Settle();
        return window;
    }

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Settle();
    }

    private static string?[] Headers(SettingsCommand palette) => palette.VisibleRows.Select(r => r.Item.Header).ToArray();

    [AvaloniaFact]
    public void An_Empty_Query_Lists_Everything_In_Declared_Order_With_The_First_Row_Active()
    {
        var palette = Palette(out var theme, out _, out _, out _);
        var window = Show(palette);
        try
        {
            Headers(palette).ShouldBe(["Theme", "Compact mode", "Read receipts", "Mentions", "Email digests", "Change email"]);
            palette.ActiveItem.ShouldBe(theme);
            palette.ResultOrder.OfType<SettingsCommandGroupHeader>().Select(t => t.Text)
                   .ShouldBe(["Appearance", "Privacy", "Notifications", "Account"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Every_Query_Word_Must_Match_And_Header_Matches_Rank_First()
    {
        var palette = Palette(out var theme, out var compact, out _, out _);
        var window = Show(palette);
        try
        {
            // "dark" is only a keyword of Theme; "m" words: Compact mode and Mentions start a word, Email digests contains it.
            palette.Query = "dark";
            Headers(palette).ShouldBe(["Theme"]);

            palette.Query = "dark scheme";
            Headers(palette).ShouldBe(["Theme"]);
            palette.Query = "dark density";
            Headers(palette).ShouldBeEmpty();

            palette.Query = "mo";
            Headers(palette).ShouldBe(["Compact mode"]);

            // Header prefix > word prefix > header contains > other fields; ties keep the declared order.
            palette.Query = "e";
            Headers(palette).ShouldBe(["Email digests", "Mentions", "Change email", "Theme", "Compact mode", "Read receipts"]);
            palette.ActiveItem!.Header.ShouldBe("Email digests");

            palette.Query = "THEME";
            Headers(palette).ShouldBe(["Theme"]);
            palette.ActiveItem.ShouldBe(theme);
            compact.IsChecked.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Groups_Follow_Their_Best_Match()
    {
        var palette = new SettingsCommand { Width = 560 };
        palette.Items.Add(new SettingsCommandToggle { Group = "General", Header = "Alpha", Description = "Works with beta features." });
        palette.Items.Add(new SettingsCommandToggle { Group = "Labs", Header = "Beta features" });
        var window = Show(palette);
        try
        {
            palette.Query = "beta";
            Headers(palette).ShouldBe(["Beta features", "Alpha"]);
            palette.ResultOrder.OfType<SettingsCommandGroupHeader>().Select(t => t.Text).ShouldBe(["Labs", "General"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Matching_Header_Text_Is_Split_Into_Runs()
    {
        var palette = Palette(out _, out var compact, out _, out _);
        var window = Show(palette);
        try
        {
            palette.Query = "mo";
            var header = palette.RowOf(compact)!.HeaderText!;
            var runs = header.Inlines!.OfType<Avalonia.Controls.Documents.Run>().ToArray();
            runs.Select(r => r.Text).ShouldBe(["Compact ", "mo", "de"]);
            var dim = palette.RowOf(compact)!.DimForeground;
            dim.ShouldNotBeNull();
            runs[0].Foreground.ShouldBe(dim);
            runs[1].Foreground.ShouldNotBe(dim);
            runs[2].Foreground.ShouldBe(dim);

            palette.Clear();
            header.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text).ShouldBe(["Compact mode"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Down_And_Enter_Flip_A_Toggle_And_Raise_SettingChanged()
    {
        var palette = Palette(out _, out var compact, out _, out _);
        var window = Show(palette);
        var changes = new List<SettingsCommandChangedEventArgs>();
        palette.SettingChanged += (_, e) => changes.Add(e);
        try
        {
            Press(window, PhysicalKey.ArrowDown);
            palette.ActiveItem.ShouldBe(compact);
            palette.Classes.Contains(SettingsCommand.ToggleActivePseudoClass).ShouldBeTrue();

            Press(window, PhysicalKey.Enter);
            compact.IsChecked.ShouldBeTrue();
            palette.RowOf(compact)!.Switch!.IsChecked.ShouldBe(true);
            changes.Count.ShouldBe(1);
            changes[0].Item.ShouldBe(compact);
            changes[0].Value.ShouldBe(true);
            palette.NoticeText.ShouldBe("Compact mode on");

            Press(window, PhysicalKey.Enter);
            compact.IsChecked.ShouldBeFalse();
            changes[1].Value.ShouldBe(false);
            palette.NoticeText.ShouldBe("Compact mode off");

            // The keyboard never left the search field.
            palette.SearchBox!.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrows_Skip_Disabled_Items_And_Do_Not_Wrap()
    {
        var palette = Palette(out var theme, out var compact, out _, out var email);
        var window = Show(palette);
        try
        {
            Press(window, PhysicalKey.ArrowUp);
            palette.ActiveItem.ShouldBe(theme);

            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.ArrowDown);
            // Read receipts is disabled: Down goes from Compact mode to Mentions.
            palette.ActiveItem!.Header.ShouldBe("Mentions");

            Press(window, PhysicalKey.PageDown);
            palette.ActiveItem.ShouldBe(email);
            Press(window, PhysicalKey.ArrowDown);
            palette.ActiveItem.ShouldBe(email);

            Press(window, PhysicalKey.PageUp);
            palette.ActiveItem.ShouldBe(theme);
            Press(window, PhysicalKey.ArrowDown);
            palette.ActiveItem.ShouldBe(compact);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Right_And_Left_Step_A_Choice_And_Enter_Cycles_It()
    {
        var palette = Palette(out var theme, out _, out _, out _);
        var window = Show(palette);
        var values = new List<object?>();
        palette.SettingChanged += (_, e) => values.Add(e.Value);
        try
        {
            theme.SelectedOption.ShouldBe("Light");
            palette.Classes.Contains(SettingsCommand.ChoiceActivePseudoClass).ShouldBeTrue();

            Press(window, PhysicalKey.ArrowRight);
            theme.SelectedOption.ShouldBe("Dark");
            Press(window, PhysicalKey.ArrowRight);
            Press(window, PhysicalKey.ArrowRight);
            theme.SelectedOption.ShouldBe("System");
            values.ShouldBe(["Dark", "System"]);
            palette.NoticeText.ShouldBe("Theme: System");
            palette.RowOf(theme)!.Pill!.Value.ShouldBe("System");

            Press(window, PhysicalKey.ArrowLeft);
            theme.SelectedOption.ShouldBe("Dark");
            Press(window, PhysicalKey.Enter);
            Press(window, PhysicalKey.Enter);
            theme.SelectedOption.ShouldBe("Light");
            values.ShouldBe(["Dark", "System", "Dark", "System", "Light"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Left_And_Right_Move_The_Caret_When_The_Active_Row_Is_Not_A_Choice()
    {
        var palette = Palette(out _, out var compact, out _, out _);
        var window = Show(palette);
        try
        {
            palette.Query = "compact";
            Settle();
            palette.ActiveItem.ShouldBe(compact);
            palette.SearchBox!.CaretIndex = 0;
            Press(window, PhysicalKey.ArrowRight);
            palette.SearchBox.CaretIndex.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Runs_An_Action_Then_Raises_ItemInvoked()
    {
        var palette = Palette(out _, out _, out _, out var email);
        var ran = 0;
        email.Command = new TestCommand(() => ran++);
        email.CommandParameter = "email";
        var window = Show(palette);
        SettingsCommandItem? invoked = null;
        palette.ItemInvoked += (_, e) =>
        {
            ran.ShouldBe(1);
            invoked = e.Item;
        };
        try
        {
            palette.Query = "change";
            Settle();
            palette.Classes.Contains(SettingsCommand.ActionActivePseudoClass).ShouldBeTrue();
            Press(window, PhysicalKey.Enter);
            ran.ShouldBe(1);
            invoked.ShouldBe(email);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Clears_The_Query_First_And_Then_Requests_Close()
    {
        var palette = Palette(out _, out _, out _, out _);
        var window = Show(palette);
        var closes = 0;
        palette.CloseRequested += (_, _) => closes++;
        try
        {
            palette.Query = "theme";
            Settle();
            Press(window, PhysicalKey.Escape);
            palette.Query.ShouldBe(string.Empty);
            closes.ShouldBe(0);
            palette.VisibleRows.Count.ShouldBe(6);

            Press(window, PhysicalKey.Escape);
            closes.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Query_Without_Matches_Shows_The_No_Results_Message()
    {
        var palette = Palette(out _, out _, out _, out _);
        var window = Show(palette);
        try
        {
            palette.Query = "zebra";
            Settle();
            palette.VisibleRows.ShouldBeEmpty();
            palette.ActiveItem.ShouldBeNull();
            palette.Classes.Contains(SettingsCommand.NoResultsPseudoClass).ShouldBeTrue();
            var empty = TestHelpers.Descendant<StackPanel>(palette, "PART_Empty");
            empty.IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(palette, "PART_EmptyTitle").Text.ShouldBe("No settings match “zebra”");
            TestHelpers.Descendant<TextBlock>(palette, "PART_EmptyHint").Text.ShouldBe("Try another word, such as theme or email.");

            palette.Labels = new SettingsCommandLabels { NoResults = q => $"Geen instellingen voor {q}" };
            TestHelpers.Descendant<TextBlock>(palette, "PART_EmptyTitle").Text.ShouldBe("Geen instellingen voor zebra");

            palette.Clear();
            Settle();
            empty.IsVisible.ShouldBeFalse();
            palette.Classes.Contains(SettingsCommand.NoResultsPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_A_Row_Acts_Like_Enter_And_Keeps_The_Search_Focused()
    {
        var palette = Palette(out _, out var compact, out _, out _);
        var window = Show(palette);
        var changes = 0;
        palette.SettingChanged += (_, _) => changes++;
        try
        {
            var row = palette.RowOf(compact)!;
            var point = row.TranslatePoint(new Point(80, row.Bounds.Height / 2), window)!.Value;
            window.MouseMove(point);
            Settle();
            palette.ActiveItem.ShouldBe(compact);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Settle();
            compact.IsChecked.ShouldBeTrue();
            changes.ShouldBe(1);
            palette.SearchBox!.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Choice_Arrows_Step_Without_Wrapping()
    {
        var palette = Palette(out var theme, out _, out _, out _);
        var window = Show(palette);
        try
        {
            var pill = palette.RowOf(theme)!.Pill!;
            var previous = TestHelpers.Descendant<SettingsCommandStepButton>(pill, SettingsCommandChoicePill.PreviousPart);
            var next = TestHelpers.Descendant<SettingsCommandStepButton>(pill, SettingsCommandChoicePill.NextPart);
            pill.Classes.Contains(SettingsCommandChoicePill.FirstPseudoClass).ShouldBeTrue();
            AutomationProperties.GetName(next).ShouldBe("Next option");

            Click(window, next);
            theme.SelectedOption.ShouldBe("Dark");
            Click(window, previous);
            Click(window, previous);
            theme.SelectedOption.ShouldBe("Light");
            palette.SearchBox!.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Highlight_Sits_On_The_Active_Row()
    {
        var palette = Palette(out _, out var compact, out _, out _);
        var window = Show(palette);
        try
        {
            var highlight = TestHelpers.Descendant<GlidingIndicator>(palette, SettingsCommand.HighlightPart);
            Press(window, PhysicalKey.ArrowDown);
            highlight.HasTarget.ShouldBeTrue();
            highlight.CurrentRect.ShouldBe(palette.RowOf(compact)!.Bounds);
            palette.RowOf(compact)!.IsActive.ShouldBeTrue();

            palette.Query = "zebra";
            Settle();
            highlight.HasTarget.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Values_Bind_Two_Way_And_Rows_Follow_Changes_Made_By_The_App()
    {
        var state = new State();
        var palette = Palette(out var theme, out var compact, out _, out _);
        compact.Bind(SettingsCommandToggle.IsCheckedProperty, new Binding(nameof(State.Compact)) { Source = state, Mode = BindingMode.TwoWay });
        theme.Bind(SettingsCommandChoice.SelectedOptionProperty, new Binding(nameof(State.Theme)) { Source = state, Mode = BindingMode.TwoWay });
        var window = Show(palette);
        var changes = 0;
        palette.SettingChanged += (_, _) => changes++;
        try
        {
            palette.RowOf(theme)!.Pill!.Value.ShouldBe("Dark");

            Press(window, PhysicalKey.ArrowRight);
            state.Theme.ShouldBe("System");
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.Enter);
            state.Compact.ShouldBeTrue();
            changes.ShouldBe(2);

            // The app changes the values: the rows follow, and no SettingChanged is raised.
            state.Compact = false;
            state.Theme = "Light";
            Settle();
            palette.RowOf(compact)!.Switch!.IsChecked.ShouldBe(false);
            palette.RowOf(theme)!.Pill!.Value.ShouldBe("Light");
            changes.ShouldBe(2);
            AutomationProperties.GetHelpText(palette.SearchBox!).ShouldBe("Compact mode, off");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Adding_And_Removing_Items_Updates_The_List()
    {
        var palette = Palette(out var theme, out _, out _, out _);
        var window = Show(palette);
        try
        {
            var language = new SettingsCommandChoice { Header = "Language", Options = { "English", "Nederlands" } };
            palette.Items.Add(language);
            Settle();
            palette.VisibleRows.Last().Item.ShouldBe(language);
            // Mixed with grouped items, an ungrouped one is listed under "Settings".
            palette.RowOf(language)!.LeadingHeader!.Text.ShouldBe("Settings");

            palette.Items.Remove(theme);
            Settle();
            palette.RowOf(theme).ShouldBeNull();
            palette.ActiveItem!.Header.ShouldBe("Compact mode");

            theme.Header = "Appearance theme";
            palette.Items = new Avalonia.Collections.AvaloniaList<SettingsCommandItem> { theme };
            Settle();
            Headers(palette).ShouldBe(["Appearance theme"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Options_Can_Be_Written_As_A_Comma_Separated_String_In_Xaml()
    {
        var palette = (SettingsCommand)AvaloniaRuntimeXamlLoader.Load(
            """
            <arc:SettingsCommand xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
                <arc:SettingsCommandChoice Header="Theme" Options="Light, Dark, System" />
                <arc:SettingsCommandToggle Header="Compact mode" IsChecked="True" />
            </arc:SettingsCommand>
            """);
        var choice = palette.Items.OfType<SettingsCommandChoice>().Single();
        choice.Options.ShouldBe(["Light", "Dark", "System"]);
        choice.SelectedOption.ShouldBe("Light");
        palette.Items.OfType<SettingsCommandToggle>().Single().IsChecked.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Search_Field_And_Rows_Are_Named_For_Assistive_Technology()
    {
        var palette = Palette(out var theme, out _, out _, out _);
        var window = Show(palette);
        try
        {
            AutomationProperties.GetName(palette.SearchBox!).ShouldBe("Search settings");
            AutomationProperties.GetHelpText(palette.SearchBox!).ShouldBe("Theme, Light");
            var results = TestHelpers.Descendant<SettingsCommandResults>(palette, SettingsCommand.ResultsPart);
            AutomationProperties.GetName(results).ShouldBe("Settings");
            var row = palette.RowOf(theme)!;
            AutomationProperties.GetName(row).ShouldBe("Theme");
            Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(row).GetAutomationControlType()
                  .ShouldBe(Avalonia.Automation.Peers.AutomationControlType.ListItem);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Footer_Hints_That_Do_Not_Fit_Give_Way_Lowest_Priority_First()
    {
        // Three hints as in the footer: Navigate (1), Enter (2), Escape (0), each 100 wide with 16 between.
        var navigate = new Border { Width = 100, Height = 20 };
        var enter = new Border { Width = 100, Height = 20 };
        var close = new Border { Width = 100, Height = 20 };
        SettingsCommandHintsPanel.SetPriority(navigate, 1);
        SettingsCommandHintsPanel.SetPriority(enter, 2);
        var hints = new SettingsCommandHintsPanel { Gap = 16, Children = { navigate, enter, close } };

        hints.Measure(new Size(400, 40));
        hints.Children.Any(hints.IsDropped).ShouldBeFalse();
        hints.DesiredSize.Width.ShouldBe(332);

        hints.Measure(new Size(300, 40));
        hints.IsDropped(close).ShouldBeTrue();
        hints.IsDropped(navigate).ShouldBeFalse();
        hints.DesiredSize.Width.ShouldBe(216);

        hints.Measure(new Size(150, 40));
        hints.IsDropped(navigate).ShouldBeTrue();
        hints.IsDropped(enter).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Palette_Is_Collected_While_Its_Items_Live_On()
    {
        var shared = new SettingsCommandToggle { Group = "Appearance", Header = "Compact mode" };
        var list = new System.Collections.ObjectModel.ObservableCollection<SettingsCommandItem>
        {
            new SettingsCommandToggle { Group = "Privacy", Header = "Read receipts" }
        };
        // An item the host keeps, in the palette's own list; and a list the host hands in.
        var withItems = TestHelpers.ShowAndClose(() =>
        {
            var palette = new SettingsCommand { Width = 560 };
            palette.Items.Add(shared);
            return palette;
        });
        var withList = TestHelpers.ShowAndClose(() => new SettingsCommand { Width = 560, Items = list });
        TestHelpers.Collect(withItems, withList);
        withItems.IsAlive.ShouldBeFalse();
        withList.IsAlive.ShouldBeFalse();
        shared.IsChecked = true;
        shared.Header = "Dense rows";
        list.Add(new SettingsCommandAction { Header = "Later" });
        list[0].Header = "Renamed";
    }

    [AvaloniaFact]
    public void A_Palette_Put_Back_Shows_What_Changed_While_It_Was_Away()
    {
        var palette = Palette(out _, out var compact, out _, out var email);
        var host = new Border { Child = palette };
        var window = TestHelpers.Show(host);
        window.Width = 700;
        window.Height = 700;
        Settle();
        try
        {
            palette.RowOf(compact)!.Switch!.IsChecked.ShouldBe(false);
            host.Child = null;
            Settle();
            compact.Header = "Dense rows";
            compact.IsChecked = true;
            palette.Items.Remove(email);

            host.Child = palette;
            Settle();
            Headers(palette).ShouldBe(["Theme", "Dense rows", "Read receipts", "Mentions", "Email digests"]);
            palette.RowOf(compact)!.HeaderText!.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text).ShouldBe(["Dense rows"]);
            palette.RowOf(compact)!.Switch!.IsChecked.ShouldBe(true);

            // Back in the window, it listens again.
            compact.IsChecked = false;
            Settle();
            palette.RowOf(compact)!.Switch!.IsChecked.ShouldBe(false);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Palette_Put_Back_Follows_A_List_The_Host_Changed_While_It_Was_Away()
    {
        var list = new System.Collections.ObjectModel.ObservableCollection<SettingsCommandItem>
        {
            new SettingsCommandToggle { Group = "Privacy", Header = "Read receipts" }
        };
        var palette = new SettingsCommand { Width = 560, Items = list };
        var host = new Border { Child = palette };
        var window = TestHelpers.Show(host);
        window.Width = 700;
        window.Height = 700;
        Settle();
        try
        {
            Headers(palette).ShouldBe(["Read receipts"]);
            host.Child = null;
            Settle();
            list.Add(new SettingsCommandToggle { Group = "Privacy", Header = "Typing indicator" });

            host.Child = palette;
            Settle();
            Headers(palette).ShouldBe(["Read receipts", "Typing indicator"]);
            list.RemoveAt(0);
            Settle();
            Headers(palette).ShouldBe(["Typing indicator"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Footer_Never_Overflows_And_Keeps_The_Enter_Hint_Longest()
    {
        var palette = Palette(out _, out _, out _, out _);
        palette.Width = 320;
        var window = Show(palette);
        try
        {
            var hints = TestHelpers.Descendant<SettingsCommandHintsPanel>(palette, "PART_Hints");
            var enter = TestHelpers.Descendant<StackPanel>(palette, "PART_EnterHint");
            SettingsCommandHintsPanel.GetPriority(enter).ShouldBe(hints.Children.Max(SettingsCommandHintsPanel.GetPriority));
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.Enter);
            palette.NoticeText.ShouldBe("Compact mode on");
            var notice = TestHelpers.Descendant<ArcContentSwap>(palette, SettingsCommand.NoticePart);
            (hints.DesiredSize.Width + notice.Bounds.Width).ShouldBeLessThanOrEqualTo(320);
        }
        finally
        {
            window.Close();
        }
    }

    private static void Click(Window window, Control control)
    {
        Settle();
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle();
    }

    private sealed class TestCommand(Action run) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => run();
    }

    private sealed class State : INotifyPropertyChanged
    {
        private bool _compact;
        private string? _theme = "Dark";

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool Compact
        {
            get => _compact;
            set => Set(ref _compact, value);
        }

        public string? Theme
        {
            get => _theme;
            set => Set(ref _theme, value);
        }

        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
