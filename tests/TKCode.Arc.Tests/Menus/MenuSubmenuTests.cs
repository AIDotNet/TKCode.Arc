using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Menus;

/// <summary>Nested submenus of MenuSurface / DropdownMenu / ContextMenu (issue #29).</summary>
public class MenuSubmenuTests
{
    private sealed record Node(string Name, params Node[] Children)
    {
        public override string ToString() => Name;
    }

    private static Window ShowWide(Control content)
    {
        var window = new Window { Content = content, Width = 900, Height = 600 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        FocusRingInput.Pump();
        return window;
    }

    private static Point Center(Visual visual, Visual root) => FocusRingInput.Center(visual, root);

    private static Rect BoundsIn(Visual visual, Visual root) => new(visual.TranslatePoint(default, root)!.Value, visual.Bounds.Size);

    /// <summary>Moves the mouse to <paramref name="point"/> once the last rendered frame hit-tests it onto <paramref name="target"/>.</summary>
    private static void Hover(Window window, Visual target, Point point)
    {
        FocusRingInput.WaitUntilOver(window, target, point);
        window.MouseMove(point);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Hover(Window window, MenuEntry row) => Hover(window, row, Center(row.RowVisual, window));

    private static void Settle(Func<bool> condition, string because)
    {
        TestHelpers.PumpUntil(() =>
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            return condition();
        }).ShouldBeTrue(because);
        FocusRingInput.Pump();
    }

    private static (MenuSurface Surface, MenuEntry Rename, MenuEntry Export, MenuEntry Csv, MenuEntry Pdf, MenuEntry Share, MenuEntry Delete) BuildMenu()
    {
        var surface = new MenuSurface();
        var rename = new MenuEntry { Header = "Rename" };
        var export = new MenuEntry { Header = "Export", InputGestureText = "⌘E" };
        var csv = new MenuEntry { Header = "Export CSV" };
        var pdf = new MenuEntry { Header = "Export PDF" };
        export.Items.Add(csv);
        export.Items.Add(pdf);
        var share = new MenuEntry { Header = "Share" };
        var delete = new MenuEntry { Header = "Delete", IsDestructive = true, HasSeparatorBefore = true };
        surface.Items.Add(rename);
        surface.Items.Add(export);
        surface.Items.Add(share);
        surface.Items.Add(delete);
        return (surface, rename, export, csv, pdf, share, delete);
    }

    [AvaloniaFact]
    public void Child_Rows_Make_A_Submenu_Row_That_Shows_A_Chevron_Instead_Of_Its_Shortcut()
    {
        var plain = new MenuEntry { Header = "Plain", InputGestureText = "⌘P" };
        plain.HasSubmenu.ShouldBeFalse();
        var data = new MenuEntry { Header = "Recent", ItemsSource = new List<string>() };
        data.HasSubmenu.ShouldBeFalse("an empty source opens nothing");
        data.ItemsSource = new[] { "a.txt", "b.txt" };
        data.HasSubmenu.ShouldBeTrue();
        data.ItemsSource = null;
        data.HasSubmenu.ShouldBeFalse();

        var (surface, _, export, _, _, _, _) = BuildMenu();
        export.HasSubmenu.ShouldBeTrue();
        surface.Items.Add(plain);
        var window = ShowWide(new StackPanel { Margin = new Thickness(20), Children = { surface } });
        try
        {
            TestHelpers.Descendant<ArcGlyph>(export, "PART_SubmenuChevron").IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(export, "PART_Gesture").IsVisible.ShouldBeFalse("the chevron takes the shortcut's place");
            TestHelpers.Descendant<ArcGlyph>(plain, "PART_SubmenuChevron").IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(plain, "PART_Gesture").IsVisible.ShouldBeTrue();

            // Removing the last child row turns the row back into a command row.
            export.Items.Clear();
            export.HasSubmenu.ShouldBeFalse();
            FocusRingInput.Pump();
            TestHelpers.Descendant<ArcGlyph>(export, "PART_SubmenuChevron").IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(export, "PART_Gesture").IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_A_Submenu_Row_Opens_It_Beside_The_Menu_Instead_Of_Activating_The_Row()
    {
        var anchor = new Button { Content = "Actions", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var (surface, _, export, csv, _, _, _) = BuildMenu();
        var clicks = 0;
        export.Click += (_, _) => clicks++;
        object? clicked = null;
        surface.ItemClicked += (_, e) => clicked = e.Item;
        var window = ShowWide(new StackPanel { Margin = new Thickness(40), Children = { anchor } });
        try
        {
            surface.Open(anchor);
            Settle(() => surface.IsOpen && export.Bounds.Height > 0, "the menu opens");
            FocusRingInput.Click(window, export.RowVisual);
            var submenu = export.Submenu.ShouldNotBeNull();
            Settle(() => submenu.IsOpen && csv.Bounds.Height > 0, "the click opens the submenu");

            clicks.ShouldBe(0);
            clicked.ShouldBeNull();
            surface.IsOpen.ShouldBeTrue("opening a submenu keeps the menu open");
            surface.OpenSubmenuRow.ShouldBe(export);
            export.Classes.Contains(MenuEntry.SubmenuOpenPseudoClass).ShouldBeTrue();
            surface.HighlightedEntry.ShouldBe(export);
            submenu.Variant.ShouldBe(surface.Variant);

            // Right of the menu, its first row level with the row that opened it.
            var menu = BoundsIn(surface, window);
            var sub = BoundsIn(submenu, window);
            sub.Left.ShouldBe(menu.Right + MenuSurface.SubmenuGap, 1.0);
            BoundsIn(csv.RowVisual, window).Top.ShouldBe(BoundsIn(export.RowVisual, window).Top, 1.0);
            submenu.Popup.ShouldNotBeNull().LastTravelDirection.ShouldBe(new Vector(-1, 0));

            // Clicking the row again keeps the submenu open (it never toggles).
            FocusRingInput.Click(window, export.RowVisual);
            submenu.IsOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hovering_Opens_The_Submenu_After_A_Delay_And_Another_Row_Closes_It()
    {
        var anchor = new Button { Content = "Actions", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var (surface, rename, export, _, _, share, _) = BuildMenu();
        var window = ShowWide(new StackPanel { Margin = new Thickness(40), Children = { anchor } });
        try
        {
            surface.Open(anchor);
            Settle(() => surface.IsOpen && export.Bounds.Height > 0, "the menu opens");
            Hover(window, export);
            surface.HighlightedEntry.ShouldBe(export);
            (export.Submenu?.IsOpen ?? false).ShouldBeFalse("passing over a row does not open its submenu at once");
            Settle(() => export.Submenu is { IsOpen: true }, "resting on the row opens the submenu");

            // Moving straight down onto the next row (not toward the submenu) closes it; that row takes the highlight.
            Hover(window, share, Center(share.RowVisual, window) - new Vector(40, 0));
            Settle(() => export.Submenu is { IsOpen: false }, "another row closes the submenu");
            surface.HighlightedEntry.ShouldBe(share);
            surface.OpenSubmenuRow.ShouldBeNull();

            // A submenu opened by hover closes when the keyboard moves on.
            Hover(window, export);
            Settle(() => export.Submenu is { IsOpen: true }, "hover opens it again");
            FocusRingInput.Press(window, PhysicalKey.ArrowUp);
            Settle(() => export.Submenu is { IsOpen: false }, "the keyboard leaving the row closes the submenu");
            surface.HighlightedEntry.ShouldBe(rename);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Heading_Diagonally_For_The_Submenu_Crosses_Other_Rows_Without_Closing_It()
    {
        var anchor = new Button { Content = "Actions", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var (surface, _, export, csv, _, share, _) = BuildMenu();
        surface.SafeZoneLifetime = TimeSpan.FromSeconds(30);
        var window = ShowWide(new StackPanel { Margin = new Thickness(40), Children = { anchor } });
        try
        {
            surface.Open(anchor);
            Settle(() => surface.IsOpen && export.Bounds.Height > 0, "the menu opens");
            Hover(window, export);
            Settle(() => export.Submenu is { IsOpen: true } && csv.Bounds.Height > 0, "hover opens the submenu");

            // Down and to the right, over Share, on the way into the submenu: Share does not take the highlight.
            var row = BoundsIn(share.RowVisual, window);
            var onShare = new Point(row.Right - 30, row.Top + 6);
            Hover(window, share, onShare);
            surface.SafeZone.ShouldNotBeNull();
            surface.HighlightedEntry.ShouldBe(export);
            export.Submenu!.IsOpen.ShouldBeTrue();
            Hover(window, share, onShare + new Vector(12, 3));
            surface.HighlightedEntry.ShouldBe(export);
            export.Submenu.IsOpen.ShouldBeTrue();

            // Into the submenu: its row takes its highlight, the menu keeps Export highlighted.
            Hover(window, csv);
            export.Submenu.HighlightedEntry.ShouldBe(csv);
            surface.HighlightedEntry.ShouldBe(export);

            // Back over Share heading left (away from the submenu): Share wins and the submenu closes.
            Hover(window, share, Center(share.RowVisual, window));
            Settle(() => export.Submenu.IsOpen == false, "a row away from the submenu closes it");
            surface.HighlightedEntry.ShouldBe(share);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Safe_Zone_Lapses_When_The_Pointer_Rests_On_A_Row_It_Crossed()
    {
        var anchor = new Button { Content = "Actions", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var (surface, _, export, csv, _, share, _) = BuildMenu();
        surface.SafeZoneLifetime = TimeSpan.FromMilliseconds(150);
        var window = ShowWide(new StackPanel { Margin = new Thickness(40), Children = { anchor } });
        try
        {
            surface.Open(anchor);
            Settle(() => surface.IsOpen && export.Bounds.Height > 0, "the menu opens");
            Hover(window, export);
            Settle(() => export.Submenu is { IsOpen: true } && csv.Bounds.Height > 0, "hover opens the submenu");
            var row = BoundsIn(share.RowVisual, window);
            Hover(window, share, new Point(row.Right - 30, row.Top + 6));
            Settle(() => export.Submenu!.IsOpen == false && ReferenceEquals(surface.HighlightedEntry, share),
                   "resting on Share hands it the highlight once the zone lapses");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Opens_Levels_On_Their_First_Row_And_Closes_One_Level_At_A_Time()
    {
        var menu = new DropdownMenu { Label = "File" };
        var open = new MenuEntry { Header = "Open" };
        var export = new MenuEntry { Header = "Export" };
        var csv = new MenuEntry { Header = "Export CSV" };
        var pdf = new MenuEntry { Header = "Export PDF" };
        export.Items.Add(csv);
        export.Items.Add(pdf);
        var delete = new MenuEntry { Header = "Delete" };
        menu.Items.Add(open);
        menu.Items.Add(export);
        menu.Items.Add(delete);
        var window = ShowWide(new StackPanel { Margin = new Thickness(40), Children = { menu } });
        try
        {
            menu.Anchor!.Focus(NavigationMethod.Tab);
            FocusRingInput.Press(window, PhysicalKey.ArrowDown);
            Settle(() => menu.IsOpen && ReferenceEquals(menu.Menu.HighlightedEntry, open), "ArrowDown opens on the first row");
            FocusRingInput.Press(window, PhysicalKey.ArrowDown);
            menu.Menu.HighlightedEntry.ShouldBe(export);

            FocusRingInput.Press(window, PhysicalKey.ArrowRight);
            var submenu = export.Submenu.ShouldNotBeNull();
            Settle(() => submenu.IsOpen && csv.IsFocused, "ArrowRight opens the submenu on its first row");
            submenu.HighlightedEntry.ShouldBe(csv);
            ControlAutomationPeer.CreatePeerForElement(export).GetProvider<IExpandCollapseProvider>()!
                .ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);

            FocusRingInput.Press(window, PhysicalKey.ArrowLeft);
            Settle(() => !submenu.IsOpen && export.IsFocused, "ArrowLeft closes the submenu and returns to its row");
            menu.IsOpen.ShouldBeTrue();
            menu.Menu.HighlightedEntry.ShouldBe(export);

            FocusRingInput.Press(window, PhysicalKey.Enter);
            Settle(() => submenu.IsOpen && csv.IsFocused, "Enter opens the submenu on its first row");
            FocusRingInput.Press(window, PhysicalKey.Escape);
            Settle(() => !submenu.IsOpen && export.IsFocused, "Escape closes only the submenu");
            menu.IsOpen.ShouldBeTrue();

            FocusRingInput.Press(window, PhysicalKey.Space);
            Settle(() => submenu.IsOpen && csv.IsFocused, "Space opens the submenu");
            FocusRingInput.Press(window, PhysicalKey.ArrowDown);
            submenu.HighlightedEntry.ShouldBe(pdf);
            FocusRingInput.Press(window, PhysicalKey.Tab);
            Settle(() => !menu.IsOpen && !submenu.IsOpen, "Tab closes every level");
            menu.Anchor.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Activating_A_Row_Three_Levels_Deep_Closes_The_Chain_And_Reports_It_Once()
    {
        var menu = new DropdownMenu { Label = "Actions" };
        var share = new MenuEntry { Header = "Share" };
        var invite = new MenuEntry { Header = "Invite" };
        var byEmail = new MenuEntry { Header = "By email" };
        var byLink = new MenuEntry { Header = "By link" };
        invite.Items.Add(byEmail);
        invite.Items.Add(byLink);
        share.Items.Add(new MenuEntry { Header = "Copy link" });
        share.Items.Add(invite);
        menu.Items.Add(new MenuEntry { Header = "Rename" });
        menu.Items.Add(share);
        var reported = new List<MenuEntry>();
        menu.ItemClicked += (_, e) => reported.Add(e.Entry);
        var surfaceEvents = 0;
        menu.Menu.ItemClicked += (_, _) => surfaceEvents++;
        var commands = 0;
        byEmail.Command = new RelayCommand(() => commands++);
        var window = ShowWide(new StackPanel { Margin = new Thickness(40), Children = { menu } });
        try
        {
            menu.Open();
            Settle(() => menu.IsOpen && share.Bounds.Height > 0, "the menu opens");
            FocusRingInput.Click(window, share.RowVisual);
            Settle(() => share.Submenu is { IsOpen: true } && invite.Bounds.Height > 0, "Share opens");
            var middleEvents = 0;
            share.Submenu!.ItemClicked += (_, _) => middleEvents++;
            FocusRingInput.Click(window, invite.RowVisual);
            Settle(() => invite.Submenu is { IsOpen: true } && byEmail.Bounds.Height > 0, "Invite opens");
            menu.Menu.HighlightedEntry.ShouldBe(share);
            share.Submenu.HighlightedEntry.ShouldBe(invite);

            FocusRingInput.Click(window, byEmail.RowVisual);
            Settle(() => !menu.IsOpen && !share.Submenu.IsOpen && !invite.Submenu!.IsOpen, "the whole chain closes");
            reported.ShouldBe(new[] { byEmail });
            surfaceEvents.ShouldBe(1);
            middleEvents.ShouldBe(0, "the event comes from the outermost surface only");
            commands.ShouldBe(1);
            share.Classes.Contains(MenuEntry.SubmenuOpenPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Presses_Inside_A_Submenu_Keep_The_Menu_Open_And_Presses_Outside_Close_Everything()
    {
        var anchor = new Button { Content = "Actions", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var (surface, _, export, csv, _, _, _) = BuildMenu();
        var window = ShowWide(new StackPanel { Margin = new Thickness(40), Children = { anchor } });
        try
        {
            surface.Open(anchor);
            Settle(() => surface.IsOpen && export.Bounds.Height > 0, "the menu opens");
            FocusRingInput.Click(window, export.RowVisual);
            var submenu = export.Submenu!;
            Settle(() => submenu.IsOpen && csv.Bounds.Height > 0, "the submenu opens");

            // The submenu's inset, outside its rows.
            var frame = BoundsIn(submenu, window);
            FocusRingInput.Click(window, submenu, new Point(frame.Center.X, frame.Bottom - 2));
            surface.IsOpen.ShouldBeTrue();
            submenu.IsOpen.ShouldBeTrue();

            window.MouseDown(new Point(880, 580), MouseButton.Left);
            window.MouseUp(new Point(880, 580), MouseButton.Left);
            Settle(() => !surface.IsOpen && !submenu.IsOpen, "a press outside closes the menu and its submenu");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Submenu_Without_Room_On_The_Right_Opens_On_The_Left()
    {
        var menu = new DropdownMenu { Label = "Actions", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        var export = new MenuEntry { Header = "Export" };
        var csv = new MenuEntry { Header = "Export CSV" };
        var share = new MenuEntry { Header = "Share link" };
        var anyone = new MenuEntry { Header = "Anyone with the link" };
        share.Items.Add(anyone);
        export.Items.Add(csv);
        export.Items.Add(share);
        menu.Items.Add(new MenuEntry { Header = "Rename" });
        menu.Items.Add(export);
        var window = ShowWide(new Panel { Margin = new Thickness(20), Children = { menu } });
        try
        {
            menu.Open();
            Settle(() => menu.IsOpen && export.Bounds.Height > 0, "the menu opens");
            FocusRingInput.Click(window, export.RowVisual);
            var submenu = export.Submenu!;
            Settle(() => submenu.IsOpen && csv.Bounds.Height > 0, "the submenu opens");
            var parent = BoundsIn(menu.Menu, window);
            var sub = BoundsIn(submenu, window);
            sub.Right.ShouldBe(parent.Left - MenuSurface.SubmenuGap, 1.0);
            BoundsIn(csv.RowVisual, window).Top.ShouldBe(BoundsIn(export.RowVisual, window).Top, 1.0);
            submenu.Popup!.LastTravelDirection.ShouldBe(new Vector(1, 0), "it travels in from the menu's side");

            // The next level keeps going left (there would be room on the right, over the menu).
            FocusRingInput.Click(window, share.RowVisual);
            var third = share.Submenu!;
            Settle(() => third.IsOpen && anyone.Bounds.Height > 0, "the third level opens");
            BoundsIn(third, window).Right.ShouldBe(sub.Left - MenuSurface.SubmenuGap, 1.0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Submenu_Near_The_Window_Bottom_Slides_Up_Instead_Of_Running_Off_The_Window()
    {
        var menu = new DropdownMenu { Label = "Actions", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom, Side = MenuSide.Top };
        var recent = new MenuEntry { Header = "Recent" };
        for (var i = 0; i < 9; i++)
        {
            recent.Items.Add(new MenuEntry { Header = $"Document {i + 1}" });
        }
        menu.Items.Add(new MenuEntry { Header = "Open" });
        menu.Items.Add(recent);
        var window = ShowWide(new Panel { Margin = new Thickness(40, 20), Children = { menu } });
        try
        {
            menu.Open();
            Settle(() => menu.IsOpen && recent.Bounds.Height > 0, "the menu opens above its trigger");
            FocusRingInput.Click(window, recent.RowVisual);
            var submenu = recent.Submenu!;
            Settle(() => submenu.IsOpen && submenu.Bounds.Height > 0, "the submenu opens");
            var sub = BoundsIn(submenu, window);
            sub.Bottom.ShouldBeLessThanOrEqualTo(window.ClientSize.Height + 0.5);
            sub.Top.ShouldBeLessThan(BoundsIn(recent.RowVisual, window).Top, "it slid up past its row");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ItemsSource_Data_Nests_Through_The_Children_Selector()
    {
        var a = new Node("a.txt");
        var data = new[]
        {
            new Node("File", new Node("New"), new Node("Recent", a, new Node("b.txt"))),
            new Node("Quit")
        };
        var menu = new DropdownMenu
        {
            Label = "Menu",
            ItemsSource = data,
            ChildrenSelector = o => ((Node)o).Children is { Length: > 0 } children ? children : null
        };
        object? reported = null;
        menu.ItemClicked += (_, e) => reported = e.Item;
        var window = ShowWide(new StackPanel { Margin = new Thickness(40), Children = { menu } });
        try
        {
            menu.Open();
            Settle(() => menu.IsOpen && menu.Menu.ContainerFromIndex(1) is MenuEntry { Bounds.Height: > 0 }, "the menu opens");
            var file = (MenuEntry)menu.Menu.ContainerFromIndex(0)!;
            ((MenuEntry)menu.Menu.ContainerFromIndex(1)!).HasSubmenu.ShouldBeFalse();
            file.HasSubmenu.ShouldBeTrue();
            FocusRingInput.Click(window, file.RowVisual);
            var fileMenu = file.Submenu!;
            Settle(() => fileMenu.IsOpen && fileMenu.ContainerFromIndex(1) is MenuEntry { Bounds.Height: > 0 }, "File opens");
            var recent = (MenuEntry)fileMenu.ContainerFromIndex(1)!;
            recent.HasSubmenu.ShouldBeTrue("the selector applies at every level");
            ((MenuEntry)fileMenu.ContainerFromIndex(0)!).HasSubmenu.ShouldBeFalse();
            FocusRingInput.Click(window, recent.RowVisual);
            var recentMenu = recent.Submenu!;
            Settle(() => recentMenu.IsOpen && recentMenu.ContainerFromIndex(0) is MenuEntry { Bounds.Height: > 0 }, "Recent opens");
            FocusRingInput.Click(window, ((MenuEntry)recentMenu.ContainerFromIndex(0)!).RowVisual);
            Settle(() => !menu.IsOpen, "the leaf closes the menu");
            reported.ShouldBe(a);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Submenu_Rows_Expose_Expand_And_Collapse_To_Assistive_Technology()
    {
        var anchor = new Button { Content = "Actions", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var (surface, rename, export, csv, _, _, _) = BuildMenu();
        var window = ShowWide(new StackPanel { Margin = new Thickness(40), Children = { anchor } });
        try
        {
            surface.Open(anchor);
            Settle(() => surface.IsOpen && export.Bounds.Height > 0, "the menu opens");
            ControlAutomationPeer.CreatePeerForElement(rename).GetProvider<IExpandCollapseProvider>().ShouldBeNull();
            var peer = ControlAutomationPeer.CreatePeerForElement(export);
            peer.GetProvider<IToggleProvider>().ShouldBeNull();
            var expand = peer.GetProvider<IExpandCollapseProvider>().ShouldNotBeNull();
            expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
            expand.Expand();
            Settle(() => export.Submenu is { IsOpen: true } && csv.IsFocused, "Expand opens the submenu on its first row");
            expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
            expand.Collapse();
            Settle(() => export.Submenu!.IsOpen == false, "Collapse closes it");
            expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
            surface.IsOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Static_Menu_Opens_Submenus_And_A_Press_Outside_Closes_Them()
    {
        var (surface, _, export, csv, _, _, _) = BuildMenu();
        var reported = 0;
        surface.ItemClicked += (_, _) => reported++;
        var window = ShowWide(new StackPanel { Margin = new Thickness(20), Children = { surface } });
        try
        {
            FocusRingInput.Click(window, export.RowVisual);
            var submenu = export.Submenu!;
            Settle(() => submenu.IsOpen && csv.Bounds.Height > 0, "the row opens its submenu");
            window.MouseDown(new Point(880, 580), MouseButton.Left);
            window.MouseUp(new Point(880, 580), MouseButton.Left);
            Settle(() => !submenu.IsOpen, "a press outside closes the submenu");

            FocusRingInput.Click(window, export.RowVisual);
            Settle(() => submenu.IsOpen && csv.Bounds.Height > 0, "it opens again");
            FocusRingInput.Click(window, csv.RowVisual);
            Settle(() => !submenu.IsOpen, "activating a submenu row closes the submenu");
            reported.ShouldBe(1);
            surface.IsVisible.ShouldBeTrue("the static menu stays");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Context_Menus_Open_Submenus_And_Report_Their_Rows()
    {
        var target = new Border { Width = 400, Height = 240, Background = Avalonia.Media.Brushes.Transparent };
        var context = new ContextMenu { Content = target };
        var sort = new MenuEntry { Header = "Sort by" };
        var byName = new MenuEntry { Header = "Name" };
        sort.Items.Add(byName);
        sort.Items.Add(new MenuEntry { Header = "Date" });
        context.Menu.Items.Add(new MenuEntry { Header = "Refresh" });
        context.Menu.Items.Add(sort);
        var reported = new List<MenuEntry>();
        context.ItemClicked += (_, e) => reported.Add(e.Entry);
        var window = ShowWide(new StackPanel { Margin = new Thickness(20), Children = { context } });
        try
        {
            context.Open(new Point(30, 30));
            Settle(() => context.IsOpen && sort.Bounds.Height > 0, "the context menu opens");
            FocusRingInput.Click(window, sort.RowVisual);
            var submenu = sort.Submenu!;
            Settle(() => submenu.IsOpen && byName.Bounds.Height > 0, "Sort by opens");
            submenu.Variant.ShouldBe(MenuSurfaceVariant.Context);
            FocusRingInput.Click(window, byName.RowVisual);
            Settle(() => !context.IsOpen && !submenu.IsOpen, "the row closes the menu");
            reported.ShouldBe(new[] { byName });
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Bottom_Sheet_Submenus_Float_Above_Their_Row_As_Panels()
    {
        var anchor = new Button { Content = "Account", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var (surface, _, export, csv, _, _, _) = BuildMenu();
        surface.Variant = MenuSurfaceVariant.Sheet;
        var window = ShowWide(new StackPanel { Margin = new Thickness(40), Children = { anchor } });
        try
        {
            surface.Open(anchor);
            Settle(() => surface.IsOpen && export.Bounds.Height > 0, "the sheet opens");
            export.PerformClick();
            var submenu = export.Submenu!;
            Settle(() => submenu.IsOpen && csv.Bounds.Height > 0, "the submenu opens");
            submenu.Variant.ShouldBe(MenuSurfaceVariant.Account);
            BoundsIn(submenu, window).Bottom.ShouldBeLessThanOrEqualTo(BoundsIn(export.RowVisual, window).Top + 0.5);
            surface.Close(returnFocus: false);
            Settle(() => !submenu.IsOpen, "closing the sheet closes the submenu");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Grows_The_Submenu_From_The_Menu_Side()
    {
        var anchor = new Button { Content = "Actions", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var (surface, _, export, csv, _, _, _) = BuildMenu();
        var window = ShowWide(new StackPanel { Margin = new Thickness(40), Children = { anchor } });
        ArcMotion.SetLevel(window, ArcMotionLevel.Full);
        try
        {
            surface.Open(anchor);
            Settle(() => surface.IsOpen && export.Bounds.Height > 0, "the menu opens");
            export.PerformClick();
            var submenu = export.Submenu!;
            Settle(() => submenu.IsOpen && csv.Bounds.Height > 0, "the submenu opens");
            var popup = submenu.Popup!;
            popup.IsMotionEnabled.ShouldBeTrue();
            popup.LastOrigin.ShouldBe(new RelativePoint(0, 0, RelativeUnit.Relative), "it grows from its top corner by the menu");
            popup.LastTravelDirection.ShouldBe(new Vector(-1, 0));
            popup.IsLightDismissEnabled.ShouldBeFalse("the outermost menu dismisses the chain");
            popup.OpenMotion!.FadeOnly.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Fades_The_Submenu_Without_Travel_Or_Scale()
    {
        var anchor = new Button { Content = "Actions", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var (surface, _, export, csv, _, _, _) = BuildMenu();
        var window = ShowWide(new StackPanel { Margin = new Thickness(40), Children = { anchor } });
        ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
        try
        {
            surface.Open(anchor);
            Settle(() => surface.IsOpen && export.Bounds.Height > 0, "the menu opens");
            export.PerformClick();
            var submenu = export.Submenu!;
            Settle(() => submenu.IsOpen && csv.Bounds.Height > 0, "the submenu opens");
            var popup = submenu.Popup!;
            popup.OpenMotion!.FadeOnly.ShouldBeTrue();
            popup.CloseMotion!.FadeOnly.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class RelayCommand(Action execute) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}
