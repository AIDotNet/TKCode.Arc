// Tests for CoreCommandsRegistration, which ports the registration of vscode's core commands
// (src/vs/editor/browser/editorExtensions.ts: EditorCommand.runCommand, registerEditorCommand; commit b57d5c9e,
// MIT license). Upstream has no direct tests; these check the commands, keybindings, preconditions and arguments end to
// end through the real registries, context key expressions and command service.

using System.Text.Json;
using Shouldly;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Platform;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class CoreCommandsRegistrationTests
{
    [Fact]
    public void Every_Command_Of_The_Table_Has_An_Implementation_And_Registers()
    {
        var commands = new CommandsRegistry();
        var keybindings = new KeybindingsRegistry(commands, OperatingSystemKind.Linux);

        using (CoreCommandsRegistration.Register(commands, keybindings))
        {
            foreach (var descriptor in CoreCommandsTable.Commands)
            {
                commands.GetCommand(descriptor.Id).ShouldNotBeNull(descriptor.Id);
            }

            keybindings.GetDefaultKeybindings().Count.ShouldBeGreaterThan(CoreCommandsTable.Commands.Length / 2);
        }

        foreach (var descriptor in CoreCommandsTable.Commands)
        {
            commands.GetCommand(descriptor.Id).ShouldBeNull(descriptor.Id);
        }

        keybindings.GetDefaultKeybindings().ShouldBeEmpty();
    }

    [Fact]
    public void Keybindings_Carry_Real_When_Clauses_And_Their_Arguments()
    {
        var commands = new CommandsRegistry();
        var keybindings = new KeybindingsRegistry(commands, OperatingSystemKind.Linux);
        using var _ = CoreCommandsRegistration.Register(commands, keybindings);

        var cursorEnd = keybindings.GetDefaultKeybindings().Single(k => k.Command == "cursorEnd");
        cursorEnd.Keybinding.ShouldBe(Keybindings.DecodeKeybinding((int)KeyCode.End, OperatingSystemKind.Linux));
        cursorEnd.When!.Serialize().ShouldBe("textInputFocus");
        var args = cursorEnd.CommandArgs.ShouldBeOfType<JsonElement>();
        args.GetProperty("sticky").GetBoolean().ShouldBeFalse();

        var tab = keybindings.GetDefaultKeybindings().Single(k => k.Command == "tab");
        var context = new ContextKeyService();
        context.CreateKey("editorTextFocus", true);
        context.ContextMatchesRules(tab.When).ShouldBeTrue();
        context.CreateKey("editorReadonly", true);
        context.ContextMatchesRules(tab.When).ShouldBeFalse();
    }

    [Fact]
    public async Task Commands_Run_On_The_Focused_Editor_Through_The_Command_Service()
    {
        using var editor = TestCodeEditor.Create(["hello world", "second"]);
        var host = new EditorHost(editor);
        var commands = new CommandsRegistry();
        using var _ = CoreCommandsRegistration.Register(commands, new KeybindingsRegistry(commands, OperatingSystemKind.Linux));
        var service = new CommandService(commands, host);

        await service.ExecuteCommand("cursorEnd");
        editor.ViewModel.GetSelection().ShouldBe(new TextSelection(1, 12, 1, 12));

        // the JSON a keybinding passes is converted by the command
        using var json = JsonDocument.Parse("{\"sticky\":true}");
        await service.ExecuteCommand("cursorDown", (object?)null);
        await service.ExecuteCommand("cursorEnd", json.RootElement.Clone());
        editor.ViewModel.GetSelection().ShouldBe(new TextSelection(2, 7, 2, 7));

        await service.ExecuteCommand("deleteLeft");
        editor.Model.GetLineContent(2).ShouldBe("secon");

        await service.ExecuteCommand("undo");
        editor.Model.GetLineContent(2).ShouldBe("second");
    }

    [Fact]
    public async Task A_Failing_Precondition_Keeps_The_Command_From_Running()
    {
        using var editor = TestCodeEditor.Create(["abc"]);
        var host = new EditorHost(editor);
        host.ContextKeyService.CreateKey("editorReadonly", true);
        var commands = new CommandsRegistry();
        using var _ = CoreCommandsRegistration.Register(commands, new KeybindingsRegistry(commands, OperatingSystemKind.Linux));
        var service = new CommandService(commands, host);

        // tab's precondition is !editorReadonly
        await service.ExecuteCommand("tab");
        editor.Model.GetLineContent(1).ShouldBe("abc");

        host.ContextKeyService.CreateKey("editorReadonly", false);
        await service.ExecuteCommand("tab");
        editor.Model.GetLineContent(1).ShouldBe("    abc");
    }

    [Fact]
    public async Task Without_An_Editor_Nothing_Runs()
    {
        var commands = new CommandsRegistry();
        using var _ = CoreCommandsRegistration.Register(commands, new KeybindingsRegistry(commands, OperatingSystemKind.Linux));
        var service = new CommandService(commands, new EditorHost(null));

        (await service.ExecuteCommand("cursorEnd")).ShouldBeNull();
    }

    /// <summary>A host with one editor that has focus, and a context key service for the preconditions.</summary>
    private sealed class EditorHost(TestCodeEditor? editor) : IServicesAccessor, ICodeEditorService
    {
        public ContextKeyService ContextKeyService { get; } = new();

        public T Get<T>()
            where T : class =>
            this as T ?? throw new InvalidOperationException($"unknown service '{typeof(T).Name}'");

        public ICoreCommandsCodeEditor? GetFocusedCodeEditor() => editor is null ? null : new ScopedEditor(editor, ContextKeyService);

        public ICoreCommandsCodeEditor? GetActiveCodeEditor() => null;
    }

    /// <summary>The test editor with a scoped context key service.</summary>
    private sealed class ScopedEditor(TestCodeEditor editor, IContextKeyService contextKeyService) : ICoreCommandsCodeEditor, IContextKeyServiceProvider
    {
        public IContextKeyService ContextKeyService => contextKeyService;

        public bool IsReadOnly => editor.IsReadOnly;

        public ViewModel.IViewModel? GetViewModel() => editor.GetViewModel();

        public Model.TextModel? GetModel() => editor.GetModel();

        public bool PushUndoStop() => editor.PushUndoStop();

        public void ExecuteCommands(string? source, IReadOnlyList<Commands.ICommand?> commands) => editor.ExecuteCommands(source, commands);
    }
}
