using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Gallery.Demos;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Projects;

/// <summary>
/// <see cref="KanbanBoard"/> in a window: layout, pointer drag (threshold, slot, cross-column drops, events, order), the
/// drop spring, tilt, keyboard drag and announcements, cancel paths, quick add, the drawer, swimlanes, auto-scroll,
/// responsive modes, motion levels and accessible names.
/// </summary>
public class KanbanBoardTests
{
    private static (Window Window, KanbanBoard Board) Show(double width = 1440, double height = 1000,
        ArcMotionLevel level = ArcMotionLevel.Off, Action<KanbanBoard>? setup = null)
    {
        var board = new KanbanBoard();
        KanbanBoardDemoData.Fill(board);
        setup?.Invoke(board);
        var window = TestHelpers.Show(board, level);
        window.Width = width;
        window.Height = height;
        Pump();
        return (window, board);
    }

    private static void Pump(int n = 4)
    {
        for (var i = 0; i < n; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static KanbanCard Card(KanbanBoard board, string key) => board.Cards.First(c => c.Key == key);

    private static KanbanCardView View(KanbanBoard board, string key) => board.Panel!.ViewOf(Card(board, key))!;

    private static Point Center(Window window, Visual visual) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)!.Value;

    /// <summary>A board point (panel coordinates) in window coordinates.</summary>
    private static Point ToWindow(Window window, KanbanBoard board, Point boardPoint) =>
        board.Panel!.TranslatePoint(boardPoint, window)!.Value;

    private static void Press(Window window, Point point)
    {
        Pump(1);
        window.MouseMove(point, RawInputModifiers.None);
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Pump(1);
    }

    private static void Move(Window window, Point point)
    {
        window.MouseMove(point, RawInputModifiers.LeftMouseButton);
        Pump(1);
    }

    private static void Release(Window window, Point point)
    {
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Pump(2);
    }

    private static string[] Keys(IEnumerable<KanbanCard> cards) => cards.Select(c => c.Key).ToArray();

    private static string[] Column(KanbanBoard board, string column) => Keys(board.Cards.Where(c => c.ColumnKey == column));

    // Defaults and layout ----------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Defaults_And_Strings_Are_Our_Own()
    {
        var board = new KanbanBoard();
        board.GroupBy.ShouldBe(KanbanGroupBy.Status);
        board.IsQuickAddEnabled.ShouldBeTrue();
        board.IsDetailsEnabled.ShouldBeTrue();
        board.IsDragEnabled.ShouldBeTrue();
        board.IsReadOnly.ShouldBeFalse();
        board.DragThreshold.ShouldBe(5);
        board.TouchHoldDelay.ShouldBe(TimeSpan.FromMilliseconds(250));
        board.MinColumnWidth.ShouldBe(251);
        board.CompactColumnWidth.ShouldBe(235);
        board.CompactWidth.ShouldBe(560);
        board.DrawerMaxWidth.ShouldBe(400);
        var strings = board.Strings;
        strings.Points(1).ShouldBe("1 point");
        strings.Points(5).ShouldBe("5 points");
        strings.PointsShort(5).ShouldBe("5 pt");
        strings.CardsWord(1).ShouldBe("card");
        strings.CardsWord(12).ShouldBe("cards");
        strings.CardName("Fix it", "K-1", null).ShouldBe("Fix it, K-1");
        strings.CardCountOfLimit(4, 3).ShouldBe("4 of 3 allowed");
    }

    [AvaloniaFact]
    public void The_Board_Lays_Out_Status_Columns_As_Measured()
    {
        var (window, board) = Show();
        try
        {
            board.IsCompact.ShouldBeFalse();
            var layout = board.Panel!.CurrentLayout!;
            layout.Metrics.ColumnWidth.ShouldBe((board.Viewport.Width - 68) / 4, 0.01);
            board.Panel.Heads.Count.ShouldBe(4);
            board.Panel.Cells.Count.ShouldBe(4);
            board.Panel.AddRows.Count.ShouldBe(4);
            // The first card sits 9 inside its cell (border + padding), the cell 54 under the top (head row + gap).
            var first = View(board, "FLD-231");
            first.Bounds.X.ShouldBe(16 + 9, 0.01);
            first.Bounds.Y.ShouldBe(54 + 9, 0.01);
            first.Bounds.Width.ShouldBe(layout.Metrics.CardWidth, 0.01);
            var second = View(board, "FLD-236");
            second.Bounds.Y.ShouldBe(first.Bounds.Bottom + 8, 0.01);
            // Column heads: counts, limits and the WIP state.
            board.Panel.Heads[1].Count.ShouldBe(3);
            board.Panel.Heads[1].IsOverLimit.ShouldBeFalse();
            board.Columns[1].Count.ShouldBe(3);
            board.Columns[1].IsOverLimit.ShouldBeFalse();
            AutomationProperties.GetName(board.Panel.Heads[1].AddButton!).ShouldBe("New card in Building");
            AutomationProperties.GetName(first).ShouldBe("Resume photo uploads after the app closes mid-transfer, FLD-231, Rafael Ortiz");
            AutomationProperties.GetItemType(first).ShouldBe("Movable card");
            first.Focusable.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Head_Row_Sticks_To_The_Top_While_The_Board_Scrolls()
    {
        var (window, board) = Show(1100, 520, setup: b => b.Cards.AddRange(KanbanBoardDemoData.CreateBacklog()));
        try
        {
            var head = board.Panel!.Heads[0];
            head.Bounds.Y.ShouldBe(2, 0.01);
            board.Scroller!.Offset = new Vector(0, 120);
            Pump();
            head.Bounds.Y.ShouldBe(122, 0.01);
            head.ZIndex.ShouldBeGreaterThan(board.Panel.ViewOf(board.Cards[0])!.ZIndex);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Swimlanes_Give_Each_Person_A_Lane_With_Sticky_Heads()
    {
        var (window, board) = Show(700, 900, setup: b => b.GroupBy = KanbanGroupBy.Assignee);
        try
        {
            var panel = board.Panel!;
            panel.LaneHeads.Count.ShouldBe(4);
            panel.Cells.Count.ShouldBe(16);
            panel.AddRows.Count.ShouldBe(0);
            panel.LaneHeads[0].PersonName.ShouldBe("Amara Okafor");
            panel.LaneHeads[0].Count.ShouldBe(4);
            panel.LaneHeads[2].Count.ShouldBe(2);
            AutomationProperties.GetName(panel.LaneHeads[0]).ShouldBe("Cards for Amara Okafor, 4 cards");
            // Mei has nothing in Building: an empty 96px cell.
            panel.CellRect(2, 1)!.Value.Height.ShouldBeGreaterThanOrEqualTo(96);
            board.Scroller!.Offset = default;
            Pump();
            panel.LaneHeads[0].Bounds.X.ShouldBe(16, 0.01);
            board.Scroller.Offset = new Vector(200, 0);
            Pump();
            panel.LaneHeads[0].Bounds.X.ShouldBe(216, 0.01);
            panel.Heads[0].Bounds.X.ShouldBe(16, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tab_Visits_The_Plus_Buttons_Then_Every_Column_In_Order()
    {
        var (window, board) = Show();
        try
        {
            board.Panel!.Heads[0].AddButton!.Focus(NavigationMethod.Tab);
            var order = new List<object?>();
            for (var i = 0; i < 9; i++)
            {
                window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
                Pump(1);
                order.Add(window.FocusManager!.GetFocusedElement());
            }
            order[0].ShouldBe(board.Panel.Heads[1].AddButton);
            order[2].ShouldBe(board.Panel.Heads[3].AddButton);
            order[3].ShouldBe(View(board, "FLD-231"));
            order[4].ShouldBe(View(board, "FLD-236"));
            order[6].ShouldBe(View(board, "FLD-244"));
            order[7].ShouldBe(board.Panel.AddRows[0]);
            order[8].ShouldBe(View(board, "FLD-218"));
        }
        finally
        {
            window.Close();
        }
    }

    // Pointer drag -----------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void A_Press_Lifts_Only_Past_5px_And_A_Click_Opens_The_Card()
    {
        var (window, board) = Show();
        try
        {
            var invoked = new List<KanbanCard>();
            board.CardInvoked += (_, e) => invoked.Add(e.Card);
            var start = Center(window, View(board, "FLD-236"));
            Press(window, start);
            Move(window, start + new Vector(3, 4));
            board.IsDragging.ShouldBeFalse();
            Release(window, start + new Vector(3, 4));
            invoked.ShouldBe([Card(board, "FLD-236")]);
            board.OpenedCard.ShouldBe(Card(board, "FLD-236"));
            board.CloseDetails();
            Pump();

            Press(window, start);
            Move(window, start + new Vector(5.2, 0));
            board.IsDragging.ShouldBeTrue();
            board.DraggedCard.ShouldBe(Card(board, "FLD-236"));
            board.Classes.Contains(":dragging").ShouldBeTrue();
            board.DragAnnouncer!.Announcement.ShouldBe("Cache tiles for the next inspection route picked up.");
            Release(window, start + new Vector(5.2, 0));
            invoked.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Grabbing_A_Card_Below_Its_Middle_Keeps_The_Slot_In_Place()
    {
        var (window, board) = Show();
        try
        {
            var view = View(board, "FLD-236");
            var below = view.TranslatePoint(new Point(view.Bounds.Width / 2, view.Bounds.Height - 6), window)!.Value;
            Press(window, below);
            Move(window, below + new Vector(6, 0));
            board.Session!.TargetIndex.ShouldBe(1);
            board.Session.TargetColumn.ShouldBe(0);
            // The card keeps the grab point: it moved with the pointer, nothing jumped.
            var visual = board.Panel!.VisualRect(view);
            visual.X.ShouldBe(view.Bounds.X + 6, 0.5);
            visual.Y.ShouldBe(view.Bounds.Y, 0.5);
            Release(window, below + new Vector(6, 0));
            Column(board, "queued").ShouldBe(["FLD-231", "FLD-236", "FLD-240", "FLD-244"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_Into_Another_Column_Opens_A_Slot_Counts_Live_And_Drops_In_Order()
    {
        var (window, board) = Show();
        try
        {
            var targets = new List<KanbanCardDragEventArgs>();
            var moves = new List<KanbanCardMovedEventArgs>();
            var changes = new List<KanbanCardsChangedEventArgs>();
            var limits = new List<KanbanColumnLimitEventArgs>();
            board.CardDragTargetChanged += (_, e) => targets.Add(e);
            board.CardMoved += (_, e) => moves.Add(e);
            board.CardsChanged += (_, e) => changes.Add(e);
            board.ColumnLimitStateChanged += (_, e) => limits.Add(e);
            var card = Card(board, "FLD-236");
            var start = Center(window, View(board, "FLD-236"));
            Press(window, start);
            Move(window, start + new Vector(8, 0));
            // Into Building, between FLD-218 and FLD-222.
            var at = Center(window, View(board, "FLD-222")) - new Vector(0, 30);
            Move(window, at);
            Move(window, at + new Vector(1, 0));
            var session = board.Session!;
            session.TargetColumn.ShouldBe(1);
            session.TargetIndex.ShouldBe(1);
            targets.Last().Target.ShouldBe(new KanbanSlot("building", "mei", 1));
            targets.Last().IsTargetOverLimit.ShouldBeTrue();
            // Live WIP: the card counts in Building (4 of 3), not in Queued; the slot says so.
            board.Panel!.Heads[0].Count.ShouldBe(3);
            board.Panel.Heads[1].Count.ShouldBe(4);
            board.Panel.Heads[1].IsOverLimit.ShouldBeTrue();
            board.Panel.CellView(0, 1)!.IsDropTarget.ShouldBeTrue();
            board.Panel.CellView(0, 1)!.IsOverLimit.ShouldBeTrue();
            board.Panel.CellView(0, 0)!.IsDropTarget.ShouldBeFalse();
            board.Panel.Slot.IsOverLimit.ShouldBeTrue();
            board.Panel.Slot.Text.ShouldBe("Limit of 3 exceeded");
            // The committed count has not changed yet.
            board.Columns[1].Count.ShouldBe(3);
            var slot = board.Panel.SlotRect;
            slot.Y.ShouldBe(View(board, "FLD-218").Bounds.Bottom + 8, 0.5);
            View(board, "FLD-222").Bounds.Y.ShouldBe(slot.Bottom + 8, 0.5);

            Release(window, at + new Vector(1, 0));
            Pump(6);
            board.IsDragging.ShouldBeFalse();
            card.ColumnKey.ShouldBe("building");
            Column(board, "building").ShouldBe(["FLD-218", "FLD-236", "FLD-222", "FLD-226"]);
            moves.Count.ShouldBe(1);
            moves[0].FromColumn!.Identity.ShouldBe("queued");
            moves[0].ToColumn!.Identity.ShouldBe("building");
            moves[0].ChangeSource.ShouldBe(KanbanMoveSource.Pointer);
            moves[0].FromIndex.ShouldBe(1);
            moves[0].To.ShouldBe(new KanbanSlot("building", "mei", 1));
            changes.Single().Reason.ShouldBe(KanbanChangeReason.Moved);
            Keys(changes[0].Cards).ShouldBe(Keys(board.Cards));
            Keys(changes[0].Cards).ShouldBe(["FLD-231", "FLD-240", "FLD-244", "FLD-218", "FLD-236", "FLD-222", "FLD-226", "FLD-209",
                "FLD-213", "FLD-201", "FLD-204", "FLD-207"]);
            board.Columns[1].IsOverLimit.ShouldBeTrue();
            limits.Single().Column.Identity.ShouldBe("building");
            limits[0].IsOverLimit.ShouldBeTrue();
            board.DragAnnouncer!.Announcement.ShouldBe("Cache tiles for the next inspection route placed in Building.");
            board.Panel.Slot.IsVisible.ShouldBeFalse();
            View(board, "FLD-236").ZIndex.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dropping_In_Another_Lane_Changes_The_Owner()
    {
        var (window, board) = Show(1440, 1000, setup: b => b.GroupBy = KanbanGroupBy.Assignee);
        try
        {
            KanbanCardMovedEventArgs? moved = null;
            board.CardMoved += (_, e) => moved = e;
            // Mei's FLD-236 (Queued) into Jonas's empty Testing cell.
            var start = Center(window, View(board, "FLD-236"));
            Press(window, start);
            Move(window, start + new Vector(0, 8));
            var cell = board.Panel!.CellRect(1, 2)!.Value;
            var target = ToWindow(window, board, cell.Center);
            Move(window, target);
            Move(window, target + new Vector(0, 1));
            board.Session!.TargetLane.ShouldBe(1);
            board.Session.TargetColumn.ShouldBe(2);
            board.Session.TargetIndex.ShouldBe(0);
            board.Panel.LaneHeads[1].Count.ShouldBe(4);
            board.Panel.LaneHeads[2].Count.ShouldBe(1);
            Release(window, target + new Vector(0, 1));
            Pump(6);
            var card = Card(board, "FLD-236");
            card.AssigneeKey.ShouldBe("jonas");
            card.ColumnKey.ShouldBe("testing");
            moved!.FromAssignee!.Name.ShouldBe("Mei Takahashi");
            moved.ToAssignee!.Name.ShouldBe("Jonas Lindqvist");
            board.DragAnnouncer!.Announcement.ShouldBe("Cache tiles for the next inspection route placed in Testing, now with Jonas Lindqvist.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Sends_A_Dragged_Card_Back_And_A_Held_Button_Does_Not_Lift_It_Again()
    {
        var (window, board) = Show();
        try
        {
            var cancelled = new List<KanbanCardDragCancelledEventArgs>();
            var moved = 0;
            board.CardDragCancelled += (_, e) => cancelled.Add(e);
            board.CardMoved += (_, _) => moved++;
            var start = Center(window, View(board, "FLD-236"));
            Press(window, start);
            Move(window, start + new Vector(8, 0));
            Move(window, start + new Vector(360, 0));
            board.Session!.TargetColumn.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump(6);
            cancelled.Single().Reason.ShouldBe(KanbanDragCancelReason.Escape);
            board.IsDragging.ShouldBeFalse();
            board.DragAnnouncer!.Announcement.ShouldBe("Cache tiles for the next inspection route is back where it was.");
            // Arc re-lifted the card here with a stale grab offset; we ignore moves until the button is released.
            Move(window, start + new Vector(400, 40));
            Move(window, start + new Vector(420, 60));
            board.IsDragging.ShouldBeFalse();
            board.Session.ShouldBeNull();
            Release(window, start + new Vector(420, 60));
            moved.ShouldBe(0);
            Column(board, "queued").ShouldBe(["FLD-231", "FLD-236", "FLD-240", "FLD-244"]);
            board.OpenedCard.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Lifted_Card_Is_Drawn_Above_The_Board_By_A_Copy_That_Follows_It()
    {
        var (window, board) = Show();
        try
        {
            var view = View(board, "FLD-236");
            var motion = board.Panel!.Flip.Get(view);
            var start = Center(window, view);
            Press(window, start);
            Move(window, start + new Vector(8, 0));
            var proxy = board.DragProxy;
            proxy.ShouldNotBeNull();
            board.DragLayer!.Children.ShouldContain(proxy);
            proxy.IsProxy.ShouldBeTrue();
            proxy.Focusable.ShouldBeFalse();
            proxy.IsHitTestVisible.ShouldBeFalse();
            proxy.Card.ShouldBe(view.Card);
            proxy.IsDragged.ShouldBeTrue();
            // The real card stays in the panel, transparent; the copy is drawn exactly where it would be.
            board.Panel.Children.ShouldContain(view);
            view.Opacity.ShouldBe(0);
            Pump(1);
            proxy.Bounds.Width.ShouldBe(view.Bounds.Width, 0.01);
            proxy.Bounds.Height.ShouldBe(view.Bounds.Height, 0.01);
            void Same(Point corner)
            {
                var real = view.TranslatePoint(corner, board)!.Value;
                var copy = board.DragProxy!.TranslatePoint(corner, board)!.Value;
                copy.X.ShouldBe(real.X, 0.5);
                copy.Y.ShouldBe(real.Y, 0.5);
            }
            Same(default);
            Same(new Point(view.Bounds.Width, view.Bounds.Height));
            motion.Scale.Value.ShouldBe(KanbanDragPhysics.LiftScale, 0.001);

            // Over the column heads and the toolbar: the copy rises above the scroll viewport, unclipped.
            var scrollerTop = board.Scroller!.TranslatePoint(default, board)!.Value.Y;
            var high = new Point(start.X + 20, 20);
            Move(window, high);
            Pump(1);
            proxy.TranslatePoint(default, board)!.Value.Y.ShouldBeLessThan(scrollerTop);
            Same(default);
            board.Session!.TargetColumn.ShouldBe(0);

            Release(window, high);
            Pump(4);
            board.DragProxy.ShouldBeNull();
            board.DragLayer.Children.ShouldBeEmpty();
            view.Opacity.ShouldBe(1);
            view.Mirror.ShouldBeNull();

            // Escape: the copy flies home and goes; the card is visible again.
            Press(window, start);
            Move(window, start + new Vector(8, 0));
            board.DragProxy.ShouldNotBeNull();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump(4);
            Release(window, start + new Vector(8, 0));
            board.DragProxy.ShouldBeNull();
            view.Opacity.ShouldBe(1);

            // The keyboard lift is drawn the same way, with its ring.
            view.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Pump();
            board.DragProxy.ShouldNotBeNull();
            board.DragProxy!.IsLifted.ShouldBeTrue();
            board.DragProxy.IsFocusRingVisible.ShouldBeTrue();
            view.Opacity.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Pump();
            Same(default);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Pump(4);
            board.DragProxy.ShouldBeNull();
            view.Opacity.ShouldBe(1);
            view.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Copy_Follows_The_Drop_Spring_And_Hands_Over_Without_A_Gap()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var now = TimeSpan.Zero;
        var (window, board) = Show(level: ArcMotionLevel.Full, setup: b => b.DragClock = () => now);
        try
        {
            void Step()
            {
                now += TimeSpan.FromMilliseconds(16);
                clock.Advance(TimeSpan.FromMilliseconds(16));
                Dispatcher.UIThread.RunJobs();
            }

            var view = View(board, "FLD-240");
            var start = Center(window, view);
            Press(window, start);
            var p = start;
            for (var i = 0; i < 8; i++)
            {
                p += new Vector(14, -6);
                Move(window, p);
                Step();
            }
            Release(window, p);
            var landed = false;
            for (var i = 0; i < 120 && !landed; i++)
            {
                Step();
                Pump(1);
                if (board.DragProxy is { } proxy)
                {
                    // Every frame of the flight: the copy is the visible one and sits on the card's transform.
                    view.Opacity.ShouldBe(0);
                    var real = view.TranslatePoint(default, board)!.Value;
                    var copy = proxy.TranslatePoint(default, board)!.Value;
                    copy.X.ShouldBe(real.X, 0.5);
                    copy.Y.ShouldBe(real.Y, 0.5);
                }
                else
                {
                    landed = true;
                    view.Opacity.ShouldBe(1);
                }
            }
            landed.ShouldBeTrue();
            board.Session.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Refused_Drop_And_A_Refused_Lift_Leave_The_Card_Alone()
    {
        var (window, board) = Show();
        try
        {
            board.CardMoving += (_, e) => e.Cancel = e.IsTargetOverLimit;
            var start = Center(window, View(board, "FLD-236"));
            Press(window, start);
            Move(window, start + new Vector(8, 0));
            var at = Center(window, View(board, "FLD-222"));
            Move(window, at);
            Release(window, at);
            Pump(6);
            Card(board, "FLD-236").ColumnKey.ShouldBe("queued");

            board.CardDragStarting += (_, e) => e.Cancel = e.Card.Key == "FLD-240";
            var other = Center(window, View(board, "FLD-240"));
            Press(window, other);
            Move(window, other + new Vector(20, 0));
            board.IsDragging.ShouldBeFalse();
            Release(window, other + new Vector(20, 0));
            board.OpenedCard.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    // Motion -----------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void The_Drop_Spring_Chases_A_Slot_That_Is_Still_Moving()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var now = TimeSpan.Zero;
        var (window, board) = Show(level: ArcMotionLevel.Full, setup: b => b.DragClock = () => now);
        try
        {
            void Step(int ms = 16)
            {
                now += TimeSpan.FromMilliseconds(ms);
                clock.Advance(TimeSpan.FromMilliseconds(ms));
                Dispatcher.UIThread.RunJobs();
            }

            var view = View(board, "FLD-236");
            var start = Center(window, view);
            Press(window, start);
            Move(window, start + new Vector(0, 8));
            Step();
            // Down past FLD-240's middle: the slot glides down one place on the layout spring.
            var below = Center(window, View(board, "FLD-240")) + new Vector(0, 10);
            Move(window, below);
            Step();
            board.Session!.TargetIndex.ShouldBe(2);
            var slotMotion = board.Panel!.Flip.Get(board.Panel.Slot);
            slotMotion.Offset.IsAnimating.ShouldBeTrue();
            // Release while the slot is still on its way: the card flies to where the slot is drawn, frame by frame.
            Release(window, below);
            Step();
            board.Session!.Mode.ShouldBe(KanbanDragMode.Landing);
            var card = board.Panel.Flip.Get(view);
            for (var i = 0; i < 6; i++)
            {
                Step();
                if (slotMotion.Offset.IsAnimating)
                {
                    card.Offset.Target.ShouldBe(slotMotion.Offset.Value);
                }
            }
            for (var i = 0; i < 90 && board.Session is not null; i++)
            {
                Step();
            }
            board.Session.ShouldBeNull();
            card.Offset.Value.ShouldBe(default(Vector));
            card.Scale.Value.ShouldBe(1);
            board.Panel.Slot.IsVisible.ShouldBeFalse();
            Column(board, "queued").ShouldBe(["FLD-231", "FLD-240", "FLD-236", "FLD-244"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Tilt_Follows_Horizontal_Speed_And_Levels_Out_When_The_Pointer_Rests()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var now = TimeSpan.Zero;
        var (window, board) = Show(level: ArcMotionLevel.Full, setup: b => b.DragClock = () => now);
        try
        {
            void Step(int ms = 16)
            {
                now += TimeSpan.FromMilliseconds(ms);
                clock.Advance(TimeSpan.FromMilliseconds(ms));
                Dispatcher.UIThread.RunJobs();
            }

            var view = View(board, "FLD-240");
            var start = Center(window, view);
            Press(window, start);
            var p = start;
            // About 600 px/s to the right: ~3.8° clockwise.
            for (var i = 0; i < 12; i++)
            {
                p += new Vector(10, 0);
                Move(window, p);
                Step();
            }
            var motion = board.Panel!.Flip.Get(view);
            motion.Scale.Value.ShouldBe(KanbanDragPhysics.LiftScale, 0.01);
            motion.Rotation.Value.ShouldBeInRange(2.5, 5.5);
            // The pointer rests: the speed goes stale and the card levels out (Arc kept the last angle).
            for (var i = 0; i < 40; i++)
            {
                Step();
            }
            motion.Rotation.Value.ShouldBe(0, 0.05);
            Release(window, p);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Drops_Tilt_And_Lands_At_Once()
    {
        var (window, board) = Show(level: ArcMotionLevel.Reduced);
        try
        {
            var view = View(board, "FLD-240");
            var start = Center(window, view);
            Press(window, start);
            for (var i = 1; i <= 6; i++)
            {
                Move(window, start + new Vector(i * 40, 0));
            }
            var motion = board.Panel!.Flip.Get(view);
            motion.Rotation.Value.ShouldBe(0);
            motion.Scale.Value.ShouldBe(KanbanDragPhysics.LiftScale, 0.001);
            Release(window, start + new Vector(240, 0));
            Pump(4);
            board.Session.ShouldBeNull();
            motion.Offset.Value.ShouldBe(default(Vector));
            motion.Scale.Value.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switching_To_Swimlanes_Flies_Every_Card_On_The_Layout_Spring()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (window, board) = Show(level: ArcMotionLevel.Full);
        try
        {
            GroupByChangedProbe(board, out var changes);
            var view = View(board, "FLD-204");
            var before = board.Panel!.VisualRect(view);
            board.ViewSwitch!.Options[1].IsChecked = true;
            Pump(1);
            board.GroupBy.ShouldBe(KanbanGroupBy.Assignee);
            changes.Single().NewValue.ShouldBe(KanbanGroupBy.Assignee);
            var motion = board.Panel.Flip.Get(view);
            motion.Offset.IsAnimating.ShouldBeTrue();
            board.Panel.VisualRect(view).Y.ShouldBe(before.Y, 1);
            clock.Advance(TimeSpan.FromMilliseconds(150));
            var midway = board.Panel.VisualRect(view).Y;
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Pump(1);
            motion.Offset.IsAnimating.ShouldBeFalse();
            var after = board.Panel.VisualRect(view).Y;
            Math.Abs(after - before.Y).ShouldBeGreaterThan(1);
            midway.ShouldBeInRange(Math.Min(before.Y, after), Math.Max(before.Y, after));
            // The lane heads and cells appear at once (Arc: no fade).
            board.Panel.LaneHeads.Count.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }

        static void GroupByChangedProbe(KanbanBoard board, out List<KanbanGroupByChangedEventArgs> list)
        {
            var changes = new List<KanbanGroupByChangedEventArgs>();
            board.GroupByChanged += (_, e) => changes.Add(e);
            list = changes;
        }
    }

    [AvaloniaFact]
    public void The_Board_Scrolls_At_Its_Edges_While_A_Card_Rests_There()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (window, board) = Show(900, 700);
        try
        {
            var scroller = board.Scroller!;
            scroller.Offset = default;
            Pump();
            var start = Center(window, View(board, "FLD-236"));
            Press(window, start);
            Move(window, start + new Vector(8, 0));
            // 10px from the right edge of the viewport: 15 px per 60Hz frame (900 px/s).
            var edge = scroller.TranslatePoint(new Point(scroller.Viewport.Width - 10, 300), window)!.Value;
            Move(window, edge);
            for (var i = 0; i < 10; i++)
            {
                clock.Advance(TimeSpan.FromMilliseconds(1000.0 / 60));
                Dispatcher.UIThread.RunJobs();
            }
            scroller.Offset.X.ShouldBe(150, 20);
            // The content moved under the resting pointer: the target followed it into a later column.
            board.Session!.TargetColumn.ShouldBeGreaterThanOrEqualTo(2);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Release(window, edge);
        }
        finally
        {
            window.Close();
        }
    }

    // Keyboard ---------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Space_Lifts_The_Arrows_Move_And_Space_Drops_With_Announcements()
    {
        var (window, board) = Show();
        try
        {
            var moved = new List<KanbanCardMovedEventArgs>();
            board.CardMoved += (_, e) => moved.Add(e);
            var view = View(board, "FLD-236");
            view.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Pump();
            board.Session!.Mode.ShouldBe(KanbanDragMode.Keyboard);
            view.IsLifted.ShouldBeTrue();
            view.Classes.Contains(":lifted").ShouldBeTrue();
            board.DragAnnouncer!.Announcement.ShouldBe(
                "Cache tiles for the next inspection route lifted. Queued, place 2. Arrow keys move it, Space places it, Escape puts it back.");

            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Pump();
            board.DragAnnouncer.Announcement.ShouldBe("Queued, place 1");
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Pump();
            // At the top: nothing moves and nothing new is said.
            board.DragAnnouncer.Announcement.ShouldBe("Queued, place 1");
            board.DragAnnouncer.History.Count(h => h == "Queued, place 1").ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Pump();
            board.DragAnnouncer.Announcement.ShouldBe("Building, place 1");
            board.Panel!.Heads[1].Count.ShouldBe(4);
            board.Panel.Heads[1].IsOverLimit.ShouldBeTrue();
            // Keyboard lifts show the warm column but no drop-target border (Arc).
            board.Panel.CellView(0, 1)!.IsDropTarget.ShouldBeFalse();
            board.Panel.CellView(0, 1)!.IsOverLimit.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Pump();
            board.DragAnnouncer.Announcement.ShouldBe("Building, place 2");
            // Nothing is committed until the drop.
            Card(board, "FLD-236").ColumnKey.ShouldBe("queued");
            moved.ShouldBeEmpty();
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Pump();
            board.Session.ShouldBeNull();
            Column(board, "building").ShouldBe(["FLD-218", "FLD-236", "FLD-222", "FLD-226"]);
            moved.Single().ChangeSource.ShouldBe(KanbanMoveSource.Keyboard);
            board.DragAnnouncer.Announcement.ShouldBe("Cache tiles for the next inspection route placed in Building.");
            view.IsFocused.ShouldBeTrue();
            view.IsLifted.ShouldBeFalse();
            AutomationProperties.GetLiveSetting(board.DragAnnouncer).ShouldBe(AutomationLiveSetting.Assertive);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_And_Losing_Focus_Put_A_Keyboard_Lifted_Card_Back()
    {
        var (window, board) = Show();
        try
        {
            var cancelled = new List<KanbanDragCancelReason>();
            board.CardDragCancelled += (_, e) => cancelled.Add(e.Reason);
            var view = View(board, "FLD-236");
            view.Focus(NavigationMethod.Tab);
            board.BeginKeyboardDrag(view.Card);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Pump();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump();
            board.Session.ShouldBeNull();
            cancelled.ShouldBe([KanbanDragCancelReason.Escape]);
            board.DragAnnouncer!.Announcement.ShouldBe("Cache tiles for the next inspection route is back where it was.");
            Column(board, "queued").ShouldBe(["FLD-231", "FLD-236", "FLD-240", "FLD-244"]);

            // Arc kept a card lifted after the focus left it; we cancel.
            board.BeginKeyboardDrag(view.Card);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Pump();
            View(board, "FLD-231").Focus(NavigationMethod.Tab);
            Pump();
            board.Session.ShouldBeNull();
            cancelled.Last().ShouldBe(KanbanDragCancelReason.FocusLost);
            Column(board, "queued").ShouldBe(["FLD-231", "FLD-236", "FLD-240", "FLD-244"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shift_Arrows_Move_A_Lifted_Card_Between_Swimlanes()
    {
        var (window, board) = Show(setup: b => b.GroupBy = KanbanGroupBy.Assignee);
        try
        {
            var view = View(board, "FLD-244");
            view.Focus(NavigationMethod.Tab);
            board.BeginKeyboardDrag(view.Card);
            board.DragAnnouncer!.Announcement!.ShouldContain("Queued, Amara Okafor, place 1");
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.Shift);
            Pump();
            board.DragAnnouncer.Announcement.ShouldBe("Queued, Jonas Lindqvist, place 1");
            board.Panel!.LaneHeads[1].Count.ShouldBe(4);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            Card(board, "FLD-244").AssigneeKey.ShouldBe("jonas");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Opens_The_Drawer_With_Focus_Inside_And_Escape_Returns_It()
    {
        var (window, board) = Show();
        try
        {
            var opened = 0;
            var closed = 0;
            board.DetailsOpened += (_, _) => opened++;
            board.DetailsClosed += (_, _) => closed++;
            var view = View(board, "FLD-218");
            view.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            board.OpenedCard.ShouldBe(view.Card);
            opened.ShouldBe(1);
            var drawer = board.Drawer!;
            drawer.IsVisible.ShouldBeTrue();
            drawer.CloseButton!.IsFocused.ShouldBeTrue(customMessage: $"focused: {window.FocusManager!.GetFocusedElement()}");
            board.Classes.Contains(":details-open").ShouldBeTrue();
            drawer.Bounds.Width.ShouldBe(400, 0.5);
            AutomationProperties.GetName(drawer).ShouldBe("Signature pad on the handover form");
            // Tab stays inside the drawer.
            for (var i = 0; i < 20; i++)
            {
                window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
                Pump(1);
                var focused = window.FocusManager!.GetFocusedElement() as Visual;
                focused.ShouldNotBeNull();
                drawer.IsVisualAncestorOf(focused!).ShouldBeTrue();
            }
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump();
            board.OpenedCard.ShouldBeNull();
            closed.ShouldBe(1);
            view.IsFocused.ShouldBeTrue();
            drawer.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Drawer_Edits_The_Card_Live_And_Raises_Events()
    {
        var (window, board) = Show();
        try
        {
            var card = Card(board, "FLD-222");
            var edits = new List<KanbanCardEditedEventArgs>();
            var changes = new List<KanbanChangeReason>();
            var toggles = new List<KanbanChecklistItemToggledEventArgs>();
            var moves = new List<KanbanCardMovedEventArgs>();
            board.CardEdited += (_, e) => edits.Add(e);
            board.CardsChanged += (_, e) => changes.Add(e.Reason);
            board.ChecklistItemToggled += (_, e) => toggles.Add(e);
            board.CardMoved += (_, e) => moves.Add(e);
            board.OpenCard(card);
            Pump();
            var drawer = board.Drawer!;
            drawer.TitleBox!.Text.ShouldBe(card.Title);
            drawer.TitleBox.Text = "Retry queue\nfor report posts";
            Pump();
            card.Title.ShouldBe("Retry queue for report posts");
            drawer.TitleBox.Text.ShouldBe("Retry queue for report posts");
            edits.Last().Property.ShouldBe(KanbanCard.TitleProperty);
            AutomationProperties.GetName(View(board, "FLD-222")).ShouldStartWith("Retry queue for report posts, FLD-222");
            drawer.NotesBox!.Text = "Try three times.";
            Pump();
            card.Description.ShouldBe("Try three times.");
            changes.ShouldContain(KanbanChangeReason.DescriptionEdited);

            // Status keeps the card's place in the board order: from Building to Shipped it lands on top.
            drawer.StatusSwitch!.Options[3].IsChecked = true;
            Pump();
            card.ColumnKey.ShouldBe("shipped");
            Column(board, "shipped").ShouldBe(["FLD-222", "FLD-201", "FLD-204", "FLD-207"]);
            moves.Last().ChangeSource.ShouldBe(KanbanMoveSource.Details);
            moves.Last().ToColumn!.Identity.ShouldBe("shipped");
            changes.ShouldContain(KanbanChangeReason.StatusChanged);
            board.Announcer!.Announcement.ShouldBe("Retry queue for report posts moved to Shipped.");

            // The checklist: tick, add.
            drawer.StepBoxes.Count.ShouldBe(2);
            drawer.StepBoxes[1].IsChecked = true;
            Pump();
            card.Checklist[1].IsDone.ShouldBeTrue();
            toggles.Single().IsDone.ShouldBeTrue();
            card.IsChecklistComplete.ShouldBeTrue();
            ((IPseudoClasses)View(board, "FLD-222").Classes).Contains(":checklist-complete").ShouldBeTrue();
            drawer.StepField!.Focus();
            drawer.StepField.Text = "Alert on the third failure";
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            card.Checklist.Count.ShouldBe(3);
            card.Checklist[2].Text.ShouldBe("Alert on the third failure");
            drawer.StepField.Text.ShouldBe(string.Empty);
            drawer.StepBoxes.Count.ShouldBe(3);
            changes.ShouldContain(KanbanChangeReason.ChecklistItemAdded);
            View(board, "FLD-222").ChecklistText.ShouldBe("2/3");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_In_The_Owner_Menu_Closes_Only_The_Menu()
    {
        var (window, board) = Show();
        try
        {
            var card = Card(board, "FLD-218");
            board.OpenCard(card);
            Pump();
            var drawer = board.Drawer!;
            drawer.OwnerTrigger!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            drawer.OwnerMenu.IsOpen.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump();
            drawer.OwnerMenu.IsOpen.ShouldBeFalse();
            board.OpenedCard.ShouldBe(card);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump();
            board.OpenedCard.ShouldBeNull();

            // Picking an owner in swimlanes moves the card to that person's lane (keys: Amara, Jonas, Mei).
            board.GroupBy = KanbanGroupBy.Assignee;
            board.OpenCard(card);
            Pump();
            drawer.OwnerTrigger!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            drawer.OwnerMenu.IsOpen.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            card.AssigneeKey.ShouldBe("mei");
            board.Grid!.TryLocate(card, out var lane, out _, out _).ShouldBeTrue();
            board.Grid.Lanes[lane].Person!.Key.ShouldBe("mei");
        }
        finally
        {
            window.Close();
        }
    }

    // Quick add --------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Quick_Add_Creates_Numbered_Cards_And_Gives_The_Focus_Back()
    {
        var (window, board) = Show();
        try
        {
            var created = new List<KanbanCardCreatedEventArgs>();
            var changes = new List<KanbanCardsChangedEventArgs>();
            board.CardCreated += (_, e) => created.Add(e);
            board.CardsChanged += (_, e) => changes.Add(e);
            var plus = board.Panel!.Heads[2].AddButton!;
            plus.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            var composer = board.Panel.Composer;
            composer.IsVisible.ShouldBeTrue();
            composer.Box!.IsFocused.ShouldBeTrue();
            AutomationProperties.GetName(composer.Box).ShouldBe("Title of the new Testing card");
            board.Panel.AddRows[2].IsVisible.ShouldBeFalse();
            composer.CanSubmit.ShouldBeFalse();

            composer.Box.Text = "Check the offline banner copy";
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            var card = created.Single().Card;
            card.Key.ShouldBe("FLD-245");
            card.Title.ShouldBe("Check the offline banner copy");
            card.ColumnKey.ShouldBe("testing");
            card.AssigneeKey.ShouldBe("amara");
            board.Cards.Last().ShouldBe(card);
            Column(board, "testing").ShouldBe(["FLD-209", "FLD-213", "FLD-245"]);
            changes.Single().Reason.ShouldBe(KanbanChangeReason.Created);
            board.Columns[2].IsOverLimit.ShouldBeTrue();
            board.Announcer!.History.ShouldContain("Check the offline banner copy added to Testing.");
            // The composer stays open, empty and focused for the next card.
            composer.IsVisible.ShouldBeTrue();
            composer.Box.Text.ShouldBe(string.Empty);
            composer.Box.IsFocused.ShouldBeTrue();
            composer.Box.Text = "Second one";
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            created.Last().Card.Key.ShouldBe("FLD-246");

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump();
            composer.IsVisible.ShouldBeFalse();
            plus.IsFocused.ShouldBeTrue();
            board.Panel.AddRows[2].IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Quick_Add_Uses_The_Key_Factory_And_Honours_Cancellation()
    {
        var (window, board) = Show(setup: b => b.CardKeyFactory = column => "NEW-" + column.Identity);
        try
        {
            board.CardCreating += (_, e) =>
            {
                if (e.Title == "skip")
                {
                    e.Cancel = true;
                }
            };
            board.BeginQuickAdd(board.Columns[0]);
            Pump();
            var composer = board.Panel!.Composer;
            composer.Box!.Text = "skip";
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            board.Cards.Count.ShouldBe(12);
            composer.Box.Text.ShouldBe("skip");
            composer.Box.Text = "  Real card  ";
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            board.Cards.Last().Key.ShouldBe("NEW-queued");
            board.Cards.Last().Title.ShouldBe("Real card");
            // Enter on an empty box closes it without a card; the focus goes to the Add card row.
            composer.Box.Text = "   ";
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            composer.IsVisible.ShouldBeFalse();
            board.Cards.Count.ShouldBe(13);
            board.Panel.AddRows[0].IsFocused.ShouldBeTrue(customMessage: $"focused: {window.FocusManager!.GetFocusedElement()}");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Next_Key_Continues_The_Highest_Number()
    {
        KanbanBoard.NextKey(["FLD-231", "FLD-244", "FLD-209"]).ShouldBe("FLD-245");
        KanbanBoard.NextKey(["T-009", "T-010"]).ShouldBe("T-011");
        KanbanBoard.NextKey(["A-1", "A-2", "B-3"]).ShouldBe("B-4");
        KanbanBoard.NextKey([]).ShouldBe("1");
        KanbanBoard.NextKey(["no number"]).ShouldBe("1");
    }

    [AvaloniaFact]
    public void In_Swimlanes_Quick_Add_Opens_In_The_Current_Users_Lane()
    {
        var (window, board) = Show(setup: b =>
        {
            b.GroupBy = KanbanGroupBy.Assignee;
            b.CurrentUser = b.People[2];
        });
        try
        {
            board.BeginQuickAdd(board.Columns[1]);
            Pump();
            board.Panel!.Composer.Cell.ShouldBe((2, 1));
            board.Panel.Composer.Box!.Text = "Mei's card";
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            board.Cards.Last().AssigneeKey.ShouldBe("mei");
            board.Panel.LaneHeads[2].Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    // Responsive, data, read-only ----------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void A_Narrow_Board_Is_Compact_With_Fixed_Columns_And_Snaps_Flush_Left()
    {
        var (window, board) = Show(390, 844);
        try
        {
            board.IsCompact.ShouldBeTrue();
            board.Classes.Contains(":compact").ShouldBeTrue();
            board.Panel!.CurrentLayout!.Metrics.ColumnWidth.ShouldBe(235);
            Pump(4);
            board.Scroller!.Offset.X.ShouldBe(12, 0.5);
            board.SnapTarget().ShouldBe(12);
            board.Scroller.Offset = new Vector(12 + 150, 0);
            board.SnapTarget().ShouldBe(259);
            // The drawer covers the board (width − 16) and stacks its status options.
            board.OpenCard(board.Cards[0]);
            Pump();
            board.Drawer!.Bounds.Width.ShouldBe(390 - 16, 0.5);
            board.Drawer.StatusSwitch!.Columns.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Compact_Board_Glides_To_The_Nearest_Column_When_Scrolling_Rests()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var now = TimeSpan.Zero;
        var (window, board) = Show(390, 844, ArcMotionLevel.Full, b => b.DragClock = () => now);
        try
        {
            Pump(4);
            board.Scroller!.Offset.X.ShouldBe(12, 0.5);
            board.Scroller.Offset = new Vector(12 + 150, 0);
            Pump(1);
            for (var i = 0; i < 90; i++)
            {
                now += TimeSpan.FromMilliseconds(16);
                clock.Advance(TimeSpan.FromMilliseconds(16));
                Dispatcher.UIThread.RunJobs();
            }
            board.Scroller.Offset.X.ShouldBe(259, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void With_Motion_Off_Every_Change_Lands_At_Once()
    {
        var (window, board) = Show();
        try
        {
            var view = View(board, "FLD-204");
            board.GroupBy = KanbanGroupBy.Assignee;
            Pump(1);
            board.Panel!.Flip.Get(view).Offset.Value.ShouldBe(default(Vector));
            board.MoveCard(view.Card, "queued", 0);
            Pump(1);
            board.Panel.Flip.Get(view).Offset.Value.ShouldBe(default(Vector));
            board.OpenCard(view.Card);
            Pump(1);
            board.Drawer!.SlideOffset.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Read_Only_Board_Shows_Cards_Without_Moving_Or_Adding()
    {
        var (window, board) = Show(setup: b => b.IsReadOnly = true);
        try
        {
            board.Panel!.AddRows.Count.ShouldBe(0);
            board.Panel.Heads[0].CanAdd.ShouldBeFalse();
            var start = Center(window, View(board, "FLD-236"));
            Press(window, start);
            Move(window, start + new Vector(40, 0));
            board.IsDragging.ShouldBeFalse();
            Release(window, start + new Vector(40, 0));
            board.BeginKeyboardDrag(Card(board, "FLD-236"));
            board.Session.ShouldBeNull();
            board.OpenCard(Card(board, "FLD-236"));
            Pump();
            board.Drawer!.TitleBox!.IsReadOnly.ShouldBeTrue();
            board.Drawer.StatusSwitch!.IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Outside_Changes_And_A_Bound_Source_Keep_The_Board_In_Step()
    {
        var source = new System.Collections.ObjectModel.ObservableCollection<KanbanCard>(KanbanBoardDemoData.CreateCards());
        var (window, board) = Show(setup: b => b.CardsSource = source);
        try
        {
            board.Panel!.Heads[0].Count.ShouldBe(4);
            source[1].ColumnKey = "shipped";
            Pump();
            board.Panel.Heads[0].Count.ShouldBe(3);
            board.Panel.Heads[3].Count.ShouldBe(4);
            source.RemoveAt(0);
            Pump();
            board.Panel.Heads[0].Count.ShouldBe(2);
            board.Panel.ViewOf(source[0]).ShouldNotBeNull();
            // A move reorders the bound list in place.
            board.MoveCard(source[0], "testing", 0);
            Pump();
            // source[0] is FLD-236 now (moved to Shipped above, then FLD-231 was removed).
            Keys(source.Where(c => c.ColumnKey == "testing"))[0].ShouldBe("FLD-236");
            Keys(board.EffectiveCards).ShouldBe(Keys(source));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Cards_Without_An_Owner_On_The_Board_Get_An_Unassigned_Lane()
    {
        var (window, board) = Show(setup: b =>
        {
            b.Cards[0].AssigneeKey = null;
            b.GroupBy = KanbanGroupBy.Assignee;
        });
        try
        {
            board.Panel!.LaneHeads.Count.ShouldBe(5);
            board.Panel.LaneHeads[4].PersonName.ShouldBe("Unassigned");
            board.Panel.LaneHeads[4].Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }
}
