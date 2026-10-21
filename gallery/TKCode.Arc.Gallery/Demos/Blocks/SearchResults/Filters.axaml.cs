using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SearchResultsFiltersDemo : UserControl
{
    public SearchResultsFiltersDemo()
    {
        InitializeComponent();
        // Filters read Kind or an entry of Attributes; the date comes from Date.
        var topics = new Dictionary<string, string>
        {
            ["Harbour entry checklist"] = "Navigation",
            ["Harbour lights and marks"] = "Navigation",
            ["Storm shelter in the harbour"] = "Safety",
            ["Winter lay-up and harbour fees"] = "Maintenance",
            ["Ines Duarte"] = "Safety",
            ["Marit Haugen"] = "Maintenance"
        };
        var day = new DateTimeOffset(2026, 2, 3, 0, 0, 0, TimeSpan.Zero);
        foreach (var item in Search.Items)
        {
            item.Attributes["topic"] = topics[item.Title];
            if (item.Kind != "People")
            {
                item.Date = day;
                day = day.AddDays(47);
            }
        }
        Search.Refresh();
    }
}
