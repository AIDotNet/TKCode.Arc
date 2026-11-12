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
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Special.Navigation;

/// <summary>
/// The morph nav: the bar and its items, panels and the surface that follows them, hover intent, the folded menu, search,
/// the keyboard model, navigation, automation and the morph's motion.
/// </summary>
public class MorphNavTests
{
    private static MorphNav Nav()
    {
        var nav = new MorphNav { BrandName = "Lantern", ActionLabel = "Get started" };
        var platform = new MorphNavItem { Value = "platform", Label = "Platform" };
        platform.Links.Add(new MorphNavLink { Label = "Notebooks", Description = "Notes in one place", Href = "/notebooks", Icon = ArcIcons.FileText });
        platform.Links.Add(new MorphNavLink { Label = "Maps", Description = "Pin every note", Href = "/maps", Icon = ArcIcons.Globe });
        platform.Links.Add(new MorphNavLink { Label = "Timeline", Href = "/timeline" });
        platform.Links.Add(new MorphNavLink { Label = "Sync", Href = "/sync" });
        platform.Feature = new MorphNavStory { Title = "Tide tables", Description = "Plan around the water", Href = "/tides" };
        platform.Footer = new MorphNavLink { Label = "Exports keep captions", Href = "/changelog" };
        var teams = new MorphNavItem { Value = "teams", Label = "Teams" };
        teams.Links.Add(new MorphNavLink { Label = "Classrooms", Href = "/classrooms" });
        teams.Links.Add(new MorphNavLink { Label = "Research", Href = "/research" });
        var learn = new MorphNavItem { Value = "learn", Label = "Learn" };
        learn.Links.Add(new MorphNavLink { Label = "Guides", Href = "/guides" });
        nav.Items.Add(platform);
        nav.Items.Add(teams);
        nav.Items.Add(learn);
        nav.Items.Add(new MorphNavItem { Value = "pricing", Label = "Pricing", Href = "/pricing" });
        foreach (var (label, group) in new[]
                 {
                     ("Notebooks", "Platform"), ("Maps", "Platform"), ("Pricing", "Company"), ("Guides", "Learn"),
                     ("Changelog", "Learn"), ("Sync", "Platform"), ("About", "Company")
                 })
        {
            nav.SearchEntries.Add(new MorphNavSearchEntry { Label = label, Group = group, Href = "/" + label.ToLowerInvariant() });
        }
        return nav;
    }

    private static Window Show(MorphNav nav, double width = 900, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        // Sized up front: a window that starts narrow folds the bar first, and unfolding it then runs theme transitions.
        var window = new Window { Content = new Panel { Children = { nav } }, Width = width, Height = 600 };
        ArcMotion.SetLevel(window, level);
        window.Show();
        Settle();
        return window;
    }

    private static void Settle()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The surface rounds its size to whole pixels.</summary>
    private static void ShouldMatch(Size actual, Size expected)
    {
        actual.Width.ShouldBe(expected.Width, 0.5);
        actual.Height.ShouldBe(expected.Height, 0.5);
    }

    private static MorphNavTrigger Trigger(MorphNav nav, string value) => nav.Triggers.First(t => t.Item?.Value == value);

    private static List<AvaloniaButton> PanelButtons(MorphNav nav) =>
        nav.OpenFace!.GetVisualDescendants().OfType<AvaloniaButton>().ToList();

    private static void Key(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        Settle();
    }

