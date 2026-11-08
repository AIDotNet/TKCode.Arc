namespace ArcStarterApp.Models;

/// <summary>A file or folder of the workspace, as the explorer shows it.</summary>
public sealed class WorkspaceNode(string name, string path, IReadOnlyList<WorkspaceNode>? children = null)
{
    public string Name { get; } = name;

    /// <summary>The path from the workspace root, with '/' separators.</summary>
    public string Path { get; } = path;

    /// <summary>The folder's entries, or null for a file.</summary>
    public IReadOnlyList<WorkspaceNode>? Children { get; } = children;

    public bool IsFolder => Children is not null;
}
