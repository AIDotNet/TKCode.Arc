using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class FilterToolbarSearchAndTableDemo : UserControl
{
    private sealed record Issue(string Title, string Status, string Owner, string Priority);

    private static readonly Issue[] s_issues =
    [
        new("Checkout button misaligned", "Open", "Mina", "High"),
        new("Search ignores accents", "In progress", "Theo", "Medium"),
        new("Export drops last row", "Open", "Ana", "Urgent"),
        new("Avatar flickers on hover", "Closed", "Mina", "Low"),
        new("Dark mode chart contrast", "In progress", "Kai", "High"),
        new("Timezone shown twice", "Open", "Theo", "Low"),
        new("Pagination skips page 3", "Closed", "Ana", "Medium"),
        new("Toast overlaps footer", "Open", "Kai", "Medium")
    ];

    private readonly ObservableCollection<Issue> _rows = new(s_issues);

    public FilterToolbarSearchAndTableDemo()
    {
        InitializeComponent();
        Table.ItemsSource = _rows;
        Table.Columns.Add(DataTableColumn.Create<Issue>("Issue", i => i.Title));
        Table.Columns.Add(DataTableColumn.Create<Issue>("Status", i => i.Status));
        Table.Columns.Add(DataTableColumn.Create<Issue>("Owner", i => i.Owner));
        Table.Columns.Add(DataTableColumn.Create<Issue>("Priority", i => i.Priority));
        Toolbar.PropertyChanged += OnToolbarPropertyChanged;
    }

    private void OnFiltersChanged(object? sender, FilterToolbarFiltersChangedEventArgs e) => Refilter();

    private void OnToolbarPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == FilterToolbar.SearchTextProperty)
        {
            Refilter();
        }
    }

    // Filters and search narrow the rows the table shows.
    private void Refilter()
    {
        var active = Toolbar.ActiveFilters ?? [];
        var query = Toolbar.SearchText ?? string.Empty;
        var visible = s_issues
            .Where(issue => active.All(f => f.Id switch
            {
                "status" => issue.Status == f.Value,
                "owner" => issue.Owner == f.Value,
                "priority" => issue.Priority == f.Value,
                _ => true
            }) && issue.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .ToList();
        _rows.Clear();
        foreach (var issue in visible)
        {
            _rows.Add(issue);
        }
    }
}
