using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Support;

/// <summary>InboxTriage: the sample, archive / snooze / assign / label with undo, the counter, keyboard and the empty state.</summary>
public class InboxTriageTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 14, 0, 0, TimeSpan.Zero);

    private static (InboxTriage Inbox, Avalonia.Controls.Window Window) Show(Action<InboxTriage>? configure = null)
    {
        var inbox = new InboxTriage { Clock = () => Now };
        configure?.Invoke(inbox);
        var window = TestHelpers.Show(inbox);
        window.Width = 600;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return (inbox, window);
    }

    private static InboxTriageItem Item(string id) => new(id, "Sender " + id, "Subject " + id, null, Now.AddHours(-1));

    [AvaloniaFact]
    public void An_Empty_Inbox_Gets_The_Sample_And_The_First_Item_Is_Current()
    {
        var (inbox, window) = Show();
        try
        {
            inbox.Items.Count.ShouldBe(6);
            inbox.Teammates.ShouldNotBeEmpty();
            inbox.AvailableLabels.ShouldNotBeEmpty();
            inbox.Remaining.ShouldBe(6);
            inbox.CurrentItem.ShouldBe(inbox.Items[0]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Archive_Removes_The_Item_And_Undo_Puts_It_Back_In_Place()
    {
        var items = new[] { Item("a"), Item("b"), Item("c") };
        var (inbox, window) = Show(i => i.Items.AddRange(items));
        try
        {
            var triaged = new List<InboxTriageAction>();
            inbox.Triaged += (_, e) => triaged.Add(e.Action);
            inbox.Archive(items[1]);
            Dispatcher.UIThread.RunJobs();
            inbox.Items.ShouldBe([items[0], items[2]]);
            inbox.Remaining.ShouldBe(2);
            inbox.Handled.ShouldBe(1);
            inbox.IsUndoVisible.ShouldBeTrue();
            inbox.UndoMessage!.ShouldContain("Subject b");
            triaged.ShouldBe([InboxTriageAction.Archive]);

            InboxTriageAction? undone = null;
            inbox.Undone += (_, e) => undone = e.Action;
            inbox.Undo().ShouldBeTrue();
            inbox.Items.ShouldBe(items);
            inbox.Handled.ShouldBe(0);
            undone.ShouldBe(InboxTriageAction.Archive);
            inbox.CurrentItem.ShouldBe(items[1]);
            inbox.Undo().ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Snooze_Sets_The_Return_Time_And_Undo_Clears_It()
    {
        var item = Item("a");
        var (inbox, window) = Show(i => i.Items.Add(item));
        try
        {
            inbox.Snooze(item);
            item.SnoozedUntil.ShouldBe(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));
            inbox.Items.ShouldBeEmpty();
            inbox.IsEmptyShown.ShouldBeTrue();
            inbox.Undo();
            item.SnoozedUntil.ShouldBeNull();
            inbox.IsEmptyShown.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Assign_And_Label_Through_The_Picker()
    {
        var item = Item("a");
        var (inbox, window) = Show(i =>
        {
            i.Items.AddRange([item, Item("b")]);
            i.Teammates.AddRange(["Ana", "Ben"]);
            i.AvailableLabels.AddRange(["Bug", "VIP"]);
        });
        try
        {
            inbox.MoveTo(item);
            inbox.OpenLabels();
            Dispatcher.UIThread.RunJobs();
            inbox.Picker.IsOpen.ShouldBeTrue();
            inbox.Picker.Rows.Count.ShouldBe(2);
            inbox.Picker.Pick("VIP");
            item.Labels.ShouldBe(["VIP"]);
            inbox.Items.ShouldContain(item, "a label keeps the item");

            inbox.OpenAssign();
            Dispatcher.UIThread.RunJobs();
            inbox.Picker.Pick("Ben");
            item.Assignee.ShouldBe("Ben");
            inbox.Items.ShouldNotContain(item);

            inbox.Undo();
            item.Assignee.ShouldBeNull();
            inbox.Undo();
            item.Labels.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keys_Move_The_Cursor_And_Archive_Then_Undo()
    {
        var items = new[] { Item("a"), Item("b"), Item("c") };
        var (inbox, window) = Show(i => i.Items.AddRange(items));
        try
        {
            inbox.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.J, RawInputModifiers.None);
            inbox.CurrentItem.ShouldBe(items[1]);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            inbox.CurrentItem.ShouldBe(items[2]);
            window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.None);
            inbox.CurrentItem.ShouldBe(items[1]);
            window.KeyPressQwerty(PhysicalKey.E, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            inbox.Items.ShouldBe([items[0], items[2]]);
            inbox.CurrentItem.ShouldBe(items[2], "the cursor stays at the same place");
            window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.None);
            inbox.Items.ShouldBe(items);
            window.KeyPressQwerty(PhysicalKey.U, RawInputModifiers.None);
            items[1].IsUnread.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Real_Inbox_Stays_Empty_Without_Sample_Data()
    {
        var (inbox, window) = Show(i => i.UsesSampleData = false);
        try
        {
            inbox.Items.ShouldBeEmpty();
            inbox.IsEmptyShown.ShouldBeTrue();
            inbox.Classes.Contains(InboxTriage.EmptyPseudoClass).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
