using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Menus;

public class RadialMenuTests
{
    private static RadialMenu CreateMenu(int count = 4)
    {
        var menu = new RadialMenu();
        string[] names = ["Copy", "Paste", "Rename", "Delete", "Share", "Pin", "Archive", "Print"];
        foreach (var name in names.Take(count))
        {
            menu.Items.Add(new RadialMenuItem { Header = name, Icon = ArcIcons.Copy });
        }
        return menu;
    }

    private static RadialMenuItem Item(RadialMenu menu, int index) => (RadialMenuItem)menu.Items[index]!;

    private static Border CreateTarget() => new() { Background = Brushes.Transparent, Focusable = true };

    private static void Tick()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>A point at <paramref name="radius"/> from the ring's centre toward <paramref name="degrees"/> (ring coordinates).</summary>
    private static Point RingPoint(RadialMenu menu, double degrees, double radius)
    {
        var center = new Point(menu.Bounds.Width / 2, menu.Bounds.Height / 2);
        var radians = degrees * Math.PI / 180;
        return center + new Vector(Math.Sin(radians), -Math.Cos(radians)) * radius;
    }

    /// <summary>The same point in window coordinates.</summary>
    private static Point WindowPoint(RadialMenu menu, Window window, double degrees, double radius) =>
        menu.TranslatePoint(RingPoint(menu, degrees, radius), window)!.Value;

    #region Layout

    [Fact]
    public void A_Full_Circle_Centres_The_First_Item_On_The_Start_Angle()
    {
        var mids = Enumerable.Range(0, 4).Select(i => RadialMenuLayout.Slot(i, 4, 0, 360).Mid).ToArray();
        mids.ShouldBe([0, 90, 180, 270]);
        var eight = Enumerable.Range(0, 8).Select(i => RadialMenuLayout.Slot(i, 8, 0, 360)).ToArray();
        eight.ShouldAllBe(s => Math.Abs(s.Sweep - 45) < 1e-9);
        eight[0].Start.ShouldBe(-22.5);
        RadialMenuLayout.Slot(1, 3, 30, 360).Mid.ShouldBe(150);
    }

    [Fact]
    public void A_Sector_Starts_The_First_Item_At_The_Start_Angle_And_Shares_The_Sweep()
    {
        var slots = Enumerable.Range(0, 3).Select(i => RadialMenuLayout.Slot(i, 3, -90, 180)).ToArray();
        slots.Select(s => s.Start).ShouldBe([-90, -30, 30]);
        slots.Select(s => s.End).ShouldBe([-30, 30, 90]);
        RadialMenuLayout.ClampSweep(720).ShouldBe(360);
        RadialMenuLayout.ClampSweep(-5).ShouldBe(1);
    }

