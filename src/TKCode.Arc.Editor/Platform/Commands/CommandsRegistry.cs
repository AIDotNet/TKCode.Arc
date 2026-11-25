// Ported from vscode (src/vs/platform/commands/common/commands.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
// - CommandsRegistry is a class that an application or an editor creates and owns; upstream's is one process-wide
//   singleton. Its constructor registers 'noop', which upstream registers on the singleton at load time.
// - There is no dependency injection. A handler is a delegate that gets an IServicesAccessor (a small interface
//   defined here, upstream's ServicesAccessor) and the arguments as object?[].
// - ICommandHandler is a delegate, as upstream's is a function type; ICommand and ICommandMetadata are interfaces with
//   Command and CommandMetadata as plain implementations; ICommandEvent is the record CommandEvent; ICommandsMap is an
//   IReadOnlyDictionary.
// - Argument constraints are predicates (Func<object?, bool>): upstream's TypeConstraint also takes a typeof name or a
//   class, which a predicate covers without reflection. Upstream wraps the handler of the command object it is given;
//   here the registry stores a copy with the wrapped handler and leaves the caller's object alone. The schema of an
//   argument is kept as an opaque object.
// - Events are .NET events backed by Emitter (upstream's delivery order); onDidRegisterCommand is DidRegisterCommand.
// - The registry is not thread safe: like upstream, it is used from one thread (the UI thread).

namespace TKCode.Arc.Editor.Platform;

/// <summary>
/// What a command handler gets to reach services (upstream's ServicesAccessor). There is no dependency injection here:
/// whoever invokes the handler (the <see cref="CommandService"/>) decides what it can return.
/// </summary>
internal interface IServicesAccessor
{
    /// <summary>Returns the service of type <typeparamref name="T"/>; throws when there is none, as upstream's get does.</summary>
    T Get<T>()
        where T : class;
}

/// <summary>A command implementation: gets the services accessor and the arguments, returns the result (or a Task of it).</summary>
internal delegate object? ICommandHandler(IServicesAccessor accessor, object?[] args);

/// <summary>A command: its id, its handler and what it describes about itself.</summary>
internal interface ICommand
{
    string Id { get; }

    ICommandHandler Handler { get; }

    ICommandMetadata? Metadata { get; }
}

/// <summary>A plain <see cref="ICommand"/> (upstream's object literal).</summary>
internal sealed record Command(string Id, ICommandHandler Handler, ICommandMetadata? Metadata = null) : ICommand;

/// <summary>Describes one argument of a command.</summary>
/// <param name="Name">The argument's name.</param>
/// <param name="IsOptional">Whether the argument may be left out.</param>
/// <param name="Description">What the argument is.</param>
/// <param name="Constraint">Returns true when the argument is acceptable; the registry checks it before the handler runs.</param>
/// <param name="Schema">A JSON schema of the argument, opaque to the engine.</param>
internal sealed record CommandArgumentMetadata(
    string Name,
    bool IsOptional = false,
    string? Description = null,
    Func<object?, bool>? Constraint = null,
    object? Schema = null);

internal interface ICommandMetadata
{
    /// <summary>
    /// A short summary of what the command does. This will be used in API commands, when showing keybindings that have no
    /// other UX and when searching for commands in the Command Palette.
    /// </summary>
    string Description { get; }

    IReadOnlyList<CommandArgumentMetadata>? Args { get; }

    string? Returns { get; }
}

/// <summary>A plain <see cref="ICommandMetadata"/>.</summary>
internal sealed record CommandMetadata(
    string Description,
    IReadOnlyList<CommandArgumentMetadata>? Args = null,
    string? Returns = null) : ICommandMetadata;

/// <summary>A command about to run or that ran (upstream's ICommandEvent).</summary>
internal sealed record CommandEvent(string CommandId, IReadOnlyList<object?> Args);

/// <summary>Runs commands by id (upstream's ICommandService).</summary>
internal interface ICommandService
{
    event Action<CommandEvent>? WillExecuteCommand;

    event Action<CommandEvent>? DidExecuteCommand;

