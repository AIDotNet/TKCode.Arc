using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Tables;

public class ReorderableListTests
{
    private static ObservableCollection<ReorderableListEntry> Entries() =>
    [
        new("a", "Ship pricing page", "Marketing"),
        new("b", "Review onboarding flow"),
        new("c", "Migrate billing webhooks"),
        new("d", "Write launch notes")
    ];

    private static (ReorderableList List, Window Window) Show(ReorderableList list)
    {
        list.Width = 360;
        var window = TestHelpers.Show(list);
        window.Width = 500;
        window.Height = 500;
        Dispatcher.UIThread.RunJobs();
        return (list, window);
    }

    private static ReorderableListHandle Handle(ReorderableList list, int index) =>
        ((ReorderableListItem)list.ContainerFromIndex(index)!).Handle!;

    private static string Order(IEnumerable<ReorderableListEntry> items) => string.Concat(items.Select(i => i.Id));

    [AvaloniaFact]
    public void Rows_Show_Arc_Geometry_Positions_And_Grip_Names()
    {
        var (list, window) = Show(new ReorderableList { ItemsSource = Entries() });
        try
        {
            var row = (ReorderableListItem)list.ContainerFromIndex(0)!;
            row.Bounds.Height.ShouldBe(56, 0.5);
            row.Bounds.Width.ShouldBe(360, 0.5);
            ((ReorderableListItem)list.ContainerFromIndex(1)!).Bounds.Y.ShouldBe(64, 0.5);
            Handle(list, 0).Bounds.Width.ShouldBe(40);
            row.PositionText.ShouldBe("01");
            ((ReorderableListItem)list.ContainerFromIndex(3)!).PositionText.ShouldBe("04");
            Avalonia.Automation.AutomationProperties.GetName(Handle(list, 0))
                    .ShouldBe("Reorder Ship pricing page. Position 1 of 4. Use arrow keys.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_At_Once_And_Keep_Focus()
    {
        var items = Entries();
        var events = new List<ReorderableListReorderedEventArgs>();
        var (list, window) = Show(new ReorderableList { ItemsSource = items });
        list.Reordered += (_, e) => events.Add(e);
        try
        {
            Handle(list, 0).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Order(items).ShouldBe("bacd");
            events.Single().OldIndex.ShouldBe(0);
            events.Single().NewIndex.ShouldBe(1);
            Handle(list, 1).IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Order(items).ShouldBe("bcad");
            ((ReorderableListItem)list.ContainerFromIndex(2)!).PositionText.ShouldBe("03");
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Order(items).ShouldBe("bacd");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Space_Picks_Up_Arrows_Preview_Space_Drops()
    {
        var items = Entries();
        var (list, window) = Show(new ReorderableList { ItemsSource = items });
        var announcer = list.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == ReorderableList.AnnouncerPart);
        try
        {
            Handle(list, 0).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            list.IsDragging.ShouldBeTrue();
            ((ReorderableListItem)list.ContainerFromIndex(0)!).Classes.Contains(":lifted").ShouldBeTrue();
            announcer.Text!.ShouldStartWith("Picked up Ship pricing page");
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Order(items).ShouldBe("abcd");
            ((ReorderableListItem)list.ContainerFromIndex(0)!).PositionText.ShouldBe("03");
            ((ReorderableListItem)list.ContainerFromIndex(0)!).Offset.ShouldBe(128, 0.5);
            ((ReorderableListItem)list.ContainerFromIndex(1)!).Offset.ShouldBe(-64, 0.5);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Order(items).ShouldBe("bcad");
            list.IsDragging.ShouldBeFalse();
            announcer.Text.ShouldBe("Ship pricing page dropped at position 3 of 4.");
            Handle(list, 2).IsFocused.ShouldBeTrue();
            ((ReorderableListItem)list.ContainerFromIndex(2)!).Offset.ShouldBe(0, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Cancels_A_Pick_Up()
    {
        var items = Entries();
        var (list, window) = Show(new ReorderableList { ItemsSource = items });
        try
        {
            Handle(list, 1).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Order(items).ShouldBe("abcd");
            list.IsDragging.ShouldBeFalse();
            ((ReorderableListItem)list.ContainerFromIndex(1)!).Offset.ShouldBe(0, 0.5);
            ((ReorderableListItem)list.ContainerFromIndex(1)!).PositionText.ShouldBe("02");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Drag_On_The_Grip_Reorders()
    {
        var items = Entries();
        var (list, window) = Show(new ReorderableList { ItemsSource = items });
        try
        {
            var handle = Handle(list, 0);
            var start = handle.TranslatePoint(new Point(20, 20), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            list.IsDragging.ShouldBeTrue();
            window.MouseMove(start + new Vector(0, 40));
            window.MouseMove(start + new Vector(0, 100));
            window.MouseMove(start + new Vector(0, 140));
            Dispatcher.UIThread.RunJobs();
            ((ReorderableListItem)list.ContainerFromIndex(0)!).Offset.ShouldBe(140, 0.5);
            ((ReorderableListItem)list.ContainerFromIndex(2)!).PositionText.ShouldBe("02");
            window.MouseUp(start + new Vector(0, 140), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Order(items).ShouldBe("bcad");
            list.IsDragging.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Immutable_Sources_Only_Report_The_Move()
    {
        var items = Entries().ToArray();
        var events = new List<ReorderableListReorderedEventArgs>();
        var (list, window) = Show(new ReorderableList { ItemsSource = items });
        list.Reordered += (_, e) => events.Add(e);
        try
        {
            Handle(list, 3).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Order(items).ShouldBe("abcd");
            events.Single().NewIndex.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Inline_Items_Are_Reordered()
    {
        var list = new ReorderableList();
        list.Items.Add("One");
        list.Items.Add("Two");
        list.Items.Add("Three");
        var (_, window) = Show(list);
        try
        {
            list.MoveItem(0, 2);
            Dispatcher.UIThread.RunJobs();
            list.Items.Cast<string>().ShouldBe(["Two", "Three", "One"]);
        }
        finally
        {
            window.Close();
        }
    }
}
