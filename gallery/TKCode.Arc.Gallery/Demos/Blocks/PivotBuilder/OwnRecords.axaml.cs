using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class PivotBuilderOwnRecordsDemo : UserControl
{
    public PivotBuilderOwnRecordsDemo()
    {
        InitializeComponent();
        var tickets = new[]
        {
            new Ticket("Billing", "Jan", 3.5), new Ticket("Billing", "Feb", 2.0), new Ticket("Accounts", "Jan", 1.5),
            new Ticket("Accounts", "Feb", 4.0), new Ticket("Billing", "Feb", 6.5), new Ticket("Setup", "Jan", 2.5),
            new Ticket("Setup", "Feb", 1.0), new Ticket("Accounts", "Feb", 3.0), new Ticket("Setup", "Feb", 5.5),
        };
        Pivot.Fields.AddRange(
        [
            PivotBuilderField.Create<Ticket>("team", "Team", t => t.Team),
            PivotBuilderField.Create<Ticket>("month", "Month", t => t.Month),
            PivotBuilderField.Create<Ticket>("hours", "Hours to solve", t => t.Hours, isNumeric: true, format: "N1"),
        ]);
        Pivot.RowFields.Add("team");
        Pivot.ColumnFields.Add("month");
        Pivot.ValueFields.Add(new PivotBuilderValue("hours", PivotBuilderAggregation.Count));
        Pivot.ValueFields.Add(new PivotBuilderValue("hours", PivotBuilderAggregation.Average));
        Pivot.ItemsSource = tickets;
    }

    private sealed record Ticket(string Team, string Month, double Hours);
}
