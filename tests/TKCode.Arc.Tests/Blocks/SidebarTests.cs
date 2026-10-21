using System.Windows.Input;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks;

public class SidebarTests
{
    private sealed class Sample
    {
        public Sidebar Sidebar { get; } = new();
        public SidebarItem Home { get; } = new() { Key = "home", Label = "Home", Icon = ArcIcons.House };
        public SidebarItem Inbox { get; } = new() { Key = "inbox", Label = "Inbox", Icon = ArcIcons.Inbox, Badge = "12" };
        public SidebarItem Issues { get; } = new() { Key = "issues", Label = "My issues", Icon = ArcIcons.CircleDot };
        public SidebarItem Projects { get; } = new() { Key = "projects", Label = "Projects", Icon = ArcIcons.Folder };
        public SidebarItem Atlas { get; } = new() { Key = "projects/atlas", Label = "Atlas" };
        public SidebarItem Beacon { get; } = new() { Key = "projects/beacon", Label = "Beacon", Badge = "3" };
        public SidebarItem Cobalt { get; } = new() { Key = "projects/cobalt", Label = "Cobalt" };
        public SidebarItem Views { get; } = new() { Key = "views", Label = "Views", Icon = ArcIcons.Layers };
        public SidebarItem Roadmap { get; } = new() { Key = "fav/roadmap", Label = "Q4 roadmap", Icon = ArcIcons.Star };
        public SidebarItem Archive { get; } = new() { Key = "fav/archive", Label = "Archive", Icon = ArcIcons.Archive, IsEnabled = false };
        public SidebarItem Reviews { get; } = new() { Key = "fav/reviews", Label = "Design reviews", Icon = ArcIcons.Bookmark };
        public SidebarSection Main { get; } = new();
        public SidebarSection Workspace { get; } = new() { Header = "Workspace" };
        public SidebarSection Favorites { get; } = new() { Header = "Favorites", IsCollapsible = true };

        public Sample()
        {
            Main.Items.AddRange([Home, Inbox, Issues]);
            Projects.Items.AddRange([Atlas, Beacon, Cobalt]);
            Workspace.Items.AddRange([Projects, Views]);
            Favorites.Items.AddRange([Roadmap, Archive, Reviews]);
            Sidebar.Sections.AddRange([Main, Workspace, Favorites]);
        }
    }

