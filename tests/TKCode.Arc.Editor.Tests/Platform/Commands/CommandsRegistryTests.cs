// Ported from vscode (src/vs/platform/commands/test/common/commands.test.ts, commit b57d5c9e), MIT license.
// The first four tests are upstream's, on a registry instance instead of the singleton ('command with description'
// uses a predicate constraint where upstream passes the Number class). The rest are not upstream: the alias, the
// CommandService (modelled on StandaloneCommandService) and the KeybindingsRegistry.

using Shouldly;
using TKCode.Arc.Editor.Platform;
using Xunit;
using static TKCode.Arc.Editor.KeyCodes;

namespace TKCode.Arc.Editor.Tests;

public sealed class CommandsRegistryTests
{
    [Fact]
    public void Register_Command_No_Handler()
    {
        var registry = new CommandsRegistry();
        Should.Throw<ArgumentException>(() => registry.RegisterCommand("foo", null!));
    }

    [Fact]
    public void Register_Dispose()
    {
        var registry = new CommandsRegistry();
        ICommandHandler command = (_, _) => null;
        var reg = registry.RegisterCommand("foo", command);
        registry.GetCommand("foo")!.Handler.ShouldBeSameAs(command);
        reg.Dispose();
        registry.GetCommand("foo").ShouldBeNull();
    }

    [Fact]
    public void Register_Register_Dispose()
    {
        var registry = new CommandsRegistry();
        ICommandHandler command1 = (_, _) => 1;
        ICommandHandler command2 = (_, _) => 2;

        // dispose overriding command
        var reg1 = registry.RegisterCommand("foo", command1);
        registry.GetCommand("foo")!.Handler.ShouldBeSameAs(command1);

        var reg2 = registry.RegisterCommand("foo", command2);
        registry.GetCommand("foo")!.Handler.ShouldBeSameAs(command2);
        reg2.Dispose();

        registry.GetCommand("foo")!.Handler.ShouldBeSameAs(command1);
        reg1.Dispose();
        registry.GetCommand("foo").ShouldBeNull();

        // dispose override command first
        reg1 = registry.RegisterCommand("foo", command1);
        reg2 = registry.RegisterCommand("foo", command2);
        registry.GetCommand("foo")!.Handler.ShouldBeSameAs(command2);

        reg1.Dispose();
        registry.GetCommand("foo")!.Handler.ShouldBeSameAs(command2);

        reg2.Dispose();
        registry.GetCommand("foo").ShouldBeNull();
    }

    [Fact]
    public void Command_With_Description()
    {
        var registry = new CommandsRegistry();
        var r1 = registry.RegisterCommand("test", (_, args) =>
        {
            args[0].ShouldBeOfType<string>();
            return null;
        });
        var r2 = registry.RegisterCommand("test2", (_, args) =>
        {
            args[0].ShouldBeOfType<string>();
            return null;
        });
        var r3 = registry.RegisterCommand(new Command(
            "test3",
            (_, _) => true,
            new CommandMetadata("a command", [new CommandArgumentMetadata("value", Constraint: static arg => arg is int or double)])));

        registry.GetCommands()["test"].Handler(null!, ["string"]);
        registry.GetCommands()["test2"].Handler(null!, ["string"]);
        Should.Throw<ArgumentException>(() => registry.GetCommands()["test3"].Handler(null!, ["string"]));
        registry.GetCommands()["test3"].Handler(null!, [1]).ShouldBe(true);

        RegistrationDisposable.Combined(r1, r2, r3).Dispose();
        registry.GetCommand("test3").ShouldBeNull();
    }

    [Fact]
    public void Noop_Is_Registered_And_Registration_Is_Announced()
    {
        var registry = new CommandsRegistry();
        registry.GetCommand("noop").ShouldNotBeNull();
        var registered = new List<string>();
        registry.DidRegisterCommand += registered.Add;
        using var reg = registry.RegisterCommand("foo", (_, _) => null);
        registered.ShouldBe(["foo"]);
    }

    [Fact]
    public async Task Command_Service_Runs_Commands_Aliases_And_Reports_Missing_Ones()
    {
        var registry = new CommandsRegistry();
        var service = new CommandService(registry);
        var events = new List<string>();
        service.WillExecuteCommand += e => events.Add("will " + e.CommandId);
        service.DidExecuteCommand += e => events.Add("did " + e.CommandId);

        registry.RegisterCommand("add", (_, args) => (int)args[0]! + (int)args[1]!);
        registry.RegisterCommand("async", (_, args) => Task.FromResult<object?>("done"));
        registry.RegisterCommandAlias("plus", "add");

        (await service.ExecuteCommand("add", 1, 2)).ShouldBe(3);
        (await service.ExecuteCommand("plus", 3, 4)).ShouldBe(7);
        (await service.ExecuteCommand("async")).ShouldBe("done");
        events.ShouldBe(["will add", "did add", "will plus", "will add", "did add", "did plus", "will async", "did async"]);

        await Should.ThrowAsync<InvalidOperationException>(() => service.ExecuteCommand("missing"));
        registry.RegisterCommand("throws", (_, _) => throw new FormatException());
        await Should.ThrowAsync<FormatException>(() => service.ExecuteCommand("throws"));
    }

    [Fact]
    public void Keybindings_Registry_Binds_Rules_For_The_Platform_And_Sorts_Them()
    {
        var commands = new CommandsRegistry();
        var registry = new KeybindingsRegistry(commands, OperatingSystemKind.Windows);

        using var rule = registry.RegisterCommandAndKeybindingRule(new CommandAndKeybindingRule
        {
            Id = "b",
            Weight = KeybindingWeight.EditorContrib,
            Primary = KeyMod.CtrlCmd | (int)KeyCode.KeyB,
            Secondary = [KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyK, (int)KeyCode.KeyB)],
            Mac = new PlatformKeybindings { Primary = KeyMod.WinCtrl | (int)KeyCode.KeyB },
            Handler = (_, _) => null,
        });
        var other = registry.RegisterKeybindingRule(new KeybindingRule
        {
            Id = "a",
            Weight = KeybindingWeight.EditorCore,
            Primary = (int)KeyCode.F1,
        });

        commands.GetCommand("b").ShouldNotBeNull();
        var items = registry.GetDefaultKeybindings();
        items.Select(i => (i.Command, i.Weight2)).ShouldBe([("a", 0), ("b", -1), ("b", 0)]);
        items[1].Keybinding!.Chords.Count.ShouldBe(2);
        items[2].Keybinding!.Equals(new KeyCodeChord(true, false, false, false, KeyCode.KeyB).ToKeybinding()).ShouldBeTrue();

        var mac = registry.GetDefaultKeybindingsForOS(OperatingSystemKind.Macintosh);
        mac.Where(i => i.Command == "b").Single().Keybinding!
            .Equals(new KeyCodeChord(true, false, false, false, KeyCode.KeyB).ToKeybinding()).ShouldBeTrue();

        other.Dispose();
        registry.GetDefaultKeybindings().Select(i => i.Command).ShouldBe(["b", "b"]);
        registry.GetDefaultKeybindingsForOS(OperatingSystemKind.Windows).Select(i => i.Command).ShouldBe(["b", "b"]);
    }
}
