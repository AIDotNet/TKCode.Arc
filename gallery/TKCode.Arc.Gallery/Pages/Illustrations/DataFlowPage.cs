using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc.Gallery.Pages;

public static class DataFlowPage
{
    public static Avalonia.Controls.Control Create()
    {
        var flow = new DataFlow();
        var status = GalleryUi.Text("Step 1 of 3", ArcDesignTokenKind.TextMuted, 13);
        flow.StepChanged += (_, e) => status.Text = $"Step {e.Step + 1} of {flow.StepCount}";

        var custom = new DataFlow
        {
            Sources = [new DataFlowSource("Checkout", "orders"), new DataFlowSource("CRM", "contacts")],
            EventsPerSecond = 8,
            FilterRatio = 0.3,
            Labels = new DataFlowLabels { Warehouse = "Lakehouse", WarehouseDetail = "parquet", Realtime = "Alerts", RealtimeDetail = "on-call" }
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Event pipeline",
                "Events leave three sources, queue up in the stream, are validated in batches (some are filtered out, fading red) and land in the warehouse; every third also updates the dashboards. Hover a node to light up its connections.",
                GalleryUi.Column(12, flow, GalleryUi.Row(IllustrationSamples.SpeedRow(flow), status))),
            GalleryUi.Card("Your own sources and words",
                "Sources, EventsPerSecond, FilterRatio and Labels: two sources at 8 events/s with 30% filtered.",
                custom),
            GalleryUi.Card("Reduced motion",
                "Under reduced motion (or Off) the illustration is a still poster of its steady state and the caption sums up the story.",
                IllustrationSamples.Reduced(new DataFlow())));
    }
}
