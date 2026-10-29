using System.Windows.Input;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Blocks;

public class PageHeaderTests
{
    private static PageHeader Create(double width = 1200, double height = 560, double bodyHeight = 2000)
    {
        var header = new PageHeader
        {
            Title = "Checkout redesign",
            Status = "On track",
            Description = "Rebuilding mobile checkout around saved payment methods.",
            Breadcrumbs = new[] { "Northline", "Projects", "Checkout redesign" },
            Width = width,
            Height = height
        };
        header.Items.Add(new PageHeaderTab { Header = "Overview", Content = new Border { Height = bodyHeight, Child = new TextBlock { Text = "Overview body" } } });
        header.Items.Add(new PageHeaderTab { Header = "Issues", Count = 9, Content = new Border { Height = bodyHeight, Child = new TextBlock { Text = "Issues body" } } });
        header.Items.Add(new PageHeaderTab { Header = "Files", Count = 8, Content = new Border { Height = bodyHeight, Child = new TextBlock { Text = "Files body" } } });
        return header;
    }

    private static PageHeader WithActions(PageHeader header, out PageHeaderAction follow, out PageHeaderAction share,
                                          out PageHeaderAction copy, out PageHeaderAction create)
    {
        follow = new PageHeaderAction { Label = "Follow", Icon = ArcIcons.Bell };
        share = new PageHeaderAction { Label = "Share update", Icon = ArcIcons.Share2 };
        copy = new PageHeaderAction { Label = "Copy link", Icon = ArcIcons.Link2 };
        create = new PageHeaderAction { Label = "New issue", Icon = ArcIcons.Plus };
        header.Actions.Add(follow);
        header.Actions.Add(share);
        header.MoreActions.Add(copy);
        header.PrimaryAction = create;
        return header;
    }

    private static void Pump() => Dispatcher.UIThread.RunJobs();

    /// <summary>A window wide enough for the header (menus only open against anchors inside the window).</summary>
    private static Window Show(PageHeader header, Motion.ArcMotionLevel level = Motion.ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(header, level);
        window.Width = 1300;
        window.Height = 800;
        Pump();
        return window;
    }

    private static ScrollViewer Scroller(PageHeader header) => TestHelpers.Descendant<ScrollViewer>(header, PageHeader.ScrollerPart);

    private static PageHeaderBody Body(PageHeader header) => TestHelpers.Descendant<PageHeaderBody>(header, PageHeader.BodyPart);

    private static string? BodyText(PageHeader header)
        => (Body(header).CurrentPresenter.Content as Border)?.Child is TextBlock text ? text.Text : null;

    private static Button More(PageHeader header) => TestHelpers.Descendant<Button>(header, PageHeader.MoreButtonPart);

    private static Button Primary(PageHeader header) => TestHelpers.Descendant<Button>(header, PageHeader.PrimaryButtonPart);

    private static PageHeaderFoldSlot Slot(PageHeader header) => TestHelpers.Descendant<PageHeaderFoldSlot>(header, PageHeader.SecondarySlotPart);

    private static List<Button> SecondaryButtons(PageHeader header)
        => TestHelpers.Descendant<Panel>(header, PageHeader.SecondaryPanelPart).Children.OfType<Button>().ToList();

    private static string? Label(Button button)
        => (button.Content as Panel)?.Children.OfType<TextBlock>().FirstOrDefault()?.Text;

    private static void ScrollTo(PageHeader header, double y)
    {
        var scroller = Scroller(header);
        scroller.Offset = new Vector(0, y);
        Pump();
    }

