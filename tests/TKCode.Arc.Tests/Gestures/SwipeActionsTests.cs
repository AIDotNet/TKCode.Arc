using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using static TKCode.Arc.Tests.Buttons.ButtonsTestSupport;
using RubberBand = global::TKCode.Arc.Gestures.RubberBand;

namespace TKCode.Arc.Tests.Gestures;

public class SwipeActionsTests
{
    private const double ListWidth = 360;

    private sealed class Harness
    {
        public required SwipeActions List { get; init; }
        public required Window Window { get; init; }
        public List<SwipeActionInvokedEventArgs> Invoked { get; } = new();

        public SwipeActionsRow Row(int index) => (SwipeActionsRow)List.ContainerFromIndex(index)!;

        public Point Center(Visual visual) =>
            visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), Window)!.Value;
    }

    private static SwipeActionsRow CreateRow(string label, bool removable = true)
    {
        var row = new SwipeActionsRow { Label = label, Content = new TextBlock { Text = label } };
        row.LeadingActions.Add(new SwipeAction { Label = "Unread", Icon = ArcIcons.Mail, Tone = SwipeActionTone.Accent, KeepsRow = true });
        row.TrailingActions.Add(new SwipeAction { Label = "Archive", Icon = ArcIcons.Archive });
        if (removable)
        {
            row.TrailingActions.Add(new SwipeAction { Label = "Delete", Icon = ArcIcons.Trash2, Tone = SwipeActionTone.Danger });
        }
        return row;
    }

    private static Harness Show(Motion.ArcMotionLevel level = Motion.ArcMotionLevel.Off, bool removeOnInvoke = true, params SwipeActionsRow[] rows)
    {
        var list = new SwipeActions { Label = "Inbox", Width = ListWidth, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        foreach (var row in rows.Length > 0 ? rows : [CreateRow("First"), CreateRow("Second"), CreateRow("Third")])
        {
            list.Items.Add(row);
        }
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { list } }, level);
        var harness = new Harness { List = list, Window = window };
        list.ActionInvoked += (_, e) =>
        {
            harness.Invoked.Add(e);
            if (removeOnInvoke && !e.Action.KeepsRow)
            {
                list.Items.Remove(e.Row);
            }
        };
        return harness;
    }

    private static double ContentX(SwipeActionsRow row) =>
        ((TranslateTransform)Part<Panel>(row, SwipeActionsRow.ContentHostPart).RenderTransform!).X;

    private static Button More(SwipeActionsRow row) => Part<Button>(row, SwipeActionsRow.MoreButtonPart);

    /// <summary>Drags from the row's content (crossing the slop first), pausing before the release so the release velocity is zero.</summary>
    private static void Drag(Harness h, SwipeActionsRow row, double dx, double dy = 0)
    {
        var start = h.Center(row) - new Point(60, 0);
        var lockStep = new Point(Math.Sign(dx) * (dy == 0 ? 10 : 2), Math.Sign(dy) * 12);
        var end = new Point(start.X + dx + lockStep.X, start.Y + dy + lockStep.Y);
        h.Window.MouseDown(start, MouseButton.Left);
        h.Window.MouseMove(start + lockStep, RawInputModifiers.LeftMouseButton);
        h.Window.MouseMove(end, RawInputModifiers.LeftMouseButton);
        Pump(90);
        h.Window.MouseUp(end, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var h = Show();
        try
        {
            var row = h.Row(0);
            row.Bounds.Height.ShouldBeGreaterThanOrEqualTo(64);
            row.ActionExtent.ShouldBe(76);
            row.OpenSide.ShouldBe(SwipeActionSide.None);
            Peer(h.List).GetAutomationControlType().ShouldBe(AutomationControlType.List);
            Peer(h.List).GetName().ShouldBe("Inbox");
            Peer(row).GetAutomationControlType().ShouldBe(AutomationControlType.ListItem);
            AutomationProperties.GetName(More(row)).ShouldBe("More actions for First");
            More(row).IsPressScaleEnabled.ShouldBeFalse();
            More(row).Bounds.Width.ShouldBe(32, 0.5);
            Part<Border>(row, SwipeActionsRow.SeparatorPart).Margin.Left.ShouldBe(16);
            // The revealed copy is hidden from assistive technology and the tab order.
            var layers = Part<SwipeActionsRowLayers>(row, SwipeActionsRow.ActionsPart);
            AutomationProperties.GetAccessibilityView(layers).ShouldBe(AccessibilityView.Raw);
            layers.Focusable.ShouldBeFalse();
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void More_Menu_Lists_The_Same_Actions_And_Removing_Hands_Focus_To_The_Neighbour()
    {
        var h = Show();
        try
        {
            var first = h.Row(0);
            var second = h.Row(1);
            More(first).Focus(NavigationMethod.Tab);
            h.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            var menu = first.Menu!;
            menu.IsOpen.ShouldBeTrue();
            menu.Items.Cast<MenuEntry>().Select(e => e.Header).ShouldBe(["Unread", "Archive", "Delete"]);
            ((MenuEntry)menu.Items[2]!).IsDestructive.ShouldBeTrue();
            menu.HighlightedEntry!.Header.ShouldBe("Unread");
            var peer = (Avalonia.Automation.Provider.IExpandCollapseProvider)Peer(More(first)).GetProvider<Avalonia.Automation.Provider.IExpandCollapseProvider>()!;
            peer.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);

            h.Window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            h.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            h.Invoked.Count.ShouldBe(1);
            h.Invoked[0].Action.Label.ShouldBe("Archive");
            h.Invoked[0].Side.ShouldBe(SwipeActionSide.Trailing);
            h.Invoked[0].Item.ShouldBeSameAs(first);
            h.List.Items.Count.ShouldBe(2);
            More(second).IsFocused.ShouldBeTrue();
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Action_That_Keeps_The_Row_Runs_At_Once_And_The_Row_Stays()
    {
        var h = Show();
        try
        {
            var row = h.Row(0);
            var ran = 0;
            row.LeadingActions[0].Invoked += (_, _) => ran++;
            row.Run(row.LeadingActions[0]);
            Dispatcher.UIThread.RunJobs();
            ran.ShouldBe(1);
            h.Invoked.Single().Action.KeepsRow.ShouldBeTrue();
            row.IsLeaving.ShouldBeFalse();
            h.List.Items.Count.ShouldBe(3);
            row.Offset.ShouldBe(0);
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Open_Rests_On_The_Action_Width_And_Only_One_Row_Stays_Open()
    {
        var h = Show();
        try
        {
            var first = h.Row(0);
            var second = h.Row(1);
            first.Open(SwipeActionSide.Trailing);
            Dispatcher.UIThread.RunJobs();
            first.OpenSide.ShouldBe(SwipeActionSide.Trailing);
            first.Offset.ShouldBe(-152);
            ContentX(first).ShouldBe(-152);
            first.Classes.ShouldContain(SwipeActionsRow.OpenPseudoClass);
            h.List.OpenRow.ShouldBe(first);

            second.Open(SwipeActionSide.Leading);
            Dispatcher.UIThread.RunJobs();
            second.Offset.ShouldBe(76);
            first.Offset.ShouldBe(0);
            first.OpenSide.ShouldBe(SwipeActionSide.None);
            h.List.OpenRow.ShouldBe(second);

            // Escape puts the open row away.
            h.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            second.Offset.ShouldBe(0);
            h.List.OpenRow.ShouldBeNull();

            // So does pressing anywhere else.
            first.Open(SwipeActionSide.Trailing);
            Dispatcher.UIThread.RunJobs();
            h.Window.MouseDown(new Point(5, 5), MouseButton.Left);
            h.Window.MouseUp(new Point(5, 5), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            first.Offset.ShouldBe(0);
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Swipe_Past_Half_The_Open_Width_Settles_Open_And_A_Short_One_Closes()
    {
        var h = Show();
        try
        {
            var row = h.Row(1);
            Drag(h, row, -100);
            row.OpenSide.ShouldBe(SwipeActionSide.Trailing);
            row.Offset.ShouldBe(-152);

            // A tap on the open row closes it instead of acting on what is underneath.
            h.Window.MouseDown(h.Center(row), MouseButton.Left);
            h.Window.MouseUp(h.Center(row), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            row.Offset.ShouldBe(0);

            Drag(h, row, 30);
            row.Offset.ShouldBe(0);
            row.OpenSide.ShouldBe(SwipeActionSide.None);
            h.Invoked.ShouldBeEmpty();
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Vertical_Drag_Passes_Through()
    {
        var h = Show();
        try
        {
            var row = h.Row(0);
            Drag(h, row, 4, 60);
            row.Offset.ShouldBe(0);
            row.OpenSide.ShouldBe(SwipeActionSide.None);
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Past_The_Last_Stop_The_Row_Follows_With_Rubber_Band_Resistance()
    {
        var row = CreateRow("Only");
        row.IsFullSwipeEnabled = false;
        var h = Show(rows: [row]);
        try
        {
            var start = h.Center(row) - new Point(60, 0);
            h.Window.MouseDown(start, MouseButton.Left);
            h.Window.MouseMove(start - new Point(10, 0), RawInputModifiers.LeftMouseButton);
            h.Window.MouseMove(start - new Point(310, 0), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            var width = row.Bounds.Width;
            var expected = -(152 + RubberBand.Apply(300 - 152, width));
            row.Offset.ShouldBe(expected, 0.01);
            ContentX(row).ShouldBe(expected, 0.01);
            Pump(90);
            h.Window.MouseUp(start - new Point(310, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            // Full swipe is off: the row rests open instead of committing.
            row.Offset.ShouldBe(-152);
            h.Invoked.ShouldBeEmpty();
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Swipe_Commits_The_Outermost_Action()
    {
        var h = Show();
        try
        {
            var row = h.Row(0);
            Drag(h, row, -300);
            h.Invoked.Count.ShouldBe(1);
            h.Invoked[0].Action.Label.ShouldBe("Delete");
            h.List.Items.Count.ShouldBe(2);

            var next = h.Row(0);
            Drag(h, next, 300);
            h.Invoked.Count.ShouldBe(2);
            h.Invoked[1].Action.Label.ShouldBe("Unread");
            h.List.Items.Count.ShouldBe(2);
            next.Offset.ShouldBe(0);
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Tapping_A_Revealed_Action_Runs_It()
    {
        var h = Show();
        try
        {
            var row = h.Row(2);
            row.Open(SwipeActionSide.Trailing);
            Dispatcher.UIThread.RunJobs();
            var layers = Part<SwipeActionsRowLayers>(row, SwipeActionsRow.ActionsPart);
            // Archive is the inner action, against the content: from W − 152 to W − 76.
            var archive = layers.TranslatePoint(new Point(layers.Bounds.Width - 114, layers.Bounds.Height / 2), h.Window)!.Value;
            layers.ActionAt(new Point(layers.Bounds.Width - 114, 10)).ShouldBe((SwipeActionSide.Trailing, 0));
            layers.ActionAt(new Point(layers.Bounds.Width - 30, 10)).ShouldBe((SwipeActionSide.Trailing, 1));
            h.Window.MouseDown(archive, MouseButton.Left);
            h.Window.MouseUp(archive, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            h.Invoked.Single().Action.Label.ShouldBe("Archive");
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Removing_Action_Runs_After_The_Row_Slides_Away_And_Collapses()
    {
        var h = Show(Motion.ArcMotionLevel.Full);
        try
        {
            var row = h.Row(0);
            var height = row.Bounds.Height;
            row.Run(row.TrailingActions[0]);
            // The commit is synchronous; the action waits for the slide and the collapse.
            row.IsLeaving.ShouldBeTrue();
            row.Classes.ShouldContain(SwipeActionsRow.LeavingPseudoClass);
            h.Invoked.ShouldBeEmpty();
            WaitFor(() => h.Invoked.Count == 1).ShouldBeTrue();
            h.List.Items.Count.ShouldBe(2);
            height.ShouldBeGreaterThan(0);
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void A_List_That_Keeps_The_Item_Gets_The_Row_Back()
    {
        var h = Show(removeOnInvoke: false);
        try
        {
            var row = h.Row(0);
            row.Run(row.TrailingActions[1]);
            Dispatcher.UIThread.RunJobs();
            h.Invoked.Count.ShouldBe(1);
            row.IsLeaving.ShouldBeTrue();
            row.Offset.ShouldBeLessThan(-ListWidth + 10);
            WaitFor(() => !row.IsLeaving, 4000).ShouldBeTrue();
            row.Offset.ShouldBe(0);
            row.Opacity.ShouldBe(1);
        }
        finally
        {
            h.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Generated_Rows_Use_The_List_Actions_And_Labels()
    {
        var items = new AvaloniaList<string> { "Alpha", "Beta" };
        var list = new SwipeActions { Label = "Things", Width = ListWidth, ItemsSource = items };
        list.TrailingActions.Add(new SwipeAction { Label = "Remove", Icon = ArcIcons.X, Tone = SwipeActionTone.Danger });
        object? removed = null;
        list.ActionInvoked += (_, e) =>
        {
            removed = e.Item;
            items.Remove((string)e.Item!);
        };
        var window = TestHelpers.Show(list);
        try
        {
            var row = (SwipeActionsRow)list.ContainerFromIndex(1)!;
            row.Label.ShouldBe("Beta");
            row.ActionsOf(SwipeActionSide.Trailing).Single().Label.ShouldBe("Remove");
            AutomationProperties.GetName(More(row)).ShouldBe("More actions for Beta");
            row.Run(list.TrailingActions[0]);
            Dispatcher.UIThread.RunJobs();
            removed.ShouldBe("Beta");
            items.ShouldBe(["Alpha"]);
            list.Classes.ShouldNotContain(SwipeActions.EmptyPseudoClass);
            items.Clear();
            Dispatcher.UIThread.RunJobs();
            list.Classes.ShouldContain(SwipeActions.EmptyPseudoClass);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Removed_Row_Is_Collected_While_Its_List_Lives_On()
    {
        var items = new AvaloniaList<string> { "Alpha", "Beta" };
        var list = new SwipeActions { Label = "Things", Width = ListWidth, ItemsSource = items };
        list.TrailingActions.Add(new SwipeAction { Label = "Remove", Icon = ArcIcons.X, Tone = SwipeActionTone.Danger });
        var window = TestHelpers.Show(list);
        try
        {
            var weak = RowAt(list, 1);
            items.RemoveAt(1);
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Collect(weak);
            weak.IsAlive.ShouldBeFalse();
            // The row that stayed still follows the shared actions.
            var row = (SwipeActionsRow)list.ContainerFromIndex(0)!;
            row.OpenMenu(MenuOpenFocus.None);
            Dispatcher.UIThread.RunJobs();
            row.Menu!.Items.Cast<MenuEntry>().Single().Header.ShouldBe("Remove");
            row.Menu.Close(returnFocus: false);
            Dispatcher.UIThread.RunJobs();
            list.TrailingActions[0].Label = "Delete";
            row.OpenMenu(MenuOpenFocus.None);
            Dispatcher.UIThread.RunJobs();
            row.Menu.Items.Cast<MenuEntry>().Single().Header.ShouldBe("Delete");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Row_Is_Collected_While_A_Shared_Action_Lives_On()
    {
        var archive = new SwipeAction { Label = "Archive", Icon = ArcIcons.Archive };
        var weak = TestHelpers.ShowAndClose(() => new SwipeActionsRow { Label = "Mail", TrailingActions = { archive } });
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
        archive.Label = "Store";
    }

    [AvaloniaFact]
    public void A_Row_Put_Back_Shows_An_Action_Changed_While_It_Was_Away()
    {
        var row = CreateRow("Mail");
        var host = new Border { Width = ListWidth, Child = row };
        var window = TestHelpers.Show(host);
        try
        {
            row.OpenMenu(MenuOpenFocus.None);
            Dispatcher.UIThread.RunJobs();
            row.Menu!.Items.Cast<MenuEntry>().Select(e => e.Header).ShouldBe(["Unread", "Archive", "Delete"]);
            row.Menu.Close(returnFocus: false);
            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            row.TrailingActions[0].Label = "Store";
            host.Child = row;
            Dispatcher.UIThread.RunJobs();
            row.OpenMenu(MenuOpenFocus.None);
            Dispatcher.UIThread.RunJobs();
            row.Menu.Items.Cast<MenuEntry>().Select(e => e.Header).ShouldBe(["Unread", "Store", "Delete"]);
            row.Menu.Close(returnFocus: false);
            Dispatcher.UIThread.RunJobs();
            row.LeadingActions[0].Label = "Read";
            row.OpenMenu(MenuOpenFocus.None);
            Dispatcher.UIThread.RunJobs();
            row.Menu.Items.Cast<MenuEntry>().Select(e => e.Header).ShouldBe(["Read", "Store", "Delete"]);
        }
        finally
        {
            window.Close();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference RowAt(SwipeActions list, int index) => new(list.ContainerFromIndex(index)!);
}