    /// <summary>
    /// Runs the command; the task fails when there is no such command or the handler throws. A handler that works
    /// asynchronously returns a Task (of object?), which the result follows, as a promise does upstream.
    /// </summary>
    Task<object?> ExecuteCommand(string commandId, params object?[] args);
}

internal interface ICommandRegistry
{
    event Action<string>? DidRegisterCommand;

    IDisposable RegisterCommand(string id, ICommandHandler command);

    IDisposable RegisterCommand(ICommand command);

    IDisposable RegisterCommandAlias(string oldId, string newId);

    ICommand? GetCommand(string id);

    IReadOnlyDictionary<string, ICommand> GetCommands();
}

/// <summary>
/// The commands an application or editor knows. Registering an id again shadows the earlier command until the newer
/// registration is disposed.
/// </summary>
internal sealed class CommandsRegistry : ICommandRegistry
{
    private readonly Dictionary<string, LinkedList<ICommand>> _commands = new(StringComparer.Ordinal);
    private readonly Emitter<string> _onDidRegisterCommand = new();

    public CommandsRegistry()
    {
        RegisterCommand("noop", static (_, _) => null);
    }

    public event Action<string>? DidRegisterCommand
    {
        add
        {
            if (value is not null)
            {
                _onDidRegisterCommand.Add(value);
            }
        }
        remove => _onDidRegisterCommand.Remove(value);
    }

    public IDisposable RegisterCommand(string id, ICommandHandler command)
    {
        if (string.IsNullOrEmpty(id) || command is null)
        {
            throw new ArgumentException("invalid command");
        }

        return RegisterCommand(new Command(id, command));
    }

    public IDisposable RegisterCommand(ICommand command)
    {
        if (command is null || string.IsNullOrEmpty(command.Id))
        {
            throw new ArgumentException("invalid command");
        }

        // add argument validation if rich command metadata is provided
        if (command.Metadata?.Args is { } args)
        {
            var constraints = new Func<object?, bool>?[args.Count];
            for (var i = 0; i < args.Count; i++)
            {
                constraints[i] = args[i].Constraint;
            }

            var actualHandler = command.Handler;
            command = new Command(command.Id, (accessor, handlerArgs) =>
            {
                ValidateConstraints(handlerArgs, constraints);
                return actualHandler(accessor, handlerArgs);
            }, command.Metadata);
        }

        // find a place to store the command
        var id = command.Id;
        if (!_commands.TryGetValue(id, out var commands))
        {
            commands = new LinkedList<ICommand>();
            _commands[id] = commands;
        }

        var node = commands.AddFirst(command);

        var ret = RegistrationDisposable.ToDisposable(() =>
        {
            if (node.List is not null)
            {
                node.List.Remove(node);
            }

            if (_commands.TryGetValue(id, out var list) && list.Count == 0)
            {
                _commands.Remove(id);
            }
        });

        // tell the world about this command
        _onDidRegisterCommand.Fire(id);

        return ret;
    }

    public IDisposable RegisterCommandAlias(string oldId, string newId)
    {
        return RegisterCommand(oldId, (accessor, args) => accessor.Get<ICommandService>().ExecuteCommand(newId, args));
    }

    public ICommand? GetCommand(string id)
    {
        if (!_commands.TryGetValue(id, out var list) || list.Count == 0)
        {
            return null;
        }

        return list.First!.Value;
    }

    public IReadOnlyDictionary<string, ICommand> GetCommands()
    {
        var result = new Dictionary<string, ICommand>(StringComparer.Ordinal);
        foreach (var key in _commands.Keys)
        {
            var command = GetCommand(key);
            if (command is not null)
            {
                result[key] = command;
            }
        }

        return result;
    }

    // src/vs/base/common/types.ts validateConstraints and validateConstraint (the predicate form)
    private static void ValidateConstraints(object?[] args, Func<object?, bool>?[] constraints)
    {
        var len = Math.Min(args.Length, constraints.Length);
        for (var i = 0; i < len; i++)
        {
            var constraint = constraints[i];
            if (constraint is not null && !constraint(args[i]))
            {
                throw new ArgumentException("argument does not match constraint");
            }
        }
    }
}
