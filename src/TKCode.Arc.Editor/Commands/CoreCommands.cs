// Ported from vscode (src/vs/editor/browser/coreCommands.ts: CoreEditorCommand, NavigationCommandRevealType and the
// option interfaces of CoreNavigationCommands, commit b57d5c9e), MIT license.
// The core commands are split over several files: this one has their base classes and argument types,
// CoreCommands.Navigation.cs the cursor commands of CoreNavigationCommands, CoreCommands.Scroll.cs EditorScroll_,
// RevealLine_ and the scroll and reveal commands, CoreCommands.Editing.cs CoreEditingCommands, and
// CoreCommands.Keybindings.cs each command's id, precondition and keybindings as data.
// Only the command bodies are ported. The registration layer is left out: registerEditorCommand, the EditorCommand,
// MultiCommand and Command classes of editorExtensions.ts, the keybinding registry, the `when` preconditions
// (ContextKeyExpr), ServicesAccessor, the command metadata (descriptions and JSON schemas) and the DOM parts
// (EditorOrNativeTextInputCommand's native input fallback, aria `status`). What the registration needs is kept as data
// in CoreCommandsTable (CoreCommands.Keybindings.cs) so that it can be registered later.
// Differences from upstream:
//  - a command is an immutable object with its id and its body (RunCoreEditorCommand); the editor upstream passes to
//    runEditorCommand is ICoreCommandsCodeEditor, the members of ICodeEditor the commands call;
//  - upstream's Partial<T> arguments are records whose members are optional (null or default when not given); a null
//    argument object is an empty one, as upstream's `args || {}`;
//  - the position arguments (IPosition) are TextPositions, the selection argument (ISelection) a TextSelection.

using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.Commands;

/// <summary>
/// The members of upstream's ICodeEditor that the core commands call: the view model, the model, the read-only option,
/// undo stops and running edit commands.
/// </summary>
internal interface ICoreCommandsCodeEditor
{
    /// <summary>The editor's view model; null when the editor has no view (upstream's <c>_getViewModel</c>).</summary>
    IViewModel? GetViewModel();

    /// <summary>The editor's model; null when it has none (upstream's <c>hasModel</c> and <c>getModel</c>).</summary>
    TextModel? GetModel();

    /// <summary>The editor's <c>readOnly</c> option (upstream's <c>getOption(EditorOption.readOnly)</c>).</summary>
    bool IsReadOnly { get; }

    /// <summary>Adds an undo stop to the model; false in a read-only editor or one without a model.</summary>
    bool PushUndoStop();

    /// <summary>Runs the edit commands on the cursors (upstream's <c>executeCommands(source, commands)</c>).</summary>
    void ExecuteCommands(string? source, IReadOnlyList<ICommand?> commands);
}

/// <summary>
/// A core command as the command registry runs it: by id, on an editor, with untyped arguments (upstream's
/// EditorCommand.runEditorCommand as registerEditorCommand calls it).
/// </summary>
internal interface ICoreCommand
{
    /// <summary>The command's id.</summary>
    string Id { get; }

    /// <summary>Runs the command on <paramref name="editor"/>; returns a Task for asynchronous commands, else null.</summary>
    object? Run(ICoreCommandsCodeEditor editor, object? args);
}

/// <summary>A command that runs on an editor's view model (upstream's CoreEditorCommand).</summary>
/// <typeparam name="T">The command's arguments; every member is optional.</typeparam>
internal abstract class CoreEditorCommand<T> : ICoreCommand
    where T : class, new()
{
    protected CoreEditorCommand(string id)
    {
        Id = id;
    }

    /// <summary>The command's id.</summary>
    public string Id { get; }

    /// <summary>Runs the command on the editor's view model; an editor without a view has no cursors.</summary>
    public void RunEditorCommand(ICoreCommandsCodeEditor editor, T? args)
    {
        var viewModel = editor.GetViewModel();
        if (viewModel is null)
        {
            // the editor has no view => has no cursors
            return;
        }
        RunCoreEditorCommand(viewModel, args ?? new T());
    }

    /// <summary>Runs the command on <paramref name="viewModel"/>.</summary>
    public abstract void RunCoreEditorCommand(IViewModel viewModel, T args);

    /// <summary>
    /// The typed arguments of untyped ones: the arguments object itself, or what a command reads from the JSON a
    /// keybinding passes (upstream passes the parsed object through; its commands read the members they know).
    /// </summary>
    public virtual T? ConvertArgs(object? args) => args as T;

    object? ICoreCommand.Run(ICoreCommandsCodeEditor editor, object? args)
    {
        RunEditorCommand(editor, ConvertArgs(args));
        return null;
    }
}

