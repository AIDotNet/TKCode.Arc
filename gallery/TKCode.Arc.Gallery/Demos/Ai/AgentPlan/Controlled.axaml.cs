using Avalonia.Controls;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A plan driven by the host: the demo is the agent. It works through the approved steps, fails the second one once, and answers every event.</summary>
public partial class AgentPlanControlledDemo : UserControl
{
    private static readonly string[] s_activities =
    [
        "Read 3 files", "Edited 2 files", "Ran 4 tests"
    ];

    private readonly List<string> _entries = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(900) };
    private int _current = -1;
    private int _line;
    private bool _failedOnce;
    private bool _paused;
    private int _suggestions;

    public AgentPlanControlledDemo()
    {
        InitializeComponent();
        Reset();
        _timer.Tick += (_, _) => Tick();
    }

    private void Reset()
    {
        _timer.Stop();
        _current = -1;
        _line = 0;
        _failedOnce = false;
        _paused = false;
        _suggestions = 0;
        Plan.Steps.Clear();
        foreach (var title in new[]
                 {
                     "List the pages that link to the changelog",
                     "Create a page that reads the release notes",
                     "Redirect the old changelog address",
                     "Update the sitemap and the footer links"
                 })
        {
            Plan.Steps.Add(new AgentPlanStep(null, title));
        }
        Plan.Model = new AgentModel("Ember 1", ArcIcons.Sparkles);
        Plan.CanKeepPlanning = true;
        Plan.IsPlanning = false;
    }

    private void Write(string text)
    {
        _entries.Add(text);
        if (_entries.Count > 5)
        {
            _entries.RemoveAt(0);
        }
        Log.Text = string.Join("\n", _entries);
    }

    private void OnApproved(object? sender, AgentPlanApprovedEventArgs e)
    {
        Write($"Approved {e.Steps.Count} steps");
        foreach (var step in Plan.Steps)
        {
            step.State = AgentPlanStepState.Pending;
        }
        Plan.Status = AgentPlanStatus.Running;
        _current = -1;
        _timer.Start();
    }

    private void OnChanged(object? sender, AgentPlanChangedEventArgs e) => Write($"Changed: {e.Kind} ({e.Steps.Count} steps)");

    private void OnKeepPlanning(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Write("Keep planning requested");
        if (_suggestions >= 2)
        {
            Plan.CanKeepPlanning = false;
            return;
        }
        Plan.IsPlanning = true;
        DispatcherTimer.RunOnce(() =>
        {
            Plan.IsPlanning = false;
            Plan.StreamSuggestion(new AgentPlanStep(null, _suggestions++ == 0
                ? "Add a feed for the release notes"
                : "Check every link on the new page"));
            Plan.CanKeepPlanning = _suggestions < 2;
        }, TimeSpan.FromMilliseconds(700));
    }

    private void OnPauseChanged(object? sender, AgentPlanPauseChangedEventArgs e)
    {
        Write(e.IsPaused ? "Paused" : "Resumed");
        _paused = e.IsPaused;
        Plan.Status = e.IsPaused ? AgentPlanStatus.Paused : AgentPlanStatus.Running;
        if (e.IsPaused)
        {
            _timer.Stop();
        }
        else if (Plan.Steps.Any(s => s.State != AgentPlanStepState.Failed))
        {
            _timer.Start();
        }
    }

    private void OnStepAction(object? sender, AgentPlanStepActionEventArgs e)
    {
        Write($"{e.Kind}: {e.Step.Title}");
        if (e.Kind == AgentPlanStepActionKind.Retry)
        {
            e.Step.Error = null;
            e.Step.State = AgentPlanStepState.Active;
            _line = e.Step.Activities.Count;
        }
        else
        {
            e.Step.State = AgentPlanStepState.Skipped;
        }
        _timer.Start();
    }

    private void OnStartOver(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Write("Start over");
        Reset();
        Plan.Status = AgentPlanStatus.Draft;
    }

    private void Tick()
    {
        if (_paused)
        {
            return;
        }
        var steps = Plan.Steps;
        if (_current < 0 || steps[_current].State is AgentPlanStepState.Done or AgentPlanStepState.Skipped)
        {
            _current = steps.ToList().FindIndex(s => s.State == AgentPlanStepState.Pending);
            if (_current < 0)
            {
                _timer.Stop();
                Plan.Summary = "The changelog now has its own page, and the old address redirects to it.";
                Plan.Status = AgentPlanStatus.Done;
                return;
            }
            steps[_current].State = AgentPlanStepState.Active;
            _line = 0;
            return;
        }
        var step = steps[_current];
        if (_line < s_activities.Length)
        {
            step.Activities.Add(s_activities[_line++]);
            return;
        }
        if (_current == 1 && !_failedOnce)
        {
            _failedOnce = true;
            _timer.Stop();
            step.Error = "The page did not build: a release note has no date";
            step.State = AgentPlanStepState.Failed;
            return;
        }
        step.Duration = TimeSpan.FromSeconds(12 + _current * 7);
        step.State = AgentPlanStepState.Done;
    }
}
