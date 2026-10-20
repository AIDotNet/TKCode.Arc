using System.Collections.ObjectModel;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class FilterToolbarPage
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

    private static IEnumerable<FilterField> Fields()
    {
        var status = new FilterField { Id = "status", Label = "Status", Icon = ArcIcons.CircleDot };
        foreach (var value in new[] { "Open", "In progress", "Closed" })
        {
            status.Options.Add(new FilterOption(value, hint: s_issues.Count(i => i.Status == value).ToString()));
        }
        var owner = new FilterField("owner", "Owner", "Mina", "Theo", "Ana", "Kai") { Icon = ArcIcons.User };
        var priority = new FilterField("priority", "Priority", "Urgent", "High", "Medium", "Low") { Icon = ArcIcons.Flag };
        return [status, owner, priority];
    }

    public static Control Create()
    {
        var log = GalleryUi.Text("Remove a chip, clear all, or add a filter: pick a field, then a value.", ArcDesignTokenKind.TextSecondary);
        var filters = new ObservableCollection<FilterChip>
        {
            new("status", "Status", "Open"),
            new("owner", "Owner", "Mina")
        };
        var basic = new FilterToolbar { ActiveFilters = filters };
        foreach (var field in Fields())
        {
            basic.Fields.Add(field);
        }
        basic.FiltersChanged += (_, e) => log.Text = $"{e.Kind}: {e.Filter?.Text ?? "all"} · {e.ActiveFilters.Count} active";

        // A toolbar that filters a table, with search.
        var rows = new ObservableCollection<Issue>(s_issues);
        var table = new SortableDataTable { ItemsSource = rows, ItemNameOne = "issue", ItemNameOther = "issues", MaxHeight = 360 };
        table.Columns.Add(DataTableColumn.Create<Issue>("Issue", i => i.Title));
        table.Columns.Add(DataTableColumn.Create<Issue>("Status", i => i.Status));
        table.Columns.Add(DataTableColumn.Create<Issue>("Owner", i => i.Owner));
        table.Columns.Add(DataTableColumn.Create<Issue>("Priority", i => i.Priority));
        var linked = new FilterToolbar { IsSearchVisible = true, SearchPlaceholder = "Search issues" };
        foreach (var field in Fields())
        {
            linked.Fields.Add(field);
        }
        void Refilter()
        {
            var active = linked.ActiveFilters ?? [];
            var query = linked.SearchText ?? string.Empty;
            var visible = s_issues.Where(issue => active.All(f => f.Id switch
                                  {
                                      "status" => issue.Status == f.Value,
                                      "owner" => issue.Owner == f.Value,
                                      "priority" => issue.Priority == f.Value,
                                      _ => true
                                  }) && issue.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                                  .ToList();
            rows.Clear();
            foreach (var issue in visible)
            {
                rows.Add(issue);
            }
            table.EmptyMessage = "No issues match these filters";
        }
        linked.FiltersChanged += (_, _) => Refilter();
        linked.PropertyChanged += (_, e) =>
        {
            if (e.Property == FilterToolbar.SearchTextProperty)
            {
                Refilter();
            }
        };

        var empty = new FilterToolbar { MenuAlignment = FilterMenuAlignment.Start };
        foreach (var field in Fields())
        {
            empty.Fields.Add(field);
        }

        var narrow = new FilterToolbar
        {
            Width = 420,
            HorizontalAlignment = HorizontalAlignment.Left,
            ActiveFilters = new ObservableCollection<FilterChip> { new("status", "Status", "In progress"), new("priority", "Priority", "High") }
        };
        foreach (var field in Fields())
        {
            narrow.Fields.Add(field);
        }

        var picked = GalleryUi.Text("Nothing picked yet", ArcDesignTokenKind.TextSecondary);
        var menu = new FilterMenu { Fields = Fields().ToList(), HorizontalAlignment = HorizontalAlignment.Left, Alignment = FilterMenuAlignment.Start };
        menu.FilterSelected += (_, e) => picked.Text = $"Picked {e.Filter.Text}";

        return GalleryUi.Column(20,
            GalleryUi.Card("Filters", "Chips open and close their own slots so neighbors slide; a changed value morphs in place. Clear all shows while any filter is applied.",
                GalleryUi.Column(14, basic, log)),
            GalleryUi.Card("Search and a table", "The toolbar drives a SortableDataTable: filters and search narrow the rows.",
                GalleryUi.Column(14, linked, table)),
            GalleryUi.Card("No filters", "The empty note sits at the leading edge; the menu aligns with the trigger's start edge.", empty),
            GalleryUi.Card("Narrow", "Below 520px the toolbar stacks: chips first, then the actions aligned to the end.", narrow),
            GalleryUi.Card("Filter menu on its own", "FilterMenu is the Add filter trigger and its two-step panel. ↓ on the trigger opens on the first field; → enters a field, ← goes back.",
                GalleryUi.Column(14, menu, picked)));
    }
}
