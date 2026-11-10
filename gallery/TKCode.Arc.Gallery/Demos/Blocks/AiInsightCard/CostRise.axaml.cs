using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AiInsightCardCostRiseDemo : UserControl
{
    public AiInsightCardCostRiseDemo()
    {
        InitializeComponent();
        Card.Points = [3540, 3610, 3580, 3700, 3650, 3720, 3690, 3710, 3680, 3940, 4120, 4290, 4380];
        Card.ComparisonPoints = null;
        Card.ValueFormatter = value => value.ToString("C0", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        Card.PointLabels = Enumerable.Range(0, 13)
            .Select(i => new DateOnly(2026, 10, 28).AddDays(i).ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        Card.FocusRange = new AiInsightRange(9, 12);
        Card.Causes =
        [
            new() { Title = "Image resizing on upload", Detail = "Worker hours tripled after the upload change shipped.", Share = 0.81, Range = new(9, 12) },
            new() { Title = "Larger test environment", Detail = "Two extra database replicas for load tests.", Share = 0.19, Range = new(10, 12) },
        ];
        Card.ActionInvoked += (_, e) => Card.Footnote = $"Sent to the platform team: {e.NextStep}";
    }
}
