using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>An enrich column on the app's own rows with its own delegate; settled cells are logged as an app would save them.</summary>
public partial class EnrichColumnYourDataDemo : UserControl
{
    private readonly List<string> _entries = new();

    public EnrichColumnYourDataDemo()
    {
        InitializeComponent();
        Table.Column = new EnrichColumnDefinition("Industry", "Classify the company's industry in two or three words");
        Table.Rows =
        [
            new EnrichColumnRow("1", "Maple Street Bakery", "maplestreet.example"),
            new EnrichColumnRow("2", "Orbit Freightworks", "orbitfreight.example"),
            new EnrichColumnRow("3", "Willow Dental Group", "willowdental.example"),
            new EnrichColumnRow("4", "Copperline Studio", "copperline.example"),
            new EnrichColumnRow("5", "Northgate Robotics", "northgate.example"),
        ];
        Table.Model = new EnrichColumnModel(AiModelCatalog.Gpt6Luna.Name, "classifier");
        Table.Enrich = EnrichAsync;
        Table.CellChanged += (_, e) =>
        {
            if (e.Cell.Status is EnrichColumnCellStatus.Done or EnrichColumnCellStatus.Failed)
            {
                Add($"{e.Row.Name}: {(e.Cell.HasValue ? e.Cell.Value : "failed, " + e.Cell.Error)}");
            }
        };
        Table.RunCompleted += (_, e) => Add(e.WasStopped ? "Run stopped" : $"Run finished: {e.Cells.Count} cells");
    }

    // Stands in for your service: honor the token, say what you are doing, throw to fail the cell.
    private static async Task<EnrichColumnResult> EnrichAsync(EnrichColumnRow row, EnrichColumnContext context)
    {
        context.SetActivity("Reading " + row.Domain);
        await Task.Delay(900 + row.Name.Length * 40, context.CancellationToken);
        if (row.Id == "4" && context.Attempt == 1)
        {
            throw new InvalidOperationException("The site did not respond");
        }
        var industry = row.Id switch
        {
            "1" => "Food and beverage",
            "2" => "Logistics",
            "3" => "Healthcare",
            "4" => "Design services",
            _ => "Industrial automation",
        };
        return new EnrichColumnResult(industry, new EnrichColumnSource(row.Domain!, "https://" + row.Domain));
    }

    private void Add(string entry)
    {
        _entries.Add(entry);
        if (_entries.Count > 4)
        {
            _entries.RemoveAt(0);
        }
        Log.Text = string.Join("\n", _entries);
    }
}
