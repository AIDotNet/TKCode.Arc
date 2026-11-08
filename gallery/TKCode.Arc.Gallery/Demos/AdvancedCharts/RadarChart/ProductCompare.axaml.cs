using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RadarChartProductCompareDemo : UserControl
{
    public RadarChartProductCompareDemo()
    {
        InitializeComponent();
        // Every axis has its own scale and its own unit in the tooltip.
        Chart.Axes =
        [
            new RadarAxis("battery", "Battery life", 30),
            new RadarAxis("storage", "Storage", 256),
            new RadarAxis("screen", "Screen sharpness", 300),
            new RadarAxis("pen", "Pen response", 100),
            new RadarAxis("value", "Value for money", 10)
        ];
        Chart.Series =
        [
            Tablet("fernpad", "Fernpad 2", 26, 64, 226, 82, 8.1),
            Tablet("quillbox", "Quillbox Air", 14, 128, 264, 94, 6.4),
            Tablet("marlo", "Marlo Note", 21, 256, 187, 68, 7.2)
        ];
        Chart.ValueFormatter = (value, axis) => axis.Key switch
        {
            "battery" => $"{value:0} h",
            "storage" => $"{value:0} GB",
            "screen" => $"{value:0} ppi",
            "pen" => $"{value:0} / 100",
            _ => $"{value:0.0} / 10"
        };
    }

    private static RadarSeries Tablet(string key, string label, double battery, double storage, double screen, double pen, double value)
        => new(key, label, new Dictionary<string, double>
        {
            ["battery"] = battery,
            ["storage"] = storage,
            ["screen"] = screen,
            ["pen"] = pen,
            ["value"] = value
        });
}
