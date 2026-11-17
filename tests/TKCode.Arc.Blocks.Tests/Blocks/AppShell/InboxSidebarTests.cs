using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Blocks.AppShell;

/// <summary>Inbox sidebar: folders and labels map onto the sidebar, counts follow, the rail toggles and the actions report.</summary>
public class InboxSidebarTests
{
    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public void Formats_Counts()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        InboxSidebar.FormatCount(0, culture).ShouldBeNull();
        InboxSidebar.FormatCount(12, culture).ShouldBe("12");
        InboxSidebar.FormatCount(999, culture).ShouldBe("999");
        InboxSidebar.FormatCount(1048, culture).ShouldBe("999+");
    }

    [AvaloniaFact]
    public void Builds_The_Sample_Mailbox()
    {
        var mail = new InboxSidebar { Height = 640 };
        var window = TestHelpers.Show(mail);
        try
        {
            Settle();
            var sidebar = TestHelpers.Descendant<Sidebar>(mail, InboxSidebar.SidebarPart);
            sidebar.Sections.Count.ShouldBe(2);
            var folders = sidebar.Sections[0].Items;
            folders.Select(i => i.Key).ShouldBe(["inbox", "starred", "snoozed", "sent", "drafts", "spam", "trash"]);
            folders[0].Badge.ShouldBe("12");
            folders[0].IsUnread.ShouldBeTrue();
            folders[4].Badge.ShouldBe("3");
            folders[4].IsUnread.ShouldBeFalse();
            folders[5].Badge.ShouldBe("999+");
            var labels = sidebar.Sections[1];
            labels.Header.ShouldBe("Labels");
            labels.IsCollapsible.ShouldBeTrue();
            labels.Items[0].Icon.ShouldBeOfType<Ellipse>();
            labels.Items.Last().IsAction.ShouldBeTrue();
            sidebar.SelectedKey.ShouldBe("inbox");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Counts_Follow_The_Model_And_Choices_Report()
    {
        var mail = new InboxSidebar { Height = 640 };
        var inbox = new InboxSidebarFolder("open", "Open", ArcIcons.Inbox, 2);
        mail.Folders.Add(inbox);
        mail.MailLabels.Add(new InboxSidebarLabel("bugs", "Bugs", InboxSidebarLabelTone.Danger, 4));
        var window = TestHelpers.Show(mail);
        try
        {
            Settle();
            var item = mail.ItemOf(inbox)!;
            item.Badge.ShouldBe("2");
            inbox.Count = 5;
            item.Badge.ShouldBe("5");
            inbox.Count = 0;
            item.Badge.ShouldBeNull();
            item.IsUnread.ShouldBeFalse();

            var sidebar = TestHelpers.Descendant<Sidebar>(mail, InboxSidebar.SidebarPart);
            sidebar.SelectedKey = "bugs";
            Settle();
            mail.SelectedKey.ShouldBe("bugs");

            var compose = 0;
            mail.ComposeRequested += (_, _) => compose++;
            TestHelpers.Descendant<AvaloniaButton>(mail, InboxSidebar.ComposeButtonPart).RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            compose.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toggle_Collapses_To_The_Rail()
    {
        var mail = new InboxSidebar { Height = 640 };
        var window = TestHelpers.Show(mail);
        try
        {
            Settle();
            var sidebar = TestHelpers.Descendant<Sidebar>(mail, InboxSidebar.SidebarPart);
            TestHelpers.Descendant<AvaloniaButton>(mail, InboxSidebar.ToggleButtonPart).RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            Settle();
            mail.IsCollapsed.ShouldBeTrue();
            sidebar.IsCollapsed.ShouldBeTrue();
            mail.Classes.Contains(":collapsed").ShouldBeTrue();
            sidebar.ToggleCollapsed();
            Settle();
            mail.IsCollapsed.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hidden_And_Disabled_Folders_Follow_Live_And_SelectionChanged_Reports_The_Key()
    {
        var mail = new InboxSidebar { Height = 640 };
        var window = TestHelpers.Show(mail);
        try
        {
            Settle();
            var sidebar = TestHelpers.Descendant<Sidebar>(mail, InboxSidebar.SidebarPart);
            var sent = mail.ShownFolders.Single(f => f.Key == "sent");
            var item = sidebar.Sections[0].Items.Single(i => i.Key == "sent");
            item.IsVisible.ShouldBeTrue();
            item.IsEnabled.ShouldBeTrue();
            sent.IsVisible = false;
            item.IsVisible.ShouldBeFalse();
            sent.IsVisible = true;
            item.IsVisible.ShouldBeTrue();
            sent.IsEnabled = false;
            item.IsEnabled.ShouldBeFalse();
            sent.IsEnabled = true;

            var changes = new List<InboxSidebarSelectionChangedEventArgs>();
            mail.SelectionChanged += (_, e) => changes.Add(e);
            mail.SelectedKey = "sent";
            changes.Count.ShouldBe(1);
            changes[0].OldKey.ShouldBe("inbox");
            changes[0].NewKey.ShouldBe("sent");
            changes[0].NewFolder.ShouldBe(sent);
            changes[0].NewLabel.ShouldBeNull();
            // The same key again is no change.
            mail.SelectedKey = "sent";
            changes.Count.ShouldBe(1);
            // A label key reports the label.
            var label = mail.ShownLabels[0];
            mail.SelectedKey = label.Key;
            changes.Count.ShouldBe(2);
            changes[1].NewLabel.ShouldBe(label);
            changes[1].NewFolder.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }
}