    [Fact]
    public void Search_suggests_the_first_five_entries_and_ranks_label_matches_first()
    {
        var entries = new List<MorphNavSearchEntry>
        {
            new() { Label = "Planning" },
            new() { Label = "Pricing", Description = "Free and team plans" },
            new() { Label = "Automations" },
            new() { Label = "Data export", Keywords = "csv" },
            new() { Label = "Changelog" },
            new() { Label = "Team spaces" },
            new() { Label = "Integrations" }
        };
        var results = new List<MorphNavSearchEntry>();
        MorphNavSearch.Rank(entries, "", results);
        results.Select(r => r.Label).ShouldBe(["Planning", "Pricing", "Automations", "Data export", "Changelog"]);

        // Label prefix, then a word of the label, then label contains, then description and keywords.
        MorphNavSearch.Rank(entries, "a", results);
        results.Select(r => r.Label).ShouldBe(["Automations", "Planning", "Data export", "Changelog", "Team spaces", "Integrations"]);
        MorphNavSearch.Rank(entries, "sp", results);
        results.Select(r => r.Label).ShouldBe(["Team spaces"]);
        MorphNavSearch.Rank(entries, "csv", results);
        results.Select(r => r.Label).ShouldBe(["Data export"]);
        MorphNavSearch.Rank(entries, "team", results);
        results.Select(r => r.Label).ShouldBe(["Team spaces", "Pricing"]);
        MorphNavSearch.Rank(entries, "zz", results);
        results.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void The_bar_shows_the_brand_the_items_search_and_the_action_and_takes_its_height()
    {
        var nav = Nav();
        var window = Show(nav);
        try
        {
            nav.Bounds.Height.ShouldBe(56, 0.5);
            nav.Triggers.Select(t => t.Label).ShouldBe(["Platform", "Teams", "Learn", "Pricing"]);
            nav.Triggers.Select(t => t.HasPanel).ShouldBe([true, true, true, false]);
            nav.MenuTrigger!.IsVisible.ShouldBeFalse();
            nav.IsCollapsed.ShouldBeFalse();
            nav.SearchButton!.IsVisible.ShouldBeTrue();
            nav.ActionButton!.IsVisible.ShouldBeTrue();
            // Closed, the surface is the bar: centred in the nav.
            var stage = nav.Stage!;
            ShouldMatch(stage.CurrentSize, nav.Bar!.DesiredSize);
            stage.SurfaceRect.Center.X.ShouldBe(nav.Bounds.Width / 2, 0.5);
            stage.CurrentRadius.ShouldBe(28, 0.5);

            nav.SearchEntries.Clear();
            nav.ActionLabel = null;
            Settle();
            nav.SearchButton.IsVisible.ShouldBeFalse();
            nav.ActionButton.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Compact_tightens_the_bar_and_folds_the_brand_name()
    {
        var nav = Nav();
        var window = Show(nav);
        try
        {
            var wide = nav.Bar!.DesiredSize.Width;
            nav.IsCompact = true;
            Settle();
            nav.Bounds.Height.ShouldBe(46, 0.5);
            nav.Bar.DesiredSize.Width.ShouldBeLessThan(wide);
            Trigger(nav, "platform").Bounds.Height.ShouldBe(32, 0.5);
            nav.Brand!.Classes.Contains(MorphNavBrandButton.BarePseudoClass).ShouldBeTrue();
            ShouldMatch(nav.Stage!.CurrentSize, nav.Bar.DesiredSize);
            nav.Stage.CurrentRadius.ShouldBe(23, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_open_panel_grows_the_surface_under_the_bar_and_closing_shrinks_it_back()
    {
        var nav = Nav();
        var window = Show(nav);
        try
        {
            var bar = nav.Bar!.DesiredSize;
            nav.Open("platform");
            Settle();
            nav.OpenValue.ShouldBe("platform");
            Trigger(nav, "platform").IsOpen.ShouldBeTrue();
            nav.Classes.Contains(MorphNav.OpenPseudoClass).ShouldBeTrue();
            var face = nav.OpenFace!;
            var size = nav.Stage!.CurrentSize;
            size.Height.ShouldBe(Math.Round(bar.Height + face.DesiredSize.Height), 0.5);
            size.Width.ShouldBe(Math.Round(Math.Max(bar.Width, face.DesiredSize.Width)), 0.5);
            nav.Stage.CurrentRadius.ShouldBe(22, 0.5);
            // The panel sits under the bar and the nav still takes only the bar's height.
            face.Bounds.Top.ShouldBe(bar.Height, 0.5);
            nav.Bounds.Height.ShouldBe(56, 0.5);
            // Four links in two columns, the feature beside them and the footer under both.
            var body = (MorphNavPanelBody)face.Child!;
            body.Columns.ShouldBe(2);
            body.Links.Count.ShouldBe(4);
            face.GetVisualDescendants().OfType<MorphNavStoryButton>().ShouldHaveSingleItem();
            face.GetVisualDescendants().OfType<MorphNavFooterLinkButton>().ShouldHaveSingleItem();

            nav.Open("teams");
            Settle();
            ((MorphNavPanelBody)nav.OpenFace!.Child!).Columns.ShouldBe(1);
            Trigger(nav, "platform").IsOpen.ShouldBeFalse();

            nav.ClosePanel();
            Settle();
            nav.OpenValue.ShouldBeNull();
            nav.OpenFace.ShouldBeNull();
            ShouldMatch(nav.Stage.CurrentSize, bar);
            nav.Stage.CurrentRadius.ShouldBe(28, 0.5);
            nav.Classes.Contains(MorphNav.OpenPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_panel_never_grows_wider_than_the_nav_less_its_gutter()
    {
        var nav = Nav();
        var window = Show(nav, width: 760);
        try
        {
            nav.Open("platform");
            Settle();
            nav.OpenFace!.Bounds.Width.ShouldBeLessThanOrEqualTo(nav.Bounds.Width - 16 + 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hovering_opens_after_the_intent_delay_follows_to_neighbours_and_leaving_closes()
    {
        var nav = Nav();
        var window = Show(nav);
        try
        {
            Point Center(Control control) => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
            var platform = Trigger(nav, "platform");
            window.MouseMove(Center(platform));
            Settle();
            nav.OpenValue.ShouldBeNull();
            nav.IsOpenPending.ShouldBeTrue();
            nav.CompleteOpenIntent();
            Settle();
            nav.OpenValue.ShouldBe("platform");

            // With a panel open, the next item opens at once.
            window.MouseMove(Center(Trigger(nav, "teams")));
            Settle();
            nav.OpenValue.ShouldBe("teams");

            // A plain item closes the panel.
            window.MouseMove(Center(Trigger(nav, "pricing")));
            Settle();
            nav.OpenValue.ShouldBeNull();

            window.MouseMove(Center(Trigger(nav, "learn")));
            Settle();
            nav.CompleteOpenIntent();
            Settle();
            nav.OpenValue.ShouldBe("learn");
            window.MouseMove(new Point(450, 560));
            Settle();
            nav.IsClosePending.ShouldBeTrue();
            nav.OpenValue.ShouldBe("learn");
            nav.CompleteCloseIntent();
            Settle();
            nav.OpenValue.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_keyboard_walks_the_items_opens_panels_and_returns_focus()
    {
        var nav = Nav();
        var window = Show(nav);
        try
        {
            var focus = window.FocusManager!;
            var platform = Trigger(nav, "platform");
            platform.Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.ArrowRight);
            focus.GetFocusedElement().ShouldBeSameAs(Trigger(nav, "teams"));
            Key(window, PhysicalKey.End);
            focus.GetFocusedElement().ShouldBeSameAs(Trigger(nav, "pricing"));
            Key(window, PhysicalKey.ArrowRight);
            focus.GetFocusedElement().ShouldBeSameAs(platform);
            Key(window, PhysicalKey.ArrowLeft);
            focus.GetFocusedElement().ShouldBeSameAs(Trigger(nav, "pricing"));
            Key(window, PhysicalKey.Home);
            focus.GetFocusedElement().ShouldBeSameAs(platform);
            // The highlight follows keyboard focus.
            nav.Track!.Highlight.HasTarget.ShouldBeTrue();

            Key(window, PhysicalKey.ArrowDown);
            nav.OpenValue.ShouldBe("platform");
            var buttons = PanelButtons(nav);
            focus.GetFocusedElement().ShouldBeSameAs(buttons[0]);
            Key(window, PhysicalKey.ArrowDown);
            focus.GetFocusedElement().ShouldBeSameAs(buttons[1]);
            Key(window, PhysicalKey.ArrowUp);
            Key(window, PhysicalKey.ArrowUp);
            focus.GetFocusedElement().ShouldBeSameAs(platform);
            nav.OpenValue.ShouldBe("platform");

            // Right with a panel open moves the panel along.
            Key(window, PhysicalKey.ArrowRight);
            nav.OpenValue.ShouldBe("teams");
            focus.GetFocusedElement().ShouldBeSameAs(Trigger(nav, "teams"));

            Key(window, PhysicalKey.Enter);
            focus.GetFocusedElement().ShouldBeSameAs(PanelButtons(nav)[0]);
            Key(window, PhysicalKey.Escape);
            nav.OpenValue.ShouldBeNull();
            focus.GetFocusedElement().ShouldBeSameAs(Trigger(nav, "teams"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Every_destination_raises_navigate_and_choosing_one_closes_the_panel()
    {
        var nav = Nav();
        var seen = new List<MorphNavNavigateEventArgs>();
        nav.Navigate += (_, e) => seen.Add(e);
        var window = Show(nav);
        try
        {
            Trigger(nav, "platform").Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.ArrowDown);
            Key(window, PhysicalKey.ArrowDown);
            Key(window, PhysicalKey.Enter);
            seen[^1].Label.ShouldBe("Maps");
            seen[^1].Href.ShouldBe("/maps");
            seen[^1].Section.ShouldBe("platform");
            seen[^1].Destination.ShouldBeSameAs(nav.Items[0].Links[1]);
            nav.OpenValue.ShouldBeNull();
            window.FocusManager!.GetFocusedElement().ShouldBeSameAs(Trigger(nav, "platform"));

            nav.Open("platform");
            Settle();
            var story = nav.OpenFace!.GetVisualDescendants().OfType<MorphNavStoryButton>().Single();
            ((IInvokeProvider)ControlAutomationPeer.CreatePeerForElement(story)).Invoke();
            Settle();
            seen[^1].Destination.ShouldBeSameAs(nav.Items[0].Feature);
            seen[^1].Href.ShouldBe("/tides");
            nav.OpenValue.ShouldBeNull();

            nav.Open("platform");
            Settle();
            var footer = nav.OpenFace!.GetVisualDescendants().OfType<MorphNavFooterLinkButton>().Single();
            ((IInvokeProvider)ControlAutomationPeer.CreatePeerForElement(footer)).Invoke();
            Settle();
            seen[^1].Label.ShouldBe("Exports keep captions");
            nav.OpenValue.ShouldBeNull();

            ((IInvokeProvider)ControlAutomationPeer.CreatePeerForElement(Trigger(nav, "pricing"))).Invoke();
            Settle();
            seen[^1].Section.ShouldBe("pricing");
            seen[^1].Href.ShouldBe("/pricing");
            seen[^1].Destination.ShouldBeSameAs(nav.Items[3]);

            ((IInvokeProvider)ControlAutomationPeer.CreatePeerForElement(nav.Brand!)).Invoke();
            Settle();
            seen[^1].Label.ShouldBe("Lantern");
            seen[^1].Section.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_button_in_a_custom_feature_closes_the_panel()
    {
        var nav = Nav();
        var button = new Button { Content = "Read more" };
        nav.Items[1].Feature = button;
        var window = Show(nav);
        try
        {
            nav.Open("teams");
            Settle();
            button.IsEffectivelyVisible.ShouldBeTrue();
            button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Settle();
            nav.OpenValue.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Search_grows_out_of_the_bar_ranks_as_you_type_and_opens_the_active_result()
    {
        var nav = Nav();
        MorphNavNavigateEventArgs? chosen = null;
        nav.Navigate += (_, e) => chosen = e;
        var window = Show(nav);
        try
        {
            nav.Open("platform");
            Settle();
            nav.OpenSearch();
            Settle();
            nav.IsSearchOpen.ShouldBeTrue();
            nav.OpenValue.ShouldBeNull();
            nav.Classes.Contains(MorphNav.SearchingPseudoClass).ShouldBeTrue();
            var input = nav.SearchBar!.Input!;
            window.FocusManager!.GetFocusedElement().ShouldBeSameAs(input);
            nav.Results.Select(r => r.Label).ShouldBe(["Notebooks", "Maps", "Pricing", "Guides", "Changelog"]);
            nav.SearchBody!.Heading.Text.ShouldBe("Suggested");
            nav.SearchBody.ActiveIndex.ShouldBe(0);
            // The surface takes the search width and the results' height.
            nav.Stage!.CurrentSize.Width.ShouldBe(544, 0.5);
            nav.Stage.CurrentSize.Height.ShouldBe(Math.Round(56 + nav.SearchFace!.DesiredSize.Height), 0.5);

            input.Text = "s";
            Settle();
            nav.Results.Select(r => r.Label).ShouldBe(["Sync", "Notebooks", "Maps", "Guides"]);
            nav.SearchBody.Heading.Text.ShouldBe("4 results");
            input.Text = "zz";
            Settle();
            nav.Results.ShouldBeEmpty();
            nav.SearchBody.Heading.Text.ShouldBe("No results for “zz”");
            input.Text = "ab";
            Settle();
            nav.SearchBody.Heading.Text.ShouldBe("1 result");

            input.Text = "";
            Settle();
            Key(window, PhysicalKey.ArrowDown);
            Key(window, PhysicalKey.ArrowDown);
            nav.SearchBody.ActiveIndex.ShouldBe(2);
            Key(window, PhysicalKey.ArrowUp);
            Key(window, PhysicalKey.Enter);
            chosen!.Label.ShouldBe("Maps");
            chosen.Destination.ShouldBeSameAs(nav.SearchEntries[1]);
            chosen.Section.ShouldBeNull();
            nav.IsSearchOpen.ShouldBeFalse();
            window.FocusManager.GetFocusedElement().ShouldBeSameAs(nav.SearchButton);

            nav.OpenSearch();
            Settle();
            Key(window, PhysicalKey.Escape);
            nav.IsSearchOpen.ShouldBeFalse();
            window.FocusManager.GetFocusedElement().ShouldBeSameAs(nav.SearchButton);
            ShouldMatch(nav.Stage.CurrentSize, nav.Bar!.DesiredSize);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_narrow_nav_folds_its_items_into_one_menu()
    {
        var nav = Nav();
        var window = Show(nav, width: 520);
        try
        {
            nav.IsCollapsed.ShouldBeTrue();
            nav.Classes.Contains(MorphNav.CollapsedPseudoClass).ShouldBeTrue();
            nav.MenuTrigger!.IsVisible.ShouldBeTrue();
            nav.MenuTrigger.Label.ShouldBe("Menu");
            nav.Triggers.ShouldAllBe(t => !t.IsVisible);
            nav.Brand!.Classes.Contains(MorphNavBrandButton.BarePseudoClass).ShouldBeTrue();
            nav.Open("platform");
            Settle();
            var menu = (MorphNavMenuBody)nav.OpenFace!.Child!;
            // One group per item with a panel, then the plain items.
            menu.Children.Count.ShouldBe(4);
            nav.OpenFace.GetVisualDescendants().OfType<MorphNavHeading>().Select(h => h.Text).ShouldBe(["Platform", "Teams", "Learn"]);
            var links = nav.OpenFace.GetVisualDescendants().OfType<MorphNavLinkButton>().ToList();
            links.Count.ShouldBe(8);
            links.ShouldAllBe(l => l.Classes.Contains(MorphNavLinkButton.DensePseudoClass));
            links[^1].Label.ShouldBe("Pricing");

            window.Width = 900;
            Settle();
            nav.IsCollapsed.ShouldBeFalse();
            nav.OpenValue.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_current_item_is_marked_and_announced()
    {
        var nav = Nav();
        nav.Current = "teams";
        var window = Show(nav);
        try
        {
            Trigger(nav, "teams").IsCurrent.ShouldBeTrue();
            Trigger(nav, "teams").Classes.Contains(MorphNavTrigger.CurrentPseudoClass).ShouldBeTrue();
            AutomationProperties.GetHelpText(Trigger(nav, "teams")).ShouldBe("Current page");
            Trigger(nav, "platform").IsCurrent.ShouldBeFalse();
            nav.Current = "pricing";
            Trigger(nav, "teams").IsCurrent.ShouldBeFalse();
            Trigger(nav, "pricing").IsCurrent.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Automation_reports_the_landmark_expanding_items_links_and_the_search_combo_box()
    {
        var nav = Nav();
        var window = Show(nav);
        try
        {
            AutomationProperties.GetName(nav).ShouldBe("Main");
            AutomationProperties.GetLandmarkType(nav).ShouldBe(AutomationLandmarkType.Navigation);
            var platform = ControlAutomationPeer.CreatePeerForElement(Trigger(nav, "platform"));
            platform.GetName().ShouldBe("Platform");
            var expand = (IExpandCollapseProvider)platform;
            expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
            expand.Expand();
            Settle();
            nav.OpenValue.ShouldBe("platform");
            expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
            AutomationProperties.GetLabeledBy(nav.OpenFace!).ShouldBeSameAs(Trigger(nav, "platform"));
            var link = ControlAutomationPeer.CreatePeerForElement(PanelButtons(nav)[0]);
            link.GetAutomationControlType().ShouldBe(AutomationControlType.Hyperlink);
            link.GetHelpText().ShouldBe("Notes in one place");
            expand.Collapse();
            Settle();
            nav.OpenValue.ShouldBeNull();
            ControlAutomationPeer.CreatePeerForElement(Trigger(nav, "pricing")).GetAutomationControlType()
                .ShouldBe(AutomationControlType.Hyperlink);

            nav.OpenSearch();
            Settle();
            ControlAutomationPeer.CreatePeerForElement(nav.SearchBar!.Input!).GetAutomationControlType()
                .ShouldBe(AutomationControlType.ComboBox);
            AutomationProperties.GetName(nav.SearchBar.Input!).ShouldBe("Search");
            AutomationProperties.GetLiveSetting(nav.SearchBody!.Heading).ShouldBe(AutomationLiveSetting.Polite);
            var row = nav.SearchBody.Rows.First();
            var rowPeer = ControlAutomationPeer.CreatePeerForElement(row);
            rowPeer.GetAutomationControlType().ShouldBe(AutomationControlType.ListItem);
            ((ISelectionItemProvider)rowPeer).IsSelected.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_press_outside_closes_the_panel_and_search()
    {
        var outside = new Border { Width = 200, Height = 100, Background = Avalonia.Media.Brushes.Gray, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom };
        var nav = Nav();
        var window = TestHelpers.Show(new Panel { Children = { outside, nav } });
        window.Width = 900;
        window.Height = 600;
        Settle();
        try
        {
            nav.Open("teams");
            Settle();
            var point = outside.TranslatePoint(new Point(100, 50), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Settle();
            nav.OpenValue.ShouldBeNull();
            nav.OpenSearch();
            Settle();
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Settle();
            nav.IsSearchOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_surface_springs_to_a_panel_and_a_neighbours_panel_slides_in_from_its_side()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var nav = Nav();
        var window = Show(nav, level: ArcMotionLevel.Full);
        try
        {
            void Frames(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    clock.Advance(TimeSpan.FromMilliseconds(16));
                    Dispatcher.UIThread.RunJobs();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                }
            }

            Frames(4);
            var bar = nav.Bar!.DesiredSize;
            nav.Open("platform");
            Frames(2);
            var stage = nav.Stage!;
            stage.IsMorphing.ShouldBeTrue();
            var target = stage.TargetSize;
            target.Height.ShouldBeGreaterThan(bar.Height + 100);
            double highest = 0;
            for (var i = 0; i < 60; i++)
            {
                Frames(1);
                highest = Math.Max(highest, stage.CurrentSize.Height);
            }
            stage.CurrentSize.Height.ShouldBe(target.Height, 0.5);
            // Growing bounces a little, never far.
            highest.ShouldBeLessThan(target.Height + 3);
            var first = nav.OpenFace!;
            first.Opacity.ShouldBe(1, 0.01);

            nav.Open("teams");
            Frames(1);
            var second = nav.OpenFace!;
            second.Motion.Offset.X.ShouldBeGreaterThan(10);
            first.Motion.Offset.X.ShouldBeLessThanOrEqualTo(0);
            Frames(60);
            second.Motion.Offset.X.ShouldBe(0, 0.5);
            first.GetVisualParent().ShouldBeNull();

            nav.ClosePanel();
            Frames(80);
            ShouldMatch(stage.CurrentSize, bar);
            second.GetVisualParent().ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }
}
