using Avalonia;

namespace TKCode.Arc.Tests.Blocks.Projects;

/// <summary>The Kanban board's pure geometry and rules (no UI): widths, cells, lanes, hit testing, WIP, auto-scroll, snapping.</summary>
public class KanbanBoardLayoutTests
{
    private static KanbanCellInput Cell(params double[] items) => new(items, double.NaN);

    private static KanbanCellInput CellWithAddRow(params double[] items) => new(items, KanbanLayout.AddRowHeight);

    [Fact]
    public void Columns_Fill_The_Width_Down_To_251_Then_Scroll()
    {
        // (W − 32 − 36) / 4, never under 251: 1440 → 343, 1100 → 258, 1072 → 251 exactly.
        KanbanLayout.Measure(1440, 4).ColumnWidth.ShouldBe(343);
        KanbanLayout.Measure(1100, 4).ColumnWidth.ShouldBe(258);
        KanbanLayout.Measure(1072, 4).ColumnWidth.ShouldBe(251);
        var narrow = KanbanLayout.Measure(900, 4);
        narrow.ColumnWidth.ShouldBe(251);
        narrow.ExtentWidth.ShouldBe(1072);
        narrow.IsCompact.ShouldBeFalse();
        narrow.Padding.ShouldBe(16);
        narrow.CardWidth.ShouldBe(251 - 18);
        KanbanLayout.Measure(1440, 4).CardWidth.ShouldBe(325);
        KanbanLayout.Measure(1440, 4).ColumnX(1).ShouldBe(16 + 343 + 12);
    }

    [Fact]
    public void At_560_And_Below_The_Board_Turns_Compact()
    {
        var at = KanbanLayout.Measure(560, 4);
        at.IsCompact.ShouldBeTrue();
        at.ColumnWidth.ShouldBe(235);
        at.Padding.ShouldBe(12);
        at.ExtentWidth.ShouldBe(4 * 235 + 36 + 24);
        at.SnapStep.ShouldBe(247);
        KanbanLayout.Measure(561, 4).IsCompact.ShouldBeFalse();
        KanbanLayout.Measure(390, 4, compactColumnWidth: 200).ColumnWidth.ShouldBe(200);
        KanbanLayout.Measure(1440, 3).ColumnWidth.ShouldBe((1440 - 32 - 24) / 3.0);
    }

    [Fact]
    public void Status_Cells_Stack_Cards_8_Apart_And_Fill_The_Viewport()
    {
        var metrics = KanbanLayout.Measure(1440, 4);
        var cells = new[]
        {
            CellWithAddRow(124.8, 105.2, 105.2, 105.2),
            CellWithAddRow(105.2, 105.2, 105.2),
            CellWithAddRow(),
            Cell()
        };
        var layout = KanbanLayout.Layout(metrics, new Size(1440, 928), 1, false, cells);
        var first = layout.Cell(0, 0);
        // Head row 42, gap 12: the cells start at 54; items 9 inside (1px border + 8 padding).
        first.Bounds.Y.ShouldBe(54);
        first.ItemTops[0].ShouldBe(63);
        first.ItemTops[1].ShouldBe(63 + 124.8 + 8, 0.001);
        first.ItemX.ShouldBe(16 + 9);
        first.Tail!.Value.Y.ShouldBe(first.ItemTops[3] + 105.2 + 8, 0.001);
        first.Tail!.Value.Height.ShouldBe(34);
        // Status rows fill the viewport under the head row: 928 − 42 − 12 − 16 = 858 (Arc's 1440 × 1000 frame).
        first.Bounds.Height.ShouldBe(858);
        layout.Cell(0, 3).Bounds.Height.ShouldBe(858);
        // An empty cell with the Add card row puts it at the top.
        layout.Cell(0, 2).Tail!.Value.Y.ShouldBe(63);
        layout.Cell(0, 3).Tail.ShouldBeNull();
        layout.Extent.ShouldBe(new Size(1440, 928));
        layout.Lanes.Length.ShouldBe(1);
        layout.Lanes[0].HasHead.ShouldBeFalse();
    }

