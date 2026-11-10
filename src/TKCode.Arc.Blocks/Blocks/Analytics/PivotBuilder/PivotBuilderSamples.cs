namespace TKCode.Arc;

/// <summary>One invented sale of the sample data.</summary>
internal sealed record PivotBuilderSampleSale(string Region, string Product, string Quarter, string Channel, double Revenue, int Units);

/// <summary>The invented sales <see cref="PivotBuilder"/> pivots while it has no records of its own.</summary>
internal static class PivotBuilderSamples
{
    public static PivotBuilderField[] Fields() =>
    [
        PivotBuilderField.Create<PivotBuilderSampleSale>("region", "Region", s => s.Region),
        PivotBuilderField.Create<PivotBuilderSampleSale>("product", "Product", s => s.Product),
        PivotBuilderField.Create<PivotBuilderSampleSale>("quarter", "Quarter", s => s.Quarter),
        PivotBuilderField.Create<PivotBuilderSampleSale>("channel", "Channel", s => s.Channel),
        PivotBuilderField.Create<PivotBuilderSampleSale>("revenue", "Revenue", s => s.Revenue, isNumeric: true, format: "C0"),
        PivotBuilderField.Create<PivotBuilderSampleSale>("units", "Units", s => s.Units, isNumeric: true),
    ];

    /// <summary>A deterministic spread of sales: four regions, four products, four quarters, two channels.</summary>
    public static PivotBuilderSampleSale[] Sales()
    {
        string[] regions = ["North", "South", "East", "West"];
        (string Name, double Price)[] products = [("Starter", 29), ("Team", 79), ("Business", 199), ("Add-ons", 12)];
        string[] quarters = ["Q1", "Q2", "Q3", "Q4"];
        string[] channels = ["Online", "Partner"];
        var list = new List<PivotBuilderSampleSale>();
        var seed = 17;
        foreach (var region in regions)
        {
            foreach (var (product, price) in products)
            {
                foreach (var quarter in quarters)
                {
                    foreach (var channel in channels)
                    {
                        seed = (seed * 1103515245 + 12345) & 0x7fffffff;
                        var units = 8 + seed % 40 + (channel == "Online" ? 10 : 0) + quarter[1] - '0';
                        list.Add(new PivotBuilderSampleSale(region, product, quarter, channel, units * price, units));
                    }
                }
            }
        }
        return list.ToArray();
    }
}
