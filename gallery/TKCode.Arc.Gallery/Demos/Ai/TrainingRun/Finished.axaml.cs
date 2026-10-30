using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A completed run loaded from history, with its own results and a deploy handler.</summary>
public partial class TrainingRunFinishedDemo : UserControl
{
    public TrainingRunFinishedDemo()
    {
        InitializeComponent();
        Run.BaseModel = new AgentModel("Base model");
        Run.Details = ["LoRA r8", "2× H200"];
        Run.Dataset = new TrainingRunDataset("fabrikam/ticket-triage", 12480);
        for (var step = 20; step <= 1200; step += 20)
        {
            var train = 0.31 + 1.6 * Math.Exp(-step / 260.0) + 0.02 * Math.Sin(step / 37.0);
            double? validation = step % 100 == 0 ? Math.Round(0.36 + 1.55 * Math.Exp(-step / 280.0), 4) : null;
            Run.Loss.Add(new TrainingRunLossPoint(step, Math.Round(train, 4), validation));
        }
        Run.Events.Add(new TrainingRunEvent("1", TrainingRunEventKind.Epoch, "Epoch 1 of 2 complete", 600));
        Run.Events.Add(new TrainingRunEvent("2", TrainingRunEventKind.Eval, "Eval · accuracy 91.4% (sample)", 1200));
        Run.Events.Add(new TrainingRunEvent("3", TrainingRunEventKind.Done, "Training complete", 1200));
        Run.Metrics =
        [
            new TrainingRunMetric("Validation loss", "0.371", "best at step 1,200"),
            new TrainingRunMetric("Accuracy", "91.4%", "+6.2 pts vs base (sample)"),
            new TrainingRunMetric("Steps", "1,200", "2 epochs"),
            new TrainingRunMetric("Duration", "9m 12s"),
        ];
        Run.DeployHandler = async token =>
        {
            await Task.Delay(1200, token);
            return "https://inference.example.com/v1/triage-v2";
        };
    }
}
