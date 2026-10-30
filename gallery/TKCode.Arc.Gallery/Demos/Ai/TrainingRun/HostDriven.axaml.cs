using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A run driven by app code: a timer stands in for the job API and the card's buttons are answered here.</summary>
public partial class TrainingRunHostDrivenDemo : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private int _ticks;
    private int _step;

    public TrainingRunHostDrivenDemo()
    {
        InitializeComponent();
        Run.BaseModel = new AgentModel("Base model");
        Run.Details = ["LoRA r16", "4× H200"];
        Run.Dataset = new TrainingRunDataset("northwind/receipts-2026", 9312);
        Run.PauseRequested += (_, _) => Answer(TrainingRunStatus.Paused, "Paused by you");
        Run.ResumeRequested += (_, _) => Answer(TrainingRunStatus.Running, "Resumed");
        Run.CancelRequested += (_, _) => Answer(TrainingRunStatus.Cancelled, "Cancelled");
        Run.RestartRequested += OnRestart;
        Run.DeployHandler = async token =>
        {
            await Task.Delay(1500, token);
            return "https://inference.example.com/v1/receipts-v1";
        };
        _timer.Tick += OnTick;
        AttachedToVisualTree += (_, _) => _timer.Start();
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    private void Answer(TrainingRunStatus status, string what)
    {
        Run.Apply(new TrainingRunProgress
        {
            Status = status,
            Event = new TrainingRunEvent(Guid.NewGuid().ToString("N"), TrainingRunEventKind.Info, what, _step),
        });
        Log.Text = what + " at step " + _step + ".";
    }

    private void OnRestart(object? sender, RoutedEventArgs e)
    {
        _step = 0;
        _ticks = 0;
        Run.Loss.Clear();
        Run.Events.Clear();
        Run.Step = 0;
        Run.Elapsed = null;
        Run.LearningRate = null;
        Run.Eta = TimeSpan.FromMinutes(6.5);
        Run.Status = TrainingRunStatus.Queued;
        Log.Text = "Queued again.";
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _ticks++;
        switch (Run.Status)
        {
            case TrainingRunStatus.Queued when _ticks > 4:
                Run.Apply(new TrainingRunProgress { Status = TrainingRunStatus.Running, Elapsed = TimeSpan.Zero, Activity = "Loading the base model" });
                Log.Text = "The job started.";
                break;
            case TrainingRunStatus.Running:
                Burst();
                break;
        }
    }

    /// <summary>Five samples at once, as a job API that reports every two seconds would send them.</summary>
    private void Burst()
    {
        if (_step == 0)
        {
            Run.Events.Add(new TrainingRunEvent("start", TrainingRunEventKind.Start, "Training started on 4× H200", 0));
        }
        for (var i = 0; i < 5 && _step < Run.TotalSteps; i++)
        {
            _step += 4;
            var train = 0.4 + 1.7 * Math.Exp(-_step / 140.0) + 0.04 * Math.Sin(_step * 1.7);
            double? validation = _step % 60 == 0 ? Math.Round(0.46 + 1.62 * Math.Exp(-_step / 150.0), 4) : null;
            Run.Apply(new TrainingRunProgress { Loss = new TrainingRunLossPoint(_step, Math.Round(train, 4), validation) });
            if (_step % 300 == 0 && _step < Run.TotalSteps)
            {
                Run.Events.Add(new TrainingRunEvent("epoch" + _step, TrainingRunEventKind.Epoch, "Epoch 1 of 2 complete", _step));
            }
            else if (_step % 120 == 0)
            {
                Run.Events.Add(new TrainingRunEvent("ckpt" + _step, TrainingRunEventKind.Checkpoint, "Checkpoint saved", _step));
            }
        }
        Run.Apply(new TrainingRunProgress
        {
            Step = _step,
            LearningRate = 1e-4 * (1 - _step / (double)Run.TotalSteps) + 1e-5,
            Elapsed = TimeSpan.FromSeconds(_step * 0.6),
            Eta = TimeSpan.FromSeconds((Run.TotalSteps - _step) * 0.6),
        });
        if (_step >= Run.TotalSteps)
        {
            Run.Apply(new TrainingRunProgress
            {
                Status = TrainingRunStatus.Completed,
                Event = new TrainingRunEvent("done", TrainingRunEventKind.Done, "Training complete", _step),
            });
            Log.Text = "The job finished. Deploy is simulated.";
        }
    }
}
