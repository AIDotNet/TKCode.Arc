// Ported from vscode (src/vs/platform/keybinding/common/keybindingsRegistry.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
// - KeybindingsRegistry is a class that an application or an editor creates and owns, with the CommandsRegistry that
//   RegisterCommandAndKeybindingRule registers commands in and the operating system its rules are bound for (the
//   process's by default); upstream's is one process-wide singleton registered in the platform Registry under
//   'platform.keybindingsRegistry', which does not exist here.
// - The data interfaces are classes named without the I: IKeybindingItem -> KeybindingItem, IKeybindings ->
//   KeybindingRule's Primary/Secondary/Win/Linux/Mac (the per-platform part is PlatformKeybindings), IKeybindingRule ->
//   KeybindingRule, IExtensionKeybindingRule -> ExtensionKeybindingRule, ICommandAndKeybindingRule ->
//   CommandAndKeybindingRule (which is an ICommand), IKeybindingAlias -> KeybindingAlias. The registry itself keeps its
//   interface, IKeybindingsRegistry.
// - KeybindingWeight is a static class of int constants (weights are numbers upstream: rules add to them).
// - A keybinding number of 0 means no keybinding, as upstream's undefined and 0 both do.
// - Sorting is stable, as JavaScript's Array.prototype.sort is; commands compare ordinally (UTF-16 code units).
// - The registry is not thread safe: like upstream, it is used from one thread (the UI thread).

namespace TKCode.Arc.Editor.Platform;

/// <summary>A default keybinding, as the registry hands it to the keybinding service.</summary>
internal sealed class KeybindingItem
{
    public Keybinding? Keybinding { get; init; }

    public string? Command { get; init; }

    public object? CommandArgs { get; init; }

    public ContextKeyExpression? When { get; init; }

    public int Weight1 { get; init; }

    public int Weight2 { get; init; }

    public string? ExtensionId { get; init; }

    public bool IsBuiltinExtension { get; init; }

    /// <summary>See <see cref="KeybindingRule.SystemWide"/>.</summary>
    public bool SystemWide { get; init; }
}

/// <summary>The keybindings of a rule on one platform (the win, linux and mac members of upstream's IKeybindings).</summary>
internal sealed class PlatformKeybindings
{
    public int Primary { get; init; }

    public IReadOnlyList<int>? Secondary { get; init; }
}

/// <summary>Binds a command to keybindings (upstream's IKeybindingRule, with IKeybindings).</summary>
internal class KeybindingRule
{
    public int Primary { get; init; }

    public IReadOnlyList<int>? Secondary { get; init; }

    public PlatformKeybindings? Win { get; init; }

    public PlatformKeybindings? Linux { get; init; }

    public PlatformKeybindings? Mac { get; init; }

    public required string Id { get; init; }

    /// <summary>See <see cref="KeybindingWeight"/>.</summary>
    public int Weight { get; init; }

    public object? Args { get; init; }

    /// <summary>Keybinding is disabled if expression returns false.</summary>
    public ContextKeyExpression? When { get; init; }

    /// <summary>
    /// Also registers the keybinding as a system-wide shortcut on desktop, which works while another application is in
    /// front, for as long as <see cref="When"/> holds in a window of the application. That window evaluates
    /// <see cref="When"/> without regard to focus, so it should only depend on keys such as settings.
    /// </summary>
    public bool SystemWide { get; init; }
}

internal sealed class ExtensionKeybindingRule
{
    public Keybinding? Keybinding { get; init; }

    public required string Id { get; init; }

    public object? Args { get; init; }

    public int Weight { get; init; }

    public ContextKeyExpression? When { get; init; }

    public string? ExtensionId { get; init; }

    public bool IsBuiltinExtension { get; init; }
}

/// <summary>The ranks of default keybindings: a higher weight wins over a lower one.</summary>
internal static class KeybindingWeight
{
    public const int EditorCore = 0;
    public const int EditorContrib = 100;
    public const int WorkbenchContrib = 200;
    public const int SessionsContrib = 250;
    public const int BuiltinExtension = 300;
    public const int ExternalExtension = 400;
}

/// <summary>A command and its keybindings, registered together.</summary>
internal sealed class CommandAndKeybindingRule : KeybindingRule, ICommand
{
    public required ICommandHandler Handler { get; init; }

    public ICommandMetadata? Metadata { get; init; }
}

