using System.Text.Json.Nodes;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class JsonViewerLongArraysDemo : UserControl
{
    public JsonViewerLongArraysDemo()
    {
        InitializeComponent();
        var items = new JsonArray();
        for (var i = 0; i < 1200; i++)
        {
            // JsonNode overload: the generic Add<T> serializes through reflection (not AOT safe).
            items.Add((JsonNode)new JsonObject
            {
                ["id"] = i + 1,
                ["sku"] = $"SKU-{i + 1:0000}",
                ["inStock"] = i % 3 != 0,
                ["price"] = Math.Round(4.5 + i * 0.25, 2)
            });
        }
        Viewer.Value = new JsonObject { ["count"] = 1200, ["items"] = items };
    }
}
