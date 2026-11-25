// Ported from vscode (src/vs/editor/contrib/codeAction/common/types.ts and the ManagedCodeActionSet of
// src/vs/editor/contrib/codeAction/browser/codeAction.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - CodeActionKind is a static class of HierarchicalKind values;
//  - CodeActionCommandArgs.FromUser reads a CodeActionUserArgs record or a dictionary with the keys kind, apply and
//    preferred (upstream: any object from a keybinding's args);
//  - CodeActionItem.Action is replaced by a copy with the resolved edit (upstream assigns action.edit: the provider's
//    CodeAction record is immutable here);
//  - CodeActionSet is one class for upstream's CodeActionSet interface, ManagedCodeActionSet and the empty set; its
//    sort is stable (Array.prototype.sort is stable in every engine upstream runs on), and it disposes the provider
//    lists it was made from;
//  - the documentation commands of providers (CodeActionProvider.documentation) are not ported: Documentation is empty.

using TKCode.Arc.Editor.Languages;

namespace TKCode.Arc.Editor.Contrib.CodeAction;

/// <summary>The well-known code action kinds (upstream's CodeActionKind).</summary>
internal static class CodeActionKind
{
    public static readonly HierarchicalKind QuickFix = new("quickfix");
    public static readonly HierarchicalKind Refactor = new("refactor");
    public static readonly HierarchicalKind RefactorExtract = Refactor.Append("extract");
    public static readonly HierarchicalKind RefactorInline = Refactor.Append("inline");
    public static readonly HierarchicalKind RefactorMove = Refactor.Append("move");
    public static readonly HierarchicalKind RefactorRewrite = Refactor.Append("rewrite");
    public static readonly HierarchicalKind Notebook = new("notebook");
    public static readonly HierarchicalKind Source = new("source");
    public static readonly HierarchicalKind SourceOrganizeImports = Source.Append("organizeImports");
    public static readonly HierarchicalKind SourceFixAll = Source.Append("fixAll");
    public static readonly HierarchicalKind SurroundWith = Refactor.Append("surround");
}

/// <summary>Whether a triggered request applies an action without showing the menu (upstream's CodeActionAutoApply).</summary>
internal enum CodeActionAutoApply
{
    /// <summary>Apply the action when it is the only one.</summary>
    IfSingle,

    /// <summary>Apply the first action.</summary>
    First,

    /// <summary>Always show the menu.</summary>
    Never,
}

/// <summary>What asked for code actions (upstream's CodeActionTriggerSource).</summary>
internal enum CodeActionTriggerSource
{
    Refactor,
    RefactorPreview,
    Lightbulb,
    Default,
    SourceAction,
    QuickFix,
    FixAll,
    OrganizeImports,
    AutoFix,
    QuickFixHover,
    OnSave,
    ProblemsView,
}

/// <summary>Which code actions a request wants (upstream's CodeActionFilter).</summary>
internal sealed record CodeActionFilter
{
    /// <summary>Only actions of this kind (or its sub-kinds).</summary>
    public HierarchicalKind? Include { get; init; }

    /// <summary>No actions of these kinds, unless <see cref="Include"/> is more specific.</summary>
    public IReadOnlyList<HierarchicalKind>? Excludes { get; init; }

    /// <summary>Whether source actions (organize imports, fix all) are wanted.</summary>
    public bool IncludeSourceActions { get; init; }

    /// <summary>Only preferred actions.</summary>
    public bool OnlyIncludePreferredActions { get; init; }
}

/// <summary>Where and why code actions were asked for (upstream's CodeActionTrigger).</summary>
internal sealed class CodeActionTrigger(CodeActionTriggerType type, CodeActionTriggerSource triggerAction)
{
    public CodeActionTriggerType Type { get; } = type;
    public CodeActionTriggerSource TriggerAction { get; } = triggerAction;

    public CodeActionFilter? Filter { get; init; }

    public CodeActionAutoApply? AutoApply { get; init; }

    /// <summary>What to tell the user when nothing applies, and where (upstream's context).</summary>
    public CodeActionTriggerContext? Context { get; init; }
}

/// <summary>The message shown when a manual request finds nothing, and where.</summary>
internal sealed record CodeActionTriggerContext(string NotAvailableMessage, TextPosition Position);

/// <summary>The arguments of a code action command as a keybinding or caller gives them.</summary>
internal sealed record CodeActionUserArgs(string? Kind, string? Apply = null, bool? Preferred = null);

