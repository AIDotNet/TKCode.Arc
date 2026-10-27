using System.Collections.ObjectModel;
using TKCode.Arc.Motion;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Tables;

/// <summary>Full-motion behavior of the Tables B controls, stepped with a manual frame clock.</summary>
public class TablesBMotionTests
{
    private sealed record Row(string Name, int Value);

    private static Window Show(Control control)
    {
        var window = TestHelpers.Show(control, ArcMotionLevel.Full);
        window.Width = 800;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Sorting_Slides_Rows_From_Their_Old_Places()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var table = new Table
        {
            ItemsSource = new[] { new Row("a", 3), new Row("b", 1), new Row("c", 2) }
        };
        table.Columns.Add(DataTableColumn.Create<Row>("Name", r => r.Name));
        table.Columns.Add(DataTableColumn.Create<Row>("Value", r => r.Value));
        var window = Show(table);
        try
        {
            table.Presenter!.Header.CellFor(1)!.Activate();
            Dispatcher.UIThread.RunJobs();
            var first = table.Presenter.RowAt(0)!;
            ((Row)first.Item!).Name.ShouldBe("b");
            // "b" was drawn in the second slot; it starts there and springs up.
            first.Offset.ShouldBe(52, 0.5);
            first.IsSettling.ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(1.5));
            first.Offset.ShouldBe(0, 0.01);
            first.IsSettling.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Move_Lands_On_A_Spring()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var items = new ObservableCollection<ReorderableListEntry> { new("a", "One"), new("b", "Two"), new("c", "Three") };
        var list = new ReorderableList { ItemsSource = items, Width = 360 };
        var window = Show(list);
        try
        {
            var handle = ((ReorderableListItem)list.ContainerFromIndex(0)!).Handle!;
            handle.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            var moved = (ReorderableListItem)list.ContainerFromIndex(1)!;
            moved.Content.ShouldBe(items[1]);
            moved.Offset.ShouldBe(-64, 0.5);
            moved.Classes.Contains(":lifted").ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(1.5));
            moved.Offset.ShouldBe(0, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removed_Chips_Collapse_Before_Leaving()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var filters = new ObservableCollection<FilterChip> { new("status", "Status", "Open"), new("owner", "Owner", "Mina") };
        var toolbar = new FilterToolbar { ActiveFilters = filters };
        var window = Show(toolbar);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(0.2));
            var slot = toolbar.Slots["status"];
            filters.RemoveAt(0);
            Dispatcher.UIThread.RunJobs();
            slot.IsLeaving.ShouldBeTrue();
            slot.Parent.ShouldNotBeNull();
            clock.Advance(TimeSpan.FromSeconds(0.1));
            Dispatcher.UIThread.RunJobs();
            slot.LayoutFactor.ShouldBeGreaterThan(0.01);
            slot.LayoutFactor.ShouldBeLessThan(1);
            clock.Advance(TimeSpan.FromSeconds(2));
            Dispatcher.UIThread.RunJobs();
            slot.Parent.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }
}
