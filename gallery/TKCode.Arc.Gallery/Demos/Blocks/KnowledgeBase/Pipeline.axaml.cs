using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class KnowledgeBasePipelineDemo : UserControl
{
    public KnowledgeBasePipelineDemo()
    {
        InitializeComponent();
        Knowledge.IndexHandler = KnowledgeBaseSamples.SimulatedIndexer(TimeSpan.FromMilliseconds(400), failName: "Pricing");
    }

    private void OnAdd(object? sender, RoutedEventArgs e)
    {
        Knowledge.AddFiles(
        [
            ("Onboarding checklist.md", 14_336, "Day one: laptop, badge and a buddy. Week one: shadow three support calls and read the returns policy."),
            ("Pricing notes.pdf", 402_000, "Prices are reviewed every quarter. Discounts above 15 percent need approval from the sales lead."),
            ("Holiday hours.txt", 2_048, "The support desk closes at 3 pm on 24 December and stays closed on 25 and 26 December.")
        ]);
    }

    private void OnReindexAll(object? sender, RoutedEventArgs e) => Knowledge.ReindexAll();
}
