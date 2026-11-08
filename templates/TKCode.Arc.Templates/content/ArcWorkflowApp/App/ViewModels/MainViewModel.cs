using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArcStarterApp.Models;
using ArcStarterApp.SampleData;
using ArcStarterApp.Services;
using TKCode.Arc;

namespace ArcStarterApp.ViewModels;

/// <summary>
/// The editor: the workflow on the canvas, the step the inspector shows, adding steps from the palette, running the
/// workflow and keeping it. The canvas edits <see cref="Graph"/> itself (moves, connections, deletions, undo); the view
/// calls <see cref="Save"/> after each edit.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IWorkflowStore _store;
    private readonly IWorkflowRunner _runner;

    [ObservableProperty]
    private WorkflowGraph _graph;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private WorkflowNode? _selected;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    private bool _isRunning;

    [ObservableProperty]
    private string _status = "Saved";

    public MainViewModel(IWorkflowStore store, IWorkflowRunner runner)
    {
        _store = store;
        _runner = runner;
        var document = store.Load() ?? SampleWorkflow.Create();
        _graph = WorkflowSerializer.ToGraph(document);
        _name = document.Name;
    }

    public IReadOnlyList<StepKind> StepKinds { get; } = Models.StepKinds.All;

    public bool HasSelection => Selected is not null;

    /// <summary>Adds a step of <paramref name="kind"/> after the selected step (connected to it) or at <paramref name="position"/>.</summary>
    [RelayCommand]
    private void AddStep(StepKind kind) => AddStep(kind, null);

    public WorkflowNode AddStep(StepKind kind, Point? position)
    {
        var step = WorkflowSerializer.CreateStep(kind);
        // One undo step for the new step and its connection.
        using (Graph.History.BeginGroup())
        {
            if (position is { } point)
            {
                step.Position = point;
            }
            else if (Selected is { } after && !double.IsNaN(after.Position.X))
            {
                step.Position = new Point(after.Position.X, after.Position.Y + 140);
            }
            Graph.Nodes.Add(step);
            if (Selected is { } source && Graph.CanConnect(source, null, step, null, out _))
            {
                Graph.Connect(source, step);
            }
        }
        Selected = step;
        Save();
        return step;
    }

    /// <summary>Writes the workflow to its store.</summary>
    public void Save()
    {
        _store.Save(WorkflowSerializer.ToDocument(Graph, Name));
        Status = $"Saved · {Graph.Nodes.Count} steps";
    }

    /// <summary>The workflow as JSON, for export.</summary>
    public string Export() => WorkflowStore.ToJson(WorkflowSerializer.ToDocument(Graph, Name));

    /// <summary>Replaces the workflow with the one in <paramref name="json"/>; false when it is not a workflow.</summary>
    public bool Import(string json)
    {
        if (WorkflowStore.FromJson(json) is not { } document)
        {
            return false;
        }
        Open(document);
        return true;
    }

    /// <summary>Starts again from the sample workflow.</summary>
    [RelayCommand]
    private void Reset() => Open(SampleWorkflow.Create());

    private bool CanRun() => !IsRunning;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync(CancellationToken cancellation)
    {
        IsRunning = true;
        Status = "Running…";
        try
        {
            await _runner.RunAsync(Graph, cancellation);
            Status = "Run finished";
        }
        catch (OperationCanceledException)
        {
            Status = "Run canceled";
        }
        finally
        {
            IsRunning = false;
        }
    }

    partial void OnNameChanged(string value) => Save();

    private void Open(WorkflowDocument document)
    {
        Selected = null;
        Graph = WorkflowSerializer.ToGraph(document);
        Name = document.Name;
        Save();
    }
}