/// <summary>The checks of a filter (upstream's mayIncludeActionsOfKind and filtersAction in types.ts).</summary>
internal static class CodeActionFilters
{
    /// <summary>Whether a provider that says it provides <paramref name="providedKind"/> may return actions the filter wants.</summary>
    public static bool MayIncludeActionsOfKind(CodeActionFilter filter, HierarchicalKind providedKind)
    {
        // a provided kind may be a subset or superset of the filtered kind
        if (filter.Include is { } include && !include.Intersects(providedKind))
        {
            return false;
        }

        if (filter.Excludes is { } excludes && excludes.Any(exclude => ExcludesAction(providedKind, exclude, filter.Include)))
        {
            return false;
        }

        // don't return source actions unless they are explicitly requested
        if (!filter.IncludeSourceActions && CodeActionKind.Source.Contains(providedKind))
        {
            return false;
        }

        return true;
    }

    /// <summary>Whether the filter keeps <paramref name="action"/>.</summary>
    public static bool FiltersAction(CodeActionFilter filter, Languages.CodeAction action)
    {
        var actionKind = action.Kind is { } kind ? new HierarchicalKind(kind) : null;

        // filter out actions by kind
        if (filter.Include is { } include)
        {
            if (actionKind is null || !include.Contains(actionKind))
            {
                return false;
            }
        }

        if (filter.Excludes is { } excludes && actionKind is not null
            && excludes.Any(exclude => ExcludesAction(actionKind, exclude, filter.Include)))
        {
            return false;
        }

        // don't return source actions unless they are explicitly requested
        if (!filter.IncludeSourceActions && actionKind is not null && CodeActionKind.Source.Contains(actionKind))
        {
            return false;
        }

        if (filter.OnlyIncludePreferredActions && !action.IsPreferred)
        {
            return false;
        }

        return true;
    }

    private static bool ExcludesAction(HierarchicalKind providedKind, HierarchicalKind exclude, HierarchicalKind? include)
    {
        if (!exclude.Contains(providedKind))
        {
            return false;
        }

        // the include is more specific: don't filter out
        if (include is not null && exclude.Contains(include))
        {
            return false;
        }

        return true;
    }
}

/// <summary>A code action command's arguments, checked (upstream's CodeActionCommandArgs).</summary>
internal sealed record CodeActionCommandArgs(HierarchicalKind Kind, CodeActionAutoApply Apply, bool Preferred)
{
    /// <summary>
    /// Reads the arguments a keybinding or caller passed: a <see cref="CodeActionUserArgs"/> or a dictionary with the keys
    /// <c>kind</c>, <c>apply</c> (<c>first</c>, <c>never</c>, <c>ifSingle</c>) and <c>preferred</c>; what is missing comes from the defaults.
    /// </summary>
    public static CodeActionCommandArgs FromUser(object? arg, HierarchicalKind defaultKind, CodeActionAutoApply defaultApply)
    {
        string? kind = null;
        string? apply = null;
        bool? preferred = null;
        switch (arg)
        {
            case CodeActionUserArgs user:
                (kind, apply, preferred) = (user.Kind, user.Apply, user.Preferred);
                break;
            case IReadOnlyDictionary<string, object?> dictionary:
                kind = dictionary.GetValueOrDefault("kind") as string;
                apply = dictionary.GetValueOrDefault("apply") as string;
                preferred = dictionary.GetValueOrDefault("preferred") as bool?;
                break;
            default:
                return new CodeActionCommandArgs(defaultKind, defaultApply, false);
        }

        return new CodeActionCommandArgs(
            kind is not null ? new HierarchicalKind(kind) : defaultKind,
            GetApplyFromUser(apply, defaultApply),
            preferred ?? false);
    }

    /// <summary>The kind a caller named in <paramref name="arg"/>, or null.</summary>
    public static string? GetUserKind(object? arg) => arg switch
    {
        CodeActionUserArgs user => user.Kind,
        IReadOnlyDictionary<string, object?> dictionary => dictionary.GetValueOrDefault("kind") as string,
        _ => null,
    };

    private static CodeActionAutoApply GetApplyFromUser(string? apply, CodeActionAutoApply defaultAutoApply) =>
        apply?.ToLowerInvariant() switch
        {
            "first" => CodeActionAutoApply.First,
            "never" => CodeActionAutoApply.Never,
            "ifsingle" => CodeActionAutoApply.IfSingle,
            _ => defaultAutoApply,
        };
}