    [Fact]
    public void Swimlane_Rows_Are_As_Tall_As_Their_Tallest_Cell_And_At_Least_96()
    {
        var metrics = KanbanLayout.Measure(1440, 2);
        var cells = new[]
        {
            Cell(105.2), Cell(124.8),
            Cell(), Cell()
        };
        var layout = KanbanLayout.Layout(metrics, new Size(1440, 600), 2, true, cells);
        // Lane 1: head 54..90, 8 gap, cells from 98; as tall as the 124.8 card plus 18.
        layout.Lanes[0].Top.ShouldBe(54);
        layout.Lanes[0].CellsTop.ShouldBe(98);
        layout.Lanes[0].CellsHeight.ShouldBe(124.8 + 18, 0.001);
        layout.Cell(0, 0).Bounds.Height.ShouldBe(124.8 + 18, 0.001);
        // Lane 2 (empty): 12 under lane 1, cells 96 high (Arc's empty lane is 140 = 36 + 8 + 96).
        layout.Lanes[1].Top.ShouldBe(98 + 142.8 + 12, 0.001);
        layout.Lanes[1].CellsHeight.ShouldBe(96);
        (layout.Lanes[1].Bottom - layout.Lanes[1].Top).ShouldBe(140, 0.001);
        layout.Extent.Height.ShouldBe(Math.Max(600, layout.Lanes[1].Bottom + 16), 0.001);
    }

    [Fact]
    public void A_Pointer_Picks_The_Column_By_X_With_Boundaries_In_The_Gaps()
    {
        var metrics = KanbanLayout.Measure(1440, 4);
        // Boundaries at the gap midpoints: 16 + 343 + 6 = 365, 720, 1075.
        KanbanLayout.ColumnAt(metrics, 364.9).ShouldBe(0);
        KanbanLayout.ColumnAt(metrics, 365).ShouldBe(1);
        KanbanLayout.ColumnAt(metrics, 719).ShouldBe(1);
        KanbanLayout.ColumnAt(metrics, 721).ShouldBe(2);
        KanbanLayout.ColumnAt(metrics, 1076).ShouldBe(3);
        // Outside the board: the nearest end column.
        KanbanLayout.ColumnAt(metrics, -50).ShouldBe(0);
        KanbanLayout.ColumnAt(metrics, 5000).ShouldBe(3);
        KanbanLayout.ColumnAt(KanbanLayout.Measure(1440, 0), 10).ShouldBe(-1);
    }

    [Fact]
    public void A_Pointer_Picks_The_Lane_With_The_Heads_As_Dead_Zones()
    {
        var lanes = new[]
        {
            new KanbanLaneRect(54, 98, 123.2, true),
            new KanbanLaneRect(233.2, 277.2, 123.2, true),
            new KanbanLaneRect(412.4, 456.4, 96, true)
        };
        KanbanLayout.LaneAt(lanes, 0, 2).ShouldBe(0);         // above the first lane
        KanbanLayout.LaneAt(lanes, 60, 1).ShouldBe(1);        // lane 1's head: keep the current lane
        KanbanLayout.LaneAt(lanes, 90, 1).ShouldBe(0);        // 8 above lane 1's cells
        KanbanLayout.LaneAt(lanes, 230, 0).ShouldBe(0);       // the gap under lane 1 is lane 1's
        KanbanLayout.LaneAt(lanes, 240, 0).ShouldBe(0);       // lane 2's head is dead: stay
        KanbanLayout.LaneAt(lanes, 240, 2).ShouldBe(2);
        KanbanLayout.LaneAt(lanes, 277.2 - 8, 0).ShouldBe(1); // entering lane 2 from above
        KanbanLayout.LaneAt(lanes, 5000, 0).ShouldBe(2);
        KanbanLayout.LaneAt([], 10, 0).ShouldBe(-1);
        // Status view's single lane has no head: everything is lane 0.
        KanbanLayout.LaneAt([new KanbanLaneRect(54, 54, 800, false)], 40, 0).ShouldBe(0);
    }

    [Fact]
    public void The_Insertion_Index_Counts_Other_Cards_Whose_Middle_Is_Above_The_Pointer()
    {
        // Arc's sweep: slot at 1 with C1 at 135 (h 124.8), slot, C3 at 381, C4 at 494 (h 105.2).
        var slotAtOne = new[] { 135 + 62.4, 381 + 52.6, 494 + 52.6 };
        KanbanLayout.IndexAt(slotAtOne, 300).ShouldBe(1);
        KanbanLayout.IndexAt(slotAtOne, 434).ShouldBe(2);  // passes C3's middle (433.6)
        // With the slot at 2, C3 has moved up to 268: back to 1 only under its new middle (320.6).
        var slotAtTwo = new[] { 135 + 62.4, 268 + 52.6, 494 + 52.6 };
        KanbanLayout.IndexAt(slotAtTwo, 330).ShouldBe(2);
        KanbanLayout.IndexAt(slotAtTwo, 320).ShouldBe(1);
        KanbanLayout.IndexAt(slotAtTwo, 197).ShouldBe(0);
        KanbanLayout.IndexAt(slotAtTwo, 900).ShouldBe(3);   // under the last card (or on Add card): the end
        KanbanLayout.IndexAt([], 50).ShouldBe(0);           // an empty cell: the top
    }

