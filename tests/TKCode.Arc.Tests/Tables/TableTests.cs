using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia;

namespace TKCode.Arc.Tests.Tables;

public class TableTests
{
    public sealed class Project
    {
        public Project(string name, string owner, double? budget, string status)
        {
            Name = name;
            Owner = owner;
            Budget = budget;
            Status = status;
        }

        public string Name { get; }
        public string Owner { get; }
        public double? Budget { get; }
        public string Status { get; }
    }

    private static ObservableCollection<Project> Projects() =>
    [
        new("Atlas", "Mina", 42000, "Active"),
        new("Beacon", "Theo", 18500, "Paused"),
        new("Cobalt", "Ana", null, "Active"),
        new("Item 10", "Zed", 9000, "Done"),
        new("Item 2", "Lee", 120000, "Active")
    ];

    private static Table Table(IEnumerable<Project> items, bool selectable = false)
    {
        var table = new Table { ItemsSource = items, IsSelectable = selectable };
        table.Columns.Add(DataTableColumn.Create<Project>("Name", p => p.Name));
        table.Columns.Add(DataTableColumn.Create<Project>("Owner", p => p.Owner));
        table.Columns.Add(DataTableColumn.Create<Project>("Budget", p => p.Budget));
        return table;
    }

    private static Window Show(Table table, double width = 800, double height = 600)
    {
        var window = TestHelpers.Show(table);
        window.Width = width;
        window.Height = height;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static List<string> Names(Table table) =>
        Enumerable.Range(0, table.RowCount).Select(d => ((Project)table.ItemAt(table.SourceAt(d))).Name).ToList();

    private static TableHeaderCell Header(Table table, int column) =>
        table.Presenter!.Header.CellFor(column)!;

    [AvaloniaFact]
    public void Header_Sorts_Ascending_Then_Descending_With_Empty_Values_Last()
    {
        var table = Table(Projects());
        var window = Show(table);
        try
        {
            Header(table, 2).Activate();
            table.SortColumn.ShouldBe(table.Columns[2]);
            table.SortDirection.ShouldBe(DataTableSortDirection.Ascending);
            table.SortKey.ShouldBe("Budget");
            Names(table).ShouldBe(["Item 10", "Beacon", "Atlas", "Item 2", "Cobalt"]);
            table.IsNumericColumn(2).ShouldBeTrue();
            Header(table, 2).Activate();
            table.SortDirection.ShouldBe(DataTableSortDirection.Descending);
            Names(table).ShouldBe(["Item 2", "Atlas", "Beacon", "Item 10", "Cobalt"]);
            Header(table, 2).Activate();
            table.SortDirection.ShouldBe(DataTableSortDirection.Ascending);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Text_Sorts_With_Natural_Order_And_Clearable_Cycle()
    {
        var table = Table(Projects());
        table.IsSortClearable = true;
        var window = Show(table);
        try
        {
            Header(table, 0).Activate();
            Names(table).ShouldBe(["Atlas", "Beacon", "Cobalt", "Item 2", "Item 10"]);
            Header(table, 0).Activate();
            Header(table, 0).Activate();
            table.SortDirection.ShouldBe(DataTableSortDirection.None);
            table.SortColumn.ShouldBeNull();
            Names(table).ShouldBe(["Atlas", "Beacon", "Cobalt", "Item 10", "Item 2"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SortKey_And_Comparer_Drive_The_Order()
    {
        var table = Table(Projects());
        table.Columns[1].Comparer = Comparer<object>.Create((a, b) => string.CompareOrdinal(((string)a).Substring(1), ((string)b).Substring(1)));
        var events = new List<DataTableSortChangedEventArgs>();
        table.SortChanged += (_, e) => events.Add(e);
        var window = Show(table);
        try
        {
            table.SortKey = "Owner";
            table.SortColumn.ShouldBe(table.Columns[1]);
            table.SortDirection.ShouldBe(DataTableSortDirection.Ascending);
            // Ordered by the owner's second letter: Mina(i), Theo(h), Ana(n), Zed(e), Lee(e).
            Names(table).ShouldBe(["Item 10", "Item 2", "Beacon", "Atlas", "Cobalt"]);
            table.Sort(table.Columns[0], DataTableSortDirection.Descending);
            events.Count.ShouldBe(1);
            events[0].Key.ShouldBe("Name");
            events[0].Direction.ShouldBe(DataTableSortDirection.Descending);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Binding_Columns_Evaluate_Against_Rows()
    {
        var table = new Table { ItemsSource = Projects() };
        table.Columns.Add(new DataTableColumn { Header = "Name", Binding = new Binding("Name") });
        table.Columns.Add(new DataTableColumn { Header = "Budget", Binding = new Binding("Budget"), StringFormat = "N0" });
        var window = Show(table);
        try
        {
            table.CellText(0, 0).ShouldBe("Atlas");
            table.CellText(0, 1).ShouldBe(42000.ToString("N0"));
            table.CellText(2, 1).ShouldBe("–");
            Header(table, 1).Activate();
            Names(table)[0].ShouldBe("Item 10");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Thousands_Of_Rows_Are_Virtualized()
    {
        var items = Enumerable.Range(0, 5000).Select(i => new Project($"Row {i}", "x", i, "Active")).ToList();
        var table = Table(items);
        table.MaxHeight = 400;
        var window = Show(table, 800, 600);
        try
        {
            table.RowCount.ShouldBe(5000);
            table.Presenter!.RealizedCount.ShouldBeLessThan(20);
            Header(table, 2).Activate();
            Header(table, 2).Activate();
            Dispatcher.UIThread.RunJobs();
            table.Presenter.RealizedCount.ShouldBeLessThan(20);
            Names(table)[0].ShouldBe("Row 4999");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Selection_Toggles_Extends_And_Clears()
    {
        var projects = Projects();
        var table = Table(projects, selectable: true);
        var changes = new List<DataTableSelectionChangedEventArgs>();
        table.SelectionChanged += (_, e) => changes.Add(e);
        var window = Show(table);
        try
        {
            table.CountText.ShouldBe("5");
            table.CountNoun.ShouldBe("rows");
            var first = table.Presenter!.RowAt(0)!;
            table.OnRowClicked(first, false);
            table.SelectedItems!.Cast<Project>().Select(p => p.Name).ShouldBe(["Atlas"]);
            table.OnRowClicked(table.Presenter.RowAt(2)!, true);
            table.SelectedItems!.Cast<Project>().Select(p => p.Name).ShouldBe(["Atlas", "Beacon", "Cobalt"]);
            table.CountText.ShouldBe("3");
            table.CountNoun.ShouldBe("selected");
            table.HasSelection.ShouldBeTrue();
            var all = Header(table, -1);
            all.Checkbox!.IsMixed.ShouldBeTrue();
            all.Activate();
            table.SelectedItems!.Count.ShouldBe(5);
            all.Checkbox.IsChecked.ShouldBeTrue();
            table.Presenter.RowAt(1)!.Focus();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            table.SelectedItems!.Count.ShouldBe(0);
            changes.Count.ShouldBe(4);
            changes[1].AddedItems.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Row_Clicks_Select_Unless_Only_The_Checkbox_Does(bool selectsOnRowClick)
    {
        var table = Table(Projects(), selectable: true);
        table.SelectsOnRowClick = selectsOnRowClick;
        var window = Show(table);
        try
        {
            var row = table.Presenter!.RowAt(0)!;
            void Click(double x)
            {
                var point = row.TranslatePoint(new Point(x, row.Bounds.Height / 2), window)!.Value;
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
            }

            Click(row.Bounds.Width / 2);
            table.SelectedItems!.Count.ShouldBe(selectsOnRowClick ? 1 : 0);
            Click(20); // the checkbox cell always toggles
            table.SelectedItems!.Count.ShouldBe(selectsOnRowClick ? 0 : 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Former_Name_Is_A_Table_With_The_Table_Theme()
    {
#pragma warning disable CS0618 // the obsolete name is what this test covers
        var table = new SortableDataTable { ItemsSource = Projects() };
#pragma warning restore CS0618
        table.Columns.Add(DataTableColumn.Create<Project>("Name", p => p.Name));
        var window = Show(table);
        try
        {
            table.Presenter.ShouldNotBeNull();
            table.RowCount.ShouldBe(5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Bound_Selection_Is_Updated_In_Place_And_Followed()
    {
        var projects = Projects();
        var selection = new ObservableCollection<object>();
        var table = Table(projects, selectable: true);
        table.SelectedItems = selection;
        var window = Show(table);
        try
        {
            table.OnRowClicked(table.Presenter!.RowAt(1)!, false);
            selection.Cast<Project>().Single().Name.ShouldBe("Beacon");
            selection.Add(projects[4]);
            table.IsSourceSelected(4).ShouldBeTrue();
            table.CountText.ShouldBe("2");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Walks_Headers_And_Rows()
    {
        var table = Table(Projects(), selectable: true);
        var window = Show(table);
        try
        {
            var all = Header(table, -1);
            all.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Header(table, 0).IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Header(table, 2).IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            table.SortKey.ShouldBe("Budget");
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            var row = table.Presenter!.RowAt(0)!;
            row.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            table.SelectedItems!.Cast<Project>().Single().Name.ShouldBe("Beacon");
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            all.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Source_Changes_Rebuild_Rows_And_Count()
    {
        var projects = Projects();
        var table = Table(projects, selectable: true);
        var window = Show(table);
        try
        {
            projects.Add(new Project("Delta", "Kai", 1, "Active"));
            Dispatcher.UIThread.RunJobs();
            table.RowCount.ShouldBe(6);
            table.CountText.ShouldBe("6");
            projects.Clear();
            Dispatcher.UIThread.RunJobs();
            table.RowCount.ShouldBe(0);
            table.Classes.Contains(":empty").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
