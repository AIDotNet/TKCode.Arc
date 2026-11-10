using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Content;

public class ChangelogFeedTests
{
    private static Window Show(Control control, double width = 1000, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Click(Avalonia.Controls.Button button) => button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));

    [AvaloniaFact]
    public void Defaults_Open_The_Newest_Entry_And_Count_Everything()
    {
        var feed = new ChangelogFeed();
        var window = Show(feed);
        try
        {
            feed.Title.ShouldBe("Changelog");
            feed.AllRows.Count.ShouldBeGreaterThan(10);
            feed.IsEntryOpen(feed.AllRows[0].Entry.Id).ShouldBeTrue();
            feed.AllRows[0].IsOpen.ShouldBeTrue();
            feed.AllRows[1].IsOpen.ShouldBeFalse();
            feed.ShownCount.ShouldBe(feed.AllRows.Count);
            feed.IsClearVisible.ShouldBeFalse();
            feed.ActiveMonthKey.ShouldBe("2026-09");
            feed.MonthNav!.SelectedIndex.ShouldBe(0);
            feed.MonthNav.Buttons.Count.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DefaultOpen_Chooses_The_Open_Entries()
    {
        var feed = new ChangelogFeed { DefaultOpen = ["bulk-move"] };
        var window = Show(feed);
        try
        {
            feed.IsEntryOpen("bulk-move").ShouldBeTrue();
            feed.IsEntryOpen("board-covers").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Row_Button_Toggles_Its_Panel()
    {
        var feed = new ChangelogFeed();
        var window = Show(feed);
        try
        {
            var row = feed.AllRows[1];
            row.Panel!.Amount.ShouldBe(0);
            Click(row.Row!);
            Dispatcher.UIThread.RunJobs();
            row.IsOpen.ShouldBeTrue();
            row.Panel.Amount.ShouldBe(1);
            row.Panel.Child.ShouldNotBeNull();
            Click(row.Row!);
            Dispatcher.UIThread.RunJobs();
            row.IsOpen.ShouldBeFalse();
            row.Panel.Amount.ShouldBe(0);
            row.Panel.Bounds.Height.ShouldBe(0, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Filter_Keeps_Its_Kind_Updates_The_Counts_And_Offers_Show_All()
    {
        var feed = new ChangelogFeed();
        var window = Show(feed);
        try
        {
            var total = feed.AllRows.Count;
            feed.ToggleFilter(ChangelogKind.Fixed);
            Dispatcher.UIThread.RunJobs();
            feed.VisibleRows.ShouldAllBe(r => r.Entry.Kind == ChangelogKind.Fixed);
            feed.ShownCount.ShouldBe(feed.VisibleRows.Count());
            feed.Chips.Single(c => c.Kind == ChangelogKind.Fixed).IsChecked.ShouldBe(true);
            feed.Chips.Single(c => c.Kind == ChangelogKind.New).IsChecked.ShouldBe(false);
            feed.IsClearVisible.ShouldBeTrue();
            feed.Announcer!.Announcement.ShouldBe($"Showing {feed.ShownCount} of {total} updates");

            feed.ToggleFilter(ChangelogKind.New);
            Dispatcher.UIThread.RunJobs();
            feed.VisibleRows.ShouldAllBe(r => r.Entry.Kind == ChangelogKind.Fixed || r.Entry.Kind == ChangelogKind.New);

            Click(feed.ClearButton!);
            Dispatcher.UIThread.RunJobs();
            feed.VisibleRows.Count().ShouldBe(total);
            feed.IsClearVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Month_With_No_Matches_Leaves_The_Nav_And_The_First_Title_Stays_Out_Of_The_List()
    {
        var feed = new ChangelogFeed
        {
            Months =
            [
                new ChangelogMonth { Key = "b", Label = "Beta 2026", ShortLabel = "B" },
                new ChangelogMonth { Key = "a", Label = "Alpha 2026", ShortLabel = "A" },
            ],
            Entries =
            [
                new ChangelogEntry { Id = "1", Month = "b", Kind = ChangelogKind.New, Title = "One" },
                new ChangelogEntry { Id = "2", Month = "a", Kind = ChangelogKind.Fixed, Title = "Two" },
            ]
        };
        var window = Show(feed);
        try
        {
            feed.MonthNav!.Buttons.Count.ShouldBe(2);
            feed.IsMonthTitleShown("b").ShouldBeFalse();
            feed.IsMonthTitleShown("a").ShouldBeTrue();

            feed.ToggleFilter(ChangelogKind.Fixed);
            Dispatcher.UIThread.RunJobs();
            feed.MonthNav.Buttons.Count.ShouldBe(1);
            feed.ActiveMonthKey.ShouldBe("a");
            feed.IsMonthTitleShown("a").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Month_Bar_Follows_The_Scroll_And_Jumps()
    {
        var feed = new ChangelogFeed();
        var window = Show(feed);
        try
        {
            feed.JumpToMonth("2026-07");
            Dispatcher.UIThread.RunJobs();
            feed.ActiveMonthKey.ShouldBe("2026-07");
            feed.MonthNav!.SelectedIndex.ShouldBe(2);

            feed.Scroller!.Offset = new Avalonia.Vector(0, 0);
            Dispatcher.UIThread.RunJobs();
            feed.ActiveMonthKey.ShouldBe("2026-09");

            feed.Scroller.Offset = new Avalonia.Vector(0, feed.Scroller.Extent.Height);
            Dispatcher.UIThread.RunJobs();
            feed.ActiveMonthKey.ShouldBe("2026-06");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Subscribing_Goes_From_The_Pill_To_The_Field_To_The_Confirmation()
    {
        var feed = new ChangelogFeed();
        string? subscribed = null;
        string? undone = null;
        feed.Subscribed += (_, e) => subscribed = e.Email;
        feed.SubscriptionUndone += (_, e) => undone = e.Email;
        var window = Show(feed);
        try
        {
            var subscribe = feed.Subscribe!;
            feed.SubscribeState.ShouldBe(ChangelogSubscribeState.Idle);
            Click(subscribe.CurrentContent.ShouldBeOfType<ChangelogSubscribeIdle>());
            Dispatcher.UIThread.RunJobs();
            feed.SubscribeState.ShouldBe(ChangelogSubscribeState.Editing);
            var input = subscribe.Input.ShouldNotBeNull();
            input.IsFocused.ShouldBeTrue();

            input.Text = "not an address";
            subscribe.Submit();
            Dispatcher.UIThread.RunJobs();
            subscribe.HasError.ShouldBeTrue();
            subscribe.NoteText.ShouldBe("Enter a valid email address");
            feed.SubscribeState.ShouldBe(ChangelogSubscribeState.Editing);

            input.Text = " me@example.com ";
            Dispatcher.UIThread.RunJobs();
            subscribe.HasError.ShouldBeFalse();
            input.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            feed.SubscribeState.ShouldBe(ChangelogSubscribeState.Done);
            subscribed.ShouldBe("me@example.com");
            subscribe.NoteText.ShouldBe("Subscribed.");

            subscribe.Undo();
            Dispatcher.UIThread.RunJobs();
            feed.SubscribeState.ShouldBe(ChangelogSubscribeState.Idle);
            undone.ShouldBe("me@example.com");
            subscribe.Email.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Cancels_The_Field()
    {
        var feed = new ChangelogFeed();
        var window = Show(feed);
        try
        {
            var subscribe = feed.Subscribe!;
            subscribe.Edit();
            Dispatcher.UIThread.RunJobs();
            subscribe.Input!.Focus();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            feed.SubscribeState.ShouldBe(ChangelogSubscribeState.Idle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(800, false, false)]
    [InlineData(600, true, false)]
    [InlineData(420, true, true)]
    public void Breakpoints_Follow_The_Blocks_Width(double width, bool compact, bool tiny)
    {
        var feed = new ChangelogFeed();
        var window = Show(feed, width);
        try
        {
            feed.Classes.Contains(ChangelogFeed.CompactPseudoClass).ShouldBe(compact);
            feed.Classes.Contains(ChangelogFeed.TinyPseudoClass).ShouldBe(tiny);
            feed.Scroller!.Bounds.Height.ShouldBe(compact ? 520 : 560, 0.5);
            feed.AllRows[0].Panel!.Child.ShouldBeOfType<ChangelogPanelInner>().Padding.Left.ShouldBe(compact ? 20 : 156);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Filter_At_Full_Motion_Removes_Rows_After_They_Faded()
    {
        var feed = new ChangelogFeed();
        var window = Show(feed, level: ArcMotionLevel.Full);
        try
        {
            feed.ToggleFilter(ChangelogKind.Fixed);
            var leaving = feed.AllRows.First(r => r.Entry.Kind != ChangelogKind.Fixed);
            feed.Flow!.IsExiting(leaving).ShouldBeTrue();
            TestHelpers.PumpUntil(() => !feed.Flow.IsExiting(leaving)).ShouldBeTrue();
            leaving.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
