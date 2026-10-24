using System.Windows.Input;

namespace TKCode.Arc;

public partial class WorkflowCanvas
{
    // Made when first asked for; the canvas tells the made ones when they may have changed whether they can run.
    private readonly List<WorkflowCommand> _commands = new();
    private WorkflowCommand? _undoCommand;
    private WorkflowCommand? _redoCommand;
    private WorkflowCommand? _deleteSelectionCommand;
    private WorkflowCommand? _copyCommand;
    private WorkflowCommand? _cutCommand;
    private WorkflowCommand? _pasteCommand;
    private WorkflowCommand? _duplicateCommand;
    private WorkflowCommand? _zoomInCommand;
    private WorkflowCommand? _zoomOutCommand;
    private WorkflowCommand? _fitCommand;

    /// <summary>Undoes the last edit (<see cref="Undo"/>); for a toolbar button. Can run on an editable canvas with an edit to undo.</summary>
    public ICommand UndoCommand => _undoCommand ??= Command(Undo, () => IsEditable && Graph?.History.CanUndo == true);

    /// <summary>Redoes the last undone edit (<see cref="Redo"/>). Can run on an editable canvas with an undone edit.</summary>
    public ICommand RedoCommand => _redoCommand ??= Command(Redo, () => IsEditable && Graph?.History.CanRedo == true);

    /// <summary>Deletes the selection (<see cref="DeleteSelection"/>). Can run on an editable canvas with something selected.</summary>
    public ICommand DeleteSelectionCommand =>
        _deleteSelectionCommand ??= Command(() => DeleteSelection(), () => IsEditable && (_selection.Count > 0 || _selectedEdges.Count > 0));

    /// <summary>Copies the selected steps (<see cref="CopySelectionAsync"/>). Can run with a step selected, read-only too.</summary>
    public ICommand CopyCommand => _copyCommand ??= Command(() => _ = CopySelectionAsync(), () => _selection.Count > 0);

    /// <summary>Cuts the selected steps (<see cref="CutSelectionAsync"/>). Can run on an editable canvas with a step selected.</summary>
    public ICommand CutCommand => _cutCommand ??= Command(() => _ = CutSelectionAsync(), () => IsEditable && _selection.Count > 0);

    /// <summary>Pastes copied steps (<see cref="PasteAsync"/>). Can run on an editable canvas (what the clipboard holds is read on paste).</summary>
    public ICommand PasteCommand => _pasteCommand ??= Command(() => _ = PasteAsync(), () => IsEditable && Graph is not null);

    /// <summary>Duplicates the selected steps (<see cref="DuplicateSelection"/>). Can run on an editable canvas with a step selected.</summary>
    public ICommand DuplicateCommand => _duplicateCommand ??= Command(() => DuplicateSelection(), () => IsEditable && _selection.Count > 0);

    /// <summary>Zooms in one step (<see cref="ZoomIn"/>). Can run below <see cref="MaxZoom"/>.</summary>
    public ICommand ZoomInCommand => _zoomInCommand ??= Command(ZoomIn, () => Target.Zoom < MaxZoom - 1e-6);

    /// <summary>Zooms out one step (<see cref="ZoomOut"/>). Can run above <see cref="MinZoom"/>.</summary>
    public ICommand ZoomOutCommand => _zoomOutCommand ??= Command(ZoomOut, () => Target.Zoom > MinZoom + 1e-6);

    /// <summary>Fits the workflow (<see cref="FitToContent"/>). Can run with a workflow.</summary>
    public ICommand FitCommand => _fitCommand ??= Command(() => FitToContent(), () => Graph is not null);

    private WorkflowCommand Command(Action run, Func<bool> canRun)
    {
        var command = new WorkflowCommand(run, canRun);
        _commands.Add(command);
        return command;
    }

    /// <summary>Tells buttons bound to the commands to ask again whether they can run.</summary>
    private void RefreshCommands()
    {
        foreach (var command in _commands)
        {
            command.Refresh();
        }
    }

    private void OnHistoryChanged(object? sender, EventArgs e) => RefreshCommands();
}

/// <summary>A command of a <see cref="WorkflowCanvas"/>: runs an action when it may.</summary>
internal sealed class WorkflowCommand(Action run, Func<bool> canRun) : ICommand
{
    private bool? _could;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => (_could = canRun()).Value;

    public void Execute(object? parameter)
    {
        if (canRun())
        {
            run();
        }
    }

    /// <summary>Raises <see cref="CanExecuteChanged"/> when the answer changed (or was never given).</summary>
    public void Refresh()
    {
        if (_could != canRun())
        {
            _could = null;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