    [AvaloniaFact]
    public void Containers_Get_Their_Sectors_And_Hidden_Items_Take_None()
    {
        var menu = CreateMenu(4);
        var window = TestHelpers.Show(menu);
        try
        {
            menu.Bounds.Width.ShouldBe(2 * (124 + 6));
            Item(menu, 1).Slot.Mid.ShouldBe(90);
            Item(menu, 3).Slot.Mid.ShouldBe(270);
            Item(menu, 1).IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            Item(menu, 0).Slot.Sweep.ShouldBe(120);
            Item(menu, 2).Slot.Mid.ShouldBe(120);
            Item(menu, 3).Slot.Mid.ShouldBe(240);
            menu.VisibleItems().Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sector_Menus_Lay_Out_Within_Their_Sweep()
    {
        var menu = CreateMenu(3);
        menu.StartAngle = -90;
        menu.SweepAngle = 180;
        var window = TestHelpers.Show(menu);
        try
        {
            menu.Classes.Contains(RadialMenu.SectorPseudoClass).ShouldBeTrue();
            Item(menu, 0).Slot.ShouldBe(new RadialMenuSlot(-90, 60));
            Item(menu, 2).Slot.ShouldBe(new RadialMenuSlot(30, 60));
            Item(menu, 0).SectorGeometry.ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Hit-testing

    [AvaloniaFact]
    public void Ring_Hit_Testing_Maps_Angle_And_Radius_To_Items()
    {
        var menu = CreateMenu(4);
        var window = TestHelpers.Show(menu);
        try
        {
            menu.ItemAtRing(RingPoint(menu, 0, 20)).ShouldBeNull("the centre belongs to no item");
            menu.ItemAtRing(RingPoint(menu, 0, 84)).ShouldBe(Item(menu, 0));
            menu.ItemAtRing(RingPoint(menu, 90, 84)).ShouldBe(Item(menu, 1));
            menu.ItemAtRing(RingPoint(menu, 44.9, 84)).ShouldBe(Item(menu, 0), "the gap belongs to the nearer item");
            menu.ItemAtRing(RingPoint(menu, 45.1, 84)).ShouldBe(Item(menu, 1));
            menu.ItemAtRing(RingPoint(menu, 315.1, 84)).ShouldBe(Item(menu, 0), "the first item wraps across 12 o'clock");
            menu.ItemAtRing(RingPoint(menu, 180, 128)).ShouldBe(Item(menu, 2), "the plate's rim still counts");
            menu.ItemAtRing(RingPoint(menu, 180, 140)).ShouldBeNull("outside the plate");
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Index_At_Is_Forgiving_Past_A_Sectors_Ends_Only_Within_The_Tolerance()
    {
        RadialMenuLayout.IndexAt(0, 3, -90, 180).ShouldBe(1);
        RadialMenuLayout.IndexAt(95, 3, -90, 180).ShouldBe(-1);
        RadialMenuLayout.IndexAt(95, 3, -90, 180, tolerance: 30).ShouldBe(2);
        RadialMenuLayout.IndexAt(265, 3, -90, 180, tolerance: 30).ShouldBe(0);
        RadialMenuLayout.IndexAt(180, 3, -90, 180, tolerance: 30).ShouldBe(-1);
        RadialMenuLayout.AngleOf(new Point(0, 0), new Point(10, 0)).ShouldBe(90, 1e-9);
        RadialMenuLayout.AngleOf(new Point(0, 0), new Point(0, -10)).ShouldBe(0, 1e-9);
        RadialMenuLayout.AngleOf(new Point(0, 0), new Point(-10, 0)).ShouldBe(270, 1e-9);
    }

    [AvaloniaFact]
    public void The_Gesture_Has_A_Dead_Zone_And_Keeps_Its_Direction_Beyond_The_Ring()
    {
        var menu = CreateMenu(4);
        var target = CreateTarget();
        var window = TestHelpers.Show(target);
        try
        {
            menu.OpenAt(target, new Point(200, 150));
            Dispatcher.UIThread.RunJobs();
            menu.BeginGesture(new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true), RingPoint(menu, 0, 0));
            menu.GestureTarget(RingPoint(menu, 90, 30)).ShouldBeNull("inside the dead zone");
            menu.GestureTarget(RingPoint(menu, 90, 60)).ShouldBe(Item(menu, 1));
            menu.GestureTarget(RingPoint(menu, 180, 400)).ShouldBe(Item(menu, 2), "far beyond the outer radius");
            Item(menu, 2).IsEnabled = false;
            menu.GestureTarget(RingPoint(menu, 180, 400)).ShouldBeNull("a disabled item cannot be chosen");
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Item convention

    [AvaloniaFact]
    public void Disabled_And_Hidden_Items_Are_Skipped()
    {
        var menu = CreateMenu(4);
        var invoked = 0;
        menu.ItemInvoked += (_, _) => invoked++;
        var window = TestHelpers.Show(menu);
        try
        {
            Item(menu, 1).IsEnabled = false;
            Item(menu, 2).IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            menu.ItemAtRing(RingPoint(menu, 120, 84)).ShouldBeNull("disabled items are not highlighted by the pointer");
            menu.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            menu.SelectedItem.ShouldBe(Item(menu, 0));
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            menu.SelectedItem.ShouldBe(Item(menu, 3), "the disabled and the hidden item are skipped");
            menu.Invoke(Item(menu, 1));
            invoked.ShouldBe(0);
            menu.SelectedItem = Item(menu, 1);
            Item(menu, 1).IsHighlighted.ShouldBeFalse("a disabled item keeps the value but shows no highlight");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Command_That_Cannot_Execute_Disables_Its_Item()
    {
        var command = new TestCommand { CanRun = false };
        var menu = CreateMenu(2);
        Item(menu, 0).Command = command;
        var window = TestHelpers.Show(menu);
        try
        {
            Item(menu, 0).IsEffectivelyEnabled.ShouldBeFalse();
            Item(menu, 0).Classes.Contains(":disabled").ShouldBeTrue();
            command.CanRun = true;
            Item(menu, 0).IsEffectivelyEnabled.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Keyboard

    [AvaloniaFact]
    public void Keyboard_Moves_Around_The_Ring_And_Enter_Chooses()
    {
        var menu = CreateMenu(4);
        var target = CreateTarget();
        RadialMenu.SetMenu(target, menu);
        object? chosen = null;
        menu.ItemInvoked += (_, e) => chosen = e.Item;
        var window = TestHelpers.Show(target);
        try
        {
            target.Focus();
            target.RaiseEvent(new ContextRequestedEventArgs());
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            menu.SelectedItem.ShouldBe(Item(menu, 0), "a keyboard open highlights the first item");
            Item(menu, 0).IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            menu.SelectedItem.ShouldBe(Item(menu, 3), "arrows wrap around the ring");
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            menu.SelectedItem.ShouldBe(Item(menu, 0));
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
            menu.SelectedItem.ShouldBe(Item(menu, 3));
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            menu.SelectedItem.ShouldBe(Item(menu, 0));
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            menu.SelectedItem.ShouldBe(Item(menu, 3));
            window.KeyPressQwerty(PhysicalKey.R, RawInputModifiers.None);
            menu.SelectedItem.ShouldBe(Item(menu, 2), "typing a letter jumps to the item it starts");
            menu.CenterText.ShouldBe("Rename");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            chosen.ShouldBe(Item(menu, 2));
            menu.IsOpen.ShouldBeFalse();
            target.IsFocused.ShouldBeTrue("focus goes back to the control");
            target.Classes.Contains(":focus-visible").ShouldBeTrue("a keyboard choice returns focus visibly");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Closes_Without_Choosing_And_Returns_Focus()
    {
        var menu = CreateMenu(4);
        var target = CreateTarget();
        RadialMenu.SetMenu(target, menu);
        var invoked = 0;
        var closed = 0;
        menu.ItemInvoked += (_, _) => invoked++;
        menu.Closed += (_, _) => closed++;
        var window = TestHelpers.Show(target);
        try
        {
            target.Focus();
            window.KeyPressQwerty(PhysicalKey.F10, RawInputModifiers.Shift);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeFalse();
            menu.IsMounted.ShouldBeFalse();
            closed.ShouldBe(1);
            invoked.ShouldBe(0);
            target.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Commands and events

    [AvaloniaFact]
    public void Choosing_Raises_ItemInvoked_Then_Runs_The_Item_And_Menu_Commands()
    {
        var order = new List<string>();
        var itemCommand = new TestCommand { OnExecute = p => order.Add($"item:{p}") };
        var menuCommand = new TestCommand { OnExecute = p => order.Add($"menu:{(p as RadialMenuItem)?.Header}") };
        var menu = CreateMenu(3);
        Item(menu, 1).Command = itemCommand;
        Item(menu, 1).CommandParameter = 42;
        menu.ItemInvokedCommand = menuCommand;
        RadialMenuItemInvokedEventArgs? args = null;
        menu.ItemInvoked += (_, e) =>
        {
            args = e;
            order.Add("event");
        };
        var target = CreateTarget();
        var window = TestHelpers.Show(target);
        try
        {
            menu.OpenAt(target, new Point(200, 150));
            Dispatcher.UIThread.RunJobs();
            Item(menu, 1).PerformClick();
            Dispatcher.UIThread.RunJobs();
            order.ShouldBe(["event", "item:42", "menu:Paste"]);
            args!.Item.ShouldBe(Item(menu, 1));
            args.Container.ShouldBe(Item(menu, 1));
            menu.SelectedItem.ShouldBe(Item(menu, 1), "the chosen item stays selected");
            menu.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectionChanged_Reports_Every_Change_Of_The_Highlight()
    {
        var menu = CreateMenu(3);
        var changes = new List<(object? Old, object? New)>();
        menu.SelectionChanged += (_, e) => changes.Add((e.OldItem, e.NewItem));
        var window = TestHelpers.Show(menu);
        try
        {
            menu.SetHighlight(Item(menu, 0));
            menu.SelectedItem = Item(menu, 2);
            Item(menu, 2).IsHighlighted.ShouldBeTrue();
            Item(menu, 0).IsHighlighted.ShouldBeFalse();
            menu.SetHighlight(null);
            changes.ShouldBe([(null, Item(menu, 0)), (Item(menu, 0), Item(menu, 2)), (Item(menu, 2), null)]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opened_And_Closed_Follow_IsOpen_And_The_Placement_Target()
    {
        var menu = CreateMenu(3);
        var target = CreateTarget();
        menu.PlacementTarget = target;
        var opened = 0;
        var closed = 0;
        menu.Opened += (_, _) => opened++;
        menu.Closed += (_, _) => closed++;
        var window = TestHelpers.Show(target);
        try
        {
            menu.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            opened.ShouldBe(1);
            menu.IsMounted.ShouldBeTrue();
            menu.HostCenter.ShouldBe(new Point(200, 150), "centred on the target");
            menu.Parent.ShouldBe(target, "the target is the logical parent while open");
            menu.IsOpen = false;
            Dispatcher.UIThread.RunJobs();
            closed.ShouldBe(1);
            menu.IsMounted.ShouldBeFalse();
            menu.Parent.ShouldBeNull();
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            opened.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Closing_With_Full_Motion_Raises_Closed_After_The_Exit()
    {
        var menu = CreateMenu(3);
        var target = CreateTarget();
        var closed = 0;
        menu.Closed += (_, _) => closed++;
        var window = TestHelpers.Show(target, Motion.ArcMotionLevel.Full);
        try
        {
            menu.OpenAt(target, new Point(200, 150));
            Dispatcher.UIThread.RunJobs();
            menu.Opacity.ShouldBeLessThan(1, "the ring fades in");
            TestHelpers.PumpUntil(() => menu.Opacity >= 1).ShouldBeTrue();
            menu.Close();
            menu.IsOpen.ShouldBeFalse();
            closed.ShouldBe(0, "Closed waits for the exit");
            menu.IsMounted.ShouldBeTrue();
            TestHelpers.PumpUntil(() => closed == 1).ShouldBeTrue();
            menu.IsMounted.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Pointer gestures

    [AvaloniaFact]
    public void Right_Press_Drag_And_Release_Chooses_The_Item_In_That_Direction()
    {
        var menu = CreateMenu(4);
        var target = CreateTarget();
        RadialMenu.SetMenu(target, menu);
        object? chosen = null;
        menu.ItemInvoked += (_, e) => chosen = e.Item;
        var window = TestHelpers.Show(target);
        try
        {
            Tick();
            window.MouseDown(new Point(200, 150), MouseButton.Right);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            menu.IsGestureActive.ShouldBeTrue();
            menu.HostCenter.ShouldBe(new Point(200, 150));
            // Toward 3 o'clock, past the outer radius: the direction still chooses.
            window.MouseMove(new Point(395, 150), RawInputModifiers.RightMouseButton);
            menu.SelectedItem.ShouldBe(Item(menu, 1));
            menu.CenterText.ShouldBe("Paste");
            window.MouseUp(new Point(395, 150), MouseButton.Right);
            Dispatcher.UIThread.RunJobs();
            chosen.ShouldBe(Item(menu, 1));
            menu.IsOpen.ShouldBeFalse();
            Tick();
            window.MouseMove(new Point(20, 20));
            menu.SelectedItem.ShouldBe(Item(menu, 1), "the chosen item stays selected after the ring leaves");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Releasing_Back_In_The_Dead_Zone_Cancels()
    {
        var menu = CreateMenu(4);
        var target = CreateTarget();
        RadialMenu.SetMenu(target, menu);
        var invoked = 0;
        menu.ItemInvoked += (_, _) => invoked++;
        var window = TestHelpers.Show(target);
        try
        {
            Tick();
            window.MouseDown(new Point(200, 150), MouseButton.Right);
            window.MouseMove(new Point(200, 60), RawInputModifiers.RightMouseButton);
            menu.SelectedItem.ShouldBe(Item(menu, 0));
            window.MouseMove(new Point(205, 145), RawInputModifiers.RightMouseButton);
            menu.SelectedItem.ShouldBeNull();
            window.MouseUp(new Point(205, 145), MouseButton.Right);
            Dispatcher.UIThread.RunJobs();
            invoked.ShouldBe(0);
            menu.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Right_Click_Leaves_The_Menu_Open_For_A_Click_On_An_Item()
    {
        var menu = CreateMenu(4);
        var target = CreateTarget();
        RadialMenu.SetMenu(target, menu);
        object? chosen = null;
        menu.ItemInvoked += (_, e) => chosen = e.Item;
        var window = TestHelpers.Show(target);
        try
        {
            Tick();
            window.MouseDown(new Point(200, 150), MouseButton.Right);
            window.MouseUp(new Point(200, 150), MouseButton.Right);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue("released without heading anywhere");
            menu.IsGestureActive.ShouldBeFalse();
            Tick();
            var point = WindowPoint(menu, window, 180, 84);
            window.MouseMove(point);
            menu.SelectedItem.ShouldBe(Item(menu, 2));
            window.MouseDown(point, MouseButton.Left);
            Item(menu, 2).Classes.Contains(RadialMenuItem.PressedPseudoClass).ShouldBeTrue();
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            chosen.ShouldBe(Item(menu, 2));
            menu.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Press_Outside_The_Ring_Closes_It()
    {
        var menu = CreateMenu(4);
        var target = CreateTarget();
        var window = TestHelpers.Show(target);
        try
        {
            menu.OpenAt(target, new Point(200, 150));
            Dispatcher.UIThread.RunJobs();
            Tick();
            window.MouseDown(new Point(10, 10), MouseButton.Left);
            window.MouseUp(new Point(10, 10), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeFalse();
            menu.IsMounted.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Press_And_Hold_Opens_At_The_Press_Point_And_Carries_On_As_A_Gesture()
    {
        var menu = CreateMenu(4);
        menu.Trigger = RadialMenuTrigger.Hold;
        var target = CreateTarget();
        RadialMenu.SetMenu(target, menu);
        object? chosen = null;
        menu.ItemInvoked += (_, e) => chosen = e.Item;
        var window = TestHelpers.Show(target);
        try
        {
            Tick();
            window.MouseDown(new Point(180, 140), MouseButton.Right);
            window.MouseUp(new Point(180, 140), MouseButton.Right);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeFalse("right press is not a trigger here");
            window.MouseDown(new Point(180, 140), MouseButton.Left);
            menu.IsOpen.ShouldBeFalse("not before the hold delay");
            RadialMenu.ElapseHold(target);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            menu.IsGestureActive.ShouldBeTrue();
            menu.RequestedCenter.ShouldBe(new Point(180, 140));
            window.MouseMove(new Point(180, 290), RawInputModifiers.LeftMouseButton);
            window.MouseUp(new Point(180, 290), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            chosen.ShouldBe(Item(menu, 2), "toward 6 o'clock");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Hold_Is_Cancelled_When_The_Pointer_Moves_Away()
    {
        var menu = CreateMenu(4);
        var target = CreateTarget();
        RadialMenu.SetMenu(target, menu);
        var window = TestHelpers.Show(target);
        try
        {
            Tick();
            window.MouseDown(new Point(180, 140), MouseButton.Left);
            window.MouseMove(new Point(220, 140), RawInputModifiers.LeftMouseButton);
            RadialMenu.ElapseHold(target);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeFalse();
            window.MouseUp(new Point(220, 140), MouseButton.Left);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Click_Trigger_Opens_On_The_Centre_Of_A_Button()
    {
        var menu = CreateMenu(4);
        menu.Trigger = RadialMenuTrigger.Click;
        var button = new Button { Content = "Tools", Width = 80, Height = 36 };
        RadialMenu.SetMenu(button, menu);
        var window = TestHelpers.Show(button);
        try
        {
            button.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            menu.RequestedCenter.ShouldBe(center);
            menu.SelectedItem.ShouldBe(Item(menu, 0), "a keyboard click highlights the first item");
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Data and edges

    [AvaloniaFact]
    public void ItemsSource_Changes_Reflow_The_Ring_And_Drop_A_Removed_Highlight()
    {
        var tools = new ObservableCollection<string> { "Select", "Pen", "Text", "Shape" };
        var menu = new RadialMenu { ItemsSource = tools };
        var target = CreateTarget();
        var window = TestHelpers.Show(target);
        try
        {
            menu.OpenAt(target, new Point(200, 150));
            Dispatcher.UIThread.RunJobs();
            menu.VisibleItems().Count.ShouldBe(4);
            ((RadialMenuItem)menu.ContainerFromIndex(0)!).Header.ShouldBe("Select");
            tools.Add("Image");
            Dispatcher.UIThread.RunJobs();
            var items = menu.VisibleItems();
            items.Count.ShouldBe(5);
            items[1].Slot.Mid.ShouldBe(72);
            menu.SelectedItem = "Text";
            ((RadialMenuItem)menu.ContainerFromIndex(2)!).IsHighlighted.ShouldBeTrue();
            menu.CenterText.ShouldBe("Text");
            tools.Remove("Text");
            Dispatcher.UIThread.RunJobs();
            menu.SelectedItem.ShouldBeNull();
            menu.VisibleItems().Select(i => i.Header).ShouldBe(["Select", "Pen", "Shape", "Image"]);
            menu.VisibleItems()[2].Slot.Mid.ShouldBe(180);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Generated_Items_Take_Their_Text_Template_And_Styled_Members()
    {
        var tools = new[] { new Tool("Select", true), new Tool("Pen", false) };
        var menu = new RadialMenu { ItemsSource = tools, TextSelector = o => (o as Tool)?.Name };
        menu.Styles.Add(new Style(x => x.OfType<RadialMenuItem>())
        {
            Setters =
            {
                new Setter(RadialMenuItem.IconProperty, ArcIcons.Pencil),
                new Setter(InputElement.IsEnabledProperty, new Binding(nameof(Tool.Enabled)))
            }
        });
        object? chosen = null;
        menu.ItemInvokedCommand = new TestCommand { OnExecute = p => chosen = p };
        var window = TestHelpers.Show(menu);
        try
        {
            var select = (RadialMenuItem)menu.ContainerFromIndex(0)!;
            var pen = (RadialMenuItem)menu.ContainerFromIndex(1)!;
            select.Header.ShouldBe("Select", "the text selector names generated items");
            select.DataContext.ShouldBe(tools[0]);
            select.Icon.ShouldBe(ArcIcons.Pencil);
            pen.IsEffectivelyEnabled.ShouldBeFalse("styles bind members of the data item");
            menu.Invoke(select);
            chosen.ShouldBe(tools[0], "the menu command gets the data item");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Style_Can_Bind_The_Header_Of_Generated_Items()
    {
        var tools = new[] { new Tool("Select", true), new Tool("Pen", true) };
        var menu = new RadialMenu { ItemsSource = tools };
        menu.Styles.Add(new Style(x => x.OfType<RadialMenuItem>())
        {
            Setters = { new Setter(RadialMenuItem.HeaderProperty, new Binding(nameof(Tool.Name))) }
        });
        var window = TestHelpers.Show(menu);
        try
        {
            ((RadialMenuItem)menu.ContainerFromIndex(1)!).Header.ShouldBe("Pen");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opening_Near_An_Edge_Shifts_The_Ring_Into_View()
    {
        var menu = CreateMenu(4);
        var target = CreateTarget();
        var window = TestHelpers.Show(target);
        try
        {
            // The plate is 130px in radius and keeps 8px from the edges of the 400x300 window.
            menu.OpenAt(target, new Point(5, 5));
            Dispatcher.UIThread.RunJobs();
            menu.HostCenter.ShouldBe(new Point(138, 138));
            menu.OpenAt(target, new Point(398, 296));
            Dispatcher.UIThread.RunJobs();
            menu.HostCenter.ShouldBe(new Point(262, 162));
            menu.OpenAt(target, new Point(200, 150));
            Dispatcher.UIThread.RunJobs();
            menu.HostCenter.ShouldBe(new Point(200, 150), "no shift where the ring fits");
            // A sector reaching upward only needs its real extent inside: it may open right above the bottom edge.
            menu.StartAngle = -90;
            menu.SweepAngle = 180;
            menu.OpenAt(target, new Point(200, 290));
            Dispatcher.UIThread.RunJobs();
            menu.HostCenter.ShouldBe(new Point(200, 300 - 8 - 44));
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Shift_Centres_An_Extent_That_Cannot_Fit()
    {
        var extent = new Rect(-130, -130, 260, 260);
        RadialMenuLayout.Shift(new Point(100, 100), extent, new Rect(0, 0, 200, 600)).ShouldBe(new Vector(0, 30));
        var sector = RadialMenuLayout.Extent(44, 124, 6, -90, 180);
        sector.Top.ShouldBe(-130, 1e-9);
        sector.Bottom.ShouldBe(44, 1e-9, "the centre disc reaches below the centre line");
        sector.Left.ShouldBe(-130, 1e-9);
        sector.Right.ShouldBe(130, 1e-9);
        var quarter = RadialMenuLayout.Extent(44, 124, 6, 0, 90);
        quarter.Left.ShouldBe(-44, 1e-9);
        quarter.Top.ShouldBe(-130, 1e-9);
        quarter.Right.ShouldBe(130, 1e-9);
    }

    #endregion

    #region Attachment, inline and accessibility

    [AvaloniaFact]
    public void An_Attached_Menu_Takes_The_Control_As_Its_Logical_Parent()
    {
        var menu = CreateMenu(2);
        var target = CreateTarget();
        var model = new object();
        target.DataContext = model;
        RadialMenu.SetMenu(target, menu);
        var window = TestHelpers.Show(target);
        try
        {
            menu.Parent.ShouldBe(target);
            menu.DataContext.ShouldBe(model, "bindings on the menu work before it opens");
            menu.HomeTarget.ShouldBe(target);
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            menu.Close();
            Dispatcher.UIThread.RunJobs();
            menu.Parent.ShouldBe(target, "it goes back to its control after closing");
            RadialMenu.SetMenu(target, null);
            menu.Parent.ShouldBeNull();
            menu.HomeTarget.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Theme_Changes_Reach_The_Menu_Open_Or_Closed()
    {
        var menu = CreateMenu(3);
        var target = CreateTarget();
        RadialMenu.SetMenu(target, menu);
        var window = TestHelpers.Show(target);
        try
        {
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            menu.SetHighlight(Item(menu, 0));
            var light = TestHelpers.ColorOf(menu.Background);
            await TestHelpers.ApplyAsync(t => t.Appearance(ArcAppearance.Dark).Accent(ArcAccentPreset.Blue));
            TestHelpers.ColorOf(menu.Background).ShouldNotBe(light);
            TestHelpers.ColorOf(menu.Background).ShouldBe(ArcTokens.GetColor(target, DesignTokens.ArcDesignTokenKind.SurfaceRaised));
            TestHelpers.ColorOf(Item(menu, 0).Background).ShouldBe(ArcTokens.GetColor(target, DesignTokens.ArcDesignTokenKind.AccentSubtle));
            menu.Close();
            Dispatcher.UIThread.RunJobs();
            await TestHelpers.ApplyAsync(null);
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            TestHelpers.ColorOf(menu.Background).ShouldBe(light, "a closed menu follows the theme too");
        }
        finally
        {
            await TestHelpers.ApplyAsync(null);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Menu_In_A_Layout_Is_A_Static_Dial_That_Never_Floats()
    {
        var menu = CreateMenu(4);
        var invoked = 0;
        menu.ItemInvoked += (_, _) => invoked++;
        var window = TestHelpers.Show(menu);
        try
        {
            menu.IsInline.ShouldBeTrue();
            menu.Open(menu);
            menu.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            menu.IsMounted.ShouldBeFalse();
            menu.IsOpen.ShouldBeFalse();
            Item(menu, 0).PerformClick();
            invoked.ShouldBe(1);
            menu.IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Screen_Readers_See_A_Menu_Of_Named_Menu_Items()
    {
        var menu = CreateMenu(3);
        menu.Label = "Canvas tools";
        var window = TestHelpers.Show(menu);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(menu);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Menu);
            peer.GetName().ShouldBe("Canvas tools");
            var itemPeer = ControlAutomationPeer.CreatePeerForElement(Item(menu, 1));
            itemPeer.GetAutomationControlType().ShouldBe(AutomationControlType.MenuItem);
            itemPeer.GetName().ShouldBe("Paste");
            var invoked = 0;
            menu.ItemInvoked += (_, _) => invoked++;
            ((Avalonia.Automation.Provider.IInvokeProvider)itemPeer).Invoke();
            invoked.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    private sealed record Tool(string Name, bool Enabled);

    private sealed class TestCommand : ICommand
    {
        private bool _canRun = true;

        public Action<object?>? OnExecute { get; init; }

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

        public void Execute(object? parameter) => OnExecute?.Invoke(parameter);
    }
}
