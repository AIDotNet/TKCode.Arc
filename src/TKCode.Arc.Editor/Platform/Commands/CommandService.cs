// Ported from vscode (src/vs/editor/standalone/browser/standaloneServices.ts: StandaloneCommandService, commit
// b57d5c9e), MIT license. Differences from upstream:
// - It runs the commands of the CommandsRegistry it is given, not of the process-wide one.
// - There is no instantiation service: the handler gets an IServicesAccessor that returns this service for
//   ICommandService and asks the accessor the application passed in for anything else.
// - A handler's result that is a Task<object?> is the result task itself, and one that is another Task is awaited and
//   gives null (upstream's Promise.resolve adopts any promise; reading another Task's result would need reflection).

namespace TKCode.Arc.Editor.Platform;

/// <summary>Runs the commands of a <see cref="CommandsRegistry"/>.</summary>
internal sealed class CommandService : ICommandService
{
    private readonly ICommandRegistry _registry;
    private readonly Accessor _accessor;
    private readonly Emitter<CommandEvent> _onWillExecuteCommand = new();
    private readonly Emitter<CommandEvent> _onDidExecuteCommand = new();

    /// <param name="registry">The commands this service runs.</param>
    /// <param name="services">What handlers get for services other than <see cref="ICommandService"/>; none when null.</param>
    public CommandService(ICommandRegistry registry, IServicesAccessor? services = null)
    {
        _registry = registry;
        _accessor = new Accessor(this, services);
    }

    public event Action<CommandEvent>? WillExecuteCommand
    {
        add
        {
            if (value is not null)
            {
                _onWillExecuteCommand.Add(value);
            }
        }
        remove => _onWillExecuteCommand.Remove(value);
    }

    public event Action<CommandEvent>? DidExecuteCommand
    {
        add
        {
            if (value is not null)
            {
                _onDidExecuteCommand.Add(value);
            }
        }
        remove => _onDidExecuteCommand.Remove(value);
    }

    public Task<object?> ExecuteCommand(string commandId, params object?[] args)
    {
        var command = _registry.GetCommand(commandId);
        if (command is null)
        {
            return Task.FromException<object?>(new InvalidOperationException($"command '{commandId}' not found"));
        }

        try
        {
            _onWillExecuteCommand.Fire(new CommandEvent(commandId, args));
            var result = command.Handler(_accessor, args);

            _onDidExecuteCommand.Fire(new CommandEvent(commandId, args));
            return result switch
            {
                Task<object?> task => task,
                Task task => AwaitWithoutResult(task),
                _ => Task.FromResult(result),
            };
        }
        catch (Exception e)
        {
            return Task.FromException<object?>(e);
        }
    }

    private static async Task<object?> AwaitWithoutResult(Task task)
    {
        await task.ConfigureAwait(false);
        return null;
    }

    private sealed class Accessor : IServicesAccessor
    {
        private readonly CommandService _owner;
        private readonly IServicesAccessor? _services;

        public Accessor(CommandService owner, IServicesAccessor? services)
        {
            _owner = owner;
            _services = services;
        }

        public T Get<T>()
            where T : class
        {
            if (_owner is T service)
            {
                return service;
            }

            if (_services is null)
            {
                throw new InvalidOperationException($"[invokeFunction] unknown service '{typeof(T).Name}'");
            }

            return _services.Get<T>();
        }
    }
}
