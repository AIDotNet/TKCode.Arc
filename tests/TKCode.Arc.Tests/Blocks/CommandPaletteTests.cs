using System.Collections.ObjectModel;
using System.Windows.Input;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;
using GalleryDocs = TKCode.Arc.Gallery.Docs.GalleryDocs;

namespace TKCode.Arc.Tests.Blocks;

public class CommandPaletteTests
{
    private static List<CommandPaletteItem> Sample() =>
    [
        new() { Id = "dashboard", Label = "Go to dashboard", Description = "Overview", Group = "Navigation", Shortcut = "G D" },
        new() { Id = "projects", Label = "Open projects", Group = "Navigation" },
        new() { Id = "new", Label = "Create project", Description = "Start from a blank board", Group = "Actions", Shortcut = "C" },
        new() { Id = "invite", Label = "Invite teammate", Group = "Actions" },
        new() { Id = "theme", Label = "Switch theme", Group = "Settings", Keywords = ["dark", "appearance"] },
        new() { Id = "sign-out", Label = "Sign out", Group = "Settings" }
    ];

    private static Window Show(CommandPalette palette, double width = 560, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        palette.Width = width;
        var window = TestHelpers.Show(palette, level);
        window.Width = 800;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static T Part<T>(CommandPalette palette, string name) where T : Control => TestHelpers.Descendant<T>(palette, name);

    private static TextBox Input(CommandPalette palette) => Part<TextBox>(palette, CommandPalette.InputPart);

    private static CommandPaletteList List(CommandPalette palette) => Part<CommandPaletteList>(palette, CommandPalette.ListPart);

    private static CommandPaletteHighlight Highlight(CommandPalette palette) => Part<CommandPaletteHighlight>(palette, CommandPalette.HighlightPart);

    private static IEnumerable<CommandPaletteRow> Rows(CommandPalette palette) =>
        List(palette).LiveEntries.Where(e => !e.IsHeading).Select(e => e.Row!);

    private static string[] Headings(CommandPalette palette) =>
        List(palette).LiveEntries.Where(e => e.IsHeading).Select(e => ((CommandPaletteGroupHeading)e.Visual).GroupName).ToArray();

    private static string[] Ids(IEnumerable<CommandPaletteItem> items) => items.Select(i => i.Id).ToArray();

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Type(Window window, string text)
    {
        window.KeyTextInput(text);
        Dispatcher.UIThread.RunJobs();
    }

    // ---- filter -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Filter_Searches_Label_Description_Group_And_Keywords_Joined_By_Spaces()
    {
        var item = new CommandPaletteItem { Label = "Switch theme", Description = "Light or dark", Group = "Settings", Keywords = ["appearance"] };
        CommandPaletteFilter.Matches(item, CommandPaletteFilter.Normalize("  THEME ")).ShouldBeTrue();
        CommandPaletteFilter.Matches(item, CommandPaletteFilter.Normalize("dark")).ShouldBeTrue();
        CommandPaletteFilter.Matches(item, CommandPaletteFilter.Normalize("settings")).ShouldBeTrue();
        CommandPaletteFilter.Matches(item, CommandPaletteFilter.Normalize("appear")).ShouldBeTrue();
        // The fields are joined, so a query may run across two of them (as in Arc).
        CommandPaletteFilter.Matches(item, CommandPaletteFilter.Normalize("theme light")).ShouldBeTrue();
        CommandPaletteFilter.Matches(item, CommandPaletteFilter.Normalize("billing")).ShouldBeFalse();
        CommandPaletteFilter.Matches(item, CommandPaletteFilter.Normalize("   ")).ShouldBeTrue();
    }

    [Fact]
    public void Groups_Follow_First_Appearance_And_Ungrouped_Commands_Go_To_The_Default_Group()
    {
        List<CommandPaletteItem> items =
        [
            new() { Id = "a", Label = "A", Group = "One" },
            new() { Id = "b", Label = "B" },
            new() { Id = "c", Label = "C", Group = "Two" },
            new() { Id = "d", Label = "D", Group = "One" },
            new() { Id = "e", Label = "E", Group = "" }
        ];
        var groups = CommandPaletteFilter.Group(items, string.Empty, "Actions");
        groups.Select(g => g.Name).ShouldBe(["One", "Actions", "Two"]);
        groups[0].Items.Select(i => i.Id).ShouldBe(["a", "d"]);
        groups[1].Items.Select(i => i.Id).ShouldBe(["b", "e"]);
    }

    [Fact]
    public void Rows_Glide_Only_When_Every_Row_Moves_Six_Places_Or_Fewer()
    {
        var items = Enumerable.Range(0, 12).Select(i => new CommandPaletteItem { Id = $"i{i}", Label = $"Item {i}" }).ToList();
        CommandPaletteFilter.ShouldGlide(items, items.Skip(3).ToList()).ShouldBeTrue();
        CommandPaletteFilter.ShouldGlide(items, items.Skip(6).ToList()).ShouldBeTrue();
        CommandPaletteFilter.ShouldGlide(items, items.Skip(7).ToList()).ShouldBeFalse();
        CommandPaletteFilter.ShouldGlide(items.Skip(9).ToList(), items).ShouldBeFalse();
        // Rows keep their identity by id, even across new item objects.
        var copies = items.Select(i => new CommandPaletteItem { Id = i.Id, Label = i.Label }).ToList();
        CommandPaletteFilter.ShouldGlide(items, copies).ShouldBeTrue();
        CommandPaletteFilter.ShouldGlide([], items).ShouldBeTrue();
    }

    // ---- defaults and template ---------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Defaults_And_Template_Parts()
    {
        var palette = new CommandPalette();
        palette.PlaceholderText.ShouldBe("Search commands");
        palette.Label.ShouldBe("Command palette");
        palette.EmptyTitle.ShouldBe("No matching actions");
        palette.EmptyDescription.ShouldBe("Try a different word or clear the search.");
        palette.DefaultGroup.ShouldBe("Actions");
        palette.Query.ShouldBe(string.Empty);
        palette.IsShortcutEnabled.ShouldBeTrue();
        palette.ShowCloseButton.ShouldBeFalse();
        palette.AutoFocus.ShouldBeFalse();
        palette.ShortcutHint.ShouldBeNull();
        palette.ActiveItem.ShouldBeNull();
        foreach (var item in Sample())
        {
            palette.Items.Add(item);
        }
        var window = Show(palette);
        try
        {
            var input = Input(palette);
            AutomationProperties.GetName(input).ShouldBe("Command palette");
            input.PlaceholderText.ShouldBe("Search commands");
            Part<AvaloniaButton>(palette, CommandPalette.CloseButtonPart).IsVisible.ShouldBeFalse();
            Part<AvaloniaButton>(palette, CommandPalette.ClearButtonPart).IsVisible.ShouldBeFalse();
            var key = Part<Border>(palette, "PART_CommandKey");
            key.IsEffectivelyVisible.ShouldBeTrue();
            var hint = key.GetVisualDescendants().OfType<TextBlock>().First().Text;
            hint.ShouldBe(OperatingSystem.IsMacOS() ? "⌘ K" : "Ctrl K");
            palette.ShortcutHint = "Ctrl K";
            Dispatcher.UIThread.RunJobs();
            key.GetVisualDescendants().OfType<TextBlock>().First().Text.ShouldBe("Ctrl K");

            // Arc's geometry: a 54px search row, 34px rows, a 342px list at most.
            Part<Border>(palette, CommandPalette.SearchRowPart).Bounds.Height.ShouldBe(54, 0.5);
            Rows(palette).First().Bounds.Height.ShouldBe(34, 0.5);
            Part<ScrollViewer>(palette, CommandPalette.ScrollPart).MaxHeight.ShouldBe(342);
            Ids(palette.VisibleItems).ShouldBe(["dashboard", "projects", "new", "invite", "theme", "sign-out"]);
            Headings(palette).ShouldBe(["Navigation", "Actions", "Settings"]);
            Part<Control>(palette, CommandPalette.EmptyPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rows_Show_Label_Description_Icon_And_Shortcut()
    {
        var custom = new Border { Width = 16, Height = 16 };
        var palette = new CommandPalette
        {
            ItemsSource =
            [
                new CommandPaletteItem { Id = "a", Label = "Create project", Description = "Blank board", Shortcut = "C" },
                new CommandPaletteItem { Id = "b", Label = "Upload", Icon = ArcIcons.Upload },
                new CommandPaletteItem { Id = "c", Label = "Custom", Icon = custom }
            ]
        };
        var window = Show(palette);
        try
        {
            var rows = Rows(palette).ToList();
            rows[0].Title.ShouldBe("Create project");
            rows[0].Description.ShouldBe("Blank board");
            rows[0].Shortcut.ShouldBe("C");
            // No icon: Lucide Command, as in Arc.
            rows[0].IconGeometry.ShouldBe(ArcIcons.Command);
            AutomationProperties.GetName(rows[0]).ShouldBe("Create project, Blank board");
            rows[1].IconGeometry.ShouldBe(ArcIcons.Upload);
            rows[1].Description.ShouldBeNull();
            rows[2].IconControl.ShouldBe(custom);
            custom.IsEffectivelyVisible.ShouldBeTrue();
            // Ungrouped commands are listed under "Actions".
            Headings(palette).ShouldBe(["Actions"]);
        }
        finally
        {
            window.Close();
        }
    }

    // ---- typing and keyboard ---------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Typing_Filters_And_Escape_Clears_Then_Asks_To_Close()
    {
        var palette = new CommandPalette { ItemsSource = Sample() };
        var closes = 0;
        palette.CloseRequested += (_, _) => closes++;
        var window = Show(palette);
        try
        {
            palette.FocusInput();
            Dispatcher.UIThread.RunJobs();
            Input(palette).IsFocused.ShouldBeTrue();
            Type(window, "proj");
            palette.Query.ShouldBe("proj");
            Ids(palette.VisibleItems).ShouldBe(["projects", "new"]);
            Headings(palette).ShouldBe(["Navigation", "Actions"]);
            palette.Classes.ShouldContain(CommandPalette.HasQueryPseudoClass);
            Part<AvaloniaButton>(palette, CommandPalette.ClearButtonPart).IsVisible.ShouldBeTrue();

            Press(window, PhysicalKey.Escape);
            palette.Query.ShouldBe(string.Empty);
            palette.VisibleItems.Count.ShouldBe(6);
            closes.ShouldBe(0);
            Part<AvaloniaButton>(palette, CommandPalette.ClearButtonPart).IsVisible.ShouldBeFalse();

            Press(window, PhysicalKey.Escape);
            closes.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Query_Is_Two_Way_And_Clear_Resets_It()
    {
        var palette = new CommandPalette { ItemsSource = Sample() };
        var window = Show(palette);
        try
        {
            palette.Query = "sign";
            Dispatcher.UIThread.RunJobs();
            Input(palette).Text.ShouldBe("sign");
            Ids(palette.VisibleItems).ShouldBe(["sign-out"]);
            palette.Clear();
            Dispatcher.UIThread.RunJobs();
            Input(palette).Text.ShouldBe(string.Empty);
            palette.VisibleItems.Count.ShouldBe(6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrows_Home_And_End_Move_The_Highlight_And_Stop_At_The_Ends()
    {
        var palette = new CommandPalette { ItemsSource = Sample() };
        var window = Show(palette);
        try
        {
            palette.FocusInput();
            Dispatcher.UIThread.RunJobs();
            // From nothing, ArrowUp lands on the last command and ArrowDown on the first.
            Press(window, PhysicalKey.ArrowUp);
            palette.ActiveItem!.Id.ShouldBe("sign-out");
            // Escape in an empty field only asks to close; it keeps the highlight (as in Arc).
            Press(window, PhysicalKey.Escape);
            palette.ActiveIndex.ShouldBe(5);
            palette.Clear();
            palette.ActiveIndex.ShouldBe(-1);
            Press(window, PhysicalKey.ArrowDown);
            palette.ActiveIndex.ShouldBe(0);
            Press(window, PhysicalKey.ArrowUp);
            palette.ActiveIndex.ShouldBe(0);
            Press(window, PhysicalKey.End);
            palette.ActiveIndex.ShouldBe(5);
            Press(window, PhysicalKey.ArrowDown);
            palette.ActiveIndex.ShouldBe(5);
            Press(window, PhysicalKey.Home);
            palette.ActiveItem!.Id.ShouldBe("dashboard");
            Press(window, PhysicalKey.ArrowDown);
            palette.ActiveItem!.Id.ShouldBe("projects");

            // One highlight sits on the active row; the row reports itself as selected.
            var highlight = Highlight(palette);
            var row = Rows(palette).Single(r => r.Item!.Id == "projects");
            highlight.IsShown.ShouldBeTrue();
            highlight.CurrentOpacity.ShouldBe(1);
            highlight.CurrentTop.ShouldBe(List(palette).FindRow(palette.ActiveItem)!.Y, 0.01);
            highlight.CurrentHeight.ShouldBe(row.Bounds.Height, 0.01);
            row.IsActive.ShouldBeTrue();
            row.Classes.ShouldContain(CommandPaletteRow.ActivePseudoClass);
            var peer = (ISelectionItemProvider)ControlAutomationPeer.CreatePeerForElement(row);
            peer.IsSelected.ShouldBeTrue();
            AutomationProperties.GetHelpText(Input(palette)).ShouldBe("Open projects");

            // Typing starts over: no highlight.
            Type(window, "s");
            palette.ActiveItem.ShouldBeNull();
            highlight.IsShown.ShouldBeFalse();
            highlight.CurrentOpacity.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Runs_The_Active_Command_Or_The_Top_Match_After_Typing()
    {
        var items = Sample();
        var executed = new List<object?>();
        var itemCommand = new RecordingCommand(executed);
        items[4].Command = itemCommand;
        items[4].CommandParameter = "theme-param";
        var selectedByCommand = new List<object?>();
        var palette = new CommandPalette { ItemsSource = items, SelectCommand = new RecordingCommand(selectedByCommand) };
        var chosen = new List<CommandPaletteItem>();
        palette.ItemSelected += (_, e) =>
        {
            chosen.Add(e.Item);
            // The field is already reset when the event is raised.
            palette.Query.ShouldBe(string.Empty);
        };
        var window = Show(palette);
        try
        {
            palette.FocusInput();
            Dispatcher.UIThread.RunJobs();
            // Nothing highlighted and nothing typed: Enter does nothing.
            Press(window, PhysicalKey.Enter);
            chosen.ShouldBeEmpty();

            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.Enter);
            chosen.Select(i => i.Id).ShouldBe(["projects"]);
            selectedByCommand.ShouldBe([items[1]]);
            palette.ActiveItem.ShouldBeNull();
            Input(palette).IsFocused.ShouldBeTrue();

            Type(window, "dark");
            Press(window, PhysicalKey.Enter);
            chosen.Select(i => i.Id).ShouldBe(["projects", "theme"]);
            executed.ShouldBe(["theme-param"]);
            palette.Query.ShouldBe(string.Empty);
            palette.VisibleItems.Count.ShouldBe(6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Commands_Are_Skipped_And_Cannot_Be_Chosen()
    {
        var items = Sample();
        items[0].IsEnabled = false;
        items[2].IsEnabled = false;
        var palette = new CommandPalette { ItemsSource = items };
        var chosen = new List<CommandPaletteItem>();
        palette.ItemSelected += (_, e) => chosen.Add(e.Item);
        var window = Show(palette);
        try
        {
            palette.FocusInput();
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.ArrowDown);
            palette.ActiveItem!.Id.ShouldBe("projects");
            Press(window, PhysicalKey.ArrowDown);
            palette.ActiveItem!.Id.ShouldBe("invite");
            Press(window, PhysicalKey.Home);
            palette.ActiveItem!.Id.ShouldBe("projects");
            Press(window, PhysicalKey.ArrowUp);
            palette.ActiveItem!.Id.ShouldBe("projects");
            Rows(palette).First().IsEnabled.ShouldBeFalse();
            Rows(palette).First().Classes.ShouldContain(":disabled");

            // The top match skips a disabled command.
            Type(window, "o");
            Ids(palette.VisibleItems).First().ShouldBe("dashboard");
            Press(window, PhysicalKey.Enter);
            chosen.Single().Id.ShouldBe("projects");
        }
        finally
        {
            window.Close();
        }
    }

    // ---- data -------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Inline_Items_And_Collection_Changes_Update_The_List()
    {
        var palette = new CommandPalette();
        var window = Show(palette);
        try
        {
            // No commands at all: the empty state.
            Part<Control>(palette, CommandPalette.EmptyPart).IsVisible.ShouldBeTrue();
            List(palette).IsVisible.ShouldBeFalse();

            palette.Items.Add(new CommandPaletteItem { Id = "a", Label = "Alpha", Group = "One" });
            palette.Items.Add(new CommandPaletteItem { Id = "b", Label = "Beta", Group = "Two" });
            Dispatcher.UIThread.RunJobs();
            Part<Control>(palette, CommandPalette.EmptyPart).IsVisible.ShouldBeFalse();
            Ids(palette.VisibleItems).ShouldBe(["a", "b"]);
            Headings(palette).ShouldBe(["One", "Two"]);

            palette.Items.RemoveAt(0);
            Dispatcher.UIThread.RunJobs();
            Ids(palette.VisibleItems).ShouldBe(["b"]);
            Headings(palette).ShouldBe(["Two"]);

            // ItemsSource replaces Items and is followed.
            var source = new ObservableCollection<CommandPaletteItem>(Sample());
            palette.ItemsSource = source;
            Dispatcher.UIThread.RunJobs();
            palette.VisibleItems.Count.ShouldBe(6);
            source.Add(new CommandPaletteItem { Id = "help", Label = "Open help", Group = "Navigation" });
            Dispatcher.UIThread.RunJobs();
            Ids(palette.VisibleItems).Take(3).ShouldBe(["dashboard", "projects", "help"]);
            source.RemoveAt(0);
            Dispatcher.UIThread.RunJobs();
            palette.VisibleItems.Count.ShouldBe(6);

            // Property changes of a command update its row and the filter.
            source[0].Label = "Open all projects";
            Dispatcher.UIThread.RunJobs();
            Rows(palette).First().Title.ShouldBe("Open all projects");
            // Groups follow the order they first appear in: "Open all projects" now leads with Settings.
            source[0].Group = "Settings";
            Dispatcher.UIThread.RunJobs();
            Headings(palette).ShouldBe(["Settings", "Actions", "Navigation"]);
            palette.VisibleItems[0].ShouldBe(source[0]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Highlight_Stays_On_Its_Command_When_The_Data_Changes()
    {
        var source = new ObservableCollection<CommandPaletteItem>(Sample());
        var palette = new CommandPalette { ItemsSource = source };
        var window = Show(palette);
        try
        {
            palette.FocusInput();
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.ArrowDown);
            palette.ActiveItem!.Id.ShouldBe("projects");
            source.Insert(0, new CommandPaletteItem { Id = "first", Label = "First", Group = "Navigation" });
            Dispatcher.UIThread.RunJobs();
            palette.ActiveItem!.Id.ShouldBe("projects");
            palette.ActiveIndex.ShouldBe(2);
            Highlight(palette).TargetTop.ShouldBe(List(palette).FindRow(palette.ActiveItem)!.Y, 0.01);

            // The active command leaves: the highlight moves to the command now at its place.
            source.RemoveAt(2);
            Dispatcher.UIThread.RunJobs();
            palette.ActiveIndex.ShouldBe(2);
            palette.ActiveItem!.Id.ShouldBe("new");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Everything_Filtered_Out_Shows_The_Empty_State()
    {
        var palette = new CommandPalette { ItemsSource = Sample(), EmptyTitle = "Nothing here", EmptyDescription = "Try again." };
        var window = Show(palette);
        try
        {
            palette.Query = "zzz";
            Dispatcher.UIThread.RunJobs();
            palette.VisibleItems.ShouldBeEmpty();
            var empty = Part<Control>(palette, CommandPalette.EmptyPart);
            empty.IsVisible.ShouldBeTrue();
            List(palette).IsVisible.ShouldBeFalse();
            Part<TextBlock>(palette, "PART_EmptyTitle").Text.ShouldBe("Nothing here");
            Part<TextBlock>(palette, "PART_EmptyDescription").Text.ShouldBe("Try again.");
            empty.Bounds.Height.ShouldBeGreaterThan(40);

            // Arrows and Enter have nothing to act on.
            palette.FocusInput();
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.ArrowDown);
            palette.ActiveItem.ShouldBeNull();

            palette.Query = "sign";
            Dispatcher.UIThread.RunJobs();
            empty.IsVisible.ShouldBeFalse();
            List(palette).IsVisible.ShouldBeTrue();
            Ids(palette.VisibleItems).ShouldBe(["sign-out"]);
        }
        finally
        {
            window.Close();
        }
    }

    // ---- pointer ----------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Hovering_A_Row_Highlights_It_And_Clicking_Chooses_It()
    {
        var palette = new CommandPalette { ItemsSource = Sample() };
        var chosen = new List<CommandPaletteItem>();
        palette.ItemSelected += (_, e) => chosen.Add(e.Item);
        var window = Show(palette);
        try
        {
            var row = Rows(palette).Single(r => r.Item!.Id == "invite");
            var point = row.TranslatePoint(new Point(40, row.Bounds.Height / 2), window)!.Value;
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            window.MouseMove(point);
            Dispatcher.UIThread.RunJobs();
            palette.ActiveItem!.Id.ShouldBe("invite");
            Highlight(palette).IsShown.ShouldBeTrue();

            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            chosen.Select(i => i.Id).ShouldBe(["invite"]);
            palette.ActiveItem.ShouldBeNull();
            Input(palette).IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Close_Button_Raises_CloseRequested_And_Clear_Button_Clears()
    {
        var palette = new CommandPalette { ItemsSource = Sample(), ShowCloseButton = true };
        var closes = 0;
        palette.CloseRequested += (_, _) => closes++;
        var window = Show(palette);
        try
        {
            var close = Part<AvaloniaButton>(palette, CommandPalette.CloseButtonPart);
            close.IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(close).ShouldBe("Close command palette");
            close.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            closes.ShouldBe(1);

            palette.Query = "theme";
            Dispatcher.UIThread.RunJobs();
            var clear = Part<AvaloniaButton>(palette, CommandPalette.ClearButtonPart);
            clear.IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(clear).ShouldBe("Clear search");
            clear.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            palette.Query.ShouldBe(string.Empty);
            clear.IsVisible.ShouldBeFalse();
            Input(palette).IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    // ---- shortcut and focus -----------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Command_K_Focuses_The_Field_Unless_Turned_Off()
    {
        var palette = new CommandPalette { ItemsSource = Sample() };
        var other = new AvaloniaButton { Content = "Elsewhere" };
        var panel = new StackPanel { Children = { other, palette } };
        var window = TestHelpers.Show(panel);
        try
        {
            other.Focus();
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.K, RawInputModifiers.Control);
            Input(palette).IsFocused.ShouldBeTrue();

            other.Focus();
            Press(window, PhysicalKey.K, RawInputModifiers.Meta);
            Input(palette).IsFocused.ShouldBeTrue();

            other.Focus();
            palette.IsShortcutEnabled = false;
            Press(window, PhysicalKey.K, RawInputModifiers.Control);
            other.IsFocused.ShouldBeTrue();

            // Detached, it no longer answers the window's shortcut.
            palette.IsShortcutEnabled = true;
            panel.Children.Remove(palette);
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.K, RawInputModifiers.Control);
            other.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Palette_Holding_The_Focus_Answers_Command_K_For_Itself()
    {
        var first = new CommandPalette { ItemsSource = Sample() };
        var second = new CommandPalette { ItemsSource = Sample() };
        var window = TestHelpers.Show(new StackPanel { Children = { first, second } });
        try
        {
            second.FocusInput();
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.K, RawInputModifiers.Control);
            Input(second).IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AutoFocus_Focuses_The_Field_When_Loaded()
    {
        var palette = new CommandPalette { ItemsSource = Sample(), AutoFocus = true };
        var window = Show(palette);
        try
        {
            Input(palette).IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    // ---- narrow -----------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Narrow_Widths_Hide_The_Shortcut_Caps_And_Tighten_The_Padding()
    {
        var palette = new CommandPalette { ItemsSource = Sample() };
        var window = Show(palette, width: 560);
        try
        {
            var row = Rows(palette).First();
            var cap = row.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Shortcut");
            palette.Classes.ShouldNotContain(CommandPalette.NarrowPseudoClass);
            cap.IsEffectivelyVisible.ShouldBeTrue();
            Part<Border>(palette, CommandPalette.SearchRowPart).Padding.ShouldBe(new Thickness(14, 8));

            palette.Width = 420;
            Dispatcher.UIThread.RunJobs();
            palette.Classes.ShouldContain(CommandPalette.NarrowPseudoClass);
            palette.Classes.ShouldNotContain(CommandPalette.NarrowestPseudoClass);
            cap.IsEffectivelyVisible.ShouldBeFalse();
            Part<Border>(palette, "PART_CommandKey").IsVisible.ShouldBeFalse();
            Part<Border>(palette, CommandPalette.SearchRowPart).Padding.ShouldBe(new Thickness(11, 8));
            Part<Border>(palette, CommandPalette.ResultsPart).Padding.ShouldBe(new Thickness(6));
            row.Padding.ShouldBe(new Thickness(8, 0));

            palette.Width = 320;
            Dispatcher.UIThread.RunJobs();
            palette.Classes.ShouldContain(CommandPalette.NarrowestPseudoClass);
            row.Padding.ShouldBe(new Thickness(6, 0));

            palette.Width = 560;
            Dispatcher.UIThread.RunJobs();
            cap.IsEffectivelyVisible.ShouldBeTrue();
            Part<Border>(palette, "PART_CommandKey").IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    // ---- motion -----------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Full_Motion_Enters_Glides_Filtered_Rows_And_Settles()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var palette = new CommandPalette { ItemsSource = Sample() };
        var window = Show(palette, level: ArcMotionLevel.Full);
        try
        {
            // The entrance starts from opacity 0 and settles.
            clock.Advance(TimeSpan.FromSeconds(1));
            Part<Border>(palette, CommandPalette.SurfacePart).Opacity.ShouldBe(1, 0.001);

            palette.FocusInput();
            Dispatcher.UIThread.RunJobs();
            var list = List(palette);
            var invite = list.LiveEntries.First(e => e.Row?.Item?.Id == "invite");
            var before = invite.Y;
            Type(window, "in");
            // "Invite teammate" moved up: it starts where it was drawn and springs into its new slot.
            invite.Y.ShouldBeLessThan(before);
            invite.Translate.Y.ShouldBe(before - invite.Y, 0.5);
            list.ExitingCount.ShouldBeGreaterThan(0);
            clock.Advance(TimeSpan.FromMilliseconds(50));
            invite.Translate.Y.ShouldBeGreaterThan(0);
            invite.Translate.Y.ShouldBeLessThan(before - invite.Y);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            invite.Translate.Y.ShouldBe(0, 0.01);
            list.ExitingCount.ShouldBe(0);

            // Arrow keys slide the highlight; the pointer would spring it.
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.ArrowDown);
            clock.Advance(TimeSpan.FromMilliseconds(200));
            Highlight(palette).CurrentTop.ShouldBe(list.FindRow(palette.ActiveItem!)!.Y, 0.01);

            // Results give way to the empty state, then come back.
            Type(window, "zzz");
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromSeconds(1));
            Part<Control>(palette, CommandPalette.EmptyPart).IsVisible.ShouldBeTrue();
            Part<Control>(palette, CommandPalette.EmptyPart).Opacity.ShouldBe(1, 0.001);
            palette.Clear();
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromSeconds(1));
            list.IsVisible.ShouldBeTrue();
            list.Opacity.ShouldBe(1, 0.001);
            palette.VisibleItems.Count.ShouldBe(6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Pumps_Real_Frames_Without_Throwing()
    {
        var palette = new CommandPalette { ItemsSource = Sample(), ShowCloseButton = true };
        var window = Show(palette, level: ArcMotionLevel.Full);
        try
        {
            palette.FocusInput();
            for (var i = 0; i < 6; i++)
            {
                Type(window, i % 2 == 0 ? "e" : "t");
                Press(window, PhysicalKey.ArrowDown);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
            }
            Press(window, PhysicalKey.Escape);
            for (var i = 0; i < 10; i++)
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
            }
            palette.Query.ShouldBe(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Keeps_Only_The_Fades()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var palette = new CommandPalette { ItemsSource = Sample() };
        var window = Show(palette, level: ArcMotionLevel.Reduced);
        try
        {
            // The entrance only fades: no rise, no scale.
            var surface = Part<Border>(palette, CommandPalette.SurfacePart);
            var transforms = ((Avalonia.Media.TransformGroup)surface.RenderTransform!).Children;
            ((Avalonia.Media.TranslateTransform)transforms[1]).Y.ShouldBe(0);
            ((Avalonia.Media.ScaleTransform)transforms[0]).ScaleX.ShouldBe(1);
            clock.Advance(TimeSpan.FromSeconds(1));
            surface.Opacity.ShouldBe(1, 0.001);

            palette.FocusInput();
            Dispatcher.UIThread.RunJobs();
            Type(window, "in");
            // Rows snap into place and leaving rows go at once; new rows may still fade.
            var list = List(palette);
            list.ExitingCount.ShouldBe(0);
            list.LiveEntries.ShouldAllBe(e => Math.Abs(e.Translate.Y) < 0.001);

            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.ArrowDown);
            Highlight(palette).CurrentTop.ShouldBe(list.FindRow(palette.ActiveItem!)!.Y, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Gallery_Has_The_Page_And_Its_Demos()
    {
        var block = BlockCatalog.Blocks.Single(b => b.ClassName == "CommandPalette");
        GalleryDocs.Has("CommandPalette").ShouldBeTrue();
        GalleryDocs.HasDemos("CommandPalette").ShouldBeTrue();
        GalleryDocs.PackageOf("CommandPalette").ShouldBe("TKCode.Arc.Blocks");
        GalleryDocs.Summary("CommandPalette", "en-US").ShouldBe(block.Description);
        GalleryDocs.Get("CommandPalette", "en-US")!.Title.ShouldBe(block.Title);
    }

    private sealed class RecordingCommand(List<object?> calls) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => calls.Add(parameter);
    }
}
