using System.Collections.ObjectModel;
using System.Globalization;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Pages;

public static class SortableDataTablePage
{
    public sealed record Project(string Name, string Owner, string Status, decimal Budget, DateOnly Due);

    private static readonly CultureInfo s_en = CultureInfo.GetCultureInfo("en-US");
    private static readonly string[] s_owners = ["Mina Park", "Theo Walsh", "Ana Ruiz", "Kai Moreno", "Lee Chen", "Ivy Brooks"];
    private static readonly string[] s_status = ["Active", "Paused", "Review", "Done"];

    private static ObservableCollection<Project> Projects() =>
    [
        new("Atlas redesign", "Mina Park", "Active", 42000, new DateOnly(2026, 11, 14)),
        new("Billing migration", "Theo Walsh", "Review", 18500, new DateOnly(2026, 10, 30)),
        new("Checkout v3", "Ana Ruiz", "Active", 126000, new DateOnly(2027, 1, 9)),
        new("Docs search", "Kai Moreno", "Paused", 9600, new DateOnly(2026, 12, 2)),
        new("Edge caching", "Lee Chen", "Done", 31250, new DateOnly(2026, 9, 18)),
        new("Field reports", "Ivy Brooks", "Active", 57800, new DateOnly(2026, 11, 28))
    ];

    public static Control Create()
    {
        var status = GalleryUi.Text("Click a header to sort; click rows or press Space to select.", ArcDesignTokenKind.TextSecondary);
        var projects = new SortableDataTable
        {
            ItemsSource = Projects(),
            IsSelectable = true,
            ItemNameOne = "project",
            ItemNameOther = "projects",
            Caption = "Projects"
        };
        AddProjectColumns(projects);
        projects.SortChanged += (_, e) => status.Text = e.Column is null ? "Sort cleared" : $"Sorted by {e.Column.Header}, {e.Direction}";
        projects.SelectionChanged += (_, e) => status.Text = $"{e.SelectedItems.Count} selected";

        var random = new Random(7);
        var many = new List<Project>();
        for (var i = 0; i < 5000; i++)
        {
            many.Add(new Project($"Workstream {i + 1:0000}", s_owners[random.Next(s_owners.Length)], s_status[random.Next(s_status.Length)],
                random.Next(1, 400) * 250, new DateOnly(2026, 1, 1).AddDays(random.Next(0, 540))));
        }
        var large = new SortableDataTable
        {
            ItemsSource = many,
            IsSelectable = true,
            IsSortClearable = true,
            MaxHeight = 420,
            ItemNameOne = "workstream",
            ItemNameOther = "workstreams"
        };
        AddProjectColumns(large);

        var empty = new SortableDataTable { ItemsSource = Array.Empty<Project>(), EmptyMessage = "No projects match these filters" };
        AddProjectColumns(empty);
        var loadingSwitch = new Switch { Content = "Loading" };
        loadingSwitch.IsCheckedChanged += (_, _) => empty.IsLoading = loadingSwitch.IsChecked == true;

        var plain = new SortableDataTable { ItemsSource = Projects(), SortKey = "Budget", SortDirection = DataTableSortDirection.Descending };
        plain.Columns.Add(DataTableColumn.Create<Project>("Project", p => p.Name));
        plain.Columns.Add(DataTableColumn.Create<Project>("Budget", p => p.Budget, key: "Budget"));
        plain.Columns[1].Formatter = Money;
        plain.Columns.Add(new DataTableColumn("Owner", p => ((Project)p).Owner) { IsSortable = false });

        var narrow = new SortableDataTable { ItemsSource = Projects(), IsSelectable = true, Width = 360, HorizontalAlignment = HorizontalAlignment.Left };
        AddProjectColumns(narrow);

        return GalleryUi.Column(20,
            GalleryUi.Card("Projects", "Selectable rows with a checkbox column, Shift range selection and a count line with Clear selection. Rows slide to their new places on a spring when the sort changes.",
                GalleryUi.Column(12, projects, status)),
            GalleryUi.Card("Five thousand rows", "Virtualized rows with a sticky header; sorting reorders indices, not containers. A third click clears the sort here.",
                large),
            GalleryUi.Card("Sorted from code", "SortKey and SortDirection are two-way; the Owner column is not sortable.", plain),
            GalleryUi.Card("Empty and loading", "The empty row and the loading veil.", GalleryUi.Column(12, loadingSwitch, empty)),
            GalleryUi.Card("Narrow", "Below 620px rows fold into two lines and the header becomes a strip of sort pills.", narrow));
    }

    private static void AddProjectColumns(SortableDataTable table)
    {
        table.Columns.Add(DataTableColumn.Create<Project>("Project", p => p.Name));
        table.Columns.Add(DataTableColumn.Create<Project>("Owner", p => p.Owner));
        table.Columns.Add(new DataTableColumn("Status", p => ((Project)p).Status) { CellTemplate = new FuncDataTemplate<Project>((_, _) => new StatusPill()) });
        table.Columns.Add(new DataTableColumn("Budget", p => ((Project)p).Budget) { Formatter = Money });
        table.Columns.Add(new DataTableColumn("Due", p => ((Project)p).Due) { Formatter = v => v is DateOnly d ? d.ToString("MMM d, yyyy", s_en) : "–" });
    }

    private static string Money(object? value) => value is decimal amount ? amount.ToString("C0", s_en) : "–";

    /// <summary>A status badge that follows its data context (cells are recycled between rows).</summary>
    private sealed class StatusPill : Border
    {
        private readonly TextBlock _text = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        private IDisposable? _fill;
        private IDisposable? _fore;

        public StatusPill()
        {
            Child = _text;
            Padding = new Thickness(8, 2);
            CornerRadius = new CornerRadius(999);
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Center;
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            var status = (DataContext as Project)?.Status ?? string.Empty;
            _text.Text = status;
            var (fill, fore) = status switch
            {
                "Active" => (ArcDesignTokenKind.SuccessSubtle, ArcDesignTokenKind.Success),
                "Paused" => (ArcDesignTokenKind.WarningSubtle, ArcDesignTokenKind.Warning),
                "Review" => (ArcDesignTokenKind.AccentSubtle, ArcDesignTokenKind.AccentStrong),
                _ => (ArcDesignTokenKind.SurfaceMuted, ArcDesignTokenKind.TextSecondary)
            };
            _fill?.Dispose();
            _fore?.Dispose();
            _fill = ArcTokens.Bind(this, BackgroundProperty, fill);
            _fore = ArcTokens.Bind(_text, TextBlock.ForegroundProperty, fore);
        }
    }
}
