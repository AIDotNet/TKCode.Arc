// Ported from vscode (src/vs/editor/contrib/codeAction/browser/codeActionKeybindingResolver.ts, commit b57d5c9e), MIT
// license.
// Differences from upstream: the keybinding service is a function that returns the resolved keybinding items (the
// editor's KeybindingResolver.GetKeybindings); Lazy is System.Lazy.

using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc.Editor.Contrib.CodeAction;

/// <summary>Finds the keybinding that runs a code action, for the menu (upstream's CodeActionKeybindingResolver).</summary>
internal sealed class CodeActionKeybindingResolver
{
    private static readonly string[] s_codeActionCommands =
    [
        CodeActionService.RefactorCommandId,
        CodeActionService.CodeActionCommandId,
        CodeActionService.SourceActionCommandId,
        CodeActionService.OrganizeImportsCommandId,
        CodeActionService.FixAllCommandId,
    ];

    private readonly Func<IReadOnlyList<ResolvedKeybindingItem>> _getKeybindings;

    public CodeActionKeybindingResolver(Func<IReadOnlyList<ResolvedKeybindingItem>> getKeybindings)
    {
        _getKeybindings = getKeybindings;
    }

    /// <summary>A function that gives the keybinding of a code action, or null (upstream's getResolver).</summary>
    public Func<Languages.CodeAction, ResolvedKeybinding?> GetResolver()
    {
        // lazy since the value may never be read
        var allCodeActionBindings = new Lazy<IReadOnlyList<ResolveCodeActionKeybinding>>(() => _getKeybindings()
            .Where(item => item.Command is { } command && Array.IndexOf(s_codeActionCommands, command) >= 0)
            .Where(item => item.ResolvedKeybinding is not null)
            .Select(item =>
            {
                // these commands come built in and don't use their args
                var commandArgs = item.CommandArgs;
                if (item.Command == CodeActionService.OrganizeImportsCommandId)
                {
                    commandArgs = new CodeActionUserArgs(CodeActionKind.SourceOrganizeImports.Value);
                }
                else if (item.Command == CodeActionService.FixAllCommandId)
                {
                    commandArgs = new CodeActionUserArgs(CodeActionKind.SourceFixAll.Value);
                }

                var args = CodeActionCommandArgs.FromUser(commandArgs, HierarchicalKind.None, CodeActionAutoApply.Never);
                return new ResolveCodeActionKeybinding(args.Kind, args.Preferred, item.ResolvedKeybinding!);
            })
            .ToList());

        return action =>
        {
            if (action.Kind is not null)
            {
                return BestKeybindingForCodeAction(action, allCodeActionBindings.Value)?.ResolvedKeybinding;
            }

            return null;
        };
    }

    private static ResolveCodeActionKeybinding? BestKeybindingForCodeAction(
        Languages.CodeAction action,
        IReadOnlyList<ResolveCodeActionKeybinding> candidates)
    {
        if (action.Kind is null)
        {
            return null;
        }

        var kind = new HierarchicalKind(action.Kind);
        var matching = candidates
            .Where(candidate => candidate.Kind.Contains(kind))
            // a keybinding that only applies to preferred actions needs a preferred action
            .Where(candidate => !candidate.Preferred || action.IsPreferred)
            .ToList();

        // reduceRight: from the last to the first, keep the more specific binding
        ResolveCodeActionKeybinding? currentBest = null;
        for (var i = matching.Count - 1; i >= 0; i--)
        {
            var candidate = matching[i];
            if (currentBest is null)
            {
                currentBest = candidate;
                continue;
            }

            currentBest = currentBest.Kind.Contains(candidate.Kind) ? candidate : currentBest;
        }

        return currentBest;
    }

    private sealed record ResolveCodeActionKeybinding(HierarchicalKind Kind, bool Preferred, ResolvedKeybinding ResolvedKeybinding);
}