    [AvaloniaFact]
    public void Defaults_And_Template_Parts()
    {
        var header = Create();
        var window = Show(header);
        try
        {
            header.StatusTone.ShouldBe(BadgeTone.Success);
            header.MoreActionsLabel.ShouldBe("More actions");
            header.BackToTopLabel.ShouldBe("back to top");
            header.TabsLabel.ShouldBe("Sections");
            header.IsCondensed.ShouldBeFalse();
            header.AreActionsFolded.ShouldBeFalse();
            header.SelectedIndex.ShouldBe(0);
            header.CornerRadius.ShouldBe(new CornerRadius(16));

            TestHelpers.Descendant<TextBlock>(header, PageHeader.TitlePart).Text.ShouldBe("Checkout redesign");
            TestHelpers.Descendant<TextBlock>(header, PageHeader.TitlePart).FontSize.ShouldBe(36);
            AutomationProperties.GetHeadingLevel(TestHelpers.Descendant<TextBlock>(header, PageHeader.TitlePart)).ShouldBe(2);
            AutomationProperties.GetName(header).ShouldBe("Checkout redesign");
            var badge = TestHelpers.Descendant<Badge>(header, "PART_Status");
            badge.Content.ShouldBe("On track");
            badge.Tone.ShouldBe(BadgeTone.Success);
            var crumbs = TestHelpers.Descendant<Breadcrumb>(header, PageHeader.BreadcrumbPart);
            crumbs.ItemCount.ShouldBe(3);
            crumbs.Opacity.ShouldBe(1);
            var compact = TestHelpers.Descendant<PageHeaderCompactTitle>(header, PageHeader.CompactTitlePart);
            compact.Opacity.ShouldBe(0);
            compact.IsTabStop.ShouldBeFalse();
            AutomationProperties.GetName(compact).ShouldBe("Checkout redesign, back to top");

            BodyText(header).ShouldBe("Overview body");
            var indicator = TestHelpers.Descendant<GlidingIndicator>(header, PageHeader.IndicatorPart);
            indicator.HasTarget.ShouldBeTrue();
            var first = (PageHeaderTab)header.ContainerFromIndex(0)!;
            first.Bounds.Height.ShouldBe(44);
            indicator.CurrentRect.Height.ShouldBe(2);
            indicator.CurrentRect.Width.ShouldBe(first.Bounds.Width - 20, 0.01);
            first.IsSelected.ShouldBeTrue();

            // No actions: no overflow button and no primary button.
            More(header).IsVisible.ShouldBeFalse();
            Primary(header).IsVisible.ShouldBeFalse();
            SecondaryButtons(header).ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tab_Counts_Roll_And_Name_The_Tab()
    {
        var header = Create();
        var window = Show(header);
        try
        {
            var overview = (PageHeaderTab)header.ContainerFromIndex(0)!;
            var issues = (PageHeaderTab)header.ContainerFromIndex(1)!;
            TestHelpers.Descendant<RollingDigits>(overview, PageHeaderTab.CountPart).IsVisible.ShouldBeFalse();
            var digits = TestHelpers.Descendant<RollingDigits>(issues, PageHeaderTab.CountPart);
            digits.IsVisible.ShouldBeTrue();
            digits.Value.ShouldBe(9);
            AutomationProperties.GetName(issues).ShouldBe("Issues, 9");
            issues.Count = 10;
            Pump();
            digits.Value.ShouldBe(10);
            AutomationProperties.GetName(issues).ShouldBe("Issues, 10");
            issues.Count = null;
            Pump();
            digits.IsVisible.ShouldBeFalse();
            issues.Classes.Contains(PageHeaderTab.HasCountPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Select_Tabs_Skip_Disabled_And_Loop()
    {
        var header = Create();
        header.Items.Add(new PageHeaderTab { Header = "Settings", IsEnabled = false, Content = new TextBlock() });
        var window = Show(header);
        try
        {
            var changes = 0;
            header.SelectionChanged += (_, _) => changes++;
            header.ContainerFromIndex(0)!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Pump();
            header.SelectedIndex.ShouldBe(1);
            BodyText(header).ShouldBe("Issues body");
            header.ContainerFromIndex(1)!.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Pump();
            // Settings is disabled: Files, then back to Overview.
            header.SelectedIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Pump();
            header.SelectedIndex.ShouldBe(2);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Pump();
            header.SelectedIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Pump();
            header.SelectedIndex.ShouldBe(2);
            changes.ShouldBe(6);

            // Only the selected tab is a Tab stop.
            header.ContainerFromIndex(2)!.IsTabStop.ShouldBeTrue();
            header.ContainerFromIndex(0)!.IsTabStop.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Condenses_Past_16_And_Opens_Again_Only_Within_4()
    {
        var header = Create();
        var window = Show(header);
        try
        {
            var condensed = new List<bool>();
            header.PropertyChanged += (_, e) =>
            {
                if (e.Property == PageHeader.IsCondensedProperty)
                {
                    condensed.Add((bool)e.NewValue!);
                }
            };
            ScrollTo(header, 16);
            header.IsCondensed.ShouldBeFalse();
            ScrollTo(header, 17);
            header.IsCondensed.ShouldBeTrue();
            header.Classes.Contains(PageHeader.CondensedPseudoClass).ShouldBeTrue();
            ScrollTo(header, 10);
            header.IsCondensed.ShouldBeTrue();
            ScrollTo(header, 5);
            header.IsCondensed.ShouldBeTrue();
            ScrollTo(header, 4);
            header.IsCondensed.ShouldBeFalse();
            condensed.ShouldBe([true, false]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Condensed_Header_Shows_The_Compact_Bar_And_Hides_The_Intro()
    {
        var header = Create();
        var window = Show(header);
        try
        {
            ScrollTo(header, 200);
            header.IsCondensed.ShouldBeTrue();
            var intro = TestHelpers.Descendant<SpringSizeDecorator>(header, PageHeader.IntroPart);
            intro.IsExpanded.ShouldBeFalse();
            intro.Bounds.Height.ShouldBe(0);
            var crumbs = TestHelpers.Descendant<Breadcrumb>(header, PageHeader.BreadcrumbPart);
            crumbs.Opacity.ShouldBe(0);
            crumbs.IsHitTestVisible.ShouldBeFalse();
            var compact = TestHelpers.Descendant<PageHeaderCompactTitle>(header, PageHeader.CompactTitlePart);
            compact.Opacity.ShouldBe(1);
            compact.IsTabStop.ShouldBeTrue();
            compact.ShowsStatus.ShouldBeTrue();
            TestHelpers.Descendant<Control>(header, PageHeader.TitleRowPart).Opacity.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Short_Body_Never_Condenses_Into_A_Flicker()
    {
        // The body is only a little taller than the expanded viewport: condensing would clamp it back to the top.
        var header = Create(bodyHeight: 0);
        var window = Show(header);
        try
        {
            var scroller = Scroller(header);
            var body = (Border)((PageHeaderTab)header.ContainerFromIndex(0)!).Content!;
            body.Height = scroller.Viewport.Height - 112 + 40;
            Pump();
            ScrollTo(header, 30);
            scroller.Offset.Y.ShouldBeGreaterThan(16);
            header.IsCondensed.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switching_Tabs_While_Condensed_Shows_The_Top_And_Keeps_The_Bar()
    {
        var header = Create();
        var window = Show(header);
        try
        {
            ScrollTo(header, 400);
            header.IsCondensed.ShouldBeTrue();
            header.SelectedIndex = 1;
            Pump();
            Scroller(header).Offset.Y.ShouldBe(PageHeader.CondenseAt + 1);
            header.IsCondensed.ShouldBeTrue();
            BodyText(header).ShouldBe("Issues body");

            ScrollTo(header, 300);
            header.RevealTab(1).ShouldBeTrue();
            Pump();
            Scroller(header).Offset.Y.ShouldBe(PageHeader.CondenseAt + 1);
            header.RevealTab(7).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Compact_Title_Scrolls_To_Top_And_Focuses_The_Title()
    {
        var header = Create();
        var window = Show(header);
        try
        {
            ScrollTo(header, 300);
            header.IsCondensed.ShouldBeTrue();
            var compact = TestHelpers.Descendant<PageHeaderCompactTitle>(header, PageHeader.CompactTitlePart);
            compact.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            Scroller(header).Offset.Y.ShouldBe(0);
            header.IsCondensed.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(header, PageHeader.TitlePart).IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Actions_Render_As_Buttons_And_Raise_Click_Command_And_ActionInvoked()
    {
        var header = WithActions(Create(), out var follow, out _, out _, out var create);
        var executed = new List<object?>();
        follow.Command = new RelayCommand(p => executed.Add(p));
        follow.CommandParameter = "follow";
        var window = Show(header);
        try
        {
            var clicks = 0;
            follow.Click += (_, _) => clicks++;
            var invoked = new List<PageHeaderActionEventArgs>();
            header.ActionInvoked += (_, e) => invoked.Add(e);

            var buttons = SecondaryButtons(header);
            buttons.Select(Label).ShouldBe(["Follow", "Share update"]);
            buttons[0].Variant.ShouldBe(ButtonVariant.Secondary);
            buttons[0].Size.ShouldBe(ControlSize.Small);
            AutomationProperties.GetName(buttons[0]).ShouldBe("Follow");
            buttons[0].RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            clicks.ShouldBe(1);
            executed.ShouldBe(["follow"]);
            invoked.Single().Action.ShouldBe(follow);
            invoked.Single().IsFromMenu.ShouldBeFalse();

            var primary = Primary(header);
            primary.IsVisible.ShouldBeTrue();
            primary.Variant.ShouldBe(ButtonVariant.Primary);
            Label(primary).ShouldBe("New issue");
            AutomationProperties.GetName(primary).ShouldBe("New issue");
            primary.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            invoked.Last().Action.ShouldBe(create);

            // The overflow button holds the menu of MoreActions.
            More(header).IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(More(header)).ShouldBe("More actions");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Loading_And_Command_State_Reach_The_Buttons()
    {
        var header = WithActions(Create(), out var follow, out var share, out _, out var create);
        var command = new RelayCommand(_ => { }) { CanRun = false };
        create.Command = command;
        var window = Show(header);
        try
        {
            Primary(header).IsEnabled.ShouldBeFalse();
            command.CanRun = true;
            Primary(header).IsEnabled.ShouldBeTrue();

            follow.IsEnabled = false;
            SecondaryButtons(header)[0].IsEnabled.ShouldBeFalse();

            var clicks = 0;
            share.Click += (_, _) => clicks++;
            share.IsLoading = true;
            var shareButton = SecondaryButtons(header)[1];
            shareButton.IsLoading.ShouldBeTrue();
            shareButton.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            clicks.ShouldBe(0);
            share.IsLoading = false;
            shareButton.IsLoading.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Label_Change_Swaps_The_Button_Content_In_Place()
    {
        var header = WithActions(Create(), out var follow, out _, out _, out _);
        var window = Show(header);
        try
        {
            var button = SecondaryButtons(header)[0];
            var before = button.Content;
            follow.Label = "Following";
            follow.Icon = ArcIcons.Check;
            Pump();
            SecondaryButtons(header)[0].ShouldBeSameAs(button);
            button.Content.ShouldNotBeSameAs(before);
            Label(button).ShouldBe("Following");
            ((Panel)button.Content!).Children.OfType<ArcGlyph>().Single().Data.ShouldBe(ArcIcons.Check);
            AutomationProperties.GetName(button).ShouldBe("Following");
            header.AreActionsFolded.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Secondary_Actions_Fold_At_240_And_Return_At_272()
    {
        var header = WithActions(Create(), out _, out _, out _, out _);
        var window = Show(header);
        try
        {
            header.AreActionsFolded.ShouldBeFalse();
            var slot = Slot(header);
            var trailing = TestHelpers.Descendant<Panel>(header, PageHeader.TrailingPart);
            var bar = TestHelpers.Descendant<Grid>(header, PageHeader.BarPart);
            // Frame width = bar width + 2 × 28 padding + 2 × 1 border.
            var chrome = header.Bounds.Width - bar.Bounds.Width;
            var fixedWidth = slot.NaturalWidth + trailing.Bounds.Width + 16 + chrome;
            (fixedWidth + PageHeader.LeadMin).ShouldBeGreaterThan(PageHeader.NarrowWidth);

            header.Width = fixedWidth + PageHeader.LeadMin - 2;
            Pump();
            header.AreActionsFolded.ShouldBeTrue();
            header.Classes.Contains(PageHeader.ActionsFoldedPseudoClass).ShouldBeTrue();
            slot.IsFolded.ShouldBeTrue();
            slot.Bounds.Width.ShouldBe(0);
            slot.IsHitTestVisible.ShouldBeFalse();

            // Between 240 and 272 it stays folded ...
            header.Width = fixedWidth + PageHeader.LeadReturn - 4;
            Pump();
            header.AreActionsFolded.ShouldBeTrue();
            // ... and returns with a little slack.
            header.Width = fixedWidth + PageHeader.LeadReturn + 2;
            Pump();
            header.AreActionsFolded.ShouldBeFalse();
            slot.Bounds.Width.ShouldBe(slot.NaturalWidth, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Folded_Actions_Lead_The_Menu_And_Run_From_It()
    {
        var header = WithActions(Create(width: 600), out var follow, out var share, out var copy, out _);
        follow.MenuLabel = "Unfollow";
        share.IsEnabled = false;
        var window = Show(header);
        try
        {
            header.AreActionsFolded.ShouldBeTrue();
            var menu = DropdownMenu.GetMenu(More(header))!;
            var entries = menu.Items.OfType<MenuEntry>().ToList();
            entries.Select(e => e.Header).ShouldBe(["Unfollow", "Share update", "Copy link"]);
            entries[0].Icon.ShouldBe(ArcIcons.Bell);
            entries[1].IsEnabled.ShouldBeFalse();
            entries[2].HasSeparatorBefore.ShouldBeTrue();

            var invoked = new List<PageHeaderActionEventArgs>();
            header.ActionInvoked += (_, e) => invoked.Add(e);
            var clicks = 0;
            copy.Click += (_, _) => clicks++;
            More(header).RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            Pump();
            menu.IsOpen.ShouldBeTrue();
            entries[2].PerformClick();
            Pump();
            clicks.ShouldBe(1);
            invoked.Single().Action.ShouldBe(copy);
            invoked.Single().IsFromMenu.ShouldBeTrue();

            // Unfolding takes the secondary actions back out of the menu.
            header.Width = 1000;
            Pump();
            header.AreActionsFolded.ShouldBeFalse();
            menu.Items.OfType<MenuEntry>().Select(e => e.Header).ShouldBe(["Copy link"]);
            menu.Items.OfType<MenuEntry>().Single().HasSeparatorBefore.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Action_Collections_Can_Change()
    {
        var header = Create();
        var window = Show(header);
        try
        {
            var follow = new PageHeaderAction { Label = "Follow" };
            header.Actions.Add(follow);
            Pump();
            SecondaryButtons(header).Select(Label).ShouldBe(["Follow"]);
            More(header).IsVisible.ShouldBeFalse();
            var archive = new PageHeaderAction { Label = "Archive project", IsDestructive = true };
            header.MoreActions.Add(archive);
            Pump();
            More(header).IsVisible.ShouldBeTrue();
            DropdownMenu.GetMenu(More(header))!.Items.OfType<MenuEntry>().Single().IsDestructive.ShouldBeTrue();
            archive.Label = "Restore project";
            DropdownMenu.GetMenu(More(header))!.Items.OfType<MenuEntry>().Single().Header.ShouldBe("Restore project");
            header.Actions.Clear();
            header.MoreActions.Clear();
            Pump();
            SecondaryButtons(header).ShouldBeEmpty();
            More(header).IsVisible.ShouldBeFalse();
            header.PrimaryAction = new PageHeaderAction { Label = "Publish" };
            Pump();
            Primary(header).IsVisible.ShouldBeTrue();
            header.PrimaryAction = null;
            Pump();
            Primary(header).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Breadcrumb_Click_Forwards_The_Item_And_Its_Index()
    {
        var header = Create();
        var window = Show(header);
        try
        {
            var clicks = new List<PageHeaderBreadcrumbClickEventArgs>();
            header.BreadcrumbClick += (_, e) => clicks.Add(e);
            var crumbs = TestHelpers.Descendant<Breadcrumb>(header, PageHeader.BreadcrumbPart);
            var projects = (BreadcrumbItem)crumbs.ContainerFromIndex(1)!;
            projects.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            clicks.Single().Item.ShouldBe("Projects");
            clicks.Single().Index.ShouldBe(1);
            ((BreadcrumbItem)crumbs.ContainerFromIndex(2)!).IsCurrent.ShouldBeTrue();

            // Narrow: the root crumb is left out, but indexes still refer to Breadcrumbs.
            header.Width = 500;
            Pump();
            crumbs.ItemCount.ShouldBe(2);
            ((BreadcrumbItem)crumbs.ContainerFromIndex(0)!).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            clicks.Last().Item.ShouldBe("Projects");
            clicks.Last().Index.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Widths_Tighten_The_Layout()
    {
        var header = WithActions(Create(), out _, out _, out _, out _);
        var source = new AvaloniaList<string> { "Northline", "Projects", "Checkout redesign" };
        header.Breadcrumbs = source;
        var window = Show(header);
        try
        {
            header.Classes.Contains(PageHeader.NarrowPseudoClass).ShouldBeFalse();
            var title = TestHelpers.Descendant<TextBlock>(header, PageHeader.TitlePart);
            var compact = TestHelpers.Descendant<PageHeaderCompactTitle>(header, PageHeader.CompactTitlePart);

            header.Width = 540;
            Pump();
            header.Classes.Contains(PageHeader.NarrowPseudoClass).ShouldBeTrue();
            header.Classes.Contains(PageHeader.TightPseudoClass).ShouldBeFalse();
            title.FontSize.ShouldBe(28);
            compact.IsStatusVisible.ShouldBeFalse();
            compact.ShowsStatus.ShouldBeFalse();
            TestHelpers.Descendant<StackPanel>(header, "PART_HeaderContent").Margin.Left.ShouldBe(18);
            var crumbs = TestHelpers.Descendant<Breadcrumb>(header, PageHeader.BreadcrumbPart);
            crumbs.ItemCount.ShouldBe(2);
            source.Insert(1, "Teams");
            Pump();
            crumbs.ItemCount.ShouldBe(3);
            TestHelpers.Descendant<ScrollViewer>(header, PageHeader.TabsViewportPart).OpacityMask.ShouldNotBeNull();
            Label(Primary(header)).ShouldBe("New issue");
            Primary(header).Classes.Contains("icon-only").ShouldBeFalse();

            header.Width = 440;
            Pump();
            header.Classes.Contains(PageHeader.TightPseudoClass).ShouldBeTrue();
            var primary = Primary(header);
            primary.Classes.Contains("icon-only").ShouldBeTrue();
            primary.Bounds.Width.ShouldBe(36);
            ((Panel)primary.Content!).Children.OfType<TextBlock>().Single().IsVisible.ShouldBeFalse();
            AutomationProperties.GetName(primary).ShouldBe("New issue");

            header.Width = 900;
            Pump();
            header.Classes.Contains(PageHeader.NarrowPseudoClass).ShouldBeFalse();
            title.FontSize.ShouldBe(36);
            crumbs.ItemCount.ShouldBe(4);
            TestHelpers.Descendant<ScrollViewer>(header, PageHeader.TabsViewportPart).OpacityMask.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Header_Starts_With_Its_Actions_Folded()
    {
        var header = WithActions(Create(width: 520), out _, out _, out _, out _);
        var window = Show(header);
        try
        {
            header.AreActionsFolded.ShouldBeTrue();
            Slot(header).Bounds.Width.ShouldBe(0);
            Slot(header).Opacity.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tabs_And_Data_Items_Can_Change()
    {
        var header = new PageHeader
        {
            Title = "API",
            Width = 800,
            Height = 400,
            ItemTemplate = new FuncDataTemplate<string>((name, _) => new TextBlock { Text = name }),
            ContentTemplate = new FuncDataTemplate<string>((name, _) => new TextBlock { Text = name + " body" })
        };
        var items = new AvaloniaList<string> { "Overview", "Endpoints" };
        header.ItemsSource = items;
        var window = Show(header);
        try
        {
            header.SelectedIndex.ShouldBe(0);
            var shown = () => TestHelpers.Descendant<TextBlock>(Body(header).CurrentPresenter).Text;
            shown().ShouldBe("Overview body");
            items.Add("Errors");
            Pump();
            header.ItemCount.ShouldBe(3);
            header.SelectedIndex = 2;
            Pump();
            shown().ShouldBe("Errors body");
            items.RemoveAt(2);
            Pump();
            header.SelectedIndex.ShouldBeInRange(0, 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Empty_Header_Shows_Only_What_It_Has()
    {
        var header = new PageHeader { Width = 600, Height = 300 };
        var window = Show(header);
        try
        {
            header.SelectedIndex.ShouldBe(-1);
            TestHelpers.Descendant<ScrollViewer>(header, PageHeader.TabsViewportPart).IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<Breadcrumb>(header, PageHeader.BreadcrumbPart).IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<Badge>(header, "PART_Status").IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(header, "PART_Description").IsVisible.ShouldBeFalse();
            More(header).IsVisible.ShouldBeFalse();
            Body(header).CurrentPresenter.Content.ShouldBeNull();
            header.ScrollToTop();
            header.RevealTab(0).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Animates_Without_Errors()
    {
        var clock = new Motion.ManualFrameClock();
        using var installed = clock.Install();
        var header = WithActions(Create(), out var follow, out _, out _, out _);
        var window = Show(header, Motion.ArcMotionLevel.Full);
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

            Frames(5);
            ScrollTo(header, 300);
            Frames(6);
            header.IsCondensed.ShouldBeTrue();
            // Mid-condense: the title row is on its way out.
            var row = TestHelpers.Descendant<Control>(header, PageHeader.TitleRowPart);
            row.Opacity.ShouldBeLessThan(1);
            header.SelectedIndex = 1;
            Frames(4);
            Body(header).ExitingPresenter.ShouldNotBeNull();
            follow.Label = "Following";
            header.Width = 600;
            Frames(4);
            header.AreActionsFolded.ShouldBeTrue();
            header.Width = 1000;
            ScrollTo(header, 0);
            Frames(80);
            header.IsCondensed.ShouldBeFalse();
            header.AreActionsFolded.ShouldBeFalse();
            row.Opacity.ShouldBe(1, 0.01);
            Body(header).ExitingPresenter.ShouldBeNull();
            Slot(header).Opacity.ShouldBe(1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Gallery_Page_Matches_The_Block_Catalog()
    {
        var block = BlockCatalog.Blocks.Single(b => b.ClassName == "PageHeader");
        TKCode.Arc.Gallery.Docs.GalleryDocs.Has("PageHeader").ShouldBeTrue();
        TKCode.Arc.Gallery.Docs.GalleryDocs.HasDemos("PageHeader").ShouldBeTrue();
        TKCode.Arc.Gallery.Docs.GalleryDocs.PackageOf("PageHeader").ShouldBe("TKCode.Arc.Blocks");
        TKCode.Arc.Gallery.Docs.GalleryDocs.Summary("PageHeader", "en-US").ShouldBe(block.Description);
        TKCode.Arc.Gallery.Docs.GalleryDocs.Get("PageHeader", "en-US")!.Language.ShouldBe("en-US");
    }

    private sealed class RelayCommand(Action<object?> run) : ICommand
    {
        private bool _canRun = true;

        public bool CanRun
        {
            get => _canRun;
            set
            {
                _canRun = value;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canRun;

        public void Execute(object? parameter) => run(parameter);
    }

    #region Motion lifetimes and subscriptions

    private static void RunFrames(Motion.ManualFrameClock clock, int count)
    {
        for (var i = 0; i < count; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(16));
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void A_Slide_Removes_The_Old_Content_And_Every_Animation_Ends()
    {
        var clock = new Motion.ManualFrameClock();
        using var installed = clock.Install();
        var header = Create();
        var window = Show(header, Motion.ArcMotionLevel.Full);
        try
        {
            RunFrames(clock, 20);
            var old = (Control)((PageHeaderTab)header.Items[0]!).Content!;
            var baseline = Motion.MotionScheduler.ActiveCount;
            old.GetVisualParent().ShouldNotBeNull();

            header.SelectedIndex = 1;
            Pump();
            RunFrames(clock, 3);
            Body(header).ExitingPresenter.ShouldNotBeNull();

            RunFrames(clock, 80);
            // The leaving panel is gone from the tree (not only hidden), and nothing keeps asking for frames.
            Body(header).ExitingPresenter.ShouldBeNull();
            old.GetVisualParent().ShouldBeNull();
            Motion.MotionScheduler.ActiveCount.ShouldBe(baseline);
            var requests = clock.RequestCount;
            RunFrames(clock, 30);
            clock.RequestCount.ShouldBe(requests);

            // Back to the first tab: the same panel comes back and the other one leaves for good.
            header.SelectedIndex = 0;
            Pump();
            RunFrames(clock, 80);
            Body(header).ExitingPresenter.ShouldBeNull();
            old.GetVisualParent().ShouldNotBeNull();
            ((Control)((PageHeaderTab)header.Items[1]!).Content!).GetVisualParent().ShouldBeNull();
            Motion.MotionScheduler.ActiveCount.ShouldBe(baseline);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switching_Tabs_Again_In_Mid_Slide_Leaves_One_Panel()
    {
        var clock = new Motion.ManualFrameClock();
        using var installed = clock.Install();
        var header = Create();
        var window = Show(header, Motion.ArcMotionLevel.Full);
        try
        {
            RunFrames(clock, 20);
            header.SelectedIndex = 1;
            Pump();
            RunFrames(clock, 2);
            header.SelectedIndex = 2;
            Pump();
            RunFrames(clock, 1);
            header.SelectedIndex = 0;
            Pump();
            RunFrames(clock, 90);
            Body(header).ExitingPresenter.ShouldBeNull();
            BodyText(header).ShouldBe("Overview body");
            ((Control)((PageHeaderTab)header.Items[1]!).Content!).GetVisualParent().ShouldBeNull();
            ((Control)((PageHeaderTab)header.Items[2]!).Content!).GetVisualParent().ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Collapsing_And_Opening_Again_Stops_Requesting_Frames_When_It_Settles()
    {
        var clock = new Motion.ManualFrameClock();
        using var installed = clock.Install();
        var header = WithActions(Create(), out _, out _, out _, out _);
        var window = Show(header, Motion.ArcMotionLevel.Full);
        try
        {
            RunFrames(clock, 20);
            var baseline = Motion.MotionScheduler.ActiveCount;
            ScrollTo(header, 300);
            RunFrames(clock, 3);
            Motion.MotionScheduler.ActiveCount.ShouldBeGreaterThan(baseline);
            RunFrames(clock, 80);
            header.IsCondensed.ShouldBeTrue();
            Motion.MotionScheduler.ActiveCount.ShouldBe(baseline);
            var requests = clock.RequestCount;
            RunFrames(clock, 30);
            clock.RequestCount.ShouldBe(requests);

            ScrollTo(header, 0);
            RunFrames(clock, 80);
            header.IsCondensed.ShouldBeFalse();
            Motion.MotionScheduler.ActiveCount.ShouldBe(baseline);
            // Both ends of the hysteresis leave no blur effect and no half-faded part behind.
            var row = TestHelpers.Descendant<Control>(header, PageHeader.TitleRowPart);
            row.Effect.ShouldBeNull();
            row.Opacity.ShouldBe(1, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    private static Window ShowHosted(Panel host, double width = 1300)
    {
        var window = TestHelpers.Show(host);
        window.Width = width;
        window.Height = 800;
        Pump();
        return window;
    }

    [AvaloniaFact]
    public void A_Header_That_Left_The_Window_Follows_Nothing_And_Catches_Up_When_It_Returns()
    {
        var crumbs = new System.Collections.ObjectModel.ObservableCollection<string> { "Northline", "Projects", "Checkout redesign" };
        var header = WithActions(Create(width: 500), out var follow, out _, out _, out _);
        header.Breadcrumbs = crumbs;
        var host = new Panel { Children = { header } };
        var window = ShowHosted(host);
        try
        {
            // Narrow: the root crumb is left out and the list follows the collection.
            var breadcrumb = TestHelpers.Descendant<Breadcrumb>(header, PageHeader.BreadcrumbPart);
            breadcrumb.ItemCount.ShouldBe(2);
            crumbs.Add("Mobile");
            Pump();
            breadcrumb.ItemCount.ShouldBe(3);

            host.Children.Remove(header);
            Pump();
            crumbs.Add("Cart");
            follow.Label = "Following";
            Pump();

            host.Children.Add(header);
            Pump();
            TestHelpers.Descendant<Breadcrumb>(header, PageHeader.BreadcrumbPart).ItemCount.ShouldBe(4);
            Label(SecondaryButtons(header)[0]).ShouldBe("Following");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Collection_And_Actions_That_Outlive_The_Header_Do_Not_Keep_It_Alive()
    {
        var crumbs = new System.Collections.ObjectModel.ObservableCollection<string> { "Northline", "Projects", "Checkout redesign" };
        var action = new PageHeaderAction { Label = "Follow", Icon = ArcIcons.Bell };
        var reference = HeaderOver(crumbs, action);
        TestHelpers.Collect(reference);
        reference.IsAlive.ShouldBeFalse();
        crumbs.Add("Later");
        action.Label = "Following";
        GC.KeepAlive(crumbs);
        GC.KeepAlive(action);
    }

    [AvaloniaFact]
    public void An_Action_Whose_Command_Outlives_The_Header_Does_Not_Keep_It_Alive()
    {
        var command = new RelayCommand(_ => { });
        var reference = TestHelpers.ShowAndClose(() =>
        {
            var header = Create();
            // A control icon lives in the action's button: through it the action reaches the whole header.
            header.Actions.Add(new PageHeaderAction { Label = "Follow", Icon = new Border { Width = 16, Height = 16 }, Command = command });
            return header;
        }, header =>
        {
            var window = (Window)TopLevel.GetTopLevel(header)!;
            window.Width = 1300;
            window.Height = 800;
            Pump();
            header.Actions[0].Icon.ShouldBeOfType<Border>().GetVisualParent().ShouldNotBeNull();
        });
        TestHelpers.Collect(reference);
        reference.IsAlive.ShouldBeFalse();
        command.CanRun = false;
    }

    [AvaloniaFact]
    public void A_Header_Put_Back_Shows_Its_Commands_State_And_Follows_It_Again()
    {
        var command = new RelayCommand(_ => { });
        var header = Create();
        header.Actions.Add(new PageHeaderAction { Label = "Follow", Icon = ArcIcons.Bell, Command = command });
        var host = new Panel { Children = { header } };
        var window = ShowHosted(host);
        try
        {
            SecondaryButtons(header)[0].IsEnabled.ShouldBeTrue();
            host.Children.Remove(header);
            Pump();
            command.CanRun = false;

            host.Children.Add(header);
            Pump();
            SecondaryButtons(header)[0].IsEnabled.ShouldBeFalse();

            // Back in the window, it listens again.
            command.CanRun = true;
            Pump();
            SecondaryButtons(header)[0].IsEnabled.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference HeaderOver(System.Collections.ObjectModel.ObservableCollection<string> crumbs, PageHeaderAction action)
    {
        var header = Create(width: 500);
        header.Breadcrumbs = crumbs;
        header.Actions.Add(action);
        var window = Show(header);
        Pump();
        window.Content = null;
        window.Close();
        Pump();
        return new WeakReference(header);
    }

    #endregion
}