    [Fact]
    public void A_Column_Is_Over_Its_Limit_Only_Past_It()
    {
        KanbanLayout.IsOverLimit(3, 3).ShouldBeFalse();
        KanbanLayout.IsOverLimit(4, 3).ShouldBeTrue();
        KanbanLayout.IsOverLimit(40, null).ShouldBeFalse();
        var column = new KanbanColumn("building", "Building", 3);
        column.SetCount(3).ShouldBeFalse();
        column.IsOverLimit.ShouldBeFalse();
        column.SetCount(4).ShouldBeTrue();
        column.IsOverLimit.ShouldBeTrue();
        column.Count.ShouldBe(4);
        column.Limit = 5;
        column.IsOverLimit.ShouldBeFalse();
        column.Identity.ShouldBe("building");
        new KanbanColumn { Name = "Done" }.Identity.ShouldBe("Done");
    }

    [Fact]
    public void Auto_Scroll_Speeds_Up_Quadratically_Inside_64px()
    {
        // Measured px per 60Hz frame at distance d from the edge.
        var expected = new (double D, double PerFrame)[]
        {
            (64, 0), (63, 1), (62, 1), (58, 1), (50, 1), (45, 2), (40, 3), (30, 6), (20, 10), (15, 12), (10, 15), (5, 17), (0, 20), (-30, 20)
        };
        foreach (var (d, perFrame) in expected)
        {
            KanbanLayout.AutoScrollPerFrame(d).ShouldBe(perFrame, $"d = {d}");
        }
        KanbanLayout.AutoScrollSpeed(0).ShouldBe(1200);
        KanbanLayout.AutoScrollVelocity(10, 500).ShouldBe(-900);
        KanbanLayout.AutoScrollVelocity(490, 500).ShouldBe(900);
        KanbanLayout.AutoScrollVelocity(250, 500).ShouldBe(0);
        // A viewport shorter than two zones: the nearer edge wins.
        KanbanLayout.AutoScrollVelocity(20, 100).ShouldBeLessThan(0);
        KanbanLayout.AutoScrollVelocity(0, 0).ShouldBe(0);
    }

    [Fact]
    public void Compact_Snap_Points_Put_Each_Column_Flush_Left()
    {
        var metrics = KanbanLayout.Measure(390, 4);
        var max = metrics.ExtentWidth - 390;
        var points = KanbanLayout.SnapPoints(metrics, max);
        points.ShouldBe([12, 259, 506, Math.Min(753, max)]);
        // Proximity: a 60px nudge falls back to the first column; 150 and 300 land on the second (Arc's wheel test).
        KanbanLayout.NearestSnap(points, 12 + 60).ShouldBe(12);
        KanbanLayout.NearestSnap(points, 12 + 150).ShouldBe(259);
        KanbanLayout.NearestSnap(points, 12 + 300).ShouldBe(259);
        KanbanLayout.NearestSnap([], 40).ShouldBe(40);
    }

    [Fact]
    public void A_Drop_Goes_Before_The_Card_It_Lands_Above_In_The_Flat_List()
    {
        var a = new object();
        var b = new object();
        var c = new object();
        var d = new object();
        // Board order without the moved card: a, b, c, d; the target cell holds b and d.
        var flat = new[] { a, b, c, d };
        var cell = new[] { b, d };
        KanbanLayout.FlatInsertionIndex(flat, cell, 0, 3).ShouldBe(1);   // above b
        KanbanLayout.FlatInsertionIndex(flat, cell, 1, 0).ShouldBe(3);   // above d
        KanbanLayout.FlatInsertionIndex(flat, cell, 2, 0).ShouldBe(4);   // after d
        KanbanLayout.FlatInsertionIndex(flat, Array.Empty<object>(), 0, 2).ShouldBe(2); // an empty cell keeps its place
        KanbanLayout.FlatInsertionIndex(flat, Array.Empty<object>(), 0, 9).ShouldBe(4);
    }

    [Fact]
    public void The_Tilt_Follows_Horizontal_Speed_Up_To_7_Degrees()
    {
        KanbanDragPhysics.TiltFor(176).ShouldBe(1.1264, 0.001);
        KanbanDragPhysics.TiltFor(588).ShouldBe(3.7632, 0.001);
        KanbanDragPhysics.TiltFor(1500).ShouldBe(7);
        KanbanDragPhysics.TiltFor(-1500).ShouldBe(-7);
        KanbanDragPhysics.TiltFor(double.NaN).ShouldBe(0);
        // The threshold is strict: 5.0 does not lift, 5.2 does.
        KanbanDragPhysics.PassedThreshold(new Point(100, 300), new Point(103, 304)).ShouldBeFalse();
        KanbanDragPhysics.PassedThreshold(new Point(100, 300), new Point(105.2, 300)).ShouldBeTrue();
        KanbanDragPhysics.Catch.Stiffness.ShouldBe(690);
        KanbanDragPhysics.Catch.Damping.ShouldBe(51);
    }