    private static Window Show(Sidebar sidebar, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(new DockPanel { Children = { sidebar } }, level);
        window.Width = 600;
        window.Height = 800;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static SidebarRow Row(Sidebar sidebar, SidebarItem item) => sidebar.RowOf(item).ShouldNotBeNull();

    private static void Pump(int frames = 1)
    {
        for (var i = 0; i < frames; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static IReadOnlyList<string> Navigable(Sidebar sidebar) => sidebar.NavigableNodes.Select(n => n.Label).ToList();

    private static Control? Focused(Window window) => window.FocusManager?.GetFocusedElement() as Control;

    private sealed class RecordingCommand : ICommand
    {
        public List<object?> Parameters { get; } = new();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => Parameters.Add(parameter);
    }

    [AvaloniaFact]
    public void Defaults_And_Template_Parts()
    {
        var s = new Sample();
        var window = Show(s.Sidebar);
        try
        {
            var sidebar = s.Sidebar;
            sidebar.Label.ShouldBe("Sidebar");
            sidebar.FilterPlaceholder.ShouldBe("Filter");
            sidebar.NoMatchesText.ShouldBe("No matches");
            sidebar.IsCollapsed.ShouldBeFalse();
            sidebar.IsFilterVisible.ShouldBeFalse();
            sidebar.IsCollapseButtonVisible.ShouldBeTrue();
            sidebar.SelectedItem.ShouldBeNull();
            sidebar.Bounds.Width.ShouldBe(260);
            AutomationProperties.GetLandmarkType(sidebar).ShouldBe(AutomationLandmarkType.Navigation);
            AutomationProperties.GetName(sidebar).ShouldBe("Sidebar");

            var inbox = Row(sidebar, s.Inbox);
            inbox.Bounds.Height.ShouldBe(34);
            inbox.Bounds.Width.ShouldBe(236);
            inbox.Classes.ShouldContain(":has-badge");
            inbox.Classes.ShouldContain(":has-icon");
            AutomationProperties.GetName(inbox).ShouldBe("Inbox, 12");
            Row(sidebar, s.Projects).Classes.ShouldContain(":parent");
            Row(sidebar, s.Atlas).Classes.ShouldContain(":nested");
            Row(sidebar, s.Archive).IsEnabled.ShouldBeFalse();

            sidebar.HeaderOf(s.Main).ShouldBeNull();
            sidebar.HeaderOf(s.Workspace).ShouldNotBeNull().Classes.ShouldContain(":titled");
            sidebar.HeaderOf(s.Favorites).ShouldNotBeNull().IsInteractive.ShouldBeTrue();
            sidebar.HeaderOf(s.Workspace)!.IsInteractive.ShouldBeFalse();

            // Nothing selected: no marker; Projects is closed, so its pages are not in the arrow-key order.
            sidebar.MarkerBlock.ShouldNotBeNull().HasTarget.ShouldBeFalse();
            Navigable(sidebar).ShouldBe(["Home", "Inbox", "My issues", "Projects", "Views", "Favorites", "Q4 roadmap", "Archive", "Design reviews"]);
            TestHelpers.Descendant<TextBox>(sidebar).IsEffectivelyVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectedKey_Set_Before_The_Pages_Selects_Them_And_Places_The_Marker()
    {
        var sidebar = new Sidebar { SelectedKey = "inbox" };
        var inboxItem = new SidebarItem { Key = "inbox", Label = "Inbox" };
        sidebar.SelectedItem.ShouldBeNull();
        sidebar.Sections.Add(new SidebarSection { Items = { new SidebarItem { Key = "home", Label = "Home" }, inboxItem } });
        var window = Show(sidebar);
        try
        {
            sidebar.SelectedItem.ShouldBe(inboxItem);
            sidebar.SelectedKey.ShouldBe("inbox");
            var row = Row(sidebar, inboxItem);
            row.IsSelected.ShouldBeTrue();
            row.Classes.ShouldContain(":selected");
            sidebar.MarkerRow.ShouldBe(row);

            var list = TestHelpers.Descendant<SidebarListPanel>(sidebar);
            var origin = row.TranslatePoint(default, list)!.Value;
            var block = sidebar.MarkerBlock!.CurrentRect;
            block.ShouldBe(new Rect(origin, row.Bounds.Size));
            var bar = sidebar.MarkerBar!.CurrentRect;
            bar.Width.ShouldBe(3);
            bar.Height.ShouldBe(16);
            bar.X.ShouldBe(block.X + 4);
            bar.Y.ShouldBe(block.Y + 9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectedItem_And_SelectedKey_Stay_In_Step_And_Parents_Are_Not_Selectable()
    {
        var s = new Sample();
        var window = Show(s.Sidebar);
        try
        {
            s.Sidebar.SelectedItem = s.Views;
            s.Sidebar.SelectedKey.ShouldBe("views");
            s.Sidebar.SelectedKey = "projects/beacon";
            s.Sidebar.SelectedItem.ShouldBe(s.Beacon);
            s.Sidebar.SelectedKey = "nowhere";
            s.Sidebar.SelectedItem.ShouldBeNull();
            s.Sidebar.SelectedKey.ShouldBe("nowhere");

            s.Sidebar.SelectedItem = s.Home;
            s.Sidebar.SelectedItem = s.Projects;
            s.Sidebar.SelectedItem.ShouldBe(s.Home);
            s.Sidebar.SelectedKey = "projects";
            s.Sidebar.SelectedItem.ShouldBeNull();

            // The key follows a renamed selected page.
            s.Sidebar.SelectedItem = s.Views;
            s.Views.Key = "saved-views";
            s.Sidebar.SelectedKey.ShouldBe("saved-views");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_A_Page_Selects_It_And_Raises_ItemInvoked_With_Commands()
    {
        var s = new Sample();
        var select = new RecordingCommand();
        var own = new RecordingCommand();
        s.Issues.Command = own;
        s.Issues.CommandParameter = "issues-param";
        s.Sidebar.SelectCommand = select;
        var invoked = new List<SidebarItem>();
        s.Sidebar.ItemInvoked += (_, e) => invoked.Add(e.Item);
        var window = Show(s.Sidebar);
        try
        {
            Pump();
            var row = Row(s.Sidebar, s.Issues);
            var center = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            s.Sidebar.SelectedItem.ShouldBe(s.Issues);
            invoked.ShouldBe([s.Issues]);
            own.Parameters.ShouldBe(["issues-param"]);
            select.Parameters.ShouldBe([s.Issues]);
            Focused(window).ShouldBe(row);

            // Choosing the selected page again still reports it.
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            invoked.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Parents_Open_And_Close_Instead_Of_Being_Selected()
    {
        var s = new Sample();
        var invoked = 0;
        s.Sidebar.ItemInvoked += (_, _) => invoked++;
        var window = Show(s.Sidebar);
        try
        {
            var projects = Row(s.Sidebar, s.Projects);
            projects.IsExpanded.ShouldBeFalse();
            s.Sidebar.OnViewClicked(projects.Node);
            s.Projects.IsExpanded.ShouldBeTrue();
            projects.Classes.ShouldContain(":expanded");
            s.Sidebar.SelectedItem.ShouldBeNull();
            invoked.ShouldBe(0);
            Navigable(s.Sidebar).ShouldContain("Beacon");

            s.Sidebar.OnViewClicked(projects.Node);
            s.Projects.IsExpanded.ShouldBeFalse();
            Navigable(s.Sidebar).ShouldNotContain("Beacon");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Selected_Page_Folded_Into_Its_Parent_Moves_The_Marker_To_The_Parent()
    {
        var s = new Sample();
        s.Projects.IsExpanded = true;
        s.Sidebar.SelectedItem = s.Beacon;
        var window = Show(s.Sidebar);
        try
        {
            s.Sidebar.MarkerRow.ShouldBe(Row(s.Sidebar, s.Beacon));
            s.Projects.IsExpanded = false;
            Dispatcher.UIThread.RunJobs();
            var projects = Row(s.Sidebar, s.Projects);
            s.Sidebar.MarkerRow.ShouldBe(projects);
            projects.Classes.ShouldContain(":current");
            Row(s.Sidebar, s.Beacon).IsSelected.ShouldBeTrue();
            s.Sidebar.SelectedItem.ShouldBe(s.Beacon);

            // A page in a folded section has no visible row and no marker.
            s.Sidebar.SelectedItem = s.Roadmap;
            s.Favorites.IsExpanded = false;
            Dispatcher.UIThread.RunJobs();
            s.Sidebar.MarkerRow.ShouldBeNull();
            s.Sidebar.MarkerBlock!.HasTarget.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Home_End_And_Nesting_Follow_The_Tree_Model()
    {
        var s = new Sample();
        s.Sidebar.SelectedItem = s.Home;
        var window = Show(s.Sidebar);
        try
        {
            var home = Row(s.Sidebar, s.Home);
            home.IsTabStop.ShouldBeTrue();
            Row(s.Sidebar, s.Inbox).IsTabStop.ShouldBeFalse();
            home.Focus(NavigationMethod.Tab);

            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Focused(window).ShouldBe(Row(s.Sidebar, s.Inbox));
            Row(s.Sidebar, s.Inbox).IsTabStop.ShouldBeTrue();
            home.IsTabStop.ShouldBeFalse();

            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Focused(window).ShouldBe(Row(s.Sidebar, s.Reviews));
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            // Archive is disabled: skipped.
            Focused(window).ShouldBe(Row(s.Sidebar, s.Roadmap));
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Focused(window).ShouldBe(s.Sidebar.HeaderOf(s.Favorites));
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Focused(window).ShouldBe(home);

            var projects = Row(s.Sidebar, s.Projects);
            projects.Focus(NavigationMethod.Directional);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            s.Projects.IsExpanded.ShouldBeTrue();
            Focused(window).ShouldBe(projects);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Focused(window).ShouldBe(Row(s.Sidebar, s.Atlas));
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Focused(window).ShouldBe(Row(s.Sidebar, s.Beacon));
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Focused(window).ShouldBe(projects);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            s.Projects.IsExpanded.ShouldBeFalse();

            // Left on a page of a collapsible section goes to its heading; Left there folds the section.
            Row(s.Sidebar, s.Reviews).Focus(NavigationMethod.Directional);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Focused(window).ShouldBe(s.Sidebar.HeaderOf(s.Favorites));
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            s.Favorites.IsExpanded.ShouldBeFalse();
            Navigable(s.Sidebar).ShouldNotContain("Q4 roadmap");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            s.Favorites.IsExpanded.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_And_Space_Choose_Pages_And_Disabled_Pages_Stay_Unchosen()
    {
        var s = new Sample();
        var invoked = new List<SidebarItem>();
        s.Sidebar.ItemInvoked += (_, e) => invoked.Add(e.Item);
        var window = Show(s.Sidebar);
        try
        {
            Row(s.Sidebar, s.Views).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            s.Sidebar.SelectedItem.ShouldBe(s.Views);
            Row(s.Sidebar, s.Inbox).Focus(NavigationMethod.Directional);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            s.Sidebar.SelectedItem.ShouldBe(s.Inbox);
            invoked.ShouldBe([s.Views, s.Inbox]);

            s.Sidebar.Activate(Row(s.Sidebar, s.Archive).Node, NavigationMethod.Pointer);
            s.Sidebar.OnViewClicked(Row(s.Sidebar, s.Archive).Node);
            s.Sidebar.SelectedItem.ShouldBe(s.Inbox);
            invoked.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_Jumps_To_The_Next_Page_With_That_Initial()
    {
        var s = new Sample();
        var window = Show(s.Sidebar);
        try
        {
            Row(s.Sidebar, s.Home).Focus(NavigationMethod.Tab);
            window.KeyTextInput("v");
            Dispatcher.UIThread.RunJobs();
            Focused(window).ShouldBe(Row(s.Sidebar, s.Views));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Cmd_Or_Ctrl_B_Inside_Toggles_The_Rail()
    {
        var s = new Sample();
        s.Projects.IsExpanded = true;
        s.Sidebar.SelectedItem = s.Atlas;
        var window = Show(s.Sidebar);
        try
        {
            var atlas = Row(s.Sidebar, s.Atlas);
            atlas.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.B, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            s.Sidebar.IsCollapsed.ShouldBeTrue();
            s.Sidebar.Classes.ShouldContain(":collapsed");
            // The focused child page folded into its parent: focus moves to the parent.
            Focused(window).ShouldBe(Row(s.Sidebar, s.Projects));

            window.KeyPressQwerty(PhysicalKey.B, RawInputModifiers.Meta);
            Dispatcher.UIThread.RunJobs();
            s.Sidebar.IsCollapsed.ShouldBeFalse();

            // Outside the sidebar the shortcut does nothing.
            var outside = new Button { Content = "Elsewhere" };
            ((DockPanel)s.Sidebar.Parent!).Children.Add(outside);
            Dispatcher.UIThread.RunJobs();
            outside.Focus();
            window.KeyPressQwerty(PhysicalKey.B, RawInputModifiers.Control);
            s.Sidebar.IsCollapsed.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Rail_Keeps_Icons_In_Place_And_Turns_Counts_Into_Dots_And_Labels_Into_Tooltips()
    {
        var s = new Sample();
        s.Projects.IsExpanded = true;
        s.Sidebar.SelectedItem = s.Beacon;
        s.Sidebar.IsFilterVisible = true;
        var window = Show(s.Sidebar);
        try
        {
            var inbox = Row(s.Sidebar, s.Inbox);
            var glyph = inbox.GetVisualDescendants().OfType<ArcGlyph>().First(g => g.Name == "PART_Glyph");
            var before = glyph.TranslatePoint(default, window)!.Value;
            Tooltip.GetTip(inbox).ShouldBeNull();

            s.Sidebar.ToggleCollapsed();
            Dispatcher.UIThread.RunJobs();
            s.Sidebar.Bounds.Width.ShouldBe(64);
            inbox.Bounds.Width.ShouldBe(40);
            glyph.TranslatePoint(default, window)!.Value.ShouldBe(before);
            inbox.Classes.ShouldContain(":rail");
            Tooltip.GetTip(inbox).ShouldBe("Inbox");
            Tooltip.GetSide(inbox).ShouldBe(TooltipSide.Right);
            inbox.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().First(e => e.Name == "PART_Dot").Opacity.ShouldBe(1);
            inbox.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "PART_Label").Opacity.ShouldBe(0);

            // Child pages leave the arrow-key order and the marker rests on their parent.
            Navigable(s.Sidebar).ShouldBe(["Home", "Inbox", "My issues", "Projects", "Views", "Q4 roadmap", "Archive", "Design reviews"]);
            s.Sidebar.MarkerRow.ShouldBe(Row(s.Sidebar, s.Projects));
            s.Sidebar.MarkerBlock!.CurrentRect.Width.ShouldBe(40);
            s.Sidebar.HeaderOf(s.Workspace)!.Classes.ShouldContain(":rail");

            // The filter gives way to a search button that opens the sidebar.
            TestHelpers.Descendant<TextBox>(s.Sidebar).Focusable.ShouldBeFalse();
            var search = s.Sidebar.GetVisualDescendants().OfType<SidebarIconButton>().First(b => b.Name == "PART_FilterButton");
            search.Focusable.ShouldBeTrue();
            AutomationProperties.GetName(search).ShouldBe("Filter");
            var collapse = s.Sidebar.GetVisualDescendants().OfType<SidebarIconButton>().First(b => b.Name == "PART_CollapseButton");
            AutomationProperties.GetName(collapse).ShouldBe("Expand sidebar");
            search.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            s.Sidebar.IsCollapsed.ShouldBeFalse();
            AutomationProperties.GetName(collapse).ShouldBe("Collapse sidebar");
            Tooltip.GetTip(inbox).ShouldBeNull();
            s.Sidebar.MarkerRow.ShouldBe(Row(s.Sidebar, s.Beacon));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void On_The_Rail_A_Parent_Opens_Its_Pages_In_A_Menu()
    {
        var s = new Sample();
        s.Sidebar.IsCollapsed = true;
        var invoked = new List<SidebarItem>();
        s.Sidebar.ItemInvoked += (_, e) => invoked.Add(e.Item);
        var window = Show(s.Sidebar);
        try
        {
            var projects = Row(s.Sidebar, s.Projects);
            projects.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            var menu = s.Sidebar.Flyout.ShouldNotBeNull();
            menu.IsOpen.ShouldBeTrue();
            menu.Side.ShouldBe(MenuSide.Right);
            var entries = menu.Items.OfType<MenuEntry>().ToList();
            entries.Select(e => e.Header).ShouldBe(["Atlas", "Beacon", "Cobalt"]);
            entries[1].InputGestureText.ShouldBe("3");
            menu.Items.OfType<MenuEntryLabel>().Single().Text.ShouldBe("Projects");
            s.Projects.IsExpanded.ShouldBeFalse();

            entries[2].PerformClick();
            Dispatcher.UIThread.RunJobs();
            s.Sidebar.SelectedItem.ShouldBe(s.Cobalt);
            invoked.ShouldBe([s.Cobalt]);
            menu.IsOpen.ShouldBeFalse();
            s.Sidebar.MarkerRow.ShouldBe(projects);

            // Right opens it too; opening the sidebar closes it.
            projects.Focus(NavigationMethod.Directional);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            menu.Items.OfType<MenuEntry>().Single(e => Equals(e.Header, "Cobalt")).IsChecked.ShouldBeTrue();
            s.Sidebar.IsCollapsed = false;
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Filter_Narrows_Pages_Opens_Parents_And_Says_When_Nothing_Matches()
    {
        var s = new Sample();
        s.Favorites.IsExpanded = false;
        s.Sidebar.IsFilterVisible = true;
        var invoked = new List<SidebarItem>();
        s.Sidebar.ItemInvoked += (_, e) => invoked.Add(e.Item);
        var window = Show(s.Sidebar);
        try
        {
            var noMatches = s.Sidebar.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "PART_NoMatches");
            var box = TestHelpers.Descendant<TextBox>(s.Sidebar);
            box.Text = "bea";
            Dispatcher.UIThread.RunJobs();
            s.Sidebar.FilterText.ShouldBe("bea");
            s.Sidebar.Classes.ShouldContain(":filtering");
            Navigable(s.Sidebar).ShouldBe(["Projects", "Beacon"]);
            // The parent opens for the match without changing its own state.
            s.Projects.IsExpanded.ShouldBeFalse();
            Row(s.Sidebar, s.Projects).Classes.ShouldContain(":expanded");
            Row(s.Sidebar, s.Atlas).IsVisible.ShouldBeFalse();
            noMatches.IsVisible.ShouldBeFalse();

            // Enter chooses the first page left; Down moves into the list.
            box.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            invoked.ShouldBe([s.Beacon]);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Focused(window).ShouldBe(Row(s.Sidebar, s.Projects));

            // A folded section opens for its matches.
            s.Sidebar.FilterText = "road";
            Navigable(s.Sidebar).ShouldBe(["Favorites", "Q4 roadmap"]);
            s.Favorites.IsExpanded.ShouldBeFalse();
            // A parent's own match keeps all of its pages, folded as the parent is.
            s.Sidebar.FilterText = "projects";
            Navigable(s.Sidebar).ShouldBe(["Projects"]);
            s.Sidebar.OnViewClicked(Row(s.Sidebar, s.Projects).Node);
            Navigable(s.Sidebar).ShouldBe(["Projects", "Atlas", "Beacon", "Cobalt"]);

            s.Sidebar.FilterText = "zzz";
            Navigable(s.Sidebar).ShouldBeEmpty();
            noMatches.IsVisible.ShouldBeTrue();
            noMatches.Text.ShouldBe("No matches");

            box.Focus();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            s.Sidebar.FilterText.ShouldBe(string.Empty);
            noMatches.IsVisible.ShouldBeFalse();
            Navigable(s.Sidebar).ShouldBe(["Home", "Inbox", "My issues", "Projects", "Atlas", "Beacon", "Cobalt", "Views", "Favorites"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Data_Changes_Update_And_Rebuild_The_Rows()
    {
        var s = new Sample();
        s.Sidebar.SelectedItem = s.Inbox;
        var window = Show(s.Sidebar);
        try
        {
            var inbox = Row(s.Sidebar, s.Inbox);
            s.Inbox.Badge = "13";
            inbox.Badge.ShouldBe("13");
            AutomationProperties.GetName(inbox).ShouldBe("Inbox, 13");
            s.Inbox.Badge = null;
            inbox.Classes.ShouldNotContain(":has-badge");
            s.Inbox.Label = "Messages";
            inbox.Label.ShouldBe("Messages");
            s.Inbox.Icon = null;
            inbox.Classes.ShouldNotContain(":has-icon");
            var custom = new Border { Width = 16, Height = 16 };
            s.Inbox.Icon = custom;
            inbox.IconContent.ShouldBe(custom);
            s.Inbox.IsEnabled = false;
            inbox.IsEnabled.ShouldBeFalse();
            s.Inbox.IsEnabled = true;

            var pinned = new SidebarItem { Key = "fav/pinned", Label = "Pinned page" };
            s.Favorites.Items.Add(pinned);
            Dispatcher.UIThread.RunJobs();
            Row(s.Sidebar, pinned).IsVisible.ShouldBeTrue();
            Navigable(s.Sidebar).Last().ShouldBe("Pinned page");
            // Rebuilt rows keep the selection and its marker; the control icon moved to the new row.
            s.Sidebar.MarkerRow.ShouldBe(Row(s.Sidebar, s.Inbox));
            Row(s.Sidebar, s.Inbox).IconContent.ShouldBe(custom);

            s.Favorites.Items.Remove(pinned);
            var section = new SidebarSection { Header = "Teams", Items = { new SidebarItem { Key = "teams/design", Label = "Design" } } };
            s.Sidebar.Sections.Add(section);
            Dispatcher.UIThread.RunJobs();
            s.Sidebar.RowOf(pinned).ShouldBeNull();
            s.Sidebar.HeaderOf(section).ShouldNotBeNull();
            Navigable(s.Sidebar).Last().ShouldBe("Design");

            // A page gains child pages and becomes a parent.
            s.Views.Items.Add(new SidebarItem { Key = "views/board", Label = "Board" });
            Dispatcher.UIThread.RunJobs();
            Row(s.Sidebar, s.Views).Classes.ShouldContain(":parent");

            s.Sidebar.Sections.Clear();
            Dispatcher.UIThread.RunJobs();
            Navigable(s.Sidebar).ShouldBeEmpty();
            s.Sidebar.MarkerRow.ShouldBeNull();
            s.Sidebar.MarkerBlock!.HasTarget.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Empty_Sidebar_Shows_Its_Chrome_And_Ignores_Keys()
    {
        var sidebar = new Sidebar { IsFilterVisible = true, Header = new TextBlock { Text = "Workspace" } };
        var window = Show(sidebar);
        try
        {
            Navigable(sidebar).ShouldBeEmpty();
            sidebar.MarkerRow.ShouldBeNull();
            var box = TestHelpers.Descendant<TextBox>(sidebar);
            box.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Focused(window).ShouldBe(box);
            sidebar.FilterText = "a";
            sidebar.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "PART_NoMatches").IsVisible.ShouldBeTrue();
            sidebar.ToggleCollapsed();
            Dispatcher.UIThread.RunJobs();
            sidebar.Bounds.Width.ShouldBe(64);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Automation_Exposes_Tree_Items_With_State()
    {
        var s = new Sample();
        s.Sidebar.SelectedItem = s.Home;
        var window = Show(s.Sidebar);
        try
        {
            var home = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(Row(s.Sidebar, s.Home));
            home.GetAutomationControlType().ShouldBe(Avalonia.Automation.Peers.AutomationControlType.TreeItem);
            home.GetName().ShouldBe("Home");
            ((Avalonia.Automation.Provider.ISelectionItemProvider)home).IsSelected.ShouldBeTrue();
            var projects = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(Row(s.Sidebar, s.Projects));
            var expand = (Avalonia.Automation.Provider.IExpandCollapseProvider)projects;
            expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
            expand.Expand();
            s.Projects.IsExpanded.ShouldBeTrue();
            var favorites = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(s.Sidebar.HeaderOf(s.Favorites)!);
            favorites.GetAutomationControlType().ShouldBe(Avalonia.Automation.Peers.AutomationControlType.Button);
            ((Avalonia.Automation.Provider.IExpandCollapseProvider)favorites).Collapse();
            s.Favorites.IsExpanded.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Selection_Nesting_And_Rail_Run_Frames_Without_Throwing()
    {
        var s = new Sample();
        s.Sidebar.IsFilterVisible = true;
        s.Sidebar.SelectedItem = s.Home;
        var window = Show(s.Sidebar, ArcMotionLevel.Full);
        try
        {
            Pump(3);
            s.Sidebar.Classes.ShouldContain(ArcMotion.FullPseudoClass);
            s.Sidebar.SelectedItem = s.Views;
            s.Projects.IsExpanded = true;
            Pump(4);
            s.Sidebar.SelectedItem = s.Beacon;
            s.Sidebar.IsCollapsed = true;
            for (var i = 0; i < 30; i++)
            {
                Pump();
                Thread.Sleep(8);
            }
            s.Sidebar.IsCollapsed = false;
            s.Favorites.IsExpanded = false;
            s.Sidebar.FilterText = "co";
            for (var i = 0; i < 30; i++)
            {
                Pump();
                Thread.Sleep(8);
            }
            s.Sidebar.MarkerBlock!.HasTarget.ShouldBeTrue();
            // "co" matches Beacon and Cobalt: the parent opens for them and the marker is back on Beacon.
            Navigable(s.Sidebar).ShouldBe(["Projects", "Beacon", "Cobalt"]);
            s.Sidebar.MarkerRow.ShouldBe(Row(s.Sidebar, s.Beacon));
        }
        finally
        {
            window.Close();
        }
    }
}
