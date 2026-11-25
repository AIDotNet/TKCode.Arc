// Ported from vscode (src/vs/editor/browser/editorExtensions.ts: EditorCommand.runCommand and registerEditorCommand,
// Command.register; src/vs/editor/browser/services/codeEditorService.ts: getFocusedCodeEditor/getActiveCodeEditor;
// commit b57d5c9e), MIT license.
//
// Differences from upstream:
//  - upstream registers the core commands into process-wide registries as a side effect of loading coreCommands.ts;
//    here Register puts them into the registries an application or editor owns, and returns what removes them;
//  - the editor service is the small ICodeEditorService below; an editor that has a scoped context key service
//    (IContextKeyServiceProvider) gets its precondition checked, as upstream's invokeWithinContext does, and one without
//    runs the command;
//  - a keybinding's `args` (JSON text in CoreCommandsTable) is passed as a JsonElement, which the commands that take
//    keybinding arguments convert (ICoreCommand / CoreEditorCommand.ConvertArgs).

using System.Text.Json;
using TKCode.Arc.Editor.Platform;
using PlatformCommand = TKCode.Arc.Editor.Platform.Command;

namespace TKCode.Arc.Editor.Commands;

/// <summary>Finds the editor a command applies to (the part of upstream's ICodeEditorService the commands use).</summary>
internal interface ICodeEditorService
{
    /// <summary>The editor that has keyboard focus, if any.</summary>
    ICoreCommandsCodeEditor? GetFocusedCodeEditor();

    /// <summary>The editor that was focused last, if any.</summary>
    ICoreCommandsCodeEditor? GetActiveCodeEditor();
}

/// <summary>An editor that has its own scoped context key service, against which command preconditions are checked.</summary>
internal interface IContextKeyServiceProvider
{
    IContextKeyService ContextKeyService { get; }
}

/// <summary>Registers the core commands and their keybindings.</summary>
internal static class CoreCommandsRegistration
{
    /// <summary>Every core command, by id.</summary>
    public static IReadOnlyDictionary<string, ICoreCommand> All { get; } = CreateAll();

    private static Dictionary<string, ICoreCommand> CreateAll()
    {
        ICoreCommand[] commands =
        [
            CoreNavigationCommands.MoveTo, CoreNavigationCommands.MoveToSelect, CoreNavigationCommands.ColumnSelect,
            CoreNavigationCommands.CursorColumnSelectLeft, CoreNavigationCommands.CursorColumnSelectRight,
            CoreNavigationCommands.CursorColumnSelectUp, CoreNavigationCommands.CursorColumnSelectPageUp,
            CoreNavigationCommands.CursorColumnSelectDown, CoreNavigationCommands.CursorColumnSelectPageDown,
            CoreNavigationCommands.CursorMove,
            CoreNavigationCommands.CursorLeft, CoreNavigationCommands.CursorLeftSelect,
            CoreNavigationCommands.CursorRight, CoreNavigationCommands.CursorRightSelect,
            CoreNavigationCommands.CursorUp, CoreNavigationCommands.CursorUpSelect,
            CoreNavigationCommands.CursorPageUp, CoreNavigationCommands.CursorPageUpSelect,
            CoreNavigationCommands.CursorDown, CoreNavigationCommands.CursorDownSelect,
            CoreNavigationCommands.CursorPageDown, CoreNavigationCommands.CursorPageDownSelect,
            CoreNavigationCommands.CreateCursor, CoreNavigationCommands.LastCursorMoveToSelect,
            CoreNavigationCommands.CursorHome, CoreNavigationCommands.CursorHomeSelect,
            CoreNavigationCommands.CursorLineStart, CoreNavigationCommands.CursorLineStartSelect,
            CoreNavigationCommands.CursorEnd, CoreNavigationCommands.CursorEndSelect,
            CoreNavigationCommands.CursorLineEnd, CoreNavigationCommands.CursorLineEndSelect,
            CoreNavigationCommands.CursorTop, CoreNavigationCommands.CursorTopSelect,
            CoreNavigationCommands.CursorBottom, CoreNavigationCommands.CursorBottomSelect,
            CoreNavigationCommands.WordSelect, CoreNavigationCommands.WordSelectDrag, CoreNavigationCommands.LastCursorWordSelect,
            CoreNavigationCommands.LineSelect, CoreNavigationCommands.LineSelectDrag,
            CoreNavigationCommands.LastCursorLineSelect, CoreNavigationCommands.LastCursorLineSelectDrag,
            CoreNavigationCommands.CancelSelection, CoreNavigationCommands.RemoveSecondaryCursors,
            CoreNavigationCommands.SelectAll, CoreNavigationCommands.SetSelection,
            CoreNavigationCommands.EditorScroll, CoreNavigationCommands.ScrollLineUp, CoreNavigationCommands.ScrollPageUp,
            CoreNavigationCommands.ScrollEditorTop, CoreNavigationCommands.ScrollLineDown, CoreNavigationCommands.ScrollPageDown,
            CoreNavigationCommands.ScrollEditorBottom, CoreNavigationCommands.ScrollLeft, CoreNavigationCommands.ScrollRight,
            CoreNavigationCommands.RevealLine,
            CoreEditingCommands.LineBreakInsert, CoreEditingCommands.Outdent, CoreEditingCommands.Tab,
            CoreEditingCommands.DeleteLeft, CoreEditingCommands.DeleteRight,
            CoreEditingCommands.Undo, CoreEditingCommands.Redo,
        ];

        var result = new Dictionary<string, ICoreCommand>(StringComparer.Ordinal);
        foreach (var command in commands)
        {
            result.Add(command.Id, command);
        }

        return result;
    }

