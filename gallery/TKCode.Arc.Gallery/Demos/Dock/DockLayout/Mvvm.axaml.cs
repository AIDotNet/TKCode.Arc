using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A file the MVVM demo shows as a document.</summary>
public sealed class DockLayoutMvvmFile
{
    public DockLayoutMvvmFile(string path, string text)
    {
        Path = path;
        Text = text;
    }

    public string Path { get; }

    public string Name => System.IO.Path.GetFileName(Path);

    public string Text { get; }
}

/// <summary>Binds the layout's documents to a collection: adding opens a tab, closing a tab removes the file.</summary>
public partial class DockLayoutMvvmDemo : UserControl
{
    private readonly ObservableCollection<DockLayoutMvvmFile> _files =
    [
        new("notes/Roadmap.md", "Q3: ship the dock layout, then the floating windows."),
        new("notes/Standup.md", "Yesterday: auto hide. Today: the pane switcher.")
    ];

    private int _next = 1;

    public DockLayoutMvvmDemo()
    {
        InitializeComponent();
        Layout.DocumentsSource = _files;
        Layout.ActivePane = Layout.Workspace.FindPane(_files[0].Path);
        _files.CollectionChanged += (_, _) => UpdateStatus();
        Layout.ActivePaneChanged += (_, _) => UpdateStatus();
        UpdateStatus();
    }

    private void OnNewFile(object? sender, RoutedEventArgs e)
    {
        _files.Add(new DockLayoutMvvmFile($"notes/Untitled-{_next}.md", $"Draft {_next++}."));
    }

    private void UpdateStatus()
    {
        var active = Layout.ActivePane?.DataContext is DockLayoutMvvmFile file ? file.Name : "none";
        Status.Text = $"{_files.Count} files · active: {active}";
    }
}
