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
            sidebar.Bounds.Width.ShouldBe(240);
            AutomationProperties.GetLandmarkType(sidebar).ShouldBe(AutomationLandmarkType.Navigation);
            AutomationProperties.GetName(sidebar).ShouldBe("Sidebar");

            var inbox = Row(sidebar, s.Inbox);
            inbox.Bounds.Height.ShouldBe(32);
            inbox.Bounds.Width.ShouldBe(216);
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
            // A plain muted block: no accent bar any more, and the selected page's icon takes the accent color.
            TestHelpers.Descendant<SidebarListPanel>(sidebar).GetVisualDescendants().OfType<GlidingIndicator>().Count().ShouldBe(1);
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
            // Everything after the icon fades as one group.
            inbox.GetVisualDescendants().OfType<Grid>().First(g => g.Name == "PART_TextGroup").Opacity.ShouldBe(0);

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
            // Rows of pages that stay are kept (same controls), with the selection and its marker.
            Row(s.Sidebar, s.Inbox).ShouldBeSameAs(inbox);
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

    [AvaloniaFact]
    public void Unread_Marks_The_Row_With_A_Dot_A_Stronger_Label_And_A_Name_Suffix()
    {
        var s = new Sample();
        s.Beacon.IsUnread = true;
        var window = Show(s.Sidebar);
        try
        {
            s.Projects.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
            var beacon = Row(s.Sidebar, s.Beacon);
            beacon.Classes.ShouldContain(":unread");
            AutomationProperties.GetName(beacon).ShouldBe("Beacon, 3, unread");
            var dot = beacon.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().First(e => e.Name == "PART_UnreadDot");
            dot.IsVisible.ShouldBeTrue();
            Row(s.Sidebar, s.Atlas).GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().First(e => e.Name == "PART_UnreadDot").IsVisible.ShouldBeFalse();

            // The wording can be localized.
            s.Sidebar.UnreadText = "ungelesen";
            AutomationProperties.GetName(beacon).ShouldBe("Beacon, 3, ungelesen");
            s.Beacon.IsUnread = false;
            beacon.Classes.ShouldNotContain(":unread");
            AutomationProperties.GetName(beacon).ShouldBe("Beacon, 3");

            // On the rail an unread page (like a count) puts a dot on its icon.
            s.Views.IsUnread = true;
            s.Sidebar.ToggleCollapsed();
            Dispatcher.UIThread.RunJobs();
            Row(s.Sidebar, s.Views).GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().First(e => e.Name == "PART_Dot").Opacity.ShouldBe(1);
            Row(s.Sidebar, s.Roadmap).GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().First(e => e.Name == "PART_Dot").Opacity.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Action_Row_Acts_Without_Becoming_The_Selected_Page()
    {
        var s = new Sample();
        var command = new RecordingCommand();
        var more = new SidebarItem { Key = "more", Label = "More", IsAction = true, Command = command, CommandParameter = "p", TrailingIcon = ArcIcons.ChevronRight };
        s.Main.Items.Add(more);
        s.Sidebar.SelectedItem = s.Home;
        var window = Show(s.Sidebar);
        try
        {
            var row = Row(s.Sidebar, more);
            row.Classes.ShouldContain(":action");
            row.Classes.ShouldContain(":has-trailing");
            row.TrailingGlyph.ShouldBe(ArcIcons.ChevronRight);
            var invoked = new List<SidebarItem>();
            s.Sidebar.ItemInvoked += (_, e) => invoked.Add(e.Item);

            // The action is part of the arrow-key order and can be chosen, but the selection stays on Home.
            Navigable(s.Sidebar).ShouldContain("More");
            var center = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            invoked.ShouldBe([more]);
            command.Parameters.ShouldBe(["p"]);
            s.Sidebar.SelectedItem.ShouldBe(s.Home);
            row.IsSelected.ShouldBeFalse();
            s.Sidebar.MarkerRow.ShouldBe(Row(s.Sidebar, s.Home));

            // It cannot be selected from code either, by item or by key.
            s.Sidebar.SelectedItem = more;
            s.Sidebar.SelectedItem.ShouldBe(s.Home);
            // A key that names an action finds no page, like any unknown key.
            s.Sidebar.SelectedKey = "more";
            s.Sidebar.SelectedItem.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Trailing_Icon_Can_Be_A_Geometry_Or_A_Control_And_Releases_The_Control()
    {
        var s = new Sample();
        var window = Show(s.Sidebar);
        try
        {
            var views = Row(s.Sidebar, s.Views);
            views.Classes.ShouldNotContain(":has-trailing");
            s.Views.TrailingIcon = ArcIcons.ChevronRight;
            views.Classes.ShouldContain(":has-trailing");
            views.TrailingGlyph.ShouldBe(ArcIcons.ChevronRight);
            var badge = new Border { Width = 8, Height = 8 };
            s.Views.TrailingIcon = badge;
            views.TrailingGlyph.ShouldBeNull();
            views.TrailingContent.ShouldBe(badge);
            Dispatcher.UIThread.RunJobs();

            // The trailing icon sits right after the label's text, not at the end of the row.
            var label = views.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "PART_Label");
            var labelRight = label.TranslatePoint(new Point(label.Bounds.Width, 0), views)!.Value.X;
            var iconLeft = badge.TranslatePoint(default, views)!.Value.X;
            iconLeft.ShouldBeGreaterThanOrEqualTo(labelRight);
            iconLeft.ShouldBeLessThan(labelRight + 12);

            // A removed page lets go of its control icon, so another row can take it.
            s.Workspace.Items.Remove(s.Views);
            Dispatcher.UIThread.RunJobs();
            badge.GetVisualParent().ShouldBeNull();
            var other = new SidebarItem { Key = "board", Label = "Board", TrailingIcon = badge };
            s.Workspace.Items.Add(other);
            Dispatcher.UIThread.RunJobs();
            Row(s.Sidebar, other).TrailingContent.ShouldBe(badge);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Control_Icon_Such_As_A_Colored_Dot_Is_Centered_In_The_Icon_Slot()
    {
        var s = new Sample();
        var dot = new Avalonia.Controls.Shapes.Ellipse { Width = 8, Height = 8 };
        s.Views.Icon = dot;
        var window = Show(s.Sidebar);
        try
        {
            var views = Row(s.Sidebar, s.Views);
            views.IconContent.ShouldBe(dot);
            views.Classes.ShouldContain(":has-icon");
            var center = dot.TranslatePoint(new Point(4, 4), views)!.Value;
            // Row padding 12 plus half of the 16px slot; vertically centered in the 32px row.
            center.X.ShouldBe(20, 0.5);
            center.Y.ShouldBe(views.Bounds.Height / 2, 0.5);
            // The rail keeps it where it is.
            s.Sidebar.ToggleCollapsed();
            Dispatcher.UIThread.RunJobs();
            dot.TranslatePoint(new Point(4, 4), views)!.Value.X.ShouldBe(20, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Collapse_Button_Can_Sit_Beside_The_Filter_And_Moves_To_The_Top_On_The_Rail()
    {
        var s = new Sample();
        s.Sidebar.IsFilterVisible = true;
        s.Sidebar.Header = new TextBlock { Text = "Workspace" };
        var window = Show(s.Sidebar);
        try
        {
            SidebarIconButton Part(string name) => s.Sidebar.GetVisualDescendants().OfType<SidebarIconButton>().First(b => b.Name == name);
            var header = Part("PART_CollapseButton");
            var beside = Part("PART_FilterCollapseButton");
            s.Sidebar.CollapseButtonPlacement.ShouldBe(SidebarCollapseButtonPlacement.Header);
            header.IsVisible.ShouldBeTrue();
            beside.IsVisible.ShouldBeFalse();
            s.Sidebar.Classes.ShouldNotContain(":filter-toggle");

            s.Sidebar.CollapseButtonPlacement = SidebarCollapseButtonPlacement.Filter;
            Dispatcher.UIThread.RunJobs();
            s.Sidebar.Classes.ShouldContain(":filter-toggle");
            header.IsVisible.ShouldBeFalse();
            beside.IsVisible.ShouldBeTrue();
            beside.Focusable.ShouldBeTrue();
            AutomationProperties.GetName(beside).ShouldBe("Collapse sidebar");
            // Beside the filter box, on the same row.
            var filter = TestHelpers.Descendant<TextBox>(s.Sidebar);
            var filterTop = filter.TranslatePoint(default, window)!.Value.Y;
            var buttonCenter = beside.TranslatePoint(new Point(0, beside.Bounds.Height / 2), window)!.Value.Y;
            buttonCenter.ShouldBeGreaterThan(filterTop);
            buttonCenter.ShouldBeLessThan(filterTop + 40);

            var center = beside.TranslatePoint(new Point(beside.Bounds.Width / 2, beside.Bounds.Height / 2), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            s.Sidebar.IsCollapsed.ShouldBeTrue();

            // On the rail the toggle is the header row's button again; the filter row's goes.
            header.IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(header).ShouldBe("Expand sidebar");
            beside.Focusable.ShouldBeFalse();
            header.Focusable.ShouldBeTrue();

            // Without the filter the placement falls back to the header row.
            s.Sidebar.IsCollapsed = false;
            s.Sidebar.IsFilterVisible = false;
            Dispatcher.UIThread.RunJobs();
            s.Sidebar.Classes.ShouldNotContain(":filter-toggle");
            header.IsVisible.ShouldBeTrue();
            beside.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void IsCollapsed_Can_Be_Driven_From_Outside_Through_A_Binding()
    {
        var s = new Sample();
        var toggle = new Avalonia.Controls.Primitives.ToggleButton();
        toggle.Bind(Avalonia.Controls.Primitives.ToggleButton.IsCheckedProperty, new Avalonia.Data.Binding(nameof(Sidebar.IsCollapsed)) { Source = s.Sidebar, Mode = Avalonia.Data.BindingMode.TwoWay });
        var window = Show(s.Sidebar);
        try
        {
            toggle.IsChecked.ShouldBe(false);
            toggle.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            s.Sidebar.IsCollapsed.ShouldBeTrue();
            s.Sidebar.Bounds.Width.ShouldBe(64);
            s.Sidebar.ToggleCollapsed();
            toggle.IsChecked.ShouldBe(false);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Footer_Sits_Under_A_Separator_And_Without_A_Footer_There_Is_None()
    {
        var s = new Sample();
        var window = Show(s.Sidebar);
        try
        {
            Border Separator() => s.Sidebar.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_FooterSeparator");
            Separator().IsVisible.ShouldBeFalse();
            s.Sidebar.Footer = new TextBlock { Text = "Maya Chen" };
            Dispatcher.UIThread.RunJobs();
            Separator().IsVisible.ShouldBeTrue();
            Separator().Bounds.Height.ShouldBe(1);
            Separator().Bounds.Width.ShouldBe(240);
            var footer = s.Sidebar.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Maya Chen");
            footer.TranslatePoint(default, s.Sidebar)!.Value.Y.ShouldBeGreaterThan(Separator().TranslatePoint(default, s.Sidebar)!.Value.Y);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Structure_Changes_Keep_The_Rows_Of_Pages_That_Stay()
    {
        var s = new Sample();
        s.Projects.IsExpanded = true;
        s.Sidebar.SelectedItem = s.Beacon;
        var window = Show(s.Sidebar);
        try
        {
            var home = Row(s.Sidebar, s.Home);
            var inbox = Row(s.Sidebar, s.Inbox);
            var projects = Row(s.Sidebar, s.Projects);
            var beacon = Row(s.Sidebar, s.Beacon);
            var views = Row(s.Sidebar, s.Views);
            var workspaceHeader = s.Sidebar.HeaderOf(s.Workspace);

            // Adding, moving and removing pages touches only those pages.
            var added = new SidebarItem { Key = "added", Label = "Added" };
            s.Main.Items.Insert(1, added);
            s.Projects.Items.Remove(s.Atlas);
            Dispatcher.UIThread.RunJobs();
            Navigable(s.Sidebar).Take(4).ShouldBe(["Home", "Added", "Inbox", "My issues"]);
            Row(s.Sidebar, s.Home).ShouldBeSameAs(home);
            Row(s.Sidebar, s.Inbox).ShouldBeSameAs(inbox);
            Row(s.Sidebar, s.Projects).ShouldBeSameAs(projects);
            Row(s.Sidebar, s.Beacon).ShouldBeSameAs(beacon);
            s.Sidebar.HeaderOf(s.Workspace).ShouldBeSameAs(workspaceHeader);
            s.Sidebar.RowOf(s.Atlas).ShouldBeNull();
            Navigable(s.Sidebar).ShouldContain("Beacon");
            s.Sidebar.MarkerRow.ShouldBe(beacon);

            // The rows follow the order of the items.
            s.Main.Items.Move(0, 3);
            Dispatcher.UIThread.RunJobs();
            Navigable(s.Sidebar).Take(4).ShouldBe(["Added", "Inbox", "My issues", "Home"]);
            Row(s.Sidebar, s.Home).ShouldBeSameAs(home);
            home.TranslatePoint(default, s.Sidebar)!.Value.Y.ShouldBeGreaterThan(Row(s.Sidebar, s.Issues).TranslatePoint(default, s.Sidebar)!.Value.Y);

            // A page moves to another section with a new row (its control icon is let go first).
            var dot = new Border { Width = 8, Height = 8 };
            s.Views.Icon = dot;
            s.Workspace.Items.Remove(s.Views);
            s.Main.Items.Add(s.Views);
            Dispatcher.UIThread.RunJobs();
            Row(s.Sidebar, s.Views).ShouldNotBeSameAs(views);
            Row(s.Sidebar, s.Views).IconContent.ShouldBe(dot);
            Navigable(s.Sidebar).Skip(3).Take(3).ShouldBe(["Home", "Views", "Projects"]);

            // A parent that loses all its pages becomes a plain page, and a page that gains pages a parent.
            s.Projects.Items.Clear();
            Dispatcher.UIThread.RunJobs();
            Row(s.Sidebar, s.Projects).Classes.ShouldNotContain(":parent");
            Row(s.Sidebar, s.Projects).ShouldNotBeSameAs(projects);
            s.Projects.Items.Add(new SidebarItem { Key = "projects/new", Label = "Newest" });
            Dispatcher.UIThread.RunJobs();
            Row(s.Sidebar, s.Projects).Classes.ShouldContain(":parent");

            // A removed section takes its rows and heading with it.
            s.Sidebar.Sections.Remove(s.Favorites);
            Dispatcher.UIThread.RunJobs();
            s.Sidebar.HeaderOf(s.Favorites).ShouldBeNull();
            s.Sidebar.RowOf(s.Roadmap).ShouldBeNull();
            Navigable(s.Sidebar).ShouldNotContain("Q4 roadmap");
            Row(s.Sidebar, s.Inbox).ShouldBeSameAs(inbox);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Marker_Follows_Its_Row_When_A_Section_Opens_Above_It()
    {
        var s = new Sample();
        s.Favorites.IsExpanded = false;
        s.Sidebar.SelectedItem = s.Views;
        var window = Show(s.Sidebar);
        try
        {
            var list = TestHelpers.Descendant<SidebarListPanel>(s.Sidebar);
            var row = Row(s.Sidebar, s.Views);
            var before = s.Sidebar.MarkerBlock!.CurrentRect;
            before.Y.ShouldBe(row.TranslatePoint(default, list)!.Value.Y, 0.5);

            // Favorites sits below Views here; move it above by inserting a section before the workspace.
            var top = new SidebarSection { Header = "Pinned", Items = { new SidebarItem { Key = "p1", Label = "One" }, new SidebarItem { Key = "p2", Label = "Two" } } };
            s.Sidebar.Sections.Insert(1, top);
            Dispatcher.UIThread.RunJobs();
            var after = s.Sidebar.MarkerBlock!.CurrentRect;
            after.Y.ShouldBeGreaterThan(before.Y);
            after.Y.ShouldBe(row.TranslatePoint(default, list)!.Value.Y, 0.5);
            s.Sidebar.MarkerTarget.Y.ShouldBe(after.Y, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Filtering_An_Action_Row_And_A_Page_Without_An_Icon_Works_With_The_New_Parts()
    {
        var s = new Sample();
        var more = new SidebarItem { Key = "more", Label = "More", IsAction = true };
        s.Favorites.Items.Add(more);
        var window = Show(s.Sidebar);
        try
        {
            s.Sidebar.FilterText = "mor";
            Navigable(s.Sidebar).ShouldBe(["Favorites", "More"]);
            s.Sidebar.FilterText = string.Empty;
            Navigable(s.Sidebar).ShouldContain("More");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Counts how often layout measures it (the page next to a sidebar).</summary>
    private sealed class CountingPage : Decorator
    {
        public int Measures { get; private set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            Measures++;
            return base.MeasureOverride(availableSize);
        }
    }

    private static (Window Window, CountingPage Page) ShowBeside(Sidebar sidebar, ArcMotionLevel level)
    {
        var page = new CountingPage { Child = new Border() };
        var dock = new DockPanel();
        DockPanel.SetDock(sidebar, Dock.Left);
        dock.Children.Add(sidebar);
        dock.Children.Add(page);
        var window = TestHelpers.Show(dock, level);
        window.Width = 800;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        return (window, page);
    }

    private static void Settle(int frames = 160)
    {
        for (var i = 0; i < frames; i++)
        {
            Pump();
            Thread.Sleep(8);
        }
    }

    [AvaloniaFact]
    public void An_Overlay_Sidebar_Jumps_Its_Layout_Width_And_Springs_Only_The_Pane()
    {
        var s = new Sample();
        s.Sidebar.ResizeMode = SidebarResizeMode.Overlay;
        var (window, page) = ShowBeside(s.Sidebar, ArcMotionLevel.Full);
        try
        {
            Pump(3);
            s.Sidebar.Classes.ShouldContain(":overlay-resize");
            page.Bind(Visual.RenderTransformProperty, new Avalonia.Data.Binding(nameof(Sidebar.PageShift)) { Source = s.Sidebar });
            s.Sidebar.Bounds.Width.ShouldBe(240);
            page.Bounds.X.ShouldBe(240);
            s.Sidebar.PageShift.ShouldBeOfType<Avalonia.Media.TranslateTransform>().X.ShouldBe(0);
            var pane = s.Sidebar.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Pane");
            var measuresBefore = page.Measures;

            s.Sidebar.IsCollapsed = true;
            Dispatcher.UIThread.RunJobs();
            // The layout has moved on at once: the sidebar is 64 wide and the page sits next to it ...
            s.Sidebar.Bounds.Width.ShouldBe(64);
            page.Bounds.X.ShouldBe(64);
            // ... while the pane is still wide and the page is shifted back to where the pane's edge is.
            pane.Bounds.Width.ShouldBeGreaterThan(64);
            s.Sidebar.PaneWidth.ShouldBeGreaterThan(64);
            var shift = (Avalonia.Media.TranslateTransform)s.Sidebar.PageShift;
            shift.X.ShouldBeGreaterThan(0);
            page.Bounds.X.ShouldBe(64);

            var seen = new List<double>();
            for (var i = 0; i < 160; i++)
            {
                Pump();
                seen.Add(pane.Bounds.Width);
                Thread.Sleep(8);
            }
            // The pane narrowed step by step to the rail, the shift followed it to 0, and the page was laid out once.
            seen.Distinct().Count().ShouldBeGreaterThan(4);
            pane.Bounds.Width.ShouldBe(64, 0.5);
            shift.X.ShouldBe(0, 0.5);
            (page.Measures - measuresBefore).ShouldBeLessThanOrEqualTo(3);

            // And back: the page jumps to its new place and slides there with the shift.
            s.Sidebar.IsCollapsed = false;
            Dispatcher.UIThread.RunJobs();
            s.Sidebar.Bounds.Width.ShouldBe(240);
            shift.X.ShouldBeLessThan(0);
            Settle();
            pane.Bounds.Width.ShouldBe(240, 0.5);
            shift.X.ShouldBe(0, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    // An overlay pane springs open while the layout already has the open width, so the rows, the header and the filter
    // keep their size and are not arranged again when the spring ends: their content must not keep the width of the
    // spring's first frame (pushed off the pane's right edge).
    [AvaloniaTheory]
    [InlineData(SidebarResizeMode.Overlay)]
    [InlineData(SidebarResizeMode.Push)]
    public void After_The_Rail_The_Open_Sidebar_Lays_Its_Content_Out_At_The_Open_Width_Again(SidebarResizeMode mode)
    {
        var s = new Sample();
        s.Sidebar.ResizeMode = mode;
        s.Sidebar.IsFilterVisible = true;
        s.Sidebar.CollapseButtonPlacement = SidebarCollapseButtonPlacement.Filter;
        s.Sidebar.Header = new Button
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            Content = new Border { Width = 180, Height = 32 }
        };
        var (window, _) = ShowBeside(s.Sidebar, ArcMotionLevel.Full);
        try
        {
            Pump(3);
            IReadOnlyList<SidebarOpenWidthHost> Hosts() =>
                s.Sidebar.GetVisualDescendants().OfType<SidebarOpenWidthHost>().Where(h => h.IsEffectivelyVisible).ToList();
            var open = Hosts().ToDictionary(h => h, h => h.Bounds.Width);
            open.Count.ShouldBeGreaterThan(8);

            s.Sidebar.IsCollapsed = true;
            Settle();
            s.Sidebar.IsCollapsed = false;
            Settle();

            s.Sidebar.PaneWidth.ShouldBe(240, 0.5);
            foreach (var host in Hosts())
            {
                var label = $"{host.Name ?? host.Child!.GetType().Name} in {(host.TemplatedParent as Control)?.GetType().Name}";
                host.Bounds.Width.ShouldBe(open[host], 0.5, label);
                host.Child!.Bounds.Width.ShouldBe(host.Bounds.Width, 0.5, label);
                host.Clip.ShouldBeNull(label);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Push_Sidebar_Lays_The_Page_Out_On_Every_Frame_Of_The_Width_Spring()
    {
        var s = new Sample();
        s.Sidebar.ResizeMode = SidebarResizeMode.Push;
        var (window, page) = ShowBeside(s.Sidebar, ArcMotionLevel.Full);
        try
        {
            Pump(3);
            ((Avalonia.Media.TranslateTransform)s.Sidebar.PageShift).X.ShouldBe(0);
            var before = page.Measures;
            s.Sidebar.IsCollapsed = true;
            Settle();
            s.Sidebar.Bounds.Width.ShouldBe(64, 0.5);
            // The reference behavior the overlay mode avoids: the page next to the sidebar follows the spring.
            (page.Measures - before).ShouldBeGreaterThan(8);
            ((Avalonia.Media.TranslateTransform)s.Sidebar.PageShift).X.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Overlay_Without_Motion_Changes_The_Width_At_Once_And_Switching_Back_To_Push_Resets_It()
    {
        var s = new Sample();
        s.Sidebar.ResizeMode = SidebarResizeMode.Overlay;
        var (window, page) = ShowBeside(s.Sidebar, ArcMotionLevel.Off);
        try
        {
            s.Sidebar.IsCollapsed = true;
            Dispatcher.UIThread.RunJobs();
            Pump(2);
            s.Sidebar.PaneWidth.ShouldBe(64);
            ((Avalonia.Media.TranslateTransform)s.Sidebar.PageShift).X.ShouldBe(0);
            var pane = s.Sidebar.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Pane");
            pane.Bounds.Width.ShouldBe(64);
            s.Sidebar.ResizeMode = SidebarResizeMode.Push;
            s.Sidebar.Classes.ShouldNotContain(":overlay-resize");
            s.Sidebar.IsCollapsed = false;
            Dispatcher.UIThread.RunJobs();
            Pump(2);
            pane.Bounds.Width.ShouldBe(240);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Closed_Parents_And_Sections_Leave_Layout_And_Rendering_Until_They_Open()
    {
        var s = new Sample();
        s.Favorites.IsExpanded = false;
        var window = Show(s.Sidebar);
        try
        {
            Pump(2);
            Dispatcher.UIThread.RunJobs();
            // Projects is closed from the start: its pages are not laid out or drawn at all.
            var atlas = Row(s.Sidebar, s.Atlas);
            atlas.IsEffectivelyVisible.ShouldBeFalse();
            Row(s.Sidebar, s.Roadmap).IsEffectivelyVisible.ShouldBeFalse();

            s.Projects.IsExpanded = true;
            s.Favorites.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
            atlas.IsEffectivelyVisible.ShouldBeTrue();
            atlas.Bounds.Height.ShouldBe(30);
            Row(s.Sidebar, s.Roadmap).IsEffectivelyVisible.ShouldBeTrue();

            s.Projects.IsExpanded = false;
            Dispatcher.UIThread.RunJobs();
            Pump(2);
            Dispatcher.UIThread.RunJobs();
            atlas.IsEffectivelyVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Detached_Sidebar_Releases_Its_Rows_And_Item_Subscriptions_And_Builds_Them_Again_On_Return()
    {
        var s = new Sample();
        s.Sidebar.SelectedItem = s.Inbox;
        var dock = new DockPanel { Children = { s.Sidebar } };
        var window = TestHelpers.Show(dock);
        try
        {
            Dispatcher.UIThread.RunJobs();
            var inbox = Row(s.Sidebar, s.Inbox);
            s.Sidebar.MarkerRow.ShouldBe(inbox);

            dock.Children.Remove(s.Sidebar);
            Dispatcher.UIThread.RunJobs();
            s.Sidebar.RowOf(s.Inbox).ShouldBeNull();
            s.Sidebar.NavigableNodes.ShouldBeEmpty();
            // Changes made while it is away cost nothing and are picked up when it returns.
            s.Inbox.Label = "Messages";
            s.Main.Items.Add(new SidebarItem { Key = "late", Label = "Late" });
            Dispatcher.UIThread.RunJobs();
            s.Sidebar.RowOf(s.Inbox).ShouldBeNull();

            dock.Children.Add(s.Sidebar);
            Dispatcher.UIThread.RunJobs();
            Pump(2);
            var again = Row(s.Sidebar, s.Inbox);
            again.Label.ShouldBe("Messages");
            again.IsSelected.ShouldBeTrue();
            s.Sidebar.MarkerRow.ShouldBe(again);
            Navigable(s.Sidebar).ShouldContain("Late");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Overlay_Sidebar_Under_Reduced_Motion_Jumps_Too()
    {
        var s = new Sample();
        s.Sidebar.ResizeMode = SidebarResizeMode.Overlay;
        var (window, _) = ShowBeside(s.Sidebar, ArcMotionLevel.Reduced);
        try
        {
            Pump(3);
            s.Sidebar.IsCollapsed = true;
            Dispatcher.UIThread.RunJobs();
            Pump(2);
            s.Sidebar.PaneWidth.ShouldBe(64);
            ((Avalonia.Media.TranslateTransform)s.Sidebar.PageShift).X.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }
}
