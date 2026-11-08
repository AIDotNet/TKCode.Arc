using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArcStarterApp.Models;
using ArcStarterApp.Services;

namespace ArcStarterApp.ViewModels;

/// <summary>
/// The tool: the workspace's files, the documents opened from them, the file the Properties pane shows, and the build
/// with its output and problems. Views/MainView.axaml.cs turns documents into editor tabs of the dock layout.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IBuildService _build;
    private readonly Dictionary<string, EditorDocument> _documents = new();

    [ObservableProperty]
    private EditorDocument? _selected;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    private bool _isBuilding;

    [ObservableProperty]
    private string _status = "Ready";

    public MainViewModel(IWorkspace workspace, IBuildService build)
    {
        Workspace = workspace;
        _build = build;
    }

    public IWorkspace Workspace { get; }

    /// <summary>The build's output, one line per entry.</summary>
    public ObservableCollection<string> Output { get; } = new();

    /// <summary>The diagnostics of the last build.</summary>
    public ObservableCollection<BuildProblem> Problems { get; } = new();

    /// <summary>The document of <paramref name="path"/>, read from the workspace the first time (one per file).</summary>
    public EditorDocument Document(string path)
    {
        if (!_documents.TryGetValue(path, out var document))
        {
            document = new EditorDocument(path, Workspace.Read(path));
            _documents[path] = document;
        }
        return document;
    }

    /// <summary>Writes <paramref name="document"/> to the workspace.</summary>
    public void Save(EditorDocument document)
    {
        Workspace.Write(document.Path, document.Text);
        document.MarkSaved();
        Status = $"Saved {document.Name}";
    }

    [RelayCommand]
    private void SaveAll()
    {
        foreach (var document in _documents.Values.Where(d => d.IsModified))
        {
            Save(document);
        }
    }

    private bool CanRun() => !IsBuilding;

    /// <summary>Saves the changed files and builds, streaming the output into <see cref="Output"/> and <see cref="Problems"/>.</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync(CancellationToken cancellation)
    {
        SaveAll();
        IsBuilding = true;
        Status = "Building…";
        Output.Clear();
        Problems.Clear();
        try
        {
            await foreach (var step in _build.BuildAsync(Workspace, cancellation))
            {
                Output.Add(step.Line);
                if (step.Problem is { } problem)
                {
                    Problems.Add(problem);
                }
            }
            Status = Problems.Count == 0 ? "Build succeeded" : $"Build succeeded, {Problems.Count} warning(s)";
        }
        catch (OperationCanceledException)
        {
            Output.Add("Build canceled.");
            Status = "Build canceled";
        }
        finally
        {
            IsBuilding = false;
        }
    }
}
