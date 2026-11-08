using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RadarChartTeamSkillsDemo : UserControl
{
    private static readonly RadarAxis[] s_axes =
    [
        new("design", "System design"),
        new("delivery", "Delivery"),
        new("debugging", "Debugging"),
        new("mentoring", "Mentoring"),
        new("writing", "Writing"),
        new("product", "Product sense")
    ];

    private static readonly RadarSeries s_maya = Candidate("maya", "Maya", 88, 72, 91, 64, 70, 58);

    private static readonly Dictionary<string, RadarSeries> s_others = new()
    {
        ["theo"] = Candidate("theo", "Theo", 62, 90, 68, 83, 55, 76),
        ["ines"] = Candidate("ines", "Ines", 75, 58, 52, 92, 88, 84),
        ["kofi"] = Candidate("kofi", "Kofi", 94, 81, 40, 47, 66, 90)
    };

    public RadarChartTeamSkillsDemo()
    {
        InitializeComponent();
        Chart.Axes = s_axes;
        Chart.Series = [s_maya, s_others["theo"]];
        // The second profile takes over the shape on screen, so picking someone else morphs it.
        Picker.SelectionChanged += (_, _) =>
        {
            if (Picker.SelectedValue is string key && s_others.TryGetValue(key, out var other))
            {
                Chart.Series = [s_maya, other];
            }
        };
    }

    private static RadarSeries Candidate(string key, string label, params double[] scores)
        => new(key, label, s_axes.Select((axis, i) => KeyValuePair.Create(axis.Key, scores[i])));
}