    [Fact]
    public void A_Touch_Lifts_After_A_Still_Hold_And_Pans_When_It_Moves_First()
    {
        var hold = new KanbanHoldRecognizer();
        hold.Press(new Point(10, 10), TimeSpan.Zero);
        hold.State.ShouldBe(KanbanHoldState.Pending);
        hold.Move(new Point(14, 13), TimeSpan.FromMilliseconds(100)).ShouldBe(KanbanHoldState.Pending);
        hold.Tick(TimeSpan.FromMilliseconds(249)).ShouldBe(KanbanHoldState.Pending);
        hold.Tick(TimeSpan.FromMilliseconds(250)).ShouldBe(KanbanHoldState.Lifted);

        hold.Press(new Point(10, 10), TimeSpan.FromSeconds(1));
        hold.Move(new Point(10, 40), TimeSpan.FromSeconds(1.15)).ShouldBe(KanbanHoldState.Panning);
        hold.Tick(TimeSpan.FromSeconds(2)).ShouldBe(KanbanHoldState.Panning);
        hold.Reset();
        hold.State.ShouldBe(KanbanHoldState.Idle);
    }

    [Fact]
    public void The_Grid_Splits_The_Flat_List_By_Column_And_Owner_In_List_Order()
    {
        var columns = new[] { new KanbanColumn("todo", "To do"), new KanbanColumn("done", "Done") };
        var ana = new ProjectPerson("Ana Ruiz", "ana");
        var ben = new ProjectPerson("Ben Okoro", "ben");
        var c1 = new KanbanCard("K-1", "One", "todo") { AssigneeKey = "ben" };
        var c2 = new KanbanCard("K-2", "Two", "done") { AssigneeKey = "ana" };
        var c3 = new KanbanCard("K-3", "Three", "todo") { AssigneeKey = "ana" };
        var c4 = new KanbanCard("K-4", "Four", "todo") { AssigneeKey = "zed" };
        var c5 = new KanbanCard("K-5", "Lost", "nowhere");
        var cards = new[] { c1, c2, c3, c4, c5 };

        var status = KanbanGrid.Build(cards, columns, [ana, ben], KanbanGroupBy.Status);
        status.Lanes.Count.ShouldBe(1);
        status.Cell(0, 0).ShouldBe([c1, c3, c4]);
        status.Cell(0, 1).ShouldBe([c2]);
        status.ColumnCount(0).ShouldBe(3);
        status.Placed.ShouldNotContain(c5);
        status.SlotOf(c3).ShouldBe(new KanbanSlot("todo", "ana", 1));

        var lanes = KanbanGrid.Build(cards, columns, [ana, ben], KanbanGroupBy.Assignee);
        lanes.Lanes.Count.ShouldBe(3);
        lanes.Lanes[2].IsUnassigned.ShouldBeTrue();
        lanes.Cell(0, 0).ShouldBe([c3]);
        lanes.Cell(1, 0).ShouldBe([c1]);
        lanes.Cell(2, 0).ShouldBe([c4]);
        lanes.LaneCount(0).ShouldBe(2);
        lanes.ColumnCount(0).ShouldBe(3);
        lanes.SlotOf(c4).ShouldBe(new KanbanSlot("todo", null, 0));

        // Nobody without an owner on the board: no unassigned lane.
        KanbanGrid.Build([c1, c2, c3], columns, [ana, ben], KanbanGroupBy.Assignee).Lanes.Count.ShouldBe(2);
    }

    [Fact]
    public void A_Card_Counts_Its_Checklist()
    {
        var card = new KanbanCard("K-1", "One", "todo");
        card.ChecklistTotal.ShouldBe(0);
        card.IsChecklistComplete.ShouldBeFalse();
        var step = new KanbanChecklistItem("Draft");
        card.Checklist.Add(step);
        card.Checklist.Add(new KanbanChecklistItem("Review", true));
        card.ChecklistTotal.ShouldBe(2);
        card.ChecklistDone.ShouldBe(1);
        step.IsDone = true;
        card.IsChecklistComplete.ShouldBeTrue();
        card.Checklist.RemoveAt(0);
        card.ChecklistDone.ShouldBe(1);
        card.Checklist = [new KanbanChecklistItem("New")];
        card.ChecklistTotal.ShouldBe(1);
        card.ChecklistDone.ShouldBe(0);
        step.IsDone = false;
        card.ChecklistDone.ShouldBe(0);
    }
}
