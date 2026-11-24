using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

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

    // #193: inside a height-limited ScrollViewer the lifted row and its shadow are drawn in the window's overlay layer, so
    // the scroll viewport does not clip them; the other rows keep clipping, and the reorder itself is unchanged.

    private static ObservableCollection<ReorderableListEntry> ManyEntries() =>
        new("abcdefghijkl".Select(c => new ReorderableListEntry(c.ToString(), $"Task {c}", "Detail")));

    private static (ReorderableList List, ScrollViewer Scroller, Window Window) ShowScrolled(
        ObservableCollection<ReorderableListEntry> items, double offset = 128)
    {
        var list = new ReorderableList { ItemsSource = items, Width = 360 };
        var scroller = new ScrollViewer
        {
            Height = 200,
            Width = 380,
            Margin = new Thickness(40),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Content = list
        };
        var window = TestHelpers.Show(scroller);
        window.Width = 500;
        window.Height = 500;
        Dispatcher.UIThread.RunJobs();
        scroller.Offset = new Vector(0, offset);
        Dispatcher.UIThread.RunJobs();
        return (list, scroller, window);
    }

    private static Rect InWindow(Visual visual, Window window) =>
        new Rect(visual.Bounds.Size).TransformToAABB(visual.TransformToVisual(window)!.Value);

    private static Visual TemplateRoot(ReorderableListItem row) => row.GetVisualChildren().First();

    [AvaloniaFact]
    public void Dragged_Row_Is_Drawn_Above_The_Scroll_Viewport()
    {
        // Scrolled to the end (760px of rows in a 200px viewport), so holding the row at the bottom edge does not
        // auto-scroll and the geometry stays exact.
        var items = ManyEntries();
        var (list, scroller, window) = ShowScrolled(items, offset: 560);
        try
        {
            var row = (ReorderableListItem)list.ContainerFromIndex(9)!;
            var start = row.Handle!.TranslatePoint(new Point(20, 20), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            var preview = list.DragPreview.ShouldNotBeNull();
            // The preview lives on the window's overlay layer, outside the scroll viewer; the row in the list is the
            // transparent stand-in that keeps the springs, capture and focus.
            preview.Layer.ShouldBeOfType<OverlayLayer>();
            preview.GetVisualAncestors().ShouldNotContain(scroller);
            preview.GetVisualAncestors().ShouldContain(window);
            preview.IsHitTestVisible.ShouldBeFalse();
            row.Opacity.ShouldBe(0);

            window.MouseMove(start + new Vector(0, 60));
            window.MouseMove(start + new Vector(0, 200));
            Dispatcher.UIThread.RunJobs();
            // Clamped 24px past the list's end, as before.
            row.Offset.ShouldBe(152, 0.5);
            // The preview covers the row exactly (scroll offset included) and reaches past the bottom of the viewport.
            var drawn = InWindow(preview, window);
            var model = InWindow(TemplateRoot(row), window);
            drawn.Top.ShouldBe(model.Top, 0.5);
            drawn.Left.ShouldBe(model.Left, 0.5);
            drawn.Width.ShouldBe(model.Width, 0.5);
            drawn.Height.ShouldBe(model.Height, 0.5);
            var viewport = InWindow(scroller, window);
            drawn.Top.ShouldBe(viewport.Top + 576 - 560 + 152, 0.5);
            drawn.Bottom.ShouldBe(viewport.Bottom + 24, 0.5);

            // Scrolling under a held row: the preview follows the row after the layout pass.
            scroller.Offset = new Vector(0, 500);
            Dispatcher.UIThread.RunJobs();
            InWindow(preview, window).Top.ShouldBe(InWindow(TemplateRoot(row), window).Top, 0.5);
            scroller.Offset = new Vector(0, 560);
            Dispatcher.UIThread.RunJobs();

            window.MouseUp(start + new Vector(0, 200), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Order(items).ShouldBe("abcdefghiklj");
            list.IsDragging.ShouldBeFalse();
            list.DragPreview.ShouldBeNull();
            preview.GetVisualParent().ShouldBeNull();
            preview.Layer.Children.ShouldNotContain(preview);
            for (var i = 0; i < items.Count; i++)
            {
                var container = (ReorderableListItem)list.ContainerFromIndex(i)!;
                container.Opacity.ShouldBe(1);
                container.Preview.ShouldBeNull();
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Preview_Covers_The_Row_In_A_Right_To_Left_Window()
    {
        // The overlay layer undoes its RTL parent's mirror; the preview must still land on the row, unmirrored.
        var items = ManyEntries();
        var (list, _, window) = ShowScrolled(items, offset: 560);
        window.FlowDirection = Avalonia.Media.FlowDirection.RightToLeft;
        Dispatcher.UIThread.RunJobs();
        try
        {
            var row = (ReorderableListItem)list.ContainerFromIndex(9)!;
            var start = row.Handle!.TranslatePoint(new Point(20, 20), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(0, 200));
            Dispatcher.UIThread.RunJobs();
            var preview = list.DragPreview.ShouldNotBeNull();
            preview.FlowDirection.ShouldBe(Avalonia.Media.FlowDirection.LeftToRight);
            var drawn = InWindow(preview, window);
            var model = InWindow(TemplateRoot(row), window);
            drawn.Left.ShouldBe(model.Left, 0.5);
            drawn.Right.ShouldBe(model.Right, 0.5);
            drawn.Top.ShouldBe(model.Top, 0.5);
            // Same orientation: the row's local origin maps to the same window point through both.
            var viaPreview = new Point(0, 0).Transform(preview.TransformToVisual(window)!.Value);
            var viaRow = new Point(0, 0).Transform(TemplateRoot(row).TransformToVisual(window)!.Value);
            viaPreview.X.ShouldBe(viaRow.X, 0.5);
            viaPreview.Y.ShouldBe(viaRow.Y, 0.5);
            window.MouseUp(start + new Vector(0, 200), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Order(items).ShouldBe("abcdefghiklj");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_During_A_Pointer_Drag_Removes_The_Preview()
    {
        var items = ManyEntries();
        var (list, _, window) = ShowScrolled(items);
        try
        {
            var row = (ReorderableListItem)list.ContainerFromIndex(3)!;
            var start = row.Handle!.TranslatePoint(new Point(20, 20), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(0, 100));
            Dispatcher.UIThread.RunJobs();
            var preview = list.DragPreview.ShouldNotBeNull();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            list.IsDragging.ShouldBeFalse();
            preview.GetVisualParent().ShouldBeNull();
            row.Opacity.ShouldBe(1);
            row.Preview.ShouldBeNull();
            window.MouseUp(start + new Vector(0, 100), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Order(items).ShouldBe("abcdefghijkl");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Pick_Up_Uses_The_Preview_And_Escape_Removes_It()
    {
        var items = ManyEntries();
        var (list, _, window) = ShowScrolled(items);
        try
        {
            var row = (ReorderableListItem)list.ContainerFromIndex(3)!;
            row.Handle!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            var preview = list.DragPreview.ShouldNotBeNull();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            InWindow(preview, window).Top.ShouldBe(InWindow(TemplateRoot(row), window).Top, 0.5);
            row.Handle.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            list.DragPreview.ShouldBeNull();
            preview.GetVisualParent().ShouldBeNull();
            row.Opacity.ShouldBe(1);
            Order(items).ShouldBe("abcdefghijkl");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Losing_Pointer_Capture_Ends_The_Drag_And_Removes_The_Preview()
    {
        var items = ManyEntries();
        var (list, _, window) = ShowScrolled(items);
        try
        {
            var row = (ReorderableListItem)list.ContainerFromIndex(3)!;
            IPointer? pointer = null;
            row.Handle!.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer,
                Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
            var start = row.Handle.TranslatePoint(new Point(20, 20), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(0, 70));
            Dispatcher.UIThread.RunJobs();
            var preview = list.DragPreview.ShouldNotBeNull();
            pointer.ShouldNotBeNull().Capture(null);
            Dispatcher.UIThread.RunJobs();
            list.IsDragging.ShouldBeFalse();
            preview.GetVisualParent().ShouldBeNull();
            foreach (var container in list.GetRealizedContainers().OfType<ReorderableListItem>())
            {
                container.Opacity.ShouldBe(1);
            }
            window.MouseUp(start + new Vector(0, 70), MouseButton.Left);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removing_The_List_Mid_Drag_Cancels_And_Removes_The_Preview()
    {
        var items = ManyEntries();
        var (list, scroller, window) = ShowScrolled(items);
        try
        {
            var row = (ReorderableListItem)list.ContainerFromIndex(3)!;
            var start = row.Handle!.TranslatePoint(new Point(20, 20), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(0, 110));
            Dispatcher.UIThread.RunJobs();
            var preview = list.DragPreview.ShouldNotBeNull();
            var layer = preview.Layer;
            window.Content = null;
            Dispatcher.UIThread.RunJobs();
            list.IsDragging.ShouldBeFalse();
            preview.GetVisualParent().ShouldBeNull();
            layer.Children.OfType<ReorderableListDragPreview>().ShouldBeEmpty();
            row.Opacity.ShouldBe(1);
            Order(items).ShouldBe("abcdefghijkl");
            window.MouseUp(start + new Vector(0, 110), MouseButton.Left);
            _ = scroller;
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_Leaves_Nothing_Behind_On_The_Window()
    {
        // The list itself is not a good probe here: Avalonia's headless mouse keeps the element it last pressed alive (a
        // plain press and release on a grip does that, preview or not). The previews must go: nothing but the drag holds them.
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var window = TestHelpers.Show(new Border(), ArcMotionLevel.Full);
        window.Width = 500;
        window.Height = 500;
        try
        {
            var previews = DragTwiceAndRemove(window, clock);
            clock.Advance(TimeSpan.FromSeconds(0.2));
            TestHelpers.Collect(previews);
            previews.ShouldAllBe(p => !p.IsAlive);
            OverlayLayer.GetOverlayLayer(window)?.Children.OfType<ReorderableListDragPreview>().ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference[] DragTwiceAndRemove(Window window, ManualFrameClock clock)
    {
        var list = new ReorderableList { ItemsSource = ManyEntries(), Width = 360 };
        window.Content = new ScrollViewer { Height = 200, Content = list };
        Dispatcher.UIThread.RunJobs();
        var start = ((ReorderableListItem)list.ContainerFromIndex(1)!).Handle!.TranslatePoint(new Point(20, 20), window)!.Value;
        window.MouseDown(start, MouseButton.Left);
        clock.Advance(TimeSpan.FromSeconds(0.3));
        window.MouseMove(start + new Vector(0, 70));
        clock.Advance(TimeSpan.FromSeconds(0.3));
        Dispatcher.UIThread.RunJobs();
        var first = new WeakReference(list.DragPreview.ShouldNotBeNull());
        window.MouseUp(start + new Vector(0, 70), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        list.DragPreview.ShouldBeNull();
        clock.Advance(TimeSpan.FromSeconds(1.5));
        Dispatcher.UIThread.RunJobs();
        // A second drag, cut short by removing the list.
        start = ((ReorderableListItem)list.ContainerFromIndex(2)!).Handle!.TranslatePoint(new Point(20, 20), window)!.Value;
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Vector(0, 30));
        clock.Advance(TimeSpan.FromSeconds(0.3));
        Dispatcher.UIThread.RunJobs();
        var second = new WeakReference(list.DragPreview.ShouldNotBeNull());
        window.Content = null;
        Dispatcher.UIThread.RunJobs();
        list.DragPreview.ShouldBeNull();
        window.MouseUp(start + new Vector(0, 30), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        return [first, second];
    }
}
