using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary><see cref="CatchUp"/>: grouping, filters, read state, opening, mark all and the keyboard.</summary>
public class CatchUpTests
{
    private static (CatchUp Card, Window Window) Show()
    {
        var card = new CatchUp();
        var window = TestHelpers.Show(card);
        Dispatcher.UIThread.RunJobs();
        return (card, window);
    }

    [AvaloniaFact]
    public void Sample_groups_seven_items_with_five_unread()
    {
        var (card, window) = Show();
        try
        {
            card.Rows.Count.ShouldBe(7);
            card.Rows.Select(r => r.Item.Kind).ShouldBe([CatchUpKind.Highlight, CatchUpKind.Highlight, CatchUpKind.Highlight,
                CatchUpKind.Mention, CatchUpKind.Mention, CatchUpKind.Decision, CatchUpKind.Decision]);
            card.UnreadCount.ShouldBe(5);
            card.Rows.Count(r => r.IsUnread).ShouldBe(5);
            AutomationProperties.GetName(card.Rows[0].OpenButton!).ShouldBe("Mobile beta reached 2,000 testers, unread");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_filter_lists_one_group_and_counts_unread()
    {
        var (card, window) = Show();
        try
        {
            card.Filter = CatchUpFilter.Mentions;
            card.Rows.Count.ShouldBe(2);
            card.Rows.ShouldAllBe(r => r.Item.Kind == CatchUpKind.Mention);
            var filter = TestHelpers.Descendant<SegmentedControl>(card, CatchUp.FilterPart);
            filter.Items.OfType<SegmentedOption>().Select(o => o.Label).ShouldBe(["All 5", "Highlights 2", "Mentions 2", "Decisions 1"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opening_marks_read_and_the_toggle_marks_unread()
    {
        var (card, window) = Show();
        CatchUpItem? opened = null;
        var reads = new List<bool>();
        card.ItemInvoked += (_, e) => opened = e.Item;
        card.ItemsRead += (_, e) => reads.Add(e.IsRead);
        try
        {
            var row = card.Rows[3];
            row.OpenButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            opened.ShouldNotBeNull().Link.ShouldBe("doc:pricing#c12");
            card.IsRead(row.Item).ShouldBeTrue();
            card.UnreadCount.ShouldBe(4);
            card.Rows[3].ToggleButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            card.IsRead(card.Rows[3].Item).ShouldBeFalse();
            reads.ShouldBe([true, false]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Mark_all_read_turns_the_recap_into_caught_up()
    {
        var (card, window) = Show();
        try
        {
            card.MarkAllRead();
            Dispatcher.UIThread.RunJobs();
            card.UnreadCount.ShouldBe(0);
            card.Classes.Contains(":caught-up").ShouldBeTrue();
            TestHelpers.Descendant<Button>(card, CatchUp.MarkAllPart).IsEnabled.ShouldBeFalse();
            card.Announcement.ShouldBe("You are all caught up.");
            card.Rows.ShouldAllBe(r => !r.IsUnread);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_keys_move_between_items()
    {
        var (card, window) = Show();
        try
        {
            card.Rows[0].FocusOpen();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            card.Rows[1].OpenButton!.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            card.Rows[^1].OpenButton!.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
