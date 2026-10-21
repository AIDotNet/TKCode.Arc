using System.Collections.ObjectModel;
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
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks;

public class NotificationCenterTests
{
    private static NotificationItem[] Sample() =>
    [
        new() { Id = "mention", Title = "Mia Chen mentioned you", Description = "Can you check the empty cart state?", Time = "2m", ActorName = "Mia Chen" },
        new() { Id = "deploy", Title = "Deploy finished", Description = "Version 2.4.1 is live.", Time = "18m", Tone = NotificationTone.Success },
        new() { Id = "usage", Title = "Usage is at 85%", Time = "1h", Tone = NotificationTone.Warning },
        new() { Id = "share", Title = "Noah Park shared a file", Time = "3h", ActorName = "Noah Park", IsRead = true },
        new() { Id = "digest", Title = "Weekly digest is ready", Time = "Yesterday", IsRead = true }
    ];

    private static NotificationCenter Create(IEnumerable<NotificationItem>? items = null)
    {
        var center = new NotificationCenter();
        foreach (var item in items ?? Sample())
        {
            center.Items.Add(item);
        }
        return center;
    }

    private static Window Show(NotificationCenter center, ArcMotionLevel level = ArcMotionLevel.Off) =>
        TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { center } }, level);

    private static void Click(Control button)
    {
        button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static NotificationCenterPanel OpenPanel(NotificationCenter center)
    {
        center.IsOpen = true;
        Dispatcher.UIThread.RunJobs();
        return center.Panel.ShouldNotBeNull();
    }

    private static NotificationCenterRow RowOf(NotificationCenterPanel panel, string id) =>
        panel.ShownRows.First(r => r.Item.Id == id);

    private static IFocusManager Focus(Window window) => window.FocusManager!;

    [AvaloniaFact]
    public void Defaults_Show_The_Bell_With_An_Unread_Badge()
    {
        var center = Create();
        var window = Show(center);
        try
        {
            center.Label.ShouldBe("Notifications");
            center.View.ShouldBe(NotificationCenterView.All);
            center.IsOpen.ShouldBeFalse();
            center.UnreadCount.ShouldBe(3);
            center.ReadCount.ShouldBe(2);
            center.Classes.ShouldContain(":has-unread");
            var trigger = center.Trigger.ShouldNotBeNull();
            trigger.Bounds.Width.ShouldBe(42);
            trigger.Bounds.Height.ShouldBe(42);
            trigger.CornerRadius.ShouldBe(new CornerRadius(12));
            AutomationProperties.GetName(trigger).ShouldBe("Notifications, 3 unread");
            var badge = TestHelpers.Descendant<PresenceHost>(center, NotificationCenter.BadgePart);
            badge.IsPresent.ShouldBeTrue();
            TestHelpers.Descendant<ArcContentSwap>(center, NotificationCenter.BadgeCountPart).Content.ShouldBe("3");
            center.Panel.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Badge_Caps_At_Nine_And_Leaves_When_Everything_Is_Read()
    {
        var items = Enumerable.Range(1, 12).Select(i => new NotificationItem { Id = $"n{i}", Title = $"Update {i}", Time = $"{i}m" }).ToList();
        var center = Create(items);
        var window = Show(center);
        try
        {
            var count = TestHelpers.Descendant<ArcContentSwap>(center, NotificationCenter.BadgeCountPart);
            count.Content.ShouldBe("9+");
            center.MarkAllRead();
            Dispatcher.UIThread.RunJobs();
            center.UnreadCount.ShouldBe(0);
            center.ReadCount.ShouldBe(12);
            TestHelpers.Descendant<PresenceHost>(center, NotificationCenter.BadgePart).IsPresent.ShouldBeFalse();
            center.Classes.ShouldNotContain(":has-unread");
            AutomationProperties.GetName(center.Trigger!).ShouldBe("Notifications");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Trigger_Opens_The_Panel_Below_The_Bell_And_Focuses_Close()
    {
        var center = Create();
        var window = Show(center);
        try
        {
            window.Height = 720;
            Dispatcher.UIThread.RunJobs();
            Click(center.Trigger!);
            center.IsOpen.ShouldBeTrue();
            center.Classes.ShouldContain(":open");
            center.Trigger!.IsOn.ShouldBeTrue();
            var popup = center.Popup.ShouldNotBeNull();
            popup.IsOpen.ShouldBeTrue();
            popup.MarginToAnchor.ShouldBe(12);
            var panel = center.Panel.ShouldNotBeNull();
            panel.Heading.ShouldBe("Notifications");
            AutomationProperties.GetName(panel).ShouldBe("Notifications");
            panel.CountSwap!.Content.ShouldBe("3");
            panel.SummarySwap!.Content.ShouldBe("3 updates waiting for you");
            panel.ShownRows.Select(r => r.Item.Id).ShouldBe(["mention", "deploy", "usage", "share", "digest"]);
            // The window is 400 wide: min(424, 400 − 24).
            panel.Bounds.Width.ShouldBe(376);
            panel.CornerRadius.ShouldBe(new CornerRadius(26));
            var panelTop = panel.TranslatePoint(default, window)!.Value.Y;
            var bellBottom = center.Trigger.TranslatePoint(new Point(0, 42), window)!.Value.Y;
            panelTop.ShouldBe(bellBottom + 12, 0.5);
            panel.CloseButton!.IsFocused.ShouldBeTrue();
            AutomationProperties.GetName(panel.CloseButton).ShouldBe("Close notifications");
            KeyboardNavigation.GetTabNavigation(panel).ShouldBe(KeyboardNavigationMode.Cycle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_And_Close_Return_Focus_To_The_Bell()
    {
        var center = Create();
        var window = Show(center);
        try
        {
            var panel = OpenPanel(center);
            panel.CloseButton!.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            center.IsOpen.ShouldBeFalse();
            center.Popup!.IsOpen.ShouldBeFalse();
            center.Trigger!.IsFocused.ShouldBeTrue();
            center.Trigger.IsOn.ShouldBeFalse();

            OpenPanel(center);
            Click(panel.CloseButton!);
            center.IsOpen.ShouldBeFalse();
            center.Trigger.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Press_Outside_Closes_And_The_Bell_Toggles()
    {
        var center = Create();
        var window = Show(center);
        try
        {
            OpenPanel(center);
            window.MouseDown(new Point(390, 290), MouseButton.Left);
            window.MouseUp(new Point(390, 290), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            center.IsOpen.ShouldBeFalse();

            Click(center.Trigger!);
            center.IsOpen.ShouldBeTrue();
            Click(center.Trigger!);
            center.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Opens_From_The_Bell()
    {
        var center = Create();
        var window = Show(center);
        try
        {
            center.Trigger!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            center.IsOpen.ShouldBeTrue();
            center.Panel!.CloseButton!.IsFocused.ShouldBeTrue();
            center.Panel.CloseButton.Classes.ShouldContain(":focus-visible");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tab_Moves_Through_Close_Views_Bulk_Action_And_Rows()
    {
        var center = Create();
        var window = Show(center);
        try
        {
            window.Height = 720;
            Dispatcher.UIThread.RunJobs();
            var panel = OpenPanel(center);
            panel.CloseButton!.IsFocused.ShouldBeTrue();
            var order = new List<object?>();
            for (var i = 0; i < 5; i++)
            {
                window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
                window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                order.Add(Focus(window).GetFocusedElement());
            }
            order[0].ShouldBe(panel.AllViewButton);
            order[1].ShouldBe(panel.UnreadViewButton);
            order[2].ShouldBe(panel.MarkAllButton);
            order[3].ShouldBe(RowOf(panel, "mention").Toggle);
            order[4].ShouldBe(RowOf(panel, "deploy").Toggle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rows_Show_The_Avatar_Or_The_Tone_Icon_And_Fallback_Text()
    {
        var center = Create();
        var window = Show(center);
        try
        {
            var panel = OpenPanel(center);
            var mention = RowOf(panel, "mention");
            mention.Classes.ShouldContain(":actor");
            TestHelpers.Descendant<Avatar>(mention, "PART_Avatar").IsVisible.ShouldBeTrue();
            mention.PreviewText.ShouldBe("Can you check the empty cart state?");
            mention.Dot!.IsPresent.ShouldBeTrue();

            var deploy = RowOf(panel, "deploy");
            deploy.Classes.ShouldContain(":success");
            deploy.EventGlyph.ShouldBe(ArcIcons.CircleCheck);
            TestHelpers.Descendant<Avatar>(deploy, "PART_Avatar").IsVisible.ShouldBeFalse();

            var usage = RowOf(panel, "usage");
            usage.EventGlyph.ShouldBe(ArcIcons.TriangleAlert);
            usage.PreviewText.ShouldBe("View update details");
            usage.DetailText.ShouldBe("This update is ready to review.");

            var share = RowOf(panel, "share");
            share.Classes.ShouldContain(":read");
            share.Dot!.IsPresent.ShouldBeFalse();
            share.PreviewText.ShouldBe("From Noah Park");
            share.DetailText.ShouldBe("Noah Park shared an update with you.");

            RowOf(panel, "digest").EventGlyph.ShouldBe(ArcIcons.MessageCircle);
            AutomationProperties.GetName(mention.Toggle!).ShouldBe("Mia Chen mentioned you, unread. Show details");
            AutomationProperties.GetName(share.Toggle!).ShouldBe("Noah Park shared a file. Show details");
            AutomationProperties.GetName(panel.List!).ShouldBe("All notifications");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_A_Row_Expands_It_And_Only_One_Row_Stays_Open()
    {
        var center = Create();
        var window = Show(center);
        try
        {
            var panel = OpenPanel(center);
            var deploy = RowOf(panel, "deploy");
            deploy.Details!.IsShown.ShouldBeFalse();
            Click(deploy.Toggle!);
            deploy.IsExpanded.ShouldBeTrue();
            deploy.Classes.ShouldContain(":expanded");
            deploy.Toggle!.IsOn.ShouldBeTrue();
            deploy.Details.IsShown.ShouldBeTrue();
            deploy.ReadToggle!.IsEffectivelyVisible.ShouldBeTrue();
            AutomationProperties.GetName(deploy.ReadToggle).ShouldBe("Mark read");
            AutomationProperties.GetName(deploy.Toggle).ShouldEndWith("Hide details");
            var peer = ControlAutomationPeer.CreatePeerForElement(deploy.Toggle);
            peer.GetProvider<IExpandCollapseProvider>()!.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);

            var usage = RowOf(panel, "usage");
            Click(usage.Toggle!);
            usage.IsExpanded.ShouldBeTrue();
            deploy.IsExpanded.ShouldBeFalse();
            deploy.Details.IsShown.ShouldBeFalse();

            Click(usage.Toggle!);
            usage.IsExpanded.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Mark_Read_In_All_View_Raises_ReadChanged_And_Updates_Counts()
    {
        var center = Create();
        var events = new List<NotificationReadChangedEventArgs>();
        center.ReadChanged += (_, e) => events.Add(e);
        var window = Show(center);
        try
        {
            var panel = OpenPanel(center);
            var deploy = RowOf(panel, "deploy");
            Click(deploy.Toggle!);
            Click(deploy.ReadToggle!);
            events.Count.ShouldBe(1);
            events[0].Item.Id.ShouldBe("deploy");
            events[0].IsRead.ShouldBeTrue();
            events[0].RoutedEvent.ShouldBe(NotificationCenter.ReadChangedEvent);
            deploy.Item.IsRead.ShouldBeTrue();
            center.UnreadCount.ShouldBe(2);
            center.ReadCount.ShouldBe(3);
            panel.CountSwap!.Content.ShouldBe("2");
            panel.SummarySwap!.Content.ShouldBe("2 updates waiting for you");
            panel.ReadCountSwap!.Content.ShouldBe("3");
            deploy.Dot!.IsPresent.ShouldBeFalse();
            deploy.Classes.ShouldContain(":read");
            AutomationProperties.GetName(deploy.ReadToggle!).ShouldBe("Mark unread");
            // In the All view the row stays, still expanded.
            deploy.IsExpanded.ShouldBeTrue();

            Click(deploy.ReadToggle!);
            events.Count.ShouldBe(2);
            events[1].IsRead.ShouldBeFalse();
            center.UnreadCount.ShouldBe(3);
            deploy.Dot.IsPresent.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Unread_View_Lists_Unread_Rows_And_Moves_Focus_After_Marking()
    {
        var center = Create();
        center.View = NotificationCenterView.Unread;
        var window = Show(center);
        try
        {
            var panel = OpenPanel(center);
            panel.ShownRows.Select(r => r.Item.Id).ShouldBe(["mention", "deploy", "usage"]);
            panel.UnreadViewButton!.IsOn.ShouldBeTrue();
            panel.AllViewButton!.IsOn.ShouldBeFalse();
            AutomationProperties.GetName(panel.List!).ShouldBe("Unread notifications");
            var toggle = ControlAutomationPeer.CreatePeerForElement(panel.UnreadViewButton).GetProvider<IToggleProvider>()!;
            toggle.ToggleState.ShouldBe(ToggleState.On);

            // Marking the middle row read: it leaves, and focus moves to the next row.
            var deploy = RowOf(panel, "deploy");
            Click(deploy.Toggle!);
            Click(deploy.ReadToggle!);
            panel.ShownRows.Select(r => r.Item.Id).ShouldBe(["mention", "usage"]);
            RowOf(panel, "usage").Toggle!.IsFocused.ShouldBeTrue();
            center.ExpandedKey.ShouldBeNull();

            // The last row: focus moves to the previous one.
            var usage = RowOf(panel, "usage");
            Click(usage.Toggle!);
            Click(usage.ReadToggle!);
            RowOf(panel, "mention").Toggle!.IsFocused.ShouldBeTrue();

            // The only row: focus moves to the Unread button, and the empty state shows.
            var mention = RowOf(panel, "mention");
            Click(mention.Toggle!);
            Click(mention.ReadToggle!);
            panel.ShownRows.ShouldBeEmpty();
            panel.UnreadViewButton.IsFocused.ShouldBeTrue();
            panel.EmptyReveal!.IsShown.ShouldBeTrue();
            panel.EmptyTitleSwap!.Content.ShouldBe("Nothing unread");
            panel.IsViewAllVisible.ShouldBeTrue();
            panel.MarkAllReveal!.IsShown.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dismiss_Removes_The_Item_And_Raises_Dismissed()
    {
        var center = Create();
        var dismissed = new List<NotificationItem>();
        center.Dismissed += (_, e) => dismissed.Add(e.Item);
        var window = Show(center);
        try
        {
            var panel = OpenPanel(center);
            var deploy = RowOf(panel, "deploy");
            Click(deploy.Toggle!);
            Click(deploy.DismissButton!);
            dismissed.Select(i => i.Id).ShouldBe(["deploy"]);
            center.Items.Select(i => i.Id).ShouldBe(["mention", "usage", "share", "digest"]);
            panel.ShownRows.Select(r => r.Item.Id).ShouldBe(["mention", "usage", "share", "digest"]);
            panel.List!.Children.Count.ShouldBe(4);
            RowOf(panel, "usage").Toggle!.IsFocused.ShouldBeTrue();
            center.UnreadCount.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Mark_All_Read_Raises_Once_Per_Item_And_Focuses_The_View()
    {
        var center = Create();
        center.View = NotificationCenterView.Unread;
        var events = new List<NotificationReadChangedEventArgs>();
        center.ReadChanged += (_, e) => events.Add(e);
        var window = Show(center);
        try
        {
            var panel = OpenPanel(center);
            panel.MarkAllReveal!.IsShown.ShouldBeTrue();
            AutomationProperties.GetName(panel.MarkAllButton!).ShouldBe("Mark all read");
            Click(panel.MarkAllButton!);
            events.Select(e => e.Item.Id).ShouldBe(["mention", "deploy", "usage"]);
            events.ShouldAllBe(e => e.IsRead);
            center.Items.ShouldAllBe(i => i.IsRead);
            panel.ShownRows.ShouldBeEmpty();
            panel.UnreadViewButton!.IsFocused.ShouldBeTrue();
            panel.SummarySwap!.Content.ShouldBe("You’re all caught up");
            panel.MarkAllReveal.IsShown.ShouldBeFalse();
            panel.EmptyReveal!.IsShown.ShouldBeTrue();
            panel.ReadCountSwap!.Content.ShouldBe("5");

            // "View all updates" goes back to every update.
            Click(panel.ViewAllButton!);
            center.View.ShouldBe(NotificationCenterView.All);
            panel.ShownRows.Count().ShouldBe(5);
            panel.EmptyReveal.IsShown.ShouldBeFalse();
            panel.AllViewButton!.IsOn.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clear_Read_Dismisses_Every_Read_Item_And_Hides_The_Footer()
    {
        var center = Create();
        var dismissed = new List<NotificationItem>();
        center.Dismissed += (_, e) => dismissed.Add(e.Item);
        var window = Show(center);
        try
        {
            var panel = OpenPanel(center);
            panel.FooterReveal!.IsShown.ShouldBeTrue();
            panel.ReadCountSwap!.Content.ShouldBe("2");
            panel.ReadPrefix.ShouldBe(string.Empty);
            panel.ReadSuffix.ShouldBe(" read");
            Click(panel.ClearReadButton!);
            dismissed.Select(i => i.Id).ShouldBe(["share", "digest"]);
            center.Items.Count.ShouldBe(3);
            center.ReadCount.ShouldBe(0);
            panel.FooterReveal.IsShown.ShouldBeFalse();
            panel.AllViewButton!.IsFocused.ShouldBeTrue();
            panel.ShownRows.Count().ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ItemsSource_Is_Followed_And_Dismiss_Removes_From_A_Writable_Source()
    {
        var source = new ObservableCollection<NotificationItem>(Sample());
        var center = new NotificationCenter { ItemsSource = source };
        var window = Show(center);
        try
        {
            var panel = OpenPanel(center);
            panel.ShownRows.Count().ShouldBe(5);
            source.Insert(0, new NotificationItem { Id = "new", Title = "Invoice paid", Time = "now", Tone = NotificationTone.Success });
            Dispatcher.UIThread.RunJobs();
            panel.ShownRows.First().Item.Id.ShouldBe("new");
            center.UnreadCount.ShouldBe(4);

            // The app marks an item read in its store: the row and the counts follow.
            source[1].IsRead = true;
            Dispatcher.UIThread.RunJobs();
            center.UnreadCount.ShouldBe(3);
            RowOf(panel, "mention").Classes.ShouldContain(":read");

            var usage = RowOf(panel, "usage");
            Click(usage.Toggle!);
            Click(usage.DismissButton!);
            source.Select(i => i.Id).ShouldNotContain("usage");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Read_Only_Source_Keeps_Its_Items_And_The_Control_Hides_Dismissed_Ones()
    {
        var source = Sample();
        var center = new NotificationCenter { ItemsSource = Array.AsReadOnly(source) };
        var dismissed = new List<NotificationItem>();
        center.Dismissed += (_, e) => dismissed.Add(e.Item);
        var window = Show(center);
        try
        {
            var panel = OpenPanel(center);
            Click(panel.ClearReadButton!);
            dismissed.Count.ShouldBe(2);
            source.Length.ShouldBe(5);
            panel.ShownRows.Select(r => r.Item.Id).ShouldBe(["mention", "deploy", "usage"]);
            center.ReadCount.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Empty_Data_Shows_All_Clear_Without_Badge_Footer_Or_Bulk_Action()
    {
        var center = new NotificationCenter();
        var window = Show(center);
        try
        {
            TestHelpers.Descendant<PresenceHost>(center, NotificationCenter.BadgePart).IsPresent.ShouldBeFalse();
            var panel = OpenPanel(center);
            panel.ShownRows.ShouldBeEmpty();
            panel.EmptyReveal!.IsShown.ShouldBeTrue();
            panel.EmptyTitleSwap!.Content.ShouldBe("All clear");
            panel.IsViewAllVisible.ShouldBeFalse();
            panel.FooterReveal!.IsShown.ShouldBeFalse();
            panel.MarkAllReveal!.IsShown.ShouldBeFalse();
            panel.SummarySwap!.Content.ShouldBe("You’re all caught up");
            panel.CountSwap!.Content.ShouldBe("0");

            // Unread view with nothing at all: no link to the All view.
            center.View = NotificationCenterView.Unread;
            Dispatcher.UIThread.RunJobs();
            panel.EmptyTitleSwap.Content.ShouldBe("Nothing unread");
            panel.IsViewAllVisible.ShouldBeFalse();

            // An update arrives while the panel is open.
            center.Items.Add(new NotificationItem { Id = "x", Title = "Build failed", Time = "now", Tone = NotificationTone.Warning });
            Dispatcher.UIThread.RunJobs();
            panel.ShownRows.Count().ShouldBe(1);
            panel.EmptyReveal.IsShown.ShouldBeFalse();
            panel.MarkAllReveal.IsShown.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void View_Switch_Moves_The_Highlight_And_Collapses_Details()
    {
        var center = Create();
        var window = Show(center);
        try
        {
            var panel = OpenPanel(center);
            var highlight = panel.ViewHighlight!;
            highlight.HasTarget.ShouldBeTrue();
            highlight.CurrentRect.X.ShouldBe(0, 0.5);
            Click(RowOf(panel, "deploy").Toggle!);
            center.ExpandedKey.ShouldBe("deploy");

            Click(panel.UnreadViewButton!);
            center.View.ShouldBe(NotificationCenterView.Unread);
            center.ExpandedKey.ShouldBeNull();
            var unreadX = panel.UnreadViewButton!.TranslatePoint(default, highlight)!.Value.X;
            highlight.CurrentRect.X.ShouldBe(unreadX, 0.5);
            highlight.CurrentRect.Width.ShouldBe(panel.UnreadViewButton!.Bounds.Width, 0.5);
            panel.ShownRows.Count().ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_And_Label_Change_Every_Word()
    {
        var center = Create();
        center.Label = "Inbox";
        center.Labels = new NotificationCenterLabels
        {
            Trigger = (label, unread) => $"{label} ({unread})",
            Summary = unread => $"{unread} neue",
            All = "Alle",
            Unread = "Ungelesen",
            MarkAllRead = "Alle gelesen",
            ReadCount = "Gelesen: {0}",
            ClearRead = "Leeren",
            FromActor = name => $"Von {name}"
        };
        var window = Show(center);
        try
        {
            AutomationProperties.GetName(center.Trigger!).ShouldBe("Inbox (3)");
            var panel = OpenPanel(center);
            panel.Heading.ShouldBe("Inbox");
            panel.SummarySwap!.Content.ShouldBe("3 neue");
            panel.AllText.ShouldBe("Alle");
            panel.UnreadText.ShouldBe("Ungelesen");
            panel.MarkAllText.ShouldBe("Alle gelesen");
            panel.ReadPrefix.ShouldBe("Gelesen: ");
            panel.ReadSuffix.ShouldBe(string.Empty);
            panel.ClearReadText.ShouldBe("Leeren");
            RowOf(panel, "share").PreviewText.ShouldBe("Von Noah Park");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Window_Uses_The_Narrow_Layout()
    {
        var center = Create();
        var window = Show(center);
        try
        {
            window.Width = 360;
            Dispatcher.UIThread.RunJobs();
            var panel = OpenPanel(center);
            panel.IsNarrow.ShouldBeTrue();
            panel.Classes.ShouldContain(":narrow");
            panel.Bounds.Width.ShouldBe(336);
            RowOf(panel, "deploy").Classes.ShouldContain(":narrow");
            TestHelpers.Descendant<TextBlock>(panel, "PART_MarkAllText").IsVisible.ShouldBeFalse();

            window.Width = 400;
            Dispatcher.UIThread.RunJobs();
            panel.IsNarrow.ShouldBeFalse();
            panel.Bounds.Width.ShouldBe(376);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void IsOpen_Set_Before_Showing_Opens_Once_The_Bell_Is_In_A_Window()
    {
        var center = Create();
        center.IsOpen = true;
        var window = Show(center);
        try
        {
            Dispatcher.UIThread.RunJobs();
            center.Popup.ShouldNotBeNull().IsOpen.ShouldBeTrue();
            center.Close();
            Dispatcher.UIThread.RunJobs();
            center.IsOpen.ShouldBeFalse();
            center.Popup.IsOpen.ShouldBeFalse();
            center.Open();
            Dispatcher.UIThread.RunJobs();
            center.Popup.IsOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Plays_Open_Bulk_And_Close_Without_Throwing()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var center = Create();
        var window = Show(center, ArcMotionLevel.Full);
        void Run(int milliseconds)
        {
            for (var elapsed = 0; elapsed < milliseconds; elapsed += 16)
            {
                clock.Advance(TimeSpan.FromMilliseconds(16));
                Dispatcher.UIThread.RunJobs();
            }
        }
        try
        {
            Dispatcher.UIThread.RunJobs();
            Run(100);
            center.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            Run(32);
            var panel = center.Panel!;
            // Mid-entrance: later rows are still waiting for their stagger.
            var last = panel.ShownRows.Last();
            TestHelpers.Descendant<Border>(last, NotificationCenterRow.ArticlePart).Opacity.ShouldBeLessThan(1);
            Run(600);
            TestHelpers.Descendant<Border>(last, NotificationCenterRow.ArticlePart).Opacity.ShouldBe(1);

            Click(RowOf(panel, "deploy").Toggle!);
            Run(100);
            RowOf(panel, "deploy").Details!.HeightFraction.ShouldBeInRange(0.01, 0.99);
            Run(800);

            center.View = NotificationCenterView.Unread;
            Dispatcher.UIThread.RunJobs();
            Run(50);
            // Read rows are leaving: still in the list while they collapse.
            panel.List!.Children.Count.ShouldBe(5);
            Run(900);
            panel.List.Children.Count.ShouldBe(3);

            Click(panel.MarkAllButton!);
            Run(60);
            panel.List.Children.Count.ShouldBe(3);
            Run(1200);
            panel.List.Children.Count.ShouldBe(0);
            panel.EmptyReveal!.HeightFraction.ShouldBe(1, 0.01);

            center.IsOpen = false;
            Dispatcher.UIThread.RunJobs();
            center.Popup!.IsOpen.ShouldBeTrue();
            Run(300);
            center.Popup.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Fades_Rows_Out_Without_Collapsing_And_The_Bell_Stays_Upright()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var center = Create();
        var window = Show(center, ArcMotionLevel.Reduced);
        try
        {
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(32));
            Dispatcher.UIThread.RunJobs();
            center.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            // The panel shows at once (Arc turns its animation off for reduced motion).
            var reducedPanel = center.Panel.ShouldNotBeNull();
            reducedPanel.Opacity.ShouldBe(1);
            var bell = TestHelpers.Descendant<ArcGlyph>(center, "PART_Bell");
            bell.RenderTransform!.Value.M12.ShouldBe(0, 1e-6);

            center.View = NotificationCenterView.Unread;
            Dispatcher.UIThread.RunJobs();
            var leaving = reducedPanel.List!.Children.OfType<NotificationCenterReveal>().Where(h => h.IsLeaving).ToList();
            leaving.Count.ShouldBe(2);
            clock.Advance(TimeSpan.FromMilliseconds(64));
            Dispatcher.UIThread.RunJobs();
            leaving.ShouldAllBe(h => h.HeightFraction == 1 && h.Opacity < 1);
            clock.Advance(TimeSpan.FromMilliseconds(200));
            Dispatcher.UIThread.RunJobs();
            reducedPanel.List.Children.Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }
}
