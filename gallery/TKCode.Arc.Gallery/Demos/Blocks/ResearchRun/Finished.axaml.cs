using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ResearchRunFinishedDemo : UserControl
{
    public ResearchRunFinishedDemo()
    {
        InitializeComponent();
        var sources = ResearchRunSamples.Sources();
        foreach (var source in sources)
        {
            source.Status = source.Index == 6 ? ResearchRunSourceStatus.Skipped : ResearchRunSourceStatus.Read;
            source.Progress = 1;
        }
        Run.Plan = ResearchRunSamples.Plan();
        Run.CompletedPlanSteps = 4;
        Run.Sources = sources;
        Run.Notes =
        [
            new ResearchRunNote("Sealing the house first cut electricity for heat by about a quarter.", [1, 4]),
            new ResearchRunNote("Larger radiators allowed a 45 °C flow temperature.", [3])
        ];
        Run.Report = ResearchRunSamples.Report;
        Log.Text = "Press a citation to see the event.";
        Run.SourceInvoked += (_, e) =>
        {
            // Handled: the demo does not open the made-up address.
            e.Handled = true;
            Log.Text = $"SourceInvoked: [{e.ResearchSource.Index}] {e.ResearchSource.Title} ({e.ResearchSource.Domain})";
        };
    }
}