/// <summary>How a navigation command reveals the cursor it moved.</summary>
internal enum NavigationCommandRevealType
{
    /// <summary>Do regular revealing.</summary>
    Regular = 0,

    /// <summary>Do only minimal revealing.</summary>
    Minimal = 1,

    /// <summary>Do not reveal the position.</summary>
    None = 2,
}

internal static partial class CoreNavigationCommands
{
    /// <summary>The arguments every core navigation command takes.</summary>
    internal record BaseCommandOptions
    {
        /// <summary>What started the command: <c>mouse</c>, <c>keyboard</c> or another source.</summary>
        public string? Source { get; init; }
    }

    /// <summary>The arguments of the commands that move to a position (MoveTo, WordSelect, LineSelect, ...).</summary>
    internal record MoveCommandOptions : BaseCommandOptions
    {
        /// <summary>The model position; the command does nothing without one.</summary>
        public TextPosition? Position { get; init; }

        /// <summary>The view position, when the caller knows it.</summary>
        public TextPosition? ViewPosition { get; init; }

        /// <summary>How to reveal the position afterwards.</summary>
        public NavigationCommandRevealType RevealType { get; init; }
    }

    /// <summary>The arguments of ColumnSelect.</summary>
    internal sealed record ColumnSelectCommandOptions : BaseCommandOptions
    {
        /// <summary>The model position under the mouse.</summary>
        public TextPosition? Position { get; init; }

        /// <summary>The view position under the mouse.</summary>
        public TextPosition? ViewPosition { get; init; }

        /// <summary>The (1-based) visible column under the mouse.</summary>
        public int? MouseColumn { get; init; }

        /// <summary>Whether to continue the current column selection (true) or start a new one.</summary>
        public bool DoColumnSelect { get; init; }
    }

    /// <summary>
    /// The arguments of the <c>cursorMove</c> command (upstream's <c>BaseCommandOptions &amp; CursorMove.RawArguments</c>);
    /// see <see cref="TKCode.Arc.Editor.Cursor.CursorMove.RawArguments"/> for the values.
    /// </summary>
    internal sealed record CursorMoveArguments : BaseCommandOptions
    {
        /// <summary>Where to move the cursor (<see cref="TKCode.Arc.Editor.Cursor.CursorMove.RawDirection"/>); mandatory.</summary>
        public string? To { get; init; }

        /// <summary>If true makes the selection.</summary>
        public bool? Select { get; init; }

        /// <summary>The unit to move by (<see cref="TKCode.Arc.Editor.Cursor.CursorMove.RawUnit"/>).</summary>
        public string? By { get; init; }

        /// <summary>The number of units to move.</summary>
        public int? Value { get; init; }

        /// <summary>If true the move is not added to the navigation history.</summary>
        public bool? NoHistory { get; init; }
    }

    /// <summary>The arguments of the cursor move commands (CursorLeft, CursorPageDown, ...).</summary>
    internal sealed record CursorMoveCommandOptions : BaseCommandOptions
    {
        /// <summary>The page size of the page up and down commands; the cursor configuration's when 0 or not given.</summary>
        public int? PageSize { get; init; }
    }

    /// <summary>The arguments of CreateCursor.</summary>
    internal sealed record CreateCursorCommandOptions : MoveCommandOptions
    {
        /// <summary>Whether the new cursor selects the whole line.</summary>
        public bool? WholeLine { get; init; }
    }

    /// <summary>The arguments of CursorEnd and CursorEndSelect.</summary>
    internal sealed record EndCommandOptions : BaseCommandOptions
    {
        /// <summary>Stick to the end even when going to longer lines.</summary>
        public bool? Sticky { get; init; }
    }

    /// <summary>The arguments of SetSelection.</summary>
    internal sealed record SetSelectionCommandOptions : BaseCommandOptions
    {
        /// <summary>The new selection; the command does nothing without one.</summary>
        public TextSelection? Selection { get; init; }
    }
}
