using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class UsageMeterPage
{
    public static Control Create()
    {
        var media = new UsageSegment("media", "Media", 14.2);
        var docs = new UsageSegment("docs", "Documents", 9.6);
        var storage = new UsageMeter
        {
            Label = "Workspace storage",
            Limit = 50,
            Unit = "GB",
            Segments = { media, docs, new UsageSegment("backups", "Backups", 6.1), new UsageSegment("other", "Other", 2.4) }
        };
        var random = new Random(11);
        var grow = new Button { Content = "Upload media", Variant = ButtonVariant.Secondary };
        grow.Click += (_, _) => media.Value = Math.Round(media.Value + random.Next(3, 9) + random.NextDouble(), 1);
        var clean = new Button { Content = "Clean up", Variant = ButtonVariant.Ghost };
        clean.Click += (_, _) =>
        {
            media.Value = 14.2;
            docs.Value = 9.6;
        };

        var seats = new UsageMeter
        {
            Label = "Seats",
            Limit = 20,
            Decimals = 0,
            Segments = { new UsageSegment("eng", "Engineering", 12), new UsageSegment("design", "Design", 6), new UsageSegment("sales", "Sales", 5) }
        };
        var api = new UsageMeter
        {
            Label = "API calls this month",
            Limit = 100,
            Unit = "K",
            Segments = { new UsageSegment("prod", "Production", 61.5), new UsageSegment("staging", "Staging", 29.8) }
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Storage", "Categories grow in left to right the first time the meter is seen; hover or focus a category to preview it, press to pin it (Escape clears).",
                GalleryUi.Column(16, storage, GalleryUi.Row(grow, clean))),
            GalleryUi.Card("Almost full", "At 90% of the limit the badge warns.", api),
            GalleryUi.Card("Over the limit", "The overage is struck through with a hatch beside the limit marker, so it reads without color.", seats));
    }
}