/// <summary>A code action and the provider that returned it (upstream's CodeActionItem).</summary>
internal sealed class CodeActionItem
{
    public CodeActionItem(Languages.CodeAction action, ICodeActionProvider? provider, bool highlightRange = false)
    {
        Action = action;
        Provider = provider;
        HighlightRange = highlightRange;
    }

    /// <summary>The action (with its edit once <see cref="Resolve"/> filled it in).</summary>
    public Languages.CodeAction Action { get; private set; }

    public ICodeActionProvider? Provider { get; }

    public bool HighlightRange { get; set; }

    /// <summary>Asks the provider for the action's edit when it has none yet.</summary>
    public async Task<CodeActionItem> Resolve(CancellationToken token)
    {
        if (Provider is { SupportsResolveCodeAction: true } provider && Action.Edit is null)
        {
            Languages.CodeAction? resolved = null;
            try
            {
                resolved = await provider.ResolveCodeAction(Action, token).ConfigureAwait(true);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                UnexpectedErrors.Report(error);
            }

            if (resolved is not null)
            {
                Action = Action with { Edit = resolved.Edit };
            }
        }

        return this;
    }

    /// <summary>Replaces the action (upstream assigns fields of the provider's object, which is immutable here).</summary>
    internal void ReplaceAction(Languages.CodeAction action) => Action = action;

    public override string ToString() => Action.Title;
}

/// <summary>The code actions of one request (upstream's CodeActionSet and ManagedCodeActionSet).</summary>
internal sealed class CodeActionSet : IDisposable
{
    private readonly Action? _dispose;
    private bool _disposed;

    /// <summary>A set of the given lists, in the given order.</summary>
    public CodeActionSet(
        IReadOnlyList<CodeActionItem> validActions,
        IReadOnlyList<CodeActionItem> allActions,
        IReadOnlyList<LanguageCommand> documentation,
        Action? dispose = null)
    {
        ValidActions = validActions;
        AllActions = allActions;
        Documentation = documentation;
        _dispose = dispose;
    }

    /// <summary>The empty set (upstream's emptyCodeActionSet).</summary>
    public static CodeActionSet Empty { get; } = new([], [], []);

    /// <summary>The actions that are not disabled, in menu order.</summary>
    public IReadOnlyList<CodeActionItem> ValidActions { get; }

    /// <summary>All actions, in menu order.</summary>
    public IReadOnlyList<CodeActionItem> AllActions { get; }

    public IReadOnlyList<LanguageCommand> Documentation { get; }

    /// <summary>Whether a preferred quick fix is among the valid actions.</summary>
    public bool HasAutoFix => ValidActions.Any(item =>
        item.Action.Kind is { } kind && CodeActionKind.QuickFix.Contains(new HierarchicalKind(kind)) && item.Action.IsPreferred);

    public bool HasAIFix => ValidActions.Any(item => item.Action.IsAI);

    public bool AllAIFixes => ValidActions.All(item => item.Action.IsAI);

    public bool IsDisposed => _disposed;

    /// <summary>
    /// Upstream's ManagedCodeActionSet: the actions sorted (AI actions last; actions for diagnostics first, preferred ones
    /// first among them; the order of the providers otherwise), the valid ones the actions that are not disabled.
    /// </summary>
    public static CodeActionSet Create(IEnumerable<CodeActionItem> actions, IReadOnlyList<LanguageCommand> documentation, Action? dispose)
    {
        var all = actions.Order(Comparer<CodeActionItem>.Create(CodeActionsComparator)).ToList();
        var valid = all.Where(item => item.Action.Disabled is null).ToList();
        return new CodeActionSet(valid, all, documentation, dispose);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _dispose?.Invoke();
    }

    private static int CodeActionsPreferredComparator(Languages.CodeAction a, Languages.CodeAction b)
    {
        if (a.IsPreferred && !b.IsPreferred)
        {
            return -1;
        }

        if (!a.IsPreferred && b.IsPreferred)
        {
            return 1;
        }

        return 0;
    }

    private static int CodeActionsComparator(CodeActionItem x, CodeActionItem y)
    {
        var a = x.Action;
        var b = y.Action;
        if (a.IsAI && !b.IsAI)
        {
            return 1;
        }

        if (!a.IsAI && b.IsAI)
        {
            return -1;
        }

        if (a.Diagnostics is { Count: > 0 })
        {
            return b.Diagnostics is { Count: > 0 } ? CodeActionsPreferredComparator(a, b) : -1;
        }

        if (b.Diagnostics is { Count: > 0 })
        {
            return 1;
        }

        // both have no diagnostics
        return CodeActionsPreferredComparator(a, b);
    }
}