/// <summary>Binds <see cref="Command"/> to the keybindings of <see cref="Source"/>, including the user's, where <see cref="When"/> holds.</summary>
/// <param name="Command">The command that is bound.</param>
/// <param name="Source">
/// The command whose keybindings are inherited, without their when clauses, which describe where <paramref name="Source"/>
/// applies. Removing a keybinding of <paramref name="Source"/> also removes the default keybindings of
/// <paramref name="Command"/> at that key, but not those it inherits from the user's keybindings of
/// <paramref name="Source"/>.
/// </param>
/// <param name="When">Where the inherited keybindings apply. There, they outrank those of <paramref name="Source"/>.</param>
internal sealed record KeybindingAlias(string Command, string Source, ContextKeyExpression When);

internal interface IKeybindingsRegistry
{
    IDisposable RegisterKeybindingRule(KeybindingRule rule);

    void SetExtensionKeybindings(IReadOnlyList<ExtensionKeybindingRule> rules);

    IDisposable RegisterCommandAndKeybindingRule(CommandAndKeybindingRule desc);

    IDisposable RegisterKeybindingAlias(KeybindingAlias alias);

    IReadOnlyList<KeybindingItem> GetDefaultKeybindings();

    IReadOnlyList<KeybindingItem> GetDefaultKeybindingsForOS(OperatingSystemKind os);

    IReadOnlyList<KeybindingAlias> GetKeybindingAliases();
}

/// <summary>Stores all built-in and extension-provided keybindings (but not ones that user defines themselves).</summary>
internal sealed class KeybindingsRegistry : IKeybindingsRegistry
{
    private static readonly IComparer<KeybindingItem> s_sorter = Comparer<KeybindingItem>.Create(Sorter);

    private readonly ICommandRegistry _commands;
    private readonly OperatingSystemKind _os;
    private readonly LinkedList<KeybindingItem> _coreKeybindings = new();
    private readonly LinkedList<KeybindingRule> _coreKeybindingRules = new();
    private readonly LinkedList<KeybindingAlias> _keybindingAliases = new();
    private IReadOnlyList<KeybindingItem> _extensionKeybindings = [];
    private List<KeybindingItem>? _cachedMergedKeybindings;

    /// <param name="commands">Where <see cref="RegisterCommandAndKeybindingRule"/> registers the commands.</param>
    /// <param name="os">The platform rules are bound for; the process's when null (upstream's OS).</param>
    public KeybindingsRegistry(ICommandRegistry commands, OperatingSystemKind? os = null)
    {
        _commands = commands;
        _os = os ?? OperatingSystemKinds.OS;
    }

    private static (int Primary, IReadOnlyList<int>? Secondary) BindToPlatform(KeybindingRule kb, OperatingSystemKind os)
    {
        var platform = os switch
        {
            OperatingSystemKind.Windows => kb.Win,
            OperatingSystemKind.Macintosh => kb.Mac,
            _ => kb.Linux,
        };
        if (platform is not null)
        {
            return (platform.Primary, platform.Secondary);
        }

        return (kb.Primary, kb.Secondary);
    }

    public IDisposable RegisterKeybindingRule(KeybindingRule rule)
    {
        var actualKb = BindToPlatform(rule, _os);
        var result = new List<IDisposable>();

        if (actualKb.Primary != 0)
        {
            var kk = Keybindings.DecodeKeybinding(actualKb.Primary, _os);
            if (kk is not null)
            {
                result.Add(RegisterDefaultKeybinding(kk, rule.Id, rule.Args, rule.Weight, 0, rule.When, rule.SystemWide));
            }
        }

        if (actualKb.Secondary is { } secondary)
        {
            for (var i = 0; i < secondary.Count; i++)
            {
                var kk = Keybindings.DecodeKeybinding(secondary[i], _os);
                if (kk is not null)
                {
                    result.Add(RegisterDefaultKeybinding(kk, rule.Id, rule.Args, rule.Weight, -i - 1, rule.When, rule.SystemWide));
                }
            }
        }

        var ruleNode = _coreKeybindingRules.AddLast(rule);
        result.Add(RegistrationDisposable.ToDisposable(() => RemoveNode(ruleNode)));

        return RegistrationDisposable.Combined([.. result]);
    }

