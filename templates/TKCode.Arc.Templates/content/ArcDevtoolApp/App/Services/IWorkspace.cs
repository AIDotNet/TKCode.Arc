using ArcStarterApp.Models;

namespace ArcStarterApp.Services;

/// <summary>
/// The files the tool works on. SampleData/SampleWorkspace.cs keeps an invented project in memory; read a folder on
/// disk (System.IO.Directory, File.ReadAllText ...) or a remote repository instead.
/// </summary>
public interface IWorkspace
{
    string Name { get; }

    /// <summary>The top-level files and folders.</summary>
    IReadOnlyList<WorkspaceNode> Root { get; }

    /// <summary>Whether a file exists at <paramref name="path"/>.</summary>
    bool Exists(string path);

    string Read(string path);

    void Write(string path, string text);
}

/// <summary>Builds the workspace. SampleData/SampleBuildService.cs prints a made-up build; run your compiler, linter or test runner instead.</summary>
public interface IBuildService
{
    /// <summary>The build's output line by line, with the diagnostics among them.</summary>
    IAsyncEnumerable<BuildEvent> BuildAsync(IWorkspace workspace, CancellationToken cancellation);
}
