using System.Collections.ObjectModel;
using System.Windows.Input;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;
using GalleryDocs = TKCode.Arc.Gallery.Docs.GalleryDocs;

namespace TKCode.Arc.Tests.Blocks;

public class CommandPaletteTests
{
    internal static List<CommandPaletteItem> Sample() =>
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
        List(palette).LiveEntries.Where(e => !e.IsHeading && e.Row is not null).Select(e => e.Row!);

    private static string[] Headings(CommandPalette palette) =>
        List(palette).LiveEntries.Where(e => e.IsHeading && e.Visual is not null).Select(e => ((CommandPaletteGroupHeading)e.Visual!).GroupName).ToArray();

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

    // ---- long lists (virtual rows) ------------------------------------------------------------------------------------

    private static List<CommandPaletteItem> Many(int count) =>
        Enumerable.Range(0, count).Select(i => new CommandPaletteItem
        {
            Id = $"cmd-{i}",
            Label = $"Command {i}",
            Description = i % 3 == 0 ? $"Does thing {i}" : null,
            Group = $"Group {i / 50}",
            Shortcut = i % 7 == 0 ? "G X" : null
        }).ToList();

    [AvaloniaFact]
    public void A_Long_List_Builds_Visuals_Only_Near_The_Viewport()
    {
        var palette = new CommandPalette { ItemsSource = Many(1000) };
        var window = Show(palette);
        try
        {
            var list = List(palette);
            list.RowCount.ShouldBe(1000);
            palette.VisibleItems.Count.ShouldBe(1000);
            // The viewport shows about ten rows; a short margin more is realized, nothing like 1000.
            list.RealizedCount.ShouldBeLessThan(30);
            list.RealizedCount.ShouldBeGreaterThan(8);
            // The scroll extent still covers every row.
            Part<ScrollViewer>(palette, CommandPalette.ScrollPart).Extent.Height.ShouldBeGreaterThan(1000 * 30);
            Rows(palette).First().Item!.Id.ShouldBe("cmd-0");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Scrolling_Realizes_The_Rows_That_Come_Into_View_And_Recycles_The_Others()
    {
        var palette = new CommandPalette { ItemsSource = Many(1000) };
        var window = Show(palette);
        try
        {
            var list = List(palette);
            var scroll = Part<ScrollViewer>(palette, CommandPalette.ScrollPart);
            var first = Rows(palette).First();
            scroll.Offset = new Vector(0, 20000);
            Dispatcher.UIThread.RunJobs();
            list.RealizedCount.ShouldBeLessThan(30);
            var rows = Rows(palette).ToList();
            rows.ShouldNotBeEmpty();
            rows.Select(r => r.Item!.Id).ShouldNotContain("cmd-0");
            // Rows are in order and sit where the layout puts them, in the viewport.
            var ids = rows.Select(r => int.Parse(r.Item!.Id.Substring(4))).ToList();
            ids.ShouldBe(ids.OrderBy(i => i).ToList());
            var visible = rows.Where(r => r.TranslatePoint(default, scroll) is { } p && p.Y >= -1 && p.Y < scroll.Viewport.Height).ToList();
            visible.ShouldNotBeEmpty();
            // The visuals were recycled, not piled up: the first row's visual now shows another command.
            first.Item!.Id.ShouldNotBe("cmd-0");

            scroll.Offset = new Vector(0, 0);
            Dispatcher.UIThread.RunJobs();
            Rows(palette).First().Item!.Id.ShouldBe("cmd-0");
            list.RealizedCount.ShouldBeLessThan(30);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void End_And_Home_Move_The_Highlight_To_Rows_That_Were_Not_Built_Yet()
    {
        var items = Many(1000);
        var palette = new CommandPalette { ItemsSource = items };
        var window = Show(palette);
        try
        {
            palette.FocusInput();
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.End);
            palette.ActiveItem.ShouldBe(items[^1]);
            // The row is realized once the list scrolled to it, and it draws as the active one.
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();
            var list = List(palette);
            var entry = list.FindRow(items[^1])!;
            entry.Row.ShouldNotBeNull();
            entry.Row!.IsActive.ShouldBeTrue();
            Highlight(palette).TargetTop.ShouldBe(entry.Y, 0.01);
            list.RealizedCount.ShouldBeLessThan(30);
            Rows(palette).Count(r => r.IsActive).ShouldBe(1);

            Press(window, PhysicalKey.Home);
            Dispatcher.UIThread.RunJobs();
            palette.ActiveItem.ShouldBe(items[0]);
            list.FindRow(items[0])!.Row.ShouldNotBeNull();
            Rows(palette).Count(r => r.IsActive).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Filtering_A_Long_List_Keeps_The_Realized_Rows_Few_And_Correct()
    {
        var palette = new CommandPalette { ItemsSource = Many(1000) };
        var window = Show(palette);
        try
        {
            palette.FocusInput();
            Dispatcher.UIThread.RunJobs();
            Type(window, "command 9");
            // "Command 9", "Command 90".."Command 99", "Command 900".."Command 999": 111 rows.
            palette.VisibleItems.Count.ShouldBe(111);
            var list = List(palette);
            list.RowCount.ShouldBe(111);
            list.RealizedCount.ShouldBeLessThan(30);
            Rows(palette).First().Item!.Id.ShouldBe("cmd-9");
            Headings(palette).First().ShouldBe("Group 0");

            Type(window, "99");
            palette.VisibleItems.Select(i => i.Id).ShouldBe(["cmd-999"]);
            Rows(palette).Single().Item!.Id.ShouldBe("cmd-999");
            Headings(palette).ShouldBe(["Group 19"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rows_Report_Their_Place_In_The_Set_And_Clicking_A_Scrolled_Row_Chooses_It()
    {
        var items = Many(300);
        var chosen = new List<string>();
        var palette = new CommandPalette { ItemsSource = items };
        palette.ItemSelected += (_, e) => chosen.Add(e.Item.Id);
        var window = Show(palette);
        try
        {
            var scroll = Part<ScrollViewer>(palette, CommandPalette.ScrollPart);
            scroll.Offset = new Vector(0, 5000);
            Dispatcher.UIThread.RunJobs();
            var row = Rows(palette).First(r => r.TranslatePoint(default, scroll) is { } p && p.Y >= 0 && p.Y < scroll.Viewport.Height - 40);
            var index = items.IndexOf(row.Item!);
            AutomationProperties.GetPositionInSet(row).ShouldBe(index + 1);
            AutomationProperties.GetSizeOfSet(row).ShouldBe(300);
            var point = row.TranslatePoint(new Point(40, row.Bounds.Height / 2), window)!.Value;
            window.MouseMove(point);
            Dispatcher.UIThread.RunJobs();
            palette.ActiveItem.ShouldBe(row.Item);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            chosen.ShouldBe([row.Item!.Id]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Replacing_The_Source_Reuses_The_Rows_By_Id()
    {
        var palette = new CommandPalette { ItemsSource = Many(500) };
        var window = Show(palette);
        try
        {
            var before = Rows(palette).ToList();
            var replacement = Many(500);
            replacement[1].Label = "Renamed";
            palette.ItemsSource = replacement;
            Dispatcher.UIThread.RunJobs();
            var after = Rows(palette).ToList();
            after.Count.ShouldBe(before.Count);
            after[1].Title.ShouldBe("Renamed");
            after[1].Item.ShouldBe(replacement[1]);
            List(palette).RealizedCount.ShouldBeLessThan(30);
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

    // Arc's narrow rules are viewport media queries: the window width decides, not the palette's own width.
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

            window.Width = 420;
            Dispatcher.UIThread.RunJobs();
            palette.Classes.ShouldContain(CommandPalette.NarrowPseudoClass);
            palette.Classes.ShouldNotContain(CommandPalette.NarrowestPseudoClass);
            cap.IsEffectivelyVisible.ShouldBeFalse();
            Part<Border>(palette, "PART_CommandKey").IsVisible.ShouldBeFalse();
            Part<Border>(palette, CommandPalette.SearchRowPart).Padding.ShouldBe(new Thickness(11, 8));
            Part<Border>(palette, CommandPalette.ResultsPart).Padding.ShouldBe(new Thickness(6));
            row.Padding.ShouldBe(new Thickness(8, 0));

            window.Width = 320;
            Dispatcher.UIThread.RunJobs();
            palette.Classes.ShouldContain(CommandPalette.NarrowestPseudoClass);
            row.Padding.ShouldBe(new Thickness(6, 0));

            window.Width = 800;
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
            invite.TranslateY.ShouldBe(before - invite.Y, 0.5);
            list.ExitingCount.ShouldBeGreaterThan(0);
            clock.Advance(TimeSpan.FromMilliseconds(50));
            invite.TranslateY.ShouldBeGreaterThan(0);
            invite.TranslateY.ShouldBeLessThan(before - invite.Y);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            invite.TranslateY.ShouldBe(0, 0.01);
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
            list.LiveEntries.ShouldAllBe(e => Math.Abs(e.TranslateY) < 0.001);

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

public class CommandPaletteDialogTests
{
    private static (Window Window, CommandPaletteDialog Dialog, AvaloniaButton Opener, TextBox Field) Setup(
        Action<CommandPaletteDialog>? configure = null, ArcMotionLevel level = ArcMotionLevel.Off, double width = 900, double height = 700)
    {
        var opener = new AvaloniaButton { Content = "Open" };
        var field = new TextBox { Text = "page text" };
        var dialog = new CommandPaletteDialog { ItemsSource = CommandPaletteTests.Sample() };
        configure?.Invoke(dialog);
        var window = TestHelpers.Show(new StackPanel { Children = { opener, field, dialog } }, level);
        window.Width = width;
        window.Height = height;
        Dispatcher.UIThread.RunJobs();
        opener.Focus();
        Dispatcher.UIThread.RunJobs();
        return (window, dialog, opener, field);
    }

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

    private static TextBox Input(CommandPaletteDialog dialog) => TestHelpers.Descendant<TextBox>(dialog.Palette!, CommandPalette.InputPart);

    private static IInputElement? Focused(Window window) => TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement();

    private static bool IsOnLayer(CommandPaletteDialog dialog) => dialog.Presence!.Host.GetVisualParent() is not null;

    [AvaloniaFact]
    public void A_Closed_Dialog_Takes_No_Space_And_Builds_Nothing_Until_It_Opens()
    {
        var (window, dialog, _, _) = Setup();
        try
        {
            dialog.IsVisible.ShouldBeFalse();
            dialog.IsOpen.ShouldBeFalse();
            dialog.Palette.ShouldBeNull();
            dialog.Presence.ShouldBeNull();
            dialog.ShowCloseButton.ShouldBeTrue();
            dialog.IsShortcutEnabled.ShouldBeTrue();
            dialog.CloseOnSelect.ShouldBeTrue();
            dialog.CloseOnEscape.ShouldBeTrue();
            dialog.CloseOnOutsidePress.ShouldBeTrue();
            dialog.IsModal.ShouldBeTrue();
            dialog.PanelWidth.ShouldBe(560);
            dialog.PanelTopRatio.ShouldBe(0.13, 0.0001);
            dialog.BackdropBlur.ShouldBe(0);
            // The theme gives the scrim a light dim.
            dialog.ScrimBrush.ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opening_Mounts_The_Palette_Near_The_Top_Centre_And_Focuses_The_Field()
    {
        var (window, dialog, opener, _) = Setup();
        try
        {
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var palette = dialog.Palette.ShouldNotBeNull();
            dialog.Presence!.State.ShouldBe(ModalState.Open);
            IsOnLayer(dialog).ShouldBeTrue();
            palette.IsEffectivelyVisible.ShouldBeTrue();
            palette.ShowCloseButton.ShouldBeTrue();
            // 560 wide, centred, 13% of the 700px window down.
            palette.Bounds.Width.ShouldBe(560, 0.5);
            palette.TranslatePoint(default, window)!.Value.X.ShouldBe((window.ClientSize.Width - 560) / 2, 1);
            palette.TranslatePoint(default, window)!.Value.Y.ShouldBe(window.ClientSize.Height * 0.13, 1.5);
            Focused(window).ShouldBe(Input(dialog));
            dialog.Presence.Host.Scrim.IsVisible.ShouldBeTrue();

            dialog.IsOpen = false;
            Dispatcher.UIThread.RunJobs();
            dialog.Presence.State.ShouldBe(ModalState.Closed);
            IsOnLayer(dialog).ShouldBeFalse();
            opener.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Command_K_Toggles_From_Anywhere_And_Focus_Returns_To_A_Text_Box()
    {
        var (window, dialog, _, field) = Setup();
        try
        {
            field.Focus();
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.K, RawInputModifiers.Control);
            dialog.IsOpen.ShouldBeTrue();
            Focused(window).ShouldBe(Input(dialog));
            // The same gesture inside the palette closes it again.
            Press(window, PhysicalKey.K, RawInputModifiers.Control);
            dialog.IsOpen.ShouldBeFalse();
            field.IsFocused.ShouldBeTrue();

            Press(window, PhysicalKey.K, RawInputModifiers.Meta);
            dialog.IsOpen.ShouldBeTrue();
            Press(window, PhysicalKey.K, RawInputModifiers.Meta);
            dialog.IsOpen.ShouldBeFalse();
            dialog.LastCloseReason.ShouldBe(DismissReason.Programmatic);
            // Other modifiers do not match.
            Press(window, PhysicalKey.K, RawInputModifiers.Control | RawInputModifiers.Shift);
            dialog.IsOpen.ShouldBeFalse();
            Press(window, PhysicalKey.K);
            dialog.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Custom_Gesture_Replaces_Command_K_And_Shows_On_The_Key_Cap()
    {
        var (window, dialog, _, _) = Setup(d => d.OpenGesture = new KeyGesture(Key.P, KeyModifiers.Control | KeyModifiers.Shift));
        try
        {
            Press(window, PhysicalKey.K, RawInputModifiers.Control);
            dialog.IsOpen.ShouldBeFalse();
            Press(window, PhysicalKey.P, RawInputModifiers.Control | RawInputModifiers.Shift);
            dialog.IsOpen.ShouldBeTrue();
            var expected = OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() ? "⌃ ⇧ P" : "Ctrl Shift P";
            dialog.Palette!.EffectiveShortcutHint.ShouldBe(expected);
            TestHelpers.Descendant<Border>(dialog.Palette, "PART_CommandKey").IsEffectivelyVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Gesture_Is_Ignored_When_Disabled_Hidden_Detached_Or_Under_Another_Modal()
    {
        var (window, dialog, _, _) = Setup();
        try
        {
            dialog.IsShortcutEnabled = false;
            Press(window, PhysicalKey.K, RawInputModifiers.Control);
            dialog.IsOpen.ShouldBeFalse();
            dialog.IsShortcutEnabled = true;

            // A hidden page does not answer.
            var parent = (Panel)dialog.GetVisualParent()!;
            parent.IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.K, RawInputModifiers.Control);
            dialog.IsOpen.ShouldBeFalse();
            parent.IsVisible = true;
            Dispatcher.UIThread.RunJobs();

            // A disabled host does not answer.
            dialog.IsEnabled = false;
            Press(window, PhysicalKey.K, RawInputModifiers.Control);
            dialog.IsOpen.ShouldBeFalse();
            dialog.IsEnabled = true;

            // Another modal on top owns the window.
            var other = new Dialog { Title = "Rename", Content = new TextBox() };
            parent.Children.Add(other);
            Dispatcher.UIThread.RunJobs();
            other.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.K, RawInputModifiers.Control);
            dialog.IsOpen.ShouldBeFalse();
            other.IsOpen = false;
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.K, RawInputModifiers.Control);
            dialog.IsOpen.ShouldBeTrue();
            Press(window, PhysicalKey.K, RawInputModifiers.Control);

            // Detached, it no longer listens.
            parent.Children.Remove(dialog);
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.K, RawInputModifiers.Control);
            dialog.IsOpen.ShouldBeFalse();
            ModalPresence.GetModals(window).Count.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Open_Non_Modal_Dialog_Does_Not_Block_The_Gesture_Of_Another_Dialog()
    {
        var (window, dialog, _, _) = Setup();
        try
        {
            // A popup palette that is always open (a docs preview) is not a modal: the other host still answers.
            var popup = new CommandPaletteDialog
            {
                ItemsSource = CommandPaletteTests.Sample(),
                IsModal = false,
                IsOpen = true,
                OpenGesture = new KeyGesture(Key.P, KeyModifiers.Control | KeyModifiers.Shift)
            };
            ((Panel)dialog.GetVisualParent()!).Children.Add(popup);
            Dispatcher.UIThread.RunJobs();
            popup.IsOpen.ShouldBeTrue();

            Press(window, PhysicalKey.K, RawInputModifiers.Control);
            dialog.IsOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Clears_The_Query_First_And_Closes_When_It_Is_Empty()
    {
        var (window, dialog, opener, _) = Setup();
        try
        {
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            Type(window, "inv");
            dialog.Palette!.Query.ShouldBe("inv");
            Press(window, PhysicalKey.Escape);
            dialog.IsOpen.ShouldBeTrue();
            dialog.Palette.Query.ShouldBe(string.Empty);
            Press(window, PhysicalKey.Escape);
            dialog.IsOpen.ShouldBeFalse();
            dialog.LastCloseReason.ShouldBe(DismissReason.Escape);
            opener.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Can_Be_Turned_Off_Or_Cancelled()
    {
        var (window, dialog, _, _) = Setup(d => d.CloseOnEscape = false);
        try
        {
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.Escape);
            dialog.IsOpen.ShouldBeTrue();
            dialog.CloseOnEscape = true;
            var cancel = true;
            OverlayClosingEventArgs? closing = null;
            dialog.Closing += (_, e) =>
            {
                closing = e;
                e.Cancel = cancel;
            };
            Press(window, PhysicalKey.Escape);
            closing.ShouldNotBeNull();
            closing!.Reason.ShouldBe(DismissReason.Escape);
            dialog.IsOpen.ShouldBeTrue();
            cancel = false;
            Press(window, PhysicalKey.Escape);
            dialog.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Esc_Key_Cap_Closes_Even_When_Escape_Is_Off()
    {
        var (window, dialog, opener, _) = Setup(d => d.CloseOnEscape = false);
        try
        {
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var cap = TestHelpers.Descendant<AvaloniaButton>(dialog.Palette!, CommandPalette.CloseButtonPart);
            cap.IsEffectivelyVisible.ShouldBeTrue();
            Avalonia.Automation.AutomationProperties.GetName(cap).ShouldBe("Close command palette");
            cap.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent, cap));
            Dispatcher.UIThread.RunJobs();
            dialog.IsOpen.ShouldBeFalse();
            dialog.LastCloseReason.ShouldBe(DismissReason.CloseButton);
            opener.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Press_On_The_Scrim_Closes_Unless_Turned_Off()
    {
        var (window, dialog, _, _) = Setup(d => d.CloseOnOutsidePress = false);
        try
        {
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var outside = new Point(20, window.ClientSize.Height - 20);
            window.MouseDown(outside, MouseButton.Left);
            window.MouseUp(outside, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            dialog.IsOpen.ShouldBeTrue();

            // A press inside the palette does not close it.
            var inside = dialog.Palette!.TranslatePoint(new Point(40, 20), window)!.Value;
            window.MouseDown(inside, MouseButton.Left);
            window.MouseUp(inside, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            dialog.IsOpen.ShouldBeTrue();

            dialog.CloseOnOutsidePress = true;
            window.MouseDown(outside, MouseButton.Left);
            window.MouseUp(outside, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            dialog.IsOpen.ShouldBeFalse();
            dialog.LastCloseReason.ShouldBe(DismissReason.PressOutside);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Choosing_A_Command_Runs_It_Then_Closes_And_Returns_The_Focus()
    {
        var calls = new List<object?>();
        var (window, dialog, opener, _) = Setup(d => d.SelectCommand = new RecordingCommand(calls));
        try
        {
            var raised = new List<string>();
            dialog.ItemSelected += (_, e) => raised.Add($"{e.Item.Id}:{dialog.IsOpen}");
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            Type(window, "invite");
            Press(window, PhysicalKey.Enter);
            // The dialog closed first (the focus is back on the page by now); the handler and the commands ran after it.
            raised.ShouldBe(["invite:False"]);
            calls.Count.ShouldBe(1);
            dialog.IsOpen.ShouldBeFalse();
            dialog.LastCloseReason.ShouldBe(DismissReason.Action);
            opener.IsFocused.ShouldBeTrue();

            // Opening again starts from a clean field.
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            dialog.Palette!.Query.ShouldBe(string.Empty);
            dialog.Palette.VisibleItems.Count.ShouldBe(6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Choosing_Can_Keep_The_Dialog_Open_And_Bubbles_To_The_Page()
    {
        var (window, dialog, _, _) = Setup(d => d.CloseOnSelect = false);
        try
        {
            CommandPaletteItem? seen = null;
            ((Panel)dialog.GetVisualParent()!).AddHandler(CommandPaletteDialog.ItemSelectedEvent, (object? _, CommandPaletteItemSelectedEventArgs e) => seen = e.Item);
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.Enter);
            seen.ShouldNotBeNull();
            seen!.Id.ShouldBe("dashboard");
            dialog.IsOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Button_Opens_It_Through_The_Attached_Property_And_The_Commands()
    {
        var (window, dialog, opener, _) = Setup();
        try
        {
            CommandPaletteDialog.SetOpens(opener, dialog);
            CommandPaletteDialog.GetOpens(opener).ShouldBe(dialog);
            opener.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent, opener));
            Dispatcher.UIThread.RunJobs();
            dialog.IsOpen.ShouldBeTrue();
            dialog.CloseCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            dialog.IsOpen.ShouldBeFalse();
            dialog.OpenCommand.Execute(null);
            dialog.IsOpen.ShouldBeTrue();
            dialog.ToggleCommand.Execute(null);
            dialog.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Open_Close_Open_In_A_Row_Leaves_One_Palette_And_No_Stuck_Scrim()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (window, dialog, opener, _) = Setup(level: ArcMotionLevel.Full);
        try
        {
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(40));
            dialog.IsOpen = false;
            clock.Advance(TimeSpan.FromMilliseconds(30));
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            dialog.Presence!.State.ShouldBe(ModalState.Open);
            dialog.Presence.Host.Scrim.Opacity.ShouldBe(1, 0.001);
            dialog.Presence.Host.PanelHost.Opacity.ShouldBe(1, 0.001);
            ModalPresence.GetModals(window).Count.ShouldBe(1);
            Focused(window).ShouldBe(Input(dialog));

            // Closing for real removes the layer and gives the focus back.
            dialog.IsOpen = false;
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            dialog.Presence.State.ShouldBe(ModalState.Closed);
            ModalPresence.GetModals(window).Count.ShouldBe(0);
            IsOnLayer(dialog).ShouldBeFalse();
            opener.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Rises_The_Palette_And_Fades_The_Scrim_And_The_Panel_Once()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (window, dialog, _, _) = Setup(level: ArcMotionLevel.Full);
        try
        {
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var surface = TestHelpers.Descendant<Border>(dialog.Palette!, CommandPalette.SurfacePart);
            // The first frame is the starting pose: invisible through the presence's fade, so nothing flashes.
            dialog.Presence!.Host.PanelHost.Opacity.ShouldBe(0, 0.01);
            dialog.Presence.Host.Scrim.Opacity.ShouldBe(0, 0.01);
            clock.Advance(TimeSpan.FromMilliseconds(60));
            Dispatcher.UIThread.RunJobs();
            // The palette's own opacity stays 1 (the presence fades the whole panel): one fade, not two.
            surface.Opacity.ShouldBe(1, 0.001);
            dialog.Presence.Host.PanelHost.Opacity.ShouldBeGreaterThan(0);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            dialog.Presence.Host.PanelHost.Opacity.ShouldBe(1, 0.001);
            var transforms = ((Avalonia.Media.TransformGroup)surface.RenderTransform!).Children;
            ((Avalonia.Media.TranslateTransform)transforms[1]).Y.ShouldBe(0, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_And_Off_Motion_Do_Not_Move_The_Palette()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (window, dialog, _, _) = Setup(level: ArcMotionLevel.Reduced);
        try
        {
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var surface = TestHelpers.Descendant<Border>(dialog.Palette!, CommandPalette.SurfacePart);
            var transforms = ((Avalonia.Media.TransformGroup)surface.RenderTransform!).Children;
            ((Avalonia.Media.TranslateTransform)transforms[1]).Y.ShouldBe(0);
            ((Avalonia.Media.ScaleTransform)transforms[0]).ScaleX.ShouldBe(1);
            clock.Advance(TimeSpan.FromSeconds(1));
            dialog.Presence!.Host.PanelHost.Opacity.ShouldBe(1, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Dialog_Removed_While_Open_Leaves_Nothing_Behind()
    {
        var (window, dialog, opener, _) = Setup();
        try
        {
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var closed = 0;
            dialog.Closed += (_, _) => closed++;
            ((Panel)dialog.GetVisualParent()!).Children.Remove(dialog);
            Dispatcher.UIThread.RunJobs();
            dialog.IsOpen.ShouldBeFalse();
            closed.ShouldBe(1);
            ModalPresence.GetModals(window).Count.ShouldBe(0);
            // Nothing listens any more.
            Press(window, PhysicalKey.K, RawInputModifiers.Control);
            dialog.IsOpen.ShouldBeFalse();
            ModalPresence.GetModals(window).Count.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Dialog_Opened_Before_It_Is_In_A_Window_Opens_When_Attached()
    {
        var dialog = new CommandPaletteDialog { ItemsSource = CommandPaletteTests.Sample(), IsOpen = true };
        dialog.Palette.ShouldNotBeNull();
        var host = new StackPanel();
        var window = TestHelpers.Show(host);
        try
        {
            ModalPresence.GetModals(window).Count.ShouldBe(0);
            host.Children.Add(dialog);
            Dispatcher.UIThread.RunJobs();
            dialog.Presence!.State.ShouldBe(ModalState.Open);
            ModalPresence.GetModals(window).Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Palette_Fits_A_Narrow_And_A_Short_Window()
    {
        var (window, dialog, _, _) = Setup(width: 400, height: 360);
        try
        {
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var palette = dialog.Palette!;
            // 16px from each side, narrow layout: no shortcut caps.
            palette.Bounds.Width.ShouldBe(368, 0.5);
            palette.TranslatePoint(default, window)!.Value.X.ShouldBe(16, 0.5);
            palette.Classes.ShouldContain(CommandPalette.NarrowPseudoClass);
            TestHelpers.Descendant<Border>(palette, "PART_CommandKey").IsEffectivelyVisible.ShouldBeFalse();
            // It never reaches below the window, however tall the list is.
            var bottom = palette.TranslatePoint(new Point(0, palette.Bounds.Height), window)!.Value.Y;
            // (layout rounding may add a pixel)
            bottom.ShouldBeLessThanOrEqualTo(window.ClientSize.Height - 16 + 1);

            window.Width = 900;
            window.Height = 700;
            Dispatcher.UIThread.RunJobs();
            palette.Bounds.Width.ShouldBe(560, 0.5);
            palette.Classes.ShouldNotContain(CommandPalette.NarrowPseudoClass);
            TestHelpers.Descendant<Border>(palette, "PART_CommandKey").IsEffectivelyVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Palette_Keeps_Its_Top_Edge_While_The_Results_Change()
    {
        var (window, dialog, _, _) = Setup();
        try
        {
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var palette = dialog.Palette!;
            var top = palette.TranslatePoint(default, window)!.Value.Y;
            var height = palette.Bounds.Height;
            Type(window, "inv");
            palette.TranslatePoint(default, window)!.Value.Y.ShouldBe(top, 0.01);
            palette.Bounds.Height.ShouldBeLessThan(height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Texts_Items_And_Hint_Flow_To_The_Palette()
    {
        var (window, dialog, _, _) = Setup(d =>
        {
            d.PlaceholderText = "Jump to a page";
            d.Label = "Quick switcher";
            d.EmptyTitle = "Nothing here";
            d.CloseButtonLabel = "Close the switcher";
        });
        try
        {
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var palette = dialog.Palette!;
            palette.PlaceholderText.ShouldBe("Jump to a page");
            palette.Label.ShouldBe("Quick switcher");
            palette.EmptyTitle.ShouldBe("Nothing here");
            palette.CloseButtonLabel.ShouldBe("Close the switcher");
            dialog.Label = "Switcher";
            palette.Label.ShouldBe("Switcher");
            // The dialog is named by Label and reads as a window to assistive technology.
            var frame = (Control)palette.Parent!;
            Avalonia.Automation.AutomationProperties.GetName(frame).ShouldBe("Switcher");
            Avalonia.Automation.AutomationProperties.GetControlTypeOverride(frame).ShouldBe(Avalonia.Automation.Peers.AutomationControlType.Window);

            // The list follows the source, open or not.
            var source = new ObservableCollection<CommandPaletteItem> { new() { Id = "a", Label = "Alpha" } };
            dialog.ItemsSource = source;
            palette.VisibleItems.Select(i => i.Id).ShouldBe(["a"]);
            source.Add(new CommandPaletteItem { Id = "b", Label = "Beta" });
            palette.VisibleItems.Select(i => i.Id).ShouldBe(["a", "b"]);

            // The shortcut cap hides when there is no shortcut to show.
            dialog.IsShortcutEnabled = false;
            palette.Classes.ShouldContain(CommandPalette.NoHintPseudoClass);
            dialog.ShortcutHint = "⌘ J";
            palette.Classes.ShouldNotContain(CommandPalette.NoHintPseudoClass);
            palette.EffectiveShortcutHint.ShouldBe("⌘ J");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Field_Is_The_First_Tab_Stop_Then_The_Clear_And_Esc_Buttons()
    {
        var (window, dialog, _, _) = Setup();
        try
        {
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            FocusManager.FindFirstFocusableElement(dialog.Palette!).ShouldBe(Input(dialog));
            Type(window, "x");
            Press(window, PhysicalKey.Tab);
            Focused(window).ShouldBeAssignableTo<AvaloniaButton>()!.Name.ShouldBe(CommandPalette.ClearButtonPart);
            Press(window, PhysicalKey.Tab);
            Focused(window).ShouldBeAssignableTo<AvaloniaButton>()!.Name.ShouldBe(CommandPalette.CloseButtonPart);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Inline_Palette_Stands_Down_While_A_Modal_Is_On_Top()
    {
        // With the dialog's own gesture off, only the inline palette listens: while the dialog is open, the page behind
        // it does not take the focus.
        var inline = new CommandPalette { ItemsSource = CommandPaletteTests.Sample() };
        var host = new StackPanel { Children = { inline } };
        var window = TestHelpers.Show(host);
        try
        {
            var dialog = new CommandPaletteDialog { ItemsSource = CommandPaletteTests.Sample(), IsShortcutEnabled = false };
            host.Children.Add(dialog);
            Dispatcher.UIThread.RunJobs();
            var field = TestHelpers.Descendant<TextBox>(inline, CommandPalette.InputPart);
            dialog.Open();
            Dispatcher.UIThread.RunJobs();
            Focused(window).ShouldBe(Input(dialog));
            Press(window, PhysicalKey.K, RawInputModifiers.Control);
            field.IsFocused.ShouldBeFalse();
            Focused(window).ShouldBe(Input(dialog));
            dialog.IsOpen.ShouldBeTrue();

            // Closed again, the inline palette answers as before.
            dialog.Close();
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.K, RawInputModifiers.Control);
            field.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Non_Modal_Dialog_Keeps_The_Page_Alive_And_Closes_On_A_Press_Outside()
    {
        var (window, dialog, _, field) = Setup(d => d.IsModal = false);
        try
        {
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            dialog.Presence!.Host.Scrim.IsVisible.ShouldBeFalse();
            // Focus is free: the page's own field can take it.
            field.Focus();
            Dispatcher.UIThread.RunJobs();
            field.IsFocused.ShouldBeTrue();
            dialog.IsOpen.ShouldBeTrue();

            var inside = dialog.Palette!.TranslatePoint(new Point(40, 20), window)!.Value;
            window.MouseDown(inside, MouseButton.Left);
            window.MouseUp(inside, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            dialog.IsOpen.ShouldBeTrue();

            var outside = new Point(20, window.ClientSize.Height - 20);
            window.MouseDown(outside, MouseButton.Left);
            window.MouseUp(outside, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            dialog.IsOpen.ShouldBeFalse();
            dialog.LastCloseReason.ShouldBe(DismissReason.PressOutside);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opened_And_Closed_Events_Fire_Once_Per_Open()
    {
        var (window, dialog, _, _) = Setup();
        try
        {
            var log = new List<string>();
            dialog.Opened += (_, _) => log.Add("opened");
            dialog.Closed += (_, e) => log.Add($"closed:{e.Reason}");
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            dialog.IsOpen = false;
            Dispatcher.UIThread.RunJobs();
            dialog.Open();
            Press(window, PhysicalKey.Escape);
            log.ShouldBe(["opened", "closed:Programmatic", "opened", "closed:Escape"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Closed_Dialog_Does_No_Work_And_Catches_Up_When_It_Opens()
    {
        var source = new ObservableCollection<CommandPaletteItem>(CommandPaletteTests.Sample());
        var (window, dialog, _, _) = Setup(d => d.ItemsSource = source);
        try
        {
            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var palette = dialog.Palette!;
            palette.VisibleItems.Count.ShouldBe(6);
            dialog.IsOpen = false;
            Dispatcher.UIThread.RunJobs();
            TopLevel.GetTopLevel(palette).ShouldBeNull();

            // Detached, the palette neither listens to the source nor watches its commands.
            source.Add(new CommandPaletteItem { Id = "extra", Label = "Extra" });
            source[0].Label = "Renamed";
            palette.VisibleItems.Count.ShouldBe(6);
            palette.VisibleItems[0].Label.ShouldBe("Renamed");

            dialog.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            palette.VisibleItems.Count.ShouldBe(7);
            List<string> titles = [];
            foreach (var row in palette.GetVisualDescendants().OfType<CommandPaletteRow>())
            {
                titles.Add(row.Title);
            }
            titles.ShouldContain("Renamed");
            titles.ShouldContain("Extra");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Two_Way_IsOpen_Follows_Every_Close()
    {
        var (window, dialog, _, _) = Setup();
        try
        {
            var model = new Model();
            dialog.Bind(CommandPaletteDialog.IsOpenProperty, new Avalonia.Data.Binding(nameof(Model.Open)) { Source = model, Mode = Avalonia.Data.BindingMode.TwoWay });
            model.Open = true;
            Dispatcher.UIThread.RunJobs();
            dialog.IsOpen.ShouldBeTrue();
            Press(window, PhysicalKey.Escape);
            model.Open.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class Model : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _open;

        public bool Open
        {
            get => _open;
            set
            {
                _open = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Open)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
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
