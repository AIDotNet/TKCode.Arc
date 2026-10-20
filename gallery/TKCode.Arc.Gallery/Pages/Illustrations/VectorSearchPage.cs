using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class VectorSearchPage
{
    public static Control Create()
    {
        var search = new VectorSearch();

        var ask = new VectorSearch { IsPlaying = true };
        var field = new SearchField { Placeholder = "Ask the help centre", Width = 360, HorizontalAlignment = HorizontalAlignment.Left };
        var go = new Button { Content = "Search", Variant = ButtonVariant.Primary, Size = ControlSize.Small };
        var answers = GalleryUi.Text("Try “change my password” or “invoice for last month”.", ArcDesignTokenKind.TextMuted, 13);
        void Run()
        {
            if (!string.IsNullOrWhiteSpace(field.Text))
            {
                ask.Search(field.Text);
            }
        }
        go.Click += (_, _) => Run();
        field.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Run();
                e.Handled = true;
            }
        };
        ask.ResultsChanged += (_, _) =>
            answers.Text = string.Join("  ·  ", ask.Results.Select(r => $"{r.Rank}. {r.Document.Title} ({r.Score:0.00})"));

        var recipes = new VectorSearch
        {
            TopK = 4,
            Documents =
            [
                new VectorSearchDocument("Overnight oats with berries", "Breakfast", "oats morning quick"),
                new VectorSearchDocument("Fluffy buttermilk pancakes", "Breakfast", "pancakes morning griddle"),
                new VectorSearchDocument("Shakshuka with feta", "Breakfast", "eggs tomato morning"),
                new VectorSearchDocument("Avocado toast three ways", "Breakfast", "toast avocado morning quick"),
                new VectorSearchDocument("Weeknight chicken curry", "Dinner", "chicken curry rice spice"),
                new VectorSearchDocument("One-pan lemon salmon", "Dinner", "fish salmon oven quick"),
                new VectorSearchDocument("Mushroom risotto", "Dinner", "rice mushroom parmesan"),
                new VectorSearchDocument("Slow-cooker beef chili", "Dinner", "beef beans spice"),
                new VectorSearchDocument("Dark chocolate mousse", "Dessert", "chocolate cream sweet"),
                new VectorSearchDocument("Lemon tart", "Dessert", "lemon pastry sweet"),
                new VectorSearchDocument("Banana bread", "Dessert", "banana baking sweet"),
                new VectorSearchDocument("No-churn vanilla ice cream", "Dessert", "vanilla cream frozen sweet")
            ],
            Queries = ["quick salmon dinner", "something sweet with chocolate", "eggs for breakfast"]
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Help-centre search",
                "A question is typed, embedded, dropped among 32 article vectors and answered with its nearest neighbours by cosine similarity. The search is real: hashed word and trigram embeddings, cosine ranking and a map laid out from the same distances.",
                GalleryUi.Column(12, search, IllustrationSamples.SpeedRow(search))),
            GalleryUi.Card("Ask your own question", "Search(query) shows any question now; Results and ResultsChanged give the ranked answers.",
                GalleryUi.Column(12, GalleryUi.Row(field, go), ask, answers)),
            GalleryUi.Card("Your documents", "Documents, Queries and TopK = 4: a small recipe index in three topics.",
                recipes),
            GalleryUi.Card("Reduced motion", "The answers to the first question as a still frame.",
                IllustrationSamples.Reduced(new VectorSearch())));
    }
}
