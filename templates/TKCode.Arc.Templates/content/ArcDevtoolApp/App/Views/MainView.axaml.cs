using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ArcStarterApp.Models;
using ArcStarterApp.Settings;
using ArcStarterApp.ViewModels;
using TKCode.Arc;

namespace ArcStarterApp.Views;

/// <summary>
/// The app's content: the dock layout with the explorer, the editor tabs, the build output and the properties. The main
/// window shows it on the desktop. An editor tab is a <see cref="DockPane"/> with the id <c>doc:&lt;path&gt;</c>.
/// </summary>
public partial class MainView : UserControl
{
    private const string DocumentPrefix = "doc:";

    // The layout as MainView.axaml declares it, for "Reset layout".
    private readonly string _declaredLayout;
    private MainViewModel? _viewModel;

    public MainView()
    {
        InitializeComponent();
        _declaredLayout = Dock.SaveLayout();
        Explorer.ChildrenSelector = node => ((WorkspaceNode)node).Children;
        Explorer.HeaderSelector = node => ((WorkspaceNode)node).Name;
        Explorer.IconSelector = node => ((WorkspaceNode)node).IsFolder ? ArcIcons.Folder : ArcIcons.FileCode;
        Dock.ActivePaneChanged += (_, e) =>
        {
            // The Properties pane follows the editor in front.
            if (_viewModel is { } vm && e.Pane?.Content is EditorView { DataContext: EditorDocument document })
            {
                vm.Selected = document;
            }
        };
    }

    /// <summary>Opens <paramref name="path"/> in an editor tab, or brings its tab to the front.</summary>
    public void OpenFile(string path)
    {
        if (_viewModel is not { } vm || !vm.Workspace.Exists(path))
        {
            return;
        }
        if (Dock.Workspace.FindPane(DocumentPrefix + path) is { } open)
        {
            Dock.Activate(open);
            return;
        }
        // A document pane (no PreferredSide) joins the document group used last.
        var pane = CreateEditor(vm.Document(path));
        Dock.Open(pane);
        Dock.Activate(pane);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not MainViewModel vm || vm == _viewModel)
        {
            return;
        }
        _viewModel = vm;
        Explorer.ItemsSource = vm.Workspace.Root;
        Explorer.ExpandAll();
        foreach (var path in Files(vm.Workspace.Root))
        {
            Palette.Items.Add(new CommandPaletteItem { Id = DocumentPrefix + path, Label = path, Group = "Files", Icon = ArcIcons.FileCode });
        }
        vm.Output.CollectionChanged += OnOutputChanged;

        // The saved layout names the editor tabs by id; PaneResolver makes them again (a file that is gone is skipped).
        Dock.PaneResolver = id => id.StartsWith(DocumentPrefix, StringComparison.Ordinal) && vm.Workspace.Exists(id[DocumentPrefix.Length..])
            ? CreateEditor(vm.Document(id[DocumentPrefix.Length..]))
            : null;
        if (AppSettings.Current.DockLayout is { } saved)
        {
            Dock.LoadLayout(saved);
        }
        else
        {
            OpenFile("README.md");
        }
        // Every move, resize, close, float or new tab is saved.
        Dock.LayoutChanged += (_, _) =>
        {
            AppSettings.Current.DockLayout = Dock.SaveLayout();
            AppSettings.Current.Save();
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // Cmd+S / Ctrl+S saves the file in front.
        if (e.Key == Key.S && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)) &&
            Dock.ActivePane?.Content is EditorView { DataContext: EditorDocument document } && _viewModel is { } vm)
        {
            vm.Save(document);
            e.Handled = true;
        }
    }

    private static DockPane CreateEditor(EditorDocument document)
    {
        var pane = new DockPane
        {
            Id = DocumentPrefix + document.Path,
            Title = document.Name,
            Icon = ArcIcons.FileCode,
            Content = new EditorView { DataContext = document },
        };
        // The tab shows a dot while the file has unsaved changes. Closing the tab keeps them: the document stays in
        // MainViewModel until it is saved.
        document.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EditorDocument.IsModified))
            {
                pane.IsModified = document.IsModified;
            }
        };
        pane.IsModified = document.IsModified;
        return pane;
    }

    private void OnOutputChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.UIThread.Post(() => OutputScroll.ScrollToEnd(), DispatcherPriority.Background);

    private void OnExplorerSelectionChanged(object? sender, TreeViewSelectionChangedEventArgs e)
    {
        if (_viewModel is { } vm && e.NewItem is WorkspaceNode { IsFolder: false } node)
        {
            vm.Selected = vm.Document(node.Path);
        }
    }

    private void OnExplorerItemInvoked(object? sender, TreeViewItemEventArgs e)
    {
        if (e.Item is WorkspaceNode { IsFolder: false } node)
        {
            OpenFile(node.Path);
        }
    }

    private void OnProblemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string path })
        {
            OpenFile(path);
        }
    }

    private void OnResetLayout(object? sender, RoutedEventArgs e) => Dock.LoadLayout(_declaredLayout);

    private void OnCommandSelected(object? sender, CommandPaletteItemSelectedEventArgs e)
    {
        switch (e.Item.Id)
        {
            case "run":
                _viewModel?.RunCommand.Execute(null);
                break;
            case "save-all":
                _viewModel?.SaveAllCommand.Execute(null);
                break;
            case "reset-layout":
                Dock.LoadLayout(_declaredLayout);
                break;
            case { } id when id.StartsWith(DocumentPrefix, StringComparison.Ordinal):
                OpenFile(id[DocumentPrefix.Length..]);
                break;
        }
    }

    private static IEnumerable<string> Files(IEnumerable<WorkspaceNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.Children is { } children)
            {
                foreach (var path in Files(children))
                {
                    yield return path;
                }
            }
            else
            {
                yield return node.Path;
            }
        }
    }
}
