using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.ViewModel;
using EditCommand = TKCode.Arc.Editor.Commands.ICommand;

namespace TKCode.Arc;

// The editor as the core commands see it (upstream's CodeEditorWidget: getModel, _getViewModel, the readOnly option,
// pushUndoStop, executeCommands, invokeWithinContext), and the command service its keybindings run through.
public sealed partial class CodeEditor : ICoreCommandsCodeEditor, IContextKeyServiceProvider, ICodeEditorService, IServicesAccessor
{
    private CommandService? _commandService;

    internal CommandService CommandService => _commandService ??= new CommandService(CommandsRegistry, this);

    IContextKeyService IContextKeyServiceProvider.ContextKeyService => ContextKeyService;

    bool ICoreCommandsCodeEditor.IsReadOnly => IsReadOnly;

    IViewModel? ICoreCommandsCodeEditor.GetViewModel() => ViewModel;

    TextModel? ICoreCommandsCodeEditor.GetModel() => Model;

    bool ICoreCommandsCodeEditor.PushUndoStop() => PushUndoStop();

    void ICoreCommandsCodeEditor.ExecuteCommands(string? source, IReadOnlyList<EditCommand?> commands) =>
        ViewModel?.ExecuteCommands(commands, source);

    ICoreCommandsCodeEditor? ICodeEditorService.GetFocusedCodeEditor() => this;

    ICoreCommandsCodeEditor? ICodeEditorService.GetActiveCodeEditor() => this;

    T IServicesAccessor.Get<T>() =>
        this as T ?? throw new InvalidOperationException($"[invokeFunction] unknown service '{typeof(T).Name}'");

    /// <summary>Adds an undo stop (upstream's pushUndoStop); false in a read-only editor.</summary>
    internal bool PushUndoStop()
    {
        if (IsReadOnly)
        {
            // read only editor => sorry!
            return false;
        }

        Model.PushStackElement();
        return true;
    }

    /// <summary>Runs a command by id, as a keybinding does.</summary>
    internal Task<object?> ExecuteCommand(string commandId, object? args = null) => CommandService.ExecuteCommand(commandId, args);
}
