using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ActivityTerrainLiveDataDemo : UserControl
{
    private uint _week = 1;

    public ActivityTerrainLiveDataDemo()
    {
        InitializeComponent();
        Terrain.DayNames = ["Montag", "Dienstag", "Mittwoch", "Donnerstag", "Freitag", "Samstag", "Sonntag"];
        Terrain.DayAbbreviations = ["Mo", "Di", "Mi", "Do", "Fr", "Sa", "So"];
        var chats = new ActivityTerrainMetric { Key = "chats", Label = "Chats", Unit = "chats", Tone = ActivityTerrainTone.Accent };
        var tickets = new ActivityTerrainMetric { Key = "tickets", Label = "Tickets", Unit = "tickets", Tone = ActivityTerrainTone.Neutral };
        Terrain.Metrics.Add(chats);
        Terrain.Metrics.Add(tickets);
        LoadWeek(chats, tickets);
        NextWeek.Click += (_, _) =>
        {
            _week++;
            LoadWeek(chats, tickets);
        };
        FlyBusiest.Click += (_, _) =>
        {
            if (Terrain.FocusedCell is null)
            {
                Terrain.FlyTo(BusiestHour(Terrain.SelectedMetric));
            }
            else
            {
                Terrain.ShowWeek();
            }
        };
        Terrain.CellHovered += (_, e) => Status.Text = e.Cell is { } cell
            ? $"Highlighted: {Terrain.DayNames![cell.Day]} {cell.Hour:00}:00, {e.Value:N0}"
            : "Hover a bar or use the arrow keys.";
        Terrain.CellFocused += (_, e) =>
        {
            if (e.Cell is null)
            {
                Status.Text = "Whole week.";
            }
        };
        Status.Text = "Hover a bar or use the arrow keys.";
    }

    private void LoadWeek(ActivityTerrainMetric chats, ActivityTerrainMetric tickets)
    {
        // Your data source goes here: 168 values per metric, Monday 00:00 first.
        var thisWeek = ActivityTerrainDemoData.Visits(1000 + _week);
        var lastWeek = ActivityTerrainDemoData.Visits(1000 + _week - 1);
        chats.Values = thisWeek.Select(v => Math.Round(v / 40)).ToArray();
        chats.PreviousValues = lastWeek.Select(v => Math.Round(v / 40)).ToArray();
        tickets.Values = thisWeek.Select((v, i) => Math.Round(v / 90 + (i % 24 == 9 ? 12 : 0))).ToArray();
        tickets.PreviousValues = lastWeek.Select(v => Math.Round(v / 90)).ToArray();
    }

    private static ActivityTerrainCell BusiestHour(ActivityTerrainMetric? metric)
    {
        var values = metric?.Values ?? [];
        var best = 0;
        for (var i = 1; i < values.Count; i++)
        {
            if (values[i] > values[best])
            {
                best = i;
            }
        }
        return ActivityTerrainCell.FromIndex(best);
    }
}
