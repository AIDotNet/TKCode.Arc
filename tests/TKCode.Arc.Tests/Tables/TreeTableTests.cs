using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Tables;

public class TreeTableTests
{
    private sealed record Item(string Id, string Name, string Owner, int Cost, List<Item>? Children = null, bool Lazy = false);

    private static List<Item> Data() =>
    [
        new("design", "Design", "Mira", 30,
        [
            new("design-a", "Brand", "Mira", 10),
            new("design-b", "Illustration", "Noor", 20),
        ]),
        new("eng", "Engineering", "Sam", 90,
        [
            new("eng-b", "Backend", "Sam", 60,
            [
                new("eng-b1", "API", "Sam", 40),
                new("eng-b2", "Jobs", "Lee", 20),
            ]),
            new("eng-a", "Apps", "Lee", 30),
        ]),
        new("ops", "Operations", "Kay", 15),
    ];

    private static TreeTable Create(IEnumerable<Item>? items = null, bool selectable = false)
    {
        var table = new TreeTable
        {
            ItemsSource = (items ?? Data()).ToList(),
            ChildrenSelector = item => ((Item)item).Children,
            KeySelector = item => ((Item)item).Id,
            RowLabelSelector = item => ((Item)item).Name,
            IsSelectable = selectable,
            Width = 640
        };
        table.Columns.Add(TreeTableColumn.Create<Item>("Name", i => i.Name));
        table.Columns.Add(new TreeTableColumn { Header = "Owner", ValueSelector = i => ((Item)i).Owner, HideBelow = 700 });
        table.Columns.Add(new TreeTableColumn
        {
            Header = "Cost", ValueSelector = i => ((Item)i).Cost, Width = new GridLength(96), IsEndAligned = true, StringFormat = "{0} MB"
        });
        return table;
    }