    /// <summary>
    /// Registers every command of <see cref="CoreCommandsTable"/> with its handler, and its keybindings, into the given
    /// registries. Disposing the result removes them again.
    /// </summary>
    public static IDisposable Register(CommandsRegistry commands, KeybindingsRegistry keybindings)
    {
        var registrations = new List<IDisposable>();
        foreach (var descriptor in CoreCommandsTable.Commands)
        {
            if (!All.TryGetValue(descriptor.Id, out var command))
            {
                throw new InvalidOperationException($"Core command '{descriptor.Id}' has no implementation.");
            }

            var precondition = ContextKeyExpr.Deserialize(descriptor.Precondition);
            registrations.Add(commands.RegisterCommand(new PlatformCommand(
                descriptor.Id,
                (accessor, args) => RunCommand(accessor, command, precondition, args.Length > 0 ? args[0] : null))));

            foreach (var kb in descriptor.Keybindings)
            {
                registrations.Add(keybindings.RegisterKeybindingRule(new KeybindingRule
                {
                    Id = descriptor.Id,
                    Weight = kb.Weight,
                    When = ContextKeyExpr.Deserialize(kb.When),
                    Primary = kb.Primary,
                    Secondary = kb.Secondary,
                    Mac = ToPlatform(kb.Mac),
                    Win = ToPlatform(kb.Win),
                    Linux = ToPlatform(kb.Linux),
                    Args = kb.Args is null ? null : ParseArgs(kb.Args),
                }));
            }
        }

        return new Registrations(registrations);
    }

    // EditorCommand.runCommand: the focused editor, else the active one; nothing when there is none.
    private static object? RunCommand(IServicesAccessor accessor, ICoreCommand command, ContextKeyExpression? precondition, object? args)
    {
        var codeEditorService = accessor.Get<ICodeEditorService>();
        var editor = codeEditorService.GetFocusedCodeEditor() ?? codeEditorService.GetActiveCodeEditor();
        if (editor is null)
        {
            // well, at least we tried...
            return null;
        }

        if (editor is IContextKeyServiceProvider provider && !provider.ContextKeyService.ContextMatchesRules(precondition))
        {
            // precondition does not hold
            return null;
        }

        return command.Run(editor, args);
    }

    private static PlatformKeybindings? ToPlatform(CoreCommandPlatformKeybinding? keybinding) =>
        keybinding is null ? null : new PlatformKeybindings { Primary = keybinding.Primary, Secondary = keybinding.Secondary };

    private static JsonElement ParseArgs(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed class Registrations(List<IDisposable> registrations) : IDisposable
    {
        public void Dispose()
        {
            for (var i = registrations.Count - 1; i >= 0; i--)
            {
                registrations[i].Dispose();
            }

            registrations.Clear();
        }
    }
}
