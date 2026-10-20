using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class WaffleChartPage
{
    private sealed record Source(string Name, double TWh);

    private static readonly WaffleCategory[] Mix2015 =
    [
        new("coal", "Coal", 9400), new("gas", "Gas", 5500), new("hydro", "Hydro", 3950),
        new("nuclear", "Nuclear", 2570), new("wind-solar", "Wind and solar", 1080), new("other", "Other", 700)
    ];

    private static readonly WaffleCategory[] Mix2023 =
    [
        new("coal", "Coal", 10430), new("gas", "Gas", 6630), new("hydro", "Hydro", 4210),
        new("nuclear", "Nuclear", 2690), new("wind-solar", "Wind and solar", 3940), new("other", "Other", 760)
    ];

    public static Control Create()
    {
        var energy = new WaffleChart { Label = "Electricity generation", Unit = "TWh", ItemsSource = Mix2023 };
        var y2015 = new Button { Content = "2015", Variant = ButtonVariant.Secondary };
        var y2023 = new Button { Content = "2023", Variant = ButtonVariant.Secondary };
        y2015.Click += (_, _) => energy.ItemsSource = Mix2015;
        y2023.Click += (_, _) => energy.ItemsSource = Mix2023;
        var pinned = GalleryUi.Text("Pinned: none", TKCode.Arc.DesignTokens.ArcDesignTokenKind.TextMuted, 13);
        energy.ActiveKeyChanged += (_, e) => pinned.Text = $"Pinned: {e.Category?.Label ?? "none"}";

        var survey = new WaffleChart
        {
            Label = "Survey answers",
            ItemsSource = new[] { new Source("Yes", 64), new Source("No", 27), new Source("Not sure", 9) },
            KeySelector = item => ((Source)item).Name,
            ValueSelector = item => ((Source)item).TWh,
            Decimals = 1,
            MaxWidth = 300,
            IsLegendVisible = false
        };
        var neutral = new WaffleChart { Label = "Browser share", AccentKey = WaffleChart.NoAccent };
        neutral.Items.Add(new WaffleCategory("chrome", "Chrome", 65.4));
        neutral.Items.Add(new WaffleCategory("safari", "Safari", 18.6));
        neutral.Items.Add(new WaffleCategory("edge", "Edge", 5.2));
        neutral.Items.Add(new WaffleCategory("firefox", "Firefox", 2.8));
        neutral.Items.Add(new WaffleCategory("samsung", "Samsung Internet", 2.6));
        neutral.Items.Add(new WaffleCategory("other", "Other", 5.4));

        var empty = new WaffleChart { Label = "No answers yet", MaxWidth = 300 };
        return GalleryUi.Column(20,
            GalleryUi.Card("Energy mix", "Every cell is one percent. Switch the year: cells stay where they can and the rest fly to their new block.",
                GalleryUi.Column(16, energy, GalleryUi.Row(y2015, y2023), pinned)),
            GalleryUi.Card("Selectors, no legend", "Any objects through KeySelector / ValueSelector; hover a cell to read it.", survey),
            GalleryUi.Card("Neutral", "AccentKey = NoAccent: series colors without an accent; neutral categories also differ in shape.", neutral),
            GalleryUi.Card("Empty", null, empty));
    }
}
