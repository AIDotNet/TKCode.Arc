using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ArcStarterApp.ViewModels;
using TKCode.Arc;

namespace ArcStarterApp.Views;

/// <summary>
/// The app's content: palette, canvas and inspector. The main window shows it on the desktop. The canvas owns the
/// selection and the edits; this view passes them to the view model.
/// </summary>
public partial class MainView : UserControl
{
    private static readonly FilePickerFileType s_json = new("Workflow (JSON)") { Patterns = ["*.json"] };

    public MainView()
    {
        InitializeComponent();
        Canvas.SelectionChanged += (_, _) =>
        {
            if (ViewModel is { } vm)
            {
                vm.Selected = Canvas.SelectedNodes.Count == 1 ? Canvas.SelectedNodes[0] : null;
            }
        };
        // Every move, connection, deletion, paste, undo and redo is saved.
        Canvas.GraphEdited += (_, _) => ViewModel?.Save();
        // A connection dropped on empty canvas asks for a step there: add one, connected.
        Canvas.ConnectionDroppedOnCanvas += (_, e) =>
        {
            if (ViewModel is { } vm)
            {
                Canvas.Select([e.SourceNode]);
                vm.Selected = e.SourceNode;
                var step = vm.AddStep(Models.StepKinds.Find("action"), new Avalonia.Point(e.Position.X - 150, e.Position.Y));
                Canvas.Select([step]);
            }
        };
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // Text typed in the inspector changes the step; save it a moment after typing stops.
        if (ViewModel is { } vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.Selected) && vm.Selected is { } step)
                {
                    step.PropertyChanged += (_, change) =>
                    {
                        if (change.Property == WorkflowNode.TitleProperty || change.Property == WorkflowNode.DescriptionProperty ||
                            change.Property == WorkflowNode.IsLockedProperty)
                        {
                            vm.Save();
                        }
                    };
                }
            };
        }
    }

    private void OnArrange(object? sender, RoutedEventArgs e) => Canvas.ArrangeNodes();

    private async void OnExport(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export workflow",
            SuggestedFileName = vm.Name + ".json",
            FileTypeChoices = [s_json],
        });
        if (file is not null)
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(vm.Export());
        }
    }

    private async void OnImport(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Import workflow", FileTypeFilter = [s_json] });
        if (files.Count == 0)
        {
            return;
        }
        await using var stream = await files[0].OpenReadAsync();
        using var reader = new StreamReader(stream);
        if (!vm.Import(await reader.ReadToEndAsync()))
        {
            vm.Status = "That file is not a workflow";
        }
    }
}