    private static Window Show(TreeTable table, ArcMotionLevel level = ArcMotionLevel.Off, double width = 800)
    {
        var window = TestHelpers.Show(table, level);
        window.Width = width;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static IEnumerable<Item> Roots(TreeTable table) => table.ItemsSource!.Cast<Item>();

    private static string[] Keys(TreeTable table) => table.VisibleRows.Select(l => (string)l.Key).ToArray();

    [AvaloniaFact]
    public void Top_Level_Rows_Show_Collapsed_With_Header()
    {
        var table = Create();
        var window = Show(table);
        try
        {
            Keys(table).ShouldBe(["design", "eng", "ops"]);
            table.Header!.Cells.Select(c => c.Column.Header).ShouldBe(["Name", "Cost"]);
            table.VisibleRows[0].Expandable.ShouldBeTrue();
            table.VisibleRows[2].Expandable.ShouldBeFalse();
            table.VisibleRows[0].Depth.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Expanding_Adds_Child_Rows_At_The_Next_Depth_And_Raises_The_Event()
    {
        var table = Create();
        var window = Show(table);
        try
        {
            IReadOnlyList<object>? reported = null;
            table.RowExpandedChanged += (_, e) => reported = e.ExpandedKeys;
            table.Expand(Roots(table).First(i => i.Id == "eng"));
            Keys(table).ShouldBe(["design", "eng", "eng-b", "eng-a", "ops"]);
            table.VisibleRows[2].Depth.ShouldBe(2);
            table.VisibleRows[2].ParentKey.ShouldBe("eng");
            table.VisibleRows[2].Position.ShouldBe(1);
            table.VisibleRows[2].Size.ShouldBe(2);
            reported.ShouldNotBeNull().ShouldBe(["eng"]);
            table.ExpandedKeys.ShouldNotBeNull().ShouldBe(["eng"]);

            table.Collapse(Roots(table).First(i => i.Id == "eng"));
            Keys(table).ShouldBe(["design", "eng", "ops"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ExpandedKeys_From_The_Outside_Open_Rows()
    {
        var table = Create();
        table.ExpandedKeys = new object[] { "eng", "eng-b" };
        var window = Show(table);
        try
        {
            Keys(table).ShouldBe(["design", "eng", "eng-b", "eng-b1", "eng-b2", "eng-a", "ops"]);
            table.VisibleRows.Select(l => l.Depth).ShouldBe([1, 1, 2, 3, 3, 2, 1]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Expand_All_Toggle_Opens_And_Closes_Every_Parent()
    {
        var table = Create();
        var window = Show(table);
        try
        {
            table.Header!.HasExpandAll.ShouldBeTrue();
            table.ExpandAll();
            Keys(table).ShouldBe(["design", "design-a", "design-b", "eng", "eng-b", "eng-b1", "eng-b2", "eng-a", "ops"]);
            table.Header.LastEveryOpen.ShouldBeTrue();
            table.CollapseAll();
            Keys(table).ShouldBe(["design", "eng", "ops"]);
            table.Header.LastEveryOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Follows_The_Treegrid_Pattern()
    {
        var table = Create();
        var window = Show(table);
        try
        {
            table.FocusItem(Roots(table).First(i => i.Id == "design"));
            Dispatcher.UIThread.RunJobs();
            table.FocusKey.ShouldBe("design");

            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Keys(table).ShouldBe(["design", "design-a", "design-b", "eng", "ops"]);
            table.FocusKey.ShouldBe("design");

            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            table.FocusKey.ShouldBe("design-a");

            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            table.FocusKey.ShouldBe("design-b");

            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            table.FocusKey.ShouldBe("design");

            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Keys(table).ShouldBe(["design", "eng", "ops"]);

            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            table.FocusKey.ShouldBe("ops");
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            table.FocusKey.ShouldBe("design");

            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            table.IsExpanded(Roots(table).First(i => i.Id == "design")).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_On_A_Leaf_Raises_RowActivated_And_Parents_Do_Not()
    {
        var table = Create();
        var window = Show(table);
        try
        {
            var activated = new List<string>();
            table.RowActivated += (_, e) => activated.Add((string)e.Key);
            table.FocusItem(Roots(table).First(i => i.Id == "ops"));
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            activated.ShouldBe(["ops"]);
            table.FocusItem(Roots(table).First(i => i.Id == "design"));
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            activated.ShouldBe(["ops"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Star_Expands_All_Siblings()
    {
        var table = Create();
        var window = Show(table);
        try
        {
            table.FocusItem(Roots(table).First(i => i.Id == "design"));
            window.KeyPressQwerty(PhysicalKey.Digit8, RawInputModifiers.Shift);
            Keys(table).ShouldBe(["design", "design-a", "design-b", "eng", "eng-b", "eng-a", "ops"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Collapsing_A_Parent_Moves_Focus_To_The_Nearest_Visible_Ancestor()
    {
        var table = Create();
        table.ExpandedKeys = new object[] { "eng", "eng-b" };
        var window = Show(table);
        try
        {
            table.FocusItem(Roots(table).SelectMany(i => i.Children ?? []).SelectMany(i => i.Children ?? []).First(i => i.Id == "eng-b2"));
            table.FocusKey.ShouldBe("eng-b2");
            table.Collapse(Roots(table).First(i => i.Id == "eng"));
            table.FocusKey.ShouldBe("eng");
            TopLevelFocus(window).ShouldBe("eng");
        }
        finally
        {
            window.Close();
        }
    }

    private static object? TopLevelFocus(Window window) => (window.FocusManager?.GetFocusedElement() as TreeTableRow)?.Key;

    [AvaloniaFact]
    public void Selecting_A_Parent_Selects_Everything_Under_It_And_A_Partial_Child_Selection_Is_Mixed()
    {
        var table = Create(selectable: true);
        table.ExpandedKeys = new object[] { "eng", "eng-b" };
        var window = Show(table);
        try
        {
            IReadOnlyList<object>? last = null;
            table.SelectionChanged += (_, e) => last = e.SelectedKeys;
            table.ToggleSelect("eng-b");
            table.SelectedKeys.ShouldNotBeNull().OrderBy(k => (string)k).ShouldBe(["eng-b", "eng-b1", "eng-b2"]);
            last.ShouldNotBeNull().Count.ShouldBe(3);
            Check(table, "eng").ShouldBe(CheckboxState.Indeterminate);
            Check(table, "eng-b").ShouldBe(CheckboxState.Checked);
            Check(table, "eng-b1").ShouldBe(CheckboxState.Checked);

            table.ToggleSelect("eng-a");
            Check(table, "eng").ShouldBe(CheckboxState.Checked);
            table.SelectedKeys.ShouldNotBeNull().ShouldContain("eng");

            table.ToggleSelect("eng-b1");
            Check(table, "eng-b").ShouldBe(CheckboxState.Indeterminate);
            Check(table, "eng").ShouldBe(CheckboxState.Indeterminate);
            table.SelectedKeys.ShouldNotBeNull().ShouldNotContain("eng");
            table.SelectedKeys.ShouldNotContain("eng-b");
        }
        finally
        {
            window.Close();
        }
    }

    private static CheckboxState Check(TreeTable table, string key) => table.RowsHost!.RowFor(key)!.Check!.CheckedState;

    [AvaloniaFact]
    public void Select_All_Checkbox_Is_Mixed_Then_Checked_And_Clears()
    {
        var table = Create(selectable: true);
        var window = Show(table);
        try
        {
            var all = table.Header!.SelectAllCheckbox!;
            all.CheckedState.ShouldBe(CheckboxState.Unchecked);
            table.ToggleSelect("ops");
            all.CheckedState.ShouldBe(CheckboxState.Indeterminate);
            table.ToggleSelectAll();
            all.CheckedState.ShouldBe(CheckboxState.Checked);
            table.SelectedKeys.ShouldNotBeNull().Count.ShouldBe(9);
            table.ToggleSelectAll();
            all.CheckedState.ShouldBe(CheckboxState.Unchecked);
            table.SelectedKeys.ShouldNotBeNull().Count.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Space_Toggles_Selection_And_Ctrl_A_Selects_All()
    {
        var table = Create(selectable: true);
        var window = Show(table);
        try
        {
            table.FocusItem(Roots(table).First(i => i.Id == "ops"));
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            table.SelectedKeys.ShouldNotBeNull().ShouldBe(["ops"]);
            window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);
            table.SelectedKeys.ShouldNotBeNull().Count.ShouldBe(9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sorting_Orders_Siblings_Inside_Their_Parent_And_Cycles_To_Off()
    {
        var table = Create();
        table.ExpandedKeys = new object[] { "eng" };
        var window = Show(table);
        try
        {
            var cost = table.Header!.Cells.First(c => c.Column.Header == "Cost");
            cost.Activate();
            table.SortKey.ShouldBe("Cost");
            table.SortDirection.ShouldBe(DataTableSortDirection.Ascending);
            Keys(table).ShouldBe(["ops", "design", "eng", "eng-a", "eng-b"]);

            cost.Activate();
            table.SortDirection.ShouldBe(DataTableSortDirection.Descending);
            Keys(table).ShouldBe(["eng", "eng-b", "eng-a", "design", "ops"]);

            cost.Activate();
            table.SortKey.ShouldBeNull();
            table.SortDirection.ShouldBe(DataTableSortDirection.None);
            Keys(table).ShouldBe(["design", "eng", "eng-b", "eng-a", "ops"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sort_Raises_The_Event_And_Marks_The_Header()
    {
        var table = Create();
        var window = Show(table);
        try
        {
            TreeTableSortChangedEventArgs? seen = null;
            table.SortChanged += (_, e) => seen = e;
            var name = table.Header!.Cells.First(c => c.Column.Header == "Name");
            name.Activate();
            seen.ShouldNotBeNull().Column.ShouldBe(name.Column);
            seen.Direction.ShouldBe(DataTableSortDirection.Ascending);
            name.Direction.ShouldBe(DataTableSortDirection.Ascending);
            table.LastAnnouncement.ShouldBe("Sorted by name, ascending");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Columns_Below_Their_HideBelow_Width_Are_Hidden()
    {
        var table = Create();
        table.Width = 640;
        var window = Show(table, width: 800);
        try
        {
            table.ShownColumns.Select(c => c.Header).ShouldBe(["Name", "Cost"]);
            table.Width = 760;
            Dispatcher.UIThread.RunJobs();
            table.ShownColumns.Select(c => c.Header).ShouldBe(["Name", "Owner", "Cost"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Lazy_Children_Load_On_First_Expand_Through_A_Loading_Row()
    {
        var gate = new TaskCompletionSource<System.Collections.IEnumerable?>();
        var calls = 0;
        var table = new TreeTable
        {
            ItemsSource = new List<Item> { new("root", "Root", "x", 1, null, Lazy: true) },
            ChildrenSelector = item => ((Item)item).Children,
            HasChildrenSelector = item => ((Item)item).Lazy,
            ChildrenLoader = _ =>
            {
                calls++;
                return gate.Task;
            },
            KeySelector = item => ((Item)item).Id,
            Width = 600
        };
        table.Columns.Add(TreeTableColumn.Create<Item>("Name", i => i.Name));
        var window = Show(table);
        try
        {
            table.VisibleRows[0].Expandable.ShouldBeTrue();
            table.Expand(Roots(table).First());
            table.RowsHost!.RowFor(new TreeTablePendingKey("root")).ShouldNotBeNull();
            table.RowsHost.RowFor("root")!.Chevron!.IsVisible.ShouldBeFalse();
            calls.ShouldBe(1);

            gate.SetResult(new List<object> { new Item("kid", "Kid", "x", 1), new Item("kid2", "Kid 2", "x", 2) });
            await Task.Yield();
            Dispatcher.UIThread.RunJobs();
            Keys(table).ShouldBe(["root", "kid", "kid2"]);
            table.RowsHost.RowFor(new TreeTablePendingKey("root")).ShouldBeNull();
            table.LastAnnouncement.ShouldBe("2 rows loaded");

            // Collapsing and opening again does not fetch twice.
            table.Collapse(Roots(table).First());
            table.Expand(Roots(table).First());
            calls.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_Failed_Load_Shows_A_Retry_Row_That_Loads_Again()
    {
        var attempts = 0;
        var table = new TreeTable
        {
            ItemsSource = new List<Item> { new("root", "Root", "x", 1, null, Lazy: true) },
            ChildrenSelector = item => ((Item)item).Children,
            HasChildrenSelector = item => ((Item)item).Lazy,
            KeySelector = item => ((Item)item).Id,
            Width = 600
        };
        table.ChildrenLoader = _ =>
        {
            attempts++;
            return attempts == 1
                ? Task.FromException<System.Collections.IEnumerable?>(new InvalidOperationException("offline"))
                : Task.FromResult<System.Collections.IEnumerable?>(new List<object> { new Item("kid", "Kid", "x", 1) });
        };
        table.Columns.Add(TreeTableColumn.Create<Item>("Name", i => i.Name));
        var window = Show(table);
        try
        {
            Exception? failure = null;
            table.ChildrenLoadFailed += (_, e) => failure = e.Error;
            table.Expand(Roots(table).First());
            await Task.Yield();
            Dispatcher.UIThread.RunJobs();
            failure.ShouldNotBeNull().Message.ShouldBe("offline");
            var pending = table.RowsHost!.RowFor(new TreeTablePendingKey("root")).ShouldNotBeNull();
            pending.Line.Kind.ShouldBe(TreeTableLineKind.Error);
            pending.RetryButton.ShouldNotBeNull();

            table.Retry(pending);
            await Task.Yield();
            Dispatcher.UIThread.RunJobs();
            Keys(table).ShouldBe(["root", "kid"]);
            attempts.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Children_That_Load_Under_A_Checked_Parent_Start_Checked()
    {
        var table = new TreeTable
        {
            ItemsSource = new List<Item> { new("root", "Root", "x", 1, null, Lazy: true) },
            ChildrenSelector = item => ((Item)item).Children,
            HasChildrenSelector = item => ((Item)item).Lazy,
            ChildrenLoader = _ => Task.FromResult<System.Collections.IEnumerable?>(new List<object> { new Item("kid", "Kid", "x", 1) }),
            KeySelector = item => ((Item)item).Id,
            IsSelectable = true,
            Width = 600
        };
        table.Columns.Add(TreeTableColumn.Create<Item>("Name", i => i.Name));
        var window = Show(table);
        try
        {
            table.ToggleSelect("root");
            table.Expand(Roots(table).First());
            await Task.Yield();
            Dispatcher.UIThread.RunJobs();
            table.SelectedKeys.ShouldNotBeNull().OrderBy(k => (string)k).ShouldBe(["kid", "root"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Empty_Table_Shows_The_Message()
    {
        var table = Create(new List<Item>());
        table.EmptyMessage = "No files";
        var window = Show(table);
        try
        {
            TestHelpers.Descendant<TextBlock>(table, TreeTable.EmptyPart).IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(table, TreeTable.EmptyPart).Text.ShouldBe("No files");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Only_One_Row_Is_A_Tab_Stop()
    {
        var table = Create();
        var window = Show(table);
        try
        {
            table.RowsHost!.Rows.Count(r => r.IsTabStop).ShouldBe(1);
            table.FocusItem(Roots(table).First(i => i.Id == "ops"));
            table.RowsHost.Rows.Single(r => r.IsTabStop).Key.ShouldBe("ops");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Indent_Guides_Run_Through_Each_Ancestor()
    {
        var table = Create();
        table.ExpandedKeys = new object[] { "eng", "eng-b" };
        var window = Show(table);
        try
        {
            table.RowsHost!.RowFor("eng")!.Guides!.Levels.ShouldBe(0);
            table.RowsHost.RowFor("eng-b")!.Guides!.Levels.ShouldBe(1);
            table.RowsHost.RowFor("eng-b1")!.Guides!.Levels.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Row_Heights_Are_44_And_Rows_Stack_With_Full_Motion_Settled_By_Off()
    {
        var table = Create();
        table.ExpandedKeys = new object[] { "design" };
        var window = Show(table);
        try
        {
            var rows = table.RowsHost!.Rows.ToList();
            rows.Select(r => r.Bounds.Height).ShouldAllBe(h => Math.Abs(h - 44) < 0.5);
            rows.Select(r => r.Bounds.Y).ShouldBe([0, 44, 88, 132, 176]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rows_Leave_And_Enter_With_Full_Motion()
    {
        var table = Create();
        var window = Show(table, ArcMotionLevel.Full);
        try
        {
            table.Expand(Roots(table).First(i => i.Id == "design"));
            var child = table.RowsHost!.RowFor("design-a")!;
            child.Presence.ShouldBeLessThan(0.5);
            table.Collapse(Roots(table).First(i => i.Id == "design"));
            // The leaving row stays in the stack until its spring is done.
            table.RowsHost.RowFor("design-a").ShouldNotBeNull();
            table.RowsHost.RowFor("design-a")!.IsExiting.ShouldBeTrue();
            table.VisibleRows.Select(l => (string)l.Key).ShouldBe(["design", "eng", "ops"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Binding_Free_Selectors_Format_Cells()
    {
        var table = Create();
        var window = Show(table);
        try
        {
            var cost = table.Columns[2];
            table.CellText(cost, Roots(table).First()).ShouldBe("30 MB");
            table.LabelOf(Roots(table).First()).ShouldBe("Design");
        }
        finally
        {
            window.Close();
        }
    }
}