    public void SetExtensionKeybindings(IReadOnlyList<ExtensionKeybindingRule> rules)
    {
        var result = new List<KeybindingItem>();
        foreach (var rule in rules)
        {
            if (rule.Keybinding is not null)
            {
                result.Add(new KeybindingItem
                {
                    Keybinding = rule.Keybinding,
                    Command = rule.Id,
                    CommandArgs = rule.Args,
                    When = rule.When,
                    Weight1 = rule.Weight,
                    Weight2 = 0,
                    ExtensionId = string.IsNullOrEmpty(rule.ExtensionId) ? null : rule.ExtensionId,
                    IsBuiltinExtension = rule.IsBuiltinExtension,
                });
            }
        }

        _extensionKeybindings = result;
        _cachedMergedKeybindings = null;
    }

    public IDisposable RegisterCommandAndKeybindingRule(CommandAndKeybindingRule desc)
    {
        return RegistrationDisposable.Combined(
            RegisterKeybindingRule(desc),
            _commands.RegisterCommand(desc));
    }

    public IDisposable RegisterKeybindingAlias(KeybindingAlias alias)
    {
        var node = _keybindingAliases.AddLast(alias);
        return RegistrationDisposable.ToDisposable(() => RemoveNode(node));
    }

    public IReadOnlyList<KeybindingAlias> GetKeybindingAliases() => [.. _keybindingAliases];

    private IDisposable RegisterDefaultKeybinding(
        Keybinding keybinding,
        string commandId,
        object? commandArgs,
        int weight1,
        int weight2,
        ContextKeyExpression? when,
        bool systemWide)
    {
        var node = _coreKeybindings.AddLast(new KeybindingItem
        {
            Keybinding = keybinding,
            Command = commandId,
            CommandArgs = commandArgs,
            When = when,
            Weight1 = weight1,
            Weight2 = weight2,
            ExtensionId = null,
            IsBuiltinExtension = false,
            SystemWide = systemWide,
        });
        _cachedMergedKeybindings = null;

        return RegistrationDisposable.ToDisposable(() =>
        {
            RemoveNode(node);
            _cachedMergedKeybindings = null;
        });
    }

    public IReadOnlyList<KeybindingItem> GetDefaultKeybindings()
    {
        _cachedMergedKeybindings ??= _coreKeybindings.Concat(_extensionKeybindings).Order(s_sorter).ToList();
        return [.. _cachedMergedKeybindings];
    }

    public IReadOnlyList<KeybindingItem> GetDefaultKeybindingsForOS(OperatingSystemKind os)
    {
        var result = new List<KeybindingItem>();
        foreach (var rule in _coreKeybindingRules)
        {
            var actualKb = BindToPlatform(rule, os);

            if (actualKb.Primary != 0)
            {
                var kk = Keybindings.DecodeKeybinding(actualKb.Primary, os);
                if (kk is not null)
                {
                    result.Add(new KeybindingItem
                    {
                        Keybinding = kk,
                        Command = rule.Id,
                        CommandArgs = rule.Args,
                        When = rule.When,
                        Weight1 = rule.Weight,
                        Weight2 = 0,
                        ExtensionId = null,
                        IsBuiltinExtension = false,
                    });
                }
            }

            if (actualKb.Secondary is { } secondary)
            {
                for (var i = 0; i < secondary.Count; i++)
                {
                    var kk = Keybindings.DecodeKeybinding(secondary[i], os);
                    if (kk is not null)
                    {
                        result.Add(new KeybindingItem
                        {
                            Keybinding = kk,
                            Command = rule.Id,
                            CommandArgs = rule.Args,
                            When = rule.When,
                            Weight1 = rule.Weight,
                            Weight2 = -i - 1,
                            ExtensionId = null,
                            IsBuiltinExtension = false,
                        });
                    }
                }
            }
        }

        return result.Order(s_sorter).ToList();
    }

    private static void RemoveNode<T>(LinkedListNode<T> node)
    {
        node.List?.Remove(node);
    }

    private static int Sorter(KeybindingItem? a, KeybindingItem? b)
    {
        if (a is null || b is null)
        {
            return (a is null ? 0 : 1) - (b is null ? 0 : 1);
        }

        if (a.Weight1 != b.Weight1)
        {
            return a.Weight1.CompareTo(b.Weight1);
        }

        if (!string.IsNullOrEmpty(a.Command) && !string.IsNullOrEmpty(b.Command))
        {
            var c = string.CompareOrdinal(a.Command, b.Command);
            if (c != 0)
            {
                return c < 0 ? -1 : 1;
            }
        }

        return a.Weight2.CompareTo(b.Weight2);
    }
}
