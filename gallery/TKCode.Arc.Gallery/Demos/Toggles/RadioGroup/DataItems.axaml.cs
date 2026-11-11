using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A region from the app's own model: no RadioOption, the rows read it through a style.</summary>
public sealed class RadioGroupDemoRegion
{
    public RadioGroupDemoRegion(string id, string name, string note, bool isAvailable = true)
    {
        Id = id;
        Name = name;
        Note = note;
        IsAvailable = isAvailable;
    }

    public string Id { get; }

    public string Name { get; }

    public string Note { get; }

    public bool IsAvailable { get; }
}

public partial class RadioGroupDataItemsDemo : UserControl
{
    public RadioGroupDataItemsDemo()
    {
        InitializeComponent();
        Regions.ItemsSource = new[]
        {
            new RadioGroupDemoRegion("fra", "Frankfurt", "Lowest latency for Europe."),
            new RadioGroupDemoRegion("iad", "Virginia", "Closest to most customers in the Americas."),
            new RadioGroupDemoRegion("sin", "Singapore", "Opens in January.", isAvailable: false)
        };
        Regions.SelectedValue = "fra";
    }
}
