using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentStatusHostDrivenDemo : UserControl
{
    private readonly List<string> _entries = new();

    public AgentStatusHostDrivenDemo()
    {
        InitializeComponent();
    }

    private void Write(string text)
    {
        _entries.Add(text);
        if (_entries.Count > 4)
        {
            _entries.RemoveAt(0);
        }
        Log.Text = string.Join("  ·  ", _entries);
    }

    private void OnStart(object? sender, RoutedEventArgs e)
    {
        Pill.Steps.Clear();
        Pill.Steps.Add(new AgentStatusStep("plan", "Drafted 3 sub-questions", AgentStatusStepState.Done, ArcIcons.ListChecks) { Duration = TimeSpan.FromMilliseconds(1400) });
        Pill.Steps.Add(new AgentStatusStep("open", "Opening pages", AgentStatusStepState.Active, ArcIcons.BookOpen) { StartedAt = DateTimeOffset.UtcNow });
        Pill.Steps.Add(new AgentStatusStep("write", "Write the summary", AgentStatusStepState.Pending));
        Pill.StartedAt = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(2);
        Pill.Activity = new AgentStatusActivity("Opening pages", ArcIcons.BookOpen, 3, 9);
        Pill.Status = AgentStatusRunState.Working;
        Write("Run started");
    }

    private void OnAsk(object? sender, RoutedEventArgs e)
    {
        Pill.Activity = new AgentStatusActivity("Needs your approval to open 2 paywalled pages", ArcIcons.BookOpen);
        Pill.Status = AgentStatusRunState.NeedsInput;
        Write("Waiting for approval");
    }

    private void OnFail(object? sender, RoutedEventArgs e)
    {
        Pill.Activity = new AgentStatusActivity("Fetch failed · 2 pages did not load");
        if (Pill.Steps.Count > 1)
        {
            Pill.Steps[1].State = AgentStatusStepState.Failed;
            Pill.Steps[1].Label = "2 pages did not load";
        }
        Pill.Status = AgentStatusRunState.Failed;
        Write("Run failed");
    }

    private void OnFinish(object? sender, RoutedEventArgs e)
    {
        foreach (var step in Pill.Steps)
        {
            step.State = AgentStatusStepState.Done;
            step.Duration ??= TimeSpan.FromMilliseconds(1800);
        }
        Pill.Activity = new AgentStatusActivity("8 pages cited");
        Pill.Status = AgentStatusRunState.Done;
        Write("Run finished");
    }

    private void Working(string activity)
    {
        Pill.Activity = new AgentStatusActivity(activity, ArcIcons.BookOpen, 4, 9);
        Pill.Status = AgentStatusRunState.Working;
        if (Pill.Steps.Count > 1)
        {
            Pill.Steps[1].State = AgentStatusStepState.Active;
            Pill.Steps[1].Label = "Opening pages";
        }
    }

    private void OnPause(object? sender, AgentStatusActionEventArgs e)
    {
        Pill.Status = AgentStatusRunState.Paused;
        Write("Pause requested");
    }

    private void OnResume(object? sender, AgentStatusActionEventArgs e)
    {
        Working("Opening pages");
        Write("Resume requested");
    }

    private void OnStop(object? sender, AgentStatusActionEventArgs e)
    {
        Pill.Status = AgentStatusRunState.Stopped;
        Write("Stop requested");
    }

    private void OnApprove(object? sender, AgentStatusActionEventArgs e)
    {
        Working("Opening pages");
        Write("Approved");
    }

    private void OnDeny(object? sender, AgentStatusActionEventArgs e)
    {
        Pill.Activity = new AgentStatusActivity("6 pages cited, 2 skipped");
        Pill.Status = AgentStatusRunState.Done;
        Write("Denied");
    }

    private void OnRetry(object? sender, AgentStatusActionEventArgs e)
    {
        Working("Opening pages");
        Write("Retry requested");
    }

    private void OnOpenRun(object? sender, AgentStatusActionEventArgs e) => Write("Open run requested");

    private void OnExpandedChanged(object? sender, AgentStatusExpandedChangedEventArgs e) => Write(e.IsExpanded ? "Panel opened" : "Panel closed");
}
